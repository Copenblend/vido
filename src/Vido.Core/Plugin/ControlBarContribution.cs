using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one control bar item declaration from a plugin manifest.
/// </summary>
public sealed class ControlBarContribution
{
    /// <summary>
    /// Gets or sets the unique identifier used to reference this control bar item within the plugin.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tooltip text displayed when the user hovers over this control bar item.
    /// </summary>
    [JsonPropertyName("tooltip")]
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort priority that controls where this item appears relative to other control bar items (lower values appear first).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}
