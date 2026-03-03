using Vido.Core.Models.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-004: OSR2+ Model types integrated into Vido.Core.
/// Covers <see cref="AxisConfig"/>, <see cref="AxisFillMode"/>, <see cref="BeatBarMode"/>,
/// <see cref="BeatDetectionMode"/>, <see cref="ConnectionMode"/>, <see cref="FunscriptAction"/>,
/// <see cref="FunscriptData"/>, and <see cref="VisualizationMode"/>.
/// </summary>
public sealed class Osr2PlusModelTests
{
    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ AxisConfig Tests                                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that CreateDefaults returns exactly 4 axes.
    /// </summary>
    [Fact]
    public void AxisConfig_CreateDefaults_ReturnsFourAxes()
    {
        var axes = AxisConfig.CreateDefaults();

        Assert.Equal(4, axes.Count);
    }

    /// <summary>
    /// Verifies that CreateDefaults L0 has correct identity.
    /// </summary>
    [Fact]
    public void AxisConfig_CreateDefaults_L0_HasCorrectIdentity()
    {
        var axes = AxisConfig.CreateDefaults();
        var l0 = axes[0];

        Assert.Equal("L0", l0.Id);
        Assert.Equal("Stroke", l0.Name);
        Assert.Equal("linear", l0.Type);
        Assert.Equal("#007ACC", l0.Color);
    }

    /// <summary>
    /// Verifies that CreateDefaults R0 has correct identity.
    /// </summary>
    [Fact]
    public void AxisConfig_CreateDefaults_R0_HasCorrectIdentity()
    {
        var axes = AxisConfig.CreateDefaults();
        var r0 = axes[1];

        Assert.Equal("R0", r0.Id);
        Assert.Equal("Twist", r0.Name);
        Assert.Equal("rotation", r0.Type);
        Assert.Equal("#B800CC", r0.Color);
    }

    /// <summary>
    /// Verifies that CreateDefaults R1 has correct identity.
    /// </summary>
    [Fact]
    public void AxisConfig_CreateDefaults_R1_HasCorrectIdentity()
    {
        var axes = AxisConfig.CreateDefaults();
        var r1 = axes[2];

        Assert.Equal("R1", r1.Id);
        Assert.Equal("Roll", r1.Name);
        Assert.Equal("rotation", r1.Type);
        Assert.Equal("#CC5200", r1.Color);
    }

    /// <summary>
    /// Verifies that CreateDefaults R2 has correct identity and reduced max.
    /// </summary>
    [Fact]
    public void AxisConfig_CreateDefaults_R2_HasCorrectIdentityAndReducedMax()
    {
        var axes = AxisConfig.CreateDefaults();
        var r2 = axes[3];

        Assert.Equal("R2", r2.Id);
        Assert.Equal("Pitch", r2.Name);
        Assert.Equal("rotation", r2.Type);
        Assert.Equal("#14CC00", r2.Color);
        Assert.Equal(75, r2.Max);
    }

    /// <summary>
    /// Verifies that default axis properties are correct.
    /// </summary>
    [Fact]
    public void AxisConfig_DefaultProperties_AreCorrect()
    {
        var axis = new AxisConfig();

        Assert.Equal(0, axis.Min);
        Assert.Equal(100, axis.Max);
        Assert.True(axis.Enabled);
        Assert.Equal(AxisFillMode.None, axis.FillMode);
        Assert.True(axis.SyncWithStroke);
        Assert.Equal(1.0, axis.FillSpeedHz);
        Assert.Equal(0.0, axis.PositionOffset);
    }

    /// <summary>
    /// Verifies that Min setter clamps to valid range.
    /// </summary>
    [Fact]
    public void AxisConfig_Min_ClampsToValidRange()
    {
        var axis = new AxisConfig { Max = 100 };

        axis.Min = -10;
        Assert.Equal(0, axis.Min);

        axis.Min = 50;
        Assert.Equal(50, axis.Min);
    }

    /// <summary>
    /// Verifies that Min setter rejects value equal to Max.
    /// </summary>
    [Fact]
    public void AxisConfig_Min_RejectsValueEqualToMax()
    {
        var axis = new AxisConfig { Max = 50 };

        axis.Min = 50; // Must be strictly less than Max
        Assert.Equal(0, axis.Min); // Should remain at default
    }

