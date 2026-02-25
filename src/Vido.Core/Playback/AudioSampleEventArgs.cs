namespace Vido.Core.Playback;

/// <summary>
/// Contains decoded audio sample data from the playback engine.
/// The buffer is a zero-copy <see cref="ReadOnlyMemory{T}"/> slice of the decode buffer —
/// it is only valid for the duration of the event callback. Consumers that need
/// to retain the data must copy it (e.g., into a ring buffer).
/// </summary>
/// <remarks>
/// Audio format is always interleaved float32 PCM (IEEE 754), matching the
/// FFmpeg swr_convert output format (AV_SAMPLE_FMT_FLT).
/// </remarks>
public sealed class AudioSampleEventArgs
{
    /// <summary>
    /// Raw audio sample buffer as interleaved float32 PCM bytes.
    /// Only valid during the event callback — do not store a reference.
    /// </summary>
    public required ReadOnlyMemory<byte> Buffer { get; init; }

    /// <summary>
    /// Number of audio samples (per channel) in this buffer.
    /// Total floats = <see cref="SampleCount"/> × <see cref="Channels"/>.
    /// Total bytes  = <see cref="SampleCount"/> × <see cref="Channels"/> × 4.
    /// </summary>
    public required int SampleCount { get; init; }

    /// <summary>
    /// Output sample rate in Hz (e.g., 44100, 48000).
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Number of audio channels (e.g., 2 for stereo).
    /// </summary>
    public required int Channels { get; init; }
}
