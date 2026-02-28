namespace Vido.Core.Logging;

/// <summary>
/// Represents one immutable log entry captured by the logging subsystem.
/// </summary>
/// <param name="Timestamp">UTC timestamp when the entry was created.</param>
/// <param name="Level">Severity level for the entry.</param>
/// <param name="Message">Human-readable message text.</param>
/// <param name="Source">Optional logical source/category name.</param>
public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message, string? Source = null);
