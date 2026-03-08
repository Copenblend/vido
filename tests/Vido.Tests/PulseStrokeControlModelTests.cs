using Vido.Core.Models.Pulse;
using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for vido-194: StrokePattern enum, PulseStrokeSettings record,
/// and AppSettings stroke control extensions.
/// </summary>
public sealed class PulseStrokeControlModelTests
{
    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ StrokePattern Enum Tests                                       ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that StrokePattern has exactly 5 values.
    /// </summary>
    [Fact]
    public void StrokePattern_HasFiveValues()
    {
        var values = Enum.GetValues<StrokePattern>();

        Assert.Equal(5, values.Length);
    }

    /// <summary>
    /// Verifies all expected StrokePattern values exist and have correct ordinal values.
    /// </summary>
    [Theory]
    [InlineData(StrokePattern.Classic, 0)]
    [InlineData(StrokePattern.DoubleTap, 1)]
    [InlineData(StrokePattern.TripleTap, 2)]
    [InlineData(StrokePattern.HoldTop, 3)]
    [InlineData(StrokePattern.HoldBottom, 4)]
    public void StrokePattern_HasExpectedOrdinalValue(StrokePattern pattern, int expectedOrdinal)
    {
        Assert.Equal(expectedOrdinal, (int)pattern);
    }

