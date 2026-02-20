using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.FileSystem;
using Vido.Core.State;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the file explorer panel. Manages the open folder,
/// tree state, node selection, and lazy-loading of directory children.
/// </summary>
public partial class FileExplorerViewModel : ObservableObject
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IStateService _stateService;

    /// <summary>Root-level nodes displayed in the tree.</summary>
    public ObservableCollection<FileNode> RootNodes { get; } = [];

    /// <summary>Full path of the currently open folder.</summary>
    [ObservableProperty]
    private string? _folderPath;

    /// <summary>Display name of the currently open folder.</summary>
    [ObservableProperty]
    private string? _folderName;

    /// <summary>Whether a folder is currently open.</summary>
    [ObservableProperty]
    private bool _hasFolderOpen;

    public FileExplorerViewModel(IFileSystemService fileSystemService, IStateService stateService)
    {
        _fileSystemService = fileSystemService;
        _stateService = stateService;
    }

    /// <summary>
    /// Opens a folder and populates the tree with its contents.
    /// Persists the folder path in application state.
    /// </summary>
    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        CloseFolder();

        FolderPath = path;
        FolderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(FolderName))
            FolderName = path; // Root drives like "C:\"
        HasFolderOpen = true;

        foreach (var node in _fileSystemService.GetChildren(path))
            RootNodes.Add(node);

        _stateService.Current.LastOpenFolder = path;
    }

    /// <summary>
    /// Closes the currently open folder and clears the tree.
    /// </summary>
    [RelayCommand]
    public void CloseFolder()
    {
        RootNodes.Clear();
        FolderPath = null;
        FolderName = null;
        HasFolderOpen = false;
        _stateService.Current.LastOpenFolder = null;
    }

    /// <summary>
    /// Lazily loads children of a directory node when first expanded.
    /// </summary>
    public void ExpandNode(FileNode node)
    {
        _fileSystemService.LoadChildren(node);
    }

    /// <summary>
    /// Restores the last opened folder from persisted state (called on startup).
    /// </summary>
    public void RestoreLastFolder()
    {
        var last = _stateService.Current.LastOpenFolder;
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
            OpenFolder(last);
    }
}
