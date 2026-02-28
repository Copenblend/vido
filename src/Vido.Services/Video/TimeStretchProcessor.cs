using SoundTouch;

namespace Vido.Services.Video;

/// <summary>
/// Wraps SoundTouch to provide pitch-preserving time-stretch (tempo change)
/// for the audio pipeline. At tempo 1.0 the processor is a pass-through;
/// at other values it stretches or compresses audio while keeping pitch constant.
/// </summary>
internal sealed class TimeStretchProcessor : IDisposable
{
    private readonly SoundTouchProcessor _processor;
    private bool _disposed;

    /// <summary>
    /// Creates a time-stretch processor configured for the given audio format,
    /// using SoundTouch with quick-seek and anti-alias filtering enabled.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate in Hz (e.g. 44100, 48000).</param>
    /// <param name="channels">The number of audio channels (e.g. 2 for stereo).</param>
    public TimeStretchProcessor(int sampleRate, int channels)
    {
        _processor = new SoundTouchProcessor
        {
            SampleRate = sampleRate,
            Channels = channels
        };

        // Quick-seek trades a little quality for lower CPU â€” acceptable for
        // real-time video playback.
        _processor.SetSetting(SettingId.UseQuickSeek, 1);
        _processor.SetSetting(SettingId.UseAntiAliasFilter, 1);
    }

    /// <summary>
    /// Gets or sets the playback tempo. 1.0 = normal, 2.0 = double speed, 0.5 = half speed.
    /// Pitch is preserved automatically.
    /// </summary>
    public double Tempo
    {
        get => _processor.Tempo;
        set => _processor.Tempo = value;
    }

    /// <summary>
    /// Pushes interleaved float samples into the processor.
    /// </summary>
    /// <param name="samples">Interleaved float sample buffer.</param>
    /// <param name="numSamples">Number of sample frames (not individual floats).</param>
    public void PutSamples(ReadOnlySpan<float> samples, int numSamples)
    {
        _processor.PutSamples(samples, numSamples);
    }

    /// <summary>
    /// Pulls processed samples from the processor.
    /// </summary>
    /// <param name="buffer">Output buffer for interleaved float samples.</param>
    /// <param name="maxSamples">Maximum number of sample frames to receive.</param>
    /// <returns>Number of sample frames actually written.</returns>
    public int ReceiveSamples(Span<float> buffer, int maxSamples)
    {
        return _processor.ReceiveSamples(buffer, maxSamples);
    }

    /// <summary>
    /// Number of sample frames available for reading.
    /// </summary>
    public int AvailableSamples => _processor.AvailableSamples;

    /// <summary>
    /// Clears all internal buffers without disposing.
    /// Use after a speed change or seek to discard stale stretched samples.
    /// </summary>
    public void Clear()
    {
        _processor.Clear();
    }
    
    /// <summary>
    /// Clears internal SoundTouch buffers and marks this processor as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _processor.Clear();
    }
}
