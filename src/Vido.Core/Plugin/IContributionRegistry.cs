using Vido.Core.FileSystem;
using Vido.Core.Keyboard;

namespace Vido.Core.Plugin;

/// <summary>
/// Central registry for all UI contributions from plugins.
/// Plugins register contributions via <see cref="IPluginContext"/>;
/// the UI layer queries this registry to wire contributed panels, items, and handlers.
/// </summary>
public interface IContributionRegistry
{
    // ── Registration (called by PluginContext) ──

    void RegisterSidebarPanel(string pluginId, string contributionId, string title, string? iconPath, int order, Func<object> viewFactory);
    void RegisterBottomPanel(string pluginId, string contributionId, string title, int order, Func<object> viewFactory);
    void RegisterRightPanel(string pluginId, string contributionId, string title, int order, Func<object> viewFactory);
    void RegisterStatusBarItem(string pluginId, string contributionId, string name, string position, int order, Func<object> viewFactory);
    void RegisterToolbarButton(string pluginId, string contributionId, string tooltip, string? iconPath, int order, Action clickHandler);
    void RegisterContextMenuHandler(string pluginId, string contributionId, string label, string[] fileExtensions, int order, Action<FileNode> handler);
    void RegisterFileHandler(string pluginId, string[] extensions, Action<FileNode> handler);
    void RegisterFileIcons(string pluginId, Dictionary<string, string> extensionToIconPath);
    void RegisterKeyBinding(string pluginId, KeyBinding binding, string commandId, Action handler);

    // ── Query (called by UI layer) ──

    IReadOnlyList<SidebarRegistration> GetSidebarPanels();
    IReadOnlyList<PanelRegistration> GetBottomPanels();
    IReadOnlyList<PanelRegistration> GetRightPanels();
    IReadOnlyList<StatusBarRegistration> GetStatusBarItems();
    IReadOnlyList<ToolbarButtonRegistration> GetToolbarButtons();
    IReadOnlyList<ContextMenuRegistration> GetContextMenuItems();
    IReadOnlyList<FileHandlerRegistration> GetFileHandlers();
    IReadOnlyDictionary<string, string> GetFileIcons();

    // ── Unregistration (called when plugin deactivates) ──

    void UnregisterAll(string pluginId);

    // ── Change notification ──

    /// <summary>Raised whenever contributions are added or removed.</summary>
    event Action? ContributionsChanged;
}

// ── Registration records ──

/// <summary>A sidebar panel contributed by a plugin.</summary>
public sealed record SidebarRegistration(
    string PluginId,
    string ContributionId,
    string Title,
    string? IconPath,
    int Order,
    Func<object> ViewFactory);

/// <summary>A bottom or right panel tab contributed by a plugin.</summary>
public sealed record PanelRegistration(
    string PluginId,
    string ContributionId,
    string Title,
    int Order,
    Func<object> ViewFactory);

/// <summary>A status bar item contributed by a plugin.</summary>
public sealed record StatusBarRegistration(
    string PluginId,
    string ContributionId,
    string Name,
    string Position,
    int Order,
    Func<object> ViewFactory);

/// <summary>A toolbar button contributed by a plugin.</summary>
public sealed record ToolbarButtonRegistration(
    string PluginId,
    string ContributionId,
    string Tooltip,
    string? IconPath,
    int Order,
    Action ClickHandler);

/// <summary>A context menu item contributed by a plugin.</summary>
public sealed record ContextMenuRegistration(
    string PluginId,
    string ContributionId,
    string Label,
    string[] FileExtensions,
    int Order,
    Action<FileNode> Handler);

/// <summary>A file handler contributed by a plugin.</summary>
public sealed record FileHandlerRegistration(
    string PluginId,
    string[] Extensions,
    Action<FileNode> Handler);
