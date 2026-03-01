using Vido.Core.Plugin;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginPaths"/>.
/// </summary>
public sealed class PluginPathsTests
{
    /// <summary>
    /// Verifies the default plugin directory is non-empty.
    /// </summary>
    [Fact]
    public void DefaultPluginDirectory_IsNotNullOrEmpty()
    {
        var path = PluginPaths.DefaultPluginDirectory;

        Assert.False(string.IsNullOrWhiteSpace(path));
    }

    /// <summary>
    /// Verifies the default plugin directory contains the expected path segments.
    /// </summary>
    [Fact]
    public void DefaultPluginDirectory_ContainsExpectedSegments()
    {
        var path = PluginPaths.DefaultPluginDirectory;

        Assert.Contains("Vido", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plugins", path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies repeated accesses return the same cached string instance.
    /// </summary>
    [Fact]
    public void DefaultPluginDirectory_ReturnsSameReferenceAcrossAccesses()
    {
        var first = PluginPaths.DefaultPluginDirectory;
        var second = PluginPaths.DefaultPluginDirectory;

        Assert.True(object.ReferenceEquals(first, second));
    }

    /// <summary>
    /// Verifies the default plugin directory starts with the user's APPDATA directory.
    /// </summary>
    [Fact]
    public void DefaultPluginDirectory_StartsWithApplicationDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = PluginPaths.DefaultPluginDirectory;

        Assert.StartsWith(appData, path, StringComparison.OrdinalIgnoreCase);
    }
}
