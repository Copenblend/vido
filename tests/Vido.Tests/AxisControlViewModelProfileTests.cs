using NSubstitute;
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
        Assert.Equal(3, profiles.Count);
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

    // ── SaveProfile ───────────────────────────────────────────────────

    [Fact]
    public void CompleteSaveProfile_CreatesNewProfile_WhenNameIsNew()
    {
        _vm.AxisCards[0].Min = 15;
        _vm.AxisCards[0].Max = 85;

        _vm.CompleteSaveProfile("My Profile");

        Assert.NotNull(_vm.SelectedProfile);
        Assert.Equal("My Profile", _vm.SelectedProfile!.Name);
        Assert.False(_vm.IsProfileModified);
        Assert.Contains(_vm.AvailableProfiles, p => p.Name == "My Profile");
    }

    [Fact]
    public void CompleteSaveProfile_UpdatesExistingProfile_WhenNameExists()
    {
        var axes = MakeProfile("Existing").Axes;
        _profileService.CreateProfile("Existing", axes);

        // Change an axis value
        _vm.AxisCards[0].Min = 25;
        _vm.AxisCards[0].Max = 75;

        _vm.CompleteSaveProfile("Existing");

        Assert.NotNull(_vm.SelectedProfile);
        Assert.Equal("Existing", _vm.SelectedProfile!.Name);
        Assert.False(_vm.IsProfileModified);
        // The updated profile should reflect the current axis values
        var updated = _profileService.FindByName("Existing");
        Assert.NotNull(updated);
        Assert.Equal(25, updated!.Axes["L0"].Min);
        Assert.Equal(75, updated.Axes["L0"].Max);
    }

    [Fact]
    public void CompleteSaveProfile_SetsSelectedProfileToNew()
    {
        _vm.CompleteSaveProfile("Brand New");

        Assert.NotNull(_vm.SelectedProfile);
        Assert.Equal("Brand New", _vm.SelectedProfile!.Name);
    }

    [Fact]
    public void SaveProfileCommand_RaisesRequestProfileName()
    {
        var raised = false;
        _vm.RequestProfileName += (_, _) => raised = true;

        _vm.SaveProfileCommand.Execute(null);

        Assert.True(raised);
    }

    // ── DeleteProfile ─────────────────────────────────────────────────

    [Fact]
    public void DeleteProfile_RemovesUserProfile()
    {
        var profile = _profileService.CreateProfile("ToDelete", MakeProfile("ToDelete").Axes);
        _vm.SelectedProfile = profile;

        _vm.DeleteProfileCommand.Execute(null);

        Assert.Null(_vm.SelectedProfile);
        Assert.DoesNotContain(_vm.AvailableProfiles, p => p.Name == "ToDelete");
    }

    [Fact]
    public void DeleteProfile_DoesNothing_WhenBuiltIn()
    {
        _vm.SelectedProfile = _profileService.FindByName("Default");

        _vm.DeleteProfileCommand.Execute(null);

        // Built-in profile should still be selected
        Assert.NotNull(_vm.SelectedProfile);
        Assert.Equal("Default", _vm.SelectedProfile!.Name);
    }

    [Fact]
    public void DeleteProfile_ClearsSelection()
    {
        var profile = _profileService.CreateProfile("ToDelete", MakeProfile("ToDelete").Axes);
        _vm.SelectedProfile = profile;

        _vm.DeleteProfileCommand.Execute(null);

        Assert.Null(_vm.SelectedProfile);
        Assert.False(_vm.IsProfileModified);
    }

    // ── RenameProfile ─────────────────────────────────────────────────

    [Fact]
    public void CompleteRenameProfile_ChangesNameInService()
    {
        var profile = _profileService.CreateProfile("OldName", MakeProfile("OldName").Axes);
        _vm.SelectedProfile = profile;

        _vm.CompleteRenameProfile("NewName");

        Assert.Null(_profileService.FindByName("OldName"));
        Assert.NotNull(_profileService.FindByName("NewName"));
    }

    [Fact]
    public void CompleteRenameProfile_UpdatesSelectedProfile()
    {
        var profile = _profileService.CreateProfile("OldName", MakeProfile("OldName").Axes);
        _vm.SelectedProfile = profile;

        _vm.CompleteRenameProfile("NewName");

        Assert.NotNull(_vm.SelectedProfile);
        Assert.Equal("NewName", _vm.SelectedProfile!.Name);
    }

    [Fact]
    public void RenameProfileCommand_RaisesRequestProfileRename()
    {
        var raised = false;
        _vm.RequestProfileRename += (_, _) => raised = true;

        _vm.RenameProfileCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void CompleteRenameProfile_DoesNothing_WhenBuiltIn()
    {
        _vm.SelectedProfile = _profileService.FindByName("Default");

        _vm.CompleteRenameProfile("Renamed");

        // Built-in should not be renamed
        Assert.NotNull(_profileService.FindByName("Default"));
        Assert.Null(_profileService.FindByName("Renamed"));
    }

    // ── SetProfileService preserves persisted settings ────────────────

    [Fact]
    public void SetProfileService_PreservesPersistedAxisSettings()
    {
        // Arrange: persisted settings differ from Default profile
        _settings.Osr2AxisSettings["R0"] = new AxisSettingsData
        {
            Min = 20, Max = 80, FillMode = "Sine", SyncWithStroke = true, FillSpeedHz = 1.5
        };

        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        // Act: attach profile service (auto-selects Default)
        var logService = NSubstitute.Substitute.For<Core.Logging.ILogService>();
        var profileService = new FillProfileService(logService);
        vm.SetProfileService(profileService);

        // Assert: persisted values survive, not overwritten by Default profile
        var r0 = vm.AxisCards[1]; // R0
        Assert.Equal(20, r0.Min);
        Assert.Equal(80, r0.Max);
        Assert.Equal(AxisFillMode.Sine, r0.FillMode);
        Assert.True(r0.SyncWithStroke);
        Assert.Equal(1.5, r0.FillSpeedHz, 3);
    }

    [Fact]
    public void SetProfileService_SetsIsProfileModified_WhenSettingsDiffer()
    {
        _settings.Osr2AxisSettings["R0"] = new AxisSettingsData
        {
            Min = 20, Max = 80, FillMode = "Sine", SyncWithStroke = true
        };

        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        var logService = NSubstitute.Substitute.For<Core.Logging.ILogService>();
        vm.SetProfileService(new FillProfileService(logService));

        Assert.True(vm.IsProfileModified);
    }

    [Fact]
    public void SetProfileService_NoModified_WhenSettingsMatchDefault()
    {
        // Default profile: Min=0, Max=100, FillMode=None, SyncWithStroke=false
        // AxisSettingsData defaults: Min=0, Max=100, Enabled=true, FillMode=None, SyncWithStroke=true
        // Need to set SyncWithStroke=false to match the Default profile
        foreach (var key in new[] { "L0", "R0", "R1", "R2" })
        {
            _settings.Osr2AxisSettings[key] = new AxisSettingsData
            {
                Min = 0, Max = 100, Enabled = true,
                FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0
            };
        }

        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        var logService = NSubstitute.Substitute.For<Core.Logging.ILogService>();
        vm.SetProfileService(new FillProfileService(logService));

        Assert.False(vm.IsProfileModified);
    }
}
