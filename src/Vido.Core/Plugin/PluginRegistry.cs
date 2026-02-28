using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents the top-level JSON structure of a plugin registry document.
/// </summary>
public sealed class PluginRegistry
{
    /// <summary>
    /// Gets or sets the human-readable name that identifies this plugin registry to users.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of plugin entries published in this registry, each describing an installable plugin and its available versions.
    /// </summary>
    [JsonPropertyName("plugins")]
    public List<PluginRegistryEntry> Plugins { get; set; } = [];
}
