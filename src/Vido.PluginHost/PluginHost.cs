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
    private readonly Dictionary<string, PluginSettingsStore> _settingsStores = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>List of directories to scan for plugins.</summary>
    private readonly List<string> _scanDirectories = [];

    /// <summary>
    /// Default plugin directory: <c>%APPDATA%/Vido/plugins/</c>.
    /// Always scanned regardless of custom directories.
    /// </summary>
    public static string DefaultPluginDirectory => PluginPaths.DefaultPluginDirectory;

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
        IKeyboardShortcutService keyboardShortcutService,
        bool scanDefaultDirectory = true)
    {
        _eventBus = eventBus;
        _videoEngine = videoEngine;
        _logService = logService;
        _settingsService = settingsService;
        _contributions = contributions;
        _contextMenuRegistry = contextMenuRegistry;
        _keyboardShortcutService = keyboardShortcutService;

        // Always scan the default directory (unless disabled for testing)
        if (scanDefaultDirectory)
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
    /// then activates all that are not disabled, in dependency order.
    /// </summary>
    public void ActivateAll()
    {
        _logService.Info("Plugin system starting — scanning for plugins...", "PluginHost");

        DiscoverPlugins();

        var disabledIds = new HashSet<string>(
            _settingsService.Current.DisabledPluginIds, StringComparer.OrdinalIgnoreCase);

        // Mark disabled plugins before dependency resolution
        foreach (var info in _plugins)
        {
            if (info.State == PluginState.Error || info.State == PluginState.Active)
                continue;

            if (disabledIds.Contains(info.Manifest.Id))
            {
                info.State = PluginState.Disabled;
                _logService.Info($"Plugin '{info.Manifest.Id}' is disabled", "PluginHost");
            }
        }

        // Topological sort — dependencies activated before dependants
        var activationOrder = TopologicalSort(_plugins);

        // Validate dependencies and activate in order
        foreach (var info in activationOrder)
        {
            if (info.State == PluginState.Error || info.State == PluginState.Disabled
                || info.State == PluginState.Active)
                continue;

            // Validate all declared dependencies before activation
            var depError = ValidateDependencies(info);
            if (depError is not null)
            {
                info.State = PluginState.Error;
                info.ErrorMessage = depError;
                _logService.Error($"Plugin '{info.Manifest.Id}': {depError}", "PluginHost");
                continue;
            }

            ActivatePlugin(info);
        }

        // Prune orphaned entries from the disabled list (IDs that don't
        // correspond to any discovered plugin). Keeps the settings tidy.
        PruneOrphanedDisabledIds();

        _logService.Info(
            $"Plugin system ready: {_plugins.Count(p => p.State == PluginState.Active)} active, " +
            $"{_plugins.Count(p => p.State == PluginState.Disabled)} disabled, " +
            $"{_plugins.Count(p => p.State == PluginState.Error)} errors",
            "PluginHost");
    }

    /// <summary>
    /// Deactivates all active plugins in reverse topological order
    /// (dependants are deactivated before their dependencies).
    /// </summary>
    public void DeactivateAll()
    {
        _logService.Info("Deactivating all plugins...", "PluginHost");

        // Reverse topological order: dependants deactivated before dependencies
        var deactivationOrder = TopologicalSort(_plugins);
        deactivationOrder.Reverse();

        foreach (var info in deactivationOrder)
        {
            if (info.State == PluginState.Active)
                DeactivatePlugin(info);
        }

        _logService.Info("All plugins deactivated", "PluginHost");
    }

    public PluginInfo? GetPlugin(string pluginId) =>
        _plugins.FirstOrDefault(p =>
            string.Equals(p.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase));

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
            disabledIds.RemoveAll(id =>
                string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));

            if (info.State == PluginState.Disabled)
            {
                info.State = PluginState.Loaded;
                ActivatePlugin(info);
            }
        }
        else
        {
            if (!disabledIds.Any(id =>
                    string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase)))
                disabledIds.Add(pluginId);

            if (info.State == PluginState.Active)
                DeactivatePlugin(info);

            info.State = PluginState.Disabled;
        }

        _settingsService.QueueSave();
    }

    public IReadOnlyList<string> GetDisabledPluginIds() =>
        _settingsService.Current.DisabledPluginIds.ToList().AsReadOnly();

    /// <summary>
    /// Removes entries from <see cref="AppSettings.DisabledPluginIds"/> that
    /// don't match any discovered plugin. This cleans up stale IDs left by
    /// renamed, removed, or manually-edited plugins.
    /// </summary>
    private void PruneOrphanedDisabledIds()
    {
        var knownIds = new HashSet<string>(
            _plugins.Select(p => p.Manifest.Id), StringComparer.OrdinalIgnoreCase);
        var disabledIds = _settingsService.Current.DisabledPluginIds;
        var removed = disabledIds.RemoveAll(id => !knownIds.Contains(id));
        if (removed > 0)
        {
            _logService.Debug(
                $"Pruned {removed} orphaned disabled plugin ID(s)", "PluginHost");
            _settingsService.QueueSave();
        }
    }

    // ── Discovery ──

    private void DiscoverPlugins()
    {
        // Seed seenIds with already-discovered plugins to prevent duplicates
        // when ActivateAll is called multiple times (e.g. after install).
        var seenIds = new HashSet<string>(
            _plugins.Select(p => p.Manifest.Id),
            StringComparer.OrdinalIgnoreCase);

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
            // Load the assembly from a byte array so neither the original file
            // nor any shadow-copy is locked by the process. This allows clean
            // uninstall and reinstall at any time.
            var assemblyBytes = File.ReadAllBytes(dllPath);

            // Also load a PDB if present (enables stack-trace line numbers)
            var pdbPath = Path.ChangeExtension(dllPath, ".pdb");
            var pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;

            var assembly = pdbBytes is not null
                ? Assembly.Load(assemblyBytes, pdbBytes)
                : Assembly.Load(assemblyBytes);
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
            _settingsStores[info.Manifest.Id] = settingsStore;

            // Apply forceOverride settings — developer-specified values that always win
            ApplyForceOverrides(info.Manifest, settingsStore);

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
                try { ctx.Cleanup(); } catch (Exception cleanupEx) { _logService.Debug($"Cleanup after activation failure threw: {cleanupEx.Message}", "PluginHost"); }
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
            try { ctx.Cleanup(); } catch (Exception cleanupEx) { _logService.Debug($"Cleanup during deactivation threw: {cleanupEx.Message}", "PluginHost"); }
            _contexts.Remove(info.Manifest.Id);
        }

        info.State = PluginState.Deactivated;
        _logService.Info($"Plugin '{info.Manifest.Id}' deactivated", "PluginHost");
    }

    // ── Settings Store Access ──

    /// <inheritdoc/>
    public IPluginSettingsStore GetSettingsStore(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_settingsStores.TryGetValue(pluginId, out var existing))
            return existing;

        // Create a new store for plugins that are installed but not yet activated
        var store = new PluginSettingsStore(pluginId);
        _settingsStores[pluginId] = store;
        return store;
    }

    /// <inheritdoc/>
    public void RemovePlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        // Remove from the discovered plugins list
        var info = _plugins.FirstOrDefault(p =>
            string.Equals(p.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase));

        if (info is not null)
        {
            // Deactivate if still active
            if (info.State == PluginState.Active)
                DeactivatePlugin(info);

            _plugins.Remove(info);
        }

        // Remove context and settings store
        if (_contexts.TryGetValue(pluginId, out var ctx))
        {
            try { ctx.Cleanup(); } catch (Exception cleanupEx) { _logService.Debug($"Cleanup during removal threw: {cleanupEx.Message}", "PluginHost"); }
            _contexts.Remove(pluginId);
        }

        _settingsStores.Remove(pluginId);

        // Remove from disabled list so reinstallation starts fresh
        _settingsService.Current.DisabledPluginIds.RemoveAll(id =>
            string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));
        _settingsService.QueueSave();

        _logService.Debug($"Plugin '{pluginId}' removed from runtime state", "PluginHost");
    }

    // ── Force Override ──

    /// <summary>
    /// For settings marked <c>forceOverride: true</c> in the manifest,
    /// overwrite the stored value with the developer-specified default on every activation.
    /// This lets plugin authors enforce specific values (e.g. debug flags during beta).
    /// </summary>
    private void ApplyForceOverrides(PluginManifest manifest, PluginSettingsStore settingsStore)
    {
        if (manifest.Contributes.Settings is not { Count: > 0 })
            return;

        foreach (var setting in manifest.Contributes.Settings)
        {
            if (!setting.ForceOverride || setting.Default is null)
                continue;

            settingsStore.Set(setting.Id, setting.Default);
            _logService.Debug(
                $"Plugin '{manifest.Id}': forceOverride applied for setting '{setting.Id}'",
                "PluginHost");
        }
    }

    // ── Dependency Resolution ──

    /// <summary>
    /// Validates that all declared dependencies for a plugin are present, enabled/active,
    /// and meet the minimum version requirement. Returns null on success or an error message.
    /// </summary>
    private string? ValidateDependencies(PluginInfo info)
    {
        if (info.Manifest.Dependencies is not { Count: > 0 })
            return null;

        foreach (var dep in info.Manifest.Dependencies)
        {
            var depPlugin = _plugins.FirstOrDefault(p =>
                string.Equals(p.Manifest.Id, dep.Id, StringComparison.OrdinalIgnoreCase));

            if (depPlugin is null)
                return $"Missing dependency: {dep.Id} ≥{dep.MinVersion}";

            if (depPlugin.State == PluginState.Disabled)
                return $"Dependency '{dep.Id}' is disabled — enable it first";

            if (depPlugin.State == PluginState.Error)
                return $"Dependency '{dep.Id}' is in error state";

            if (depPlugin.State != PluginState.Active && depPlugin.State != PluginState.Loaded)
                return $"Dependency '{dep.Id}' is not available (state: {depPlugin.State})";

            // Version check
            if (!string.IsNullOrWhiteSpace(dep.MinVersion)
                && Version.TryParse(dep.MinVersion, out var minVer)
                && Version.TryParse(depPlugin.Manifest.Version, out var actualVer)
                && actualVer < minVer)
            {
                return $"Dependency '{dep.Id}' version {depPlugin.Manifest.Version} " +
                       $"does not meet minimum required version {dep.MinVersion}";
            }
        }

        return null;
    }

    /// <summary>
    /// Performs a topological sort of plugins using Kahn's algorithm (BFS).
    /// Dependencies are placed before the plugins that depend on them.
    /// If a cycle is detected, the remaining plugins are appended in discovery order
    /// and will fail during dependency validation.
    /// </summary>
    internal static List<PluginInfo> TopologicalSort(List<PluginInfo> plugins)
    {
        if (plugins.Count <= 1)
            return new List<PluginInfo>(plugins);

        // Build adjacency list and in-degree map (case-insensitive IDs)
        var idLookup = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in plugins)
        {
            // Use first-seen for duplicate IDs (shouldn't happen, but be safe)
            idLookup.TryAdd(p.Manifest.Id, p);
        }

        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dependants = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in plugins)
        {
            inDegree.TryAdd(p.Manifest.Id, 0);
            dependants.TryAdd(p.Manifest.Id, []);
        }

        foreach (var p in plugins)
        {
            if (p.Manifest.Dependencies is not { Count: > 0 })
                continue;

            foreach (var dep in p.Manifest.Dependencies)
            {
                // Only count edges to known plugins (unknown deps will fail validation later)
                if (!idLookup.ContainsKey(dep.Id))
                    continue;

                inDegree[p.Manifest.Id]++;

                if (!dependants.ContainsKey(dep.Id))
                    dependants[dep.Id] = [];
                dependants[dep.Id].Add(p.Manifest.Id);
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>();
        foreach (var (id, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(id);
        }

        var sorted = new List<PluginInfo>(plugins.Count);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (idLookup.TryGetValue(id, out var info))
                sorted.Add(info);

            if (dependants.TryGetValue(id, out var deps))
            {
                foreach (var depId in deps)
                {
                    inDegree[depId]--;
                    if (inDegree[depId] == 0)
                        queue.Enqueue(depId);
                }
            }
        }

        // If cycle detected, append remaining plugins (they'll fail validation)
        if (sorted.Count < plugins.Count)
        {
            var sortedIds = new HashSet<string>(
                sorted.Select(p => p.Manifest.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var p in plugins)
            {
                if (!sortedIds.Contains(p.Manifest.Id))
                    sorted.Add(p);
            }
        }

        return sorted;
    }
}
