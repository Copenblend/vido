using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Plugin;
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
    private readonly IContributionRegistry _contributionRegistry;
    private readonly VideoPlayerViewModel _sut;

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
        _contributionRegistry = Substitute.For<IContributionRegistry>();
        _sut = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService, _contributionRegistry);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── SkipNext ──

    [Fact]
    public async Task SkipNext_DelegatesToProvider_WhenActiveProviderReturnsFile()
    {
        var dir = CreateTempVideoDir("target.mp4");
        try
        {
            var targetPath = Path.Combine(dir, "target.mp4");
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(true);
            provider.GetNextFile().Returns(targetPath);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SkipNext();

            provider.Received(1).GetNextFile();
            await _engine.Received(1).LoadAsync(targetPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SkipNext_FallsBack_WhenProviderIsNotActive()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(false);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            await _sut.SkipNext();

            provider.DidNotReceive().GetNextFile();
            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SkipNext_FallsBack_WhenProviderReturnsNull()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(true);
            provider.GetNextFile().Returns((string?)null);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            await _sut.SkipNext();

            provider.Received(1).GetNextFile();
            // Falls back to sibling file navigation
            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SkipNext_FallsBack_WhenNoProviderRegistered()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _contributionRegistry.GetPlaylistProvider().Returns((IPlaylistProvider?)null);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            await _sut.SkipNext();

            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── SkipPrevious ──

    [Fact]
    public async Task SkipPrevious_DelegatesToProvider_WhenActiveProviderReturnsFile()
    {
        var dir = CreateTempVideoDir("target.mp4");
        try
        {
            var targetPath = Path.Combine(dir, "target.mp4");
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(true);
            provider.GetPreviousFile().Returns(targetPath);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SkipPrevious();

            provider.Received(1).GetPreviousFile();
            await _engine.Received(1).LoadAsync(targetPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SkipPrevious_FallsBack_WhenProviderIsNotActive()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(false);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[1]);
            _engine.ClearReceivedCalls();

            await _sut.SkipPrevious();

            provider.DidNotReceive().GetPreviousFile();
            await _engine.Received(1).LoadAsync(files[0]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SkipPrevious_FallsBack_WhenProviderReturnsNull()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(true);
            provider.GetPreviousFile().Returns((string?)null);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[1]);
            _engine.ClearReceivedCalls();

            await _sut.SkipPrevious();

            provider.Received(1).GetPreviousFile();
            await _engine.Received(1).LoadAsync(files[0]);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── OnEngineMediaEnded (auto-advance) ──

    [Fact]
    public async Task MediaEnded_DelegatesToProvider_WhenActive()
    {
        var dir = CreateTempVideoDir("target.mp4");
        try
        {
            var targetPath = Path.Combine(dir, "target.mp4");
            var provider = Substitute.For<IPlaylistProvider>();
            provider.IsActive.Returns(true);
            provider.GetNextFile().Returns(targetPath);
            _contributionRegistry.GetPlaylistProvider().Returns(provider);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            // Trigger OnEngineMediaEnded via the engine event
            _engine.MediaEnded += Raise.Event<Action>();

            // Give async LoadAndPlayAsync time to process
            await Task.Delay(100);

            provider.Received(1).GetNextFile();
            await _engine.Received(1).LoadAsync(targetPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task MediaEnded_FallsBack_WhenNoProvider()
    {
        var dir = CreateTempVideoDir("a.mp4", "b.mp4");
        try
        {
            var files = Directory.GetFiles(dir).OrderBy(f => f).ToArray();
            _contributionRegistry.GetPlaylistProvider().Returns((IPlaylistProvider?)null);
            _engine.Duration.Returns(TimeSpan.FromMinutes(1));

            await _sut.SetExplorerRootAsync(dir);
            await _sut.LoadAndPlayAsync(files[0]);
            _engine.ClearReceivedCalls();

            // Trigger OnEngineMediaEnded
            _engine.MediaEnded += Raise.Event<Action>();
            await Task.Delay(100);

            await _engine.Received(1).LoadAsync(files[1]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MediaEnded_DoesNotAdvance_WhenLooping()
    {
        var provider = Substitute.For<IPlaylistProvider>();
        provider.IsActive.Returns(true);
        _contributionRegistry.GetPlaylistProvider().Returns(provider);

        _sut.IsLooping = true;

        // Trigger OnEngineMediaEnded
        _engine.MediaEnded += Raise.Event<Action>();

        // Provider should not be consulted when looping (engine handles it)
        provider.DidNotReceive().GetNextFile();
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
