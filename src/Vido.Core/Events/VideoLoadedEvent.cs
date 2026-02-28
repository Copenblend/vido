using Vido.Core.Playback;

namespace Vido.Core.Events;

/// <summary>
/// Published when a video file has been loaded and is ready for playback.
/// </summary>
public sealed class VideoLoadedEvent
{
    /// <summary>
    /// Full path to the loaded video file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Metadata extracted from the video file.
    /// </summary>
    public required VideoMetadata Metadata { get; init; }
}
