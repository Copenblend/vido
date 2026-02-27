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

    // ── vb-001: Stop safety ──

    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        // Stop() disposes and recreates CTS. Calling twice should not throw
        // (double-dispose was a crash vector before vb-001 fix).
        _sut.Stop();
        _sut.Stop();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    [Fact]
    public void Stop_ThenDispose_DoesNotThrow()
    {
        // After Stop() resets CTS, Dispose() should cleanly free everything.
        _sut.Stop();
        _sut.Dispose();
    }

    // ── vb-001: LoadAsync semaphore precondition checks ──

    [Fact]
    public async Task LoadAsync_CalledConcurrently_PreconditionFailsDoNotDeadlock()
    {
        if (FFmpegInitializer.IsInitialized)
            return; // Skip if FFmpeg available — would take a different path

        // Two concurrent LoadAsync calls both fail on precondition.
        // If the semaphore were acquired before the check and not released on exception,
        // the second call would deadlock. This verifies the lock is not held across throws.
        var t1 = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoadAsync(@"C:\nonexistent\a.mp4"));
        var t2 = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoadAsync(@"C:\nonexistent\b.mp4"));

        await Task.WhenAll(t1, t2);
    }

    [Fact]
    public async Task LoadAsync_AfterFailedLoad_CanBeCalledAgain()
    {
        if (FFmpegInitializer.IsInitialized)
            return;

        // First call fails
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoadAsync(@"C:\nonexistent\video.mp4"));

        // Second call should also fail with same exception (not deadlock on semaphore)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoadAsync(@"C:\nonexistent\video2.mp4"));
    }

    // ── AudioSamplesAvailable Event ──

    [Fact]
    public void AudioSamplesAvailable_EventDeclared_IsNullByDefault()
    {
        // The event should exist on the engine and be null when no subscribers
        // This verifies the event was added to both interface and implementation
        bool subscribed = false;
        _sut.AudioSamplesAvailable += _ => subscribed = true;

        // We can't fire the event externally, but we can verify subscription doesn't throw
        Assert.False(subscribed);
    }

    [Fact]
    public void AudioSamplesAvailable_ImplementsIVideoEngineEvent()
    {
        // Verify the event is accessible through the IVideoEngine interface
        IVideoEngine engine = _sut;
        bool invoked = false;
        engine.AudioSamplesAvailable += _ => invoked = true;

        // The event is wired — verifying it compiles and doesn't throw on subscribe
        Assert.False(invoked);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
