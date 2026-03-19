namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Fill mode applied to an axis when no funscript is loaded or as an override pattern.
/// Determines the idle-position waveform shape.
/// </summary>
public enum AxisFillMode
{
    /// <summary>No fill — only funscript data or midpoint.</summary>
    None,

    /// <summary>Smooth random movement (cosine-interpolated).</summary>
    Random,

    /// <summary>Linear ascending/descending waveform.</summary>
    Triangle,

    /// <summary>Smooth sinusoidal waveform.</summary>
    Sine,

    /// <summary>Linear ascending ramp, instant drop.</summary>
    Saw,

    /// <summary>Instant snap up, linear descending ramp.</summary>
    SawtoothReverse,

    /// <summary>Instant alternation between min and max.</summary>
    Square,

    /// <summary>Holds at extremes with quick transitions.</summary>
    Pulse,

    /// <summary>Sine-like with sharper acceleration/deceleration at extremes.</summary>
    EaseInOut,

    /// <summary>Ramp 0→1, hold at top, smooth cosine drop back to 0. Pitch-only.</summary>
    Grind,

    /// <summary>Ramp 1→0, hold at bottom, smooth cosine rise back to 1. Pitch-only.</summary>
    ReverseGrind,

    /// <summary>Ramp 0→1 then hold at top for remainder. Pitch-only.</summary>
    SharpGrind,

    /// <summary>Ramp 1→0 then hold at bottom for remainder. Pitch-only.</summary>
    SharpReverseGrind,

    /// <summary>Phase-shifted sine producing a rocking motion. Pitch-only.</summary>
    Rocker,

    /// <summary>Reverse phase-shifted sine producing a rocking motion. Pitch-only.</summary>
    ReverseRocker
}
