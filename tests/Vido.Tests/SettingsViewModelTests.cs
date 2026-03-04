using NSubstitute;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="SettingsViewModel"/> â€” settings categories,
/// search filtering, and plugin settings integration.
/// </summary>
public sealed class SettingsViewModelTests
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettingsStore _appSettingsStore;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public SettingsViewModelTests()
    {
        var settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(settings);
        _appSettingsStore = new AppSettingsStore(_settingsService);
    }

    private SettingsViewModel CreateViewModel() =>
        new(_settingsService, _appSettingsStore);

    // â”€â”€ Category construction â”€â”€

    /// <summary>
    /// Verifies that Constructor creates app settings categories.
    /// </summary>
    [Fact]
    public void Constructor_CreatesAppSettingsCategories()
    {
        var vm = CreateViewModel();

        Assert.True(vm.AllCategories.Count >= 3, "Should have at least 3 app categories");
        Assert.Contains(vm.AllCategories, c => c.Name == "Playback");
        Assert.Contains(vm.AllCategories, c => c.Name == "File Explorer");
        Assert.Contains(vm.AllCategories, c => c.Name == "Plugins");
    }

    /// <summary>
    /// Verifies that Constructor playback category has expected settings.
    /// </summary>
    [Fact]
    public void Constructor_PlaybackCategory_HasExpectedSettings()
    {
        var vm = CreateViewModel();
        var playback = vm.AllCategories.First(c => c.Name == "Playback");

        Assert.Equal(3, playback.Settings.Count);
        Assert.Contains(playback.Settings, s => s.Id == "playback.volume");
        Assert.Contains(playback.Settings, s => s.Id == "playback.speed");
        Assert.Contains(playback.Settings, s => s.Id == "playback.loop");
    }

    /// <summary>
    /// Verifies that Constructor file explorer category has expected settings.
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
    /// Verifies that Constructor plugins category has expected settings.
    /// </summary>
    [Fact]
    public void Constructor_PluginsCategory_HasExpectedSettings()
    {
        var vm = CreateViewModel();
        var plugins = vm.AllCategories.First(c => c.Name == "Plugins");

        Assert.Single(plugins.Settings);
        Assert.Equal("plugins.registryUrls", plugins.Settings[0].Id);
    }

    /// <summary>
    /// Verifies that Constructor app categories are not marked as plugin.
    /// </summary>
    [Fact]
    public void Constructor_AppCategories_AreNotMarkedAsPlugin()
    {
        var vm = CreateViewModel();
        Assert.All(vm.AllCategories, c => Assert.False(c.IsPlugin));
    }

    // â”€â”€ Filtering â”€â”€

    /// <summary>
    /// Verifies that Filtered Categories shows all when search empty.
    /// </summary>
    [Fact]
    public void FilteredCategories_ShowsAll_WhenSearchEmpty()
    {
        var vm = CreateViewModel();

        Assert.Equal(vm.AllCategories.Count, vm.FilteredCategories.Count);
    }

    /// <summary>
    /// Verifies that Search Text filters settings by title.
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
    /// Verifies that Search Text filters settings by description.
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
    /// Verifies that Search Text matches category name shows entire category.
    /// </summary>
    [Fact]
    public void SearchText_MatchesCategoryName_ShowsEntireCategory()
    {
        var vm = CreateViewModel();

        vm.SearchText = "Playback";

        Assert.Contains(vm.FilteredCategories, c => c.Name == "Playback");
        var playback = vm.FilteredCategories.First(c => c.Name == "Playback");
        Assert.Equal(3, playback.Settings.Count); // All playback settings shown
    }

    /// <summary>
    /// Verifies that Search Text no match shows no categories.
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
    /// Verifies that Search Text empty after filter shows all.
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
    /// Verifies that Search Text is case insensitive.
    /// </summary>
    [Fact]
    public void SearchText_IsCaseInsensitive()
    {
        var vm = CreateViewModel();

        vm.SearchText = "vOlUmE";

        Assert.NotEmpty(vm.FilteredCategories);
    }

    /// <summary>
    /// Verifies that No Results false when search empty.
    /// </summary>
    [Fact]
    public void NoResults_FalseWhenSearchEmpty()
    {
        var vm = CreateViewModel();
        vm.SearchText = "";
        Assert.False(vm.NoResults);
    }

    // â”€â”€ Plugin settings integration â”€â”€


    // â”€â”€ Constructor validation â”€â”€

    /// <summary>
    /// Verifies that Constructor throws on null settings service.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullSettingsService()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SettingsViewModel(null!, _appSettingsStore));
    }

    /// <summary>
    /// Verifies that Constructor throws on null app settings store.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullAppSettingsStore()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SettingsViewModel(_settingsService, null!));
    }


    // â”€â”€ Helpers â”€â”€
}
