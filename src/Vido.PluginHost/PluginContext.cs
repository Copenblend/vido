using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Menus;
using Vido.Core.Playback;
using Vido.Core.Plugin;

namespace Vido.PluginHost;

/// <summary>
/// Concrete implementation of <see cref="IPluginContext"/> passed to each plugin
/// during activation. Provides access to all Vido services and UI registration methods.
/// Thread-safe: delegates to thread-safe registries.
/// </summary>
public sealed class PluginContext : IPluginContext
{
    private readonly ContributionRegistry _contributions;
    private readonly IContextMenuRegistry _contextMenuRegistry;
    private readonly IKeyboardShortcutService _keyboardShortcutService;
    private readonly List<string> _registeredContextMenuIds = [];
    private readonly List<string> _registeredKeyBindingIds = [];

    public PluginManifest Manifest { get; }
    public string PluginDirectory { get; }
    public IEventBus Events { get; }
    public IVideoEngine VideoEngine { get; }
    public ILogService Logger { get; }
    public IPluginSettingsStore Settings { get; }

    public PluginContext(
        PluginManifest manifest,
        string pluginDirectory,
        IEventBus eventBus,
        IVideoEngine videoEngine,
        ILogService logService,
        IPluginSettingsStore settingsStore,
        ContributionRegistry contributions,
        IContextMenuRegistry contextMenuRegistry,
        IKeyboardShortcutService keyboardShortcutService)
    {
        Manifest = manifest;
        PluginDirectory = pluginDirectory;
        Events = eventBus;
        VideoEngine = videoEngine;
        Logger = logService;
        Settings = settingsStore;
        _contributions = contributions;
        _contextMenuRegistry = contextMenuRegistry;
        _keyboardShortcutService = keyboardShortcutService;
    }

