using System.Runtime.Versioning;
using Vido.Core.FileSystem;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="FileAssociationHelper"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileAssociationHelperTests
{
    /// <summary>
    /// Verifies that Prog Id is expected value.
    /// </summary>
    [Fact]
    public void ProgId_IsExpectedValue()
    {
        Assert.Equal("Vido.VideoFile", FileAssociationHelper.ProgId);
    }

    /// <summary>
    /// Verifies that File Type Description is expected value.
    /// </summary>
    [Fact]
    public void FileTypeDescription_IsExpectedValue()
    {
        Assert.Equal("Vido Video File", FileAssociationHelper.FileTypeDescription);
    }

    /// <summary>
    /// Verifies that Supported Extensions matches file node video extensions.
    /// </summary>
    [Fact]
    public void SupportedExtensions_MatchesFileNodeVideoExtensions()
    {
        Assert.Same(FileNode.VideoExtensions, FileAssociationHelper.SupportedExtensions);
    }

    /// <summary>
    /// Verifies that Supported Extensions contains expected formats.
    /// </summary>
    [Fact]
    public void SupportedExtensions_ContainsExpectedFormats()
    {
        var extensions = FileAssociationHelper.SupportedExtensions;

        Assert.Contains(".mp4", extensions);
        Assert.Contains(".avi", extensions);
        Assert.Contains(".mkv", extensions);
        Assert.Contains(".mov", extensions);
        Assert.Contains(".wmv", extensions);
        Assert.Contains(".flv", extensions);
        Assert.Contains(".webm", extensions);
    }

    /// <summary>
    /// Verifies that Supported Extensions count matches video extensions.
    /// </summary>
    [Fact]
    public void SupportedExtensions_CountMatchesVideoExtensions()
    {
        Assert.Equal(FileNode.VideoExtensions.Count, FileAssociationHelper.SupportedExtensions.Count);
    }

    /// <summary>
    /// Verifies that Register throws on null or whitespace exe path.
    /// </summary>
    /// <param name="exePath">The executable path to register.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_ThrowsOnNullOrWhitespaceExePath(string? exePath)
    {
        Assert.ThrowsAny<ArgumentException>(() => FileAssociationHelper.Register(exePath!));
    }

    /// <summary>
    /// Verifies that Is Associated returns false for unregistered extension.
    /// </summary>
    [Fact]
    public void IsAssociated_ReturnsFalse_ForUnregisteredExtension()
    {
        // Use an obscure extension that is almost certainly not registered to Vido.
        Assert.False(FileAssociationHelper.IsAssociated(".xyzvidotest"));
    }
}