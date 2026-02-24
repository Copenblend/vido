using Vido.Core.FileSystem;
using Vido.Core.Logging;

namespace Vido.Services.FileSystem;

/// <summary>
/// Provides file-system operations backed by <see cref="System.IO"/>.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly ILogService _log;

    public FileSystemService(ILogService log)
    {
        _log = log;
    }

    /// <inheritdoc />
    public List<FileNode> GetChildren(string directoryPath)
    {
        var nodes = new List<FileNode>();
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            if (!dirInfo.Exists) return nodes;

            // Directories first (sorted), then files (sorted)
            foreach (var dir in dirInfo.EnumerateDirectories()
                         .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                         .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new FileNode(dir.FullName, isDirectory: true));
            }

            foreach (var file in dirInfo.EnumerateFiles()
                         .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                         .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new FileNode(file.FullName, isDirectory: false));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Warning($"Access denied: {directoryPath} — {ex.Message}", nameof(FileSystemService));
        }
        catch (IOException ex)
        {
            _log.Error($"IO error reading: {directoryPath} — {ex.Message}", nameof(FileSystemService));
        }

        return nodes;
    }

    /// <inheritdoc />
    public Task<List<FileNode>> GetChildrenAsync(string directoryPath)
    {
        return Task.Run(() => GetChildren(directoryPath));
    }

    /// <inheritdoc />
    public void LoadChildren(FileNode node)
    {
        if (!node.IsDirectory || !node.NeedsLoading) return;

        node.Children.Clear(); // Remove the dummy child
        foreach (var child in GetChildren(node.FullPath))
            node.Children.Add(child);
    }
}
