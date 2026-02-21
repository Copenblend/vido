using NSubstitute;
using Vido.Core.Logging;
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

    public PluginManifestLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _logger = Substitute.For<ILogService>();
    }

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

    [Fact]
    public void Load_NoPluginJson_ReturnsNull()
    {
        var dir = Path.Combine(_tempDir, "empty-dir");
        Directory.CreateDirectory(dir);

        var manifest = PluginManifestLoader.Load(dir, _logger);

        Assert.Null(manifest);
    }

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

    [Fact]
    public void Validate_MissingMultipleFields_ReturnsMultipleErrors()
    {
        var manifest = new Core.Plugin.PluginManifest(); // all defaults are empty

        var errors = PluginManifestLoader.Validate(manifest);

        Assert.True(errors.Count >= 4); // id, name, version, entryPoint, pluginClass
    }

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
}
