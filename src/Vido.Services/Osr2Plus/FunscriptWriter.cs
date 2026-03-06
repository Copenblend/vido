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
    /// Replicates PulseTCodeMapper's intensity formula: at each beat, the
    /// waveform amplitude is sampled from <see cref="BeatMap.WaveformSamples"/>
    /// and combined with <see cref="BeatEvent.Strength"/> to scale the stroke range.
    /// Even-indexed beats produce upstroke (top) positions, odd-indexed beats
    /// produce downstroke (bottom) positions.
    /// </summary>
    /// <param name="beatMap">Beat map containing beats, waveform samples, and sample rate.</param>
    /// <returns>List of amplitude-scaled funscript actions.</returns>
    public static List<FunscriptAction> CreateActionsFromBeatMap(BeatMap beatMap)
    {
        const double MinPosition = 5.0;
        const double MaxPosition = 95.0;
        const double RestPosition = 50.0;
        const double MinAmplitudeScale = 0.15;

        var beats = beatMap.Beats;
        var actions = new List<FunscriptAction>(beats.Count);
        if (beats.Count == 0) return actions;

        var waveform = beatMap.WaveformSamples;
        int sampleRate = beatMap.WaveformSampleRate;

        for (int i = 0; i < beats.Count; i++)
        {
            var beat = beats[i];
            double beatStrength = Math.Clamp(beat.Strength, 0.0, 1.0);

            // Sample amplitude from the pre-computed waveform envelope
            double amplitude = SampleWaveformAmplitude(waveform, sampleRate, beat.TimestampMs);

            // Replicate PulseTCodeMapper intensity formula
            double amplitudeScale = MinAmplitudeScale + (1.0 - MinAmplitudeScale) * amplitude;
            double intensityScale = amplitudeScale * (0.5 + 0.5 * beatStrength);

            double halfRange = (MaxPosition - MinPosition) / 2.0 * intensityScale;
            double top = Math.Min(RestPosition + halfRange, MaxPosition);
            double bottom = Math.Max(RestPosition - halfRange, MinPosition);

            // Even beats → top (upstroke peak), odd beats → bottom (downstroke trough)
            double position = (i % 2 == 0) ? top : bottom;
            actions.Add(new FunscriptAction((long)beat.TimestampMs, (int)Math.Round(position)));
        }

        return actions;
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
