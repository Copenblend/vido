namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Direction-of-change filter for funscript-based beat detection.
/// Used by the beat detection service to find peaks or valleys in stroke data.
/// </summary>
public enum BeatDetectionMode
{
    /// <summary>Detect peaks (up→down direction changes).</summary>
    OnPeak,

    /// <summary>Detect valleys (down→up direction changes).</summary>
    OnValley
}
