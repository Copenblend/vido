using System.Text.Json;
using Vido.Core.Plugin;

namespace Vido.PluginHost;

/// <summary>
/// Per-plugin settings store backed by a JSON file at
/// <c>%APPDATA%/Vido/plugins/{pluginId}/settings.json</c>.
/// Loads lazily on first access; writes are debounced to reduce disk I/O.
/// Thread-safe.
/// </summary>
public sealed class PluginSettingsStore : IPluginSettingsStore, IDisposable
{
    private const int DefaultDebounceMs = 500;

    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private readonly int _debounceMs;
    private readonly Action? _onSave;

    private Dictionary<string, JsonElement> _store = [];
    private bool _loaded;
    private bool _dirty;
    private bool _disposed;
    private Timer? _debounceTimer;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    /// <summary>
    /// Raised after a setting value is written or removed, providing the affected key.
    /// </summary>
    public event Action<string>? SettingChanged;

    /// <summary>
    /// Creates a settings store for the specified plugin, backed by a JSON file
    /// at <c>%APPDATA%/Vido/plugins/{pluginId}/settings.json</c>.
    /// </summary>
    /// <param name="pluginId">Unique identifier of the plugin whose settings are stored.</param>
    public PluginSettingsStore(string pluginId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pluginDir = Path.Combine(appData, "Vido", "plugins", pluginId);
        _settingsFilePath = Path.Combine(pluginDir, "settings.json");
        _debounceMs = DefaultDebounceMs;
    }

    /// <summary>
    /// Creates a settings store backed by a specific file path (for testing).
    /// </summary>
    internal static PluginSettingsStore ForTesting(string settingsFilePath)
    {
        return new PluginSettingsStore(settingsFilePath, isExplicitPath: true);
    }

    /// <summary>
    /// Creates a settings store backed by a specific file path (for testing),
    /// with an optional debounce interval override and save callback.
    /// </summary>
    internal static PluginSettingsStore ForTesting(string settingsFilePath, int debounceMs, Action? onSave)
    {
        return new PluginSettingsStore(settingsFilePath, isExplicitPath: true, debounceMs, onSave);
    }

    private PluginSettingsStore(
        string filePath,
        bool isExplicitPath,
        int debounceMs = DefaultDebounceMs,
        Action? onSave = null)
    {
        _settingsFilePath = filePath;
        _debounceMs = debounceMs;
        _onSave = onSave;
    }

    /// <summary>
    /// Retrieves a setting value by key, deserializing it to <typeparamref name="T"/>.
    /// Returns <paramref name="defaultValue"/> if the key is not found or deserialization fails.
    /// </summary>
    /// <param name="key">The setting key to look up.</param>
    /// <param name="defaultValue">Value returned when the key is absent or cannot be deserialized.</param>
    public T Get<T>(string key, T defaultValue)
    {
        lock (_lock)
        {
            EnsureLoaded();

            if (!_store.TryGetValue(key, out var element))
                return defaultValue;

            try
            {
                var result = element.Deserialize<T>(s_jsonOptions);
                return result ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    /// <summary>
    /// Stores a setting value under the given key, serializing it to JSON.
    /// Schedules persistence and raises <see cref="SettingChanged"/>.
    /// </summary>
    /// <param name="key">The setting key to write.</param>
    /// <param name="value">The value to serialize and store.</param>
    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            EnsureLoaded();

            var json = JsonSerializer.SerializeToElement(value, s_jsonOptions);
            _store[key] = json;
            _dirty = true;
            QueueSave();
        }

        SettingChanged?.Invoke(key);
    }

    /// <summary>
    /// Removes a single setting by key and persists the change.
    /// Raises <see cref="SettingChanged"/> if the key existed.
    /// </summary>
    /// <param name="key">The setting key to remove.</param>
    public bool Reset(string key)
    {
        bool removed;
        lock (_lock)
        {
            EnsureLoaded();
            removed = _store.Remove(key);
            if (removed)
            {
                _dirty = true;
                QueueSave();
            }
        }

        if (removed)
            SettingChanged?.Invoke(key);

        return removed;
    }

    /// <summary>
    /// Clears all stored settings for this plugin, persists the empty state,
    /// and raises <see cref="SettingChanged"/> for every removed key.
    /// </summary>
    public void ResetAll()
    {
        List<string> keys;
        lock (_lock)
        {
            EnsureLoaded();
            keys = [.. _store.Keys];
            _store.Clear();
            _dirty = true;
            QueueSave();
        }

        foreach (var key in keys)
            SettingChanged?.Invoke(key);
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, s_jsonOptions);
                if (deserialized is not null)
                    _store = deserialized;
            }
        }
        catch
        {
            // If settings are corrupted, start fresh
            _store = [];
        }
    }

    /// <summary>
    /// Schedules persistence after a short delay to coalesce rapid writes.
    /// Must be called while holding <see cref="_lock"/>.
    /// </summary>
    private void QueueSave()
    {
        if (_disposed) return;

        if (_debounceTimer is null)
        {
            _debounceTimer = new Timer(_ =>
            {
                lock (_lock)
                    SaveIfDirty();
            }, null, _debounceMs, Timeout.Infinite);
            return;
        }

        _debounceTimer.Change(_debounceMs, Timeout.Infinite);
    }

    /// <summary>
    /// Immediately persists pending changes, if any.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            SaveIfDirty();
        }
    }

    /// <summary>
    /// Flushes pending changes and disposes timer resources.
    /// </summary>
    public void Dispose()
    {
        Timer? timer;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            timer = _debounceTimer;
            _debounceTimer = null;
            SaveIfDirty();
        }

        timer?.Dispose();
    }

    private void SaveIfDirty()
    {
        if (!_dirty) return;

        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_store, s_jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            _dirty = false;
            _onSave?.Invoke();
        }
        catch
        {
            // Best-effort save â€” don't crash the plugin
        }
    }
}
