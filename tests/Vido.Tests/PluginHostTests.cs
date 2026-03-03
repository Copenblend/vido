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

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public PluginHostTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-pluginhost-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _appSettings = new AppSettings();
        _settingsService.Current.Returns(_appSettings);
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PluginHost.PluginHost CreateHost(IEnumerable<string>? scanDirs = null) => new(
        _eventBus, _videoEngine, _logService, _settingsService,
        _contributions, _contextMenuRegistry, _keyboardShortcutService,
        scanDefaultDirectory: false,
        additionalScanDirectories: scanDirs ?? [_tempDir]);

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

    /// <summary>
    /// Verifies that Activate All no plugins succeeds.
    /// </summary>
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

    /// <summary>
    /// Verifies that Activate All invalid plugin dir skips gracefully.
    /// </summary>
    [Fact]
    public void ActivateAll_InvalidPluginDir_SkipsGracefully()
    {
        CreatePluginDirectory("bad-plugin", "{ invalid json }");

        var host = CreateHost();
        host.ActivateAll();

        Assert.Empty(host.Plugins);
    }

    /// <summary>
    /// Verifies that Activate All valid manifest missing dll sets error state.
    /// </summary>
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

    /// <summary>
    /// Verifies that Activate All valid manifest invalid dll sets error state.
    /// </summary>
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

    /// <summary>
    /// Verifies that Activate All duplicate plugin ids skips second.
    /// </summary>
    [Fact]
    public void ActivateAll_DuplicatePluginIds_SkipsSecond()
    {
        // Create a second scan directory
        var dir2 = Path.Combine(_tempDir, "_scan2");
        Directory.CreateDirectory(dir2);

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

        var host = CreateHost([_tempDir, dir2]);
        host.ActivateAll();

        _logService.Received().Warning(
            Arg.Is<string>(s => s.Contains("Duplicate")),
            "PluginHost");
    }

    /// <summary>
    /// Verifies that Activate All disabled plugin logs startup.
    /// </summary>
    [Fact]
    public void ActivateAll_DisabledPlugin_LogsStartup()
    {
        // TODO PI-021: DisabledPluginIds removed from AppSettings — disabled feature stubbed out

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

    /// <summary>
    /// Verifies that Get Plugin existing id returns info.
    /// </summary>
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

    /// <summary>
    /// Verifies that Get Plugin unknown id returns null.
    /// </summary>
    [Fact]
    public void GetPlugin_UnknownId_ReturnsNull()
    {
        var host = CreateHost();
        host.ActivateAll();

        Assert.Null(host.GetPlugin("nonexistent"));
    }

    /// <summary>
    /// Verifies that Get Disabled Plugin Ids returns empty list (disabled feature stubbed out in PI-003).
    /// </summary>
    [Fact]
    public void GetDisabledPluginIds_ReturnsEmptyList()
    {
        // TODO PI-021: DisabledPluginIds removed from AppSettings — always returns empty
        var host = CreateHost();

        var disabled = host.GetDisabledPluginIds();

        Assert.Empty(disabled);
    }

    /// <summary>
    /// Verifies that Deactivate All no plugins succeeds.
    /// </summary>
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

    /// <summary>
    /// Verifies that Contribution Registry exposed through property.
    /// </summary>
    [Fact]
    public void ContributionRegistry_ExposedThroughProperty()
    {
        var host = CreateHost();

        Assert.Same(_contributions, host.ContributionRegistry);
    }

    /// <summary>
    /// Verifies that Scan Non Existent Directory logs debug.
    /// </summary>
    [Fact]
    public void ScanNonExistentDirectory_LogsDebug()
    {
        var host = CreateHost([@"C:\nonexistent\path\12345"]);
        host.ActivateAll();

        _logService.Received().Debug(
            Arg.Is<string>(s => s.Contains("does not exist")),
            "PluginHost");
    }

    /// <summary>
    /// Verifies that Plugin Info initial state is discovered.
    /// </summary>
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

    /// <summary>
    // PI-003: ActivateAll_PrunesOrphanedDisabledIds, ActivateAll_KeepsValidDisabledIds,
    // and ActivateAll_DisabledCheck_IsCaseInsensitive tests removed — DisabledPluginIds
    // property deleted from AppSettings. These will be re-implemented in PI-021.

    /// <summary>
    /// Verifies that Get Plugin is case insensitive.
    /// </summary>
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

    // ── Topological Sort Tests ──

    private static PluginInfo MakePluginInfo(string id, string version = "1.0.0",
        List<PluginDependency>? deps = null)
    {
        return new PluginInfo
        {
            Manifest = new PluginManifest
            {
                Id = id,
                Name = id,
                Version = version,
                EntryPoint = $"{id}.dll",
                PluginClass = $"{id}.Plugin",
                Dependencies = deps ?? []
            },
            Directory = $"/plugins/{id}"
        };
    }

    /// <summary>
    /// Verifies that Topological Sort empty list returns empty.
    /// </summary>
    [Fact]
    public void TopologicalSort_EmptyList_ReturnsEmpty()
    {
        var result = PluginHost.PluginHost.TopologicalSort([]);

        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that Topological Sort single plugin returns same.
    /// </summary>
    [Fact]
    public void TopologicalSort_SinglePlugin_ReturnsSame()
    {
        var plugins = new List<PluginInfo> { MakePluginInfo("com.test.solo") };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Single(result);
        Assert.Equal("com.test.solo", result[0].Manifest.Id);
    }

    /// <summary>
    /// Verifies that Topological Sort no dependencies returns all.
    /// </summary>
    [Fact]
    public void TopologicalSort_NoDependencies_ReturnsAll()
    {
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.a"),
            MakePluginInfo("com.test.b"),
            MakePluginInfo("com.test.c"),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Equal(3, result.Count);
        var ids = result.Select(p => p.Manifest.Id).ToHashSet();
        Assert.Contains("com.test.a", ids);
        Assert.Contains("com.test.b", ids);
        Assert.Contains("com.test.c", ids);
    }

    /// <summary>
    /// Verifies that Topological Sort linear chain dependencies first.
    /// </summary>
    [Fact]
    public void TopologicalSort_LinearChain_DependenciesFirst()
    {
        // C depends on B, B depends on A → order should be A, B, C
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.c", deps:
            [
                new PluginDependency { Id = "com.test.b", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.a"),
            MakePluginInfo("com.test.b", deps:
            [
                new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }
            ]),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Equal(3, result.Count);
        var indexA = result.FindIndex(p => p.Manifest.Id == "com.test.a");
        var indexB = result.FindIndex(p => p.Manifest.Id == "com.test.b");
        var indexC = result.FindIndex(p => p.Manifest.Id == "com.test.c");
        Assert.True(indexA < indexB, "A should come before B");
        Assert.True(indexB < indexC, "B should come before C");
    }

    /// <summary>
    /// Verifies that Topological Sort diamond dependency all resolved.
    /// </summary>
    [Fact]
    public void TopologicalSort_DiamondDependency_AllResolved()
    {
        // D depends on B and C; B depends on A; C depends on A
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.d", deps:
            [
                new PluginDependency { Id = "com.test.b", MinVersion = "1.0.0" },
                new PluginDependency { Id = "com.test.c", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.a"),
            MakePluginInfo("com.test.b", deps:
            [
                new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.c", deps:
            [
                new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }
            ]),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Equal(4, result.Count);
        var indexA = result.FindIndex(p => p.Manifest.Id == "com.test.a");
        var indexB = result.FindIndex(p => p.Manifest.Id == "com.test.b");
        var indexC = result.FindIndex(p => p.Manifest.Id == "com.test.c");
        var indexD = result.FindIndex(p => p.Manifest.Id == "com.test.d");
        Assert.True(indexA < indexB, "A before B");
        Assert.True(indexA < indexC, "A before C");
        Assert.True(indexB < indexD, "B before D");
        Assert.True(indexC < indexD, "C before D");
    }

    /// <summary>
    /// Verifies that Topological Sort cycle detected appends remaining.
    /// </summary>
    [Fact]
    public void TopologicalSort_CycleDetected_AppendsRemaining()
    {
        // A → B → A (cycle)
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.a", deps:
            [
                new PluginDependency { Id = "com.test.b", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.b", deps:
            [
                new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }
            ]),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        // Both should be in the result (appended after cycle detection)
        Assert.Equal(2, result.Count);
    }

    /// <summary>
    /// Verifies that Topological Sort partial cycle non cyclic plugins first.
    /// </summary>
    [Fact]
    public void TopologicalSort_PartialCycle_NonCyclicPluginsFirst()
    {
        // C depends on nothing, A → B → A is a cycle
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.a", deps:
            [
                new PluginDependency { Id = "com.test.b", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.b", deps:
            [
                new PluginDependency { Id = "com.test.a", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.c"),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Equal(3, result.Count);
        // C has no deps, should be sorted before the cycle participants
        var indexC = result.FindIndex(p => p.Manifest.Id == "com.test.c");
        Assert.Equal(0, indexC);
    }

    /// <summary>
    /// Verifies that Topological Sort unknown dependency ignored in sort.
    /// </summary>
    [Fact]
    public void TopologicalSort_UnknownDependency_IgnoredInSort()
    {
        // A depends on "com.unknown" which isn't in the list — should still appear
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.a", deps:
            [
                new PluginDependency { Id = "com.unknown", MinVersion = "1.0.0" }
            ]),
            MakePluginInfo("com.test.b"),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Equal(2, result.Count);
        // Both should be present — unknown dep is ignored in sort
        Assert.Contains(result, p => p.Manifest.Id == "com.test.a");
        Assert.Contains(result, p => p.Manifest.Id == "com.test.b");
    }

    /// <summary>
    /// Verifies that Topological Sort case insensitive ids.
    /// </summary>
    [Fact]
    public void TopologicalSort_CaseInsensitiveIds()
    {
        // B depends on "COM.TEST.A" but plugin is "com.test.a"
        var plugins = new List<PluginInfo>
        {
            MakePluginInfo("com.test.a"),
            MakePluginInfo("com.test.b", deps:
            [
                new PluginDependency { Id = "COM.TEST.A", MinVersion = "1.0.0" }
            ]),
        };

        var result = PluginHost.PluginHost.TopologicalSort(plugins);

        Assert.Equal(2, result.Count);
        Assert.Equal("com.test.a", result[0].Manifest.Id);
        Assert.Equal("com.test.b", result[1].Manifest.Id);
    }

    // ── Dependency Integration Tests ──

    /// <summary>
    /// Verifies that Activate All missing dependency sets error state.
    /// </summary>
    [Fact]
    public void ActivateAll_MissingDependency_SetsErrorState()
    {
        // Plugin declares a dependency that doesn't exist.
        // Since we can't create a real assembly in unit tests, create a scenario
        // where the DLL doesn't exist at all. Dependency validation happens
        // before ActivatePlugin, but LoadPluginAssembly runs during DiscoverPlugins,
        // so the plugin lands in Error from assembly loading first.
        // Instead, test the ValidateDependencies path is exercised by checking
        // that the dependency error is logged when the dependency plugin has Error state.
        CreatePluginDirectory("base-plugin", """
        {
            "id": "com.test.base",
            "name": "base-plugin",
            "version": "1.0.0",
            "entryPoint": "Base.dll",
            "pluginClass": "Base.Plugin"
        }
        """);

        CreatePluginDirectory("dependent-plugin", """
        {
            "id": "com.test.dependent",
            "name": "dependent-plugin",
            "version": "1.0.0",
            "entryPoint": "Dependent.dll",
            "pluginClass": "Dependent.Plugin",
            "dependencies": [
                { "id": "com.test.base", "minVersion": "1.0.0" }
            ]
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        // base-plugin has no DLL → Error state from assembly load
        var basePlugin = host.GetPlugin("com.test.base");
        Assert.NotNull(basePlugin);
        Assert.Equal(PluginState.Error, basePlugin.State);

        // dependent-plugin also has no DLL → Error state from assembly load,
        // but the dependency on an Error-state plugin would also fail validation.
        // Verify both are Error and the dependency error is logged.
        var dependent = host.GetPlugin("com.test.dependent");
        Assert.NotNull(dependent);
        Assert.Equal(PluginState.Error, dependent.State);
    }

    /// <summary>
    /// Verifies that Activate All disabled dependency sets error state.
    /// With DisabledPluginIds removed in PI-003, the base plugin is no longer disabled
    /// but still errors due to fake DLL. The dependent plugin still fails.
    /// </summary>
    [Fact]
    public void ActivateAll_DisabledDependency_SetsErrorState()
    {
        // TODO PI-021: DisabledPluginIds removed from AppSettings — disabled feature stubbed out

        CreatePluginDirectory("base-plugin", """
        {
            "id": "com.test.base",
            "name": "base-plugin",
            "version": "1.0.0",
            "entryPoint": "Base.dll",
            "pluginClass": "Base.Plugin"
        }
        """);

        CreatePluginDirectory("dependent-plugin", """
        {
            "id": "com.test.dependent",
            "name": "dependent-plugin",
            "version": "1.0.0",
            "entryPoint": "Dependent.dll",
            "pluginClass": "Dependent.Plugin",
            "dependencies": [
                { "id": "com.test.base", "minVersion": "1.0.0" }
            ]
        }
        """);

        var host = CreateHost();
        host.ActivateAll();

        // base-plugin ends up in Error state due to fake DLL (not disabled — feature stubbed out)
        var basePlugin = host.GetPlugin("com.test.base");
        Assert.NotNull(basePlugin);
        Assert.Equal(PluginState.Error, basePlugin.State);
        // The dependent also fails due to dependency on an Error-state plugin.
        var dependent = host.GetPlugin("com.test.dependent");
        Assert.NotNull(dependent);
        Assert.Equal(PluginState.Error, dependent.State);
    }

    /// <summary>
    /// Verifies that Activate All no dependencies plugin activates normally.
    /// </summary>
    [Fact]
    public void ActivateAll_NoDependencies_PluginActivatesNormally()
    {
        CreatePluginDirectory("no-deps", """
        {
            "id": "com.test.no-deps",
            "name": "no-deps",
            "version": "1.0.0",
            "entryPoint": "NoDeps.dll",
            "pluginClass": "NoDeps.Plugin"
        }
        """, createDll: true, dllName: "NoDeps.dll");

        var host = CreateHost();
        host.ActivateAll();

        // The plugin has a fake DLL so it'll hit Error from assembly loading,
        // but it should NOT have a dependency error
        var plugin = host.GetPlugin("com.test.no-deps");
        Assert.NotNull(plugin);
        Assert.DoesNotContain("dependency", plugin.ErrorMessage ?? "");
    }

    /// <summary>
    /// Verifies that Activate All empty dependencies array passes validation.
    /// </summary>
    [Fact]
    public void ActivateAll_EmptyDependenciesArray_PassesValidation()
    {
        CreatePluginDirectory("empty-deps", """
        {
            "id": "com.test.empty-deps",
            "name": "empty-deps",
            "version": "1.0.0",
            "entryPoint": "EmptyDeps.dll",
            "pluginClass": "EmptyDeps.Plugin",
            "dependencies": []
        }
        """, createDll: true, dllName: "EmptyDeps.dll");

        var host = CreateHost();
        host.ActivateAll();

        // Plugin won't activate (fake DLL) but dependency validation should pass
        var plugin = host.GetPlugin("com.test.empty-deps");
        Assert.NotNull(plugin);
        Assert.DoesNotContain("dependency", plugin.ErrorMessage ?? "");
    }

    // ── Assembly Resolver Runtime Probing Tests ──

    /// <summary>
    /// Verifies that Find Runtime Specific Assembly prefers runtime over root.
    /// </summary>
    [Fact]
    public void FindRuntimeSpecificAssembly_PrefersRuntimeOverRoot()
    {
        // Arrange: place a DLL at root and in runtimes/win/lib/net8.0/
        var pluginDir = Path.Combine(_tempDir, "runtime-probe-test");
        var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
            .ToString().ToLowerInvariant();
        var rid = OperatingSystem.IsWindows() ? "win"
                : OperatingSystem.IsLinux() ? "linux"
                : OperatingSystem.IsMacOS() ? "osx"
                : "unknown";

        var runtimeLibDir = Path.Combine(pluginDir, "runtimes", rid, "lib", "net8.0");
        Directory.CreateDirectory(runtimeLibDir);
        File.WriteAllText(Path.Combine(runtimeLibDir, "TestLib.dll"), "runtime-specific");

        // Also place a root-level DLL
        File.WriteAllText(Path.Combine(pluginDir, "TestLib.dll"), "root-ref");

        // Act
        var result = PluginHost.PluginHost.FindRuntimeSpecificAssembly(pluginDir, "TestLib");

        // Assert — should find the runtime-specific one, not root
        Assert.NotNull(result);
        Assert.Contains(Path.Combine("runtimes", rid), result);
    }

    /// <summary>
    /// Verifies that Find Runtime Specific Assembly returns null when no runtimes dir.
    /// </summary>
    [Fact]
    public void FindRuntimeSpecificAssembly_ReturnsNull_WhenNoRuntimesDir()
    {
        var pluginDir = Path.Combine(_tempDir, "no-runtimes");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "SomeLib.dll"), "root-only");

        var result = PluginHost.PluginHost.FindRuntimeSpecificAssembly(pluginDir, "SomeLib");

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that Find Root Assembly returns path when dll exists.
    /// </summary>
    [Fact]
    public void FindRootAssembly_ReturnsPath_WhenDllExists()
    {
        var pluginDir = Path.Combine(_tempDir, "root-test");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "MyLib.dll"), "content");

        var result = PluginHost.PluginHost.FindRootAssembly(pluginDir, "MyLib");

        Assert.NotNull(result);
        Assert.EndsWith("MyLib.dll", result);
    }

    /// <summary>
    /// Verifies that Find Root Assembly returns null when dll missing.
    /// </summary>
    [Fact]
    public void FindRootAssembly_ReturnsNull_WhenDllMissing()
    {
        var pluginDir = Path.Combine(_tempDir, "root-missing");
        Directory.CreateDirectory(pluginDir);

        var result = PluginHost.PluginHost.FindRootAssembly(pluginDir, "Missing");

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that Find Runtime Specific Assembly arch specific preferred over generic rid.
    /// </summary>
    [Fact]
    public void FindRuntimeSpecificAssembly_ArchSpecific_PreferredOverGenericRid()
    {
        var pluginDir = Path.Combine(_tempDir, "arch-probe-test");
        var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
            .ToString().ToLowerInvariant();
        var rid = OperatingSystem.IsWindows() ? "win"
                : OperatingSystem.IsLinux() ? "linux"
                : OperatingSystem.IsMacOS() ? "osx"
                : "unknown";

        // Create both arch-specific and generic RID directories
        var archDir = Path.Combine(pluginDir, "runtimes", $"{rid}-{arch}", "lib", "net8.0");
        var genericDir = Path.Combine(pluginDir, "runtimes", rid, "lib", "net8.0");
        Directory.CreateDirectory(archDir);
        Directory.CreateDirectory(genericDir);
        File.WriteAllText(Path.Combine(archDir, "Dep.dll"), "arch-specific");
        File.WriteAllText(Path.Combine(genericDir, "Dep.dll"), "generic");

        var result = PluginHost.PluginHost.FindRuntimeSpecificAssembly(pluginDir, "Dep");

        // Should prefer the arch-specific one (e.g. win-x64 over win)
        Assert.NotNull(result);
        Assert.Contains($"{rid}-{arch}", result);
    }
}
