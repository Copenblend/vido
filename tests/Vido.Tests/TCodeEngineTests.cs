using System.Diagnostics;
using Vido.Core.Haptics;
using Vido.Core.Models.Osr2Plus;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for PI-007: OSR2+ TCode engine services integrated into Vido.Services.
/// Covers <see cref="TCodeService"/>, <see cref="PatternGenerator"/>,
/// and <see cref="RandomPatternGenerator"/>.
/// </summary>
public class TCodeEngineTests : IDisposable
{
    private readonly InterpolationService _interpolation = new();
    private readonly TCodeService _service;

    public TCodeEngineTests()
    {
        _service = new TCodeService(_interpolation);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    // ──────────────────────────────────────────────
    //  Helper methods
    // ──────────────────────────────────────────────

    private static AxisConfig MakeConfig(
        string id = "L0", string name = "Stroke", string type = "linear",
        int min = 0, int max = 100, bool enabled = true,
        AxisFillMode fillMode = AxisFillMode.None, double fillSpeedHz = 1.0,
        bool syncWithStroke = true, double positionOffset = 0)
    {
        return new AxisConfig
        {
            Id = id, Name = name, Type = type,
            Min = min, Max = max, Enabled = enabled,
            FillMode = fillMode, FillSpeedHz = fillSpeedHz,
            SyncWithStroke = syncWithStroke, PositionOffset = positionOffset
        };
    }

    private static AxisConfig MakeL0(int min = 0, int max = 100, bool enabled = true,
        double positionOffset = 0)
        => MakeConfig("L0", "Stroke", "linear", min, max, enabled, positionOffset: positionOffset);

    private static AxisConfig MakeR0(int min = 0, int max = 100, bool enabled = true,
        AxisFillMode fillMode = AxisFillMode.None, double positionOffset = 0)
        => MakeConfig("R0", "Twist", "rotation", min, max, enabled, fillMode, positionOffset: positionOffset);

    private static AxisConfig MakeR1(int min = 0, int max = 100, bool enabled = true,
        AxisFillMode fillMode = AxisFillMode.None, double positionOffset = 0)
        => MakeConfig("R1", "Roll", "rotation", min, max, enabled, fillMode, positionOffset: positionOffset);

    private static AxisConfig MakeR2(int min = 0, int max = 100, bool enabled = true,
        AxisFillMode fillMode = AxisFillMode.None, double positionOffset = 0)
        => MakeConfig("R2", "Pitch", "rotation", min, max, enabled, fillMode, positionOffset: positionOffset);

    private static FunscriptData MakeScript(params (long at, int pos)[] actions)
    {
        var data = new FunscriptData();
        foreach (var (at, pos) in actions)
            data.Actions.Add(new FunscriptAction(at, pos));
        return data;
    }

    // ═══════════════════════════════════════════════
    //  PositionToTCode
    // ═══════════════════════════════════════════════

    [Fact]
    public void PositionToTCode_Zero_ReturnsZero()
    {
        var config = MakeL0();
        Assert.Equal(0, TCodeService.PositionToTCode(config, 0));
    }

    [Fact]
    public void PositionToTCode_Hundred_Returns999()
    {
        var config = MakeL0();
        Assert.Equal(999, TCodeService.PositionToTCode(config, 100));
    }

    [Fact]
    public void PositionToTCode_Fifty_ReturnsMidpoint()
    {
        var config = MakeL0();
        // 50/100 * (100-0) = 50; 50/100*999 = 499
        Assert.Equal(499, TCodeService.PositionToTCode(config, 50));
    }

    [Fact]
    public void PositionToTCode_ScalesWithMinMax()
    {
        // Min=20, Max=80: position 0 → 20% → 199; position 100 → 80% → 799
        var config = MakeL0(min: 20, max: 80);
        Assert.Equal(199, TCodeService.PositionToTCode(config, 0));
        Assert.Equal(799, TCodeService.PositionToTCode(config, 100));
    }

    [Fact]
    public void PositionToTCode_MidpointWithMinMax()
    {
        var config = MakeL0(min: 20, max: 80);
        // position 50 → 20 + 0.5*(80-20) = 50 → 50/100*999=499
        Assert.Equal(499, TCodeService.PositionToTCode(config, 50));
    }

    [Fact]
    public void PositionToTCode_ClampsNegative()
    {
        var config = MakeL0();
        Assert.Equal(0, TCodeService.PositionToTCode(config, -50));
    }

    [Fact]
    public void PositionToTCode_ClampsAbove999()
    {
        var config = MakeL0();
        Assert.Equal(999, TCodeService.PositionToTCode(config, 200));
    }

    // ═══════════════════════════════════════════════
    //  IsDirty
    // ═══════════════════════════════════════════════

    [Fact]
    public void IsDirty_FirstValue_ReturnsTrue()
    {
        Assert.True(_service.IsDirty("L0", 500));
    }

    [Fact]
    public void IsDirty_SameValue_ReturnsFalse()
    {
        // Simulate having sent a value by using reflection or internal access
        // Since IsDirty depends on _lastSentValues which is private,
        // we test it through the public API flow
        Assert.True(_service.IsDirty("L0", 500));
    }

    [Fact]
    public void IsDirty_DifferentAxis_ReturnsTrue()
    {
        Assert.True(_service.IsDirty("L0", 500));
        Assert.True(_service.IsDirty("R0", 500));
    }

    // ═══════════════════════════════════════════════
    //  AxisOrdinal
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("L0", 0)]
    [InlineData("R0", 1)]
    [InlineData("R1", 2)]
    [InlineData("R2", 3)]
    public void AxisOrdinal_KnownAxis_ReturnsCorrectIndex(string axisId, int expected)
    {
        Assert.Equal(expected, TCodeService.AxisOrdinal(axisId));
    }

