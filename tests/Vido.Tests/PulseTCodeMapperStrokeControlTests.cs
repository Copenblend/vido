using Vido.Core.Models.Pulse;
using Vido.Services.Pulse;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for vido-195: PulseTCodeMapper stroke controls —
/// amplitude offset, easing blend, stroke patterns, and randomness.
/// </summary>
public sealed class PulseTCodeMapperStrokeControlTests
{
    // ── Test helpers ──

    /// <summary>Creates a simple beat map with evenly spaced beats at the given BPM.</summary>
    private static BeatMap CreateBeatMap(double bpm = 120.0, int beatCount = 10, double strength = 1.0)
    {
        double intervalMs = 60000.0 / bpm;
        var beats = new BeatEvent[beatCount];
        for (int i = 0; i < beatCount; i++)
        {
            beats[i] = new BeatEvent
            {
                TimestampMs = i * intervalMs,
                Strength = strength,
                IsQuantized = true,
            };
        }

        return new BeatMap
        {
            Beats = beats,
            Bpm = bpm,
            BpmConfidence = 1.0,
            DurationMs = beatCount * intervalMs,
        };
    }

    /// <summary>Creates a mapper with the specified stroke settings.</summary>
    private static PulseTCodeMapper CreateMapper(PulseStrokeSettings? settings = null)
    {
        var mapper = new PulseTCodeMapper();
        if (settings is not null)
            mapper.SetStrokeSettings(settings);
        return mapper;
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Default Settings Regression Tests                              ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that default settings produce the same output as before stroke controls were added.
    /// </summary>
    [Fact]
    public void MapToPosition_DefaultSettings_ProducesSameOutputAsOriginal()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4);
        var mapper = CreateMapper();

        // At the start of beat 0, amplitude 1.0, position should be at the bottom of stroke.
        double posAtBeatStart = mapper.MapToPosition(beatMap, 0.0, 1.0);

        // Reset and test with explicit Default settings.
        var mapperWithDefault = CreateMapper(PulseStrokeSettings.Default);
        double posWithDefault = mapperWithDefault.MapToPosition(beatMap, 0.0, 1.0);

