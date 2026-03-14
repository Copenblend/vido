using Vido.Core.Haptics;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for haptic contract types integrated from Vido.Haptics.
/// Verifies construction, default values, and mockability.
/// </summary>
public sealed class HapticTypesTests
{
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
}
