namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// A single funscript action: a position (0–100) at a specific time in milliseconds.
/// </summary>
/// <param name="AtMs">Timestamp in milliseconds from the start of the media.</param>
/// <param name="Pos">Target position value (0–100).</param>
public record FunscriptAction(long AtMs, int Pos);

/// <summary>
/// Parsed funscript data for a single axis, containing the axis identifier,
/// source file path, and the ordered list of actions.
/// </summary>
public class FunscriptData
{
    /// <summary>The axis this funscript targets (e.g. "L0", "R0").</summary>
    public string AxisId { get; set; } = "L0";

    /// <summary>Full file path to the source funscript file.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Ordered list of funscript actions (position changes over time).</summary>
    public List<FunscriptAction> Actions { get; set; } = [];
}
