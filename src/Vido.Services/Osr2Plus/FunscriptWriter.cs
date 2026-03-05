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
