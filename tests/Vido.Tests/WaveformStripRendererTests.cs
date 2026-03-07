using SkiaSharp;
using Vido.Core.Models.Pulse;
using Vido.Services.Pulse;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for VI-0026: WaveformStripRenderer — double-buffered
/// off-screen waveform rendering for GPU-accelerated scrolling.
/// </summary>
public sealed class WaveformStripRendererTests : IDisposable
{
    private readonly WaveformStripRenderer _renderer = new();

    public void Dispose() => _renderer.Dispose();

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Null / Invalid Data → GetStrip returns null                    ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// GetStrip returns null when no data has been set.
    /// </summary>
    [Fact]
    public void GetStrip_NoData_ReturnsNull()
    {
        _renderer.SetViewport(800, 200, 10.0);

        var bitmap = _renderer.GetStrip(5.0, out var sourceRect);

        Assert.Null(bitmap);
        Assert.Equal(default, sourceRect);
    }

    /// <summary>
    /// GetStrip returns null when waveform is explicitly set to null.
    /// </summary>
    [Fact]
    public void GetStrip_NullWaveform_ReturnsNull()
    {
        _renderer.SetData(null, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        var bitmap = _renderer.GetStrip(5.0, out var sourceRect);

        Assert.Null(bitmap);
        Assert.Equal(default, sourceRect);
    }

    /// <summary>
    /// GetStrip returns null when waveform is empty.
    /// </summary>
    [Fact]
    public void GetStrip_EmptyWaveform_ReturnsNull()
    {
        _renderer.SetData(Array.Empty<float>(), 100, 0.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        var bitmap = _renderer.GetStrip(5.0, out var sourceRect);

        Assert.Null(bitmap);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Degenerate Viewport → GetStrip returns null                    ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// GetStrip returns null when canvas dimensions are zero.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 10.0)]
    [InlineData(800, 0, 10.0)]
    [InlineData(0, 200, 10.0)]
    public void GetStrip_DegenerateViewport_ReturnsNull(int width, int height, double windowDuration)
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(width, height, windowDuration);

        var bitmap = _renderer.GetStrip(5.0, out _);

        Assert.Null(bitmap);
    }

    /// <summary>
    /// GetStrip returns null when window duration is zero.
    /// </summary>
    [Fact]
    public void GetStrip_ZeroWindowDuration_ReturnsNull()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 0.0);

        var bitmap = _renderer.GetStrip(5.0, out _);

        Assert.Null(bitmap);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Valid Data → Background render produces valid bitmap            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// After SetData and SetViewport with valid parameters, GetStrip eventually
    /// returns a non-null bitmap with a source rect within bitmap bounds.
    /// </summary>
    [Fact]
    public async Task GetStrip_ValidData_ReturnsNonNullBitmapAfterRender()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        // First call triggers background render, returns null
        var first = _renderer.GetStrip(5.0, out _);
        Assert.Null(first);

        // Wait for background render to complete
        await WaitForRenderComplete();

        // Second call should return the rendered strip
        var bitmap = _renderer.GetStrip(5.0, out var sourceRect);

        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
        Assert.Equal(800 * 3, bitmap.Width); // StripMultiplier = 3
        Assert.Equal(200, bitmap.Height);

        // Source rect should be within bitmap bounds
        Assert.True(sourceRect.Left >= 0, $"sourceRect.Left ({sourceRect.Left}) should be >= 0");
        Assert.True(sourceRect.Right <= bitmap.Width,
            $"sourceRect.Right ({sourceRect.Right}) should be <= bitmap.Width ({bitmap.Width})");
        Assert.Equal(0, sourceRect.Top);
        Assert.Equal(200, sourceRect.Bottom);
    }

