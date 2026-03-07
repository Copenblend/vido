using SkiaSharp;
using Vido.Core.Events;
using Vido.Core.Haptics;
using Vido.Core.Logging;
using Vido.Core.Models.Pulse;
using Vido.Core.Playback;
using Vido.Services.Events;
using Vido.Services.Pulse;
using NSubstitute;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for Pulse real-time services (PI-013):
/// AudioRingBuffer, LiveAmplitudeService, PulseTCodeMapper, PulseEngine, PulseBeatSource.
/// </summary>
public class PulseRealtimeServiceTests
{
    // ══════════════════════════════════════════════
    //  AudioRingBuffer Tests
    // ══════════════════════════════════════════════

    [Fact]
    public void AudioRingBuffer_Constructor_ValidCapacity_CreatesInstance()
    {
        var buffer = new AudioRingBuffer(1024);
        Assert.Equal(1024, buffer.Capacity);
        Assert.Equal(0, buffer.Available);
    }

    [Fact]
    public void AudioRingBuffer_Constructor_ZeroCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(0));
    }

    [Fact]
    public void AudioRingBuffer_Constructor_NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(-5));
    }

    [Fact]
    public void AudioRingBuffer_Write_EmptySpan_NoOp()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Write(ReadOnlySpan<float>.Empty);
        Assert.Equal(0, buffer.Available);
    }

    [Fact]
    public void AudioRingBuffer_Write_ThenRead_ReturnsCorrectData()
    {
        var buffer = new AudioRingBuffer(100);
        var data = new float[] { 1f, 2f, 3f, 4f, 5f };
        buffer.Write(data);

        Assert.Equal(5, buffer.Available);

        var output = new float[5];
        int read = buffer.Read(output);

        Assert.Equal(5, read);
        Assert.Equal(data, output);
        Assert.Equal(0, buffer.Available);
    }

    [Fact]
    public void AudioRingBuffer_Write_MultipleThenRead_ReturnsAllData()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Write(new float[] { 1f, 2f });
        buffer.Write(new float[] { 3f, 4f });

        Assert.Equal(4, buffer.Available);

        var output = new float[4];
        int read = buffer.Read(output);
        Assert.Equal(4, read);
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f }, output);
    }

    [Fact]
    public void AudioRingBuffer_Read_EmptyBuffer_ReturnsZero()
    {
        var buffer = new AudioRingBuffer(100);
        var output = new float[10];
        int read = buffer.Read(output);
        Assert.Equal(0, read);
    }

    [Fact]
    public void AudioRingBuffer_Read_EmptyOutput_ReturnsZero()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Write(new float[] { 1f, 2f });
        int read = buffer.Read(Span<float>.Empty);
        Assert.Equal(0, read);
    }

    [Fact]
    public void AudioRingBuffer_Read_PartialRead_LeavesRemainder()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Write(new float[] { 1f, 2f, 3f, 4f, 5f });

        var output = new float[3];
        int read = buffer.Read(output);
        Assert.Equal(3, read);
        Assert.Equal(new float[] { 1f, 2f, 3f }, output);
        Assert.Equal(2, buffer.Available);
    }

    [Fact]
    public void AudioRingBuffer_Write_OverflowDropsOldest()
    {
        var buffer = new AudioRingBuffer(5);
        buffer.Write(new float[] { 1f, 2f, 3f, 4f });

        // Fill to near capacity (4 of 5, since capacity-1 is max usable)
        Assert.Equal(4, buffer.Available);

        // Write 3 more — should drop oldest to make room
        buffer.Write(new float[] { 5f, 6f, 7f });

        // Read all available data — should contain most recent samples
        var output = new float[10];
        int read = buffer.Read(output);
        Assert.True(read > 0);

        // Should contain 7f (the most recent sample)
        Assert.Contains(7f, output.AsSpan(0, read).ToArray());
    }

    [Fact]
    public void AudioRingBuffer_Write_ExceedsCapacity_KeepsLatest()
    {
        var buffer = new AudioRingBuffer(4);
        var data = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
        buffer.Write(data);

        // When writing more than capacity, only keep the last capacity samples
        var output = new float[10];
        int read = buffer.Read(output);
        Assert.True(read > 0 && read <= 4);
    }

    [Fact]
    public void AudioRingBuffer_Clear_ResetsBuffer()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Write(new float[] { 1f, 2f, 3f });
        Assert.Equal(3, buffer.Available);

        buffer.Clear();
        Assert.Equal(0, buffer.Available);

        var output = new float[3];
        int read = buffer.Read(output);
        Assert.Equal(0, read);
    }

    [Fact]
    public void AudioRingBuffer_WrapAround_WorksCorrectly()
    {
        var buffer = new AudioRingBuffer(8);

        // Fill and drain to advance internal position near the end
        buffer.Write(new float[] { 1f, 2f, 3f, 4f, 5f });
        var discard = new float[5];
        buffer.Read(discard);

        // Now write data that wraps around
        var data = new float[] { 10f, 20f, 30f, 40f, 50f };
        buffer.Write(data);

        Assert.Equal(5, buffer.Available);

        var output = new float[5];
        int read = buffer.Read(output);
        Assert.Equal(5, read);
        Assert.Equal(data, output);
    }

    [Fact]
    public async Task AudioRingBuffer_ConcurrentWriteRead_DoesNotCorrupt()
    {
        var buffer = new AudioRingBuffer(4096);
        int totalWritten = 0;
        int totalRead = 0;
        bool done = false;

        var writer = Task.Run(() =>
        {
            var chunk = new float[64];
            for (int i = 0; i < 100; i++)
            {
                Array.Fill(chunk, (float)i);
                buffer.Write(chunk);
                Interlocked.Add(ref totalWritten, chunk.Length);
                Thread.SpinWait(10);
            }
            done = true;
        });

        var reader = Task.Run(() =>
        {
            var output = new float[64];
            while (!done || buffer.Available > 0)
            {
                int read = buffer.Read(output);
                Interlocked.Add(ref totalRead, read);
                if (read == 0) Thread.SpinWait(100);
            }
        });

        await Task.WhenAll(writer, reader);

        // All data should have been written; most should have been read
        Assert.Equal(6400, totalWritten);
        Assert.True(totalRead > 0, "Reader should have read some data");
    }

    // ══════════════════════════════════════════════
    //  LiveAmplitudeService Tests
    // ══════════════════════════════════════════════

    [Fact]
    public void LiveAmplitudeService_Constructor_DefaultParams_CreatesInstance()
    {
        var svc = new LiveAmplitudeService();
        Assert.Equal(0, svc.CurrentAmplitude);
    }

    [Fact]
    public void LiveAmplitudeService_Start_Stop_NoThrow()
    {
        var svc = new LiveAmplitudeService();
        svc.Start();
        svc.Stop();
    }

    [Fact]
    public void LiveAmplitudeService_Reset_ClearsState()
    {
        var svc = new LiveAmplitudeService();
        svc.Start();

        // Submit some samples
        var mono = new float[960];
        Array.Fill(mono, 0.5f);
        var stereo = MonoToStereoBytes(mono);

        svc.SubmitSamples(new ReadOnlyMemory<byte>(stereo), 960, 48000, 2);
        svc.ProcessAvailable(0);

        // Reset
        svc.Reset();
        Assert.Equal(0, svc.CurrentAmplitude);
    }

    [Fact]
    public void LiveAmplitudeService_SubmitSamples_WhenNotRunning_NoOp()
    {
        var svc = new LiveAmplitudeService();
        // Not started — should not throw
        var bytes = new byte[256];
        svc.SubmitSamples(new ReadOnlyMemory<byte>(bytes), 32, 44100, 2);
        svc.ProcessAvailable(0);
        Assert.Equal(0, svc.CurrentAmplitude);
    }

    [Fact]
    public void LiveAmplitudeService_SubmitSamples_InvalidParams_NoOp()
    {
        var svc = new LiveAmplitudeService();
        svc.Start();

        // Invalid sample rate
        svc.SubmitSamples(new ReadOnlyMemory<byte>(new byte[64]), 8, 0, 2);
        // Invalid channels
        svc.SubmitSamples(new ReadOnlyMemory<byte>(new byte[64]), 8, 44100, 0);
        // Invalid sample count
        svc.SubmitSamples(new ReadOnlyMemory<byte>(new byte[64]), 0, 44100, 2);
    }

    [Fact]
    public void LiveAmplitudeService_SubmitAndProcess_ProducesAmplitude()
    {
        var svc = new LiveAmplitudeService(bufferCapacity: 48000, windowMs: 10);
        svc.Start();

        // Generate stereo audio at 0.5 amplitude
        var mono = new float[4800]; // 100ms at 48kHz
        Array.Fill(mono, 0.5f);
        var stereoBytes = MonoToStereoBytes(mono);

        svc.SubmitSamples(new ReadOnlyMemory<byte>(stereoBytes), 4800, 48000, 2);
        svc.ProcessAvailable(50.0);

        Assert.True(svc.CurrentAmplitude > 0, "Should have non-zero amplitude after processing audio");
    }

    [Fact]
    public void LiveAmplitudeService_AmplitudeUpdated_FiresEvent()
    {
        var svc = new LiveAmplitudeService(bufferCapacity: 48000, windowMs: 10);
        svc.Start();

        double? receivedAmplitude = null;
        svc.AmplitudeUpdated += amp => receivedAmplitude = amp;

        var mono = new float[4800];
        Array.Fill(mono, 0.5f);
        var stereoBytes = MonoToStereoBytes(mono);

        svc.SubmitSamples(new ReadOnlyMemory<byte>(stereoBytes), 4800, 48000, 2);
        svc.ProcessAvailable(50.0);

        Assert.NotNull(receivedAmplitude);
        Assert.True(receivedAmplitude > 0);
    }

    [Fact]
    public void LiveAmplitudeService_ProcessAvailable_WhenNotRunning_NoOp()
    {
        var svc = new LiveAmplitudeService();
        // Not started — should not throw
        svc.ProcessAvailable(100);
        Assert.Equal(0, svc.CurrentAmplitude);
    }

    // ══════════════════════════════════════════════
    //  PulseTCodeMapper Tests
    // ══════════════════════════════════════════════

    private readonly PulseTCodeMapper _mapper = new();

    private static BeatMap MakeBeatMap(double bpm, int beatCount, double strength = 1.0)
    {
        double intervalMs = 60000.0 / bpm;
        var beats = new List<BeatEvent>();
        for (int i = 0; i < beatCount; i++)
        {
            beats.Add(new BeatEvent
            {
                TimestampMs = i * intervalMs,
                Strength = strength,
                IsQuantized = false
            });
        }
        return new BeatMap
        {
            Beats = beats.AsReadOnly(),
            Bpm = bpm,
            BpmConfidence = 0.9,
            DurationMs = beatCount * intervalMs
        };
    }

    [Fact]
    public void PulseTCodeMapper_NullBeatMap_ReturnsRestPosition()
    {
        double pos = _mapper.MapToPosition(null, 1000, 0.5);
        Assert.Equal(50.0, pos);
    }

    [Fact]
    public void PulseTCodeMapper_EmptyBeats_ReturnsRestPosition()
    {
        var map = new BeatMap { Beats = Array.Empty<BeatEvent>() };
        double pos = _mapper.MapToPosition(map, 1000, 0.5);
        Assert.Equal(50.0, pos);
    }

    [Fact]
    public void PulseTCodeMapper_WithOutBeatIndex_BeforeFirstBeat_ReturnsNeg1()
    {
        var map = MakeBeatMap(120, 10);
        _ = _mapper.MapToPosition(map, -100, 0.5, out int beatIndex);
        Assert.Equal(-1, beatIndex);
    }

    [Fact]
    public void PulseTCodeMapper_WithOutBeatIndex_OnBeat_ReturnsMatchingIndex()
    {
        var map = MakeBeatMap(120, 10);
        _ = _mapper.MapToPosition(map, 1000, 0.5, out int beatIndex);
        Assert.Equal(2, beatIndex); // 1000ms / 500ms interval = 2
    }

    [Fact]
    public void PulseTCodeMapper_WithOutBeatIndex_AfterLastBeat_ReturnsLastIndex()
    {
        var map = MakeBeatMap(120, 5);
        _ = _mapper.MapToPosition(map, 10_000, 0.5, out int beatIndex);
        Assert.Equal(4, beatIndex);
    }

    [Fact]
    public void PulseTCodeMapper_BeforeFirstBeat_ReturnsRestPosition()
    {
        var map = MakeBeatMap(120, 10);
        double pos = _mapper.MapToPosition(map, -100, 0.5);
        Assert.Equal(50.0, pos);
    }

    [Fact]
    public void PulseTCodeMapper_AlwaysWithinRange()
    {
        var map = MakeBeatMap(120, 20);
        for (double t = 0; t < 10000; t += 16.67)
        {
            double pos = _mapper.MapToPosition(map, t, 0.8);
            Assert.True(pos >= 5.0 && pos <= 95.0,
                $"Position {pos:F1} at t={t:F0} out of range 5–95");
        }
    }

    [Fact]
    public void PulseTCodeMapper_FullAmplitude_WiderRange()
    {
        var map = MakeBeatMap(120, 10);
        double peakTime = 500 * 0.4; // end of upstroke

        var mapperHigh = new PulseTCodeMapper();
        var mapperLow = new PulseTCodeMapper();

        double posHigh = mapperHigh.MapToPosition(map, peakTime * 0.99, 1.0);
        double posLow = mapperLow.MapToPosition(map, peakTime * 0.99, 0.0);

        Assert.True(posHigh > posLow,
            $"Full amplitude ({posHigh:F1}) should be higher than zero amplitude ({posLow:F1})");
    }

    [Fact]
    public void PulseTCodeMapper_ZeroAmplitude_StillMoves()
    {
        var map = MakeBeatMap(120, 10);
        double pos = _mapper.MapToPosition(map, 500 * 0.2, 0.0);
        Assert.NotEqual(50.0, pos);
    }

    [Fact]
    public void PulseTCodeMapper_AtBeatStart_IsNearBottom()
    {
        var map = MakeBeatMap(120, 10);
        double pos = _mapper.MapToPosition(map, 0, 0.8);
        Assert.True(pos < 50.0, $"At beat start, position ({pos:F1}) should be below rest (50)");
    }

    [Fact]
    public void PulseTCodeMapper_AtUpstrokePeak_IsAboveRest()
    {
        var map = MakeBeatMap(120, 10);
        double peakTime = 500 * 0.39;
        double pos = _mapper.MapToPosition(map, peakTime, 0.8);
        Assert.True(pos > 50.0, $"At upstroke peak, position ({pos:F1}) should be above rest (50)");
    }

    [Fact]
    public void PulseTCodeMapper_Reset_ClearsState()
    {
        var map = MakeBeatMap(120, 10);
        _mapper.MapToPosition(map, 500, 0.8);
        _mapper.Reset();

        // After reset, first call should adopt position directly (no smoothing)
        double pos = _mapper.MapToPosition(null, 500, 0);
        Assert.Equal(50.0, pos); // Rest position
    }

    [Fact]
    public void PulseTCodeMapper_FindCurrentBeatIndex_EmptyList_ReturnsNeg1()
    {
        int idx = PulseTCodeMapper.FindCurrentBeatIndex(Array.Empty<BeatEvent>(), 100);
        Assert.Equal(-1, idx);
    }

    [Fact]
    public void PulseTCodeMapper_FindCurrentBeatIndex_BeforeAll_ReturnsNeg1()
    {
        var beats = new[] { new BeatEvent { TimestampMs = 500 } };
        int idx = PulseTCodeMapper.FindCurrentBeatIndex(beats, 100);
        Assert.Equal(-1, idx);
    }

    [Fact]
    public void PulseTCodeMapper_FindCurrentBeatIndex_ExactMatch_ReturnsIndex()
    {
        var beats = new[]
        {
            new BeatEvent { TimestampMs = 0 },
            new BeatEvent { TimestampMs = 500 },
            new BeatEvent { TimestampMs = 1000 }
        };
        int idx = PulseTCodeMapper.FindCurrentBeatIndex(beats, 500);
        Assert.Equal(1, idx);
    }

    [Fact]
    public void PulseTCodeMapper_FindCurrentBeatIndex_BetweenBeats_ReturnsPrevious()
    {
        var beats = new[]
        {
            new BeatEvent { TimestampMs = 0 },
            new BeatEvent { TimestampMs = 500 },
            new BeatEvent { TimestampMs = 1000 }
        };
        int idx = PulseTCodeMapper.FindCurrentBeatIndex(beats, 750);
        Assert.Equal(1, idx);
    }

    [Fact]
    public void PulseTCodeMapper_FindCurrentBeatIndex_AfterAll_ReturnsLast()
    {
        var beats = new[]
        {
            new BeatEvent { TimestampMs = 0 },
            new BeatEvent { TimestampMs = 500 }
        };
        int idx = PulseTCodeMapper.FindCurrentBeatIndex(beats, 10000);
        Assert.Equal(1, idx);
    }

    // ══════════════════════════════════════════════
    //  PulseBeatSource Tests
    // ══════════════════════════════════════════════

    private readonly PulseBeatSource _beatSource = new();

    [Fact]
    public void PulseBeatSource_Id_IsPulsePluginId()
    {
        Assert.Equal("com.vido.pulse", _beatSource.Id);
    }

    [Fact]
    public void PulseBeatSource_DisplayName_IsPulse()
    {
        Assert.Equal("Pulse", _beatSource.DisplayName);
    }

    [Fact]
    public void PulseBeatSource_HidesBuiltInModes_IsTrue()
    {
        Assert.True(_beatSource.HidesBuiltInModes);
    }

    [Fact]
    public void PulseBeatSource_IsAvailable_DefaultsFalse()
    {
        Assert.False(_beatSource.IsAvailable);
    }

    [Fact]
    public void PulseBeatSource_IsAvailable_CanBeToggled()
    {
        _beatSource.IsAvailable = true;
        Assert.True(_beatSource.IsAvailable);
        _beatSource.IsAvailable = false;
        Assert.False(_beatSource.IsAvailable);
    }

    [Fact]
    public void PulseBeatSource_ImplementsIExternalBeatSource()
    {
        Assert.IsAssignableFrom<IExternalBeatSource>(_beatSource);
    }

    [Fact]
    public void PulseBeatSource_RenderBeat_DoesNotThrow()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        _beatSource.RenderBeat(surface.Canvas, 100, 100, 40, 0f);
        _beatSource.RenderBeat(surface.Canvas, 100, 100, 40, 0.5f);
        _beatSource.RenderBeat(surface.Canvas, 100, 100, 40, 1.0f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void PulseBeatSource_RenderBeat_AllProgressValues_DoNotThrow(float progress)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        _beatSource.RenderBeat(surface.Canvas, 100, 100, 40, progress);
    }

    [Fact]
    public void PulseBeatSource_RenderBeat_RepeatedGlowFrames_DoNotThrow()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 300));
        for (int i = 0; i < 500; i++)
            _beatSource.RenderBeat(surface.Canvas, 150, 150, 60, 1.0f);
    }

    [Theory]
    [InlineData(10f)]
    [InlineData(20f)]
    [InlineData(50f)]
    [InlineData(100f)]
    public void PulseBeatSource_RenderBeat_VariousSizes_DoNotThrow(float size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        _beatSource.RenderBeat(surface.Canvas, 100, 100, size, 0);
    }

    [Fact]
    public void PulseBeatSource_RenderIndicator_DoesNotThrow()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        _beatSource.RenderIndicator(surface.Canvas, 100, 100, 40);
    }

    [Fact]
    public void PulseBeatSource_RenderBeat_ProducesVisiblePixels()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Transparent);

        _beatSource.RenderBeat(surface.Canvas, 100, 100, 60, 0f);

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        bool hasColor = false;
        for (int y = 0; y < 200 && !hasColor; y++)
            for (int x = 0; x < 200 && !hasColor; x++)
                if (pixmap.GetPixelColor(x, y).Alpha > 0)
                    hasColor = true;

        Assert.True(hasColor, "RenderBeat should produce visible heart pixels");
    }

    [Fact]
    public void PulseBeatSource_RenderBeat_HeartColorIsRed()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Transparent);

        _beatSource.RenderBeat(surface.Canvas, 100, 100, 80, 0f);

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();

        var centerColor = pixmap.GetPixelColor(100, 90);
        Assert.True(centerColor.Red > 150, $"Expected red heart, got R={centerColor.Red}");
        Assert.True(centerColor.Green < 100, $"Expected low green, got G={centerColor.Green}");
    }

    [Fact]
    public void PulseBeatSource_RenderIndicator_ProducesVisiblePixels()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Transparent);

        _beatSource.RenderIndicator(surface.Canvas, 100, 100, 60);

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        bool hasColor = false;
        for (int y = 0; y < 200 && !hasColor; y++)
            for (int x = 0; x < 200 && !hasColor; x++)
                if (pixmap.GetPixelColor(x, y).Alpha > 0)
                    hasColor = true;

        Assert.True(hasColor, "RenderIndicator should produce visible hollow heart pixels");
    }

    [Fact]
    public void PulseBeatSource_RenderIndicator_AfterBeatProgress_DoesNotThrow()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        _beatSource.RenderBeat(surface.Canvas, 100, 100, 40, 0.85f);
        _beatSource.RenderIndicator(surface.Canvas, 100, 100, 40);
    }

    [Fact]
    public void PulseBeatSource_RenderIndicator_IsHollow_CenterTransparent()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Transparent);

        _beatSource.RenderIndicator(surface.Canvas, 100, 100, 80);

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();

        var centerColor = pixmap.GetPixelColor(100, 100);
        Assert.True(centerColor.Alpha < 50,
            $"Center of hollow heart should be transparent, got alpha={centerColor.Alpha}");
    }

    [Fact]
    public void PulseBeatSource_RenderBeat_StronglyTypedSKCanvas()
    {
        // Verify the method signature accepts SKCanvas directly (not object)
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        SKCanvas canvas = surface.Canvas;

        // This call should compile without any casting — proves strongly typed
        _beatSource.RenderBeat(canvas, 100, 100, 40, 0.5f);
    }

    [Fact]
    public void PulseBeatSource_RenderIndicator_StronglyTypedSKCanvas()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        SKCanvas canvas = surface.Canvas;

        _beatSource.RenderIndicator(canvas, 100, 100, 40);
    }

    // ══════════════════════════════════════════════
    //  PulseEngine Tests
    // ══════════════════════════════════════════════

    private static TestEventBus CreateEventBus() => new();

    private static PulseEngine CreateEngine(
        TestEventBus eventBus,
        AudioPreAnalysisService? preAnalysis = null,
        LiveAmplitudeService? liveAmplitude = null,
        PulseTCodeMapper? mapper = null)
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        preAnalysis ??= new AudioPreAnalysisService(decoder);
        liveAmplitude ??= new LiveAmplitudeService();
        mapper ??= new PulseTCodeMapper();
        var logger = Substitute.For<ILogService>();

        return new PulseEngine(preAnalysis, liveAmplitude, mapper, eventBus, logger);
    }

    private static AudioPreAnalysisService CreatePreAnalysisWithClickTrack()
    {
        // Simple click track that produces a quick analysis result
        int sampleRate = 44100;
        int chunkSize = 4410;
        int totalSamples = sampleRate * 2;

        var samples = new float[totalSamples];
        // Create a click track at 120 BPM
        double intervalSamples = sampleRate * 60.0 / 120;
        double pos = 0;
        while (pos < totalSamples)
        {
            int idx = (int)pos;
            if (idx < totalSamples)
            {
                int clickLen = Math.Min(8, totalSamples - idx);
                for (int i = 0; i < clickLen && idx + i < totalSamples; i++)
                    samples[idx + i] = 1.0f - (float)i / clickLen;
            }
            pos += intervalSamples;
        }

        var chunks = new List<AudioChunk>();
        for (int offset = 0; offset < totalSamples; offset += chunkSize)
        {
            int remaining = Math.Min(chunkSize, totalSamples - offset);
            var chunk = new float[remaining];
            Array.Copy(samples, offset, chunk, 0, remaining);
            chunks.Add(new AudioChunk
            {
                Samples = chunk,
                SampleRate = sampleRate,
                TimestampMs = offset * 1000.0 / sampleRate,
                TotalDurationMs = totalSamples * 1000.0 / sampleRate
            });
        }

        return new AudioPreAnalysisService(new TestAudioDecoder(chunks));
    }

    private static VideoLoadedEvent MakeVideoLoaded(string path = @"C:\Videos\test.mp4") => new()
    {
        FilePath = path,
        Metadata = new VideoMetadata
        {
            FilePath = path,
            FileName = System.IO.Path.GetFileName(path)
        }
    };

    private static async Task WaitForState(PulseEngine engine, PulseState target, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (engine.State != target && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.Equal(target, engine.State);
    }

    [Fact]
    public void PulseEngine_Constructor_NullPreAnalysis_Throws()
    {
        var bus = CreateEventBus();
        var logger = Substitute.For<ILogService>();
        Assert.Throws<ArgumentNullException>(() =>
            new PulseEngine(null!, new LiveAmplitudeService(), new PulseTCodeMapper(), bus, logger));
    }

    [Fact]
    public void PulseEngine_Constructor_NullLiveAmplitude_Throws()
    {
        var bus = CreateEventBus();
        var logger = Substitute.For<ILogService>();
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        Assert.Throws<ArgumentNullException>(() =>
            new PulseEngine(new AudioPreAnalysisService(decoder), null!, new PulseTCodeMapper(), bus, logger));
    }

    [Fact]
    public void PulseEngine_Constructor_NullMapper_Throws()
    {
        var bus = CreateEventBus();
        var logger = Substitute.For<ILogService>();
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        Assert.Throws<ArgumentNullException>(() =>
            new PulseEngine(new AudioPreAnalysisService(decoder), new LiveAmplitudeService(), null!, bus, logger));
    }

    [Fact]
    public void PulseEngine_Constructor_NullEventBus_Throws()
    {
        var logger = Substitute.For<ILogService>();
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        Assert.Throws<ArgumentNullException>(() =>
            new PulseEngine(new AudioPreAnalysisService(decoder), new LiveAmplitudeService(), new PulseTCodeMapper(), null!, logger));
    }

    [Fact]
    public void PulseEngine_Constructor_NullLogger_Throws()
    {
        var bus = CreateEventBus();
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        Assert.Throws<ArgumentNullException>(() =>
            new PulseEngine(new AudioPreAnalysisService(decoder), new LiveAmplitudeService(), new PulseTCodeMapper(), bus, null!));
    }

    [Fact]
    public void PulseEngine_Constructor_NoCurrentMediaPath_Parameter()
    {
        // Verify the constructor does NOT take a currentMediaPath parameter
        // (it was removed per the PI-013 ticket)
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        Assert.Equal(PulseState.Inactive, engine.State);
        Assert.False(engine.IsEnabled);
        Assert.Null(engine.CurrentBeatMap);
        Assert.Equal(0, engine.CurrentBpm);
    }

    [Fact]
    public void PulseEngine_InitialState_IsInactive()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        Assert.Equal(PulseState.Inactive, engine.State);
        Assert.False(engine.IsEnabled);
        Assert.Null(engine.CurrentBeatMap);
        Assert.Equal(0, engine.CurrentBpm);
        Assert.Equal(1, engine.BeatDivisor);
    }

    [Fact]
    public void PulseEngine_SetEnabled_True_PublishesSuppressAndRegistration()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        engine.SetEnabled(true);

        Assert.True(engine.IsEnabled);

        var suppress = bus.GetPublished<SuppressFunscriptEvent>();
        Assert.Single(suppress);
        Assert.True(suppress[0].SuppressFunscripts);

        var reg = bus.GetPublished<ExternalBeatSourceRegistration>();
        Assert.Single(reg);
        Assert.True(reg[0].IsRegistering);
        Assert.NotNull(reg[0].Source);
        Assert.Equal("com.vido.pulse", reg[0].Source!.Id);
    }

    [Fact]
    public void PulseEngine_SetEnabled_False_PublishesUnsuppressAndUnregistration()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        engine.SetEnabled(true);
        bus.ClearPublished();

        engine.SetEnabled(false);

        Assert.False(engine.IsEnabled);
        Assert.Equal(PulseState.Inactive, engine.State);

        var suppress = bus.GetPublished<SuppressFunscriptEvent>();
        Assert.Single(suppress);
        Assert.False(suppress[0].SuppressFunscripts);

        var reg = bus.GetPublished<ExternalBeatSourceRegistration>();
        Assert.Single(reg);
        Assert.False(reg[0].IsRegistering);
    }

    [Fact]
    public void PulseEngine_SetEnabled_TrueWhenAlreadyEnabled_NoOp()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        engine.SetEnabled(true);
        bus.ClearPublished();

        engine.SetEnabled(true);
        Assert.Empty(bus.PublishedEvents);
    }

    [Fact]
    public void PulseEngine_SetEnabled_FalseWhenAlreadyDisabled_NoOp()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);
        bus.ClearPublished();

        engine.SetEnabled(false);
        Assert.Empty(bus.PublishedEvents);
    }

    [Fact]
    public void PulseEngine_SetEnabled_TrueWithNoVideo_StaysInactive()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        engine.SetEnabled(true);
        Assert.Equal(PulseState.Inactive, engine.State);
    }

    [Fact]
    public async Task PulseEngine_SetEnabled_TrueWithVideoLoaded_StartsAnalysis()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);

        Assert.Equal(PulseState.Analyzing, engine.State);

        await WaitForState(engine, PulseState.Ready);
        Assert.NotNull(engine.CurrentBeatMap);
        Assert.True(engine.CurrentBpm > 0);
    }

    [Fact]
    public async Task PulseEngine_VideoLoaded_WhileEnabled_StartsAnalysis()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        engine.SetEnabled(true);
        Assert.Equal(PulseState.Inactive, engine.State);

        bus.Publish(MakeVideoLoaded());

        Assert.Equal(PulseState.Analyzing, engine.State);
        await WaitForState(engine, PulseState.Ready);
        Assert.NotNull(engine.CurrentBeatMap);
    }

    /// <summary>
    /// Regression: switching videos while Pulse is enabled must NOT set
    /// BeatSource.IsAvailable to false. Flashing it to false causes
    /// BeatBarViewModel.RebuildAvailableModes() to drop the Pulse mode
    /// and reset the user's BeatBar selection to Off.
    /// </summary>
    [Fact]
    public async Task PulseEngine_VideoSwitch_WhileEnabled_KeepsBeatSourceAvailable()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        // Enable Pulse and load first video
        engine.SetEnabled(true);
        bus.Publish(MakeVideoLoaded(@"C:\Videos\first.mp4"));
        await WaitForState(engine, PulseState.Ready);
        Assert.True(engine.BeatSource.IsAvailable);

        // Switch to a second video — BeatSource must stay available
        bus.Publish(MakeVideoLoaded(@"C:\Videos\second.mp4"));

        // Immediately after video switch, before analysis completes
        Assert.True(engine.BeatSource.IsAvailable,
            "BeatSource.IsAvailable must remain true during video switch so BeatBar mode persists");
        Assert.Equal(PulseState.Analyzing, engine.State);
    }

    [Fact]
    public void PulseEngine_VideoLoaded_WhileDisabled_DoesNotAnalyze()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        bus.Publish(MakeVideoLoaded());
        Assert.Equal(PulseState.Inactive, engine.State);
        Assert.Null(engine.CurrentBeatMap);
    }

    [Fact]
    public async Task PulseEngine_AnalysisComplete_FiresBeatMapReady()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        var tcs = new TaskCompletionSource<BeatMap>();
        engine.BeatMapReady += map => tcs.TrySetResult(map);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);

        var receivedMap = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task
            ? tcs.Task.Result
            : null;
        Assert.NotNull(receivedMap);
    }

    [Fact]
    public async Task PulseEngine_AnalysisComplete_BeatSourceIsAvailable()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);

        await WaitForState(engine, PulseState.Ready);
        Assert.True(engine.BeatSource.IsAvailable);
    }

    [Fact]
    public async Task PulseEngine_PlaybackPlaying_DuringReady_GoesToActive()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);
        await WaitForState(engine, PulseState.Ready);

        bus.Publish(new PlaybackStateChangedEvent { State = PlaybackState.Playing });
        Assert.Equal(PulseState.Active, engine.State);
    }

    [Fact]
    public async Task PulseEngine_PlaybackPaused_DuringActive_GoesToReady()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);
        await WaitForState(engine, PulseState.Ready);

        bus.Publish(new PlaybackStateChangedEvent { State = PlaybackState.Playing });
        Assert.Equal(PulseState.Active, engine.State);

        bus.Publish(new PlaybackStateChangedEvent { State = PlaybackState.Paused });
        Assert.Equal(PulseState.Ready, engine.State);
    }

    [Fact]
    public async Task PulseEngine_OnPositionChanged_DuringActive_PublishesL0Position()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);
        await WaitForState(engine, PulseState.Ready);
        bus.Publish(new PlaybackStateChangedEvent { State = PlaybackState.Playing });
        Assert.Equal(PulseState.Active, engine.State);
        bus.ClearPublished();

        engine.OnPositionChanged(500);

        var posEvents = bus.GetPublished<ExternalAxisPositionsEvent>();
        Assert.NotEmpty(posEvents);
        var positions = posEvents[0].Positions.ToArray();
        Assert.Contains(positions, p => p.AxisId == "L0");

        double l0 = positions.First(p => p.AxisId == "L0").Position;
        Assert.True(l0 >= 5.0 && l0 <= 95.0, $"L0 position {l0} out of range");
    }

    [Fact]
    public void PulseEngine_OnPositionChanged_DuringInactive_DoesNotPublish()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);
        bus.ClearPublished();

        engine.OnPositionChanged(500);

        var posEvents = bus.GetPublished<ExternalAxisPositionsEvent>();
        Assert.Empty(posEvents);
    }

    [Fact]
    public void PulseEngine_OnAudioSamplesAvailable_DuringNonActive_NoOp()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        var args = new AudioSampleEventArgs
        {
            Buffer = new byte[256],
            SampleCount = 32,
            SampleRate = 44100,
            Channels = 2
        };

        engine.OnAudioSamplesAvailable(args); // Should not throw
    }

    [Fact]
    public async Task PulseEngine_OnSeekCompleted_ResetsTracking()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);
        await WaitForState(engine, PulseState.Ready);
        bus.Publish(new PlaybackStateChangedEvent { State = PlaybackState.Playing });

        engine.OnPositionChanged(1000);
        engine.OnSeekCompleted(); // Should not throw

        // After seek, should still be Active
        Assert.Equal(PulseState.Active, engine.State);
    }

    [Fact]
    public void PulseEngine_SetEnabled_FalseDuringAnalyzing_CancelsAndGoesInactive()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        engine.SetEnabled(true);
        bus.Publish(MakeVideoLoaded());
        Assert.Equal(PulseState.Analyzing, engine.State);

        engine.SetEnabled(false);
        Assert.Equal(PulseState.Inactive, engine.State);
        Assert.False(engine.BeatSource.IsAvailable);
    }

    [Fact]
    public async Task PulseEngine_ReEnable_ReusesBeatMap()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);
        await WaitForState(engine, PulseState.Ready);

        var originalMap = engine.CurrentBeatMap;
        Assert.NotNull(originalMap);

        engine.SetEnabled(false);
        Assert.Equal(PulseState.Inactive, engine.State);

        engine.SetEnabled(true);

        Assert.Equal(PulseState.Ready, engine.State);
        Assert.Same(originalMap, engine.CurrentBeatMap);
    }

    [Fact]
    public void PulseEngine_VideoUnloaded_GoesInactive()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        engine.SetEnabled(true);
        bus.Publish(MakeVideoLoaded());

        bus.Publish(new VideoUnloadedEvent());

        Assert.Equal(PulseState.Inactive, engine.State);
    }

    [Fact]
    public void PulseEngine_BeatDivisor_DefaultIsOne()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);
        Assert.Equal(1, engine.BeatDivisor);
    }

    [Fact]
    public void PulseEngine_BeatDivisor_ClampedTo1Through4()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        engine.BeatDivisor = 0;
        Assert.Equal(1, engine.BeatDivisor);

        engine.BeatDivisor = 5;
        Assert.Equal(4, engine.BeatDivisor);

        engine.BeatDivisor = 2;
        Assert.Equal(2, engine.BeatDivisor);
    }

    [Fact]
    public void PulseEngine_BeatDivisor_SameValue_NoEvent()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        int eventCount = 0;
        engine.BeatDivisorChanged += _ => eventCount++;

        engine.BeatDivisor = 1; // Already 1
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void PulseEngine_BeatDivisor_Changed_FiresEvent()
    {
        var bus = CreateEventBus();
        using var engine = CreateEngine(bus);

        int? divisorValue = null;
        engine.BeatDivisorChanged += val => divisorValue = val;

        engine.BeatDivisor = 3;
        Assert.Equal(3, divisorValue);
    }

    [Fact]
    public void PulseEngine_Dispose_DoesNotThrow()
    {
        var bus = CreateEventBus();
        var engine = CreateEngine(bus);

        engine.SetEnabled(true);
        engine.Dispose();
        // Should not throw on double dispose
    }

    [Fact]
    public async Task PulseEngine_StateChanged_FiresOnTransitions()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        var stateChanges = new List<PulseState>();
        engine.StateChanged += state => stateChanges.Add(state);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true); // Should fire Analyzing

        Assert.Contains(PulseState.Analyzing, stateChanges);

        await WaitForState(engine, PulseState.Ready);
        Assert.Contains(PulseState.Ready, stateChanges);
    }

    [Fact]
    public async Task PulseEngine_AnalysisProgress_FiresEvent()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        var progresses = new List<double>();
        engine.AnalysisProgress += p => progresses.Add(p);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);

        await WaitForState(engine, PulseState.Ready);
        Assert.NotEmpty(progresses);
        Assert.Contains(progresses, p => p >= 1.0);
    }

    [Fact]
    public async Task PulseEngine_AnalysisFailed_SetsErrorState()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>(), throwException: new InvalidOperationException("decode error"));
        var preAnalysis = new AudioPreAnalysisService(decoder);
        var bus = CreateEventBus();
        var logger = Substitute.For<ILogService>();
        using var engine = new PulseEngine(preAnalysis, new LiveAmplitudeService(), new PulseTCodeMapper(), bus, logger);

        string? errorMsg = null;
        engine.ErrorOccurred += msg => errorMsg = msg;

        engine.SetEnabled(true);
        bus.Publish(MakeVideoLoaded());

        await WaitForState(engine, PulseState.Error);
        Assert.Contains("decode error", errorMsg);
    }

    [Fact]
    public async Task PulseEngine_OnPositionChanged_PublishesBeatEvents()
    {
        var bus = CreateEventBus();
        var preAnalysis = CreatePreAnalysisWithClickTrack();
        using var engine = CreateEngine(bus, preAnalysis: preAnalysis);

        bus.Publish(MakeVideoLoaded());
        engine.SetEnabled(true);
        await WaitForState(engine, PulseState.Ready);
        bus.Publish(new PlaybackStateChangedEvent { State = PlaybackState.Playing });
        bus.ClearPublished();

        engine.OnPositionChanged(0);

        var beatEvents = bus.GetPublished<ExternalBeatEvent>();
        if (beatEvents.Count > 0)
        {
            Assert.Equal("com.vido.pulse", beatEvents[0].SourceId);
            Assert.True(beatEvents[0].BeatTimesMs.Length > 0);
        }
    }

    // ══════════════════════════════════════════════
    //  Test Infrastructure
    // ══════════════════════════════════════════════

    /// <summary>
    /// In-memory event bus for testing. Captures subscriptions and published events.
    /// </summary>
    private sealed class TestEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly List<object> _published = new();
        private readonly object _lock = new();

        public IReadOnlyList<object> PublishedEvents
        {
            get { lock (_lock) return _published.ToList(); }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            lock (_lock)
            {
                if (!_handlers.ContainsKey(type))
                    _handlers[type] = new List<Delegate>();
                _handlers[type].Add(handler);
            }

            return new Subscription(() =>
            {
                lock (_lock)
                {
                    if (_handlers.TryGetValue(type, out var list))
                        list.Remove(handler);
                }
            });
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            List<Delegate> snapshot;
            lock (_lock)
            {
                _published.Add(eventData!);
                snapshot = _handlers.TryGetValue(typeof(TEvent), out var list)
                    ? list.ToList()
                    : new List<Delegate>();
            }

            foreach (var h in snapshot)
                ((Action<TEvent>)h)(eventData);
        }

        public IReadOnlyList<T> GetPublished<T>()
        {
            lock (_lock) return _published.OfType<T>().ToList();
        }

        public void ClearPublished()
        {
            lock (_lock) _published.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _onDispose;
            public Subscription(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }

    /// <summary>
    /// Mock audio decoder for testing — yields pre-built chunks or throws.
    /// </summary>
    private sealed class TestAudioDecoder : IAudioDecoder
    {
        private readonly IReadOnlyList<AudioChunk> _chunks;
        private readonly Exception? _exception;

        public TestAudioDecoder(IReadOnlyList<AudioChunk> chunks, Exception? throwException = null)
        {
            _chunks = chunks;
            _exception = throwException;
        }

        public async IAsyncEnumerable<AudioChunk> DecodeAsync(
            string mediaPath,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            if (_exception != null)
                throw _exception;

            foreach (var chunk in _chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }
    }

    // ── Audio helpers ──

    /// <summary>
    /// Convert mono float samples to interleaved stereo byte buffer (float32 PCM).
    /// </summary>
    private static byte[] MonoToStereoBytes(float[] mono)
    {
        var stereo = new float[mono.Length * 2];
        for (int i = 0; i < mono.Length; i++)
        {
            stereo[i * 2] = mono[i];
            stereo[i * 2 + 1] = mono[i];
        }
        var bytes = new byte[stereo.Length * sizeof(float)];
        Buffer.BlockCopy(stereo, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
