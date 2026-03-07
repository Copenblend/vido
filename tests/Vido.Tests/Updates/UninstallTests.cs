using System.IO;
using System.Runtime.Versioning;

using Microsoft.Win32;

using Vido.Setup.Services;
using Vido.Views.Updates;

using Xunit;

namespace Vido.Tests.Updates;

/// <summary>
/// Tests for <see cref="UninstallDialog"/> covering registry cleanup,
/// file association removal, cleanup script generation, and app data deletion.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UninstallTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _registryKeysToCleanup = [];
    private readonly List<string> _filesToCleanup = [];
    private readonly List<string> _dirsToCleanup = [];

    private const string TestProgId = "Vido.UninstallTest";
    private static string TestProgIdRegistryPath => $@"Software\Classes\{TestProgId}";

    public UninstallTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VidoUninstallTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var keyPath in _registryKeysToCleanup)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
            catch (IOException) { /* key still being released */ }
        }

        foreach (var file in _filesToCleanup)
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (IOException) { /* file locked momentarily */ }
        }

        foreach (var dir in _dirsToCleanup)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* directory locked momentarily */ }
        }

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { /* temp dir locked momentarily */ }
    }

    // ── Constants ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UninstallDialog constants match InstallEngine constants.
    /// </summary>
    [Fact]
    public void Constants_MatchInstallEngine()
    {
        Assert.Equal(UninstallDialog.UninstallGuid, InstallEngine.UninstallGuid);
        Assert.Equal(UninstallDialog.ProgId, InstallEngine.ProgId);
        Assert.Equal(UninstallDialog.VideoExtensions.Length, InstallEngine.VideoExtensions.Count);
        foreach (var ext in InstallEngine.VideoExtensions)
            Assert.Contains(ext, UninstallDialog.VideoExtensions);
    }

    // ── RemoveFileAssociations ─────────────────────────────────────────

    /// <summary>
    /// Verifies that RemoveFileAssociations only removes extensions whose value
    /// is "Vido.VideoFile", leaving other applications' associations intact.
    /// </summary>
    [Fact]
    public void RemoveFileAssociations_OnlyRemovesVidoOwnedExtensions()
    {
        var ownedExt = ".vidouninsttest1";
        var foreignExt = ".vidouninsttest2";
        var testExtensions = new List<string> { ownedExt, foreignExt };

        _registryKeysToCleanup.Add(TestProgIdRegistryPath);
        _registryKeysToCleanup.Add($@"Software\Classes\{ownedExt}");
        _registryKeysToCleanup.Add($@"Software\Classes\{foreignExt}");

        // Set up owned extension pointing to test ProgID
        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ownedExt}"))
            key.SetValue(null, TestProgId);

        // Set up foreign extension pointing to another ProgID
        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{foreignExt}"))
            key.SetValue(null, "OtherApp.VideoFile");

        // Create test ProgID key
        using (var key = Registry.CurrentUser.CreateSubKey(TestProgIdRegistryPath))
            key.SetValue(null, "Vido Video File");

        UninstallDialog.RemoveFileAssociations(testExtensions, TestProgId);

        // Owned extension should be removed
        using var ownedKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ownedExt}");
        Assert.Null(ownedKey);

        // Foreign extension should still exist
        using var foreignKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{foreignExt}");
        Assert.NotNull(foreignKey);
        Assert.Equal("OtherApp.VideoFile", foreignKey.GetValue(null));
    }

    /// <summary>
    /// Verifies that RemoveFileAssociations removes the ProgID key.
    /// </summary>
    [Fact]
    public void RemoveFileAssociations_RemovesProgId()
    {
        _registryKeysToCleanup.Add(TestProgIdRegistryPath);

        using (var key = Registry.CurrentUser.CreateSubKey(TestProgIdRegistryPath))
            key.SetValue(null, "Vido Video File");

        UninstallDialog.RemoveFileAssociations([], TestProgId);

        using var progIdKey = Registry.CurrentUser.OpenSubKey(TestProgIdRegistryPath);
        Assert.Null(progIdKey);
    }

    /// <summary>
    /// Verifies that RemoveFileAssociations does not throw when extension keys
    /// do not exist in the registry.
    /// </summary>
    [Fact]
    public void RemoveFileAssociations_DoesNotThrow_WhenKeysDoNotExist()
    {
        // Ensure test extensions don't exist
        var testExtensions = new List<string> { ".vidononexist1", ".vidononexist2" };

        var exception = Record.Exception(() => UninstallDialog.RemoveFileAssociations(testExtensions, TestProgId));

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that RemoveFileAssociations uses the default video extensions
    /// when no extensions parameter is provided.
    /// </summary>
    [Fact]
    public void RemoveFileAssociations_UsesDefaultExtensions_WhenNull()
    {
        // This test verifies the default parameter — we just ensure it doesn't throw.
        // We don't want to create real .mp4/.avi keys, so we ensure the method
        // handles missing keys gracefully (which it does via null check).
        var exception = Record.Exception(() => UninstallDialog.RemoveFileAssociations(null, TestProgId));

        Assert.Null(exception);
    }

    // ── GenerateCleanupScript ──────────────────────────────────────────

    /// <summary>
    /// Verifies that GenerateCleanupScript includes the correct PID.
    /// </summary>
    [Fact]
    public void GenerateCleanupScript_ContainsCorrectPid()
    {
        var script = UninstallDialog.GenerateCleanupScript(@"C:\Test\Vido", 12345);

        Assert.Contains("12345", script);
        Assert.Contains("PID eq 12345", script);
    }

    /// <summary>
    /// Verifies that GenerateCleanupScript includes the correct install directory.
    /// </summary>
    [Fact]
    public void GenerateCleanupScript_ContainsCorrectInstallDir()
    {
        var installDir = @"C:\Users\Test\AppData\Local\Vido";
        var script = UninstallDialog.GenerateCleanupScript(installDir, 99999);

        Assert.Contains($"rd /s /q \"{installDir}\"", script);
    }

    /// <summary>
    /// Verifies that GenerateCleanupScript produces a valid batch script structure.
    /// </summary>
    [Fact]
    public void GenerateCleanupScript_HasCorrectBatchStructure()
    {
        var script = UninstallDialog.GenerateCleanupScript(@"C:\Vido", 1);

        Assert.Contains("@echo off", script);
        Assert.Contains(":wait", script);
        Assert.Contains("tasklist", script);
        Assert.Contains("goto wait", script);
        Assert.Contains("rd /s /q", script);
    }

    // ── DeleteAppDataFolder ────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteAppDataFolder deletes an existing directory.
    /// </summary>
    [Fact]
    public void DeleteAppDataFolder_DeletesExistingDirectory()
    {
        var testAppData = Path.Combine(_tempDir, "AppData");
        Directory.CreateDirectory(testAppData);
        File.WriteAllText(Path.Combine(testAppData, "settings.json"), "{}");

        UninstallDialog.DeleteAppDataFolder(testAppData);

        Assert.False(Directory.Exists(testAppData));
    }

    /// <summary>
    /// Verifies that DeleteAppDataFolder does not throw when the directory does not exist.
    /// </summary>
    [Fact]
    public void DeleteAppDataFolder_DoesNotThrow_WhenDirectoryMissing()
    {
        var nonExistentPath = Path.Combine(_tempDir, "NonExistent");

        var exception = Record.Exception(() => UninstallDialog.DeleteAppDataFolder(nonExistentPath));

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that DeleteAppDataFolder recursively deletes subdirectories.
    /// </summary>
    [Fact]
    public void DeleteAppDataFolder_DeletesRecursively()
    {
        var testAppData = Path.Combine(_tempDir, "AppDataRecursive");
        var subDir = Path.Combine(testAppData, "SubFolder", "Deep");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "data.txt"), "test");

        UninstallDialog.DeleteAppDataFolder(testAppData);

        Assert.False(Directory.Exists(testAppData));
    }

    // ── RemoveDesktopShortcut ──────────────────────────────────────────

    /// <summary>
    /// Verifies that RemoveDesktopShortcut removes an existing Vido.lnk from the desktop.
    /// </summary>
    [Fact]
    public void RemoveDesktopShortcut_RemovesExistingShortcut()
    {
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");
        _filesToCleanup.Add(shortcutPath);

        // Create a dummy shortcut file
        File.WriteAllText(shortcutPath, "dummy");

        UninstallDialog.RemoveDesktopShortcut();

        Assert.False(File.Exists(shortcutPath));
    }

    /// <summary>
    /// Verifies that RemoveDesktopShortcut does not throw when shortcut does not exist.
    /// </summary>
    [Fact]
    public void RemoveDesktopShortcut_DoesNotThrow_WhenShortcutMissing()
    {
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");

        // Ensure it doesn't exist
        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);

        var exception = Record.Exception(() => UninstallDialog.RemoveDesktopShortcut());

        Assert.Null(exception);
    }

    // ── RemoveStartMenuShortcut ────────────────────────────────────────

    /// <summary>
    /// Verifies that RemoveStartMenuShortcut removes the Vido folder from Start Menu.
    /// </summary>
    [Fact]
    public void RemoveStartMenuShortcut_RemovesExistingFolder()
    {
        var vidoFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");
        _dirsToCleanup.Add(vidoFolder);

        Directory.CreateDirectory(vidoFolder);
        File.WriteAllText(Path.Combine(vidoFolder, "Vido.lnk"), "dummy");

        UninstallDialog.RemoveStartMenuShortcut();

        Assert.False(Directory.Exists(vidoFolder));
    }

    /// <summary>
    /// Verifies that RemoveStartMenuShortcut does not throw when folder does not exist.
    /// </summary>
    [Fact]
    public void RemoveStartMenuShortcut_DoesNotThrow_WhenFolderMissing()
    {
        var vidoFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");

        // Ensure it doesn't exist
        if (Directory.Exists(vidoFolder))
            Directory.Delete(vidoFolder, recursive: true);

        var exception = Record.Exception(() => UninstallDialog.RemoveStartMenuShortcut());

        Assert.Null(exception);
    }
}
