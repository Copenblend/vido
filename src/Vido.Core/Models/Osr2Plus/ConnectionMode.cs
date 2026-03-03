namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Transport connection mode for the OSR2+ device.
/// </summary>
public enum ConnectionMode
{
    /// <summary>UDP network connection (default).</summary>
    UDP,

    /// <summary>Serial COM port connection.</summary>
    Serial
}
