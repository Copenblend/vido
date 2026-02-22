using NSubstitute;
using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Menus;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Vido.PluginHost;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginContext"/> — the API surface plugins use
/// for registering UI contributions and accessing services.
/// </summary>
public class PluginContextTests
{
    private readonly ContributionRegistry _contributions = new();
    private readonly IContextMenuRegistry _contextMenuRegistry = Substitute.For<IContextMenuRegistry>();
    private readonly IKeyboardShortcutService _keyboardShortcutService = Substitute.For<IKeyboardShortcutService>();
    private readonly ILogService _logService = Substitute.For<ILogService>();

    private PluginContext CreateContext(PluginManifest? manifest = null)
    {
        manifest ??= new PluginManifest
        {
            Id = "com.test.plugin",
            Name = "test-plugin",
            Version = "1.0.0",
            EntryPoint = "Test.dll",
            PluginClass = "Test.Plugin",
            Contributes = new PluginContributions
            {
                Sidebar = [new SidebarContribution { Id = "sidebar1", Title = "Test Sidebar", Icon = "icons/panel.png", Order = 50 }],
                BottomPanel = [new PanelContribution { Id = "bottom1", Title = "Test Bottom", Order = 30 }],
                RightPanel = [new PanelContribution { Id = "right1", Title = "Test Right", Order = 40 }],
                StatusBar = [new StatusBarContribution { Id = "status1", Name = "Test Status", Position = "left", Order = 10 }],
                ToolbarButtons = [new ToolbarButtonContribution { Id = "btn1", Tooltip = "Test Btn", Icon = "icons/btn.png", Order = 20 }],
                ContextMenu = [new ContextMenuContribution { Id = "ctx1", Label = "Test Action", FileExtensions = [".mp4"], Order = 60 }]
            }
        };

        return new PluginContext(
            manifest,
            @"C:\plugins\test-plugin",
            Substitute.For<IEventBus>(),
            Substitute.For<IVideoEngine>(),
            _logService,
            Substitute.For<IPluginSettingsStore>(),
            _contributions,
            _contextMenuRegistry,
            _keyboardShortcutService);
    }

    [Fact]
    public void Properties_ExposeInjectedServices()
    {
        var context = CreateContext();

        Assert.NotNull(context.Manifest);
        Assert.Equal("com.test.plugin", context.Manifest.Id);
        Assert.Equal(@"C:\plugins\test-plugin", context.PluginDirectory);
        Assert.NotNull(context.Events);
        Assert.NotNull(context.VideoEngine);
        Assert.NotNull(context.Logger);
        Assert.NotNull(context.Settings);
    }

    [Fact]
    public void RegisterSidebarPanel_UsesManifestMetadata()
    {
        var context = CreateContext();

        context.RegisterSidebarPanel("sidebar1", () => "panel-view");

        var panels = _contributions.GetSidebarPanels();
        Assert.Single(panels);
        Assert.Equal("com.test.plugin", panels[0].PluginId);
        Assert.Equal("sidebar1", panels[0].ContributionId);
        Assert.Equal("Test Sidebar", panels[0].Title);
        Assert.Equal(@"C:\plugins\test-plugin\icons/panel.png", panels[0].IconPath);
        Assert.Equal(50, panels[0].Order);
    }

    [Fact]
    public void RegisterSidebarPanel_UnknownContribution_UsesDefaults()
    {
        var context = CreateContext();

        context.RegisterSidebarPanel("unknown", () => "view");

        var panels = _contributions.GetSidebarPanels();
        Assert.Single(panels);
        Assert.Equal("unknown", panels[0].Title);
        Assert.Null(panels[0].IconPath);
        Assert.Equal(100, panels[0].Order);
    }

    [Fact]
    public void RegisterBottomPanel_UsesManifestMetadata()
    {
        var context = CreateContext();

        context.RegisterBottomPanel("bottom1", () => "view");

        var panels = _contributions.GetBottomPanels();
        Assert.Single(panels);
        Assert.Equal("Test Bottom", panels[0].Title);
        Assert.Equal(30, panels[0].Order);
    }

    [Fact]
    public void RegisterRightPanel_UsesManifestMetadata()
    {
        var context = CreateContext();

        context.RegisterRightPanel("right1", () => "view");

        var panels = _contributions.GetRightPanels();
        Assert.Single(panels);
        Assert.Equal("Test Right", panels[0].Title);
        Assert.Equal(40, panels[0].Order);
    }

    [Fact]
    public void RegisterStatusBarItem_UsesManifestMetadata()
    {
        var context = CreateContext();

        context.RegisterStatusBarItem("status1", () => "item");

        var items = _contributions.GetStatusBarItems();
        Assert.Single(items);
        Assert.Equal("left", items[0].Position);
        Assert.Equal(10, items[0].Order);
    }

    [Fact]
    public void RegisterToolbarButtonHandler_UsesManifestMetadata()
    {
        var context = CreateContext();
        var clicked = false;

        context.RegisterToolbarButtonHandler("btn1", () => clicked = true);

        var buttons = _contributions.GetToolbarButtons();
        Assert.Single(buttons);
        Assert.Equal("Test Btn", buttons[0].Tooltip);
        Assert.Equal(@"C:\plugins\test-plugin\icons/btn.png", buttons[0].IconPath);

        buttons[0].ClickHandler();
        Assert.True(clicked);
    }

    [Fact]
    public void RegisterContextMenuHandler_RegistersInBothRegistries()
    {
        var context = CreateContext();
        var handled = false;

        context.RegisterContextMenuHandler("ctx1", _ => handled = true);

        // Verify ContributionRegistry
        var items = _contributions.GetContextMenuItems();
        Assert.Single(items);
        Assert.Equal("Test Action", items[0].Label);

        items[0].Handler(new FileNode("test.mp4", false));
        Assert.True(handled);

        // Verify existing ContextMenuRegistry was also called
        _contextMenuRegistry.Received(1).Register(
            Arg.Is<ContextMenuEntry>(e => e.Id == "plugin.com.test.plugin.ctx1"));
    }

