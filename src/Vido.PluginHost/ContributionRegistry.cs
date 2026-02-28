using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Layout;
using Vido.Core.Plugin;

namespace Vido.PluginHost;

/// <summary>
/// Thread-safe central registry for all plugin UI contributions.
/// Plugins register contributions during activation; the UI layer queries
/// this registry to build dynamic panels, status bar items, toolbar buttons, etc.
/// </summary>
public sealed class ContributionRegistry : IContributionRegistry
{
    private readonly object _lock = new();

    private readonly List<SidebarRegistration> _sidebars = [];
    private readonly List<PanelRegistration> _bottomPanels = [];
    private readonly List<PanelRegistration> _rightPanels = [];
    private readonly List<StatusBarRegistration> _statusBarItems = [];
    private readonly List<ToolbarButtonRegistration> _toolbarButtons = [];
    private readonly List<ContextMenuRegistration> _contextMenuItems = [];
    private readonly List<FileHandlerRegistration> _fileHandlers = [];
    private readonly List<ControlBarRegistration> _controlBarItems = [];
    private readonly Dictionary<string, string> _fileIcons = new(StringComparer.OrdinalIgnoreCase);

    // Track which plugin registered which file icon extensions for cleanup
    private readonly Dictionary<string, List<string>> _pluginFileIconKeys = [];

    // Map of wired status-bar full IDs â†’ host StatusBarItem instances for text updates
    private readonly Dictionary<string, StatusBarItem> _statusBarItemRefs = new(StringComparer.OrdinalIgnoreCase);

    // Playlist provider (only one at a time)
    private IPlaylistProvider? _playlistProvider;
    private string? _playlistProviderPluginId;
    /// <summary>
    /// Raised whenever any contribution is added or removed, signaling the UI to refresh.
    /// </summary>

    public event Action? ContributionsChanged;
    /// <summary>
    /// Raised when a toolbar button's highlight state changes, carrying the full button ID and the new state.
    /// </summary>
    public event Action<string, bool>? ToolbarButtonHighlightChanged;
    /// <summary>
    /// Raised to request that the host UI show and expand a specific right panel tab.
    /// </summary>
    public event Action<string>? RightPanelShowRequested;
    /// <summary>
    /// Raised to request that the host UI show and activate a specific bottom panel tab.
    /// </summary>
    public event Action<string>? BottomPanelShowRequested;
    /// <summary>
    /// Raised when a control bar overlay's visibility is toggled, carrying the full ID and the new visibility state.
    /// </summary>
    public event Action<string, bool>? ControlBarOverlayToggled;

    // Track highlighted toolbar buttons
    private readonly HashSet<string> _highlightedToolbarButtons = new(StringComparer.OrdinalIgnoreCase);

    // â”€â”€ Helpers â”€â”€

    /// <summary>
    /// Inserts an item into a sorted list using binary search for O(log n) insertion,
    /// then fires ContributionsChanged.
    /// </summary>
    private void InsertSorted<T>(List<T> list, T item, Comparison<T> comparison)
    {
        lock (_lock)
        {
            var index = list.BinarySearch(item, Comparer<T>.Create(comparison));
            if (index < 0) index = ~index;
            list.Insert(index, item);
        }
        ContributionsChanged?.Invoke();
    }

    // â”€â”€ Registration â”€â”€
    /// <summary>
    /// Adds a sidebar panel contribution, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the panel.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="title">Display title shown in the sidebar tab.</param>
    /// <param name="iconPath">Absolute path to the sidebar icon, or null for no icon.</param>
    /// <param name="order">Sort order; lower values appear first.</param>
    /// <param name="viewFactory">Factory that creates the sidebar panel's view element.</param>

