using System.Text.Json;
using Vido.Core.Settings;
using Vido.Services.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for SettingsService JSON persistence.
/// Uses real file I/O in temp directories to validate end-to-end behavior.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vido_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private SettingsService CreateService() => new SettingsService(_tempDir);

    /// <summary>
    /// Verifies that Current has sensible defaults.
    /// </summary>
    [Fact]
    public void Current_HasSensibleDefaults()
    {
        using var svc = CreateService();

        Assert.Equal(0.50, svc.Current.Volume);
        Assert.False(svc.Current.IsMuted);
        Assert.Equal(1.0, svc.Current.PlaybackSpeed);
        Assert.False(svc.Current.LoopPlayback);
        Assert.True(svc.Current.SidebarVisible);
        Assert.Equal(300, svc.Current.SidebarWidth);
        Assert.True(svc.Current.StatusBarVisible);
        Assert.True(svc.Current.BottomPanelVisible);
        Assert.True(svc.Current.RightPanelVisible);
        Assert.False(svc.Current.ShowHiddenFiles);
    }

    /// <summary>
    /// Verifies that Save And Load round trips.
    /// </summary>
    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        using var svc = CreateService();
        svc.Current.Volume = 0.5;
        svc.Current.IsMuted = true;
        svc.Current.PlaybackSpeed = 1.5;

        await svc.SaveAsync();

        // Create a new instance and load — should pick up what was saved
        using var svc2 = CreateService();
        await svc2.LoadAsync();

        Assert.Equal(0.5, svc2.Current.Volume);
        Assert.True(svc2.Current.IsMuted);
        Assert.Equal(1.5, svc2.Current.PlaybackSpeed);

        // Reset to defaults to prevent pollution
        svc.Current.ResetToDefaults();
        await svc.SaveAsync();
    }

    /// <summary>
    /// Verifies that Load Async with missing file keeps defaults.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WithMissingFile_KeepsDefaults()
    {
        // Use a fresh temp subdir with no settings.json
        var emptyDir = Path.Combine(_tempDir, "empty");
        using var svc = new SettingsService(emptyDir);
        var ex = await Record.ExceptionAsync(() => svc.LoadAsync());
        Assert.Null(ex);
        Assert.Equal(0.50, svc.Current.Volume); // still defaults
    }

    /// <summary>
    /// Verifies that Save Async creates directory if missing.
    /// </summary>
    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var newDir = Path.Combine(_tempDir, "new_subdir");
        using var svc = new SettingsService(newDir);
        var ex = await Record.ExceptionAsync(() => svc.SaveAsync());
        Assert.Null(ex);
        Assert.True(Directory.Exists(newDir));
    }

    /// <summary>
    /// Verifies that Queue Save does not throw.
    /// </summary>
    [Fact]
    public void QueueSave_DoesNotThrow()
    {
        using var svc = CreateService();
        var ex = Record.Exception(() => svc.QueueSave());
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that Dispose does not throw.
    /// </summary>
    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var svc = CreateService();
        svc.QueueSave();
        var ex = Record.Exception(() => svc.Dispose());
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that Load Async ensures official registry url when missing.
    /// </summary>
    [Fact]
    public async Task LoadAsync_EnsuresOfficialRegistryUrl_WhenMissing()
    {
        // Write a settings file where the official URL is missing
        var json = JsonSerializer.Serialize(new
        {
            volume = 0.5,
            pluginRegistryUrls = new[] { "file:///C:/custom/registry.json" }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json);

        using var svc = CreateService();
        await svc.LoadAsync();

        // Official URL should be inserted at index 0
        Assert.True(svc.Current.PluginRegistryUrls.Count >= 2);
        Assert.Equal(AppSettings.OfficialRegistryUrl, svc.Current.PluginRegistryUrls[0]);
        Assert.Equal("file:///C:/custom/registry.json", svc.Current.PluginRegistryUrls[1]);
    }

    /// <summary>
    /// Verifies that Load Async does not duplicate official url when present.
    /// </summary>
    [Fact]
    public async Task LoadAsync_DoesNotDuplicateOfficialUrl_WhenPresent()
    {
        // Write a settings file where the official URL is already present
        var json = JsonSerializer.Serialize(new
        {
            volume = 0.5,
            pluginRegistryUrls = new[] { AppSettings.OfficialRegistryUrl, "https://custom.com" }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json);

        using var svc = CreateService();
        await svc.LoadAsync();

        Assert.Equal(2, svc.Current.PluginRegistryUrls.Count);
        Assert.Equal(AppSettings.OfficialRegistryUrl, svc.Current.PluginRegistryUrls[0]);
    }

    /// <summary>
    /// Verifies that multiple rapid QueueSave calls persist the latest values after debounce.
    /// </summary>
    [Fact]
    public async Task QueueSave_MultipleCalls_PersistsLatestValues()
    {
        using var svc = CreateService();

        svc.Current.Volume = 0.25;
        svc.QueueSave();

        svc.Current.Volume = 0.75;
        svc.QueueSave();

        await Task.Delay(900);

        using var reloaded = CreateService();
        await reloaded.LoadAsync();
        Assert.Equal(0.75, reloaded.Current.Volume);
    }

    /// <summary>
    /// Verifies that QueueSave reuses the same timer instance across calls.
    /// </summary>
    [Fact]
    public void QueueSave_TimerReused()
    {
        using var svc = CreateService();

        svc.QueueSave();
        var field = typeof(SettingsService).GetField("_debounceTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);

        var firstTimer = field!.GetValue(svc);
        Assert.NotNull(firstTimer);

        svc.QueueSave();
        var secondTimer = field.GetValue(svc);

        Assert.Same(firstTimer, secondTimer);
    }
}