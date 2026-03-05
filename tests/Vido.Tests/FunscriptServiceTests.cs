using System.IO;
using System.Text;
using Vido.Core.Models.Osr2Plus;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for PI-006: OSR2+ Funscript services integrated into Vido.Services.
/// Covers <see cref="FunscriptParser"/>, <see cref="FunscriptMatcher"/>,
/// <see cref="FunscriptLoadingService"/>, <see cref="InterpolationService"/>,
/// and <see cref="BeatDetectionService"/>.
/// </summary>
public class FunscriptServiceTests : IDisposable
{
    private readonly string _tempDir;

    public FunscriptServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido_funscript_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ──────────────────────────────────────────────
    //  Helper methods
    // ──────────────────────────────────────────────

    private static string MakeJson(params (long at, int pos)[] actions)
    {
        var sb = new StringBuilder();
        sb.Append("{\"actions\":[");
        for (int i = 0; i < actions.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"at\":{actions[i].at},\"pos\":{actions[i].pos}}}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteFileBytes(string name, byte[] bytes)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ──────────────────────────────────────────────
    //  FunscriptParser — Parse(string)
    // ──────────────────────────────────────────────

    [Fact]
    public void Parser_Parse_ValidJson_ReturnsActions()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((0, 0), (1000, 100), (2000, 50));

        var result = parser.Parse(json);

        Assert.Equal("L0", result.AxisId);
        Assert.Equal(3, result.Actions.Count);
        Assert.Equal(new FunscriptAction(0, 0), result.Actions[0]);
        Assert.Equal(new FunscriptAction(1000, 100), result.Actions[1]);
        Assert.Equal(new FunscriptAction(2000, 50), result.Actions[2]);
    }

    [Fact]
    public void Parser_Parse_CustomAxisId_SetsAxisId()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((0, 50));

        var result = parser.Parse(json, "R0");

        Assert.Equal("R0", result.AxisId);
    }

