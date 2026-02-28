using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one status bar item declaration from a plugin manifest.
/// </summary>
public sealed class StatusBarContribution
{
    /// <summary>
    /// Gets or sets the unique identifier used to reference this status bar item within the plugin.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name shown in visibility toggle menus so users can show or hide this status bar item.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alignment side of the status bar where this item is placed (<c>left</c> or <c>right</c>).
    /// </summary>
    [JsonPropertyName("position")]
    public string Position { get; set; } = "right";

    /// <summary>
    /// Gets or sets the sort priority that controls where this item appears relative to other status bar items on the same side (lower values appear first).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}
