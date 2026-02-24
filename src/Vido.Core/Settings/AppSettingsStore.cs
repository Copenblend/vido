using Vido.Core.Plugin;

namespace Vido.Core.Settings;

/// <summary>
/// Adapts <see cref="AppSettings"/> properties to the <see cref="IPluginSettingsStore"/>
/// interface, allowing reuse of <see cref="SettingDisplayItem"/> for app settings display.
/// Each setting key maps to a specific strongly-typed property on <see cref="AppSettings"/>.
/// </summary>
public sealed class AppSettingsStore : IPluginSettingsStore
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, Func<object>> _getters;
    private readonly Dictionary<string, Action<object>> _setters;

    public event Action<string>? SettingChanged;

    public AppSettingsStore(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _getters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["playback.volume"] = () => Math.Round(Settings.Volume * 100),
            ["playback.speed"] = () => FormatSpeed(Settings.PlaybackSpeed),
            ["playback.loop"] = () => Settings.LoopPlayback,
            ["explorer.showHiddenFiles"] = () => Settings.ShowHiddenFiles,
            ["screenshot.enabled"] = () => Settings.ScreenshotEnabled,
            ["screenshot.directory"] = () => Settings.ScreenshotDirectory,
            ["plugins.registryUrls"] = () => GetCustomRegistryUrls(),
        };

        _setters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["playback.volume"] = v =>
            {
                Settings.Volume = Math.Clamp(Convert.ToDouble(v) / 100.0, 0, 1);
                _settingsService.QueueSave();
            },
            ["playback.speed"] = v =>
            {
                Settings.PlaybackSpeed = ParseSpeed(v?.ToString() ?? "1.0x");
                _settingsService.QueueSave();
            },
            ["playback.loop"] = v =>
            {
                Settings.LoopPlayback = Convert.ToBoolean(v);
                _settingsService.QueueSave();
            },
            ["explorer.showHiddenFiles"] = v =>
            {
                Settings.ShowHiddenFiles = Convert.ToBoolean(v);
                _settingsService.QueueSave();
            },
            ["screenshot.enabled"] = v =>
            {
                Settings.ScreenshotEnabled = Convert.ToBoolean(v);
                _settingsService.QueueSave();
            },
            ["screenshot.directory"] = v =>
            {
                Settings.ScreenshotDirectory = v?.ToString() ?? string.Empty;
                _settingsService.QueueSave();
            },
            ["plugins.registryUrls"] = v =>
            {
                SetCustomRegistryUrls(v as List<string> ?? []);
                _settingsService.QueueSave();
            },
        };
    }

    /// <summary>Current settings instance (resolved live to handle reloads).</summary>
    private AppSettings Settings => _settingsService.Current;

    public T Get<T>(string key, T defaultValue)
    {
        if (!_getters.TryGetValue(key, out var getter))
            return defaultValue;

        var value = getter();
        if (value is T typed)
            return typed;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        if (value is null || !_setters.TryGetValue(key, out var setter))
            return;

        setter(value);
        SettingChanged?.Invoke(key);
    }

    public bool Reset(string key)
    {
        // App settings don't support individual reset via this interface.
        return false;
    }

    public void ResetAll()
    {
        // App settings don't support bulk reset via this interface.
    }

    /// <summary>
    /// Returns the custom registry URLs (everything except the official URL at index 0).
    /// Ensures the official URL is always present as index 0.
    /// </summary>
    private List<string> GetCustomRegistryUrls()
    {
        EnsureOfficialUrlPresent();
        return Settings.PluginRegistryUrls
            .Where(u => !u.Equals(AppSettings.OfficialRegistryUrl, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Replaces the custom registry URLs (indices 1+) with the given list.
    /// The official URL at index 0 is never modified.
    /// </summary>
    private void SetCustomRegistryUrls(List<string> customUrls)
    {
        EnsureOfficialUrlPresent();
        var urls = Settings.PluginRegistryUrls;

        // Remove all entries after the official URL
        while (urls.Count > 1)
            urls.RemoveAt(urls.Count - 1);

        // Add the new custom URLs
        foreach (var url in customUrls)
        {
            if (!string.IsNullOrWhiteSpace(url))
                urls.Add(url);
        }
    }

    /// <summary>
    /// Ensures the official registry URL is always the first entry.
    /// Fixes settings files where it was missing.
    /// </summary>
    private void EnsureOfficialUrlPresent()
    {
        var urls = Settings.PluginRegistryUrls;
        if (urls.Count == 0 ||
            !urls[0].Equals(AppSettings.OfficialRegistryUrl, StringComparison.OrdinalIgnoreCase))
        {
            urls.Insert(0, AppSettings.OfficialRegistryUrl);
        }
    }

    private static string FormatSpeed(double speed) => speed switch
    {
        0.25 => "0.25x",
        0.5 => "0.5x",
        1.0 => "1.0x",
        1.5 => "1.5x",
        2.0 => "2.0x",
        _ => $"{speed}x"
    };

    private static double ParseSpeed(string value) => value switch
    {
        "0.25x" => 0.25,
        "0.5x" => 0.5,
        "1.0x" => 1.0,
        "1.5x" => 1.5,
        "2.0x" => 2.0,
        _ => 1.0
    };
}
