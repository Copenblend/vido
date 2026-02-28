namespace Vido.Core.Playback;

/// <summary>
/// Represents the current state of the video playback engine.
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// No media loaded.
    /// </summary>
    None,

    /// <summary>
    /// Media is actively playing.
    /// </summary>
    Playing,

    /// <summary>
    /// Media is paused at the current position.
    /// </summary>
    Paused,

    /// <summary>
    /// Media is stopped (position reset to beginning).
    /// </summary>
    Stopped
}
