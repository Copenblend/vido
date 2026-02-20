namespace Vido.Core.FileSystem;

/// <summary>
/// Provides file-system operations for the file explorer.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Gets the top-level items (files and directories) inside <paramref name="directoryPath"/>.
    /// Directories are listed first, followed by files. Both groups are sorted alphabetically.
    /// Each directory node contains a dummy child for lazy-loading.
    /// </summary>
    List<FileNode> GetChildren(string directoryPath);

    /// <summary>
    /// Replaces the dummy child of <paramref name="node"/> with real children from disk.
    /// No-op if already loaded or if the node is not a directory.
    /// </summary>
    void LoadChildren(FileNode node);
}
