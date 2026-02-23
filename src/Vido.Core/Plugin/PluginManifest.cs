using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Deserialized representation of a plugin's <c>plugin.json</c> manifest.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Unique plugin identifier (e.g. "com.example.my-plugin").</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Internal plugin name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>User-facing display name.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Plugin version (semver).</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Short description of the plugin's functionality.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Plugin author name.</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>License identifier (e.g. "MIT").</summary>
    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    /// <summary>Relative path to the plugin's entry-point DLL.</summary>
    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>Fully-qualified class name implementing <see cref="IVidoPlugin"/>.</summary>
    [JsonPropertyName("pluginClass")]
    public string PluginClass { get; set; } = string.Empty;

    /// <summary>Minimum Vido version required to run this plugin.</summary>
    [JsonPropertyName("minVidoVersion")]
    public string MinVidoVersion { get; set; } = string.Empty;

    /// <summary>Repository URL for the plugin source.</summary>
    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    /// <summary>Tags for search/categorization.</summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>Relative path to the plugin icon image (e.g. "Assets/plugin-icon.png").</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>UI contributions declared by the plugin.</summary>
    [JsonPropertyName("contributes")]
    public PluginContributions Contributes { get; set; } = new();
}

/// <summary>
/// All UI contributions a plugin declares in its manifest.
/// </summary>
public sealed class PluginContributions
{
    /// <summary>Sidebar panel contributions (activity bar icon + panel content).</summary>
    [JsonPropertyName("sidebar")]
    public List<SidebarContribution> Sidebar { get; set; } = [];

    /// <summary>Bottom panel tab contributions.</summary>
    [JsonPropertyName("bottomPanel")]
    public List<PanelContribution> BottomPanel { get; set; } = [];

    /// <summary>Right panel tab contributions.</summary>
    [JsonPropertyName("rightPanel")]
    public List<PanelContribution> RightPanel { get; set; } = [];

    /// <summary>Status bar item contributions.</summary>
    [JsonPropertyName("statusBar")]
    public List<StatusBarContribution> StatusBar { get; set; } = [];

    /// <summary>Toolbar button contributions (title bar area).</summary>
    [JsonPropertyName("toolbarButtons")]
    public List<ToolbarButtonContribution> ToolbarButtons { get; set; } = [];

    /// <summary>
    /// File icon contributions. Keys are extensions (e.g. ".funscript"),
    /// values are paths relative to the plugin directory.
    /// </summary>
    [JsonPropertyName("fileIcons")]
    public Dictionary<string, string> FileIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Context menu item contributions.</summary>
    [JsonPropertyName("contextMenu")]
    public List<ContextMenuContribution> ContextMenu { get; set; } = [];

    /// <summary>File handler contributions (double-click handlers).</summary>
    [JsonPropertyName("fileHandlers")]
    public List<FileHandlerContribution> FileHandlers { get; set; } = [];

    /// <summary>Setting contributions (plugin settings).</summary>
    [JsonPropertyName("settings")]
    public List<SettingContribution> Settings { get; set; } = [];
}

/// <summary>Sidebar panel contribution declaration.</summary>
public sealed class SidebarContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Path to icon image relative to plugin directory.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}

/// <summary>Panel tab contribution (used for both bottom and right panels).</summary>
public sealed class PanelContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}

/// <summary>Status bar item contribution declaration.</summary>
public sealed class StatusBarContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Mandatory display name shown in the View menu for show/hide toggle.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>"left" or "right".</summary>
    [JsonPropertyName("position")]
    public string Position { get; set; } = "right";

    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}

/// <summary>Toolbar button contribution declaration.</summary>
public sealed class ToolbarButtonContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tooltip")]
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>Path to icon image relative to plugin directory.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}

/// <summary>Context menu item contribution declaration.</summary>
public sealed class ContextMenuContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>File extensions this menu item applies to (empty = all files).</summary>
    [JsonPropertyName("fileExtensions")]
    public List<string> FileExtensions { get; set; } = [];

    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}

/// <summary>File handler contribution (double-click handling).</summary>
public sealed class FileHandlerContribution
{
    /// <summary>File extensions handled (e.g. [".funscript"]).</summary>
    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = [];

    /// <summary>Action type (currently only "open" is supported).</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "open";
}

/// <summary>Plugin setting declaration.</summary>
public sealed class SettingContribution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Setting type: "boolean", "string", "number", "enum".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>Default value (as a JSON element — parsed at runtime).</summary>
    [JsonPropertyName("default")]
    public object? Default { get; set; }

    /// <summary>Display title for the setting.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Longer description text shown below the title.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Allowed values for <c>type: "enum"</c> settings.
    /// Must be non-empty when <see cref="Type"/> is <c>"enum"</c>.
    /// </summary>
    [JsonPropertyName("enumValues")]
    public List<string> EnumValues { get; set; } = [];

    /// <summary>
    /// Optional section name for visual grouping.
    /// Settings with the same section value are grouped under a header with a divider.
    /// </summary>
    [JsonPropertyName("section")]
    public string? Section { get; set; }

    /// <summary>
    /// When <c>true</c>, the developer's default value overwrites the user's saved value
    /// on every plugin load. Use sparingly — intended for breaking changes.
    /// </summary>
    [JsonPropertyName("forceOverride")]
    public bool ForceOverride { get; set; }

    /// <summary>
    /// Optional validation rule for stringList items.
    /// Supported values: <c>"url"</c> (requires valid https:// or file:// URI).
    /// When null or empty, no validation is applied.
    /// </summary>
    [JsonPropertyName("validation")]
    public string? Validation { get; set; }

    /// <summary>Supported setting type identifiers.</summary>
    public static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "boolean", "string", "number", "enum"
    };
}
