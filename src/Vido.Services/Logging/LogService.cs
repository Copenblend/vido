using Vido.Core.Logging;

namespace Vido.Services.Logging;

/// <summary>
/// Thread-safe in-memory logging service.
/// Entries are stored and the <see cref="EntryAdded"/> event is raised
/// for each new entry (on the calling thread â€” UI marshalling is the
/// consumer's responsibility).
/// </summary>
public sealed class LogService : ILogService
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _lock = new();
    private IReadOnlyList<LogEntry> _snapshot = Array.Empty<LogEntry>();
    private volatile bool _snapshotDirty = true;

    /// <summary>
    /// Returns whether the specified log level is currently enabled.
    /// </summary>
    /// <param name="level">The level to query.</param>
    /// <returns>Always <c>true</c> for the in-memory log service.</returns>
    public bool IsEnabled(LogLevel level) => true;

    /// <summary>
    /// Returns a snapshot of all log entries recorded so far, safe to enumerate from any thread.
    /// Rebuilds the snapshot only when new entries have been added since the last read.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            if (_snapshotDirty)
            {
                lock (_lock)
                {
                    if (_snapshotDirty)
                    {
                        Volatile.Write(ref _snapshot, _entries.ToArray());
                        _snapshotDirty = false;
                    }
                }
            }
            return Volatile.Read(ref _snapshot);
        }
    }

    /// <summary>
    /// Raised on the calling thread immediately after a new entry is appended to the log.
    /// </summary>
    public event Action<LogEntry>? EntryAdded;

    /// <summary>
    /// Records a <see cref="LogLevel.Debug"/>-level entry with the given message and optional source.
    /// </summary>
    /// <param name="message">The diagnostic message to log.</param>
    /// <param name="source">An optional label identifying the subsystem that produced the message.</param>
    public void Debug(string message, string? source = null) => Log(LogLevel.Debug, message, source);

    /// <summary>
    /// Records a <see cref="LogLevel.Info"/>-level entry with the given message and optional source.
    /// </summary>
    /// <param name="message">The informational message to log.</param>
    /// <param name="source">An optional label identifying the subsystem that produced the message.</param>
    public void Info(string message, string? source = null) => Log(LogLevel.Info, message, source);

    /// <summary>
    /// Records a <see cref="LogLevel.Warning"/>-level entry with the given message and optional source.
    /// </summary>
    /// <param name="message">The warning message describing a potential problem.</param>
    /// <param name="source">An optional label identifying the subsystem that produced the message.</param>
    public void Warning(string message, string? source = null) => Log(LogLevel.Warning, message, source);

    /// <summary>
    /// Records a <see cref="LogLevel.Error"/>-level entry with the given message and optional source.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="source">An optional label identifying the subsystem that produced the message.</param>
    public void Error(string message, string? source = null) => Log(LogLevel.Error, message, source);
    
    /// <summary>
    /// Removes all previously recorded log entries from the in-memory store.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            Volatile.Write(ref _snapshot, Array.Empty<LogEntry>());
            _snapshotDirty = false;
        }
    }

    private void Log(LogLevel level, string message, string? source)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, message, source);

        lock (_lock)
        {
            _entries.Add(entry);
            _snapshotDirty = true;
        }

        EntryAdded?.Invoke(entry);
    }
}
