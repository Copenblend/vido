using NSubstitute;
using Vido.Core.Layout;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="MainWindowViewModel"/> — tab management,
/// panel visibility, tab reordering, status bar, and fullscreen state.
/// </summary>
public class MainWindowViewModelTests
{
    private readonly ISettingsService _settingsService;
    private readonly MainWindowViewModel _sut;

    public MainWindowViewModelTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings
        {
            BottomPanelVisible = true,
            RightPanelVisible = true
        });
        _sut = new MainWindowViewModel(_settingsService);
    }

    // ── Constructor / Initial State ──

    [Fact]
    public void Constructor_CreatesPlayerTab()
    {
        Assert.Single(_sut.Tabs);
        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.Tabs[0].Id);
    }

    [Fact]
    public void Constructor_PlayerTabIsActiveByDefault()
    {
        Assert.NotNull(_sut.ActiveTab);
        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.ActiveTab!.Id);
    }

    [Fact]
    public void Constructor_PlayerTabIsPinnedAndNotClosable()
    {
        var playerTab = _sut.Tabs[0];
        Assert.True(playerTab.IsPinned);
        Assert.False(playerTab.IsClosable);
    }

    [Fact]
    public void Constructor_PanelsVisibleButCollapsedByDefault()
    {
        // Create a VM with explicit collapsed=true to test the "collapsed but visible" state
        var settingsSvc = Substitute.For<ISettingsService>();
        settingsSvc.Current.Returns(new AppSettings
        {
            BottomPanelVisible = true,
            BottomPanelCollapsed = true,
            RightPanelVisible = true,
            RightPanelCollapsed = true
        });
        var vm = new MainWindowViewModel(settingsSvc);

        Assert.True(vm.IsBottomPanelVisible);
        Assert.True(vm.IsBottomPanelCollapsed);
        Assert.True(vm.IsRightPanelVisible);
        Assert.True(vm.IsRightPanelCollapsed);
    }

    [Fact]
    public void Constructor_PanelsVisibleAndExpanded_WhenDefaultSettings()
    {
        // Default AppSettings now has panels visible + expanded
        var settingsSvc = Substitute.For<ISettingsService>();
        settingsSvc.Current.Returns(new AppSettings());
        var vm = new MainWindowViewModel(settingsSvc);

        Assert.True(vm.IsBottomPanelVisible);
        Assert.False(vm.IsBottomPanelCollapsed);
        Assert.True(vm.IsRightPanelVisible);
        Assert.False(vm.IsRightPanelCollapsed);
    }

    // ── OpenTab ──

    [Fact]
    public void OpenTab_AddsNewTabAndActivatesIt()
    {
        _sut.OpenTab("test", "Test Tab");

        Assert.Equal(2, _sut.Tabs.Count);
        Assert.Equal("test", _sut.ActiveTab!.Id);
    }

    [Fact]
    public void OpenTab_ExistingId_ActivatesWithoutDuplicating()
    {
        _sut.OpenTab("test", "Test Tab");
        _sut.ActivateTab(MainWindowViewModel.PlayerTabId);
        _sut.OpenTab("test", "Test Tab");

        Assert.Equal(2, _sut.Tabs.Count);
        Assert.Equal("test", _sut.ActiveTab!.Id);
    }

    [Fact]
    public void OpenTab_SetsIconGeometry()
    {
        _sut.OpenTab("test", "Test", iconGeometry: "M 0,0 L 10,10");

        var tab = _sut.FindTab("test");
        Assert.Equal("M 0,0 L 10,10", tab?.IconGeometry);
    }

    [Fact]
    public void OpenTab_SetsIsClosable()
    {
        _sut.OpenTab("test", "Test", isClosable: false);

        var tab = _sut.FindTab("test");
        Assert.False(tab?.IsClosable);
    }

    // ── CloseTab ──

    [Fact]
    public void CloseTab_RemovesClosableTab()
    {
        _sut.OpenTab("test", "Test Tab");
        _sut.CloseTab("test");

        Assert.Single(_sut.Tabs);
        Assert.Null(_sut.FindTab("test"));
    }

    [Fact]
    public void CloseTab_CannotClosePlayerTab()
    {
        _sut.CloseTab(MainWindowViewModel.PlayerTabId);

        Assert.Single(_sut.Tabs);
        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.Tabs[0].Id);
    }

    [Fact]
    public void CloseTab_ActiveTabClosed_ActivatesNeighbor()
    {
        _sut.OpenTab("a", "A");
        _sut.OpenTab("b", "B");
        // Active is "b"
        _sut.CloseTab("b");

        Assert.Equal("a", _sut.ActiveTab!.Id);
    }

    [Fact]
    public void CloseTab_ActiveTabClosed_ActivatesPlayerWhenLastClosable()
    {
        _sut.OpenTab("test", "Test");
        _sut.CloseTab("test");

        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.ActiveTab!.Id);
    }

    [Fact]
    public void CloseTab_NonexistentId_DoesNothing()
    {
        _sut.CloseTab("nonexistent");

        Assert.Single(_sut.Tabs);
    }

    [Fact]
    public void CloseTab_InactiveTab_DoesNotChangeActiveTab()
    {
        _sut.OpenTab("a", "A");
        _sut.OpenTab("b", "B");
        // Active is "b"
        _sut.CloseTab("a");

        Assert.Equal("b", _sut.ActiveTab!.Id);
        Assert.Equal(2, _sut.Tabs.Count);
    }

    // ── ActivateTab ──

    [Fact]
    public void ActivateTab_SwitchesToExistingTab()
    {
        _sut.OpenTab("test", "Test");
        _sut.ActivateTab(MainWindowViewModel.PlayerTabId);

        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.ActiveTab!.Id);
    }

    [Fact]
    public void ActivateTab_NonexistentId_DoesNothing()
    {
        _sut.ActivateTab("nonexistent");

        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.ActiveTab!.Id);
    }

    // ── ReorderTab ──

    [Fact]
    public void ReorderTab_MovesTabToNewPosition()
    {
        _sut.OpenTab("a", "A");
        _sut.OpenTab("b", "B");
        // Tabs: Player(0), A(1), B(2)

        _sut.ReorderTab(2, 1);

        Assert.Equal("b", _sut.Tabs[1].Id);
        Assert.Equal("a", _sut.Tabs[2].Id);
    }

    [Fact]
    public void ReorderTab_CannotMovePinnedTab()
    {
        _sut.OpenTab("a", "A");
        // Player is pinned at index 0

        _sut.ReorderTab(0, 1);

        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.Tabs[0].Id);
    }

    [Fact]
    public void ReorderTab_CannotMoveBeforePinnedTab()
    {
        _sut.OpenTab("a", "A");
        _sut.OpenTab("b", "B");
        // Cannot move B to index 0 (before pinned Player)

        _sut.ReorderTab(2, 0);

        Assert.Equal(MainWindowViewModel.PlayerTabId, _sut.Tabs[0].Id);
    }

    [Fact]
    public void ReorderTab_InvalidIndices_DoesNothing()
    {
        _sut.OpenTab("a", "A");

        _sut.ReorderTab(-1, 0);
        _sut.ReorderTab(0, 99);
        _sut.ReorderTab(5, 0);

        Assert.Equal(2, _sut.Tabs.Count);
    }

    [Fact]
    public void ReorderTab_SameIndex_DoesNothing()
    {
        _sut.OpenTab("a", "A");

        _sut.ReorderTab(1, 1);

        Assert.Equal("a", _sut.Tabs[1].Id);
    }

    // ── OpenSettings ──

    [Fact]
    public void OpenSettings_CreatesSettingsTab()
    {
        _sut.OpenSettings();

        Assert.Equal(2, _sut.Tabs.Count);
        Assert.Equal(MainWindowViewModel.SettingsTabId, _sut.ActiveTab!.Id);
    }

    [Fact]
    public void OpenSettings_CalledTwice_DoesNotDuplicate()
    {
        _sut.OpenSettings();
        _sut.OpenSettings();

        Assert.Equal(2, _sut.Tabs.Count);
    }

    [Fact]
    public void OpenSettings_SettingsTabIsClosable()
    {
        _sut.OpenSettings();

        var tab = _sut.FindTab(MainWindowViewModel.SettingsTabId);
        Assert.True(tab?.IsClosable);
    }

    // ── Panel Toggles ──

    [Fact]
    public void ToggleBottomPanel_TogglesVisibility()
    {
        // Starts visible (collapsed)
        Assert.True(_sut.IsBottomPanelVisible);

        _sut.ToggleBottomPanel();
        Assert.False(_sut.IsBottomPanelVisible);

        _sut.ToggleBottomPanel();
        Assert.True(_sut.IsBottomPanelVisible);
    }

    [Fact]
    public void ToggleRightPanel_TogglesVisibility()
    {
        // Starts visible (collapsed)
        Assert.True(_sut.IsRightPanelVisible);

        _sut.ToggleRightPanel();
        Assert.False(_sut.IsRightPanelVisible);

        _sut.ToggleRightPanel();
        Assert.True(_sut.IsRightPanelVisible);
    }

    // ── FindTab ──

    [Fact]
    public void FindTab_ReturnsCorrectTab()
    {
        _sut.OpenTab("test", "Test");

        var tab = _sut.FindTab("test");
        Assert.NotNull(tab);
        Assert.Equal("Test", tab!.Title);
    }

    [Fact]
    public void FindTab_NonexistentId_ReturnsNull()
    {
        Assert.Null(_sut.FindTab("nonexistent"));
    }

    // ── TabItemModel ──

    [Fact]
    public void TabItemModel_DefaultValues()
    {
        var tab = new TabItemModel("id", "Title");

        Assert.Equal("id", tab.Id);
        Assert.Equal("Title", tab.Title);
        Assert.True(tab.IsClosable);
        Assert.False(tab.IsPinned);
        Assert.Null(tab.IconGeometry);
    }

    // ── PropertyChanged notifications ──

    [Fact]
    public void ActiveTab_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab))
                raised = true;
        };

        _sut.OpenTab("test", "Test");

        Assert.True(raised);
    }

    [Fact]
    public void IsBottomPanelVisible_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsBottomPanelVisible))
                raised = true;
        };

        _sut.ToggleBottomPanel();

        Assert.True(raised);
    }

    [Fact]
    public void IsRightPanelVisible_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsRightPanelVisible))
                raised = true;
        };

        _sut.ToggleRightPanel();

        Assert.True(raised);
    }

    // ── Right Panel Collapse ──

    [Fact]
    public void ToggleRightPanelCollapse_TogglesState()
    {
        _sut.IsRightPanelVisible = true;
        _sut.IsRightPanelCollapsed = false;

        _sut.ToggleRightPanelCollapse();
        Assert.True(_sut.IsRightPanelCollapsed);

        _sut.ToggleRightPanelCollapse();
        Assert.False(_sut.IsRightPanelCollapsed);
    }

    [Fact]
    public void ToggleRightPanelCollapse_WhenHidden_ShowsExpanded()
    {
        _sut.IsRightPanelVisible = false;

        _sut.ToggleRightPanelCollapse();

        Assert.True(_sut.IsRightPanelVisible);
        Assert.False(_sut.IsRightPanelCollapsed);
    }

    [Fact]
    public void ToggleRightPanel_ShowsExpanded_ClearsCollapsed()
    {
        _sut.IsRightPanelVisible = true;
        _sut.IsRightPanelCollapsed = true;

        _sut.ToggleRightPanel(); // hides
        _sut.ToggleRightPanel(); // shows — should clear collapsed

        Assert.True(_sut.IsRightPanelVisible);
        Assert.False(_sut.IsRightPanelCollapsed);
    }

    [Fact]
    public void IsRightPanelCollapsed_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsRightPanelCollapsed))
                raised = true;
        };

        _sut.IsRightPanelVisible = true;
        _sut.ToggleRightPanelCollapse();

        Assert.True(raised);
    }

    // ── Bottom Panel Tabs ──

    [Fact]
    public void Constructor_CreatesBottomPanelTabs()
    {
        Assert.Single(_sut.BottomPanelTabs);
        Assert.Equal(MainWindowViewModel.OutputTabId, _sut.BottomPanelTabs[0].Id);
    }

    [Fact]
    public void Constructor_OutputTabIsActiveByDefault()
    {
        Assert.NotNull(_sut.ActiveBottomPanelTab);
        Assert.Equal(MainWindowViewModel.OutputTabId, _sut.ActiveBottomPanelTab!.Id);
    }

    [Fact]
    public void ActivateBottomPanelTab_SwitchesToTab()
    {
        _sut.ActivateBottomPanelTab(MainWindowViewModel.OutputTabId);

        Assert.Equal(MainWindowViewModel.OutputTabId, _sut.ActiveBottomPanelTab!.Id);
    }

    [Fact]
    public void ActivateBottomPanelTab_ShowsPanel()
    {
        _sut.IsBottomPanelVisible = false; // Hide first

        _sut.ActivateBottomPanelTab(MainWindowViewModel.OutputTabId);

        Assert.True(_sut.IsBottomPanelVisible);
    }

    [Fact]
    public void CloseBottomPanelTab_CannotCloseNonClosableTab()
    {
        // Output tab has IsClosable = false
        _sut.CloseBottomPanelTab(MainWindowViewModel.OutputTabId);

        Assert.Single(_sut.BottomPanelTabs);
        Assert.NotNull(_sut.FindBottomPanelTab(MainWindowViewModel.OutputTabId));
    }

    [Fact]
    public void Constructor_OutputTabIsNotClosable()
    {
        var outputTab = _sut.FindBottomPanelTab(MainWindowViewModel.OutputTabId);
        Assert.NotNull(outputTab);
        Assert.False(outputTab!.IsClosable);
    }

    [Fact]
    public void OpenBottomPanelTab_ExistingTab_ActivatesIt()
    {
        _sut.OpenBottomPanelTab(MainWindowViewModel.OutputTabId);

        Assert.Equal(MainWindowViewModel.OutputTabId, _sut.ActiveBottomPanelTab!.Id);
        Assert.Single(_sut.BottomPanelTabs); // No duplicates
    }

    [Fact]
    public void ActivateBottomPanelTab_ExistingTab_ShowsPanel()
    {
        _sut.IsBottomPanelVisible = false; // Hide first

        _sut.ActivateBottomPanelTab(MainWindowViewModel.OutputTabId);

        Assert.True(_sut.IsBottomPanelVisible);
        Assert.Equal(MainWindowViewModel.OutputTabId, _sut.ActiveBottomPanelTab!.Id);
    }

    [Fact]
    public void ActiveBottomPanelTab_SetsIsActiveFlags()
    {
        var output = _sut.FindBottomPanelTab(MainWindowViewModel.OutputTabId)!;
        Assert.True(output.IsActive);
    }

    [Fact]
    public void ActiveBottomPanelTab_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ActiveBottomPanelTab))
                raised = true;
        };

        // Activate the same tab explicitly — the ActivateBottomPanelTab method
        // sets ActiveBottomPanelTab which raises PropertyChanged
        _sut.ActiveBottomPanelTab = null; // Reset to force change
        raised = false;
        _sut.ActivateBottomPanelTab(MainWindowViewModel.OutputTabId);

        Assert.True(raised);
    }

    [Fact]
    public void FindBottomPanelTab_ReturnsCorrectTab()
    {
        var tab = _sut.FindBottomPanelTab(MainWindowViewModel.OutputTabId);
        Assert.NotNull(tab);
        Assert.Equal("LOG OUTPUT", tab!.Title);
    }

    [Fact]
    public void FindBottomPanelTab_NonexistentId_ReturnsNull()
    {
        Assert.Null(_sut.FindBottomPanelTab("nonexistent"));
    }

    [Fact]
    public void CloseBottomPanelTab_NonexistentId_DoesNothing()
    {
        _sut.CloseBottomPanelTab("nonexistent");
        Assert.Single(_sut.BottomPanelTabs);
    }

    // ── Bottom Panel Collapse ──

    [Fact]
    public void ToggleBottomPanelCollapse_TogglesState()
    {
        _sut.IsBottomPanelVisible = true;
        _sut.IsBottomPanelCollapsed = false;

        _sut.ToggleBottomPanelCollapse();
        Assert.True(_sut.IsBottomPanelCollapsed);

        _sut.ToggleBottomPanelCollapse();
        Assert.False(_sut.IsBottomPanelCollapsed);
    }

    [Fact]
    public void ToggleBottomPanelCollapse_WhenHidden_ShowsExpanded()
    {
        _sut.IsBottomPanelVisible = false; // Hide first

        _sut.ToggleBottomPanelCollapse();

        Assert.True(_sut.IsBottomPanelVisible);
        Assert.False(_sut.IsBottomPanelCollapsed);
    }

    [Fact]
    public void ToggleBottomPanel_ShowsExpanded_ClearsCollapsed()
    {
        _sut.IsBottomPanelVisible = true;
        _sut.IsBottomPanelCollapsed = true;

        _sut.ToggleBottomPanel(); // hides
        _sut.ToggleBottomPanel(); // shows — should clear collapsed

        Assert.True(_sut.IsBottomPanelVisible);
        Assert.False(_sut.IsBottomPanelCollapsed);
    }

    [Fact]
    public void IsBottomPanelCollapsed_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsBottomPanelCollapsed))
                raised = true;
        };

        _sut.IsBottomPanelVisible = true;
        _sut.ToggleBottomPanelCollapse();

        Assert.True(raised);
    }

    // ── Status Bar Visibility ──

    [Fact]
    public void Constructor_StatusBarVisibleByDefault()
    {
        Assert.True(_sut.IsStatusBarVisible);
    }

    [Fact]
    public void ToggleStatusBar_TogglesVisibility()
    {
        Assert.True(_sut.IsStatusBarVisible);

        _sut.ToggleStatusBar();
        Assert.False(_sut.IsStatusBarVisible);

        _sut.ToggleStatusBar();
        Assert.True(_sut.IsStatusBarVisible);
    }

    [Fact]
    public void IsStatusBarVisible_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsStatusBarVisible))
                raised = true;
        };

        _sut.ToggleStatusBar();

        Assert.True(raised);
    }

    // ── Fullscreen ──

    [Fact]
    public void IsFullscreen_DefaultFalse()
    {
        Assert.False(_sut.IsFullscreen);
    }

    [Fact]
    public void IsFullscreen_CanBeSet()
    {
        _sut.IsFullscreen = true;
        Assert.True(_sut.IsFullscreen);

        _sut.IsFullscreen = false;
        Assert.False(_sut.IsFullscreen);
    }

    [Fact]
    public void IsFullscreen_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsFullscreen))
                raised = true;
        };

        _sut.IsFullscreen = true;

        Assert.True(raised);
    }
}
