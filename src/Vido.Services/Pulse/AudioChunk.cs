namespace Vido.Services.Pulse;

/// <summary>
/// A chunk of decoded mono PCM audio.
/// </summary>
internal sealed class AudioChunk
{
    /// <summary>Mono float32 PCM samples.</summary>
    public required float[] Samples { get; init; }

    /// <summary>Sample rate in Hz.</summary>
    public required int SampleRate { get; init; }

    /// <summary>Timestamp of the first sample in this chunk, in milliseconds.</summary>
    public required double TimestampMs { get; init; }

    /// <summary>Total duration of the source audio in milliseconds (0 if unknown).</summary>
    public double TotalDurationMs { get; init; }
}