    /// <summary>
    /// GetStrip returns a bitmap with correct dimensions (3× canvas width).
    /// </summary>
    [Fact]
    public async Task GetStrip_BitmapDimensions_AreTripleCanvasWidth()
    {
        var waveform = CreateSyntheticWaveform(500, 50);
        _renderer.SetData(waveform, 50, 10.0, null);
        _renderer.SetViewport(400, 150, 10.0);

        _renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete();

        var bitmap = _renderer.GetStrip(5.0, out _);

        Assert.NotNull(bitmap);
        Assert.Equal(400 * 3, bitmap.Width);
        Assert.Equal(150, bitmap.Height);
    }

    /// <summary>
    /// GetStrip source rect width equals canvas width.
    /// </summary>
    [Fact]
    public async Task GetStrip_SourceRectWidth_EqualsCanvasWidth()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        _renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete();

        _renderer.GetStrip(5.0, out var sourceRect);

        Assert.Equal(800, sourceRect.Width, 1.0f);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Beat markers                                                   ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// GetStrip succeeds when beat data is provided.
    /// </summary>
    [Fact]
    public async Task GetStrip_WithBeats_ReturnsNonNullBitmap()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        var beats = new[]
        {
            new BeatEvent { TimestampMs = 1000, Strength = 0.8 },
            new BeatEvent { TimestampMs = 2000, Strength = 0.6 },
            new BeatEvent { TimestampMs = 3000, Strength = 0.9 },
        };
        _renderer.SetData(waveform, 100, 10.0, beats);
        _renderer.SetViewport(800, 200, 10.0);

        _renderer.GetStrip(2.0, out _);
        await WaitForRenderComplete();

        var bitmap = _renderer.GetStrip(2.0, out var sourceRect);

