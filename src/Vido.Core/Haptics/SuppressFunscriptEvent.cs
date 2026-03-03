namespace Vido.Core.Haptics;

/// <summary>
/// Published when a feature wants to suppress funscript auto-loading.
/// When <see cref="SuppressFunscripts"/> is <c>true</c>, the haptic transport
/// should skip funscript auto-matching and clear any currently loaded scripts.
/// </summary>
/// <remarks>
/// This event is typically emitted by external control features when they need to own
/// position output and avoid script interference.
/// </remarks>
public readonly record struct SuppressFunscriptEvent
{
    /// <summary>Whether funscript auto-loading should be suppressed.</summary>
    public bool SuppressFunscripts { get; init; }
}
