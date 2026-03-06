using System.Text.Json;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Models.Pulse;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for VI-0020: <see cref="FunscriptWriter"/> — creates funscript actions
/// from beat data and serialises/writes them to disk.
/// </summary>
public class FunscriptWriterTests : IDisposable
{
    private readonly string _tempDir;

    public FunscriptWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido_fswriter_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ══════════════════════════════════════════════
    //  CreateActionsFromBeats
    // ══════════════════════════════════════════════

    [Fact]
    public void CreateActionsFromBeats_EmptyBeats_ReturnsEmptyList()
    {
        var result = FunscriptWriter.CreateActionsFromBeats([]);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateActionsFromBeats_SingleBeat_ReturnsHighPosition()
    {
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 500.0, Strength = 0.8 }
        };

        var result = FunscriptWriter.CreateActionsFromBeats(beats);

        Assert.Single(result);
        Assert.Equal(500L, result[0].AtMs);
        Assert.Equal(100, result[0].Pos);
    }

    [Fact]
    public void CreateActionsFromBeats_MultipleBeats_AlternatesHighLow()
    {
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 0.8 },
            new() { TimestampMs = 500, Strength = 0.7 },
            new() { TimestampMs = 1000, Strength = 0.9 },
            new() { TimestampMs = 1500, Strength = 0.6 },
        };

        var result = FunscriptWriter.CreateActionsFromBeats(beats);

        Assert.Equal(4, result.Count);
        Assert.Equal(100, result[0].Pos); // even → high
        Assert.Equal(0, result[1].Pos);   // odd  → low
        Assert.Equal(100, result[2].Pos); // even → high
        Assert.Equal(0, result[3].Pos);   // odd  → low
    }

    [Fact]
    public void CreateActionsFromBeats_CustomPositions_UsesProvidedValues()
    {
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 100, Strength = 1.0 },
            new() { TimestampMs = 200, Strength = 1.0 },
        };

        var result = FunscriptWriter.CreateActionsFromBeats(beats, highPos: 80, lowPos: 20);

        Assert.Equal(80, result[0].Pos);
        Assert.Equal(20, result[1].Pos);
    }

    [Fact]
    public void CreateActionsFromBeats_PreservesTimestamps()
    {
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 1234.56, Strength = 0.5 },
            new() { TimestampMs = 5678.99, Strength = 0.5 },
        };

        var result = FunscriptWriter.CreateActionsFromBeats(beats);

        Assert.Equal(1234L, result[0].AtMs);
        Assert.Equal(5678L, result[1].AtMs);
    }

    // ══════════════════════════════════════════════
    //  Serialize
    // ══════════════════════════════════════════════

    [Fact]
    public void Serialize_EmptyActions_ProducesValidJson()
    {
        var json = FunscriptWriter.Serialize([]);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1.0", doc.RootElement.GetProperty("version").GetString());
        Assert.Empty(doc.RootElement.GetProperty("actions").EnumerateArray().ToList());
    }

    [Fact]
    public void Serialize_MultipleActions_ProducesCorrectJson()
    {
        var actions = new List<FunscriptAction>
        {
            new(1000, 100),
            new(2000, 0),
            new(3000, 50),
        };

        var json = FunscriptWriter.Serialize(actions);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("actions").EnumerateArray().ToList();
        Assert.Equal(3, arr.Count);
        Assert.Equal(1000, arr[0].GetProperty("at").GetInt64());
        Assert.Equal(100, arr[0].GetProperty("pos").GetInt32());
        Assert.Equal(2000, arr[1].GetProperty("at").GetInt64());
        Assert.Equal(0, arr[1].GetProperty("pos").GetInt32());
        Assert.Equal(3000, arr[2].GetProperty("at").GetInt64());
        Assert.Equal(50, arr[2].GetProperty("pos").GetInt32());
    }

    [Fact]
    public void Serialize_RoundTrip_ParsedByFunscriptParser()
    {
        var original = new List<FunscriptAction>
        {
            new(500, 100),
            new(1000, 0),
            new(1500, 75),
        };

        var json = FunscriptWriter.Serialize(original);
        var parser = new FunscriptParser();
        var parsed = parser.Parse(json);

        Assert.Equal(original.Count, parsed.Actions.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].AtMs, parsed.Actions[i].AtMs);
            Assert.Equal(original[i].Pos, parsed.Actions[i].Pos);
        }
    }

    // ══════════════════════════════════════════════
    //  WriteAsync
    // ══════════════════════════════════════════════

    [Fact]
    public async Task WriteAsync_CreatesFileWithValidJson()
    {
        var actions = new List<FunscriptAction> { new(100, 50), new(200, 75) };
        var filePath = Path.Combine(_tempDir, "test.funscript");

        await FunscriptWriter.WriteAsync(actions, filePath);

        Assert.True(File.Exists(filePath));
        var json = await File.ReadAllTextAsync(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("actions").EnumerateArray().Count());
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfNotExists()
    {
        var actions = new List<FunscriptAction> { new(100, 50) };
        var subDir = Path.Combine(_tempDir, "sub", "dir");
        var filePath = Path.Combine(subDir, "test.funscript");

        await FunscriptWriter.WriteAsync(actions, filePath);

        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(filePath));
    }

    // ══════════════════════════════════════════════
    //  CreateActionsFromBeatMap (VI-0023)
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
            DurationMs = durationMs
        };
    }

    [Fact]
    public void CreateActionsFromBeatMap_EmptyBeats_ReturnsEmptyList()
    {
        var beatMap = MakeBeatMap([]);
        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateActionsFromBeatMap_AlternatesTopBottom()
    {
        // Full amplitude (1.0) and full strength (1.0) → positions near 5 and 95
        var waveform = new float[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 1.0 },
            new() { TimestampMs = 10, Strength = 1.0 },
            new() { TimestampMs = 20, Strength = 1.0 },
            new() { TimestampMs = 30, Strength = 1.0 },
        };
        var beatMap = MakeBeatMap(beats, waveform);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap);

        Assert.Equal(4, result.Count);
        // Even beats → top (> 50), odd beats → bottom (< 50)
        Assert.True(result[0].Pos > 50, $"Even beat should be top, was {result[0].Pos}");
        Assert.True(result[1].Pos < 50, $"Odd beat should be bottom, was {result[1].Pos}");
        Assert.True(result[2].Pos > 50, $"Even beat should be top, was {result[2].Pos}");
        Assert.True(result[3].Pos < 50, $"Odd beat should be bottom, was {result[3].Pos}");
    }

    [Fact]
    public void CreateActionsFromBeatMap_ScalesWithAmplitude()
    {
        // High amplitude beat vs low amplitude beat — both with same strength
        // Waveform: index 0 = 1.0 (loud), index 1 = 0.1 (quiet)
        // SampleRate=100 → index = timestampMs / 1000 * 100 = timestampMs / 10
        // Beat at 0ms → index 0 (1.0), beat at 10ms → index 1 (0.1)
        var waveform = new float[] { 1.0f, 0.1f };
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 1.0 },   // high amplitude → wide range
            new() { TimestampMs = 10, Strength = 1.0 },   // low amplitude → narrow range
        };
        var beatMap = MakeBeatMap(beats, waveform);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap);

        // High amplitude beat (even) should be further from 50 than low amplitude beat (odd from center)
        // With amplitude=1.0: amplitudeScale = 0.15 + 0.85*1.0 = 1.0, intensityScale = 1.0 * 1.0 = 1.0
        //   halfRange = 45*1.0 = 45, top = 95
        // With amplitude=0.1: amplitudeScale = 0.15 + 0.85*0.1 = 0.235, intensityScale = 0.235 * 1.0 = 0.235
        //   halfRange = 45*0.235 = 10.575, bottom = 50 - 10.575 = 39.4 → 39
        int highAmpTop = result[0].Pos;  // even, high amplitude
        int lowAmpBottom = result[1].Pos;  // odd, low amplitude

        Assert.Equal(95, highAmpTop);  // Full amplitude → max position
        Assert.True(lowAmpBottom > 35 && lowAmpBottom < 50,
            $"Low amplitude bottom should be closer to 50, was {lowAmpBottom}");
    }

    [Fact]
    public void CreateActionsFromBeatMap_ScalesWithBeatStrength()
    {
        // Same amplitude (1.0), different beat strength
        var waveform = new float[] { 1.0f, 1.0f, 1.0f };
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 1.0 },   // strong beat → wide range
            new() { TimestampMs = 10, Strength = 0.0 },   // weak beat → narrower range
        };
        var beatMap = MakeBeatMap(beats, waveform);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap);

        // Strength=1.0: intensityScale = 1.0 * (0.5 + 0.5) = 1.0, halfRange=45, top=95
        // Strength=0.0: intensityScale = 1.0 * (0.5 + 0.0) = 0.5, halfRange=22.5, bottom=50-22.5=27.5→28
        int strongTop = result[0].Pos;
        int weakBottom = result[1].Pos;

        Assert.Equal(95, strongTop);
        // Weak beat (odd) should be closer to 50 than a strong beat would be
        Assert.True(weakBottom > 20 && weakBottom < 40,
            $"Weak beat bottom should be closer to center, was {weakBottom}");
    }

    [Fact]
    public void CreateActionsFromBeatMap_EmptyWaveform_UsesMinAmplitude()
    {
        // No waveform data → amplitude = 0.0 → MinAmplitudeScale (0.15)
        var beats = new List<BeatEvent>
        {
            new() { TimestampMs = 0, Strength = 1.0 },
            new() { TimestampMs = 500, Strength = 1.0 },
        };
        var beatMap = MakeBeatMap(beats);  // empty waveform

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap);

        Assert.Equal(2, result.Count);
        // amplitude=0.0: amplitudeScale=0.15, intensityScale=0.15*1.0=0.15
        // halfRange=45*0.15=6.75, top=56.75→57, bottom=43.25→43
        Assert.True(result[0].Pos > 50 && result[0].Pos < 65,
            $"Min amplitude top should be just above center, was {result[0].Pos}");
        Assert.True(result[1].Pos > 35 && result[1].Pos < 50,
            $"Min amplitude bottom should be just below center, was {result[1].Pos}");
    }

    [Fact]
    public void CreateActionsFromBeatMap_PositionsWithinBounds()
    {
        // Various amplitudes and strengths — all positions must be within 5–95
        var waveform = new float[200];
        for (int i = 0; i < waveform.Length; i++)
            waveform[i] = (float)(i % 10) / 10f;

        var beats = new List<BeatEvent>();
        for (int i = 0; i < 20; i++)
            beats.Add(new BeatEvent { TimestampMs = i * 100, Strength = i / 20.0 });

        var beatMap = MakeBeatMap(beats, waveform);

        var result = FunscriptWriter.CreateActionsFromBeatMap(beatMap);

        Assert.Equal(20, result.Count);
        foreach (var action in result)
        {
            Assert.InRange(action.Pos, 5, 95);
        }
    }

    // ══════════════════════════════════════════════
    //  SampleWaveformAmplitude (VI-0023)
    // ══════════════════════════════════════════════

    [Fact]
    public void SampleWaveformAmplitude_ValidIndex_ReturnsCorrectValue()
    {
        // SampleRate=100 → 100 samples/sec → index = timestampMs / 1000 * 100 = timestampMs / 10
        var waveform = new float[] { 0.1f, 0.5f, 0.9f };
        var result = FunscriptWriter.SampleWaveformAmplitude(waveform, 100, 10.0);
        Assert.Equal(0.5, result, 3);
    }

    [Fact]
    public void SampleWaveformAmplitude_OutOfRange_ReturnsZero()
    {
        var waveform = new float[] { 0.5f, 0.8f };
        var result = FunscriptWriter.SampleWaveformAmplitude(waveform, 100, 5000.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void SampleWaveformAmplitude_EmptyWaveform_ReturnsZero()
    {
        var result = FunscriptWriter.SampleWaveformAmplitude(Array.Empty<float>(), 100, 0.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void SampleWaveformAmplitude_NullWaveform_ReturnsZero()
    {
        var result = FunscriptWriter.SampleWaveformAmplitude(null!, 100, 0.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void SampleWaveformAmplitude_ZeroSampleRate_ReturnsZero()
    {
        var waveform = new float[] { 0.5f };
        var result = FunscriptWriter.SampleWaveformAmplitude(waveform, 0, 0.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void SampleWaveformAmplitude_ClampsAboveOne()
    {
        var waveform = new float[] { 1.5f };
        var result = FunscriptWriter.SampleWaveformAmplitude(waveform, 100, 0.0);
        Assert.Equal(1.0, result);
    }
}
