namespace Vido.Core.Models.Pulse;

/// <summary>
/// Defines the available stroke waveform patterns for Pulse haptic motion.
/// </summary>
public enum StrokePattern
{
    /// <summary>Standard up/down stroke per beat.</summary>
    Classic,

    /// <summary>Two up/down cycles per beat interval.</summary>
    DoubleTap,

    /// <summary>Three up/down cycles per beat interval.</summary>
    TripleTap,

    /// <summary>Stroke up, hold at top, then return.</summary>
    HoldTop,

    /// <summary>Stroke down, hold at bottom, then return.</summary>
    HoldBottom
}
