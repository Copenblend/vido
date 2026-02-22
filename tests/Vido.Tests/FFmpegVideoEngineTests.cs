using NSubstitute;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Services.Video;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for FFmpegVideoEngine.
/// Tests focus on initial state, precondition validation, and state machine behavior.
/// Actual FFmpeg decoding requires DLLs and real video files (covered by manual test plan).
/// </summary>
public class FFmpegVideoEngineTests : IDisposable
{
    private readonly ILogService _logService = Substitute.For<ILogService>();
    private readonly FFmpegVideoEngine _sut;

    public FFmpegVideoEngineTests()
    {
        _sut = new FFmpegVideoEngine(_logService);
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
    public void InitialVolume_IsDefault()
    {
        Assert.Equal(75, _sut.Volume);
    }

    [Fact]
    public void InitialMuted_IsFalse()
    {
        Assert.False(_sut.IsMuted);
    }

    [Fact]
    public void InitialLooping_IsFalse()
    {
        Assert.False(_sut.IsLooping);
    }

    [Fact]
    public void InitialMetadata_IsNull()
    {
        Assert.Null(_sut.CurrentMetadata);
    }

    // ── Volume ──

    [Fact]
    public void Volume_ClampsToRange()
    {
        _sut.Volume = -10;
        Assert.Equal(0, _sut.Volume);

        _sut.Volume = 150;
        Assert.Equal(100, _sut.Volume);

        _sut.Volume = 50;
        Assert.Equal(50, _sut.Volume);
    }

    // ── Mute ──

    [Fact]
    public void IsMuted_CanBeToggled()
    {
        _sut.IsMuted = true;
        Assert.True(_sut.IsMuted);

        _sut.IsMuted = false;
        Assert.False(_sut.IsMuted);
    }

    // ── Looping ──

    [Fact]
    public void IsLooping_CanBeToggled()
    {
        _sut.IsLooping = true;
        Assert.True(_sut.IsLooping);

        _sut.IsLooping = false;
        Assert.False(_sut.IsLooping);
    }

    // ── Precondition Checks ──

    [Fact]
    public async Task LoadAsync_ThrowsInvalidOperation_WhenFFmpegNotInitialized()
    {
        // FFmpeg is not initialized in the test environment (no DLLs)
        if (FFmpegInitializer.IsInitialized)
            return; // Skip if FFmpeg happens to be available

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoadAsync(@"C:\nonexistent\video.mp4"));
    }

    [Fact]
    public void Play_NoOp_WhenNoMediaLoaded()
    {
        // Should not throw when no media is loaded
        _sut.Play();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    [Fact]
    public void Pause_NoOp_WhenNoMediaLoaded()
    {
        _sut.Pause();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    [Fact]
    public void Stop_NoOp_WhenNoMediaLoaded()
    {
        _sut.Stop();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    [Fact]
    public void Seek_NoOp_WhenNoMediaLoaded()
    {
        _sut.Seek(TimeSpan.FromSeconds(10));

        Assert.Equal(PlaybackState.None, _sut.State);
        Assert.Equal(TimeSpan.Zero, _sut.Position);
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();
        _sut.Dispose(); // Should not throw
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
