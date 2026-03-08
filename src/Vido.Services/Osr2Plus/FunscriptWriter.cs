using System.Text;
using System.Text.Json;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Models.Pulse;

namespace Vido.Services.Osr2Plus;

/// <summary>
/// Utility for creating and writing .funscript files from Pulse beat data.
/// All methods are static and thread-safe.
/// </summary>
public static class FunscriptWriter
{
    /// <summary>
    /// Converts beat events into funscript actions that alternate between
    /// <paramref name="highPos"/> and <paramref name="lowPos"/>.
    /// Even-indexed beats get <paramref name="highPos"/>, odd-indexed beats get <paramref name="lowPos"/>.
    /// </summary>
    /// <param name="beats">Ordered list of beat events.</param>
    /// <param name="highPos">Position for even-indexed beats (default 100).</param>
    /// <param name="lowPos">Position for odd-indexed beats (default 0).</param>
    /// <returns>List of funscript actions matching the beat timestamps.</returns>
    public static List<FunscriptAction> CreateActionsFromBeats(
        IReadOnlyList<BeatEvent> beats, int highPos = 100, int lowPos = 0)
    {
        var actions = new List<FunscriptAction>(beats.Count);
        for (int i = 0; i < beats.Count; i++)
        {
            int pos = (i % 2 == 0) ? highPos : lowPos;
            actions.Add(new FunscriptAction((long)beats[i].TimestampMs, pos));
        }
        return actions;
    }

    /// <summary>
    /// Converts a beat map into amplitude-aware funscript actions.
    /// Delegates to <see cref="CreateActionsFromBeatMap(BeatMap, PulseStrokeSettings)"/>
    /// with <see cref="PulseStrokeSettings.Default"/>.
    /// </summary>
    /// <param name="beatMap">Beat map containing beats, waveform samples, and sample rate.</param>
    /// <returns>List of amplitude-scaled funscript actions.</returns>
    public static List<FunscriptAction> CreateActionsFromBeatMap(BeatMap beatMap)
        => CreateActionsFromBeatMap(beatMap, PulseStrokeSettings.Default);

