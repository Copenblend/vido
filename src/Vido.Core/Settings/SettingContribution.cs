namespace Vido.Core.Settings;

/// <summary>
/// Describes a single setting for display in the settings UI.
/// Each contribution specifies the key, type, title, description, default value,
/// and optional enum values, section grouping, and validation rule.
/// </summary>
public sealed class SettingContribution
{
    /// <summary>
    /// Unique setting key (e.g. "playback.volume").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Setting type: boolean, string, number, enum, stringList, folderPath.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Display title shown in the settings UI.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Description text shown below the setting control.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Default value for this setting. May be a <see cref="System.Text.Json.JsonElement"/>.
    /// </summary>
    public object? Default { get; init; }

    /// <summary>
    /// Allowed values for enum-type settings.
    /// </summary>
    public IReadOnlyList<string> EnumValues { get; init; } = [];

    /// <summary>
    /// Optional section name for grouping related settings within a category.
    /// </summary>
    public string? Section { get; init; }

    /// <summary>
    /// Optional validation rule identifier (e.g. "url").
    /// </summary>
    public string? Validation { get; init; }
}
