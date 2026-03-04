using System.Collections.ObjectModel;
using System.IO;
using Vido.Core.Models.Playlists;
using Vido.Core.Playlists;
using Vido.Services.Playlists;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-017: Playlist services integrated into Vido.Services.
/// Covers <see cref="PlaylistFileService"/> and <see cref="PlaylistProvider"/>.
/// </summary>
public sealed class PlaylistServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PlaylistProvider _provider;
    private readonly ObservableCollection<PlaylistItem> _items;

    public PlaylistServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _provider = new PlaylistProvider();
        _items =
        [
            new PlaylistItem(@"C:\Videos\a.mp4"),
            new PlaylistItem(@"C:\Videos\b.mp4"),
            new PlaylistItem(@"C:\Videos\c.mp4"),
        ];
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistFileService Tests                                      ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies CreateNew returns a playlist with default name.
    /// </summary>
    [Fact]
    public void FileService_CreateNew_ReturnsUntitledPlaylist()
    {
        var svc = new PlaylistFileService();

        var playlist = svc.CreateNew();

        Assert.Equal("Untitled Playlist", playlist.Name);
        Assert.Empty(playlist.Items);
        Assert.False(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies save/load round-trip preserves name and items.
    /// </summary>
    [Fact]
    public async Task FileService_SaveAndLoad_RoundTrip()
    {
        var svc = new PlaylistFileService();
        var path = Path.Combine(_tempDir, "test.vidpl");
        var playlist = new Playlist("My Playlist",
        [
            new PlaylistItem(@"C:\Videos\one.mp4"),
            new PlaylistItem(@"C:\Videos\two.mkv"),
        ]);

        await svc.SaveAsync(playlist, path);

        Assert.Equal(path, playlist.FilePath);
        Assert.False(playlist.IsDirty);
        Assert.True(File.Exists(path));

        var loaded = await svc.LoadAsync(path);

        Assert.Equal("My Playlist", loaded.Name);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(@"C:\Videos\one.mp4", loaded.Items[0].FilePath);
        Assert.Equal(@"C:\Videos\two.mkv", loaded.Items[1].FilePath);
        Assert.Equal(path, loaded.FilePath);
        Assert.False(loaded.IsDirty);
    }

    /// <summary>
    /// Verifies SaveAsync throws on null playlist.
    /// </summary>
    [Fact]
    public async Task FileService_SaveAsync_NullPlaylist_Throws()
    {
        var svc = new PlaylistFileService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.SaveAsync(null!, "test.vidpl"));
    }

    /// <summary>
    /// Verifies SaveAsync throws on null/whitespace path.
    /// </summary>
    [Fact]
    public async Task FileService_SaveAsync_WhitespacePath_Throws()
    {
        var svc = new PlaylistFileService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            svc.SaveAsync(new Playlist("Test"), "  "));
    }

    /// <summary>
    /// Verifies LoadAsync throws on missing file.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_MissingFile_Throws()
    {
        var svc = new PlaylistFileService();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            svc.LoadAsync(Path.Combine(_tempDir, "nonexistent.vidpl")));
    }

    /// <summary>
    /// Verifies LoadAsync throws on invalid JSON.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_InvalidJson_Throws()
    {
        var svc = new PlaylistFileService();
        var path = Path.Combine(_tempDir, "bad.vidpl");
        await File.WriteAllTextAsync(path, "not valid json {{{}}}");

        await Assert.ThrowsAsync<InvalidDataException>(() => svc.LoadAsync(path));
    }

    /// <summary>
    /// Verifies LoadAsync throws on null JSON.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_NullJson_Throws()
    {
        var svc = new PlaylistFileService();
        var path = Path.Combine(_tempDir, "null.vidpl");
        await File.WriteAllTextAsync(path, "null");

        await Assert.ThrowsAsync<InvalidDataException>(() => svc.LoadAsync(path));
    }

    /// <summary>
    /// Verifies LoadAsync handles empty items list.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_EmptyItems()
    {
        var svc = new PlaylistFileService();
        var path = Path.Combine(_tempDir, "empty.vidpl");
        var playlist = new Playlist("Empty List");
        await svc.SaveAsync(playlist, path);

        var loaded = await svc.LoadAsync(path);

        Assert.Equal("Empty List", loaded.Name);
        Assert.Empty(loaded.Items);
    }

    /// <summary>
    /// Verifies LoadAsync throws on whitespace path.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_WhitespacePath_Throws()
    {
        var svc = new PlaylistFileService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => svc.LoadAsync("  "));
    }

    /// <summary>
    /// Verifies LoadAsync skips items with null/whitespace file paths in JSON.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_SkipsNullFilePaths()
    {
        var svc = new PlaylistFileService();
        var path = Path.Combine(_tempDir, "nullpaths.vidpl");
        var json = """
        {
          "name": "Test",
          "items": [
            { "filePath": "C:\\Videos\\a.mp4" },
            { "filePath": null },
            { "filePath": "" },
            { "filePath": "C:\\Videos\\b.mp4" }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, json);

        var loaded = await svc.LoadAsync(path);

        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(@"C:\Videos\a.mp4", loaded.Items[0].FilePath);
        Assert.Equal(@"C:\Videos\b.mp4", loaded.Items[1].FilePath);
    }

    /// <summary>
    /// Verifies LoadAsync uses default name when JSON name is null.
    /// </summary>
    [Fact]
    public async Task FileService_LoadAsync_NullName_DefaultsToUntitled()
    {
        var svc = new PlaylistFileService();
        var path = Path.Combine(_tempDir, "noname.vidpl");
        var json = """{"items": []}""";
        await File.WriteAllTextAsync(path, json);

        var loaded = await svc.LoadAsync(path);

        Assert.Equal("Untitled Playlist", loaded.Name);
    }

    /// <summary>
    /// Verifies SaveAsync creates directory if it doesn't exist.
    /// </summary>
    [Fact]
    public async Task FileService_SaveAsync_CreatesDirectory()
    {
        var svc = new PlaylistFileService();
        var nestedDir = Path.Combine(_tempDir, "sub", "dir");
        var path = Path.Combine(nestedDir, "test.vidpl");

        await svc.SaveAsync(new Playlist("Test"), path);

        Assert.True(File.Exists(path));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistProvider — Activation Tests                            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void Provider_IsActive_FalseByDefault()
    {
        Assert.False(_provider.IsActive);
    }

    [Fact]
    public void Provider_Activate_SetsIsActiveTrue()
    {
        _provider.Activate(_items, 0);

        Assert.True(_provider.IsActive);
    }

    [Fact]
    public void Provider_Deactivate_SetsIsActiveFalse()
    {
        _provider.Activate(_items, 0);
        _provider.Deactivate();

        Assert.False(_provider.IsActive);
    }

    [Fact]
    public void Provider_Activate_SetsCurrentIndex()
    {
        _provider.Activate(_items, 1);

        Assert.Equal(1, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_Deactivate_ResetsCurrentIndex()
    {
        _provider.Activate(_items, 2);
        _provider.Deactivate();

        Assert.Equal(-1, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_SetCurrentIndex_UpdatesIndex()
    {
        _provider.Activate(_items, 0);
        _provider.SetCurrentIndex(2);

        Assert.Equal(2, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_Activate_NullItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _provider.Activate(null!, 0));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistProvider — Sequential Navigation Tests                 ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void Provider_GetNextFile_ReturnsNextItem()
    {
        _provider.Activate(_items, 0);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\b.mp4", next);
        Assert.Equal(1, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_GetNextFile_WrapsAround()
    {
        _provider.Activate(_items, 2);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\a.mp4", next);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_GetPreviousFile_ReturnsPreviousItem()
    {
        _provider.Activate(_items, 2);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\b.mp4", prev);
        Assert.Equal(1, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_GetPreviousFile_WrapsAround()
    {
        _provider.Activate(_items, 0);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\c.mp4", prev);
        Assert.Equal(2, _provider.CurrentIndex);
    }

    [Fact]
    public void Provider_GetNextFile_NotActive_ReturnsNull()
    {
        var result = _provider.GetNextFile();

        Assert.Null(result);
    }

    [Fact]
    public void Provider_GetPreviousFile_NotActive_ReturnsNull()
    {
        var result = _provider.GetPreviousFile();

        Assert.Null(result);
    }

    [Fact]
    public void Provider_GetNextFile_EmptyList_ReturnsNull()
    {
        _provider.Activate(new ObservableCollection<PlaylistItem>(), 0);

        Assert.Null(_provider.GetNextFile());
    }

    /// <summary>
    /// Verifies that GetNextFile skips non-video files.
    /// </summary>
    [Fact]
    public void Provider_GetNextFile_SkipsNonVideoFiles()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Files\readme.txt"),
            new(@"C:\Files\script.funscript"),
            new(@"C:\Videos\b.mkv"),
        };
        _provider.Activate(items, 0);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\b.mkv", next);
        Assert.Equal(3, _provider.CurrentIndex);
    }

    /// <summary>
    /// Verifies GetNextFile returns null if all items are non-video.
    /// </summary>
    [Fact]
    public void Provider_GetNextFile_AllNonVideo_ReturnsNull()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Files\readme.txt"),
            new(@"C:\Files\data.json"),
        };
        _provider.Activate(items, 0);

        Assert.Null(_provider.GetNextFile());
    }

    /// <summary>
    /// Verifies GetPreviousFile skips non-video files.
    /// </summary>
    [Fact]
    public void Provider_GetPreviousFile_SkipsNonVideoFiles()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Files\readme.txt"),
            new(@"C:\Files\script.funscript"),
            new(@"C:\Videos\b.mkv"),
        };
        _provider.Activate(items, 3);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\a.mp4", prev);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistProvider — Shuffle Tests                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void Provider_IsShuffling_FalseByDefault()
    {
        Assert.False(_provider.IsShuffling);
    }

    [Fact]
    public void Provider_EnableShuffle_SetsIsShuffling()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();

        Assert.True(_provider.IsShuffling);
    }

    [Fact]
    public void Provider_DisableShuffle_ClearsIsShuffling()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();
        _provider.DisableShuffle();

        Assert.False(_provider.IsShuffling);
    }

    /// <summary>
    /// Verifies that enabling shuffle pins the current item at position 0.
    /// </summary>
    [Fact]
    public void Provider_EnableShuffle_PinsCurrentItemFirst()
    {
        _provider.Activate(_items, 1);
        _provider.EnableShuffle();

        Assert.NotNull(_provider.ShuffledIndices);
        Assert.Equal(1, _provider.ShuffledIndices![0]);
    }

    /// <summary>
    /// Verifies that shuffle covers all items.
    /// </summary>
    [Fact]
    public void Provider_EnableShuffle_ContainsAllIndices()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();

        Assert.NotNull(_provider.ShuffledIndices);
        Assert.Equal(_items.Count, _provider.ShuffledIndices!.Count);
        Assert.Equal(new[] { 0, 1, 2 }, _provider.ShuffledIndices!.OrderBy(x => x));
    }

    /// <summary>
    /// Verifies deterministic shuffle with seeded Random.
    /// </summary>
    [Fact]
    public void Provider_Shuffle_IsDeterministicWithSeed()
    {
        var p1 = new PlaylistProvider(new Random(42));
        var p2 = new PlaylistProvider(new Random(42));

        p1.Activate(_items, 0);
        p1.EnableShuffle();
        p2.Activate(_items, 0);
        p2.EnableShuffle();

        Assert.Equal(p1.ShuffledIndices, p2.ShuffledIndices);
    }

    /// <summary>
    /// Verifies GetNextFile in shuffle mode uses shuffled order.
    /// </summary>
    [Fact]
    public void Provider_GetNextFile_Shuffle_UsesShuffledOrder()
    {
        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(_items, 0);
        provider.EnableShuffle();

        var results = new List<string?>();
        for (int i = 0; i < _items.Count - 1; i++)
            results.Add(provider.GetNextFile());

        Assert.All(results, r => Assert.NotNull(r));
        // All results should be video files from our item list
        Assert.All(results, r => Assert.Contains(_items, item => item.FilePath == r));
    }

    /// <summary>
    /// Verifies that shuffle navigation skips non-video files.
    /// </summary>
    [Fact]
    public void Provider_Shuffle_SkipsNonVideoFiles()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Files\readme.txt"),
            new(@"C:\Videos\b.mp4"),
        };
        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        var results = new List<string?>();
        for (int i = 0; i < 2; i++)
        {
            var r = provider.GetNextFile();
            if (r != null) results.Add(r);
        }

        Assert.All(results, r => Assert.True(PlaylistProvider.IsVideoFile(r!)));
    }

    /// <summary>
    /// Verifies that Activate while shuffle is enabled builds shuffle order.
    /// </summary>
    [Fact]
    public void Provider_Activate_WithShuffle_BuildsShuffleOrder()
    {
        _provider.EnableShuffle();
        _provider.Activate(_items, 0);

        Assert.NotNull(_provider.ShuffledIndices);
        Assert.Equal(_items.Count, _provider.ShuffledIndices!.Count);
    }

    /// <summary>
    /// Verifies DisableShuffle restores the current index from shuffled position.
    /// </summary>
    [Fact]
    public void Provider_DisableShuffle_RestoresCurrentIndex()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();
        // Navigate in shuffle to get to a different original index
        _provider.GetNextFile();
        var indexBefore = _provider.CurrentIndex;

        _provider.DisableShuffle();

        Assert.Equal(indexBefore, _provider.CurrentIndex);
    }

    /// <summary>
    /// Verifies SetCurrentIndex updates shuffle position.
    /// </summary>
    [Fact]
    public void Provider_SetCurrentIndex_UpdatesShufflePosition()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();

        // Find an index that's in the shuffled list
        var targetOrigIdx = _provider.ShuffledIndices![1];
        _provider.SetCurrentIndex(targetOrigIdx);

        Assert.Equal(1, _provider.ShufflePosition);
    }

    /// <summary>
    /// Verifies RebuildShuffleOrder works after items change.
    /// </summary>
    [Fact]
    public void Provider_RebuildShuffleOrder_AdjustsAfterItemsChange()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();

        _items.Add(new PlaylistItem(@"C:\Videos\d.mp4"));
        _provider.RebuildShuffleOrder();

        Assert.NotNull(_provider.ShuffledIndices);
        Assert.Equal(4, _provider.ShuffledIndices!.Count);
    }

    /// <summary>
    /// Verifies RebuildShuffleOrder is no-op when not shuffling.
    /// </summary>
    [Fact]
    public void Provider_RebuildShuffleOrder_NotShuffling_NoOp()
    {
        _provider.Activate(_items, 0);
        _provider.RebuildShuffleOrder();

        Assert.Null(_provider.ShuffledIndices);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistProvider — IsVideoFile Tests                           ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Theory]
    [InlineData(@"C:\Videos\clip.mp4", true)]
    [InlineData(@"C:\Videos\clip.mkv", true)]
    [InlineData(@"C:\Videos\clip.avi", true)]
    [InlineData(@"C:\Videos\clip.mov", true)]
    [InlineData(@"C:\Videos\clip.wmv", true)]
    [InlineData(@"C:\Videos\clip.webm", true)]
    [InlineData(@"C:\Videos\clip.flv", true)]
    [InlineData(@"C:\Files\doc.txt", false)]
    [InlineData(@"C:\Files\pic.png", false)]
    [InlineData(@"C:\Files\script.funscript", false)]
    [InlineData(@"C:\Files\noext", false)]
    public void Provider_IsVideoFile_RecognizesExtensions(string path, bool expected)
    {
        Assert.Equal(expected, PlaylistProvider.IsVideoFile(path));
    }

    [Fact]
    public void Provider_IsVideoFile_IsCaseInsensitive()
    {
        Assert.True(PlaylistProvider.IsVideoFile(@"C:\Videos\clip.MP4"));
        Assert.True(PlaylistProvider.IsVideoFile(@"C:\Videos\clip.Mkv"));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistProvider — IPlaylistProvider Interface Tests            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies PlaylistProvider implements IPlaylistProvider.
    /// </summary>
    [Fact]
    public void Provider_ImplementsIPlaylistProvider()
    {
        IPlaylistProvider provider = _provider;

        Assert.NotNull(provider);
        Assert.False(provider.IsActive);
    }
}
