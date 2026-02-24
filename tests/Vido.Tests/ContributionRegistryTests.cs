using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Plugin;
using Vido.PluginHost;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="ContributionRegistry"/> — thread-safe registration,
/// query, and cleanup of plugin UI contributions.
/// </summary>
public class ContributionRegistryTests
{
    private readonly ContributionRegistry _registry = new();

    [Fact]
    public void RegisterSidebarPanel_AddsEntry()
    {
        _registry.RegisterSidebarPanel("plugin1", "panel1", "My Panel", "/icon.png", 50, () => "view");

        var panels = _registry.GetSidebarPanels();

        Assert.Single(panels);
        Assert.Equal("plugin1", panels[0].PluginId);
        Assert.Equal("panel1", panels[0].ContributionId);
        Assert.Equal("My Panel", panels[0].Title);
        Assert.Equal("/icon.png", panels[0].IconPath);
        Assert.Equal(50, panels[0].Order);
    }

    [Fact]
    public void RegisterSidebarPanel_SortsByOrder()
    {
        _registry.RegisterSidebarPanel("p1", "b", "B", null, 200, () => "view2");
        _registry.RegisterSidebarPanel("p2", "a", "A", null, 100, () => "view1");
        _registry.RegisterSidebarPanel("p3", "c", "C", null, 150, () => "view3");

        var panels = _registry.GetSidebarPanels();

        Assert.Equal(3, panels.Count);
        Assert.Equal("a", panels[0].ContributionId);
        Assert.Equal("c", panels[1].ContributionId);
        Assert.Equal("b", panels[2].ContributionId);
    }

    [Fact]
    public void RegisterBottomPanel_AddsAndSortsByOrder()
    {
        _registry.RegisterBottomPanel("p1", "z", "Z", 300, () => "v1");
        _registry.RegisterBottomPanel("p2", "a", "A", 100, () => "v2");

        var panels = _registry.GetBottomPanels();

        Assert.Equal(2, panels.Count);
        Assert.Equal("a", panels[0].ContributionId);
        Assert.Equal("z", panels[1].ContributionId);
    }

    [Fact]
    public void RegisterRightPanel_AddsEntry()
    {
        _registry.RegisterRightPanel("p1", "info", "Info", 50, () => "view");

        var panels = _registry.GetRightPanels();

        Assert.Single(panels);
        Assert.Equal("info", panels[0].ContributionId);
    }

    [Fact]
    public void RegisterStatusBarItem_AddsEntry()
    {
        _registry.RegisterStatusBarItem("p1", "status1", "Test Status", "left", 10, () => "item");

        var items = _registry.GetStatusBarItems();

        Assert.Single(items);
        Assert.Equal("status1", items[0].ContributionId);
        Assert.Equal("left", items[0].Position);
    }

    [Fact]
    public void RegisterToolbarButton_AddsAndSortsByOrder()
    {
        _registry.RegisterToolbarButton("p1", "btn2", "Tip 2", null, 200, () => { });
        _registry.RegisterToolbarButton("p2", "btn1", "Tip 1", null, 100, () => { });

        var buttons = _registry.GetToolbarButtons();

        Assert.Equal(2, buttons.Count);
        Assert.Equal("btn1", buttons[0].ContributionId);
        Assert.Equal("btn2", buttons[1].ContributionId);
    }

    [Fact]
    public void RegisterContextMenuHandler_AddsEntry()
    {
        var called = false;
        _registry.RegisterContextMenuHandler(
            "p1", "ctx1", "My Action", [".mp4"], 50,
            _ => called = true);

        var items = _registry.GetContextMenuItems();

        Assert.Single(items);
        Assert.Equal("My Action", items[0].Label);
        Assert.Single(items[0].FileExtensions);

        items[0].Handler(new FileNode("test.mp4", false));
        Assert.True(called);
    }

    [Fact]
    public void RegisterFileHandler_AddsEntry()
    {
        _registry.RegisterFileHandler("p1", [".special"], _ => { });

        var handlers = _registry.GetFileHandlers();

        Assert.Single(handlers);
        Assert.Equal("p1", handlers[0].PluginId);
        Assert.Equal(".special", handlers[0].Extensions[0]);
    }

