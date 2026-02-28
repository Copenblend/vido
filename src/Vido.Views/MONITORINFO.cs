using System.Runtime.InteropServices;

namespace Vido.Views;

/// <summary>
/// Native Win32 MONITORINFO structure used when calculating maximized bounds.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    /// <summary>
    /// Size of this structure in bytes.
    /// </summary>
    public int cbSize;

    /// <summary>
    /// Monitor rectangle in virtual-screen coordinates.
    /// </summary>
    public RECT rcMonitor;

    /// <summary>
    /// Work area rectangle in virtual-screen coordinates.
    /// </summary>
    public RECT rcWork;

    /// <summary>
    /// Monitor flags.
    /// </summary>
    public uint dwFlags;
}
