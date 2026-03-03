using System.Text.Json;
using Vido.Core.Settings;

namespace Vido.Services.Settings;

/// <summary>
/// JSON-based settings persistence to %APPDATA%/Vido/settings.json.
/// Supports debounced saving â€” multiple rapid <see cref="QueueSave"/> calls
/// coalesce into a single disk write after 500ms of inactivity.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private static readonly string DefaultSettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vido");

    private readonly string _settingsDir;
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private Timer? _debounceTimer;
    private bool _disposed;
    private const int DebounceMs = 500;

    /// <summary>
    /// Creates a settings service that persists to the default %APPDATA%/Vido directory.
    /// </summary>
    public SettingsService() : this(DefaultSettingsDir) { }

    /// <summary>
    /// Creates a settings service that persists to the specified directory.
    /// Used for testing with isolated temp directories.
    /// </summary>
    /// <param name="settingsDirectory">The directory path where <c>settings.json</c> will be read from and written to.</param>
    public SettingsService(string settingsDirectory)
    {
        _settingsDir = settingsDirectory;
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
    }

    /// <summary>
    /// Holds the current application settings, initially loaded from disk or populated with defaults.
    /// </summary>
    public AppSettings Current { get; private set; } = new();

    /// <summary>
    /// Reads <c>settings.json</c> from disk and overwrites <see cref="Current"/> with the deserialized values.
    /// Falls back to defaults if the file is missing, corrupt, or inaccessible.
    /// </summary>
    public async Task LoadAsync()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is not null)
            {
                Current = loaded;
            }
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            // Corrupted or inaccessible file â€” use defaults
            Current = new AppSettings();
        }
    }

    /// <summary>
    /// Schedules a debounced save — if no additional <see cref="QueueSave"/> call arrives within 500 ms,
    /// the current settings are persisted to disk via <see cref="SaveAsync"/>.
    /// </summary>
    public void QueueSave()
    {
        if (_disposed)
            return;

        if (_debounceTimer is null)
        {
            _debounceTimer = new Timer(static state =>
            {
                var service = (SettingsService)state!;
                if (service._disposed)
                    return;

                _ = service.SaveAsync();
            }, this, DebounceMs, Timeout.Infinite);
            return;
        }

        _debounceTimer.Change(DebounceMs, Timeout.Infinite);
    }

    /// <summary>
    /// Serializes <see cref="Current"/> to JSON and writes it to <c>settings.json</c> on disk,
    /// creating the settings directory if it does not exist.
    /// </summary>
    public async Task SaveAsync()
    {
        if (_disposed)
            return;

        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        finally
        {
            _saveLock.Release();
        }
    }
    
    /// <summary>
    /// Cancels pending debounced saves and releases internal synchronization resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _saveLock.Dispose();
    }
}
