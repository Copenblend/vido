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
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vido");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 500;

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(SettingsPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is not null)
                Current = loaded;
        }
        catch
        {
            // Corrupted file — use defaults
            Current = new AppSettings();
        }
    }

    public void QueueSave()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs, token);
                await SaveAsync();
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelled — a newer save was queued
            }
        }, token);
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(SettingsPath, json);
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
