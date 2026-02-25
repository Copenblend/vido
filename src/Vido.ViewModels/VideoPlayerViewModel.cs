using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Formatting;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.Core.State;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the video player tab. Binds to <see cref="IVideoEngine"/>
/// and exposes commands for transport controls, skip prev/next, and frame display.
/// </summary>
public partial class VideoPlayerViewModel : ObservableObject, IDisposable
{
    private readonly IVideoEngine _engine;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly IStateService _stateService;
    private readonly IContributionRegistry _contributionRegistry;
    private bool _disposed;
    private bool _isSeeking;
    private double _lastSavedPositionSeconds;

    /// <summary>Seek slider range maximum (0 – SliderMaximum).</summary>
    private const double SeekSliderMaximum = 1000.0;

    /// <summary>Minimum playback change (seconds) before persisting position to state.</summary>
    private const double PositionSaveIntervalSeconds = 5.0;

    // ── Observable state ──

    /// <summary>Current playback state.</summary>
    [ObservableProperty]
    private PlaybackState _state;

    /// <summary>Current playback position.</summary>
    [ObservableProperty]
    private TimeSpan _position;

    /// <summary>Total duration of the loaded media.</summary>
    [ObservableProperty]
    private TimeSpan _duration;

    /// <summary>Volume level (0–100).</summary>
    [ObservableProperty]
    private int _volume;

    /// <summary>Whether audio is muted.</summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>Whether playback loops at end.</summary>
    [ObservableProperty]
    private bool _isLooping;

    /// <summary>Whether shuffle mode is active.</summary>
    [ObservableProperty]
    private bool _isShuffling;

    /// <summary>Current playback speed multiplier (0.25–4.0).</summary>
    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    /// <summary>Display text for the current playback speed (e.g. "1x", "2x").</summary>
    [ObservableProperty]
    private string _playbackSpeedText = "1x";

    /// <summary>Whether a video file is currently being loaded.</summary>
    [ObservableProperty]
    private bool _isLoadingMedia;

    /// <summary>Whether a video file is currently loaded.</summary>
    [ObservableProperty]
    private bool _hasMedia;

    /// <summary>Metadata of the currently loaded video.</summary>
    [ObservableProperty]
    private VideoMetadata? _currentMetadata;

    /// <summary>Display text for the current position (e.g. "01:23").</summary>
    [ObservableProperty]
    private string _positionText = "00:00";

    /// <summary>Display text for the total duration (e.g. "05:47").</summary>
    [ObservableProperty]
    private string _durationText = "00:00";

    /// <summary>
    /// Seek position as a value from 0 to 1000 for slider binding.
    /// </summary>
    [ObservableProperty]
    private double _seekPosition;

    /// <summary>True when the play button icon should appear (i.e. not currently playing).</summary>
    [ObservableProperty]
    private bool _showPlayIcon = true;

    /// <summary>
    /// Path of the currently loaded video file, or null if none.
    /// Used for skip prev/next logic.
    /// </summary>
    [ObservableProperty]
    private string? _currentFilePath;

    // ── Resume Bar ──

    /// <summary>Whether the resume playback prompt bar is visible.</summary>
    [ObservableProperty]
    private bool _showResumeBar;

    /// <summary>Title text shown in the resume bar (e.g. the video file name).</summary>
    [ObservableProperty]
    private string _resumeBarTitle = string.Empty;

    /// <summary>
    /// Ordered list of all video files under the explorer root.
    /// Populated when the explorer root changes or a file is loaded.
    /// </summary>
    private List<string> _siblingVideoFiles = [];

    /// <summary>
    /// The root folder path from the file explorer. Used to scan all nested video files.
    /// </summary>
    private string? _explorerRootPath;

    /// <summary>
    /// Pre-shuffled playlist with no repeats. Built when shuffle is toggled on.
    /// Index tracks current position in the shuffle order.
    /// </summary>
    private List<string> _shufflePlaylist = [];
    private int _shuffleIndex = -1;

    /// <summary>
    /// Version counter for the loading-indicator delay.  Bumping the version
    /// suppresses any in-flight delay whose captured version no longer matches,
    /// avoiding the first-chance <see cref="TaskCanceledException"/> noise that
    /// a <see cref="CancellationTokenSource"/> + <c>Task.Delay(delay, ct)</c>
    /// approach would produce in the debugger output.
    /// </summary>
    private int _loadingIndicatorVersion;

