using NSubstitute;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="SettingsViewModel"/> — settings categories,
/// search filtering, and feature settings integration.
/// </summary>
public sealed class SettingsViewModelTests
{
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Sets up test dependencies.
    /// </summary>
    public SettingsViewModelTests()
    {
        var settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(settings);
    }

    private SettingsViewModel CreateViewModel() =>
        new(_settingsService);

    // — Category construction —

    /// <summary>
    /// Verifies that Constructor creates all expected categories.
    /// </summary>
    [Fact]
    public void Constructor_CreatesAllExpectedCategories()
    {
        var vm = CreateViewModel();

        Assert.Equal(6, vm.AllCategories.Count);
        Assert.Contains(vm.AllCategories, c => c.Name == "General");
        Assert.Contains(vm.AllCategories, c => c.Name == "Playback");
        Assert.Contains(vm.AllCategories, c => c.Name == "File Explorer");
        Assert.Contains(vm.AllCategories, c => c.Name == "Screenshot");
        Assert.Contains(vm.AllCategories, c => c.Name == "OSR2+");
        Assert.Contains(vm.AllCategories, c => c.Name == "Updates");
    }

    /// <summary>
    /// Verifies that Playback category has expected settings.
    /// </summary>
    [Fact]
    public void Constructor_PlaybackCategory_HasExpectedSettings()
    {
        var vm = CreateViewModel();
        var playback = vm.AllCategories.First(c => c.Name == "Playback");

        Assert.Equal(6, playback.Settings.Count);
        Assert.Contains(playback.Settings, s => s.Id == "playback.volume");
        Assert.Contains(playback.Settings, s => s.Id == "playback.speed");
        Assert.Contains(playback.Settings, s => s.Id == "playback.loop");
        Assert.Contains(playback.Settings, s => s.Id == "playback.fullscreenAutoHide");
        Assert.Contains(playback.Settings, s => s.Id == "playback.fullscreenShowVideoName");
        Assert.Contains(playback.Settings, s => s.Id == "playback.resumePlaybackPrompt");
    }

    /// <summary>
    /// Verifies that File Explorer category has expected settings.
    /// </summary>
    [Fact]
    public void Constructor_FileExplorerCategory_HasExpectedSettings()
    {
        var vm = CreateViewModel();
        var explorer = vm.AllCategories.First(c => c.Name == "File Explorer");

        Assert.Single(explorer.Settings);
        Assert.Equal("explorer.showHiddenFiles", explorer.Settings[0].Id);
    }

    /// <summary>
    /// Verifies that Screenshot category has expected settings.
    /// </summary>
    [Fact]
    public void Constructor_ScreenshotCategory_HasExpectedSettings()
    {
        var vm = CreateViewModel();
        var category = vm.AllCategories.First(c => c.Name == "Screenshot");

        Assert.Equal(2, category.Settings.Count);
        Assert.Contains(category.Settings, s => s.Id == "screenshot.enabled");
        Assert.Contains(category.Settings, s => s.Id == "screenshot.directory");
    }

    /// <summary>
    /// Verifies that OSR2+ category has expected settings.
    /// </summary>
    [Fact]
    public void Constructor_Osr2PlusCategory_HasExpectedSettings()
    {
        var vm = CreateViewModel();
        var category = vm.AllCategories.First(c => c.Name == "OSR2+");

        Assert.Equal(6, category.Settings.Count);
        Assert.Contains(category.Settings, s => s.Id == "osr2.connectionMode");
        Assert.Contains(category.Settings, s => s.Id == "osr2.udpPort");
        Assert.Contains(category.Settings, s => s.Id == "osr2.baudRate");
        Assert.Contains(category.Settings, s => s.Id == "osr2.outputRate");
        Assert.Contains(category.Settings, s => s.Id == "osr2.globalOffset");
        Assert.Contains(category.Settings, s => s.Id == "osr2.visualizerWindowDuration");
    }

    /// <summary>
    /// Verifies that Playlists category no longer exists after auto-save removal.
    /// </summary>
    [Fact]
    public void Constructor_PlaylistsCategory_DoesNotExist()
    {
        var vm = CreateViewModel();
        Assert.DoesNotContain(vm.AllCategories, c => c.Name == "Playlists");
    }

    // — Filtering —

    /// <summary>
    /// Verifies FilteredCategories shows all when search empty.
    /// </summary>
    [Fact]
    public void FilteredCategories_ShowsAll_WhenSearchEmpty()
    {
        var vm = CreateViewModel();

        Assert.Equal(vm.AllCategories.Count, vm.FilteredCategories.Count);
    }

    /// <summary>
    /// Verifies SearchText filters settings by title.
    /// </summary>
    [Fact]
    public void SearchText_FiltersSettingsByTitle()
    {
        var vm = CreateViewModel();

        vm.SearchText = "Volume";

        Assert.Single(vm.FilteredCategories);
        Assert.Equal("Playback", vm.FilteredCategories[0].Name);
        Assert.Contains(vm.FilteredCategories[0].Settings, s => s.Title.Contains("Volume"));
    }

