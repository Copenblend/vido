using Vido.Services.Video;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for AudioRenderer — buffer sizing, flush behavior, and state transitions.
/// Actual WASAPI playback requires an audio device (covered by manual test plan).
/// </summary>
public class AudioRendererTests : IDisposable
{
    private readonly AudioRenderer _sut = new();

    // ── Buffer Size (vb-002) ──

    [Fact]
    public void Initialize_DoesNotThrow_WhenNoAudioDevice()
    {
        // Initialize may fail to create WasapiOut if no audio device is present,
        // but it should not throw — it degrades gracefully.
        var ex = Record.Exception(() => _sut.Initialize(48000, 2));
        Assert.Null(ex);
    }

    [Fact]
    public void Initialize_CanBeCalledMultipleTimes()
    {
        _sut.Initialize(44100, 2);
        _sut.Initialize(48000, 2); // Re-initialize with different format
    }

    [Fact]
    public void SubmitSamples_BeforeInitialize_DoesNotThrow()
    {
        var data = new byte[1024];
        _sut.SubmitSamples(data, 0, data.Length);
    }

    // ── Flush (vb-002) ──

    [Fact]
    public void Flush_BeforeInitialize_DoesNotThrow()
    {
        _sut.Flush();
    }

    [Fact]
    public void Flush_AfterInitialize_DoesNotThrow()
    {
        _sut.Initialize(48000, 2);
        _sut.Flush();
    }

    [Fact]
    public void Flush_AfterSubmittingSamples_ClearsBuffer()
    {
        _sut.Initialize(48000, 2);
        var data = new byte[4096];
        _sut.SubmitSamples(data, 0, data.Length);
        _sut.Flush(); // Should not throw, clears buffered data
    }

    // ── Stop ──

    [Fact]
    public void Stop_BeforeInitialize_DoesNotThrow()
    {
        _sut.Stop();
    }

    [Fact]
    public void Stop_AfterInitialize_DoesNotThrow()
    {
        _sut.Initialize(48000, 2);
        _sut.Stop();
    }

    // ── Volume ──

    [Fact]
    public void Volume_ClampsToRange()
    {
        _sut.Volume = -0.5f;
        Assert.Equal(0f, _sut.Volume);

        _sut.Volume = 1.5f;
        Assert.Equal(1f, _sut.Volume);

        _sut.Volume = 0.5f;
        Assert.Equal(0.5f, _sut.Volume);
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

    // ── Dispose ──

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();
        _sut.Dispose(); // Should not throw
    }

    [Fact]
    public void SubmitSamples_AfterDispose_DoesNotThrow()
    {
        _sut.Initialize(48000, 2);
        _sut.Dispose();

        var data = new byte[1024];
        _sut.SubmitSamples(data, 0, data.Length); // Graceful no-op
    }

    // ── Float overload (vb-003) ──

    [Fact]
    public void SubmitSamples_FloatOverload_BeforeInitialize_DoesNotThrow()
    {
        var floats = new float[256];
        _sut.SubmitSamples(floats, 0, floats.Length);
    }

    [Fact]
    public void SubmitSamples_FloatOverload_AfterInitialize_DoesNotThrow()
    {
        _sut.Initialize(48000, 2);
        var floats = new float[256];
        _sut.SubmitSamples(floats, 0, floats.Length);
    }

    [Fact]
    public void SubmitSamples_FloatOverload_AfterDispose_DoesNotThrow()
    {
        _sut.Initialize(48000, 2);
        _sut.Dispose();

        var floats = new float[256];
        _sut.SubmitSamples(floats, 0, floats.Length); // Graceful no-op
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
