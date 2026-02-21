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
    private readonly List<LogEntry> _entries = [];
    private readonly object _lock = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList().AsReadOnly();
            }
        }
    }

    public event Action<LogEntry>? EntryAdded;

    public void Debug(string message, string? source = null) => Log(LogLevel.Debug, message, source);
    public void Info(string message, string? source = null) => Log(LogLevel.Info, message, source);
    public void Warning(string message, string? source = null) => Log(LogLevel.Warning, message, source);
    public void Error(string message, string? source = null) => Log(LogLevel.Error, message, source);

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private void Log(LogLevel level, string message, string? source)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, message, source);

        lock (_lock)
        {
            _entries.Add(entry);
        }

        EntryAdded?.Invoke(entry);
    }
}
