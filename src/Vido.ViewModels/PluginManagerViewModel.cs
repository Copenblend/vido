using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Logging;
using Vido.Core.Plugin;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the Plugin Manager sidebar panel.
/// Manages registry fetching, search/filter, install/uninstall, and plugin state.
/// </summary>
public partial class PluginManagerViewModel : ObservableObject
{
    private readonly IPluginHost _pluginHost;
    private readonly IPluginInstaller _pluginInstaller;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;

    /// <summary>All plugin items (both installed and available).</summary>
    private readonly List<PluginItemViewModel> _allPlugins = [];

    /// <summary>Map of registry URL → display name for the dropdown.</summary>
    private readonly Dictionary<string, string> _registryNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Installed plugins filtered by search and registry.</summary>
    public ObservableCollection<PluginItemViewModel> InstalledPlugins { get; } = [];

    /// <summary>Available plugins filtered by search and registry.</summary>
    public ObservableCollection<PluginItemViewModel> AvailablePlugins { get; } = [];

    /// <summary>Registry source options for the dropdown.</summary>
    public ObservableCollection<string> RegistrySources { get; } = ["All"];

    /// <summary>Search query for filtering.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    /// <summary>Selected registry source.</summary>
    [ObservableProperty]
    private string _selectedRegistrySource = "All";

    partial void OnSelectedRegistrySourceChanged(string value) => ApplyFilter();

    /// <summary>Whether the registry is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Count of installed plugins (for badge).</summary>
    [ObservableProperty]
    private int _installedCount;

    /// <summary>Count of available plugins (for badge).</summary>
    [ObservableProperty]
    private int _availableCount;

    /// <summary>Whether the installed section is expanded.</summary>
    [ObservableProperty]
    private bool _isInstalledExpanded = true;

    /// <summary>Whether the available section is expanded.</summary>
    [ObservableProperty]
    private bool _isAvailableExpanded = true;

    /// <summary>
    /// Fired when a plugin item's detail should be opened in the main tab area.
    /// The string parameter is the plugin ID.
    /// </summary>
    public event Action<PluginItemViewModel>? OpenDetailRequested;

    /// <summary>
    /// Fired when a plugin item's settings should be opened.
    /// The string parameter is the plugin ID.
    /// </summary>
    public event Action<PluginItemViewModel>? OpenSettingsRequested;

    /// <summary>
    /// Fired when a plugin operation cannot complete immediately and a restart is needed.
    /// The parameter is a user-facing message describing why.
    /// </summary>
    public event Action<string>? RestartRequired;

    public PluginManagerViewModel(
        IPluginHost pluginHost,
        IPluginInstaller pluginInstaller,
        ISettingsService settingsService,
        ILogService logService)
    {
        _pluginHost = pluginHost;
        _pluginInstaller = pluginInstaller;
        _settingsService = settingsService;
        _logService = logService;

        // Restore collapsed/expanded state from persisted settings
        var s = _settingsService.Current;
        _isInstalledExpanded = s.PluginInstalledSectionExpanded;
        _isAvailableExpanded = s.PluginAvailableSectionExpanded;
    }

    partial void OnIsInstalledExpandedChanged(bool value)
    {
        _settingsService.Current.PluginInstalledSectionExpanded = value;
        _settingsService.QueueSave();
    }

    partial void OnIsAvailableExpandedChanged(bool value)
    {
        _settingsService.Current.PluginAvailableSectionExpanded = value;
        _settingsService.QueueSave();
    }

