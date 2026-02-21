using System.Reflection;
using Vido.Core.Events;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Menus;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Vido.Core.Settings;

namespace Vido.PluginHost;

/// <summary>
/// Manages the full plugin lifecycle: discovery, validation, loading, activation,
/// deactivation, and error handling. Implements <see cref="IPluginHost"/>.
/// </summary>
public sealed class PluginHost : IPluginHost
{
    private readonly IEventBus _eventBus;
    private readonly IVideoEngine _videoEngine;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly ContributionRegistry _contributions;
    private readonly IContextMenuRegistry _contextMenuRegistry;
    private readonly IKeyboardShortcutService _keyboardShortcutService;

    private readonly List<PluginInfo> _plugins = [];
    private readonly Dictionary<string, PluginContext> _contexts = [];

    /// <summary>List of directories to scan for plugins.</summary>
    private readonly List<string> _scanDirectories = [];

    /// <summary>
    /// Default plugin directory: <c>%APPDATA%/Vido/plugins/</c>.
    /// Always scanned regardless of custom directories.
    /// </summary>
    public static string DefaultPluginDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Vido", "plugins");
        }
    }

    public IReadOnlyList<PluginInfo> Plugins => _plugins.AsReadOnly();

    /// <summary>
    /// The contribution registry containing all registered plugin UI contributions.
    /// </summary>
    public IContributionRegistry ContributionRegistry => _contributions;

    public PluginHost(
        IEventBus eventBus,
        IVideoEngine videoEngine,
        ILogService logService,
        ISettingsService settingsService,
        ContributionRegistry contributions,
        IContextMenuRegistry contextMenuRegistry,
        IKeyboardShortcutService keyboardShortcutService)
    {
        _eventBus = eventBus;
        _videoEngine = videoEngine;
        _logService = logService;
        _settingsService = settingsService;
        _contributions = contributions;
        _contextMenuRegistry = contextMenuRegistry;
        _keyboardShortcutService = keyboardShortcutService;

        // Always scan the default directory
        _scanDirectories.Add(DefaultPluginDirectory);

        // Add custom directories from settings
        var customDirs = settingsService.Current.PluginDirectories;
        if (customDirs is not null)
        {
            foreach (var dir in customDirs)
            {
                if (!string.IsNullOrWhiteSpace(dir))
                    _scanDirectories.Add(dir);
            }
        }
    }

    /// <summary>
    /// Discovers and loads all plugins from configured directories,
    /// then activates all that are not disabled.
    /// </summary>
    public void ActivateAll()
    {
        _logService.Info("Plugin system starting — scanning for plugins...", "PluginHost");

        DiscoverPlugins();

        var disabledIds = GetDisabledPluginIds();

        foreach (var info in _plugins)
        {
            if (info.State == PluginState.Error || info.State == PluginState.Disabled)
                continue;

            if (disabledIds.Contains(info.Manifest.Id))
            {
                info.State = PluginState.Disabled;
                _logService.Info($"Plugin '{info.Manifest.Id}' is disabled", "PluginHost");
                continue;
            }

            ActivatePlugin(info);
        }

        _logService.Info(
            $"Plugin system ready: {_plugins.Count(p => p.State == PluginState.Active)} active, " +
            $"{_plugins.Count(p => p.State == PluginState.Disabled)} disabled, " +
            $"{_plugins.Count(p => p.State == PluginState.Error)} errors",
            "PluginHost");
    }

    /// <summary>
    /// Deactivates all active plugins in reverse activation order.
    /// </summary>
    public void DeactivateAll()
    {
        _logService.Info("Deactivating all plugins...", "PluginHost");

        // Deactivate in reverse order to handle dependencies gracefully
        for (int i = _plugins.Count - 1; i >= 0; i--)
        {
            var info = _plugins[i];
            if (info.State == PluginState.Active)
                DeactivatePlugin(info);
        }

        _logService.Info("All plugins deactivated", "PluginHost");
    }

    public PluginInfo? GetPlugin(string pluginId) =>
        _plugins.FirstOrDefault(p => p.Manifest.Id == pluginId);

    public void SetEnabled(string pluginId, bool enabled)
    {
        var info = GetPlugin(pluginId);
        if (info is null)
        {
            _logService.Warning($"Cannot set enabled state — plugin '{pluginId}' not found", "PluginHost");
            return;
        }

        var disabledIds = _settingsService.Current.DisabledPluginIds;

        if (enabled)
        {
            disabledIds.Remove(pluginId);

            if (info.State == PluginState.Disabled)
            {
                info.State = PluginState.Loaded;
                ActivatePlugin(info);
            }
        }
        else
        {
            if (!disabledIds.Contains(pluginId))
                disabledIds.Add(pluginId);

            if (info.State == PluginState.Active)
                DeactivatePlugin(info);

            info.State = PluginState.Disabled;
        }

        _settingsService.QueueSave();
    }

    public IReadOnlyList<string> GetDisabledPluginIds() =>
        _settingsService.Current.DisabledPluginIds.ToList().AsReadOnly();

    // ── Discovery ──

    private void DiscoverPlugins()
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanDir in _scanDirectories)
        {
            if (!System.IO.Directory.Exists(scanDir))
            {
                _logService.Debug($"Plugin scan directory does not exist: '{scanDir}'", "PluginHost");
                continue;
            }

            _logService.Debug($"Scanning for plugins in '{scanDir}'", "PluginHost");

            foreach (var pluginDir in System.IO.Directory.GetDirectories(scanDir))
            {
                var manifest = PluginManifestLoader.Load(pluginDir, _logService);
                if (manifest is null) continue;

                if (!seenIds.Add(manifest.Id))
                {
                    _logService.Warning(
                        $"Duplicate plugin id '{manifest.Id}' found in '{pluginDir}' — skipping",
                        "PluginHost");
                    continue;
                }

                var info = new PluginInfo
                {
                    Manifest = manifest,
                    Directory = pluginDir,
                    State = PluginState.Validated
                };

                // Try to load the assembly and find the plugin class
                if (LoadPluginAssembly(info))
                {
                    info.State = PluginState.Loaded;
                }

                _plugins.Add(info);
            }
        }
    }

    /// <summary>
    /// Loads the plugin's entry-point assembly and instantiates the <see cref="IVidoPlugin"/> class.
    /// Returns true on success, false on failure (info.State set to Error).
    /// </summary>
    private bool LoadPluginAssembly(PluginInfo info)
    {
        var dllPath = Path.Combine(info.Directory, info.Manifest.EntryPoint);

        if (!File.Exists(dllPath))
        {
            info.State = PluginState.Error;
            info.ErrorMessage = $"Entry point DLL not found: '{info.Manifest.EntryPoint}'";
            _logService.Error($"Plugin '{info.Manifest.Id}': {info.ErrorMessage}", "PluginHost");
            return false;
        }

        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var pluginType = assembly.GetType(info.Manifest.PluginClass);

            if (pluginType is null)
            {
                info.State = PluginState.Error;
                info.ErrorMessage = $"Plugin class '{info.Manifest.PluginClass}' not found in assembly";
                _logService.Error($"Plugin '{info.Manifest.Id}': {info.ErrorMessage}", "PluginHost");
                return false;
            }

            if (!typeof(IVidoPlugin).IsAssignableFrom(pluginType))
            {
                info.State = PluginState.Error;
                info.ErrorMessage = $"Class '{info.Manifest.PluginClass}' does not implement IVidoPlugin";
                _logService.Error($"Plugin '{info.Manifest.Id}': {info.ErrorMessage}", "PluginHost");
                return false;
            }

            var instance = Activator.CreateInstance(pluginType) as IVidoPlugin;
            if (instance is null)
            {
                info.State = PluginState.Error;
                info.ErrorMessage = $"Failed to create instance of '{info.Manifest.PluginClass}'";
                _logService.Error($"Plugin '{info.Manifest.Id}': {info.ErrorMessage}", "PluginHost");
                return false;
            }

            info.Instance = instance;
            return true;
        }
        catch (Exception ex)
        {
            info.State = PluginState.Error;
            info.ErrorMessage = $"Failed to load assembly: {ex.Message}";
            _logService.Error($"Plugin '{info.Manifest.Id}': {info.ErrorMessage}", "PluginHost");
            return false;
        }
    }

    // ── Activation / Deactivation ──

    private void ActivatePlugin(PluginInfo info)
    {
        if (info.Instance is null)
        {
            info.State = PluginState.Error;
            info.ErrorMessage = "No plugin instance available for activation";
            return;
        }

        try
        {
            var settingsStore = new PluginSettingsStore(info.Manifest.Id);
            var context = new PluginContext(
                info.Manifest,
                info.Directory,
                _eventBus,
                _videoEngine,
                _logService,
                settingsStore,
                _contributions,
                _contextMenuRegistry,
                _keyboardShortcutService);

            _contexts[info.Manifest.Id] = context;

            info.Instance.Activate(context);
            info.State = PluginState.Active;

            _logService.Info($"Plugin '{info.Manifest.DisplayName ?? info.Manifest.Name}' v{info.Manifest.Version} activated", "PluginHost");
        }
        catch (Exception ex)
        {
            info.State = PluginState.Error;
            info.ErrorMessage = $"Activation failed: {ex.Message}";
            _logService.Error($"Plugin '{info.Manifest.Id}' activation failed: {ex.Message}", "PluginHost");

            // Clean up any partial registrations
            if (_contexts.TryGetValue(info.Manifest.Id, out var ctx))
            {
                try { ctx.Cleanup(); } catch { /* swallow cleanup errors */ }
                _contexts.Remove(info.Manifest.Id);
            }
        }
    }

    private void DeactivatePlugin(PluginInfo info)
    {
        try
        {
            info.Instance?.Deactivate();
        }
        catch (Exception ex)
        {
            _logService.Warning($"Plugin '{info.Manifest.Id}' threw during deactivation: {ex.Message}", "PluginHost");
        }

        // Clean up contributions regardless of whether deactivation threw
        if (_contexts.TryGetValue(info.Manifest.Id, out var ctx))
        {
            try { ctx.Cleanup(); } catch { /* swallow */ }
            _contexts.Remove(info.Manifest.Id);
        }

        info.State = PluginState.Deactivated;
        _logService.Info($"Plugin '{info.Manifest.Id}' deactivated", "PluginHost");
    }
}
