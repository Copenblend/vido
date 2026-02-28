using NSubstitute;
using Vido.Core.Logging;
using Vido.Core.Plugin;
using Vido.PluginHost;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginManifestLoader"/> — JSON loading and validation.
/// </summary>
public class PluginManifestLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogService _logger;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public PluginManifestLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _logger = Substitute.For<ILogService>();
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreatePluginDir(string json, string dirName = "test-plugin")
    {
        var pluginDir = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "plugin.json"), json);
        return pluginDir;
    }

    /// <summary>
    /// Verifies that Load valid manifest returns manifest.
    /// </summary>
    [Fact]
    public void Load_ValidManifest_ReturnsManifest()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.valid",
            "name": "valid-plugin",
            "version": "1.0.0",
            "entryPoint": "Valid.dll",
            "pluginClass": "Valid.Plugin"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.NotNull(manifest);
        Assert.Equal("com.test.valid", manifest.Id);
        Assert.Equal("Valid.dll", manifest.EntryPoint);
    }

    /// <summary>
    /// Verifies that Load no plugin json returns null.
    /// </summary>
    [Fact]
    public void Load_NoPluginJson_ReturnsNull()
    {
        var dir = Path.Combine(_tempDir, "empty-dir");
        Directory.CreateDirectory(dir);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
    }

    /// <summary>
    /// Verifies that Load malformed json returns null logs error.
    /// </summary>
    [Fact]
    public void Load_MalformedJson_ReturnsNull_LogsError()
    {
        var dir = CreatePluginDir("{ invalid json!! }");

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
        _logger.Received().Error(
            Arg.Is<string>(s => s.Contains("Malformed")),
            "PluginLoader");
    }

    /// <summary>
    /// Verifies that Load missing id returns null logs warning.
    /// </summary>
    [Fact]
    public void Load_MissingId_ReturnsNull_LogsWarning()
    {
        var dir = CreatePluginDir("""
        {
            "name": "no-id",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
        _logger.Received().Warning(
            Arg.Is<string>(s => s.Contains("id")),
            "PluginLoader");
    }

    /// <summary>
    /// Verifies that Load missing entry point returns null.
    /// </summary>
    [Fact]
    public void Load_MissingEntryPoint_ReturnsNull()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.no-entry",
            "name": "no-entry",
            "version": "1.0.0",
            "pluginClass": "Test.Plugin"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
    }

    /// <summary>
    /// Verifies that Load missing plugin class returns null.
    /// </summary>
    [Fact]
    public void Load_MissingPluginClass_ReturnsNull()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.no-class",
            "name": "no-class",
            "version": "1.0.0",
            "entryPoint": "Test.dll"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
    }

    /// <summary>
    /// Verifies that Load invalid plugin id returns null.
    /// </summary>
    [Fact]
    public void Load_InvalidPluginId_ReturnsNull()
    {
        var dir = CreatePluginDir("""
        {
            "id": "Invalid Plugin ID!",
            "name": "bad-id",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
    }

    /// <summary>
    /// Verifies that Load non dll entry point returns null.
    /// </summary>
    [Fact]
    public void Load_NonDllEntryPoint_ReturnsNull()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.bad-entry",
            "name": "bad-entry",
            "version": "1.0.0",
            "entryPoint": "Test.exe",
            "pluginClass": "Test.Plugin"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
    }

    /// <summary>
    /// Verifies that Load json with comments succeeds.
    /// </summary>
    [Fact]
    public void Load_JsonWithComments_Succeeds()
    {
        var dir = CreatePluginDir("""
        {
            // This is a comment
            "id": "com.test.comments",
            "name": "comments",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin"
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.NotNull(manifest);
        Assert.Equal("com.test.comments", manifest.Id);
    }

    /// <summary>
    /// Verifies that Load json with trailing commas succeeds.
    /// </summary>
    [Fact]
    public void Load_JsonWithTrailingCommas_Succeeds()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.commas",
            "name": "commas",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
        }
        """);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.NotNull(manifest);
    }

    /// <summary>
    /// Verifies that Validate valid manifest returns no errors.
    /// </summary>
    [Fact]
    public void Validate_ValidManifest_ReturnsNoErrors()
    {
        var manifest = new Core.Plugin.PluginManifest
        {
            Id = "com.valid.plugin",
            Name = "valid",
            Version = "1.0.0",
            EntryPoint = "Valid.dll",
            PluginClass = "Valid.Plugin"
        };

        var errors = PluginManifestLoader.Validate(manifest);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Verifies that Validate duplicate contribution ids returns error.
    /// </summary>
    [Fact]
    public void Validate_DuplicateContributionIds_ReturnsError()
    {
        var manifest = new Core.Plugin.PluginManifest
        {
            Id = "com.dup.ids",
            Name = "dup",
            Version = "1.0.0",
            EntryPoint = "Dup.dll",
            PluginClass = "Dup.Plugin",
            Contributes = new Core.Plugin.PluginContributions
            {
                Sidebar = [
                    new Core.Plugin.SidebarContribution { Id = "panel1" },
                    new Core.Plugin.SidebarContribution { Id = "panel1" }
                ]
            }
        };

        var errors = PluginManifestLoader.Validate(manifest);

        Assert.Contains(errors, e => e.Contains("Duplicate"));
    }

    /// <summary>
    /// Verifies that Validate missing multiple fields returns multiple errors.
    /// </summary>
    [Fact]
    public void Validate_MissingMultipleFields_ReturnsMultipleErrors()
    {
        var manifest = new Core.Plugin.PluginManifest(); // all defaults are empty

        var errors = PluginManifestLoader.Validate(manifest);

        Assert.True(errors.Count >= 4); // id, name, version, entryPoint, pluginClass
    }

    /// <summary>
    /// Verifies that Validate plugin id with letters digits dots is valid.
    /// </summary>
    [Fact]
    public void Validate_PluginIdWithLettersDigitsDots_IsValid()
    {
        var manifest = new Core.Plugin.PluginManifest
        {
            Id = "com.example.my-plugin.v2",
            Name = "test",
            Version = "1.0.0",
            EntryPoint = "Test.dll",
            PluginClass = "Test.Plugin"
        };

        var errors = PluginManifestLoader.Validate(manifest);

        Assert.Empty(errors);
    }

    // ── Settings validation ──

    private static PluginManifest ValidManifest() => new()
    {
        Id = "com.test.settings",
        Name = "test",
        Version = "1.0.0",
        EntryPoint = "Test.dll",
        PluginClass = "Test.Plugin"
    };

    /// <summary>
    /// Verifies that Validate setting with valid type no errors.
    /// </summary>
    [Fact]
    public void Validate_SettingWithValidType_NoErrors()
    {
        var m = ValidManifest();
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "s1", Type = "boolean", Title = "Flag" },
            new SettingContribution { Id = "s2", Type = "string", Title = "Name" },
            new SettingContribution { Id = "s3", Type = "number", Title = "Count" },
            new SettingContribution { Id = "s4", Type = "enum", Title = "Mode", EnumValues = ["a", "b"] }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Empty(errors);
    }

    /// <summary>
    /// Verifies that Validate setting invalid type returns error.
    /// </summary>
    [Fact]
    public void Validate_SettingInvalidType_ReturnsError()
    {
        var m = ValidManifest();
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "s1", Type = "color", Title = "Color" }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Single(errors);
        Assert.Contains("invalid type", errors[0]);
    }

    /// <summary>
    /// Verifies that Validate enum without enum values returns error.
    /// </summary>
    [Fact]
    public void Validate_EnumWithoutEnumValues_ReturnsError()
    {
        var m = ValidManifest();
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "s1", Type = "enum", Title = "Mode" }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Single(errors);
        Assert.Contains("enumValues", errors[0]);
    }

    /// <summary>
    /// Verifies that Validate setting missing id returns error.
    /// </summary>
    [Fact]
    public void Validate_SettingMissingId_ReturnsError()
    {
        var m = ValidManifest();
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "", Type = "boolean", Title = "Flag" }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Contains(errors, e => e.Contains("empty 'id'"));
    }

    /// <summary>
    /// Verifies that Validate setting missing title returns error.
    /// </summary>
    [Fact]
    public void Validate_SettingMissingTitle_ReturnsError()
    {
        var m = ValidManifest();
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "s1", Type = "boolean", Title = "" }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Contains(errors, e => e.Contains("empty 'title'"));
    }

    /// <summary>
    /// Verifies that Validate duplicate setting ids returns error.
    /// </summary>
    [Fact]
    public void Validate_DuplicateSettingIds_ReturnsError()
    {
        var m = ValidManifest();
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "dup", Type = "boolean", Title = "A" },
            new SettingContribution { Id = "dup", Type = "string", Title = "B" }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Contains(errors, e => e.Contains("Duplicate setting id"));
    }

    /// <summary>
    /// Verifies that Validate setting id conflicts with contribution returns error.
    /// </summary>
    [Fact]
    public void Validate_SettingIdConflictsWithContribution_ReturnsError()
    {
        var m = ValidManifest();
        m.Contributes.Sidebar = [new SidebarContribution { Id = "shared-id" }];
        m.Contributes.Settings =
        [
            new SettingContribution { Id = "shared-id", Type = "boolean", Title = "Flag" }
        ];

        var errors = PluginManifestLoader.Validate(m);
        Assert.Contains(errors, e => e.Contains("conflicts"));
    }

    /// <summary>
    /// Verifies that Load settings in manifest parses correctly.
    /// </summary>
    [Fact]
    public void Load_SettingsInManifest_ParsesCorrectly()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.with-settings",
            "name": "settings-plugin",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
            "contributes": {
                "settings": [
                    {
                        "id": "enabled",
                        "type": "boolean",
                        "default": true,
                        "title": "Enabled",
                        "description": "Enable the plugin",
                        "section": "General",
                        "forceOverride": false
                    },
                    {
                        "id": "mode",
                        "type": "enum",
                        "default": "fast",
                        "title": "Mode",
                        "enumValues": ["fast", "slow", "auto"]
                    }
                ]
            }
        }
        """, "settings-plugin");

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Contributes.Settings.Count);

        var enabled = manifest.Contributes.Settings[0];
        Assert.Equal("enabled", enabled.Id);
        Assert.Equal("boolean", enabled.Type);
        Assert.Equal("Enabled", enabled.Title);
        Assert.Equal("General", enabled.Section);
        Assert.False(enabled.ForceOverride);

        var mode = manifest.Contributes.Settings[1];
        Assert.Equal("enum", mode.Type);
        Assert.Equal(3, mode.EnumValues.Count);
        Assert.Contains("auto", mode.EnumValues);
    }

    // ── Dependency Validation Tests ──

    /// <summary>
    /// Verifies that Validate valid dependencies no errors.
    /// </summary>
    [Fact]
    public void Validate_ValidDependencies_NoErrors()
    {
        var m = ValidManifest();
        m.Dependencies =
        [
            new PluginDependency { Id = "com.vido.osr2-plus", MinVersion = "4.0.0" },
            new PluginDependency { Id = "com.vido.other", MinVersion = "1.0.0" }
        ];

        var errors = PluginManifestLoader.Validate(m);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Verifies that Validate dependency empty id returns error.
    /// </summary>
    [Fact]
    public void Validate_DependencyEmptyId_ReturnsError()
    {
        var m = ValidManifest();
        m.Dependencies =
        [
            new PluginDependency { Id = "", MinVersion = "1.0.0" }
        ];

        var errors = PluginManifestLoader.Validate(m);

        Assert.Contains(errors, e => e.Contains("empty 'id'"));
    }

    /// <summary>
    /// Verifies that Validate dependency empty min version returns error.
    /// </summary>
    [Fact]
    public void Validate_DependencyEmptyMinVersion_ReturnsError()
    {
        var m = ValidManifest();
        m.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "" }
        ];

        var errors = PluginManifestLoader.Validate(m);

        Assert.Contains(errors, e => e.Contains("empty 'minVersion'"));
    }

    /// <summary>
    /// Verifies that Validate dependency invalid min version returns error.
    /// </summary>
    [Fact]
    public void Validate_DependencyInvalidMinVersion_ReturnsError()
    {
        var m = ValidManifest();
        m.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "not-a-version" }
        ];

        var errors = PluginManifestLoader.Validate(m);

        Assert.Contains(errors, e => e.Contains("invalid minVersion"));
    }

    /// <summary>
    /// Verifies that Validate duplicate dependency returns error.
    /// </summary>
    [Fact]
    public void Validate_DuplicateDependency_ReturnsError()
    {
        var m = ValidManifest();
        m.Dependencies =
        [
            new PluginDependency { Id = "com.test.dep", MinVersion = "1.0.0" },
            new PluginDependency { Id = "com.test.dep", MinVersion = "2.0.0" }
        ];

        var errors = PluginManifestLoader.Validate(m);

        Assert.Contains(errors, e => e.Contains("Duplicate"));
    }

    /// <summary>
    /// Verifies that Validate empty dependencies array no errors.
    /// </summary>
    [Fact]
    public void Validate_EmptyDependenciesArray_NoErrors()
    {
        var m = ValidManifest();
        m.Dependencies = [];

        var errors = PluginManifestLoader.Validate(m);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Verifies that Load manifest with dependencies parses correctly.
    /// </summary>
    [Fact]
    public void Load_ManifestWithDependencies_ParsesCorrectly()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.with-deps",
            "name": "deps-plugin",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin",
            "dependencies": [
                { "id": "com.vido.osr2-plus", "minVersion": "4.0.0" },
                { "id": "com.vido.other", "minVersion": "1.2.0" }
            ]
        }
        """, "deps-plugin");

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Dependencies.Count);
        Assert.Equal("com.vido.osr2-plus", manifest.Dependencies[0].Id);
        Assert.Equal("4.0.0", manifest.Dependencies[0].MinVersion);
        Assert.Equal("com.vido.other", manifest.Dependencies[1].Id);
        Assert.Equal("1.2.0", manifest.Dependencies[1].MinVersion);
    }

    /// <summary>
    /// Verifies that Load manifest without dependencies defaults to empty list.
    /// </summary>
    [Fact]
    public void Load_ManifestWithoutDependencies_DefaultsToEmptyList()
    {
        var dir = CreatePluginDir("""
        {
            "id": "com.test.no-deps",
            "name": "no-deps",
            "version": "1.0.0",
            "entryPoint": "Test.dll",
            "pluginClass": "Test.Plugin"
        }
        """, "no-deps-plugin");

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.NotNull(manifest);
        Assert.NotNull(manifest.Dependencies);
        Assert.Empty(manifest.Dependencies);
    }
}