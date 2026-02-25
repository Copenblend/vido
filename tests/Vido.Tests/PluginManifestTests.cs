using System.Text.Json;
using Vido.Core.Plugin;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginManifest"/> JSON deserialization.
/// Verifies that plugin.json files are correctly parsed into the manifest model.
/// </summary>
public class PluginManifestTests
{
    [Fact]
    public void Deserialize_MinimalManifest_SetsRequiredFields()
    {
        var json = """
        {
            "id": "com.example.test",
            "name": "test-plugin",
            "displayName": "Test Plugin",
            "version": "1.0.0",
            "description": "A test plugin",
            "author": "Test Author",
            "license": "MIT",
            "entryPoint": "TestPlugin.dll",
            "pluginClass": "TestPlugin.Plugin"
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("com.example.test", manifest.Id);
        Assert.Equal("test-plugin", manifest.Name);
        Assert.Equal("Test Plugin", manifest.DisplayName);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("A test plugin", manifest.Description);
        Assert.Equal("Test Author", manifest.Author);
        Assert.Equal("MIT", manifest.License);
        Assert.Equal("TestPlugin.dll", manifest.EntryPoint);
        Assert.Equal("TestPlugin.Plugin", manifest.PluginClass);
    }

    [Fact]
    public void Deserialize_OptionalFields_DefaultCorrectly()
    {
        var json = """
        {
            "id": "com.example.test",
            "name": "test-plugin",
            "version": "1.0.0",
            "entryPoint": "TestPlugin.dll",
            "pluginClass": "TestPlugin.Plugin"
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Null(manifest.Repository);
        Assert.Empty(manifest.Tags);
        Assert.NotNull(manifest.Contributes);
        Assert.Empty(manifest.Contributes.Sidebar);
        Assert.Empty(manifest.Contributes.BottomPanel);
        Assert.Empty(manifest.Contributes.StatusBar);
        Assert.Empty(manifest.Contributes.FileIcons);
    }

    [Fact]
    public void Deserialize_FullManifest_AllContributionsParsed()
    {
        var json = """
        {
            "id": "com.example.full",
            "name": "full-plugin",
            "displayName": "Full Plugin",
            "version": "2.1.0",
            "description": "Full featured plugin",
            "author": "Author",
            "license": "MIT",
            "entryPoint": "Full.dll",
            "pluginClass": "Full.MyPlugin",
            "minVidoVersion": "1.0.0",
            "repository": "https://github.com/example/full",
            "tags": ["video", "tools"],
            "contributes": {
                "sidebar": [
                    { "id": "panel1", "title": "My Panel", "icon": "icons/panel.png", "order": 50 }
                ],
                "bottomPanel": [
                    { "id": "output1", "title": "My Output", "order": 10 }
                ],
                "rightPanel": [
                    { "id": "info1", "title": "Extra Info", "order": 20 }
                ],
                "statusBar": [
                    { "id": "status1", "position": "left", "order": 5 }
                ],
                "toolbarButtons": [
                    { "id": "btn1", "tooltip": "Do something", "icon": "icons/btn.png", "order": 1 }
                ],
                "fileIcons": {
                    ".custom": "icons/custom.png"
                },
                "contextMenu": [
                    { "id": "ctx1", "label": "Custom Action", "fileExtensions": [".mp4"], "order": 50 }
                ],
                "fileHandlers": [
                    { "extensions": [".special"], "action": "open" }
                ],
                "settings": [
                    { "id": "autoPlay", "type": "boolean", "default": true, "title": "Auto Play", "description": "Auto play on load" }
                ]
            }
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("com.example.full", manifest.Id);
        Assert.Equal("https://github.com/example/full", manifest.Repository);
        Assert.Equal(2, manifest.Tags.Count);

        // Sidebar
        Assert.Single(manifest.Contributes.Sidebar);
        Assert.Equal("panel1", manifest.Contributes.Sidebar[0].Id);
        Assert.Equal("My Panel", manifest.Contributes.Sidebar[0].Title);
        Assert.Equal("icons/panel.png", manifest.Contributes.Sidebar[0].Icon);
        Assert.Equal(50, manifest.Contributes.Sidebar[0].Order);

        // Bottom panel
        Assert.Single(manifest.Contributes.BottomPanel);
        Assert.Equal("output1", manifest.Contributes.BottomPanel[0].Id);
        Assert.Equal(10, manifest.Contributes.BottomPanel[0].Order);

        // Right panel
        Assert.Single(manifest.Contributes.RightPanel);
        Assert.Equal("info1", manifest.Contributes.RightPanel[0].Id);

        // Status bar
        Assert.Single(manifest.Contributes.StatusBar);
        Assert.Equal("status1", manifest.Contributes.StatusBar[0].Id);
        Assert.Equal("left", manifest.Contributes.StatusBar[0].Position);
        Assert.Equal(5, manifest.Contributes.StatusBar[0].Order);

        // Toolbar buttons
        Assert.Single(manifest.Contributes.ToolbarButtons);
        Assert.Equal("btn1", manifest.Contributes.ToolbarButtons[0].Id);

        // File icons
        Assert.Single(manifest.Contributes.FileIcons);
        Assert.Equal("icons/custom.png", manifest.Contributes.FileIcons[".custom"]);

        // Context menu
        Assert.Single(manifest.Contributes.ContextMenu);
        Assert.Equal("ctx1", manifest.Contributes.ContextMenu[0].Id);
        Assert.Equal("Custom Action", manifest.Contributes.ContextMenu[0].Label);
        Assert.Single(manifest.Contributes.ContextMenu[0].FileExtensions);

        // File handlers
        Assert.Single(manifest.Contributes.FileHandlers);
        Assert.Equal("open", manifest.Contributes.FileHandlers[0].Action);

        // Settings
        Assert.Single(manifest.Contributes.Settings);
        Assert.Equal("autoPlay", manifest.Contributes.Settings[0].Id);
        Assert.Equal("boolean", manifest.Contributes.Settings[0].Type);
    }

    [Fact]
    public void Deserialize_FileIcons_CaseInsensitiveKeys()
    {
        var json = """
        {
            "id": "test",
            "name": "test",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
            "contributes": {
                "fileIcons": {
                    ".MP4": "icons/mp4.png",
                    ".mkv": "icons/mkv.png"
                }
            }
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Contributes.FileIcons.Count);
    }

    [Fact]
    public void Deserialize_EmptyContributes_DefaultsToEmptyCollections()
    {
        var json = """
        {
            "id": "test",
            "name": "test",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
            "contributes": {}
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Empty(manifest.Contributes.Sidebar);
        Assert.Empty(manifest.Contributes.BottomPanel);
        Assert.Empty(manifest.Contributes.RightPanel);
        Assert.Empty(manifest.Contributes.StatusBar);
        Assert.Empty(manifest.Contributes.ToolbarButtons);
        Assert.Empty(manifest.Contributes.FileIcons);
        Assert.Empty(manifest.Contributes.ContextMenu);
        Assert.Empty(manifest.Contributes.FileHandlers);
        Assert.Empty(manifest.Contributes.Settings);
    }

    [Fact]
    public void Roundtrip_ManifestSerializesAndDeserializes()
    {
        var manifest = new PluginManifest
        {
            Id = "com.roundtrip.test",
            Name = "roundtrip",
            DisplayName = "Roundtrip Test",
            Version = "3.0.0",
            EntryPoint = "Roundtrip.dll",
            PluginClass = "Roundtrip.Plugin",
            Tags = ["tag1", "tag2"],
            Contributes = new PluginContributions
            {
                Sidebar = [new SidebarContribution { Id = "s1", Title = "Sidebar 1", Order = 10 }],
                StatusBar = [new StatusBarContribution { Id = "sb1", Position = "left", Order = 5 }]
            }
        };

        var json = JsonSerializer.Serialize(manifest);
        var deserialized = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(manifest.Id, deserialized.Id);
        Assert.Equal(manifest.Tags.Count, deserialized.Tags.Count);
        Assert.Single(deserialized.Contributes.Sidebar);
        Assert.Equal("s1", deserialized.Contributes.Sidebar[0].Id);
        Assert.Single(deserialized.Contributes.StatusBar);
        Assert.Equal("left", deserialized.Contributes.StatusBar[0].Position);
    }

    [Fact]
    public void ContributionDefaults_OrderIs100_PositionIsRight()
    {
        var sidebar = new SidebarContribution();
        Assert.Equal(100, sidebar.Order);

        var status = new StatusBarContribution();
        Assert.Equal("right", status.Position);
        Assert.Equal(100, status.Order);

        var panel = new PanelContribution();
        Assert.Equal(100, panel.Order);

        var toolbar = new ToolbarButtonContribution();
        Assert.Equal(100, toolbar.Order);

        var contextMenu = new ContextMenuContribution();
        Assert.Equal(100, contextMenu.Order);

        var fileHandler = new FileHandlerContribution();
        Assert.Equal("open", fileHandler.Action);
    }

    // ── Dependency Deserialization Tests ──

    [Fact]
    public void Deserialize_WithDependencies_ParsesAll()
    {
        var json = """
        {
            "id": "com.test.with-deps",
            "name": "deps-plugin",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
            "dependencies": [
                { "id": "com.vido.osr2-plus", "minVersion": "4.0.0" },
                { "id": "com.vido.other", "minVersion": "1.2.3" }
            ]
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Dependencies.Count);
        Assert.Equal("com.vido.osr2-plus", manifest.Dependencies[0].Id);
        Assert.Equal("4.0.0", manifest.Dependencies[0].MinVersion);
        Assert.Equal("com.vido.other", manifest.Dependencies[1].Id);
        Assert.Equal("1.2.3", manifest.Dependencies[1].MinVersion);
    }

    [Fact]
    public void Deserialize_WithoutDependencies_DefaultsToEmptyList()
    {
        var json = """
        {
            "id": "com.test.no-deps",
            "name": "no-deps",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin"
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.NotNull(manifest.Dependencies);
        Assert.Empty(manifest.Dependencies);
    }

    [Fact]
    public void Deserialize_EmptyDependencies_ParsesAsEmptyList()
    {
        var json = """
        {
            "id": "com.test.empty-deps",
            "name": "empty-deps",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
            "dependencies": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.NotNull(manifest.Dependencies);
        Assert.Empty(manifest.Dependencies);
    }

    [Fact]
    public void PluginDependency_DefaultValues_AreEmptyStrings()
    {
        var dep = new PluginDependency();

        Assert.Equal(string.Empty, dep.Id);
        Assert.Equal(string.Empty, dep.MinVersion);
    }
}
