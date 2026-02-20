namespace Vido.Core.Logging;

/// <summary>
/// Severity levels for log entries.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
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

    void Debug(string message, string? source = null);
    void Info(string message, string? source = null);
    void Warning(string message, string? source = null);
    void Error(string message, string? source = null);

    /// <summary>
    /// Clears all accumulated log entries.
    /// </summary>
    void Clear();
}
