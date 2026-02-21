namespace Vido.Core.Plugin;

/// <summary>
/// Per-plugin settings store. Each plugin gets its own settings file
/// at <c>%APPDATA%/Vido/plugins/&lt;id&gt;/settings.json</c>.
/// </summary>
public interface IPluginSettingsStore
{
    /// <summary>Get a setting value by key, returning <paramref name="defaultValue"/> if not set.</summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>Set a setting value by key. Persisted automatically.</summary>
    void Set<T>(string key, T value);

    /// <summary>Event raised when any setting changes. The string argument is the key.</summary>
    event Action<string>? SettingChanged;
}
