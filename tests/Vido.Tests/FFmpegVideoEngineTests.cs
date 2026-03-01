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

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public FFmpegVideoEngineTests()
    {
        _sut = new FFmpegVideoEngine(_logService);
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
    /// Verifies that Initial Volume is default.
    /// </summary>
    [Fact]
    public void InitialVolume_IsDefault()
    {
        Assert.Equal(75, _sut.Volume);
    }

    /// <summary>
    /// Verifies that Initial Muted is false.
    /// </summary>
    [Fact]
    public void InitialMuted_IsFalse()
    {
        Assert.False(_sut.IsMuted);
    }

    /// <summary>
    /// Verifies that Initial Looping is false.
    /// </summary>
    [Fact]
    public void InitialLooping_IsFalse()
    {
        Assert.False(_sut.IsLooping);
    }

    /// <summary>
    /// Verifies that Initial Metadata is null.
    /// </summary>
    [Fact]
    public void InitialMetadata_IsNull()
    {
        Assert.Null(_sut.CurrentMetadata);
    }

    // ── Volume ──

    /// <summary>
    /// Verifies that Volume clamps to range.
    /// </summary>
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

    /// <summary>
    /// Verifies that Is Muted can be toggled.
    /// </summary>
    [Fact]
    public void IsMuted_CanBeToggled()
    {
        _sut.IsMuted = true;
        Assert.True(_sut.IsMuted);

        _sut.IsMuted = false;
        Assert.False(_sut.IsMuted);
    }

    // ── Looping ──

    /// <summary>
    /// Verifies that Is Looping can be toggled.
    /// </summary>
    [Fact]
    public void IsLooping_CanBeToggled()
    {
        _sut.IsLooping = true;
        Assert.True(_sut.IsLooping);

        _sut.IsLooping = false;
        Assert.False(_sut.IsLooping);
    }

    // ── Precondition Checks ──

    /// <summary>
    /// Verifies that Load Async throws invalid operation when f fmpeg not initialized.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ThrowsInvalidOperation_WhenFFmpegNotInitialized()
    {
        // FFmpeg is not initialized in the test environment (no DLLs)
        if (FFmpegInitializer.IsInitialized)
            return; // Skip if FFmpeg happens to be available

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoadAsync(@"C:\nonexistent\video.mp4"));
    }

    /// <summary>
    /// Verifies that Play no op when no media loaded.
    /// </summary>
    [Fact]
    public void Play_NoOp_WhenNoMediaLoaded()
    {
        // Should not throw when no media is loaded
        _sut.Play();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    /// <summary>
    /// Verifies that Pause no op when no media loaded.
    /// </summary>
    [Fact]
    public void Pause_NoOp_WhenNoMediaLoaded()
    {
        _sut.Pause();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    /// <summary>
    /// Verifies that Stop no op when no media loaded.
    /// </summary>
    [Fact]
    public void Stop_NoOp_WhenNoMediaLoaded()
    {
        _sut.Stop();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    /// <summary>
    /// Verifies that Seek no op when no media loaded.
    /// </summary>
    [Fact]
    public void Seek_NoOp_WhenNoMediaLoaded()
    {
        _sut.Seek(TimeSpan.FromSeconds(10));

        Assert.Equal(PlaybackState.None, _sut.State);
        Assert.Equal(TimeSpan.Zero, _sut.Position);
    }

    // ── Dispose ──

    /// <summary>
    /// Verifies that Dispose can be called multiple times.
    /// </summary>
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();
        _sut.Dispose(); // Should not throw
    }

    // ── vb-001: Stop safety ──

    /// <summary>
    /// Verifies that Stop called twice does not throw.
    /// </summary>
    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        // Stop() disposes and recreates CTS. Calling twice should not throw
        // (double-dispose was a crash vector before vb-001 fix).
        _sut.Stop();
        _sut.Stop();

        Assert.Equal(PlaybackState.None, _sut.State);
    }

    /// <summary>
    /// Verifies that Stop then dispose does not throw.
    /// </summary>
    [Fact]
    public void Stop_ThenDispose_DoesNotThrow()
    {
        // After Stop() resets CTS, Dispose() should cleanly free everything.
        _sut.Stop();
        _sut.Dispose();
    }

    // ── vb-001: LoadAsync semaphore precondition checks ──

    /// <summary>
    /// Verifies that Load Async called concurrently precondition fails do not deadlock.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async after failed load can be called again.
    /// </summary>
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

    /// <summary>
    /// Verifies that Audio Samples Available event declared is null by default.
    /// </summary>
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

    /// <summary>
    /// Verifies that Audio Samples Available implements i video engine event.
    /// </summary>
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

    // ── SpeedRatio / Time-Stretch (vb-003) ──

    /// <summary>
    /// Verifies that Speed Ratio default is1.
    /// </summary>
    [Fact]
    public void SpeedRatio_DefaultIs1()
    {
        Assert.Equal(1.0, _sut.SpeedRatio);
    }

    /// <summary>
    /// Verifies that Speed Ratio set clamped within range.
    /// </summary>
    /// <param name="speed">The playback speed ratio.</param>
    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void SpeedRatio_Set_ClampedWithinRange(double speed)
    {
        _sut.SpeedRatio = speed;
        Assert.Equal(speed, _sut.SpeedRatio, precision: 3);
    }

    /// <summary>
    /// Verifies that Speed Ratio below minimum clamped to025.
    /// </summary>
    [Fact]
    public void SpeedRatio_BelowMinimum_ClampedTo025()
    {
        _sut.SpeedRatio = 0.1;
        Assert.Equal(0.25, _sut.SpeedRatio, precision: 3);
    }

    /// <summary>
    /// Verifies that Speed Ratio above maximum clamped to4.
    /// </summary>
    [Fact]
    public void SpeedRatio_AboveMaximum_ClampedTo4()
    {
        _sut.SpeedRatio = 10.0;
        Assert.Equal(4.0, _sut.SpeedRatio, precision: 3);
    }

    /// <summary>
    /// Verifies that Speed Ratio rapid changes does not throw.
    /// </summary>
    [Fact]
    public void SpeedRatio_RapidChanges_DoesNotThrow()
    {
        // Simulate rapid speed toggling — should not throw or deadlock
        for (int i = 0; i < 20; i++)
        {
            _sut.SpeedRatio = 2.0;
            _sut.SpeedRatio = 0.5;
            _sut.SpeedRatio = 1.0;
        }
    }

    /// <summary>
    /// Verifies that Speed Ratio same value no op.
    /// </summary>
    [Fact]
    public void SpeedRatio_SameValue_NoOp()
    {
        _sut.SpeedRatio = 1.5;
        // Setting same value again should be a no-op (no exception)
        _sut.SpeedRatio = 1.5;
        Assert.Equal(1.5, _sut.SpeedRatio, precision: 3);
    }

    /// <summary>
    /// Verifies that ReportMetrics returns immediately when debug logging is disabled.
    /// </summary>
    [Fact]
    public void ReportMetrics_DebugDisabled_DoesNotLog()
    {
        _logService.IsEnabled(LogLevel.Debug).Returns(false);

        var reportMetrics = typeof(FFmpegVideoEngine).GetMethod("ReportMetrics", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(reportMetrics);

        reportMetrics!.Invoke(_sut, null);

        _logService.DidNotReceive().Debug(Arg.Any<string>(), Arg.Any<string?>());
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        _sut.Dispose();
    }
}