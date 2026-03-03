namespace Vido.Core.Settings;

/// <summary>
/// Describes a single setting for display in the Settings UI.
/// Replaces the plugin-era <c>SettingContribution</c> with a compile-time-safe definition
/// that includes typed getter/setter delegates for direct property access.
/// </summary>
/// <param name="Key">Unique setting key (e.g. "osr2.outputRate").</param>
/// <param name="Type">Setting type: "boolean", "number", "string", "enum", "stringList".</param>
/// <param name="DefaultValue">Default value used when resetting the setting.</param>
/// <param name="Title">Human-readable title shown in the Settings UI.</param>
/// <param name="Description">Descriptive text shown below the setting control.</param>
/// <param name="Section">Optional section header to group related settings.</param>
/// <param name="EnumValues">Allowed values when <paramref name="Type"/> is "enum".</param>
/// <param name="Validation">Optional numeric constraints for "number" type settings.</param>
/// <param name="Getter">Delegate that reads the current value from <see cref="AppSettings"/>.</param>
/// <param name="Setter">Delegate that writes a new value to <see cref="AppSettings"/>.</param>
public sealed record SettingDefinition(
    string Key,
    string Type,
    object? DefaultValue,
    string Title,
    string Description,
    string? Section = null,
    IReadOnlyList<string>? EnumValues = null,
    SettingValidation? Validation = null,
    Func<AppSettings, object?>? Getter = null,
    Action<AppSettings, object?>? Setter = null);

/// <summary>
/// Validation constraints for number-type settings.
/// </summary>
/// <param name="Min">Minimum allowed value (inclusive), or <c>null</c> for no lower bound.</param>
/// <param name="Max">Maximum allowed value (inclusive), or <c>null</c> for no upper bound.</param>
public sealed record SettingValidation(double? Min = null, double? Max = null);
