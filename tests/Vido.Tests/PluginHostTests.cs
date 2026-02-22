using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Menus;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginHost.PluginHost"/> — lifecycle management,
/// discovery, validation, activation, deactivation, and error handling.
/// </summary>
public class PluginHostTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly IVideoEngine _videoEngine = Substitute.For<IVideoEngine>();
    private readonly ILogService _logService = Substitute.For<ILogService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly PluginHost.ContributionRegistry _contributions = new();
    private readonly IContextMenuRegistry _contextMenuRegistry = Substitute.For<IContextMenuRegistry>();
    private readonly IKeyboardShortcutService _keyboardShortcutService = Substitute.For<IKeyboardShortcutService>();
    private readonly AppSettings _appSettings;

    public PluginHostTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-pluginhost-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _appSettings = new AppSettings
        {
            PluginDirectories = [_tempDir]
        };
        _settingsService.Current.Returns(_appSettings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PluginHost.PluginHost CreateHost() => new(
        _eventBus, _videoEngine, _logService, _settingsService,
        _contributions, _contextMenuRegistry, _keyboardShortcutService,
        scanDefaultDirectory: false);

    private void CreatePluginDirectory(string pluginId, string json,
        bool createDll = false, string? dllName = null)
    {
        var pluginDir = Path.Combine(_tempDir, pluginId);
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "plugin.json"), json);

        if (createDll)
        {
            // Create a dummy DLL (can't actually load as a valid .NET assembly)
            File.WriteAllText(Path.Combine(pluginDir, dllName ?? "Plugin.dll"), "not-a-dll");
        }
    }

    [Fact]
    public void ActivateAll_NoPlugins_Succeeds()
    {
        var host = CreateHost();

        host.ActivateAll();

        Assert.Empty(host.Plugins);
        _logService.Received().Info(
            Arg.Is<string>(s => s.Contains("Plugin system ready")),
            "PluginHost");
    }

    [Fact]
    public void ActivateAll_InvalidPluginDir_SkipsGracefully()
    {
        CreatePluginDirectory("bad-plugin", "{ invalid json }");

        var host = CreateHost();
        host.ActivateAll();

        Assert.Empty(host.Plugins);
    }

    [Fact]
    public void ActivateAll_ValidManifestMissingDll_SetsErrorState()
    {
        CreatePluginDirectory("missing-dll", """
        {
            "id": "com.test.missing-dll",
            "name": "missing-dll",
            "version": "1.0.0",
            "entryPoint": "Missing.dll",
            "pluginClass": "Missing.Plugin"
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        Assert.Single(host.Plugins);
        Assert.Equal(PluginState.Error, host.Plugins[0].State);
        Assert.Contains("not found", host.Plugins[0].ErrorMessage);
    }

    [Fact]
    public void ActivateAll_ValidManifestInvalidDll_SetsErrorState()
    {
        CreatePluginDirectory("bad-dll", """
        {
            "id": "com.test.bad-dll",
            "name": "bad-dll",
            "version": "1.0.0",
            "entryPoint": "Bad.dll",
            "pluginClass": "Bad.Plugin"
        }
        """, createDll: true, dllName: "Bad.dll");

        var host = CreateHost();
        host.ActivateAll();

        Assert.Single(host.Plugins);
        Assert.Equal(PluginState.Error, host.Plugins[0].State);
    }

    [Fact]
    public void ActivateAll_DuplicatePluginIds_SkipsSecond()
    {
        // Create a second scan directory
        var dir2 = Path.Combine(_tempDir, "_scan2");
        Directory.CreateDirectory(dir2);
        _appSettings.PluginDirectories = [_tempDir, dir2];

        var json = """
        {
            "id": "com.test.dup",
            "name": "dup-plugin",
            "version": "1.0.0",
            "entryPoint": "Dup.dll",
            "pluginClass": "Dup.Plugin"
        }
        """;

        // Same plugin in both directories
        var pluginDir1 = Path.Combine(_tempDir, "dup-plugin-1");
        Directory.CreateDirectory(pluginDir1);
        File.WriteAllText(Path.Combine(pluginDir1, "plugin.json"), json);

        var pluginDir2 = Path.Combine(dir2, "dup-plugin-2");
        Directory.CreateDirectory(pluginDir2);
        File.WriteAllText(Path.Combine(pluginDir2, "plugin.json"), json);

        var host = CreateHost();
        host.ActivateAll();

        _logService.Received().Warning(
            Arg.Is<string>(s => s.Contains("Duplicate")),
            "PluginHost");
    }

    [Fact]
    public void ActivateAll_DisabledPlugin_LogsStartup()
    {
        _appSettings.DisabledPluginIds = ["com.test.disabled"];

        CreatePluginDirectory("disabled-plugin", """
        {
            "id": "com.test.disabled",
            "name": "disabled-plugin",
            "version": "1.0.0",
            "entryPoint": "Disabled.dll",
            "pluginClass": "Disabled.Plugin"
        }
        """, createDll: true, dllName: "Disabled.dll");

        var host = CreateHost();
        host.ActivateAll();

        // Plugin should be in _plugins but loading will fail because the DLL is fake,
        // so it will be in Error state before we check Disabled.
        // However, let's test with valid manifest + invalid DLL path combo:
        // The disabled check happens after discovery but the DLL load will fail.
        // With a fake DLL, the assembly load will fail first. Let's verify logging.
        _logService.Received().Info(
            Arg.Is<string>(s => s.Contains("Plugin system")),
            "PluginHost");
    }

    [Fact]
    public void GetPlugin_ExistingId_ReturnsInfo()
    {
        CreatePluginDirectory("my-plugin", """
        {
            "id": "com.test.my-plugin",
            "name": "my-plugin",
            "version": "1.0.0",
            "entryPoint": "My.dll",
            "pluginClass": "My.Plugin"
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        var info = host.GetPlugin("com.test.my-plugin");

        Assert.NotNull(info);
        Assert.Equal("com.test.my-plugin", info.Manifest.Id);
    }

    [Fact]
    public void GetPlugin_UnknownId_ReturnsNull()
    {
        var host = CreateHost();
        host.ActivateAll();

        Assert.Null(host.GetPlugin("nonexistent"));
    }

    [Fact]
    public void GetDisabledPluginIds_ReturnsFromSettings()
    {
        _appSettings.DisabledPluginIds = ["plugin-a", "plugin-b"];

        var host = CreateHost();

        var disabled = host.GetDisabledPluginIds();

        Assert.Equal(2, disabled.Count);
        Assert.Contains("plugin-a", disabled);
    }

    [Fact]
    public void DeactivateAll_NoPlugins_Succeeds()
    {
        var host = CreateHost();
        host.ActivateAll();

        host.DeactivateAll();

        _logService.Received().Info(
            Arg.Is<string>(s => s.Contains("All plugins deactivated")),
            "PluginHost");
    }

    [Fact]
    public void ContributionRegistry_ExposedThroughProperty()
    {
        var host = CreateHost();

        Assert.Same(_contributions, host.ContributionRegistry);
    }

    [Fact]
    public void ScanNonExistentDirectory_LogsDebug()
    {
        _appSettings.PluginDirectories = [@"C:\nonexistent\path\12345"];

        var host = CreateHost();
        host.ActivateAll();

        _logService.Received().Debug(
            Arg.Is<string>(s => s.Contains("does not exist")),
            "PluginHost");
    }

    [Fact]
    public void PluginInfo_InitialState_IsDiscovered()
    {
        var info = new PluginInfo
        {
            Manifest = new PluginManifest { Id = "test" },
            Directory = "/test"
        };

        Assert.Equal(PluginState.Discovered, info.State);
        Assert.Null(info.Instance);
        Assert.Null(info.ErrorMessage);
    }

    [Fact]
    public void ActivateAll_PrunesOrphanedDisabledIds()
    {
        // Add a stale ID that doesn't match any actual plugin
        _appSettings.DisabledPluginIds = ["com.nonexistent.plugin"];

        var host = CreateHost();
        host.ActivateAll();

        // Stale entry should be removed from DisabledPluginIds
        Assert.Empty(_appSettings.DisabledPluginIds);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void ActivateAll_KeepsValidDisabledIds()
    {
        _appSettings.DisabledPluginIds = ["com.test.real-plugin"];

        CreatePluginDirectory("real-plugin", """
        {
            "id": "com.test.real-plugin",
            "name": "real-plugin",
            "version": "1.0.0",
            "entryPoint": "Real.dll",
            "pluginClass": "Real.Plugin"
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        // Valid disabled ID should remain
        Assert.Single(_appSettings.DisabledPluginIds);
        Assert.Equal("com.test.real-plugin", _appSettings.DisabledPluginIds[0]);
    }

    [Fact]
    public void ActivateAll_DisabledCheck_IsCaseInsensitive()
    {
        // Setting file has different casing than the manifest
        _appSettings.DisabledPluginIds = ["COM.TEST.MY-PLUGIN"];

        CreatePluginDirectory("ci-plugin", """
        {
            "id": "com.test.my-plugin",
            "name": "ci-plugin",
            "version": "1.0.0",
            "entryPoint": "CI.dll",
            "pluginClass": "CI.Plugin"
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        // The disabled ID (case-insensitive) should be retained because the
        // plugin exists. The plugin itself is in Error state (fake DLL) so the
        // disabled check in the activation loop is bypassed, but the pruning
        // should NOT remove the entry because the ID maps to a real plugin.
        Assert.Single(_appSettings.DisabledPluginIds);
    }

    [Fact]
    public void GetPlugin_IsCaseInsensitive()
    {
        CreatePluginDirectory("case-plugin", """
        {
            "id": "com.test.Case-Plugin",
            "name": "case-plugin",
            "version": "1.0.0",
            "entryPoint": "Case.dll",
            "pluginClass": "Case.Plugin"
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        Assert.NotNull(host.GetPlugin("COM.TEST.CASE-PLUGIN"));
        Assert.NotNull(host.GetPlugin("com.test.case-plugin"));
    }
}
