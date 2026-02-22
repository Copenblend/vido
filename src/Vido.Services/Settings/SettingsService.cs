using System.Text.Json;
using Vido.Core.Settings;

namespace Vido.Services.Settings;

/// <summary>
/// JSON-based settings persistence to %APPDATA%/Vido/settings.json.
/// Supports debounced saving — multiple rapid <see cref="QueueSave"/> calls
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
    private readonly object _debounceGuard = new();
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 500;

    /// <summary>
    /// Creates a settings service that persists to the default %APPDATA%/Vido directory.
    /// </summary>
    public SettingsService() : this(DefaultSettingsDir) { }

    /// <summary>
    /// Creates a settings service that persists to the specified directory.
    /// Used for testing with isolated temp directories.
    /// </summary>
    public SettingsService(string settingsDirectory)
    {
        _settingsDir = settingsDirectory;
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is not null)
                Current = loaded;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            // Corrupted or inaccessible file — use defaults
            Current = new AppSettings();
        }
    }

    public void QueueSave()
    {
        lock (_debounceGuard)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            // Do NOT pass token to Task.Run — only use it inside the lambda.
            // Passing it to Task.Run causes a first-chance TaskCanceledException
            // when the token is cancelled before the task begins.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceMs, token);
                    await SaveAsync();
                }
                catch (OperationCanceledException)
                {
                    // Debounce cancelled — a newer save was queued
                }
            });
        }
    }

    public async Task SaveAsync()
    {
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

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _saveLock.Dispose();
    }
}
