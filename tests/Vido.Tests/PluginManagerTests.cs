using System.Text.Json;
using NSubstitute;
using Vido.Core.Logging;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Comprehensive unit tests for vi-019: Plugin Manager UI, Sample Plugin &amp;
/// End-to-End Validation. Covers PluginManagerViewModel, PluginItemViewModel,
/// SettingDisplayItem, PluginInstaller, and sample plugin manifest validation.
/// </summary>
public sealed class PluginManagerTests
{
    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Helpers                                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    private static PluginRegistryEntry MakeEntry(
        string id = "com.test.plugin",
        string displayName = "Test Plugin",
        string author = "Tester",
        string version = "1.0.0",
        string registryName = "TestRegistry",
        string registryUrl = "https://example.com/registry",
        bool isOfficial = false)
    {
        return new PluginRegistryEntry
        {
            Id = id,
            DisplayName = displayName,
            Description = $"Description of {displayName}",
            Author = author,
            Version = version,
            License = "MIT",
            Tags = ["test", "sample"],
            DownloadUrl = $"https://example.com/{id}.zip",
            Repository = "https://github.com/test/plugin",
            LastUpdated = "2025-01-15",
            RegistryName = registryName,
            RegistryUrl = registryUrl,
            IsOfficial = isOfficial
        };
    }

    private static PluginInfo MakePluginInfo(
        string id = "com.test.plugin",
        string displayName = "Test Plugin",
        string author = "Tester",
        PluginState state = PluginState.Active)
    {
        return new PluginInfo
        {
            Manifest = new PluginManifest
            {
                Id = id,
                DisplayName = displayName,
                Description = $"Description of {displayName}",
                Author = author,
                Version = "1.0.0",
                License = "MIT",
                Tags = ["test"],
            },
            Directory = $"C:\\plugins\\{id}",
            State = state
        };
    }

    private static (IPluginHost host, IPluginInstaller installer, ISettingsService settings, ILogService log) CreateMocks()
    {
        var host = Substitute.For<IPluginHost>();
        var installer = Substitute.For<IPluginInstaller>();
        var settings = Substitute.For<ISettingsService>();
        var log = Substitute.For<ILogService>();
        settings.Current.Returns(new AppSettings());
        host.Plugins.Returns(Array.Empty<PluginInfo>());
        return (host, installer, settings, log);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PluginItemViewModel Tests                                      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void FromRegistryEntry_SetsAllProperties()
    {
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        Assert.Equal("com.test.plugin", item.Id);
        Assert.Equal("Test Plugin", item.DisplayName);
        Assert.Equal("Tester", item.Publisher);
        Assert.Equal("1.0.0", item.Version);
        Assert.Equal("MIT", item.License);
        Assert.False(item.IsInstalled);
        Assert.True(item.IsEnabled); // default
        Assert.Same(entry, item.RegistryEntry);
    }

    [Fact]
    public void FromPluginInfo_SetsInstalledProperties()
    {
        var info = MakePluginInfo();
        var item = PluginItemViewModel.FromPluginInfo(info);

        Assert.Equal("com.test.plugin", item.Id);
        Assert.Equal("Test Plugin", item.DisplayName);
        Assert.True(item.IsInstalled);
        Assert.True(item.IsEnabled);
        Assert.Same(info, item.PluginInfo);
    }

    [Fact]
    public void FromPluginInfo_Disabled_SetsIsEnabledFalse()
    {
        var info = MakePluginInfo(state: PluginState.Disabled);
        var item = PluginItemViewModel.FromPluginInfo(info);

        Assert.True(item.IsInstalled);
        Assert.False(item.IsEnabled);
    }

    [Fact]
    public void FromPluginInfo_Error_SetsIsEnabledFalse()
    {
        var info = MakePluginInfo(state: PluginState.Error);
        var item = PluginItemViewModel.FromPluginInfo(info);

        Assert.False(item.IsEnabled);
    }

    [Fact]
    public void StatusText_Installed_Enabled()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        Assert.Equal("Enabled", item.StatusText);
    }

