namespace Vido.Core.Events;

/// <summary>
/// Published at ~60 Hz during video playback with the current position.
/// </summary>
public sealed class PlaybackPositionChangedEvent
{
    /// <summary>
    /// Current playback position.
    /// </summary>
    public required TimeSpan Position { get; init; }

    /// <summary>
    /// Total duration of the media.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}
