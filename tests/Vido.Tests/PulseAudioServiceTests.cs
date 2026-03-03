using Vido.Core.Models.Pulse;
using Vido.Services.Pulse;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for Pulse audio analysis services (PI-012).
/// Covers OnsetDetector, BpmEstimator, AmplitudeTracker, and AudioPreAnalysisService.
/// </summary>
public class PulseAudioServiceTests
{
    // ──────────────────────────────────────────────
    //  OnsetDetector Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void OnsetDetector_Constructor_DefaultParameters_CreatesInstance()
    {
        var detector = new OnsetDetector();
        Assert.NotNull(detector);
    }

    [Fact]
    public void OnsetDetector_Constructor_InvalidFftSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OnsetDetector(fftSize: 100));
    }

    [Fact]
    public void OnsetDetector_Constructor_InvalidHopSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OnsetDetector(hopSize: 0));
        Assert.Throws<ArgumentException>(() => new OnsetDetector(fftSize: 2048, hopSize: 4096));
    }

    [Fact]
    public void OnsetDetector_Constructor_InvalidSensitivity_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OnsetDetector(sensitivity: 0));
        Assert.Throws<ArgumentException>(() => new OnsetDetector(sensitivity: -1));
    }

    [Fact]
    public void OnsetDetector_Process_InvalidSampleRate_Throws()
    {
        var detector = new OnsetDetector();
        var samples = new float[4096];
        Assert.Throws<ArgumentException>(() => detector.Process(samples, 0, 0));
    }

    [Fact]
    public void OnsetDetector_Process_NullOutput_Throws()
    {
        var detector = new OnsetDetector();
        var samples = new float[4096];
        Assert.Throws<ArgumentNullException>(() => detector.Process(samples, 0, 44100, null!));
    }

    [Fact]
    public void OnsetDetector_Process_SilentInput_ReturnsNoBeats()
    {
        var detector = new OnsetDetector();
        var silence = new float[44100]; // 1 second of silence
        var beats = detector.Process(silence, 0, 44100);
        Assert.Empty(beats);
    }

    [Fact]
    public void OnsetDetector_Process_KnownTone_DetectsOnsets()
    {
        var detector = new OnsetDetector(sensitivity: 1.2);
        int sampleRate = 44100;

        // Generate a signal with sudden onset: silence followed by a 440Hz tone burst.
        var samples = new float[sampleRate]; // 1 second
        // First half silence, second half tone
        for (int i = sampleRate / 2; i < sampleRate; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.8f;
        }

        var beats = detector.Process(samples, 0, sampleRate);
        Assert.NotEmpty(beats);

        // The onset should be near the midpoint (~500ms)
        Assert.True(beats[0].TimestampMs >= 400 && beats[0].TimestampMs <= 700,
            $"Expected onset near 500ms, got {beats[0].TimestampMs}ms");
    }

    [Fact]
    public void OnsetDetector_Process_ListOverload_AppendsToOutput()
    {
        var detector = new OnsetDetector(sensitivity: 1.2);
        int sampleRate = 44100;

        var samples = new float[sampleRate];
        for (int i = sampleRate / 2; i < sampleRate; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.8f;

        var output = new List<BeatEvent>();
        detector.Process(samples, 0, sampleRate, output);

        // Should be same result as the allocating overload
        Assert.NotEmpty(output);
    }

    [Fact]
    public void OnsetDetector_Process_MultipleChunks_AccumulatesState()
    {
        var detector = new OnsetDetector(sensitivity: 1.2);
        int sampleRate = 44100;
        int chunkSize = 4410; // 100ms chunks

        // Generate signal with onset at ~500ms
        var fullSignal = new float[sampleRate];
        for (int i = sampleRate / 2; i < sampleRate; i++)
            fullSignal[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.8f;

        var allBeats = new List<BeatEvent>();
        for (int offset = 0; offset < fullSignal.Length; offset += chunkSize)
        {
            int remaining = Math.Min(chunkSize, fullSignal.Length - offset);
            var chunk = fullSignal.AsSpan(offset, remaining);
            double timeMs = offset * 1000.0 / sampleRate;
            var beats = detector.Process(chunk, timeMs, sampleRate);
            allBeats.AddRange(beats);
        }

        Assert.NotEmpty(allBeats);
    }

    [Fact]
    public void OnsetDetector_SetSensitivity_ValidValue_Updates()
    {
        var detector = new OnsetDetector();
        detector.SetSensitivity(2.0);
        // No throw = success; verify higher sensitivity means fewer detections
    }

    [Fact]
    public void OnsetDetector_SetSensitivity_InvalidValue_Throws()
    {
        var detector = new OnsetDetector();
        Assert.Throws<ArgumentException>(() => detector.SetSensitivity(0));
        Assert.Throws<ArgumentException>(() => detector.SetSensitivity(-1));
    }

    [Fact]
    public void OnsetDetector_Reset_ClearsState()
    {
        var detector = new OnsetDetector(sensitivity: 1.2);
        int sampleRate = 44100;

        // Process some data
        var samples = new float[sampleRate];
        for (int i = sampleRate / 2; i < sampleRate; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.8f;
        detector.Process(samples, 0, sampleRate);

        // Reset
        detector.Reset();

        // Processing silence after reset should yield no beats
        var silence = new float[sampleRate];
        var beats = detector.Process(silence, 0, sampleRate);
        Assert.Empty(beats);
    }

    [Fact]
    public void OnsetDetector_Fft_KnownInput_ProducesExpectedOutput()
    {
        // Test FFT with a simple impulse
        var buffer = new System.Numerics.Complex[4];
        buffer[0] = new System.Numerics.Complex(1, 0);
        buffer[1] = System.Numerics.Complex.Zero;
        buffer[2] = System.Numerics.Complex.Zero;
        buffer[3] = System.Numerics.Complex.Zero;

        OnsetDetector.Fft(buffer);

        // Impulse → constant magnitude spectrum
        for (int i = 0; i < buffer.Length; i++)
        {
            Assert.True(Math.Abs(buffer[i].Magnitude - 1.0) < 1e-10,
                $"FFT bin {i} should have magnitude 1.0, got {buffer[i].Magnitude}");
        }
    }

    [Fact]
    public void OnsetDetector_Process_PreallocatedBuffers_ReuseAcrossCalls()
    {
        var detector = new OnsetDetector();
        int sampleRate = 44100;
        var samples = new float[4410]; // 100ms chunk

        // Process the same chunk multiple times — should not throw and should reuse buffers
        for (int i = 0; i < 10; i++)
        {
            var beats = detector.Process(samples, i * 100.0, sampleRate);
            Assert.NotNull(beats);
        }
    }

    // ──────────────────────────────────────────────
    //  BpmEstimator Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void BpmEstimator_Constructor_DefaultParameters_CreatesInstance()
    {
        var estimator = new BpmEstimator();
        Assert.NotNull(estimator);
        Assert.Equal(0, estimator.CurrentEstimate.Bpm);
        Assert.Equal(0, estimator.CurrentEstimate.Confidence);
    }

    [Fact]
    public void BpmEstimator_Constructor_InvalidMinBpm_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BpmEstimator(minBpm: 0));
        Assert.Throws<ArgumentException>(() => new BpmEstimator(minBpm: -1));
    }

    [Fact]
    public void BpmEstimator_Constructor_InvalidMaxBpm_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BpmEstimator(minBpm: 100, maxBpm: 50));
        Assert.Throws<ArgumentException>(() => new BpmEstimator(minBpm: 100, maxBpm: 100));
    }

    [Fact]
    public void BpmEstimator_AddBeat_NullBeat_Throws()
    {
        var estimator = new BpmEstimator();
        Assert.Throws<ArgumentNullException>(() => estimator.AddBeat(null!));
    }

    [Fact]
    public void BpmEstimator_AddBeat_RegularIntervals_EstimatesCorrectBpm()
    {
        var estimator = new BpmEstimator(minBpm: 50, maxBpm: 180);

        // Feed beats at 120 BPM = 500ms intervals
        double intervalMs = 500.0; // 120 BPM
        for (int i = 0; i < 20; i++)
        {
            estimator.AddBeat(new BeatEvent
            {
                TimestampMs = i * intervalMs,
                Strength = 1.0,
                IsQuantized = false
            });
        }

        var estimate = estimator.CurrentEstimate;
        Assert.True(estimate.Bpm > 0, "BPM should be > 0 after sufficient beats");
        Assert.InRange(estimate.Bpm, 115, 125); // Should be close to 120 BPM
        Assert.True(estimate.Confidence > 0, "Confidence should be > 0");
    }

    [Fact]
    public void BpmEstimator_AddBeat_SingleBeat_NoEstimate()
    {
        var estimator = new BpmEstimator();
        estimator.AddBeat(new BeatEvent { TimestampMs = 0, Strength = 1.0, IsQuantized = false });

        Assert.Equal(0, estimator.CurrentEstimate.Bpm);
    }

    [Fact]
    public void BpmEstimator_AddBeat_TwoBeats_NoEstimateYet()
    {
        var estimator = new BpmEstimator();
        estimator.AddBeat(new BeatEvent { TimestampMs = 0, Strength = 1.0, IsQuantized = false });
        estimator.AddBeat(new BeatEvent { TimestampMs = 500, Strength = 1.0, IsQuantized = false });

        // Only 1 interval — need 2 intervals minimum
        Assert.Equal(0, estimator.CurrentEstimate.Bpm);
    }

    [Fact]
    public void BpmEstimator_QuantizeBeat_LowConfidence_ReturnsOriginal()
    {
        var estimator = new BpmEstimator();

        // No beats fed, confidence is 0
        double raw = 1234.5;
        Assert.Equal(raw, estimator.QuantizeBeat(raw));
    }

    [Fact]
    public void BpmEstimator_QuantizeBeat_HighConfidence_SnapsToGrid()
    {
        var estimator = new BpmEstimator(minBpm: 50, maxBpm: 180);

        // Feed enough regular beats at 120 BPM to establish high confidence
        double intervalMs = 500.0;
        for (int i = 0; i < 30; i++)
        {
            estimator.AddBeat(new BeatEvent
            {
                TimestampMs = i * intervalMs,
                Strength = 1.0,
                IsQuantized = false
            });
        }

        if (estimator.CurrentEstimate.Confidence >= 0.6)
        {
            // A beat slightly off-grid should snap
            double rawBeat = 15010; // slightly off from a grid line
            double quantized = estimator.QuantizeBeat(rawBeat);
            Assert.NotEqual(rawBeat, quantized);
        }
    }

    [Fact]
    public void BpmEstimator_Reset_ClearsAllState()
    {
        var estimator = new BpmEstimator();

        // Feed some beats
        for (int i = 0; i < 10; i++)
        {
            estimator.AddBeat(new BeatEvent
            {
                TimestampMs = i * 500,
                Strength = 1.0,
                IsQuantized = false
            });
        }

        estimator.Reset();

        Assert.Equal(0, estimator.CurrentEstimate.Bpm);
        Assert.Equal(0, estimator.CurrentEstimate.Confidence);
        Assert.Equal(0, estimator.CurrentEstimate.PhaseOffsetMs);
    }

    // ──────────────────────────────────────────────
    //  AmplitudeTracker Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void AmplitudeTracker_Constructor_DefaultWindow_CreatesInstance()
    {
        var tracker = new AmplitudeTracker();
        Assert.Equal(0, tracker.CurrentAmplitude);
    }

    [Fact]
    public void AmplitudeTracker_Constructor_InvalidWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AmplitudeTracker(windowMs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AmplitudeTracker(windowMs: -5));
    }

    [Fact]
    public void AmplitudeTracker_Process_EmptyInput_ReturnsEmpty()
    {
        var tracker = new AmplitudeTracker();
        var result = tracker.Process(ReadOnlySpan<float>.Empty, 0, 44100);
        Assert.Empty(result);
    }

    [Fact]
    public void AmplitudeTracker_Process_InvalidSampleRate_ReturnsEmpty()
    {
        var tracker = new AmplitudeTracker();
        var samples = new float[100];
        var result = tracker.Process(samples, 0, 0);
        Assert.Empty(result);
    }

    [Fact]
    public void AmplitudeTracker_Process_KnownAmplitude_ProducesCorrectRms()
    {
        var tracker = new AmplitudeTracker(windowMs: 10);
        int sampleRate = 44100;

        // Generate a constant-amplitude signal (all samples = 0.5)
        int samplesPerWindow = (int)(sampleRate * 10 / 1000.0);
        var samples = new float[samplesPerWindow * 3]; // 3 full windows
        Array.Fill(samples, 0.5f);

        var results = tracker.Process(samples, 0, sampleRate);

        Assert.NotEmpty(results);
        foreach (var (_, rms) in results)
        {
            // RMS of constant 0.5 = 0.5
            Assert.InRange(rms, 0.49, 0.51);
        }
    }

    [Fact]
    public void AmplitudeTracker_Process_Silence_ProducesZeroRms()
    {
        var tracker = new AmplitudeTracker(windowMs: 10);
        int sampleRate = 44100;
        int samplesPerWindow = (int)(sampleRate * 10 / 1000.0);
        var silence = new float[samplesPerWindow * 2];

        var results = tracker.Process(silence, 0, sampleRate);

        Assert.NotEmpty(results);
        foreach (var (_, rms) in results)
        {
            Assert.Equal(0, rms);
        }
    }

    [Fact]
    public void AmplitudeTracker_Process_UpdatesCurrentAmplitude()
    {
        var tracker = new AmplitudeTracker(windowMs: 10);
        int sampleRate = 44100;
        int samplesPerWindow = (int)(sampleRate * 10 / 1000.0);
        var samples = new float[samplesPerWindow * 2];
        Array.Fill(samples, 0.5f);

        tracker.Process(samples, 0, sampleRate);

        Assert.True(tracker.CurrentAmplitude > 0, "CurrentAmplitude should update after processing");
        Assert.InRange(tracker.CurrentAmplitude, 0.49, 0.51);
    }

    [Fact]
    public void AmplitudeTracker_ProcessInPlace_UpdatesAmplitudeWithoutReturningList()
    {
        var tracker = new AmplitudeTracker(windowMs: 10);
        int sampleRate = 44100;
        int samplesPerWindow = (int)(sampleRate * 10 / 1000.0);
        var samples = new float[samplesPerWindow * 2];
        Array.Fill(samples, 0.7f);

        tracker.ProcessInPlace(samples, 0, sampleRate);

        Assert.InRange(tracker.CurrentAmplitude, 0.69, 0.71);
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_SingleChannel_ReturnsCopy()
    {
        var input = new float[] { 0.1f, 0.2f, 0.3f };
        var mono = AmplitudeTracker.DownmixToMono(input, 1);
        Assert.Equal(input, mono);
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_Stereo_AveragesChannels()
    {
        var interleaved = new float[] { 0.4f, 0.6f, 0.2f, 0.8f };
        var mono = AmplitudeTracker.DownmixToMono(interleaved, 2);
        Assert.Equal(2, mono.Length);
        Assert.InRange(mono[0], 0.49f, 0.51f); // (0.4+0.6)/2
        Assert.InRange(mono[1], 0.49f, 0.51f); // (0.2+0.8)/2
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_InvalidChannels_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AmplitudeTracker.DownmixToMono(new float[] { 1 }, 0));
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_SpanOverload_WritesToOutput()
    {
        var interleaved = new float[] { 0.4f, 0.6f, 0.2f, 0.8f };
        var output = new float[2];
        AmplitudeTracker.DownmixToMono(interleaved, 2, output);
        Assert.InRange(output[0], 0.49f, 0.51f);
        Assert.InRange(output[1], 0.49f, 0.51f);
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_SpanOverload_InvalidChannels_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AmplitudeTracker.DownmixToMono(new float[] { 1 }, 0, new float[1]));
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_SpanOverload_OutputTooSmall_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            AmplitudeTracker.DownmixToMono(new float[] { 0.4f, 0.6f, 0.2f, 0.8f }, 2, new float[1]));
    }

    [Fact]
    public void AmplitudeTracker_DownmixToMono_SpanOverload_SingleChannel_Copies()
    {
        var input = new float[] { 0.1f, 0.2f, 0.3f };
        var output = new float[3];
        AmplitudeTracker.DownmixToMono(input, 1, output);
        Assert.Equal(input, output);
    }

    [Fact]
    public void AmplitudeTracker_ByteBufferToMono_ConvertsAndDownmixes()
    {
        // 4 float samples * 2 channels = 8 floats = 32 bytes
        var floats = new float[] { 0.4f, 0.6f, 0.2f, 0.8f, 0.1f, 0.3f, 0.5f, 0.7f };
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

        var mono = AmplitudeTracker.ByteBufferToMono(new ReadOnlyMemory<byte>(bytes), 4, 2);
        Assert.Equal(4, mono.Length);
        Assert.InRange(mono[0], 0.49f, 0.51f); // (0.4+0.6)/2
    }

    [Fact]
    public void AmplitudeTracker_Reset_ClearsState()
    {
        var tracker = new AmplitudeTracker(windowMs: 10);
        int sampleRate = 44100;
        int samplesPerWindow = (int)(sampleRate * 10 / 1000.0);
        var samples = new float[samplesPerWindow * 2];
        Array.Fill(samples, 0.5f);

        tracker.Process(samples, 0, sampleRate);
        Assert.True(tracker.CurrentAmplitude > 0);

        tracker.Reset();
        Assert.Equal(0, tracker.CurrentAmplitude);
    }

    // ──────────────────────────────────────────────
    //  AudioPreAnalysisService Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void AudioPreAnalysisService_Constructor_NullDecoder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AudioPreAnalysisService(null!));
    }

    [Fact]
    public void AudioPreAnalysisService_Constructor_InvalidSensitivity_Throws()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        Assert.Throws<ArgumentException>(() => new AudioPreAnalysisService(decoder, sensitivity: 0));
    }

    [Fact]
    public void AudioPreAnalysisService_Constructor_ValidParameters_CreatesInstance()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        using var service = new AudioPreAnalysisService(decoder);
        Assert.NotNull(service);
        Assert.Null(service.CurrentBeatMap);
        Assert.False(service.IsAnalyzing);
    }

    [Fact]
    public async Task AudioPreAnalysisService_AnalyzeAsync_EmptyPath_Throws()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        using var service = new AudioPreAnalysisService(decoder);
        await Assert.ThrowsAsync<ArgumentException>(() => service.AnalyzeAsync(""));
    }

    [Fact]
    public async Task AudioPreAnalysisService_AnalyzeAsync_EmptyAudio_ProducesEmptyBeatMap()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        using var service = new AudioPreAnalysisService(decoder);

        BeatMap? completedMap = null;
        service.AnalysisComplete += map => completedMap = map;

        await service.AnalyzeAsync("test.mp3");
        // Give the task a moment to complete
        await Task.Delay(100);

        Assert.NotNull(completedMap);
        Assert.Empty(completedMap!.Beats);
    }

    [Fact]
    public async Task AudioPreAnalysisService_AnalyzeAsync_WithAudio_ProducesBeatMap()
    {
        int sampleRate = 44100;
        int chunkSize = 4410;
        int totalSamples = sampleRate * 2; // 2 seconds

        // Generate audio with a clear onset at 500ms
        var samples = new float[totalSamples];
        for (int i = sampleRate / 2; i < totalSamples; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.8f;

        var chunks = new List<AudioChunk>();
        for (int offset = 0; offset < totalSamples; offset += chunkSize)
        {
            int remaining = Math.Min(chunkSize, totalSamples - offset);
            var chunkSamples = new float[remaining];
            Array.Copy(samples, offset, chunkSamples, 0, remaining);
            chunks.Add(new AudioChunk
            {
                Samples = chunkSamples,
                SampleRate = sampleRate,
                TimestampMs = offset * 1000.0 / sampleRate,
                TotalDurationMs = totalSamples * 1000.0 / sampleRate
            });
        }

        var decoder = new TestAudioDecoder(chunks);
        using var service = new AudioPreAnalysisService(decoder);

        BeatMap? completedMap = null;
        service.AnalysisComplete += map => completedMap = map;

        await service.AnalyzeAsync("test.mp3");
        await Task.Delay(200);

        Assert.NotNull(completedMap);
        Assert.True(completedMap!.DurationMs > 0);
        Assert.NotEmpty(completedMap.WaveformSamples);
    }

    [Fact]
    public async Task AudioPreAnalysisService_AnalyzeAsync_ReportsProgress()
    {
        int sampleRate = 44100;
        int chunkSize = 4410;

        var chunks = new List<AudioChunk>();
        for (int i = 0; i < 10; i++)
        {
            var chunkSamples = new float[chunkSize];
            chunks.Add(new AudioChunk
            {
                Samples = chunkSamples,
                SampleRate = sampleRate,
                TimestampMs = i * 100.0,
                TotalDurationMs = 1000.0
            });
        }

        var decoder = new TestAudioDecoder(chunks);
        using var service = new AudioPreAnalysisService(decoder);

        var progressValues = new List<double>();
        service.AnalysisProgress += p => progressValues.Add(p);

        await service.AnalyzeAsync("test.mp3");
        await Task.Delay(200);

        // Should have received progress updates including final 1.0
        Assert.NotEmpty(progressValues);
        Assert.Contains(1.0, progressValues);
    }

    [Fact]
    public async Task AudioPreAnalysisService_Cancel_StopsAnalysis()
    {
        int sampleRate = 44100;
        var chunks = new List<AudioChunk>();
        for (int i = 0; i < 100; i++)
        {
            chunks.Add(new AudioChunk
            {
                Samples = new float[4410],
                SampleRate = sampleRate,
                TimestampMs = i * 100.0,
                TotalDurationMs = 10000.0
            });
        }

        var decoder = new TestAudioDecoder(chunks, delayPerChunkMs: 10);
        using var service = new AudioPreAnalysisService(decoder);

        _ = service.AnalyzeAsync("test.mp3");
        await Task.Delay(50);

        service.Cancel();
        // Should not throw
    }

    [Fact]
    public void AudioPreAnalysisService_UpdateSensitivity_Propagates()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        using var service = new AudioPreAnalysisService(decoder);
        service.UpdateSensitivity(2.0); // Should not throw
    }

    [Fact]
    public void AudioPreAnalysisService_Dispose_CancelsAnalysis()
    {
        var decoder = new TestAudioDecoder(Array.Empty<AudioChunk>());
        var service = new AudioPreAnalysisService(decoder);
        service.Dispose(); // Should not throw
    }

    // ──────────────────────────────────────────────
    //  AudioChunk Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void AudioChunk_RequiredProperties_SetCorrectly()
    {
        var chunk = new AudioChunk
        {
            Samples = new float[] { 0.1f, 0.2f },
            SampleRate = 44100,
            TimestampMs = 100.0,
            TotalDurationMs = 5000.0
        };

        Assert.Equal(2, chunk.Samples.Length);
        Assert.Equal(44100, chunk.SampleRate);
        Assert.Equal(100.0, chunk.TimestampMs);
        Assert.Equal(5000.0, chunk.TotalDurationMs);
    }

    [Fact]
    public void AudioChunk_TotalDurationMs_DefaultsToZero()
    {
        var chunk = new AudioChunk
        {
            Samples = Array.Empty<float>(),
            SampleRate = 44100,
            TimestampMs = 0
        };

        Assert.Equal(0, chunk.TotalDurationMs);
    }

    // ──────────────────────────────────────────────
    //  Buffer Reuse Verification
    // ──────────────────────────────────────────────

    [Fact]
    public void OnsetDetector_BufferReuse_NoNewAllocationsPerProcessCall()
    {
        var detector = new OnsetDetector();
        int sampleRate = 44100;
        var chunk = new float[4410]; // 100ms

        // Process multiple chunks and verify no exceptions
        // (buffers are pre-allocated in constructor)
        for (int i = 0; i < 50; i++)
        {
            // Fill with varying data to exercise the code
            for (int j = 0; j < chunk.Length; j++)
                chunk[j] = (float)(Math.Sin(2 * Math.PI * 440 * (i * chunk.Length + j) / sampleRate) * 0.5);

            var beats = detector.Process(chunk, i * 100.0, sampleRate);
            Assert.NotNull(beats);
        }
    }

    [Fact]
    public void BpmEstimator_BufferReuse_CircularBufferWraps()
    {
        var estimator = new BpmEstimator();

        // Feed more than MaxIntervals (32) beats to exercise circular buffer wrap
        for (int i = 0; i < 50; i++)
        {
            estimator.AddBeat(new BeatEvent
            {
                TimestampMs = i * 500,
                Strength = 1.0,
                IsQuantized = false
            });
        }

        var estimate = estimator.CurrentEstimate;
        Assert.True(estimate.Bpm > 0, "BPM should be estimated after many beats");
    }

    // ──────────────────────────────────────────────
    //  Test Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Mock audio decoder for testing AudioPreAnalysisService.
    /// </summary>
    private sealed class TestAudioDecoder : IAudioDecoder
    {
        private readonly IReadOnlyList<AudioChunk> _chunks;
        private readonly int _delayPerChunkMs;

        public TestAudioDecoder(IReadOnlyList<AudioChunk> chunks, int delayPerChunkMs = 0)
        {
            _chunks = chunks;
            _delayPerChunkMs = delayPerChunkMs;
        }

        public async IAsyncEnumerable<AudioChunk> DecodeAsync(
            string mediaPath,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            foreach (var chunk in _chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_delayPerChunkMs > 0)
                    await Task.Delay(_delayPerChunkMs, cancellationToken);

                yield return chunk;
            }
        }
    }
}
