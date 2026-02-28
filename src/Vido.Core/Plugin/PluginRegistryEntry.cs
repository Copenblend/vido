using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents a plugin entry from a remote or local plugin registry.
/// </summary>
public sealed class PluginRegistryEntry
{
    /// <summary>
    /// Unique plugin identifier (must match the plugin manifest id).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// User-facing display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Short description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Plugin author / publisher name.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Current version available in the registry (semver).
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// License identifier.
    /// </summary>
    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// Tags for search / categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// URL to download the plugin zip archive.
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to the plugin icon image.
    /// </summary>
    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// URL to the plugin README.md content (shown in the Details tab).
    /// </summary>
    [JsonPropertyName("readmeUrl")]
    public string? ReadmeUrl { get; set; }

    /// <summary>
    /// URL to the plugin CHANGELOG.md content (shown in the Changelog tab).
    /// </summary>
    [JsonPropertyName("changelogUrl")]
    public string? ChangelogUrl { get; set; }

    /// <summary>
    /// Repository URL.
    /// </summary>
    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    /// <summary>
    /// ISO 8601 date string for last update.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; set; }

    /// <summary>
    /// Dependencies required by this plugin (must be installed before this plugin).
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<PluginDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Minimum Vido version required to run this plugin.
    /// </summary>
    [JsonPropertyName("minVidoVersion")]
    public string? MinVidoVersion { get; set; }

    /// <summary>
    /// The registry URL this entry was fetched from.
    /// Set at runtime during registry loading — not serialized from registry JSON.
    /// </summary>
    [JsonIgnore]
    public string RegistryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the registry this entry came from.
    /// Set at runtime during registry loading — not serialized from registry JSON.
    /// </summary>
    [JsonIgnore]
    public string RegistryName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this entry is from the official Vido plugin registry.
    /// </summary>
    [JsonIgnore]
    public bool IsOfficial { get; set; }
}
