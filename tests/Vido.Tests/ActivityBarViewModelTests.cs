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

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-016 — Explorer Panel Empty on App Restore                   ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void SetActivePanel_SetsActivePanelWithoutTogglingSidebar()
    {
        var vm = new ActivityBarViewModel();
        Assert.True(vm.IsSidebarVisible);

        // SetActivePanel should set the panel without toggling sidebar
        vm.SetActivePanel(SidebarPanelKind.Extensions);

        Assert.Equal(SidebarPanelKind.Extensions, vm.ActivePanel);
        Assert.True(vm.IsSidebarVisible); // sidebar stays visible
    }

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

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ vb-007 — Drag-and-Drop Sidebar Icon Reordering                 ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void AddPluginItem_WithNoSavedOrder_UsesDefaultOrder()
    {
        var vm = new ActivityBarViewModel();
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 10 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.b", Order = 5 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.c", Order = 20 });

        Assert.Equal(3, vm.PluginItems.Count);
        Assert.Equal("plugin.b", vm.PluginItems[0].Id);
        Assert.Equal("plugin.a", vm.PluginItems[1].Id);
        Assert.Equal("plugin.c", vm.PluginItems[2].Id);
    }

    [Fact]
    public void AddPluginItem_WithSavedOrder_RestoresOrder()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            SidebarVisible = true,
            PluginSidebarOrder = ["plugin.c", "plugin.a", "plugin.b"]
        });
        var vm = new ActivityBarViewModel(settings);

        // Add in default order — should be reordered to match saved order
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.b", Order = 1 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.c", Order = 2 });

        Assert.Equal("plugin.c", vm.PluginItems[0].Id);
        Assert.Equal("plugin.a", vm.PluginItems[1].Id);
        Assert.Equal("plugin.b", vm.PluginItems[2].Id);
    }

    [Fact]
    public void RemovePluginItem_RemovesById()
    {
        var vm = new ActivityBarViewModel();
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.b", Order = 1 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.c", Order = 2 });

        vm.RemovePluginItem("plugin.b");

        Assert.Equal(2, vm.PluginItems.Count);
        Assert.Equal("plugin.a", vm.PluginItems[0].Id);
        Assert.Equal("plugin.c", vm.PluginItems[1].Id);
    }

    [Fact]
    public void RemovePluginItem_NonExistentId_DoesNothing()
    {
        var vm = new ActivityBarViewModel();
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });

        vm.RemovePluginItem("plugin.does-not-exist");

        Assert.Single(vm.PluginItems);
    }

    [Fact]
    public void MovePluginItem_ReordersItems()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { SidebarVisible = true });
        var vm = new ActivityBarViewModel(settings);

        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.b", Order = 1 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.c", Order = 2 });

        // Move first item to last position
        vm.MovePluginItem(0, 2);

        Assert.Equal("plugin.b", vm.PluginItems[0].Id);
        Assert.Equal("plugin.c", vm.PluginItems[1].Id);
        Assert.Equal("plugin.a", vm.PluginItems[2].Id);
    }

    [Fact]
    public void MovePluginItem_PersistsOrder()
    {
        var settings = Substitute.For<ISettingsService>();
        var appSettings = new AppSettings { SidebarVisible = true };
        settings.Current.Returns(appSettings);
        var vm = new ActivityBarViewModel(settings);

        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.b", Order = 1 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.c", Order = 2 });

        vm.MovePluginItem(0, 2);

        // Verify persisted order matches the new visual order
        Assert.Equal(new[] { "plugin.b", "plugin.c", "plugin.a" }, appSettings.PluginSidebarOrder);
        settings.Received().QueueSave();
    }

    [Fact]
    public void MovePluginItem_InvalidIndices_DoesNothing()
    {
        var vm = new ActivityBarViewModel();
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });

        vm.MovePluginItem(-1, 0);
        vm.MovePluginItem(0, 5);
        vm.MovePluginItem(0, 0); // same index

        Assert.Single(vm.PluginItems);
        Assert.Equal("plugin.a", vm.PluginItems[0].Id);
    }

    [Fact]
    public void MovePluginItem_UpdatesOrderValues()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { SidebarVisible = true });
        var vm = new ActivityBarViewModel(settings);

        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.a", Order = 0 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.b", Order = 1 });
        vm.AddPluginItem(new PluginSidebarItem { Id = "plugin.c", Order = 2 });

        vm.MovePluginItem(2, 0);

        // After move, Order values should be sequential starting from 0
        for (var i = 0; i < vm.PluginItems.Count; i++)
            Assert.Equal(i, vm.PluginItems[i].Order);
    }
}
