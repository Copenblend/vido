using Vido.Core.Playback;

namespace Vido.Core.Events;

/// <summary>
/// Published when a video file has been loaded and is ready for playback.
/// </summary>
public readonly record struct VideoLoadedEvent
{
    private readonly string? _filePath;
    private readonly VideoMetadata? _metadata;

    /// <summary>
    /// Full path to the loaded video file. Defaults to <see cref="string.Empty"/> when unset.
    /// </summary>
    public string FilePath
    {
        get => _filePath ?? string.Empty;
        init => _filePath = value;
    }

    /// <summary>
    /// Metadata extracted from the video file. Defaults to <see cref="VideoMetadata.Empty"/> when unset.
    /// </summary>
    public VideoMetadata Metadata
    {
        get => _metadata ?? VideoMetadata.Empty;
        init => _metadata = value;
    }
}
