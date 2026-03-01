using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Vido.Core.FileSystem;

/// <summary>
/// Represents a single node in the file explorer tree.
/// Can be a file or a directory. Directories use a dummy-child pattern
/// for lazy-loading: a sentinel child is added so the TreeView shows
/// an expander; real children are loaded on first expansion.
/// </summary>
public sealed class FileNode : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IsHiddenChangedArgs = new(nameof(IsHidden));

    private bool _isHidden;

    /// <summary>
    /// Full path to the file or directory.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Display name (file/folder name only, not full path).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether this node is a directory.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Whether this is a recognized video file.
    /// </summary>
    public bool IsVideoFile { get; }

    /// <summary>
    /// Whether this node is hidden from view by the user.
    /// Hidden nodes appear dimmed when "Show Hidden Files" is enabled.
    /// </summary>
    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (_isHidden == value) return;
            _isHidden = value;
            PropertyChanged?.Invoke(this, IsHiddenChangedArgs);
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Child nodes. Observable so the TreeView updates when children are
    /// lazily loaded. Directories start with a single <see cref="DummyChild"/>.
    /// </summary>
    public ObservableCollection<FileNode> Children { get; } = [];

    /// <summary>
    /// Video file extensions recognized by Vido.
    /// </summary>
    public static readonly FrozenSet<string> VideoExtensions =
        new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sentinel child used to show the expand arrow before real children are loaded.
    /// </summary>
    internal static readonly FileNode DummyChild = new(string.Empty, false);

    /// <summary>
    /// Whether this directory still has the sentinel and needs real children loaded.
    /// </summary>
    public bool NeedsLoading => IsDirectory && Children.Count == 1
                                && ReferenceEquals(Children[0], DummyChild);
                           
    /// <summary>
    /// Creates a file or directory node for the explorer tree, extracting the display
    /// name from the path and detecting whether it is a recognized video file.
    /// Directories receive a sentinel child so the TreeView renders an expand arrow.
    /// </summary>
    /// <param name="fullPath">Absolute path to the file or directory on disk.</param>
    /// <param name="isDirectory">True if this node represents a directory; false for a file.</param>
    public FileNode(string fullPath, bool isDirectory)
    {
        FullPath = fullPath;
        Name = string.IsNullOrEmpty(fullPath)
            ? string.Empty
            : (Path.GetFileName(fullPath) ?? fullPath);
        IsDirectory = isDirectory;
        IsVideoFile = !isDirectory && VideoExtensions.Contains(
            Path.GetExtension(fullPath));

        // Directories get a placeholder child so the TreeView shows an expander arrow.
        if (isDirectory)
            Children.Add(DummyChild);
    }
}
