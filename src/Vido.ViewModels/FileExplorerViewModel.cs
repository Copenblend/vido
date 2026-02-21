using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
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
    private readonly ILogService _logService;

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

    /// <summary>The currently selected node in the tree (set from the view).</summary>
    [ObservableProperty]
    private FileNode? _selectedNode;

    /// <summary>
    /// When true, hidden files/folders appear in the tree (dimmed).
    /// When false, hidden items are excluded from the tree entirely.
    /// </summary>
    [ObservableProperty]
    private bool _showHiddenFiles;

    public FileExplorerViewModel(IFileSystemService fileSystemService, IStateService stateService, ILogService logService)
    {
        _fileSystemService = fileSystemService;
        _stateService = stateService;
        _logService = logService;
    }

    /// <summary>
    /// Returns the set of hidden file paths from state for filtering.
    /// </summary>
    private HashSet<string> GetHiddenPaths()
    {
        return new HashSet<string>(_stateService.Current.HiddenFiles, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies hidden-file logic to a list of nodes returned by the file system.
    /// When <see cref="ShowHiddenFiles"/> is false, hidden nodes are excluded.
    /// When true, hidden nodes are included but marked with <see cref="FileNode.IsHidden"/>.
    /// Returns only the nodes that should appear in the tree.
    /// </summary>
    private List<FileNode> ApplyHiddenFilter(List<FileNode> nodes)
    {
        var hidden = GetHiddenPaths();
        if (hidden.Count == 0) return nodes;

        var result = new List<FileNode>(nodes.Count);
        foreach (var node in nodes)
        {
            if (hidden.Contains(node.FullPath))
            {
                if (ShowHiddenFiles)
                {
                    node.IsHidden = true;
                    result.Add(node);
                }
                // else: skip — hidden and not showing hidden
            }
            else
            {
                result.Add(node);
            }
        }
        return result;
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

        var allNodes = _fileSystemService.GetChildren(path);
        foreach (var node in ApplyHiddenFilter(allNodes))
            RootNodes.Add(node);

        _stateService.Current.LastOpenFolder = path;
        _logService.Info($"Folder opened: {path}", "Explorer");
    }

    /// <summary>
    /// Closes the currently open folder and clears the tree.
    /// </summary>
    [RelayCommand]
    public void CloseFolder()
    {
        var wasOpen = FolderPath;
        RootNodes.Clear();
        FolderPath = null;
        FolderName = null;
        HasFolderOpen = false;
        SelectedNode = null;
        _stateService.Current.LastOpenFolder = null;
        if (wasOpen is not null)
            _logService.Info("Folder closed", "Explorer");
    }

    /// <summary>
    /// Lazily loads children of a directory node when first expanded.
    /// </summary>
    public void ExpandNode(FileNode node)
    {
        if (!node.IsDirectory || !node.NeedsLoading) return;

        node.Children.Clear(); // remove dummy
        var allChildren = _fileSystemService.GetChildren(node.FullPath);
        foreach (var child in ApplyHiddenFilter(allChildren))
            node.Children.Add(child);
    }

    /// <summary>
    /// Re-reads the current folder from disk, preserving expanded state where possible.
    /// Hidden files remain hidden (they persist in state).
    /// </summary>
    [RelayCommand]
    public void RescanFolder()
    {
        if (FolderPath is null) return;

        // Collect expanded directory paths before clearing
        var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExpandedPaths(RootNodes, expandedPaths);

        RootNodes.Clear();
        var allNodes = _fileSystemService.GetChildren(FolderPath);
        foreach (var node in ApplyHiddenFilter(allNodes))
        {
            RootNodes.Add(node);
            RestoreExpandedState(node, expandedPaths);
        }
    }

    /// <summary>
    /// Hides a file or folder from the explorer view (does NOT delete from disk).
    /// The path is added to persistent state so it remains hidden across sessions.
    /// If <see cref="ShowHiddenFiles"/> is true, the node stays but is dimmed.
    /// If false, the node is removed from the tree.
    /// </summary>
    [RelayCommand]
    public void HideFile(FileNode? node)
    {
        if (node is null) return;

        // Add to hidden list in state
        var hiddenFiles = _stateService.Current.HiddenFiles;
        if (!hiddenFiles.Contains(node.FullPath, StringComparer.OrdinalIgnoreCase))
            hiddenFiles.Add(node.FullPath);

        if (ShowHiddenFiles)
        {
            // Mark it hidden visually but keep in tree
            node.IsHidden = true;
        }
        else
        {
            // Remove from tree entirely
            RemoveNodeFromTree(RootNodes, node);
        }
    }

    /// <summary>
    /// Unhides a previously hidden file or folder, removing it from the hidden list
    /// and clearing the <see cref="FileNode.IsHidden"/> flag.
    /// </summary>
    [RelayCommand]
    public void UnhideFile(FileNode? node)
    {
        if (node is null) return;

        _stateService.Current.HiddenFiles.RemoveAll(
            p => string.Equals(p, node.FullPath, StringComparison.OrdinalIgnoreCase));

        node.IsHidden = false;
    }

    /// <summary>
    /// Opens the file's parent directory in Windows Explorer with the file selected.
    /// </summary>
    [RelayCommand]
    public void RevealInExplorer(FileNode? node)
    {
        if (node is null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{node.FullPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently ignore if explorer cannot be launched
        }
    }

    /// <summary>
    /// Toggles visibility of hidden files. When toggled on, hidden items appear dimmed.
    /// When toggled off, hidden items are removed from the tree.
    /// </summary>
    [RelayCommand]
    public void ToggleShowHiddenFiles()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        RefreshTree();
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

    /// <summary>
    /// Rebuilds the tree from disk, preserving expanded state.
    /// Used when the hidden-files visibility toggle changes.
    /// </summary>
    private void RefreshTree()
    {
        if (FolderPath is null) return;

        var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExpandedPaths(RootNodes, expandedPaths);

        RootNodes.Clear();
        var allNodes = _fileSystemService.GetChildren(FolderPath);
        foreach (var node in ApplyHiddenFilter(allNodes))
        {
            RootNodes.Add(node);
            RestoreExpandedState(node, expandedPaths);
        }
    }

    /// <summary>
    /// Recursively collects the full paths of expanded directory nodes.
    /// A node is "expanded" if it has been loaded (no dummy child) and has children.
    /// </summary>
    private static void CollectExpandedPaths(
        IEnumerable<FileNode> nodes, HashSet<string> paths)
    {
        foreach (var node in nodes)
        {
            if (node.IsDirectory && !node.NeedsLoading)
            {
                paths.Add(node.FullPath);
                CollectExpandedPaths(node.Children, paths);
            }
        }
    }

    /// <summary>
    /// Restores expanded state for nodes whose paths were previously expanded.
    /// </summary>
    private void RestoreExpandedState(FileNode node, HashSet<string> expandedPaths)
    {
        if (!node.IsDirectory || !expandedPaths.Contains(node.FullPath)) return;

        // Manually load children (bypass NeedsLoading check since we just created the node)
        if (node.NeedsLoading)
        {
            node.Children.Clear();
            var allChildren = _fileSystemService.GetChildren(node.FullPath);
            foreach (var child in ApplyHiddenFilter(allChildren))
                node.Children.Add(child);
        }

        foreach (var child in node.Children)
            RestoreExpandedState(child, expandedPaths);
    }

    /// <summary>
    /// Removes a node from a collection by reference. Returns true if found.
    /// </summary>
    private static bool RemoveNodeFromTree(
        ObservableCollection<FileNode> nodes, FileNode target)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], target))
            {
                nodes.RemoveAt(i);
                return true;
            }

            if (nodes[i].IsDirectory && !nodes[i].NeedsLoading)
            {
                if (RemoveNodeFromTree(nodes[i].Children, target))
                    return true;
            }
        }
        return false;
    }
}
