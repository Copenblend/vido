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
    private readonly Dictionary<string, string> _fileIcons = new(StringComparer.OrdinalIgnoreCase);

    // Track which plugin registered which file icon extensions for cleanup
    private readonly Dictionary<string, List<string>> _pluginFileIconKeys = [];

    // Map of wired status-bar full IDs → host StatusBarItem instances for text updates
    private readonly Dictionary<string, StatusBarItem> _statusBarItemRefs = new(StringComparer.OrdinalIgnoreCase);

    public event Action? ContributionsChanged;
    public event Action<string, bool>? ToolbarButtonHighlightChanged;
    public event Action<string>? RightPanelShowRequested;
    public event Action<string>? BottomPanelShowRequested;

    // Track highlighted toolbar buttons
    private readonly HashSet<string> _highlightedToolbarButtons = new(StringComparer.OrdinalIgnoreCase);

    // ── Helpers ──

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

    // ── Registration ──

    public void RegisterSidebarPanel(string pluginId, string contributionId, string title,
        string? iconPath, int order, Func<object> viewFactory)
    {
        InsertSorted(_sidebars,
            new SidebarRegistration(pluginId, contributionId, title, iconPath, order, viewFactory),
            (a, b) => a.Order.CompareTo(b.Order));
    }

    public void RegisterBottomPanel(string pluginId, string contributionId, string title,
        int order, Func<object> viewFactory)
    {
        InsertSorted(_bottomPanels,
            new PanelRegistration(pluginId, contributionId, title, order, viewFactory),
            (a, b) => a.Order.CompareTo(b.Order));
    }

    public void RegisterRightPanel(string pluginId, string contributionId, string title,
        int order, Func<object> viewFactory)
    {
        InsertSorted(_rightPanels,
            new PanelRegistration(pluginId, contributionId, title, order, viewFactory),
            (a, b) => a.Order.CompareTo(b.Order));
    }

    public void RegisterStatusBarItem(string pluginId, string contributionId, string name, string position,
        int order, Func<object> viewFactory)
    {
        lock (_lock)
        {
            _statusBarItems.Add(new StatusBarRegistration(pluginId, contributionId, name, position, order, viewFactory));
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterToolbarButton(string pluginId, string contributionId, string tooltip,
        string? iconPath, int order, Action clickHandler)
    {
        InsertSorted(_toolbarButtons,
            new ToolbarButtonRegistration(pluginId, contributionId, tooltip, iconPath, order, clickHandler),
            (a, b) => a.Order.CompareTo(b.Order));
    }

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

    public void RegisterContextMenuHandler(string pluginId, string contributionId, string label,
        string[] fileExtensions, int order, Action<FileNode> handler)
    {
        InsertSorted(_contextMenuItems,
            new ContextMenuRegistration(pluginId, contributionId, label, fileExtensions, order, handler),
            (a, b) => a.Order.CompareTo(b.Order));
    }

    public void RegisterFileHandler(string pluginId, string[] extensions, Action<FileNode> handler)
    {
        lock (_lock)
        {
            _fileHandlers.Add(new FileHandlerRegistration(pluginId, extensions, handler));
        }
        ContributionsChanged?.Invoke();
    }

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

    public void RegisterKeyBinding(string pluginId, KeyBinding binding, string commandId, Action handler)
    {
        // Key binding registration is handled by PluginContext via IKeyboardShortcutService.
        // This method exists to satisfy the IContributionRegistry interface contract.
    }

    // ── Status bar item updates ──

    public void SetStatusBarItemReference(string fullId, StatusBarItem item)
    {
        lock (_lock)
            _statusBarItemRefs[fullId] = item;
    }

    public void UpdateStatusBarItem(string fullId, string text)
    {
        StatusBarItem? item;
        lock (_lock)
            _statusBarItemRefs.TryGetValue(fullId, out item);

        if (item is null) return;
        item.Text = text;
    }

    // ── Query ──

    public IReadOnlyList<SidebarRegistration> GetSidebarPanels()
    {
        lock (_lock) return _sidebars.ToList();
    }

    public IReadOnlyList<PanelRegistration> GetBottomPanels()
    {
        lock (_lock) return _bottomPanels.ToList();
    }

    public IReadOnlyList<PanelRegistration> GetRightPanels()
    {
        lock (_lock) return _rightPanels.ToList();
    }

    public IReadOnlyList<StatusBarRegistration> GetStatusBarItems()
    {
        lock (_lock) return _statusBarItems.ToList();
    }

    public IReadOnlyList<ToolbarButtonRegistration> GetToolbarButtons()
    {
        lock (_lock) return _toolbarButtons.ToList();
    }

    public IReadOnlyList<ContextMenuRegistration> GetContextMenuItems()
    {
        lock (_lock) return _contextMenuItems.ToList();
    }

    public IReadOnlyList<FileHandlerRegistration> GetFileHandlers()
    {
        lock (_lock) return _fileHandlers.ToList();
    }

    public IReadOnlyDictionary<string, string> GetFileIcons()
    {
        lock (_lock) return new Dictionary<string, string>(_fileIcons, StringComparer.OrdinalIgnoreCase);
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

            // Remove file icons registered by this plugin
            if (_pluginFileIconKeys.TryGetValue(pluginId, out var iconKeys))
            {
                foreach (var key in iconKeys)
                    _fileIcons.Remove(key);
                _pluginFileIconKeys.Remove(pluginId);
            }
        }
        ContributionsChanged?.Invoke();
    }
}
