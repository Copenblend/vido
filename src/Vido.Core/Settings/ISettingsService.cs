namespace Vido.Core.Settings;

/// <summary>
/// Manages application settings persistence.
/// Settings are saved to %APPDATA%/Vido/settings.json with debounced writes.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// The current in-memory settings. Modify properties directly,
    /// then call <see cref="QueueSave"/> to persist.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Loads settings from disk. If the file doesn't exist, uses defaults.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Queues a debounced save to disk (500ms delay).
    /// Multiple rapid calls coalesce into a single write.
    /// </summary>
    void QueueSave();

    /// <summary>
    /// Saves settings to disk immediately, bypassing the debounce.
    /// Used during application shutdown.
    /// </summary>
    Task SaveAsync();
}
