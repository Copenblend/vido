using System.Runtime.InteropServices;

namespace Vido.Views;

/// <summary>
/// Native Win32 MINMAXINFO structure for WM_GETMINMAXINFO handling.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MINMAXINFO
{
    /// <summary>
    /// Reserved point.
    /// </summary>
    public POINT ptReserved;

    /// <summary>
    /// Maximized window size.
    /// </summary>
    public POINT ptMaxSize;

    /// <summary>
    /// Maximized window position.
    /// </summary>
    public POINT ptMaxPosition;

    /// <summary>
    /// Minimum tracking size.
    /// </summary>
    public POINT ptMinTrackSize;

    /// <summary>
    /// Maximum tracking size.
    /// </summary>
    public POINT ptMaxTrackSize;
}
