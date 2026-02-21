using Vido.Core.Layout;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="MainWindowViewModel"/> — tab management,
/// panel visibility, and tab reordering logic.
/// </summary>
public class MainWindowViewModelTests
{
    private readonly MainWindowViewModel _sut = new();

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
    public void Constructor_PanelsHiddenByDefault()
    {
        Assert.False(_sut.IsBottomPanelVisible);
        Assert.False(_sut.IsRightPanelVisible);
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
        Assert.False(_sut.IsBottomPanelVisible);

        _sut.ToggleBottomPanel();
        Assert.True(_sut.IsBottomPanelVisible);

        _sut.ToggleBottomPanel();
        Assert.False(_sut.IsBottomPanelVisible);
    }

    [Fact]
    public void ToggleRightPanel_TogglesVisibility()
    {
        Assert.False(_sut.IsRightPanelVisible);

        _sut.ToggleRightPanel();
        Assert.True(_sut.IsRightPanelVisible);

        _sut.ToggleRightPanel();
        Assert.False(_sut.IsRightPanelVisible);
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
}
