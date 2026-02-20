using System.Collections.Concurrent;
using Vido.Core.Logging;

namespace Vido.Services.Logging;

/// <summary>
/// Thread-safe in-memory logging service.
/// Entries are stored and the <see cref="EntryAdded"/> event is raised
/// for each new entry (on the calling thread — UI marshalling is the
/// consumer's responsibility).
/// </summary>
public sealed class LogService : ILogService
{
    private readonly ConcurrentBag<LogEntry> _entries = [];
    private readonly object _eventLock = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            // Return chronologically ordered snapshot
            return _entries.Reverse().ToList().AsReadOnly();
        }
    }

    public event Action<LogEntry>? EntryAdded;

    public void Debug(string message, string? source = null) => Log(LogLevel.Debug, message, source);
    public void Info(string message, string? source = null) => Log(LogLevel.Info, message, source);
    public void Warning(string message, string? source = null) => Log(LogLevel.Warning, message, source);
    public void Error(string message, string? source = null) => Log(LogLevel.Error, message, source);

    public void Clear()
    {
        // ConcurrentBag doesn't have Clear — drain it
        while (_entries.TryTake(out _)) { }
    }

    private void Log(LogLevel level, string message, string? source)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, message, source);
        _entries.Add(entry);

        lock (_eventLock)
        {
            EntryAdded?.Invoke(entry);
        }
    }
}