    /// <summary>
    /// Verifies SearchText filters settings by description.
    /// </summary>
    [Fact]
    public void SearchText_FiltersSettingsByDescription()
    {
        var vm = CreateViewModel();

        vm.SearchText = "hidden";

        Assert.Single(vm.FilteredCategories);
        Assert.Equal("File Explorer", vm.FilteredCategories[0].Name);
    }

    /// <summary>
    /// Verifies SearchText matches category name shows entire category.
    /// </summary>
    [Fact]
    public void SearchText_MatchesCategoryName_ShowsEntireCategory()
    {
        var vm = CreateViewModel();

        vm.SearchText = "Playback";

        Assert.Contains(vm.FilteredCategories, c => c.Name == "Playback");
        var playback = vm.FilteredCategories.First(c => c.Name == "Playback");
        Assert.Equal(6, playback.Settings.Count);
    }

    /// <summary>
    /// Verifies SearchText no match shows no categories.
    /// </summary>
    [Fact]
    public void SearchText_NoMatch_ShowsNoCategories()
    {
        var vm = CreateViewModel();

        vm.SearchText = "xyznonexistent123";

        Assert.Empty(vm.FilteredCategories);
        Assert.True(vm.NoResults);
    }

    /// <summary>
    /// Verifies SearchText empty after filter shows all.
    /// </summary>
    [Fact]
    public void SearchText_EmptyAfterFilter_ShowsAll()
    {
        var vm = CreateViewModel();

        vm.SearchText = "Volume";
        Assert.Single(vm.FilteredCategories);

        vm.SearchText = "";
        Assert.Equal(vm.AllCategories.Count, vm.FilteredCategories.Count);
    }

    /// <summary>
    /// Verifies SearchText is case insensitive.
    /// </summary>
    [Fact]
    public void SearchText_IsCaseInsensitive()
    {
        var vm = CreateViewModel();

        vm.SearchText = "vOlUmE";

        Assert.NotEmpty(vm.FilteredCategories);
    }

    /// <summary>
    /// Verifies NoResults is false when search empty.
    /// </summary>
    [Fact]
    public void NoResults_FalseWhenSearchEmpty()
    {
        var vm = CreateViewModel();
        vm.SearchText = "";
        Assert.False(vm.NoResults);
    }

    // — Constructor validation —

    /// <summary>
    /// Verifies Constructor throws on null settings service.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullSettingsService()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SettingsViewModel(null!));
    }

    // — Screenshot visibility —

    /// <summary>
    /// Verifies screenshot directory is hidden when screenshot is disabled.
    /// </summary>
    [Fact]
    public void ScreenshotDirectory_HiddenWhenScreenshotDisabled()
    {
        var settings = new AppSettings { ScreenshotEnabled = false };
        var svc = Substitute.For<ISettingsService>();
        svc.Current.Returns(settings);
        var vm = new SettingsViewModel(svc);

        var screenshot = vm.AllCategories.First(c => c.Name == "Screenshot");
        var dirItem = screenshot.Settings.First(s => s.Id == "screenshot.directory");

        Assert.False(dirItem.IsSettingVisible);
    }

    /// <summary>
    /// Verifies screenshot directory is visible when screenshot is enabled.
    /// </summary>
    [Fact]
    public void ScreenshotDirectory_VisibleWhenScreenshotEnabled()
    {
        var settings = new AppSettings { ScreenshotEnabled = true };
        var svc = Substitute.For<ISettingsService>();
        svc.Current.Returns(settings);
        var vm = new SettingsViewModel(svc);

        var screenshot = vm.AllCategories.First(c => c.Name == "Screenshot");
        var dirItem = screenshot.Settings.First(s => s.Id == "screenshot.directory");

        Assert.True(dirItem.IsSettingVisible);
    }

    // — RefreshSetting —

    [Fact]
    public void RefreshSetting_UpdatesDisplayedValue()
    {
        var settings = new AppSettings { Osr2OutputRate = 100 };
        var svc = Substitute.For<ISettingsService>();
        svc.Current.Returns(settings);
        var store = new AppSettingsStore(svc);
        var vm = new SettingsViewModel(svc, store);

        var osr2 = vm.AllCategories.First(c => c.Name == "OSR2+");
        var rateItem = osr2.Settings.First(s => s.Id == "osr2.outputRate");
        Assert.Equal("100", rateItem.StringValue);

        // Mutate AppSettings directly (simulating sidebar change)
        settings.Osr2OutputRate = 150;
        vm.RefreshSetting("osr2.outputRate");

        Assert.Equal("150", rateItem.StringValue);
    }

    [Fact]
    public void RefreshSetting_UnknownKey_DoesNotThrow()
    {
        var vm = CreateViewModel();
        var ex = Record.Exception(() => vm.RefreshSetting("nonexistent.key"));
        Assert.Null(ex);
    }
}