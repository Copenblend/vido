using System.Text.Json;
using System.Text.Json.Serialization;
using Vido.Core.State;

namespace Vido.Services.State;

/// <summary>
/// JSON-based state persistence to %APPDATA%/Vido/state.json.
/// Stores implicit application state (window geometry, last session, etc.).
/// </summary>
public sealed class StateService : IStateService
{
    private static readonly string StateDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vido");

    private static readonly string StatePath =
        Path.Combine(StateDir, "state.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public AppState Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(StatePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(StatePath);
            var loaded = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
            if (loaded is not null)
                Current = loaded;
        }
        catch
        {
            // Corrupted file — use defaults
            Current = new AppState();
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(StateDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(StatePath, json);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
