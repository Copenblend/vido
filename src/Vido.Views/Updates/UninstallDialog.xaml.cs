using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;

using Microsoft.Win32;

namespace Vido.Views.Updates;

/// <summary>
/// Branded uninstall confirmation dialog.
/// Shown when the app is launched with <c>--uninstall</c> from Add/Remove Programs.
/// Handles all registry cleanup, shortcut removal, optional app data deletion,
/// and self-deletion via a cleanup script.
/// </summary>
[SupportedOSPlatform("windows")]
public partial class UninstallDialog : Window
{
    // Constants matching InstallEngine in Vido.Setup
    internal const string UninstallGuid = "B4E9A7C2-3F18-4D6E-A5C1-7E2D9F0B8A63";
    internal const string ProgId = "Vido.VideoFile";

    internal static readonly string[] VideoExtensions =
        [".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"];

    internal static string UninstallRegistryPath =>
        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{UninstallGuid}}}";

    internal static string InstallPathRegistryPath => @"Software\Vido\Install";
    internal static string ProgIdRegistryPath => $@"Software\Classes\{ProgId}";

    /// <summary>
    /// Whether the user opted to also delete settings and app data.
    /// </summary>
    public bool DeleteAppData { get; set; }

    public UninstallDialog()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteAppData = DeleteAppDataCheckBox.IsChecked == true;
        ExecuteUninstall();
    }

    /// <summary>
    /// Performs the full uninstall sequence:
    /// 1. Remove Add/Remove Programs registry entry.
    /// 2. Remove file associations (only if value is Vido.VideoFile).
    /// 3. Remove install path registry key.
    /// 4. Remove Desktop and Start Menu shortcuts.
    /// 5. Optionally delete app data (%APPDATA%\Vido\).
    /// 6. Notify shell of association changes.
    /// 7. Write cleanup.cmd to temp and launch it for self-deletion.
    /// </summary>
    internal void ExecuteUninstall()
    {
        // Transition to progress state
        ConfirmPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;

        // 1. Remove Add/Remove Programs entry
        UpdateProgress(10, "Removing registry entries...");
        Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);

        // 2. Remove file associations
        UpdateProgress(25, "Removing file associations...");
        RemoveFileAssociations();

        // 3. Remove install path registry key
        UpdateProgress(40, "Cleaning up registry...");
        Registry.CurrentUser.DeleteSubKeyTree(InstallPathRegistryPath, throwOnMissingSubKey: false);

        // 4. Remove shortcuts
        UpdateProgress(55, "Removing shortcuts...");
        RemoveDesktopShortcut();
        RemoveStartMenuShortcut();

        // 5. Optionally delete app data
        if (DeleteAppData)
        {
            UpdateProgress(70, "Deleting app data...");
            DeleteAppDataFolder();
        }

        // 6. Notify shell of association changes
        UpdateProgress(85, "Finalizing...");
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

        // 7. Self-deletion via cleanup script
        UpdateProgress(95, "Preparing cleanup...");
        var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        LaunchCleanupScript(installDir);

        // Transition to completion state
        UpdateProgress(100, "Done");
        ProgressPanel.Visibility = Visibility.Collapsed;
        CompletePanel.Visibility = Visibility.Visible;
    }

    internal static void RemoveFileAssociations(IReadOnlyList<string>? extensions = null, string? progId = null)
    {
        extensions ??= VideoExtensions;
        progId ??= ProgId;
        var progIdPath = $@"Software\Classes\{progId}";

        // Remove per-extension entries only if they point to our ProgID
        foreach (var ext in extensions)
        {
            var extKeyPath = $@"Software\Classes\{ext}";
            using var extKey = Registry.CurrentUser.OpenSubKey(extKeyPath);
            if (extKey is null) continue;

            var value = extKey.GetValue(null) as string;
            if (string.Equals(value, progId, StringComparison.OrdinalIgnoreCase))
            {
                Registry.CurrentUser.DeleteSubKeyTree(extKeyPath, throwOnMissingSubKey: false);
            }
        }

        // Remove ProgID
        Registry.CurrentUser.DeleteSubKeyTree(progIdPath, throwOnMissingSubKey: false);
    }

    internal static void RemoveDesktopShortcut()
    {
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");

        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }

    internal static void RemoveStartMenuShortcut()
    {
        var vidoFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");

        if (Directory.Exists(vidoFolder))
        {
            try
            {
                Directory.Delete(vidoFolder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup — folder may be locked by Explorer or indexing
            }
        }
    }

    internal static void DeleteAppDataFolder(string? appDataPath = null)
    {
        appDataPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Vido");

        if (Directory.Exists(appDataPath))
        {
            try
            {
                Directory.Delete(appDataPath, recursive: true);
            }
            catch
            {
                // Some files may be locked — best effort deletion
            }
        }
    }

    /// <summary>
    /// Generates the cleanup script content that waits for the process to exit
    /// and then deletes the install folder.
    /// </summary>
    internal static string GenerateCleanupScript(string installDir, int pid)
    {
        return $"""
            @echo off
            :wait
            tasklist /fi "PID eq {pid}" 2>nul | find "{pid}" >nul
            if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)
            rd /s /q "{installDir}"
            rd /s /q "%~dp0"
            """;
    }

    /// <summary>
    /// Writes a cleanup script to %TEMP%\Vido\ that waits for this process to exit
    /// and then deletes the install folder and itself.
    /// </summary>
    internal static string LaunchCleanupScript(string installDir)
    {
        var pid = Environment.ProcessId;
        var cleanupDir = Path.Combine(Path.GetTempPath(), "Vido");
        Directory.CreateDirectory(cleanupDir);

        var scriptPath = Path.Combine(cleanupDir, "cleanup.cmd");
        var script = GenerateCleanupScript(installDir, pid);
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        return scriptPath;
    }

    private void UpdateProgress(int percent, string status)
    {
        ProgressBar.Value = percent;
        StatusText.Text = status;

        // Force UI update
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
    }

    // P/Invoke for shell notification
    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(
        uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