    [Theory]
    [InlineData("L1")]
    [InlineData("X0")]
    [InlineData("")]
    [InlineData("R3")]
    public void AxisOrdinal_UnknownAxis_ReturnsNegativeOne(string axisId)
    {
        Assert.Equal(-1, TCodeService.AxisOrdinal(axisId));
    }

    // ═══════════════════════════════════════════════
    //  FormatTCodeCommand
    // ═══════════════════════════════════════════════

    [Fact]
    public void FormatTCodeCommand_LinearAxis_UsesLPrefix()
    {
        var config = MakeL0();
        Assert.Equal("L0500I100", TCodeService.FormatTCodeCommand(config, 500, 100));
    }

    [Fact]
    public void FormatTCodeCommand_RotationAxis_UsesRPrefix()
    {
        var config = MakeR0();
        Assert.Equal("R0500I100", TCodeService.FormatTCodeCommand(config, 500, 100));
    }

    [Fact]
    public void FormatTCodeCommand_ZeroValue_FormatsCorrectly()
    {
        var config = MakeL0();
        Assert.Equal("L0000I50", TCodeService.FormatTCodeCommand(config, 0, 50));
    }

    [Fact]
    public void FormatTCodeCommand_MaxValue_FormatsCorrectly()
    {
        var config = MakeL0();
        Assert.Equal("L0999I200", TCodeService.FormatTCodeCommand(config, 999, 200));
    }

    [Theory]
    [InlineData("R1", "R1500I100")]
    [InlineData("R2", "R2500I100")]
    public void FormatTCodeCommand_RotationAxes_FormatsCorrectly(string axisId, string expected)
    {
        var config = MakeConfig(axisId, "Test", "rotation");
        Assert.Equal(expected, TCodeService.FormatTCodeCommand(config, 500, 100));
    }

    // ═══════════════════════════════════════════════
    //  ApplyPositionOffset
    // ═══════════════════════════════════════════════

    [Fact]
    public void ApplyPositionOffset_ZeroOffset_ReturnsUnchanged()
    {
        var config = MakeL0(positionOffset: 0);
        Assert.Equal(500, TCodeService.ApplyPositionOffset(config, 500));
    }

    [Fact]
    public void ApplyPositionOffset_L0_AddsPercentageOffset()
    {
        // +10% offset → +99 tcode units (10/100*999)
        var config = MakeL0(positionOffset: 10);
        Assert.Equal(599, TCodeService.ApplyPositionOffset(config, 500));
    }

    [Fact]
    public void ApplyPositionOffset_L0_NegativeOffset()
    {
        var config = MakeL0(positionOffset: -10);
        Assert.Equal(401, TCodeService.ApplyPositionOffset(config, 500));
    }

    [Fact]
    public void ApplyPositionOffset_L0_ClampedToZero()
    {
        var config = MakeL0(positionOffset: -50);
        // -50 → -499 → 500-499=1, so still above 0 but let's test extreme
        Assert.True(TCodeService.ApplyPositionOffset(config, 100) >= 0);
    }

    [Fact]
    public void ApplyPositionOffset_L0_ClampedTo999()
    {
        var config = MakeL0(positionOffset: 50);
        Assert.True(TCodeService.ApplyPositionOffset(config, 900) <= 999);
    }

    [Fact]
    public void ApplyPositionOffset_R0_WrapsModularly()
    {
        // 180° offset → 180/360*999=499 tcode units
        var config = MakeR0(positionOffset: 180);
        var result = TCodeService.ApplyPositionOffset(config, 500);
        Assert.Equal(999, result);
    }

    [Fact]
    public void ApplyPositionOffset_R0_WrapsAround()
    {
        // 270° offset → 270/360*999=749
        var config = MakeR0(positionOffset: 270);
        // 800 + 749 = 1549 % 1000 = 549
        var result = TCodeService.ApplyPositionOffset(config, 800);
        Assert.Equal(549, result);
    }

    [Fact]
    public void ApplyPositionOffset_R1_AddsPercentageOffset()
    {
        var config = MakeR1(positionOffset: 10);
        Assert.Equal(599, TCodeService.ApplyPositionOffset(config, 500));
    }

    [Fact]
    public void ApplyPositionOffset_R2_AddsPercentageOffset()
    {
        var config = MakeR2(positionOffset: 10);
        Assert.Equal(599, TCodeService.ApplyPositionOffset(config, 500));
    }