    [Fact]
    public void RegisterFileHandler_DelegatesToContributions()
    {
        var context = CreateContext();

        context.RegisterFileHandler([".special", ".custom"], _ => { });

        var handlers = _contributions.GetFileHandlers();
        Assert.Single(handlers);
        Assert.Equal("com.test.plugin", handlers[0].PluginId);
        Assert.Contains(".special", handlers[0].Extensions);
    }

    [Fact]
    public void RegisterFileIcons_ResolvesRelativePaths()
    {
        var context = CreateContext();

        context.RegisterFileIcons(new Dictionary<string, string>
        {
            [".abc"] = "icons/abc.png"
        });

        var icons = _contributions.GetFileIcons();
        Assert.Single(icons);
        Assert.Equal(Path.Combine(@"C:\plugins\test-plugin", "icons/abc.png"), icons[".abc"]);
    }

    [Fact]
    public void RegisterKeyBinding_DelegatesToService()
    {
        var context = CreateContext();
        _keyboardShortcutService.Register(Arg.Any<KeyBinding>(), Arg.Any<string>(), Arg.Any<Action>())
            .Returns(true);

        context.RegisterKeyBinding(new KeyBinding("F5"), () => { });

        _keyboardShortcutService.Received(1).Register(
            Arg.Is<KeyBinding>(b => b.Key == "F5"),
            Arg.Is<string>(s => s.StartsWith("plugin.com.test.plugin.")),
            Arg.Any<Action>());
    }

    [Fact]
    public void RegisterKeyBinding_FailedRegistration_LogsWarning()
    {
        var context = CreateContext();
        _keyboardShortcutService.Register(Arg.Any<KeyBinding>(), Arg.Any<string>(), Arg.Any<Action>())
            .Returns(false);

        context.RegisterKeyBinding(new KeyBinding("F5"), () => { });

        _logService.Received().Warning(
            Arg.Is<string>(s => s.Contains("failed")),
            "PluginHost");
    }

    [Fact]
    public void Cleanup_UnregistersContextMenuItems()
    {
        var context = CreateContext();

        context.RegisterContextMenuHandler("ctx1", _ => { });
        context.Cleanup();

        _contextMenuRegistry.Received(1).Unregister("plugin.com.test.plugin.ctx1");
    }

    [Fact]
    public void Cleanup_UnregistersKeyBindings()
    {
        var context = CreateContext();
        _keyboardShortcutService.Register(Arg.Any<KeyBinding>(), Arg.Any<string>(), Arg.Any<Action>())
            .Returns(true);

        context.RegisterKeyBinding(new KeyBinding("F5"), () => { });
        context.Cleanup();

        _keyboardShortcutService.Received(1).Unregister(
            Arg.Is<string>(s => s.StartsWith("plugin.com.test.plugin.")));
    }

    [Fact]
    public void Cleanup_UnregistersAllContributions()
    {
        var context = CreateContext();
        _keyboardShortcutService.Register(Arg.Any<KeyBinding>(), Arg.Any<string>(), Arg.Any<Action>())
            .Returns(true);

        context.RegisterSidebarPanel("sidebar1", () => "v");
        context.RegisterBottomPanel("bottom1", () => "v");
        context.RegisterStatusBarItem("status1", () => "v");
        context.RegisterKeyBinding(new KeyBinding("F5"), () => { });

        context.Cleanup();

        Assert.Empty(_contributions.GetSidebarPanels());
        Assert.Empty(_contributions.GetBottomPanels());
        Assert.Empty(_contributions.GetStatusBarItems());
    }

    // ── Input validation ──

    [Fact]
    public void RegisterSidebarPanel_NullId_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterSidebarPanel(null!, () => "v"));
    }

    [Fact]
    public void RegisterSidebarPanel_EmptyId_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentException>(() => ctx.RegisterSidebarPanel("", () => "v"));
    }

    [Fact]
    public void RegisterSidebarPanel_NullFactory_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterSidebarPanel("sidebar1", null!));
    }

    [Fact]
    public void RegisterBottomPanel_NullFactory_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterBottomPanel("bottom1", null!));
    }

    [Fact]
    public void RegisterRightPanel_EmptyId_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentException>(() => ctx.RegisterRightPanel(" ", () => "v"));
    }

    [Fact]
    public void RegisterStatusBarItem_NullFactory_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterStatusBarItem("status1", null!));
    }

    [Fact]
    public void RegisterToolbarButtonHandler_NullHandler_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterToolbarButtonHandler("btn1", null!));
    }

    [Fact]
    public void RegisterContextMenuHandler_NullHandler_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterContextMenuHandler("ctx1", null!));
    }

    [Fact]
    public void RegisterFileHandler_EmptyExtensions_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentException>(() => ctx.RegisterFileHandler([], _ => { }));
    }

    [Fact]
    public void RegisterFileHandler_NullHandler_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterFileHandler([".txt"], null!));
    }

    [Fact]
    public void RegisterFileIcons_EmptyDict_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentException>(() => ctx.RegisterFileIcons(new Dictionary<string, string>()));
    }

    [Fact]
    public void RegisterKeyBinding_NullBinding_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterKeyBinding(null!, () => { }));
    }

    [Fact]
    public void RegisterKeyBinding_NullHandler_Throws()
    {
        var ctx = CreateContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterKeyBinding(new KeyBinding("F5"), null!));
    }
}
