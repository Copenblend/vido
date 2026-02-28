using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents all UI and behavior contributions declared by a plugin manifest.
/// </summary>
public sealed class PluginContributions
{
    /// <summary>
    /// Gets or sets the sidebar panels this plugin adds to the application shell.
    /// </summary>
    [JsonPropertyName("sidebar")]
    public List<SidebarContribution> Sidebar { get; set; } = [];

    /// <summary>
    /// Gets or sets the tabs this plugin adds to the bottom panel area.
    /// </summary>
    [JsonPropertyName("bottomPanel")]
    public List<PanelContribution> BottomPanel { get; set; } = [];

    /// <summary>
    /// Gets or sets the tabs this plugin adds to the right panel area.
    /// </summary>
    [JsonPropertyName("rightPanel")]
    public List<PanelContribution> RightPanel { get; set; } = [];

    /// <summary>
    /// Gets or sets the status bar items this plugin renders at the bottom of the application window.
    /// </summary>
    [JsonPropertyName("statusBar")]
    public List<StatusBarContribution> StatusBar { get; set; } = [];

    /// <summary>
    /// Gets or sets the toolbar buttons this plugin adds to the main toolbar.
    /// </summary>
    [JsonPropertyName("toolbarButtons")]
    public List<ToolbarButtonContribution> ToolbarButtons { get; set; } = [];

    /// <summary>
    /// Gets or sets the mapping of file extensions to plugin-relative icon paths used to display custom icons in file listings.
    /// </summary>
    [JsonPropertyName("fileIcons")]
    public Dictionary<string, string> FileIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the context menu items this plugin adds to file right-click menus.
    /// </summary>
    [JsonPropertyName("contextMenu")]
    public List<ContextMenuContribution> ContextMenu { get; set; } = [];

    /// <summary>
    /// Gets or sets the file handlers this plugin registers to open or process specific file types.
    /// </summary>
    [JsonPropertyName("fileHandlers")]
    public List<FileHandlerContribution> FileHandlers { get; set; } = [];

    /// <summary>
    /// Gets or sets the user-configurable settings this plugin exposes in the settings UI.
    /// </summary>
    [JsonPropertyName("settings")]
    public List<SettingContribution> Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the control bar items this plugin adds to the media control bar.
    /// </summary>
    [JsonPropertyName("controlBar")]
    public List<ControlBarContribution> ControlBar { get; set; } = [];
}
