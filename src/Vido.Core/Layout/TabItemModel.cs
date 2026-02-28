using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Vido.Core.Layout;

/// <summary>
/// Represents a single tab in the tab well. Tabs have a unique ID,
/// display title, optional icon geometry, and a flag indicating
/// whether they can be closed by the user.
/// </summary>
public sealed class TabItemModel : INotifyPropertyChanged
{
    private bool _isActive;

    /// <summary>
    /// Unique identifier for this tab.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display title shown in the tab strip.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Optional StreamGeometry string for the tab icon.
    /// Null means no icon is shown.
    /// </summary>
    public string? IconGeometry { get; set; }

    /// <summary>
    /// Whether the tab can be closed by the user.
    /// The "Player" tab is not closable.
    /// </summary>
    public bool IsClosable { get; set; } = true;

    /// <summary>
    /// Whether this tab is pinned to the leftmost position
    /// and cannot be reordered.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Whether this tab is currently active (selected).
    /// Used by the UI to show close button and active styling.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// Occurs when PropertyChanged is raised.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Creates a tab model with the given identifier and display title.
    /// </summary>
    /// <param name="id">Unique identifier for the tab (used for activation and lookup).</param>
    /// <param name="title">Display title shown in the tab strip header.</param>
    public TabItemModel(string id, string title)
    {
        Id = id;
        Title = title;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
