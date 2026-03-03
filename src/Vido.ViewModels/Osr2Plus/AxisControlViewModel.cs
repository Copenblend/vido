using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Haptics;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Settings;
using Vido.Services.Osr2Plus;

namespace Vido.ViewModels.Osr2Plus;

/// <summary>
/// ViewModel for the axis control panel. Manages all four axis cards,
/// persists axis settings, and orchestrates funscript auto-loading.
/// Supports funscript suppression via <see cref="SuppressFunscriptEvent"/>.
/// </summary>
public class AxisControlViewModel : INotifyPropertyChanged
{
    private readonly TCodeService _tcode;
    private readonly ISettingsService _settingsService;
    private readonly FunscriptParser _parser;
    private readonly FunscriptMatcher _matcher;
    private readonly List<AxisConfig> _configs;
    private readonly Dictionary<string, FunscriptData> _loadedScripts = new(4, StringComparer.OrdinalIgnoreCase);
    private bool _isVideoPlaying;
    private bool _isDeviceConnected;
    private bool _isTesting;
    private bool _funscriptsSuppressed;
    private string? _currentVideoPath;

    /// <summary>The four axis cards: L0, R0, R1, R2.</summary>
    public ObservableCollection<AxisCardViewModel> AxisCards { get; }

    /// <summary>
    /// Raised when loaded scripts change (load, clear, or manual override).
    /// Carries the current set of loaded scripts.
    /// </summary>
    public event Action<Dictionary<string, FunscriptData>>? ScriptsChanged;

    /// <summary>
    /// Raised when any axis configuration changes (min/max/enabled/fill settings).
    /// </summary>
    public event Action? AxisConfigChanged;

