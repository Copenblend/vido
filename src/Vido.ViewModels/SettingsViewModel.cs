using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the Settings tab. Manages application settings grouped by category
/// and search filtering.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISettingsStore _appSettingsStore;

    /// <summary>
    /// Current search filter text.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// All settings categories (app + plugin), unfiltered.
    /// </summary>
    public ObservableCollection<SettingsCategoryViewModel> AllCategories { get; } = [];

    /// <summary>
    /// Filtered settings categories (visible in the UI after search).
    /// </summary>
    public ObservableCollection<SettingsCategoryViewModel> FilteredCategories { get; } = [];

    /// <summary>
    /// Whether there are no results matching the current search.
    /// </summary>
    public bool NoResults => FilteredCategories.Count == 0 && !string.IsNullOrEmpty(SearchText);
    
    /// <summary>
    /// Creates the settings view model, building app setting categories
    /// and applying the initial filter.
    /// </summary>
    /// <param name="settingsService">Service for reading application-level settings.</param>
    /// <param name="appSettingsStore">Backing store for application settings values.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="settingsService"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="appSettingsStore"/> is null.</exception>
    public SettingsViewModel(ISettingsService settingsService, ISettingsStore appSettingsStore)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));

        BuildAppSettings();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(NoResults));
    }

    /// <summary>
    /// Reloads the registry URL list from the backing store so the UI
    /// reflects changes made outside the settings panel (e.g. "Enter Repository Code").
    /// </summary>
    public void RefreshRegistryUrls()
    {
        var item = AllCategories
            .SelectMany(c => c.Settings)
            .FirstOrDefault(s => s.Id == "plugins.registryUrls");
        item?.Reload();
    }

    /// <summary>
    /// Reference to the screenshot directory setting item for conditional visibility.
    /// </summary>
    private SettingDisplayItem? _screenshotDirectoryItem;

    private void OnAppSettingChanged(string key)
    {
        if (key.Equals("screenshot.enabled", StringComparison.OrdinalIgnoreCase))
        {
            UpdateScreenshotDirectoryVisibility();
        }
    }

    private void UpdateScreenshotDirectoryVisibility()
    {
        if (_screenshotDirectoryItem is null) return;
        _screenshotDirectoryItem.IsSettingVisible = _appSettingsStore.Get("screenshot.enabled", false);
    }

    /// <summary>
    /// Rebuilds plugin settings categories. Call after a plugin is
    /// enabled/disabled/installed/uninstalled.
    /// </summary>
    public void RefreshPluginSettings()
    {
        // No-op: plugin settings system removed.
    }

    /// <summary>
    /// Creates app settings categories: Playback, File Explorer, Plugins.
    /// Each setting is defined as a <see cref="SettingContribution"/> and backed
    /// by the <see cref="ISettingsStore"/>.
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

        // ── Screenshot ──
        var screenshotDefinitions = new List<SettingContribution>
        {
            new()
            {
                Id = "screenshot.enabled",
                Type = "boolean",
                Title = "Enable Screenshot Capture",
                Description = "Show a camera button in the title bar for capturing full-window screenshots.",
                Default = false
            },
            new()
            {
                Id = "screenshot.directory",
                Type = "folderPath",
                Title = "Screenshot Save Directory",
                Description = "Folder where screenshots are saved. Leave empty to use the default Pictures\\Screenshots folder.",
                Default = ""
            },
        };

        var screenshotItems = screenshotDefinitions
            .Select(d => new SettingDisplayItem(d, _appSettingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Screenshot", screenshotItems));

        // Set initial visibility of the directory setting based on the enabled flag
        _screenshotDirectoryItem = screenshotItems.FirstOrDefault(s => s.Id == "screenshot.directory");
        UpdateScreenshotDirectoryVisibility();

        // Listen for changes to toggle the directory setting visibility
        _appSettingsStore.SettingChanged += OnAppSettingChanged;

        // ── OSR2+ ──
        var osr2Definitions = new List<SettingContribution>
        {
            new()
            {
                Id = "osr2.connectionMode",
                Type = "enum",
                Title = "Default Connection Mode",
                Description = "Select the default connection mode for the OSR2+ device.",
                Default = "UDP",
                EnumValues = ["UDP", "Serial"],
                Section = "Connection"
            },
            new()
            {
                Id = "osr2.udpPort",
                Type = "number",
                Title = "Default UDP Port",
                Description = "UDP port number for device communication (default: 7777).",
                Default = 7777.0,
                Section = "Connection"
            },
            new()
            {
                Id = "osr2.baudRate",
                Type = "enum",
                Title = "Default Baud Rate",
                Description = "Serial baud rate for device communication.",
                Default = "115200",
                EnumValues = ["9600", "19200", "38400", "57600", "115200", "250000"],
                Section = "Connection"
            },
            new()
            {
                Id = "osr2.outputRate",
                Type = "number",
                Title = "TCode Output Rate (Hz)",
                Description = "How many TCode commands per second to send (30\u2013200 Hz).",
                Default = 100.0,
                Section = "Output"
            },
            new()
            {
                Id = "osr2.globalOffset",
                Type = "number",
                Title = "Global Funscript Offset (ms)",
                Description = "Time offset applied to all funscript axes (\u2212500 to +500 ms). Negative = earlier, Positive = later.",
                Default = 0.0,
                Section = "Output"
            },
            new()
            {
                Id = "osr2.visualizerWindowDuration",
                Type = "enum",
                Title = "Visualizer Window Duration",
                Description = "Duration of the funscript visualization window in seconds.",
                Default = "60",
                EnumValues = ["30", "60", "120", "300"],
                Section = "Visualizer"
            },
        };

        var osr2Items = osr2Definitions
            .Select(d => new SettingDisplayItem(d, _appSettingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("OSR2+", osr2Items));
    }

    /// <summary>
    /// Reserved for future plugin settings support. Currently a no-op.
    /// </summary>
    private void BuildPluginSettings()
    {
        // Plugin system removed — no dynamic settings to build.
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
