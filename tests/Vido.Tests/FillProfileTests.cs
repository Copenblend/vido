using Vido.Core.Models.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for VI-0014: <see cref="FillProfile"/> and <see cref="FillAxisSettings"/>
/// data model — deep cloning and equality comparison.
/// </summary>
public class FillProfileTests
{
    // ══════════════════════════════════════════════
    //  CloneAxes
    // ══════════════════════════════════════════════

    [Fact]
    public void CloneAxes_ReturnsDeepCopy_ModifyingCloneDoesNotAffectOriginal()
    {
        var profile = MakeProfile("Test", new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 10, Max = 90, FillMode = "R0", SyncWithStroke = true, FillSpeedHz = 2.0 },
            ["R1"] = new() { Enabled = false, Min = 0, Max = 50, FillMode = "R1", SyncWithStroke = false, FillSpeedHz = 1.5 },
        });

        var clone = profile.CloneAxes();

        // Mutate the clone
        clone["R0"].Min = 99;
        clone["R0"].FillMode = "Changed";
        clone["R1"].Enabled = true;

        // Original unchanged
        Assert.Equal(10, profile.Axes["R0"].Min);
        Assert.Equal("R0", profile.Axes["R0"].FillMode);
        Assert.False(profile.Axes["R1"].Enabled);

        // Clone has its own values
        Assert.Equal(99, clone["R0"].Min);
        Assert.Equal("Changed", clone["R0"].FillMode);
        Assert.True(clone["R1"].Enabled);
    }

    [Fact]
    public void CloneAxes_EmptyAxes_ReturnsEmptyDictionary()
    {
        var profile = MakeProfile("Empty", new Dictionary<string, FillAxisSettings>());

        var clone = profile.CloneAxes();

        Assert.Empty(clone);
    }

    // ══════════════════════════════════════════════
    //  MatchesAxes
    // ══════════════════════════════════════════════

    [Fact]
    public void MatchesAxes_SameValues_ReturnsTrue()
    {
        var axes = MakeDefaultAxes();
        var profile = MakeProfile("Test", axes);

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
            ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };

        Assert.True(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_DifferentMin_ReturnsFalse()
    {
        var profile = MakeProfile("Test", MakeDefaultAxes());

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 5, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
            ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };

        Assert.False(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_DifferentFillMode_ReturnsFalse()
    {
        var profile = MakeProfile("Test", MakeDefaultAxes());

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "R0", SyncWithStroke = false, FillSpeedHz = 1.0 },
            ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };

        Assert.False(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_MissingAxis_ReturnsFalse()
    {
        var profile = MakeProfile("Test", MakeDefaultAxes());

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };

        Assert.False(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_CaseInsensitiveFillMode_ReturnsTrue()
    {
        var axes = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { FillMode = "None" },
        };
        var profile = MakeProfile("Test", axes);

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { FillMode = "none" },
        };

        Assert.True(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_DifferentSyncWithStroke_ReturnsFalse()
    {
        var profile = MakeProfile("Test", MakeDefaultAxes());

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = true, FillSpeedHz = 1.0 },
            ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };

        Assert.False(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_DifferentFillSpeedHz_ReturnsFalse()
    {
        var profile = MakeProfile("Test", MakeDefaultAxes());

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 2.0 },
            ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };

        Assert.False(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_FillSpeedHzWithinTolerance_ReturnsTrue()
    {
        var axes = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { FillSpeedHz = 1.0 },
        };
        var profile = MakeProfile("Test", axes);

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { FillSpeedHz = 1.0005 },
        };

        Assert.True(profile.MatchesAxes(other));
    }

    [Fact]
    public void MatchesAxes_ExtraAxisInOther_ReturnsFalse()
    {
        var axes = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new(),
        };
        var profile = MakeProfile("Test", axes);

        var other = new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new(),
            ["R1"] = new(),
        };

        Assert.False(profile.MatchesAxes(other));
    }

    // ══════════════════════════════════════════════
    //  FillProfile properties
    // ══════════════════════════════════════════════

    [Fact]
    public void IsBuiltIn_DefaultsFalse()
    {
        var profile = new FillProfile { Name = "Custom" };
        Assert.False(profile.IsBuiltIn);
    }

    [Fact]
    public void Axes_DefaultsToEmptyDictionary()
    {
        var profile = new FillProfile { Name = "Custom" };
        Assert.Empty(profile.Axes);
    }

    // ══════════════════════════════════════════════
    //  FillAxisSettings defaults
    // ══════════════════════════════════════════════

    [Fact]
    public void FillAxisSettings_DefaultValues()
    {
        var settings = new FillAxisSettings();

        Assert.True(settings.Enabled);
        Assert.Equal(0, settings.Min);
        Assert.Equal(100, settings.Max);
        Assert.Equal("None", settings.FillMode);
        Assert.False(settings.SyncWithStroke);
        Assert.Equal(1.0, settings.FillSpeedHz);
    }

    // ══════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════

    private static FillProfile MakeProfile(string name, Dictionary<string, FillAxisSettings> axes, bool builtIn = false)
    {
        return new FillProfile { Name = name, IsBuiltIn = builtIn, Axes = axes };
    }

    private static Dictionary<string, FillAxisSettings> MakeDefaultAxes()
    {
        return new Dictionary<string, FillAxisSettings>
        {
            ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
            ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
        };
    }
}
