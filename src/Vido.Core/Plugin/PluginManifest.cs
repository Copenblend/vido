using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Deserialized representation of a plugin's <c>plugin.json</c> manifest.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>
    /// Unique plugin identifier (e.g. "com.example.my-plugin").
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Internal plugin name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// User-facing display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version (semver).
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Short description of the plugin's functionality.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Plugin author name.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// License identifier (e.g. "MIT").
    /// </summary>
    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// Relative path to the plugin's entry-point DLL.
    /// </summary>
    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Fully-qualified class name implementing <see cref="IVidoPlugin"/>.
    /// </summary>
    [JsonPropertyName("pluginClass")]
    public string PluginClass { get; set; } = string.Empty;

    /// <summary>
    /// Minimum Vido version required to run this plugin.
    /// </summary>
    [JsonPropertyName("minVidoVersion")]
    public string MinVidoVersion { get; set; } = string.Empty;

    /// <summary>
    /// Repository URL for the plugin source.
    /// </summary>
    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    /// <summary>
    /// Tags for search/categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Relative path to the plugin icon image (e.g. "Assets/plugin-icon.png").
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// UI contributions declared by the plugin.
    /// </summary>
    [JsonPropertyName("contributes")]
    public PluginContributions Contributes { get; set; } = new();

    /// <summary>
    /// Plugin dependencies — other plugins that must be installed and activated before this one.
    /// Evaluated during activation for topological ordering and version validation.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<PluginDependency> Dependencies { get; set; } = [];
}
