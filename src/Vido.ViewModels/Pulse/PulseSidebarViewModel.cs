using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Events;
using Vido.Core.Models.Pulse;
using Vido.Core.Settings;
using Vido.Services.Osr2Plus;
using Vido.Services.Playlists;
using Vido.Services.Pulse;

namespace Vido.ViewModels.Pulse;

/// <summary>
/// ViewModel for the Pulse sidebar panel — toggle switch, analysis progress,
/// BPM readout, state indicator, and description text. Persists settings
/// via <see cref="ISettingsService"/>.
/// </summary>
internal sealed class PulseSidebarViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly PulseEngine _engine;
    private readonly ISettingsService _settingsService;
    private readonly IToastService? _toastService;
    private readonly IEventBus _eventBus;
    private readonly Func<string, string, Task<bool>>? _confirmOverwrite;
    private readonly List<IDisposable> _subscriptions = [];

    private bool _usePulse;
    private PulseState _state;
    private double _currentBpm;
    private double _analysisProgress;
    private string _statusMessage = string.Empty;
    private string _statusBarText = "\u2665 Pulse: Off";
    private string? _errorMessage;
    private int _selectedBeatRateIndex;
    private int _selectedFunscriptBeatRateIndex;
    private double _amplitudeOffset;
    private double _easingBlend;
    private StrokePattern _strokePattern;
    private double _randomness;
    private bool _disposed;
    private string? _currentVideoPath;
    private bool _isGenerating;

    /// <summary>Raised when a property value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Initializes a new instance of the sidebar view model.</summary>
    /// <param name="engine">Pulse engine that provides state, progress, and BPM updates.</param>
    /// <param name="settingsService">Settings service for persisting Pulse preferences.</param>
    /// <param name="eventBus">Event bus for publishing/subscribing to application events.</param>
    /// <param name="toastService">Optional toast service for showing enable/disable notifications.</param>
    /// <param name="confirmOverwrite">Optional callback to confirm overwriting an existing funscript file.</param>
    public PulseSidebarViewModel(
        PulseEngine engine,
        ISettingsService settingsService,
        IEventBus eventBus,
        IToastService? toastService = null,
        Func<string, string, Task<bool>>? confirmOverwrite = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(eventBus);

        _engine = engine;
        _settingsService = settingsService;
        _eventBus = eventBus;
        _toastService = toastService;
        _confirmOverwrite = confirmOverwrite;

        // Load persisted state
        _usePulse = _settingsService.Current.PulseUsePulse;
        _selectedBeatRateIndex = _settingsService.Current.PulseBeatRateIndex;
        _selectedFunscriptBeatRateIndex = _settingsService.Current.PulseFunscriptBeatRateIndex;
        _amplitudeOffset = _settingsService.Current.PulseAmplitudeOffset;
        _easingBlend = _settingsService.Current.PulseEasingBlend;
        _strokePattern = Enum.TryParse<StrokePattern>(_settingsService.Current.PulseStrokePattern, out var pat) ? pat : StrokePattern.Classic;
        _randomness = _settingsService.Current.PulseRandomness;
        _state = _engine.State;

        _engine.StateChanged += OnEngineStateChanged;
        _engine.AnalysisProgress += OnAnalysisProgress;
        _engine.BeatMapReady += OnBeatMapReady;
        _engine.ErrorOccurred += OnErrorOccurred;

        _subscriptions.Add(_eventBus.Subscribe<VideoLoadedEvent>(e =>
        {
            _currentVideoPath = e.FilePath;
            OnPropertyChanged(nameof(CanGenerateFunscript));
        }));
        _subscriptions.Add(_eventBus.Subscribe<VideoUnloadedEvent>(_ =>
        {
            _currentVideoPath = null;
            OnPropertyChanged(nameof(CanGenerateFunscript));
        }));

        // Restore persisted enabled state — the engine starts inactive,
        // so we must call SetEnabled to register the beat source and
        // suppress funscripts when the user previously left Pulse on.
        if (_usePulse)
            _engine.SetEnabled(true);

        PropagateStrokeSettings();
        UpdateStatusMessage();
    }

    // ── Properties ──

    /// <summary>Main toggle — enables/disables Pulse.</summary>
    public bool UsePulse
    {
        get => _usePulse;
        set
        {
            if (_usePulse == value) return;
            _usePulse = value;
            OnPropertyChanged();
            _engine.SetEnabled(value);
            _settingsService.Current.PulseUsePulse = value;
            _settingsService.QueueSave();
            _toastService?.Show(value ? "Pulse enabled" : "Pulse disabled");
        }
    }

    /// <summary>Current engine state.</summary>
    public PulseState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnalyzing));
            OnPropertyChanged(nameof(ShowBpm));
            OnPropertyChanged(nameof(StateColor));
        }
    }

    /// <summary>Current detected BPM.</summary>
    public double CurrentBpm
    {
        get => _currentBpm;
        private set
        {
            if (Math.Abs(_currentBpm - value) < 0.01) return;
            _currentBpm = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Analysis progress 0.0–1.0.</summary>
    public double AnalysisProgress
    {
        get => _analysisProgress;
        private set
        {
            if (Math.Abs(_analysisProgress - value) < 0.005) return;
            _analysisProgress = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Human-readable status message.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Short status string for the application status bar.</summary>
    public string StatusBarText
    {
        get => _statusBarText;
        private set
        {
            if (_statusBarText == value) return;
            _statusBarText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the engine is currently analyzing (progress bar visible).</summary>
    public bool IsAnalyzing => _state == PulseState.Analyzing;

    /// <summary>Whether to show the BPM readout (Ready or Active).</summary>
    public bool ShowBpm => _state is PulseState.Ready or PulseState.Active;

    /// <summary>State indicator color key: "Green", "Yellow", "Grey", "Red".</summary>
    public string StateColor => _state switch
    {
        PulseState.Active => "Green",
        PulseState.Ready or PulseState.Analyzing => "Yellow",
        PulseState.Error => "Red",
        _ => "Grey"
    };

    // ── Beat Rate ──

    /// <summary>Display labels for the beat rate ComboBox.</summary>
    public static IReadOnlyList<string> BeatRateOptions { get; } = new[]
    {
        "Every Beat",
        "Every 2nd Beat",
        "Every 3rd Beat",
        "Every 4th Beat"
    };

    /// <summary>
    /// Selected beat rate index (0 = every beat, 1 = every 2nd, etc.).
    /// Maps to engine BeatDivisor = index + 1.
    /// </summary>
    public int SelectedBeatRateIndex
    {
        get => _selectedBeatRateIndex;
        set
        {
            if (_selectedBeatRateIndex == value) return;
            _selectedBeatRateIndex = value;
            OnPropertyChanged();
            _engine.BeatDivisor = value + 1;
            _settingsService.Current.PulseBeatRateIndex = value;
            _settingsService.QueueSave();
        }
    }

    /// <summary>
    /// Selected beat rate index for funscript generation (0 = every beat, 1 = every 2nd, etc.).
    /// Independent from the live playback beat rate.
    /// </summary>
    public int SelectedFunscriptBeatRateIndex
    {
        get => _selectedFunscriptBeatRateIndex;
        set
        {
            if (_selectedFunscriptBeatRateIndex == value) return;
            _selectedFunscriptBeatRateIndex = value;
            OnPropertyChanged();
            _settingsService.Current.PulseFunscriptBeatRateIndex = value;
            _settingsService.QueueSave();
        }
    }

    // ── Stroke Controls ──

    /// <summary>
    /// Amplitude offset slider value (-1.0 to +1.0).
    /// Negative = less movement, positive = more movement.
    /// </summary>
    public double AmplitudeOffset
    {
        get => _amplitudeOffset;
        set
        {
            value = Math.Clamp(value, -1.0, 1.0);
            if (Math.Abs(_amplitudeOffset - value) < 1e-9) return;
            _amplitudeOffset = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AmplitudeOffsetLabel));
            PropagateStrokeSettings();
            _settingsService.Current.PulseAmplitudeOffset = value;
            _settingsService.QueueSave();
        }
    }

    /// <summary>Display label for the current amplitude offset value.</summary>
    public string AmplitudeOffsetLabel => _amplitudeOffset.ToString("+0.0;-0.0;0.0");

    /// <summary>
    /// Easing blend slider value (-1.0 to +1.0).
    /// Negative = gentle (sinusoidal), positive = aggressive (linear).
    /// </summary>
    public double EasingBlend
    {
        get => _easingBlend;
        set
        {
            value = Math.Clamp(value, -1.0, 1.0);
            if (Math.Abs(_easingBlend - value) < 1e-9) return;
            _easingBlend = value;
            OnPropertyChanged();
            PropagateStrokeSettings();
            _settingsService.Current.PulseEasingBlend = value;
            _settingsService.QueueSave();
        }
    }

    /// <summary>The currently selected stroke pattern.</summary>
    public StrokePattern SelectedStrokePattern
    {
        get => _strokePattern;
        set
        {
            if (_strokePattern == value) return;
            _strokePattern = value;
            OnPropertyChanged();
            PropagateStrokeSettings();
            _settingsService.Current.PulseStrokePattern = value.ToString();
            _settingsService.QueueSave();
        }
    }

    /// <summary>Display names for the stroke pattern ComboBox.</summary>
    public static IReadOnlyList<string> StrokePatternOptions { get; } = new[]
    {
        "Classic", "Double Tap", "Triple Tap", "Hold Top", "Hold Bottom"
    };

    /// <summary>
    /// Selected stroke pattern index (0–4), maps to/from <see cref="StrokePattern"/> enum ordinal.
    /// </summary>
    public int SelectedStrokePatternIndex
    {
        get => (int)_strokePattern;
        set
        {
            var pattern = (StrokePattern)Math.Clamp(value, 0, 4);
            if (_strokePattern == pattern) return;
            _strokePattern = pattern;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedStrokePattern));
            PropagateStrokeSettings();
            _settingsService.Current.PulseStrokePattern = pattern.ToString();
            _settingsService.QueueSave();
        }
    }

    /// <summary>
    /// Randomness slider value (0.0 to 1.0).
    /// Adds organic per-beat amplitude variation.
    /// </summary>
    public double Randomness
    {
        get => _randomness;
        set
        {
            value = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_randomness - value) < 1e-9) return;
            _randomness = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RandomnessLabel));
            PropagateStrokeSettings();
            _settingsService.Current.PulseRandomness = value;
            _settingsService.QueueSave();
        }
    }

    /// <summary>Display label for the current randomness value as a percentage.</summary>
    public string RandomnessLabel => $"{_randomness:P0}";

    /// <summary>Description text explaining Pulse behaviour.</summary>
    public string Description =>
        "When Use Pulse is enabled:\n" +
        "\u2022 Audio is pre-analyzed for beat detection on load\n" +
        "\u2022 Funscript auto-loading is suppressed\n" +
        "\u2022 A \u2018Pulse\u2019 BeatBar mode appears (with red hearts)\n" +
        "\u2022 L0 axis is driven by beat-synchronized strokes\n" +
        "\u2022 Other axes (R0/R1/R2) continue with fill modes\n" +
        "\u2022 OSR2+ axis Min/Max/Enabled settings still apply\n\n" +
        "Stroke Controls:\n" +
        "\u2022 Beat Rate \u2014 select which beats drive the strokes\n" +
        "\u2022 Amplitude \u2014 adjust stroke intensity (left = less, right = more)\n" +
        "\u2022 Speed \u2014 adjust stroke feel (Gentle \u2190 \u2192 Aggressive)\n" +
        "\u2022 Pattern \u2014 choose stroke waveform (Classic, Double Tap, etc.)\n" +
        "\u2022 Randomness \u2014 add organic variation to stroke amplitude\n\n" +
        "Generate Funscript:\n" +
        "Creates a .funscript file with all current stroke settings baked in. " +
        "The generated script includes amplitude, speed, pattern, and randomness adjustments.\n\n" +
        "Toggle off to restore normal funscript behavior.";

    // ── Generate Funscript ──

    /// <summary>Whether the Generate Funscript button should be enabled.</summary>
    public bool CanGenerateFunscript =>
        !_isGenerating
        && _state is PulseState.Ready or PulseState.Active
        && _engine.CurrentBeatMap is not null
        && !string.IsNullOrEmpty(_currentVideoPath);

    /// <summary>Command that generates a .funscript file from the current beat map.</summary>
    public ICommand GenerateFunscriptCommand => _generateFunscriptCommand ??= new AsyncRelayCommand(GenerateFunscriptAsync);
    private ICommand? _generateFunscriptCommand;

    private async Task GenerateFunscriptAsync()
    {
        var beatMap = _engine.CurrentBeatMap;
        var videoPath = _currentVideoPath;

        if (beatMap is null || string.IsNullOrEmpty(videoPath))
            return;

        var targetPath = Path.ChangeExtension(videoPath, ".funscript");
        var fileName = Path.GetFileName(targetPath);

        if (File.Exists(targetPath))
        {
            if (_confirmOverwrite is not null)
            {
                var confirmed = await _confirmOverwrite(
                    "Overwrite Funscript?",
                    $"\"{fileName}\" already exists. Overwrite it?");
                if (!confirmed) return;
            }
        }

        _isGenerating = true;
        OnPropertyChanged(nameof(CanGenerateFunscript));
        try
        {
            int divisor = _selectedFunscriptBeatRateIndex + 1;
            var filteredBeatMap = FunscriptWriter.FilterBeatsByDivisor(beatMap, divisor);
            var actions = FunscriptWriter.CreateActionsFromBeatMap(filteredBeatMap, BuildStrokeSettings());
            await FunscriptWriter.WriteAsync(actions, targetPath);
            _toastService?.Show("Funscript generated:", fileName);
            _eventBus.Publish(new FunscriptGeneratedEvent
            {
                FilePath = targetPath,
                VideoPath = videoPath
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _toastService?.ShowError("Failed to write funscript:", ex.Message);
        }
        finally
        {
            _isGenerating = false;
            OnPropertyChanged(nameof(CanGenerateFunscript));
        }
    }

    // ── Engine callbacks ──

    private void OnEngineStateChanged(PulseState newState)
    {
        State = newState;
        if (newState != PulseState.Error)
            _errorMessage = null;
        UpdateStatusMessage();
        OnPropertyChanged(nameof(CanGenerateFunscript));
    }

    private void OnAnalysisProgress(double progress)
    {
        AnalysisProgress = progress;
        UpdateStatusMessage();
    }

    private void OnBeatMapReady(BeatMap beatMap)
    {
        CurrentBpm = beatMap.Bpm;
        UpdateStatusMessage();
        OnPropertyChanged(nameof(CanGenerateFunscript));
    }

    private void OnErrorOccurred(string message)
    {
        _errorMessage = message;
        UpdateStatusMessage();
    }

    // ── Helpers ──

    private PulseStrokeSettings BuildStrokeSettings() => new()
    {
        AmplitudeOffset = _amplitudeOffset,
        EasingBlend = _easingBlend,
        Pattern = _strokePattern,
        Randomness = _randomness,
    };

    private void PropagateStrokeSettings()
    {
        _engine.SetStrokeSettings(BuildStrokeSettings());
    }

    private void UpdateStatusMessage()
    {
        StatusMessage = _state switch
        {
            PulseState.Inactive => string.Empty,
            PulseState.Analyzing => $"Analyzing audio... {_analysisProgress:P0}",
            PulseState.Ready => $"Ready \u2014 \u2665 {_currentBpm:F0} BPM detected",
            PulseState.Active => $"\u2665 {_currentBpm:F0} BPM",
            PulseState.Error => $"Error: {_errorMessage ?? "Unknown"}",
            _ => string.Empty
        };

        StatusBarText = _state switch
        {
            PulseState.Inactive => "\u2665 Pulse: Off",
            PulseState.Analyzing => "\u2665 Pulse: Analyzing...",
            PulseState.Ready => "\u2665 Pulse: Ready",
            PulseState.Active => $"\u2665 Pulse: Active {_currentBpm:F0} BPM",
            PulseState.Error => "\u2665 Pulse: Error",
            _ => "\u2665 Pulse: Off"
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Disposes the view model and unsubscribes from engine events.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _engine.StateChanged -= OnEngineStateChanged;
        _engine.AnalysisProgress -= OnAnalysisProgress;
        _engine.BeatMapReady -= OnBeatMapReady;
        _engine.ErrorOccurred -= OnErrorOccurred;

        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
    }
}
