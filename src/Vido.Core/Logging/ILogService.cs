namespace Vido.Core.Logging;

/// <summary>
/// Severity levels for log entries.
/// </summary>
public enum LogLevel
{
    /// <summary>Verbose diagnostic information for developers.</summary>
    Debug,

    /// <summary>General informational messages about application flow.</summary>
    Info,

    /// <summary>Potentially harmful conditions that merit attention.</summary>
    Warning,

    /// <summary>Error events that indicate failures or unexpected behavior.</summary>
    Error
}

/// <summary>
/// A single log entry.
/// </summary>
public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message, string? Source = null);

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

    /// <summary>Logs a message at <see cref="LogLevel.Debug"/> level.</summary>
    void Debug(string message, string? source = null);

    /// <summary>Logs a message at <see cref="LogLevel.Info"/> level.</summary>
    void Info(string message, string? source = null);

    /// <summary>Logs a message at <see cref="LogLevel.Warning"/> level.</summary>
    void Warning(string message, string? source = null);

    /// <summary>Logs a message at <see cref="LogLevel.Error"/> level.</summary>
    void Error(string message, string? source = null);

    /// <summary>
    /// Clears all accumulated log entries.
    /// </summary>
    void Clear();
}
