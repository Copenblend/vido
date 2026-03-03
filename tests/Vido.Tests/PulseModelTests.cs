using Vido.Core.Models.Pulse;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-011: Pulse model types integrated into Vido.Core.
/// Covers <see cref="PulseState"/>, <see cref="BeatEvent"/>, <see cref="BeatMap"/>,
/// <see cref="BpmEstimate"/>, and <see cref="PulseAnalysisResult"/>.
/// </summary>
public sealed class PulseModelTests
{
    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PulseState Enum Tests                                          ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that PulseState has exactly 5 values.
    /// </summary>
    [Fact]
    public void PulseState_HasFiveValues()
    {
        var values = Enum.GetValues<PulseState>();

        Assert.Equal(5, values.Length);
    }

    /// <summary>
    /// Verifies all expected PulseState values exist and have correct ordinal values.
    /// </summary>
    [Theory]
    [InlineData(PulseState.Inactive, 0)]
    [InlineData(PulseState.Analyzing, 1)]
    [InlineData(PulseState.Ready, 2)]
    [InlineData(PulseState.Active, 3)]
    [InlineData(PulseState.Error, 4)]
    public void PulseState_HasExpectedOrdinalValue(PulseState state, int expectedOrdinal)
    {
        Assert.Equal(expectedOrdinal, (int)state);
    }

    /// <summary>
    /// Verifies that default PulseState is Inactive.
    /// </summary>
    [Fact]
    public void PulseState_Default_IsInactive()
    {
        PulseState state = default;

        Assert.Equal(PulseState.Inactive, state);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ BeatEvent Tests                                                ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies BeatEvent default property values.
    /// </summary>
    [Fact]
    public void BeatEvent_Defaults_AreZeroAndFalse()
    {
        var beat = new BeatEvent();

        Assert.Equal(0.0, beat.TimestampMs);
        Assert.Equal(0.0, beat.Strength);
        Assert.False(beat.IsQuantized);
    }

    /// <summary>
    /// Verifies BeatEvent init properties are settable.
    /// </summary>
    [Fact]
    public void BeatEvent_InitProperties_SetCorrectly()
    {
        var beat = new BeatEvent
        {
            TimestampMs = 1500.5,
            Strength = 0.85,
            IsQuantized = true
        };

        Assert.Equal(1500.5, beat.TimestampMs);
        Assert.Equal(0.85, beat.Strength);
        Assert.True(beat.IsQuantized);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ BeatMap Tests                                                  ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies BeatMap defaults to empty beats and waveform.
    /// </summary>
    [Fact]
    public void BeatMap_Defaults_AreEmpty()
    {
        var map = new BeatMap();

        Assert.Empty(map.Beats);
        Assert.Equal(0.0, map.Bpm);
        Assert.Equal(0.0, map.BpmConfidence);
        Assert.Equal(0.0, map.DurationMs);
        Assert.Empty(map.WaveformSamples);
        Assert.Equal(0, map.WaveformSampleRate);
    }

    /// <summary>
    /// Verifies BeatMap can hold sorted beats with BPM and confidence.
    /// </summary>
    [Fact]
    public void BeatMap_WithBeats_HoldsSortedData()
    {
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 500, Strength = 0.9 },
            new() { TimestampMs = 1000, Strength = 0.8 },
            new() { TimestampMs = 1500, Strength = 0.7 },
        };

        var map = new BeatMap
        {
            Beats = beats,
            Bpm = 120.0,
            BpmConfidence = 0.95,
            DurationMs = 60000.0,
        };

        Assert.Equal(3, map.Beats.Count);
        Assert.Equal(500, map.Beats[0].TimestampMs);
        Assert.Equal(1000, map.Beats[1].TimestampMs);
        Assert.Equal(1500, map.Beats[2].TimestampMs);
        Assert.Equal(120.0, map.Bpm);
        Assert.Equal(0.95, map.BpmConfidence);
        Assert.Equal(60000.0, map.DurationMs);
    }

    /// <summary>
    /// Verifies BeatMap can hold waveform data.
    /// </summary>
    [Fact]
    public void BeatMap_WithWaveform_HoldsData()
    {
        var waveform = new float[] { 0.1f, 0.5f, 0.3f, 0.8f };

        var map = new BeatMap
        {
            WaveformSamples = waveform,
            WaveformSampleRate = 44100,
        };

        Assert.Equal(4, map.WaveformSamples.Count);
        Assert.Equal(0.1f, map.WaveformSamples[0]);
        Assert.Equal(44100, map.WaveformSampleRate);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ BpmEstimate Tests                                              ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies BpmEstimate default property values.
    /// </summary>
    [Fact]
    public void BpmEstimate_Defaults_AreZero()
    {
        var estimate = new BpmEstimate();

        Assert.Equal(0.0, estimate.Bpm);
        Assert.Equal(0.0, estimate.Confidence);
        Assert.Equal(0.0, estimate.PhaseOffsetMs);
    }

    /// <summary>
    /// Verifies BpmEstimate init properties are settable.
    /// </summary>
    [Fact]
    public void BpmEstimate_InitProperties_SetCorrectly()
    {
        var estimate = new BpmEstimate
        {
            Bpm = 128.0,
            Confidence = 0.92,
            PhaseOffsetMs = 42.5
        };

        Assert.Equal(128.0, estimate.Bpm);
        Assert.Equal(0.92, estimate.Confidence);
        Assert.Equal(42.5, estimate.PhaseOffsetMs);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PulseAnalysisResult Tests                                      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies PulseAnalysisResult default property values.
    /// </summary>
    [Fact]
    public void PulseAnalysisResult_Defaults_AreZeroAndEmpty()
    {
        var result = new PulseAnalysisResult();

        Assert.Equal(0.0, result.TimestampMs);
        Assert.Equal(0.0, result.RmsAmplitude);
        Assert.Empty(result.WaveformSamples);
    }

    /// <summary>
    /// Verifies PulseAnalysisResult init properties are settable.
    /// </summary>
    [Fact]
    public void PulseAnalysisResult_InitProperties_SetCorrectly()
    {
        var samples = new float[] { 0.2f, -0.3f, 0.5f };
        var result = new PulseAnalysisResult
        {
            TimestampMs = 2000.0,
            RmsAmplitude = 0.65,
            WaveformSamples = samples,
        };

        Assert.Equal(2000.0, result.TimestampMs);
        Assert.Equal(0.65, result.RmsAmplitude);
        Assert.Equal(3, result.WaveformSamples.Length);
        Assert.Equal(0.2f, result.WaveformSamples[0]);
    }
}
