using System.Collections.Concurrent;
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
    private AVStream* _audioStream;

    // ── Audio resampling ──
    private SwrContext* _swrContext;
    private int _audioOutSampleRate;
    private int _audioOutChannels;

    // ── Conversion & rendering ──
    private readonly FrameConverter _frameConverter = new();
    private readonly AudioRenderer _audioRenderer = new();

    // ── Threading ──
    private Thread? _decodeThread;
    private readonly CancellationTokenSource _decodeCts = new();
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private readonly ConcurrentQueue<FrameData> _videoFrameQueue = new();
    private readonly ConcurrentQueue<byte[]> _audioSampleQueue = new();
    private const int MaxVideoQueueSize = 8;
    private const int MaxAudioQueueSize = 16;

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
    private VideoMetadata? _currentMetadata;
    private bool _disposed;
    private readonly object _stateLock = new();
    private volatile bool _isSeeking;

    public FFmpegVideoEngine(ILogService logService)
    {
        _logService = logService;
    }

    // ── IVideoEngine State Properties ──

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

    public TimeSpan Position
    {
        get { lock (_stateLock) return _position; }
        private set { lock (_stateLock) _position = value; }
    }

    public TimeSpan Duration
    {
        get { lock (_stateLock) return _duration; }
        private set { lock (_stateLock) _duration = value; }
    }

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _audioRenderer.Volume = _volume / 100f;
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            _audioRenderer.IsMuted = value;
        }
    }

    public bool IsLooping
    {
        get => _isLooping;
        set => _isLooping = value;
    }

    public VideoMetadata? CurrentMetadata => _currentMetadata;

    // ── Events ──
    public event Action<TimeSpan>? PositionChanged;
    public event Action<PlaybackState>? StateChanged;
    public event Action<FrameData>? FrameReady;
    public event Action? MediaEnded;

    // ── Commands ──

    public Task LoadAsync(string filePath)
    {
        if (!FFmpegInitializer.IsInitialized)
            throw new InvalidOperationException("FFmpeg is not initialized. Call FFmpegInitializer.Initialize() first.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Video file not found.", filePath);

        // Stop any current playback
        Stop();
        CleanupMedia();

        return Task.Run(() =>
        {
            OpenMedia(filePath);
            _logService.Info($"Loaded: {Path.GetFileName(filePath)} ({_currentMetadata?.Resolution}, {_currentMetadata?.Duration:hh\\:mm\\:ss})");
        });
    }

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
        State = PlaybackState.Playing;
    }

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

    public void Stop()
    {
        if (State == PlaybackState.None) return;

        StopPositionTimer();
        _pauseEvent.Set(); // Unblock decode thread so it can exit

        _audioRenderer.Stop();
        _playbackClock.Stop();

        Position = TimeSpan.Zero;
        _clockOffset = TimeSpan.Zero;

        // Clear queues
        while (_videoFrameQueue.TryDequeue(out _)) { }
        while (_audioSampleQueue.TryDequeue(out _)) { }

        State = PlaybackState.Stopped;
    }

    public void Seek(TimeSpan position)
    {
        if (State == PlaybackState.None) return;
        SeekInternal(position);
    }

    // ── Internal Implementation ──

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
            _videoCodecContext = OpenCodec(_videoStream);
        }

        // Find audio stream
        _audioStreamIndex = FindBestStream(AVMediaType.AVMEDIA_TYPE_AUDIO);
        if (_audioStreamIndex >= 0)
        {
            _audioStream = _formatContext->streams[_audioStreamIndex];
            _audioCodecContext = OpenCodec(_audioStream);
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

    private void InitializeAudioResampler()
    {
        if (_audioCodecContext == null) return;

        _audioOutSampleRate = _audioCodecContext->sample_rate;
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

                if (_isSeeking)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // Throttle if queues are full
                if (_videoFrameQueue.Count >= MaxVideoQueueSize)
                {
                    Thread.Sleep(1);
                    continue;
                }

                var readResult = ffmpeg.av_read_frame(_formatContext, packet);
                if (readResult < 0)
                {
                    // End of file or error
                    if (readResult == ffmpeg.AVERROR_EOF)
                    {
                        HandleMediaEnded();
                    }
                    break;
                }

                try
                {
                    if (packet->stream_index == _videoStreamIndex)
                    {
                        DecodeVideoPacket(packet, frame);
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

    private void DecodeVideoPacket(AVPacket* packet, AVFrame* frame)
    {
        var result = ffmpeg.avcodec_send_packet(_videoCodecContext, packet);
        if (result < 0) return;

        while (ffmpeg.avcodec_receive_frame(_videoCodecContext, frame) == 0)
        {
            // Configure frame converter on first frame or format change
            _frameConverter.Configure(
                frame->width, frame->height,
                (AVPixelFormat)frame->format);

            // Calculate presentation timestamp
            var pts = CalculateTimestamp(frame->best_effort_timestamp, _videoStream);

            var frameData = _frameConverter.Convert(frame, pts);
            if (frameData != null)
            {
                // Wait for the correct display time
                WaitForPresentationTime(pts);
                _videoFrameQueue.Enqueue(frameData);
                FrameReady?.Invoke(frameData);
            }

            ffmpeg.av_frame_unref(frame);
        }
    }

    private void DecodeAudioPacket(AVPacket* packet, AVFrame* frame)
    {
        if (_swrContext == null) return;

        var result = ffmpeg.avcodec_send_packet(_audioCodecContext, packet);
        if (result < 0) return;

        while (ffmpeg.avcodec_receive_frame(_audioCodecContext, frame) == 0)
        {
            // Resample to float interleaved
            var outSamples = ffmpeg.swr_get_out_samples(_swrContext, frame->nb_samples);
            var outBufferSize = outSamples * _audioOutChannels * sizeof(float);
            var outBuffer = new byte[outBufferSize];

            fixed (byte* pOut = outBuffer)
            {
                var outPtr = pOut;
                var converted = ffmpeg.swr_convert(
                    _swrContext,
                    &outPtr, outSamples,
                    frame->extended_data, frame->nb_samples);

                if (converted > 0)
                {
                    var actualSize = converted * _audioOutChannels * sizeof(float);
                    _audioRenderer.SubmitSamples(outBuffer, 0, actualSize);
                }
            }

            ffmpeg.av_frame_unref(frame);
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

    private void WaitForPresentationTime(TimeSpan pts)
    {
        if (State != PlaybackState.Playing) return;

        var clockPos = GetClockPosition();
        var delay = pts - clockPos;

        if (delay > TimeSpan.FromMilliseconds(1) && delay < TimeSpan.FromSeconds(2))
        {
            // Use SpinWait for short waits, Thread.Sleep for longer
            if (delay.TotalMilliseconds < 5)
                Thread.SpinWait((int)(delay.TotalMilliseconds * 1000));
            else
                Thread.Sleep(delay - TimeSpan.FromMilliseconds(1));
        }
    }

    private TimeSpan GetClockPosition()
    {
        return _clockOffset + _playbackClock.Elapsed;
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

    // ── Seeking ──

    private void SeekInternal(TimeSpan position)
    {
        if (_formatContext == null) return;

        _isSeeking = true;

        try
        {
            // Clear queues
            while (_videoFrameQueue.TryDequeue(out _)) { }
            while (_audioSampleQueue.TryDequeue(out _)) { }
            _audioRenderer.Flush();

            // Seek to the target position
            var timestamp = (long)(position.TotalSeconds * ffmpeg.AV_TIME_BASE);
            ffmpeg.avformat_seek_file(_formatContext, -1, long.MinValue, timestamp, long.MaxValue, 0);

            // Flush codec buffers
            if (_videoCodecContext != null)
                ffmpeg.avcodec_flush_buffers(_videoCodecContext);
            if (_audioCodecContext != null)
                ffmpeg.avcodec_flush_buffers(_audioCodecContext);

            Position = position;
            _clockOffset = position;
            if (_playbackClock.IsRunning)
                _playbackClock.Restart();

            PositionChanged?.Invoke(position);
        }
        finally
        {
            _isSeeking = false;
        }
    }

    // ── End of Media ──

    private void HandleMediaEnded()
    {
        MediaEnded?.Invoke();

        if (_isLooping)
        {
            SeekInternal(TimeSpan.Zero);
            return;
        }

        Stop();
    }

    // ── Cleanup ──

    private void CleanupMedia()
    {
        // Stop decode thread
        _decodeCts.Cancel();
        _pauseEvent.Set();
        _decodeThread?.Join(TimeSpan.FromSeconds(2));

        StopPositionTimer();
        _audioRenderer.Stop();

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

        _videoStreamIndex = -1;
        _audioStreamIndex = -1;
        _videoStream = null;
        _audioStream = null;

        // Clear queues
        while (_videoFrameQueue.TryDequeue(out _)) { }
        while (_audioSampleQueue.TryDequeue(out _)) { }

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CleanupMedia();
        _frameConverter.Dispose();
        _audioRenderer.Dispose();
        _decodeCts.Dispose();
        _pauseEvent.Dispose();
    }
}
