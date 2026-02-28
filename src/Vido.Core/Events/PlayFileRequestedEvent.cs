namespace Vido.Core.Events;

/// <summary>
/// Published when a plugin requests playback of a specific file.
/// VideoPlayerViewModel subscribes to this and routes through LoadAndPlayAsync.
/// </summary>
public sealed class PlayFileRequestedEvent
{
    /// <summary>
    /// Full path to the file to play.
    /// </summary>
    public required string FilePath { get; init; }
}
