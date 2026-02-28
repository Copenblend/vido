namespace Vido.Core.FileSystem;

/// <summary>
/// Native shell interop methods used by <see cref="FileAssociationHelper"/>.
/// </summary>
internal static class FileAssociationNativeMethods
{
    /// <summary>
    /// Notifies the Windows shell that file associations have changed.
    /// </summary>
    /// <param name="wEventId">Shell change event identifier.</param>
    /// <param name="uFlags">Notification flags that control payload interpretation.</param>
    /// <param name="dwItem1">Optional first payload pointer.</param>
    /// <param name="dwItem2">Optional second payload pointer.</param>
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    internal static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
