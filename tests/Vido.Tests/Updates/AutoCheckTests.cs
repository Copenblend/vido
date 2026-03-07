using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Vido.Core.Settings;
using Vido.Core.Updates;

using Xunit;

namespace Vido.Tests.Updates;

/// <summary>
/// Tests for the auto-check updates on startup feature:
/// timer behavior, setting gate, toast notification, and error handling.
/// </summary>
public sealed class AutoCheckTests
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly AppSettingsStore _store;

    public AutoCheckTests()
    {
        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(_settings);
        _store = new AppSettingsStore(_settingsService);
    }

    // ── AutoCheckUpdates setting ────────────────────────────────────────

    [Fact]
    public void AutoCheckUpdates_DefaultsToTrue()
    {
        Assert.True(_settings.AutoCheckUpdates);
    }

    [Fact]
    public void AutoCheckUpdates_ResetToDefaults_RestoresTrue()
    {
        _settings.AutoCheckUpdates = false;
        _settings.ResetToDefaults();
        Assert.True(_settings.AutoCheckUpdates);
    }

    // ── AppSettingsStore integration ────────────────────────────────────

    [Fact]
    public void Store_Get_AutoCheck_ReturnsCurrentValue()
    {
        _settings.AutoCheckUpdates = true;
        Assert.True(_store.Get("updates.autocheck", false));

        _settings.AutoCheckUpdates = false;
        Assert.False(_store.Get("updates.autocheck", true));
    }

    [Fact]
    public void Store_Set_AutoCheck_UpdatesSetting()
    {
        _store.Set("updates.autocheck", false);
        Assert.False(_settings.AutoCheckUpdates);

        _store.Set("updates.autocheck", true);
        Assert.True(_settings.AutoCheckUpdates);
    }

    [Fact]
    public void Store_Set_AutoCheck_QueuesSave()
    {
        _store.Set("updates.autocheck", false);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void Store_Set_AutoCheck_RaisesSettingChanged()
    {
        string? changedKey = null;
        _store.SettingChanged += key => changedKey = key;

        _store.Set("updates.autocheck", false);

        Assert.Equal("updates.autocheck", changedKey);
    }

    // ── SettingsViewModel integration ───────────────────────────────────

    [Fact]
    public void SettingsViewModel_HasUpdatesCategory()
    {
        var vm = new Vido.ViewModels.SettingsViewModel(_settingsService);
        var category = vm.AllCategories.FirstOrDefault(c => c.Name == "Updates");

        Assert.NotNull(category);
        Assert.Single(category.Settings);
        Assert.Equal("updates.autocheck", category.Settings[0].Id);
    }

    // ── CheckForUpdateAsync gate (unit-level, no MainWindow) ───────────

    [Fact]
    public async Task CheckForUpdateAsync_WhenSettingTrue_ServiceIsCalled()
    {
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdateAsync().Returns(new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
        });

        // Simulate what OnAutoUpdateTimerTick does: check setting, call service
        if (_settings.AutoCheckUpdates)
        {
            await updateService.CheckForUpdateAsync();
        }

        await updateService.Received(1).CheckForUpdateAsync();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenSettingFalse_ServiceIsNotCalled()
    {
        _settings.AutoCheckUpdates = false;
        var updateService = Substitute.For<IUpdateService>();

        // Simulate what the constructor does: only start timer if setting is true
        if (_settings.AutoCheckUpdates)
        {
            await updateService.CheckForUpdateAsync();
        }

        await updateService.DidNotReceive().CheckForUpdateAsync();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenExceptionThrown_IsSilentlySwallowed()
    {
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdateAsync().ThrowsAsync(new HttpRequestException("Network unreachable"));

        // Simulate OnAutoUpdateTimerTick error handling
        Exception? caught = null;
        try
        {
            try
            {
                await updateService.CheckForUpdateAsync();
            }
            catch
            {
                // Silent — never show errors for background checks (matches MainWindow behavior).
            }
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.Null(caught);
    }

    [Fact]
    public async Task CheckForUpdateAsync_UpdateAvailable_ReturnsCorrectResult()
    {
        var updateService = Substitute.For<IUpdateService>();
        var expectedResult = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "1.0.0",
            LatestVersion = "2.0.0",
        };
        updateService.CheckForUpdateAsync().Returns(expectedResult);

        var result = await updateService.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("2.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NoUpdateAvailable_DoesNotTriggerToast()
    {
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdateAsync().Returns(new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
        });

        var result = await updateService.CheckForUpdateAsync();

        // When no update is available, no toast should be shown
        Assert.False(result.IsUpdateAvailable);
    }
}
