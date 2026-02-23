using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vido.Core.Plugin;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the Settings tab. Manages application settings grouped by category,
/// plugin settings gathered from active plugins, and search filtering.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IPluginHost? _pluginHost;
    private readonly IPluginSettingsStore _appSettingsStore;

    /// <summary>Current search filter text.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>All settings categories (app + plugin), unfiltered.</summary>
    public ObservableCollection<SettingsCategoryViewModel> AllCategories { get; } = [];

    /// <summary>Filtered settings categories (visible in the UI after search).</summary>
    public ObservableCollection<SettingsCategoryViewModel> FilteredCategories { get; } = [];

    /// <summary>
    /// Whether there are no results matching the current search.
    /// </summary>
    public bool NoResults => FilteredCategories.Count == 0 && !string.IsNullOrEmpty(SearchText);

    public SettingsViewModel(ISettingsService settingsService, IPluginSettingsStore appSettingsStore, IPluginHost? pluginHost = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
        _pluginHost = pluginHost;

        BuildAppSettings();
        BuildPluginSettings();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(NoResults));
    }

    /// <summary>
    /// Rebuilds plugin settings categories. Call after a plugin is
    /// enabled/disabled/installed/uninstalled.
    /// </summary>
    public void RefreshPluginSettings()
    {
        // Remove existing plugin categories
        for (int i = AllCategories.Count - 1; i >= 0; i--)
        {
            if (AllCategories[i].IsPlugin)
                AllCategories.RemoveAt(i);
        }

        BuildPluginSettings();
        ApplyFilter();
    }

    /// <summary>
    /// Creates app settings categories: Playback, File Explorer, Plugins.
    /// Each setting is defined as a <see cref="SettingContribution"/> and backed
    /// by the <see cref="AppSettingsStore"/>.
    /// </summary>
    private void BuildAppSettings()
    {
        // ── Playback ──
        var playbackDefinitions = new List<SettingContribution>
        {
            new()
            {
                Id = "playback.volume",
                Type = "number",
                Title = "Default Volume",
                Description = "Default volume level when opening a new video (0–100).",
                Default = 50.0
            },
            new()
            {
                Id = "playback.speed",
                Type = "enum",
                Title = "Default Playback Speed",
                Description = "Default playback speed for new videos.",
                Default = "1.0x",
                EnumValues = ["0.25x", "0.5x", "1.0x", "1.5x", "2.0x"]
            },
            new()
            {
                Id = "playback.loop",
                Type = "boolean",
                Title = "Loop Playback",
                Description = "Automatically loop videos when they reach the end.",
                Default = false
            },
        };

        var playbackItems = playbackDefinitions
            .Select(d => new SettingDisplayItem(d, _appSettingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Playback", playbackItems));

        // ── File Explorer ──
        var explorerDefinitions = new List<SettingContribution>
        {
            new()
            {
                Id = "explorer.showHiddenFiles",
                Type = "boolean",
                Title = "Show Hidden Files",
                Description = "Show files and folders that are normally hidden in the file explorer.",
                Default = false
            },
        };

        var explorerItems = explorerDefinitions
            .Select(d => new SettingDisplayItem(d, _appSettingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("File Explorer", explorerItems));

        // ── Plugins ──
        var pluginDefinitions = new List<SettingContribution>
        {
            new()
            {
                Id = "plugins.registryUrls",
                Type = "stringList",
                Title = "Plugin Registry URLs",
                Description = "Additional plugin registry URLs. Supports https:// and file:// for local testing. The official Vido registry is always included.",
                Default = null,
                Validation = "url"
            },
        };

        var pluginItems = pluginDefinitions
            .Select(d => new SettingDisplayItem(d, _appSettingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Plugins", pluginItems));
    }

    /// <summary>
    /// Adds a settings category for each active plugin that declares settings.
    /// </summary>
    private void BuildPluginSettings()
    {
        if (_pluginHost is null) return;

        foreach (var plugin in _pluginHost.Plugins)
        {
            if (plugin.State != PluginState.Active)
                continue;

            var settings = plugin.Manifest.Contributes.Settings;
            if (settings.Count == 0)
                continue;

            var store = _pluginHost.GetSettingsStore(plugin.Manifest.Id);
            var items = settings
                .Select(s => new SettingDisplayItem(s, store))
                .ToList();

            AllCategories.Add(new SettingsCategoryViewModel(
                plugin.Manifest.DisplayName, items, isPlugin: true));
        }
    }

    /// <summary>
    /// Filters <see cref="AllCategories"/> into <see cref="FilteredCategories"/>
    /// based on the current <see cref="SearchText"/>. An empty search shows all.
    /// Matches against setting title, description, and category name.
    /// </summary>
    private void ApplyFilter()
    {
        FilteredCategories.Clear();
        var search = SearchText?.Trim() ?? string.Empty;

        foreach (var category in AllCategories)
        {
            if (string.IsNullOrEmpty(search))
            {
                FilteredCategories.Add(category);
                continue;
            }

            // If the category name itself matches, include the entire category
            if (category.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredCategories.Add(category);
                continue;
            }

            // Otherwise, filter individual settings
            var matchingItems = category.Settings
                .Where(s =>
                    s.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingItems.Count > 0)
            {
                FilteredCategories.Add(
                    new SettingsCategoryViewModel(category.Name, matchingItems, category.IsPlugin));
            }
        }
    }
}