        Assert.Equal(posAtBeatStart, posWithDefault, precision: 10);
    }

    /// <summary>
    /// Verifies that null settings input falls back to Default.
    /// </summary>
    [Fact]
    public void SetStrokeSettings_Null_FallsBackToDefault()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4);
        var mapper = CreateMapper(new PulseStrokeSettings { AmplitudeOffset = 1.0 });

        // Now set null to reset.
        mapper.SetStrokeSettings(null);
        mapper.Reset();

        double pos = mapper.MapToPosition(beatMap, 0.0, 0.5);
        var defaultMapper = CreateMapper();
        double defaultPos = defaultMapper.MapToPosition(beatMap, 0.0, 0.5);

        Assert.Equal(defaultPos, pos, precision: 10);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Amplitude Offset Tests                                         ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// At amplitude offset +1.0, strokes should be full range regardless of audio amplitude.
    /// </summary>
    [Fact]
    public void AmplitudeOffset_PlusOne_ProducesFullRangeStrokes()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4, strength: 1.0);
        var settings = new PulseStrokeSettings { AmplitudeOffset = 1.0 };
        var mapper = CreateMapper(settings);

        // At midpoint of upstroke (phase ≈ 0.2 of a 500ms interval = 100ms), with low amplitude.
        // Full range should mean top ≈ 95, bottom ≈ 5.
        // Test at phase=0 (bottom) and at peak of upstroke.
        double posAtStart = mapper.MapToPosition(beatMap, 0.0, 0.1);
        mapper.Reset();

        // At the upstroke fraction (40% of 500ms = 200ms), position should be near top.
        double posAtUpstrokePeak = mapper.MapToPosition(beatMap, 200.0, 0.1);

        // Start should be near bottom (5), peak should be near top (95).
        Assert.InRange(posAtStart, 5.0, 15.0);
        Assert.InRange(posAtUpstrokePeak, 85.0, 95.0);
    }

    /// <summary>
    /// At amplitude offset -1.0, strokes should produce zero movement (rest position).
    /// </summary>
    [Fact]
    public void AmplitudeOffset_MinusOne_ProducesZeroMovement()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4, strength: 1.0);
        var settings = new PulseStrokeSettings { AmplitudeOffset = -1.0 };
        var mapper = CreateMapper(settings);

        double pos1 = mapper.MapToPosition(beatMap, 0.0, 1.0);
        double pos2 = mapper.MapToPosition(beatMap, 100.0, 1.0);
        double pos3 = mapper.MapToPosition(beatMap, 250.0, 1.0);

        // All positions should be at or very near RestPosition (50).
        Assert.Equal(50.0, pos1, precision: 1);
        Assert.Equal(50.0, pos2, precision: 1);
        Assert.Equal(50.0, pos3, precision: 1);
    }

    /// <summary>
    /// At amplitude offset 0.0, behavior should be unchanged from default.
    /// </summary>
    [Fact]
    public void AmplitudeOffset_Zero_IsUnchanged()
    {
        var beatMap = CreateBeatMap();
        var defaultMapper = CreateMapper();
        var zeroMapper = CreateMapper(new PulseStrokeSettings { AmplitudeOffset = 0.0 });

        double defPos = defaultMapper.MapToPosition(beatMap, 100.0, 0.5);
        double zeroPos = zeroMapper.MapToPosition(beatMap, 100.0, 0.5);

        Assert.Equal(defPos, zeroPos, precision: 10);
    }

    /// <summary>
    /// Positive amplitude offset increases stroke size vs. default.
    /// </summary>
    [Fact]
    public void AmplitudeOffset_Positive_IncreasesStrokeRange()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4, strength: 1.0);

        // Compare at the peak of the upstroke with moderate amplitude.
        var defaultMapper = CreateMapper();
        double defaultPeak = defaultMapper.MapToPosition(beatMap, 200.0, 0.3);

        var boostedMapper = CreateMapper(new PulseStrokeSettings { AmplitudeOffset = 0.5 });
        double boostedPeak = boostedMapper.MapToPosition(beatMap, 200.0, 0.3);

        Assert.True(boostedPeak > defaultPeak, "Positive amplitude offset should increase peak position.");
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Easing Blend Tests                                             ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// At easing blend 0.0, BlendedEaseOut should equal EaseOutQuad.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void BlendedEaseOut_ZeroBlend_EqualsEaseOutQuad(double t)
    {
        double expected = 1.0 - (1.0 - t) * (1.0 - t); // EaseOutQuad
        double actual = PulseTCodeMapper.BlendedEaseOut(t, 0.0);

        Assert.Equal(expected, actual, precision: 10);
    }

    /// <summary>
    /// At easing blend +1.0, BlendedEaseOut should equal linear t.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void BlendedEaseOut_PlusOneBlend_EqualsLinear(double t)
    {
        double actual = PulseTCodeMapper.BlendedEaseOut(t, 1.0);

        Assert.Equal(t, actual, precision: 10);
    }

    /// <summary>
    /// At easing blend -1.0, BlendedEaseOut should equal sin(t × π/2).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void BlendedEaseOut_MinusOneBlend_EqualsSinusoidal(double t)
    {
        double expected = Math.Sin(t * Math.PI / 2.0);
        double actual = PulseTCodeMapper.BlendedEaseOut(t, -1.0);

        Assert.Equal(expected, actual, precision: 10);
    }

    /// <summary>
    /// At easing blend 0.0, BlendedEaseIn should equal EaseInQuad.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void BlendedEaseIn_ZeroBlend_EqualsEaseInQuad(double t)
    {
        double expected = t * t; // EaseInQuad
        double actual = PulseTCodeMapper.BlendedEaseIn(t, 0.0);

        Assert.Equal(expected, actual, precision: 10);
    }

    /// <summary>
    /// At easing blend +1.0, BlendedEaseIn should equal linear t.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void BlendedEaseIn_PlusOneBlend_EqualsLinear(double t)
    {
        double actual = PulseTCodeMapper.BlendedEaseIn(t, 1.0);

        Assert.Equal(t, actual, precision: 10);
    }

    /// <summary>
    /// At easing blend -1.0, BlendedEaseIn should equal 1 - sin((1-t) × π/2).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void BlendedEaseIn_MinusOneBlend_EqualsSinusoidal(double t)
    {
        double expected = 1.0 - Math.Sin((1.0 - t) * Math.PI / 2.0);
        double actual = PulseTCodeMapper.BlendedEaseIn(t, -1.0);

        Assert.Equal(expected, actual, precision: 10);
    }

    /// <summary>
    /// All easing functions return 0.0 at t=0 and 1.0 at t=1, regardless of blend.
    /// </summary>
    [Theory]
    [InlineData(-1.0)]
    [InlineData(-0.5)]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void BlendedEasing_BoundaryValues_ZeroAndOne(double blend)
    {
        Assert.Equal(0.0, PulseTCodeMapper.BlendedEaseOut(0.0, blend), precision: 10);
        Assert.Equal(1.0, PulseTCodeMapper.BlendedEaseOut(1.0, blend), precision: 10);
        Assert.Equal(0.0, PulseTCodeMapper.BlendedEaseIn(0.0, blend), precision: 10);
        Assert.Equal(1.0, PulseTCodeMapper.BlendedEaseIn(1.0, blend), precision: 10);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Stroke Pattern Tests                                           ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Classic pattern with default settings should match the original behavior.
    /// </summary>
    [Fact]
    public void Pattern_Classic_MatchesDefault()
    {
        var beatMap = CreateBeatMap();
        var defaultMapper = CreateMapper();
        var classicMapper = CreateMapper(new PulseStrokeSettings { Pattern = StrokePattern.Classic });

        double defPos = defaultMapper.MapToPosition(beatMap, 150.0, 0.8);
        double classicPos = classicMapper.MapToPosition(beatMap, 150.0, 0.8);

        Assert.Equal(defPos, classicPos, precision: 10);
    }

    /// <summary>
    /// DoubleTap pattern should produce a different position than Classic at the same time.
    /// </summary>
    [Fact]
    public void Pattern_DoubleTap_DiffersFromClassic()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4);

        var classicMapper = CreateMapper(new PulseStrokeSettings { Pattern = StrokePattern.Classic });
        double classicPos = classicMapper.MapToPosition(beatMap, 300.0, 0.8);

        var doubleTapMapper = CreateMapper(new PulseStrokeSettings { Pattern = StrokePattern.DoubleTap });
        double doubleTapPos = doubleTapMapper.MapToPosition(beatMap, 300.0, 0.8);

        // At phase 0.6 (300ms of 500ms), Classic is on downstroke but DoubleTap is in its second sub-cycle.
        Assert.NotEqual(classicPos, doubleTapPos, precision: 1);
    }

    /// <summary>
    /// TripleTap pattern should produce a different position than Classic at the same time.
    /// </summary>
    [Fact]
    public void Pattern_TripleTap_DiffersFromClassic()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4);

        var classicMapper = CreateMapper(new PulseStrokeSettings { Pattern = StrokePattern.Classic });
        double classicPos = classicMapper.MapToPosition(beatMap, 350.0, 0.8);

        var tripleTapMapper = CreateMapper(new PulseStrokeSettings { Pattern = StrokePattern.TripleTap });
        double tripleTapPos = tripleTapMapper.MapToPosition(beatMap, 350.0, 0.8);

        Assert.NotEqual(classicPos, tripleTapPos, precision: 1);
    }

    /// <summary>
    /// HoldTop should hold near the top position during the hold phase (30-70% of interval).
    /// </summary>
    [Fact]
    public void Pattern_HoldTop_HoldsNearTopDuringHoldPhase()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4, strength: 1.0);
        var settings = new PulseStrokeSettings
        {
            Pattern = StrokePattern.HoldTop,
            AmplitudeOffset = 1.0, // Full range for clear testing.
        };
        var mapper = CreateMapper(settings);

        // Hold phase is 30-70% of interval. At 120 BPM, interval = 500ms.
        // So hold phase is 150ms–350ms. Test at 250ms (50% = middle of hold).
        double posAtHold = mapper.MapToPosition(beatMap, 250.0, 1.0);

        // For HoldTop, top position is the target. With full amplitude, top ≈ 95.
        Assert.InRange(posAtHold, 85.0, 95.0);
    }

    /// <summary>
    /// HoldBottom should hold near the bottom position during the hold phase.
    /// </summary>
    [Fact]
    public void Pattern_HoldBottom_HoldsNearBottomDuringHoldPhase()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4, strength: 1.0);
        var settings = new PulseStrokeSettings
        {
            Pattern = StrokePattern.HoldBottom,
            AmplitudeOffset = 1.0,
        };
        var mapper = CreateMapper(settings);

        // Hold phase at 250ms (50% of 500ms interval).
        double posAtHold = mapper.MapToPosition(beatMap, 250.0, 1.0);

        // For HoldBottom, bottom position is the target. With full amplitude, bottom ≈ 5.
        Assert.InRange(posAtHold, 5.0, 15.0);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Randomness Tests                                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// At randomness 0.0, positions should be identical to default.
    /// </summary>
    [Fact]
    public void Randomness_Zero_IsIdenticalToDefault()
    {
        var beatMap = CreateBeatMap();
        var defaultMapper = CreateMapper();
        var noRandomMapper = CreateMapper(new PulseStrokeSettings { Randomness = 0.0 });

        double defPos = defaultMapper.MapToPosition(beatMap, 150.0, 0.7);
        double noRandPos = noRandomMapper.MapToPosition(beatMap, 150.0, 0.7);

        Assert.Equal(defPos, noRandPos, precision: 10);
    }

    /// <summary>
    /// At randomness 1.0, different beats should produce different stroke intensities.
    /// </summary>
    [Fact]
    public void Randomness_Full_ProducesDifferentIntensitiesPerBeat()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 10, strength: 1.0);
        var settings = new PulseStrokeSettings { Randomness = 1.0 };

        // Sample the peak of upstroke for multiple beats.
        var peaks = new double[5];
        for (int i = 0; i < 5; i++)
        {
            var mapper = CreateMapper(settings);
            // Peak is at 40% of 500ms = 200ms into each beat.
            double peakTime = i * 500.0 + 200.0;
            peaks[i] = mapper.MapToPosition(beatMap, peakTime, 0.8);
        }

        // Not all peaks should be identical (randomness varies them).
        bool allSame = true;
        for (int i = 1; i < peaks.Length; i++)
        {
            if (Math.Abs(peaks[i] - peaks[0]) > 0.1)
            {
                allSame = false;
                break;
            }
        }

        Assert.False(allSame, "With full randomness, not all beat peaks should be identical.");
    }

    /// <summary>
    /// Randomness should be deterministic — same inputs produce same outputs.
    /// </summary>
    [Fact]
    public void Randomness_IsDeterministic()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4);
        var settings = new PulseStrokeSettings { Randomness = 0.8 };

        var mapper1 = CreateMapper(settings);
        double pos1 = mapper1.MapToPosition(beatMap, 150.0, 0.6);

        var mapper2 = CreateMapper(settings);
        double pos2 = mapper2.MapToPosition(beatMap, 150.0, 0.6);

        Assert.Equal(pos1, pos2, precision: 10);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PseudoRandom Tests                                             ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// PseudoRandom should return values in [0, 1].
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void PseudoRandom_ReturnsValueInUnitRange(int seed)
    {
        double value = PulseTCodeMapper.PseudoRandom(seed);

        Assert.InRange(value, 0.0, 1.0);
    }

    /// <summary>
    /// PseudoRandom should be deterministic — same seed gives same result.
    /// </summary>
    [Fact]
    public void PseudoRandom_SameSeed_SameResult()
    {
        double a = PulseTCodeMapper.PseudoRandom(73856093);
        double b = PulseTCodeMapper.PseudoRandom(73856093);

        Assert.Equal(a, b);
    }

    /// <summary>
    /// PseudoRandom should produce different values for different seeds.
    /// </summary>
    [Fact]
    public void PseudoRandom_DifferentSeeds_DifferentResults()
    {
        var values = new HashSet<double>();
        for (int i = 0; i < 100; i++)
        {
            values.Add(PulseTCodeMapper.PseudoRandom(i * 73856093));
        }

        // At least 90 distinct values out of 100.
        Assert.True(values.Count >= 90, $"Expected at least 90 distinct values but got {values.Count}.");
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Combined Settings Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Combined amplitude offset and easing blend should produce valid output.
    /// </summary>
    [Fact]
    public void CombinedSettings_AmplitudeAndEasing_ProducesValidOutput()
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 4);
        var settings = new PulseStrokeSettings
        {
            AmplitudeOffset = 0.5,
            EasingBlend = -0.7,
        };
        var mapper = CreateMapper(settings);

        double pos = mapper.MapToPosition(beatMap, 150.0, 0.6);

        Assert.InRange(pos, 5.0, 95.0);
    }

    /// <summary>
    /// All four controls combined should still produce output in valid range.
    /// </summary>
    [Theory]
    [InlineData(-1.0, -1.0, StrokePattern.Classic, 0.0)]
    [InlineData(1.0, 1.0, StrokePattern.DoubleTap, 1.0)]
    [InlineData(0.5, -0.5, StrokePattern.TripleTap, 0.5)]
    [InlineData(-0.5, 0.5, StrokePattern.HoldTop, 0.3)]
    [InlineData(0.0, 0.0, StrokePattern.HoldBottom, 1.0)]
    public void CombinedSettings_AllControls_OutputInValidRange(
        double amplitude, double easing, StrokePattern pattern, double randomness)
    {
        var beatMap = CreateBeatMap(bpm: 120.0, beatCount: 10, strength: 0.8);
        var settings = new PulseStrokeSettings
        {
            AmplitudeOffset = amplitude,
            EasingBlend = easing,
            Pattern = pattern,
            Randomness = randomness,
        };
        var mapper = CreateMapper(settings);

        // Sample multiple time points across a beat.
        for (int i = 0; i <= 20; i++)
        {
            double timeMs = i * 25.0; // 0 to 500ms in 25ms steps.
            double pos = mapper.MapToPosition(beatMap, timeMs, 0.7);
            Assert.InRange(pos, 5.0, 95.0);
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Output Range Safety Tests                                      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Output should always be within [5, 95] regardless of extreme settings.
    /// </summary>
    [Fact]
    public void MapToPosition_ExtremeSettings_AlwaysWithinBounds()
    {
        var beatMap = CreateBeatMap(bpm: 60.0, beatCount: 20, strength: 1.0);

        var extremeSettings = new[]
        {
            new PulseStrokeSettings { AmplitudeOffset = 1.0, Randomness = 1.0, EasingBlend = 1.0, Pattern = StrokePattern.DoubleTap },
            new PulseStrokeSettings { AmplitudeOffset = -1.0, Randomness = 1.0, EasingBlend = -1.0, Pattern = StrokePattern.TripleTap },
            new PulseStrokeSettings { AmplitudeOffset = 1.0, EasingBlend = 1.0, Pattern = StrokePattern.HoldTop },
            new PulseStrokeSettings { AmplitudeOffset = 1.0, EasingBlend = -1.0, Pattern = StrokePattern.HoldBottom },
        };

        foreach (var settings in extremeSettings)
        {
            var mapper = CreateMapper(settings);
            for (int i = 0; i <= 100; i++)
            {
                double timeMs = i * 10.0;
                double pos = mapper.MapToPosition(beatMap, timeMs, 1.0);
                Assert.InRange(pos, 5.0, 95.0);
            }
        }
    }

    /// <summary>
    /// Null or empty beat map should return rest position regardless of settings.
    /// </summary>
    [Fact]
    public void MapToPosition_NullBeatMap_ReturnsRestPosition()
    {
        var mapper = CreateMapper(new PulseStrokeSettings
        {
            AmplitudeOffset = 1.0,
            EasingBlend = 0.5,
            Pattern = StrokePattern.DoubleTap,
            Randomness = 0.8,
        });

        double pos = mapper.MapToPosition(null, 100.0, 0.5);

        Assert.Equal(50.0, pos);
    }
}
