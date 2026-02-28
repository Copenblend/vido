using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
using Vido.Core.Settings;
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
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;

    /// <summary>
    /// Root-level nodes displayed in the tree.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = [];

    /// <summary>
    /// Full path of the currently open folder.
    /// </summary>
    [ObservableProperty]
    private string? _folderPath;

    /// <summary>
    /// Display name of the currently open folder.
    /// </summary>
    [ObservableProperty]
    private string? _folderName;

    /// <summary>
    /// Whether a folder is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _hasFolderOpen;

    /// <summary>
    /// Whether the explorer is currently loading file data.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// The currently selected node in the tree (set from the view).
    /// </summary>
    [ObservableProperty]
    private FileNode? _selectedNode;

    /// <summary>
    /// File extensions accepted by plugins (e.g. ".sample"). Files with these
    /// extensions will be accepted during drag-and-drop and menu-based addition
    /// in addition to the built-in video extensions.
    /// </summary>
    public HashSet<string> AdditionalAcceptedExtensions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When true, hidden files/folders appear in the tree (dimmed).
    /// When false, hidden items are excluded from the tree entirely.
    /// </summary>
    [ObservableProperty]
    private bool _showHiddenFiles;
    /// <summary>
    /// Creates a file explorer view model wired to file system, state, settings, and logging services.
    /// Restores the hidden-files toggle from persisted settings.
    /// </summary>
    /// <param name="fileSystemService">Service for enumerating directory contents.</param>
    /// <param name="stateService">Service for persisting explorer state (last folder, hidden files).</param>
    /// <param name="settingsService">Service for persisting user preferences (show hidden files toggle).</param>
    /// <param name="logService">Logging service for explorer operations.</param>
    public FileExplorerViewModel(IFileSystemService fileSystemService, IStateService stateService,
        ISettingsService settingsService, ILogService logService)
    {
        _fileSystemService = fileSystemService;
        _stateService = stateService;
        _settingsService = settingsService;
        _logService = logService;
        _showHiddenFiles = settingsService.Current.ShowHiddenFiles;
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
    public async Task OpenFolderAsync(string path)
    {
        IsLoading = true;

        try
        {
            // Check existence on thread pool so UI stays free on slow/network paths
            if (!await Task.Run(() => Directory.Exists(path)))
                return;

            // Enumerate children entirely on thread pool
            var allNodes = await _fileSystemService.GetChildrenAsync(path);
            var filtered = ApplyHiddenFilter(allNodes);

            CloseFolder();

            FolderPath = path;
            FolderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(FolderName))
                FolderName = path; // Root drives like "C:\"
            HasFolderOpen = true;

            RootNodes = new ObservableCollection<FileNode>(filtered);
        }
        finally
        {
            IsLoading = false;
        }

        _stateService.Current.LastOpenFolder = path;
        _stateService.QueueSave();
        _logService.Info($"Folder opened: {path}", "Explorer");
    }

    /// <summary>
    /// Additively inserts files and folders into the explorer tree.
    /// Folders are added as expandable directory nodes. Only recognized video
    /// files are added; non-video files are skipped (the caller handles
    /// the unsupported notification). Duplicate paths are ignored.
    /// Returns true if any unsupported (non-video) files were encountered.
    /// </summary>
    public bool AddItems(IReadOnlyList<string> paths)
    {
        bool hasUnsupported = false;
        bool added = false;

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (Directory.Exists(path))
            {
                if (!ContainsRootPath(path))
                {
                    RootNodes.Add(new FileNode(path, isDirectory: true));
                    added = true;
                }
            }
            else if (File.Exists(path))
            {
                var ext = Path.GetExtension(path);
                if (FileNode.VideoExtensions.Contains(ext) || AdditionalAcceptedExtensions.Contains(ext))
                {
                    if (!ContainsRootPath(path))
                    {
                        RootNodes.Add(new FileNode(path, isDirectory: false));
                        added = true;
                    }
                }
                else
                {
                    hasUnsupported = true;
                }
            }
        }

        if (added)
        {
            SortRootNodes();

            if (!HasFolderOpen)
            {
                HasFolderOpen = true;
            }

            // Title becomes "CUSTOM" whenever items are added beyond the
            // originally opened folder (or when no folder was opened at all).
            FolderName = "CUSTOM";

            _logService.Info($"Added items to explorer", "Explorer");
        }

        return hasUnsupported;
    }

    /// <summary>
    /// Closes the currently open folder and clears the tree.
    /// </summary>
    [RelayCommand]
    public void CloseFolder()
    {
        var wasOpen = FolderPath;
        RootNodes = [];
        FolderPath = null;
        FolderName = null;
        HasFolderOpen = false;
        SelectedNode = null;
        _stateService.Current.LastOpenFolder = null;
        _stateService.QueueSave();
        if (wasOpen is not null)
            _logService.Info("Folder closed", "Explorer");
    }

    /// <summary>
    /// Lazily loads children of a directory node when first expanded.
    /// </summary>
    public async Task ExpandNodeAsync(FileNode node)
    {
        if (!node.IsDirectory || !node.NeedsLoading) return;

        node.Children.Clear(); // remove dummy
        var allChildren = await _fileSystemService.GetChildrenAsync(node.FullPath);
        foreach (var child in ApplyHiddenFilter(allChildren))
            node.Children.Add(child);
    }

    /// <summary>
    /// Re-reads the current folder from disk, preserving expanded state where possible.
    /// Hidden files remain hidden (they persist in state).
    /// </summary>
    [RelayCommand]
    public async Task RescanFolderAsync()
    {
        if (FolderPath is null) return;

        IsLoading = true;

        try
        {
            // Collect expanded directory paths before clearing
            var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectExpandedPaths(RootNodes, expandedPaths);

            var allNodes = await _fileSystemService.GetChildrenAsync(FolderPath);
            var filtered = ApplyHiddenFilter(allNodes);

            RootNodes = new ObservableCollection<FileNode>(filtered);

            foreach (var node in RootNodes)
                await RestoreExpandedStateAsync(node, expandedPaths);
        }
        finally
        {
            IsLoading = false;
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

        _stateService.QueueSave();

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
    /// Removes a file or folder node from the explorer tree without persisting
    /// to the hidden-files list. Unlike <see cref="HideFile"/>, this is a
    /// transient removal — the item will reappear on folder rescan.
    /// If the last root node is removed, resets the folder-open state.
    /// </summary>
    [RelayCommand]
    public void RemoveFile(FileNode? node)
    {
        if (node is null) return;

        RemoveNodeFromTree(RootNodes, node);

        if (RootNodes.Count == 0)
        {
            HasFolderOpen = false;
            FolderName = null;
        }

        _logService.Info($"Removed from explorer: {node.Name}", "Explorer");
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

        _stateService.QueueSave();
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
        catch (Exception)
        {
            // Explorer launch failed — non-critical, ignore
        }
    }

    /// <summary>
    /// Toggles visibility of hidden files. When toggled on, hidden items appear dimmed.
    /// When toggled off, hidden items are removed from the tree.
    /// </summary>
    [RelayCommand]
    public async Task ToggleShowHiddenFilesAsync()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        _settingsService.Current.ShowHiddenFiles = ShowHiddenFiles;
        _settingsService.QueueSave();
        await RescanFolderAsync();
    }

    /// <summary>
    /// Restores the last opened folder from persisted state (called on startup).
    /// </summary>
    public async Task RestoreLastFolderAsync()
    {
        var last = _stateService.Current.LastOpenFolder;
        if (!string.IsNullOrEmpty(last))
            await OpenFolderAsync(last);
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
    private async Task RestoreExpandedStateAsync(FileNode node, HashSet<string> expandedPaths)
    {
        if (!node.IsDirectory || !expandedPaths.Contains(node.FullPath)) return;

        // Manually load children (bypass NeedsLoading check since we just created the node)
        if (node.NeedsLoading)
        {
            node.Children.Clear();
            var allChildren = await _fileSystemService.GetChildrenAsync(node.FullPath);
            foreach (var child in ApplyHiddenFilter(allChildren))
                node.Children.Add(child);
        }

        foreach (var child in node.Children)
            await RestoreExpandedStateAsync(child, expandedPaths);
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

    /// <summary>
    /// Checks whether a path already exists at the root level of the tree.
    /// </summary>
    private bool ContainsRootPath(string path) =>
        RootNodes.Any(n => string.Equals(n.FullPath, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sorts root nodes: directories first, then files, both alphabetically.
    /// </summary>
    private void SortRootNodes()
    {
        var sorted = RootNodes
            .OrderByDescending(n => n.IsDirectory)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RootNodes.Clear();
        foreach (var node in sorted)
            RootNodes.Add(node);
    }
}
