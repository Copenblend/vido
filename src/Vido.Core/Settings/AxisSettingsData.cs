namespace Vido.Core.Settings;

/// <summary>
/// Persisted per-axis configuration for the OSR2+ haptic device.
/// Each axis (L0, R0, R1, R2) has its own instance stored in
/// <see cref="AppSettings.Osr2AxisSettings"/>.
/// </summary>
public sealed class AxisSettingsData
{
    /// <summary>
    /// Minimum position value for this axis (0–100 range).
    /// </summary>
    public int Min { get; set; }

    /// <summary>
    /// Maximum position value for this axis (0–100 range).
    /// </summary>
    public int Max { get; set; } = 100;

    /// <summary>
    /// Whether this axis is enabled for output.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Fill mode name (e.g. "None", "Sin", "Triangle").
    /// Determines idle-position behavior when no funscript is active.
    /// </summary>
    public string FillMode { get; set; } = "None";

    /// <summary>
    /// Whether this axis synchronizes its fill pattern with the stroke axis (L0).
    /// </summary>
    public bool SyncWithStroke { get; set; } = true;

    /// <summary>
    /// Fill pattern speed in Hz (cycles per second).
    /// </summary>
    public double FillSpeedHz { get; set; } = 1.0;

    /// <summary>
    /// Manual position offset applied to this axis (0–100 range).
    /// </summary>
    public double PositionOffset { get; set; }

    /// <summary>
    /// Creates default axis settings for L0, R0, R1, R2.
    /// R2 defaults to <see cref="SyncWithStroke"/> = <c>false</c>.
    /// </summary>
    public static Dictionary<string, AxisSettingsData> CreateDefaults() => new()
    {
        ["L0"] = new() { Min = 0, Max = 100 },
        ["R0"] = new() { Min = 0, Max = 100 },
        ["R1"] = new() { Min = 0, Max = 100 },
        ["R2"] = new() { Min = 0, Max = 100, SyncWithStroke = false },
    };
}