    /// <summary>
    /// Verifies that default StrokePattern is Classic.
    /// </summary>
    [Fact]
    public void StrokePattern_Default_IsClassic()
    {
        StrokePattern pattern = default;

        Assert.Equal(StrokePattern.Classic, pattern);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PulseStrokeSettings Tests                                      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that PulseStrokeSettings.Default has all zero/Classic values.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_Default_HasExpectedValues()
    {
        var settings = PulseStrokeSettings.Default;

        Assert.Equal(0.0, settings.AmplitudeOffset);
        Assert.Equal(0.0, settings.EasingBlend);
        Assert.Equal(StrokePattern.Classic, settings.Pattern);
        Assert.Equal(0.0, settings.Randomness);
    }

    /// <summary>
    /// Verifies that a new PulseStrokeSettings instance matches Default values.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_NewInstance_MatchesDefault()
    {
        var fresh = new PulseStrokeSettings();
        var def = PulseStrokeSettings.Default;

        Assert.Equal(def.AmplitudeOffset, fresh.AmplitudeOffset);
        Assert.Equal(def.EasingBlend, fresh.EasingBlend);
        Assert.Equal(def.Pattern, fresh.Pattern);
        Assert.Equal(def.Randomness, fresh.Randomness);
    }

    /// <summary>
    /// Verifies that PulseStrokeSettings.Default is a singleton instance.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_Default_IsSingleton()
    {
        Assert.Same(PulseStrokeSettings.Default, PulseStrokeSettings.Default);
    }

    /// <summary>
    /// Verifies that PulseStrokeSettings init properties can be set.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_InitProperties_CanBeSet()
    {
        var settings = new PulseStrokeSettings
        {
            AmplitudeOffset = 0.5,
            EasingBlend = -0.3,
            Pattern = StrokePattern.DoubleTap,
            Randomness = 0.75,
        };

        Assert.Equal(0.5, settings.AmplitudeOffset);
        Assert.Equal(-0.3, settings.EasingBlend);
        Assert.Equal(StrokePattern.DoubleTap, settings.Pattern);
        Assert.Equal(0.75, settings.Randomness);
    }

    /// <summary>
    /// Verifies record equality semantics — two instances with same values are equal.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_RecordEquality_SameValues_AreEqual()
    {
        var a = new PulseStrokeSettings { AmplitudeOffset = 0.5, EasingBlend = -1.0, Pattern = StrokePattern.HoldTop, Randomness = 0.2 };
        var b = new PulseStrokeSettings { AmplitudeOffset = 0.5, EasingBlend = -1.0, Pattern = StrokePattern.HoldTop, Randomness = 0.2 };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies record equality semantics — two instances with different values are not equal.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new PulseStrokeSettings { AmplitudeOffset = 0.5 };
        var b = new PulseStrokeSettings { AmplitudeOffset = -0.5 };

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies that PulseStrokeSettings with expression creates a modified copy.
    /// </summary>
    [Fact]
    public void PulseStrokeSettings_WithExpression_CreatesModifiedCopy()
    {
        var original = new PulseStrokeSettings { AmplitudeOffset = 0.5, Randomness = 0.3 };
        var modified = original with { Pattern = StrokePattern.TripleTap };

        Assert.Equal(0.5, modified.AmplitudeOffset);
        Assert.Equal(0.3, modified.Randomness);
        Assert.Equal(StrokePattern.TripleTap, modified.Pattern);
        Assert.Equal(StrokePattern.Classic, original.Pattern);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ AppSettings Stroke Control Extensions Tests                    ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that new AppSettings has correct stroke control defaults.
    /// </summary>
    [Fact]
    public void AppSettings_NewInstance_HasStrokeControlDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(0.0, settings.PulseAmplitudeOffset);
        Assert.Equal(0.0, settings.PulseEasingBlend);
        Assert.Equal("Classic", settings.PulseStrokePattern);
        Assert.Equal(0.0, settings.PulseRandomness);
    }

    /// <summary>
    /// Verifies that ResetToDefaults restores all stroke control properties.
    /// </summary>
    [Fact]
    public void AppSettings_ResetToDefaults_RestoresStrokeControlProperties()
    {
        var settings = new AppSettings
        {
            PulseAmplitudeOffset = 0.75,
            PulseEasingBlend = -0.5,
            PulseStrokePattern = "DoubleTap",
            PulseRandomness = 0.9,
        };

        settings.ResetToDefaults();

        Assert.Equal(0.0, settings.PulseAmplitudeOffset);
        Assert.Equal(0.0, settings.PulseEasingBlend);
        Assert.Equal("Classic", settings.PulseStrokePattern);
        Assert.Equal(0.0, settings.PulseRandomness);
    }

    /// <summary>
    /// Verifies that reset stroke control properties match a fresh instance.
    /// </summary>
    [Fact]
    public void AppSettings_ResetToDefaults_StrokeControls_MatchFreshInstance()
    {
        var mutated = new AppSettings
        {
            PulseAmplitudeOffset = 1.0,
            PulseEasingBlend = 1.0,
            PulseStrokePattern = "HoldBottom",
            PulseRandomness = 1.0,
        };

        mutated.ResetToDefaults();
        var fresh = new AppSettings();

        Assert.Equal(fresh.PulseAmplitudeOffset, mutated.PulseAmplitudeOffset);
        Assert.Equal(fresh.PulseEasingBlend, mutated.PulseEasingBlend);
        Assert.Equal(fresh.PulseStrokePattern, mutated.PulseStrokePattern);
        Assert.Equal(fresh.PulseRandomness, mutated.PulseRandomness);
    }

    /// <summary>
    /// Verifies that stroke control properties can be set to boundary values.
    /// </summary>
    [Theory]
    [InlineData(-1.0, -1.0, "Classic", 0.0)]
    [InlineData(1.0, 1.0, "HoldBottom", 1.0)]
    [InlineData(0.0, 0.0, "TripleTap", 0.5)]
    public void AppSettings_StrokeControls_AcceptBoundaryValues(
        double amplitude, double easing, string pattern, double randomness)
    {
        var settings = new AppSettings
        {
            PulseAmplitudeOffset = amplitude,
            PulseEasingBlend = easing,
            PulseStrokePattern = pattern,
            PulseRandomness = randomness,
        };

        Assert.Equal(amplitude, settings.PulseAmplitudeOffset);
        Assert.Equal(easing, settings.PulseEasingBlend);
        Assert.Equal(pattern, settings.PulseStrokePattern);
        Assert.Equal(randomness, settings.PulseRandomness);
    }
}
