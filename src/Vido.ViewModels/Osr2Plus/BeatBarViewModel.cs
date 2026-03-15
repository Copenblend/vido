using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Settings;
using Vido.Services.Osr2Plus;

namespace Vido.ViewModels.Osr2Plus;

/// <summary>
/// ViewModel for the beat bar overlay and control bar ComboBox.
/// Manages mode selection (Off/OnPeak/OnValley/OnPeakAndValley/MidStroke),
/// beat detection, current playback time, and settings persistence.
/// </summary>
public class BeatBarViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly BeatDetectionService _beatDetection;

    private BeatBarMode _mode = BeatBarMode.Off;
    private double _currentTimeMs;
    private List<double> _beats = new();
    private FunscriptData? _currentScript;

    // Suppress settings save when loading from store
    private bool _suppressSave;

    // ── Properties ───────────────────────────────────────────

    /// <summary>
    /// Available modes for the ComboBox.
    /// </summary>
    public ObservableCollection<BeatBarMode> AvailableModes { get; } = new(BeatBarMode.BuiltInModes);

    /// <summary>
    /// The active beat bar mode. Bound to the control bar ComboBox.
    /// Persisted to settings.
    /// </summary>
    public BeatBarMode Mode
    {
        get => _mode;
        set
        {
            if (value == null!) return;
            if (Set(ref _mode, value))
            {
                if (!_suppressSave)
                {
                    _settingsService.Current.Osr2BeatBarMode = value.ToString();
                    _settingsService.QueueSave();
                }

                RedetectBeats();

                OnPropertyChanged(nameof(IsActive));
                ModeChanged?.Invoke(value);
                RepaintRequested?.Invoke();
            }
        }
    }

    /// <summary>
    /// True when the beat bar should be visible: mode is not Off and beats are loaded.
    /// </summary>
    public bool IsActive => _mode != BeatBarMode.Off && HasBeats;

    /// <summary>
    /// Current playback position in milliseconds. Updated at ~60Hz.
    /// </summary>
    public double CurrentTimeMs
    {
        get => _currentTimeMs;
        private set => Set(ref _currentTimeMs, value);
    }

    /// <summary>
    /// Sorted list of beat timestamps in milliseconds.
    /// Detected from the funscript based on the current mode.
    /// </summary>
    public List<double> Beats
    {
        get => _beats;
        private set
        {
            if (Set(ref _beats, value))
                OnPropertyChanged(nameof(HasBeats));
        }
    }

    /// <summary>
    /// True when at least one beat has been detected.
    /// </summary>
    public bool HasBeats => _beats.Count > 0;


    // ── Events ───────────────────────────────────────────────

    /// <summary>
    /// Raised when the overlay should repaint (time update, mode change, beat data change).
    /// </summary>
    public event Action? RepaintRequested;

    /// <summary>
    /// Raised when the mode changes. Used by the coordinator to toggle overlay visibility.
    /// </summary>
    public event Action<BeatBarMode>? ModeChanged;

    // ── Constructor ──────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="BeatBarViewModel"/> class.
    /// </summary>
    /// <param name="settingsService">Settings service for persisting beat bar mode.</param>
    /// <param name="beatDetection">Beat detection service for peak/valley detection.</param>
    public BeatBarViewModel(ISettingsService settingsService, BeatDetectionService beatDetection)
    {
        _settingsService = settingsService;
        _beatDetection = beatDetection;
        LoadSettings();
    }

    // ── Public Methods ───────────────────────────────────────

    /// <summary>
    /// Called when a new video is loaded and funscript data is available.
    /// Stores the script reference and detects beats based on the current mode.
    /// </summary>
    /// <param name="scriptData">The L0 funscript data for beat detection.</param>
    public void LoadBeats(FunscriptData? scriptData)
    {
        _currentScript = scriptData;
        RedetectBeats();
        OnPropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Called when the video is unloaded. Clears all beat data and
    /// the stored script reference.
    /// </summary>
    public void ClearBeats()
    {
        _currentScript = null;
        Beats = new List<double>();
        OnPropertyChanged(nameof(IsActive));
        RepaintRequested?.Invoke();
    }

    /// <summary>
    /// Called at ~60Hz during playback. Updates the current time and
    /// requests a repaint.
    /// </summary>
    /// <param name="positionMs">Current playback position in milliseconds.</param>
    public void UpdateTime(double positionMs)
    {
        CurrentTimeMs = positionMs;
        RepaintRequested?.Invoke();
    }

    // ── Settings Persistence ─────────────────────────────────

    private void LoadSettings()
    {
        var modeStr = _settingsService.Current.Osr2BeatBarMode;
        var resolved = BeatBarMode.BuiltInModes.FirstOrDefault(m => m.Id == modeStr);
        if (resolved != null)
        {
            _suppressSave = true;
            _mode = resolved;
            _suppressSave = false;
        }
    }

    // ── Private Helpers ──────────────────────────────────────

    /// <summary>
    /// Re-runs beat detection using the current mode and stored script data.
    /// </summary>
    private void RedetectBeats()
    {
        if (_mode == BeatBarMode.Off)
        {
            Beats = new List<double>();
        }
        else if (_mode == BeatBarMode.OnPeak)
        {
            Beats = _beatDetection.DetectBeats(_currentScript, BeatDetectionMode.OnPeak);
        }
        else if (_mode == BeatBarMode.OnValley)
        {
            Beats = _beatDetection.DetectBeats(_currentScript, BeatDetectionMode.OnValley);
        }
        else if (_mode == BeatBarMode.OnPeakAndValley)
        {
            Beats = _beatDetection.DetectBeats(_currentScript, BeatDetectionMode.OnPeakAndValley);
        }
        else if (_mode == BeatBarMode.MidStroke)
        {
            Beats = _beatDetection.DetectBeats(_currentScript, BeatDetectionMode.MidStroke);
        }
        RepaintRequested?.Invoke();
    }

    // ── INotifyPropertyChanged ───────────────────────────────

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises the <see cref="PropertyChanged"/> event.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Sets a field and raises <see cref="PropertyChanged"/> if the value changed.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
