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
    private int _debounceVersion;
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
            {
                Current = loaded;
                EnsureOfficialRegistryUrl();
            }
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            // Corrupted or inaccessible file — use defaults
            Current = new AppSettings();
        }
    }

    /// <summary>
    /// Ensures the official Vido registry URL is always present as the first entry
    /// in <see cref="AppSettings.PluginRegistryUrls"/>. Fixes settings files where
    /// the official URL was accidentally removed or replaced.
    /// </summary>
    private void EnsureOfficialRegistryUrl()
    {
        var urls = Current.PluginRegistryUrls;
        var officialUrl = AppSettings.OfficialRegistryUrl;

        if (urls.Count == 0 ||
            !urls[0].Equals(officialUrl, StringComparison.OrdinalIgnoreCase))
        {
            // Remove any existing occurrences of the official URL in wrong positions
            urls.RemoveAll(u => u.Equals(officialUrl, StringComparison.OrdinalIgnoreCase));
            urls.Insert(0, officialUrl);
        }
    }

    public void QueueSave()
    {
        // Increment the version counter. Any in-flight debounce with an older
        // version will see the mismatch and skip the save. This avoids
        // CancellationTokenSource + Task.Delay which produce first-chance
        // TaskCanceledException noise in debugger output.
        var version = Interlocked.Increment(ref _debounceVersion);

        _ = Task.Run(async () =>
        {
            await Task.Delay(DebounceMs);
            // Only save if no newer QueueSave was called during the delay
            if (Interlocked.CompareExchange(ref _debounceVersion, 0, 0) == version)
                await SaveAsync();
        });
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
        // Bump version to suppress any in-flight debounce
        Interlocked.Increment(ref _debounceVersion);
        _saveLock.Dispose();
    }
}
