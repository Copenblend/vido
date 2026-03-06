using SkiaSharp;
using Vido.Core.Models.Pulse;

namespace Vido.Services.Pulse;

/// <summary>
/// Pre-renders the Pulse waveform to an off-screen bitmap strip for
/// GPU-accelerated scrolling. The strip covers 3× the visible window
/// and is re-rendered on a background thread when the viewport scrolls
/// past it or settings change.
/// </summary>
/// <remarks>
/// <para>Thread safety: <see cref="GetStrip"/> is called from the UI thread.
/// Rendering runs on a background thread via <see cref="Task.Run"/>. An immutable
/// <see cref="StripInfo"/> holder is swapped atomically via
/// <see cref="Interlocked.Exchange{T}(ref T, T)"/> so the UI thread never
/// reads a partially-updated state.</para>
/// <para>Each render allocates a fresh bitmap and the previous bitmap is disposed
/// after the atomic swap, avoiding back-buffer reuse races.</para>
/// </remarks>
internal sealed class WaveformStripRenderer : IDisposable
{
    // ── Theme colors (matching WaveformPanelView) ──
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1E1E1E");
    private static readonly SKColor GridLineColor = SKColor.Parse("#2A2A2A");
    private static readonly SKColor WaveformColor = SKColor.Parse("#4EC9B0");
    private static readonly SKColor WaveformFillColor = SKColor.Parse("#264EC9B0");
    private static readonly SKColor TextSecondaryColor = SKColor.Parse("#808080");
    private static readonly SKColor BeatMarkerColor = WaveformColor.WithAlpha(100);

    /// <summary>Cursor position as fraction of the canvas width (20% from left).</summary>
    internal const float CursorFraction = 0.20f;

    /// <summary>Strip width multiplier relative to canvas width.</summary>
    private const int StripMultiplier = 3;

    /// <summary>
    /// When the visible window's margin (distance from visible edge to strip edge)
    /// drops below this fraction of <c>windowDuration</c>, a proactive re-render
    /// is triggered.
    /// </summary>
    private const double RerenderMarginFraction = 0.3;

    /// <summary>Maximum waveform amplitude as fraction of half-height.</summary>
    private const float AmplitudeScale = 0.45f;

    /// <summary>Cancellation check interval during waveform path construction.</summary>
    private const int CancellationCheckInterval = 512;

    // ── Immutable snapshot for thread-safe buffer access ──

    private sealed class StripInfo
    {
        public required SKBitmap Bitmap { get; init; }
        public required double StartTime { get; init; }
        public required double EndTime { get; init; }
    }

    // ── Viewport ──
    private int _canvasWidth;
    private int _canvasHeight;
    private double _windowDuration;

    // ── Data ──
    private IReadOnlyList<float>? _waveform;
    private int _waveformSampleRate;
    private double _totalDuration;
    private IReadOnlyList<BeatEvent>? _beats;

    // ── Strip buffer (atomically swapped) ──
    private StripInfo? _currentStrip;

    // ── Render coordination ──
    private CancellationTokenSource? _renderCts;
    internal volatile bool IsRendering;
    private bool _isDisposed;

    // ── Reusable paint objects (used only on the background render thread) ──

    private readonly SKPaint _gridPaint = new()
    {
        Color = GridLineColor,
        StrokeWidth = 1,
        IsAntialias = false
    };

    private readonly SKPaint _gridQuarterPaint = new()
    {
        Color = GridLineColor.WithAlpha(80),
        StrokeWidth = 1,
        IsAntialias = false
    };

