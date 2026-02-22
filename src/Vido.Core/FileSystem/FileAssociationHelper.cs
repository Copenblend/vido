using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Vido.Core.FileSystem;

/// <summary>
/// Provides methods for registering and unregistering file associations
/// in the Windows registry. Intended to be called by an installer or
/// a "Set File Associations" action, not by normal app startup.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FileAssociationHelper
{
    /// <summary>
    /// The ProgID used to identify Vido in the registry.
    /// </summary>
    public const string ProgId = "Vido.VideoFile";

    /// <summary>
    /// A user-friendly description for the associated file type.
    /// </summary>
    public const string FileTypeDescription = "Vido Video File";

    /// <summary>
    /// The set of video extensions that Vido can associate with.
    /// Sourced from <see cref="FileNode.VideoExtensions"/>.
    /// </summary>
    public static IReadOnlyCollection<string> SupportedExtensions => FileNode.VideoExtensions;

    /// <summary>
    /// Registers Vido as a handler for the specified video file extensions
    /// under HKEY_CURRENT_USER so no admin elevation is required.
    /// </summary>
    /// <param name="exePath">Full path to Vido.exe.</param>
    /// <param name="extensions">
    /// Extensions to register (e.g. ".mp4", ".mkv"). If null, all
    /// <see cref="SupportedExtensions"/> are registered.
    /// </param>
    public static void Register(string exePath, IEnumerable<string>? extensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        var exts = extensions ?? SupportedExtensions;

        // Create the ProgID key:
        //   HKCU\Software\Classes\Vido.VideoFile
        //     (Default)  = "Vido Video File"
        //     shell\open\command  = "\"<exePath>\" \"%1\""
        using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        progIdKey.SetValue(string.Empty, FileTypeDescription);

        using var shellKey = progIdKey.CreateSubKey(@"shell\open\command");
        shellKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");

        // Set the default icon to the exe itself (icon index 0).
        using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
        iconKey.SetValue(string.Empty, $"\"{exePath}\",0");

        // For each extension, point it at our ProgID:
        //   HKCU\Software\Classes\.mp4
        //     (Default) = "Vido.VideoFile"
        foreach (var ext in exts)
        {
            var normalizedExt = ext.StartsWith('.') ? ext : $".{ext}";
            using var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{normalizedExt}");
            extKey.SetValue(string.Empty, ProgId);
        }

        // Notify the shell that associations have changed.
        NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Removes file associations previously registered by <see cref="Register"/>.
    /// </summary>
    /// <param name="extensions">
    /// Extensions to unregister. If null, all <see cref="SupportedExtensions"/>
    /// are unregistered.
    /// </param>
    public static void Unregister(IEnumerable<string>? extensions = null)
    {
        var exts = extensions ?? SupportedExtensions;

        foreach (var ext in exts)
        {
            var normalizedExt = ext.StartsWith('.') ? ext : $".{ext}";
            var keyPath = $@"Software\Classes\{normalizedExt}";

            using var extKey = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (extKey is null) continue;

            // Only remove if it points to our ProgID.
            var current = extKey.GetValue(string.Empty) as string;
            if (string.Equals(current, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
            }
        }

        // Remove the ProgID key.
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);

        NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Checks whether the specified extension is currently associated with Vido.
    /// </summary>
    public static bool IsAssociated(string extension)
    {
        var normalizedExt = extension.StartsWith('.') ? extension : $".{extension}";
        using var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{normalizedExt}");
        var current = extKey?.GetValue(string.Empty) as string;
        return string.Equals(current, ProgId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// P/Invoke for shell change notification.
    /// </summary>
    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        internal static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
