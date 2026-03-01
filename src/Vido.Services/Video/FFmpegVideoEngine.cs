using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using Vido.Core.Logging;
using Vido.Core.Playback;

namespace Vido.Services.Video;

/// <summary>
/// FFmpeg-based video playback engine implementing IVideoEngine.
/// Handles demuxing, decoding, frame conversion, audio output, and playback timing.
/// </summary>
public sealed unsafe class FFmpegVideoEngine : IVideoEngine
{
    private readonly ILogService _logService;

    // ── FFmpeg contexts ──
    private AVFormatContext* _formatContext;
    private AVCodecContext* _videoCodecContext;
    private AVCodecContext* _audioCodecContext;
    private int _videoStreamIndex = -1;
    private int _audioStreamIndex = -1;
    private AVStream* _videoStream;

    // ── Audio resampling & time-stretch ──
    private SwrContext* _swrContext;
    private int _audioOutSampleRate;
    private int _audioOutChannels;
    private TimeStretchProcessor? _timeStretch;

    // ── Conversion & rendering ──
    private readonly FrameConverter _frameConverter = new();
    private readonly AudioRenderer _audioRenderer = new();

    // ── Hardware-accelerated decoding ──
    // D3D11VA is tried first, then DXVA2, with automatic software fallback.
    // When active, decoded frames arrive in GPU memory and are transferred to
    // system memory via av_hwframe_transfer_data before swscale conversion.
    private AVBufferRef* _hwDeviceCtx;
    private AVPixelFormat _hwPixelFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    private bool _hwDecodingActive;

    // Keep the managed get_format delegate alive to prevent GC of the native callback.
    private AVCodecContext_get_format? _getFormatDelegate;

    // ── Threading ──
    private Thread? _decodeThread;
    private CancellationTokenSource _decodeCts = new();
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    // ── Timing ──
    private readonly Stopwatch _playbackClock = new();
    private TimeSpan _clockOffset;
    private Timer? _positionTimer;

    // ── State ──
    private PlaybackState _state = PlaybackState.None;
    private TimeSpan _position;
    private TimeSpan _duration;
    private int _volume = 75;
    private bool _isMuted;
    private bool _isLooping;
    private double _speedRatio = 1.0;
    private VideoMetadata? _currentMetadata;
    private bool _disposed;
    private readonly object _stateLock = new();

    // ── Thread-safe seek ──
    // Seek targets are posted here and consumed by the decode thread so that
    // all codec access (send_packet, receive_frame, flush_buffers) happens
    // on a single thread, avoiding the FFmpeg pthread_frame async_lock assertion.
    private long _pendingSeekTicks = long.MinValue;
    private const long NoSeekPending = long.MinValue;

    // Generation counter incremented on each seek request. Frames decoded under
    // an older generation are discarded, preventing stale frames from rendering
    // at high speed between Seek() and SeekInternal().
    private volatile uint _seekGeneration;

    // After a seek, avformat_seek_file lands on the nearest keyframe BEFORE the
    // target. Frames from that keyframe up to the target must be decoded (for
    // codec reference) but NOT rendered — this is "silent preroll."
    // _prerollTargetPts holds the seek target; frames with PTS below it are
    // decoded silently. Set to -1 when no preroll is active.
    private long _prerollTargetPts = -1;

    // After a seek, the first few decoded audio frames may contain garbled
    // samples from the codec flush. We squelch (discard) a small number of
    // audio frames to prevent audible pops/clicks.
    private volatile int _audioPrerollFrames;

    // ── Performance metrics ──
    // Tracks frame delivery rate and dropped frames for performance monitoring.
    private long _framesRendered;
    private long _framesDropped;
    private readonly Stopwatch _metricsTimer = new();
    private Timer? _metricsReportTimer;
    private const int MetricsIntervalMs = 30_000; // Log metrics every 30 seconds

    /// <summary>
    /// Creates an FFmpeg-based video engine that uses the provided log service for diagnostics and performance metrics.
    /// </summary>
    /// <param name="logService">The logging service used to report load times, decode errors, and playback metrics.</param>
    public FFmpegVideoEngine(ILogService logService)
    {
        _logService = logService;
    }

    // ── IVideoEngine State Properties ──
    /// <summary>
    /// Current playback state (None, Stopped, Playing, Paused). Fires <see cref="StateChanged"/> on transitions.
    /// </summary>
    public PlaybackState State
    {
        get { lock (_stateLock) return _state; }
        private set
        {
            lock (_stateLock)
            {
                if (_state == value) return;
                _state = value;
            }
            StateChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Current playback position within the loaded media, updated at ~60 Hz while playing.
    /// </summary>
    public TimeSpan Position
    {
        get { lock (_stateLock) return _position; }
        private set { lock (_stateLock) _position = value; }
    }

    /// <summary>
    /// Total duration of the currently loaded media file.
    /// </summary>
    public TimeSpan Duration
    {
        get { lock (_stateLock) return _duration; }
        private set { lock (_stateLock) _duration = value; }
    }

    /// <summary>
    /// Audio volume level from 0 (silent) to 100 (full), applied to the WASAPI audio renderer.
    /// </summary>
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _audioRenderer.Volume = _volume / 100f;
        }
    }