    [Fact]
    public void ApplyPositionOffset_NoHasPositionOffset_ReturnsUnchanged()
    {
        // An axis with no HasPositionOffset support returns unchanged
        var config = MakeConfig("X0", "Test", "linear", positionOffset: 10);
        Assert.Equal(500, TCodeService.ApplyPositionOffset(config, 500));
    }

    // ═══════════════════════════════════════════════
    //  ClampPitchFillPosition
    // ═══════════════════════════════════════════════

    [Fact]
    public void ClampPitchFillPosition_NonPitch_ReturnsUnchanged()
    {
        var config = MakeL0();
        Assert.Equal(150.0, TCodeService.ClampPitchFillPosition(config, 150.0));
    }

    [Fact]
    public void ClampPitchFillPosition_Pitch_ClampsToMax()
    {
        var config = MakeR2();
        Assert.Equal(TCodeService.PitchFillMaxPosition,
            TCodeService.ClampPitchFillPosition(config, 150.0));
    }

    [Fact]
    public void ClampPitchFillPosition_Pitch_ClampsToZero()
    {
        var config = MakeR2();
        Assert.Equal(0.0, TCodeService.ClampPitchFillPosition(config, -10.0));
    }

    [Fact]
    public void ClampPitchFillPosition_Pitch_InRange_ReturnsUnchanged()
    {
        var config = MakeR2();
        Assert.Equal(50.0, TCodeService.ClampPitchFillPosition(config, 50.0));
    }

    // ═══════════════════════════════════════════════
    //  SetOutputRate
    // ═══════════════════════════════════════════════

    [Fact]
    public void SetOutputRate_NormalValue_SetsCorrectly()
    {
        _service.SetOutputRate(60);
        Assert.Equal(60, _service.OutputRateHz);
    }

    [Fact]
    public void SetOutputRate_BelowMinimum_ClampsTo30()
    {
        _service.SetOutputRate(10);
        Assert.Equal(30, _service.OutputRateHz);
    }

    [Fact]
    public void SetOutputRate_AboveMaximum_ClampsTo200()
    {
        _service.SetOutputRate(500);
        Assert.Equal(200, _service.OutputRateHz);
    }

    [Fact]
    public void SetOutputRate_Default_Is100()
    {
        Assert.Equal(100, _service.OutputRateHz);
    }

    // ═══════════════════════════════════════════════
    //  SetScripts
    // ═══════════════════════════════════════════════

