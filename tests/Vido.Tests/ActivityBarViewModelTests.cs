using NSubstitute;
using Vido.Core.Layout;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="ActivityBarViewModel"/>.
/// </summary>
public class ActivityBarViewModelTests
{
    /// <summary>
    /// Verifies that Default State explorer active sidebar visible.
    /// </summary>
    [Fact]
    public void DefaultState_ExplorerActive_SidebarVisible()
    {
        var vm = new ActivityBarViewModel();

        Assert.Equal(SidebarPanelKind.Explorer, vm.ActivePanel);
        Assert.True(vm.IsSidebarVisible);
    }

    /// <summary>
    /// Verifies that Select Panel different panel switches active.
    /// </summary>
    [Fact]
    public void SelectPanel_DifferentPanel_SwitchesActive()
    {
        var vm = new ActivityBarViewModel();

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Playlists);

        Assert.Equal(SidebarPanelKind.Playlists, vm.ActivePanel);
        Assert.True(vm.IsSidebarVisible);
    }

    /// <summary>
    /// Verifies that Select Panel same active panel toggles sidebar off.
    /// </summary>
    [Fact]
    public void SelectPanel_SameActivePanel_TogglesSidebarOff()
    {
        var vm = new ActivityBarViewModel();
        Assert.True(vm.IsSidebarVisible);

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);

        Assert.False(vm.IsSidebarVisible);
    }

    /// <summary>
    /// Verifies that Select Panel same panel when sidebar hidden shows sidebar again.
    /// </summary>
    [Fact]
    public void SelectPanel_SamePanel_WhenSidebarHidden_ShowsSidebarAgain()
    {
        var vm = new ActivityBarViewModel();

        // Hide sidebar by clicking active panel
        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);
        Assert.False(vm.IsSidebarVisible);

        // Click same panel again — should show sidebar
        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);
        Assert.True(vm.IsSidebarVisible);
    }

    /// <summary>
    /// Verifies that Select Panel different panel when sidebar hidden shows sidebar.
    /// </summary>
    [Fact]
    public void SelectPanel_DifferentPanel_WhenSidebarHidden_ShowsSidebar()
    {
        var vm = new ActivityBarViewModel();

        // Hide sidebar
        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);
        Assert.False(vm.IsSidebarVisible);

        // Click different panel — should show sidebar with new panel
        vm.SelectPanelCommand.Execute(SidebarPanelKind.Settings);
        Assert.True(vm.IsSidebarVisible);
        Assert.Equal(SidebarPanelKind.Settings, vm.ActivePanel);
    }

    /// <summary>
    /// Verifies that Is Panel Active returns true for active panel.
    /// </summary>
    [Fact]
    public void IsPanelActive_ReturnsTrue_ForActivePanel()
    {
        var vm = new ActivityBarViewModel();

        Assert.True(vm.IsPanelActive(SidebarPanelKind.Explorer));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Playlists));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Settings));
    }

    /// <summary>
    /// Verifies that Is Panel Active updates after switch.
    /// </summary>
    [Fact]
    public void IsPanelActive_UpdatesAfterSwitch()
    {
        var vm = new ActivityBarViewModel();

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Settings);

        Assert.False(vm.IsPanelActive(SidebarPanelKind.Explorer));
        Assert.True(vm.IsPanelActive(SidebarPanelKind.Settings));
    }

    /// <summary>
    /// Verifies that Select Panel raises property changed for active panel.
    /// </summary>
    [Fact]
    public void SelectPanel_RaisesPropertyChanged_ForActivePanel()
    {
        var vm = new ActivityBarViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Playlists);

        Assert.Contains("ActivePanel", changedProperties);
    }

    /// <summary>
    /// Verifies that Select Panel raises property changed for is sidebar visible.
    /// </summary>
    [Fact]
    public void SelectPanel_RaisesPropertyChanged_ForIsSidebarVisible()
    {
        var vm = new ActivityBarViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Toggle off
        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);

        Assert.Contains("IsSidebarVisible", changedProperties);
    }

    /// <summary>
    /// Verifies that Select Panel all panels work.
    /// </summary>
    /// <param name="panel">The sidebar panel kind.</param>
    [Theory]
    [InlineData(SidebarPanelKind.Explorer)]
    [InlineData(SidebarPanelKind.Playlists)]
    [InlineData(SidebarPanelKind.Osr2Plus)]
    [InlineData(SidebarPanelKind.Pulse)]
    [InlineData(SidebarPanelKind.Settings)]
    public void SelectPanel_AllPanels_Work(SidebarPanelKind panel)
    {
        var vm = new ActivityBarViewModel();

        vm.SelectPanelCommand.Execute(panel);

        Assert.Equal(panel, vm.ActivePanel);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-016 — Explorer Panel Empty on App Restore                   ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that Set Active Panel sets active panel without toggling sidebar.
    /// </summary>
    [Fact]
    public void SetActivePanel_SetsActivePanelWithoutTogglingSidebar()
    {
        var vm = new ActivityBarViewModel();
        Assert.True(vm.IsSidebarVisible);

        // SetActivePanel should set the panel without toggling sidebar
        vm.SetActivePanel(SidebarPanelKind.Playlists);

        Assert.Equal(SidebarPanelKind.Playlists, vm.ActivePanel);
        Assert.True(vm.IsSidebarVisible); // sidebar stays visible
    }

    /// <summary>
    /// Verifies that Set Active Panel explorer is assigned even when already default.
    /// </summary>
    [Fact]
    public void SetActivePanel_Explorer_IsAssignedEvenWhenAlreadyDefault()
    {
        var vm = new ActivityBarViewModel();
        Assert.Equal(SidebarPanelKind.Explorer, vm.ActivePanel);

        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Setting Explorer when it's already the default — the property setter
        // should still accept it (ObservableProperty skips if equal, which is
        // expected; the important thing is the field value is correct).
        vm.SetActivePanel(SidebarPanelKind.Explorer);

        Assert.Equal(SidebarPanelKind.Explorer, vm.ActivePanel);
        // IsSidebarVisible should NOT have been toggled
        Assert.True(vm.IsSidebarVisible);
    }

    /// <summary>
    /// Verifies that ClearActivePanel deselects all panels.
    /// </summary>
    [Fact]
    public void ClearActivePanel_DeselectsAllPanels()
    {
        var vm = new ActivityBarViewModel();
        Assert.True(vm.IsPanelActive(SidebarPanelKind.Explorer));

        vm.ClearActivePanel();

        Assert.False(vm.IsPanelActive(SidebarPanelKind.Explorer));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Playlists));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Osr2Plus));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Pulse));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Settings));
    }

    /// <summary>
    /// Verifies that sidebar visibility is persisted via ISettingsService.
    /// </summary>
    [Fact]
    public void SidebarVisibility_PersistedViaSettingsService()
    {
        var settings = new AppSettings { SidebarVisible = true };
        var svc = Substitute.For<ISettingsService>();
        svc.Current.Returns(settings);
        var vm = new ActivityBarViewModel(svc);

        // Toggle sidebar off
        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);

        Assert.False(settings.SidebarVisible);
        svc.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that constructor restores sidebar visibility from settings.
    /// </summary>
    [Fact]
    public void Constructor_RestoresSidebarVisibilityFromSettings()
    {
        var settings = new AppSettings { SidebarVisible = false };
        var svc = Substitute.For<ISettingsService>();
        svc.Current.Returns(settings);

        var vm = new ActivityBarViewModel(svc);

        Assert.False(vm.IsSidebarVisible);
    }
}
