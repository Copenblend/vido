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
    private bool _disposed;
    private float _volume = 1.0f;
    private bool _isMuted;
    private byte[]? _floatSubmitBuffer;
    private bool _deferredStart;

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

        // Use IEEE float samples (32-bit) â€” this is what swresample outputs
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _waveProvider = new BufferedWaveProvider(waveFormat)
        {
            // Buffer up to 2 seconds of audio â€” larger buffer reduces underruns and
            // crackling, especially over Bluetooth or with high-latency devices.
            BufferLength = sampleRate * channels * sizeof(float) * 2,
            DiscardOnBufferOverflow = true
        };

        try
        {
            _waveOut = new WasapiOut(
                NAudio.CoreAudioApi.AudioClientShareMode.Shared,
                latency: 100); // 100ms latency â€” more headroom for Bluetooth / USB audio

            _waveOut.Init(_waveProvider);
            ApplyVolume();
        }
        catch (Exception)
        {
            // Audio device may not be available â€” degrade gracefully
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    /// <summary>
    /// Arms the renderer for deferred start: the next call to
    /// <see cref="SubmitSamples(byte[], int, int)"/> or <see cref="SubmitSamples(float[], int, int)"/>
    /// will call <c>WasapiOut.Play()</c> automatically after submitting the samples,
    /// ensuring the buffer is primed before playback begins.
    /// </summary>
    public void ArmDeferredStart()
    {
        _deferredStart = true;
        _waveProvider?.ClearBuffer();
    }

    /// <summary>
    /// Submits decoded audio samples for playback.
    /// Samples must be IEEE float format matching the initialized sample rate and channels.
    /// </summary>
    /// <param name="data">The byte array containing IEEE float audio samples.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="data"/> at which to begin reading.</param>
    /// <param name="count">The number of bytes to submit from <paramref name="data"/>.</param>
    public void SubmitSamples(byte[] data, int offset, int count)
    {
        if (_waveProvider == null || _disposed)
            return;

        _waveProvider.AddSamples(data, offset, count);

        if (_deferredStart)
        {
            _deferredStart = false;
            if (_waveOut?.PlaybackState != PlaybackState.Playing)
                _waveOut?.Play();
        }
    }

    /// <summary>
    /// Submits decoded audio samples from a float buffer.
    /// <paramref name="floatCount"/> is the number of individual float values (samples Ã— channels).
    /// </summary>
    /// <param name="data">The float array containing interleaved audio samples.</param>
    /// <param name="offset">The zero-based index in <paramref name="data"/> at which to begin reading.</param>
    /// <param name="floatCount">The number of individual float values (sample frames × channels) to submit.</param>
    public void SubmitSamples(float[] data, int offset, int floatCount)
    {
        if (_waveProvider == null || _disposed)
            return;

        var byteCount = floatCount * sizeof(float);

        if (_floatSubmitBuffer is null || _floatSubmitBuffer.Length < byteCount)
            _floatSubmitBuffer = new byte[byteCount];

        Buffer.BlockCopy(data, offset * sizeof(float), _floatSubmitBuffer, 0, byteCount);
        _waveProvider.AddSamples(_floatSubmitBuffer, 0, byteCount);

        if (_deferredStart)
        {
            _deferredStart = false;
            if (_waveOut?.PlaybackState != PlaybackState.Playing)
                _waveOut?.Play();
        }
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
        _floatSubmitBuffer = null;
    }
    
    /// <summary>
    /// Stops playback, releases the WASAPI output device, and marks this renderer as disposed.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }
}
