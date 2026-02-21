using Vido.Core.Settings;
using Vido.Core.State;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for AppSettings.ResetToDefaults() and AppState.ResetToDefaults().
/// Ensures that mutated settings/state are fully reverted to default values.
/// </summary>
public sealed class ResetToDefaultsTests
{
    // ── AppSettings ──

    [Fact]
    public void AppSettings_ResetToDefaults_RestoresAllProperties()
    {
        var settings = new AppSettings
        {
            Volume = 0.99,
            IsMuted = true,
            PlaybackSpeed = 2.0,
            LoopPlayback = true,
            SidebarVisible = false,
            SidebarWidth = 500,
            StatusBarVisible = false,
            BottomPanelVisible = false,
            BottomPanelCollapsed = true,
            BottomPanelHeight = 999,
            RightPanelVisible = false,
            RightPanelCollapsed = true,
            RightPanelWidth = 999,
            ShowHiddenFiles = true
        };

        settings.ResetToDefaults();

        Assert.Equal(0.50, settings.Volume);
        Assert.False(settings.IsMuted);
        Assert.Equal(1.0, settings.PlaybackSpeed);
        Assert.False(settings.LoopPlayback);
        Assert.True(settings.SidebarVisible);
        Assert.Equal(300, settings.SidebarWidth);
        Assert.True(settings.StatusBarVisible);
        Assert.True(settings.BottomPanelVisible);
        Assert.False(settings.BottomPanelCollapsed);
        Assert.Equal(200, settings.BottomPanelHeight);
        Assert.True(settings.RightPanelVisible);
        Assert.False(settings.RightPanelCollapsed);
        Assert.Equal(300, settings.RightPanelWidth);
        Assert.False(settings.ShowHiddenFiles);
    }

    [Fact]
    public void AppSettings_ResetToDefaults_MatchesFreshInstance()
    {
        var mutated = new AppSettings
        {
            Volume = 0.1,
            IsMuted = true,
            PlaybackSpeed = 0.5,
            LoopPlayback = true,
            ShowHiddenFiles = true
        };

        mutated.ResetToDefaults();
        var fresh = new AppSettings();

        Assert.Equal(fresh.Volume, mutated.Volume);
        Assert.Equal(fresh.IsMuted, mutated.IsMuted);
        Assert.Equal(fresh.PlaybackSpeed, mutated.PlaybackSpeed);
        Assert.Equal(fresh.LoopPlayback, mutated.LoopPlayback);
        Assert.Equal(fresh.SidebarVisible, mutated.SidebarVisible);
        Assert.Equal(fresh.SidebarWidth, mutated.SidebarWidth);
        Assert.Equal(fresh.StatusBarVisible, mutated.StatusBarVisible);
        Assert.Equal(fresh.BottomPanelVisible, mutated.BottomPanelVisible);
        Assert.Equal(fresh.BottomPanelCollapsed, mutated.BottomPanelCollapsed);
        Assert.Equal(fresh.BottomPanelHeight, mutated.BottomPanelHeight);
        Assert.Equal(fresh.RightPanelVisible, mutated.RightPanelVisible);
        Assert.Equal(fresh.RightPanelCollapsed, mutated.RightPanelCollapsed);
        Assert.Equal(fresh.RightPanelWidth, mutated.RightPanelWidth);
        Assert.Equal(fresh.ShowHiddenFiles, mutated.ShowHiddenFiles);
    }

    [Fact]
    public void AppSettings_ResetToDefaults_IsIdempotent()
    {
        var settings = new AppSettings();
        settings.ResetToDefaults();
        settings.ResetToDefaults();

        Assert.Equal(0.50, settings.Volume);
        Assert.Equal(1.0, settings.PlaybackSpeed);
        Assert.False(settings.IsMuted);
    }

    // ── AppState ──

    [Fact]
    public void AppState_ResetToDefaults_RestoresAllProperties()
    {
        var state = new AppState
        {
            WindowLeft = 100,
            WindowTop = 200,
            WindowWidth = 1920,
            WindowHeight = 1080,
            IsMaximized = true,
            LastOpenFolder = @"C:\Videos",
            LastVideoPath = @"C:\Videos\test.mp4",
            LastVideoPosition = 42.5,
            ActiveSidebarPanel = "Settings",
            HiddenFiles = [@"C:\hidden.mp4"],
            RecentFiles = [@"C:\recent.mp4"]
        };

        state.ResetToDefaults();

        Assert.True(double.IsNaN(state.WindowLeft));
        Assert.True(double.IsNaN(state.WindowTop));
        Assert.Equal(1280, state.WindowWidth);
        Assert.Equal(720, state.WindowHeight);
        Assert.False(state.IsMaximized);
        Assert.Null(state.LastOpenFolder);
        Assert.Null(state.LastVideoPath);
        Assert.Equal(0, state.LastVideoPosition);
        Assert.Equal("Explorer", state.ActiveSidebarPanel);
        Assert.Empty(state.HiddenFiles);
        Assert.Empty(state.RecentFiles);
    }

    [Fact]
    public void AppState_ResetToDefaults_MatchesFreshInstance()
    {
        var mutated = new AppState();
        mutated.AddRecentFile(@"C:\a.mp4");
        mutated.AddRecentFile(@"C:\b.mp4");
        mutated.WindowWidth = 800;
        mutated.IsMaximized = true;
        mutated.ActiveSidebarPanel = "Settings";

        mutated.ResetToDefaults();
        var fresh = new AppState();

        Assert.Equal(fresh.WindowWidth, mutated.WindowWidth);
        Assert.Equal(fresh.WindowHeight, mutated.WindowHeight);
        Assert.Equal(fresh.IsMaximized, mutated.IsMaximized);
        Assert.Equal(fresh.LastOpenFolder, mutated.LastOpenFolder);
        Assert.Equal(fresh.LastVideoPath, mutated.LastVideoPath);
        Assert.Equal(fresh.LastVideoPosition, mutated.LastVideoPosition);
        Assert.Equal(fresh.ActiveSidebarPanel, mutated.ActiveSidebarPanel);
        Assert.Equal(fresh.HiddenFiles.Count, mutated.HiddenFiles.Count);
        Assert.Equal(fresh.RecentFiles.Count, mutated.RecentFiles.Count);
    }

    [Fact]
    public void AppState_ResetToDefaults_IsIdempotent()
    {
        var state = new AppState();
        state.ResetToDefaults();
        state.ResetToDefaults();

        Assert.Equal(1280, state.WindowWidth);
        Assert.Equal(720, state.WindowHeight);
        Assert.Equal("Explorer", state.ActiveSidebarPanel);
    }

    [Fact]
    public void AppState_ResetToDefaults_ClearsCollections()
    {
        var state = new AppState();
        for (int i = 0; i < 5; i++)
        {
            state.AddRecentFile($@"C:\video{i}.mp4");
            state.HiddenFiles.Add($@"C:\hidden{i}.mp4");
        }

        Assert.Equal(5, state.RecentFiles.Count);
        Assert.Equal(5, state.HiddenFiles.Count);

        state.ResetToDefaults();

        Assert.Empty(state.RecentFiles);
        Assert.Empty(state.HiddenFiles);
    }
}
