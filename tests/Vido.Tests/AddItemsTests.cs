using NSubstitute;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="FileExplorerViewModel.AddItems"/> — additive drag-drop
/// into the file explorer with sorting (folders first, then files, alpha).
/// </summary>
public sealed class AddItemsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IFileSystemService _fileSystemService = Substitute.For<IFileSystemService>();
    private readonly IStateService _stateService = Substitute.For<IStateService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogService _logService = Substitute.For<ILogService>();
    private readonly FileExplorerViewModel _sut;

    public AddItemsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"VidoAddItemsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _stateService.Current.Returns(new AppState());
        _settingsService.Current.Returns(new AppSettings());
        _sut = new FileExplorerViewModel(_fileSystemService, _stateService, _settingsService, _logService);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ── Basic additive behaviour ──

    [Fact]
    public void AddItems_SingleVideoFile_AddsToRoot()
    {
        var videoPath = CreateFile("test.mp4");

        _sut.AddItems([videoPath]);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("test.mp4", _sut.RootNodes[0].Name);
        Assert.False(_sut.RootNodes[0].IsDirectory);
        Assert.True(_sut.HasFolderOpen);
    }

    [Fact]
    public void AddItems_SingleFolder_AddsToRoot()
    {
        var folderPath = CreateSubDir("MyFolder");

        _sut.AddItems([folderPath]);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("MyFolder", _sut.RootNodes[0].Name);
        Assert.True(_sut.RootNodes[0].IsDirectory);
        Assert.True(_sut.HasFolderOpen);
    }

    [Fact]
    public void AddItems_IsAdditive_DoesNotClearExisting()
    {
        var video1 = CreateFile("a.mp4");
        var video2 = CreateFile("b.mkv");

        _sut.AddItems([video1]);
        Assert.Single(_sut.RootNodes);

        _sut.AddItems([video2]);
        Assert.Equal(2, _sut.RootNodes.Count);
    }

    [Fact]
    public void AddItems_SkipsDuplicatePaths()
    {
        var videoPath = CreateFile("dup.mp4");

        _sut.AddItems([videoPath]);
        _sut.AddItems([videoPath]);

        Assert.Single(_sut.RootNodes);
    }

    [Fact]
    public void AddItems_SkipsDuplicatePathsCaseInsensitive()
    {
        var videoPath = CreateFile("case.mp4");
        var upperPath = videoPath.ToUpperInvariant();

        _sut.AddItems([videoPath]);
        _sut.AddItems([upperPath]);

        Assert.Single(_sut.RootNodes);
    }

    // ── Filtering ──

    [Fact]
    public void AddItems_ReturnsTrue_WhenUnsupportedFilePresent()
    {
        var txt = CreateFile("readme.txt");

        var hasUnsupported = _sut.AddItems([txt]);

        Assert.True(hasUnsupported);
        Assert.Empty(_sut.RootNodes); // txt not added
    }

    [Fact]
    public void AddItems_ReturnsFalse_WhenAllSupported()
    {
        var video = CreateFile("clip.mp4");

        var hasUnsupported = _sut.AddItems([video]);

        Assert.False(hasUnsupported);
    }

    [Fact]
    public void AddItems_SkipsNonExistentPaths()
    {
        var fakePath = Path.Combine(_tempDir, "nonexistent.mp4");

        _sut.AddItems([fakePath]);

        Assert.Empty(_sut.RootNodes);
    }

    [Fact]
    public void AddItems_SkipsEmptyAndWhitespace()
    {
        _sut.AddItems(["", "  ", null!]);

        Assert.Empty(_sut.RootNodes);
    }

    [Fact]
    public void AddItems_MixedBatch_AddsValidOnly()
    {
        var video = CreateFile("good.mp4");
        var txt = CreateFile("bad.txt");
        var folder = CreateSubDir("folder");
        var fake = Path.Combine(_tempDir, "nope.mp4");

        var hasUnsupported = _sut.AddItems([video, txt, folder, fake]);

        Assert.True(hasUnsupported); // txt was unsupported
        Assert.Equal(2, _sut.RootNodes.Count); // video + folder
    }

    // ── Sorting ──

    [Fact]
    public void AddItems_SortsFoldersFirst_ThenFilesAlpha()
    {
        var video1 = CreateFile("c_video.mp4");
        var video2 = CreateFile("a_video.mp4");
        var folder1 = CreateSubDir("z_folder");
        var folder2 = CreateSubDir("a_folder");

        _sut.AddItems([video1, video2, folder1, folder2]);

        Assert.Equal(4, _sut.RootNodes.Count);
        // Folders first, alphabetical
        Assert.Equal("a_folder", _sut.RootNodes[0].Name);
        Assert.True(_sut.RootNodes[0].IsDirectory);
        Assert.Equal("z_folder", _sut.RootNodes[1].Name);
        Assert.True(_sut.RootNodes[1].IsDirectory);
        // Files second, alphabetical
        Assert.Equal("a_video.mp4", _sut.RootNodes[2].Name);
        Assert.False(_sut.RootNodes[2].IsDirectory);
        Assert.Equal("c_video.mp4", _sut.RootNodes[3].Name);
        Assert.False(_sut.RootNodes[3].IsDirectory);
    }

    [Fact]
    public void AddItems_PreservesSortAfterMultipleAdds()
    {
        var videoZ = CreateFile("z.mp4");
        var videoA = CreateFile("a.mp4");
        var folder = CreateSubDir("m_folder");

        _sut.AddItems([videoZ]);
        _sut.AddItems([folder]);
        _sut.AddItems([videoA]);

        // Folder first, then files alpha
        Assert.Equal("m_folder", _sut.RootNodes[0].Name);
        Assert.Equal("a.mp4", _sut.RootNodes[1].Name);
        Assert.Equal("z.mp4", _sut.RootNodes[2].Name);
    }

    // ── FolderName and HasFolderOpen ──

    [Fact]
    public void AddItems_SetsFolderNameToCustom_WhenNoFolderOpen()
    {
        var video = CreateFile("clip.mp4");

        _sut.AddItems([video]);

        Assert.Equal("CUSTOM", _sut.FolderName);
    }

    [Fact]
    public void AddItems_ChangesFolderNameToCustom_WhenFolderAlreadyOpen()
    {
        // Open a real folder first
        var realFolder = CreateSubDir("RealFolder");
        _fileSystemService.GetChildren(realFolder).Returns(new List<FileNode>());
        _sut.OpenFolder(realFolder);

        Assert.Equal("RealFolder", _sut.FolderName);

        // Additive add
        var video = CreateFile("extra.mp4");
        _sut.AddItems([video]);

        // FolderName changes to "CUSTOM" because items were added beyond the opened folder
        Assert.Equal("CUSTOM", _sut.FolderName);
    }

    // ── Folder nodes have dummy child for lazy loading ──

    [Fact]
    public void AddItems_FolderNodes_HaveDummyChild()
    {
        var folder = CreateSubDir("LazyFolder");

        _sut.AddItems([folder]);

        var node = _sut.RootNodes[0];
        Assert.True(node.IsDirectory);
        Assert.True(node.NeedsLoading);
        Assert.Single(node.Children);
    }

    // ── All video extensions are accepted ──

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".avi")]
    [InlineData(".mkv")]
    [InlineData(".mov")]
    [InlineData(".wmv")]
    [InlineData(".flv")]
    [InlineData(".webm")]
    public void AddItems_AcceptsAllVideoExtensions(string ext)
    {
        var video = CreateFile($"video{ext}");

        _sut.AddItems([video]);

        Assert.Single(_sut.RootNodes);
        Assert.True(_sut.RootNodes[0].IsVideoFile);
    }

    // ── RemoveFile ──

    [Fact]
    public void RemoveFile_RemovesNodeFromTree()
    {
        var video = CreateFile("remove_me.mp4");
        _sut.AddItems([video]);
        Assert.Single(_sut.RootNodes);

        var node = _sut.RootNodes[0];
        _sut.RemoveFile(node);

        Assert.Empty(_sut.RootNodes);
    }

    [Fact]
    public void RemoveFile_ResetsState_WhenLastNodeRemoved()
    {
        var video = CreateFile("only.mp4");
        _sut.AddItems([video]);
        Assert.True(_sut.HasFolderOpen);

        _sut.RemoveFile(_sut.RootNodes[0]);

        Assert.False(_sut.HasFolderOpen);
        Assert.Null(_sut.FolderName);
    }

    [Fact]
    public void RemoveFile_KeepsOtherNodes()
    {
        var video1 = CreateFile("keep.mp4");
        var video2 = CreateFile("remove.mp4");
        _sut.AddItems([video1, video2]);
        Assert.Equal(2, _sut.RootNodes.Count);

        var toRemove = _sut.RootNodes.First(n => n.Name == "remove.mp4");
        _sut.RemoveFile(toRemove);

        Assert.Single(_sut.RootNodes);
        Assert.Equal("keep.mp4", _sut.RootNodes[0].Name);
        Assert.True(_sut.HasFolderOpen);
    }

    [Fact]
    public void RemoveFile_DoesNotAddToHiddenState()
    {
        var video = CreateFile("no_hide.mp4");
        _sut.AddItems([video]);

        _sut.RemoveFile(_sut.RootNodes[0]);

        Assert.Empty(_stateService.Current.HiddenFiles);
    }

    [Fact]
    public void RemoveFile_NullNode_DoesNothing()
    {
        var video = CreateFile("safe.mp4");
        _sut.AddItems([video]);

        _sut.RemoveFile(null);

        Assert.Single(_sut.RootNodes);
    }

    // ── Custom title logic ──

    [Fact]
    public void AddItems_DoesNotChangeFolderName_WhenNothingAdded()
    {
        // Open a folder
        var folder = CreateSubDir("Original");
        _fileSystemService.GetChildren(folder).Returns(new List<FileNode>());
        _sut.OpenFolder(folder);

        // Try to add a non-existent file
        _sut.AddItems([Path.Combine(_tempDir, "ghost.mp4")]);

        // FolderName remains unchanged because nothing was actually added
        Assert.Equal("Original", _sut.FolderName);
    }

    [Fact]
    public void OpenFolder_AfterCustomTitle_RestoresRealName()
    {
        // Add items first (no folder open)
        var video = CreateFile("v.mp4");
        _sut.AddItems([video]);
        Assert.Equal("CUSTOM", _sut.FolderName);

        // Open a real folder — should reset to folder name
        var folder = CreateSubDir("Restored");
        _fileSystemService.GetChildren(folder).Returns(new List<FileNode>());
        _sut.OpenFolder(folder);

        Assert.Equal("Restored", _sut.FolderName);
    }

    // ── Helpers ──

    private string CreateFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "dummy");
        return path;
    }

    private string CreateSubDir(string name)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
