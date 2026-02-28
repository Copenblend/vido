using Vido.Core.Logging;
using Vido.Services.Logging;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="LogService"/>.
/// </summary>
public sealed class LogServiceTests
{
    private readonly ILogService _log = new LogService();

    /// <summary>
    /// Verifies that Debug adds entry with debug level.
    /// </summary>
    [Fact]
    public void Debug_AddsEntryWithDebugLevel()
    {
        _log.Debug("test message", "Source1");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("test message", entry.Message);
        Assert.Equal("Source1", entry.Source);
    }

    /// <summary>
    /// Verifies that Info adds entry with info level.
    /// </summary>
    [Fact]
    public void Info_AddsEntryWithInfoLevel()
    {
        _log.Info("info msg");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal("info msg", entry.Message);
    }

    /// <summary>
    /// Verifies that Warning adds entry with warning level.
    /// </summary>
    [Fact]
    public void Warning_AddsEntryWithWarningLevel()
    {
        _log.Warning("warn msg");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    /// <summary>
    /// Verifies that Error adds entry with error level.
    /// </summary>
    [Fact]
    public void Error_AddsEntryWithErrorLevel()
    {
        _log.Error("error msg");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    /// <summary>
    /// Verifies that Entries are chronological.
    /// </summary>
    [Fact]
    public void Entries_AreChronological()
    {
        _log.Debug("first");
        _log.Info("second");
        _log.Warning("third");

        var entries = _log.Entries;
        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].Timestamp <= entries[1].Timestamp);
        Assert.True(entries[1].Timestamp <= entries[2].Timestamp);
    }

    /// <summary>
    /// Verifies that Clear removes all entries.
    /// </summary>
    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _log.Debug("a");
        _log.Info("b");

        _log.Clear();

        Assert.Empty(_log.Entries);
    }

    /// <summary>
    /// Verifies that Entry Added fires on new entry.
    /// </summary>
    [Fact]
    public void EntryAdded_FiresOnNewEntry()
    {
        LogEntry? received = null;
        _log.EntryAdded += entry => received = entry;

        _log.Info("hello");

        Assert.NotNull(received);
        Assert.Equal("hello", received.Message);
    }

    /// <summary>
    /// Verifies that Source defaults to empty string.
    /// </summary>
    [Fact]
    public void Source_DefaultsToEmptyString()
    {
        _log.Info("no source");

        var entry = Assert.Single(_log.Entries);
        Assert.Null(entry.Source);
    }

    /// <summary>
    /// Verifies that Timestamp is recent utc.
    /// </summary>
    [Fact]
    public void Timestamp_IsRecentUtc()
    {
        var before = DateTime.UtcNow;
        _log.Info("timed");
        var after = DateTime.UtcNow;

        var entry = Assert.Single(_log.Entries);
        Assert.InRange(entry.Timestamp, before, after);
    }
}