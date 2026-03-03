namespace Vido.Core.Haptics;

/// <summary>
/// Published when a haptic transport connects or disconnects.
/// The haptic transport (e.g. OSR2+) publishes this event on <c>IEventBus</c>
/// so other features can observe transport state without direct coupling.
/// </summary>
/// <remarks>
/// A disconnected/default state is represented by <see cref="IsConnected"/> = <c>false</c>
/// and a null <see cref="ConnectionLabel"/>.
/// </remarks>
public readonly record struct HapticTransportStateEvent
{
    /// <summary>Whether the haptic transport is currently connected.</summary>
    public bool IsConnected { get; init; }

    /// <summary>Human-readable connection label (e.g. "UDP:7777" or "COM:COM3"). Null when disconnected.</summary>
    public string? ConnectionLabel { get; init; }
}
