using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one panel tab declaration used by both bottom and right panel contributions.
/// </summary>
public sealed class PanelContribution
{
    /// <summary>
    /// Gets or sets the unique identifier used to reference this panel tab within the plugin.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text shown on this panel's tab header.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort priority that controls the tab position relative to other panel tabs (lower values appear first).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 100;
}
