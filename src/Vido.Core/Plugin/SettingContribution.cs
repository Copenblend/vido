using System.Text.Json.Serialization;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents one plugin setting declaration from a plugin manifest.
/// </summary>
public sealed class SettingContribution
{
    /// <summary>
    /// Gets or sets the unique key used to persist and retrieve this setting value.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data type of this setting (e.g. <c>boolean</c>, <c>string</c>, <c>number</c>, <c>enum</c>, <c>folderPath</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>
    /// Gets or sets the initial value applied when no persisted user value exists for this setting.
    /// </summary>
    [JsonPropertyName("default")]
    public object? Default { get; set; }

    /// <summary>
    /// Gets or sets the human-readable label shown next to this setting in the settings UI.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the explanatory text displayed below the setting title to describe its purpose to users.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the allowed values presented in a dropdown when the setting type is <c>enum</c>.
    /// </summary>
    [JsonPropertyName("enumValues")]
    public List<string> EnumValues { get; set; } = [];

    /// <summary>
    /// Gets or sets the visual section heading used to group related settings together in the settings UI.
    /// </summary>
    [JsonPropertyName("section")]
    public string? Section { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin's default value should overwrite any previously persisted user value on plugin load.
    /// </summary>
    [JsonPropertyName("forceOverride")]
    public bool ForceOverride { get; set; }

    /// <summary>
    /// Gets or sets the name of a validation rule applied to user input for this setting (e.g. format or range constraints).
    /// </summary>
    [JsonPropertyName("validation")]
    public string? Validation { get; set; }

    /// <summary>
    /// Gets the set of recognized setting type identifiers that the manifest parser accepts.
    /// </summary>
    public static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "boolean", "string", "number", "enum", "folderPath"
    };
}
