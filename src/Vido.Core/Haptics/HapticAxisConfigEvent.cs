namespace Vido.Core.Haptics;

/// <summary>
/// Published when axis configuration changes (min/max/enabled).
/// The haptic transport publishes this event so other features can read axis constraints.
/// </summary>
/// <remarks>
/// Emitted when axis limits or enabled state change.
/// </remarks>
public readonly record struct HapticAxisConfigEvent
{
    private readonly IReadOnlyList<HapticAxisSnapshot>? _axes;

    /// <summary>Current configuration snapshot for all axes.</summary>
    public IReadOnlyList<HapticAxisSnapshot> Axes
    {
        get => _axes ?? Array.Empty<HapticAxisSnapshot>();
        init => _axes = value;
    }
}
