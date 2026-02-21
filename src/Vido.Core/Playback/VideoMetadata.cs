namespace Vido.Core.Playback;

/// <summary>
/// Contains metadata extracted from a loaded video file.
/// </summary>
public sealed class VideoMetadata
{
    /// <summary>Full path to the video file.</summary>
    public required string FilePath { get; init; }

    /// <summary>File name without directory path.</summary>
    public required string FileName { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Total duration of the video.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Video width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Video height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Video codec name (e.g., "H.264", "HEVC").</summary>
    public string? VideoCodec { get; init; }

    /// <summary>Audio codec name (e.g., "AAC", "MP3").</summary>
    public string? AudioCodec { get; init; }

    /// <summary>Video frame rate in frames per second.</summary>
    public double FrameRate { get; init; }

    /// <summary>Overall bitrate in bits per second.</summary>
    public long Bitrate { get; init; }

    /// <summary>Container format name (e.g., "mp4", "mkv").</summary>
    public string? ContainerFormat { get; init; }

    /// <summary>Number of audio channels (e.g., 2 for stereo).</summary>
    public int AudioChannels { get; init; }

    /// <summary>Audio sample rate in Hz (e.g., 44100, 48000).</summary>
    public int AudioSampleRate { get; init; }

    /// <summary>
    /// Returns a human-readable resolution string (e.g., "1920x1080").
    /// </summary>
    public string Resolution => $"{Width}x{Height}";
}