    /// <summary>
    /// Refreshes the plugin list by re-fetching registries.
    /// Resets the registry filter to "All".
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
        SelectedRegistrySource = "All";
    }

    /// <summary>
    /// Loads installed plugins from the plugin host and fetches registry data.
    /// Call this when the panel becomes visible.
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            _allPlugins.Clear();
            _registryNames.Clear();
            RegistrySources.Clear();
            RegistrySources.Add("All");

            // 0. Ensure plugins are activated before enumerating.
            //    ActivateAll is deferred at startup (runs after first render),
            //    so it may not have completed yet if the user opens the
            //    Extensions panel quickly. ActivateAll is idempotent — calling
            //    it again after it has already run is a no-op (seenIds guard).
            await Task.Run(() => _pluginHost.ActivateAll());

            // 1. Load installed plugins from the host
            var installedMap = new Dictionary<string, PluginItemViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in _pluginHost.Plugins)
            {
                var item = PluginItemViewModel.FromPluginInfo(info);
                installedMap[info.Manifest.Id] = item;
                _allPlugins.Add(item);
            }

            // 2. Fetch registries
            var registryUrls = _settingsService.Current.PluginRegistryUrls;
            var seenIds = new HashSet<string>(installedMap.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var url in registryUrls)
            {
                var registry = await _pluginInstaller.FetchRegistryAsync(url);
                if (registry is null) continue;

                var registryName = !string.IsNullOrWhiteSpace(registry.Name)
                    ? registry.Name
                    : new Uri(url).Host;

                _registryNames[url] = registryName;

                if (!RegistrySources.Contains(registryName))
                    RegistrySources.Add(registryName);

                var isOfficial = url.Equals(AppSettings.OfficialRegistryUrl, StringComparison.OrdinalIgnoreCase);

                foreach (var entry in registry.Plugins)
                {
                    entry.RegistryUrl = url;
                    entry.RegistryName = registryName;
                    entry.IsOfficial = isOfficial;

                    if (installedMap.TryGetValue(entry.Id, out var installed))
                    {
                        // Update installed plugin with registry info
                        installed.RegistryEntry = entry;
                        installed.IsOfficial = isOfficial;

                        // Detect available updates
                        if (installed.IsInstalled && IsNewerVersion(entry.Version, installed.Version))
                        {
                            installed.HasUpdate = true;
                            installed.AvailableVersion = entry.Version;
                        }
                    }
                    else if (seenIds.Add(entry.Id))
                    {
                        // New available plugin (first registry wins for dedup)
                        var item = PluginItemViewModel.FromRegistryEntry(entry);
                        _allPlugins.Add(item);
                    }
                }
            }

            // 3. Reconcile: plugins may have been discovered by ActivateAll
            //    while the registry fetch was awaiting (startup race).
            //    Link any host plugins to their existing available items,
            //    or add new installed items if they weren't in the registry.
            foreach (var info in _pluginHost.Plugins)
            {
                var id = info.Manifest.Id;

                // Already correctly linked?
                if (installedMap.ContainsKey(id))
                    continue;

                // Find the available item created from registry and upgrade it
                var existing = _allPlugins.FirstOrDefault(p =>
                    string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    existing.PluginInfo = info;
                    existing.IsInstalled = true;
                    existing.IsEnabled = info.State != PluginState.Disabled
                                      && info.State != PluginState.Error;

                    // The item was created from a registry entry, so its Version
                    // is the registry version. Compare the installed manifest
                    // version against the registry version for update detection.
                    var installedVersion = info.Manifest.Version;
                    var registryVersion = existing.RegistryEntry?.Version;
                    if (registryVersion is not null && IsNewerVersion(registryVersion, installedVersion))
                    {
                        existing.HasUpdate = true;
                        existing.AvailableVersion = registryVersion;
                    }
                }
                else
                {
                    // Not in registry at all — add as installed-only
                    var item = PluginItemViewModel.FromPluginInfo(info);
                    _allPlugins.Add(item);
                }
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to load plugin manager: {ex.Message}", "PluginManager");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Installs a plugin and updates the lists.
    /// </summary>
    [RelayCommand]
    public async Task InstallPluginAsync(PluginItemViewModel item)
    {
        if (item.IsInstalled || item.IsBusy || item.RegistryEntry is null) return;

        item.IsBusy = true;
        try
        {
            var success = await _pluginInstaller.InstallAsync(item.RegistryEntry);
            if (success)
            {
                item.IsInstalled = true;
                item.IsEnabled = true;

                // Try to activate the plugin immediately
                try
                {
                    // Ensure the plugin is not in the disabled list (may be
                    // stale from a previous session or prior uninstall)
                    _pluginHost.RemovePlugin(item.Id);

                    _pluginHost.ActivateAll(); // Will pick up the newly installed plugin
                    var info = _pluginHost.GetPlugin(item.Id);
                    if (info is not null)
                        item.PluginInfo = info;
                }
                catch (Exception ex)
                {
                    _logService.Warning($"Plugin '{item.Id}' installed but could not be activated: {ex.Message}", "PluginManager");
                    RestartRequired?.Invoke($"Plugin '{item.DisplayName}' was installed but could not be activated immediately. A restart is required.");
                }

                ApplyFilter();
                OpenDetailRequested?.Invoke(item);
                _logService.Info($"Plugin '{item.DisplayName}' installed successfully.", "PluginManager");
            }
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    /// <summary>
    /// Uninstalls a plugin and updates the lists.
    /// </summary>
    [RelayCommand]
    public async Task UninstallPluginAsync(PluginItemViewModel item)
    {
        if (!item.IsInstalled || item.IsBusy) return;

        item.IsBusy = true;
        try
        {
            // Remove all runtime state (deactivates, clears from _plugins and disabled list)
            _pluginHost.RemovePlugin(item.Id);

            var fullyRemoved = await _pluginInstaller.UninstallAsync(item.Id);

            item.IsInstalled = false;
            item.IsEnabled = false;
            item.PluginInfo = null;

            ApplyFilter();

            var msg = fullyRemoved
                ? $"Plugin '{item.DisplayName}' uninstalled."
                : $"Plugin '{item.DisplayName}' marked for removal on next restart.";
            _logService.Info(msg, "PluginManager");

            if (!fullyRemoved)
                RestartRequired?.Invoke($"Plugin '{item.DisplayName}' could not be fully removed. A restart is required to complete the uninstall.");
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    /// <summary>
    /// Toggles a plugin's enabled/disabled state.
    /// </summary>
    [RelayCommand]
    public void ToggleEnabled(PluginItemViewModel item)
    {
        if (!item.IsInstalled) return;

        var newState = !item.IsEnabled;
        _pluginHost.SetEnabled(item.Id, newState);
        item.IsEnabled = newState;

        _logService.Info($"Plugin '{item.DisplayName}' {(newState ? "enabled" : "disabled")}.", "PluginManager");
    }

    /// <summary>
    /// Opens the detail panel for a plugin.
    /// </summary>
    [RelayCommand]
    public void OpenDetail(PluginItemViewModel item)
    {
        OpenDetailRequested?.Invoke(item);
    }

    /// <summary>
    /// Opens the settings for a plugin.
    /// </summary>
    [RelayCommand]
    public void OpenPluginSettings(PluginItemViewModel item)
    {
        OpenSettingsRequested?.Invoke(item);
    }

    /// <summary>
    /// Updates a plugin to the latest version from the registry.
    /// Removes the old version, installs the new one, and re-activates.
    /// </summary>
    [RelayCommand]
    public async Task UpdatePluginAsync(PluginItemViewModel item)
    {
        if (!item.HasUpdate || item.IsBusy || item.RegistryEntry is null) return;

        item.IsBusy = true;
        try
        {
            _pluginHost.RemovePlugin(item.Id);

            var success = await _pluginInstaller.InstallAsync(item.RegistryEntry);
            if (success)
            {
                _pluginHost.ActivateAll();
                var info = _pluginHost.GetPlugin(item.Id);
                if (info is not null)
                    item.PluginInfo = info;
                item.HasUpdate = false;
                item.AvailableVersion = null;
                _logService.Info($"Plugin '{item.DisplayName}' updated successfully.", "PluginManager");
                RestartRequired?.Invoke($"Plugin '{item.DisplayName}' was updated. A restart is recommended.");
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to update plugin '{item.Id}': {ex.Message}", "PluginManager");
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    /// <summary>
    /// Compares two version strings. Returns true if <paramref name="latest"/> is newer than <paramref name="current"/>.
    /// </summary>
    internal static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) && Version.TryParse(current, out var currentVer))
            return latestVer > currentVer;
        return false; // Can't parse — assume no update
    }

    /// <summary>
    /// Applies search and registry filters to the plugin lists.
    /// </summary>
    private void ApplyFilter()
    {
        InstalledPlugins.Clear();
        AvailablePlugins.Clear();

        foreach (var item in _allPlugins)
        {
            // Search filter
            if (!item.MatchesSearch(SearchQuery))
                continue;

            // Registry filter
            if (SelectedRegistrySource != "All")
            {
                if (!string.Equals(item.RegistryName, SelectedRegistrySource, StringComparison.OrdinalIgnoreCase))
                {
                    // For installed plugins without a registry match, always show them
                    if (!item.IsInstalled)
                        continue;
                }
            }

            if (item.IsInstalled)
                InstalledPlugins.Add(item);
            else
                AvailablePlugins.Add(item);
        }

        InstalledCount = InstalledPlugins.Count;
        AvailableCount = AvailablePlugins.Count;
    }
}
