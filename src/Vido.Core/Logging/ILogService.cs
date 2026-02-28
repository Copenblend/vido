namespace Vido.Core.Logging;

/// <summary>
/// Thread-safe observable logging service.
/// Log entries are stored in memory and can be observed for UI display.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// All log entries accumulated during this session.
    /// </summary>
    IReadOnlyList<LogEntry> Entries { get; }

    /// <summary>
    /// Raised on the UI thread when a new log entry is added.
    /// </summary>
    event Action<LogEntry>? EntryAdded;

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Debug"/> level.
    /// </summary>
    /// <param name="message">The log message text.</param>
    /// <param name="source">Optional source identifier for filtering (e.g. plugin name).</param>
    void Debug(string message, string? source = null);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Info"/> level.
    /// </summary>
    /// <param name="message">The log message text.</param>
    /// <param name="source">Optional source identifier for filtering (e.g. plugin name).</param>
    void Info(string message, string? source = null);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Warning"/> level.
    /// </summary>
    /// <param name="message">The log message text.</param>
    /// <param name="source">Optional source identifier for filtering (e.g. plugin name).</param>
    void Warning(string message, string? source = null);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Error"/> level.
    /// </summary>
    /// <param name="message">The log message text.</param>
    /// <param name="source">Optional source identifier for filtering (e.g. plugin name).</param>
    void Error(string message, string? source = null);

    /// <summary>
    /// Clears all accumulated log entries.
    /// </summary>
    void Clear();
}