    /// <summary>
    /// When <c>true</c>, audio output is silenced without changing the <see cref="Volume"/> level.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            _audioRenderer.IsMuted = value;
        }
    }

    /// <summary>
    /// When <c>true</c>, playback automatically restarts from the beginning when the end of the media is reached.
    /// </summary>
    public bool IsLooping
    {
        get => _isLooping;
        set => _isLooping = value;
    }

    /// <summary>
    /// Playback speed multiplier (0.25–4.0). Adjusts both the video presentation clock
    /// and the SoundTouch time-stretch processor so audio pitch is preserved.
    /// </summary>
    public double SpeedRatio
    {
        get => _speedRatio;
        set
        {
            var clamped = Math.Clamp(value, 0.25, 4.0);
            if (Math.Abs(clamped - _speedRatio) < 0.001) return;

            // Snapshot current position before changing speed to prevent clock jumps
            if (_playbackClock.IsRunning)
            {
                _clockOffset = GetClockPosition();
                _playbackClock.Restart();
            }

            _speedRatio = clamped;

            // Update SoundTouch tempo so audio is time-stretched to match video speed.
            // Clear internal buffers to prevent stale stretched samples from playing.
            if (_timeStretch is not null)
            {
                _timeStretch.Tempo = clamped;
                _timeStretch.Clear();
            }

            // Flush the audio renderer buffer to avoid leftover samples at the
            // old tempo bleeding through during the transition.
            if (_state == PlaybackState.Playing)
                _audioRenderer.Flush();
        }
    }

    /// <summary>
    /// Metadata (resolution, codecs, duration, etc.) extracted from the currently loaded video file, or <c>null</c> if no file is loaded.
    /// </summary>
    public VideoMetadata? CurrentMetadata => _currentMetadata;

    // ── Events ──
    /// <summary>
    /// Raised at ~60 Hz while playing, delivering the current playback position.
    /// </summary>
    public event Action<TimeSpan>? PositionChanged;

    /// <summary>
    /// Raised when the playback state transitions between None, Stopped, Playing, and Paused.
    /// </summary>
    public event Action<PlaybackState>? StateChanged;

    /// <summary>
    /// Raised each time a decoded video frame is ready for rendering, delivering BGRA32 pixel data.
    /// </summary>
    public event Action<FrameData>? FrameReady;

    /// <summary>
    /// Raised when the media reaches its end (and looping is disabled), signaling the consumer to load the next file or stop.
    /// </summary>
    public event Action? MediaEnded;

    /// <summary>
    /// Raised after a seek operation has been processed by the decode thread and the codec buffers have been flushed.
    /// </summary>
    public event Action? SeekCompleted;

    /// <summary>
    /// Raised each time a batch of decoded audio samples is available, providing raw PCM data for visualization or haptics.
    /// </summary>
    public event Action<AudioSampleEventArgs>? AudioSamplesAvailable;

    // ── Commands ──
    /// <summary>
    /// Opens a video file, initializes FFmpeg demuxer/decoders (with optional hardware acceleration),
    /// sets up audio resampling, and extracts metadata. Any previously loaded media is stopped and released first.
    /// </summary>
    /// <param name="filePath">The absolute path to the video file to load.</param>
    /// <exception cref="InvalidOperationException">Thrown if FFmpeg has not been initialized via <c>FFmpegInitializer.Initialize()</c>.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the specified video file does not exist on disk.</exception>
    public Task LoadAsync(string filePath)
    {
        if (!FFmpegInitializer.IsInitialized)
            throw new InvalidOperationException("FFmpeg is not initialized. Call FFmpegInitializer.Initialize() first.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Video file not found.", filePath);

        return Task.Run(() =>
        {
            // Serialize concurrent loads — if two rapid Skip-Next calls arrive,
            // the second waits for the first to complete before proceeding.
            _loadLock.Wait();
            try
            {
                // Stop and cleanup run on the thread pool so the UI thread stays free.
                // Stop() joins the decode thread (up to 2 s), CleanupMedia() frees FFmpeg contexts.
                Stop();
                CleanupMedia();

                var loadTimer = Stopwatch.StartNew();
                OpenMedia(filePath);
                loadTimer.Stop();
                _logService.Info(
                    $"Loaded: {Path.GetFileName(filePath)} ({_currentMetadata?.Resolution}, " +
                    $"{_currentMetadata?.Duration:hh\\:mm\\:ss}) in {loadTimer.ElapsedMilliseconds} ms" +
                    (_hwDecodingActive ? " [HW accel]" : ""));
            }
            finally
            {
                _loadLock.Release();
            }
        });
    }

    /// <summary>
    /// Starts or resumes playback. Spawns the decode thread if not already running,
    /// starts the audio renderer, and begins the presentation clock.
    /// </summary>
    public void Play()
    {
        if (State == PlaybackState.None) return;

        if (State == PlaybackState.Stopped)
        {
            // Restart from beginning
            SeekInternal(TimeSpan.Zero);
        }

        _pauseEvent.Set();
        _audioRenderer.Play();

        if (_decodeThread == null || !_decodeThread.IsAlive)
        {
            _decodeThread = new Thread(DecodeLoop)
            {
                Name = "FFmpeg Decode Thread",
                IsBackground = true
            };
            _decodeThread.Start();
        }

        // Start the playback clock from the current position
        _clockOffset = Position;
        _playbackClock.Restart();

        StartPositionTimer();
        StartMetricsTimer();
        State = PlaybackState.Playing;
    }

    /// <summary>
    /// Pauses playback by blocking the decode thread, pausing the audio renderer,
    /// and freezing the presentation clock at the current position.
    /// </summary>
    public void Pause()
    {
        if (State != PlaybackState.Playing) return;

        _pauseEvent.Reset();
        _audioRenderer.Pause();

        // Capture current position and stop the clock
        _clockOffset = GetClockPosition();
        _playbackClock.Stop();

        StopPositionTimer();
        State = PlaybackState.Paused;
    }

    /// <summary>
    /// Stops playback completely by cancelling the decode thread, stopping the audio renderer,
    /// resetting position to zero, and preparing for a fresh <see cref="Play"/> call.
    /// </summary>
    public void Stop()
    {
        if (State == PlaybackState.None) return;

        StopPositionTimer();

        // Cancel and join the decode thread so it stops accessing codecs.
        _decodeCts.Cancel();
        _pauseEvent.Set(); // Unblock decode thread so it can exit
        _decodeThread?.Join(TimeSpan.FromSeconds(2));
        _decodeThread = null;

        // Reset CTS for the next Play() call.
        _decodeCts.Dispose();
        _decodeCts = new CancellationTokenSource();

        // Discard any pending seek that was never processed.
        Interlocked.Exchange(ref _pendingSeekTicks, NoSeekPending);

        _audioRenderer.Stop();
        _playbackClock.Stop();

        Position = TimeSpan.Zero;
        _clockOffset = TimeSpan.Zero;

        State = PlaybackState.Stopped;
    }
    
    /// <summary>
    /// Posts a seek request to the decode thread, which flushes codec buffers and repositions
    /// the demuxer to the nearest keyframe before the target. Stale pre-seek frames are discarded.
    /// </summary>
    /// <param name="position">The target playback position to seek to.</param>
    public void Seek(TimeSpan position)
    {
        if (State == PlaybackState.None) return;

        // Increment generation so the decode thread discards any in-flight
        // frames from before this seek. This is the key to preventing the
        // speed-up artifact — stale frames are never rendered.
        unchecked { _seekGeneration++; }

        // Update position for the UI (position timer / slider) but do NOT
        // reset the playback clock. The clock stays at the old position so
        // WaitForPresentationTime keeps frames paced normally until
        // SeekInternal runs on the decode thread and resets the clock there.
        Position = position;
        PositionChanged?.Invoke(position);

        // Post the seek target for the decode thread to process.
        Interlocked.Exchange(ref _pendingSeekTicks, position.Ticks);

        // Wake the decode thread if it's blocked on the pause gate.
        _pauseEvent.Set();
    }

    // ── Internal Implementation ──

    /// <summary>
    /// Opens the media file, locates video and audio streams, initializes decoders,
    /// sets up audio resampling and rendering, and extracts metadata.
    /// </summary>
    /// <param name="filePath">The absolute path of the media file to open.</param>
    /// <exception cref="InvalidOperationException">Thrown if the file cannot be opened or stream info cannot be read.</exception>
    private void OpenMedia(string filePath)
    {
        AVFormatContext* fmt = null;
        var result = ffmpeg.avformat_open_input(&fmt, filePath, null, null);
        if (result < 0)
            throw new InvalidOperationException($"Failed to open file: {FFmpegErrorString(result)}");

        _formatContext = fmt;

        result = ffmpeg.avformat_find_stream_info(_formatContext, null);
        if (result < 0)
            throw new InvalidOperationException($"Failed to find stream info: {FFmpegErrorString(result)}");

        // Find video stream
        _videoStreamIndex = FindBestStream(AVMediaType.AVMEDIA_TYPE_VIDEO);
        if (_videoStreamIndex >= 0)
        {
            _videoStream = _formatContext->streams[_videoStreamIndex];
            _videoCodecContext = OpenVideoCodec(_videoStream);
        }

        // Find audio stream
        _audioStreamIndex = FindBestStream(AVMediaType.AVMEDIA_TYPE_AUDIO);
        if (_audioStreamIndex >= 0)
        {
            var audioStream = _formatContext->streams[_audioStreamIndex];
            _audioCodecContext = OpenCodec(audioStream);
            InitializeAudioResampler();
            InitializeAudioRenderer();
        }

        // Extract metadata
        _currentMetadata = ExtractMetadata(filePath);
        Duration = _currentMetadata.Duration;
        Position = TimeSpan.Zero;
        State = PlaybackState.Stopped;
    }

    private int FindBestStream(AVMediaType mediaType)
    {
        return ffmpeg.av_find_best_stream(_formatContext, mediaType, -1, -1, null, 0);
    }

    /// <summary>
    /// Opens the video decoder with hardware acceleration (D3D11VA → DXVA2 → software fallback).
    /// If hw accel setup fails at any point, falls back to software decoding transparently.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the codec is unsupported, the codec context cannot be allocated, codec parameters cannot be copied, or the codec fails to open.</exception>
    private AVCodecContext* OpenVideoCodec(AVStream* stream)
    {
        var codecPar = stream->codecpar;
        var codec = ffmpeg.avcodec_find_decoder(codecPar->codec_id);
        if (codec == null)
            throw new InvalidOperationException($"Unsupported codec: {codecPar->codec_id}");

        var codecCtx = ffmpeg.avcodec_alloc_context3(codec);
        if (codecCtx == null)
            throw new InvalidOperationException("Failed to allocate codec context.");

        var result = ffmpeg.avcodec_parameters_to_context(codecCtx, codecPar);
        if (result < 0)
            throw new InvalidOperationException($"Failed to copy codec parameters: {FFmpegErrorString(result)}");

        // Enable multi-threaded decoding
        codecCtx->thread_count = Math.Min(Environment.ProcessorCount, 4);

        // Try hardware-accelerated decoding (D3D11VA first, then DXVA2)
        if (TrySetupHardwareDecoding(codecCtx, codec))
        {
            _logService.Info(
                $"Hardware-accelerated decoding enabled ({_hwPixelFormat})",
                "VideoEngine");
        }
        else
        {
            _logService.Info("Using software decoding", "VideoEngine");
        }

        result = ffmpeg.avcodec_open2(codecCtx, codec, null);
        if (result < 0)
            throw new InvalidOperationException($"Failed to open codec: {FFmpegErrorString(result)}");

        return codecCtx;
    }

    /// <summary>
    /// Attempts to set up hardware-accelerated decoding on the given codec context.
    /// Tries D3D11VA first, then DXVA2. Returns true if successful.
    /// </summary>
    private bool TrySetupHardwareDecoding(AVCodecContext* codecCtx, AVCodec* codec)
    {
        // Try device types in preference order
        AVHWDeviceType[] deviceTypes =
        [
            AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
        ];

        foreach (var deviceType in deviceTypes)
        {
            // Check if the codec supports this hw device type
            var hwPixFmt = FindHwPixelFormat(codec, deviceType);
            if (hwPixFmt == AVPixelFormat.AV_PIX_FMT_NONE)
                continue;

            // Create the hardware device context
            AVBufferRef* hwDeviceCtx = null;
            var result = ffmpeg.av_hwdevice_ctx_create(&hwDeviceCtx, deviceType, null, null, 0);
            if (result < 0)
            {
                _logService.Debug(
                    $"Failed to create {deviceType} device: {FFmpegErrorString(result)}",
                    "VideoEngine");
                continue;
            }

            // Set the hardware device context on the codec
            codecCtx->hw_device_ctx = ffmpeg.av_buffer_ref(hwDeviceCtx);

            // Store for cleanup and frame transfer
            _hwDeviceCtx = hwDeviceCtx;
            _hwPixelFormat = hwPixFmt;
            _hwDecodingActive = true;

            // Set the get_format callback to prefer the hw pixel format.
            // The delegate must be stored as a field to prevent GC collection.
            _getFormatDelegate = GetHwFormat;
            codecCtx->get_format = _getFormatDelegate;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the hardware pixel format supported by a codec for a given device type.
    /// Returns AV_PIX_FMT_NONE if the codec doesn't support this device type.
    /// </summary>
    private static AVPixelFormat FindHwPixelFormat(AVCodec* codec, AVHWDeviceType deviceType)
    {
        for (var i = 0; ; i++)
        {
            var config = ffmpeg.avcodec_get_hw_config(codec, i);
            if (config == null)
                break;

            if (config->device_type == deviceType &&
                (config->methods & 0x02 /* AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX */) != 0)
            {
                return config->pix_fmt;
            }
        }

        return AVPixelFormat.AV_PIX_FMT_NONE;
    }

    /// <summary>
    /// get_format callback that prefers the hardware pixel format.
    /// Called by FFmpeg during codec negotiation to select the output format.
    /// </summary>
    private AVPixelFormat GetHwFormat(AVCodecContext* ctx, AVPixelFormat* pix_fmts)
    {
        // Walk the list of offered formats and prefer the hw format
        for (var p = pix_fmts; *p != AVPixelFormat.AV_PIX_FMT_NONE; p++)
        {
            if (*p == _hwPixelFormat)
                return *p;
        }

        // Hardware format not available — fall back to software
        _logService.Warning(
            $"Hardware pixel format {_hwPixelFormat} not available, falling back to software",
            "VideoEngine");
        _hwDecodingActive = false;
        return pix_fmts[0];
    }

    /// <summary>
    /// Opens a decoder for the given audio stream, allocating a codec context and enabling multi-threaded decoding.
    /// </summary>
    /// <param name="stream">The FFmpeg audio stream to decode.</param>
    /// <exception cref="InvalidOperationException">Thrown if the codec is unsupported, the codec context cannot be allocated, codec parameters cannot be copied, or the codec fails to open.</exception>
    private AVCodecContext* OpenCodec(AVStream* stream)
    {
        var codecPar = stream->codecpar;
        var codec = ffmpeg.avcodec_find_decoder(codecPar->codec_id);
        if (codec == null)
            throw new InvalidOperationException($"Unsupported codec: {codecPar->codec_id}");

        var codecCtx = ffmpeg.avcodec_alloc_context3(codec);
        if (codecCtx == null)
            throw new InvalidOperationException("Failed to allocate codec context.");

        var result = ffmpeg.avcodec_parameters_to_context(codecCtx, codecPar);
        if (result < 0)
            throw new InvalidOperationException($"Failed to copy codec parameters: {FFmpegErrorString(result)}");

        // Enable multi-threaded decoding
        codecCtx->thread_count = Math.Min(Environment.ProcessorCount, 4);

        result = ffmpeg.avcodec_open2(codecCtx, codec, null);
        if (result < 0)
            throw new InvalidOperationException($"Failed to open codec: {FFmpegErrorString(result)}");

        return codecCtx;
    }

    /// <summary>
    /// Creates and initializes the SwrContext audio resampler and the SoundTouch
    /// time-stretch processor for the current audio codec context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the SwrContext cannot be allocated or initialized.</exception>
    private void InitializeAudioResampler()
    {
        if (_audioCodecContext == null) return;

        // Normalize uncommon sample rates to standard rates. Files with unusual
        // rates (e.g. 22050, 11025, 8000) can cause distortion when the WASAPI
        // output device expects 44100 or 48000 Hz.
        _audioOutSampleRate = _audioCodecContext->sample_rate switch
        {
            <= 22050 => 44100,
            <= 44100 => 44100,
            _ => 48000
        };
        _audioOutChannels = _audioCodecContext->ch_layout.nb_channels;
        if (_audioOutChannels <= 0) _audioOutChannels = 2;

        // Create resampler: convert from decoded format to float planar → interleaved float
        var swr = ffmpeg.swr_alloc();
        if (swr == null)
            throw new InvalidOperationException("Failed to allocate SwrContext.");

        // Set output channel layout
        AVChannelLayout outLayout;
        ffmpeg.av_channel_layout_default(&outLayout, _audioOutChannels);

        var inLayout = _audioCodecContext->ch_layout;

        ffmpeg.swr_alloc_set_opts2(&swr,
            &outLayout, AVSampleFormat.AV_SAMPLE_FMT_FLT, _audioOutSampleRate,
            &inLayout, _audioCodecContext->sample_fmt, _audioCodecContext->sample_rate,
            0, null);

        var result = ffmpeg.swr_init(swr);
        if (result < 0)
        {
            ffmpeg.swr_free(&swr);
            throw new InvalidOperationException($"Failed to initialize resampler: {FFmpegErrorString(result)}");
        }

        _swrContext = swr;

        // Create (or recreate) the SoundTouch time-stretch processor so that
        // non-1x playback speeds produce pitch-corrected audio.
        _timeStretch?.Dispose();
        _timeStretch = new TimeStretchProcessor(_audioOutSampleRate, _audioOutChannels);
        _timeStretch.Tempo = _speedRatio;
    }

    private void InitializeAudioRenderer()
    {
        _audioRenderer.Initialize(_audioOutSampleRate, _audioOutChannels);
        _audioRenderer.Volume = _volume / 100f;
        _audioRenderer.IsMuted = _isMuted;
    }

    private VideoMetadata ExtractMetadata(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        var duration = _formatContext->duration > 0
            ? TimeSpan.FromMicroseconds(_formatContext->duration)
            : TimeSpan.Zero;

        string? videoCodec = null;
        int width = 0, height = 0;
        double frameRate = 0;

        if (_videoCodecContext != null && _videoStream != null)
        {
            videoCodec = ffmpeg.avcodec_get_name(_videoCodecContext->codec_id);
            width = _videoCodecContext->width;
            height = _videoCodecContext->height;

            var rational = _videoStream->avg_frame_rate;
            if (rational.den > 0)
                frameRate = (double)rational.num / rational.den;
        }

        string? audioCodec = null;
        int audioChannels = 0;
        int audioSampleRate = 0;

        if (_audioCodecContext != null)
        {
            audioCodec = ffmpeg.avcodec_get_name(_audioCodecContext->codec_id);
            audioChannels = _audioCodecContext->ch_layout.nb_channels;
            audioSampleRate = _audioCodecContext->sample_rate;
        }

        var bitrate = _formatContext->bit_rate;
        var format = _formatContext->iformat != null
            ? Marshal.PtrToStringAnsi((IntPtr)_formatContext->iformat->name)
            : null;

        return new VideoMetadata
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length,
            Duration = duration,
            Width = width,
            Height = height,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            FrameRate = frameRate,
            Bitrate = bitrate,
            ContainerFormat = format,
            AudioChannels = audioChannels,
            AudioSampleRate = audioSampleRate
        };
    }

    // ── Decode Loop ──

    private void DecodeLoop()
    {
        var packet = ffmpeg.av_packet_alloc();
        var frame = ffmpeg.av_frame_alloc();

        try
        {
            while (!_decodeCts.IsCancellationRequested)
            {
                // Respect pause
                _pauseEvent.Wait(_decodeCts.Token);

                // Process any pending seek on this thread so all codec access
                // (send_packet / receive_frame / flush_buffers) is single-threaded.
                var seekTicks = Interlocked.Exchange(ref _pendingSeekTicks, NoSeekPending);
                if (seekTicks != NoSeekPending)
                {
                    SeekInternal(TimeSpan.FromTicks(seekTicks));
                    SeekCompleted?.Invoke();

                    // If we're paused, re-block so we wait again at the top.
                    if (State == PlaybackState.Paused)
                        _pauseEvent.Reset();

                    continue;
                }

                // Capture generation BEFORE reading the packet. If a seek arrives
                // between here and DecodeVideoPacket, the generation mismatch
                // will cause the stale packet's frames to be discarded.
                var gen = _seekGeneration;

                var readResult = ffmpeg.av_read_frame(_formatContext, packet);
                if (readResult < 0)
                {
                    // End of file
                    if (readResult == ffmpeg.AVERROR_EOF)
                    {
                        if (_isLooping)
                        {
                            // Seek back to beginning and keep decoding
                            SeekInternal(TimeSpan.Zero);
                            continue;
                        }

                        HandleMediaEnded();
                    }
                    break;
                }

                try
                {
                    if (packet->stream_index == _videoStreamIndex)
                    {
                        DecodeVideoPacket(packet, frame, gen);
                    }
                    else if (packet->stream_index == _audioStreamIndex)
                    {
                        DecodeAudioPacket(packet, frame);
                    }
                }
                finally
                {
                    ffmpeg.av_packet_unref(packet);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation during pause wait
        }
        catch (Exception ex)
        {
            _logService.Error($"Decode error: {ex.Message}");
        }
        finally
        {
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_packet_free(&packet);
        }
    }

    private void DecodeVideoPacket(AVPacket* packet, AVFrame* frame, uint generation)
    {
        // If a seek arrived since this packet was read, discard immediately.
        if (_seekGeneration != generation) return;

        if (_videoCodecContext == null) return;
        var result = ffmpeg.avcodec_send_packet(_videoCodecContext, packet);
        if (result < 0) return;

        // Pre-allocate a sw_frame for hw→sw transfer (reused across receive_frame calls)
        AVFrame* swFrame = null;
        if (_hwDecodingActive)
            swFrame = ffmpeg.av_frame_alloc();

        try
        {
            while (ffmpeg.avcodec_receive_frame(_videoCodecContext, frame) == 0)
            {
                // If a seek was requested since we started, discard stale frames.
                if (_seekGeneration != generation)
                {
                    ffmpeg.av_frame_unref(frame);
                    return;
                }

                // Silent preroll: after a seek, avformat_seek_file lands at a keyframe
                // BEFORE the target. We must decode these frames (B-frame references)
                // but not render them. Once we reach the target PTS, clear the flag.
                var prerollTarget = _prerollTargetPts;
                if (prerollTarget >= 0)
                {
                    if (frame->best_effort_timestamp < prerollTarget)
                    {
                        ffmpeg.av_frame_unref(frame);
                        continue; // Decode next frame silently
                    }
                    // Reached or passed the target -- end preroll
                    _prerollTargetPts = -1;
                }

                // If hardware decoding is active, transfer the frame from GPU to system memory.
                // The decoded frame is in a hardware-specific pixel format (e.g. D3D11) and must
                // be downloaded before swscale can convert to BGRA32.
                AVFrame* renderFrame = frame;
                if (_hwDecodingActive && (AVPixelFormat)frame->format == _hwPixelFormat && swFrame != null)
                {
                    result = ffmpeg.av_hwframe_transfer_data(swFrame, frame, 0);
                    if (result < 0)
                    {
                        // Transfer failed — fall back to software for this frame
                        ffmpeg.av_frame_unref(frame);
                        continue;
                    }
                    swFrame->best_effort_timestamp = frame->best_effort_timestamp;
                    renderFrame = swFrame;
                }

                // Configure frame converter on first frame or format change
                _frameConverter.Configure(
                    renderFrame->width, renderFrame->height,
                    (AVPixelFormat)renderFrame->format);

                // Calculate presentation timestamp
                var pts = CalculateTimestamp(renderFrame->best_effort_timestamp, _videoStream);

                var frameData = _frameConverter.Convert(renderFrame, pts);

                // Clean up frame references before potentially blocking on presentation time
                if (renderFrame == swFrame)
                    ffmpeg.av_frame_unref(swFrame);
                ffmpeg.av_frame_unref(frame);

                if (frameData != null)
                {
                    // Wait for the correct display time, but abort if a seek arrives
                    WaitForPresentationTime(pts, generation);

                    // Check generation after the wait
                    if (_seekGeneration != generation)
                    {
                        frameData.Dispose();
                        Interlocked.Increment(ref _framesDropped);
                        return;
                    }

                    Interlocked.Increment(ref _framesRendered);
                    FrameReady?.Invoke(frameData);
                }
            }
        }
        finally
        {
            if (swFrame != null)
                ffmpeg.av_frame_free(&swFrame);
        }
    }

    private void DecodeAudioPacket(AVPacket* packet, AVFrame* frame)
    {
        if (_swrContext == null || _audioCodecContext == null) return;

        // Skip garbled audio frames immediately after a seek.
        if (_audioPrerollFrames > 0)
        {
            Interlocked.Decrement(ref _audioPrerollFrames);
            return;
        }

        var result = ffmpeg.avcodec_send_packet(_audioCodecContext, packet);
        if (result < 0) return;

        while (ffmpeg.avcodec_receive_frame(_audioCodecContext, frame) == 0)
        {
            // Resample to float interleaved
            var outSamples = ffmpeg.swr_get_out_samples(_swrContext, frame->nb_samples);
            var floatCount = outSamples * _audioOutChannels;
            var resampledFloats = ArrayPool<float>.Shared.Rent(floatCount);

            try
            {
                fixed (float* pOut = resampledFloats)
                {
                    var outPtr = (byte*)pOut;
                    var converted = ffmpeg.swr_convert(
                        _swrContext,
                        &outPtr, outSamples,
                        frame->extended_data, frame->nb_samples);

                    if (converted > 0)
                    {
                        var convertedFloats = converted * _audioOutChannels;

                        if (_timeStretch is not null && Math.Abs(_timeStretch.Tempo - 1.0) > 0.001)
                        {
                            // Route through SoundTouch for pitch-corrected time-stretch.
                            _timeStretch.PutSamples(
                                new ReadOnlySpan<float>(resampledFloats, 0, convertedFloats),
                                converted);

                            // Pull as many stretched samples as are available.
                            var stretchBuf = ArrayPool<float>.Shared.Rent(floatCount * 4);
                            try
                            {
                                int received;
                                while ((received = _timeStretch.ReceiveSamples(
                                    new Span<float>(stretchBuf), stretchBuf.Length / _audioOutChannels)) > 0)
                                {
                                    var stretchedFloats = received * _audioOutChannels;
                                    _audioRenderer.SubmitSamples(stretchBuf, 0, stretchedFloats);

                                    EmitAudioSamples(stretchBuf, stretchedFloats, received);
                                }
                            }
                            finally
                            {
                                ArrayPool<float>.Shared.Return(stretchBuf);
                            }
                        }
                        else
                        {
                            // 1x speed — bypass SoundTouch, submit directly.
                            _audioRenderer.SubmitSamples(resampledFloats, 0, convertedFloats);

                            EmitAudioSamples(resampledFloats, convertedFloats, converted);
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(resampledFloats);
            }

            ffmpeg.av_frame_unref(frame);
        }
    }

    /// <summary>
    /// Fires the <see cref="AudioSamplesAvailable"/> event with the given float buffer.
    /// </summary>
    private void EmitAudioSamples(float[] floats, int floatCount, int sampleFrames)
    {
        if (AudioSamplesAvailable is null) return;

        // Convert float[] → byte[] for the event (consumers expect byte buffers).
        var byteCount = floatCount * sizeof(float);
        var bytes = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            Buffer.BlockCopy(floats, 0, bytes, 0, byteCount);
            AudioSamplesAvailable.Invoke(new AudioSampleEventArgs
            {
                Buffer = new ReadOnlyMemory<byte>(bytes, 0, byteCount),
                SampleCount = sampleFrames,
                SampleRate = _audioOutSampleRate,
                Channels = _audioOutChannels
            });
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    // ── Timing ──

    private TimeSpan CalculateTimestamp(long pts, AVStream* stream)
    {
        if (pts == ffmpeg.AV_NOPTS_VALUE || stream == null)
            return TimeSpan.Zero;

        var timeBase = stream->time_base;
        var seconds = pts * (double)timeBase.num / timeBase.den;
        return TimeSpan.FromSeconds(seconds);
    }

    private void WaitForPresentationTime(TimeSpan pts, uint generation)
    {
        if (State != PlaybackState.Playing) return;

        var clockPos = GetClockPosition();
        var delay = pts - clockPos;

        if (delay > TimeSpan.FromMilliseconds(1) && delay < TimeSpan.FromSeconds(2))
        {
            var end = Stopwatch.GetTimestamp() + (long)(delay.TotalSeconds * Stopwatch.Frequency);
            var spinThreshold = Stopwatch.Frequency / 500; // ~2ms

            while (Stopwatch.GetTimestamp() < end - spinThreshold)
            {
                if (_seekGeneration != generation || _decodeCts.IsCancellationRequested)
                    return;
                Thread.Sleep(1);
            }

            var spin = new SpinWait();
            while (Stopwatch.GetTimestamp() < end)
            {
                if (_seekGeneration != generation || _decodeCts.IsCancellationRequested)
                    return;
                spin.SpinOnce();
            }
        }
    }

    private TimeSpan GetClockPosition()
    {
        return _clockOffset + TimeSpan.FromTicks((long)(_playbackClock.Elapsed.Ticks * _speedRatio));
    }

    private void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new Timer(_ =>
        {
            if (State == PlaybackState.Playing)
            {
                Position = GetClockPosition();
                PositionChanged?.Invoke(Position);
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16)); // ~60Hz
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    // ── Performance Metrics ──

    private void StartMetricsTimer()
    {
        StopMetricsTimer();
        Interlocked.Exchange(ref _framesRendered, 0);
        Interlocked.Exchange(ref _framesDropped, 0);
        _metricsTimer.Restart();

        _metricsReportTimer = new Timer(_ => ReportMetrics(), null,
            TimeSpan.FromMilliseconds(MetricsIntervalMs),
            TimeSpan.FromMilliseconds(MetricsIntervalMs));
    }

    private void StopMetricsTimer()
    {
        _metricsReportTimer?.Dispose();
        _metricsReportTimer = null;

        // Report final metrics on stop if any frames were rendered
        if (_metricsTimer.IsRunning)
            ReportMetrics();

        _metricsTimer.Stop();
    }

    private void ReportMetrics()
    {
        if (!_logService.IsEnabled(LogLevel.Debug))
            return;

        var elapsed = _metricsTimer.Elapsed;
        if (elapsed.TotalSeconds < 1) return;

        var rendered = Interlocked.Read(ref _framesRendered);
        var dropped = Interlocked.Read(ref _framesDropped);
        var fps = rendered / elapsed.TotalSeconds;

        var memoryMb = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);

        _logService.Debug(
            $"Playback metrics — {fps:F1} fps, {rendered} rendered, {dropped} dropped, " +
            $"GC memory: {memoryMb:F1} MB" +
            (_hwDecodingActive ? " [HW accel]" : " [SW decode]"),
            "VideoEngine");
    }

    // ── Seeking ──

    private void SeekInternal(TimeSpan position)
    {
        if (_formatContext == null) return;

        _audioRenderer.Flush();

        // Squelch the first 2 audio frames after seeking — they often contain
        // garbled samples from the codec flush that cause audible pops.
        _audioPrerollFrames = 2;

        // Seek to the target position (lands on nearest keyframe BEFORE target)
        var timestamp = (long)(position.TotalSeconds * ffmpeg.AV_TIME_BASE);
        ffmpeg.avformat_seek_file(_formatContext, -1, long.MinValue, timestamp, long.MaxValue, 0);

        // Flush codec buffers
        if (_videoCodecContext != null)
            ffmpeg.avcodec_flush_buffers(_videoCodecContext);
        if (_audioCodecContext != null)
            ffmpeg.avcodec_flush_buffers(_audioCodecContext);

        // Enable silent preroll: frames from the keyframe up to the target are
        // decoded (for codec reference / B-frames) but not rendered.
        if (_videoStream != null)
        {
            var tb = _videoStream->time_base;
            _prerollTargetPts = (long)(position.TotalSeconds * tb.den / tb.num);
        }

        Position = position;
        _clockOffset = position;
        if (_playbackClock.IsRunning)
            _playbackClock.Restart();

        PositionChanged?.Invoke(position);
    }

    // ── End of Media ──

    private void HandleMediaEnded()
    {
        // Do not call Stop() here — the decode loop will exit naturally
        // after this method returns (readResult < 0 → break).
        // The MediaEnded subscriber (ViewModel) calls LoadAsync which calls Stop().
        // Calling Stop from the decode thread causes a deadlock on Join().
        _audioRenderer.Stop();
        _playbackClock.Stop();
        StopPositionTimer();
        StopMetricsTimer();
        State = PlaybackState.Stopped;
        MediaEnded?.Invoke();
    }

    // ── Cleanup ──

    private void CleanupMedia()
    {
        // Do not cancel/join decode thread or reset CTS here — Stop() already
        // handles that. CleanupMedia is only responsible for freeing FFmpeg resources.
        StopPositionTimer();
        StopMetricsTimer();
        _audioRenderer.Stop();

        _timeStretch?.Dispose();
        _timeStretch = null;

        // Free FFmpeg contexts
        if (_swrContext != null)
        {
            var swr = _swrContext;
            ffmpeg.swr_free(&swr);
            _swrContext = null;
        }

        if (_videoCodecContext != null)
        {
            var ctx = _videoCodecContext;
            ffmpeg.avcodec_free_context(&ctx);
            _videoCodecContext = null;
        }

        if (_audioCodecContext != null)
        {
            var ctx = _audioCodecContext;
            ffmpeg.avcodec_free_context(&ctx);
            _audioCodecContext = null;
        }

        if (_formatContext != null)
        {
            var fmt = _formatContext;
            ffmpeg.avformat_close_input(&fmt);
            _formatContext = null;
        }

        // Free hardware device context (must be freed after codec context)
        if (_hwDeviceCtx != null)
        {
            var hwCtx = _hwDeviceCtx;
            ffmpeg.av_buffer_unref(&hwCtx);
            _hwDeviceCtx = null;
        }

        _hwPixelFormat = AVPixelFormat.AV_PIX_FMT_NONE;
        _hwDecodingActive = false;
        _getFormatDelegate = null;

        _videoStreamIndex = -1;
        _audioStreamIndex = -1;
        _videoStream = null;

        _currentMetadata = null;
        Duration = TimeSpan.Zero;
        Position = TimeSpan.Zero;
        State = PlaybackState.None;
    }

    // ── Helpers ──

    private static string FFmpegErrorString(int error)
    {
        var bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
        return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"Error {error}";
    }

    // ── IDisposable ──
    /// <summary>
    /// Releases all FFmpeg contexts, the frame converter, the audio renderer, and threading primitives.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CleanupMedia();
        _frameConverter.Dispose();
        _audioRenderer.Dispose();
        _decodeCts.Dispose();
        _pauseEvent.Dispose();
        _loadLock.Dispose();
    }
}
