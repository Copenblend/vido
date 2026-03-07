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

    /// <summary>
    /// Current search filter text.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// All settings categories, unfiltered.
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
    /// Creates the settings view model, building all setting categories
    /// and applying the initial filter.
    /// </summary>
    /// <param name="settingsService">Service for reading and persisting application settings.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="settingsService"/> is null.</exception>
    private readonly ISettingsStore? _settingsStore;

    public SettingsViewModel(ISettingsService settingsService, ISettingsStore? settingsStore = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsStore = settingsStore;

        BuildAppSettings();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(NoResults));
    }

    /// <summary>
    /// Reference to the screenshot directory setting item for conditional visibility.
    /// </summary>
    private SettingDisplayItem? _screenshotDirectoryItem;

    /// <summary>
    /// Reference to the screenshot enabled setting item for observing changes.
    /// </summary>
    private SettingDisplayItem? _screenshotEnabledItem;

    /// <summary>
    /// Updates screenshot directory visibility based on the screenshot enabled toggle.
    /// </summary>
    private void UpdateScreenshotDirectoryVisibility()
    {
        if (_screenshotDirectoryItem is null || _screenshotEnabledItem is null) return;
        _screenshotDirectoryItem.IsSettingVisible =
            _screenshotEnabledItem.SelectedBooleanValue.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates all settings categories: Playback, File Explorer, Screenshot, OSR2+, Pulse, Playlists.
    /// Each setting is defined as a <see cref="SettingDefinition"/> with compile-time-safe
    /// getter/setter delegates for direct <see cref="AppSettings"/> property access.
    /// </summary>
    private void BuildAppSettings()
    {
        // ── General ──
        var generalDefinitions = new List<SettingDefinition>
        {
            new(
                Key: "general.toastDuration",
                Type: "number",
                DefaultValue: 3.0,
                Title: "Toast Notification Duration",
                Description: "How long toast notifications are displayed before auto-dismissing (seconds).",
                Validation: new SettingValidation(Min: 1.0, Max: 10.0),
                Getter: s => s.ToastDurationSeconds,
                Setter: (s, v) => s.ToastDurationSeconds = Math.Clamp(Convert.ToDouble(v), 1.0, 10.0)),
        };

        var generalItems = generalDefinitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("General", generalItems));

        // ── Playback ──
        var playbackDefinitions = new List<SettingDefinition>
        {
            new(
                Key: "playback.volume",
                Type: "number",
                DefaultValue: 50.0,
                Title: "Default Volume",
                Description: "Default volume level when opening a new video (0\u2013100).",
                Getter: s => s.Volume * 100,
                Setter: (s, v) => s.Volume = Convert.ToDouble(v ?? 50.0) / 100.0),
            new(
                Key: "playback.speed",
                Type: "enum",
                DefaultValue: "1.0x",
                Title: "Default Playback Speed",
                Description: "Default playback speed for new videos.",
                EnumValues: ["0.25x", "0.5x", "1.0x", "1.5x", "2.0x"],
                Getter: s => $"{s.PlaybackSpeed}x",
                Setter: (s, v) =>
                {
                    var str = v?.ToString() ?? "1.0x";
                    if (double.TryParse(str.TrimEnd('x'), out var speed))
                        s.PlaybackSpeed = speed;
                }),
            new(
                Key: "playback.loop",
                Type: "boolean",
                DefaultValue: false,
                Title: "Loop Playback",
                Description: "Automatically loop videos when they reach the end.",
                Getter: s => s.LoopPlayback,
                Setter: (s, v) => s.LoopPlayback = v is true),
            new(
                Key: "playback.fullscreenAutoHide",
                Type: "number",
                DefaultValue: 3.0,
                Title: "Fullscreen Auto-Hide Delay",
                Description: "Seconds of mouse inactivity before fullscreen controls hide automatically.",
                Validation: new SettingValidation(Min: 1.0, Max: 30.0),
                Getter: s => s.FullscreenAutoHideSeconds,
                Setter: (s, v) => s.FullscreenAutoHideSeconds = Math.Clamp(Convert.ToDouble(v), 1.0, 30.0)),
            new(
                Key: "playback.fullscreenShowVideoName",
                Type: "boolean",
                DefaultValue: true,
                Title: "Show Video Name in Fullscreen",
                Description: "Display the current video filename in the fullscreen overlay.",
                Getter: s => s.FullscreenShowVideoName,
                Setter: (s, v) => s.FullscreenShowVideoName = v is true),
            new(
                Key: "playback.resumePlaybackPrompt",
                Type: "boolean",
                DefaultValue: true,
                Title: "Resume Playback Prompt",
                Description: "Show a prompt to resume playback when re-opening a previously played video.",
                Getter: s => s.ResumePlaybackPrompt,
                Setter: (s, v) => s.ResumePlaybackPrompt = v is true),
        };

        var playbackItems = playbackDefinitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Playback", playbackItems));

        // ── File Explorer ──
        var explorerDefinitions = new List<SettingDefinition>
        {
            new(
                Key: "explorer.showHiddenFiles",
                Type: "boolean",
                DefaultValue: false,
                Title: "Show Hidden Files",
                Description: "Show files and folders that are normally hidden in the file explorer.",
                Getter: s => s.ShowHiddenFiles,
                Setter: (s, v) => s.ShowHiddenFiles = v is true),
        };

        var explorerItems = explorerDefinitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("File Explorer", explorerItems));

        // ── Screenshot ──
        var screenshotDefinitions = new List<SettingDefinition>
        {
            new(
                Key: "screenshot.enabled",
                Type: "boolean",
                DefaultValue: false,
                Title: "Enable Screenshot Capture",
                Description: "Show a camera button in the title bar for capturing full-window screenshots.",
                Getter: s => s.ScreenshotEnabled,
                Setter: (s, v) => s.ScreenshotEnabled = v is true),
            new(
                Key: "screenshot.directory",
                Type: "folderPath",
                DefaultValue: "",
                Title: "Screenshot Save Directory",
                Description: "Folder where screenshots are saved. Leave empty to use the default Pictures\\Screenshots folder.",
                Getter: s => s.ScreenshotDirectory,
                Setter: (s, v) => s.ScreenshotDirectory = v?.ToString() ?? string.Empty),
        };

        var screenshotItems = screenshotDefinitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Screenshot", screenshotItems));

        // Wire screenshot directory conditional visibility
        _screenshotEnabledItem = screenshotItems.FirstOrDefault(s => s.Id == "screenshot.enabled");
        _screenshotDirectoryItem = screenshotItems.FirstOrDefault(s => s.Id == "screenshot.directory");
        UpdateScreenshotDirectoryVisibility();
        if (_screenshotEnabledItem is not null)
        {
            _screenshotEnabledItem.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingDisplayItem.SelectedBooleanValue))
                    UpdateScreenshotDirectoryVisibility();
            };
        }

        // ── OSR2+ ──
        BuildOsr2PlusSettings();

        // ── Pulse ──
        BuildPulseSettings();

        // ── Playlists ──
        BuildPlaylistSettings();

        // ── Updates ──
        BuildUpdatesSettings();
    }

    /// <summary>
    /// Builds the OSR2+ settings category with connection, output, and visualizer settings.
    /// </summary>
    private void BuildOsr2PlusSettings()
    {
        var definitions = new List<SettingDefinition>
        {
            new(
                Key: "osr2.connectionMode",
                Type: "enum",
                DefaultValue: "UDP",
                Title: "Default Connection Mode",
                Description: "Select the default connection mode for the OSR2+ device.",
                Section: "Connection",
                EnumValues: ["UDP", "Serial"],
                Getter: s => s.Osr2ConnectionMode,
                Setter: (s, v) => s.Osr2ConnectionMode = v?.ToString() ?? "UDP"),
            new(
                Key: "osr2.udpPort",
                Type: "number",
                DefaultValue: 7777.0,
                Title: "Default UDP Port",
                Description: "UDP port number for device communication (default: 7777).",
                Section: "Connection",
                Getter: s => (double)s.Osr2UdpPort,
                Setter: (s, v) => s.Osr2UdpPort = (int)Convert.ToDouble(v ?? 7777.0)),
            new(
                Key: "osr2.baudRate",
                Type: "enum",
                DefaultValue: "115200",
                Title: "Default Baud Rate",
                Description: "Serial baud rate for device communication.",
                Section: "Connection",
                EnumValues: ["9600", "19200", "38400", "57600", "115200", "250000"],
                Getter: s => s.Osr2BaudRate.ToString(),
                Setter: (s, v) =>
                {
                    if (int.TryParse(v?.ToString(), out var rate))
                        s.Osr2BaudRate = rate;
                }),
            new(
                Key: "osr2.outputRate",
                Type: "number",
                DefaultValue: 100.0,
                Title: "TCode Output Rate (Hz)",
                Description: "How many TCode commands per second to send (30–200 Hz).",
                Section: "Output",
                Getter: s => (double)s.Osr2OutputRate,
                Setter: (s, v) => s.Osr2OutputRate = (int)Convert.ToDouble(v ?? 100.0)),
            new(
                Key: "osr2.globalOffset",
                Type: "number",
                DefaultValue: 0.0,
                Title: "Global Funscript Offset (ms)",
                Description: "Time offset applied to all funscript axes (−500 to +500 ms). Negative = earlier, Positive = later.",
                Section: "Output",
                Getter: s => (double)s.Osr2GlobalOffset,
                Setter: (s, v) => s.Osr2GlobalOffset = (int)Convert.ToDouble(v ?? 0.0)),
            new(
                Key: "osr2.visualizerWindowDuration",
                Type: "enum",
                DefaultValue: "60",
                Title: "Visualizer Window Duration",
                Description: "Duration of the funscript visualization window in seconds.",
                Section: "Visualizer",
                EnumValues: ["30", "60", "120", "300"],
                Getter: s => s.Osr2VisualizerWindowDuration.ToString(),
                Setter: (s, v) =>
                {
                    if (int.TryParse(v?.ToString(), out var dur))
                        s.Osr2VisualizerWindowDuration = dur;
                }),
        };

        var items = definitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("OSR2+", items));
    }

    /// <summary>
    /// Builds the Pulse settings category with beat detection and waveform settings.
    /// </summary>
    private void BuildPulseSettings()
    {
        var definitions = new List<SettingDefinition>
        {
            new(
                Key: "pulse.beatSensitivity",
                Type: "number",
                DefaultValue: 1.5,
                Title: "Beat Detection Sensitivity",
                Description: "Sensitivity multiplier for audio beat detection (0.5–5.0). Higher = more sensitive.",
                Section: "Beat Detection",
                Getter: s => s.PulseBeatSensitivity,
                Setter: (s, v) => s.PulseBeatSensitivity = Convert.ToDouble(v ?? 1.5)),
            new(
                Key: "pulse.enableBpmPhaseLock",
                Type: "boolean",
                DefaultValue: true,
                Title: "Enable BPM Phase Lock",
                Description: "Lock beat detection to a consistent BPM phase for more stable beat timing.",
                Section: "Beat Detection",
                Getter: s => s.PulseEnableBpmPhaseLock,
                Setter: (s, v) => s.PulseEnableBpmPhaseLock = v is true),
            new(
                Key: "pulse.waveformWindowDuration",
                Type: "enum",
                DefaultValue: "30",
                Title: "Waveform Window Duration",
                Description: "Duration of the waveform visualization window in seconds.",
                Section: "Visualizer",
                EnumValues: ["15", "30", "60", "120"],
                Getter: s => s.PulseWaveformWindowDuration.ToString(),
                Setter: (s, v) =>
                {
                    if (int.TryParse(v?.ToString(), out var dur))
                        s.PulseWaveformWindowDuration = dur;
                }),
        };

        var items = definitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Pulse", items));
    }

    /// <summary>
    /// Builds the Playlists settings category.
    /// </summary>
    private void BuildPlaylistSettings()
    {
        var definitions = new List<SettingDefinition>
        {
            new(
                Key: "playlist.autoSave",
                Type: "boolean",
                DefaultValue: false,
                Title: "Auto-Save Playlists",
                Description: "Automatically save playlist changes when items are added, removed, or reordered.",
                Getter: s => s.PlaylistAutoSave,
                Setter: (s, v) => s.PlaylistAutoSave = v is true),
        };

        var items = definitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Playlists", items));
    }

    /// <summary>
    /// Builds the Updates settings category with auto-check setting.
    /// </summary>
    private void BuildUpdatesSettings()
    {
        var definitions = new List<SettingDefinition>
        {
            new(
                Key: "updates.autocheck",
                Type: "boolean",
                DefaultValue: true,
                Title: "Auto-Check for Updates",
                Description: "Automatically check for updates a few seconds after startup.",
                Getter: s => s.AutoCheckUpdates,
                Setter: (s, v) => s.AutoCheckUpdates = v is true),
        };

        var items = definitions
            .Select(d => new SettingDisplayItem(d, _settingsService, _settingsStore))
            .ToList();
        AllCategories.Add(new SettingsCategoryViewModel("Updates", items));
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
                    new SettingsCategoryViewModel(category.Name, matchingItems));
            }
        }
    }
}