using NSubstitute;
using Vido.Core.Haptics;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Settings;
using Vido.Services.Osr2Plus;
using Vido.ViewModels.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for fill profile integration in <see cref="AxisControlViewModel"/>.
/// </summary>
public class AxisControlViewModelProfileTests : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly InterpolationService _interpolation = new();
    private readonly TCodeService _tcode;
    private readonly FunscriptParser _parser = new();
    private readonly FunscriptMatcher _matcher = new();
    private readonly FillProfileService _profileService;
    private readonly AxisControlViewModel _vm;

    public AxisControlViewModelProfileTests()
    {
        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(_settings);
        _tcode = new TCodeService(_interpolation);

        var logService = Substitute.For<Core.Logging.ILogService>();
        _profileService = new FillProfileService(logService);

        _vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        _vm.SetProfileService(_profileService);
    }

    public void Dispose()
    {
        _tcode.Dispose();
    }

    private static FillProfile MakeProfile(string name, string fillMode = "Sine")
    {
        return new FillProfile
        {
            Name = name,
            Axes = new()
            {
                ["L0"] = new() { Enabled = true, Min = 10, Max = 90, FillMode = fillMode, SyncWithStroke = false, FillSpeedHz = 2.0 },
                ["R0"] = new() { Enabled = false, Min = 20, Max = 80, FillMode = fillMode, SyncWithStroke = true, FillSpeedHz = 1.5 },
                ["R1"] = new() { Enabled = true, Min = 30, Max = 70, FillMode = fillMode, SyncWithStroke = false, FillSpeedHz = 1.0 },
                ["R2"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = fillMode, SyncWithStroke = true, FillSpeedHz = 0.5 },
            },
        };
    }

    // ── ApplyProfile ──────────────────────────────────────────────────

    [Fact]
    public void ApplyProfile_SetsAllAxisCardProperties()
    {
        var profile = MakeProfile("Test");

        _vm.SelectedProfile = profile;

        var l0 = _vm.AxisCards[0];
        Assert.Equal(10, l0.Min);
        Assert.Equal(90, l0.Max);
        Assert.True(l0.Enabled);
        Assert.Equal(AxisFillMode.Sine, l0.FillMode);
        Assert.False(l0.SyncWithStroke);
        Assert.Equal(2.0, l0.FillSpeedHz);

        var r0 = _vm.AxisCards[1];
        Assert.False(r0.Enabled);
        Assert.Equal(20, r0.Min);
        Assert.Equal(80, r0.Max);
        Assert.True(r0.SyncWithStroke);
        Assert.Equal(1.5, r0.FillSpeedHz);
    }

    [Fact]
    public void ApplyProfile_FiresSingleConfigChanged()
    {
        var profile = MakeProfile("Test");
        var count = 0;
        _vm.AxisConfigChanged += () => count++;

        _vm.SelectedProfile = profile;

        Assert.Equal(1, count);
    }

    [Fact]
    public void ApplyProfile_SetsIsProfileModifiedFalse()
    {
        _vm.SelectedProfile = MakeProfile("Test");

        Assert.False(_vm.IsProfileModified);
    }

    // ── Modification Detection ────────────────────────────────────────

    [Fact]
    public void ManualChange_AfterProfileApply_SetsIsProfileModifiedTrue()
    {
        var profile = MakeProfile("Test");
        _vm.SelectedProfile = profile;
        Assert.False(_vm.IsProfileModified);
        Assert.NotNull(_vm.SelectedProfile);

        // Verify the profile was applied
        Assert.Equal(10, _vm.AxisCards[0].Min);

        // Manually change a card value (must be < Max=90 due to AxisConfig validation)
        _vm.AxisCards[0].Min = 50;
        Assert.Equal(50, _vm.AxisCards[0].Min);

        // The profile still has Min=10, card now has Min=50
        var captured = _vm.CaptureCurrentAxes();
        Assert.Equal(50, captured["L0"].Min);
        Assert.Equal(10, profile.Axes["L0"].Min);
        Assert.False(profile.MatchesAxes(captured));

        Assert.True(_vm.IsProfileModified);
    }

    [Fact]
    public void SelectedProfileNull_SetsIsProfileModifiedFalse()
    {
        _vm.SelectedProfile = MakeProfile("Test");
        _vm.AxisCards[0].Min = 50; // must be < Max=90 due to AxisConfig validation
        Assert.True(_vm.IsProfileModified);

        _vm.SelectedProfile = null;

        Assert.False(_vm.IsProfileModified);
    }

    // ── CaptureCurrentAxes ────────────────────────────────────────────

    [Fact]
    public void CaptureCurrentAxes_ReturnsCurrentValues()
    {
        _vm.AxisCards[0].Min = 15;
        _vm.AxisCards[0].Max = 85;
        _vm.AxisCards[0].FillMode = AxisFillMode.Sine;
        _vm.AxisCards[0].FillSpeedHz = 2.0;

        var axes = _vm.CaptureCurrentAxes();

        Assert.Equal(4, axes.Count);
        Assert.Equal(15, axes["L0"].Min);
        Assert.Equal(85, axes["L0"].Max);
        Assert.Equal("Sine", axes["L0"].FillMode);
        Assert.Equal(2.0, axes["L0"].FillSpeedHz);
    }

    // ── AvailableProfiles ─────────────────────────────────────────────

    [Fact]
    public void AvailableProfiles_ReflectsServiceProfiles()
    {
        var profiles = _vm.AvailableProfiles;

        // Should have built-in profiles
        Assert.Equal(5, profiles.Count);
        Assert.Equal("Default", profiles[0].Name);
    }

    [Fact]
    public void AvailableProfiles_WithoutService_ReturnsEmpty()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        Assert.Empty(vm.AvailableProfiles);
    }

    // ── SetProfileService ─────────────────────────────────────────────

    [Fact]
    public void SetProfileService_RaisesAvailableProfilesChanged()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AxisControlViewModel.AvailableProfiles))
                raised = true;
        };

        var logService = Substitute.For<Core.Logging.ILogService>();
        vm.SetProfileService(new FillProfileService(logService));

        Assert.True(raised);
    }

    // ── CanDelete/CanRename ───────────────────────────────────────────

    [Fact]
    public void CanDeleteSelectedProfile_BuiltIn_ReturnsFalse()
    {
        _vm.SelectedProfile = _profileService.FindByName("Default");

        Assert.False(_vm.CanDeleteSelectedProfile);
    }

    [Fact]
    public void CanDeleteSelectedProfile_UserProfile_ReturnsTrue()
    {
        var profile = _profileService.CreateProfile("Custom", MakeProfile("Custom").Axes);

        _vm.SelectedProfile = profile;

        Assert.True(_vm.CanDeleteSelectedProfile);
    }

    [Fact]
    public void CanDeleteSelectedProfile_Null_ReturnsFalse()
    {
        _vm.SelectedProfile = null;

        Assert.False(_vm.CanDeleteSelectedProfile);
    }
}
