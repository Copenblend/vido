using NSubstitute;
using SkiaSharp;
using Vido.Core.Haptics;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for haptic contract types integrated from Vido.Haptics.
/// Verifies construction, default values, and mockability.
/// </summary>
public sealed class HapticTypesTests
{
    // ── AxisPosition ──

    [Fact]
    public void AxisPosition_Default_HasEmptyAxisId()
    {
        var position = default(AxisPosition);

        Assert.Equal(string.Empty, position.AxisId);
        Assert.Equal(0.0, position.Position);
    }

    [Fact]
    public void AxisPosition_Init_PreservesValues()
    {
        var position = new AxisPosition { AxisId = "L0", Position = 75.5 };

        Assert.Equal("L0", position.AxisId);
        Assert.Equal(75.5, position.Position);
    }

    [Fact]
    public void AxisPosition_Equality_WorksCorrectly()
    {
        var a = new AxisPosition { AxisId = "R0", Position = 50 };
        var b = new AxisPosition { AxisId = "R0", Position = 50 };
        var c = new AxisPosition { AxisId = "R1", Position = 50 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    // ── ExternalAxisPositionsEvent ──

    [Fact]
    public void ExternalAxisPositionsEvent_Default_HasEmptyPositions()
    {
        var evt = default(ExternalAxisPositionsEvent);

        Assert.True(evt.Positions.IsEmpty);
    }

    [Fact]
    public void ExternalAxisPositionsEvent_Init_PreservesPositions()
    {
        var positions = new[] { new AxisPosition { AxisId = "L0", Position = 50 } };
        var evt = new ExternalAxisPositionsEvent { Positions = positions };

        Assert.Equal(1, evt.Positions.Length);
        Assert.Equal("L0", evt.Positions.Span[0].AxisId);
    }

    // ── ExternalBeatEvent ──

    [Fact]
    public void ExternalBeatEvent_Default_HasEmptyValues()
    {
        var evt = default(ExternalBeatEvent);

        Assert.True(evt.BeatTimesMs.IsEmpty);
        Assert.Equal(string.Empty, evt.SourceId);
    }

    [Fact]
    public void ExternalBeatEvent_Init_PreservesValues()
    {
        var beats = new double[] { 100.0, 200.0, 300.0 };
        var evt = new ExternalBeatEvent { BeatTimesMs = beats, SourceId = "com.vido.pulse" };

        Assert.Equal(3, evt.BeatTimesMs.Length);
        Assert.Equal("com.vido.pulse", evt.SourceId);
    }

    // ── ExternalBeatSourceRegistration ──

    [Fact]
    public void ExternalBeatSourceRegistration_Default_HasNullSource()
    {
        var reg = default(ExternalBeatSourceRegistration);

        Assert.Null(reg.Source);
        Assert.False(reg.IsRegistering);
    }

    [Fact]
    public void ExternalBeatSourceRegistration_Init_PreservesValues()
    {
        var source = Substitute.For<IExternalBeatSource>();
        var reg = new ExternalBeatSourceRegistration { Source = source, IsRegistering = true };

        Assert.Same(source, reg.Source);
        Assert.True(reg.IsRegistering);
    }

    // ── HapticAxisConfigEvent ──

    [Fact]
    public void HapticAxisConfigEvent_Default_HasEmptyAxes()
    {
        var evt = default(HapticAxisConfigEvent);

        Assert.Empty(evt.Axes);
    }

    [Fact]
    public void HapticAxisConfigEvent_Init_PreservesAxes()
    {
        var axes = new[]
        {
            new HapticAxisSnapshot { Id = "L0", Min = 0, Max = 100, Enabled = true },
            new HapticAxisSnapshot { Id = "R0", Min = 10, Max = 90, Enabled = false },
        };
        var evt = new HapticAxisConfigEvent { Axes = axes };

        Assert.Equal(2, evt.Axes.Count);
        Assert.Equal("L0", evt.Axes[0].Id);
        Assert.True(evt.Axes[0].Enabled);
        Assert.False(evt.Axes[1].Enabled);
    }

    // ── HapticAxisSnapshot ──

    [Fact]
    public void HapticAxisSnapshot_Default_HasEmptyId()
    {
        var snapshot = default(HapticAxisSnapshot);

        Assert.Equal(string.Empty, snapshot.Id);
        Assert.Equal(0, snapshot.Min);
        Assert.Equal(0, snapshot.Max);
        Assert.False(snapshot.Enabled);
    }

    [Fact]
    public void HapticAxisSnapshot_Init_PreservesValues()
    {
        var snapshot = new HapticAxisSnapshot { Id = "R2", Min = 5, Max = 95, Enabled = true };

        Assert.Equal("R2", snapshot.Id);
        Assert.Equal(5, snapshot.Min);
        Assert.Equal(95, snapshot.Max);
        Assert.True(snapshot.Enabled);
    }

    // ── HapticScriptsChangedEvent ──

    [Fact]
    public void HapticScriptsChangedEvent_Default_HasEmptyDictionary()
    {
        var evt = default(HapticScriptsChangedEvent);

        Assert.False(evt.HasAnyScripts);
        Assert.Empty(evt.AxisScriptLoaded);
    }

    [Fact]
    public void HapticScriptsChangedEvent_Init_PreservesValues()
    {
        var scripts = new Dictionary<string, bool> { ["L0"] = true, ["R0"] = false };
        var evt = new HapticScriptsChangedEvent { HasAnyScripts = true, AxisScriptLoaded = scripts };

        Assert.True(evt.HasAnyScripts);
        Assert.True(evt.AxisScriptLoaded["L0"]);
        Assert.False(evt.AxisScriptLoaded["R0"]);
    }

    // ── HapticTransportStateEvent ──

    [Fact]
    public void HapticTransportStateEvent_Default_IsDisconnected()
    {
        var evt = default(HapticTransportStateEvent);

        Assert.False(evt.IsConnected);
        Assert.Null(evt.ConnectionLabel);
    }

    [Fact]
    public void HapticTransportStateEvent_Init_PreservesValues()
    {
        var evt = new HapticTransportStateEvent { IsConnected = true, ConnectionLabel = "UDP:7777" };

        Assert.True(evt.IsConnected);
        Assert.Equal("UDP:7777", evt.ConnectionLabel);
    }

    // ── SuppressFunscriptEvent ──

    [Fact]
    public void SuppressFunscriptEvent_Default_DoesNotSuppress()
    {
        var evt = default(SuppressFunscriptEvent);

        Assert.False(evt.SuppressFunscripts);
    }

    [Fact]
    public void SuppressFunscriptEvent_Init_PreservesValue()
    {
        var evt = new SuppressFunscriptEvent { SuppressFunscripts = true };

        Assert.True(evt.SuppressFunscripts);
    }

    // ── IExternalBeatSource mockability ──

    [Fact]
    public void IExternalBeatSource_CanBeMocked()
    {
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("test-source");
        source.DisplayName.Returns("Test");
        source.IsAvailable.Returns(true);
        source.HidesBuiltInModes.Returns(false);

        Assert.Equal("test-source", source.Id);
        Assert.Equal("Test", source.DisplayName);
        Assert.True(source.IsAvailable);
        Assert.False(source.HidesBuiltInModes);
    }

    [Fact]
    public void IExternalBeatSource_RenderBeat_AcceptsSKCanvas()
    {
        var source = Substitute.For<IExternalBeatSource>();
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);

        // Verify the method signature compiles with SKCanvas and invokes correctly
        source.RenderBeat(canvas, 10f, 20f, 30f, 0.5f);

        source.Received(1).RenderBeat(canvas, 10f, 20f, 30f, 0.5f);
    }

    [Fact]
    public void IExternalBeatSource_RenderIndicator_AcceptsSKCanvas()
    {
        var source = Substitute.For<IExternalBeatSource>();
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);

        source.RenderIndicator(canvas, 50f, 50f, 40f);

        source.Received(1).RenderIndicator(canvas, 50f, 50f, 40f);
    }
}
