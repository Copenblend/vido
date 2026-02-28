using Vido.Core.Logging;

namespace Vido.ViewModels;

/// <summary>
/// Presentation wrapper for a <see cref="LogEntry"/> with pre-formatted, immutable display properties.
/// </summary>
public sealed class LogEntryViewModel
{
    /// <summary>
    /// Wraps a log entry, pre-formatting the timestamp, level tag, and full display line for binding.
    /// </summary>
    /// <param name="entry">The log entry to adapt for presentation.</param>
    public LogEntryViewModel(LogEntry entry)
    {
        Level = entry.Level;
        Timestamp = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        Message = entry.Message;
        Source = entry.Source;
        LevelTag = entry.Level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            _ => "???"
        };
        FormattedLine = Source is not null
            ? $"[{Timestamp}] [{LevelTag}] [{Source}] {Message}"
            : $"[{Timestamp}] [{LevelTag}] {Message}";
    }

    /// <summary>
    /// Gets the severity level of the underlying entry.
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// Gets the local-time timestamp formatted as <c>HH:mm:ss.fff</c>.
    /// </summary>
    public string Timestamp { get; }

    /// <summary>
    /// Gets the short three-letter tag for <see cref="Level"/>.
    /// </summary>
    public string LevelTag { get; }

    /// <summary>
    /// Gets the log message text.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional logical source/category name.
    /// </summary>
    public string? Source { get; }

    /// <summary>
    /// Gets the full pre-formatted display line.
    /// </summary>
    public string FormattedLine { get; }
}
