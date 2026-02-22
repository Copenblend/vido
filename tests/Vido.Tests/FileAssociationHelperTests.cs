using System.Runtime.Versioning;
using Vido.Core.FileSystem;
using Xunit;

namespace Vido.Tests;

[SupportedOSPlatform("windows")]
public sealed class FileAssociationHelperTests
{
    [Fact]
    public void ProgId_IsExpectedValue()
    {
        Assert.Equal("Vido.VideoFile", FileAssociationHelper.ProgId);
    }

    [Fact]
    public void FileTypeDescription_IsExpectedValue()
    {
        Assert.Equal("Vido Video File", FileAssociationHelper.FileTypeDescription);
    }

    [Fact]
    public void SupportedExtensions_MatchesFileNodeVideoExtensions()
    {
        Assert.Same(FileNode.VideoExtensions, FileAssociationHelper.SupportedExtensions);
    }

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

    [Fact]
    public void SupportedExtensions_CountMatchesVideoExtensions()
    {
        Assert.Equal(FileNode.VideoExtensions.Count, FileAssociationHelper.SupportedExtensions.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_ThrowsOnNullOrWhitespaceExePath(string? exePath)
    {
        Assert.ThrowsAny<ArgumentException>(() => FileAssociationHelper.Register(exePath!));
    }

    [Fact]
    public void IsAssociated_ReturnsFalse_ForUnregisteredExtension()
    {
        // Use an obscure extension that is almost certainly not registered to Vido.
        Assert.False(FileAssociationHelper.IsAssociated(".xyzvidotest"));
    }
}