    public void RegisterSidebarPanel(string pluginId, string contributionId, string title,
        string? iconPath, int order, Func<object> viewFactory)
    {
        InsertSorted(_sidebars,
            new SidebarRegistration(pluginId, contributionId, title, iconPath, order, viewFactory),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Adds a bottom panel contribution, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the panel.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="title">Display title shown on the bottom panel tab.</param>
    /// <param name="order">Sort order; lower values appear first.</param>
    /// <param name="viewFactory">Factory that creates the bottom panel's view element.</param>
    public void RegisterBottomPanel(string pluginId, string contributionId, string title,
        int order, Func<object> viewFactory)
    {
        InsertSorted(_bottomPanels,
            new PanelRegistration(pluginId, contributionId, title, order, viewFactory),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Adds a right panel contribution, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the panel.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="title">Display title shown on the right panel tab.</param>
    /// <param name="order">Sort order; lower values appear first.</param>
    /// <param name="viewFactory">Factory that creates the right panel's view element.</param>
    public void RegisterRightPanel(string pluginId, string contributionId, string title,
        int order, Func<object> viewFactory)
    {
        InsertSorted(_rightPanels,
            new PanelRegistration(pluginId, contributionId, title, order, viewFactory),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Adds a status bar item contribution, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the item.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="name">Display name of the status bar item.</param>
    /// <param name="position">Alignment position in the status bar (e.g. "left" or "right").</param>
    /// <param name="order">Sort order; lower values appear first within the position group.</param>
    /// <param name="viewFactory">Factory that creates the status bar item's view element.</param>
    public void RegisterStatusBarItem(string pluginId, string contributionId, string name, string position,
        int order, Func<object> viewFactory)
    {
        InsertSorted(_statusBarItems,
            new StatusBarRegistration(pluginId, contributionId, name, position, order, viewFactory),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Adds a toolbar button contribution, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the button.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="tooltip">Tooltip text shown when hovering over the button.</param>
    /// <param name="iconPath">Absolute path to the button icon, or null for no icon.</param>
    /// <param name="order">Sort order; lower values appear first.</param>
    /// <param name="clickHandler">Action invoked when the toolbar button is clicked.</param>
    public void RegisterToolbarButton(string pluginId, string contributionId, string tooltip,
        string? iconPath, int order, Action clickHandler)
    {
        InsertSorted(_toolbarButtons,
            new ToolbarButtonRegistration(pluginId, contributionId, tooltip, iconPath, order, clickHandler),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Sets or clears the visual highlight state on a toolbar button and raises
    /// <see cref="ToolbarButtonHighlightChanged"/> to notify the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin owning the button.</param>
    /// <param name="contributionId">Contribution identifier of the toolbar button.</param>
    /// <param name="highlighted">True to highlight the button; false to remove the highlight.</param>
    public void SetToolbarButtonHighlight(string pluginId, string contributionId, bool highlighted)
    {
        var fullId = $"plugin.{pluginId}.{contributionId}";
        lock (_lock)
        {
            if (highlighted)
                _highlightedToolbarButtons.Add(fullId);
            else
                _highlightedToolbarButtons.Remove(fullId);
        }
        ToolbarButtonHighlightChanged?.Invoke(fullId, highlighted);
    }

    /// <summary>
    /// Adds a context menu handler for specific file extensions, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the handler.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="label">Display label shown in the context menu.</param>
    /// <param name="fileExtensions">File extensions this handler applies to (e.g. ".mp4").</param>
    /// <param name="order">Sort order; lower values appear first in the menu.</param>
    /// <param name="handler">Action invoked with the selected file node when the menu item is clicked.</param>
    public void RegisterContextMenuHandler(string pluginId, string contributionId, string label,
        string[] fileExtensions, int order, Action<FileNode> handler)
    {
        InsertSorted(_contextMenuItems,
            new ContextMenuRegistration(pluginId, contributionId, label, fileExtensions, order, handler),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }
    
    /// <summary>
    /// Registers a file handler that processes files matching the given extensions
    /// when opened from the file browser.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the handler.</param>
    /// <param name="extensions">File extensions this handler can open (e.g. ".funscript").</param>
    /// <param name="handler">Action invoked with the file node to process.</param>
    public void RegisterFileHandler(string pluginId, string[] extensions, Action<FileNode> handler)
    {
        lock (_lock)
        {
            _fileHandlers.Add(new FileHandlerRegistration(pluginId, extensions, handler));
        }
        ContributionsChanged?.Invoke();
    }

    /// <summary>
    /// Registers custom file icons for the given file extensions, replacing any previous
    /// mapping for those extensions, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the icons.</param>
    /// <param name="extensionToIconPath">Mapping of file extensions to absolute icon file paths.</param>
    public void RegisterFileIcons(string pluginId, Dictionary<string, string> extensionToIconPath)
    {
        lock (_lock)
        {
            var keys = new List<string>();
            foreach (var (ext, iconPath) in extensionToIconPath)
            {
                _fileIcons[ext] = iconPath;
                keys.Add(ext);
            }
            if (!_pluginFileIconKeys.TryGetValue(pluginId, out var existing))
                _pluginFileIconKeys[pluginId] = keys;
            else
                existing.AddRange(keys);
        }
        ContributionsChanged?.Invoke();
    }

    /// <summary>
    /// Placeholder for key binding registration. Actual registration is handled
    /// by <see cref="PluginContext"/> via <see cref="Core.Keyboard.IKeyboardShortcutService"/>.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the binding.</param>
    /// <param name="binding">The key combination to bind.</param>
    /// <param name="commandId">Unique command identifier for the binding.</param>
    /// <param name="handler">Action invoked when the key combination is pressed.</param>
    public void RegisterKeyBinding(string pluginId, KeyBinding binding, string commandId, Action handler)
    {
        // Key binding registration is handled by PluginContext via IKeyboardShortcutService.
        // This method exists to satisfy the IContributionRegistry interface contract.
    }

    /// <summary>
    /// Adds a control bar item contribution with an optional overlay, sorted by display order, and notifies the UI.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin registering the item.</param>
    /// <param name="contributionId">Unique contribution identifier within the plugin.</param>
    /// <param name="tooltip">Tooltip text shown when hovering over the control bar item.</param>
    /// <param name="order">Sort order; lower values appear first.</param>
    /// <param name="viewFactory">Factory that creates the control bar item's view element.</param>
    /// <param name="overlayFactory">Optional factory that creates a popup overlay for the item, or null.</param>
    public void RegisterControlBarItem(string pluginId, string contributionId, string tooltip,
        int order, Func<object> viewFactory, Func<object>? overlayFactory)
    {
        InsertSorted(_controlBarItems,
            new ControlBarRegistration(pluginId, contributionId, tooltip, order, viewFactory, overlayFactory),
            (a, b) =>
            {
                var cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.Compare(a.PluginId, b.PluginId, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Toggles visibility of a control bar overlay.
    /// </summary>
    public void ToggleControlBarOverlay(string fullId, bool visible)
    {
        ControlBarOverlayToggled?.Invoke(fullId, visible);
    }

    /// <summary>
    /// Sets the active playlist provider, replacing any previously registered provider.
    /// Only one playlist provider can be active at a time.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin providing playlist functionality.</param>
    /// <param name="provider">The playlist provider implementation to register.</param>
    public void RegisterPlaylistProvider(string pluginId, IPlaylistProvider provider)
    {
        lock (_lock)
        {
            _playlistProvider = provider;
            _playlistProviderPluginId = pluginId;
        }
    }

    /// <summary>
    /// Removes the active playlist provider if it was registered by the specified plugin.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin whose provider should be removed.</param>
    public void UnregisterPlaylistProvider(string pluginId)
    {
        lock (_lock)
        {
            if (string.Equals(_playlistProviderPluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                _playlistProvider = null;
                _playlistProviderPluginId = null;
            }
        }
    }

    /// <summary>
    /// Returns the currently registered playlist provider, or null if none is active.
    /// </summary>
    public IPlaylistProvider? GetPlaylistProvider()
    {
        lock (_lock) return _playlistProvider;
    }

    /// <summary>
    /// Stores a reference to a host-created <see cref="StatusBarItem"/> so its text
    /// can be updated later via <see cref="UpdateStatusBarItem"/>.
    /// </summary>
    /// <param name="fullId">Fully qualified status bar item ID (e.g. "plugin.myPlugin.itemId").</param>
    /// <param name="item">The host-side status bar item instance to track.</param>
    public void SetStatusBarItemReference(string fullId, StatusBarItem item)
    {
        lock (_lock)
            _statusBarItemRefs[fullId] = item;
    }

    /// <summary>
    /// Updates the display text of a previously wired status bar item.
    /// No-op if the item has not been wired via <see cref="SetStatusBarItemReference"/>.
    /// </summary>
    /// <param name="fullId">Fully qualified status bar item ID.</param>
    /// <param name="text">New text to display in the status bar item.</param>

    public void UpdateStatusBarItem(string fullId, string text)
    {
        StatusBarItem? item;
        lock (_lock)
            _statusBarItemRefs.TryGetValue(fullId, out item);

        if (item is null) return;
        item.Text = text;
    }

    /// <summary>
    /// Returns a snapshot of all registered sidebar panel contributions, in display order.
    /// </summary>

    public IReadOnlyList<SidebarRegistration> GetSidebarPanels()
    {
        lock (_lock) return _sidebars.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered bottom panel contributions, in display order.
    /// </summary>
    public IReadOnlyList<PanelRegistration> GetBottomPanels()
    {
        lock (_lock) return _bottomPanels.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered right panel contributions, in display order.
    /// </summary>
    public IReadOnlyList<PanelRegistration> GetRightPanels()
    {
        lock (_lock) return _rightPanels.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered status bar item contributions, in display order.
    /// </summary>
    public IReadOnlyList<StatusBarRegistration> GetStatusBarItems()
    {
        lock (_lock) return _statusBarItems.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered toolbar button contributions, in display order.
    /// </summary>
    public IReadOnlyList<ToolbarButtonRegistration> GetToolbarButtons()
    {
        lock (_lock) return _toolbarButtons.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered context menu item contributions, in display order.
    /// </summary>
    public IReadOnlyList<ContextMenuRegistration> GetContextMenuItems()
    {
        lock (_lock) return _contextMenuItems.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered file handler contributions.
    /// </summary>
    public IReadOnlyList<FileHandlerRegistration> GetFileHandlers()
    {
        lock (_lock) return _fileHandlers.ToList();
    }

    /// <summary>
    /// Returns a snapshot of all registered file extension-to-icon mappings.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetFileIcons()
    {
        lock (_lock) return new Dictionary<string, string>(_fileIcons, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a snapshot of all registered control bar item contributions, in display order.
    /// </summary>
    public IReadOnlyList<ControlBarRegistration> GetControlBarItems()
    {
        lock (_lock) return _controlBarItems.ToList();
    }

    /// <summary>
    /// Requests that the host show and expand the specified right panel.
    /// </summary>
    public void RequestShowRightPanel(string fullPanelId)
    {
        RightPanelShowRequested?.Invoke(fullPanelId);
    }

    /// <summary>
    /// Requests that the host show and activate the specified bottom panel tab.
    /// </summary>
    public void RequestShowBottomPanel(string fullPanelId)
    {
        BottomPanelShowRequested?.Invoke(fullPanelId);
    }

    // ── Cleanup ──
    /// <summary>
    /// Removes all UI contributions registered by the specified plugin and notifies the UI.
    /// This includes sidebar panels, bottom/right panels, status bar items, toolbar buttons,
    /// context menu items, file handlers, control bar items, file icons, and the playlist provider.
    /// </summary>
    /// <param name="pluginId">Identifier of the plugin whose contributions should be removed.</param>
    public void UnregisterAll(string pluginId)
    {
        lock (_lock)
        {
            _sidebars.RemoveAll(r => r.PluginId == pluginId);
            _bottomPanels.RemoveAll(r => r.PluginId == pluginId);
            _rightPanels.RemoveAll(r => r.PluginId == pluginId);
            _statusBarItems.RemoveAll(r => r.PluginId == pluginId);
            _toolbarButtons.RemoveAll(r => r.PluginId == pluginId);
            _contextMenuItems.RemoveAll(r => r.PluginId == pluginId);
            _fileHandlers.RemoveAll(r => r.PluginId == pluginId);
            _controlBarItems.RemoveAll(r => r.PluginId == pluginId);

            // Remove file icons registered by this plugin
            if (_pluginFileIconKeys.TryGetValue(pluginId, out var iconKeys))
            {
                foreach (var key in iconKeys)
                    _fileIcons.Remove(key);
                _pluginFileIconKeys.Remove(pluginId);
            }

            // Remove playlist provider if owned by this plugin
            if (string.Equals(_playlistProviderPluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                _playlistProvider = null;
                _playlistProviderPluginId = null;
            }
        }
        ContributionsChanged?.Invoke();
    }
}