    /// <summary>
    /// Converts a beat map into amplitude-aware funscript actions with stroke control adjustments baked in.
    /// Replicates PulseTCodeMapper's intensity formula: at each beat, the
    /// waveform amplitude is sampled from <see cref="BeatMap.WaveformSamples"/>
    /// and combined with <see cref="BeatEvent.Strength"/> to scale the stroke range.
    /// Stroke patterns, amplitude offset, and randomness are applied per the given settings.
    /// </summary>
    /// <param name="beatMap">Beat map containing beats, waveform samples, and sample rate.</param>
    /// <param name="settings">Stroke control settings to apply.</param>
    /// <returns>List of amplitude-scaled funscript actions with stroke patterns applied.</returns>
    public static List<FunscriptAction> CreateActionsFromBeatMap(BeatMap beatMap, PulseStrokeSettings settings)
    {
        const double MinPosition = 5.0;
        const double MaxPosition = 95.0;
        const double RestPosition = 50.0;
        const double MinAmplitudeScale = 0.15;

        var beats = beatMap.Beats;
        if (beats.Count == 0) return [];

        var waveform = beatMap.WaveformSamples;
        int sampleRate = beatMap.WaveformSampleRate;
        double maxHalfRange = (MaxPosition - MinPosition) / 2.0;

        int estimatedCapacity = settings.Pattern switch
        {
            StrokePattern.DoubleTap => beats.Count * 4,
            StrokePattern.TripleTap => beats.Count * 6,
            StrokePattern.HoldTop or StrokePattern.HoldBottom => beats.Count * 3,
            _ => beats.Count,
        };
        var actions = new List<FunscriptAction>(estimatedCapacity);

        for (int i = 0; i < beats.Count; i++)
        {
            var beat = beats[i];
            double beatStrength = Math.Clamp(beat.Strength, 0.0, 1.0);

            // Sample amplitude from the pre-computed waveform envelope
            double amplitude = SampleWaveformAmplitude(waveform, sampleRate, beat.TimestampMs);

            // Replicate PulseTCodeMapper intensity formula
            double amplitudeScale = MinAmplitudeScale + (1.0 - MinAmplitudeScale) * amplitude;
            double intensityScale = amplitudeScale * (0.5 + 0.5 * beatStrength);

            double halfRange = maxHalfRange * intensityScale;

            // Apply amplitude offset (same formula as PulseTCodeMapper)
            if (settings.AmplitudeOffset > 0.0)
            {
                halfRange = halfRange + (maxHalfRange - halfRange) * settings.AmplitudeOffset;
            }
            else if (settings.AmplitudeOffset < 0.0)
            {
                halfRange = Math.Clamp(halfRange * (1.0 + settings.AmplitudeOffset), 0.0, maxHalfRange);
            }

            // Apply randomness (same PseudoRandom as PulseTCodeMapper)
            if (settings.Randomness > 0.0)
            {
                double randomFactor = PseudoRandom(i * 73856093);
                double variation = 0.2 + 0.8 * randomFactor;
                double blended = 1.0 + settings.Randomness * (variation - 1.0);
                halfRange *= blended;
            }

            double top = Math.Min(RestPosition + halfRange, MaxPosition);
            double bottom = Math.Max(RestPosition - halfRange, MinPosition);

            double interval = GetBeatInterval(beats, i, beatMap.Bpm);

            switch (settings.Pattern)
            {
                case StrokePattern.DoubleTap:
                    AddMultiTapActions(actions, beat.TimestampMs, interval, top, bottom, i, 2);
                    break;
                case StrokePattern.TripleTap:
                    AddMultiTapActions(actions, beat.TimestampMs, interval, top, bottom, i, 3);
                    break;
                case StrokePattern.HoldTop:
                    AddHoldActions(actions, beat.TimestampMs, interval, top, bottom, holdAtTop: true);
                    break;
                case StrokePattern.HoldBottom:
                    AddHoldActions(actions, beat.TimestampMs, interval, top, bottom, holdAtTop: false);
                    break;
                default:
                    actions.Add(new FunscriptAction((long)beat.TimestampMs, (int)Math.Round(
                        (i % 2 == 0) ? top : bottom)));
                    break;
            }
        }

        return actions;
    }

    /// <summary>
    /// Returns the interval in milliseconds from the given beat to the next beat,
    /// or falls back to the BPM-derived interval for the last beat.
    /// </summary>
    private static double GetBeatInterval(IReadOnlyList<BeatEvent> beats, int index, double bpm)
    {
        if (index + 1 < beats.Count)
            return beats[index + 1].TimestampMs - beats[index].TimestampMs;
        if (bpm > 0)
            return 60000.0 / bpm;
        return 500.0;
    }

    /// <summary>
    /// Generates multi-tap keyframes within a beat interval.
    /// DoubleTap (taps=2) produces 4 keyframes, TripleTap (taps=3) produces 6.
    /// </summary>
    private static void AddMultiTapActions(
        List<FunscriptAction> actions, double timestampMs, double interval,
        double top, double bottom, int beatIndex, int taps)
    {
        int subActionCount = taps * 2;
        bool startHigh = (beatIndex % 2 == 0);
        for (int j = 0; j < subActionCount; j++)
        {
            double t = timestampMs + interval * j / subActionCount;
            bool isHigh = (j % 2 == 0) == startHigh;
            double pos = isHigh ? top : bottom;
            actions.Add(new FunscriptAction((long)t, (int)Math.Round(pos)));
        }
    }

