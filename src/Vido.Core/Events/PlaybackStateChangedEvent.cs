using Vido.Core.Playback;

namespace Vido.Core.Events;

/// <summary>
/// Published when the playback state changes (play, pause, stop).
/// </summary>
public sealed class PlaybackStateChangedEvent
{
    /// <summary>The new playback state.</summary>
    public required PlaybackState State { get; init; }
}