    private readonly SKPaint _waveformFillPaint = new()
    {
        Color = WaveformFillColor,
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    private readonly SKPaint _waveformLinePaint = new()
    {
        Color = WaveformColor,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f,
        IsAntialias = true
    };

    private readonly SKPaint _beatMarkerPaint = new()
    {
        Color = BeatMarkerColor,
        StrokeWidth = 1,
        IsAntialias = false
    };

    private readonly SKPaint _timeLabelPaint = new()
    {
        Color = TextSecondaryColor,
        TextSize = 10,
        IsAntialias = true
    };

    private readonly SKPaint _timeTickPaint = new()
    {
        Color = GridLineColor,
        StrokeWidth = 1,
        IsAntialias = false
    };

    // ═══════════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the waveform data source. Cancels any in-progress render and
    /// clears the current strip, forcing a fresh render on the next
    /// <see cref="GetStrip"/> call.
    /// </summary>
    /// <param name="waveform">Pre-analyzed RMS waveform samples, or <c>null</c> to clear.</param>
    /// <param name="sampleRate">Waveform sample rate (samples/second).</param>
    /// <param name="totalDurationSeconds">Total media duration in seconds.</param>
    /// <param name="beats">Pre-analyzed beat events, or <c>null</c> if none.</param>
    public void SetData(IReadOnlyList<float>? waveform, int sampleRate,
        double totalDurationSeconds, IReadOnlyList<BeatEvent>? beats)
    {
        _waveform = waveform;
        _waveformSampleRate = sampleRate;
        _totalDuration = totalDurationSeconds;
        _beats = beats;

        CancelAndClearBuffers();
    }

    /// <summary>
    /// Updates rendering parameters. Invalidates the current strip if any
    /// parameter changed.
    /// </summary>
    /// <param name="canvasWidth">Canvas width in pixels.</param>
    /// <param name="canvasHeight">Canvas height in pixels.</param>
    /// <param name="windowDurationSeconds">Visible time window in seconds.</param>
    public void SetViewport(int canvasWidth, int canvasHeight, double windowDurationSeconds)
    {
        if (_canvasWidth == canvasWidth && _canvasHeight == canvasHeight &&
            Math.Abs(_windowDuration - windowDurationSeconds) < 0.01)
            return;

        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
        _windowDuration = windowDurationSeconds;

        CancelAndClearBuffers();
    }

    /// <summary>
    /// Gets the current pre-rendered strip bitmap for the given playback position.
    /// Returns <c>null</c> if no strip has been rendered yet or if the data is invalid.
    /// If the playback position is approaching the strip boundary, a proactive
    /// background re-render is triggered.
    /// </summary>
    /// <param name="currentTimeSeconds">Current playback position in seconds.</param>
    /// <param name="sourceRect">Output: the source rectangle within the strip bitmap.</param>
    /// <returns>The strip bitmap, or <c>null</c> if not yet available.</returns>
    public SKBitmap? GetStrip(double currentTimeSeconds, out SKRect sourceRect)
    {
        sourceRect = default;

        if (_waveform == null || _waveform.Count == 0 ||
            _canvasWidth <= 0 || _canvasHeight <= 0 || _windowDuration <= 0)
            return null;

        var strip = _currentStrip; // atomic snapshot

        if (strip != null)
        {
            double stripDuration = strip.EndTime - strip.StartTime;
            if (stripDuration <= 0)
                return null;

            // Compute the visible time window
            double visibleStart = currentTimeSeconds - _windowDuration * CursorFraction;
            double visibleEnd = visibleStart + _windowDuration;

            // If visible window is completely outside the strip, return null (triggers fallback)
            if (visibleEnd < strip.StartTime || visibleStart > strip.EndTime)
            {
                if (!IsRendering)
                    TriggerRenderAsync(currentTimeSeconds);
                return null;
            }

            double pixelsPerSecond = strip.Bitmap.Width / stripDuration;
            float srcX = (float)((visibleStart - strip.StartTime) * pixelsPerSecond);

            // Clamp source rect within bitmap bounds
            srcX = Math.Clamp(srcX, 0, Math.Max(0, strip.Bitmap.Width - _canvasWidth));

            sourceRect = new SKRect(srcX, 0, srcX + _canvasWidth, _canvasHeight);

            // Check if proactive re-render is needed (viewport approaching strip edge)
            double leftMargin = visibleStart - strip.StartTime;
            double rightMargin = strip.EndTime - visibleEnd;
            double threshold = _windowDuration * RerenderMarginFraction;

            if ((leftMargin < threshold || rightMargin < threshold) && !IsRendering)
            {
                TriggerRenderAsync(currentTimeSeconds);
            }

            return strip.Bitmap;
        }

        // No strip available — trigger initial render
        if (!IsRendering)
        {
            TriggerRenderAsync(currentTimeSeconds);
        }

        return null;
    }

    /// <summary>
    /// Releases the strip bitmap and cancels any pending render.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CancelAndClearBuffers();

        // Paint objects are small and have finalizers. Not explicitly disposed
        // here to avoid race conditions with an in-flight background render
        // that may still be using them.
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Render coordination
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cancels any in-progress render and clears the current strip buffer.
    /// Called from the UI thread in response to data or viewport changes.
    /// </summary>
    private void CancelAndClearBuffers()
    {
        var oldCts = Interlocked.Exchange(ref _renderCts, null);
        oldCts?.Cancel();
        oldCts?.Dispose();

        var old = Interlocked.Exchange(ref _currentStrip, null);
        old?.Bitmap.Dispose();
    }

    /// <summary>
    /// Fire-and-forget: schedules a background strip render centered on the
    /// given time position. Cancels any previously-scheduled render.
    /// </summary>
    private async void TriggerRenderAsync(double centerTime)
    {
        if (IsRendering || _isDisposed) return;
        IsRendering = true;

        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _renderCts, cts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        // Capture locals for isolation on the background thread
        var waveform = _waveform;
        var sampleRate = _waveformSampleRate;
        var beats = _beats;
        var totalDuration = _totalDuration;
        var canvasWidth = _canvasWidth;
        var canvasHeight = _canvasHeight;
        var windowDuration = _windowDuration;
        var ct = cts.Token;

        if (waveform == null || canvasWidth <= 0 || canvasHeight <= 0 || windowDuration <= 0)
        {
            IsRendering = false;
            return;
        }

        try
        {
            await Task.Run(() => RenderStrip(
                waveform, sampleRate, beats, totalDuration,
                canvasWidth, canvasHeight, windowDuration, centerTime, ct), ct);
        }
        catch (OperationCanceledException)
        {
            // Expected when SetData/SetViewport cancels an in-progress render.
        }
        catch (Exception)
        {
            // Rendering failed — keep the existing front buffer (if any).
        }
        finally
        {
            IsRendering = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Background strip rendering
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Renders the waveform, grid, beat markers, and time labels to a new
    /// <see cref="SKBitmap"/> covering 3× the visible window centered on
    /// <paramref name="centerTime"/>. On success, the new bitmap is atomically
    /// swapped into <see cref="_currentStrip"/>.
    /// </summary>
    private void RenderStrip(
        IReadOnlyList<float> waveform, int sampleRate,
        IReadOnlyList<BeatEvent>? beats, double totalDuration,
        int canvasWidth, int canvasHeight, double windowDuration,
        double centerTime, CancellationToken ct)
    {
        double stripStartTime = centerTime - 1.5 * windowDuration;
        double stripEndTime = centerTime + 1.5 * windowDuration;
        int stripWidth = canvasWidth * StripMultiplier;

        var bitmap = new SKBitmap(stripWidth, canvasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(BackgroundColor);

            ct.ThrowIfCancellationRequested();

            DrawGridLines(canvas, stripWidth, canvasHeight);

            ct.ThrowIfCancellationRequested();

            DrawWaveform(canvas, waveform, sampleRate, stripWidth, canvasHeight,
                stripStartTime, stripEndTime, ct);

            ct.ThrowIfCancellationRequested();

            if (beats != null && beats.Count > 0)
            {
                DrawBeatMarkers(canvas, beats, stripWidth, canvasHeight,
                    stripStartTime, stripEndTime);
            }

            ct.ThrowIfCancellationRequested();

            DrawTimeLabels(canvas, stripWidth, canvasHeight,
                stripStartTime, stripEndTime, windowDuration);

            canvas.Flush();

            ct.ThrowIfCancellationRequested();

            // Atomically swap the new strip into place
            var newStrip = new StripInfo
            {
                Bitmap = bitmap,
                StartTime = stripStartTime,
                EndTime = stripEndTime
            };
            var old = Interlocked.Exchange(ref _currentStrip, newStrip);
            old?.Bitmap.Dispose();
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Drawing methods (run on background thread)
    // ═══════════════════════════════════════════════════════════════════

    private void DrawGridLines(SKCanvas canvas, int stripWidth, int canvasHeight)
    {
        float midY = canvasHeight / 2f;
        canvas.DrawLine(0, midY, stripWidth, midY, _gridPaint);
        canvas.DrawLine(0, canvasHeight * 0.25f, stripWidth, canvasHeight * 0.25f, _gridQuarterPaint);
        canvas.DrawLine(0, canvasHeight * 0.75f, stripWidth, canvasHeight * 0.75f, _gridQuarterPaint);
    }

    private void DrawWaveform(
        SKCanvas canvas, IReadOnlyList<float> waveform, int sampleRate,
        int stripWidth, int canvasHeight,
        double stripStartTime, double stripEndTime, CancellationToken ct)
    {
        if (sampleRate <= 0) return;

        float midY = canvasHeight / 2f;
        float maxAmplitude = canvasHeight * AmplitudeScale;

        double stripDuration = stripEndTime - stripStartTime;
        double pixelsPerSecond = stripWidth / stripDuration;

        // Clamp sample range to valid waveform data
        int startSample = Math.Max(0, (int)(stripStartTime * sampleRate));
        int endSample = Math.Min(waveform.Count - 1, (int)(stripEndTime * sampleRate));

        if (startSample >= endSample) return;

        // Downsample to ~2 points per pixel for performance
        int samplesInRange = endSample - startSample + 1;
        int step = Math.Max(1, samplesInRange / (stripWidth * 2));

        using var fillPath = new SKPath();
        using var topPath = new SKPath();
        using var bottomPath = new SKPath();

        bool pathStarted = false;
        int checkCounter = 0;

        for (int i = startSample; i <= endSample; i += step)
        {
            if (++checkCounter % CancellationCheckInterval == 0)
                ct.ThrowIfCancellationRequested();

            double sampleTime = (double)i / sampleRate;
            float x = (float)((sampleTime - stripStartTime) * pixelsPerSecond);
            float amplitude = Math.Abs(waveform[i]);
            float yOffset = amplitude * maxAmplitude;

            if (!pathStarted)
            {
                topPath.MoveTo(x, midY - yOffset);
                fillPath.MoveTo(x, midY - yOffset);
                bottomPath.MoveTo(x, midY + yOffset);
                pathStarted = true;
            }
            else
            {
                topPath.LineTo(x, midY - yOffset);
                fillPath.LineTo(x, midY - yOffset);
                bottomPath.LineTo(x, midY + yOffset);
            }
        }

        if (!pathStarted) return;

        // Mirror back for fill closure (bottom half)
        for (int i = endSample; i >= startSample; i -= step)
        {
            if (++checkCounter % CancellationCheckInterval == 0)
                ct.ThrowIfCancellationRequested();

            double sampleTime = (double)i / sampleRate;
            float x = (float)((sampleTime - stripStartTime) * pixelsPerSecond);
            float amplitude = Math.Abs(waveform[i]);
            float yOffset = amplitude * maxAmplitude;
            fillPath.LineTo(x, midY + yOffset);
        }
        fillPath.Close();

        canvas.DrawPath(fillPath, _waveformFillPaint);
        canvas.DrawPath(topPath, _waveformLinePaint);
        canvas.DrawPath(bottomPath, _waveformLinePaint);
    }

    private void DrawBeatMarkers(
        SKCanvas canvas, IReadOnlyList<BeatEvent> beats,
        int stripWidth, int canvasHeight,
        double stripStartTime, double stripEndTime)
    {
        double stripDuration = stripEndTime - stripStartTime;
        if (stripDuration <= 0) return;

        double pixelsPerSecond = stripWidth / stripDuration;
        float markerTop = canvasHeight * 0.85f;
        float markerBottom = canvasHeight * 0.95f;

        for (int i = 0; i < beats.Count; i++)
        {
            double beatTimeSec = beats[i].TimestampMs / 1000.0;
            if (beatTimeSec < stripStartTime) continue;
            if (beatTimeSec > stripEndTime) break;

            float x = (float)((beatTimeSec - stripStartTime) * pixelsPerSecond);
            canvas.DrawLine(x, markerTop, x, markerBottom, _beatMarkerPaint);
        }
    }

    private void DrawTimeLabels(
        SKCanvas canvas, int stripWidth, int canvasHeight,
        double stripStartTime, double stripEndTime, double windowDuration)
    {
        // Choose tick interval based on window duration (same logic as WaveformPanelView)
        double tickInterval = windowDuration switch
        {
            <= 15 => 2,
            <= 45 => 5,
            <= 120 => 10,
            <= 300 => 30,
            _ => 60
        };

        double stripDuration = stripEndTime - stripStartTime;
        if (stripDuration <= 0) return;

        double pixelsPerSecond = stripWidth / stripDuration;

        double firstTick = Math.Ceiling(stripStartTime / tickInterval) * tickInterval;
        for (double t = firstTick; t <= stripEndTime; t += tickInterval)
        {
            float x = (float)((t - stripStartTime) * pixelsPerSecond);

            // Tick mark
            canvas.DrawLine(x, canvasHeight - 12, x, canvasHeight, _timeTickPaint);

            // Time label
            var ts = TimeSpan.FromSeconds(Math.Max(0, t));
            string label = ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                : $"{ts.Seconds}s";
            float labelWidth = _timeLabelPaint.MeasureText(label);
            canvas.DrawText(label, x - labelWidth / 2, canvasHeight - 1, _timeLabelPaint);
        }
    }
}