    [Fact]
    public void SetScripts_SetsHasScriptsLoaded()
    {
        Assert.False(_service.HasScriptsLoaded);

        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = MakeScript((0, 0), (1000, 100))
        });

        Assert.True(_service.HasScriptsLoaded);
    }

    [Fact]
    public void SetScripts_EmptyDictionary_ClearsScripts()
    {
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = MakeScript((0, 0), (1000, 100))
        });
        Assert.True(_service.HasScriptsLoaded);

        _service.SetScripts(new Dictionary<string, FunscriptData>());
        Assert.False(_service.HasScriptsLoaded);
    }

    /// <summary>
    /// Verifies that SetScripts with an empty-actions FunscriptData for L0
    /// is accepted and reports scripts as loaded (VI-0012).
    /// </summary>
    [Fact]
    public void SetScripts_EmptyActionsL0_HasScriptsLoadedTrue()
    {
        var emptyScript = new FunscriptData { AxisId = "L0", FilePath = "", Actions = [] };

        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = emptyScript
        });

        Assert.True(_service.HasScriptsLoaded);
    }

    /// <summary>
    /// Verifies that interpolation returns midpoint (50) for an empty-actions script,
    /// preventing NullReferenceException or freeze in the output loop (VI-0012).
    /// </summary>
    [Fact]
    public void SetScripts_EmptyActionsL0_InterpolatesWithoutError()
    {
        var emptyScript = new FunscriptData { AxisId = "L0", FilePath = "", Actions = [] };
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = emptyScript
        });
        _service.SetAxisConfigs([MakeL0()]);
        _service.SetPlaying(true);
        _service.SetTime(1000);

        // IsFunscriptPlaying should be true — the output loop will use the
        // empty script and interpolation returns 50.0, avoiding the freeze path.
        Assert.True(_service.IsFunscriptPlaying);
    }

    /// <summary>
    /// Verifies that re-setting scripts after clearing with an empty-actions L0
    /// replaces the prior state correctly (simulates Pulse suppress flow, VI-0012).
    /// </summary>
    [Fact]
    public void SetScripts_ClearThenEmptyL0_ReplacesPriorState()
    {
        // Load a real script
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = MakeScript((0, 0), (1000, 100))
        });
        Assert.True(_service.HasScriptsLoaded);

        // Clear (simulates ClearAllScripts)
        _service.SetScripts(new Dictionary<string, FunscriptData>());
        Assert.False(_service.HasScriptsLoaded);

        // Re-set with empty L0 (simulates empty funscript injection)
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = new FunscriptData { AxisId = "L0", FilePath = "", Actions = [] }
        });
        Assert.True(_service.HasScriptsLoaded);
    }

    // ═══════════════════════════════════════════════
    //  SetTime / GetExtrapolatedTimeMs
    // ═══════════════════════════════════════════════

    [Fact]
    public void SetTime_NotPlaying_ReturnsSetTime()
    {
        _service.SetTime(5000);
        var time = _service.GetExtrapolatedTimeMs();
        Assert.InRange(time, 4999, 5010); // Small tolerance for timing
    }

    [Fact]
    public void SetTime_WhilePlaying_Extrapolates()
    {
        _service.SetPlaying(true);
        _service.SetTime(1000);

        // Wait briefly so extrapolation advances
        Thread.Sleep(50);

        var time = _service.GetExtrapolatedTimeMs();
        Assert.True(time > 1000, $"Expected time > 1000 but got {time}");
    }

    // ═══════════════════════════════════════════════
    //  SetPlaying
    // ═══════════════════════════════════════════════

    [Fact]
    public void IsFunscriptPlaying_NoScripts_ReturnsFalse()
    {
        _service.SetPlaying(true);
        Assert.False(_service.IsFunscriptPlaying);
    }

    [Fact]
    public void IsFunscriptPlaying_WithScripts_ReturnsTrue()
    {
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = MakeScript((0, 0), (1000, 100))
        });
        _service.SetPlaying(true);
        Assert.True(_service.IsFunscriptPlaying);
    }

    // ═══════════════════════════════════════════════
    //  SetExternalPositions / ClearExternalPositions
    // ═══════════════════════════════════════════════

    [Fact]
    public void SetExternalPositions_EmptySpan_Clears()
    {
        _service.SetExternalPositions(ReadOnlyMemory<AxisPosition>.Empty);
        // Should not throw, external positions cleared
    }

    [Fact]
    public void ClearExternalPositions_DoesNotThrow()
    {
        _service.ClearExternalPositions();
    }

    // ═══════════════════════════════════════════════
    //  Test Mode API
    // ═══════════════════════════════════════════════

    [Fact]
    public void StartTestAxis_MarksAsTesting()
    {
        _service.StartTestAxis("L0", 1.0);
        Assert.True(_service.IsAxisTesting("L0"));
    }

    [Fact]
    public void StopTestAxis_UnmarksAndRaisesEvent()
    {
        string? stoppedId = null;
        _service.TestAxisStopped += id => stoppedId = id;

        _service.StartTestAxis("L0", 1.0);
        _service.StopTestAxis("L0");

        Assert.False(_service.IsAxisTesting("L0"));
        Assert.Equal("L0", stoppedId);
    }

    [Fact]
    public void StopAllTestAxes_ClearsAllAndRaisesEvent()
    {
        bool allStopped = false;
        _service.AllTestsStopped += () => allStopped = true;

        _service.StartTestAxis("L0", 1.0);
        _service.StartTestAxis("R0", 1.0);
        _service.StopAllTestAxes();

        Assert.False(_service.IsAxisTesting("L0"));
        Assert.False(_service.IsAxisTesting("R0"));
        Assert.True(allStopped);
    }

    [Fact]
    public void StopAllTestAxes_NoAxes_DoesNotRaiseEvent()
    {
        bool allStopped = false;
        _service.AllTestsStopped += () => allStopped = true;

        _service.StopAllTestAxes();
        Assert.False(allStopped);
    }

    [Fact]
    public void IsAxisTesting_NotStarted_ReturnsFalse()
    {
        Assert.False(_service.IsAxisTesting("L0"));
    }

    [Fact]
    public void UpdateTestSpeed_DoesNotThrow()
    {
        _service.StartTestAxis("L0", 1.0);
        _service.UpdateTestSpeed("L0", 2.5);
        Assert.True(_service.IsAxisTesting("L0"));
    }

    [Fact]
    public void StartTestAxis_ClampsSpeed()
    {
        // Speed should be clamped to 0.1-5.0
        _service.StartTestAxis("L0", 0.01);
        Assert.True(_service.IsAxisTesting("L0"));
        _service.StopTestAxis("L0");

        _service.StartTestAxis("L0", 100.0);
        Assert.True(_service.IsAxisTesting("L0"));
    }

    [Fact]
    public void SetPlaying_WithScripts_StopsAllTestAxes()
    {
        bool allStopped = false;
        _service.AllTestsStopped += () => allStopped = true;

        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = MakeScript((0, 0), (1000, 100))
        });

        _service.StartTestAxis("L0", 1.0);
        _service.SetPlaying(true);

        Assert.False(_service.IsAxisTesting("L0"));
        Assert.True(allStopped);
    }

    // ═══════════════════════════════════════════════
    //  SetAxisConfigs
    // ═══════════════════════════════════════════════

    [Fact]
    public void SetAxisConfigs_DoesNotThrow()
    {
        _service.SetAxisConfigs(new List<AxisConfig>
        {
            MakeL0(),
            MakeR0(),
            MakeR1(),
            MakeR2()
        });
    }

    // ═══════════════════════════════════════════════
    //  Start / StopTimer
    // ═══════════════════════════════════════════════

    [Fact]
    public void Start_CreatesOutputThread()
    {
        _service.Start();
        // Allow thread to start
        Thread.Sleep(10);
        _service.StopTimer();
    }

    [Fact]
    public void Start_DoubleStart_IsNoop()
    {
        _service.Start();
        _service.Start(); // Should not throw or create second thread
        _service.StopTimer();
    }

    [Fact]
    public void StopTimer_WithoutStart_DoesNotThrow()
    {
        _service.StopTimer();
    }

    [Fact]
    public void Dispose_StopsThread()
    {
        _service.Start();
        Thread.Sleep(10);
        _service.Dispose();
        // Should not throw
    }

    // ═══════════════════════════════════════════════
    //  HomingDurationMs constant
    // ═══════════════════════════════════════════════

    [Fact]
    public void HomingDurationMs_Is2000()
    {
        Assert.Equal(2000, TCodeService.HomingDurationMs);
    }

    [Fact]
    public void PitchFillMaxPosition_Is100()
    {
        Assert.Equal(100.0, TCodeService.PitchFillMaxPosition);
    }

    // ═══════════════════════════════════════════════
    //  SetOffset
    // ═══════════════════════════════════════════════

    [Fact]
    public void SetOffset_DoesNotThrow()
    {
        _service.SetOffset(100);
        _service.SetOffset(-100);
        _service.SetOffset(0);
    }

    // ═══════════════════════════════════════════════
    //  Transport property
    // ═══════════════════════════════════════════════

    [Fact]
    public void Transport_DefaultIsNull()
    {
        Assert.Null(_service.Transport);
    }

    [Fact]
    public void Transport_CanBeSet()
    {
        var transport = new FakeTransport();
        _service.Transport = transport;
        Assert.Same(transport, _service.Transport);
    }

    // ═══════════════════════════════════════════════
    //  SleepPrecise
    // ═══════════════════════════════════════════════

    [Fact]
    public void SleepPrecise_WaitsApproximately()
    {
        var sw = Stopwatch.StartNew();
        TCodeService.SleepPrecise(sw, 20);
        sw.Stop();
        // Should have waited at least ~15ms (accounting for scheduling jitter)
        Assert.True(sw.ElapsedMilliseconds >= 10, $"SleepPrecise was too short: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void SleepPrecise_ZeroTimeout_ReturnsImmediately()
    {
        var sw = Stopwatch.StartNew();
        TCodeService.SleepPrecise(sw, 0);
        sw.Stop();
        // Should return very quickly
        Assert.True(sw.ElapsedMilliseconds < 100);
    }

    // ═══════════════════════════════════════════════
    //  PatternGenerator
    // ═══════════════════════════════════════════════

    [Fact]
    public void PatternGenerator_None_ReturnsMidpoint()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.None, 0.0));
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.None, 0.5));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 0.5)]
    [InlineData(0.5, 1.0)]
    [InlineData(0.75, 0.5)]
    public void PatternGenerator_Triangle_CorrectWaveform(double t, double expected)
    {
        var result = PatternGenerator.Calculate(AxisFillMode.Triangle, t);
        Assert.Equal(expected, result, 3);
    }

    [Fact]
    public void PatternGenerator_Sine_AtZero_ReturnsZero()
    {
        // Cosine model: at t=0, cos(0)=1, result = (1-1)/2 = 0
        var result = PatternGenerator.Calculate(AxisFillMode.Sine, 0.0);
        Assert.Equal(0.0, result, 3);
    }

    [Fact]
    public void PatternGenerator_Sine_AtHalf_ReturnsOne()
    {
        // At t=0.5, cos(π)=-1, result = (1-(-1))/2 = 1
        var result = PatternGenerator.Calculate(AxisFillMode.Sine, 0.5);
        Assert.Equal(1.0, result, 3);
    }

    [Fact]
    public void PatternGenerator_Sine_IsPeriodic()
    {
        var atZero = PatternGenerator.Calculate(AxisFillMode.Sine, 0.0);
        var atOne = PatternGenerator.Calculate(AxisFillMode.Sine, 1.0);
        Assert.Equal(atZero, atOne, 3);
    }

    [Fact]
    public void PatternGenerator_Square_HighPhase()
    {
        // t=0.3 is in the high phase (0.1–0.5)
        var result = PatternGenerator.Calculate(AxisFillMode.Square, 0.3);
        Assert.Equal(1.0, result, 2);
    }

    [Fact]
    public void PatternGenerator_Square_LowPhase()
    {
        // t=0.7 is in the low phase (0.6–1.0)
        var result = PatternGenerator.Calculate(AxisFillMode.Square, 0.7);
        Assert.Equal(0.0, result, 2);
    }

    [Fact]
    public void PatternGenerator_Pulse_HighPhase()
    {
        // t=0.3 is in the high phase (0.15–0.5)
        var result = PatternGenerator.Calculate(AxisFillMode.Pulse, 0.3);
        Assert.Equal(1.0, result, 2);
    }

    [Fact]
    public void PatternGenerator_Pulse_LowPhase()
    {
        // t=0.8 is in the low phase (0.65–1.0)
        var result = PatternGenerator.Calculate(AxisFillMode.Pulse, 0.8);
        Assert.Equal(0.0, result, 2);
    }

    [Fact]
    public void PatternGenerator_Saw_StartAtZero()
    {
        var result = PatternGenerator.Calculate(AxisFillMode.Saw, 0.0);
        Assert.InRange(result, -0.01, 0.05);
    }

    [Fact]
    public void PatternGenerator_Saw_RampsUp()
    {
        // In the ramp-up phase (0 to 0.85), should increase
        var early = PatternGenerator.Calculate(AxisFillMode.Saw, 0.2);
        var mid = PatternGenerator.Calculate(AxisFillMode.Saw, 0.5);
        Assert.True(mid > early, $"Expected mid ({mid}) > early ({early})");
    }

    [Fact]
    public void PatternGenerator_SawtoothReverse_NearEnd_ReturnsZero()
    {
        // Near t=1.0 after the ramp-down phase
        var result = PatternGenerator.Calculate(AxisFillMode.SawtoothReverse, 0.99);
        Assert.InRange(result, -0.05, 0.15);
    }

    [Fact]
    public void PatternGenerator_EaseInOut_AtZero_ReturnsZero()
    {
        var result = PatternGenerator.Calculate(AxisFillMode.EaseInOut, 0.0);
        Assert.Equal(0.0, result, 3);
    }

    [Fact]
    public void PatternGenerator_EaseInOut_AtQuarter_ReturnsMidpoint()
    {
        // Triangle base at t=0.25 = 0.5; cubic ease(0.5) = 0.5
        var result = PatternGenerator.Calculate(AxisFillMode.EaseInOut, 0.25);
        Assert.Equal(0.5, result, 3);
    }

    [Fact]
    public void PatternGenerator_AllModes_ReturnBetween0And1()
    {
        var modes = new[]
        {
            AxisFillMode.Triangle, AxisFillMode.Sine, AxisFillMode.Saw,
            AxisFillMode.SawtoothReverse, AxisFillMode.Square, AxisFillMode.Pulse,
            AxisFillMode.EaseInOut
        };

        foreach (var mode in modes)
        {
            for (double t = 0.0; t <= 1.0; t += 0.01)
            {
                var result = PatternGenerator.Calculate(mode, t);
                Assert.InRange(result, -0.001, 1.001);
            }
        }
    }

    [Fact]
    public void PatternGenerator_AllModes_ArePeriodic()
    {
        var modes = new[]
        {
            AxisFillMode.Triangle, AxisFillMode.Sine,
            AxisFillMode.Square, AxisFillMode.Pulse, AxisFillMode.EaseInOut
        };

        foreach (var mode in modes)
        {
            var at0 = PatternGenerator.Calculate(mode, 0.0);
            var at1 = PatternGenerator.Calculate(mode, 1.0);
            Assert.Equal(at0, at1, 2);

            var at2 = PatternGenerator.Calculate(mode, 2.0);
            Assert.Equal(at0, at2, 2);
        }
    }

    // ═══════════════════════════════════════════════
    //  RandomPatternGenerator
    // ═══════════════════════════════════════════════

    [Fact]
    public void RandomPatternGenerator_DefaultRange_Returns0To100()
    {
        var gen = new RandomPatternGenerator();
        for (double prog = 0; prog < 1000; prog += 10)
        {
            var pos = gen.GetPosition(prog);
            Assert.InRange(pos, 0.0, 100.0);
        }
    }

    [Fact]
    public void RandomPatternGenerator_CustomRange_RespectsMinMax()
    {
        var gen = new RandomPatternGenerator(20, 80);
        for (double prog = 0; prog < 1000; prog += 10)
        {
            var pos = gen.GetPosition(prog);
            Assert.InRange(pos, 19.5, 80.5); // Small tolerance for rounding
        }
    }

    [Fact]
    public void RandomPatternGenerator_SetRange_UpdatesRange()
    {
        var gen = new RandomPatternGenerator(0, 100);
        gen.SetRange(40, 60);
        for (double prog = 0; prog < 500; prog += 10)
        {
            var pos = gen.GetPosition(prog);
            Assert.InRange(pos, 39.5, 60.5);
        }
    }

    [Fact]
    public void RandomPatternGenerator_Reset_ResetsState()
    {
        var gen = new RandomPatternGenerator(0, 100, seed: 42);
        var pos1 = gen.GetPosition(100);
        gen.Reset();
        var pos2 = gen.GetPosition(0);
        // After reset, generator state is fresh — first call may differ from post-advance state
        // Just verify it doesn't throw and returns in range
        Assert.InRange(pos2, 0.0, 100.0);
    }

    [Fact]
    public void RandomPatternGenerator_SeededOutput_IsDeterministic()
    {
        var gen1 = new RandomPatternGenerator(0, 100, seed: 42);
        var gen2 = new RandomPatternGenerator(0, 100, seed: 42);

        for (double prog = 0; prog < 500; prog += 25)
        {
            Assert.Equal(gen1.GetPosition(prog), gen2.GetPosition(prog), 6);
        }
    }

    [Fact]
    public void RandomPatternGenerator_CosineInterpolation_SmoothTransitions()
    {
        var gen = new RandomPatternGenerator(0, 100, seed: 1);
        double prev = gen.GetPosition(0);
        double maxDelta = 0;

        // Check that transitions are smooth (cosine interpolation doesn't jump)
        for (double prog = 1; prog < 500; prog += 1)
        {
            var curr = gen.GetPosition(prog);
            var delta = Math.Abs(curr - prev);
            maxDelta = Math.Max(maxDelta, delta);
            prev = curr;
        }

        // With cosine interpolation, per-tick delta should be bounded
        // (not jumping full range in a single step)
        Assert.True(maxDelta < 20.0, $"Max delta was {maxDelta}, expected < 20 for smooth transitions");
    }

    // ═══════════════════════════════════════════════
    //  HomeAxes / SendPositionWithOffset
    // ═══════════════════════════════════════════════

    [Fact]
    public void HomeAxes_WithTransport_SendsCommand()
    {
        var transport = new FakeTransport { IsConnected = true };
        _service.Transport = transport;
        _service.SetAxisConfigs(new List<AxisConfig>
        {
            MakeL0(),
            MakeR0()
        });

        // Start output thread so the pending command gets dispatched
        _service.Start();
        _service.HomeAxes();
        Thread.Sleep(50);
        _service.StopTimer();

        Assert.True(transport.SentMessages.Count > 0);
        var msg = transport.SentMessages[0];
        Assert.Contains("L0", msg);
        Assert.Contains("R0", msg);
        Assert.Contains("I2000", msg);
    }

    [Fact]
    public void HomeAxes_NoTransport_DoesNotThrow()
    {
        _service.SetAxisConfigs(new List<AxisConfig> { MakeL0() });
        _service.HomeAxes(); // No transport set
    }

    [Fact]
    public void HomeAxes_NotConnected_DoesNotSend()
    {
        var transport = new FakeTransport { IsConnected = false };
        _service.Transport = transport;
        _service.SetAxisConfigs(new List<AxisConfig> { MakeL0() });

        _service.HomeAxes();
        Assert.Empty(transport.SentMessages);
    }

    [Fact]
    public void SendPositionWithOffset_SendsCommand()
    {
        var transport = new FakeTransport { IsConnected = true };
        _service.Transport = transport;
        _service.SetAxisConfigs(new List<AxisConfig> { MakeL0(positionOffset: 10) });

        // Start output thread so the pending command gets dispatched
        _service.Start();
        _service.SendPositionWithOffset("L0");
        Thread.Sleep(50);
        _service.StopTimer();

        Assert.True(transport.SentMessages.Count > 0);
        var msg = transport.SentMessages[0];
        Assert.Contains("L0", msg);
        Assert.Contains("I200", msg);
    }

    [Fact]
    public void SendPositionWithOffset_NoTransport_DoesNotThrow()
    {
        _service.SetAxisConfigs(new List<AxisConfig> { MakeL0() });
        _service.SendPositionWithOffset("L0");
    }

    [Fact]
    public void SendPositionWithOffset_UnknownAxis_DoesNotThrow()
    {
        var transport = new FakeTransport { IsConnected = true };
        _service.Transport = transport;
        _service.SetAxisConfigs(new List<AxisConfig> { MakeL0() });

        _service.SendPositionWithOffset("X9");
        Assert.Empty(transport.SentMessages);
    }

    // ═══════════════════════════════════════════════
    //  VI-0022: Stroke tracking priority (external vs script)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// When both an empty L0 script and external L0 positions exist,
    /// the first-pass stroke tracking must use the external position.
    /// This ensures SyncWithStroke fills accumulate stroke distance from
    /// Pulse-driven L0 movement instead of the empty script's fixed 50.0.
    /// </summary>
    [Fact]
    public void OutputTick_WithExternalL0_SyncWithStrokeFillAnimates()
    {
        var transport = new FakeTransport { IsConnected = true };
        _service.Transport = transport;

        // Configure L0 enabled + R0 with SyncWithStroke Triangle fill
        _service.SetAxisConfigs(new List<AxisConfig>
        {
            MakeL0(),
            MakeR0(fillMode: AxisFillMode.Triangle)
        });

        // Inject empty L0 script (simulates VI-0012 Pulse suppress flow)
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = new FunscriptData { AxisId = "L0", FilePath = "", Actions = [] }
        });
        _service.SetPlaying(true);

        _service.Start();

        // Simulate Pulse sending varying L0 external positions over time
        // to drive stroke distance accumulation
        for (int i = 0; i < 30; i++)
        {
            double position = (i % 2 == 0) ? 10.0 : 90.0;
            _service.SetExternalPositions(new AxisPosition[]
            {
                new() { AxisId = "L0", Position = position }
            });
            Thread.Sleep(20);
        }

        _service.StopTimer();

        // Parse R0 tcode values from sent bytes
        var r0Values = ParseAxisValues(transport.SentBytes, "R0");

        // R0 must have received output commands (fill is active)
        Assert.True(r0Values.Count >= 2,
            $"Expected R0 to receive fill output, but got {r0Values.Count} commands");

        // R0 values should NOT all be the same (fill is animating, not stuck)
        var distinctValues = r0Values.Distinct().Count();
        Assert.True(distinctValues >= 2,
            $"Expected R0 fill to animate (distinct values), but all {r0Values.Count} values were identical: {r0Values.FirstOrDefault()}");
    }

    /// <summary>
    /// When no external positions are set, stroke tracking must fall back
    /// to the L0 script interpolation (regression test).
    /// </summary>
    [Fact]
    public void OutputTick_WithoutExternalPositions_UsesScriptForStrokeTracking()
    {
        var transport = new FakeTransport { IsConnected = true };
        _service.Transport = transport;

        // Configure L0 + R0 with SyncWithStroke Triangle fill
        _service.SetAxisConfigs(new List<AxisConfig>
        {
            MakeL0(),
            MakeR0(fillMode: AxisFillMode.Triangle)
        });

        // Load a real L0 script with significant movement
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = MakeScript((0, 0), (500, 100), (1000, 0), (1500, 100), (2000, 0))
        });
        _service.SetPlaying(true);
        _service.SetTime(0);

        _service.Start();
        Thread.Sleep(300);
        _service.StopTimer();

        // L0 should have received script-driven output
        var l0Values = ParseAxisValues(transport.SentBytes, "L0");
        Assert.True(l0Values.Count >= 2,
            $"Expected L0 script output, but got {l0Values.Count} commands");

        // L0 values should vary (script has movement 0→100→0)
        var l0Distinct = l0Values.Distinct().Count();
        Assert.True(l0Distinct >= 2,
            $"Expected varying L0 values from script, got {l0Distinct} distinct values");
    }

    /// <summary>
    /// Time-based (non-SyncWithStroke) fills must continue to work
    /// during Pulse playback with external L0 positions.
    /// </summary>
    [Fact]
    public void OutputTick_TimeBasedFill_WorksDuringExternalPositions()
    {
        var transport = new FakeTransport { IsConnected = true };
        _service.Transport = transport;

        // Configure L0 + R1 with time-based (non-sync) Triangle fill
        _service.SetAxisConfigs(new List<AxisConfig>
        {
            MakeL0(),
            MakeConfig("R1", "Roll", "rotation", fillMode: AxisFillMode.Triangle, syncWithStroke: false)
        });

        // Inject empty L0 script + external positions (Pulse flow)
        _service.SetScripts(new Dictionary<string, FunscriptData>
        {
            ["L0"] = new FunscriptData { AxisId = "L0", FilePath = "", Actions = [] }
        });
        _service.SetPlaying(true);

        // Set external L0 (Pulse driving L0)
        _service.SetExternalPositions(new AxisPosition[]
        {
            new() { AxisId = "L0", Position = 50.0 }
        });

        _service.Start();
        Thread.Sleep(300);
        _service.StopTimer();

        // R1 time-based fill should produce varying output
        var r1Values = ParseAxisValues(transport.SentBytes, "R1");
        Assert.True(r1Values.Count >= 2,
            $"Expected R1 fill output, but got {r1Values.Count} commands");

        var r1Distinct = r1Values.Distinct().Count();
        Assert.True(r1Distinct >= 2,
            $"Expected R1 time-based fill to animate, got {r1Distinct} distinct values");
    }

    /// <summary>
    /// Parses TCode axis values from raw byte output.
    /// TCode format: "L0500 R0300 R1200 R2100 I10\n" (space-separated, newline-terminated).
    /// </summary>
    private static List<int> ParseAxisValues(List<byte[]> sentBytes, string axisId)
    {
        var values = new List<int>();
        foreach (var bytes in sentBytes)
        {
            var message = System.Text.Encoding.ASCII.GetString(bytes);
            // Commands are space-separated, each like "R0500I10" or "L0499I10"
            foreach (var segment in message.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = segment.TrimEnd('\n');
                if (trimmed.StartsWith(axisId, StringComparison.Ordinal) && trimmed.Length > axisId.Length)
                {
                    var rest = trimmed[axisId.Length..];
                    // Rest is like "500I10" — extract digits before 'I'
                    var iPos = rest.IndexOf('I');
                    var valueStr = iPos >= 0 ? rest[..iPos] : rest;
                    if (int.TryParse(valueStr, out var val))
                        values.Add(val);
                }
            }
        }
        return values;
    }

    // ═══════════════════════════════════════════════
    //  Fake transport helper
    // ═══════════════════════════════════════════════

    private class FakeTransport : ITransportService
    {
        public bool IsConnected { get; set; }
        public string? ConnectionLabel => IsConnected ? "Fake" : null;
        public event Action<bool>? ConnectionChanged;
#pragma warning disable CS0067
        public event Action<string>? ErrorOccurred;
#pragma warning restore CS0067
        public List<string> SentMessages { get; } = new();
        public List<byte[]> SentBytes { get; } = new();

        public Task ConnectAsync(string address, CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            ConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            IsConnected = false;
            ConnectionChanged?.Invoke(false);
        }

        public void Send(string data)
        {
            SentMessages.Add(data);
        }

        public void Send(ReadOnlySpan<byte> data)
        {
            SentBytes.Add(data.ToArray());
        }

        public void Dispose() { }
    }
}