    [Fact]
    public void Parser_Parse_EmptyString_ReturnsEmptyData()
    {
        var parser = new FunscriptParser();

        var result = parser.Parse("");

        Assert.Equal("L0", result.AxisId);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Parser_Parse_NullString_ReturnsEmptyData()
    {
        var parser = new FunscriptParser();

        var result = parser.Parse(null!);

        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Parser_Parse_MalformedJson_ReturnsEmptyData()
    {
        var parser = new FunscriptParser();

        var result = parser.Parse("{not valid json!}}}");

        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Parser_Parse_PosClamped_ClampedTo0And100()
    {
        var parser = new FunscriptParser();
        var json = "{\"actions\":[{\"at\":0,\"pos\":-50},{\"at\":1000,\"pos\":200}]}";

        var result = parser.Parse(json);

        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(0, result.Actions[0].Pos);
        Assert.Equal(100, result.Actions[1].Pos);
    }

    [Fact]
    public void Parser_Parse_UnsortedActions_SortedByAtMs()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((3000, 75), (1000, 25), (2000, 50));

        var result = parser.Parse(json);

        Assert.Equal(1000, result.Actions[0].AtMs);
        Assert.Equal(2000, result.Actions[1].AtMs);
        Assert.Equal(3000, result.Actions[2].AtMs);
    }

    [Fact]
    public void Parser_Parse_EmptyActionsArray_ReturnsEmptyList()
    {
        var parser = new FunscriptParser();

        var result = parser.Parse("{\"actions\":[]}");

        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Parser_Parse_MissingFields_SkipsIncompleteActions()
    {
        var parser = new FunscriptParser();
        // Only "at", no "pos"
        var json = "{\"actions\":[{\"at\":100},{\"at\":200,\"pos\":50}]}";

        var result = parser.Parse(json);

        Assert.Single(result.Actions);
        Assert.Equal(200, result.Actions[0].AtMs);
    }

    [Fact]
    public void Parser_Parse_TrailingCommas_Allowed()
    {
        var parser = new FunscriptParser();
        var json = "{\"actions\":[{\"at\":0,\"pos\":50},],}";

        var result = parser.Parse(json);

        Assert.Single(result.Actions);
    }

    [Fact]
    public void Parser_Parse_ExtraProperties_Ignored()
    {
        var parser = new FunscriptParser();
        var json = "{\"version\":\"1.0\",\"actions\":[{\"at\":0,\"pos\":50,\"extra\":true}],\"metadata\":{}}";

        var result = parser.Parse(json);

        Assert.Single(result.Actions);
        Assert.Equal(50, result.Actions[0].Pos);
    }

    // ──────────────────────────────────────────────
    //  FunscriptParser — ParseFile
    // ──────────────────────────────────────────────

    [Fact]
    public void Parser_ParseFile_ValidFile_ReturnsDataWithPath()
    {
        var parser = new FunscriptParser();
        var path = WriteFile("test.funscript", MakeJson((0, 0), (1000, 100)));

        var result = parser.ParseFile(path);

        Assert.Equal(path, result.FilePath);
        Assert.Equal(2, result.Actions.Count);
    }

    [Fact]
    public void Parser_ParseFile_Utf8Bom_HandledCorrectly()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((0, 25), (500, 75));
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var withBom = new byte[bom.Length + jsonBytes.Length];
        bom.CopyTo(withBom, 0);
        jsonBytes.CopyTo(withBom, bom.Length);
        var path = WriteFileBytes("bom_test.funscript", withBom);

        var result = parser.ParseFile(path);

        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(25, result.Actions[0].Pos);
    }

    [Fact]
    public void Parser_ParseFile_Utf16LeBom_Transcoded()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((0, 10), (100, 90));
        var bom = new byte[] { 0xFF, 0xFE };
        var jsonBytes = Encoding.Unicode.GetBytes(json);
        var withBom = new byte[bom.Length + jsonBytes.Length];
        bom.CopyTo(withBom, 0);
        jsonBytes.CopyTo(withBom, bom.Length);
        var path = WriteFileBytes("utf16le.funscript", withBom);

        var result = parser.ParseFile(path);

        Assert.Equal(2, result.Actions.Count);
    }

    [Fact]
    public void Parser_ParseFile_Utf16BeBom_Transcoded()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((0, 10), (100, 90));
        var bom = new byte[] { 0xFE, 0xFF };
        var jsonBytes = Encoding.BigEndianUnicode.GetBytes(json);
        var withBom = new byte[bom.Length + jsonBytes.Length];
        bom.CopyTo(withBom, 0);
        jsonBytes.CopyTo(withBom, bom.Length);
        var path = WriteFileBytes("utf16be.funscript", withBom);

        var result = parser.ParseFile(path);

        Assert.Equal(2, result.Actions.Count);
    }

    // ──────────────────────────────────────────────
    //  FunscriptParser — TryParseMultiAxis
    // ──────────────────────────────────────────────

    [Fact]
    public void Parser_TryParseMultiAxis_WithAxesArray_ReturnsDictionary()
    {
        var parser = new FunscriptParser();
        var json = """
        {
            "actions": [{"at":0,"pos":50},{"at":1000,"pos":100}],
            "axes": [
                {"id":"R0","actions":[{"at":0,"pos":25},{"at":500,"pos":75}]},
                {"id":"R1","actions":[{"at":0,"pos":10},{"at":300,"pos":90}]}
            ]
        }
        """;
        var path = WriteFile("multi.funscript", json);

        var result = parser.TryParseMultiAxis(path);

        Assert.NotNull(result);
        Assert.True(result.ContainsKey("L0"));
        Assert.True(result.ContainsKey("R0"));
        Assert.True(result.ContainsKey("R1"));
        Assert.Equal(2, result["L0"].Actions.Count);
        Assert.Equal(2, result["R0"].Actions.Count);
        Assert.Equal(2, result["R1"].Actions.Count);
    }

