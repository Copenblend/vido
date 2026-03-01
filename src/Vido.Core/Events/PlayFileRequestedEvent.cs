namespace Vido.Core.Events;

/// <summary>
/// Published when a plugin requests playback of a specific file.
/// VideoPlayerViewModel subscribes to this and routes through LoadAndPlayAsync.
/// </summary>
public readonly record struct PlayFileRequestedEvent
{
    private readonly string? _filePath;

    /// <summary>
    /// Full path to the file to play. Defaults to <see cref="string.Empty"/> when unset.
    /// </summary>
    public string FilePath
    {
        get => _filePath ?? string.Empty;
        init => _filePath = value;
    }
}
