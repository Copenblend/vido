using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Settings;
using Vido.Services.Osr2Plus;

namespace Vido.ViewModels.Osr2Plus;

/// <summary>
/// ViewModel for the axis control panel. Manages all four axis cards,
/// persists axis settings, and orchestrates funscript auto-loading.
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
    private string? _currentVideoPath;
    private IFillProfileService? _profileService;
    private FillProfile? _selectedProfile;
    private bool _isProfileModified;
    private bool _suppressConfigChanged;

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

    /// <summary>Raised to request a profile name from the view (save operation).</summary>
    public event EventHandler? RequestProfileName;

    /// <summary>Raised to request a new name from the view (rename operation).</summary>
    public event EventHandler? RequestProfileRename;

    /// <summary>Command to save the current axis settings as a profile.</summary>
    public ICommand SaveProfileCommand { get; }

    /// <summary>Command to delete the selected user profile.</summary>
    public ICommand DeleteProfileCommand { get; }

    /// <summary>Command to rename the selected user profile.</summary>
    public ICommand RenameProfileCommand { get; }

    /// <summary>The currently selected fill profile.</summary>
    public FillProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile != value)
            {
                _selectedProfile = value;
                OnPropertyChanged();
                OnSelectedProfileChanged(value);
            }
        }
    }

    /// <summary>Whether the current axis settings have diverged from the selected profile.</summary>
    public bool IsProfileModified
    {
        get => _isProfileModified;
        private set
        {
            if (_isProfileModified != value)
            {
                _isProfileModified = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Ordered list of available profiles for dropdown binding.</summary>
    public IReadOnlyList<FillProfile> AvailableProfiles =>
        _profileService?.Profiles ?? Array.Empty<FillProfile>();

    /// <summary>Whether the selected profile is user-created (enables delete/rename).</summary>
    public bool CanDeleteSelectedProfile =>
        _selectedProfile is not null && !_selectedProfile.IsBuiltIn;

    /// <summary>Whether the selected profile can be renamed.</summary>
    public bool CanRenameSelectedProfile =>
        _selectedProfile is not null && !_selectedProfile.IsBuiltIn;

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

        // Profile commands
        SaveProfileCommand = new RelayCommand(SaveProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        RenameProfileCommand = new RelayCommand(RenameProfile);

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
            card.ScriptCleared += OnCardScriptCleared;
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
    /// </summary>
    /// <param name="videoPath">Path to the video file to match scripts for.</param>
    public void LoadScriptsForVideo(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
            return;

        _currentVideoPath = videoPath;

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
        if (_suppressConfigChanged) return;

        // Re-push configs to TCodeService and persist
        _tcode.SetAxisConfigs(_configs);
        SaveSettings();
        AxisConfigChanged?.Invoke();

        // Check if settings still match the selected profile
        if (_selectedProfile is not null)
        {
            var currentAxes = CaptureCurrentAxes();
            IsProfileModified = !_selectedProfile.MatchesAxes(currentAxes);
        }
    }

    private void OnCardScriptCleared(string axisId)
    {
        _loadedScripts.Remove(axisId);
        _tcode.ClearAxisScript(axisId);
        ScriptsChanged?.Invoke(_loadedScripts);
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
    //  Profile Management
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Sets the fill profile service. Called from MainWindow after construction.
    /// </summary>
    /// <param name="profileService">The fill profile service to use.</param>
    public void SetProfileService(IFillProfileService profileService)
    {
        _profileService = profileService;
        _profileService.ProfilesChanged += () =>
        {
            OnPropertyChanged(nameof(AvailableProfiles));
        };
        OnPropertyChanged(nameof(AvailableProfiles));

        // Auto-select Default profile on startup — show it in dropdown
        // but don't apply its values (let persisted settings take precedence)
        if (_selectedProfile is null)
        {
            var defaultProfile = _profileService.FindByName("Default");
            if (defaultProfile is not null)
            {
                _selectedProfile = defaultProfile;
                OnPropertyChanged(nameof(SelectedProfile));
                OnPropertyChanged(nameof(CanDeleteSelectedProfile));
                OnPropertyChanged(nameof(CanRenameSelectedProfile));

                var currentAxes = CaptureCurrentAxes();
                IsProfileModified = !defaultProfile.MatchesAxes(currentAxes);
            }
        }
    }

    /// <summary>
    /// Captures the current axis card settings as a dictionary of <see cref="FillAxisSettings"/>.
    /// </summary>
    public Dictionary<string, FillAxisSettings> CaptureCurrentAxes()
    {
        var axes = new Dictionary<string, FillAxisSettings>();
        foreach (var card in AxisCards)
        {
            axes[card.AxisId] = new FillAxisSettings
            {
                Enabled = card.Enabled,
                Min = card.Min,
                Max = card.Max,
                FillMode = card.FillMode.ToString(),
                SyncWithStroke = card.SyncWithStroke,
                FillSpeedHz = card.FillSpeedHz,
            };
        }
        return axes;
    }

    private void OnSelectedProfileChanged(FillProfile? value)
    {
        if (value is null)
        {
            IsProfileModified = false;
            OnPropertyChanged(nameof(CanDeleteSelectedProfile));
            OnPropertyChanged(nameof(CanRenameSelectedProfile));
            return;
        }

        ApplyProfile(value);
        IsProfileModified = false;
        OnPropertyChanged(nameof(CanDeleteSelectedProfile));
        OnPropertyChanged(nameof(CanRenameSelectedProfile));
    }

    private void ApplyProfile(FillProfile profile)
    {
        _suppressConfigChanged = true;
        try
        {
            foreach (var card in AxisCards)
            {
                if (!profile.Axes.TryGetValue(card.AxisId, out var axisSettings))
                    continue;

                card.Enabled = axisSettings.Enabled;
                card.Min = axisSettings.Min;
                card.Max = axisSettings.Max;

                if (Enum.TryParse<AxisFillMode>(axisSettings.FillMode, true, out var mode))
                    card.FillMode = mode;

                card.SyncWithStroke = axisSettings.SyncWithStroke;
                card.FillSpeedHz = axisSettings.FillSpeedHz;
            }
        }
        finally
        {
            _suppressConfigChanged = false;
        }

        // Fire a single config change after all axes are updated
        OnCardConfigChanged();
    }

    /// <summary>
    /// Called by the view after the user provides a profile name for saving.
    /// Creates a new profile or updates an existing user profile.
    /// </summary>
    /// <param name="name">The profile name.</param>
    public void CompleteSaveProfile(string name)
    {
        if (_profileService is null) return;

        var axes = CaptureCurrentAxes();
        try
        {
            var existing = _profileService.FindByName(name);
            if (existing is not null && !existing.IsBuiltIn)
            {
                _profileService.UpdateProfile(name, axes);
                OnPropertyChanged(nameof(AvailableProfiles));
                SelectedProfile = _profileService.FindByName(name);
            }
            else
            {
                var profile = _profileService.CreateProfile(name, axes);
                OnPropertyChanged(nameof(AvailableProfiles));
                SelectedProfile = profile;
            }
            IsProfileModified = false;
        }
        catch (ArgumentException)
        {
            // Name validation failed — view handles error display
        }
    }

    /// <summary>
    /// Called by the view after the user provides a new name for renaming.
    /// </summary>
    /// <param name="newName">The new profile name.</param>
    public void CompleteRenameProfile(string newName)
    {
        if (_profileService is null || _selectedProfile is null || _selectedProfile.IsBuiltIn)
            return;

        try
        {
            _profileService.RenameProfile(_selectedProfile.Name, newName);
            OnPropertyChanged(nameof(AvailableProfiles));
            SelectedProfile = _profileService.FindByName(newName);
        }
        catch (ArgumentException)
        {
            // Name validation failed — view handles error display
        }
    }

    private void SaveProfile()
    {
        RequestProfileName?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteProfile()
    {
        if (_profileService is null || _selectedProfile is null || _selectedProfile.IsBuiltIn)
            return;

        _profileService.DeleteProfile(_selectedProfile.Name);
        SelectedProfile = null;
        OnPropertyChanged(nameof(AvailableProfiles));
        IsProfileModified = false;
    }

    private void RenameProfile()
    {
        RequestProfileRename?.Invoke(this, EventArgs.Empty);
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
