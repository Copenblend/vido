using Vido.Core.Playback;

namespace Vido.Core.Events;

/// <summary>
/// Published when the playback state changes (play, pause, stop).
/// </summary>
public readonly record struct PlaybackStateChangedEvent
{
    /// <summary>
    /// The new playback state.
    /// </summary>
    public PlaybackState State { get; init; }
}
