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
    private bool _hasPlaylistProvider;


    /// <summary>
    /// The deserialized plugin manifest describing this plugin's metadata and contributions.
    /// </summary>
    public PluginManifest Manifest { get; }

    /// <summary>
    /// Absolute path to the directory from which this plugin was loaded.
    /// </summary>
    public string PluginDirectory { get; }

    /// <summary>
    /// Application-wide event bus for publishing and subscribing to domain events.
    /// </summary>
    public IEventBus Events { get; }

    /// <summary>
    /// Video playback engine exposed to plugins for media control and state queries.
    /// </summary>
    public IVideoEngine VideoEngine { get; }

    /// <summary>
    /// Logging service for emitting structured log messages tagged to this plugin.
    /// </summary>
    public ILogService Logger { get; }

    /// <summary>
    /// Per-plugin settings store for reading and writing persistent key/value configuration.
    /// </summary>
    public IPluginSettingsStore Settings { get; }

    /// <summary>
    /// Creates a plugin context wired to the host's core services and contribution registry.
    /// Each plugin receives its own context instance during activation.
    /// </summary>
    /// <param name="manifest">Deserialized manifest describing the plugin's metadata and contributions.</param>
    /// <param name="pluginDirectory">Absolute path to the plugin's installation directory.</param>
    /// <param name="eventBus">Application-wide event bus for domain events.</param>
    /// <param name="videoEngine">Video playback engine for media control.</param>
    /// <param name="logService">Logging service for structured log output.</param>
    /// <param name="settingsStore">Per-plugin persistent settings store.</param>
    /// <param name="contributions">Central registry for UI contribution registration.</param>
    /// <param name="contextMenuRegistry">Registry for context menu entries.</param>
    /// <param name="keyboardShortcutService">Service for registering keyboard shortcuts.</param>
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

    /// <summary>
    /// Registers a sidebar panel for this plugin by looking up the contribution metadata
    /// from the manifest and delegating to the central contribution registry.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="viewFactory">Factory that creates the sidebar panel's view element.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewFactory"/> is null.</exception>
    public void RegisterSidebarPanel(string contributionId, Func<object> viewFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(viewFactory);

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

    /// <summary>
    /// Registers a bottom panel for this plugin by looking up the contribution metadata
    /// from the manifest and delegating to the central contribution registry.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="viewFactory">Factory that creates the bottom panel's view element.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewFactory"/> is null.</exception>
    public void RegisterBottomPanel(string contributionId, Func<object> viewFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(viewFactory);

        var contrib = FindBottomPanelContribution(contributionId);
        _contributions.RegisterBottomPanel(
            Manifest.Id, contributionId,
            contrib?.Title ?? contributionId,
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered bottom panel '{contributionId}'", "PluginHost");
    }

    /// <summary>
    /// Registers a right panel for this plugin by looking up the contribution metadata
    /// from the manifest and delegating to the central contribution registry.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="viewFactory">Factory that creates the right panel's view element.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewFactory"/> is null.</exception>
    public void RegisterRightPanel(string contributionId, Func<object> viewFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(viewFactory);

        var contrib = FindRightPanelContribution(contributionId);
        _contributions.RegisterRightPanel(
            Manifest.Id, contributionId,
            contrib?.Title ?? contributionId,
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered right panel '{contributionId}'", "PluginHost");
    }

    /// <summary>
    /// Registers a status bar item for this plugin by looking up the contribution metadata
    /// from the manifest and delegating to the central contribution registry.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="viewFactory">Factory that creates the status bar item's view element.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewFactory"/> is null.</exception>
    public void RegisterStatusBarItem(string contributionId, Func<object> viewFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(viewFactory);

        var contrib = FindStatusBarContribution(contributionId);
        _contributions.RegisterStatusBarItem(
            Manifest.Id, contributionId,
            contrib?.Name ?? contributionId,
            contrib?.Position ?? "right",
            contrib?.Order ?? 100,
            viewFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered status bar item '{contributionId}'", "PluginHost");
    }

    /// <summary>
    /// Updates the display text of a previously registered status bar item owned by this plugin.
    /// </summary>
    /// <param name="contributionId">Contribution identifier of the status bar item to update.</param>
    /// <param name="text">New text to display.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    public void UpdateStatusBarItem(string contributionId, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        var fullId = $"plugin.{Manifest.Id}.{contributionId}";
        _contributions.UpdateStatusBarItem(fullId, text);
    }

    /// <summary>
    /// Registers a toolbar button click handler for this plugin by looking up the contribution
    /// metadata from the manifest (tooltip, icon, order) and delegating to the registry.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="clickHandler">Action invoked when the toolbar button is clicked.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="clickHandler"/> is null.</exception>
    public void RegisterToolbarButtonHandler(string contributionId, Action clickHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(clickHandler);

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

    /// <summary>
    /// Sets or clears the visual highlight state on a toolbar button owned by this plugin.
    /// </summary>
    /// <param name="contributionId">Contribution identifier of the toolbar button.</param>
    /// <param name="highlighted">True to highlight; false to remove the highlight.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    public void SetToolbarButtonHighlight(string contributionId, bool highlighted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        _contributions.SetToolbarButtonHighlight(Manifest.Id, contributionId, highlighted);
    }

    /// <summary>
    /// Registers a context menu handler for this plugin. Looks up metadata (label, file extensions, order)
    /// from the manifest, registers in both the contribution registry and the context menu registry.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="handler">Action invoked with the selected file node when the menu item is clicked.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
    public void RegisterContextMenuHandler(string contributionId, Action<FileNode> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(handler);

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

    /// <summary>
    /// Registers a file handler that processes files matching the specified extensions
    /// when opened from the file browser.
    /// </summary>
    /// <param name="extensions">File extensions this handler can open (e.g. ".funscript").</param>
    /// <param name="handler">Action invoked with the file node to process.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="extensions"/> or <paramref name="handler"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="extensions"/> is empty.</exception>
    public void RegisterFileHandler(string[] extensions, Action<FileNode> handler)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(handler);
        if (extensions.Length == 0)
            throw new ArgumentException("At least one file extension is required.", nameof(extensions));

        _contributions.RegisterFileHandler(Manifest.Id, extensions, handler);
        Logger.Debug($"Plugin '{Manifest.Id}' registered file handler for [{string.Join(", ", extensions)}]", "PluginHost");
    }

    /// <summary>
    /// Registers custom file icons for the given file extensions. Relative icon paths
    /// in the mapping are resolved to absolute paths within the plugin directory.
    /// </summary>
    /// <param name="extensionToIconPath">Mapping of file extensions to relative icon file paths within the plugin.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="extensionToIconPath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="extensionToIconPath"/> is empty.</exception>
    public void RegisterFileIcons(Dictionary<string, string> extensionToIconPath)
    {
        ArgumentNullException.ThrowIfNull(extensionToIconPath);
        if (extensionToIconPath.Count == 0)
            throw new ArgumentException("At least one file icon mapping is required.", nameof(extensionToIconPath));

        // Resolve relative icon paths to absolute paths
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (ext, relativePath) in extensionToIconPath)
        {
            resolved[ext] = Path.Combine(PluginDirectory, relativePath);
        }
        _contributions.RegisterFileIcons(Manifest.Id, resolved);
        Logger.Debug($"Plugin '{Manifest.Id}' registered {resolved.Count} file icon(s)", "PluginHost");
    }

    /// <summary>
    /// Registers a global keyboard shortcut for this plugin. The binding is routed through
    /// <see cref="Core.Keyboard.IKeyboardShortcutService"/> and logged on success or failure.
    /// </summary>
    /// <param name="binding">The key combination to bind.</param>
    /// <param name="handler">Action invoked when the key combination is pressed.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="binding"/> or <paramref name="handler"/> is null.</exception>
    public void RegisterKeyBinding(KeyBinding binding, Action handler)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(handler);

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
    /// Requests the host UI to show and expand the specified right panel owned by this plugin.
    /// </summary>
    /// <param name="contributionId">Contribution identifier of the right panel to show.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    public void RequestShowRightPanel(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        var fullId = $"plugin.{Manifest.Id}.{contributionId}";
        _contributions.RequestShowRightPanel(fullId);
    }

    /// <summary>
    /// Requests the host UI to show and activate the specified bottom panel tab owned by this plugin.
    /// </summary>
    /// <param name="contributionId">Contribution identifier of the bottom panel to show.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    public void RequestShowBottomPanel(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        var fullId = $"plugin.{Manifest.Id}.{contributionId}";
        _contributions.RequestShowBottomPanel(fullId);
    }

    /// <summary>
    /// Registers a control bar item for this plugin with an optional popup overlay,
    /// using metadata from the manifest for tooltip and order.
    /// </summary>
    /// <param name="contributionId">Contribution identifier declared in the plugin manifest.</param>
    /// <param name="viewFactory">Factory that creates the control bar item's view element.</param>
    /// <param name="overlayFactory">Optional factory that creates a popup overlay for the item.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewFactory"/> is null.</exception>
    public void RegisterControlBarItem(string contributionId, Func<object> viewFactory, Func<object>? overlayFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(viewFactory);

        var contrib = FindControlBarContribution(contributionId);
        _contributions.RegisterControlBarItem(
            Manifest.Id, contributionId,
            contrib?.Tooltip ?? contributionId,
            contrib?.Order ?? 100,
            viewFactory,
            overlayFactory);
        Logger.Debug($"Plugin '{Manifest.Id}' registered control bar item '{contributionId}'", "PluginHost");
    }

    /// <summary>
    /// Toggles the visibility of a control bar overlay popup owned by this plugin.
    /// </summary>
    /// <param name="contributionId">Contribution identifier of the control bar item.</param>
    /// <param name="visible">True to show the overlay; false to hide it.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contributionId"/> is null or whitespace.</exception>
    public void ToggleControlBarOverlay(string contributionId, bool visible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        var fullId = $"plugin.{Manifest.Id}.{contributionId}";
        _contributions.ToggleControlBarOverlay(fullId, visible);
    }

    /// <summary>
    /// Registers this plugin as the active playlist provider.
    /// Only one playlist provider can be active across all plugins.
    /// </summary>
    /// <param name="provider">The playlist provider implementation to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="provider"/> is null.</exception>
    public void RegisterPlaylistProvider(IPlaylistProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _contributions.RegisterPlaylistProvider(Manifest.Id, provider);
        _hasPlaylistProvider = true;
        Logger.Debug($"Plugin '{Manifest.Id}' registered playlist provider", "PluginHost");
    }

    /// <summary>
    /// Removes this plugin's playlist provider registration if one was previously set.
    /// </summary>
    public void UnregisterPlaylistProvider()
    {
        if (_hasPlaylistProvider)
        {
            _contributions.UnregisterPlaylistProvider(Manifest.Id);
            _hasPlaylistProvider = false;
            Logger.Debug($"Plugin '{Manifest.Id}' unregistered playlist provider", "PluginHost");
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

        // Unregister playlist provider
        if (_hasPlaylistProvider)
        {
            _contributions.UnregisterPlaylistProvider(Manifest.Id);
            _hasPlaylistProvider = false;
        }

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

    private ControlBarContribution? FindControlBarContribution(string id) =>
        Manifest.Contributes.ControlBar.FirstOrDefault(c => c.Id == id);

    private string? ResolveIconPath(string? relativeIconPath)
    {
        if (string.IsNullOrEmpty(relativeIconPath)) return null;
        return Path.Combine(PluginDirectory, relativeIconPath);
    }
}