    public void RegisterSidebarPanel(string contributionId, Func<object> viewFactory)
    {
        var contrib = FindSidebarContribution(contributionId);
        var iconPath = ResolveIconPath(contrib?.Icon);
        _contributions.RegisterSidebarPanel(
            Manifest.Id, contributionId,
            contrib?.Title ?? contributionId,
            iconPath,
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered sidebar panel '{contributionId}'", "PluginHost");
    }

    public void RegisterBottomPanel(string contributionId, Func<object> viewFactory)
    {
        var contrib = FindBottomPanelContribution(contributionId);
        _contributions.RegisterBottomPanel(
            Manifest.Id, contributionId,
            contrib?.Title ?? contributionId,
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered bottom panel '{contributionId}'", "PluginHost");
    }

    public void RegisterRightPanel(string contributionId, Func<object> viewFactory)
    {
        var contrib = FindRightPanelContribution(contributionId);
        _contributions.RegisterRightPanel(
            Manifest.Id, contributionId,
            contrib?.Title ?? contributionId,
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered right panel '{contributionId}'", "PluginHost");
    }

    public void RegisterStatusBarItem(string contributionId, Func<object> viewFactory)
    {
        var contrib = FindStatusBarContribution(contributionId);
        _contributions.RegisterStatusBarItem(
            Manifest.Id, contributionId,
            contrib?.Position ?? "right",
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered status bar item '{contributionId}'", "PluginHost");
    }

    public void RegisterToolbarButtonHandler(string contributionId, Action clickHandler)
    {
        var contrib = FindToolbarButtonContribution(contributionId);
        var iconPath = ResolveIconPath(contrib?.Icon);
        _contributions.RegisterToolbarButton(
            Manifest.Id, contributionId,
            contrib?.Tooltip ?? contributionId,
            iconPath,
            contrib?.Order ?? 100,
            clickHandler);
        Logger.Debug($"Plugin '{Manifest.Id}' registered toolbar button '{contributionId}'", "PluginHost");
    }

    public void RegisterContextMenuHandler(string contributionId, Action<FileNode> handler)
    {
        var contrib = FindContextMenuContribution(contributionId);
        var label = contrib?.Label ?? contributionId;
        var extensions = contrib?.FileExtensions?.ToArray() ?? [];
        var order = contrib?.Order ?? 100;

        // Register in the ContributionRegistry for UI query
        _contributions.RegisterContextMenuHandler(
            Manifest.Id, contributionId, label, extensions, order, handler);

        // Also register in the existing ContextMenuRegistry for immediate functionality
        var fullId = $"plugin.{Manifest.Id}.{contributionId}";
        _contextMenuRegistry.Register(new ContextMenuEntry
        {
            Id = fullId,
            Label = label,
            Target = ContextMenuTarget.File,
            Order = order,
            Group = "plugin",
            Handler = node =>
            {
                if (node is not null) handler(node);
            },
            IsEnabled = node =>
            {
                if (node is null || extensions.Length == 0) return true;
                var ext = Path.GetExtension(node.Name);
                return extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
            }
        });

        _registeredContextMenuIds.Add(fullId);
        Logger.Debug($"Plugin '{Manifest.Id}' registered context menu handler '{contributionId}'", "PluginHost");
    }

    public void RegisterFileHandler(string[] extensions, Action<FileNode> handler)
    {
        _contributions.RegisterFileHandler(Manifest.Id, extensions, handler);
        Logger.Debug($"Plugin '{Manifest.Id}' registered file handler for [{string.Join(", ", extensions)}]", "PluginHost");
    }

    public void RegisterFileIcons(Dictionary<string, string> extensionToIconPath)
    {
        // Resolve relative icon paths to absolute paths
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (ext, relativePath) in extensionToIconPath)
        {
            resolved[ext] = Path.Combine(PluginDirectory, relativePath);
        }
        _contributions.RegisterFileIcons(Manifest.Id, resolved);
        Logger.Debug($"Plugin '{Manifest.Id}' registered {resolved.Count} file icon(s)", "PluginHost");
    }

    public void RegisterKeyBinding(KeyBinding binding, Action handler)
    {
        var commandId = $"plugin.{Manifest.Id}.{binding.DisplayString}";
        if (_keyboardShortcutService.Register(binding, commandId, handler))
        {
            _registeredKeyBindingIds.Add(commandId);
            _contributions.RegisterKeyBinding(Manifest.Id, binding, commandId, handler);
            Logger.Debug($"Plugin '{Manifest.Id}' registered key binding '{binding.DisplayString}'", "PluginHost");
        }
        else
        {
            Logger.Warning($"Plugin '{Manifest.Id}' failed to register key binding '{binding.DisplayString}' (already bound)", "PluginHost");
        }
    }

    /// <summary>
    /// Cleans up all registrations made by this plugin context.
    /// Called during plugin deactivation.
    /// </summary>
    internal void Cleanup()
    {
        // Unregister context menu items from the existing registry
        foreach (var id in _registeredContextMenuIds)
            _contextMenuRegistry.Unregister(id);
        _registeredContextMenuIds.Clear();

        // Unregister key bindings
        foreach (var id in _registeredKeyBindingIds)
            _keyboardShortcutService.Unregister(id);
        _registeredKeyBindingIds.Clear();

        // Unregister all UI contributions
        _contributions.UnregisterAll(Manifest.Id);
    }

    // ── Manifest lookup helpers ──

    private SidebarContribution? FindSidebarContribution(string id) =>
        Manifest.Contributes.Sidebar.FirstOrDefault(c => c.Id == id);

    private PanelContribution? FindBottomPanelContribution(string id) =>
        Manifest.Contributes.BottomPanel.FirstOrDefault(c => c.Id == id);

    private PanelContribution? FindRightPanelContribution(string id) =>
        Manifest.Contributes.RightPanel.FirstOrDefault(c => c.Id == id);

    private StatusBarContribution? FindStatusBarContribution(string id) =>
        Manifest.Contributes.StatusBar.FirstOrDefault(c => c.Id == id);

    private ToolbarButtonContribution? FindToolbarButtonContribution(string id) =>
        Manifest.Contributes.ToolbarButtons.FirstOrDefault(c => c.Id == id);

    private ContextMenuContribution? FindContextMenuContribution(string id) =>
        Manifest.Contributes.ContextMenu.FirstOrDefault(c => c.Id == id);

    private string? ResolveIconPath(string? relativeIconPath)
    {
        if (string.IsNullOrEmpty(relativeIconPath)) return null;
        return Path.Combine(PluginDirectory, relativeIconPath);
    }
}
