using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace Vido.Setup.Services;

/// <summary>
/// Core install/uninstall engine for Vido. Handles file extraction, registry
/// operations, shortcut creation, and file association management.
/// All operations target the current user (HKCU) — no elevation required.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InstallEngine
{
    /// <summary>
    /// Default install directory: <c>%LOCALAPPDATA%\Vido</c>.
    /// </summary>
    public static string DefaultInstallDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vido");

    /// <summary>
    /// GUID used for the Add/Remove Programs uninstall registry key.
    /// </summary>
    public static readonly string UninstallGuid = "B4E9A7C2-3F18-4D6E-A5C1-7E2D9F0B8A63";

    /// <summary>
    /// The ProgID registered for Vido video file associations.
    /// </summary>
    public static readonly string ProgId = "Vido.VideoFile";

    /// <summary>
    /// Supported video file extensions for file association registration.
    /// </summary>
    public static IReadOnlyList<string> VideoExtensions =>
        [".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"];

    private static string UninstallRegistryPath =>
        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{UninstallGuid}}}";

    private static string InstallPathRegistryPath => @"Software\Vido\Install";

    private static string ProgIdRegistryPath => $@"Software\Classes\{ProgId}";

    /// <summary>
    /// Extracts the contents of a zip stream to the specified install directory.
    /// Reports progress via <paramref name="progress"/> as a value from 0.0 to 1.0
    /// with a status message.
    /// </summary>
    /// <param name="payloadZipStream">Stream containing the zip archive to extract.</param>
    /// <param name="installDir">Target directory for extraction.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task ExtractPayloadAsync(
        Stream payloadZipStream,
        string installDir,
        IProgress<(double Progress, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(installDir);

        using var archive = new ZipArchive(payloadZipStream, ZipArchiveMode.Read);
        var entries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .ToList();

        var totalEntries = entries.Count;

        for (var i = 0; i < totalEntries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = entries[i];
            var destinationPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));

            // Validate that the entry doesn't escape the install directory (zip slip)
            if (!destinationPath.StartsWith(
                    Path.GetFullPath(installDir) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                !destinationPath.Equals(
                    Path.GetFullPath(installDir),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entryDir = Path.GetDirectoryName(destinationPath);
            if (entryDir is not null)
                Directory.CreateDirectory(entryDir);

            entry.ExtractToFile(destinationPath, overwrite: true);

            var fraction = (double)(i + 1) / totalEntries;
            progress?.Report((fraction, $"Extracting {entry.Name}..."));

            // Yield periodically to keep UI responsive
            if (i % 50 == 0)
                await Task.Yield();
        }

        progress?.Report((1.0, "Extraction complete."));
    }

    /// <summary>
    /// Creates a desktop shortcut for Vido.exe using IShellLink COM interop.
    /// </summary>
    /// <param name="installDir">The Vido installation directory containing Vido.exe.</param>
    public void CreateDesktopShortcut(string installDir)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktopPath, "Vido.lnk");
        var targetPath = Path.Combine(installDir, "Vido.exe");

        CreateShortcut(shortcutPath, targetPath, installDir);
    }

    /// <summary>
    /// Creates a Start Menu shortcut under Programs\Vido.
    /// </summary>
    /// <param name="installDir">The Vido installation directory containing Vido.exe.</param>
    public void CreateStartMenuShortcut(string installDir)
    {
        var programsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var vidoFolder = Path.Combine(programsPath, "Vido");
        Directory.CreateDirectory(vidoFolder);

        var shortcutPath = Path.Combine(vidoFolder, "Vido.lnk");
        var targetPath = Path.Combine(installDir, "Vido.exe");

        CreateShortcut(shortcutPath, targetPath, installDir);
    }

    /// <summary>
    /// Registers file associations for the specified video extensions.
    /// Creates the Vido.VideoFile ProgID and per-extension entries under HKCU\Software\Classes.
    /// Calls SHChangeNotify to refresh Explorer.
    /// </summary>
    /// <param name="installDir">The Vido installation directory containing Vido.exe.</param>
    /// <param name="extensions">List of file extensions to register (e.g., ".mp4").</param>
    public void RegisterFileAssociations(string installDir, IReadOnlyList<string> extensions)
    {
        var exePath = Path.Combine(installDir, "Vido.exe");

        // Create ProgID: HKCU\Software\Classes\Vido.VideoFile
        using (var progIdKey = Registry.CurrentUser.CreateSubKey(ProgIdRegistryPath))
        {
            progIdKey.SetValue(null, "Vido Video File");

            using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue(null, $"\"{exePath}\",0");

            using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(null, $"\"{exePath}\" \"%1\"");
        }

        // Register each extension
        foreach (var ext in extensions)
        {
            var extKeyPath = $@"Software\Classes\{ext}";
            using var extKey = Registry.CurrentUser.CreateSubKey(extKeyPath);
            extKey.SetValue(null, ProgId);
        }

        // Notify the shell of the association changes
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Removes file associations for the specified extensions.
    /// Only removes entries whose default value is "Vido.VideoFile" to avoid
    /// removing associations set by other applications.
    /// </summary>
    /// <param name="extensions">List of file extensions to unregister (e.g., ".mp4").</param>
    public void RemoveFileAssociations(IReadOnlyList<string> extensions)
    {
        // Remove per-extension entries (only if value is our ProgID)
        foreach (var ext in extensions)
        {
            var extKeyPath = $@"Software\Classes\{ext}";
            using var extKey = Registry.CurrentUser.OpenSubKey(extKeyPath);
            if (extKey is null) continue;

            var value = extKey.GetValue(null) as string;
            if (string.Equals(value, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                Registry.CurrentUser.DeleteSubKeyTree(extKeyPath, throwOnMissingSubKey: false);
            }
        }

        // Remove ProgID
        Registry.CurrentUser.DeleteSubKeyTree(ProgIdRegistryPath, throwOnMissingSubKey: false);

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Registers the application in Add/Remove Programs under HKCU.
    /// Sets DisplayName, DisplayVersion, Publisher, InstallLocation, UninstallString,
    /// DisplayIcon, NoModify, NoRepair, EstimatedSize, and InstallDate.
    /// </summary>
    /// <param name="installDir">The Vido installation directory.</param>
    /// <param name="version">The application version string.</param>
    public void RegisterUninstallEntry(string installDir, string version)
    {
        var exePath = Path.Combine(installDir, "Vido.exe");

        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath);
        key.SetValue("DisplayName", "Vido");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "Vido");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
        key.SetValue("DisplayIcon", $"{exePath},0");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

        // Calculate estimated size in KB
        var installDirInfo = new DirectoryInfo(installDir);
        if (installDirInfo.Exists)
        {
            var sizeKb = (int)(installDirInfo
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length) / 1024);
            key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
        }
    }

    /// <summary>
    /// Removes the Add/Remove Programs uninstall registry entry.
    /// </summary>
    public void RemoveUninstallEntry()
    {
        Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);
    }

    /// <summary>
    /// Writes the install path to HKCU\Software\Vido\Install\Path.
    /// </summary>
    /// <param name="installDir">The Vido installation directory.</param>
    public void RegisterInstallPath(string installDir)
    {
        using var key = Registry.CurrentUser.CreateSubKey(InstallPathRegistryPath);
        key.SetValue("Path", installDir);
    }

    /// <summary>
    /// Removes the install path registry key.
    /// </summary>
    public void RemoveInstallPath()
    {
        Registry.CurrentUser.DeleteSubKeyTree(InstallPathRegistryPath, throwOnMissingSubKey: false);
    }

    /// <summary>
    /// Detects an existing Vido installation by checking the uninstall registry key.
    /// </summary>
    /// <returns>The installed version string, or <c>null</c> if not installed.</returns>
    public string? DetectExistingInstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
        return key?.GetValue("DisplayVersion") as string;
    }

    /// <summary>
    /// Removes the desktop shortcut if it exists.
    /// </summary>
    public void RemoveDesktopShortcut()
    {
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");

        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }

    /// <summary>
    /// Removes the Start Menu shortcut folder and its contents.
    /// </summary>
    public void RemoveStartMenuShortcut()
    {
        var vidoFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");

        if (Directory.Exists(vidoFolder))
            Directory.Delete(vidoFolder, recursive: true);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        // Use WScript.Shell COM to create .lnk files
        var wshShellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
        var wshShell = Activator.CreateInstance(wshShellType)!;

        try
        {
            dynamic shell = wshShell;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.IconLocation = $"{targetPath},0";
            shortcut.Description = "Vido \u2014 Video Player";
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(wshShell);
        }
    }

    // P/Invoke for shell notification
    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(
        uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
