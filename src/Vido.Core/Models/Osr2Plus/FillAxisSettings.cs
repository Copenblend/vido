namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Captured fill settings for a single axis within a FillProfile.
/// Serializable to JSON for persistence.
/// </summary>
public sealed class FillAxisSettings
{
    /// <summary>Whether this axis is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minimum amplitude (0–99).</summary>
    public int Min { get; set; } = 0;

    /// <summary>Maximum amplitude (1–100).</summary>
    public int Max { get; set; } = 100;

    /// <summary>Fill mode name (e.g. "None", "R0", "R1", "R2").</summary>
    public string FillMode { get; set; } = "None";

    /// <summary>Whether fill movement synchronizes with the L0 stroke.</summary>
    public bool SyncWithStroke { get; set; }

    /// <summary>Fill oscillation speed in hertz.</summary>
    public double FillSpeedHz { get; set; } = 1.0;
}