    [Fact]
    public void RegisterFileIcons_AddsMappings()
    {
        _registry.RegisterFileIcons("p1", new Dictionary<string, string>
        {
            [".abc"] = "/icons/abc.png",
            [".xyz"] = "/icons/xyz.png"
        });

        var icons = _registry.GetFileIcons();

        Assert.Equal(2, icons.Count);
        Assert.Equal("/icons/abc.png", icons[".abc"]);
        Assert.Equal("/icons/xyz.png", icons[".xyz"]);
    }

    [Fact]
    public void RegisterFileIcons_CaseInsensitive()
    {
        _registry.RegisterFileIcons("p1", new Dictionary<string, string>
        {
            [".ABC"] = "/icons/abc.png"
        });

        var icons = _registry.GetFileIcons();

        Assert.True(icons.ContainsKey(".abc"));
        Assert.True(icons.ContainsKey(".ABC"));
    }

    [Fact]
    public void UnregisterAll_RemovesAllContributions()
    {
        _registry.RegisterSidebarPanel("p1", "s1", "S", null, 10, () => "v");
        _registry.RegisterBottomPanel("p1", "b1", "B", 10, () => "v");
        _registry.RegisterRightPanel("p1", "r1", "R", 10, () => "v");
        _registry.RegisterStatusBarItem("p1", "sb1", "SB", "right", 10, () => "v");
        _registry.RegisterToolbarButton("p1", "btn1", "T", null, 10, () => { });
        _registry.RegisterContextMenuHandler("p1", "ctx1", "C", [], 10, _ => { });
        _registry.RegisterFileHandler("p1", [".ext"], _ => { });
        _registry.RegisterFileIcons("p1", new Dictionary<string, string> { [".x"] = "icon" });
        _registry.RegisterKeyBinding("p1", new KeyBinding("F1"), "cmd1", () => { });

        _registry.UnregisterAll("p1");

        Assert.Empty(_registry.GetSidebarPanels());
        Assert.Empty(_registry.GetBottomPanels());
        Assert.Empty(_registry.GetRightPanels());
        Assert.Empty(_registry.GetStatusBarItems());
        Assert.Empty(_registry.GetToolbarButtons());
        Assert.Empty(_registry.GetContextMenuItems());
        Assert.Empty(_registry.GetFileHandlers());
        Assert.Empty(_registry.GetFileIcons());
    }

    [Fact]
    public void UnregisterAll_OnlyRemovesSpecifiedPlugin()
    {
        _registry.RegisterSidebarPanel("p1", "s1", "S1", null, 10, () => "v1");
        _registry.RegisterSidebarPanel("p2", "s2", "S2", null, 20, () => "v2");

        _registry.UnregisterAll("p1");

        var panels = _registry.GetSidebarPanels();
        Assert.Single(panels);
        Assert.Equal("p2", panels[0].PluginId);
    }

    [Fact]
    public void UnregisterAll_UnknownPlugin_NoOp()
    {
        _registry.RegisterSidebarPanel("p1", "s1", "S1", null, 10, () => "v");

        _registry.UnregisterAll("nonexistent");

        Assert.Single(_registry.GetSidebarPanels());
    }

    [Fact]
    public void ContributionsChanged_FiredOnRegistration()
    {
        var count = 0;
        _registry.ContributionsChanged += () => count++;

        _registry.RegisterSidebarPanel("p1", "s1", "S", null, 10, () => "v");
        _registry.RegisterBottomPanel("p1", "b1", "B", 10, () => "v");
        _registry.RegisterFileIcons("p1", new Dictionary<string, string> { [".x"] = "i" });

        Assert.Equal(3, count);
    }

    [Fact]
    public void ContributionsChanged_FiredOnUnregister()
    {
        _registry.RegisterSidebarPanel("p1", "s1", "S", null, 10, () => "v");

        var count = 0;
        _registry.ContributionsChanged += () => count++;

        _registry.UnregisterAll("p1");

        Assert.Equal(1, count);
    }

    [Fact]
    public void GetSidebarPanels_ReturnsSnapshot()
    {
        _registry.RegisterSidebarPanel("p1", "s1", "S", null, 10, () => "v");

        var snapshot1 = _registry.GetSidebarPanels();
        _registry.RegisterSidebarPanel("p2", "s2", "S2", null, 20, () => "v2");
        var snapshot2 = _registry.GetSidebarPanels();

        Assert.Single(snapshot1);
        Assert.Equal(2, snapshot2.Count);
    }

