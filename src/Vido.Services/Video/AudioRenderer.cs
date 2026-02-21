using NAudio.Wave;

namespace Vido.Services.Video;

/// <summary>
/// Renders decoded audio samples via WASAPI (Windows Audio Session API).
/// Uses NAudio's WasapiOut for low-latency audio output.
/// </summary>
internal sealed class AudioRenderer : IDisposable
{
    private WasapiOut? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    private WaveFormat? _waveFormat;
    private bool _disposed;
    private float _volume = 1.0f;
    private bool _isMuted;

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.0).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            ApplyVolume();
        }
    }

    /// <summary>
    /// Gets or sets whether audio output is muted.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyVolume();
        }
    }

    /// <summary>
    /// Initializes the audio renderer for the specified format.
    /// Must be called before submitting samples.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (e.g., 44100, 48000).</param>
    /// <param name="channels">Number of audio channels (e.g., 2 for stereo).</param>
    public void Initialize(int sampleRate, int channels)
    {
        Cleanup();

        // Use IEEE float samples (32-bit) — this is what swresample outputs
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _waveProvider = new BufferedWaveProvider(_waveFormat)
        {
            // Buffer up to 1 second of audio — prevents underruns while limiting latency
            BufferLength = sampleRate * channels * sizeof(float),
            DiscardOnBufferOverflow = true
        };

        try
        {
            _waveOut = new WasapiOut(
                NAudio.CoreAudioApi.AudioClientShareMode.Shared,
                latency: 50); // 50ms latency for responsive playback

            _waveOut.Init(_waveProvider);
            ApplyVolume();
        }
        catch (Exception)
        {
            // Audio device may not be available — degrade gracefully
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    /// <summary>
    /// Submits decoded audio samples for playback.
    /// Samples must be IEEE float format matching the initialized sample rate and channels.
    /// </summary>
    public void SubmitSamples(byte[] data, int offset, int count)
    {
        if (_waveProvider == null || _disposed)
            return;

        _waveProvider.AddSamples(data, offset, count);
    }

    /// <summary>
    /// Starts audio playback.
    /// </summary>
    public void Play()
    {
        if (_waveOut?.PlaybackState != PlaybackState.Playing)
            _waveOut?.Play();
    }

    /// <summary>
    /// Pauses audio playback.
    /// </summary>
    public void Pause()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
            _waveOut?.Pause();
    }

    /// <summary>
    /// Stops audio playback and clears the buffer.
    /// </summary>
    public void Stop()
    {
        _waveOut?.Stop();
        _waveProvider?.ClearBuffer();
    }

    /// <summary>
    /// Clears the audio buffer (used during seeking to avoid stale audio).
    /// </summary>
    public void Flush()
    {
        _waveProvider?.ClearBuffer();
    }

    private void ApplyVolume()
    {
        if (_waveOut?.AudioStreamVolume != null)
        {
            try
            {
                var effectiveVolume = _isMuted ? 0f : _volume;
                var channelCount = _waveOut.AudioStreamVolume.ChannelCount;
                for (int i = 0; i < channelCount; i++)
                    _waveOut.AudioStreamVolume.SetChannelVolume(i, effectiveVolume);
            }
            catch
            {
                // Volume control may not be supported in all configurations
            }
        }
    }

    private void Cleanup()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _waveProvider = null;
        _waveFormat = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }
}