    /// <summary>
    /// Timestamp (via <see cref="Stopwatch.GetTimestamp"/>) recorded when the
    /// loading indicator was shown. Zero means the indicator was never displayed.
    /// </summary>
    private long _loadingShownTimestamp;

    /// <summary>
    /// Fired when a decoded frame is ready for display.
    /// The view subscribes to this to write pixels into a WriteableBitmap.
    /// </summary>
    public event Action<FrameData>? FrameReady;

    public VideoPlayerViewModel(IVideoEngine engine, IEventBus eventBus, ILogService logService,
        ISettingsService settingsService, IStateService stateService, IContributionRegistry contributionRegistry)
    {
        _engine = engine;
        _eventBus = eventBus;
        _logService = logService;
        _settingsService = settingsService;
        _stateService = stateService;
        _contributionRegistry = contributionRegistry;

        // Initialize from persisted settings (use backing fields to avoid triggering save handlers)
        var settings = settingsService.Current;
        _engine.Volume = (int)(settings.Volume * 100);
        _engine.IsMuted = settings.IsMuted;
        _engine.IsLooping = settings.LoopPlayback;
        _volume = _engine.Volume;
        _isMuted = _engine.IsMuted;
        _isLooping = _engine.IsLooping;
        _playbackSpeed = settings.PlaybackSpeed;
        _playbackSpeedText = FormatSpeed(_playbackSpeed);
        _engine.SpeedRatio = _playbackSpeed;

        _engine.StateChanged += OnEngineStateChanged;
        _engine.PositionChanged += OnEnginePositionChanged;
        _engine.FrameReady += OnEngineFrameReady;
        _engine.MediaEnded += OnEngineMediaEnded;

        _eventBus.Subscribe<PlayFileRequestedEvent>(e => _ = LoadAndPlayAsync(e.FilePath));
    }

    // ── Engine event handlers ──

    private void OnEngineStateChanged(PlaybackState newState)
    {
        State = newState;
        ShowPlayIcon = newState != PlaybackState.Playing;
        _eventBus.Publish(new PlaybackStateChangedEvent { State = newState });
    }

    private void OnEnginePositionChanged(TimeSpan position)
    {
        if (_isSeeking) return;

        Position = position;
        PositionText = FormatTime(position);
        _eventBus.Publish(new PlaybackPositionChangedEvent { Position = position, Duration = Duration });

        if (Duration.TotalSeconds > 0)
            SeekPosition = position.TotalSeconds / Duration.TotalSeconds * SeekSliderMaximum;

        // Save position to state every N seconds of playback change
        if (Math.Abs(position.TotalSeconds - _lastSavedPositionSeconds) >= PositionSaveIntervalSeconds)
        {
            _lastSavedPositionSeconds = position.TotalSeconds;
            _stateService.Current.LastVideoPosition = position.TotalSeconds;
            _stateService.QueueSave();
        }
    }

    private void OnEngineFrameReady(FrameData frame)
    {
        // Always forward frames — the engine handles stale frame prevention
        // via the seek generation counter and silent preroll.
        FrameReady?.Invoke(frame);
    }

    private void OnEngineMediaEnded()
    {
        // If looping the current file, the engine already handles it.
        // Otherwise, advance to next file (respecting shuffle mode).
        if (!IsLooping)
        {
            // Delegate to playlist provider if one is registered and active
            var provider = _contributionRegistry.GetPlaylistProvider();
            if (provider is { IsActive: true })
            {
                var next = provider.GetNextFile();
                if (next is not null)
                {
                    _ = LoadAndPlayAsync(next);
                    return;
                }
            }

            var nextFile = GetNextFile();
            if (nextFile is not null)
            {
                _ = LoadAndPlayAsync(nextFile);
            }
        }
    }


    // ── Commands ──

    /// <summary>
    /// Shared media loading pipeline used by both <see cref="LoadAndPlayAsync"/>
    /// and <see cref="RestoreLastVideoAsync"/>. Loads the file into the engine,
    /// updates duration/metadata/sibling list, and sets <see cref="HasMedia"/>.
    /// </summary>
    /// <summary>How long to wait before showing the loading spinner.</summary>
    private static readonly TimeSpan LoadingIndicatorDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Once shown, the spinner stays visible for at least this long so it
    /// doesn't appear as a distracting flash.
    /// </summary>
    private static readonly TimeSpan MinimumIndicatorDisplay = TimeSpan.FromMilliseconds(500);

