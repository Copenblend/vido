using System.ComponentModel;
using Vido.Core.Models.Osr2Plus;
using Vido.Services.Osr2Plus;
using Vido.ViewModels.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="AxisCardViewModel"/> reset functionality.
/// </summary>
public class AxisCardViewModelTests : IDisposable
{
    private readonly InterpolationService _interpolation = new();
    private readonly TCodeService _tcode;

    public AxisCardViewModelTests()
    {
        _tcode = new TCodeService(_interpolation);
    }

    public void Dispose()
    {
        _tcode.Dispose();
    }

    // ──────────────────────────────────────────────
    //  Helper methods
    // ──────────────────────────────────────────────

    private static AxisConfig MakeConfig(string id, string name, int min = 50, int max = 80,
        bool enabled = false, AxisFillMode fillMode = AxisFillMode.Sine,
        double fillSpeedHz = 2.5, bool syncWithStroke = false, double positionOffset = 10)
    {
        return new AxisConfig
        {
            Id = id, Name = name, Type = id == "L0" ? "linear" : "rotation",
            Min = min, Max = max, Enabled = enabled,
            FillMode = fillMode, FillSpeedHz = fillSpeedHz,
            SyncWithStroke = syncWithStroke, PositionOffset = positionOffset
        };
    }

    private AxisCardViewModel MakeVm(string id, string name) =>
        new(MakeConfig(id, name), _tcode);

    // ═══════════════════════════════════════════════
    //  ResetToDefaults
    // ═══════════════════════════════════════════════

    [Fact]
    public void ResetToDefaults_L0_SetsCorrectValues()
    {
        var vm = MakeVm("L0", "Stroke");
        vm.ResetCommand.Execute(null);

        Assert.Equal(0, vm.Min);
        Assert.Equal(100, vm.Max);
        Assert.True(vm.Enabled);
        Assert.Equal(AxisFillMode.None, vm.FillMode);
        Assert.Equal(1.0, vm.FillSpeedHz);
        Assert.True(vm.SyncWithStroke);
        Assert.Equal(0, vm.PositionOffset);
    }

    [Fact]
    public void ResetToDefaults_R0_SetsCorrectValues()
    {
        var vm = MakeVm("R0", "Twist");
        vm.ResetCommand.Execute(null);

        Assert.Equal(0, vm.Min);
        Assert.Equal(100, vm.Max);
        Assert.True(vm.Enabled);
        Assert.Equal(AxisFillMode.None, vm.FillMode);
        Assert.Equal(1.0, vm.FillSpeedHz);
        Assert.True(vm.SyncWithStroke);
        Assert.Equal(0, vm.PositionOffset);
    }

    [Fact]
    public void ResetToDefaults_R1_SetsCorrectValues()
    {
        var vm = MakeVm("R1", "Roll");
        vm.ResetCommand.Execute(null);

        Assert.Equal(0, vm.Min);
        Assert.Equal(100, vm.Max);
        Assert.True(vm.Enabled);
        Assert.Equal(AxisFillMode.None, vm.FillMode);
        Assert.Equal(1.0, vm.FillSpeedHz);
        Assert.True(vm.SyncWithStroke);
        Assert.Equal(0, vm.PositionOffset);
    }

    [Fact]
    public void ResetToDefaults_R2_SyncWithStrokeFalse()
    {
        var vm = MakeVm("R2", "Pitch");
        vm.ResetCommand.Execute(null);

        Assert.Equal(0, vm.Min);
        Assert.Equal(100, vm.Max);
        Assert.True(vm.Enabled);
        Assert.Equal(AxisFillMode.None, vm.FillMode);
        Assert.Equal(1.0, vm.FillSpeedHz);
        Assert.False(vm.SyncWithStroke);
        Assert.Equal(0, vm.PositionOffset);
    }

    [Fact]
    public void ResetToDefaults_FromModifiedState_RestoresDefaults()
    {
        var vm = MakeVm("R1", "Roll");

        // Verify non-default state
        Assert.Equal(50, vm.Min);
        Assert.Equal(80, vm.Max);
        Assert.False(vm.Enabled);
        Assert.Equal(AxisFillMode.Sine, vm.FillMode);
        Assert.Equal(2.5, vm.FillSpeedHz);
        Assert.False(vm.SyncWithStroke);
        Assert.Equal(10, vm.PositionOffset);

        vm.ResetCommand.Execute(null);

        Assert.Equal(0, vm.Min);
        Assert.Equal(100, vm.Max);
        Assert.True(vm.Enabled);
        Assert.Equal(AxisFillMode.None, vm.FillMode);
        Assert.Equal(1.0, vm.FillSpeedHz);
        Assert.True(vm.SyncWithStroke);
        Assert.Equal(0, vm.PositionOffset);
    }

    [Fact]
    public void ResetToDefaults_RaisesConfigChanged()
    {
        var vm = MakeVm("L0", "Stroke");
        var configChangedCount = 0;
        vm.ConfigChanged += () => configChangedCount++;

        vm.ResetCommand.Execute(null);

        Assert.True(configChangedCount > 0, "ConfigChanged should fire at least once during reset");
    }

    [Fact]
    public void ResetToDefaults_RaisesPropertyChanged()
    {
        var vm = MakeVm("R0", "Twist");
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
        };

        vm.ResetCommand.Execute(null);

        Assert.Contains(nameof(AxisCardViewModel.Min), changedProperties);
        Assert.Contains(nameof(AxisCardViewModel.Max), changedProperties);
        Assert.Contains(nameof(AxisCardViewModel.Enabled), changedProperties);
        Assert.Contains(nameof(AxisCardViewModel.FillMode), changedProperties);
        Assert.Contains(nameof(AxisCardViewModel.FillSpeedHz), changedProperties);
        Assert.Contains(nameof(AxisCardViewModel.SyncWithStroke), changedProperties);
        Assert.Contains(nameof(AxisCardViewModel.PositionOffset), changedProperties);
    }
}