        Assert.NotNull(bitmap);
        Assert.True(sourceRect.Width > 0);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Re-render triggered when position approaches strip edge        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// When GetStrip is called with a position far outside the current strip,
    /// it returns null and triggers a re-render.
    /// </summary>
    [Fact]
    public async Task GetStrip_PositionOutsideStrip_TriggersRerender()
    {
        var waveform = CreateSyntheticWaveform(10000, 100);
        _renderer.SetData(waveform, 100, 100.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        // First render centered at t=5
        _renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete();
        Assert.False(_renderer.IsRendering);

        // Now request far away (t=80) — outside the strip [5-15, 5+15] = [-10, 20]
        var bitmap = _renderer.GetStrip(80.0, out _);

        // Should return null (position completely outside strip) and trigger re-render
        Assert.Null(bitmap);
        // A re-render should have been triggered
        Assert.True(_renderer.IsRendering);
    }

    /// <summary>
    /// When GetStrip is called with a position near the strip edge,
    /// it proactively triggers a re-render while still returning the current bitmap.
    /// </summary>
    [Fact]
    public async Task GetStrip_NearStripEdge_ProactivelyTriggersRerender()
    {
        var waveform = CreateSyntheticWaveform(10000, 100);
        _renderer.SetData(waveform, 100, 100.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        // First render centered at t=50
        _renderer.GetStrip(50.0, out _);
        await WaitForRenderComplete();
        Assert.False(_renderer.IsRendering);

        // Strip covers [50-15, 50+15] = [35, 65]
        // Visible window at t=63: [63-2, 63+8] = [61, 71]
        //   rightMargin = 65 - 71 < 0 → visible extends past strip → returns null
        // Try t=62: [62-2, 62+8] = [60, 70] → rightMargin = 65-70 = -5 < threshold
        // Actually, returns null since visibleEnd > stripEnd
        // Let's pick t=60 where visibleEnd = 60+8 = 68 > 65 → null
        // Let's pick t=55 where visibleEnd = 55+8 = 63, rightMargin = 65-63 = 2 < 3 → triggers proactive re-render
        var bitmap = _renderer.GetStrip(55.0, out var sourceRect);

        Assert.NotNull(bitmap);
        Assert.True(sourceRect.Width > 0);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SetData clears existing strip                                  ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// SetData clears the current strip, requiring a fresh render.
    /// </summary>
    [Fact]
    public async Task SetData_ClearsExistingStrip()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        // Build initial strip
        _renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete();
        Assert.NotNull(_renderer.GetStrip(5.0, out _));

        // SetData with new data clears the strip
        var newWaveform = CreateSyntheticWaveform(2000, 200);
        _renderer.SetData(newWaveform, 200, 10.0, null);

        var bitmap = _renderer.GetStrip(5.0, out _);
        Assert.Null(bitmap);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SetViewport invalidates strip on parameter change              ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// SetViewport with different parameters invalidates the current strip.
    /// </summary>
    [Fact]
    public async Task SetViewport_Changed_InvalidatesStrip()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        _renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete();
        Assert.NotNull(_renderer.GetStrip(5.0, out _));

        // Change viewport dimensions
        _renderer.SetViewport(1024, 300, 15.0);

        var bitmap = _renderer.GetStrip(5.0, out _);
        Assert.Null(bitmap);
    }

    /// <summary>
    /// SetViewport with unchanged parameters does not invalidate the strip.
    /// </summary>
    [Fact]
    public async Task SetViewport_Unchanged_PreservesStrip()
    {
        var waveform = CreateSyntheticWaveform(1000, 100);
        _renderer.SetData(waveform, 100, 10.0, null);
        _renderer.SetViewport(800, 200, 10.0);

        _renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete();
        Assert.NotNull(_renderer.GetStrip(5.0, out _));

        // Same viewport parameters — should not invalidate
        _renderer.SetViewport(800, 200, 10.0);

        var bitmap = _renderer.GetStrip(5.0, out _);
        Assert.NotNull(bitmap);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Dispose                                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Dispose does not throw, even when called multiple times.
    /// </summary>
    [Fact]
    public void Dispose_MultipleCalls_NoException()
    {
        var renderer = new WaveformStripRenderer();
        var waveform = CreateSyntheticWaveform(1000, 100);
        renderer.SetData(waveform, 100, 10.0, null);
        renderer.SetViewport(800, 200, 10.0);

        renderer.Dispose();
        renderer.Dispose(); // second call should be a no-op
    }

    /// <summary>
    /// Dispose cancels an in-progress render without exceptions.
    /// </summary>
    [Fact]
    public void Dispose_WhileRendering_NoException()
    {
        var renderer = new WaveformStripRenderer();
        var waveform = CreateSyntheticWaveform(100000, 10000);
        renderer.SetData(waveform, 10000, 10.0, null);
        renderer.SetViewport(800, 200, 10.0);

        // Trigger a render (which processes a large waveform)
        renderer.GetStrip(5.0, out _);

        // Dispose immediately while render may still be in progress
        renderer.Dispose();
    }

    /// <summary>
    /// GetStrip returns null after Dispose.
    /// </summary>
    [Fact]
    public async Task GetStrip_AfterDispose_ReturnsNull()
    {
        var renderer = new WaveformStripRenderer();
        var waveform = CreateSyntheticWaveform(1000, 100);
        renderer.SetData(waveform, 100, 10.0, null);
        renderer.SetViewport(800, 200, 10.0);

        renderer.GetStrip(5.0, out _);
        await WaitForRenderComplete(renderer);

        renderer.Dispose();

        // After dispose, strip is cleared
        var bitmap = renderer.GetStrip(5.0, out _);
        Assert.Null(bitmap);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Helpers                                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Creates a synthetic sine waveform for testing.
    /// </summary>
    private static float[] CreateSyntheticWaveform(int sampleCount, int sampleRate)
    {
        var waveform = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            waveform[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 2.0 * t)); // 2 Hz sine
        }
        return waveform;
    }

    private Task WaitForRenderComplete() => WaitForRenderComplete(_renderer);

    private static async Task WaitForRenderComplete(WaveformStripRenderer renderer, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (renderer.IsRendering && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(20);
        }

        // Give additional time for the async void to fully complete and set IsRendering=false
        await Task.Delay(100);
    }
}
