using NSubstitute;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="FileExplorerViewModel"/>.
/// </summary>
public sealed class FileExplorerViewModelTests
{
    private readonly IFileSystemService _fileSystemService = Substitute.For<IFileSystemService>();
    private readonly IStateService _stateService = Substitute.For<IStateService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogService _logService = Substitute.For<ILogService>();
    private readonly AppSettings _appSettings = new();
    private readonly AppState _appState = new();
    private readonly FileExplorerViewModel _sut;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public FileExplorerViewModelTests()
    {
        _stateService.Current.Returns(_appState);
        _settingsService.Current.Returns(_appSettings);
        _sut = new FileExplorerViewModel(_fileSystemService, _stateService, _settingsService, _logService);
    }

    /// <summary>
    /// Verifies that Initial has no folder open.
    /// </summary>
    [Fact]
    public void Initial_HasNoFolderOpen()
    {
        Assert.False(_sut.HasFolderOpen);
        Assert.Null(_sut.FolderPath);
        Assert.Null(_sut.FolderName);
        Assert.Empty(_sut.RootNodes);
    }

    /// <summary>
    /// Verifies that Open Folder populates root nodes.
    /// </summary>
    [Fact]
    public async Task OpenFolder_PopulatesRootNodes()
    {
        var testDir = CreateTempDir();
        var nodes = new List<FileNode>
        {
            new(Path.Combine(testDir, "a.mp4"), false),
            new(Path.Combine(testDir, "sub"), true)
        };
        _fileSystemService.GetChildrenAsync(testDir).Returns(nodes);

        await _sut.OpenFolderAsync(testDir);

        Assert.True(_sut.HasFolderOpen);
        Assert.Equal(testDir, _sut.FolderPath);
        Assert.Equal(2, _sut.RootNodes.Count);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Open Folder sets folder name.
    /// </summary>
    [Fact]
    public async Task OpenFolder_SetsFolderName()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>());

        await _sut.OpenFolderAsync(testDir);