    private async Task LoadMediaCoreAsync(string filePath)
    {
        // Only show the loading indicator if the load takes longer than the
        // threshold. Fast local loads complete before the delay elapses,
        // avoiding any UI overhead from showing and immediately hiding the
        // spinner.  Uses a version counter instead of CancellationTokenSource
        // to avoid first-chance TaskCanceledException noise in the debugger.
        var loadVersion = Interlocked.Increment(ref _loadingIndicatorVersion);
        _loadingShownTimestamp = 0;
        _ = ShowLoadingIndicatorAfterDelayAsync(loadVersion);

        try
        {
            await _engine.LoadAsync(filePath);
            CurrentFilePath = filePath;
            Duration = _engine.Duration;
            DurationText = FormatTime(Duration);
            CurrentMetadata = _engine.CurrentMetadata;
            HasMedia = true;

            // Fire-and-forget: sibling list scan runs concurrently so it doesn't
            // delay playback start. Skip-prev/next will use whatever list exists
            // until the scan completes.
            _ = BuildSiblingListAsync(filePath);

            _eventBus.Publish(new VideoLoadedEvent
            {
                FilePath = filePath,
                Metadata = CurrentMetadata ?? new VideoMetadata
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath)
                }
            });
        }
        finally
        {
            Interlocked.Increment(ref _loadingIndicatorVersion);

            if (_loadingShownTimestamp != 0)
            {
                // Spinner was shown — keep it visible for a minimum duration
                // to avoid a distracting flash. Fire-and-forget so we don't
                // block playback start.
                var elapsed = Stopwatch.GetElapsedTime(_loadingShownTimestamp);
                var remaining = MinimumIndicatorDisplay - elapsed;
                if (remaining > TimeSpan.Zero)
                    _ = HideLoadingAfterDelayAsync(remaining);
                else
                    IsLoadingMedia = false;
            }
            else
            {
                IsLoadingMedia = false;
            }
        }
    }

    /// <summary>
    /// Sets <see cref="IsLoadingMedia"/> to <c>true</c> after a short delay.
    /// If the version has been bumped before the delay elapses (i.e. the load
    /// finished quickly), the indicator is never shown.
    /// </summary>
    private async Task ShowLoadingIndicatorAfterDelayAsync(int version)
    {
        await Task.Delay(LoadingIndicatorDelay);

        // Only show the indicator if no newer load has started and the
        // original load hasn't finished yet (the finally block bumps the
        // version when the load completes).
        if (Volatile.Read(ref _loadingIndicatorVersion) == version)
        {
            _loadingShownTimestamp = Stopwatch.GetTimestamp();
            IsLoadingMedia = true;
        }
    }

    /// <summary>
    /// Hides the loading indicator after a delay, ensuring a minimum visible
    /// duration so the spinner doesn't appear as a brief distracting flash.
    /// </summary>
    private async Task HideLoadingAfterDelayAsync(TimeSpan delay)
    {
        await Task.Delay(delay);
        IsLoadingMedia = false;
    }

    /// <summary>
    /// Loads a video file and begins playback.
    /// </summary>
    public async Task LoadAndPlayAsync(string filePath)
    {
        ShowResumeBar = false;
        _logService.Info($"Loading video: {Path.GetFileName(filePath)}", "Player");
        await LoadMediaCoreAsync(filePath);
        Position = TimeSpan.Zero;
        PositionText = "00:00";
        SeekPosition = 0;

        // Sync shuffle index to the file we just loaded
        if (IsShuffling && _shufflePlaylist.Count > 0)
        {
            _shuffleIndex = _shufflePlaylist.FindIndex(
                f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
        }

        _engine.Play();
        _logService.Info($"Playing: {Path.GetFileName(filePath)} ({CurrentMetadata?.Resolution}, {FormatTime(Duration)})", "Player");

        // Track last video and recent files
        _lastSavedPositionSeconds = 0;
        _stateService.Current.LastVideoPath = filePath;
        _stateService.Current.LastVideoPosition = 0;
        _stateService.Current.AddRecentFile(filePath);
        _stateService.QueueSave();
    }

    /// <summary>
    /// Restores the last played video from state, seeking to the saved position
    /// and pausing. Shows a resume bar prompting the user to continue or dismiss.
    /// </summary>
    public async Task RestoreLastVideoAsync()
    {
        var lastPath = _stateService.Current.LastVideoPath;
        if (string.IsNullOrEmpty(lastPath) || !File.Exists(lastPath))
            return;

        _logService.Info($"Restoring last video: {Path.GetFileName(lastPath)}", "Player");
        await LoadMediaCoreAsync(lastPath);

        // Start playback so the decode thread renders a frame, then seek and pause.
        // Without Play(), the decode thread never starts and Seek is a no-op,
        // leaving a gray background.
        var seekDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSeekDone() => seekDone.TrySetResult();
        _engine.SeekCompleted += OnSeekDone;

        _engine.Play();

        var savedPosition = _stateService.Current.LastVideoPosition;
        if (savedPosition > 0 && Duration.TotalSeconds > 0)
        {
            var target = TimeSpan.FromSeconds(Math.Min(savedPosition, Duration.TotalSeconds));
            _engine.Seek(target);
            Position = target;
            PositionText = FormatTime(target);
            SeekPosition = target.TotalSeconds / Duration.TotalSeconds * SeekSliderMaximum;
            _lastSavedPositionSeconds = target.TotalSeconds;
        }
        else
        {
            Position = TimeSpan.Zero;
            PositionText = "00:00";
            SeekPosition = 0;
            // Seek to zero so SeekCompleted fires
            _engine.Seek(TimeSpan.Zero);
        }

        // Wait for the seek to complete (frame is decoded and rendered), then pause
        await Task.WhenAny(seekDone.Task, Task.Delay(2000));
        _engine.SeekCompleted -= OnSeekDone;
        _engine.Pause();

        // Show the resume bar prompt
        ResumeBarTitle = Path.GetFileName(lastPath);
        ShowResumeBar = true;

        _logService.Info($"Restored: {Path.GetFileName(lastPath)} at {PositionText}", "Player");
    }

    /// <summary>Accepts the resume prompt — continues playback from the restored position.</summary>
    [RelayCommand]
    public void ResumePlayback()
    {
        ShowResumeBar = false;
        if (HasMedia)
        {
            _engine.Play();
            _logService.Info("Resume accepted — playback started", "Player");
        }
    }

    /// <summary>Dismisses the resume prompt — closes/unloads the video.</summary>
    [RelayCommand]
    public void DismissResume()
    {
        ShowResumeBar = false;
        Stop();
        _logService.Info("Resume dismissed — video unloaded", "Player");
    }

    /// <summary>Toggles between play and pause.</summary>
    [RelayCommand]
    public void PlayPause()
    {
        if (!HasMedia) return;

        // If resume bar is visible, treat play/pause as accepting the resume
        if (ShowResumeBar)
        {
            ResumePlayback();
            return;
        }

        if (State == PlaybackState.Playing)
        {
            _engine.Pause();
            _logService.Info("Playback paused", "Player");
        }
        else
        {
            _engine.Play();
            _logService.Info("Playback resumed", "Player");
        }
    }

    /// <summary>Stops playback and resets position.</summary>
    [RelayCommand]
    public void Stop()
    {
        if (!HasMedia) return;
        _engine.Stop();
        _eventBus.Publish(new VideoUnloadedEvent());
        _logService.Info("Playback stopped", "Player");
        HasMedia = false;
        CurrentMetadata = null;
        CurrentFilePath = null;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        PositionText = "00:00";
        DurationText = "00:00";
        SeekPosition = 0;

        _stateService.Current.LastVideoPath = null;
        _stateService.Current.LastVideoPosition = 0;
        _stateService.QueueSave();
    }

    /// <summary>Skips to the previous video file in the folder (wraps around).</summary>
    [RelayCommand]
    public async Task SkipPrevious()
    {
        // Delegate to playlist provider if one is registered and active
        var provider = _contributionRegistry.GetPlaylistProvider();
        if (provider is { IsActive: true })
        {
            var prev = provider.GetPreviousFile();
            if (prev is not null)
            {
                await LoadAndPlayAsync(prev);
                return;
            }
        }

        var prevFile = IsShuffling ? GetShuffleFile(-1) : GetAdjacentVideoFile(-1);
        if (prevFile is not null)
            await LoadAndPlayAsync(prevFile);
    }

    /// <summary>Skips to the next video file in the folder (wraps around).</summary>
    [RelayCommand]
    public async Task SkipNext()
    {
        // Delegate to playlist provider if one is registered and active
        var provider = _contributionRegistry.GetPlaylistProvider();
        if (provider is { IsActive: true })
        {
            var next = provider.GetNextFile();
            if (next is not null)
            {
                await LoadAndPlayAsync(next);
                return;
            }
        }

        var nextFile = IsShuffling ? GetShuffleFile(1) : GetAdjacentVideoFile(1);
        if (nextFile is not null)
            await LoadAndPlayAsync(nextFile);
    }

    /// <summary>Toggles mute state.</summary>
    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    /// <summary>Toggles loop mode.</summary>
    [RelayCommand]
    public void ToggleLoop()
    {
        IsLooping = !IsLooping;
    }

    /// <summary>Toggles shuffle mode. Builds or clears the shuffle playlist.</summary>
    [RelayCommand]
    public void ToggleShuffle()
    {
        IsShuffling = !IsShuffling;
    }

    // ── Property change callbacks ──

    partial void OnVolumeChanged(int value)
    {
        _engine.Volume = Math.Clamp(value, 0, 100);

        // Any manual volume adjustment should unmute
        if (IsMuted)
            IsMuted = false;

        _settingsService.Current.Volume = value / 100.0;
        _settingsService.QueueSave();
    }

    partial void OnIsMutedChanged(bool value)
    {
        _engine.IsMuted = value;
        _settingsService.Current.IsMuted = value;
        _settingsService.QueueSave();
    }

    partial void OnIsLoopingChanged(bool value)
    {
        _engine.IsLooping = value;
        _settingsService.Current.LoopPlayback = value;
        _settingsService.QueueSave();
    }

    partial void OnIsShufflingChanged(bool value)
    {
        // Propagate shuffle toggle to the active playlist provider (if any)
        var provider = _contributionRegistry.GetPlaylistProvider();
        if (provider is { IsActive: true })
        {
            if (value)
                provider.EnableShuffle();
            else
                provider.DisableShuffle();
        }

        if (value)
            BuildShufflePlaylist();
        else
            ClearShufflePlaylist();
    }

    partial void OnPlaybackSpeedChanged(double value)
    {
        _engine.SpeedRatio = value;
        PlaybackSpeedText = FormatSpeed(value);
        _settingsService.Current.PlaybackSpeed = value;
        _settingsService.QueueSave();
    }

    /// <summary>Sets the playback speed to a specific value.</summary>
    [RelayCommand]
    public void SetPlaybackSpeed(double speed)
    {
        PlaybackSpeed = Math.Clamp(speed, 0.25, 4.0);
    }

    private static string FormatSpeed(double speed)
    {
        return speed == (int)speed ? $"{(int)speed}x" : $"{speed:0.##}x";
    }

    // ── Seek support ──

    /// <summary>
    /// Suppresses engine position updates so the slider doesn't fight during drag.
    /// Called on seek mouse-down.
    /// </summary>
    public void BeginSeek()
    {
        _isSeeking = true;
    }

    /// <summary>
    /// Resumes engine position updates.
    /// Called on seek mouse-up.
    /// </summary>
    public void EndSeek()
    {
        _isSeeking = false;
    }

    /// <summary>
    /// Seeks the engine to the current slider position without changing _isSeeking state.
    /// Called on each mouse-down click and on every mouse-move during drag.
    /// </summary>
    public void ApplySeek()
    {
        if (Duration.TotalSeconds > 0)
        {
            var target = TimeSpan.FromSeconds(SeekPosition / SeekSliderMaximum * Duration.TotalSeconds);
            _engine.Seek(target);
            Position = target;
            PositionText = FormatTime(target);
        }
    }

    /// <summary>
    /// Sets the explorer root folder path. When set, the sibling video list
    /// is rebuilt to include all video files recursively under this root.
    /// Called from MainWindow when a folder is opened or closed.
    /// </summary>
    public async Task SetExplorerRootAsync(string? rootPath)
    {
        _explorerRootPath = rootPath;
        await RebuildSiblingListAsync();

        // Clear shuffle when the library changes
        if (IsShuffling)
        {
            ClearShufflePlaylist();
            BuildShufflePlaylist();
        }
    }

    // ── Skip prev/next helpers ──

    /// <summary>
    /// Builds the sorted list of all video files under the explorer root recursively.
    /// Falls back to the parent directory of the given file if no root is set.
    /// </summary>
    private async Task BuildSiblingListAsync(string filePath)
    {
        // If there's no explorer root, use the file's parent folder as fallback
        if (_explorerRootPath is null)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (dir is not null)
                _explorerRootPath = dir;
        }

        await RebuildSiblingListAsync();
    }

    /// <summary>
    /// Scans the explorer root (recursively) for all video files and sorts alphabetically.
    /// The directory enumeration runs on the thread pool to avoid blocking the UI thread.
    /// </summary>
    /// Shared options for recursive video-file scanning.
    /// IgnoreInaccessible avoids per-entry exceptions on network shares.
    private static readonly EnumerationOptions s_recursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        ReturnSpecialDirectories = false
    };

    private async Task RebuildSiblingListAsync()
    {
        if (_explorerRootPath is null)
        {
            _siblingVideoFiles = [];
            return;
        }

        var root = _explorerRootPath;
        _siblingVideoFiles = await Task.Run(() =>
        {
            try
            {
                var list = new List<string>();
                foreach (var path in Directory.EnumerateFiles(root, "*", s_recursiveOptions))
                {
                    if (FileNode.VideoExtensions.Contains(Path.GetExtension(path)))
                        list.Add(path);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
                return list;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        });
    }

    /// <summary>
    /// Returns the video file at <paramref name="offset"/> positions relative
    /// to the current file in the sibling list. Wraps around from end to beginning
    /// and vice-versa.
    /// </summary>
    internal string? GetAdjacentVideoFile(int offset)
    {
        if (CurrentFilePath is null || _siblingVideoFiles.Count == 0)
            return null;

        var idx = _siblingVideoFiles.FindIndex(
            f => string.Equals(f, CurrentFilePath, StringComparison.OrdinalIgnoreCase));

        if (idx < 0) return null;

        // Wrap around using modular arithmetic
        var count = _siblingVideoFiles.Count;
        var target = ((idx + offset) % count + count) % count;
        return _siblingVideoFiles[target];
    }

    /// <summary>
    /// Returns the next file to play (respecting shuffle mode).
    /// Used by auto-advance on media end.
    /// </summary>
    internal string? GetNextFile()
    {
        return IsShuffling ? GetShuffleFile(1) : GetAdjacentVideoFile(1);
    }

    /// <summary>
    /// Returns the file at <paramref name="offset"/> from the current shuffle index.
    /// Wraps around the shuffle playlist.
    /// </summary>
    internal string? GetShuffleFile(int offset)
    {
        if (_shufflePlaylist.Count == 0) return null;

        var count = _shufflePlaylist.Count;
        var target = ((_shuffleIndex + offset) % count + count) % count;
        return _shufflePlaylist[target];
    }

    /// <summary>
    /// Builds a randomized playlist from the sibling video files with no duplicates.
    /// The current file is placed at the start so the user continues from here.
    /// </summary>
    internal void BuildShufflePlaylist()
    {
        if (_siblingVideoFiles.Count == 0)
        {
            _shufflePlaylist = [];
            _shuffleIndex = -1;
            return;
        }

        var rng = new Random();
        var remaining = _siblingVideoFiles
            .Where(f => !string.Equals(f, CurrentFilePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => rng.Next())
            .ToList();

        _shufflePlaylist = new List<string>(_siblingVideoFiles.Count);

        // Current file first so skip-next goes to a random one
        if (CurrentFilePath is not null)
            _shufflePlaylist.Add(CurrentFilePath);

        _shufflePlaylist.AddRange(remaining);
        _shuffleIndex = 0;
    }

    /// <summary>
    /// Clears the shuffle playlist.
    /// </summary>
    internal void ClearShufflePlaylist()
    {
        _shufflePlaylist = [];
        _shuffleIndex = -1;
    }

    // ── Formatting ──

    internal static string FormatTime(TimeSpan ts) => TimeFormatter.Format(ts);

    // ── Cleanup ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _engine.StateChanged -= OnEngineStateChanged;
        _engine.PositionChanged -= OnEnginePositionChanged;
        _engine.FrameReady -= OnEngineFrameReady;
        _engine.MediaEnded -= OnEngineMediaEnded;
    }
}
