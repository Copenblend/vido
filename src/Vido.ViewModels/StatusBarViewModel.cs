using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vido.Core.Formatting;
using Vido.Core.Layout;
using Vido.Core.Playback;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the status bar. Manages a registry of status bar items
/// and populates built-in items from the currently loaded video's metadata.
/// </summary>
public partial class StatusBarViewModel : ObservableObject, IDisposable
{
    // ── Well-known item IDs ──
    internal const string FileNameItemId = "vido.fileName";
    internal const string ResolutionItemId = "vido.resolution";
    internal const string DurationItemId = "vido.duration";
    internal const string CodecItemId = "vido.codec";

    private readonly VideoPlayerViewModel _playerViewModel;
    private bool _disposed;

    /// <summary>Items aligned to the left side of the status bar.</summary>
    public ObservableCollection<StatusBarItem> LeftItems { get; } = [];

    /// <summary>Items aligned to the right side of the status bar.</summary>
    public ObservableCollection<StatusBarItem> RightItems { get; } = [];

    // ── Built-in items ──

    private readonly StatusBarItem _fileNameItem;
    private readonly StatusBarItem _resolutionItem;
    private readonly StatusBarItem _durationItem;
    private readonly StatusBarItem _codecItem;

    public StatusBarViewModel(VideoPlayerViewModel playerViewModel)
    {
        _playerViewModel = playerViewModel;

        // Create built-in items
        _fileNameItem = new StatusBarItem(FileNameItemId, StatusBarAlignment.Left, 0)
        {
            Text = "No file",
            Tooltip = "Current video file",
            IsVisible = true
        };

        _resolutionItem = new StatusBarItem(ResolutionItemId, StatusBarAlignment.Right, 10200)
        {
            Text = string.Empty,
            Tooltip = "Video resolution",
            IsVisible = false
        };

        _durationItem = new StatusBarItem(DurationItemId, StatusBarAlignment.Right, 10100)
        {
            Text = string.Empty,
            Tooltip = "Video duration",
            IsVisible = false
        };

        _codecItem = new StatusBarItem(CodecItemId, StatusBarAlignment.Right, 10300)
        {
            Text = string.Empty,
            Tooltip = "Video codec",
            IsVisible = false
        };

        // Register built-in items
        LeftItems.Add(_fileNameItem);
        RightItems.Add(_durationItem);
        RightItems.Add(_resolutionItem);
        RightItems.Add(_codecItem);

        // Subscribe to metadata changes
        _playerViewModel.PropertyChanged += OnPlayerPropertyChanged;

        // Initialize from current state
        UpdateFromMetadata(_playerViewModel.CurrentMetadata);
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPlayerViewModel.CurrentMetadata))
        {
            UpdateFromMetadata(_playerViewModel.CurrentMetadata);
        }
    }

    /// <summary>
    /// Updates all built-in status bar items from the current video metadata.
    /// </summary>
    internal void UpdateFromMetadata(VideoMetadata? metadata)
    {
        if (metadata is null)
        {
            _fileNameItem.Text = "No file";
            _fileNameItem.Tooltip = "No video loaded";

            _resolutionItem.IsVisible = false;
            _durationItem.IsVisible = false;
            _codecItem.IsVisible = false;
            return;
        }

        _fileNameItem.Text = metadata.FileName;
        _fileNameItem.Tooltip = metadata.FilePath;

        _resolutionItem.Text = metadata.Resolution;
        _resolutionItem.IsVisible = true;

        _durationItem.Text = TimeFormatter.FormatPadded(metadata.Duration);
        _durationItem.IsVisible = true;

        _codecItem.Text = (metadata.VideoCodec ?? "Unknown").ToUpperInvariant();
        _codecItem.IsVisible = true;
    }

    // ── Item Registry ──

    /// <summary>
    /// Registers a new status bar item. Inserts it in priority order
    /// within its alignment group. Returns the item for further updates.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if an item with the same ID already exists.</exception>
    public StatusBarItem RegisterItem(string id, StatusBarAlignment alignment, int priority)
    {
        if (FindItem(id) is not null)
            throw new ArgumentException($"A status bar item with ID '{id}' is already registered.", nameof(id));

        var item = new StatusBarItem(id, alignment, priority);
        InsertByPriority(item);
        return item;
    }

    /// <summary>
    /// Unregisters a status bar item by ID. No-op if the item doesn't exist.
    /// </summary>
    public void UnregisterItem(string id)
    {
        var item = FindItem(id);
        if (item is null) return;

        if (item.Alignment == StatusBarAlignment.Left)
        {
            LeftItems.Remove(item);
            UpdateSeparatorFlags(LeftItems);
        }
        else
        {
            RightItems.Remove(item);
            UpdateSeparatorFlags(RightItems);
        }
    }

    /// <summary>Finds a registered status bar item by ID, or null if not found.</summary>
    public StatusBarItem? FindItem(string id)
    {
        for (int i = 0; i < LeftItems.Count; i++)
            if (LeftItems[i].Id == id) return LeftItems[i];

        for (int i = 0; i < RightItems.Count; i++)
            if (RightItems[i].Id == id) return RightItems[i];

        return null;
    }

    /// <summary>
    /// Inserts an item into the correct collection, sorted by priority (ascending).
    /// When priorities are equal, items are sorted alphabetically by ID for
    /// deterministic ordering.
    /// </summary>
    private void InsertByPriority(StatusBarItem item)
    {
        var collection = item.Alignment == StatusBarAlignment.Left ? LeftItems : RightItems;

        for (int i = 0; i < collection.Count; i++)
        {
            if (collection[i].Priority > item.Priority)
            {
                collection.Insert(i, item);
                UpdateSeparatorFlags(collection);
                return;
            }

            // Same priority — sort by ID alphabetically for deterministic ordering
            if (collection[i].Priority == item.Priority &&
                string.Compare(collection[i].Id, item.Id, StringComparison.OrdinalIgnoreCase) > 0)
            {
                collection.Insert(i, item);
                UpdateSeparatorFlags(collection);
                return;
            }
        }

        collection.Add(item);
        UpdateSeparatorFlags(collection);
    }

    /// <summary>
    /// Sets <see cref="StatusBarItem.ShowSeparator"/> on every item in the
    /// collection: false for the first item, true for all subsequent items.
    /// Called after every insert or remove so the UI always has correct state.
    /// </summary>
    private static void UpdateSeparatorFlags(ObservableCollection<StatusBarItem> collection)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            collection[i].ShowSeparator = i > 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playerViewModel.PropertyChanged -= OnPlayerPropertyChanged;
    }
}
