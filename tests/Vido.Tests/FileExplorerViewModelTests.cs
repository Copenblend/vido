using NSubstitute;
using Vido.Core.FileSystem;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

public sealed class FileExplorerViewModelTests
{
    private readonly IFileSystemService _fileSystemService = Substitute.For<IFileSystemService>();
    private readonly IStateService _stateService = Substitute.For<IStateService>();
    private readonly AppState _appState = new();
    private readonly FileExplorerViewModel _sut;

    public FileExplorerViewModelTests()
    {
        _stateService.Current.Returns(_appState);
        _sut = new FileExplorerViewModel(_fileSystemService, _stateService);
    }

    [Fact]
    public void Initial_HasNoFolderOpen()
    {
        Assert.False(_sut.HasFolderOpen);
        Assert.Null(_sut.FolderPath);
        Assert.Null(_sut.FolderName);
        Assert.Empty(_sut.RootNodes);
    }

    [Fact]
    public void OpenFolder_PopulatesRootNodes()
    {
        // Arrange
        var testDir = CreateTempDir();
        var nodes = new List<FileNode>
        {
            new(Path.Combine(testDir, "a.txt"), false),
            new(Path.Combine(testDir, "sub"), true)
        };
        _fileSystemService.GetChildren(testDir).Returns(nodes);

        // Act
        _sut.OpenFolder(testDir);

        // Assert
        Assert.True(_sut.HasFolderOpen);
        Assert.Equal(testDir, _sut.FolderPath);
        Assert.Equal(2, _sut.RootNodes.Count);

        CleanupDir(testDir);
    }

    [Fact]
    public void OpenFolder_SetsFolderName()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>());

        _sut.OpenFolder(testDir);

        Assert.Equal(Path.GetFileName(testDir), _sut.FolderName);
        CleanupDir(testDir);
    }

    [Fact]
    public void OpenFolder_PersistsLastOpenFolder()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>());

        _sut.OpenFolder(testDir);

        Assert.Equal(testDir, _appState.LastOpenFolder);
        CleanupDir(testDir);
    }

    [Fact]
    public void OpenFolder_IgnoresNonExistentPath()
    {
        _sut.OpenFolder(@"C:\NonExistent_" + Guid.NewGuid());

        Assert.False(_sut.HasFolderOpen);
        Assert.Empty(_sut.RootNodes);
    }

    [Fact]
    public void CloseFolder_ClearsEverything()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "file.txt"), false)
        });

        _sut.OpenFolder(testDir);
        Assert.True(_sut.HasFolderOpen);

        _sut.CloseFolder();

        Assert.False(_sut.HasFolderOpen);
        Assert.Null(_sut.FolderPath);
        Assert.Null(_sut.FolderName);
        Assert.Empty(_sut.RootNodes);
        Assert.Null(_appState.LastOpenFolder);
        CleanupDir(testDir);
    }

    [Fact]
    public void OpenFolder_ClearsPreviousFolder()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        _fileSystemService.GetChildren(dir1).Returns(new List<FileNode>
        {
            new(Path.Combine(dir1, "a.txt"), false)
        });
        _fileSystemService.GetChildren(dir2).Returns(new List<FileNode>
        {
            new(Path.Combine(dir2, "b.txt"), false),
            new(Path.Combine(dir2, "c.txt"), false)
        });

        _sut.OpenFolder(dir1);
        Assert.Single(_sut.RootNodes);

        _sut.OpenFolder(dir2);
        Assert.Equal(2, _sut.RootNodes.Count);
        Assert.Equal(dir2, _sut.FolderPath);

        CleanupDir(dir1);
        CleanupDir(dir2);
    }

    [Fact]
    public void ExpandNode_DelegatesToFileSystemService()
    {
        var node = new FileNode(@"C:\Test", isDirectory: true);

        _sut.ExpandNode(node);

        _fileSystemService.Received(1).LoadChildren(node);
    }

    [Fact]
    public void RestoreLastFolder_OpensPersistedFolder()
    {
        var testDir = CreateTempDir();
        _appState.LastOpenFolder = testDir;
        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>());

        _sut.RestoreLastFolder();

        Assert.True(_sut.HasFolderOpen);
        Assert.Equal(testDir, _sut.FolderPath);
        CleanupDir(testDir);
    }

    [Fact]
    public void RestoreLastFolder_NoOp_WhenNoPersistedFolder()
    {
        _appState.LastOpenFolder = null;

        _sut.RestoreLastFolder();

        Assert.False(_sut.HasFolderOpen);
    }

    [Fact]
    public void RestoreLastFolder_NoOp_WhenPersistedFolderDeleted()
    {
        _appState.LastOpenFolder = @"C:\NonExistent_" + Guid.NewGuid();

        _sut.RestoreLastFolder();

        Assert.False(_sut.HasFolderOpen);
    }

    [Fact]
    public void PropertyChanged_RaisedForHasFolderOpen()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>());

        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.OpenFolder(testDir);

        Assert.Contains(nameof(FileExplorerViewModel.HasFolderOpen), raised);
        Assert.Contains(nameof(FileExplorerViewModel.FolderPath), raised);
        Assert.Contains(nameof(FileExplorerViewModel.FolderName), raised);
        CleanupDir(testDir);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"VidoTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupDir(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* test cleanup */ }
    }
}
