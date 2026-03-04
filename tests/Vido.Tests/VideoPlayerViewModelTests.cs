using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Playlists;
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
    private readonly IPlaylistProvider _playlistProvider;
    private readonly VideoPlayerViewModel _sut;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
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
        _playlistProvider = Substitute.For<IPlaylistProvider>();
        _sut = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService, _playlistProvider);
    }

    // ── Initial State ──

    /// <summary>
    /// Verifies that Initial State is none.
    /// </summary>
    [Fact]
    public void InitialState_IsNone()
    {
        Assert.Equal(PlaybackState.None, _sut.State);
    }

    /// <summary>
    /// Verifies that Initial Position is zero.
    /// </summary>
    [Fact]
    public void InitialPosition_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, _sut.Position);
    }

    /// <summary>
    /// Verifies that Initial Duration is zero.
    /// </summary>
    [Fact]
    public void InitialDuration_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, _sut.Duration);
    }

    /// <summary>
    /// Verifies that Initial Volume matches settings.
    /// </summary>
    [Fact]
    public void InitialVolume_MatchesSettings()
    {
        Assert.Equal(50, _sut.Volume);
    }

    /// <summary>
    /// Verifies that Initial Is Muted inherits from engine.
    /// </summary>
    [Fact]
    public void InitialIsMuted_InheritsFromEngine()
    {
        Assert.False(_sut.IsMuted);
    }

    /// <summary>
    /// Verifies that Initial Is Looping inherits from engine.
    /// </summary>
    [Fact]
    public void InitialIsLooping_InheritsFromEngine()
    {
        Assert.False(_sut.IsLooping);
    }

    /// <summary>
    /// Verifies that Initial Has Media is false.
    /// </summary>
    [Fact]
    public void InitialHasMedia_IsFalse()
    {
        Assert.False(_sut.HasMedia);
    }

    /// <summary>
    /// Verifies that Initial Show Play Icon is true.
    /// </summary>
    [Fact]
    public void InitialShowPlayIcon_IsTrue()
    {
        Assert.True(_sut.ShowPlayIcon);
    }

    /// <summary>
    /// Verifies that Initial Position Text is zero.
    /// </summary>
    [Fact]
    public void InitialPositionText_IsZero()
    {
        Assert.Equal("00:00", _sut.PositionText);
    }

    /// <summary>
    /// Verifies that Initial Duration Text is zero.
    /// </summary>
    [Fact]
    public void InitialDurationText_IsZero()
    {
        Assert.Equal("00:00", _sut.DurationText);
    }

    /// <summary>
    /// Verifies that Initial Current File Path is null.
    /// </summary>
    [Fact]
    public void InitialCurrentFilePath_IsNull()
    {
        Assert.Null(_sut.CurrentFilePath);
    }

    /// <summary>
    /// Verifies that Initial Current Metadata is null.
    /// </summary>
    [Fact]
    public void InitialCurrentMetadata_IsNull()
    {
        Assert.Null(_sut.CurrentMetadata);
    }

    // ── Volume ──

    /// <summary>
    /// Verifies that Set Volume forwards to engine.
    /// </summary>
    [Fact]
    public void SetVolume_ForwardsToEngine()
    {
        _sut.Volume = 50;
        _engine.Received().Volume = 50;
    }

    /// <summary>
    /// Verifies that Set Volume clamps value.
    /// </summary>
    /// <param name="input">The input value to process.</param>
    /// <param name="expected">The expected result value.</param>
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

    /// <summary>
    /// Verifies that Toggle Mute sets is muted true.
    /// </summary>
    [Fact]
    public void ToggleMute_SetsIsMutedTrue()
    {
        _sut.ToggleMute();
        Assert.True(_sut.IsMuted);
    }

    /// <summary>
    /// Verifies that Toggle Mute forwards to engine.
    /// </summary>
    [Fact]
    public void ToggleMute_ForwardsToEngine()
    {
        _sut.ToggleMute();
        _engine.Received().IsMuted = true;
    }

    /// <summary>
    /// Verifies that Toggle Loop sets is looping true.
    /// </summary>
    [Fact]
    public void ToggleLoop_SetsIsLoopingTrue()
    {
        _sut.ToggleLoop();
        Assert.True(_sut.IsLooping);
    }

    /// <summary>
    /// Verifies that Toggle Loop forwards to engine.
    /// </summary>
    [Fact]
    public void ToggleLoop_ForwardsToEngine()
    {
        _sut.ToggleLoop();
        _engine.Received().IsLooping = true;
    }

    // ── PlayPause / Stop without media ──

    /// <summary>
    /// Verifies that Play Pause does nothing when no media.
    /// </summary>
    [Fact]
    public void PlayPause_DoesNothing_WhenNoMedia()
    {
        _sut.PlayPause();
        _engine.DidNotReceive().Play();
        _engine.DidNotReceive().Pause();
    }

    /// <summary>
    /// Verifies that Stop does nothing when no media.
    /// </summary>
    [Fact]
    public void Stop_DoesNothing_WhenNoMedia()
    {
        _sut.Stop();
        _engine.DidNotReceive().Stop();
    }

    // ── Engine event handling ──

    /// <summary>
    /// Verifies that State Changed updates view model state.
    /// </summary>
    [Fact]
    public void StateChanged_UpdatesViewModelState()
    {
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);

        Assert.Equal(PlaybackState.Playing, _sut.State);
        Assert.False(_sut.ShowPlayIcon);
    }

    /// <summary>
    /// Verifies that State Changed to paused shows play icon.
    /// </summary>
    [Fact]
    public void StateChanged_ToPaused_ShowsPlayIcon()
    {
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Paused);

        Assert.Equal(PlaybackState.Paused, _sut.State);
        Assert.True(_sut.ShowPlayIcon);
    }

    /// <summary>
    /// Verifies that Position Changed updates position and text.
    /// </summary>
    [Fact]
    public void PositionChanged_UpdatesPositionAndText()
    {
        var pos = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(30);
        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(pos);

        Assert.Equal(pos, _sut.Position);
        Assert.Equal("02:30", _sut.PositionText);
    }

    /// <summary>
    /// Verifies that position text only changes when the displayed second changes.
    /// </summary>
    [Fact]
    public void PositionChanged_SameSecond_DoesNotRaisePositionTextChangedTwice()
    {
        var changedCount = 0;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VideoPlayerViewModel.PositionText))
                changedCount++;
        };

        _sut.GetType().GetProperty("Duration")!.SetValue(_sut, TimeSpan.FromMinutes(5));

        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(TimeSpan.FromSeconds(10.10));
        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(TimeSpan.FromSeconds(10.90));

        Assert.Equal(1, changedCount);
    }

    /// <summary>
    /// Verifies that stop resets cached formatting state so subsequent updates render correctly.
    /// </summary>
    [Fact]
    public void Stop_ResetsFormattedSecondCache()
    {
        _sut.GetType().GetProperty("HasMedia")!.SetValue(_sut, true);

        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(TimeSpan.FromSeconds(10.10));
        Assert.Equal("00:10", _sut.PositionText);

        _sut.Stop();
        Assert.Equal("00:00", _sut.PositionText);

        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(TimeSpan.FromSeconds(10.50));
        Assert.Equal("00:10", _sut.PositionText);
    }

    /// <summary>
    /// Verifies that Frame Ready raises view model event.
    /// </summary>
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

    /// <summary>
    /// Verifies that Format Time formats correctly.
    /// </summary>
    /// <param name="totalSeconds">The total number of seconds to format.</param>
    /// <param name="expected">The expected result value.</param>
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

    // ── IsLoadingMedia ──

    /// <summary>
    /// Verifies that Load And Play Async sets is loading media true then false.
    /// </summary>
    [Fact]
    public async Task LoadAndPlayAsync_SetsIsLoadingMedia_True_Then_False()
    {
        var dir = CreateTempVideoDir("test.mp4");
        try
        {
            var tcs = new TaskCompletionSource();
            _engine.LoadAsync(Arg.Any<string>()).Returns(tcs.Task);
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            Assert.False(_sut.IsLoadingMedia);

            var task = _sut.LoadAndPlayAsync(Path.Combine(dir, "test.mp4"));

            // The loading indicator only appears after a 500 ms delay,
            // so we wait long enough for that threshold to elapse.
            await Task.Delay(650);
            Assert.True(_sut.IsLoadingMedia);

            tcs.SetResult();
            await task;

            // The spinner stays visible for a minimum 500 ms display period
            // after it was shown. Wait for that to elapse.
            await Task.Delay(600);
            Assert.False(_sut.IsLoadingMedia);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Load And Play Async resets is loading media on failure.
    /// </summary>
    [Fact]
    public async Task LoadAndPlayAsync_ResetsIsLoadingMedia_OnFailure()
    {
        _engine.LoadAsync(Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("test error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.LoadAndPlayAsync(Path.Combine(Path.GetTempPath(), "nonexistent.mp4")));

        Assert.False(_sut.IsLoadingMedia);
    }

    /// <summary>
    /// Verifies that Load And Play Async fast load never shows loading indicator.
    /// </summary>
    [Fact]
    public async Task LoadAndPlayAsync_FastLoad_NeverShowsLoadingIndicator()
    {
        var dir = CreateTempVideoDir("fast.mp4");
        try
        {
            // LoadAsync returns immediately — simulates a fast local load.
            _engine.LoadAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.LoadAndPlayAsync(Path.Combine(dir, "fast.mp4"));

            // Even after waiting past the 500 ms threshold, the indicator
            // should never have been shown because the load already finished.
            await Task.Delay(650);
            Assert.False(_sut.IsLoadingMedia);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── GetAdjacentVideoFile ──

    /// <summary>
    /// Verifies that Get Adjacent Video File returns null when no current file.
    /// </summary>
    [Fact]
    public void GetAdjacentVideoFile_ReturnsNull_WhenNoCurrentFile()
    {
        var result = _sut.GetAdjacentVideoFile(1);
        Assert.Null(result);
    }

    // ── Skip wrapping with temp files ──

    /// <summary>
    /// Verifies that Skip Next wraps to first file when at end.
    /// </summary>
    [Fact]
    public async Task SkipNext_WrapsToFirstFile_WhenAtEnd()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[2]);

            // GetAdjacentVideoFile should wrap to first
            var next = _sut.GetAdjacentVideoFile(1);
            Assert.Equal(files[0], next);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Skip Previous wraps to last file when at beginning.
    /// </summary>
    [Fact]
    public async Task SkipPrevious_WrapsToLastFile_WhenAtBeginning()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);

            // GetAdjacentVideoFile(-1) should wrap to last
            var prev = _sut.GetAdjacentVideoFile(-1);
            Assert.Equal(files[2], prev);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Get Adjacent Video File returns middle file normal.
    /// </summary>
    [Fact]
    public async Task GetAdjacentVideoFile_ReturnsMiddleFile_Normal()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);

            var next = _sut.GetAdjacentVideoFile(1);
            Assert.Equal(files[1], next);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Shuffle ──

    /// <summary>
    /// Verifies that Initial Is Shuffling is false.
    /// </summary>
    [Fact]
    public void InitialIsShuffling_IsFalse()
    {
        Assert.False(_sut.IsShuffling);
    }

    /// <summary>
    /// Verifies that Toggle Shuffle sets is shuffling true.
    /// </summary>
    [Fact]
    public void ToggleShuffle_SetsIsShufflingTrue()
    {
        _sut.ToggleShuffle();
        Assert.True(_sut.IsShuffling);
    }

    /// <summary>
    /// Verifies that Toggle Shuffle twice sets is shuffling false.
    /// </summary>
    [Fact]
    public void ToggleShuffle_Twice_SetsIsShufflingFalse()
    {
        _sut.ToggleShuffle();
        _sut.ToggleShuffle();
        Assert.False(_sut.IsShuffling);
    }

    /// <summary>
    /// Verifies that Build Shuffle Playlist contains all siblings.
    /// </summary>
    [Fact]
    public async Task BuildShufflePlaylist_ContainsAllSiblings()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
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

    /// <summary>
    /// Verifies that Build Shuffle Playlist current file is first.
    /// </summary>
    [Fact]
    public async Task BuildShufflePlaylist_CurrentFileIsFirst()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[1]); // load middle file
            _sut.ToggleShuffle();

            var first = _sut.GetShuffleFile(0);
            Assert.Equal(files[1], first);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Get Shuffle File wraps around.
    /// </summary>
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

    /// <summary>
    /// Verifies that Get Shuffle File wraps backward.
    /// </summary>
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

    /// <summary>
    /// Verifies that Clear Shuffle Playlist on toggle off.
    /// </summary>
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

    /// <summary>
    /// Verifies that Get Next File uses shuffle when shuffling.
    /// </summary>
    [Fact]
    public async Task GetNextFile_UsesShuffle_WhenShuffling()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
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

    /// <summary>
    /// Verifies that Get Next File uses alphabetical when not shuffling.
    /// </summary>
    [Fact]
    public async Task GetNextFile_UsesAlphabetical_WhenNotShuffling()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(5));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);

            var next = _sut.GetNextFile();
            Assert.Equal(files[1], next);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── ApplySeek ──

    /// <summary>
    /// Verifies that Apply Seek seeks engine when has duration.
    /// </summary>
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

    /// <summary>
    /// Verifies that Begin Seek suppresses position updates.
    /// </summary>
    [Fact]
    public void BeginSeek_SuppressesPositionUpdates()
    {
        _sut.BeginSeek();

        var pos = TimeSpan.FromSeconds(10);
        _engine.PositionChanged += Raise.Event<Action<TimeSpan>>(pos);

        // Position should not update during seeking
        Assert.Equal(TimeSpan.Zero, _sut.Position);
    }

    /// <summary>
    /// Verifies that End Seek resumes position updates.
    /// </summary>
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

    /// <summary>
    /// Creates a temp directory populated with empty video stub files for testing.
    /// </summary>
    /// <param name="fileNames">File names (including extension) to create as empty stubs.</param>
    private static string CreateTempVideoDir(params string[] fileNames)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vido_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var name in fileNames)
            File.WriteAllBytes(Path.Combine(dir, name), []);
        return dir;
    }

    /// <summary>
    /// Creates a temp directory with nested subfolders and empty video stub files for testing.
    /// </summary>
    /// <param name="relativePaths">Relative paths (including subdirectories) of stub files to create.</param>
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

    /// <summary>
    /// Verifies that Set Explorer Root scans nested folders.
    /// </summary>
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

    /// <summary>
    /// Verifies that Skip Next crosses folder boundary.
    /// </summary>
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

    /// <summary>
    /// Verifies that Set Explorer Root null clears list.
    /// </summary>
    [Fact]
    public async Task SetExplorerRoot_Null_ClearsList()
    {
        await _sut.SetExplorerRootAsync(null);
        var result = _sut.GetAdjacentVideoFile(1);
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that Set Explorer Root Async non existent path clears list.
    /// </summary>
    [Fact]
    public async Task SetExplorerRootAsync_NonExistentPath_ClearsList()
    {
        await _sut.SetExplorerRootAsync(@"C:\NonExistent_" + Guid.NewGuid());
        var result = _sut.GetAdjacentVideoFile(1);
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that Set Explorer Root Async skips hidden video files.
    /// </summary>
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

    /// <summary>
    /// Verifies that Dispose can be called multiple times.
    /// </summary>
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();
        _sut.Dispose(); // should not throw
    }

    /// <summary>
    /// Verifies that Dispose unsubscribes from engine events.
    /// </summary>
    [Fact]
    public void Dispose_UnsubscribesFromEngineEvents()
    {
        _sut.Dispose();

        // After dispose, engine events should not update ViewModel state
        _engine.StateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        Assert.Equal(PlaybackState.None, _sut.State);
    }

    // ── Stop with media ──

    /// <summary>
    /// Verifies that Stop with media resets state.
    /// </summary>
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

    /// <summary>
    /// Verifies that Stop with media calls engine stop.
    /// </summary>
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

    /// <summary>
    /// Verifies that Stop with media clears state service.
    /// </summary>
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

    /// <summary>
    /// Verifies that Play Pause when playing pauses.
    /// </summary>
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

    /// <summary>
    /// Verifies that Play Pause when paused plays.
    /// </summary>
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

    /// <summary>
    /// Verifies that Play Pause with resume bar accepts resume.
    /// </summary>
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

    /// <summary>
    /// Verifies that Resume Playback hides bar and plays.
    /// </summary>
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

    /// <summary>
    /// Verifies that Dismiss Resume hides bar and stops.
    /// </summary>
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

    /// <summary>
    /// Verifies that Set Volume when muted auto unmutes.
    /// </summary>
    [Fact]
    public void SetVolume_WhenMuted_AutoUnmutes()
    {
        _sut.ToggleMute(); // mute
        Assert.True(_sut.IsMuted);

        _sut.Volume = 80; // change volume

        Assert.False(_sut.IsMuted);
    }

    /// <summary>
    /// Verifies that Set Volume persists to settings.
    /// </summary>
    [Fact]
    public void SetVolume_PersistsToSettings()
    {
        _sut.Volume = 60;

        Assert.Equal(0.60, _settingsService.Current.Volume, precision: 2);
        _settingsService.Received().QueueSave();
    }

    // ── Playback speed ──

    /// <summary>
    /// Verifies that Set Playback Speed forwards to engine.
    /// </summary>
    [Fact]
    public void SetPlaybackSpeed_ForwardsToEngine()
    {
        _sut.SetPlaybackSpeed(2.0);

        _engine.Received().SpeedRatio = 2.0;
    }

    /// <summary>
    /// Verifies that Set Playback Speed clamps to range.
    /// </summary>
    [Fact]
    public void SetPlaybackSpeed_ClampsToRange()
    {
        _sut.SetPlaybackSpeed(10.0);
        Assert.Equal(4.0, _sut.PlaybackSpeed);

        _sut.SetPlaybackSpeed(0.1);
        Assert.Equal(0.25, _sut.PlaybackSpeed);
    }

    /// <summary>
    /// Verifies that Set Playback Speed updates speed text.
    /// </summary>
    [Fact]
    public void SetPlaybackSpeed_UpdatesSpeedText()
    {
        _sut.SetPlaybackSpeed(1.5);
        Assert.Equal("1.5x", _sut.PlaybackSpeedText);

        _sut.SetPlaybackSpeed(2.0);
        Assert.Equal("2x", _sut.PlaybackSpeedText);
    }

    /// <summary>
    /// Verifies that Set Playback Speed persists to settings.
    /// </summary>
    [Fact]
    public void SetPlaybackSpeed_PersistsToSettings()
    {
        _sut.SetPlaybackSpeed(1.75);

        Assert.Equal(1.75, _settingsService.Current.PlaybackSpeed);
        _settingsService.Received().QueueSave();
    }

    // ── LoadAndPlayAsync ──

    /// <summary>
    /// Verifies that Load And Play Async sets media properties.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load And Play Async saves state service.
    /// </summary>
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

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        _sut.Dispose();
    }
}