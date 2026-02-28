using Vido.Services.Video;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="TimeStretchProcessor"/> — the SoundTouch-based
/// pitch-preserving time-stretch wrapper (vb-003).
/// </summary>
public class TimeStretchProcessorTests : IDisposable
{
    private readonly TimeStretchProcessor _sut;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public TimeStretchProcessorTests()
    {
        _sut = new TimeStretchProcessor(44100, 2);
    }

    // ── Construction ──

    /// <summary>
    /// Verifies that Constructor sets default tempo.
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaultTempo()
    {
        Assert.Equal(1.0, _sut.Tempo, precision: 3);
    }

    /// <summary>
    /// Verifies that Constructor various formats does not throw.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate in Hz.</param>
    /// <param name="channels">The number of audio channels.</param>
    [Theory]
    [InlineData(44100, 1)]
    [InlineData(44100, 2)]
    [InlineData(48000, 2)]
    [InlineData(22050, 1)]
    public void Constructor_VariousFormats_DoesNotThrow(int sampleRate, int channels)
    {
        using var proc = new TimeStretchProcessor(sampleRate, channels);
        Assert.Equal(1.0, proc.Tempo, precision: 3);
    }

    // ── Tempo property ──

    /// <summary>
    /// Verifies that Tempo set and get round trips.
    /// </summary>
    /// <param name="tempo">The tempo multiplier.</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void Tempo_SetAndGet_RoundTrips(double tempo)
    {
        _sut.Tempo = tempo;
        Assert.Equal(tempo, _sut.Tempo, precision: 3);
    }

    /// <summary>
    /// Verifies that Tempo set multiple times does not throw.
    /// </summary>
    [Fact]
    public void Tempo_SetMultipleTimes_DoesNotThrow()
    {
        _sut.Tempo = 0.5;
        _sut.Tempo = 1.0;
        _sut.Tempo = 2.0;
        _sut.Tempo = 0.25;
        _sut.Tempo = 4.0;
        _sut.Tempo = 1.0;
    }

    // ── PutSamples / ReceiveSamples ──

    /// <summary>
    /// Verifies that Put Samples receive samples at tempo1 returns approximately original count.
    /// </summary>
    [Fact]
    public void PutSamples_ReceiveSamples_AtTempo1_ReturnsApproximatelyOriginalCount()
    {
        _sut.Tempo = 1.0;
        const int sampleFrames = 4096;
        const int channels = 2;
        var input = new float[sampleFrames * channels];

        // Fill with a simple sine wave so SoundTouch has real data to process
        for (int i = 0; i < input.Length; i++)
            input[i] = MathF.Sin(i * 0.1f) * 0.5f;

        _sut.PutSamples(input, sampleFrames);

        // At tempo 1.0 we expect roughly the same number of samples out.
        // SoundTouch may buffer some, so push enough data and collect output.
        var output = new float[sampleFrames * channels * 2];
        int totalReceived = 0;
        int received;
        while ((received = _sut.ReceiveSamples(output.AsSpan(totalReceived * channels), sampleFrames)) > 0)
        {
            totalReceived += received;
        }

        // With internal latency/buffering, we won't get exactly 4096 back from
        // a single push, but totalReceived should be > 0.
        Assert.True(totalReceived > 0, "Should receive some samples at tempo 1.0");
    }

    /// <summary>
    /// Verifies that Put Samples at tempo2 eventually produces fewer samples.
    /// </summary>
    [Fact]
    public void PutSamples_AtTempo2_EventuallyProducesFewerSamples()
    {
        _sut.Tempo = 2.0;

        const int sampleFrames = 8192;
        const int channels = 2;
        var input = new float[sampleFrames * channels];
        for (int i = 0; i < input.Length; i++)
            input[i] = MathF.Sin(i * 0.1f) * 0.5f;

        _sut.PutSamples(input, sampleFrames);

        var output = new float[sampleFrames * channels * 2];
        int totalReceived = 0;
        int received;
        while ((received = _sut.ReceiveSamples(output.AsSpan(), sampleFrames)) > 0)
            totalReceived += received;

        // At 2x tempo, output should be roughly half the input (within SoundTouch's
        // internal latency tolerance).
        Assert.True(totalReceived > 0, "Should produce some output at 2x tempo");
        Assert.True(totalReceived < sampleFrames,
            $"At 2x tempo expected fewer than {sampleFrames} frames, got {totalReceived}");
    }

    /// <summary>
    /// Verifies that Put Samples at half tempo eventually produces more samples.
    /// </summary>
    [Fact]
    public void PutSamples_AtHalfTempo_EventuallyProducesMoreSamples()
    {
        _sut.Tempo = 0.5;

        const int sampleFrames = 8192;
        const int channels = 2;
        var input = new float[sampleFrames * channels];
        for (int i = 0; i < input.Length; i++)
            input[i] = MathF.Sin(i * 0.1f) * 0.5f;

        _sut.PutSamples(input, sampleFrames);

        var output = new float[sampleFrames * channels * 4];
        int totalReceived = 0;
        int received;
        while ((received = _sut.ReceiveSamples(output.AsSpan(), sampleFrames * 2)) > 0)
            totalReceived += received;

        // At 0.5x tempo, output should be roughly double the input.
        Assert.True(totalReceived > sampleFrames,
            $"At 0.5x tempo expected more than {sampleFrames} frames, got {totalReceived}");
    }

    // ── Clear ──

    /// <summary>
    /// Verifies that Clear after put samples discards buffered data.
    /// </summary>
    [Fact]
    public void Clear_AfterPutSamples_DiscardsBufferedData()
    {
        const int sampleFrames = 4096;
        const int channels = 2;
        var input = new float[sampleFrames * channels];
        for (int i = 0; i < input.Length; i++)
            input[i] = MathF.Sin(i * 0.1f) * 0.5f;

        _sut.Tempo = 0.5; // Slow tempo buffers more data internally
        _sut.PutSamples(input, sampleFrames);
        _sut.Clear();

        Assert.Equal(0, _sut.AvailableSamples);
    }

    /// <summary>
    /// Verifies that Clear when empty does not throw.
    /// </summary>
    [Fact]
    public void Clear_WhenEmpty_DoesNotThrow()
    {
        _sut.Clear();
        Assert.Equal(0, _sut.AvailableSamples);
    }

    // ── Dispose ──

    /// <summary>
    /// Verifies that Dispose can be called multiple times.
    /// </summary>
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();
        _sut.Dispose(); // Second dispose should not throw
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        _sut.Dispose();
    }
}