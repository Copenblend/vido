using NSubstitute;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for vi-016: State persistence — verifies that settings and state
/// are saved when properties change and restored on construction.
/// </summary>
public sealed class StatePersistenceTests
{
    // ── AppState.AddRecentFile ──

    [Fact]
    public void AddRecentFile_AddsToFront()
    {
        var state = new AppState();
        state.AddRecentFile(@"C:\a.mp4");
        state.AddRecentFile(@"C:\b.mp4");

        Assert.Equal(2, state.RecentFiles.Count);
        Assert.Equal(@"C:\b.mp4", state.RecentFiles[0]);
        Assert.Equal(@"C:\a.mp4", state.RecentFiles[1]);
    }

    [Fact]
    public void AddRecentFile_MovesDuplicateToFront()
    {
        var state = new AppState();
        state.AddRecentFile(@"C:\a.mp4");
        state.AddRecentFile(@"C:\b.mp4");
        state.AddRecentFile(@"C:\a.mp4");

        Assert.Equal(2, state.RecentFiles.Count);
        Assert.Equal(@"C:\a.mp4", state.RecentFiles[0]);
        Assert.Equal(@"C:\b.mp4", state.RecentFiles[1]);
    }

    [Fact]
    public void AddRecentFile_TrimsToMax()
    {
        var state = new AppState();
        for (int i = 0; i < 15; i++)
            state.AddRecentFile($@"C:\video{i}.mp4");

        Assert.Equal(AppState.MaxRecentFiles, state.RecentFiles.Count);
        Assert.Equal(@"C:\video14.mp4", state.RecentFiles[0]);
    }

    [Fact]
    public void AddRecentFile_CaseInsensitiveDedupe()
    {
        var state = new AppState();
        state.AddRecentFile(@"C:\Video.MP4");
        state.AddRecentFile(@"c:\video.mp4");

        Assert.Single(state.RecentFiles);
        Assert.Equal(@"c:\video.mp4", state.RecentFiles[0]);
    }

    // ── MainWindowViewModel persists panel visibility ──

    [Fact]
    public void MainWindowVM_ToggleBottomPanel_SavesSettings()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings { BottomPanelVisible = true });
        var vm = new MainWindowViewModel(settingsService);

        vm.ToggleBottomPanel(); // false
        settingsService.Received().QueueSave();
        Assert.False(settingsService.Current.BottomPanelVisible);
    }

    [Fact]
    public void MainWindowVM_ToggleRightPanel_SavesSettings()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings { RightPanelVisible = true });
        var vm = new MainWindowViewModel(settingsService);

        vm.ToggleRightPanel(); // false
        settingsService.Received().QueueSave();
        Assert.False(settingsService.Current.RightPanelVisible);
    }

    [Fact]
    public void MainWindowVM_ToggleStatusBar_SavesSettings()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings());
        var vm = new MainWindowViewModel(settingsService);

        vm.ToggleStatusBar(); // false
        settingsService.Received().QueueSave();
        Assert.False(settingsService.Current.StatusBarVisible);
    }

    [Fact]
    public void MainWindowVM_RestoresPanelState_FromSettings()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings
        {
            BottomPanelVisible = false,
            RightPanelVisible = false,
            StatusBarVisible = false
        });

        var vm = new MainWindowViewModel(settingsService);

        Assert.False(vm.IsBottomPanelVisible);
        Assert.False(vm.IsRightPanelVisible);
        Assert.False(vm.IsStatusBarVisible);
    }

    // ── ActivityBarViewModel persists sidebar visibility ──

    [Fact]
    public void ActivityBarVM_RestoresSidebarVisibility()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings { SidebarVisible = false });

        var vm = new ActivityBarViewModel(settingsService);

        Assert.False(vm.IsSidebarVisible);
    }

    [Fact]
    public void ActivityBarVM_SidebarToggle_SavesSettings()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings { SidebarVisible = true });
        var vm = new ActivityBarViewModel(settingsService);

        vm.IsSidebarVisible = false;
        settingsService.Received().QueueSave();
        Assert.False(settingsService.Current.SidebarVisible);
    }

    [Fact]
    public void ActivityBarVM_SetActivePanel_DoesNotToggleVisibility()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings());
        var vm = new ActivityBarViewModel(settingsService);

        vm.SetActivePanel(Core.Layout.SidebarPanelKind.Explorer);
        Assert.True(vm.IsSidebarVisible); // unchanged
    }

    // ── State round-trip with RecentFiles ──

    [Fact]
    public async Task StateService_SaveAndLoad_RoundTripsRecentFiles()
    {
        var svc = new Vido.Services.State.StateService();
        svc.Current.AddRecentFile(@"C:\a.mp4");
        svc.Current.AddRecentFile(@"C:\b.mp4");

        await svc.SaveAsync();

        var svc2 = new Vido.Services.State.StateService();
        await svc2.LoadAsync();

        Assert.Equal(2, svc2.Current.RecentFiles.Count);
        Assert.Equal(@"C:\b.mp4", svc2.Current.RecentFiles[0]);
        Assert.Equal(@"C:\a.mp4", svc2.Current.RecentFiles[1]);
    }

    // ── Settings round-trip ──

    [Fact]
    public async Task SettingsService_SaveAndLoad_RoundTrips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "VidoTest_" + Guid.NewGuid().ToString("N"));
        try
        {
        var svc = new Vido.Services.Settings.SettingsService(tempDir);
        svc.Current.Volume = 0.42;
        svc.Current.IsMuted = true;
        svc.Current.LoopPlayback = true;
        svc.Current.SidebarVisible = false;
        svc.Current.SidebarWidth = 250;
        svc.Current.BottomPanelVisible = true;
        svc.Current.BottomPanelCollapsed = false;
        svc.Current.BottomPanelHeight = 150;
        svc.Current.RightPanelVisible = true;
        svc.Current.RightPanelCollapsed = false;
        svc.Current.RightPanelWidth = 350;
        svc.Current.StatusBarVisible = false;
        svc.Current.ShowHiddenFiles = true;

        await svc.SaveAsync();

        var svc2 = new Vido.Services.Settings.SettingsService(tempDir);
        await svc2.LoadAsync();

        Assert.Equal(0.42, svc2.Current.Volume);
        Assert.True(svc2.Current.IsMuted);
        Assert.True(svc2.Current.LoopPlayback);
        Assert.False(svc2.Current.SidebarVisible);
        Assert.Equal(250, svc2.Current.SidebarWidth);
        Assert.True(svc2.Current.BottomPanelVisible);
        Assert.False(svc2.Current.BottomPanelCollapsed);
        Assert.Equal(150, svc2.Current.BottomPanelHeight);
        Assert.True(svc2.Current.RightPanelVisible);
        Assert.False(svc2.Current.RightPanelCollapsed);
        Assert.Equal(350, svc2.Current.RightPanelWidth);
        Assert.False(svc2.Current.StatusBarVisible);
        Assert.True(svc2.Current.ShowHiddenFiles);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
