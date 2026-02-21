using NSubstitute;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

public sealed class FileExplorerViewModelTests
{
    private readonly IFileSystemService _fileSystemService = Substitute.For<IFileSystemService>();
    private readonly IStateService _stateService = Substitute.For<IStateService>();
    private readonly ILogService _logService = Substitute.For<ILogService>();
    private readonly AppState _appState = new();
    private readonly FileExplorerViewModel _sut;

    public FileExplorerViewModelTests()
    {
        _stateService.Current.Returns(_appState);
        _sut = new FileExplorerViewModel(_fileSystemService, _stateService, _logService);
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
        var testDir = CreateTempDir();
        var nodes = new List<FileNode>
        {
            new(Path.Combine(testDir, "a.txt"), false),
            new(Path.Combine(testDir, "sub"), true)
        };
        _fileSystemService.GetChildren(testDir).Returns(nodes);

        _sut.OpenFolder(testDir);

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
        _fileSystemService.GetChildren(node.FullPath).Returns(new List<FileNode>());

        _sut.ExpandNode(node);

        _fileSystemService.Received(1).GetChildren(node.FullPath);
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

    //  Rescan tests 

    [Fact]
    public void RescanFolder_ReloadsFromDisk()
    {
        var testDir = CreateTempDir();
        var initialNodes = new List<FileNode> { new(Path.Combine(testDir, "a.txt"), false) };
        var updatedNodes = new List<FileNode>
        {
            new(Path.Combine(testDir, "a.txt"), false),
            new(Path.Combine(testDir, "b.txt"), false)
        };

        _fileSystemService.GetChildren(testDir)
            .Returns(initialNodes, updatedNodes);

        _sut.OpenFolder(testDir);
        Assert.Single(_sut.RootNodes);

        _sut.RescanFolder();
        Assert.Equal(2, _sut.RootNodes.Count);
        CleanupDir(testDir);
    }

    [Fact]
    public void RescanFolder_NoOp_WhenNoFolderOpen()
    {
        _sut.RescanFolder();
        Assert.Empty(_sut.RootNodes);
    }

    [Fact]
    public void RescanFolder_PreservesHiddenFiles()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.txt");
        var visiblePath = Path.Combine(testDir, "visible.txt");

        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        _sut.OpenFolder(testDir);

        // Hidden file is excluded (ShowHiddenFiles is false by default)
        Assert.Single(_sut.RootNodes);

        _sut.RescanFolder();

        // Still excluded after rescan  hidden files persist
        Assert.Single(_sut.RootNodes);
        Assert.Equal("visible.txt", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    //  HideFile tests 

    [Fact]
    public void HideFile_RemovesNodeFromTree_WhenShowHiddenFalse()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "hide-me.txt"), false);
        var keepNode = new FileNode(Path.Combine(testDir, "keep.txt"), false);

        _fileSystemService.GetChildren(testDir)
            .Returns(new List<FileNode> { fileNode, keepNode });

        _sut.OpenFolder(testDir);
        Assert.Equal(2, _sut.RootNodes.Count);