    /// <summary>
    /// Verifies that Max setter clamps to valid range.
    /// </summary>
    [Fact]
    public void AxisConfig_Max_ClampsToValidRange()
    {
        var axis = new AxisConfig { Min = 0 };

        axis.Max = 200;
        Assert.Equal(100, axis.Max);

        axis.Max = 50;
        Assert.Equal(50, axis.Max);
    }

    /// <summary>
    /// Verifies that Max setter rejects value equal to Min.
    /// </summary>
    [Fact]
    public void AxisConfig_Max_RejectsValueEqualToMin()
    {
        var axis = new AxisConfig();
        axis.Min = 30;

        axis.Max = 30; // Must be strictly greater than Min
        Assert.Equal(100, axis.Max); // Should remain at default
    }

    /// <summary>
    /// Verifies that FillSpeedHz clamps to valid range.
    /// </summary>
    [Fact]
    public void AxisConfig_FillSpeedHz_ClampsToValidRange()
    {
        var axis = new AxisConfig();

        axis.FillSpeedHz = 0.01;
        Assert.Equal(0.1, axis.FillSpeedHz);

        axis.FillSpeedHz = 5.0;
        Assert.Equal(3.0, axis.FillSpeedHz);

        axis.FillSpeedHz = 2.0;
        Assert.Equal(2.0, axis.FillSpeedHz);
    }

    /// <summary>
    /// Verifies that RangeLabel reflects current Min-Max.
    /// </summary>
    [Fact]
    public void AxisConfig_RangeLabel_ReflectsMinMax()
    {
        var axis = new AxisConfig { Min = 0, Max = 100 };

        Assert.Equal("0-100", axis.RangeLabel);

        axis.Min = 10;
        Assert.Equal("10-100", axis.RangeLabel);
    }

    /// <summary>
    /// Verifies that IsStroke is true only for L0.
    /// </summary>
    [Theory]
    [InlineData("L0", true)]
    [InlineData("R0", false)]
    [InlineData("R1", false)]
    [InlineData("R2", false)]
    public void AxisConfig_IsStroke_TrueOnlyForL0(string id, bool expected)
    {
        var axis = new AxisConfig { Id = id };
        Assert.Equal(expected, axis.IsStroke);
    }

    /// <summary>
    /// Verifies that IsPitch is true only for R2.
    /// </summary>
    [Theory]
    [InlineData("L0", false)]
    [InlineData("R0", false)]
    [InlineData("R1", false)]
    [InlineData("R2", true)]
    public void AxisConfig_IsPitch_TrueOnlyForR2(string id, bool expected)
    {
        var axis = new AxisConfig { Id = id };
        Assert.Equal(expected, axis.IsPitch);
    }

    /// <summary>
    /// Verifies that L0 only has None fill mode available.
    /// </summary>
    [Fact]
    public void AxisConfig_AvailableFillModes_L0_OnlyNone()
    {
        var axis = new AxisConfig { Id = "L0" };
        Assert.Single(axis.AvailableFillModes);
        Assert.Equal(AxisFillMode.None, axis.AvailableFillModes[0]);
    }

    /// <summary>
    /// Verifies that non-L0 axes have all 9 fill modes.
    /// </summary>
    [Theory]
    [InlineData("R0")]
    [InlineData("R1")]
    [InlineData("R2")]
    public void AxisConfig_AvailableFillModes_NonL0_HasAllModes(string id)
    {
        var axis = new AxisConfig { Id = id };
        Assert.Equal(9, axis.AvailableFillModes.Length);
    }

    /// <summary>
    /// Verifies that HasScript is false by default and true when ScriptFileName is set.
    /// </summary>
    [Fact]
    public void AxisConfig_HasScript_ReflectsScriptFileName()
    {
        var axis = new AxisConfig();

        Assert.False(axis.HasScript);
        Assert.Null(axis.ScriptFileName);

        axis.ScriptFileName = "test.funscript";
        Assert.True(axis.HasScript);

        axis.ScriptFileName = null;
        Assert.False(axis.HasScript);
    }

    /// <summary>
    /// Verifies that PropertyChanged fires for observable properties.
    /// </summary>
    [Fact]
    public void AxisConfig_PropertyChanged_FiresOnChange()
    {
        var axis = new AxisConfig();
        var changedProps = new List<string?>();
        axis.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        axis.Enabled = false;

        Assert.Contains(nameof(AxisConfig.Enabled), changedProps);
    }

