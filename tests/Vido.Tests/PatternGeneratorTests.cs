using Vido.Core.Models.Osr2Plus;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PatternGenerator"/> — waveform calculation for fill modes.
/// Covers the six new pitch-only waveforms plus general boundary behavior.
/// </summary>
public class PatternGeneratorTests
{
    private const double Tolerance = 1e-10;

    // ===== Grind =====

    [Fact]
    public void Grind_AtStart_ReturnsZero()
    {
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.Grind, 0.0), Tolerance);
    }

    [Fact]
    public void Grind_AtMidRamp_ReturnsHalf()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.Grind, 0.25), Tolerance);
    }

    [Fact]
    public void Grind_AtRampEnd_ReturnsOne()
    {
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.Grind, 0.5), Tolerance);
    }

    [Fact]
    public void Grind_DuringHold_ReturnsOne()
    {
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.Grind, 0.7), Tolerance);
    }

    [Fact]
    public void Grind_AtHoldEnd_ReturnsOne()
    {
        // t = 0.85 is the boundary — still 1.0 (start of cosine drop, cos(0)=1 → (1+1)/2=1)
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.Grind, 0.85), Tolerance);
    }

    [Fact]
    public void Grind_DuringDrop_ReturnsMidValue()
    {
        // t = 0.925 → dt = (0.925-0.85)/0.15 = 0.5 → cos(0.5π) = 0 → (0+1)/2 = 0.5
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.Grind, 0.925), Tolerance);
    }

    [Fact]
    public void Grind_ApproachingEnd_ApproachesZero()
    {
        // Very close to t=1.0 (which wraps to 0.0), the cosine drop should be near 0
        double result = PatternGenerator.Calculate(AxisFillMode.Grind, 0.9999);
        Assert.True(result < 0.01);
    }

    [Fact]
    public void Grind_AlwaysInRange()
    {
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double result = PatternGenerator.Calculate(AxisFillMode.Grind, t);
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    // ===== ReverseGrind =====

    [Fact]
    public void ReverseGrind_AtStart_ReturnsOne()
    {
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.ReverseGrind, 0.0), Tolerance);
    }

    [Fact]
    public void ReverseGrind_AtMidRamp_ReturnsHalf()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.ReverseGrind, 0.25), Tolerance);
    }

    [Fact]
    public void ReverseGrind_AtRampEnd_ReturnsZero()
    {
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.ReverseGrind, 0.5), Tolerance);
    }

    [Fact]
    public void ReverseGrind_DuringHold_ReturnsZero()
    {
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.ReverseGrind, 0.7), Tolerance);
    }

    [Fact]
    public void ReverseGrind_DuringRise_ReturnsMidValue()
    {
        // t = 0.925 → dt = 0.5 → (-cos(0.5π)+1)/2 = (0+1)/2 = 0.5
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.ReverseGrind, 0.925), Tolerance);
    }

    [Fact]
    public void ReverseGrind_ApproachingEnd_ApproachesOne()
    {
        double result = PatternGenerator.Calculate(AxisFillMode.ReverseGrind, 0.9999);
        Assert.True(result > 0.99);
    }

    [Fact]
    public void ReverseGrind_AlwaysInRange()
    {
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double result = PatternGenerator.Calculate(AxisFillMode.ReverseGrind, t);
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    // ===== SharpGrind =====

    [Fact]
    public void SharpGrind_AtStart_ReturnsZero()
    {
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.SharpGrind, 0.0), Tolerance);
    }

    [Fact]
    public void SharpGrind_AtMidRamp_ReturnsHalf()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.SharpGrind, 0.25), Tolerance);
    }

    [Fact]
    public void SharpGrind_AtRampEnd_ReturnsOne()
    {
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.SharpGrind, 0.5), Tolerance);
    }

    [Fact]
    public void SharpGrind_AfterRamp_HoldsAtOne()
    {
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.SharpGrind, 0.75), Tolerance);
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.SharpGrind, 0.99), Tolerance);
    }

    [Fact]
    public void SharpGrind_AlwaysInRange()
    {
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double result = PatternGenerator.Calculate(AxisFillMode.SharpGrind, t);
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    // ===== SharpReverseGrind =====

    [Fact]
    public void SharpReverseGrind_AtStart_ReturnsOne()
    {
        Assert.Equal(1.0, PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, 0.0), Tolerance);
    }

    [Fact]
    public void SharpReverseGrind_AtMidRamp_ReturnsHalf()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, 0.25), Tolerance);
    }

    [Fact]
    public void SharpReverseGrind_AtRampEnd_ReturnsZero()
    {
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, 0.5), Tolerance);
    }

    [Fact]
    public void SharpReverseGrind_AfterRamp_HoldsAtZero()
    {
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, 0.75), Tolerance);
        Assert.Equal(0.0, PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, 0.99), Tolerance);
    }

    [Fact]
    public void SharpReverseGrind_AlwaysInRange()
    {
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double result = PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, t);
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    // ===== Rocker =====

    [Fact]
    public void Rocker_AtStart_IsNotZeroOrOne()
    {
        // 0.5*(1 - cos(π/4)) = 0.5*(1 - √2/2) ≈ 0.1464
        double expected = 0.5 * (1.0 - Math.Cos(Math.PI / 4.0));
        Assert.Equal(expected, PatternGenerator.Calculate(AxisFillMode.Rocker, 0.0), Tolerance);
    }

    [Fact]
    public void Rocker_AtQuarter_ReturnsOne()
    {
        // t=0.25 → cos(2π*0.25 + π/4) = cos(π/2 + π/4) = cos(3π/4) = -√2/2
        // 0.5*(1 - (-√2/2)) = 0.5*(1 + √2/2) ≈ 0.8536
        double expected = 0.5 * (1.0 - Math.Cos(3.0 * Math.PI / 4.0));
        Assert.Equal(expected, PatternGenerator.Calculate(AxisFillMode.Rocker, 0.25), Tolerance);
    }

    [Fact]
    public void Rocker_AlwaysInRange()
    {
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double result = PatternGenerator.Calculate(AxisFillMode.Rocker, t);
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    [Fact]
    public void Rocker_IsPeriodic()
    {
        // Value at t should equal value at t+1
        double v0 = PatternGenerator.Calculate(AxisFillMode.Rocker, 0.3);
        double v1 = PatternGenerator.Calculate(AxisFillMode.Rocker, 1.3);
        Assert.Equal(v0, v1, Tolerance);
    }

    // ===== ReverseRocker =====

    [Fact]
    public void ReverseRocker_AtStart_IsNotZeroOrOne()
    {
        // 0.5*(1 - cos(-π/4)) = 0.5*(1 - √2/2) ≈ 0.1464
        double expected = 0.5 * (1.0 - Math.Cos(-Math.PI / 4.0));
        Assert.Equal(expected, PatternGenerator.Calculate(AxisFillMode.ReverseRocker, 0.0), Tolerance);
    }

    [Fact]
    public void ReverseRocker_AtQuarter_ReturnsExpected()
    {
        // t=0.25 → cos(2π*0.25 - π/4) = cos(π/2 - π/4) = cos(π/4) = √2/2
        // 0.5*(1 - √2/2) ≈ 0.1464
        double expected = 0.5 * (1.0 - Math.Cos(Math.PI / 4.0));
        Assert.Equal(expected, PatternGenerator.Calculate(AxisFillMode.ReverseRocker, 0.25), Tolerance);
    }

    [Fact]
    public void ReverseRocker_AlwaysInRange()
    {
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double result = PatternGenerator.Calculate(AxisFillMode.ReverseRocker, t);
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    [Fact]
    public void ReverseRocker_IsPeriodic()
    {
        double v0 = PatternGenerator.Calculate(AxisFillMode.ReverseRocker, 0.3);
        double v1 = PatternGenerator.Calculate(AxisFillMode.ReverseRocker, 1.3);
        Assert.Equal(v0, v1, Tolerance);
    }

    // ===== Rocker vs ReverseRocker symmetry =====

    [Fact]
    public void Rocker_And_ReverseRocker_ArePhaseShifted()
    {
        // Rocker(t) uses +π/4, ReverseRocker(t) uses -π/4 — they are mirror offsets
        // Rocker at t should equal ReverseRocker at (1-t) if both are symmetric
        // Actually, Rocker(t) = ReverseRocker(-t) = ReverseRocker(1-t) due to cosine period
        for (int i = 0; i <= 100; i++)
        {
            double t = i / 100.0;
            double rocker = PatternGenerator.Calculate(AxisFillMode.Rocker, t);
            double reverseRocker = PatternGenerator.Calculate(AxisFillMode.ReverseRocker, 1.0 - t);
            Assert.Equal(rocker, reverseRocker, Tolerance);
        }
    }

    // ===== Grind vs ReverseGrind symmetry =====

    [Fact]
    public void Grind_And_ReverseGrind_AreMirrors()
    {
        // Grind(t) + ReverseGrind(t) should equal 1.0 for all t
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double grind = PatternGenerator.Calculate(AxisFillMode.Grind, t);
            double reverseGrind = PatternGenerator.Calculate(AxisFillMode.ReverseGrind, t);
            Assert.Equal(1.0, grind + reverseGrind, Tolerance);
        }
    }

    // ===== SharpGrind vs SharpReverseGrind symmetry =====

    [Fact]
    public void SharpGrind_And_SharpReverseGrind_AreMirrors()
    {
        // SharpGrind(t) + SharpReverseGrind(t) should equal 1.0 for all t
        for (int i = 0; i <= 1000; i++)
        {
            double t = i / 1000.0;
            double sharp = PatternGenerator.Calculate(AxisFillMode.SharpGrind, t);
            double sharpReverse = PatternGenerator.Calculate(AxisFillMode.SharpReverseGrind, t);
            Assert.Equal(1.0, sharp + sharpReverse, Tolerance);
        }
    }

    // ===== Negative time and wrapping =====

    [Fact]
    public void Calculate_NegativeTime_WrapsCorrectly()
    {
        double pos = PatternGenerator.Calculate(AxisFillMode.Grind, -0.25);
        double expected = PatternGenerator.Calculate(AxisFillMode.Grind, 0.75);
        Assert.Equal(expected, pos, Tolerance);
    }

    [Fact]
    public void Calculate_TimeGreaterThanOne_WrapsCorrectly()
    {
        double pos = PatternGenerator.Calculate(AxisFillMode.Grind, 1.25);
        double expected = PatternGenerator.Calculate(AxisFillMode.Grind, 0.25);
        Assert.Equal(expected, pos, Tolerance);
    }

    // ===== None and Random return default =====

    [Fact]
    public void Calculate_None_ReturnsDefault()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.None, 0.5), Tolerance);
    }

    [Fact]
    public void Calculate_Random_ReturnsDefault()
    {
        Assert.Equal(0.5, PatternGenerator.Calculate(AxisFillMode.Random, 0.5), Tolerance);
    }

    // ===== AxisConfig.AvailableFillModes =====

    [Fact]
    public void AvailableFillModes_L0_OnlyNone()
    {
        var config = new AxisConfig { Id = "L0", Name = "Stroke" };
        Assert.Single(config.AvailableFillModes);
        Assert.Equal(AxisFillMode.None, config.AvailableFillModes[0]);
    }

    [Fact]
    public void AvailableFillModes_R2_ContainsAll15Modes()
    {
        var config = new AxisConfig { Id = "R2", Name = "Pitch" };
        Assert.Equal(15, config.AvailableFillModes.Length);
        Assert.Contains(AxisFillMode.Grind, config.AvailableFillModes);
        Assert.Contains(AxisFillMode.ReverseGrind, config.AvailableFillModes);
        Assert.Contains(AxisFillMode.SharpGrind, config.AvailableFillModes);
        Assert.Contains(AxisFillMode.SharpReverseGrind, config.AvailableFillModes);
        Assert.Contains(AxisFillMode.Rocker, config.AvailableFillModes);
        Assert.Contains(AxisFillMode.ReverseRocker, config.AvailableFillModes);
    }

    [Fact]
    public void AvailableFillModes_R1_Has9Modes()
    {
        var config = new AxisConfig { Id = "R1", Name = "Roll" };
        Assert.Equal(9, config.AvailableFillModes.Length);
        Assert.DoesNotContain(AxisFillMode.Grind, config.AvailableFillModes);
        Assert.DoesNotContain(AxisFillMode.ReverseGrind, config.AvailableFillModes);
        Assert.DoesNotContain(AxisFillMode.SharpGrind, config.AvailableFillModes);
        Assert.DoesNotContain(AxisFillMode.SharpReverseGrind, config.AvailableFillModes);
        Assert.DoesNotContain(AxisFillMode.Rocker, config.AvailableFillModes);
        Assert.DoesNotContain(AxisFillMode.ReverseRocker, config.AvailableFillModes);
    }

    [Fact]
    public void AvailableFillModes_R0_Has9Modes()
    {
        var config = new AxisConfig { Id = "R0", Name = "Twist" };
        Assert.Equal(9, config.AvailableFillModes.Length);
        Assert.DoesNotContain(AxisFillMode.Grind, config.AvailableFillModes);
    }
}
