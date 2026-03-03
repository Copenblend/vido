namespace Vido.Core.Haptics;

/// <summary>
/// Immutable snapshot of a single haptic axis configuration.
/// </summary>
/// <remarks>
/// This value type is used in event payloads and is safe to use as <c>default(HapticAxisSnapshot)</c>.
/// </remarks>
public readonly record struct HapticAxisSnapshot
{
    private readonly string? _id;

    /// <summary>Axis identifier (e.g. "L0", "R0", "R1", "R2").</summary>
    public string Id
    {
        get => _id ?? string.Empty;
        init => _id = value;
    }

    /// <summary>Minimum position value for this axis (0–100 scale).</summary>
    public int Min { get; init; }

    /// <summary>Maximum position value for this axis (0–100 scale).</summary>
    public int Max { get; init; }

    /// <summary>Whether this axis is enabled.</summary>
    public bool Enabled { get; init; }
}
