using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for VideoPlayerViewModel.
/// Uses a mock IVideoEngine to verify ViewModel behavior without FFmpeg DLLs.
/// </summary>
public class VideoPlayerViewModelTests : IDisposable
{
    private readonly IVideoEngine _engine;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly IStateService _stateService;
    private readonly VideoPlayerViewModel _sut;

    public VideoPlayerViewModelTests()
    {
        _engine = Substitute.For<IVideoEngine>();
        _logService = Substitute.For<ILogService>();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings());
        _stateService = Substitute.For<IStateService>();
        _stateService.Current.Returns(new AppState());
        _engine.Volume.Returns(75);
        _engine.IsMuted.Returns(false);
        _engine.IsLooping.Returns(false);
        _sut = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService);
    }

    // ── Initial State ──

    [Fact]
    public void InitialState_IsNone()
    {
        Assert.Equal(PlaybackState.None, _sut.State);
    }

    [Fact]
    public void InitialPosition_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, _sut.Position);
    }

    [Fact]
    public void InitialDuration_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, _sut.Duration);
    }

    [Fact]
    public void InitialVolume_MatchesSettings()
    {
        Assert.Equal(50, _sut.Volume);
    }

    [Fact]
    public void InitialIsMuted_InheritsFromEngine()
    {
        Assert.False(_sut.IsMuted);
    }

    [Fact]
    public void InitialIsLooping_InheritsFromEngine()
    {
        Assert.False(_sut.IsLooping);
    }

    [Fact]
    public void InitialHasMedia_IsFalse()
    {
        Assert.False(_sut.HasMedia);
    }

    [Fact]
    public void InitialShowPlayIcon_IsTrue()
    {
        Assert.True(_sut.ShowPlayIcon);
    }

    [Fact]
    public void InitialPositionText_IsZero()
    {
        Assert.Equal("00:00", _sut.PositionText);
    }

    [Fact]
    public void InitialDurationText_IsZero()
    {
        Assert.Equal("00:00", _sut.DurationText);
    }

    [Fact]
    public void InitialCurrentFilePath_IsNull()
    {
        Assert.Null(_sut.CurrentFilePath);
    }

    [Fact]
    public void InitialCurrentMetadata_IsNull()
    {
        Assert.Null(_sut.CurrentMetadata);
    }

    // ── Volume ──

    [Fact]
    public void SetVolume_ForwardsToEngine()
    {
        _sut.Volume = 50;
        _engine.Received().Volume = 50;
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void SetVolume_ClampsValue(int input, int expected)
    {
        _sut.Volume = input;
        _engine.Received().Volume = expected;
    }

    // ── Mute / Loop toggles ──

    [Fact]
    public void ToggleMute_SetsIsMutedTrue()
    {
        _sut.ToggleMute();
        Assert.True(_sut.IsMuted);
    }

    [Fact]
    public void ToggleMute_ForwardsToEngine()
    {
        _sut.ToggleMute();
        _engine.Received().IsMuted = true;
    }

    [Fact]
    public void ToggleLoop_SetsIsLoopingTrue()
    {
        _sut.ToggleLoop();
        Assert.True(_sut.IsLooping);
    }

    [Fact]
    public void ToggleLoop_ForwardsToEngine()
    {
        _sut.ToggleLoop();
        _engine.Received().IsLooping = true;
    }

    // ── PlayPause / Stop without media ──

    [Fact]
    public void PlayPause_DoesNothing_WhenNoMedia()
    {
        _sut.PlayPause();
        _engine.DidNotReceive().Play();
        _engine.DidNotReceive().Pause();
    }

    [Fact]
    public void Stop_DoesNothing_WhenNoMedia()
    {
        _sut.Stop();
        _engine.DidNotReceive().Stop();
    }

    // ── Engine event handling ──

    [Fact]
    public void StateChanged_UpdatesViewModelState()
    {
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);

        Assert.Equal(PlaybackState.Playing, _sut.State);
        Assert.False(_sut.ShowPlayIcon);
    }

    [Fact]
    public void StateChanged_ToPaused_ShowsPlayIcon()
    {
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Paused);

        Assert.Equal(PlaybackState.Paused, _sut.State);
        Assert.True(_sut.ShowPlayIcon);
    }

    [Fact]
    public void PositionChanged_UpdatesPositionAndText()
    {
        var pos = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(30);
        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(pos);

        Assert.Equal(pos, _sut.Position);
        Assert.Equal("02:30", _sut.PositionText);
    }

    [Fact]
    public void FrameReady_RaisesViewModelEvent()
    {
        FrameData? received = null;
        _sut.FrameReady += f => received = f;

        var frame = new FrameData(new byte[100], 100, 10, 10, 40, TimeSpan.Zero, pooled: false);

        _engine.FrameReady += Raise.Event<Action<FrameData>>(frame);

        Assert.Same(frame, received);
    }

    // ── FormatTime ──

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(7261, "2:01:01")]
    public void FormatTime_FormatsCorrectly(int totalSeconds, string expected)
    {
        var result = VideoPlayerViewModel.FormatTime(TimeSpan.FromSeconds(totalSeconds));
        Assert.Equal(expected, result);
    }

    // ── GetAdjacentVideoFile ──

    [Fact]
    public void GetAdjacentVideoFile_ReturnsNull_WhenNoCurrentFile()
    {
        var result = _sut.GetAdjacentVideoFile(1);
        Assert.Null(result);
    }

    // ── Skip wrapping with temp files ──

    [Fact]
    public async Task SkipNext_WrapsToFirstFile_WhenAtEnd()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            // Load the last file
            await _sut.LoadAndPlayAsync(files[2]);

            // GetAdjacentVideoFile should wrap to first
            var next = _sut.GetAdjacentVideoFile(1);
            Assert.Equal(files[0], next);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SkipPrevious_WrapsToLastFile_WhenAtBeginning()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            // Load the first file
            await _sut.LoadAndPlayAsync(files[0]);

            // GetAdjacentVideoFile(-1) should wrap to last
            var prev = _sut.GetAdjacentVideoFile(-1);
            Assert.Equal(files[2], prev);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task GetAdjacentVideoFile_ReturnsMiddleFile_Normal()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);

            var next = _sut.GetAdjacentVideoFile(1);
            Assert.Equal(files[1], next);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Shuffle ──

    [Fact]
    public void InitialIsShuffling_IsFalse()
    {
        Assert.False(_sut.IsShuffling);
    }

    [Fact]
    public void ToggleShuffle_SetsIsShufflingTrue()
    {
        _sut.ToggleShuffle();
        Assert.True(_sut.IsShuffling);
    }

    [Fact]
    public void ToggleShuffle_Twice_SetsIsShufflingFalse()
    {
        _sut.ToggleShuffle();
        _sut.ToggleShuffle();
        Assert.False(_sut.IsShuffling);
    }

    [Fact]
    public async Task BuildShufflePlaylist_ContainsAllSiblings()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);
            _sut.ToggleShuffle(); // builds the shuffle playlist

            // BuildShufflePlaylist is internal, but we can verify via GetShuffleFile
            // The playlist should contain all 3 files, no duplicates
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Walk through the shuffle playlist using GetShuffleFile
            for (int i = 0; i < files.Length; i++)
            {
                var f = _sut.GetShuffleFile(i);
                Assert.NotNull(f);
                Assert.True(visited.Add(f!), $"Duplicate in shuffle playlist: {f}");
            }
            Assert.Equal(files.Length, visited.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task BuildShufflePlaylist_CurrentFileIsFirst()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[1]); // load middle file
            _sut.ToggleShuffle();

            var first = _sut.GetShuffleFile(0);
            Assert.Equal(files[1], first);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task GetShuffleFile_WrapsAround()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);
            _sut.ToggleShuffle();

            // Going past the end should wrap around
            var wrapped = _sut.GetShuffleFile(3);
            var first = _sut.GetShuffleFile(0);
            Assert.Equal(first, wrapped);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task GetShuffleFile_WrapsBackward()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);
            _sut.ToggleShuffle();

            // Going before the beginning should wrap to end
            var wrapBack = _sut.GetShuffleFile(-1);
            var last = _sut.GetShuffleFile(2);
            Assert.Equal(last, wrapBack);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task ClearShufflePlaylist_OnToggleOff()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);
            _sut.ToggleShuffle(); // on
            _sut.ToggleShuffle(); // off — clears the list

            var result = _sut.GetShuffleFile(0);
            Assert.Null(result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task GetNextFile_UsesShuffle_WhenShuffling()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);
            _sut.ToggleShuffle();

            var next = _sut.GetNextFile();
            Assert.NotNull(next);
            // In shuffle mode, next should be the file at shuffle index 1
            var expectedShuffle = _sut.GetShuffleFile(1);
            Assert.Equal(expectedShuffle, next);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task GetNextFile_UsesAlphabetical_WhenNotShuffling()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.LoadAndPlayAsync(files[0]);

            var next = _sut.GetNextFile();
            Assert.Equal(files[1], next);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── ApplySeek ──

    [Fact]
    public void ApplySeek_SeeksEngine_WhenHasDuration()
    {
        // Simulate having media loaded with a known duration
        _engine.Duration.Returns(TimeSpan.FromSeconds(100));
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);

        // Set up state as if media was loaded
        _sut.GetType().GetProperty("Duration")!.SetValue(_sut, TimeSpan.FromSeconds(100));
        _sut.GetType().GetProperty("HasMedia")!.SetValue(_sut, true);

        // Simulate click at 50% position
        _sut.GetType().GetProperty("SeekPosition")!.SetValue(_sut, 500.0);
        _sut.ApplySeek();

        _engine.Received().Seek(Arg.Is<TimeSpan>(t => Math.Abs(t.TotalSeconds - 50) < 0.1));
    }

    // ── Seek ──

    [Fact]
    public void BeginSeek_SuppressesPositionUpdates()
    {
        _sut.BeginSeek();

        var pos = TimeSpan.FromSeconds(10);
        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(pos);

        // Position should not update during seeking
        Assert.Equal(TimeSpan.Zero, _sut.Position);
    }

    [Fact]
    public void EndSeek_ResumesPositionUpdates()
    {
        _sut.BeginSeek();
        _sut.EndSeek();

        var pos = TimeSpan.FromSeconds(10);
        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(pos);

        Assert.Equal(pos, _sut.Position);
    }

    // ── Helpers ──

    /// <summary>Creates a temp directory with empty video stub files.</summary>
    private static string CreateTempVideoDir(params string[] fileNames)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vido_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var name in fileNames)
            File.WriteAllBytes(Path.Combine(dir, name), []);
        return dir;
    }

    /// <summary>Creates a temp directory with nested subfolders and empty video stub files.</summary>
    private static string CreateTempNestedVideoDir(params string[] relativePaths)
    {
        var root = Path.Combine(Path.GetTempPath(), "vido_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        foreach (var rel in relativePaths)
        {
            var full = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, []);
        }
        return root;
    }

    // ── SetExplorerRootAsync + nested folder scanning ──

    [Fact]
    public async Task SetExplorerRoot_ScansNestedFolders()
    {
        var root = CreateTempNestedVideoDir(
            "a.mp4",
            Path.Combine("sub1", "b.mp4"),
            Path.Combine("sub2", "c.mp4"));
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(root);
            await _sut.LoadAndPlayAsync(Path.Combine(root, "a.mp4"));

            // Should find all 3 files across nested folders
            var next = _sut.GetAdjacentVideoFile(1);
            Assert.NotNull(next);

            // Collect all reachable files via wrapping
            var files = new List<string>();
            var current = Path.Combine(root, "a.mp4");
            for (int i = 0; i < 3; i++)
            {
                files.Add(_sut.GetAdjacentVideoFile(i)!);
            }
            Assert.Equal(3, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SkipNext_CrossesFolderBoundary()
    {
        var root = CreateTempNestedVideoDir(
            "a.mp4",
            Path.Combine("sub", "b.mp4"));
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(root);
            await _sut.LoadAndPlayAsync(Path.Combine(root, "a.mp4"));

            var next = _sut.GetAdjacentVideoFile(1);
            Assert.NotNull(next);
            Assert.Contains("sub", next!);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SetExplorerRoot_Null_ClearsList()
    {
        await _sut.SetExplorerRootAsync(null);
        var result = _sut.GetAdjacentVideoFile(1);
        Assert.Null(result);
    }

    [Fact]
    public async Task SetExplorerRootAsync_NonExistentPath_ClearsList()
    {
        await _sut.SetExplorerRootAsync(@"C:\NonExistent_" + Guid.NewGuid());
        var result = _sut.GetAdjacentVideoFile(1);
        Assert.Null(result);
    }

    [Fact]
    public async Task SetExplorerRootAsync_SkipsHiddenVideoFiles()
    {
        var root = CreateTempNestedVideoDir("visible.mp4", "hidden.mp4");
        try
        {
            File.SetAttributes(Path.Combine(root, "hidden.mp4"), FileAttributes.Hidden);
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(root);
            await _sut.LoadAndPlayAsync(Path.Combine(root, "visible.mp4"));

            // Only the visible file should be in the sibling list
            var next = _sut.GetAdjacentVideoFile(1);
            // Wraps back to itself since it's the only file
            Assert.Equal(Path.Combine(root, "visible.mp4"), next);
        }
        finally { Directory.Delete(root, true); }
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();
        _sut.Dispose(); // should not throw
    }

    [Fact]
    public void Dispose_UnsubscribesFromEngineEvents()
    {
        _sut.Dispose();

        // After dispose, engine events should not update ViewModel state
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        Assert.Equal(PlaybackState.None, _sut.State);
    }

    // ── Stop with media ──

    [Fact]
    public async Task Stop_WithMedia_ResetsState()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            _sut.Stop();

            Assert.False(_sut.HasMedia);
            Assert.Null(_sut.CurrentFilePath);
            Assert.Null(_sut.CurrentMetadata);
            Assert.Equal(TimeSpan.Zero, _sut.Position);
            Assert.Equal(TimeSpan.Zero, _sut.Duration);
            Assert.Equal("00:00", _sut.PositionText);
            Assert.Equal("00:00", _sut.DurationText);
            Assert.Equal(0, _sut.SeekPosition);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Stop_WithMedia_CallsEngineStop()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            _sut.Stop();

            _engine.Received().Stop();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Stop_WithMedia_ClearsStateService()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            _sut.Stop();

            Assert.Null(_stateService.Current.LastVideoPath);
            Assert.Equal(0, _stateService.Current.LastVideoPosition);
            _stateService.Received().QueueSave();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── PlayPause with media ──

    [Fact]
    public async Task PlayPause_WhenPlaying_Pauses()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            // Simulate engine reports Playing state
            _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);

            _sut.PlayPause();

            _engine.Received().Pause();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task PlayPause_WhenPaused_Plays()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            // Simulate engine reports Paused state
            _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Paused);

            _engine.ClearReceivedCalls();
            _sut.PlayPause();

            _engine.Received().Play();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task PlayPause_WithResumeBar_AcceptsResume()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            // Manually set resume bar visible
            _sut.ShowResumeBar = true;
            _engine.ClearReceivedCalls();

            _sut.PlayPause();

            Assert.False(_sut.ShowResumeBar);
            _engine.Received().Play();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Resume / Dismiss ──

    [Fact]
    public async Task ResumePlayback_HidesBarAndPlays()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));
            _sut.ShowResumeBar = true;
            _engine.ClearReceivedCalls();

            _sut.ResumePlayback();

            Assert.False(_sut.ShowResumeBar);
            _engine.Received().Play();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task DismissResume_HidesBarAndStops()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));
            await _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));
            _sut.ShowResumeBar = true;

            _sut.DismissResume();

            Assert.False(_sut.ShowResumeBar);
            Assert.False(_sut.HasMedia);
            _engine.Received().Stop();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Volume auto-unmute ──

    [Fact]
    public void SetVolume_WhenMuted_AutoUnmutes()
    {
        _sut.ToggleMute(); // mute
        Assert.True(_sut.IsMuted);

        _sut.Volume = 80; // change volume

        Assert.False(_sut.IsMuted);
    }

    [Fact]
    public void SetVolume_PersistsToSettings()
    {
        _sut.Volume = 60;

        Assert.Equal(0.60, _settingsService.Current.Volume, precision: 2);
        _settingsService.Received().QueueSave();
    }

    // ── Playback speed ──

    [Fact]
    public void SetPlaybackSpeed_ForwardsToEngine()
    {
        _sut.SetPlaybackSpeed(2.0);

        _engine.Received().SpeedRatio = 2.0;
    }

    [Fact]
    public void SetPlaybackSpeed_ClampsToRange()
    {
        _sut.SetPlaybackSpeed(10.0);
        Assert.Equal(4.0, _sut.PlaybackSpeed);

        _sut.SetPlaybackSpeed(0.1);
        Assert.Equal(0.25, _sut.PlaybackSpeed);
    }

    [Fact]
    public void SetPlaybackSpeed_UpdatesSpeedText()
    {
        _sut.SetPlaybackSpeed(1.5);
        Assert.Equal("1.5x", _sut.PlaybackSpeedText);

        _sut.SetPlaybackSpeed(2.0);
        Assert.Equal("2x", _sut.PlaybackSpeedText);
    }

    [Fact]
    public void SetPlaybackSpeed_PersistsToSettings()
    {
        _sut.SetPlaybackSpeed(1.75);

        Assert.Equal(1.75, _settingsService.Current.PlaybackSpeed);
        _settingsService.Received().QueueSave();
    }

    // ── LoadAndPlayAsync ──

    [Fact]
    public async Task LoadAndPlayAsync_SetsMediaProperties()
    {
        var dir = CreateTempVideoDir("video.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(3));
            var metadata = new VideoMetadata { FilePath = "video.mp4", FileName = "video.mp4", Width = 1920, Height = 1080 };
            _engine.CurrentMetadata.Returns(metadata);

            await _sut.LoadAndPlayAsync(Path.Combine(dir, "video.mp4"));

            Assert.True(_sut.HasMedia);
            Assert.Contains("video.mp4", _sut.CurrentFilePath);
            Assert.Equal(TimeSpan.FromMinutes(3), _sut.Duration);
            Assert.Same(metadata, _sut.CurrentMetadata);
            Assert.Equal("00:00", _sut.PositionText);
            _engine.Received().Play();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task LoadAndPlayAsync_SavesStateService()
    {
        var dir = CreateTempVideoDir("save.mp4");
        try
        {
            _engine.Duration.Returns(TimeSpan.FromMinutes(2));
            var path = Path.Combine(dir, "save.mp4");

            await _sut.LoadAndPlayAsync(path);

            Assert.Equal(path, _stateService.Current.LastVideoPath);
            Assert.Equal(0, _stateService.Current.LastVideoPosition);
            Assert.Contains(path, _stateService.Current.RecentFiles);
            _stateService.Received().QueueSave();
        }
        finally { Directory.Delete(dir, true); }
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
