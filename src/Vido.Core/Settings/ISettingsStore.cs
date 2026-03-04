namespace Vido.Core.Settings;

/// <summary>
/// Key-value store interface for reading and writing application settings.
/// Provides a generic get/set API with change notification.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Retrieves the current value of a setting by key, converting it to <typeparamref name="T"/>.
    /// Returns <paramref name="defaultValue"/> if the key is unknown or conversion fails.
    /// </summary>
    /// <param name="key">The setting key (e.g. "playback.volume").</param>
    /// <param name="defaultValue">Value returned when the key is not found or conversion fails.</param>
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Updates a setting value by key, persists the change, and raises <see cref="SettingChanged"/>.
    /// </summary>
    /// <param name="key">The setting key (e.g. "playback.volume").</param>
    /// <param name="value">The new value to apply.</param>
    void Set<T>(string key, T value);

    /// <summary>
    /// Raised when a setting value changes. The argument is the setting key.
    /// </summary>
    event Action<string>? SettingChanged;
}
