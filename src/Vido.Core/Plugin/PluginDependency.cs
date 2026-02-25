using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Declares a dependency on another plugin with a minimum version requirement.
/// Used in <see cref="PluginManifest.Dependencies"/>.
/// </summary>
public sealed class PluginDependency
{
    /// <summary>Plugin ID of the required dependency (e.g. "com.vido.osr2-plus").</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Minimum required version (inclusive) as a dotted version string (e.g. "4.0.0").
    /// The installed dependency's version must be ≥ this value.
    /// </summary>
    [JsonPropertyName("minVersion")]
    public string MinVersion { get; set; } = string.Empty;
}