    [Fact]
    public void Parser_TryParseMultiAxis_NoAxesArray_ReturnsNull()
    {
        var parser = new FunscriptParser();
        var json = MakeJson((0, 50), (1000, 100));
        var path = WriteFile("single.funscript", json);

        var result = parser.TryParseMultiAxis(path);

        Assert.Null(result);
    }

    [Fact]
    public void Parser_TryParseMultiAxis_UnsupportedAxis_Filtered()
    {
        var parser = new FunscriptParser();
        var json = """
        {
            "axes": [
                {"id":"L1","actions":[{"at":0,"pos":50}]},
                {"id":"R0","actions":[{"at":0,"pos":25}]}
            ]
        }
        """;
        var path = WriteFile("unsupported.funscript", json);

        var result = parser.TryParseMultiAxis(path);

        Assert.NotNull(result);
        Assert.False(result!.ContainsKey("L1"));
        Assert.True(result.ContainsKey("R0"));
    }

    [Fact]
    public void Parser_TryParseMultiAxis_EmptyAxesArray_ReturnsNull()
    {
        var parser = new FunscriptParser();
        var json = "{\"axes\":[]}";
        var path = WriteFile("empty_axes.funscript", json);

        var result = parser.TryParseMultiAxis(path);

        Assert.Null(result);
    }

    [Fact]
    public void Parser_TryParseMultiAxis_MalformedFile_ReturnsNull()
    {
        var parser = new FunscriptParser();
        var path = WriteFile("bad.funscript", "not json at all {{{");

        var result = parser.TryParseMultiAxis(path);

        Assert.Null(result);
    }

    // ──────────────────────────────────────────────
    //  FunscriptMatcher
    // ──────────────────────────────────────────────

    [Fact]
    public void Matcher_FindMatchingScripts_L0Match_Found()
    {
        var matcher = new FunscriptMatcher();
        var videoPath = Path.Combine(_tempDir, "video.mp4");
        WriteFile("video.mp4", "");
        WriteFile("video.funscript", "{}");

        var result = matcher.FindMatchingScripts(videoPath);

        Assert.True(result.ContainsKey("L0"));
        Assert.Contains("video.funscript", result["L0"]);
    }

    [Fact]
    public void Matcher_FindMatchingScripts_AllAxes_Found()
    {
        var matcher = new FunscriptMatcher();
        var videoPath = Path.Combine(_tempDir, "movie.mp4");
        WriteFile("movie.mp4", "");
        WriteFile("movie.funscript", "{}");
        WriteFile("movie.twist.funscript", "{}");
        WriteFile("movie.roll.funscript", "{}");
        WriteFile("movie.pitch.funscript", "{}");

        var result = matcher.FindMatchingScripts(videoPath);

        Assert.Equal(4, result.Count);
        Assert.True(result.ContainsKey("L0"));
        Assert.True(result.ContainsKey("R0"));
        Assert.True(result.ContainsKey("R1"));
        Assert.True(result.ContainsKey("R2"));
    }

    [Fact]
    public void Matcher_FindMatchingScripts_CaseInsensitive()
    {
        var matcher = new FunscriptMatcher();
        var videoPath = Path.Combine(_tempDir, "clip.mp4");
        WriteFile("clip.mp4", "");
        WriteFile("CLIP.FUNSCRIPT", "{}");
        WriteFile("clip.TWIST.funscript", "{}");

        var result = matcher.FindMatchingScripts(videoPath);

        Assert.True(result.ContainsKey("L0"));
        Assert.True(result.ContainsKey("R0"));
    }

    [Fact]
    public void Matcher_FindMatchingScripts_NoMatches_ReturnsEmpty()
    {
        var matcher = new FunscriptMatcher();
        var videoPath = Path.Combine(_tempDir, "something.mp4");
        WriteFile("something.mp4", "");

        var result = matcher.FindMatchingScripts(videoPath);

        Assert.Empty(result);
    }

