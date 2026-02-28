using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one file handler declaration from a plugin manifest.
/// </summary>
public sealed class FileHandlerContribution
{
    /// <summary>
    /// Gets or sets the list of file extensions (e.g. <c>.mp4</c>, <c>.mkv</c>) this handler supports.
    /// </summary>
    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = [];

    /// <summary>
    /// Gets or sets the action performed when a matching file is encountered; currently only <c>open</c> is supported.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "open";
}
