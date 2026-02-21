using System.Text.Json;
using Vido.Core.Plugin;

namespace Vido.PluginHost;

/// <summary>
/// Per-plugin settings store backed by a JSON file at
/// <c>%APPDATA%/Vido/plugins/{pluginId}/settings.json</c>.
/// Loads lazily on first access; saves on every write.
/// Thread-safe.
/// </summary>
public sealed class PluginSettingsStore : IPluginSettingsStore
{
    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private Dictionary<string, JsonElement> _store = [];
    private bool _loaded;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public event Action<string>? SettingChanged;

    public PluginSettingsStore(string pluginId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pluginDir = Path.Combine(appData, "Vido", "plugins", pluginId);
        _settingsFilePath = Path.Combine(pluginDir, "settings.json");
    }

    /// <summary>
    /// Creates a settings store backed by a specific file path (for testing).
    /// </summary>
    internal PluginSettingsStore(string pluginId, string settingsFilePath)
    {
        _ = pluginId; // unused, path is explicit
        _settingsFilePath = settingsFilePath;
    }

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

    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            EnsureLoaded();

            var json = JsonSerializer.SerializeToElement(value, s_jsonOptions);
            _store[key] = json;
            Save();
        }

        SettingChanged?.Invoke(key);
    }

    public bool Reset(string key)
    {
        bool removed;
        lock (_lock)
        {
            EnsureLoaded();
            removed = _store.Remove(key);
            if (removed)
                Save();
        }

        if (removed)
            SettingChanged?.Invoke(key);

        return removed;
    }

    public void ResetAll()
    {
        List<string> keys;
        lock (_lock)
        {
            EnsureLoaded();
            keys = [.. _store.Keys];
            _store.Clear();
            Save();
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

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_store, s_jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Best-effort save — don't crash the plugin
        }
    }
}