        Assert.Equal(Path.GetFileName(testDir), _sut.FolderName);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Open Folder persists last open folder.
    /// </summary>
    [Fact]
    public async Task OpenFolder_PersistsLastOpenFolder()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>());

        await _sut.OpenFolderAsync(testDir);

        Assert.Equal(testDir, _appState.LastOpenFolder);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Open Folder ignores non existent path.
    /// </summary>
    [Fact]
    public async Task OpenFolder_IgnoresNonExistentPath()
    {
        await _sut.OpenFolderAsync(@"C:\NonExistent_" + Guid.NewGuid());

        Assert.False(_sut.HasFolderOpen);
        Assert.Empty(_sut.RootNodes);
        Assert.False(_sut.IsLoading);
    }

    /// <summary>
    /// Verifies that Close Folder clears everything.
    /// </summary>
    [Fact]
    public async Task CloseFolder_ClearsEverything()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "file.mp4"), false)
        });

        await _sut.OpenFolderAsync(testDir);
        Assert.True(_sut.HasFolderOpen);

        _sut.CloseFolder();

        Assert.False(_sut.HasFolderOpen);
        Assert.Null(_sut.FolderPath);
        Assert.Null(_sut.FolderName);
        Assert.Empty(_sut.RootNodes);
        Assert.Null(_appState.LastOpenFolder);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Open Folder clears previous folder.
    /// </summary>
    [Fact]
    public async Task OpenFolder_ClearsPreviousFolder()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        _fileSystemService.GetChildrenAsync(dir1).Returns(new List<FileNode>
        {
            new(Path.Combine(dir1, "a.mp4"), false)
        });
        _fileSystemService.GetChildrenAsync(dir2).Returns(new List<FileNode>
        {
            new(Path.Combine(dir2, "b.mp4"), false),
            new(Path.Combine(dir2, "c.mp4"), false)
        });

        await _sut.OpenFolderAsync(dir1);
        Assert.Single(_sut.RootNodes);

        await _sut.OpenFolderAsync(dir2);
        Assert.Equal(2, _sut.RootNodes.Count);
        Assert.Equal(dir2, _sut.FolderPath);

        CleanupDir(dir1);
        CleanupDir(dir2);
    }

    /// <summary>
    /// Verifies that Add Items triggers root node sort via collection reassignment.
    /// </summary>
    [Fact]
    public void AddItems_AssignsNewRootNodesCollection()
    {
        var dir = CreateTempDir();
        var filePath = Path.Combine(dir, "clip.mp4");
        File.WriteAllText(filePath, "x");

        var before = _sut.RootNodes;

        _sut.AddItems([filePath]);

        Assert.NotSame(before, _sut.RootNodes);
        CleanupDir(dir);
    }

    /// <summary>
    /// Verifies that Expand Node delegates to file system service.
    /// </summary>
    [Fact]
    public async Task ExpandNode_DelegatesToFileSystemService()
    {
        var node = new FileNode(@"C:\Test", isDirectory: true);
        _fileSystemService.GetChildrenAsync(node.FullPath).Returns(new List<FileNode>());

        await _sut.ExpandNodeAsync(node);

        await _fileSystemService.Received(1).GetChildrenAsync(node.FullPath);
    }

    /// <summary>
    /// Verifies that Restore Last Folder opens persisted folder.
    /// </summary>
    [Fact]
    public async Task RestoreLastFolder_OpensPersistedFolder()
    {
        var testDir = CreateTempDir();
        _appState.LastOpenFolder = testDir;
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>());

        await _sut.RestoreLastFolderAsync();

        Assert.True(_sut.HasFolderOpen);
        Assert.Equal(testDir, _sut.FolderPath);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Restore Last Folder no op when no persisted folder.
    /// </summary>
    [Fact]
    public async Task RestoreLastFolder_NoOp_WhenNoPersistedFolder()
    {
        _appState.LastOpenFolder = null;
        await _sut.RestoreLastFolderAsync();
        Assert.False(_sut.HasFolderOpen);
    }

    /// <summary>
    /// Verifies that Restore Last Folder no op when persisted folder deleted.
    /// </summary>
    [Fact]
    public async Task RestoreLastFolder_NoOp_WhenPersistedFolderDeleted()
    {
        _appState.LastOpenFolder = @"C:\NonExistent_" + Guid.NewGuid();
        await _sut.RestoreLastFolderAsync();
        Assert.False(_sut.HasFolderOpen);
    }

    /// <summary>
    /// Verifies that Property Changed raised for has folder open.
    /// </summary>
    [Fact]
    public async Task PropertyChanged_RaisedForHasFolderOpen()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>());

        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await _sut.OpenFolderAsync(testDir);

        Assert.Contains(nameof(FileExplorerViewModel.HasFolderOpen), raised);
        Assert.Contains(nameof(FileExplorerViewModel.FolderPath), raised);
        Assert.Contains(nameof(FileExplorerViewModel.FolderName), raised);
        CleanupDir(testDir);
    }

    //  Rescan tests 

    /// <summary>
    /// Verifies that Rescan Folder reloads from disk.
    /// </summary>
    [Fact]
    public async Task RescanFolder_ReloadsFromDisk()
    {
        var testDir = CreateTempDir();
        var initialNodes = new List<FileNode> { new(Path.Combine(testDir, "a.mp4"), false) };
        var updatedNodes = new List<FileNode>
        {
            new(Path.Combine(testDir, "a.mp4"), false),
            new(Path.Combine(testDir, "b.mp4"), false)
        };

        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(initialNodes, updatedNodes);

        await _sut.OpenFolderAsync(testDir);
        Assert.Single(_sut.RootNodes);

        await _sut.RescanFolderAsync();
        Assert.Equal(2, _sut.RootNodes.Count);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Rescan Folder no op when no folder open.
    /// </summary>
    [Fact]
    public async Task RescanFolder_NoOp_WhenNoFolderOpen()
    {
        await _sut.RescanFolderAsync();
        Assert.Empty(_sut.RootNodes);
    }

    /// <summary>
    /// Verifies that Rescan Folder preserves hidden files.
    /// </summary>
    [Fact]
    public async Task RescanFolder_PreservesHiddenFiles()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.mp4");
        var visiblePath = Path.Combine(testDir, "visible.mp4");

        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        await _sut.OpenFolderAsync(testDir);

        // Hidden file is excluded (ShowHiddenFiles is false by default)
        Assert.Single(_sut.RootNodes);

        await _sut.RescanFolderAsync();

        // Still excluded after rescan  hidden files persist
        Assert.Single(_sut.RootNodes);
        Assert.Equal("visible.mp4", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    //  HideFile tests 

    /// <summary>
    /// Verifies that Hide File removes node from tree when show hidden false.
    /// </summary>
    [Fact]
    public async Task HideFile_RemovesNodeFromTree_WhenShowHiddenFalse()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "hide-me.mp4"), false);
        var keepNode = new FileNode(Path.Combine(testDir, "keep.mp4"), false);

        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(new List<FileNode> { fileNode, keepNode });

        await _sut.OpenFolderAsync(testDir);
        Assert.Equal(2, _sut.RootNodes.Count);

        _sut.HideFile(fileNode);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("keep.mp4", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Hide File marks node hidden when show hidden true.
    /// </summary>
    [Fact]
    public async Task HideFile_MarksNodeHidden_WhenShowHiddenTrue()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "dim-me.mp4"), false);

        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(new List<FileNode> { fileNode });

        _sut.ShowHiddenFiles = true;
        await _sut.OpenFolderAsync(testDir);

        _sut.HideFile(fileNode);

        // Node stays in tree but is marked hidden
        Assert.Single(_sut.RootNodes);
        Assert.True(_sut.RootNodes[0].IsHidden);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Hide File adds to hidden files state.
    /// </summary>
    [Fact]
    public async Task HideFile_AddsToHiddenFilesState()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "hide.mp4"), false);
        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(new List<FileNode> { fileNode });

        await _sut.OpenFolderAsync(testDir);
        _sut.HideFile(fileNode);

        Assert.Contains(fileNode.FullPath, _appState.HiddenFiles);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Hide File no duplicates in hidden files.
    /// </summary>
    [Fact]
    public async Task HideFile_NoDuplicatesInHiddenFiles()
    {
        var testDir = CreateTempDir();
        var fileNode = new FileNode(Path.Combine(testDir, "dup.mp4"), false);
        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(new List<FileNode> { fileNode });

        _sut.ShowHiddenFiles = true;
        await _sut.OpenFolderAsync(testDir);
        _sut.HideFile(fileNode);
        _sut.HideFile(fileNode);

        Assert.Single(_appState.HiddenFiles);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Hide File no op when null.
    /// </summary>
    [Fact]
    public void HideFile_NoOp_WhenNull()
    {
        _sut.HideFile(null);
        Assert.Empty(_appState.HiddenFiles);
    }

    /// <summary>
    /// Verifies that Hide File works for folders.
    /// </summary>
    [Fact]
    public async Task HideFile_WorksForFolders()
    {
        var testDir = CreateTempDir();
        var folderNode = new FileNode(Path.Combine(testDir, "SubFolder"), true);
        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(new List<FileNode> { folderNode });

        await _sut.OpenFolderAsync(testDir);
        _sut.HideFile(folderNode);

        Assert.Empty(_sut.RootNodes);
        Assert.Contains(folderNode.FullPath, _appState.HiddenFiles);
        CleanupDir(testDir);
    }

    //  UnhideFile tests 

    /// <summary>
    /// Verifies that Unhide File removes from hidden state.
    /// </summary>
    [Fact]
    public void UnhideFile_RemovesFromHiddenState()
    {
        var path = @"C:\test\hidden.mp4";
        _appState.HiddenFiles.Add(path);
        var node = new FileNode(path, false) { IsHidden = true };

        _sut.UnhideFile(node);

        Assert.Empty(_appState.HiddenFiles);
        Assert.False(node.IsHidden);
    }

    /// <summary>
    /// Verifies that Unhide File no op when null.
    /// </summary>
    [Fact]
    public void UnhideFile_NoOp_WhenNull()
    {
        _sut.UnhideFile(null);
    }

    //  ShowHiddenFiles toggle 

    /// <summary>
    /// Verifies that Toggle Show Hidden Files toggles property.
    /// </summary>
    [Fact]
    public async Task ToggleShowHiddenFiles_TogglesProperty()
    {
        Assert.False(_sut.ShowHiddenFiles);
        await _sut.ToggleShowHiddenFilesAsync();
        Assert.True(_sut.ShowHiddenFiles);
        await _sut.ToggleShowHiddenFilesAsync();
        Assert.False(_sut.ShowHiddenFiles);
    }

    /// <summary>
    /// Verifies that Show Hidden Files true includes hidden nodes as marked.
    /// </summary>
    [Fact]
    public async Task ShowHiddenFiles_True_IncludesHiddenNodesAsMarked()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.mp4");
        var visiblePath = Path.Combine(testDir, "visible.mp4");

        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        _sut.ShowHiddenFiles = true;
        await _sut.OpenFolderAsync(testDir);

        Assert.Equal(2, _sut.RootNodes.Count);
        var hidden = _sut.RootNodes.First(n => n.FullPath == hiddenPath);
        Assert.True(hidden.IsHidden);
        var visible = _sut.RootNodes.First(n => n.FullPath == visiblePath);
        Assert.False(visible.IsHidden);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Show Hidden Files false excludes hidden nodes.
    /// </summary>
    [Fact]
    public async Task ShowHiddenFiles_False_ExcludesHiddenNodes()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.mp4");
        var visiblePath = Path.Combine(testDir, "visible.mp4");

        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        _sut.ShowHiddenFiles = false;
        await _sut.OpenFolderAsync(testDir);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("visible.mp4", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Toggle Show Hidden Files refreshes tree with hidden nodes.
    /// </summary>
    [Fact]
    public async Task ToggleShowHiddenFiles_RefreshesTreeWithHiddenNodes()
    {
        var testDir = CreateTempDir();
        var hiddenPath = Path.Combine(testDir, "hidden.mp4");
        var visiblePath = Path.Combine(testDir, "visible.mp4");

        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(hiddenPath, false),
            new(visiblePath, false)
        });

        _appState.HiddenFiles.Add(hiddenPath);
        await _sut.OpenFolderAsync(testDir);

        // Initially hidden files are excluded
        Assert.Single(_sut.RootNodes);

        // Toggle on — hidden node appears marked
        await _sut.ToggleShowHiddenFilesAsync();
        Assert.Equal(2, _sut.RootNodes.Count);
        var hiddenNode = _sut.RootNodes.First(n => n.FullPath == hiddenPath);
        Assert.True(hiddenNode.IsHidden);

        // Toggle off — hidden node disappears again
        await _sut.ToggleShowHiddenFilesAsync();
        Assert.Single(_sut.RootNodes);
        Assert.Equal("visible.mp4", _sut.RootNodes[0].Name);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Expand Node applies hidden filter.
    /// </summary>
    [Fact]
    public async Task ExpandNode_AppliesHiddenFilter()
    {
        var testDir = CreateTempDir();
        var parentPath = Path.Combine(testDir, "parent");
        var hiddenChildPath = Path.Combine(parentPath, "hidden-child.mp4");
        var visibleChildPath = Path.Combine(parentPath, "visible-child.mp4");

        _fileSystemService.GetChildrenAsync(parentPath).Returns(new List<FileNode>
        {
            new(hiddenChildPath, false),
            new(visibleChildPath, false)
        });

        _appState.HiddenFiles.Add(hiddenChildPath);
        var parentNode = new FileNode(parentPath, isDirectory: true);

        await _sut.ExpandNodeAsync(parentNode);

        Assert.Single(parentNode.Children);
        Assert.Equal("visible-child.mp4", parentNode.Children[0].Name);
        CleanupDir(testDir);
    }

    //  CloseFolder clears SelectedNode 

    /// <summary>
    /// Verifies that Close Folder clears selected node.
    /// </summary>
    [Fact]
    public async Task CloseFolder_ClearsSelectedNode()
    {
        var testDir = CreateTempDir();
        var node = new FileNode(Path.Combine(testDir, "file.mp4"), false);
        _fileSystemService.GetChildrenAsync(testDir)
            .Returns(new List<FileNode> { node });

        await _sut.OpenFolderAsync(testDir);
        _sut.SelectedNode = node;

        _sut.CloseFolder();
        Assert.Null(_sut.SelectedNode);
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that Selected Node raises property changed.
    /// </summary>
    [Fact]
    public void SelectedNode_RaisesPropertyChanged()
    {
        var node = new FileNode(@"C:\test.mp4", false);
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedNode = node;

        Assert.Contains(nameof(FileExplorerViewModel.SelectedNode), raised);
    }

    /// <summary>
    /// Verifies that Open Folder Async sets is loading during operation.
    /// </summary>
    [Fact]
    public async Task OpenFolderAsync_SetsIsLoadingDuringOperation()
    {
        var testDir = CreateTempDir();
        var tcs = new TaskCompletionSource<List<FileNode>>();
        _fileSystemService.GetChildrenAsync(testDir).Returns(tcs.Task);

        Assert.False(_sut.IsLoading);

        var task = _sut.OpenFolderAsync(testDir);

        // IsLoading is set synchronously before the first await.
        // Give the method a moment to reach the await.
        await Task.Delay(100);
        Assert.True(_sut.IsLoading);

        tcs.SetResult(new List<FileNode>());
        await task;

        Assert.False(_sut.IsLoading);
        CleanupDir(testDir);
    }

    // ── Video file filtering tests ──────────────────────────────────────

    /// <summary>
    /// Verifies that OpenFolderAsync filters out non-video files.
    /// </summary>
    [Fact]
    public async Task OpenFolder_FiltersOutNonVideoFiles()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "video.mp4"), false),
            new(Path.Combine(testDir, "notes.txt"), false),
            new(Path.Combine(testDir, "photo.jpg"), false),
            new(Path.Combine(testDir, "subfolder"), true)
        });

        await _sut.OpenFolderAsync(testDir);

        Assert.Equal(2, _sut.RootNodes.Count);
        Assert.Contains(_sut.RootNodes, n => n.Name == "video.mp4");
        Assert.Contains(_sut.RootNodes, n => n.Name == "subfolder");
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that ExpandNodeAsync filters out non-video files from children.
    /// </summary>
    [Fact]
    public async Task ExpandNode_FiltersOutNonVideoFiles()
    {
        var testDir = CreateTempDir();
        var parentPath = Path.Combine(testDir, "parent");

        _fileSystemService.GetChildrenAsync(parentPath).Returns(new List<FileNode>
        {
            new(Path.Combine(parentPath, "clip.mkv"), false),
            new(Path.Combine(parentPath, "readme.txt"), false),
            new(Path.Combine(parentPath, "child"), true)
        });

        var parentNode = new FileNode(parentPath, isDirectory: true);
        await _sut.ExpandNodeAsync(parentNode);

        Assert.Equal(2, parentNode.Children.Count);
        Assert.Contains(parentNode.Children, n => n.Name == "clip.mkv");
        Assert.Contains(parentNode.Children, n => n.Name == "child");
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that RescanFolderAsync filters out non-video files.
    /// </summary>
    [Fact]
    public async Task RescanFolder_FiltersOutNonVideoFiles()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "movie.avi"), false),
            new(Path.Combine(testDir, "data.csv"), false),
            new(Path.Combine(testDir, "shows"), true)
        });

        await _sut.OpenFolderAsync(testDir);

        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "movie.avi"), false),
            new(Path.Combine(testDir, "data.csv"), false),
            new(Path.Combine(testDir, "extra.log"), false),
            new(Path.Combine(testDir, "shows"), true)
        });

        await _sut.RescanFolderAsync();

        Assert.Equal(2, _sut.RootNodes.Count);
        Assert.Contains(_sut.RootNodes, n => n.Name == "movie.avi");
        Assert.Contains(_sut.RootNodes, n => n.Name == "shows");
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that files with AdditionalAcceptedExtensions are retained.
    /// </summary>
    [Fact]
    public async Task OpenFolder_KeepsAdditionalAcceptedExtensions()
    {
        var testDir = CreateTempDir();
        _sut.AdditionalAcceptedExtensions.Add(".funscript");

        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "video.mp4"), false),
            new(Path.Combine(testDir, "script.funscript"), false),
            new(Path.Combine(testDir, "notes.txt"), false)
        });

        await _sut.OpenFolderAsync(testDir);

        Assert.Equal(2, _sut.RootNodes.Count);
        Assert.Contains(_sut.RootNodes, n => n.Name == "video.mp4");
        Assert.Contains(_sut.RootNodes, n => n.Name == "script.funscript");
        CleanupDir(testDir);
    }

    /// <summary>
    /// Verifies that empty directories are retained even when they contain no video files.
    /// </summary>
    [Fact]
    public async Task OpenFolder_RetainsEmptyDirectories()
    {
        var testDir = CreateTempDir();
        _fileSystemService.GetChildrenAsync(testDir).Returns(new List<FileNode>
        {
            new(Path.Combine(testDir, "emptyDir"), true),
            new(Path.Combine(testDir, "readme.txt"), false)
        });

        await _sut.OpenFolderAsync(testDir);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("emptyDir", _sut.RootNodes[0].Name);
        Assert.True(_sut.RootNodes[0].IsDirectory);
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