namespace Vido.Core.Models.Pulse;

/// <summary>
/// Immutable settings controlling Pulse stroke behavior — amplitude, easing, pattern, and randomness.
/// </summary>
public sealed record PulseStrokeSettings
{
    /// <summary>
    /// Additive amplitude offset. −1.0 = zero movement, 0.0 = default (audio-driven),
    /// +1.0 = full-range strokes.
    /// </summary>
    public double AmplitudeOffset { get; init; }

    /// <summary>
    /// Easing blend. −1.0 = gentle (sinusoidal), 0.0 = default (quadratic),
    /// +1.0 = aggressive (linear).
    /// </summary>
    public double EasingBlend { get; init; }

    /// <summary>
    /// Stroke waveform pattern. Default: Classic.
    /// </summary>
    public StrokePattern Pattern { get; init; }

    /// <summary>
    /// Random amplitude variation per beat. 0.0 = none, 1.0 = full (0.2×–1.0× random multiplier).
    /// </summary>
    public double Randomness { get; init; }

    /// <summary>Default settings matching current behavior.</summary>
    public static PulseStrokeSettings Default { get; } = new();
}
