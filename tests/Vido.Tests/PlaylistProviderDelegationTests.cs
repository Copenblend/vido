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
/// Tests that <see cref="VideoPlayerViewModel"/> correctly delegates to
/// an <see cref="IPlaylistProvider"/> when one is registered and active,
/// and falls back to built-in sibling-file navigation when not.
/// </summary>
public class PlaylistProviderDelegationTests : IDisposable
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
    public PlaylistProviderDelegationTests()
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

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        _sut.Dispose();
    }

    // â”€â”€ SkipNext â”€â”€

    /// <summary>
    /// Verifies that Skip Next delegates to _playlistProvider when active _playlistProvider returns file.
    /// </summary>
    [Fact]
    public async Task SkipNext_DelegatesToProvider_WhenActiveProviderReturnsFile()
    {
        var dir = CreateTempVideoDir("target.mp4");
        try
        {
            var targetPath = Path.Combine(dir, "target.mp4");
            _playlistProvider.IsActive.Returns(true);
            _playlistProvider.GetNextFile().Returns(targetPath);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SkipNext();

            _playlistProvider.Received(1).GetNextFile();
            await _engine.Received(1).LoadAsync(targetPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Skip Next falls back when _playlistProvider is not active.
    /// </summary>
    [Fact]
    public async Task SkipNext_FallsBack_WhenProviderIsNotActive()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _playlistProvider.IsActive.Returns(false);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            await _sut.SkipNext();

            _playlistProvider.DidNotReceive().GetNextFile();
            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Skip Next falls back when _playlistProvider returns null.
    /// </summary>
    [Fact]
    public async Task SkipNext_FallsBack_WhenProviderReturnsNull()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _playlistProvider.IsActive.Returns(true);
            _playlistProvider.GetNextFile().Returns((string?)null);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            await _sut.SkipNext();

            _playlistProvider.Received(1).GetNextFile();
            // Falls back to sibling file navigation
            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Skip Next falls back when no _playlistProvider registered.
    /// </summary>
    [Fact]
    public async Task SkipNext_FallsBack_WhenNoProviderRegistered()
    {
        using var noProviderVm = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService);
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await noProviderVm.SetExplorerRootAsync(dir);
            await noProviderVm.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            await noProviderVm.SkipNext();

            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    // â”€â”€ SkipPrevious â”€â”€

    /// <summary>
    /// Verifies that Skip Previous delegates to _playlistProvider when active _playlistProvider returns file.
    /// </summary>
    [Fact]
    public async Task SkipPrevious_DelegatesToProvider_WhenActiveProviderReturnsFile()
    {
        var dir = CreateTempVideoDir("target.mp4");
        try
        {
            var targetPath = Path.Combine(dir, "target.mp4");
            _playlistProvider.IsActive.Returns(true);
            _playlistProvider.GetPreviousFile().Returns(targetPath);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SkipPrevious();

            _playlistProvider.Received(1).GetPreviousFile();
            await _engine.Received(1).LoadAsync(targetPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Skip Previous falls back when _playlistProvider is not active.
    /// </summary>
    [Fact]
    public async Task SkipPrevious_FallsBack_WhenProviderIsNotActive()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _playlistProvider.IsActive.Returns(false);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[1]);
            _engine.ClearReceivedCalls();

            await _sut.SkipPrevious();

            _playlistProvider.DidNotReceive().GetPreviousFile();
            await _engine.Received(1).LoadAsync(files[0]);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Skip Previous falls back when _playlistProvider returns null.
    /// </summary>
    [Fact]
    public async Task SkipPrevious_FallsBack_WhenProviderReturnsNull()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _playlistProvider.IsActive.Returns(true);
            _playlistProvider.GetPreviousFile().Returns((string?)null);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[1]);
            _engine.ClearReceivedCalls();

            await _sut.SkipPrevious();

            _playlistProvider.Received(1).GetPreviousFile();
            await _engine.Received(1).LoadAsync(files[0]);
        }
        finally { Directory.Delete(dir, true); }
    }

    // â”€â”€ OnEngineMediaEnded (auto-advance) â”€â”€

    /// <summary>
    /// Verifies that Media Ended delegates to _playlistProvider when active.
    /// </summary>
    [Fact]
    public async Task MediaEnded_DelegatesToProvider_WhenActive()
    {
        var dir = CreateTempVideoDir("target.mp4");
        try
        {
            var targetPath = Path.Combine(dir, "target.mp4");
            _playlistProvider.IsActive.Returns(true);
            _playlistProvider.GetNextFile().Returns(targetPath);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            // Trigger OnEngineMediaEnded via the engine event
            _engine.MediaEnded += Raise.Event<Action>();

            // Give async LoadAndPlayAsync time to process
            await Task.Delay(100);

            _playlistProvider.Received(1).GetNextFile();
            await _engine.Received(1).LoadAsync(targetPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Media Ended falls back when no _playlistProvider.
    /// </summary>
    [Fact]
    public async Task MediaEnded_FallsBack_WhenNoProvider()
    {
        using var noProviderVm = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService);
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await noProviderVm.SetExplorerRootAsync(dir);
            await noProviderVm.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            // Trigger OnEngineMediaEnded
            _engine.MediaEnded += Raise.Event<Action>();
            await Task.Delay(100);

            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that Media Ended does not advance when looping.
    /// </summary>
    [Fact]
    public void MediaEnded_DoesNotAdvance_WhenLooping()
    {
        _playlistProvider.IsActive.Returns(true);

        _sut.IsLooping = true;

        // Trigger OnEngineMediaEnded
        _engine.MediaEnded += Raise.Event<Action>();

        // _playlistProvider should not be consulted when looping (engine handles it)
        _playlistProvider.DidNotReceive().GetNextFile();
    }

    // ── Shuffle delegation ──

    /// <summary>
    /// Verifies that toggling shuffle ON delegates to <see cref="IPlaylistProvider.EnableShuffle"/>
    /// when the provider is active, and does not build an explorer-based shuffle playlist.
    /// </summary>
    [Fact]
    public async Task ToggleShuffle_DelegatesToProviderAndSkipsExplorerShuffle_WhenProviderActive()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);

            _playlistProvider.IsActive.Returns(true);

            _sut.IsShuffling = true;

            _playlistProvider.Received(1).EnableShuffle();
            // Internal shuffle playlist must remain empty (explorer shuffle not built)
            Assert.Null(_sut.GetShuffleFile(1));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies that toggling shuffle OFF delegates to <see cref="IPlaylistProvider.DisableShuffle"/>
    /// when the provider is active.
    /// </summary>
    [Fact]
    public void ToggleShuffle_DisablesProviderShuffle_WhenProviderActive()
    {
        _playlistProvider.IsActive.Returns(true);

        _sut.IsShuffling = true;
        _sut.IsShuffling = false;

        _playlistProvider.Received(1).DisableShuffle();
    }

    /// <summary>
    /// Verifies that toggling shuffle ON builds the explorer-based shuffle playlist
    /// when the provider is not active.
    /// </summary>
    [Fact]
    public async Task ToggleShuffle_BuildsExplorerShuffle_WhenProviderNotActive()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4", "c.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);

            _playlistProvider.IsActive.Returns(false);

            _sut.IsShuffling = true;

            _playlistProvider.DidNotReceive().EnableShuffle();
            // Internal shuffle playlist should be populated from explorer files
            Assert.NotNull(_sut.GetShuffleFile(1));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Helpers ──

    private static string CreateTempVideoDir(params string[] fileNames)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vido_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var name in fileNames)
            File.WriteAllBytes(Path.Combine(dir, name), []);
        return dir;
    }
}