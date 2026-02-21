using NSubstitute;
using Vido.Core.Logging;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for OutputLogViewModel — filtering, clearing, entry formatting,
/// and level cycling behavior.
/// </summary>
public sealed class OutputLogViewModelTests
{
    private readonly ILogService _logService;
    private readonly OutputLogViewModel _sut;

    // Captured EntryAdded callback so tests can simulate log events
    private Action<LogEntry>? _entryAddedCallback;

    public OutputLogViewModelTests()
    {
        _logService = Substitute.For<ILogService>();
        _logService.Entries.Returns(new List<LogEntry>().AsReadOnly());

        // Capture the EntryAdded subscription
        _logService.EntryAdded += Arg.Do<Action<LogEntry>>(cb => _entryAddedCallback = cb);

        _sut = new OutputLogViewModel(_logService);
    }

    // ── Initial State ──

    [Fact]
    public void InitialState_HasNoEntries()
    {
        Assert.Empty(_sut.Entries);
        Assert.False(_sut.HasEntries);
    }

    [Fact]
    public void InitialState_AutoScrollEnabled()
    {
        Assert.True(_sut.IsAutoScrollEnabled);
    }

    [Fact]
    public void InitialState_FilterIsAll()
    {
        Assert.Equal("All", _sut.FilterText);
    }

    // ── Loading existing entries ──

    [Fact]
    public void Constructor_LoadsExistingEntries()
    {
        var existingEntries = new List<LogEntry>
        {
            new(DateTime.UtcNow, LogLevel.Info, "First"),
            new(DateTime.UtcNow, LogLevel.Warning, "Second")
        };
        var logService = Substitute.For<ILogService>();
        logService.Entries.Returns(existingEntries.AsReadOnly());

        var vm = new OutputLogViewModel(logService);

        Assert.Equal(2, vm.Entries.Count);
        Assert.True(vm.HasEntries);
    }

    // ── New entry via callback ──

    [Fact]
    public void EntryAdded_AppendsToEntries()
    {
        var entry = new LogEntry(DateTime.UtcNow, LogLevel.Info, "Test message", "Source");

        // Simulate LogService raising EntryAdded (on same thread — no dispatcher needed)
        _entryAddedCallback?.Invoke(entry);

        Assert.Single(_sut.Entries);
        Assert.True(_sut.HasEntries);
        Assert.Equal("Test message", _sut.Entries[0].Message);
        Assert.Equal("Source", _sut.Entries[0].Source);
    }