    [Fact]
    public void StatusText_Installed_Disabled()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo(state: PluginState.Disabled));
        Assert.Equal("Disabled", item.StatusText);
    }

    [Fact]
    public void StatusText_Available_Empty()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.Equal(string.Empty, item.StatusText);
    }

    [Fact]
    public void IsInstalled_ChangeNotifiesStatusText()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.IsInstalled = true;

        Assert.Contains(nameof(item.StatusText), changedProps);
    }

    [Fact]
    public void IsEnabled_ChangeNotifiesStatusText()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.IsEnabled = false;

        Assert.Contains(nameof(item.StatusText), changedProps);
    }

    [Fact]
    public void MatchesSearch_EmptyQuery_ReturnsTrue()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.True(item.MatchesSearch(""));
        Assert.True(item.MatchesSearch(null!));
        Assert.True(item.MatchesSearch("   "));
    }

    [Fact]
    public void MatchesSearch_MatchesDisplayName()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry(displayName: "Video Effects"));
        Assert.True(item.MatchesSearch("video"));
        Assert.True(item.MatchesSearch("EFFECTS"));
        Assert.False(item.MatchesSearch("audio"));
    }

    [Fact]
    public void MatchesSearch_MatchesTags()
    {
        var entry = MakeEntry();
        entry.Tags = ["video", "effects"];
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        Assert.True(item.MatchesSearch("effects"));
        Assert.False(item.MatchesSearch("audio"));
    }

    [Fact]
    public void FromRegistryEntry_OfficialRegistry_SetsIsOfficial()
    {
        var entry = MakeEntry(isOfficial: true);
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        Assert.True(item.IsOfficial);
    }

    [Fact]
    public void FromRegistryEntry_UnofficialRegistry_IsOfficialFalse()
    {
        var entry = MakeEntry(isOfficial: false);
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        Assert.False(item.IsOfficial);
    }

    [Fact]
    public void FromRegistryEntry_NullEntry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PluginItemViewModel.FromRegistryEntry(null!));
    }

    [Fact]
    public void FromPluginInfo_NullInfo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PluginItemViewModel.FromPluginInfo(null!));
    }

    [Fact]
    public void DisplayName_FallsBackToId_WhenBlank()
    {
        var entry = MakeEntry(displayName: "");
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        Assert.Equal(entry.Id, item.DisplayName);
    }

    [Fact]
    public void FromPluginInfo_WithRegistryEntry_MergesData()
    {
        var info = MakePluginInfo();
        var entry = MakeEntry(isOfficial: true);
        var item = PluginItemViewModel.FromPluginInfo(info, entry);

        Assert.True(item.IsOfficial);
        Assert.Same(entry, item.RegistryEntry);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PluginManagerViewModel Tests                                    ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public async Task LoadAsync_PopulatesInstalledFromHost()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo();
        host.Plugins.Returns(new[] { info });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(null));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Single(vm.InstalledPlugins);
        Assert.Equal("com.test.plugin", vm.InstalledPlugins[0].Id);
        Assert.Equal(1, vm.InstalledCount);
    }

    [Fact]
    public async Task LoadAsync_PopulatesAvailableFromRegistry()
    {
        var (host, installer, settings, log) = CreateMocks();
        var registry = new PluginRegistry
        {
            Name = "My Registry",
            Plugins = [MakeEntry()]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Single(vm.AvailablePlugins);
        Assert.Equal("com.test.plugin", vm.AvailablePlugins[0].Id);
        Assert.Equal(1, vm.AvailableCount);
        Assert.Empty(vm.InstalledPlugins);
    }

    [Fact]
    public async Task LoadAsync_MergesInstalledWithRegistry()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.alpha");
        host.Plugins.Returns(new[] { info });

        var registry = new PluginRegistry
        {
            Name = "Reg",
            Plugins = [MakeEntry(id: "com.test.alpha", isOfficial: true)]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Single(vm.InstalledPlugins);
        Assert.Empty(vm.AvailablePlugins);
        Assert.True(vm.InstalledPlugins[0].IsOfficial);
    }

    [Fact]
    public async Task LoadAsync_DeduplicatesAcrossRegistries()
    {
        var (host, installer, settings, log) = CreateMocks();
        var appSettings = new AppSettings();
        appSettings.PluginRegistryUrls.Add("https://extra.registry.com");
        settings.Current.Returns(appSettings);

        var pluginEntry = MakeEntry(id: "com.test.shared");

        var registry1 = new PluginRegistry { Name = "Registry1", Plugins = [pluginEntry] };
        var registry2 = new PluginRegistry { Name = "Registry2", Plugins = [MakeEntry(id: "com.test.shared")] };

        // First call returns registry1, second returns registry2
        installer.FetchRegistryAsync(appSettings.PluginRegistryUrls[0]).Returns(Task.FromResult<PluginRegistry?>(registry1));
        installer.FetchRegistryAsync("https://extra.registry.com").Returns(Task.FromResult<PluginRegistry?>(registry2));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        // Should appear only once (first registry wins)
        Assert.Single(vm.AvailablePlugins);
    }

    [Fact]
    public async Task LoadAsync_SetsRegistrySourceDropdown()
    {
        var (host, installer, settings, log) = CreateMocks();
        var registry = new PluginRegistry { Name = "My Registry", Plugins = [] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Contains("All", vm.RegistrySources);
        Assert.Contains("My Registry", vm.RegistrySources);
    }

    [Fact]
    public async Task LoadAsync_SetsIsLoadingDuringExecution()
    {
        var (host, installer, settings, log) = CreateMocks();
        var tcs = new TaskCompletionSource<PluginRegistry?>();
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(tcs.Task);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        var loadTask = vm.LoadAsync();

        Assert.True(vm.IsLoading);

        tcs.SetResult(null);
        await loadTask;

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_HandlesRegistryFetchError()
    {
        var (host, installer, settings, log) = CreateMocks();
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(null));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync(); // Should not throw

        Assert.Empty(vm.AvailablePlugins);
    }

    [Fact]
    public async Task LoadAsync_PreventsConcurrentExecution()
    {
        var (host, installer, settings, log) = CreateMocks();
        var callCount = 0;
        var tcs = new TaskCompletionSource<PluginRegistry?>();
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(_ =>
        {
            Interlocked.Increment(ref callCount);
            return tcs.Task;
        });

        var vm = new PluginManagerViewModel(host, installer, settings, log);

        // First call starts (IsLoading = true); second should bail
        var t1 = vm.LoadAsync();
        var t2 = vm.LoadAsync(); // Should bail because IsLoading is true

        tcs.SetResult(null); // Release the first call
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task SearchQuery_FiltersPlugins()
    {
        var (host, installer, settings, log) = CreateMocks();
        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins =
            [
                MakeEntry(id: "alpha", displayName: "Alpha Plugin"),
                MakeEntry(id: "beta", displayName: "Beta Plugin")
            ]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Equal(2, vm.AvailablePlugins.Count);

        vm.SearchQuery = "Alpha";
        Assert.Single(vm.AvailablePlugins);
        Assert.Equal("Alpha Plugin", vm.AvailablePlugins[0].DisplayName);
    }

    [Fact]
    public async Task SearchQuery_ClearingResetsFilter()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entryX = MakeEntry(id: "x", displayName: "Xylophone");
        entryX.Tags = ["unique-x"];
        var entryY = MakeEntry(id: "y", displayName: "Yonder");
        entryY.Tags = ["unique-y"];
        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins = [entryX, entryY]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        vm.SearchQuery = "Xylophone";
        Assert.Single(vm.AvailablePlugins);

        vm.SearchQuery = "";
        Assert.Equal(2, vm.AvailablePlugins.Count);
    }

    [Fact]
    public async Task RegistrySourceFilter_FiltersAvailable()
    {
        var (host, installer, settings, log) = CreateMocks();

        var appSettings = new AppSettings();
        appSettings.PluginRegistryUrls.Add("https://second.registry.com");
        settings.Current.Returns(appSettings);

        var r1 = new PluginRegistry { Name = "Registry1", Plugins = [MakeEntry(id: "r1-plugin", displayName: "R1 Plugin")] };
        var r2 = new PluginRegistry { Name = "Registry2", Plugins = [MakeEntry(id: "r2-plugin", displayName: "R2 Plugin")] };

        installer.FetchRegistryAsync(appSettings.PluginRegistryUrls[0]).Returns(Task.FromResult<PluginRegistry?>(r1));
        installer.FetchRegistryAsync("https://second.registry.com").Returns(Task.FromResult<PluginRegistry?>(r2));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Equal(2, vm.AvailablePlugins.Count);

        vm.SelectedRegistrySource = "Registry1";
        Assert.Single(vm.AvailablePlugins);
        Assert.Equal("R1 Plugin", vm.AvailablePlugins[0].DisplayName);
    }

    [Fact]
    public async Task RegistrySourceFilter_InstalledPluginsAlwaysShown()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "installed-plugin");
        host.Plugins.Returns(new[] { info });

        var registry = new PluginRegistry { Name = "SomeReg", Plugins = [] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        vm.SelectedRegistrySource = "SomeReg";
        // Installed plugins without matching registry should still show
        Assert.Single(vm.InstalledPlugins);
    }

    [Fact]
    public async Task InstallPluginAsync_TransitionsItemToInstalled()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        installer.InstallAsync(entry).Returns(Task.FromResult(true));
        host.GetPlugin(entry.Id).Returns((PluginInfo?)null);

        var vm = new PluginManagerViewModel(host, installer, settings, log);

        await vm.InstallPluginAsync(item);

        Assert.True(item.IsInstalled);
        Assert.True(item.IsEnabled);
        Assert.False(item.IsBusy);
    }

    [Fact]
    public async Task InstallPluginAsync_FailedInstall_NoStateChange()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        installer.InstallAsync(entry).Returns(Task.FromResult(false));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.InstallPluginAsync(item);

        Assert.False(item.IsInstalled);
    }

    [Fact]
    public async Task InstallPluginAsync_FiresOpenDetailRequested()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        installer.InstallAsync(entry).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        PluginItemViewModel? requestedItem = null;
        vm.OpenDetailRequested += i => requestedItem = i;

        await vm.InstallPluginAsync(item);

        Assert.Same(item, requestedItem);
    }

    [Fact]
    public async Task InstallPluginAsync_SkipsIfAlreadyInstalled()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        item.IsInstalled = true;

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.InstallPluginAsync(item);

        await installer.DidNotReceive().InstallAsync(Arg.Any<PluginRegistryEntry>());
    }

    [Fact]
    public async Task InstallPluginAsync_SkipsIfBusy()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        item.IsBusy = true;

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.InstallPluginAsync(item);

        await installer.DidNotReceive().InstallAsync(Arg.Any<PluginRegistryEntry>());
    }

    [Fact]
    public async Task UninstallPluginAsync_TransitionsItemToAvailable()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo();
        var item = PluginItemViewModel.FromPluginInfo(info);

        installer.UninstallAsync(item.Id).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.UninstallPluginAsync(item);

        Assert.False(item.IsInstalled);
        Assert.False(item.IsEnabled);
        Assert.Null(item.PluginInfo);
        Assert.False(item.IsBusy);
    }

    [Fact]
    public async Task UninstallPluginAsync_SkipsIfNotInstalled()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.UninstallPluginAsync(item);

        await installer.DidNotReceive().UninstallAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task UninstallPluginAsync_RemovesPluginFirst()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo();
        var item = PluginItemViewModel.FromPluginInfo(info);
        installer.UninstallAsync(item.Id).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.UninstallPluginAsync(item);

        host.Received(1).RemovePlugin(item.Id);
    }

    [Fact]
    public void ToggleEnabled_TogglesState()
    {
        var (host, installer, settings, log) = CreateMocks();
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        Assert.True(item.IsEnabled);

        var vm = new PluginManagerViewModel(host, installer, settings, log);

        vm.ToggleEnabled(item);
        Assert.False(item.IsEnabled);
        host.Received(1).SetEnabled(item.Id, false);

        vm.ToggleEnabled(item);
        Assert.True(item.IsEnabled);
        host.Received(1).SetEnabled(item.Id, true);
    }

    [Fact]
    public void ToggleEnabled_IgnoresNonInstalled()
    {
        var (host, installer, settings, log) = CreateMocks();
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        vm.ToggleEnabled(item); // Should be a no-op

        host.DidNotReceive().SetEnabled(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void OpenDetail_FiresOpenDetailRequested()
    {
        var (host, installer, settings, log) = CreateMocks();
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        PluginItemViewModel? received = null;
        vm.OpenDetailRequested += i => received = i;

        vm.OpenDetail(item);

        Assert.Same(item, received);
    }

    [Fact]
    public void OpenPluginSettings_FiresOpenSettingsRequested()
    {
        var (host, installer, settings, log) = CreateMocks();
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        PluginItemViewModel? received = null;
        vm.OpenSettingsRequested += i => received = i;

        vm.OpenPluginSettings(item);

        Assert.Same(item, received);
    }

    [Fact]
    public async Task CountBadges_UpdateOnFilterChanges()
    {
        var (host, installer, settings, log) = CreateMocks();
        host.Plugins.Returns(new[] { MakePluginInfo(id: "inst1"), MakePluginInfo(id: "inst2") });

        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins = [MakeEntry(id: "avail1")]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Equal(2, vm.InstalledCount);
        Assert.Equal(1, vm.AvailableCount);

        // Search that matches no plugins
        vm.SearchQuery = "zzzzz";
        Assert.Equal(0, vm.InstalledCount);
        Assert.Equal(0, vm.AvailableCount);
    }

    [Fact]
    public async Task LoadAsync_OfficialRegistryUrl_SetsOfficial()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry(id: "official-plugin");
        var registry = new PluginRegistry { Name = "Vido Official", Plugins = [entry] };

        installer.FetchRegistryAsync(AppSettings.OfficialRegistryUrl).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.True(vm.AvailablePlugins[0].IsOfficial);
    }

    [Fact]
    public async Task LoadAsync_NonOfficialUrl_SetsOfficialFalse()
    {
        var (host, installer, settings, log) = CreateMocks();
        var appSettings = new AppSettings();
        appSettings.PluginRegistryUrls.Clear();
        appSettings.PluginRegistryUrls.Add("https://custom.example.com/registry");
        settings.Current.Returns(appSettings);

        var entry = MakeEntry(id: "custom-plugin");
        var registry = new PluginRegistry { Name = "Custom", Plugins = [entry] };
        installer.FetchRegistryAsync("https://custom.example.com/registry").Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.False(vm.AvailablePlugins[0].IsOfficial);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SettingDisplayItem Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void BooleanSetting_InitialisesFromStore()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("flag", Arg.Any<bool>()).Returns(true);

        var def = new SettingContribution { Id = "flag", Type = "boolean", Default = true, Title = "Flag", Description = "A flag" };
        var item = new SettingDisplayItem(def, store);

        Assert.True(item.IsBoolean);
        Assert.False(item.IsString);
        Assert.False(item.IsNumber);
        Assert.False(item.IsEnum);
        Assert.Equal("True", item.SelectedBooleanValue);
    }

    [Fact]
    public void BooleanSetting_DefaultFalse()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("flag", Arg.Any<bool>()).Returns(false);

        var def = new SettingContribution { Id = "flag", Type = "boolean", Default = false };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("False", item.SelectedBooleanValue);
    }

    [Fact]
    public void BooleanSetting_AutoSavesOnChange()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("flag", Arg.Any<bool>()).Returns(true);

        var def = new SettingContribution { Id = "flag", Type = "boolean" };
        var item = new SettingDisplayItem(def, store);

        item.SelectedBooleanValue = "False";

        store.Received(1).Set("flag", false);
    }

    [Fact]
    public void BooleanSetting_DoesNotSaveDuringInitialization()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("flag", Arg.Any<bool>()).Returns(true);

        var def = new SettingContribution { Id = "flag", Type = "boolean" };
        _ = new SettingDisplayItem(def, store);

        // Set is never called during construction
        store.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void StringSetting_InitialisesFromStore()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("name", Arg.Any<string>()).Returns("Hello");

        var def = new SettingContribution { Id = "name", Type = "string", Default = "Default" };
        var item = new SettingDisplayItem(def, store);

        Assert.True(item.IsString);
        Assert.Equal("Hello", item.StringValue);
    }

    [Fact]
    public void StringSetting_AutoSavesOnChange()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("name", Arg.Any<string>()).Returns("Old");

        var def = new SettingContribution { Id = "name", Type = "string" };
        var item = new SettingDisplayItem(def, store);

        item.StringValue = "New";

        store.Received(1).Set("name", "New");
    }

    [Fact]
    public void NumberSetting_InitialisesFromStore()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("interval", Arg.Any<double>()).Returns(30.0);

        var def = new SettingContribution { Id = "interval", Type = "number", Default = 30.0 };
        var item = new SettingDisplayItem(def, store);

        Assert.True(item.IsNumber);
        Assert.Equal("30", item.StringValue);
    }

    [Fact]
    public void NumberSetting_AutoSavesOnChange()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("interval", Arg.Any<double>()).Returns(30.0);

        var def = new SettingContribution { Id = "interval", Type = "number" };
        var item = new SettingDisplayItem(def, store);

        item.StringValue = "60";

        store.Received(1).Set("interval", 60.0);
    }

    [Fact]
    public void NumberSetting_InvalidString_DoesNotSave()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("interval", Arg.Any<double>()).Returns(30.0);

        var def = new SettingContribution { Id = "interval", Type = "number" };
        var item = new SettingDisplayItem(def, store);

        item.StringValue = "abc";

        store.DidNotReceive().Set("interval", Arg.Any<double>());
    }

    [Fact]
    public void EnumSetting_InitialisesFromStore()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("level", Arg.Any<string>()).Returns("Warning");

        var def = new SettingContribution
        {
            Id = "level",
            Type = "enum",
            Default = "Info",
            EnumValues = ["Debug", "Info", "Warning", "Error"]
        };
        var item = new SettingDisplayItem(def, store);

        Assert.True(item.IsEnum);
        Assert.Equal("Warning", item.SelectedEnumValue);
        Assert.Equal(4, item.EnumValues.Count);
    }

    [Fact]
    public void EnumSetting_AutoSavesOnChange()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("level", Arg.Any<string>()).Returns("Info");

        var def = new SettingContribution
        {
            Id = "level",
            Type = "enum",
            Default = "Info",
            EnumValues = ["Debug", "Info", "Warning", "Error"]
        };
        var item = new SettingDisplayItem(def, store);

        item.SelectedEnumValue = "Debug";

        store.Received(1).Set("level", "Debug");
    }

    [Fact]
    public void EnumSetting_EmptyValue_DoesNotSave()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("level", Arg.Any<string>()).Returns("Info");

        var def = new SettingContribution
        {
            Id = "level",
            Type = "enum",
            EnumValues = ["Debug", "Info"]
        };
        var item = new SettingDisplayItem(def, store);

        item.SelectedEnumValue = "";

        store.DidNotReceive().Set("level", "");
    }

    [Fact]
    public void SectionProperty_ReturnsManifestSection()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("x", Arg.Any<string>()).Returns("v");
        var def = new SettingContribution { Id = "x", Type = "string", Section = "Display" };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("Display", item.Section);
    }

    [Fact]
    public void BooleanOptions_ContainsTrueAndFalse()
    {
        Assert.Equal(2, SettingDisplayItem.BooleanOptions.Count);
        Assert.Contains("True", SettingDisplayItem.BooleanOptions);
        Assert.Contains("False", SettingDisplayItem.BooleanOptions);
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        Assert.Throws<ArgumentNullException>(() => new SettingDisplayItem(null!, store));
    }

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        var def = new SettingContribution { Id = "x", Type = "string" };
        Assert.Throws<ArgumentNullException>(() => new SettingDisplayItem(def, null!));
    }

    [Fact]
    public void ConvertDefault_HandlesJsonElement_Boolean()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        var json = JsonSerializer.Deserialize<JsonElement>("true");
        store.Get("b", Arg.Any<bool>()).Returns(true);

        var def = new SettingContribution { Id = "b", Type = "boolean", Default = json };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("True", item.SelectedBooleanValue);
    }

    [Fact]
    public void ConvertDefault_HandlesJsonElement_Number()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        var json = JsonSerializer.Deserialize<JsonElement>("42");
        store.Get("n", Arg.Any<double>()).Returns(42.0);

        var def = new SettingContribution { Id = "n", Type = "number", Default = json };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("42", item.StringValue);
    }

    [Fact]
    public void ConvertDefault_HandlesJsonElement_String()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        var json = JsonSerializer.Deserialize<JsonElement>("\"hello\"");
        store.Get("s", Arg.Any<string>()).Returns("hello");

        var def = new SettingContribution { Id = "s", Type = "string", Default = json };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("hello", item.StringValue);
    }

    [Fact]
    public void TitleAndDescription_ReturnFromDefinition()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("x", Arg.Any<string>()).Returns("v");
        var def = new SettingContribution { Id = "x", Type = "string", Title = "My Title", Description = "My Desc" };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("My Title", item.Title);
        Assert.Equal("My Desc", item.Description);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PluginInstaller Tests (file-system integration)                 ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public async Task InstallAsync_NullEntry_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        await Assert.ThrowsAsync<ArgumentNullException>(() => installer.InstallAsync(null!));
    }

    [Fact]
    public async Task InstallAsync_EmptyId_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        var entry = new PluginRegistryEntry { Id = "", DownloadUrl = "http://example.com/p.zip" };
        await Assert.ThrowsAsync<ArgumentException>(() => installer.InstallAsync(entry));
    }

    [Fact]
    public async Task InstallAsync_EmptyDownloadUrl_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        var entry = new PluginRegistryEntry { Id = "test", DownloadUrl = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => installer.InstallAsync(entry));
    }

    [Fact]
    public async Task UninstallAsync_EmptyId_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        await Assert.ThrowsAsync<ArgumentException>(() => installer.UninstallAsync(""));
    }

    [Fact]
    public async Task UninstallAsync_NonexistentDir_ReturnsTrue()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), tempDir);

        var result = await installer.UninstallAsync("nonexistent-plugin");

        Assert.True(result);
    }

    [Fact]
    public async Task UninstallAsync_ExistingDir_DeletesIt()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"vido-test-{Guid.NewGuid()}");
        var pluginDir = Path.Combine(tempDir, "test-plugin");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "plugin.json"), "{}");

        try
        {
            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), tempDir);
            var result = await installer.UninstallAsync("test-plugin");

            Assert.True(result);
            Assert.False(Directory.Exists(pluginDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CleanupPendingUninstalls_RemovesMarkedDirectories()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"vido-test-{Guid.NewGuid()}");
        var markedDir = Path.Combine(tempDir, "marked-plugin");
        var cleanDir = Path.Combine(tempDir, "clean-plugin");

        Directory.CreateDirectory(markedDir);
        Directory.CreateDirectory(cleanDir);
        File.WriteAllText(Path.Combine(markedDir, ".uninstall"), DateTime.UtcNow.ToString("O"));
        File.WriteAllText(Path.Combine(cleanDir, "plugin.json"), "{}");

        try
        {
            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), tempDir);
            installer.CleanupPendingUninstalls();

            Assert.False(Directory.Exists(markedDir));
            Assert.True(Directory.Exists(cleanDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CleanupPendingUninstalls_NonexistentBaseDir_NoOp()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), @"C:\nonexistent\path\" + Guid.NewGuid());
        installer.CleanupPendingUninstalls(); // Should not throw
    }

    [Fact]
    public async Task FetchRegistryAsync_EmptyUrl_ReturnsNull()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());

        var result = await installer.FetchRegistryAsync("");
        Assert.Null(result);

        result = await installer.FetchRegistryAsync(null!);
        Assert.Null(result);
    }

    [Fact]
    public async Task FetchRegistryAsync_FileUrl_ParsesJson()
    {
        var log = Substitute.For<ILogService>();
        var tempFile = Path.GetTempFileName();
        var registryJson = """
        {
            "name": "Test Registry",
            "plugins": [
                {
                    "id": "com.test.plugin",
                    "displayName": "Test",
                    "description": "Desc",
                    "author": "Author",
                    "version": "1.0.0",
                    "license": "MIT",
                    "tags": ["test"],
                    "downloadUrl": "https://example.com/test.zip"
                }
            ]
        }
        """;
        File.WriteAllText(tempFile, registryJson);

        try
        {
            var fileUrl = new Uri(tempFile).ToString();
            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
            var registry = await installer.FetchRegistryAsync(fileUrl);

            Assert.NotNull(registry);
            Assert.Equal("Test Registry", registry!.Name);
            Assert.Single(registry.Plugins);
            Assert.Equal("com.test.plugin", registry.Plugins[0].Id);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FetchRegistryAsync_InvalidJson_ReturnsNull()
    {
        var log = Substitute.For<ILogService>();
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "NOT JSON");

        try
        {
            var fileUrl = new Uri(tempFile).ToString();
            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
            var registry = await installer.FetchRegistryAsync(fileUrl);

            Assert.Null(registry);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InstallAsync_FileUrl_ExtractsAndValidates()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"vido-install-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        // Create a valid plugin zip
        var zipPath = Path.Combine(tempDir, "test-plugin.zip");
        var stageDir = Path.Combine(tempDir, "stage");
        Directory.CreateDirectory(stageDir);
        File.WriteAllText(Path.Combine(stageDir, "plugin.json"), """{"id":"test-plugin","name":"test"}""");
        File.WriteAllText(Path.Combine(stageDir, "test.dll"), "fake-dll");
        System.IO.Compression.ZipFile.CreateFromDirectory(stageDir, zipPath);

        var pluginsDir = Path.Combine(tempDir, "plugins");

        try
        {
            var fileUrl = new Uri(zipPath).ToString();
            var entry = new PluginRegistryEntry
            {
                Id = "test-plugin",
                DownloadUrl = fileUrl,
                DisplayName = "Test"
            };

            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), pluginsDir);
            var result = await installer.InstallAsync(entry);

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(pluginsDir, "test-plugin", "plugin.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_ZipWithRootFolder_MovesContentsUp()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"vido-root-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        // Create zip with single root folder wrapping plugin files
        var zipPath = Path.Combine(tempDir, "wrapped.zip");
        var stageDir = Path.Combine(tempDir, "stage", "inner-folder");
        Directory.CreateDirectory(stageDir);
        File.WriteAllText(Path.Combine(stageDir, "plugin.json"), """{"id":"wrapped","name":"wrapped"}""");
        System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(tempDir, "stage"), zipPath);

        var pluginsDir = Path.Combine(tempDir, "plugins");

        try
        {
            var entry = new PluginRegistryEntry
            {
                Id = "wrapped",
                DownloadUrl = new Uri(zipPath).ToString(),
                DisplayName = "Wrapped"
            };

            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), pluginsDir);
            var result = await installer.InstallAsync(entry);

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(pluginsDir, "wrapped", "plugin.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_ZipWithoutManifest_ReturnsFalse()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"vido-nomanifest-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        // Create zip with no plugin.json
        var zipPath = Path.Combine(tempDir, "bad.zip");
        var stageDir = Path.Combine(tempDir, "stage");
        Directory.CreateDirectory(stageDir);
        File.WriteAllText(Path.Combine(stageDir, "readme.txt"), "no manifest here");
        System.IO.Compression.ZipFile.CreateFromDirectory(stageDir, zipPath);

        var pluginsDir = Path.Combine(tempDir, "plugins");

        try
        {
            var entry = new PluginRegistryEntry
            {
                Id = "bad-plugin",
                DownloadUrl = new Uri(zipPath).ToString(),
                DisplayName = "Bad"
            };

            var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), pluginsDir);
            var result = await installer.InstallAsync(entry);

            Assert.False(result);
            Assert.False(Directory.Exists(Path.Combine(pluginsDir, "bad-plugin")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PluginRegistryEntry Tests                                      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void PluginRegistryEntry_DefaultValues()
    {
        var entry = new PluginRegistryEntry();

        Assert.Equal(string.Empty, entry.Id);
        Assert.Equal(string.Empty, entry.DisplayName);
        Assert.Equal(string.Empty, entry.Description);
        Assert.Equal(string.Empty, entry.Author);
        Assert.Equal(string.Empty, entry.Version);
        Assert.Equal(string.Empty, entry.License);
        Assert.Empty(entry.Tags);
        Assert.Equal(string.Empty, entry.DownloadUrl);
        Assert.Null(entry.IconUrl);
        Assert.Null(entry.Repository);
        Assert.Null(entry.LastUpdated);
        Assert.Equal(string.Empty, entry.RegistryUrl);
        Assert.Equal(string.Empty, entry.RegistryName);
        Assert.False(entry.IsOfficial);
    }

    [Fact]
    public void PluginRegistry_DefaultValues()
    {
        var registry = new PluginRegistry();

        Assert.Equal(string.Empty, registry.Name);
        Assert.Empty(registry.Plugins);
    }

    [Fact]
    public void PluginRegistryEntry_JsonRoundTrip()
    {
        var entry = MakeEntry();
        var json = JsonSerializer.Serialize(entry);
        var roundTripped = JsonSerializer.Deserialize<PluginRegistryEntry>(json)!;

        Assert.Equal(entry.Id, roundTripped.Id);
        Assert.Equal(entry.DisplayName, roundTripped.DisplayName);
        Assert.Equal(entry.Version, roundTripped.Version);
        // JsonIgnore properties should not be serialized
        Assert.Equal(string.Empty, roundTripped.RegistryUrl);
        Assert.Equal(string.Empty, roundTripped.RegistryName);
        Assert.False(roundTripped.IsOfficial);
    }

    [Fact]
    public void PluginRegistry_JsonRoundTrip()
    {
        var registry = new PluginRegistry
        {
            Name = "Test Registry",
            Plugins = [MakeEntry(id: "p1"), MakeEntry(id: "p2")]
        };
        var json = JsonSerializer.Serialize(registry);
        var roundTripped = JsonSerializer.Deserialize<PluginRegistry>(json)!;

        Assert.Equal("Test Registry", roundTripped.Name);
        Assert.Equal(2, roundTripped.Plugins.Count);
        Assert.Equal("p1", roundTripped.Plugins[0].Id);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Sample Plugin Manifest Validation                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void SamplePluginManifest_HasAllRequiredFields()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        Assert.Equal("com.vido.sample-plugin", manifest.Id);
        Assert.Equal("vido-sample-plugin", manifest.Name);
        Assert.Equal("Sample Plugin", manifest.DisplayName);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Description));
        Assert.Equal("Vido Team", manifest.Author);
        Assert.Equal("MIT", manifest.License);
        Assert.Equal("Vido.SamplePlugin.dll", manifest.EntryPoint);
        Assert.Equal("Vido.SamplePlugin.SamplePlugin", manifest.PluginClass);
        Assert.False(string.IsNullOrWhiteSpace(manifest.MinVidoVersion));
        Assert.NotEmpty(manifest.Tags);
    }

    [Fact]
    public void SamplePluginManifest_DeclaresAllContributionTypes()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;
        var c = manifest.Contributes;

        Assert.NotEmpty(c.Sidebar);
        Assert.NotEmpty(c.BottomPanel);
        Assert.NotEmpty(c.RightPanel);
        Assert.NotEmpty(c.StatusBar);
        Assert.NotEmpty(c.ToolbarButtons);
        Assert.NotEmpty(c.ContextMenu);
        Assert.NotEmpty(c.FileHandlers);
        Assert.NotEmpty(c.FileIcons);
    }

    [Fact]
    public void SamplePluginManifest_DeclaresAllSettingTypes()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;
        var settings = manifest.Contributes.Settings;

        Assert.True(settings.Count >= 4, "Should have at least 4 settings");

        var types = settings.Select(s => s.Type.ToLowerInvariant()).ToHashSet();
        Assert.Contains("boolean", types);
        Assert.Contains("string", types);
        Assert.Contains("number", types);
        Assert.Contains("enum", types);
    }

    [Fact]
    public void SamplePluginManifest_HasAtLeastTwoSections()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        var sections = manifest.Contributes.Settings
            .Where(s => !string.IsNullOrWhiteSpace(s.Section))
            .Select(s => s.Section)
            .Distinct()
            .ToList();

        Assert.True(sections.Count >= 2, $"Expected at least 2 sections, got {sections.Count}: {string.Join(", ", sections)}");
    }

    [Fact]
    public void SamplePluginManifest_HasForceOverrideSetting()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        Assert.True(
            manifest.Contributes.Settings.Any(s => s.ForceOverride),
            "At least one setting must have forceOverride: true");
    }

    [Fact]
    public void SamplePluginManifest_EnumSetting_HasEnumValues()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        var enumSettings = manifest.Contributes.Settings.Where(s => s.Type.Equals("enum", StringComparison.OrdinalIgnoreCase));
        foreach (var setting in enumSettings)
        {
            Assert.NotEmpty(setting.EnumValues);
        }
    }

    [Fact]
    public void SamplePluginManifest_SidebarContribution_HasIdAndTitle()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        foreach (var sidebar in manifest.Contributes.Sidebar)
        {
            Assert.False(string.IsNullOrWhiteSpace(sidebar.Id));
            Assert.False(string.IsNullOrWhiteSpace(sidebar.Title));
        }
    }

    [Fact]
    public void SamplePluginManifest_FileHandler_HasSampleExtension()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        var extensions = manifest.Contributes.FileHandlers.SelectMany(h => h.Extensions).ToList();
        Assert.Contains(".sample", extensions);
    }

    [Fact]
    public void SamplePluginManifest_FileIcons_HasSampleExtension()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        Assert.True(manifest.Contributes.FileIcons.ContainsKey(".sample"));
    }

    [Fact]
    public void SamplePlugin_ReadmeExists()
    {
        Assert.True(File.Exists(Path.Combine(GetSamplePluginPath(), "README.md")));
    }

    [Fact]
    public void SamplePlugin_ChangelogExists()
    {
        Assert.True(File.Exists(Path.Combine(GetSamplePluginPath(), "CHANGELOG.md")));
    }

    [Fact]
    public void SamplePlugin_RegistryJsonExists()
    {
        Assert.True(File.Exists(Path.Combine(GetSamplePluginPath(), "registry.json")));
    }

    [Fact]
    public void SamplePlugin_RegistryJson_ContainsSamplePlugin()
    {
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "registry.json"));
        var registry = JsonSerializer.Deserialize<PluginRegistry>(json)!;

        Assert.NotNull(registry);
        Assert.NotEmpty(registry.Plugins);
        Assert.Contains(registry.Plugins, p => p.Id == "com.vido.sample-plugin");
    }

    private static string GetSamplePluginPath()
    {
        // Navigate from the test project to the sample plugin directory
        // Test project: c:\source\vido\tests\Vido.Tests
        // Sample plugin: c:\source\vido-sample-plugin
        var testDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var samplePluginPath = Path.Combine(Path.GetDirectoryName(solutionRoot)!, "vido-sample-plugin");

        if (!Directory.Exists(samplePluginPath))
            throw new DirectoryNotFoundException($"Sample plugin not found at: {samplePluginPath}");

        return samplePluginPath;
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PluginPaths Tests                                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void PluginPaths_DefaultPluginDirectory_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(PluginPaths.DefaultPluginDirectory));
    }

    [Fact]
    public void PluginPaths_DefaultPluginDirectory_EndsWithPlugins()
    {
        Assert.EndsWith("plugins", PluginPaths.DefaultPluginDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Integration: PluginManagerViewModel end-to-end flows            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public async Task FullFlow_Install_ThenUninstall()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry(id: "flow-plugin", displayName: "Flow Plugin");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(entry).Returns(Task.FromResult(true));
        installer.UninstallAsync("flow-plugin").Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        Assert.Single(vm.AvailablePlugins);
        Assert.Empty(vm.InstalledPlugins);

        // Install
        var item = vm.AvailablePlugins[0];
        await vm.InstallPluginAsync(item);

        Assert.True(item.IsInstalled);
        Assert.Single(vm.InstalledPlugins);
        Assert.Empty(vm.AvailablePlugins);

        // Uninstall
        await vm.UninstallPluginAsync(item);

        Assert.False(item.IsInstalled);
        Assert.Empty(vm.InstalledPlugins);
        Assert.Single(vm.AvailablePlugins);
    }

    [Fact]
    public async Task FullFlow_Install_ToggleEnabled()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry(id: "toggle-plugin");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(entry).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var item = vm.AvailablePlugins[0];
        await vm.InstallPluginAsync(item);

        Assert.True(item.IsEnabled);
        Assert.Equal("Enabled", item.StatusText);

        vm.ToggleEnabled(item);

        Assert.False(item.IsEnabled);
        Assert.Equal("Disabled", item.StatusText);

        vm.ToggleEnabled(item);

        Assert.True(item.IsEnabled);
        Assert.Equal("Enabled", item.StatusText);
    }

    [Fact]
    public async Task ExpandCollapse_Sections_AreIndependent()
    {
        var (host, installer, settings, log) = CreateMocks();
        var vm = new PluginManagerViewModel(host, installer, settings, log);

        Assert.True(vm.IsInstalledExpanded);
        Assert.True(vm.IsAvailableExpanded);

        vm.IsInstalledExpanded = false;
        Assert.False(vm.IsInstalledExpanded);
        Assert.True(vm.IsAvailableExpanded);

        vm.IsAvailableExpanded = false;
        Assert.False(vm.IsInstalledExpanded);
        Assert.False(vm.IsAvailableExpanded);
    }

    [Fact]
    public async Task SearchQuery_IsCaseInsensitive()
    {
        var (host, installer, settings, log) = CreateMocks();
        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins = [MakeEntry(id: "p1", displayName: "Video Editor")]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        vm.SearchQuery = "VIDEO";
        Assert.Single(vm.AvailablePlugins);

        vm.SearchQuery = "video";
        Assert.Single(vm.AvailablePlugins);

        vm.SearchQuery = "ViDeO";
        Assert.Single(vm.AvailablePlugins);
    }
}