        _sut.HideFile(fileNode);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("keep.txt", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    [Fact]
    public void HideFile_MarksNodeHidden_WhenShowHiddenTrue()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "dim-me.txt"), false);

        _fileSystemService.GetChildren(testDir)
            .Returns(new List<FileNode> { fileNode });

        _sut.ShowHiddenFiles = true;
        _sut.OpenFolder(testDir);

        _sut.HideFile(fileNode);

        // Node stays in tree but is marked hidden
        Assert.Single(_sut.RootNodes);
        Assert.True(_sut.RootNodes[0].IsHidden);
        CleanupDir(testDir);
    }

    [Fact]
    public void HideFile_AddsToHiddenFilesState()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "hide.txt"), false);
        _fileSystemService.GetChildren(testDir)
            .Returns(new List<FileNode> { fileNode });

        _sut.OpenFolder(testDir);
        _sut.HideFile(fileNode);

        Assert.Contains(fileNode.FullPath, _appState.HiddenFiles);
        CleanupDir(testDir);
    }

    [Fact]
    public void HideFile_NoDuplicatesInHiddenFiles()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "dup.txt"), false);
        _fileSystemService.GetChildren(testDir)
            .Returns(new List<FileNode> { fileNode });

        _sut.ShowHiddenFiles = true;
        _sut.OpenFolder(testDir);
        _sut.HideFile(fileNode);
        _sut.HideFile(fileNode);

        Assert.Single(_appState.HiddenFiles);
        CleanupDir(testDir);
    }

    [Fact]
    public void HideFile_NoOp_WhenNull()
    {
        _sut.HideFile(null);
        Assert.Empty(_appState.HiddenFiles);
    }

    [Fact]
    public void HideFile_WorksForFolders()
    {
        var testDir = CreateTempDir();
        var folderNode = new FileNode(Path.Combine(testDir, "SubFolder"), true);
        _fileSystemService.GetChildren(testDir)
            .Returns(new List<FileNode> { folderNode });

        _sut.OpenFolder(testDir);
        _sut.HideFile(folderNode);

        Assert.Empty(_sut.RootNodes);
        Assert.Contains(folderNode.FullPath, _appState.HiddenFiles);
        CleanupDir(testDir);
    }

    //  UnhideFile tests 

    [Fact]
    public void UnhideFile_RemovesFromHiddenState()
    {
        var path = @"C:\test\hidden.txt";
        _appState.HiddenFiles.Add(path);
        var node = new FileNode(path, false) { IsHidden = true };

        _sut.UnhideFile(node);

        Assert.Empty(_appState.HiddenFiles);
        Assert.False(node.IsHidden);
    }

    [Fact]
    public void UnhideFile_NoOp_WhenNull()
    {
        _sut.UnhideFile(null);
    }

    //  ShowHiddenFiles toggle 

    [Fact]
    public void ToggleShowHiddenFiles_TogglesProperty()
    {
        Assert.False(_sut.ShowHiddenFiles);
        _sut.ToggleShowHiddenFiles();
        Assert.True(_sut.ShowHiddenFiles);
        _sut.ToggleShowHiddenFiles();
        Assert.False(_sut.ShowHiddenFiles);
    }

    [Fact]
    public void ShowHiddenFiles_True_IncludesHiddenNodesAsMarked()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.txt");
        var visiblePath = Path.Combine(testDir, "visible.txt");

        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        _sut.ShowHiddenFiles = true;
        _sut.OpenFolder(testDir);

        Assert.Equal(2, _sut.RootNodes.Count);
        var hidden = _sut.RootNodes.First(n => n.FullPath == hiddenPath);
        Assert.True(hidden.IsHidden);
        var visible = _sut.RootNodes.First(n => n.FullPath == visiblePath);
        Assert.False(visible.IsHidden);
        CleanupDir(testDir);
    }

    [Fact]
    public void ShowHiddenFiles_False_ExcludesHiddenNodes()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.txt");
        var visiblePath = Path.Combine(testDir, "visible.txt");

        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        _sut.ShowHiddenFiles = false;
        _sut.OpenFolder(testDir);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("visible.txt", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    [Fact]
    public void ToggleShowHiddenFiles_RefreshesTreeWithHiddenNodes()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.txt");
        var visiblePath = Path.Combine(testDir, "visible.txt");

        _fileSystemService.GetChildren(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        _sut.OpenFolder(testDir);

        // Initially hidden files are excluded
        Assert.Single(_sut.RootNodes);

        // Toggle on — hidden node appears marked
        _sut.ToggleShowHiddenFiles();
        Assert.Equal(2, _sut.RootNodes.Count);
        var hiddenNode = _sut.RootNodes.First(n => n.FullPath == hiddenPath);
        Assert.True(hiddenNode.IsHidden);

        // Toggle off — hidden node disappears again
        _sut.ToggleShowHiddenFiles();
        Assert.Single(_sut.RootNodes);
        Assert.Equal("visible.txt", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    [Fact]
    public void ExpandNode_AppliesHiddenFilter()
    {
        var testDir = CreateTempDir();
        var parentPath = Path.Combine(testDir, "parent");
        var hiddenChildPath = Path.Combine(parentPath, "hidden-child.txt");
        var visibleChildPath = Path.Combine(parentPath, "visible-child.txt");

        _fileSystemService.GetChildren(parentPath).Returns(new List<FileNode>
        {
            new(hiddenChildPath, false),
            new(visibleChildPath, false)
        });

        _appState.HiddenFiles.Add(hiddenChildPath);
        var parentNode = new FileNode(parentPath, isDirectory: true);

        _sut.ExpandNode(parentNode);

        Assert.Single(parentNode.Children);
        Assert.Equal("visible-child.txt", parentNode.Children[0].Name);
        CleanupDir(testDir);
    }

    //  CloseFolder clears SelectedNode 

    [Fact]
    public void CloseFolder_ClearsSelectedNode()
    {
        var testDir = CreateTempDir();
        var node = new FileNode(Path.Combine(testDir, "file.txt"), false);
        _fileSystemService.GetChildren(testDir)
            .Returns(new List<FileNode> { node });

        _sut.OpenFolder(testDir);
        _sut.SelectedNode = node;

        _sut.CloseFolder();
        Assert.Null(_sut.SelectedNode);
        CleanupDir(testDir);
    }

    [Fact]
    public void SelectedNode_RaisesPropertyChanged()
    {
        var node = new FileNode(@"C:\test.mp4", false);
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedNode = node;

        Assert.Contains(nameof(FileExplorerViewModel.SelectedNode), raised);
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