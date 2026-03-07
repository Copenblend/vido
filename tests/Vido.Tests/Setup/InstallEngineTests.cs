using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;

using Microsoft.Win32;

using Vido.Setup.Services;

using Xunit;

namespace Vido.Tests.Setup;

/// <summary>
/// Tests for <see cref="InstallEngine"/> covering file extraction, registry operations,
/// shortcut creation, and file association management.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InstallEngineTests : IDisposable
{
    private readonly InstallEngine _engine = new();
    private readonly string _tempDir;
    private readonly List<string> _registryKeysToCleanup = [];
    private readonly List<string> _filesToCleanup = [];
    private readonly List<string> _dirsToCleanup = [];

    /// <summary>
    /// Test registry root path used to avoid polluting real registry keys during tests.
    /// We use the real uninstall/install paths with the real GUID since the engine
    /// writes to fixed paths. Tests clean up after themselves.
    /// </summary>
    private static string UninstallRegistryPath =>
        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{InstallEngine.UninstallGuid}}}";

    private static string InstallPathRegistryPath => @"Software\Vido\Install";

    private static string ProgIdRegistryPath => $@"Software\Classes\{InstallEngine.ProgId}";

    public InstallEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VidoTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Clean up registry keys created during tests
        foreach (var keyPath in _registryKeysToCleanup)
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }

        // Clean up files
        foreach (var file in _filesToCleanup)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        // Clean up directories
        foreach (var dir in _dirsToCleanup)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── DefaultInstallDir ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that DefaultInstallDir points to %LOCALAPPDATA%\Vido.
    /// </summary>
    [Fact]
    public void DefaultInstallDir_ReturnsLocalAppDataVidoPath()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vido");

        Assert.Equal(expected, InstallEngine.DefaultInstallDir);
    }

    // ── VideoExtensions ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that VideoExtensions contains all 7 supported video formats.
    /// </summary>
    [Fact]
    public void VideoExtensions_ContainsAllSevenFormats()
    {
        var extensions = InstallEngine.VideoExtensions;

        Assert.Equal(7, extensions.Count);
        Assert.Contains(".mp4", extensions);
        Assert.Contains(".avi", extensions);
        Assert.Contains(".mkv", extensions);
        Assert.Contains(".mov", extensions);
        Assert.Contains(".wmv", extensions);
        Assert.Contains(".flv", extensions);
        Assert.Contains(".webm", extensions);
    }

    /// <summary>
    /// Verifies that all extensions start with a dot.
    /// </summary>
    [Fact]
    public void VideoExtensions_AllStartWithDot()
    {
        foreach (var ext in InstallEngine.VideoExtensions)
        {
            Assert.StartsWith(".", ext);
        }
    }

    // ── ExtractPayloadAsync ────────────────────────────────────────────

    /// <summary>
    /// Verifies that ExtractPayloadAsync extracts all files from a zip archive
    /// to the specified directory.
    /// </summary>
    [Fact]
    public async Task ExtractPayloadAsync_ExtractsAllFilesToDirectory()
    {
        var installDir = Path.Combine(_tempDir, "install");
        using var zipStream = CreateTestZip(
            ("file1.txt", "content1"),
            ("subdir/file2.txt", "content2"),
            ("subdir/deep/file3.txt", "content3"));

        await _engine.ExtractPayloadAsync(zipStream, installDir);

        Assert.True(File.Exists(Path.Combine(installDir, "file1.txt")));
        Assert.Equal("content1", File.ReadAllText(Path.Combine(installDir, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(installDir, "subdir", "file2.txt")));
        Assert.Equal("content2", File.ReadAllText(Path.Combine(installDir, "subdir", "file2.txt")));
        Assert.True(File.Exists(Path.Combine(installDir, "subdir", "deep", "file3.txt")));
        Assert.Equal("content3", File.ReadAllText(Path.Combine(installDir, "subdir", "deep", "file3.txt")));
    }

    /// <summary>
    /// Verifies that ExtractPayloadAsync reports progress from 0 to 1.
    /// </summary>
    [Fact]
    public async Task ExtractPayloadAsync_ReportsProgress()
    {
        var installDir = Path.Combine(_tempDir, "install_progress");
        using var zipStream = CreateTestZip(
            ("a.txt", "aaa"),
            ("b.txt", "bbb"),
            ("c.txt", "ccc"));

        var progressValues = new List<(double Progress, string Status)>();
        var progress = new Progress<(double Progress, string Status)>(
            p => progressValues.Add(p));

        await _engine.ExtractPayloadAsync(zipStream, installDir, progress);

        // Allow progress callbacks to fire (Progress<T> uses SynchronizationContext)
        await Task.Delay(100);

        Assert.NotEmpty(progressValues);

        // Last progress value should be 1.0
        var last = progressValues[^1];
        Assert.Equal(1.0, last.Progress, precision: 2);
    }

    /// <summary>
    /// Verifies that ExtractPayloadAsync creates the install directory if it doesn't exist.
    /// </summary>
    [Fact]
    public async Task ExtractPayloadAsync_CreatesInstallDirectory()
    {
        var installDir = Path.Combine(_tempDir, "nonexistent", "nested", "dir");
        using var zipStream = CreateTestZip(("test.txt", "data"));

        await _engine.ExtractPayloadAsync(zipStream, installDir);

        Assert.True(Directory.Exists(installDir));
        Assert.True(File.Exists(Path.Combine(installDir, "test.txt")));
    }

    /// <summary>
    /// Verifies that ExtractPayloadAsync overwrites existing files.
    /// </summary>
    [Fact]
    public async Task ExtractPayloadAsync_OverwritesExistingFiles()
    {
        var installDir = Path.Combine(_tempDir, "install_overwrite");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "file.txt"), "old content");

        using var zipStream = CreateTestZip(("file.txt", "new content"));

        await _engine.ExtractPayloadAsync(zipStream, installDir);

        Assert.Equal("new content", File.ReadAllText(Path.Combine(installDir, "file.txt")));
    }

    /// <summary>
    /// Verifies that ExtractPayloadAsync respects cancellation.
    /// </summary>
    [Fact]
    public async Task ExtractPayloadAsync_ThrowsOnCancellation()
    {
        var installDir = Path.Combine(_tempDir, "install_cancel");
        using var zipStream = CreateTestZip(("file.txt", "data"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _engine.ExtractPayloadAsync(zipStream, installDir, cancellationToken: cts.Token));
    }

    /// <summary>
    /// Verifies that ExtractPayloadAsync protects against zip slip attacks
    /// by ignoring entries that would escape the install directory.
    /// </summary>
    [Fact]
    public async Task ExtractPayloadAsync_SkipsEntriesOutsideInstallDir()
    {
        var installDir = Path.Combine(_tempDir, "install_zipslip");

        // Create a zip with a malicious path entry
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Normal entry
            var normalEntry = archive.CreateEntry("safe.txt");
            using (var writer = new StreamWriter(normalEntry.Open()))
                writer.Write("safe content");

            // Malicious entry attempting directory traversal
            var maliciousEntry = archive.CreateEntry("../../../evil.txt");
            using (var writer = new StreamWriter(maliciousEntry.Open()))
                writer.Write("evil content");
        }

        ms.Position = 0;
        await _engine.ExtractPayloadAsync(ms, installDir);

        Assert.True(File.Exists(Path.Combine(installDir, "safe.txt")));
        // The malicious entry should not have been extracted
        Assert.False(File.Exists(Path.Combine(installDir, "..", "..", "..", "evil.txt")));
    }

    // ── RegisterUninstallEntry / DetectExistingInstall ──────────────────

    /// <summary>
    /// Verifies that DetectExistingInstall returns null when no installation exists.
    /// </summary>
    [Fact]
    public void DetectExistingInstall_ReturnsNull_WhenNoRegistryKey()
    {
        // Ensure the key doesn't exist
        Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);

        var result = _engine.DetectExistingInstall();

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that RegisterUninstallEntry creates the correct registry entries
    /// and DetectExistingInstall can read the version back.
    /// </summary>
    [Fact]
    public void RegisterUninstallEntry_And_DetectExistingInstall_RoundTrip()
    {
        _registryKeysToCleanup.Add(UninstallRegistryPath);

        var installDir = Path.Combine(_tempDir, "install_reg");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        _engine.RegisterUninstallEntry(installDir, "1.2.3");

        var version = _engine.DetectExistingInstall();
        Assert.Equal("1.2.3", version);

        // Verify other registry values
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
        Assert.NotNull(key);
        Assert.Equal("Vido", key.GetValue("DisplayName"));
        Assert.Equal("Vido", key.GetValue("Publisher"));
        Assert.Equal(installDir, key.GetValue("InstallLocation"));
        Assert.Equal(1, key.GetValue("NoModify"));
        Assert.Equal(1, key.GetValue("NoRepair"));

        var uninstallString = key.GetValue("UninstallString") as string;
        Assert.NotNull(uninstallString);
        Assert.Contains("--uninstall", uninstallString);
    }

    /// <summary>
    /// Verifies that RemoveUninstallEntry removes the registry key.
    /// </summary>
    [Fact]
    public void RemoveUninstallEntry_RemovesRegistryKey()
    {
        _registryKeysToCleanup.Add(UninstallRegistryPath);

        var installDir = Path.Combine(_tempDir, "install_remove");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        _engine.RegisterUninstallEntry(installDir, "1.0.0");
        Assert.NotNull(_engine.DetectExistingInstall());

        _engine.RemoveUninstallEntry();
        Assert.Null(_engine.DetectExistingInstall());
    }

    // ── RegisterInstallPath / RemoveInstallPath ────────────────────────

    /// <summary>
    /// Verifies that RegisterInstallPath writes the install directory
    /// to the registry and RemoveInstallPath cleans it up.
    /// </summary>
    [Fact]
    public void RegisterInstallPath_WritesPathToRegistry()
    {
        _registryKeysToCleanup.Add(InstallPathRegistryPath);
        // Also clean up the parent key
        _registryKeysToCleanup.Add(@"Software\Vido");

        var installDir = @"C:\TestInstall\Vido";

        _engine.RegisterInstallPath(installDir);

        using var key = Registry.CurrentUser.OpenSubKey(InstallPathRegistryPath);
        Assert.NotNull(key);
        Assert.Equal(installDir, key.GetValue("Path"));
    }

    /// <summary>
    /// Verifies that RemoveInstallPath removes the registry key.
    /// </summary>
    [Fact]
    public void RemoveInstallPath_RemovesRegistryKey()
    {
        _registryKeysToCleanup.Add(InstallPathRegistryPath);
        _registryKeysToCleanup.Add(@"Software\Vido");

        _engine.RegisterInstallPath(@"C:\Test\Vido");
        _engine.RemoveInstallPath();

        using var key = Registry.CurrentUser.OpenSubKey(InstallPathRegistryPath);
        Assert.Null(key);
    }

    // ── RegisterFileAssociations / RemoveFileAssociations ───────────────

    /// <summary>
    /// Verifies that RegisterFileAssociations creates the ProgID and per-extension
    /// registry entries with correct values.
    /// </summary>
    [Fact]
    public void RegisterFileAssociations_CreatesCorrectRegistryEntries()
    {
        // Use a test extension to avoid interfering with real file associations
        var testExtensions = new List<string> { ".vidotest1", ".vidotest2" };
        _registryKeysToCleanup.Add(ProgIdRegistryPath);
        foreach (var ext in testExtensions)
            _registryKeysToCleanup.Add($@"Software\Classes\{ext}");

        var installDir = Path.Combine(_tempDir, "install_assoc");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        _engine.RegisterFileAssociations(installDir, testExtensions);

        // Verify ProgID
        using var progIdKey = Registry.CurrentUser.OpenSubKey(ProgIdRegistryPath);
        Assert.NotNull(progIdKey);
        Assert.Equal("Vido Video File", progIdKey.GetValue(null));

        // Verify DefaultIcon
        using var iconKey = Registry.CurrentUser.OpenSubKey($@"{ProgIdRegistryPath}\DefaultIcon");
        Assert.NotNull(iconKey);
        var iconValue = iconKey.GetValue(null) as string;
        Assert.NotNull(iconValue);
        Assert.Contains("Vido.exe", iconValue);

        // Verify shell\open\command
        using var commandKey = Registry.CurrentUser.OpenSubKey(
            $@"{ProgIdRegistryPath}\shell\open\command");
        Assert.NotNull(commandKey);
        var commandValue = commandKey.GetValue(null) as string;
        Assert.NotNull(commandValue);
        Assert.Contains("Vido.exe", commandValue);
        Assert.Contains("%1", commandValue);

        // Verify extension entries
        foreach (var ext in testExtensions)
        {
            using var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}");
            Assert.NotNull(extKey);
            Assert.Equal(InstallEngine.ProgId, extKey.GetValue(null));
        }
    }

    /// <summary>
    /// Verifies that RemoveFileAssociations only removes extensions whose
    /// value is "Vido.VideoFile", leaving other applications' associations intact.
    /// </summary>
    [Fact]
    public void RemoveFileAssociations_OnlyRemovesVidoOwnedEntries()
    {
        var ownedExt = ".vidoowned";
        var foreignExt = ".vidoforeign";
        var testExtensions = new List<string> { ownedExt, foreignExt };

        _registryKeysToCleanup.Add(ProgIdRegistryPath);
        _registryKeysToCleanup.Add($@"Software\Classes\{ownedExt}");
        _registryKeysToCleanup.Add($@"Software\Classes\{foreignExt}");

        var installDir = Path.Combine(_tempDir, "install_remove_assoc");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        // Register Vido's file associations
        _engine.RegisterFileAssociations(installDir, [ownedExt]);

        // Manually set foreign extension to a different ProgID
        using (var foreignKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{foreignExt}"))
        {
            foreignKey.SetValue(null, "OtherApp.VideoFile");
        }

        // Remove using both extensions — should only remove the owned one
        _engine.RemoveFileAssociations(testExtensions);

        // Owned extension should be removed
        using var ownedKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ownedExt}");
        Assert.Null(ownedKey);

        // Foreign extension should still exist
        using var foreignKeyCheck = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{foreignExt}");
        Assert.NotNull(foreignKeyCheck);
        Assert.Equal("OtherApp.VideoFile", foreignKeyCheck.GetValue(null));
    }

    /// <summary>
    /// Verifies that RemoveFileAssociations removes the ProgID.
    /// </summary>
    [Fact]
    public void RemoveFileAssociations_RemovesProgId()
    {
        _registryKeysToCleanup.Add(ProgIdRegistryPath);

        var installDir = Path.Combine(_tempDir, "install_progid");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        _engine.RegisterFileAssociations(installDir, [".vidoprogid"]);
        _registryKeysToCleanup.Add(@"Software\Classes\.vidoprogid");

        _engine.RemoveFileAssociations([".vidoprogid"]);

        using var key = Registry.CurrentUser.OpenSubKey(ProgIdRegistryPath);
        Assert.Null(key);
    }

    // ── Shortcut creation ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that CreateDesktopShortcut creates a .lnk file on the desktop.
    /// </summary>
    [Fact]
    public void CreateDesktopShortcut_CreatesLnkFile()
    {
        var installDir = Path.Combine(_tempDir, "install_shortcut");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");
        _filesToCleanup.Add(shortcutPath);

        _engine.CreateDesktopShortcut(installDir);

        Assert.True(File.Exists(shortcutPath));
    }

    /// <summary>
    /// Verifies that CreateStartMenuShortcut creates a .lnk file in the Start Menu.
    /// </summary>
    [Fact]
    public void CreateStartMenuShortcut_CreatesLnkFile()
    {
        var installDir = Path.Combine(_tempDir, "install_startmenu");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "Vido.exe"), "dummy");

        var vidoFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");
        _dirsToCleanup.Add(vidoFolder);

        _engine.CreateStartMenuShortcut(installDir);

        Assert.True(File.Exists(Path.Combine(vidoFolder, "Vido.lnk")));
    }

    /// <summary>
    /// Verifies that RemoveDesktopShortcut deletes the shortcut file.
    /// </summary>
    [Fact]
    public void RemoveDesktopShortcut_DeletesShortcutFile()
    {
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");

        // Create a dummy shortcut file
        File.WriteAllText(shortcutPath, "dummy");
        _filesToCleanup.Add(shortcutPath);

        _engine.RemoveDesktopShortcut();

        Assert.False(File.Exists(shortcutPath));
    }

    /// <summary>
    /// Verifies that RemoveStartMenuShortcut deletes the shortcut folder.
    /// </summary>
    [Fact]
    public void RemoveStartMenuShortcut_DeletesShortcutFolder()
    {
        var vidoFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");
        Directory.CreateDirectory(vidoFolder);
        File.WriteAllText(Path.Combine(vidoFolder, "Vido.lnk"), "dummy");
        _dirsToCleanup.Add(vidoFolder);

        _engine.RemoveStartMenuShortcut();

        Assert.False(Directory.Exists(vidoFolder));
    }

    /// <summary>
    /// Verifies that RemoveDesktopShortcut does not throw when the shortcut doesn't exist.
    /// </summary>
    [Fact]
    public void RemoveDesktopShortcut_DoesNotThrow_WhenShortcutMissing()
    {
        var exception = Record.Exception(() => _engine.RemoveDesktopShortcut());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that RemoveStartMenuShortcut does not throw when the folder doesn't exist.
    /// </summary>
    [Fact]
    public void RemoveStartMenuShortcut_DoesNotThrow_WhenFolderMissing()
    {
        var exception = Record.Exception(() => _engine.RemoveStartMenuShortcut());
        Assert.Null(exception);
    }

    // ── UninstallGuid ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UninstallGuid is a valid GUID string.
    /// </summary>
    [Fact]
    public void UninstallGuid_IsValidGuid()
    {
        Assert.True(Guid.TryParse(InstallEngine.UninstallGuid, out _));
    }

    // ── ProgId ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that ProgId is "Vido.VideoFile".
    /// </summary>
    [Fact]
    public void ProgId_IsVidoVideoFile()
    {
        Assert.Equal("Vido.VideoFile", InstallEngine.ProgId);
    }

    // ── Helper ─────────────────────────────────────────────────────────

    private static MemoryStream CreateTestZip(params (string Path, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        ms.Position = 0;
        return ms;
    }
}
