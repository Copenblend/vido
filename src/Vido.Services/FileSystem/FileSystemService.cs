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

    // Shared options: single-pass, OS-level hidden-file skip, no extra stat calls.
    // .NET 8 uses FindFirstFileEx with FIND_FIRST_EX_LARGE_FETCH internally,
    // which fetches directory entries in large SMB batches — same as Explorer.
    private static readonly EnumerationOptions s_shallowOptions = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        RecurseSubdirectories = false
    };

    /// <inheritdoc />
    public List<FileNode> GetChildren(string directoryPath)
    {
        var dirs = new List<FileNode>();
        var files = new List<FileNode>();
        try
        {
            // Single pass over the directory — one SMB round-trip sequence
            // instead of two separate EnumerateDirectories + EnumerateFiles calls.
            // AttributesToSkip filters hidden entries at the OS enumeration level
            // so no per-entry Attributes check or extra stat call is needed.
            foreach (var entry in new DirectoryInfo(directoryPath)
                         .EnumerateFileSystemInfos("*", s_shallowOptions))
            {
                if (entry is DirectoryInfo)
                    dirs.Add(new FileNode(entry.FullName, isDirectory: true));
                else
                    files.Add(new FileNode(entry.FullName, isDirectory: false));
            }

            // In-place sort avoids LINQ OrderBy allocation overhead
            dirs.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            files.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Warning($"Access denied: {directoryPath} — {ex.Message}", nameof(FileSystemService));
        }
        catch (DirectoryNotFoundException)
        {
            // Path doesn't exist — return empty
        }
        catch (IOException ex)
        {
            _log.Error($"IO error reading: {directoryPath} — {ex.Message}", nameof(FileSystemService));
        }

        var nodes = new List<FileNode>(dirs.Count + files.Count);
        nodes.AddRange(dirs);
        nodes.AddRange(files);
        return nodes;
    }

    /// <inheritdoc />
    public Task<List<FileNode>> GetChildrenAsync(string directoryPath)
    {
        return Task.Run(() => GetChildren(directoryPath));
    }

}
