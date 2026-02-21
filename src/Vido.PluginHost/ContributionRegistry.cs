using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
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

    // Track which plugin registered which key bindings for cleanup
    private readonly Dictionary<string, List<string>> _pluginKeyBindings = [];

    public event Action? ContributionsChanged;

    // ── Registration ──

    public void RegisterSidebarPanel(string pluginId, string contributionId, string title,
        string? iconPath, int order, Func<object> viewFactory)
    {
        lock (_lock)
        {
            _sidebars.Add(new SidebarRegistration(pluginId, contributionId, title, iconPath, order, viewFactory));
            _sidebars.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterBottomPanel(string pluginId, string contributionId, string title,
        int order, Func<object> viewFactory)
    {
        lock (_lock)
        {
            _bottomPanels.Add(new PanelRegistration(pluginId, contributionId, title, order, viewFactory));
            _bottomPanels.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterRightPanel(string pluginId, string contributionId, string title,
        int order, Func<object> viewFactory)
    {
        lock (_lock)
        {
            _rightPanels.Add(new PanelRegistration(pluginId, contributionId, title, order, viewFactory));
            _rightPanels.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterStatusBarItem(string pluginId, string contributionId, string position,
        int order, Func<object> viewFactory)
    {
        lock (_lock)
        {
            _statusBarItems.Add(new StatusBarRegistration(pluginId, contributionId, position, order, viewFactory));
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterToolbarButton(string pluginId, string contributionId, string tooltip,
        string? iconPath, int order, Action clickHandler)
    {
        lock (_lock)
        {
            _toolbarButtons.Add(new ToolbarButtonRegistration(pluginId, contributionId, tooltip, iconPath, order, clickHandler));
            _toolbarButtons.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterContextMenuHandler(string pluginId, string contributionId, string label,
        string[] fileExtensions, int order, Action<FileNode> handler)
    {
        lock (_lock)
        {
            _contextMenuItems.Add(new ContextMenuRegistration(pluginId, contributionId, label, fileExtensions, order, handler));
            _contextMenuItems.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        ContributionsChanged?.Invoke();
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
            {
                _pluginFileIconKeys[pluginId] = keys;
            }
            else
            {
                existing.AddRange(keys);
            }
        }
        ContributionsChanged?.Invoke();
    }

    public void RegisterKeyBinding(string pluginId, KeyBinding binding, string commandId, Action handler)
    {
        lock (_lock)
        {
            if (!_pluginKeyBindings.TryGetValue(pluginId, out var bindings))
            {
                bindings = [];
                _pluginKeyBindings[pluginId] = bindings;
            }
            bindings.Add(commandId);
        }
        // Note: actual key binding registration is handled by PluginContext via IKeyboardShortcutService
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

    // ── Cleanup ──

    /// <summary>
    /// Removes all contributions registered by the specified plugin.
    /// Returns the list of key binding command IDs that were registered so the caller
    /// can unregister them from <see cref="IKeyboardShortcutService"/>.
    /// </summary>
    public IReadOnlyList<string> GetPluginKeyBindingCommandIds(string pluginId)
    {
        lock (_lock)
        {
            return _pluginKeyBindings.TryGetValue(pluginId, out var bindings)
                ? bindings.ToList()
                : [];
        }
    }

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

            _pluginKeyBindings.Remove(pluginId);
        }
        ContributionsChanged?.Invoke();
    }
}
