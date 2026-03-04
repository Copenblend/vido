using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Vido.Core.Models.Playlists;

namespace Vido.ViewModels.Playlists;

/// <summary>
/// ViewModel wrapping a <see cref="PlaylistItem"/> for display in the playlist sidebar.
/// Provides file-exists checking, playing state, and tooltip text.
/// </summary>
public sealed class PlaylistItemViewModel : INotifyPropertyChanged
{
    private bool _isPlaying;
    private bool _fileExists;
    private string? _toolTipText;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Display name derived from the file path (filename without directory).
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Absolute path to the file on disk.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Whether this item is currently being played.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the referenced file exists on disk.
    /// Used to gray-out missing files in the UI.
    /// </summary>
    public bool FileExists => _fileExists;

    /// <summary>
    /// Tooltip text showing the full path and existence status.
    /// </summary>
    public string ToolTipText => _toolTipText ??= FileExists
        ? FilePath
        : $"{FilePath} (file not found)";

    /// <summary>
    /// The underlying model item.
    /// </summary>
    internal PlaylistItem Model { get; }

    /// <summary>
    /// Initializes a new <see cref="PlaylistItemViewModel"/> wrapping the given model item.
    /// </summary>
    /// <param name="item">The playlist item model. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    public PlaylistItemViewModel(PlaylistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Model = item;
        FileName = item.FileName;
        FilePath = item.FilePath;
        _fileExists = File.Exists(FilePath);
    }

    /// <summary>
    /// Re-checks whether the file exists on disk and updates bound properties when state changes.
    /// </summary>
    public void RefreshFileExists()
    {
        var exists = File.Exists(FilePath);
        if (exists == _fileExists) return;

        _fileExists = exists;
        _toolTipText = null;

        OnPropertyChanged(nameof(FileExists));
        OnPropertyChanged(nameof(ToolTipText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