    /// <summary>
    /// Generates hold-pattern keyframes: arrival at 30%, hold-end at 70%, return at 100% of interval.
    /// </summary>
    private static void AddHoldActions(
        List<FunscriptAction> actions, double timestampMs, double interval,
        double top, double bottom, bool holdAtTop)
    {
        double holdPos = holdAtTop ? top : bottom;
        double returnPos = holdAtTop ? bottom : top;

        actions.Add(new FunscriptAction((long)(timestampMs + interval * 0.30), (int)Math.Round(holdPos)));
        actions.Add(new FunscriptAction((long)(timestampMs + interval * 0.70), (int)Math.Round(holdPos)));
        actions.Add(new FunscriptAction((long)(timestampMs + interval), (int)Math.Round(returnPos)));
    }

    /// <summary>
    /// Returns a deterministic pseudo-random value in [0, 1] for a given integer seed.
    /// Duplicated from PulseTCodeMapper for isolation (FunscriptWriter is static).
    /// </summary>
    private static double PseudoRandom(int seed)
    {
        uint x = unchecked((uint)seed);
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0x7FFFFFFF) / (double)0x7FFFFFFF;
    }

    /// <summary>
    /// Filters a beat map by taking every Nth beat, where N is the divisor.
    /// Replicates <c>PulseEngine.RebuildEffectiveBeatMap</c> logic.
    /// If <paramref name="divisor"/> is 1 or less, returns the input unchanged.
    /// </summary>
    /// <param name="beatMap">The beat map to filter.</param>
    /// <param name="divisor">Beat divisor: 1 = every beat, 2 = every other, 3 = every 3rd, 4 = every 4th.</param>
    /// <returns>A filtered beat map with scaled BPM and preserved waveform data.</returns>
    public static BeatMap FilterBeatsByDivisor(BeatMap beatMap, int divisor)
    {
        if (divisor <= 1) return beatMap;

        var beats = beatMap.Beats;
        var filtered = new List<BeatEvent>();
        for (int i = 0; i < beats.Count; i += divisor)
            filtered.Add(beats[i]);

        return new BeatMap
        {
            Beats = filtered,
            Bpm = beatMap.Bpm / divisor,
            BpmConfidence = beatMap.BpmConfidence,
            DurationMs = beatMap.DurationMs,
            WaveformSamples = beatMap.WaveformSamples,
            WaveformSampleRate = beatMap.WaveformSampleRate
        };
    }

    /// <summary>
    /// Samples the pre-computed waveform amplitude at the given timestamp.
    /// Returns 0.0 if waveform data is empty or the timestamp is out of range.
    /// </summary>
    internal static double SampleWaveformAmplitude(
        IReadOnlyList<float> waveformSamples, int sampleRate, double timestampMs)
    {
        if (waveformSamples == null || waveformSamples.Count == 0 || sampleRate <= 0)
            return 0.0;

        int index = (int)(timestampMs / 1000.0 * sampleRate);
        if (index < 0) return 0.0;
        if (index >= waveformSamples.Count) return 0.0;

        return Math.Clamp(waveformSamples[index], 0f, 1f);
    }

    /// <summary>
    /// Serializes funscript actions to a standard .funscript JSON string.
    /// Output format: <c>{"version":"1.0","actions":[{"at":1250,"pos":100},...]}</c>.
    /// </summary>
    /// <param name="actions">Ordered list of funscript actions.</param>
    /// <returns>JSON string in the standard funscript format.</returns>
    public static string Serialize(IReadOnlyList<FunscriptAction> actions)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("version", "1.0");
        writer.WriteStartArray("actions");
        foreach (var action in actions)
        {
            writer.WriteStartObject();
            writer.WriteNumber("at", action.AtMs);
            writer.WriteNumber("pos", action.Pos);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Writes funscript actions to a file asynchronously.
    /// Creates the target directory if it does not exist.
    /// </summary>
    /// <param name="actions">Ordered list of funscript actions.</param>
    /// <param name="filePath">Target file path (should end in .funscript).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task WriteAsync(
        IReadOnlyList<FunscriptAction> actions, string filePath,
        CancellationToken cancellationToken = default)
    {
        var json = Serialize(actions);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
    }
}