    [Fact]
    public void MultipleSamePluginIcons_OverwritePrevious()
    {
        _registry.RegisterFileIcons("p1", new Dictionary<string, string>
        {
            [".ext"] = "/icons/old.png"
        });
        _registry.RegisterFileIcons("p1", new Dictionary<string, string>
        {
            [".ext"] = "/icons/new.png"
        });

        var icons = _registry.GetFileIcons();
        Assert.Equal("/icons/new.png", icons[".ext"]);
    }

    [Fact]
    public void InsertSorted_EqualOrder_AcceptsBothItems()
    {
        _registry.RegisterSidebarPanel("p1", "a", "A", null, 100, () => "v1");
        _registry.RegisterSidebarPanel("p2", "b", "B", null, 100, () => "v2");

        var panels = _registry.GetSidebarPanels();
        Assert.Equal(2, panels.Count);
    }

    [Fact]
    public void InsertSorted_MultipleEqualOrder_MaintainsValidList()
    {
        _registry.RegisterBottomPanel("p1", "x", "X", 50, () => "v1");
        _registry.RegisterBottomPanel("p2", "y", "Y", 50, () => "v2");
        _registry.RegisterBottomPanel("p3", "z", "Z", 50, () => "v3");
        _registry.RegisterBottomPanel("p4", "w", "W", 10, () => "v4");

        var panels = _registry.GetBottomPanels();
        Assert.Equal(4, panels.Count);
        // w (order 10) should come first
        Assert.Equal("w", panels[0].ContributionId);
        // All three order-50 items should follow
        Assert.True(panels.Skip(1).All(p => p.Order == 50));
    }

    [Fact]
    public void RegisterKeyBinding_DoesNotThrow()
    {
        // RegisterKeyBinding is a no-op but should not throw
        var ex = Record.Exception(() =>
            _registry.RegisterKeyBinding("p1", new KeyBinding("Ctrl+P"), "cmd1", () => { }));
        Assert.Null(ex);
    }

    [Fact]
    public void SetToolbarButtonHighlight_FiresEvent()
    {
        _registry.RegisterToolbarButton("p1", "btn1", "T", null, 10, () => { });

        string? firedId = null;
        bool? firedState = null;
        _registry.ToolbarButtonHighlightChanged += (id, state) => { firedId = id; firedState = state; };

        _registry.SetToolbarButtonHighlight("p1", "btn1", true);

        Assert.Equal("plugin.p1.btn1", firedId);
        Assert.True(firedState);
    }

    [Fact]
    public void SetToolbarButtonHighlight_ClearFiresEvent()
    {
        _registry.RegisterToolbarButton("p1", "btn1", "T", null, 10, () => { });

        _registry.SetToolbarButtonHighlight("p1", "btn1", true);

        string? firedId = null;
        bool? firedState = null;
        _registry.ToolbarButtonHighlightChanged += (id, state) => { firedId = id; firedState = state; };

        _registry.SetToolbarButtonHighlight("p1", "btn1", false);

        Assert.Equal("plugin.p1.btn1", firedId);
        Assert.False(firedState);
    }

    // ── Priority tiebreaking ──

    [Fact]
    public void RegisterStatusBarItem_SamePriority_OrdersByPluginId()
    {
        _registry.RegisterStatusBarItem("pluginC", "s1", "C", "left", 10, () => "v");
        _registry.RegisterStatusBarItem("pluginA", "s2", "A", "left", 10, () => "v");
        _registry.RegisterStatusBarItem("pluginB", "s3", "B", "left", 10, () => "v");

        var items = _registry.GetStatusBarItems();
        Assert.Equal(3, items.Count);
        Assert.Equal("pluginA", items[0].PluginId);
        Assert.Equal("pluginB", items[1].PluginId);
        Assert.Equal("pluginC", items[2].PluginId);
    }

    [Fact]
    public void RegisterSidebarPanel_SamePriority_OrdersByPluginId()
    {
        _registry.RegisterSidebarPanel("pluginZ", "a", "Z", null, 50, () => "v");
        _registry.RegisterSidebarPanel("pluginA", "b", "A", null, 50, () => "v");

        var panels = _registry.GetSidebarPanels();
        Assert.Equal("pluginA", panels[0].PluginId);
        Assert.Equal("pluginZ", panels[1].PluginId);
    }
}