    /// <summary>Whether test mode is currently active.</summary>
    public bool IsTesting
    {
        get => _isTesting;
        private set
        {
            if (_isTesting != value)
            {
                _isTesting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TestButtonText));
            }
        }
    }

    /// <summary>Test button display text.</summary>
    public string TestButtonText => IsTesting ? "Stop" : "Test";

    /// <summary>Whether the test button is enabled.</summary>
    public bool IsTestEnabled => _isDeviceConnected && !_isVideoPlaying;

    /// <summary>Toggles test mode for all configured axes.</summary>
    public ICommand TestCommand { get; }

    /// <summary>
    /// Injectable delegates for testing. Defaults use real implementations.
    /// </summary>
    internal Func<string, Dictionary<string, string>>? FindMatchingScriptsFunc { get; set; }

    /// <summary>Injectable multi-axis parse delegate.</summary>
    internal Func<string, Dictionary<string, FunscriptData>?>? TryParseMultiAxisFunc { get; set; }

    /// <summary>Injectable single-file parse delegate.</summary>
    internal Func<string, string, FunscriptData>? ParseFileFunc { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AxisControlViewModel"/> class.
    /// </summary>
    /// <param name="tcode">The TCode service for sending commands and managing test mode.</param>
    /// <param name="settingsService">Settings service for persisting axis configurations.</param>
    /// <param name="parser">Funscript parser for loading script files.</param>
    /// <param name="matcher">Funscript matcher for finding axis-specific script files.</param>
    public AxisControlViewModel(
        TCodeService tcode,
        ISettingsService settingsService,
        FunscriptParser parser,
        FunscriptMatcher matcher)
    {
        _tcode = tcode;
        _settingsService = settingsService;
        _parser = parser;
        _matcher = matcher;

        // Set up default delegates (can be overridden for testing)
        FindMatchingScriptsFunc = _matcher.FindMatchingScripts;
        TryParseMultiAxisFunc = _parser.TryParseMultiAxis;
        ParseFileFunc = _parser.ParseFile;

        // Test command
        TestCommand = new RelayCommand(ExecuteTest);
        _tcode.AllTestsStopped += OnAllTestsStopped;

        // Create axis configs from defaults, then load persisted settings
        _configs = AxisConfig.CreateDefaults();
        LoadSettings();

        // Push configs to TCodeService
        _tcode.SetAxisConfigs(_configs);

        // Create axis card ViewModels
        AxisCards = new ObservableCollection<AxisCardViewModel>();
        foreach (var config in _configs)
        {
            var card = new AxisCardViewModel(config, _tcode);
            card.ParseFileFunc = ParseFileFunc;
            card.ConfigChanged += OnCardConfigChanged;
            AxisCards.Add(card);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Script Loading
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Orchestrates funscript auto-loading for a video file.
    /// 1. Tries multi-axis format on the base funscript.
    /// 2. Falls back to individual axis-tagged files via FunscriptMatcher.
    /// 3. Updates each card's script and pushes to TCodeService.
    /// Skipped when funscripts are suppressed via <see cref="SuppressFunscriptEvent"/>.
    /// </summary>
    /// <param name="videoPath">Path to the video file to match scripts for.</param>
    public void LoadScriptsForVideo(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
            return;

        _currentVideoPath = videoPath;

        // When funscripts are suppressed, skip auto-loading entirely
        if (_funscriptsSuppressed)
            return;

        // Find matching individual scripts
        var matchedScripts = FindMatchingScriptsFunc!(videoPath);

        // Try multi-axis on the base funscript (L0) first
        Dictionary<string, FunscriptData>? multiAxisData = null;
        if (matchedScripts.TryGetValue("L0", out var basePath))
        {
            multiAxisData = TryParseMultiAxisFunc!(basePath);
        }

        _loadedScripts.Clear();

        foreach (var card in AxisCards)
        {
            // Skip manual overrides
            if (card.IsScriptManual)
            {
                // Keep existing manual script in loadedScripts if present
                if (card.ScriptFileName != null)
                {
                    try
                    {
                        var data = ParseFileFunc!(card.ScriptFileName, card.AxisId);
                        _loadedScripts[card.AxisId] = data;
                    }
                    catch { /* manual file missing — ignore */ }
                }
                continue;
            }

            // Try multi-axis data first
            if (multiAxisData != null && multiAxisData.TryGetValue(card.AxisId, out var multiData))
            {
                card.SetAutoLoadedScript(basePath);
                _loadedScripts[card.AxisId] = multiData;
                continue;
            }

            // Fall back to individual axis file
            if (matchedScripts.TryGetValue(card.AxisId, out var axisPath))
            {
                try
                {
                    var data = ParseFileFunc!(axisPath, card.AxisId);
                    card.SetAutoLoadedScript(axisPath);
                    _loadedScripts[card.AxisId] = data;
                }
                catch { /* parse error — skip axis */ }
            }
            else
            {
                card.ClearAutoLoadedScript();
            }
        }

        // Push all loaded scripts to TCodeService
        _tcode.SetScripts(_loadedScripts);
        ScriptsChanged?.Invoke(_loadedScripts);
    }

    /// <summary>
    /// Clears all auto-loaded scripts (respects manual overrides).
    /// </summary>
    public void ClearScripts()
    {
        _currentVideoPath = null;
        _loadedScripts.Clear();

        foreach (var card in AxisCards)
        {
            if (card.IsScriptManual && card.ScriptFileName != null)
            {
                // Keep manual scripts
                try
                {
                    var data = ParseFileFunc!(card.ScriptFileName, card.AxisId);
                    _loadedScripts[card.AxisId] = data;
                }
                catch { /* manual file missing */ }
            }
            else
            {
                card.ClearAutoLoadedScript();
            }
        }

        _tcode.SetScripts(_loadedScripts);
        ScriptsChanged?.Invoke(_loadedScripts);
    }

    /// <summary>
    /// Force-clears ALL scripts including manual overrides.
    /// </summary>
    public void ClearAllScripts()
    {
        foreach (var card in AxisCards)
            card.ClearAllScripts();

        _loadedScripts.Clear();
        _tcode.SetScripts(_loadedScripts);
        ScriptsChanged?.Invoke(_loadedScripts);
    }

    /// <summary>
    /// Handles <see cref="SuppressFunscriptEvent"/> from the event bus.
    /// When suppressed, clears all loaded scripts and prevents auto-loading.
    /// When unsuppressed, allows auto-loading to resume.
    /// </summary>
    /// <param name="e">The suppress funscript event.</param>
    public void OnSuppressFunscript(SuppressFunscriptEvent e)
    {
        _funscriptsSuppressed = e.SuppressFunscripts;

        if (_funscriptsSuppressed)
        {
            // Clear all loaded scripts when suppression is activated
            ClearAllScripts();
        }
        else
        {
            // Suppression lifted — re-load funscripts for the current video
            // so the BeatBar and haptics resume without requiring a new video load.
            if (!string.IsNullOrEmpty(_currentVideoPath))
                LoadScriptsForVideo(_currentVideoPath);
        }
    }

    /// <summary>Whether funscript auto-loading is currently suppressed.</summary>
    public bool IsFunscriptsSuppressed => _funscriptsSuppressed;

    // ═══════════════════════════════════════════════════════
    //  State Updates (called by coordinator logic)
    // ═══════════════════════════════════════════════════════

    /// <summary>Updates video playing state. Disables test, stops test axes when playing.</summary>
    /// <param name="playing">Whether a video is currently playing.</param>
    public void SetVideoPlaying(bool playing)
    {
        if (_isVideoPlaying != playing)
        {
            _isVideoPlaying = playing;
            OnPropertyChanged(nameof(IsTestEnabled));
        }

        // Stop all test axes when video starts playing
        if (playing)
        {
            _tcode.StopAllTestAxes();
            IsTesting = false;
        }
    }

    /// <summary>Updates device connection state. Stops test mode on disconnect.</summary>
    /// <param name="connected">Whether the device is connected.</param>
    public void SetDeviceConnected(bool connected)
    {
        if (_isDeviceConnected != connected)
        {
            _isDeviceConnected = connected;
            OnPropertyChanged(nameof(IsTestEnabled));
        }

        // Stop all test axes when device disconnects
        if (!connected)
        {
            _tcode.StopAllTestAxes();
            IsTesting = false;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Settings Persistence
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Loads persisted axis settings from the settings service.
    /// Called during construction.
    /// </summary>
    internal void LoadSettings()
    {
        var axisSettings = _settingsService.Current.Osr2AxisSettings;
        foreach (var config in _configs)
        {
            if (!axisSettings.TryGetValue(config.Id, out var data))
                continue;

            config.Min = data.Min;
            config.Max = data.Max;
            config.Enabled = data.Enabled;

            if (Enum.TryParse<AxisFillMode>(data.FillMode, out var fillMode))
                config.FillMode = fillMode;

            config.SyncWithStroke = data.SyncWithStroke;
            config.FillSpeedHz = data.FillSpeedHz;
            config.PositionOffset = data.PositionOffset;
        }
    }

    /// <summary>
    /// Saves all axis settings to the settings service.
    /// Called whenever a card's config changes.
    /// </summary>
    internal void SaveSettings()
    {
        var axisSettings = _settingsService.Current.Osr2AxisSettings;
        foreach (var config in _configs)
        {
            if (!axisSettings.TryGetValue(config.Id, out var data))
            {
                data = new AxisSettingsData();
                axisSettings[config.Id] = data;
            }

            data.Min = config.Min;
            data.Max = config.Max;
            data.Enabled = config.Enabled;
            data.FillMode = config.FillMode.ToString();
            data.SyncWithStroke = config.SyncWithStroke;
            data.FillSpeedHz = config.FillSpeedHz;
            data.PositionOffset = config.PositionOffset;
        }
        _settingsService.QueueSave();
    }

    // ═══════════════════════════════════════════════════════
    //  Event Handlers
    // ═══════════════════════════════════════════════════════

    private void OnCardConfigChanged()
    {
        // Re-push configs to TCodeService and persist
        _tcode.SetAxisConfigs(_configs);
        SaveSettings();
        AxisConfigChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════
    //  Test Mode
    // ═══════════════════════════════════════════════════════

    private void ExecuteTest()
    {
        if (IsTesting)
        {
            _tcode.StopAllTestAxes();
            IsTesting = false;
        }
        else
        {
            if (!IsTestEnabled) return;

            // Start test on each enabled axis
            foreach (var config in _configs)
            {
                if (!config.Enabled) continue;

                // Start all enabled axes — even those with FillMode.None.
                // The output loop skips None fills but keeps the axis in the
                // test set, so switching fill mode mid-test takes effect immediately.
                _tcode.StartTestAxis(config.Id, config.FillSpeedHz);
            }

            IsTesting = true;
        }
    }

    private void OnAllTestsStopped()
    {
        IsTesting = false;
    }

    // ═══════════════════════════════════════════════════════
    //  INotifyPropertyChanged
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises the <see cref="PropertyChanged"/> event.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
