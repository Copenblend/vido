using Vido.Core.Layout;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="ActivityBarViewModel"/>.
/// </summary>
public class ActivityBarViewModelTests
{
    [Fact]
    public void DefaultState_ExplorerActive_SidebarVisible()
    {
        var vm = new ActivityBarViewModel();

        Assert.Equal(SidebarPanelKind.Explorer, vm.ActivePanel);
        Assert.True(vm.IsSidebarVisible);
    }

    [Fact]
    public void SelectPanel_DifferentPanel_SwitchesActive()
    {
        var vm = new ActivityBarViewModel();

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Extensions);

        Assert.Equal(SidebarPanelKind.Extensions, vm.ActivePanel);
        Assert.True(vm.IsSidebarVisible);
    }

    [Fact]
    public void SelectPanel_SameActivePanel_TogglesSidebarOff()
    {
        var vm = new ActivityBarViewModel();
        Assert.True(vm.IsSidebarVisible);

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);

        Assert.False(vm.IsSidebarVisible);
    }

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

    [Fact]
    public void IsPanelActive_ReturnsTrue_ForActivePanel()
    {
        var vm = new ActivityBarViewModel();

        Assert.True(vm.IsPanelActive(SidebarPanelKind.Explorer));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Extensions));
        Assert.False(vm.IsPanelActive(SidebarPanelKind.Settings));
    }

    [Fact]
    public void IsPanelActive_UpdatesAfterSwitch()
    {
        var vm = new ActivityBarViewModel();

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Settings);

        Assert.False(vm.IsPanelActive(SidebarPanelKind.Explorer));
        Assert.True(vm.IsPanelActive(SidebarPanelKind.Settings));
    }

    [Fact]
    public void SelectPanel_RaisesPropertyChanged_ForActivePanel()
    {
        var vm = new ActivityBarViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.SelectPanelCommand.Execute(SidebarPanelKind.Extensions);

        Assert.Contains("ActivePanel", changedProperties);
    }

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

    [Theory]
    [InlineData(SidebarPanelKind.Explorer)]
    [InlineData(SidebarPanelKind.Extensions)]
    [InlineData(SidebarPanelKind.Settings)]
    public void SelectPanel_AllPanels_Work(SidebarPanelKind panel)
    {
        var vm = new ActivityBarViewModel();

        vm.SelectPanelCommand.Execute(panel);

        Assert.Equal(panel, vm.ActivePanel);
    }
}