    [Fact]
    public void Matcher_FindMatchingScripts_NullPath_ReturnsEmpty()
    {
        var matcher = new FunscriptMatcher();

        var result = matcher.FindMatchingScripts(null!);

        Assert.Empty(result);
    }

    [Fact]
    public void Matcher_FindMatchingScripts_EmptyPath_ReturnsEmpty()
    {
        var matcher = new FunscriptMatcher();

        var result = matcher.FindMatchingScripts("");

        Assert.Empty(result);
    }

    [Fact]
    public void Matcher_FindMatchingScripts_NonExistentDirectory_ReturnsEmpty()
    {
        var matcher = new FunscriptMatcher();

        var result = matcher.FindMatchingScripts(Path.Combine(_tempDir, "nonexistent", "video.mp4"));

        Assert.Empty(result);
    }

    // ──────────────────────────────────────────────
    //  InterpolationService
    // ──────────────────────────────────────────────

    [Fact]
    public void Interpolation_EmptyActions_Returns50()
    {
        var service = new InterpolationService();
        var script = new FunscriptData { AxisId = "L0" };

        Assert.Equal(50.0, service.GetPosition(script, 500, "L0"));
    }

    [Fact]
    public void Interpolation_SingleAction_ReturnsItsPos()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(1000, 75)]
        };

        Assert.Equal(75.0, service.GetPosition(script, 500, "L0"));
        Assert.Equal(75.0, service.GetPosition(script, 1500, "L0"));
    }

    [Fact]
    public void Interpolation_BeforeFirstAction_ReturnsFirstPos()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(1000, 25), new FunscriptAction(2000, 75)]
        };

        Assert.Equal(25.0, service.GetPosition(script, 0, "L0"));
        Assert.Equal(25.0, service.GetPosition(script, 999, "L0"));
    }

    [Fact]
    public void Interpolation_AfterLastAction_ReturnsLastPos()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(1000, 25), new FunscriptAction(2000, 75)]
        };

        Assert.Equal(75.0, service.GetPosition(script, 2000, "L0"));
        Assert.Equal(75.0, service.GetPosition(script, 5000, "L0"));
    }

    [Fact]
    public void Interpolation_ExactMatch_ReturnsExactPos()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(0, 0), new FunscriptAction(1000, 100), new FunscriptAction(2000, 0)]
        };

        Assert.Equal(0.0, service.GetPosition(script, 0, "L0"));
        Assert.Equal(100.0, service.GetPosition(script, 1000, "L0"));
    }

    [Fact]
    public void Interpolation_Midpoint_LinearlyInterpolated()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(0, 0), new FunscriptAction(1000, 100)]
        };

        Assert.Equal(50.0, service.GetPosition(script, 500, "L0"));
        Assert.Equal(25.0, service.GetPosition(script, 250, "L0"));
        Assert.Equal(75.0, service.GetPosition(script, 750, "L0"));
    }

    [Fact]
    public void Interpolation_SequentialCalls_UseCachedIndex()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions =
            [
                new FunscriptAction(0, 0),
                new FunscriptAction(1000, 100),
                new FunscriptAction(2000, 0),
                new FunscriptAction(3000, 100),
            ]
        };

        // Sequential forward calls should use cached index
        Assert.Equal(50.0, service.GetPosition(script, 500, "L0"));
        Assert.Equal(50.0, service.GetPosition(script, 1500, "L0"));
        Assert.Equal(50.0, service.GetPosition(script, 2500, "L0"));
    }

    [Fact]
    public void Interpolation_SeekBackward_FallsBackToBinarySearch()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions =
            [
                new FunscriptAction(0, 0),
                new FunscriptAction(1000, 100),
                new FunscriptAction(2000, 0),
                new FunscriptAction(3000, 100),
            ]
        };

        // Go to end, then seek back
        service.GetPosition(script, 2500, "L0");
        var result = service.GetPosition(script, 500, "L0");

        Assert.Equal(50.0, result);
    }

    [Fact]
    public void Interpolation_ResetIndices_ClearsCache()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions =
            [
                new FunscriptAction(0, 0),
                new FunscriptAction(1000, 100),
                new FunscriptAction(2000, 0),
            ]
        };

        service.GetPosition(script, 1500, "L0");
        service.ResetIndices();

        // After reset, should still work correctly
        var result = service.GetPosition(script, 500, "L0");
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void Interpolation_DifferentAxes_IndependentCaches()
    {
        var service = new InterpolationService();
        var l0 = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(0, 0), new FunscriptAction(1000, 100)]
        };
        var r0 = new FunscriptData
        {
            AxisId = "R0",
            Actions = [new FunscriptAction(0, 100), new FunscriptAction(1000, 0)]
        };

        var posL0 = service.GetPosition(l0, 500, "L0");
        var posR0 = service.GetPosition(r0, 500, "R0");

        Assert.Equal(50.0, posL0);
        Assert.Equal(50.0, posR0);
    }

    [Fact]
    public void Interpolation_ZeroRange_ReturnsFirstPos()
    {
        var service = new InterpolationService();
        var script = new FunscriptData
        {
            AxisId = "L0",
            Actions = [new FunscriptAction(1000, 25), new FunscriptAction(1000, 75)]
        };

        // Same timestamp — zero range, should return first pos
        var result = service.GetPosition(script, 1000, "L0");
        Assert.Equal(25.0, result);
    }

    // ──────────────────────────────────────────────
    //  BeatDetectionService
    // ──────────────────────────────────────────────

    [Fact]
    public void BeatDetection_NullScript_ReturnsEmpty()
    {
        var service = new BeatDetectionService();

        var result = service.DetectBeats(null, BeatDetectionMode.OnPeak);

        Assert.Empty(result);
    }

    [Fact]
    public void BeatDetection_TooFewActions_ReturnsEmpty()
    {
        var service = new BeatDetectionService();
        var script = new FunscriptData
        {
            Actions = [new FunscriptAction(0, 0), new FunscriptAction(1000, 100)]
        };

        var result = service.DetectBeats(script, BeatDetectionMode.OnPeak);

        Assert.Empty(result);
    }

    [Fact]
    public void BeatDetection_OnPeak_DetectsPeaks()
    {
        var service = new BeatDetectionService();
        var script = new FunscriptData
        {
            Actions =
            [
                new FunscriptAction(0, 0),
                new FunscriptAction(500, 100),   // Peak
                new FunscriptAction(1000, 0),
                new FunscriptAction(1500, 80),   // Peak
                new FunscriptAction(2000, 20),
            ]
        };

        var result = service.DetectBeats(script, BeatDetectionMode.OnPeak);

        Assert.Equal(2, result.Count);
        Assert.Equal(500, result[0]);
        Assert.Equal(1500, result[1]);
    }

    [Fact]
    public void BeatDetection_OnValley_DetectsValleys()
    {
        var service = new BeatDetectionService();
        var script = new FunscriptData
        {
            Actions =
            [
                new FunscriptAction(0, 100),
                new FunscriptAction(500, 10),    // Valley
                new FunscriptAction(1000, 90),
                new FunscriptAction(1500, 5),    // Valley
                new FunscriptAction(2000, 80),
            ]
        };

        var result = service.DetectBeats(script, BeatDetectionMode.OnValley);

        Assert.Equal(2, result.Count);
        Assert.Equal(500, result[0]);
        Assert.Equal(1500, result[1]);
    }

    [Fact]
    public void BeatDetection_Plateau_PeakWithEqualNeighbor()
    {
        var service = new BeatDetectionService();
        // curr > prev && curr >= next — "equal next" counts as peak
        var script = new FunscriptData
        {
            Actions =
            [
                new FunscriptAction(0, 0),
                new FunscriptAction(500, 100),
                new FunscriptAction(1000, 100),  // equal to prev, not > prev → not a peak
                new FunscriptAction(1500, 0),
            ]
        };

        var result = service.DetectBeats(script, BeatDetectionMode.OnPeak);

        // Index 1: 100 > 0 (prev) && 100 >= 100 (next) → peak
        // Index 2: 100 > 100 is false → not peak
        Assert.Single(result);
        Assert.Equal(500, result[0]);
    }

    [Fact]
    public void BeatDetection_FlatLine_NoBeatDetected()
    {
        var service = new BeatDetectionService();
        var script = new FunscriptData
        {
            Actions =
            [
                new FunscriptAction(0, 50),
                new FunscriptAction(500, 50),
                new FunscriptAction(1000, 50),
                new FunscriptAction(1500, 50),
            ]
        };

        Assert.Empty(service.DetectBeats(script, BeatDetectionMode.OnPeak));
        Assert.Empty(service.DetectBeats(script, BeatDetectionMode.OnValley));
    }

    /// <summary>
    /// Verifies that OnPeakAndValley detects both peaks and valleys in a single pass.
    /// </summary>
    [Fact]
    public void BeatDetection_OnPeakAndValley_DetectsBoth()
    {
        var service = new BeatDetectionService();
        var script = new FunscriptData
        {
            Actions =
            [
                new FunscriptAction(0, 0),
                new FunscriptAction(500, 100),   // Peak
                new FunscriptAction(1000, 0),    // Valley
                new FunscriptAction(1500, 80),   // Peak
                new FunscriptAction(2000, 10),   // Valley
                new FunscriptAction(2500, 50),
            ]
        };

        var result = service.DetectBeats(script, BeatDetectionMode.OnPeakAndValley);

        Assert.Equal(4, result.Count);
        Assert.Equal(500, result[0]);   // Peak
        Assert.Equal(1000, result[1]);  // Valley
        Assert.Equal(1500, result[2]);  // Peak
        Assert.Equal(2000, result[3]);  // Valley
    }

    /// <summary>
    /// Verifies that OnPeakAndValley returns an empty list for a null script.
    /// </summary>
    [Fact]
    public void BeatDetection_OnPeakAndValley_NullScript_ReturnsEmpty()
    {
        var service = new BeatDetectionService();
        Assert.Empty(service.DetectBeats(null, BeatDetectionMode.OnPeakAndValley));
    }

    // ──────────────────────────────────────────────
    //  FunscriptLoadingService
    // ──────────────────────────────────────────────

    [Fact]
    public void LoadingService_LoadScriptsForVideo_AutoMatches()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var videoPath = Path.Combine(_tempDir, "test.mp4");
        WriteFile("test.mp4", "");
        WriteFile("test.funscript", MakeJson((0, 0), (1000, 100)));

        var logs = service.LoadScriptsForVideo(videoPath);

        Assert.Single(service.LoadedScripts);
        Assert.True(service.LoadedScripts.ContainsKey("L0"));
        Assert.Equal(2, service.LoadedScripts["L0"].Actions.Count);
        Assert.Equal(videoPath, service.CurrentVideoPath);
        Assert.Contains(logs, l => l.Contains("Auto-matched"));
    }

    [Fact]
    public void LoadingService_LoadScriptsForVideo_MultiAxis()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var videoPath = Path.Combine(_tempDir, "multi.mp4");
        WriteFile("multi.mp4", "");
        var multiJson = """
        {
            "actions": [{"at":0,"pos":50},{"at":1000,"pos":100}],
            "axes": [
                {"id":"R0","actions":[{"at":0,"pos":25},{"at":500,"pos":75}]}
            ]
        }
        """;
        WriteFile("multi.funscript", multiJson);

        var logs = service.LoadScriptsForVideo(videoPath);

        Assert.True(service.LoadedScripts.ContainsKey("L0"));
        Assert.True(service.LoadedScripts.ContainsKey("R0"));
        Assert.Contains(logs, l => l.Contains("Multi-axis"));
    }

    [Fact]
    public void LoadingService_LoadScriptsForVideo_EmptyPath_ReturnsLog()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var logs = service.LoadScriptsForVideo("");

        Assert.Empty(service.LoadedScripts);
        Assert.Contains(logs, l => l.Contains("No video path"));
    }

    [Fact]
    public void LoadingService_LoadScriptsForVideo_NoScripts_ReturnsLog()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var videoPath = Path.Combine(_tempDir, "noscript.mp4");
        WriteFile("noscript.mp4", "");

        var logs = service.LoadScriptsForVideo(videoPath);

        Assert.Empty(service.LoadedScripts);
        Assert.Contains(logs, l => l.Contains("No funscript files"));
    }

    [Fact]
    public void LoadingService_ClearScripts_ClearsAll()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var videoPath = Path.Combine(_tempDir, "clear.mp4");
        WriteFile("clear.mp4", "");
        WriteFile("clear.funscript", MakeJson((0, 0), (1000, 100)));
        service.LoadScriptsForVideo(videoPath);

        var logs = service.ClearScripts();

        Assert.Empty(service.LoadedScripts);
        Assert.Null(service.CurrentVideoPath);
        Assert.Contains(logs, l => l.Contains("Cleared"));
    }

    [Fact]
    public void LoadingService_ClearScripts_WhenEmpty_NoLog()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var logs = service.ClearScripts();

        Assert.Empty(logs);
    }

    [Fact]
    public void LoadingService_ScriptsChanged_FiredOnLoad()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        IReadOnlyDictionary<string, FunscriptData>? received = null;
        service.ScriptsChanged += dict => received = dict;

        var videoPath = Path.Combine(_tempDir, "event.mp4");
        WriteFile("event.mp4", "");
        WriteFile("event.funscript", MakeJson((0, 50)));

        service.LoadScriptsForVideo(videoPath);

        Assert.NotNull(received);
        Assert.Single(received!);
    }

    [Fact]
    public void LoadingService_ScriptsChanged_FiredOnClear()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var videoPath = Path.Combine(_tempDir, "clearevent.mp4");
        WriteFile("clearevent.mp4", "");
        WriteFile("clearevent.funscript", MakeJson((0, 50)));
        service.LoadScriptsForVideo(videoPath);

        IReadOnlyDictionary<string, FunscriptData>? received = null;
        service.ScriptsChanged += dict => received = dict;

        service.ClearScripts();

        Assert.NotNull(received);
        Assert.Empty(received!);
    }

    [Fact]
    public void LoadingService_ManualOverride_TakesPrecedence()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        var videoPath = Path.Combine(_tempDir, "override.mp4");
        WriteFile("override.mp4", "");
        WriteFile("override.funscript", MakeJson((0, 0), (1000, 100)));
        var overridePath = WriteFile("custom.funscript", MakeJson((0, 25), (500, 75), (1000, 50)));

        service.SetManualOverride("L0", overridePath);
        var logs = service.LoadScriptsForVideo(videoPath);

        Assert.True(service.LoadedScripts.ContainsKey("L0"));
        Assert.Equal(3, service.LoadedScripts["L0"].Actions.Count);
        Assert.Contains(logs, l => l.Contains("Manual override"));
    }

    [Fact]
    public void LoadingService_ManualOverride_Properties()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        Assert.False(service.HasManualOverrides);
        Assert.Empty(service.ManualOverrides);

        service.SetManualOverride("R0", "fake.funscript");

        Assert.True(service.HasManualOverrides);
        Assert.Single(service.ManualOverrides);

        service.ClearManualOverride("R0");

        Assert.False(service.HasManualOverrides);
    }

    [Fact]
    public void LoadingService_ClearAllManualOverrides_ClearsAll()
    {
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        var service = new FunscriptLoadingService(parser, matcher);

        service.SetManualOverride("L0", "a.funscript");
        service.SetManualOverride("R0", "b.funscript");

        service.ClearAllManualOverrides();

        Assert.False(service.HasManualOverrides);
        Assert.Empty(service.ManualOverrides);
    }
}
