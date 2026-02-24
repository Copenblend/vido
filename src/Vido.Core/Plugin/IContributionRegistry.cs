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

    /// <summary>Registers a sidebar panel contribution from a plugin.</summary>
    void RegisterSidebarPanel(string pluginId, string contributionId, string title, string? iconPath, int order, Func<object> viewFactory);

    /// <summary>Registers a bottom panel tab contribution from a plugin.</summary>
    void RegisterBottomPanel(string pluginId, string contributionId, string title, int order, Func<object> viewFactory);

    /// <summary>Registers a right panel tab contribution from a plugin.</summary>
    void RegisterRightPanel(string pluginId, string contributionId, string title, int order, Func<object> viewFactory);

    /// <summary>Registers a status bar item contribution from a plugin.</summary>
    void RegisterStatusBarItem(string pluginId, string contributionId, string name, string position, int order, Func<object> viewFactory);

    /// <summary>Registers a toolbar button contribution in the title bar area.</summary>
    void RegisterToolbarButton(string pluginId, string contributionId, string tooltip, string? iconPath, int order, Action clickHandler);

    /// <summary>
    /// Sets the highlight (active) state of a toolbar button.
    /// When highlighted, the host renders the button with an accent background.
    /// </summary>
    void SetToolbarButtonHighlight(string pluginId, string contributionId, bool highlighted);

    /// <summary>Registers a context menu handler for files matching specified extensions.</summary>
    void RegisterContextMenuHandler(string pluginId, string contributionId, string label, string[] fileExtensions, int order, Action<FileNode> handler);

    /// <summary>Registers a file handler for double-click actions on specified extensions.</summary>
    void RegisterFileHandler(string pluginId, string[] extensions, Action<FileNode> handler);

    /// <summary>Registers custom file icons mapping file extensions to icon paths.</summary>
    void RegisterFileIcons(string pluginId, Dictionary<string, string> extensionToIconPath);

    /// <summary>Registers a keyboard binding for a plugin command.</summary>
    void RegisterKeyBinding(string pluginId, KeyBinding binding, string commandId, Action handler);

    /// <summary>Registers a control bar item contribution (shown left of loop button).</summary>
    void RegisterControlBarItem(string pluginId, string contributionId, string tooltip, int order,
        Func<object> viewFactory, Func<object>? overlayFactory);

    // ── Query (called by UI layer) ──

    /// <summary>Gets all registered sidebar panel contributions, ordered by priority.</summary>
    IReadOnlyList<SidebarRegistration> GetSidebarPanels();

    /// <summary>Gets all registered bottom panel tab contributions, ordered by priority.</summary>
    IReadOnlyList<PanelRegistration> GetBottomPanels();

    /// <summary>Gets all registered right panel tab contributions, ordered by priority.</summary>
    IReadOnlyList<PanelRegistration> GetRightPanels();

    /// <summary>Gets all registered status bar item contributions, ordered by priority.</summary>
    IReadOnlyList<StatusBarRegistration> GetStatusBarItems();

    /// <summary>Gets all registered toolbar button contributions, ordered by priority.</summary>
    IReadOnlyList<ToolbarButtonRegistration> GetToolbarButtons();

    /// <summary>Gets all registered context menu item contributions.</summary>
    IReadOnlyList<ContextMenuRegistration> GetContextMenuItems();

    /// <summary>Gets all registered file handler contributions.</summary>
    IReadOnlyList<FileHandlerRegistration> GetFileHandlers();

    /// <summary>Gets the merged file icon map from all plugins (extension → icon path).</summary>
    IReadOnlyDictionary<string, string> GetFileIcons();

    /// <summary>Gets all registered control bar item contributions, ordered by priority.</summary>
    IReadOnlyList<ControlBarRegistration> GetControlBarItems();

    // ── Unregistration (called when plugin deactivates) ──

    /// <summary>
    /// Stores a reference to the host-side <see cref="Layout.StatusBarItem"/> so
    /// that plugins can push text updates after initial registration.
    /// Called by the UI layer after wiring.
    /// </summary>
    void SetStatusBarItemReference(string fullId, Layout.StatusBarItem item);

    /// <summary>
    /// Updates the text of a status bar item.
    /// No-op if the item has not been wired yet.
    /// </summary>
    void UpdateStatusBarItem(string fullId, string text);

    /// <summary>Removes all contributions registered by the specified plugin.</summary>
    void UnregisterAll(string pluginId);

    // ── Change notification ──

    /// <summary>Raised whenever contributions are added or removed.</summary>
    event Action? ContributionsChanged;

    /// <summary>
    /// Raised when a toolbar button's highlight state changes.
    /// Arguments: full button ID ("plugin.{pluginId}.{contributionId}"), highlighted.
    /// </summary>
    event Action<string, bool>? ToolbarButtonHighlightChanged;

    /// <summary>
    /// Raised when a plugin requests that a specific right panel be shown and expanded.
    /// The argument is the full panel ID (e.g. "plugin.{pluginId}.{contributionId}").
    /// </summary>
    event Action<string>? RightPanelShowRequested;

    /// <summary>
    /// Raised when a plugin requests that a specific bottom panel tab be shown and activated.
    /// The argument is the full panel ID (e.g. "plugin.{pluginId}.{contributionId}").
    /// </summary>
    event Action<string>? BottomPanelShowRequested;

    /// <summary>
    /// Raised when a plugin toggles a control bar overlay's visibility.
    /// Arguments: full ID ("plugin.{pluginId}.{contributionId}"), visible.
    /// </summary>
    event Action<string, bool>? ControlBarOverlayToggled;
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

/// <summary>A control bar item contributed by a plugin (shown left of the loop button).</summary>
public sealed record ControlBarRegistration(
    string PluginId,
    string ContributionId,
    string Tooltip,
    int Order,
    Func<object> ViewFactory,
    Func<object>? OverlayFactory);
