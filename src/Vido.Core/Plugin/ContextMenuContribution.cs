using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one context menu declaration from a plugin manifest.
/// </summary>
public sealed class ContextMenuContribution
{
    /// <summary>
    /// Gets or sets the unique identifier used to reference this context menu item within the plugin.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display text shown for this context menu item.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of file extensions this menu item applies to; an empty list means the item appears for all files.
    /// </summary>
    [JsonPropertyName("fileExtensions")]
    public List<string> FileExtensions { get; set; } = [];

    /// <summary>
    /// Gets or sets the sort priority that controls where this item appears relative to other context menu items (lower values appear first).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}
