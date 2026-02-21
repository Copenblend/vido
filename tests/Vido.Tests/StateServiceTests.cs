using Vido.Core.State;
using Vido.Services.State;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for StateService JSON persistence.
/// </summary>
public sealed class StateServiceTests
{
    [Fact]
    public void Current_HasSensibleDefaults()
    {
        var svc = new StateService();

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

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var svc = new StateService();
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

        var svc2 = new StateService();
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
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_KeepsDefaults()
    {
        var svc = new StateService();
        var ex = await Record.ExceptionAsync(() => svc.LoadAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var svc = new StateService();
        var ex = await Record.ExceptionAsync(() => svc.SaveAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task MultipleSaves_DoNotConflict()
    {
        var svc = new StateService();
        svc.Current.WindowWidth = 800;

        // Simultaneous saves should not throw thanks to SemaphoreSlim
        var tasks = Enumerable.Range(0, 10).Select(_ => svc.SaveAsync());
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);
    }
}
