using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Vido.Core.Models.Playlists;

/// <summary>
/// Represents a playlist containing an ordered collection of <see cref="PlaylistItem"/> entries.
/// Tracks unsaved changes via <see cref="IsDirty"/> and notifies via <see cref="INotifyPropertyChanged"/>.
/// </summary>
public sealed class Playlist : INotifyPropertyChanged
{
    private string _name;
    private string? _filePath;
    private bool _isDirty;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// User-defined playlist name.
    /// Setting this property marks the playlist as dirty.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            IsDirty = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Ordered list of items in the playlist.
    /// Modifying this collection automatically marks the playlist as dirty.
    /// </summary>
    public RangeObservableCollection<PlaylistItem> Items { get; }

    /// <summary>
    /// Path where the playlist file is saved on disk.
    /// <c>null</c> if the playlist has never been saved.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the playlist has unsaved changes.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Initializes a new empty playlist with the specified name.
    /// </summary>
    /// <param name="name">The playlist name. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public Playlist(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        Items = [];
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    /// <summary>
    /// Initializes a new playlist with the specified name and items.
    /// </summary>
    /// <param name="name">The playlist name. Must not be null or whitespace.</param>
    /// <param name="items">The initial items for the playlist.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public Playlist(string name, IEnumerable<PlaylistItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        Items = new RangeObservableCollection<PlaylistItem>(items);
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsDirty = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
