using Vido.Core.Models.Osr2Plus;
using Vido.Core.Models.Pulse;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for vido-196: <see cref="FunscriptWriter.CreateActionsFromBeatMap(BeatMap, PulseStrokeSettings)"/>
/// — stroke control adjustments baked into generated funscript actions.
/// </summary>
public class FunscriptWriterStrokeControlTests
{
    // ══════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════

    private static BeatMap MakeBeatMap(
        IReadOnlyList<BeatEvent> beats,
        float[]? waveformSamples = null,
        int waveformSampleRate = 100,
        double bpm = 120,
        double durationMs = 10000)
    {
        return new BeatMap
        {
            Beats = beats,
            WaveformSamples = waveformSamples ?? Array.Empty<float>(),
            WaveformSampleRate = waveformSampleRate,
            Bpm = bpm,
            DurationMs = durationMs,
        };
    }

    /// <summary>Creates a beat map with evenly spaced beats at full amplitude and strength.</summary>
    private static BeatMap MakeFullAmplitudeBeatMap(int beatCount, double intervalMs = 500)
    {
        var beats = new List<BeatEvent>();
        // Waveform at 100 Hz sample rate — need enough samples to cover all beats
        int totalSamples = (int)(beatCount * intervalMs / 10.0) + 10;
        var waveform = new float[totalSamples];
        Array.Fill(waveform, 1.0f);

        for (int i = 0; i < beatCount; i++)
            beats.Add(new BeatEvent { TimestampMs = i * intervalMs, Strength = 1.0 });

        return MakeBeatMap(beats, waveform, waveformSampleRate: 100, bpm: 60000.0 / intervalMs);
    }

    // ══════════════════════════════════════════════
    //  Backward Compatibility (Default settings)
    // ══════════════════════════════════════════════

