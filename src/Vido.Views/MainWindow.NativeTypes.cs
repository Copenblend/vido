using System.Runtime.InteropServices;

namespace Vido.Views;

/// <summary>
/// Native Win32 POINT structure used by window sizing interop.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    /// <summary>
    /// X coordinate.
    /// </summary>
    public int X;

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public int Y;
}
