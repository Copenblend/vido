using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one toolbar button declaration from a plugin manifest.
/// </summary>
public sealed class ToolbarButtonContribution
{
    /// <summary>
    /// Gets or sets the unique identifier used to reference this toolbar button within the plugin.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tooltip text displayed when the user hovers over this toolbar button.
    /// </summary>
    [JsonPropertyName("tooltip")]
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the button icon, relative to the plugin directory, rendered on the toolbar.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the sort priority that controls where this button appears relative to other toolbar buttons (lower values appear first).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}
