using NSubstitute;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
using Vido.Services.FileSystem;
using Xunit;

namespace Vido.Tests;

public sealed class FileSystemServiceTests
{
    private readonly ILogService _log = Substitute.For<ILogService>();
    private readonly FileSystemService _sut;
    private readonly string _testDir;

    public FileSystemServiceTests()
    {
        _sut = new FileSystemService(_log);

        // Create a unique temp directory for each test run
        _testDir = Path.Combine(Path.GetTempPath(), $"VidoTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void GetChildren_ReturnsEmpty_ForNonExistentDir()
    {
        var result = _sut.GetChildren(@"C:\NonExistent_" + Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public void GetChildren_ReturnsDirectoriesFirst_ThenFiles()
    {
        // Arrange: create files and subdirectories
        File.WriteAllText(Path.Combine(_testDir, "beta.txt"), "");
        File.WriteAllText(Path.Combine(_testDir, "alpha.txt"), "");
        Directory.CreateDirectory(Path.Combine(_testDir, "Zulu"));
        Directory.CreateDirectory(Path.Combine(_testDir, "Alpha"));

        // Act
        var result = _sut.GetChildren(_testDir);

        // Assert: directories first (sorted), then files (sorted)
        Assert.Equal(4, result.Count);
        Assert.True(result[0].IsDirectory);
        Assert.Equal("Alpha", result[0].Name);
        Assert.True(result[1].IsDirectory);
        Assert.Equal("Zulu", result[1].Name);
        Assert.False(result[2].IsDirectory);
        Assert.Equal("alpha.txt", result[2].Name);
        Assert.False(result[3].IsDirectory);
        Assert.Equal("beta.txt", result[3].Name);

        // Cleanup
        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void GetChildren_SkipsHiddenFiles()
    {
        // Arrange
        var visibleFile = Path.Combine(_testDir, "visible.txt");
        var hiddenFile = Path.Combine(_testDir, "hidden.txt");
        File.WriteAllText(visibleFile, "");
        File.WriteAllText(hiddenFile, "");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        // Act
        var result = _sut.GetChildren(_testDir);

        // Assert
        Assert.Single(result);
        Assert.Equal("visible.txt", result[0].Name);

        // Cleanup
        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void GetChildren_DirectoryNodes_HaveDummyChild()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "SubDir"));

        var result = _sut.GetChildren(_testDir);

        Assert.Single(result);
        Assert.True(result[0].IsDirectory);
        Assert.True(result[0].NeedsLoading);

        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void LoadChildren_PopulatesDirectoryNode()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "Sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "video.mp4"), "");

        var parentNode = new FileNode(subDir, isDirectory: true);
        Assert.True(parentNode.NeedsLoading);

        // Act
        _sut.LoadChildren(parentNode);

        // Assert
        Assert.False(parentNode.NeedsLoading);
        Assert.Single(parentNode.Children);
        Assert.Equal("video.mp4", parentNode.Children[0].Name);
        Assert.True(parentNode.Children[0].IsVideoFile);

        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void LoadChildren_NoOp_WhenAlreadyLoaded()
    {
        var subDir = Path.Combine(_testDir, "Sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "a.txt"), "");

        var node = new FileNode(subDir, isDirectory: true);
        _sut.LoadChildren(node);
        Assert.Single(node.Children);

        // Add another file after loading
        File.WriteAllText(Path.Combine(subDir, "b.txt"), "");

        // LoadChildren should not re-load
        _sut.LoadChildren(node);
        Assert.Single(node.Children); // Still 1, not 2

        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void LoadChildren_NoOp_ForFileNode()
    {
        var filePath = Path.Combine(_testDir, "test.mp4");
        File.WriteAllText(filePath, "");

        var node = new FileNode(filePath, isDirectory: false);
        _sut.LoadChildren(node); // Should not throw or modify

        Assert.Empty(node.Children);

        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void GetChildren_ReturnsEmpty_ForEmptyDir()
    {
        var result = _sut.GetChildren(_testDir);
        Assert.Empty(result);

        Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsSameResultAsSync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "file.txt"), "");
        Directory.CreateDirectory(Path.Combine(_testDir, "SubDir"));

        // Act
        var syncResult = _sut.GetChildren(_testDir);
        var asyncResult = await _sut.GetChildrenAsync(_testDir);

        // Assert
        Assert.Equal(syncResult.Count, asyncResult.Count);
        for (var i = 0; i < syncResult.Count; i++)
        {
            Assert.Equal(syncResult[i].Name, asyncResult[i].Name);
            Assert.Equal(syncResult[i].IsDirectory, asyncResult[i].IsDirectory);
        }

        Directory.Delete(_testDir, recursive: true);
    }
}
