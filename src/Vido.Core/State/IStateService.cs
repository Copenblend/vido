namespace Vido.Core.State;

/// <summary>
/// Manages application state persistence (window geometry, last session info).
/// State is saved to %APPDATA%/Vido/state.json.
/// </summary>
public interface IStateService
{
    /// <summary>
    /// The current in-memory state.
    /// </summary>
    AppState Current { get; }

    /// <summary>
    /// Loads state from disk. If the file doesn't exist, uses defaults.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves state to disk immediately.
    /// </summary>
    Task SaveAsync();
}