    /// <summary>
    /// Verifies that PropertyChanged does not fire when value is unchanged.
    /// </summary>
    [Fact]
    public void AxisConfig_PropertyChanged_DoesNotFireWhenUnchanged()
    {
        var axis = new AxisConfig { Enabled = true };
        var changedProps = new List<string?>();
        axis.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        axis.Enabled = true; // Same value

        Assert.Empty(changedProps);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ AxisFillMode Tests                                             ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that AxisFillMode has exactly 9 members.
    /// </summary>
    [Fact]
    public void AxisFillMode_HasNineMembers()
    {
        Assert.Equal(9, Enum.GetValues<AxisFillMode>().Length);
    }

    /// <summary>
    /// Verifies that AxisFillMode contains all expected values.
    /// </summary>
    [Theory]
    [InlineData(AxisFillMode.None)]
    [InlineData(AxisFillMode.Random)]
    [InlineData(AxisFillMode.Triangle)]
    [InlineData(AxisFillMode.Sine)]
    [InlineData(AxisFillMode.Saw)]
    [InlineData(AxisFillMode.SawtoothReverse)]
    [InlineData(AxisFillMode.Square)]
    [InlineData(AxisFillMode.Pulse)]
    [InlineData(AxisFillMode.EaseInOut)]
    public void AxisFillMode_ContainsExpectedMember(AxisFillMode mode)
    {
        Assert.True(Enum.IsDefined(mode));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ BeatBarMode Tests                                              ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that BuiltInModes contains Off, OnPeak, OnValley in order.
    /// </summary>
    [Fact]
    public void BeatBarMode_BuiltInModes_ContainsThreeModes()
    {
        Assert.Equal(3, BeatBarMode.BuiltInModes.Count);
        Assert.Same(BeatBarMode.Off, BeatBarMode.BuiltInModes[0]);
        Assert.Same(BeatBarMode.OnPeak, BeatBarMode.BuiltInModes[1]);
        Assert.Same(BeatBarMode.OnValley, BeatBarMode.BuiltInModes[2]);
    }

    /// <summary>
    /// Verifies that built-in modes are not external.
    /// </summary>
    [Fact]
    public void BeatBarMode_BuiltInModes_AreNotExternal()
    {
        Assert.False(BeatBarMode.Off.IsExternal);
        Assert.False(BeatBarMode.OnPeak.IsExternal);
        Assert.False(BeatBarMode.OnValley.IsExternal);
    }

    /// <summary>
    /// Verifies that built-in modes have correct IDs.
    /// </summary>
    [Fact]
    public void BeatBarMode_BuiltInModes_HaveCorrectIds()
    {
        Assert.Equal("Off", BeatBarMode.Off.Id);
        Assert.Equal("OnPeak", BeatBarMode.OnPeak.Id);
        Assert.Equal("OnValley", BeatBarMode.OnValley.Id);
    }

    /// <summary>
    /// Verifies that CreateExternal creates an external mode.
    /// </summary>
    [Fact]
    public void BeatBarMode_CreateExternal_CreatesExternalMode()
    {
        var mode = BeatBarMode.CreateExternal("pulse.beat", "Pulse Beat");

        Assert.Equal("pulse.beat", mode.Id);
        Assert.Equal("Pulse Beat", mode.DisplayName);
        Assert.True(mode.IsExternal);
    }

    /// <summary>
    /// Verifies that equality works by ID.
    /// </summary>
    [Fact]
    public void BeatBarMode_Equality_ById()
    {
        var a = BeatBarMode.CreateExternal("test", "Test A");
        var b = BeatBarMode.CreateExternal("test", "Test B");
        var c = BeatBarMode.CreateExternal("other", "Other");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    /// <summary>
    /// Verifies that equality operators work correctly.
    /// </summary>
    [Fact]
    public void BeatBarMode_EqualityOperators_WorkCorrectly()
    {
        var off1 = BeatBarMode.Off;
        var off2 = BeatBarMode.Off;
        Assert.True(off1 == off2);
        Assert.False(off1 != off2);
        Assert.True(BeatBarMode.Off != BeatBarMode.OnPeak);
        Assert.False(BeatBarMode.Off == null);
    }

    /// <summary>
    /// Verifies that ToString returns the ID.
    /// </summary>
    [Fact]
    public void BeatBarMode_ToString_ReturnsId()
    {
        Assert.Equal("Off", BeatBarMode.Off.ToString());
        Assert.Equal("OnPeak", BeatBarMode.OnPeak.ToString());
    }

    /// <summary>
    /// Verifies that GetHashCode is consistent for same ID.
    /// </summary>
    [Fact]
    public void BeatBarMode_GetHashCode_ConsistentForSameId()
    {
        var a = BeatBarMode.CreateExternal("same-id", "A");
        var b = BeatBarMode.CreateExternal("same-id", "B");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// Verifies that Equals with null returns false.
    /// </summary>
    [Fact]
    public void BeatBarMode_Equals_NullReturnsFalse()
    {
        Assert.False(BeatBarMode.Off.Equals(null));
        Assert.False(BeatBarMode.Off.Equals((object?)null));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ BeatDetectionMode Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that BeatDetectionMode has exactly 2 members.
    /// </summary>
    [Fact]
    public void BeatDetectionMode_HasTwoMembers()
    {
        Assert.Equal(2, Enum.GetValues<BeatDetectionMode>().Length);
    }

    /// <summary>
    /// Verifies that BeatDetectionMode contains expected values.
    /// </summary>
    [Theory]
    [InlineData(BeatDetectionMode.OnPeak)]
    [InlineData(BeatDetectionMode.OnValley)]
    public void BeatDetectionMode_ContainsExpectedMember(BeatDetectionMode mode)
    {
        Assert.True(Enum.IsDefined(mode));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ ConnectionMode Tests                                           ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that ConnectionMode has exactly 2 members.
    /// </summary>
    [Fact]
    public void ConnectionMode_HasTwoMembers()
    {
        Assert.Equal(2, Enum.GetValues<ConnectionMode>().Length);
    }

    /// <summary>
    /// Verifies that ConnectionMode contains expected values.
    /// </summary>
    [Theory]
    [InlineData(ConnectionMode.UDP)]
    [InlineData(ConnectionMode.Serial)]
    public void ConnectionMode_ContainsExpectedMember(ConnectionMode mode)
    {
        Assert.True(Enum.IsDefined(mode));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ FunscriptAction Tests                                          ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that FunscriptAction record construction sets properties.
    /// </summary>
    [Fact]
    public void FunscriptAction_Construction_SetsProperties()
    {
        var action = new FunscriptAction(1000, 50);

        Assert.Equal(1000, action.AtMs);
        Assert.Equal(50, action.Pos);
    }

    /// <summary>
    /// Verifies that FunscriptAction record equality works by value.
    /// </summary>
    [Fact]
    public void FunscriptAction_Equality_ByValue()
    {
        var a = new FunscriptAction(1000, 50);
        var b = new FunscriptAction(1000, 50);
        var c = new FunscriptAction(2000, 50);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    /// <summary>
    /// Verifies that FunscriptAction deconstruction works.
    /// </summary>
    [Fact]
    public void FunscriptAction_Deconstruction_Works()
    {
        var action = new FunscriptAction(500, 75);
        var (atMs, pos) = action;

        Assert.Equal(500, atMs);
        Assert.Equal(75, pos);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ FunscriptData Tests                                            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that FunscriptData default properties are correct.
    /// </summary>
    [Fact]
    public void FunscriptData_DefaultProperties_AreCorrect()
    {
        var data = new FunscriptData();

        Assert.Equal("L0", data.AxisId);
        Assert.Equal("", data.FilePath);
        Assert.Empty(data.Actions);
    }

    /// <summary>
    /// Verifies that FunscriptData properties are mutable.
    /// </summary>
    [Fact]
    public void FunscriptData_Properties_AreMutable()
    {
        var data = new FunscriptData
        {
            AxisId = "R0",
            FilePath = "/path/to/script.funscript",
            Actions = [new FunscriptAction(0, 0), new FunscriptAction(1000, 100)]
        };

        Assert.Equal("R0", data.AxisId);
        Assert.Equal("/path/to/script.funscript", data.FilePath);
        Assert.Equal(2, data.Actions.Count);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ VisualizationMode Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that VisualizationMode has exactly 2 members.
    /// </summary>
    [Fact]
    public void VisualizationMode_HasTwoMembers()
    {
        Assert.Equal(2, Enum.GetValues<VisualizationMode>().Length);
    }

    /// <summary>
    /// Verifies that VisualizationMode contains expected values.
    /// </summary>
    [Theory]
    [InlineData(VisualizationMode.Graph)]
    [InlineData(VisualizationMode.Heatmap)]
    public void VisualizationMode_ContainsExpectedMember(VisualizationMode mode)
    {
        Assert.True(Enum.IsDefined(mode));
    }
}
