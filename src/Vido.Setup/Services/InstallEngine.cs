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

    private readonly string _uninstallRegistryPath;
    private readonly string _installPathRegistryPath;
    private readonly string _progIdRegistryPath;

    /// <summary>
    /// Creates a new <see cref="InstallEngine"/> using production registry paths.
    /// </summary>
    public InstallEngine() : this(null) { }

    /// <summary>
    /// Creates a new <see cref="InstallEngine"/> with an optional registry key
    /// prefix for test isolation.  When <paramref name="registryKeyPrefix"/> is
    /// provided, all registry operations target paths under that prefix instead
    /// of the shared production paths, eliminating Windows kernel zombie-key
    /// races between tests and the real installation.
    /// </summary>
    /// <param name="registryKeyPrefix">
    /// Registry path prefix (e.g. <c>"Software\VidoTests\{guid}"</c>).
    /// When <c>null</c>, standard production paths are used.
    /// </param>
    public InstallEngine(string? registryKeyPrefix)
    {
        if (registryKeyPrefix is null)
        {
            _uninstallRegistryPath = UninstallRegistryPath;
            _installPathRegistryPath = InstallPathRegistryPath;
            _progIdRegistryPath = ProgIdRegistryPath;
        }
        else
        {
            _uninstallRegistryPath = $@"{registryKeyPrefix}\Uninstall\{{{UninstallGuid}}}";
            _installPathRegistryPath = $@"{registryKeyPrefix}\Install";
            _progIdRegistryPath = $@"{registryKeyPrefix}\Classes\{ProgId}";
        }
    }

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

        // Retry on transient IO/COM errors from shell indexing or antivirus.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                CreateShortcut(shortcutPath, targetPath, installDir);
                return;
            }
            catch (Exception ex) when (
                attempt < 4 &&
                (ex is IOException || ex.HResult == unchecked((int)0x80020009)))
            {
                Thread.Sleep(100 * (attempt + 1));
            }
        }
    }

    /// <summary>
    /// Creates a Start Menu shortcut under Programs\Vido.
    /// </summary>
    /// <param name="installDir">The Vido installation directory containing Vido.exe.</param>
    public void CreateStartMenuShortcut(string installDir)
    {
        var programsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var vidoFolder = Path.Combine(programsPath, "Vido");
        var shortcutPath = Path.Combine(vidoFolder, "Vido.lnk");
        var targetPath = Path.Combine(installDir, "Vido.exe");

        // Directory creation + COM shortcut save can race with Windows Explorer
        // or antivirus indexing; retry on transient IO errors.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.CreateDirectory(vidoFolder);
                CreateShortcut(shortcutPath, targetPath, installDir);
                return;
            }
            catch (Exception ex) when (
                attempt < 4 &&
                (ex is IOException || ex is DirectoryNotFoundException ||
                 ex.HResult == unchecked((int)0x80020009))) // DISP_E_EXCEPTION from COM
            {
                Thread.Sleep(100 * (attempt + 1));
            }
        }
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
        WriteAndVerifyRegistryValue(
            _progIdRegistryPath,
            () =>
            {
                using var progIdKey = Registry.CurrentUser.CreateSubKey(_progIdRegistryPath);
                progIdKey.SetValue(null, "Vido Video File");

                using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue(null, $"\"{exePath}\",0");

                using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
                commandKey.SetValue(null, $"\"{exePath}\" \"%1\"");
            });

        // Register each extension
        foreach (var ext in extensions)
        {
            var extKeyPath = $@"Software\Classes\{ext}";
            WriteAndVerifyRegistryValue(
                extKeyPath,
                () =>
                {
                    using var extKey = Registry.CurrentUser.CreateSubKey(extKeyPath);
                    extKey.SetValue(null, ProgId);
                });
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
                RetryOnRegistryConflict(() =>
                    Registry.CurrentUser.DeleteSubKeyTree(extKeyPath, throwOnMissingSubKey: false));
            }
        }

        // Remove ProgID
        RetryOnRegistryConflict(() =>
            Registry.CurrentUser.DeleteSubKeyTree(_progIdRegistryPath, throwOnMissingSubKey: false));

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

        // Windows may still be finalising a prior DeleteSubKeyTree at the kernel
        // level.  CreateSubKey can succeed on a key that is still marked for
        // deletion, causing the data to silently vanish once the kernel completes
        // the delete.  We use a unique write-marker to detect zombie handles and
        // retry the entire write‐then‐verify cycle to tolerate this race.
        var writeMarker = Guid.NewGuid().ToString("N");

        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(_uninstallRegistryPath))
                {
                    key.SetValue("DisplayName", "Vido");
                    key.SetValue("DisplayVersion", version);
                    key.SetValue("Publisher", "Vido");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
                    key.SetValue("DisplayIcon", $"{exePath},0");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("_WriteMarker", writeMarker);

                    var installDirInfo = new DirectoryInfo(installDir);
                    if (installDirInfo.Exists)
                    {
                        var sizeKb = (int)(installDirInfo
                            .EnumerateFiles("*", SearchOption.AllDirectories)
                            .Sum(f => f.Length) / 1024);
                        key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
                    }
                }

                // Verify the write actually persisted using the unique marker,
                // not a data value that could match stale zombie data.
                using (var verify = Registry.CurrentUser.OpenSubKey(_uninstallRegistryPath, writable: true))
                {
                    if (verify?.GetValue("_WriteMarker") as string == writeMarker)
                    {
                        verify.DeleteValue("_WriteMarker", throwOnMissingValue: false);
                        return;
                    }
                }
            }
            catch (IOException) when (attempt < 9)
            {
                // Key still marked for deletion — retry
            }

            Thread.Sleep(100 * (attempt + 1));
        }
    }

    /// <summary>
    /// Removes the Add/Remove Programs uninstall registry entry.
    /// </summary>
    public void RemoveUninstallEntry()
    {
        RetryOnRegistryConflict(() =>
            Registry.CurrentUser.DeleteSubKeyTree(_uninstallRegistryPath, throwOnMissingSubKey: false));
    }

    /// <summary>
    /// Writes the install path to HKCU\Software\Vido\Install\Path.
    /// </summary>
    /// <param name="installDir">The Vido installation directory.</param>
    public void RegisterInstallPath(string installDir)
    {
        WriteAndVerifyRegistryValue(
            _installPathRegistryPath,
            () =>
            {
                using var key = Registry.CurrentUser.CreateSubKey(_installPathRegistryPath);
                key.SetValue("Path", installDir);
            });
    }

    /// <summary>
    /// Removes the install path registry key.
    /// </summary>
    public void RemoveInstallPath()
    {
        RetryOnRegistryConflict(() =>
            Registry.CurrentUser.DeleteSubKeyTree(_installPathRegistryPath, throwOnMissingSubKey: false));
    }

    /// <summary>
    /// Detects an existing Vido installation by checking the uninstall registry key.
    /// </summary>
    /// <returns>The installed version string, or <c>null</c> if not installed.</returns>
    public string? DetectExistingInstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_uninstallRegistryPath);
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

    /// <summary>
    /// Retries an action that may fail because Windows is still finalising
    /// a registry key deletion (async at the kernel level).
    /// </summary>
    private static void RetryOnRegistryConflict(Action action, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (i < maxAttempts - 1)
            {
                Thread.Sleep(100 * (i + 1));
            }
        }
    }

    /// <summary>
    /// Writes registry values via <paramref name="writeAction"/>, adding a unique
    /// write-marker to verify the data persisted (guards against zombie handles
    /// from prior deletions still being finalised by the Windows kernel).
    /// </summary>
    private static void WriteAndVerifyRegistryValue(
        string keyPath, Action writeAction)
    {
        var marker = Guid.NewGuid().ToString("N");

        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                writeAction();

                // Write a unique marker to detect zombie handles
                using (var markerKey = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                {
                    markerKey?.SetValue("_WriteMarker", marker);
                }

                // Verify the marker survived (proves the handle isn't a zombie)
                using (var verify = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                {
                    if (verify?.GetValue("_WriteMarker") as string == marker)
                    {
                        verify.DeleteValue("_WriteMarker", throwOnMissingValue: false);
                        return;
                    }
                }
            }
            catch (IOException) when (attempt < 9)
            {
                // Key still marked for deletion — retry
            }

            Thread.Sleep(100 * (attempt + 1));
        }
    }
}
