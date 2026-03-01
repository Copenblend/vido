using Vido.Core.State;
using Vido.Services.State;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for StateService JSON persistence.
/// Uses real file I/O in temp directories to validate end-to-end behavior.
/// </summary>
public sealed class StateServiceTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public StateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vido_StateTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private StateService CreateService() => new StateService(_tempDir);

    /// <summary>
    /// Verifies that Current has sensible defaults.
    /// </summary>
    [Fact]
    public void Current_HasSensibleDefaults()
    {
        using var svc = CreateService();

        Assert.Equal(1280, svc.Current.WindowWidth);
        Assert.Equal(720, svc.Current.WindowHeight);
        Assert.True(double.IsNaN(svc.Current.WindowLeft));
        Assert.True(double.IsNaN(svc.Current.WindowTop));
        Assert.False(svc.Current.IsMaximized);
        Assert.Null(svc.Current.LastOpenFolder);
        Assert.Null(svc.Current.LastVideoPath);
        Assert.Equal(0, svc.Current.LastVideoPosition);
        Assert.Equal("Explorer", svc.Current.ActiveSidebarPanel);
    }

    /// <summary>
    /// Verifies that Save And Load round trips.
    /// </summary>
    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        using var svc = CreateService();
        svc.Current.WindowLeft = 100;
        svc.Current.WindowTop = 200;
        svc.Current.WindowWidth = 1920;
        svc.Current.WindowHeight = 1080;
        svc.Current.IsMaximized = true;
        svc.Current.LastOpenFolder = @"C:\Videos";
        svc.Current.LastVideoPath = @"C:\Videos\test.mp4";
        svc.Current.LastVideoPosition = 42.5;
        svc.Current.ActiveSidebarPanel = "Settings";

        await svc.SaveAsync();

        using var svc2 = CreateService();
        await svc2.LoadAsync();

        Assert.Equal(100, svc2.Current.WindowLeft);
        Assert.Equal(200, svc2.Current.WindowTop);
        Assert.Equal(1920, svc2.Current.WindowWidth);
        Assert.Equal(1080, svc2.Current.WindowHeight);
        Assert.True(svc2.Current.IsMaximized);
        Assert.Equal(@"C:\Videos", svc2.Current.LastOpenFolder);
        Assert.Equal(@"C:\Videos\test.mp4", svc2.Current.LastVideoPath);
        Assert.Equal(42.5, svc2.Current.LastVideoPosition);
        Assert.Equal("Settings", svc2.Current.ActiveSidebarPanel);

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
        var emptyDir = Path.Combine(_tempDir, "empty");
        using var svc = new StateService(emptyDir);
        var ex = await Record.ExceptionAsync(() => svc.LoadAsync());
        Assert.Null(ex);
        Assert.Equal(1280, svc.Current.WindowWidth); // still defaults
    }

    /// <summary>
    /// Verifies that Save Async creates directory if missing.
    /// </summary>
    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var newDir = Path.Combine(_tempDir, "new_subdir");
        using var svc = new StateService(newDir);
        var ex = await Record.ExceptionAsync(() => svc.SaveAsync());
        Assert.Null(ex);
        Assert.True(Directory.Exists(newDir));
    }

    /// <summary>
    /// Verifies that Multiple Saves do not conflict.
    /// </summary>
    [Fact]
    public async Task MultipleSaves_DoNotConflict()
    {
        using var svc = CreateService();
        svc.Current.WindowWidth = 800;

        // Simultaneous saves should not throw thanks to SemaphoreSlim
        var tasks = Enumerable.Range(0, 10).Select(_ => svc.SaveAsync());
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that multiple rapid QueueSave calls persist the latest values after debounce.
    /// </summary>
    [Fact]
    public async Task QueueSave_MultipleCalls_PersistsLatestValues()
    {
        using var svc = CreateService();

        svc.Current.WindowWidth = 900;
        svc.QueueSave();

        svc.Current.WindowWidth = 1440;
        svc.QueueSave();

        await Task.Delay(900);

        using var reloaded = CreateService();
        await reloaded.LoadAsync();
        Assert.Equal(1440, reloaded.Current.WindowWidth);
    }

    /// <summary>
    /// Verifies that QueueSave reuses the same timer instance across calls.
    /// </summary>
    [Fact]
    public void QueueSave_TimerReused()
    {
        using var svc = CreateService();

        svc.QueueSave();
        var field = typeof(StateService).GetField("_debounceTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);

        var firstTimer = field!.GetValue(svc);
        Assert.NotNull(firstTimer);

        svc.QueueSave();
        var secondTimer = field.GetValue(svc);

        Assert.Same(firstTimer, secondTimer);
    }
}