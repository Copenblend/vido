using System.Runtime.InteropServices;

namespace Vido.Views;

/// <summary>
/// Native Win32 RECT structure used by monitor sizing interop.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    /// <summary>
    /// Left coordinate.
    /// </summary>
    public int Left;

    /// <summary>
    /// Top coordinate.
    /// </summary>
    public int Top;

    /// <summary>
    /// Right coordinate.
    /// </summary>
    public int Right;

    /// <summary>
    /// Bottom coordinate.
    /// </summary>
    public int Bottom;
}
