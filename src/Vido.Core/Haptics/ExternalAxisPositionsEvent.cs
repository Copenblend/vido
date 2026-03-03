namespace Vido.Core.Haptics;

/// <summary>
/// Published when an external source provides axis positions, bypassing funscript interpolation.
/// The haptic transport should use these positions for the specified axes.
/// </summary>
/// <remarks>
/// Sources such as Pulse publish this event to override interpolated script output with
/// explicit axis positions for one or more axes.
/// </remarks>
public readonly record struct ExternalAxisPositionsEvent
{
    private readonly ReadOnlyMemory<AxisPosition>? _positions;

    /// <summary>
    /// Axis positions to apply. Each element contains an axis ID and position value.
    /// Only axes present in this memory slice are affected.
    /// Defaults to <see cref="ReadOnlyMemory{T}.Empty"/> for <c>default(ExternalAxisPositionsEvent)</c>.
    /// </summary>
    public ReadOnlyMemory<AxisPosition> Positions
    {
        get => _positions ?? ReadOnlyMemory<AxisPosition>.Empty;
        init => _positions = value;
    }
}
