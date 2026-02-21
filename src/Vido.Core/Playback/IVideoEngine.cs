namespace Vido.Core.Playback;

/// <summary>
/// Core video playback engine interface.
/// Provides media loading, playback control, and frame/audio output.
/// </summary>
public interface IVideoEngine : IDisposable
{
    // ── State ──

    /// <summary>Current playback state.</summary>
    PlaybackState State { get; }

    /// <summary>Current playback position.</summary>
    TimeSpan Position { get; }

    /// <summary>Total duration of the loaded media.</summary>
    TimeSpan Duration { get; }

    /// <summary>Volume level (0–100).</summary>
    int Volume { get; set; }

    /// <summary>Whether audio output is muted.</summary>
    bool IsMuted { get; set; }

    /// <summary>Whether playback loops when reaching the end.</summary>
    bool IsLooping { get; set; }

    /// <summary>Metadata for the currently loaded video, or null if none.</summary>
    VideoMetadata? CurrentMetadata { get; }

    // ── Commands ──

    /// <summary>
    /// Loads a video file asynchronously. Extracts metadata, sets up decoders.
    /// </summary>
    Task LoadAsync(string filePath);

    /// <summary>Starts or resumes playback.</summary>
    void Play();

    /// <summary>Pauses playback at the current position.</summary>
    void Pause();

    /// <summary>Stops playback and resets position to the beginning.</summary>
    void Stop();

    /// <summary>Seeks to the specified position.</summary>
    void Seek(TimeSpan position);

    // ── Events ──

    /// <summary>Fires at ~60Hz with the current playback position during playback.</summary>
    event Action<TimeSpan>? PositionChanged;

    /// <summary>Fires when the playback state changes.</summary>
    event Action<PlaybackState>? StateChanged;

    /// <summary>Fires when a decoded video frame is ready for display.</summary>
    event Action<FrameData>? FrameReady;

    /// <summary>Fires when the media reaches the end (before looping, if enabled).</summary>
    event Action? MediaEnded;
}
