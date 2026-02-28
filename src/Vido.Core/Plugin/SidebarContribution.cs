using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one sidebar panel declaration from a plugin manifest.
/// </summary>
public sealed class SidebarContribution
{
    /// <summary>
    /// Gets or sets the unique identifier used to reference this sidebar panel within the plugin.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text displayed on this sidebar panel's header.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the panel icon, relative to the plugin directory, shown in the sidebar navigation rail.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the sort priority that controls where this panel appears relative to other sidebar panels (lower values appear first).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}
