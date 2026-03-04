using System.IO;

namespace Vido.Core.Models.Playlists;

/// <summary>
/// Represents a single item in a playlist.
/// Each item references a file on disk (video, funscript, or any other type).
/// </summary>
public sealed class PlaylistItem : IEquatable<PlaylistItem>
{
    /// <summary>
    /// Absolute path to the file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Display name derived from <see cref="FilePath"/>.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Initializes a new <see cref="PlaylistItem"/> referencing the specified file path.
    /// </summary>
    /// <param name="filePath">Absolute path to the file. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    public PlaylistItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    /// <summary>
    /// Equality based on <see cref="FilePath"/> (case-insensitive on Windows).
    /// </summary>
    /// <param name="other">The other item to compare.</param>
    /// <returns><c>true</c> if both items reference the same file path (case-insensitive).</returns>
    public bool Equals(PlaylistItem? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PlaylistItem);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath);

    /// <inheritdoc />
    public override string ToString() => FileName;
}