    [Fact]
    public void EntryAdded_SetsHasEntries()
    {
        Assert.False(_sut.HasEntries);

        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Info, "x"));

        Assert.True(_sut.HasEntries);
    }

    // ── Clear ──

    [Fact]
    public void ClearLog_RemovesAllEntries()
    {
        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Info, "a"));
        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Error, "b"));
        Assert.Equal(2, _sut.Entries.Count);

        _sut.ClearLog();

        Assert.Empty(_sut.Entries);
        Assert.False(_sut.HasEntries);
        _logService.Received(1).Clear();
    }

    // ── Auto-scroll toggle ──

    [Fact]
    public void ToggleAutoScroll_TogglesState()
    {
        Assert.True(_sut.IsAutoScrollEnabled);

        _sut.ToggleAutoScroll();
        Assert.False(_sut.IsAutoScrollEnabled);

        _sut.ToggleAutoScroll();
        Assert.True(_sut.IsAutoScrollEnabled);
    }

    // ── Filter cycling ──

    [Fact]
    public void CycleFilter_CyclesFromAllToInfoPlusToWarnPlusToErrorsToAll()
    {
        // Initial: All (Debug)
        Assert.Equal("All", _sut.FilterText);

        _sut.CycleFilter();
        Assert.Equal("Info+", _sut.FilterText);

        _sut.CycleFilter();
        Assert.Equal("Warn+", _sut.FilterText);

        _sut.CycleFilter();
        Assert.Equal("Errors", _sut.FilterText);

        _sut.CycleFilter();
        Assert.Equal("All", _sut.FilterText);
    }

    [Fact]
    public void CycleFilter_RebuildsList_OnlyShowsMatchingEntries()
    {
        // Pre-populate all levels via the service's Entries property
        var entries = new List<LogEntry>
        {
            new(DateTime.UtcNow, LogLevel.Debug, "debug msg"),
            new(DateTime.UtcNow, LogLevel.Info, "info msg"),
            new(DateTime.UtcNow, LogLevel.Warning, "warning msg"),
            new(DateTime.UtcNow, LogLevel.Error, "error msg")
        };
        var logService = Substitute.For<ILogService>();
        logService.Entries.Returns(entries.AsReadOnly());

        var vm = new OutputLogViewModel(logService);
        Assert.Equal(4, vm.Entries.Count);

        // Cycle to Info+ — should exclude Debug
        vm.CycleFilter();
        Assert.Equal(3, vm.Entries.Count);
        Assert.DoesNotContain(vm.Entries, e => e.Level == LogLevel.Debug);

        // Cycle to Warn+ — should exclude Debug & Info
        vm.CycleFilter();
        Assert.Equal(2, vm.Entries.Count);
        Assert.DoesNotContain(vm.Entries, e => e.Level == LogLevel.Info);

        // Cycle to Errors only
        vm.CycleFilter();
        Assert.Single(vm.Entries);
        Assert.Equal(LogLevel.Error, vm.Entries[0].Level);

        // Cycle back to All
        vm.CycleFilter();
        Assert.Equal(4, vm.Entries.Count);
    }

    [Fact]
    public void Filter_ExcludesNewEntriesBelowLevel()
    {
        // Set filter to Warning+
        _sut.SetFilter(LogLevel.Warning);

        // Debug entry should be excluded
        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "skip me"));
        Assert.Empty(_sut.Entries);

        // Info entry should be excluded
        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Info, "skip me too"));
        Assert.Empty(_sut.Entries);

        // Warning entry should be included
        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "show me"));
        Assert.Single(_sut.Entries);

        // Error entry should be included
        _entryAddedCallback?.Invoke(new LogEntry(DateTime.UtcNow, LogLevel.Error, "show me too"));
        Assert.Equal(2, _sut.Entries.Count);
    }

    // ── SetFilter ──

    [Fact]
    public void SetFilter_SetsLevelAndText()
    {
        _sut.SetFilter(LogLevel.Error);

        Assert.Equal("Errors", _sut.FilterText);
    }

    [Fact]
    public void SetFilter_Debug_ShowsAll()
    {
        _sut.SetFilter(LogLevel.Debug);

        Assert.Equal("All", _sut.FilterText);
    }

    // ── LogEntryViewModel formatting ──

    [Fact]
    public void LogEntryViewModel_FormatsTimestamp()
    {
        var when = new DateTime(2025, 6, 15, 14, 30, 45, 123, DateTimeKind.Utc);
        var entry = new LogEntry(when, LogLevel.Info, "test");
        var vm = new LogEntryViewModel(entry);

        Assert.Equal("INF", vm.LevelTag);
        Assert.Equal("test", vm.Message);
        Assert.Null(vm.Source);
        // Timestamp should be in local time HH:mm:ss.fff format
        Assert.Matches(@"\d{2}:\d{2}:\d{2}\.\d{3}", vm.Timestamp);
    }

    [Fact]
    public void LogEntryViewModel_FormattedLine_WithoutSource()
    {
        var entry = new LogEntry(DateTime.UtcNow, LogLevel.Error, "oops");
        var vm = new LogEntryViewModel(entry);

        Assert.Contains("[ERR]", vm.FormattedLine);
        Assert.Contains("oops", vm.FormattedLine);
        Assert.DoesNotContain("[null]", vm.FormattedLine);
    }

    [Fact]
    public void LogEntryViewModel_FormattedLine_WithSource()
    {
        var entry = new LogEntry(DateTime.UtcNow, LogLevel.Info, "hello", "MyService");
        var vm = new LogEntryViewModel(entry);

        Assert.Contains("[INF]", vm.FormattedLine);
        Assert.Contains("[MyService]", vm.FormattedLine);
        Assert.Contains("hello", vm.FormattedLine);
    }

    [Theory]
    [InlineData(LogLevel.Debug, "DBG")]
    [InlineData(LogLevel.Info, "INF")]
    [InlineData(LogLevel.Warning, "WRN")]
    [InlineData(LogLevel.Error, "ERR")]
    public void LogEntryViewModel_LevelTags(LogLevel level, string expectedTag)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, "msg");
        var vm = new LogEntryViewModel(entry);

        Assert.Equal(expectedTag, vm.LevelTag);
    }
}
