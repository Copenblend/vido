namespace Vido.Core.Settings;

/// <summary>
/// Exposes <see cref="AppSettings"/> properties through a key-value store interface,
/// allowing reuse of SettingDisplayItem for app settings display.
/// Each setting key maps to a specific strongly-typed property on <see cref="AppSettings"/>.
/// </summary>
public sealed class AppSettingsStore : ISettingsStore
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, Func<object>> _getters;
    private readonly Dictionary<string, Action<object>> _setters;
    /// <summary>
    /// Occurs when SettingChanged is raised.
    /// </summary>

    public event Action<string>? SettingChanged;
    /// <summary>
    /// Creates the settings store, wiring up getter/setter maps for each
    /// supported setting key to the underlying <see cref="AppSettings"/> properties.
    /// </summary>
    /// <param name="settingsService">The settings service providing the live <see cref="AppSettings"/> instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="settingsService"/> is null.</exception>
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
            ["osr2.connectionMode"] = () => Settings.Osr2ConnectionMode,
            ["osr2.udpPort"] = () => (double)Settings.Osr2UdpPort,
            ["osr2.baudRate"] = () => Settings.Osr2BaudRate.ToString(),
            ["osr2.outputRate"] = () => (double)Settings.Osr2OutputRate,
            ["osr2.globalOffset"] = () => (double)Settings.Osr2GlobalOffset,
            ["osr2.visualizerWindowDuration"] = () => Settings.Osr2VisualizerWindowDuration.ToString(),
            ["general.toastDuration"] = () => Settings.ToastDurationSeconds,
            ["playback.fullscreenAutoHide"] = () => Settings.FullscreenAutoHideSeconds,
            ["playback.fullscreenShowVideoName"] = () => Settings.FullscreenShowVideoName,
            ["playback.resumePlaybackPrompt"] = () => Settings.ResumePlaybackPrompt,
            ["updates.autocheck"] = () => Settings.AutoCheckUpdates,
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
            ["osr2.connectionMode"] = v =>
            {
                Settings.Osr2ConnectionMode = v?.ToString() ?? "UDP";
                _settingsService.QueueSave();
            },
            ["osr2.udpPort"] = v =>
            {
                Settings.Osr2UdpPort = (int)Math.Clamp(Convert.ToDouble(v), 1, 65535);
                _settingsService.QueueSave();
            },
            ["osr2.baudRate"] = v =>
            {
                if (int.TryParse(v?.ToString(), out var baud))
                    Settings.Osr2BaudRate = baud;
                _settingsService.QueueSave();
            },
            ["osr2.outputRate"] = v =>
            {
                Settings.Osr2OutputRate = (int)Math.Clamp(Convert.ToDouble(v), 30, 200);
                _settingsService.QueueSave();
            },
            ["osr2.globalOffset"] = v =>
            {
                Settings.Osr2GlobalOffset = (int)Math.Clamp(Convert.ToDouble(v), -500, 500);
                _settingsService.QueueSave();
            },
            ["osr2.visualizerWindowDuration"] = v =>
            {
                if (int.TryParse(v?.ToString(), out var dur))
                    Settings.Osr2VisualizerWindowDuration = dur;
                _settingsService.QueueSave();
            },
            ["general.toastDuration"] = v =>
            {
                Settings.ToastDurationSeconds = Math.Clamp(Convert.ToDouble(v), 1.0, 10.0);
                _settingsService.QueueSave();
            },
            ["playback.fullscreenAutoHide"] = v =>
            {
                Settings.FullscreenAutoHideSeconds = Math.Clamp(Convert.ToDouble(v), 1.0, 30.0);
                _settingsService.QueueSave();
            },
            ["playback.fullscreenShowVideoName"] = v =>
            {
                Settings.FullscreenShowVideoName = Convert.ToBoolean(v);
                _settingsService.QueueSave();
            },
            ["playback.resumePlaybackPrompt"] = v =>
            {
                Settings.ResumePlaybackPrompt = Convert.ToBoolean(v);
                _settingsService.QueueSave();
            },
            ["updates.autocheck"] = v =>
            {
                Settings.AutoCheckUpdates = Convert.ToBoolean(v);
                _settingsService.QueueSave();
            },
        };
    }

    /// <summary>
    /// Current settings instance (resolved live to handle reloads).
    /// </summary>
    private AppSettings Settings => _settingsService.Current;
    /// <summary>
    /// Retrieves the current value of a setting by key, converting it to <typeparamref name="T"/>.
    /// Returns <paramref name="defaultValue"/> if the key is unknown or conversion fails.
    /// </summary>
    /// <param name="key">The setting key (e.g. "playback.volume").</param>
    /// <param name="defaultValue">Value returned when the key is not found or conversion fails.</param>
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
    /// <summary>
    /// Updates a setting value by key, persists the change, and raises <see cref="SettingChanged"/>.
    /// No-ops if the key is unrecognized or the value is null.
    /// </summary>
    /// <param name="key">The setting key (e.g. "playback.volume").</param>
    /// <param name="value">The new value to apply.</param>
    public void Set<T>(string key, T value)
    {
        if (value is null || !_setters.TryGetValue(key, out var setter))
            return;

        setter(value);
        SettingChanged?.Invoke(key);
    }
    /// <summary>
    /// Attempts to reset a single setting to its default. Currently unsupported
    /// for app settings; always returns false.
    /// </summary>
    /// <param name="key">The setting key to reset.</param>
    public bool Reset(string key)
    {
        // App settings don't support individual reset via this interface.
        return false;
    }
    /// <summary>
    /// Resets all settings to defaults. Currently a no-op for app settings.
    /// </summary>
    public void ResetAll()
    {
        // App settings don't support bulk reset via this interface.
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
