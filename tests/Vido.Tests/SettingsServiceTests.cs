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

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vido_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private SettingsService CreateService() => new SettingsService(_tempDir);

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

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var newDir = Path.Combine(_tempDir, "new_subdir");
        using var svc = new SettingsService(newDir);
        var ex = await Record.ExceptionAsync(() => svc.SaveAsync());
        Assert.Null(ex);
        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void QueueSave_DoesNotThrow()
    {
        using var svc = CreateService();
        var ex = Record.Exception(() => svc.QueueSave());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var svc = CreateService();
        svc.QueueSave();
        var ex = Record.Exception(() => svc.Dispose());
        Assert.Null(ex);
    }
}
