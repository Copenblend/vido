using System.ComponentModel;

namespace Vido.Core.Layout;

/// <summary>
/// Represents a single item displayed in the status bar.
/// Items have an alignment (left or right), a priority for ordering,
/// and observable text/tooltip/visibility properties.
/// </summary>
public class StatusBarItem : INotifyPropertyChanged
{
    /// <summary>Unique identifier for this status bar item.</summary>
    public string Id { get; }

    /// <summary>Which side of the status bar this item is placed on.</summary>
    public StatusBarAlignment Alignment { get; }

    /// <summary>
    /// Sort priority within its alignment group. Lower values appear first
    /// (leftmost on left side, rightmost on right side is highest priority).
    /// </summary>
    public int Priority { get; }

    private string _text = string.Empty;

    /// <summary>The text displayed in the status bar for this item.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    private string? _tooltip;

    /// <summary>Optional tooltip shown on hover.</summary>
    public string? Tooltip
    {
        get => _tooltip;
        set
        {
            if (_tooltip == value) return;
            _tooltip = value;
            OnPropertyChanged(nameof(Tooltip));
        }
    }

    private bool _isVisible = true;

    /// <summary>Whether this item is currently visible.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    private object? _contentView;

    /// <summary>
    /// Optional custom WPF content to display instead of <see cref="Text"/>.
    /// When set, the status bar renders this element directly rather than a TextBlock.
    /// Used by plugins that provide their own <c>FrameworkElement</c> via the view factory.
    /// </summary>
    public object? ContentView
    {
        get => _contentView;
        set
        {
            if (_contentView == value) return;
            _contentView = value;
            OnPropertyChanged(nameof(ContentView));
            OnPropertyChanged(nameof(HasContentView));
        }
    }

    /// <summary>Whether <see cref="ContentView"/> is set (non-null).</summary>
    public bool HasContentView => _contentView is not null;

    private bool _showSeparator;

    /// <summary>
    /// Whether a separator dot should be shown before this item.
    /// Managed by <c>StatusBarViewModel</c> whenever the collection changes.
    /// </summary>
    public bool ShowSeparator
    {
        get => _showSeparator;
        set
        {
            if (_showSeparator == value) return;
            _showSeparator = value;
            OnPropertyChanged(nameof(ShowSeparator));
        }
    }

    public StatusBarItem(string id, StatusBarAlignment alignment, int priority)
    {
        Id = id;
        Alignment = alignment;
        Priority = priority;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
