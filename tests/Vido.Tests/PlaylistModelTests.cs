using System.Collections.Specialized;
using System.ComponentModel;
using Vido.Core.Models.Playlists;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-016: Playlist model types integrated into Vido.Core.
/// Covers <see cref="Playlist"/>, <see cref="PlaylistItem"/>, and
/// <see cref="RangeObservableCollection{T}"/>.
/// </summary>
public sealed class PlaylistModelTests
{
    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PlaylistItem Tests                                             ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that the constructor sets FilePath and FileName.
    /// </summary>
    [Fact]
    public void PlaylistItem_Constructor_SetsProperties()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");

        Assert.Equal(@"C:\Videos\test.mp4", item.FilePath);
        Assert.Equal("test.mp4", item.FileName);
    }

    /// <summary>
    /// Verifies that the constructor throws on null.
    /// </summary>
    [Fact]
    public void PlaylistItem_Constructor_NullPath_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PlaylistItem(null!));
    }

    /// <summary>
    /// Verifies that the constructor throws on whitespace.
    /// </summary>
    [Fact]
    public void PlaylistItem_Constructor_WhitespacePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlaylistItem("   "));
    }

    /// <summary>
    /// Verifies that the constructor throws on empty string.
    /// </summary>
    [Fact]
    public void PlaylistItem_Constructor_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlaylistItem(""));
    }

    /// <summary>
    /// Verifies case-insensitive equality.
    /// </summary>
    [Fact]
    public void PlaylistItem_Equals_CaseInsensitive()
    {
        var a = new PlaylistItem(@"C:\Videos\Test.mp4");
        var b = new PlaylistItem(@"c:\videos\test.mp4");

        Assert.True(a.Equals(b));
        Assert.True(b.Equals(a));
    }

    /// <summary>
    /// Verifies that different paths are not equal.
    /// </summary>
    [Fact]
    public void PlaylistItem_Equals_DifferentPaths_NotEqual()
    {
        var a = new PlaylistItem(@"C:\Videos\a.mp4");
        var b = new PlaylistItem(@"C:\Videos\b.mp4");

        Assert.False(a.Equals(b));
    }

    /// <summary>
    /// Verifies that Equals returns false for null.
    /// </summary>
    [Fact]
    public void PlaylistItem_Equals_Null_ReturnsFalse()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");

        Assert.False(item.Equals((PlaylistItem?)null));
    }

    /// <summary>
    /// Verifies that Equals(object) works for PlaylistItem.
    /// </summary>
    [Fact]
    public void PlaylistItem_EqualsObject_SamePath_ReturnsTrue()
    {
        var a = new PlaylistItem(@"C:\Videos\test.mp4");
        object b = new PlaylistItem(@"C:\Videos\TEST.MP4");

        Assert.True(a.Equals(b));
    }

    /// <summary>
    /// Verifies that Equals(object) returns false for non-PlaylistItem.
    /// </summary>
    [Fact]
    public void PlaylistItem_EqualsObject_DifferentType_ReturnsFalse()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");

        Assert.False(item.Equals("not a playlist item"));
    }

    /// <summary>
    /// Verifies that ReferenceEquals is handled.
    /// </summary>
    [Fact]
    public void PlaylistItem_Equals_SameReference_ReturnsTrue()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");

        Assert.True(item.Equals(item));
    }

    /// <summary>
    /// Verifies that GetHashCode is consistent with Equals (case-insensitive).
    /// </summary>
    [Fact]
    public void PlaylistItem_GetHashCode_CaseInsensitive()
    {
        var a = new PlaylistItem(@"C:\Videos\Test.mp4");
        var b = new PlaylistItem(@"c:\videos\test.mp4");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// Verifies that ToString returns the file name.
    /// </summary>
    [Fact]
    public void PlaylistItem_ToString_ReturnsFileName()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");

        Assert.Equal("test.mp4", item.ToString());
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ RangeObservableCollection Tests                                ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that the default constructor creates an empty collection.
    /// </summary>
    [Fact]
    public void RangeCollection_DefaultConstructor_IsEmpty()
    {
        var col = new RangeObservableCollection<int>();

        Assert.Empty(col);
    }

    /// <summary>
    /// Verifies that the items constructor populates the collection.
    /// </summary>
    [Fact]
    public void RangeCollection_ItemsConstructor_PopulatesCollection()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3]);

        Assert.Equal(3, col.Count);
        Assert.Equal(1, col[0]);
        Assert.Equal(2, col[1]);
        Assert.Equal(3, col[2]);
    }

    /// <summary>
    /// Verifies that AddRange adds all items.
    /// </summary>
    [Fact]
    public void RangeCollection_AddRange_AddsAllItems()
    {
        var col = new RangeObservableCollection<int>();

        col.AddRange([10, 20, 30]);

        Assert.Equal(3, col.Count);
        Assert.Equal(10, col[0]);
        Assert.Equal(20, col[1]);
        Assert.Equal(30, col[2]);
    }

    /// <summary>
    /// Verifies that AddRange fires a single Reset notification.
    /// </summary>
    [Fact]
    public void RangeCollection_AddRange_FiresSingleResetNotification()
    {
        var col = new RangeObservableCollection<int>();
        var notifications = new List<NotifyCollectionChangedAction>();
        col.CollectionChanged += (_, e) => notifications.Add(e.Action);

        col.AddRange([1, 2, 3]);

        Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, notifications[0]);
    }

    /// <summary>
    /// Verifies that AddRange with empty list does nothing.
    /// </summary>
    [Fact]
    public void RangeCollection_AddRange_Empty_NoNotification()
    {
        var col = new RangeObservableCollection<int>();
        var notifications = new List<NotifyCollectionChangedAction>();
        col.CollectionChanged += (_, e) => notifications.Add(e.Action);

        col.AddRange([]);

        Assert.Empty(notifications);
    }

    /// <summary>
    /// Verifies that AddRange throws on null.
    /// </summary>
    [Fact]
    public void RangeCollection_AddRange_Null_Throws()
    {
        var col = new RangeObservableCollection<int>();

        Assert.Throws<ArgumentNullException>(() => col.AddRange(null!));
    }

    /// <summary>
    /// Verifies that RemoveRange removes matching items.
    /// </summary>
    [Fact]
    public void RangeCollection_RemoveRange_RemovesMatchingItems()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3, 4, 5]);

        int removed = col.RemoveRange(x => x > 3);

        Assert.Equal(2, removed);
        Assert.Equal(3, col.Count);
        Assert.Equal([1, 2, 3], col);
    }

    /// <summary>
    /// Verifies that RemoveRange fires a single Reset notification.
    /// </summary>
    [Fact]
    public void RangeCollection_RemoveRange_FiresSingleResetNotification()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3]);
        var notifications = new List<NotifyCollectionChangedAction>();
        col.CollectionChanged += (_, e) => notifications.Add(e.Action);

        col.RemoveRange(x => x == 2);

        Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, notifications[0]);
    }

    /// <summary>
    /// Verifies that RemoveRange with no matches returns 0 and fires no notification.
    /// </summary>
    [Fact]
    public void RangeCollection_RemoveRange_NoMatch_NoNotification()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3]);
        var notifications = new List<NotifyCollectionChangedAction>();
        col.CollectionChanged += (_, e) => notifications.Add(e.Action);

        int removed = col.RemoveRange(x => x > 100);

        Assert.Equal(0, removed);
        Assert.Empty(notifications);
    }

    /// <summary>
    /// Verifies that RemoveRange throws on null predicate.
    /// </summary>
    [Fact]
    public void RangeCollection_RemoveRange_NullPredicate_Throws()
    {
        var col = new RangeObservableCollection<int>();

        Assert.Throws<ArgumentNullException>(() => col.RemoveRange(null!));
    }

    /// <summary>
    /// Verifies that ReplaceAll replaces all items.
    /// </summary>
    [Fact]
    public void RangeCollection_ReplaceAll_ReplacesAllItems()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3]);

        col.ReplaceAll([10, 20]);

        Assert.Equal(2, col.Count);
        Assert.Equal(10, col[0]);
        Assert.Equal(20, col[1]);
    }

    /// <summary>
    /// Verifies that ReplaceAll fires a single Reset notification.
    /// </summary>
    [Fact]
    public void RangeCollection_ReplaceAll_FiresSingleResetNotification()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3]);
        var notifications = new List<NotifyCollectionChangedAction>();
        col.CollectionChanged += (_, e) => notifications.Add(e.Action);

        col.ReplaceAll([10, 20]);

        Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, notifications[0]);
    }

    /// <summary>
    /// Verifies that ReplaceAll with empty list clears the collection.
    /// </summary>
    [Fact]
    public void RangeCollection_ReplaceAll_EmptyList_ClearsCollection()
    {
        var col = new RangeObservableCollection<int>([1, 2, 3]);

        col.ReplaceAll([]);

        Assert.Empty(col);
    }

    /// <summary>
    /// Verifies that ReplaceAll throws on null.
    /// </summary>
    [Fact]
    public void RangeCollection_ReplaceAll_Null_Throws()
    {
        var col = new RangeObservableCollection<int>();

        Assert.Throws<ArgumentNullException>(() => col.ReplaceAll(null!));
    }

    /// <summary>
    /// Verifies that AddRange raises Count and Item[] property changed notifications.
    /// </summary>
    [Fact]
    public void RangeCollection_AddRange_RaisesPropertyChanged()
    {
        var col = new RangeObservableCollection<int>();
        var props = new List<string?>();
        ((INotifyPropertyChanged)col).PropertyChanged += (_, e) => props.Add(e.PropertyName);

        col.AddRange([1, 2]);

        Assert.Contains("Count", props);
        Assert.Contains("Item[]", props);
    }

    /// <summary>
    /// Verifies that single Add still fires per-item notification (not suppressed).
    /// </summary>
    [Fact]
    public void RangeCollection_SingleAdd_FiresPerItemNotification()
    {
        var col = new RangeObservableCollection<int>();
        var notifications = new List<NotifyCollectionChangedAction>();
        col.CollectionChanged += (_, e) => notifications.Add(e.Action);

        col.Add(42);

        Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Add, notifications[0]);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Playlist Tests                                                 ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that the constructor sets Name and creates empty Items.
    /// </summary>
    [Fact]
    public void Playlist_Constructor_SetsNameAndEmptyItems()
    {
        var playlist = new Playlist("My Playlist");

        Assert.Equal("My Playlist", playlist.Name);
        Assert.Empty(playlist.Items);
        Assert.Null(playlist.FilePath);
        Assert.False(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies that the constructor throws on null name.
    /// </summary>
    [Fact]
    public void Playlist_Constructor_NullName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Playlist(null!));
    }

    /// <summary>
    /// Verifies that the constructor throws on whitespace name.
    /// </summary>
    [Fact]
    public void Playlist_Constructor_WhitespaceName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Playlist("   "));
    }

    /// <summary>
    /// Verifies that the items constructor populates the collection.
    /// </summary>
    [Fact]
    public void Playlist_ItemsConstructor_PopulatesItems()
    {
        var items = new[]
        {
            new PlaylistItem(@"C:\a.mp4"),
            new PlaylistItem(@"C:\b.mp4"),
        };

        var playlist = new Playlist("Test", items);

        Assert.Equal(2, playlist.Items.Count);
        Assert.Equal(@"C:\a.mp4", playlist.Items[0].FilePath);
        Assert.Equal(@"C:\b.mp4", playlist.Items[1].FilePath);
    }

    /// <summary>
    /// Verifies that the items constructor throws on null name.
    /// </summary>
    [Fact]
    public void Playlist_ItemsConstructor_NullName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new Playlist(null!, [new PlaylistItem(@"C:\a.mp4")]));
    }

    /// <summary>
    /// Verifies that setting Name marks the playlist as dirty.
    /// </summary>
    [Fact]
    public void Playlist_SetName_MarksDirty()
    {
        var playlist = new Playlist("Original");

        playlist.Name = "Renamed";

        Assert.True(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies that setting Name to the same value does not change IsDirty.
    /// </summary>
    [Fact]
    public void Playlist_SetName_SameValue_DoesNotMarkDirty()
    {
        var playlist = new Playlist("Original");

        playlist.Name = "Original";

        Assert.False(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies that adding an item marks the playlist as dirty.
    /// </summary>
    [Fact]
    public void Playlist_AddItem_MarksDirty()
    {
        var playlist = new Playlist("Test");

        playlist.Items.Add(new PlaylistItem(@"C:\video.mp4"));

        Assert.True(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies that clearing items marks the playlist as dirty.
    /// </summary>
    [Fact]
    public void Playlist_ClearItems_MarksDirty()
    {
        var playlist = new Playlist("Test", [new PlaylistItem(@"C:\a.mp4")]);
        playlist.IsDirty = false;

        playlist.Items.Clear();

        Assert.True(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies that setting IsDirty to false clears the flag.
    /// </summary>
    [Fact]
    public void Playlist_IsDirty_CanBeCleared()
    {
        var playlist = new Playlist("Test");
        playlist.Items.Add(new PlaylistItem(@"C:\video.mp4"));
        Assert.True(playlist.IsDirty);

        playlist.IsDirty = false;

        Assert.False(playlist.IsDirty);
    }

    /// <summary>
    /// Verifies that setting FilePath raises PropertyChanged.
    /// </summary>
    [Fact]
    public void Playlist_SetFilePath_RaisesPropertyChanged()
    {
        var playlist = new Playlist("Test");
        var changed = new List<string?>();
        playlist.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        playlist.FilePath = @"C:\playlists\test.vidpl";

        Assert.Contains(nameof(Playlist.FilePath), changed);
    }

    /// <summary>
    /// Verifies that setting Name raises PropertyChanged for Name and IsDirty.
    /// </summary>
    [Fact]
    public void Playlist_SetName_RaisesPropertyChanged()
    {
        var playlist = new Playlist("Original");
        var changed = new List<string?>();
        playlist.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        playlist.Name = "New Name";

        Assert.Contains(nameof(Playlist.Name), changed);
        Assert.Contains(nameof(Playlist.IsDirty), changed);
    }

    /// <summary>
    /// Verifies that setting IsDirty raises PropertyChanged.
    /// </summary>
    [Fact]
    public void Playlist_SetIsDirty_RaisesPropertyChanged()
    {
        var playlist = new Playlist("Test");
        var changed = new List<string?>();
        playlist.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        playlist.IsDirty = true;

        Assert.Contains(nameof(Playlist.IsDirty), changed);
    }

    /// <summary>
    /// Verifies that setting IsDirty to the same value does not raise PropertyChanged.
    /// </summary>
    [Fact]
    public void Playlist_SetIsDirty_SameValue_NoPropertyChanged()
    {
        var playlist = new Playlist("Test");
        var changed = new List<string?>();
        playlist.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        playlist.IsDirty = false; // same as default

        Assert.DoesNotContain(nameof(Playlist.IsDirty), changed);
    }

    /// <summary>
    /// Verifies that setting FilePath to the same value does not raise PropertyChanged.
    /// </summary>
    [Fact]
    public void Playlist_SetFilePath_SameValue_NoPropertyChanged()
    {
        var playlist = new Playlist("Test");
        playlist.FilePath = @"C:\test.vidpl";
        var changed = new List<string?>();
        playlist.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        playlist.FilePath = @"C:\test.vidpl";

        Assert.DoesNotContain(nameof(Playlist.FilePath), changed);
    }

    /// <summary>
    /// Verifies that AddRange on Items marks the playlist as dirty.
    /// </summary>
    [Fact]
    public void Playlist_AddRangeItems_MarksDirty()
    {
        var playlist = new Playlist("Test");

        playlist.Items.AddRange([
            new PlaylistItem(@"C:\a.mp4"),
            new PlaylistItem(@"C:\b.mp4"),
        ]);

        Assert.True(playlist.IsDirty);
    }
}
