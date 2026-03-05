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
}