    [Fact]
    public void DefaultSettings_ProducesIdenticalOutputToParameterlessOverload()
    {
        var waveform = new float[] { 0.8f, 0.3f, 0.9f, 0.5f, 0.7f, 0.2f };
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 0.9 },
            new() { TimestampMs = 10, Strength = 0.4 },
            new() { TimestampMs = 20, Strength = 1.0 },
            new() { TimestampMs = 30, Strength = 0.6 },
            new() { TimestampMs = 40, Strength = 0.2 },
            new() { TimestampMs = 50, Strength = 0.8 },
        };
        var beatMap = MakeBeatMap(beats, waveform);

        var original = FunscriptWriter.CreateActionsFromBeatMap(beatMap);
        var withDefaults = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);

        Assert.Equal(original.Count, withDefaults.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].AtMs, withDefaults[i].AtMs);
            Assert.Equal(original[i].Pos, withDefaults[i].Pos);
        }
    }

    [Fact]
    public void DefaultSettings_EmptyBeats_ReturnsEmpty()
    {
        var beatMap = MakeBeatMap([]);
        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);
        Assert.Empty(result);
    }

    // ══════════════════════════════════════════════
    //  Classic Pattern
    // ══════════════════════════════════════════════

    [Fact]
    public void Classic_ProducesOneActionPerBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.Classic };
        var beatMap = MakeFullAmplitudeBeatMap(6);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void Classic_AlternatesTopBottom()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.Classic };
        var beatMap = MakeFullAmplitudeBeatMap(4);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.True(result[0].Pos > 50); // even → top
        Assert.True(result[1].Pos < 50); // odd → bottom
        Assert.True(result[2].Pos > 50); // even → top
        Assert.True(result[3].Pos < 50); // odd → bottom
    }

    // ══════════════════════════════════════════════
    //  Amplitude Offset
    // ══════════════════════════════════════════════

    [Fact]
    public void PositiveAmplitudeOffset_IncreasesRange()
    {
        var beatMap = MakeFullAmplitudeBeatMap(2);

        var defaultResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);
        var boosted = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { AmplitudeOffset = 0.5 });

        int defaultTop = defaultResult[0].Pos;
        int boostedTop = boosted[0].Pos;

        // With positive offset, range expands toward max — top should be >= default top
        Assert.True(boostedTop >= defaultTop,
            $"Positive offset top ({boostedTop}) should be >= default top ({defaultTop})");
    }

    [Fact]
    public void NegativeAmplitudeOffset_DecreasesRange()
    {
        var beatMap = MakeFullAmplitudeBeatMap(2);

        var defaultResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);
        var reduced = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { AmplitudeOffset = -0.5 });

        int defaultTop = defaultResult[0].Pos;
        int reducedTop = reduced[0].Pos;

        // With negative offset, range shrinks toward center
        Assert.True(reducedTop <= defaultTop,
            $"Negative offset top ({reducedTop}) should be <= default top ({defaultTop})");
        Assert.True(reducedTop > 50,
            $"Negative offset top ({reducedTop}) should still be above center");
    }

    [Fact]
    public void MaxNegativeAmplitudeOffset_CollapsesToCenter()
    {
        var beatMap = MakeFullAmplitudeBeatMap(2);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { AmplitudeOffset = -1.0 });

        // halfRange * (1 + (-1)) = 0, so top = bottom = 50
        Assert.Equal(50, result[0].Pos);
        Assert.Equal(50, result[1].Pos);
    }

    [Fact]
    public void MaxPositiveAmplitudeOffset_ExpandsToMax()
    {
        // Use low amplitude so there's room to expand
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 0.5 },
            new() { TimestampMs = 500, Strength = 0.5 },
        };
        var waveform = new float[] { 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f };
        var beatMap = MakeBeatMap(beats, waveform);

        var defaultResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);
        var maxBoost = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { AmplitudeOffset = 1.0 });

        // With AmplitudeOffset=1.0, halfRange = halfRange + (maxHalfRange - halfRange) * 1.0 = maxHalfRange
        Assert.Equal(95, maxBoost[0].Pos); // top = 50 + 45 = 95
        Assert.Equal(5, maxBoost[1].Pos);  // bottom = 50 - 45 = 5

        // Default should have narrower range
        Assert.True(defaultResult[0].Pos < 95);
        Assert.True(defaultResult[1].Pos > 5);
    }

    // ══════════════════════════════════════════════
    //  Randomness
    // ══════════════════════════════════════════════

    [Fact]
    public void Randomness_ProducesDeterministicVariation()
    {
        var beatMap = MakeFullAmplitudeBeatMap(6);
        var settings = PulseStrokeSettings.Default with { Randomness = 0.5 };

        var result1 = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);
        var result2 = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Deterministic: same input → same output
        Assert.Equal(result1.Count, result2.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1[i].AtMs, result2[i].AtMs);
            Assert.Equal(result1[i].Pos, result2[i].Pos);
        }
    }

    [Fact]
    public void Randomness_IntroducesVariationAcrossBeats()
    {
        var beatMap = MakeFullAmplitudeBeatMap(10);
        var settings = PulseStrokeSettings.Default with { Randomness = 1.0 };

        var defaultResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);
        var randomResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // With randomness, at least some beats should differ from default
        bool anyDifferent = false;
        for (int i = 0; i < defaultResult.Count; i++)
        {
            if (defaultResult[i].Pos != randomResult[i].Pos)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Randomness should cause at least some beats to differ from default");
    }

    [Fact]
    public void ZeroRandomness_ProducesNoVariation()
    {
        var beatMap = MakeFullAmplitudeBeatMap(4);
        var settings = PulseStrokeSettings.Default with { Randomness = 0.0 };

        var defaultResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);
        var zeroRandomResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        for (int i = 0; i < defaultResult.Count; i++)
        {
            Assert.Equal(defaultResult[i].Pos, zeroRandomResult[i].Pos);
        }
    }

    // ══════════════════════════════════════════════
    //  DoubleTap Pattern
    // ══════════════════════════════════════════════

    [Fact]
    public void DoubleTap_ProducesFourActionsPerBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap };
        var beatMap = MakeFullAmplitudeBeatMap(3);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(12, result.Count); // 3 beats × 4 actions
    }

    [Fact]
    public void DoubleTap_AlternatesWithinBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(4, result.Count);
        // Even beat (index 0): starts high → high, low, high, low
        Assert.True(result[0].Pos > 50, $"First sub-action should be top, was {result[0].Pos}");
        Assert.True(result[1].Pos < 50, $"Second sub-action should be bottom, was {result[1].Pos}");
        Assert.True(result[2].Pos > 50, $"Third sub-action should be top, was {result[2].Pos}");
        Assert.True(result[3].Pos < 50, $"Fourth sub-action should be bottom, was {result[3].Pos}");
    }

    [Fact]
    public void DoubleTap_OddBeatStartsLow()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap };
        var beatMap = MakeFullAmplitudeBeatMap(2, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Beat 1 (odd) actions are at indices 4, 5, 6, 7
        Assert.True(result[4].Pos < 50, $"Odd beat first sub-action should be bottom, was {result[4].Pos}");
        Assert.True(result[5].Pos > 50, $"Odd beat second sub-action should be top, was {result[5].Pos}");
        Assert.True(result[6].Pos < 50, $"Odd beat third sub-action should be bottom, was {result[6].Pos}");
        Assert.True(result[7].Pos > 50, $"Odd beat fourth sub-action should be top, was {result[7].Pos}");
    }

    [Fact]
    public void DoubleTap_EvenlySpacedTimestamps()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap };
        var beatMap = MakeFullAmplitudeBeatMap(2, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Beat 0 at 0ms, interval 1000ms: 0, 250, 500, 750
        Assert.Equal(0L, result[0].AtMs);
        Assert.Equal(250L, result[1].AtMs);
        Assert.Equal(500L, result[2].AtMs);
        Assert.Equal(750L, result[3].AtMs);

        // Beat 1 at 1000ms: 1000, 1250, 1500, 1750
        Assert.Equal(1000L, result[4].AtMs);
        Assert.Equal(1250L, result[5].AtMs);
        Assert.Equal(1500L, result[6].AtMs);
        Assert.Equal(1750L, result[7].AtMs);
    }

    // ══════════════════════════════════════════════
    //  TripleTap Pattern
    // ══════════════════════════════════════════════

    [Fact]
    public void TripleTap_ProducesSixActionsPerBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.TripleTap };
        var beatMap = MakeFullAmplitudeBeatMap(3);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(18, result.Count); // 3 beats × 6 actions
    }

    [Fact]
    public void TripleTap_AlternatesWithinBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.TripleTap };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1200);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(6, result.Count);
        // Even beat: starts high → high, low, high, low, high, low
        Assert.True(result[0].Pos > 50);
        Assert.True(result[1].Pos < 50);
        Assert.True(result[2].Pos > 50);
        Assert.True(result[3].Pos < 50);
        Assert.True(result[4].Pos > 50);
        Assert.True(result[5].Pos < 50);
    }

    [Fact]
    public void TripleTap_EvenlySpacedTimestamps()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.TripleTap };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 600);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Beat 0 at 0ms, interval 600ms, 6 sub-actions: 0, 100, 200, 300, 400, 500
        Assert.Equal(0L, result[0].AtMs);
        Assert.Equal(100L, result[1].AtMs);
        Assert.Equal(200L, result[2].AtMs);
        Assert.Equal(300L, result[3].AtMs);
        Assert.Equal(400L, result[4].AtMs);
        Assert.Equal(500L, result[5].AtMs);
    }

    // ══════════════════════════════════════════════
    //  HoldTop Pattern
    // ══════════════════════════════════════════════

    [Fact]
    public void HoldTop_ProducesThreeActionsPerBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldTop };
        var beatMap = MakeFullAmplitudeBeatMap(4);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(12, result.Count); // 4 beats × 3 actions
    }

    [Fact]
    public void HoldTop_HoldsAtTopPosition()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldTop };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(3, result.Count);

        // First two keyframes: hold at top position
        Assert.True(result[0].Pos > 50, $"Hold arrival should be top, was {result[0].Pos}");
        Assert.Equal(result[0].Pos, result[1].Pos); // Hold: same position

        // Third keyframe: return to bottom
        Assert.True(result[2].Pos < 50, $"Return should be bottom, was {result[2].Pos}");
    }

    [Fact]
    public void HoldTop_CorrectTimingPercentages()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldTop };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Beat at 0ms, interval 1000ms:
        // Arrival at 30% → 300ms, Hold-end at 70% → 700ms, Return at 100% → 1000ms
        Assert.Equal(300L, result[0].AtMs);
        Assert.Equal(700L, result[1].AtMs);
        Assert.Equal(1000L, result[2].AtMs);
    }

    [Fact]
    public void HoldTop_ConsecutiveBeats_ProduceConsistentHold()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldTop };
        var beatMap = MakeFullAmplitudeBeatMap(3, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(9, result.Count);

        // Every beat holds at top — arrival and hold-end should have same position
        for (int beat = 0; beat < 3; beat++)
        {
            int arrivalIdx = beat * 3;
            int holdEndIdx = beat * 3 + 1;
            Assert.Equal(result[arrivalIdx].Pos, result[holdEndIdx].Pos);
            Assert.True(result[arrivalIdx].Pos > 50);
        }

        // Every return action should be at bottom
        for (int beat = 0; beat < 3; beat++)
        {
            int returnIdx = beat * 3 + 2;
            Assert.True(result[returnIdx].Pos < 50);
        }
    }

    // ══════════════════════════════════════════════
    //  HoldBottom Pattern
    // ══════════════════════════════════════════════

    [Fact]
    public void HoldBottom_ProducesThreeActionsPerBeat()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldBottom };
        var beatMap = MakeFullAmplitudeBeatMap(4);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(12, result.Count); // 4 beats × 3 actions
    }

    [Fact]
    public void HoldBottom_HoldsAtBottomPosition()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldBottom };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(3, result.Count);

        // First two keyframes: hold at bottom position
        Assert.True(result[0].Pos < 50, $"Hold arrival should be bottom, was {result[0].Pos}");
        Assert.Equal(result[0].Pos, result[1].Pos); // Hold: same position

        // Third keyframe: return to top
        Assert.True(result[2].Pos > 50, $"Return should be top, was {result[2].Pos}");
    }

    [Fact]
    public void HoldBottom_InvertedPositionsFromHoldTop()
    {
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1000);

        var holdTopResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldTop });
        var holdBottomResult = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldBottom });

        // HoldTop arrival = top, HoldBottom arrival = bottom
        // Same timing
        Assert.Equal(holdTopResult[0].AtMs, holdBottomResult[0].AtMs);
        Assert.Equal(holdTopResult[1].AtMs, holdBottomResult[1].AtMs);
        Assert.Equal(holdTopResult[2].AtMs, holdBottomResult[2].AtMs);

        // Inverted positions: holdTop arrival pos = holdBottom return pos
        Assert.Equal(holdTopResult[0].Pos, holdBottomResult[2].Pos);
        Assert.Equal(holdTopResult[2].Pos, holdBottomResult[0].Pos);
    }

    [Fact]
    public void HoldBottom_CorrectTimingPercentages()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldBottom };
        var beatMap = MakeFullAmplitudeBeatMap(1, intervalMs: 1000);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(300L, result[0].AtMs);
        Assert.Equal(700L, result[1].AtMs);
        Assert.Equal(1000L, result[2].AtMs);
    }

    // ══════════════════════════════════════════════
    //  Position Bounds
    // ══════════════════════════════════════════════

    [Fact]
    public void AllPatterns_PositionsWithinBounds()
    {
        var beatMap = MakeFullAmplitudeBeatMap(8);
        var patterns = new[]
        {
            StrokePattern.Classic,
            StrokePattern.DoubleTap,
            StrokePattern.TripleTap,
            StrokePattern.HoldTop,
            StrokePattern.HoldBottom,
        };

        foreach (var pattern in patterns)
        {
            var settings = new PulseStrokeSettings
            {
                AmplitudeOffset = 0.8,
                Randomness = 1.0,
                Pattern = pattern,
            };
            var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

            foreach (var action in result)
            {
                Assert.InRange(action.Pos, 0, 100);
            }
        }
    }

    [Fact]
    public void ExtremeSettings_PositionsStayValid()
    {
        var beatMap = MakeFullAmplitudeBeatMap(4);

        // Max positive offset + max randomness
        var result1 = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            new PulseStrokeSettings { AmplitudeOffset = 1.0, Randomness = 1.0 });

        // Max negative offset
        var result2 = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            new PulseStrokeSettings { AmplitudeOffset = -1.0 });

        foreach (var action in result1.Concat(result2))
        {
            Assert.InRange(action.Pos, 0, 100);
        }
    }

    // ══════════════════════════════════════════════
    //  GetBeatInterval (via pattern timing)
    // ══════════════════════════════════════════════

    [Fact]
    public void LastBeat_UsesBpmForInterval()
    {
        // BPM=120 → interval = 500ms
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.HoldTop };
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 1.0 },
        };
        var waveform = new float[] { 1.0f };
        var beatMap = MakeBeatMap(beats, waveform, bpm: 120);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Single beat at 0ms, BPM=120 → interval=500ms
        // HoldTop: arrival at 150ms, hold-end at 350ms, return at 500ms
        Assert.Equal(150L, result[0].AtMs);
        Assert.Equal(350L, result[1].AtMs);
        Assert.Equal(500L, result[2].AtMs);
    }

    [Fact]
    public void UnevenBeatSpacing_UsesActualIntervals()
    {
        var settings = PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap };
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 1.0 },
            new() { TimestampMs = 800, Strength = 1.0 },
        };
        var waveform = new float[100];
        Array.Fill(waveform, 1.0f);
        var beatMap = MakeBeatMap(beats, waveform, bpm: 120);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // Beat 0: interval = 800ms. DoubleTap at 0, 200, 400, 600
        Assert.Equal(0L, result[0].AtMs);
        Assert.Equal(200L, result[1].AtMs);
        Assert.Equal(400L, result[2].AtMs);
        Assert.Equal(600L, result[3].AtMs);

        // Beat 1: last beat, falls back to BPM → 500ms. DoubleTap at 800, 925, 1050, 1175
        Assert.Equal(800L, result[4].AtMs);
        Assert.Equal(925L, result[5].AtMs);
        Assert.Equal(1050L, result[6].AtMs);
        Assert.Equal(1175L, result[7].AtMs);
    }

    // ══════════════════════════════════════════════
    //  Combined Settings
    // ══════════════════════════════════════════════

    [Fact]
    public void DoubleTap_WithAmplitudeOffset_BothApplied()
    {
        var beatMap = MakeFullAmplitudeBeatMap(2, intervalMs: 1000);

        var defaultDouble = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap });
        var boostedDouble = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            new PulseStrokeSettings { Pattern = StrokePattern.DoubleTap, AmplitudeOffset = -0.5 });

        // Both should have 8 actions (2 beats × 4)
        Assert.Equal(8, defaultDouble.Count);
        Assert.Equal(8, boostedDouble.Count);

        // Reduced offset should have narrower range (top positions closer to 50)
        int defaultTopPos = defaultDouble[0].Pos;
        int reducedTopPos = boostedDouble[0].Pos;
        Assert.True(reducedTopPos < defaultTopPos,
            $"Reduced offset top ({reducedTopPos}) should be < default top ({defaultTopPos})");
    }

    [Fact]
    public void HoldTop_WithRandomness_VariesAmplitude()
    {
        var beatMap = MakeFullAmplitudeBeatMap(6, intervalMs: 1000);
        var settings = new PulseStrokeSettings { Pattern = StrokePattern.HoldTop, Randomness = 1.0 };

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        // 6 beats × 3 = 18 actions. Arrival positions should vary across beats.
        var arrivalPositions = new HashSet<int>();
        for (int beat = 0; beat < 6; beat++)
            arrivalPositions.Add(result[beat * 3].Pos);

        // With full randomness, at least 2 distinct positions (likely more)
        Assert.True(arrivalPositions.Count >= 2,
            $"Expected varied positions with full randomness, got {arrivalPositions.Count} distinct values");
    }

    // ══════════════════════════════════════════════
    //  Action Count Invariants
    // ══════════════════════════════════════════════

    [Theory]
    [InlineData(StrokePattern.Classic, 1)]
    [InlineData(StrokePattern.DoubleTap, 4)]
    [InlineData(StrokePattern.TripleTap, 6)]
    [InlineData(StrokePattern.HoldTop, 3)]
    [InlineData(StrokePattern.HoldBottom, 3)]
    public void Pattern_ProducesExpectedActionsPerBeat(StrokePattern pattern, int actionsPerBeat)
    {
        var settings = PulseStrokeSettings.Default with { Pattern = pattern };
        int beatCount = 5;
        var beatMap = MakeFullAmplitudeBeatMap(beatCount);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        Assert.Equal(beatCount * actionsPerBeat, result.Count);
    }

    [Fact]
    public void DoubleTap_ProducesMoreActionsThanClassic()
    {
        var beatMap = MakeFullAmplitudeBeatMap(4);

        var classic = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { Pattern = StrokePattern.Classic });
        var doubleTap = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { Pattern = StrokePattern.DoubleTap });
        var tripleTap = FunscriptWriter.CreateActionsFromBeatMap(beatMap,
            PulseStrokeSettings.Default with { Pattern = StrokePattern.TripleTap });

        Assert.True(doubleTap.Count > classic.Count);
        Assert.True(tripleTap.Count > doubleTap.Count);
    }

    // ══════════════════════════════════════════════
    //  Timestamp Ordering
    // ══════════════════════════════════════════════

    [Theory]
    [InlineData(StrokePattern.Classic)]
    [InlineData(StrokePattern.DoubleTap)]
    [InlineData(StrokePattern.TripleTap)]
    [InlineData(StrokePattern.HoldTop)]
    [InlineData(StrokePattern.HoldBottom)]
    public void AllPatterns_TimestampsAreNonDecreasing(StrokePattern pattern)
    {
        var settings = PulseStrokeSettings.Default with { Pattern = pattern };
        var beatMap = MakeFullAmplitudeBeatMap(8);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap, settings);

        for (int i = 1; i < result.Count; i++)
        {
            Assert.True(result[i].AtMs >= result[i - 1].AtMs,
                $"Timestamp at index {i} ({result[i].AtMs}) should be >= previous ({result[i - 1].AtMs})");
        }
    }
}
