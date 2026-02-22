using System.Text.Json;
using System.Text.Json.Serialization;
using Vido.Core.State;

namespace Vido.Services.State;

/// <summary>
/// JSON-based state persistence to %APPDATA%/Vido/state.json.
/// Stores implicit application state (window geometry, last session, etc.).
/// Supports debounced saving — multiple rapid <see cref="QueueSave"/> calls
/// coalesce into a single disk write after 500ms of inactivity.
/// </summary>
public sealed class StateService : IStateService, IDisposable
{
    private static readonly string DefaultStateDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vido");

    private readonly string _stateDir;
    private readonly string _statePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly object _debounceGuard = new();
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 500;

    /// <summary>
    /// Creates a state service that persists to the default %APPDATA%/Vido directory.
    /// </summary>
    public StateService() : this(DefaultStateDir) { }

    /// <summary>
    /// Creates a state service that persists to the specified directory.
    /// Used for testing with isolated temp directories.
    /// </summary>
    public StateService(string stateDirectory)
    {
        _stateDir = stateDirectory;
        _statePath = Path.Combine(_stateDir, "state.json");
    }

    public AppState Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(_statePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_statePath);
            var loaded = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
            if (loaded is not null)
                Current = loaded;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            // Corrupted or inaccessible file — use defaults
            Current = new AppState();
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
            Directory.CreateDirectory(_stateDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(_statePath, json);
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
