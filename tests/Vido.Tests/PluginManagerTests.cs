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

    /// <summary>
    /// Verifies that From Registry Entry sets all properties.
    /// </summary>
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

    /// <summary>
    /// Verifies that From Plugin Info sets installed properties.
    /// </summary>
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

    /// <summary>
    /// Verifies that From Plugin Info disabled sets is enabled false.
    /// </summary>
    [Fact]
    public void FromPluginInfo_Disabled_SetsIsEnabledFalse()
    {
        var info = MakePluginInfo(state: PluginState.Disabled);
        var item = PluginItemViewModel.FromPluginInfo(info);

        Assert.True(item.IsInstalled);
        Assert.False(item.IsEnabled);
    }

    /// <summary>
    /// Verifies that From Plugin Info error sets is enabled false.
    /// </summary>
    [Fact]
    public void FromPluginInfo_Error_SetsIsEnabledFalse()
    {
        var info = MakePluginInfo(state: PluginState.Error);
        var item = PluginItemViewModel.FromPluginInfo(info);

        Assert.False(item.IsEnabled);
    }

    /// <summary>
    /// Verifies that Status Text installed enabled.
    /// </summary>
    [Fact]
    public void StatusText_Installed_Enabled()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        Assert.Equal("Enabled", item.StatusText);
    }

    /// <summary>
    /// Verifies that Status Text installed disabled.
    /// </summary>
    [Fact]
    public void StatusText_Installed_Disabled()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo(state: PluginState.Disabled));
        Assert.Equal("Disabled", item.StatusText);
    }

    /// <summary>
    /// Verifies that Status Text available empty.
    /// </summary>
    [Fact]
    public void StatusText_Available_Empty()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.Equal(string.Empty, item.StatusText);
    }

    /// <summary>
    /// Verifies that Is Installed change notifies status text.
    /// </summary>
    [Fact]
    public void IsInstalled_ChangeNotifiesStatusText()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.IsInstalled = true;

        Assert.Contains(nameof(item.StatusText), changedProps);
    }

    /// <summary>
    /// Verifies that Is Enabled change notifies status text.
    /// </summary>
    [Fact]
    public void IsEnabled_ChangeNotifiesStatusText()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.IsEnabled = false;

        Assert.Contains(nameof(item.StatusText), changedProps);
    }

    /// <summary>
    /// Verifies that Matches Search empty query returns true.
    /// </summary>
    [Fact]
    public void MatchesSearch_EmptyQuery_ReturnsTrue()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.True(item.MatchesSearch(""));
        Assert.True(item.MatchesSearch(null!));
        Assert.True(item.MatchesSearch("   "));
    }

    /// <summary>
    /// Verifies that Matches Search matches display name.
    /// </summary>
    [Fact]
    public void MatchesSearch_MatchesDisplayName()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry(displayName: "Video Effects"));
        Assert.True(item.MatchesSearch("video"));
        Assert.True(item.MatchesSearch("EFFECTS"));
        Assert.False(item.MatchesSearch("audio"));
    }

    /// <summary>
    /// Verifies that Matches Search matches tags.
    /// </summary>
    [Fact]
    public void MatchesSearch_MatchesTags()
    {
        var entry = MakeEntry();
        entry.Tags = ["video", "effects"];
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        Assert.True(item.MatchesSearch("effects"));
        Assert.False(item.MatchesSearch("audio"));
    }

    /// <summary>
    /// Verifies that From Registry Entry official registry sets is official.
    /// </summary>
    [Fact]
    public void FromRegistryEntry_OfficialRegistry_SetsIsOfficial()
    {
        var entry = MakeEntry(isOfficial: true);
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        Assert.True(item.IsOfficial);
    }

    /// <summary>
    /// Verifies that From Registry Entry unofficial registry is official false.
    /// </summary>
    [Fact]
    public void FromRegistryEntry_UnofficialRegistry_IsOfficialFalse()
    {
        var entry = MakeEntry(isOfficial: false);
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        Assert.False(item.IsOfficial);
    }

    /// <summary>
    /// Verifies that From Registry Entry null entry throws.
    /// </summary>
    [Fact]
    public void FromRegistryEntry_NullEntry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PluginItemViewModel.FromRegistryEntry(null!));
    }

    /// <summary>
    /// Verifies that From Plugin Info null info throws.
    /// </summary>
    [Fact]
    public void FromPluginInfo_NullInfo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PluginItemViewModel.FromPluginInfo(null!));
    }

    /// <summary>
    /// Verifies that Display Name falls back to id when blank.
    /// </summary>
    [Fact]
    public void DisplayName_FallsBackToId_WhenBlank()
    {
        var entry = MakeEntry(displayName: "");
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        Assert.Equal(entry.Id, item.DisplayName);
    }

    /// <summary>
    /// Verifies that From Plugin Info with registry entry merges data.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async populates installed from host.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async populates available from registry.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async merges installed with registry.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async deduplicates across registries.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async sets registry source dropdown.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async sets is loading during execution.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async handles registry fetch error.
    /// </summary>
    [Fact]
    public async Task LoadAsync_HandlesRegistryFetchError()
    {
        var (host, installer, settings, log) = CreateMocks();
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(null));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync(); // Should not throw

        Assert.Empty(vm.AvailablePlugins);
    }

    /// <summary>
    /// Verifies that Load Async prevents concurrent execution.
    /// </summary>
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

    /// <summary>
    /// Verifies that Search Query filters plugins.
    /// </summary>
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

    /// <summary>
    /// Verifies that Search Query clearing resets filter.
    /// </summary>
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

    /// <summary>
    /// Verifies that Registry Source Filter filters available.
    /// </summary>
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

    /// <summary>
    /// Verifies that Registry Source Filter installed plugins always shown.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Plugin Async transitions item to installed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Plugin Async failed install no state change.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Plugin Async fires open detail requested.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Plugin Async skips if already installed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Plugin Async skips if busy.
    /// </summary>
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

    /// <summary>
    /// Verifies that Uninstall Plugin Async transitions item to available.
    /// </summary>
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

    /// <summary>
    /// Verifies that Uninstall Plugin Async skips if not installed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Uninstall Plugin Async removes plugin first.
    /// </summary>
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

    /// <summary>
    /// Verifies that Toggle Enabled toggles state.
    /// </summary>
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

    /// <summary>
    /// Verifies that Toggle Enabled ignores non installed.
    /// </summary>
    [Fact]
    public void ToggleEnabled_IgnoresNonInstalled()
    {
        var (host, installer, settings, log) = CreateMocks();
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        vm.ToggleEnabled(item); // Should be a no-op

        host.DidNotReceive().SetEnabled(Arg.Any<string>(), Arg.Any<bool>());
    }

    /// <summary>
    /// Verifies that Open Detail fires open detail requested.
    /// </summary>
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

    /// <summary>
    /// Verifies that Open Plugin Settings fires open settings requested.
    /// </summary>
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

    /// <summary>
    /// Verifies that Count Badges update on filter changes.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async official registry url sets official.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async non official url sets official false.
    /// </summary>
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

    /// <summary>
    /// Verifies that Load Async nsfw registry url sets official.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NsfwRegistryUrl_SetsOfficial()
    {
        var (host, installer, settings, log) = CreateMocks();
        var appSettings = new AppSettings();
        appSettings.PluginRegistryUrls.Add(AppSettings.NsfwRegistryUrl);
        settings.Current.Returns(appSettings);

        var entry = MakeEntry(id: "nsfw-plugin");
        var registry = new PluginRegistry { Name = "NSFW", Plugins = [entry] };
        installer.FetchRegistryAsync(AppSettings.NsfwRegistryUrl).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var nsfwPlugin = vm.AvailablePlugins.First(p => p.Id == "nsfw-plugin");
        Assert.True(nsfwPlugin.IsOfficial);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SettingDisplayItem Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Boolean Setting initialises from store.
    /// </summary>
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

    /// <summary>
    /// Verifies that Boolean Setting default false.
    /// </summary>
    [Fact]
    public void BooleanSetting_DefaultFalse()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("flag", Arg.Any<bool>()).Returns(false);

        var def = new SettingContribution { Id = "flag", Type = "boolean", Default = false };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("False", item.SelectedBooleanValue);
    }

    /// <summary>
    /// Verifies that Boolean Setting auto saves on change.
    /// </summary>
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

    /// <summary>
    /// Verifies that Boolean Setting does not save during initialization.
    /// </summary>
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

    /// <summary>
    /// Verifies that String Setting initialises from store.
    /// </summary>
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

    /// <summary>
    /// Verifies that String Setting auto saves on change.
    /// </summary>
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

    /// <summary>
    /// Verifies that Number Setting initialises from store.
    /// </summary>
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

    /// <summary>
    /// Verifies that Number Setting auto saves on change.
    /// </summary>
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

    /// <summary>
    /// Verifies that Number Setting invalid string does not save.
    /// </summary>
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

    /// <summary>
    /// Verifies that Enum Setting initialises from store.
    /// </summary>
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

    /// <summary>
    /// Verifies that Enum Setting auto saves on change.
    /// </summary>
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

    /// <summary>
    /// Verifies that Enum Setting empty value does not save.
    /// </summary>
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

    /// <summary>
    /// Verifies that Section Property returns manifest section.
    /// </summary>
    [Fact]
    public void SectionProperty_ReturnsManifestSection()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get("x", Arg.Any<string>()).Returns("v");
        var def = new SettingContribution { Id = "x", Type = "string", Section = "Display" };
        var item = new SettingDisplayItem(def, store);

        Assert.Equal("Display", item.Section);
    }

    /// <summary>
    /// Verifies that Boolean Options contains true and false.
    /// </summary>
    [Fact]
    public void BooleanOptions_ContainsTrueAndFalse()
    {
        Assert.Equal(2, SettingDisplayItem.BooleanOptions.Count);
        Assert.Contains("True", SettingDisplayItem.BooleanOptions);
        Assert.Contains("False", SettingDisplayItem.BooleanOptions);
    }

    /// <summary>
    /// Verifies that Constructor null definition throws.
    /// </summary>
    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        var store = Substitute.For<IPluginSettingsStore>();
        Assert.Throws<ArgumentNullException>(() => new SettingDisplayItem(null!, store));
    }

    /// <summary>
    /// Verifies that Constructor null store throws.
    /// </summary>
    [Fact]
    public void Constructor_NullStore_Throws()
    {
        var def = new SettingContribution { Id = "x", Type = "string" };
        Assert.Throws<ArgumentNullException>(() => new SettingDisplayItem(def, null!));
    }

    /// <summary>
    /// Verifies that Convert Default handles json element boolean.
    /// </summary>
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

    /// <summary>
    /// Verifies that Convert Default handles json element number.
    /// </summary>
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

    /// <summary>
    /// Verifies that Convert Default handles json element string.
    /// </summary>
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

    /// <summary>
    /// Verifies that Title And Description return from definition.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Async null entry throws.
    /// </summary>
    [Fact]
    public async Task InstallAsync_NullEntry_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        await Assert.ThrowsAsync<ArgumentNullException>(() => installer.InstallAsync(null!));
    }

    /// <summary>
    /// Verifies that Install Async empty id throws.
    /// </summary>
    [Fact]
    public async Task InstallAsync_EmptyId_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        var entry = new PluginRegistryEntry { Id = "", DownloadUrl = "http://example.com/p.zip" };
        await Assert.ThrowsAsync<ArgumentException>(() => installer.InstallAsync(entry));
    }

    /// <summary>
    /// Verifies that Install Async empty download url throws.
    /// </summary>
    [Fact]
    public async Task InstallAsync_EmptyDownloadUrl_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        var entry = new PluginRegistryEntry { Id = "test", DownloadUrl = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => installer.InstallAsync(entry));
    }

    /// <summary>
    /// Verifies that Uninstall Async empty id throws.
    /// </summary>
    [Fact]
    public async Task UninstallAsync_EmptyId_Throws()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), Path.GetTempPath());
        await Assert.ThrowsAsync<ArgumentException>(() => installer.UninstallAsync(""));
    }

    /// <summary>
    /// Verifies that Uninstall Async nonexistent dir returns true.
    /// </summary>
    [Fact]
    public async Task UninstallAsync_NonexistentDir_ReturnsTrue()
    {
        var log = Substitute.For<ILogService>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), tempDir);

        var result = await installer.UninstallAsync("nonexistent-plugin");

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Uninstall Async existing dir deletes it.
    /// </summary>
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

    /// <summary>
    /// Verifies that Cleanup Pending Uninstalls removes marked directories.
    /// </summary>
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

    /// <summary>
    /// Verifies that Cleanup Pending Uninstalls nonexistent base dir no op.
    /// </summary>
    [Fact]
    public void CleanupPendingUninstalls_NonexistentBaseDir_NoOp()
    {
        var log = Substitute.For<ILogService>();
        var installer = new Vido.Services.Plugin.PluginInstaller(log, new HttpClient(), @"C:\nonexistent\path\" + Guid.NewGuid());
        installer.CleanupPendingUninstalls(); // Should not throw
    }

    /// <summary>
    /// Verifies that Fetch Registry Async empty url returns null.
    /// </summary>
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

    /// <summary>
    /// Verifies that Fetch Registry Async file url parses json.
    /// </summary>
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

    /// <summary>
    /// Verifies that Fetch Registry Async invalid json returns null.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Async file url extracts and validates.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Async zip with root folder moves contents up.
    /// </summary>
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

    /// <summary>
    /// Verifies that Install Async zip without manifest returns false.
    /// </summary>
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

    /// <summary>
    /// Verifies that Plugin Registry Entry default values.
    /// </summary>
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

    /// <summary>
    /// Verifies that Plugin Registry default values.
    /// </summary>
    [Fact]
    public void PluginRegistry_DefaultValues()
    {
        var registry = new PluginRegistry();

        Assert.Equal(string.Empty, registry.Name);
        Assert.Empty(registry.Plugins);
    }

    /// <summary>
    /// Verifies that Plugin Registry Entry json round trip.
    /// </summary>
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

    /// <summary>
    /// Verifies that Plugin Registry json round trip.
    /// </summary>
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

    /// <summary>
    /// Verifies that Sample Plugin Manifest has all required fields.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_HasAllRequiredFields()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
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

    /// <summary>
    /// Verifies that Sample Plugin Manifest declares all contribution types.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_DeclaresAllContributionTypes()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
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

    /// <summary>
    /// Verifies that Sample Plugin Manifest declares all setting types.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_DeclaresAllSettingTypes()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
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

    /// <summary>
    /// Verifies that Sample Plugin Manifest has at least two sections.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_HasAtLeastTwoSections()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        var sections = manifest.Contributes.Settings
            .Where(s => !string.IsNullOrWhiteSpace(s.Section))
            .Select(s => s.Section)
            .Distinct()
            .ToList();

        Assert.True(sections.Count >= 2, $"Expected at least 2 sections, got {sections.Count}: {string.Join(", ", sections)}");
    }

    /// <summary>
    /// Verifies that Sample Plugin Manifest has force override setting.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_HasForceOverrideSetting()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        Assert.True(
            manifest.Contributes.Settings.Any(s => s.ForceOverride),
            "At least one setting must have forceOverride: true");
    }

    /// <summary>
    /// Verifies that Sample Plugin Manifest enum setting has enum values.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_EnumSetting_HasEnumValues()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        var enumSettings = manifest.Contributes.Settings.Where(s => s.Type.Equals("enum", StringComparison.OrdinalIgnoreCase));
        foreach (var setting in enumSettings)
        {
            Assert.NotEmpty(setting.EnumValues);
        }
    }

    /// <summary>
    /// Verifies that Sample Plugin Manifest sidebar contribution has id and title.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_SidebarContribution_HasIdAndTitle()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        foreach (var sidebar in manifest.Contributes.Sidebar)
        {
            Assert.False(string.IsNullOrWhiteSpace(sidebar.Id));
            Assert.False(string.IsNullOrWhiteSpace(sidebar.Title));
        }
    }

    /// <summary>
    /// Verifies that Sample Plugin Manifest file handler has sample extension.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_FileHandler_HasSampleExtension()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        var extensions = manifest.Contributes.FileHandlers.SelectMany(h => h.Extensions).ToList();
        Assert.Contains(".sample", extensions);
    }

    /// <summary>
    /// Verifies that Sample Plugin Manifest file icons has sample extension.
    /// </summary>
    [Fact]
    public void SamplePluginManifest_FileIcons_HasSampleExtension()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        var json = File.ReadAllText(Path.Combine(GetSamplePluginPath(), "plugin.json"));
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json)!;

        Assert.True(manifest.Contributes.FileIcons.ContainsKey(".sample"));
    }

    /// <summary>
    /// Verifies that Sample Plugin readme exists.
    /// </summary>
    [Fact]
    public void SamplePlugin_ReadmeExists()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        Assert.True(File.Exists(Path.Combine(GetSamplePluginPath(), "README.md")));
    }

    /// <summary>
    /// Verifies that Sample Plugin changelog exists.
    /// </summary>
    [Fact]
    public void SamplePlugin_ChangelogExists()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        Assert.True(File.Exists(Path.Combine(GetSamplePluginPath(), "CHANGELOG.md")));
    }

    /// <summary>
    /// Verifies that Sample Plugin registry json exists.
    /// </summary>
    [Fact]
    public void SamplePlugin_RegistryJsonExists()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
        Assert.True(File.Exists(Path.Combine(GetSamplePluginPath(), "registry.json")));
    }

    /// <summary>
    /// Verifies that Sample Plugin registry json contains sample plugin.
    /// </summary>
    [Fact]
    public void SamplePlugin_RegistryJson_ContainsSamplePlugin()
    {
        if (!SamplePluginExists()) return; // Skip in CI — external repo not available
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

    private static bool SamplePluginExists()
    {
        var testDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var samplePluginPath = Path.Combine(Path.GetDirectoryName(solutionRoot)!, "vido-sample-plugin");
        return Directory.Exists(samplePluginPath);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PluginPaths Tests                                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Plugin Paths default plugin directory is not empty.
    /// </summary>
    [Fact]
    public void PluginPaths_DefaultPluginDirectory_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(PluginPaths.DefaultPluginDirectory));
    }

    /// <summary>
    /// Verifies that Plugin Paths default plugin directory ends with plugins.
    /// </summary>
    [Fact]
    public void PluginPaths_DefaultPluginDirectory_EndsWithPlugins()
    {
        Assert.EndsWith("plugins", PluginPaths.DefaultPluginDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Integration: PluginManagerViewModel end-to-end flows            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Full Flow install then uninstall.
    /// </summary>
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

    /// <summary>
    /// Verifies that Full Flow install toggle enabled.
    /// </summary>
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

    /// <summary>
    /// Verifies that Expand Collapse sections are independent.
    /// </summary>
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

    /// <summary>
    /// Verifies that Search Query is case insensitive.
    /// </summary>
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

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vi-046: Plugin Update Detection & UI                           ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Status Text shows update available when has update.
    /// </summary>
    [Fact]
    public void StatusText_ShowsUpdateAvailable_When_HasUpdate()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        item.HasUpdate = true;
        Assert.Equal("Update Available", item.StatusText);
    }

    /// <summary>
    /// Verifies that Has Update notifies status text changed.
    /// </summary>
    [Fact]
    public void HasUpdate_NotifiesStatusTextChanged()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.HasUpdate = true;

        Assert.Contains(nameof(item.StatusText), changedProps);
        Assert.Contains(nameof(item.HasUpdate), changedProps);
    }

    /// <summary>
    /// Verifies that Is Newer Version compares correctly.
    /// </summary>
    /// <param name="latest">The latest available version string.</param>
    /// <param name="current">The current installed version string.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    public void IsNewerVersion_ComparesCorrectly(string latest, string current, bool expected)
    {
        Assert.Equal(expected, PluginManagerViewModel.IsNewerVersion(latest, current));
    }

    /// <summary>
    /// Verifies that Is Newer Version unparseable strings returns false.
    /// </summary>
    [Fact]
    public void IsNewerVersion_UnparseableStrings_ReturnsFalse()
    {
        // Can't parse — assume no update (conservative)
        Assert.False(PluginManagerViewModel.IsNewerVersion("abc", "def"));
        Assert.False(PluginManagerViewModel.IsNewerVersion("abc", "abc"));
    }

    /// <summary>
    /// Verifies that Load Async detects update when registry version newer.
    /// </summary>
    [Fact]
    public async Task LoadAsync_DetectsUpdate_When_RegistryVersionNewer()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.plugin", displayName: "Test Plugin");
        host.Plugins.Returns(new[] { info });

        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins = [MakeEntry(id: "com.test.plugin", version: "2.0.0")]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var installed = vm.InstalledPlugins.Single(p => p.Id == "com.test.plugin");
        Assert.True(installed.HasUpdate);
        Assert.Equal("2.0.0", installed.AvailableVersion);
        Assert.Equal("Update Available", installed.StatusText);
    }

    /// <summary>
    /// Verifies that Load Async no update when versions same.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NoUpdate_When_VersionsSame()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.plugin");
        host.Plugins.Returns(new[] { info });

        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins = [MakeEntry(id: "com.test.plugin", version: "1.0.0")]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var installed = vm.InstalledPlugins.Single(p => p.Id == "com.test.plugin");
        Assert.False(installed.HasUpdate);
        Assert.Null(installed.AvailableVersion);
    }

    /// <summary>
    /// Verifies that Load Async no update when registry version older.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NoUpdate_When_RegistryVersionOlder()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.plugin");
        host.Plugins.Returns(new[] { info });

        var registry = new PluginRegistry
        {
            Name = "R",
            Plugins = [MakeEntry(id: "com.test.plugin", version: "0.5.0")]
        };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var installed = vm.InstalledPlugins.Single(p => p.Id == "com.test.plugin");
        Assert.False(installed.HasUpdate);
    }

    /// <summary>
    /// Verifies that Update Plugin Async reinstalls and clears has update.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_Reinstalls_And_ClearsHasUpdate()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.plugin");
        host.Plugins.Returns(new[] { info });

        var entry = MakeEntry(id: "com.test.plugin", version: "2.0.0");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var updatedInfo = MakePluginInfo(id: "com.test.plugin");
        host.GetPlugin("com.test.plugin").Returns(updatedInfo);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var installed = vm.InstalledPlugins.Single(p => p.Id == "com.test.plugin");
        Assert.True(installed.HasUpdate);

        await vm.UpdatePluginAsync(installed);

        Assert.False(installed.HasUpdate);
        Assert.Null(installed.AvailableVersion);
        Assert.False(installed.IsBusy);
        host.Received(1).RemovePlugin("com.test.plugin");
        await installer.Received(1).InstallAsync(Arg.Any<PluginRegistryEntry>());
        host.Received().ActivateAll();
    }

    /// <summary>
    /// Verifies that Update Plugin Async does nothing when no update.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_DoesNothing_When_NoUpdate()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        item.IsInstalled = true;
        item.HasUpdate = false;

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.UpdatePluginAsync(item);

        host.DidNotReceive().RemovePlugin(Arg.Any<string>());
        await installer.DidNotReceive().InstallAsync(Arg.Any<PluginRegistryEntry>());
    }

    /// <summary>
    /// Verifies that Update Plugin Async does nothing when busy.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_DoesNothing_When_Busy()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        var item = PluginItemViewModel.FromRegistryEntry(entry);
        item.IsInstalled = true;
        item.HasUpdate = true;
        item.IsBusy = true;

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.UpdatePluginAsync(item);

        host.DidNotReceive().RemovePlugin(Arg.Any<string>());
    }

    /// <summary>
    /// Verifies that Update Plugin Async fires restart required.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_Fires_RestartRequired()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.plugin");
        host.Plugins.Returns(new[] { info });

        var entry = MakeEntry(id: "com.test.plugin", version: "2.0.0");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var updatedInfo = MakePluginInfo(id: "com.test.plugin");
        host.GetPlugin("com.test.plugin").Returns(updatedInfo);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        string? restartMessage = null;
        vm.RestartRequired += msg => restartMessage = msg;

        var installed = vm.InstalledPlugins.Single(p => p.Id == "com.test.plugin");
        await vm.UpdatePluginAsync(installed);

        Assert.NotNull(restartMessage);
        Assert.Contains("Test Plugin", restartMessage);
        Assert.Contains("updated", restartMessage);
    }

    /// <summary>
    /// Verifies that Load Async reconcile detects update when plugin appears late.
    /// </summary>
    [Fact]
    public async Task LoadAsync_Reconcile_DetectsUpdate_WhenPluginAppearsLate()
    {
        // Simulates the startup race: ActivateAll returns no plugins initially,
        // but after the registry fetch, the plugin appears in the host.
        var (host, installer, settings, log) = CreateMocks();

        var pluginInfo = MakePluginInfo(id: "com.test.plugin"); // version 1.0.0

        // First call to Plugins returns empty (ActivateAll hasn't run yet in this scenario).
        // After ActivateAll is called inside LoadAsync, subsequent calls return the plugin.
        var callCount = 0;
        host.Plugins.Returns(_ =>
        {
            callCount++;
            // First call is for step 1 enumeration; ActivateAll is called before it,
            // so all calls should return the plugin. But to test step 3 reconciliation,
            // simulate the plugin appearing only after the first enumeration.
            return callCount > 1 ? new[] { pluginInfo } : Array.Empty<PluginInfo>();
        });

        var entry = MakeEntry(id: "com.test.plugin", version: "2.0.0");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        // The plugin should be marked as installed (from reconciliation step 3)
        var plugin = vm.InstalledPlugins.FirstOrDefault(p => p.Id == "com.test.plugin");
        Assert.NotNull(plugin);
        Assert.True(plugin.IsInstalled);
        // And the update should be detected (registry 2.0.0 > installed 1.0.0)
        Assert.True(plugin.HasUpdate);
        Assert.Equal("2.0.0", plugin.AvailableVersion);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ VPP-003: Dependency Auto-Install & Uninstall Blocking          ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Install Plugin Async auto installs dependencies.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_AutoInstallsDependencies()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Dep entry available in registry
        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep Plugin", version: "2.0.0");
        // Target entry depends on it
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target Plugin");
        targetEntry.Dependencies = [new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" }];

        var registry = new PluginRegistry { Name = "R", Plugins = [depEntry, targetEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        // Neither plugin is installed
        host.GetPlugin("com.test.dep").Returns((PluginInfo?)null);
        host.GetPlugin("com.test.target").Returns((PluginInfo?)null);

        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.AvailablePlugins.First(p => p.Id == "com.test.target");
        await vm.InstallPluginAsync(targetItem);

        // Dep should be installed first, then target
        Received.InOrder(() =>
        {
            installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"));
            installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));
        });
    }

    /// <summary>
    /// Verifies that Install Plugin Async skips already installed deps.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_SkipsAlreadyInstalledDeps()
    {
        var (host, installer, settings, log) = CreateMocks();

        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep Plugin");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target Plugin");
        targetEntry.Dependencies = [new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" }];

        var depInfo = MakePluginInfo(id: "com.test.dep", displayName: "Dep Plugin");
        host.Plugins.Returns(new[] { depInfo });
        host.GetPlugin("com.test.dep").Returns(depInfo);
        host.GetPlugin("com.test.target").Returns((PluginInfo?)null);

        var registry = new PluginRegistry { Name = "R", Plugins = [depEntry, targetEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.AvailablePlugins.First(p => p.Id == "com.test.target");
        await vm.InstallPluginAsync(targetItem);

        // Dep should NOT be installed (already present)
        await installer.DidNotReceive().InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"));
        // Target should be installed
        await installer.Received(1).InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));
    }

    /// <summary>
    /// Verifies that Install Plugin Async dep install fails aborts target install.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_DepInstallFails_AbortsTargetInstall()
    {
        var (host, installer, settings, log) = CreateMocks();

        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep Plugin");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target Plugin");
        targetEntry.Dependencies = [new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" }];

        var registry = new PluginRegistry { Name = "R", Plugins = [depEntry, targetEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        host.GetPlugin(Arg.Any<string>()).Returns((PluginInfo?)null);

        // Dep install fails
        installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"))
            .Returns(Task.FromResult(false));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.AvailablePlugins.First(p => p.Id == "com.test.target");
        await vm.InstallPluginAsync(targetItem);

        // Target should NOT be installed because dep failed
        await installer.DidNotReceive().InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));
        Assert.False(targetItem.IsInstalled);
    }

    /// <summary>
    /// Verifies that Install Plugin Async transitive deps installed in order.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_TransitiveDeps_InstalledInOrder()
    {
        var (host, installer, settings, log) = CreateMocks();

        // C depends on B, B depends on A
        var entryA = MakeEntry(id: "com.test.a", displayName: "A");
        var entryB = MakeEntry(id: "com.test.b", displayName: "B");
        entryB.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];
        var entryC = MakeEntry(id: "com.test.c", displayName: "C");
        entryC.Dependencies = [new PluginDependency { Id = "com.test.b", MinVersion = "1.0.0" }];

        var registry = new PluginRegistry { Name = "R", Plugins = [entryA, entryB, entryC] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        host.GetPlugin(Arg.Any<string>()).Returns((PluginInfo?)null);
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var itemC = vm.AvailablePlugins.First(p => p.Id == "com.test.c");
        await vm.InstallPluginAsync(itemC);

        // Should install A, then B, then C
        Received.InOrder(() =>
        {
            installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.a"));
            installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.b"));
            installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.c"));
        });
    }

    /// <summary>
    /// Verifies that Install Plugin Async no deps installs directly.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_NoDeps_InstallsDirectly()
    {
        var (host, installer, settings, log) = CreateMocks();

        var entry = MakeEntry(id: "com.test.no-deps", displayName: "No Deps Plugin");
        // No dependencies

        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        host.GetPlugin(Arg.Any<string>()).Returns((PluginInfo?)null);
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var item = vm.AvailablePlugins.First(p => p.Id == "com.test.no-deps");
        await vm.InstallPluginAsync(item);

        // Only one install call — no dep installs
        await installer.Received(1).InstallAsync(Arg.Any<PluginRegistryEntry>());
    }

    /// <summary>
    /// Verifies that Uninstall Plugin Async blocked when dependants exist.
    /// </summary>
    [Fact]
    public async Task UninstallPluginAsync_BlockedWhenDependantsExist()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Plugin B depends on plugin A
        var infoA = MakePluginInfo(id: "com.test.a", displayName: "Plugin A");
        var infoB = MakePluginInfo(id: "com.test.b", displayName: "Plugin B");
        infoB.Manifest.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];

        host.Plugins.Returns(new[] { infoA, infoB });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(null));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        string? blockedMessage = null;
        vm.UninstallBlocked += msg => blockedMessage = msg;

        var itemA = vm.InstalledPlugins.First(p => p.Id == "com.test.a");
        await vm.UninstallPluginAsync(itemA);

        // Uninstall should be blocked
        Assert.NotNull(blockedMessage);
        Assert.Contains("Plugin B", blockedMessage);
        Assert.Contains("Cannot remove", blockedMessage);

        // Plugin A should still be installed
        Assert.True(itemA.IsInstalled);
        Assert.False(itemA.IsBusy);

        // RemovePlugin and UninstallAsync should NOT have been called
        host.DidNotReceive().RemovePlugin("com.test.a");
        await installer.DidNotReceive().UninstallAsync("com.test.a");
    }

    /// <summary>
    /// Verifies that Uninstall Plugin Async allowed when no dependants.
    /// </summary>
    [Fact]
    public async Task UninstallPluginAsync_AllowedWhenNoDependants()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Plugin B depends on A, but we're uninstalling B (which has no dependants)
        var infoA = MakePluginInfo(id: "com.test.a", displayName: "Plugin A");
        var infoB = MakePluginInfo(id: "com.test.b", displayName: "Plugin B");
        infoB.Manifest.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];

        host.Plugins.Returns(new[] { infoA, infoB });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(null));
        installer.UninstallAsync("com.test.b").Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var itemB = vm.InstalledPlugins.First(p => p.Id == "com.test.b");
        await vm.UninstallPluginAsync(itemB);

        // Should proceed normally
        Assert.False(itemB.IsInstalled);
        host.Received(1).RemovePlugin("com.test.b");
        await installer.Received(1).UninstallAsync("com.test.b");
    }

    /// <summary>
    /// Verifies that Uninstall Plugin Async blocked lists multiple dependants.
    /// </summary>
    [Fact]
    public async Task UninstallPluginAsync_BlockedListsMultipleDependants()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Plugins B and C both depend on A
        var infoA = MakePluginInfo(id: "com.test.a", displayName: "Plugin A");
        var infoB = MakePluginInfo(id: "com.test.b", displayName: "Plugin B");
        infoB.Manifest.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];
        var infoC = MakePluginInfo(id: "com.test.c", displayName: "Plugin C");
        infoC.Manifest.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];

        host.Plugins.Returns(new[] { infoA, infoB, infoC });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(null));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        string? blockedMessage = null;
        vm.UninstallBlocked += msg => blockedMessage = msg;

        var itemA = vm.InstalledPlugins.First(p => p.Id == "com.test.a");
        await vm.UninstallPluginAsync(itemA);

        Assert.NotNull(blockedMessage);
        Assert.Contains("Plugin B", blockedMessage);
        Assert.Contains("Plugin C", blockedMessage);
    }

    /// <summary>
    /// Verifies that Get Installed Dependants no deps returns empty.
    /// </summary>
    [Fact]
    public void GetInstalledDependants_NoDeps_ReturnsEmpty()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(id: "com.test.plugin");
        host.Plugins.Returns(new[] { info });

        var vm = new PluginManagerViewModel(host, installer, settings, log);

        var dependants = vm.GetInstalledDependants("com.test.plugin");
        Assert.Empty(dependants);
    }

    /// <summary>
    /// Verifies that Get Installed Dependants case insensitive.
    /// </summary>
    [Fact]
    public void GetInstalledDependants_CaseInsensitive()
    {
        var (host, installer, settings, log) = CreateMocks();
        var infoA = MakePluginInfo(id: "com.test.a", displayName: "A");
        var infoB = MakePluginInfo(id: "com.test.b", displayName: "B");
        infoB.Manifest.Dependencies = [new PluginDependency { Id = "COM.TEST.A", MinVersion = "1.0.0" }];
        host.Plugins.Returns(new[] { infoA, infoB });

        var vm = new PluginManagerViewModel(host, installer, settings, log);

        var dependants = vm.GetInstalledDependants("com.test.a");
        Assert.Single(dependants);
        Assert.Equal("B", dependants[0]);
    }

    /// <summary>
    /// Verifies that Resolve Dependencies returns leaf first.
    /// </summary>
    [Fact]
    public async Task ResolveDependencies_ReturnsLeafFirst()
    {
        var (host, installer, settings, log) = CreateMocks();

        // B depends on A
        var entryA = MakeEntry(id: "com.test.a", displayName: "A");
        var entryB = MakeEntry(id: "com.test.b", displayName: "B");
        entryB.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];

        var registry = new PluginRegistry { Name = "R", Plugins = [entryA, entryB] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        host.GetPlugin(Arg.Any<string>()).Returns((PluginInfo?)null);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var deps = vm.ResolveDependencies(entryB);
        Assert.Single(deps);
        Assert.Equal("com.test.a", deps[0].Id);
    }

    /// <summary>
    /// Verifies that Resolve Dependencies skips installed deps.
    /// </summary>
    [Fact]
    public async Task ResolveDependencies_SkipsInstalledDeps()
    {
        var (host, installer, settings, log) = CreateMocks();

        var entryA = MakeEntry(id: "com.test.a", displayName: "A");
        var entryB = MakeEntry(id: "com.test.b", displayName: "B");
        entryB.Dependencies = [new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }];

        var infoA = MakePluginInfo(id: "com.test.a");
        host.Plugins.Returns(new[] { infoA });
        host.GetPlugin("com.test.a").Returns(infoA);

        var registry = new PluginRegistry { Name = "R", Plugins = [entryA, entryB] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var deps = vm.ResolveDependencies(entryB);
        Assert.Empty(deps); // A is already installed
    }

    /// <summary>
    /// Verifies that Install Plugin Async dep auto installed updates item state.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_DepAutoInstalled_UpdatesItemState()
    {
        var (host, installer, settings, log) = CreateMocks();

        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep Plugin");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target Plugin");
        targetEntry.Dependencies = [new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" }];

        var registry = new PluginRegistry { Name = "R", Plugins = [depEntry, targetEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        host.GetPlugin(Arg.Any<string>()).Returns((PluginInfo?)null);
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var depItem = vm.AvailablePlugins.First(p => p.Id == "com.test.dep");
        var targetItem = vm.AvailablePlugins.First(p => p.Id == "com.test.target");

        await vm.InstallPluginAsync(targetItem);

        // The dep item should also be marked as installed
        Assert.True(depItem.IsInstalled);
        Assert.True(depItem.IsEnabled);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-004 — Block install/update/enable when minVidoVersion not met║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Meets Min Vido Version null or empty returns true.
    /// </summary>
    /// <param name="minVersion">The minimum required Vido version.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MeetsMinVidoVersion_NullOrEmpty_ReturnsTrue(string? minVersion)
    {
        Assert.True(PluginManagerViewModel.MeetsMinVidoVersion(minVersion));
    }

    /// <summary>
    /// Verifies that Meets Min Vido Version unparseable version returns true.
    /// </summary>
    [Fact]
    public void MeetsMinVidoVersion_UnparseableVersion_ReturnsTrue()
    {
        Assert.True(PluginManagerViewModel.MeetsMinVidoVersion("not-a-version"));
    }

    /// <summary>
    /// Verifies that Meets Min Vido Version future version returns false.
    /// </summary>
    [Fact]
    public void MeetsMinVidoVersion_FutureVersion_ReturnsFalse()
    {
        // A version far in the future should not be met
        Assert.False(PluginManagerViewModel.MeetsMinVidoVersion("999.0.0"));
    }

    /// <summary>
    /// Verifies that Meets Min Vido Version current or older version returns true.
    /// </summary>
    [Fact]
    public void MeetsMinVidoVersion_CurrentOrOlderVersion_ReturnsTrue()
    {
        // Version 0.0.1 should always be met
        Assert.True(PluginManagerViewModel.MeetsMinVidoVersion("0.0.1"));
    }

    /// <summary>
    /// Verifies that Install Plugin Async when min vido version not met does not install.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_WhenMinVidoVersionNotMet_DoesNotInstall()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        entry.MinVidoVersion = "999.0.0"; // Far in the future
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.InstallPluginAsync(item);

        await installer.DidNotReceive().InstallAsync(Arg.Any<PluginRegistryEntry>());
        Assert.False(item.IsInstalled);
        Assert.NotNull(item.StatusMessage);
        Assert.Contains("999.0.0", item.StatusMessage);
    }

    /// <summary>
    /// Verifies that Install Plugin Async when min vido version met installs.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_WhenMinVidoVersionMet_Installs()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        entry.MinVidoVersion = "0.0.1"; // Always met
        var item = PluginItemViewModel.FromRegistryEntry(entry);

        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.InstallPluginAsync(item);

        await installer.Received(1).InstallAsync(entry);
        Assert.True(item.IsInstalled);
    }

    /// <summary>
    /// Verifies that Update Plugin Async when min vido version not met does not update.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_WhenMinVidoVersionNotMet_DoesNotUpdate()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry(version: "2.0.0");
        entry.MinVidoVersion = "999.0.0";
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        item.RegistryEntry = entry;
        item.HasUpdate = true;
        item.AvailableVersion = "2.0.0";

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.UpdatePluginAsync(item);

        await installer.DidNotReceive().InstallAsync(Arg.Any<PluginRegistryEntry>());
        Assert.True(item.HasUpdate); // Still has update (not cleared)
        Assert.NotNull(item.StatusMessage);
        Assert.Contains("999.0.0", item.StatusMessage);
    }

    /// <summary>
    /// Verifies that Toggle Enabled when min vido version not met does not enable.
    /// </summary>
    [Fact]
    public void ToggleEnabled_WhenMinVidoVersionNotMet_DoesNotEnable()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(state: PluginState.Disabled);
        info.Manifest.MinVidoVersion = "999.0.0";
        var item = PluginItemViewModel.FromPluginInfo(info);
        item.IsEnabled = false;

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        vm.ToggleEnabled(item);

        Assert.False(item.IsEnabled);
        host.DidNotReceive().SetEnabled(Arg.Any<string>(), true);
        Assert.NotNull(item.StatusMessage);
        Assert.Contains("999.0.0", item.StatusMessage);
    }

    /// <summary>
    /// Verifies that Toggle Enabled when min vido version met enables.
    /// </summary>
    [Fact]
    public void ToggleEnabled_WhenMinVidoVersionMet_Enables()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo(state: PluginState.Disabled);
        info.Manifest.MinVidoVersion = "0.0.1";
        var item = PluginItemViewModel.FromPluginInfo(info);
        item.IsEnabled = false;

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        vm.ToggleEnabled(item);

        Assert.True(item.IsEnabled);
        host.Received(1).SetEnabled(item.Id, true);
    }

    /// <summary>
    /// Verifies that Toggle Enabled disable always allowed even with future min version.
    /// </summary>
    [Fact]
    public void ToggleEnabled_Disable_AlwaysAllowedEvenWithFutureMinVersion()
    {
        var (host, installer, settings, log) = CreateMocks();
        var info = MakePluginInfo();
        info.Manifest.MinVidoVersion = "999.0.0";
        var item = PluginItemViewModel.FromPluginInfo(info);
        Assert.True(item.IsEnabled);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        vm.ToggleEnabled(item);

        Assert.False(item.IsEnabled);
        host.Received(1).SetEnabled(item.Id, false);
    }

    /// <summary>
    /// Verifies that Load Async sets requires newer vido for available plugins.
    /// </summary>
    [Fact]
    public async Task LoadAsync_SetsRequiresNewerVido_ForAvailablePlugins()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        entry.MinVidoVersion = "999.0.0";

        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var item = vm.AvailablePlugins.First(p => p.Id == entry.Id);
        Assert.True(item.RequiresNewerVido);
    }

    /// <summary>
    /// Verifies that Load Async does not set requires newer vido when version met.
    /// </summary>
    [Fact]
    public async Task LoadAsync_DoesNotSetRequiresNewerVido_WhenVersionMet()
    {
        var (host, installer, settings, log) = CreateMocks();
        var entry = MakeEntry();
        entry.MinVidoVersion = "0.0.1";

        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var item = vm.AvailablePlugins.First(p => p.Id == entry.Id);
        Assert.False(item.RequiresNewerVido);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-005 — Dependency Resolution During Update                   ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Update Plugin Async with outdated dependency updates dependency first.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_WithOutdatedDependency_UpdatesDependencyFirst()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Installed: target v1.0, dep v1.0
        var targetInfo = MakePluginInfo(id: "com.test.target", displayName: "Target");
        var depInfo = MakePluginInfo(id: "com.test.dep", displayName: "Dep");
        host.Plugins.Returns(new[] { targetInfo, depInfo });
        host.GetPlugin("com.test.dep").Returns(depInfo);
        host.GetPlugin("com.test.target").Returns(targetInfo);

        // Registry: target v2.0 depends on dep >= 2.0, dep v2.0
        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep", version: "2.0.0");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target", version: "2.0.0");
        targetEntry.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "2.0.0" }
        ];

        var registry = new PluginRegistry { Name = "R", Plugins = [targetEntry, depEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.InstalledPlugins.Single(p => p.Id == "com.test.target");
        Assert.True(targetItem.HasUpdate);

        await vm.UpdatePluginAsync(targetItem);

        // Dependency should have been removed and reinstalled
        host.Received(1).RemovePlugin("com.test.dep");
        await installer.Received(1).InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"));

        // Target should have been removed and reinstalled
        host.Received(1).RemovePlugin("com.test.target");
        await installer.Received(1).InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));

        Assert.False(targetItem.HasUpdate);
    }

    /// <summary>
    /// Verifies that Update Plugin Async dependency update fails aborts target update.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_DependencyUpdateFails_AbortsTargetUpdate()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Installed: target v1.0, dep v1.0
        var targetInfo = MakePluginInfo(id: "com.test.target", displayName: "Target");
        var depInfo = MakePluginInfo(id: "com.test.dep", displayName: "Dep");
        host.Plugins.Returns(new[] { targetInfo, depInfo });
        host.GetPlugin("com.test.dep").Returns(depInfo);
        host.GetPlugin("com.test.target").Returns(targetInfo);

        // Registry: target v2.0 depends on dep >= 2.0, dep v2.0
        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep", version: "2.0.0");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target", version: "2.0.0");
        targetEntry.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "2.0.0" }
        ];

        var registry = new PluginRegistry { Name = "R", Plugins = [targetEntry, depEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));

        // Dep install fails
        installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"))
            .Returns(Task.FromResult(false));
        installer.InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"))
            .Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.InstalledPlugins.Single(p => p.Id == "com.test.target");
        Assert.True(targetItem.HasUpdate);

        await vm.UpdatePluginAsync(targetItem);

        // Dep was attempted (removed + install)
        host.Received(1).RemovePlugin("com.test.dep");

        // Target update should NOT have been attempted
        host.DidNotReceive().RemovePlugin("com.test.target");
        await installer.DidNotReceive().InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));

        // Target should still show as having an update
        Assert.True(targetItem.HasUpdate);
    }

    /// <summary>
    /// Verifies that Update Plugin Async dependency already meets version skips it.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_DependencyAlreadyMeetsVersion_SkipsIt()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Installed: target v1.0, dep v2.0 (already meets requirement)
        var targetInfo = MakePluginInfo(id: "com.test.target", displayName: "Target");
        var depInfo = MakePluginInfo(id: "com.test.dep", displayName: "Dep");
        depInfo.Manifest.Version = "2.0.0"; // Already at v2.0
        host.Plugins.Returns(new[] { targetInfo, depInfo });
        host.GetPlugin("com.test.dep").Returns(depInfo);
        host.GetPlugin("com.test.target").Returns(targetInfo);

        // Registry: target v2.0 depends on dep >= 1.0 (already satisfied), dep v2.0
        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep", version: "2.0.0");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target", version: "2.0.0");
        targetEntry.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" }
        ];

        var registry = new PluginRegistry { Name = "R", Plugins = [targetEntry, depEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.InstalledPlugins.Single(p => p.Id == "com.test.target");
        Assert.True(targetItem.HasUpdate);

        await vm.UpdatePluginAsync(targetItem);

        // Dependency should NOT have been removed or reinstalled (already meets version)
        host.DidNotReceive().RemovePlugin("com.test.dep");
        await installer.DidNotReceive().InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"));

        // Target should have been updated normally
        host.Received(1).RemovePlugin("com.test.target");
        await installer.Received(1).InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));
        Assert.False(targetItem.HasUpdate);
    }

    /// <summary>
    /// Verifies that Update Plugin Async dependency not installed installs it.
    /// </summary>
    [Fact]
    public async Task UpdatePluginAsync_DependencyNotInstalled_InstallsIt()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Installed: target v1.0 only (dep NOT installed)
        var targetInfo = MakePluginInfo(id: "com.test.target", displayName: "Target");
        host.Plugins.Returns(new[] { targetInfo });
        host.GetPlugin("com.test.dep").Returns((PluginInfo?)null);
        host.GetPlugin("com.test.target").Returns(targetInfo);

        // Registry: target v2.0 depends on dep >= 1.0, dep v1.0
        var depEntry = MakeEntry(id: "com.test.dep", displayName: "Dep", version: "1.0.0");
        var targetEntry = MakeEntry(id: "com.test.target", displayName: "Target", version: "2.0.0");
        targetEntry.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" }
        ];

        var registry = new PluginRegistry { Name = "R", Plugins = [targetEntry, depEntry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        var targetItem = vm.InstalledPlugins.Single(p => p.Id == "com.test.target");
        Assert.True(targetItem.HasUpdate);

        // Find the dep item (should be in available plugins, not installed)
        var depItem = vm.AvailablePlugins.SingleOrDefault(p => p.Id == "com.test.dep");
        Assert.NotNull(depItem);
        Assert.False(depItem.IsInstalled);

        await vm.UpdatePluginAsync(targetItem);

        // Dependency should have been auto-installed
        await installer.Received(1).InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.dep"));
        Assert.True(depItem.IsInstalled);

        // Target should have been updated
        host.Received(1).RemovePlugin("com.test.target");
        await installer.Received(1).InstallAsync(Arg.Is<PluginRegistryEntry>(e => e.Id == "com.test.target"));
        Assert.False(targetItem.HasUpdate);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-008 — Installed Plugin Immediately Shown After Install      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Install Plugin Async success plugin appears in installed list.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_Success_PluginAppearsInInstalledList()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Registry has one available plugin
        var entry = MakeEntry(id: "com.test.new", displayName: "New Plugin", version: "1.0.0");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));
        host.GetPlugin("com.test.new").Returns((PluginInfo?)null);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        // Verify it starts in Available, not Installed
        Assert.Single(vm.AvailablePlugins, p => p.Id == "com.test.new");
        Assert.DoesNotContain(vm.InstalledPlugins, p => p.Id == "com.test.new");

        var item = vm.AvailablePlugins.Single(p => p.Id == "com.test.new");
        await vm.InstallPluginAsync(item);

        // After install, it should appear in InstalledPlugins
        Assert.Contains(vm.InstalledPlugins, p => p.Id == "com.test.new");
        Assert.Equal(1, vm.InstalledCount);
    }

    /// <summary>
    /// Verifies that Install Plugin Async success plugin removed from available list.
    /// </summary>
    [Fact]
    public async Task InstallPluginAsync_Success_PluginRemovedFromAvailableList()
    {
        var (host, installer, settings, log) = CreateMocks();

        // Registry has two available plugins
        var entry1 = MakeEntry(id: "com.test.one", displayName: "Plugin One", version: "1.0.0");
        var entry2 = MakeEntry(id: "com.test.two", displayName: "Plugin Two", version: "1.0.0");
        var registry = new PluginRegistry { Name = "R", Plugins = [entry1, entry2] };
        settings.Current.Returns(new AppSettings { PluginRegistryUrls = ["https://example.com/registry"] });
        installer.FetchRegistryAsync(Arg.Any<string>()).Returns(Task.FromResult<PluginRegistry?>(registry));
        installer.InstallAsync(Arg.Any<PluginRegistryEntry>()).Returns(Task.FromResult(true));
        host.GetPlugin(Arg.Any<string>()).Returns((PluginInfo?)null);

        var vm = new PluginManagerViewModel(host, installer, settings, log);
        await vm.LoadAsync();

        // Both plugins start in Available
        Assert.Equal(2, vm.AvailablePlugins.Count);
        Assert.Empty(vm.InstalledPlugins);

        var item = vm.AvailablePlugins.Single(p => p.Id == "com.test.one");
        await vm.InstallPluginAsync(item);

        // Plugin One should no longer be in AvailablePlugins
        Assert.DoesNotContain(vm.AvailablePlugins, p => p.Id == "com.test.one");
        Assert.Equal(1, vm.AvailableCount);

        // Plugin Two should still be available
        Assert.Contains(vm.AvailablePlugins, p => p.Id == "com.test.two");
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-009 — Install/Update Button State Transitions               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Action Text when not installed returns install.
    /// </summary>
    [Fact]
    public void ActionText_WhenNotInstalled_ReturnsInstall()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.Equal("Install", item.ActionText);
    }

    /// <summary>
    /// Verifies that Action Text when installed returns uninstall.
    /// </summary>
    [Fact]
    public void ActionText_WhenInstalled_ReturnsUninstall()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        Assert.Equal("Uninstall", item.ActionText);
    }

    /// <summary>
    /// Verifies that Action Text when has update returns update.
    /// </summary>
    [Fact]
    public void ActionText_WhenHasUpdate_ReturnsUpdate()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        item.HasUpdate = true;
        Assert.Equal("Update", item.ActionText);
    }

    /// <summary>
    /// Verifies that Action Text when busy installing returns installing.
    /// </summary>
    [Fact]
    public void ActionText_WhenBusyInstalling_ReturnsInstalling()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        item.SetBusyAction("Installing...");
        item.IsBusy = true;
        Assert.Equal("Installing...", item.ActionText);
    }

    /// <summary>
    /// Verifies that Action Text when busy updating returns updating.
    /// </summary>
    [Fact]
    public void ActionText_WhenBusyUpdating_ReturnsUpdating()
    {
        var item = PluginItemViewModel.FromPluginInfo(MakePluginInfo());
        item.HasUpdate = true;
        item.SetBusyAction("Updating...");
        item.IsBusy = true;
        Assert.Equal("Updating...", item.ActionText);
    }

    /// <summary>
    /// Verifies that Is Action Enabled when busy returns false.
    /// </summary>
    [Fact]
    public void IsActionEnabled_WhenBusy_ReturnsFalse()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.True(item.IsActionEnabled);
        item.IsBusy = true;
        Assert.False(item.IsActionEnabled);
    }

    /// <summary>
    /// Verifies that Action Text when busy cleared reverts to original.
    /// </summary>
    [Fact]
    public void ActionText_WhenBusyCleared_RevertsToOriginal()
    {
        var item = PluginItemViewModel.FromRegistryEntry(MakeEntry());
        Assert.Equal("Install", item.ActionText);

        item.SetBusyAction("Installing...");
        item.IsBusy = true;
        Assert.Equal("Installing...", item.ActionText);

        item.IsBusy = false;
        Assert.Equal("Install", item.ActionText);
    }
}