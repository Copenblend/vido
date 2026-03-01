namespace Vido.Core.Events;

/// <summary>
/// Published at ~60 Hz during video playback with the current position.
/// </summary>
public readonly record struct PlaybackPositionChangedEvent
{
    /// <summary>
    /// Current playback position.
    /// </summary>
    public TimeSpan Position { get; init; }

    /// <summary>
    /// Total duration of the media.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
