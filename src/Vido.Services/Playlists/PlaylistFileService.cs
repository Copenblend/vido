using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vido.Core.Models.Playlists;

namespace Vido.Services.Playlists;

/// <summary>
/// Handles serialization and deserialization of <see cref="Playlist"/> objects
/// to and from <c>.vidpl</c> JSON playlist files.
/// </summary>
public sealed class PlaylistFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates a new empty playlist with a default name.
    /// </summary>
    /// <returns>A new <see cref="Playlist"/> with the name "Untitled Playlist" and <see cref="Playlist.IsDirty"/> set to <c>false</c>.</returns>
    public Playlist CreateNew()
    {
        var playlist = new Playlist("Untitled Playlist");
        playlist.IsDirty = false;
        return playlist;
    }

    /// <summary>
    /// Serializes a <see cref="Playlist"/> to a JSON <c>.vidpl</c> file.
    /// Creates the target directory if it does not exist.
    /// </summary>
    /// <param name="playlist">The playlist to save. Must not be <c>null</c>.</param>
    /// <param name="filePath">The destination file path. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="playlist"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    public async Task SaveAsync(Playlist playlist, string filePath)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var dto = new PlaylistDto
        {
            Name = playlist.Name,
            Items = playlist.Items.Select(i => new PlaylistItemDto { FilePath = i.FilePath }).ToList()
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);

        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions);

        playlist.FilePath = filePath;
        playlist.IsDirty = false;
    }

    /// <summary>
    /// Deserializes a <see cref="Playlist"/> from a JSON <c>.vidpl</c> file.
    /// Items whose files no longer exist are kept in the list (UI will flag them).
    /// </summary>
    /// <param name="filePath">The playlist file to load. Must not be null or whitespace.</param>
    /// <returns>The deserialized playlist with <see cref="Playlist.IsDirty"/> set to <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">The playlist file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not valid JSON or missing required fields.</exception>
    public async Task<Playlist> LoadAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Playlist file not found.", filePath);

        PlaylistDto? dto;
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);

        try
        {
            dto = await JsonSerializer.DeserializeAsync<PlaylistDto>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse playlist file: {filePath}", ex);
        }

        if (dto is null)
            throw new InvalidDataException($"Playlist file is empty or invalid: {filePath}");

        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Untitled Playlist" : dto.Name;
        var items = (dto.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.FilePath))
            .Select(i => new PlaylistItem(i.FilePath!));

        var playlist = new Playlist(name, items)
        {
            FilePath = filePath,
            IsDirty = false
        };

        return playlist;
    }

    // ── DTOs for JSON serialization ──

    internal sealed class PlaylistDto
    {
        public string? Name { get; set; }
        public List<PlaylistItemDto>? Items { get; set; }
    }

    internal sealed class PlaylistItemDto
    {
        public string? FilePath { get; set; }
    }
}
