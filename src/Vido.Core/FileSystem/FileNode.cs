using System.Collections.ObjectModel;

namespace Vido.Core.FileSystem;

/// <summary>
/// Represents a single node in the file explorer tree.
/// Can be a file or a directory. Directories use a dummy-child pattern
/// for lazy-loading: a sentinel child is added so the TreeView shows
/// an expander; real children are loaded on first expansion.
/// </summary>
public sealed class FileNode
{
    /// <summary>Full path to the file or directory.</summary>
    public string FullPath { get; }

    /// <summary>Display name (file/folder name only, not full path).</summary>
    public string Name { get; }

    /// <summary>Whether this node is a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>Whether this is a recognized video file.</summary>
    public bool IsVideoFile { get; }

    /// <summary>
    /// Child nodes. Observable so the TreeView updates when children are
    /// lazily loaded. Directories start with a single <see cref="DummyChild"/>.
    /// </summary>
    public ObservableCollection<FileNode> Children { get; } = [];

    /// <summary>
    /// Video file extensions recognized by Vido.
    /// </summary>
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
    };

    /// <summary>Sentinel child used to show the expand arrow before real children are loaded.</summary>
    internal static readonly FileNode DummyChild = new(string.Empty, false);

    /// <summary>Whether this directory still has the sentinel and needs real children loaded.</summary>
    public bool NeedsLoading => IsDirectory && Children.Count == 1
                                && ReferenceEquals(Children[0], DummyChild);

    public FileNode(string fullPath, bool isDirectory)
    {
        FullPath = fullPath;
        Name = string.IsNullOrEmpty(fullPath)
            ? string.Empty
            : (Path.GetFileName(fullPath) ?? fullPath);
        IsDirectory = isDirectory;
        IsVideoFile = !isDirectory && VideoExtensions.Contains(
            Path.GetExtension(fullPath).ToLowerInvariant());

        // Directories get a placeholder child so the TreeView shows an expander arrow.
        if (isDirectory)
            Children.Add(DummyChild);
    }
}
