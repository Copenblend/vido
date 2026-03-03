namespace Vido.Core.Haptics;

/// <summary>
/// Immutable position value for a single haptic axis.
/// </summary>
/// <remarks>
/// Used as an element in <see cref="ExternalAxisPositionsEvent"/> for multi-axis,
/// allocation-aware position publishing on the event bus.
/// </remarks>
public readonly record struct AxisPosition
{
    private readonly string? _axisId;

    /// <summary>
    /// Axis identifier (for example: "L0", "R0", "R1", "R2").
    /// Defaults to <see cref="string.Empty"/> for <c>default(AxisPosition)</c>.
    /// </summary>
    public string AxisId
    {
        get => _axisId ?? string.Empty;
        init => _axisId = value;
    }

    /// <summary>
    /// Position value for this axis (0–100 scale).
    /// </summary>
    public double Position { get; init; }
}
