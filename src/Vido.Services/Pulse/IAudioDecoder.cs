namespace Vido.Services.Pulse;

/// <summary>
/// Abstraction for decoding audio from a media file to PCM float32 mono samples.
/// The real implementation wraps FFmpeg process decoding; tests use a mock.
/// </summary>
internal interface IAudioDecoder
{
    /// <summary>
    /// Decode the audio track from the given media file path.
    /// Yields chunks of mono float32 PCM samples along with metadata.
    /// </summary>
    /// <param name="mediaPath">Path to the media file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of decoded audio chunks.</returns>
    IAsyncEnumerable<AudioChunk> DecodeAsync(string mediaPath, CancellationToken cancellationToken = default);
}
