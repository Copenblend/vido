using System.ComponentModel;

namespace Vido.Core.Layout;

/// <summary>
/// Represents a tab in the bottom panel (Output, Problems, Terminal, etc.).
/// Each tab has a unique ID, display title, closable flag, and active state.
/// </summary>
public sealed class BottomPanelTabItem : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IsActiveChangedArgs = new(nameof(IsActive));

    private bool _isActive;

    /// <summary>
    /// Unique identifier for this panel tab.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display title shown in the panel tab strip (uppercase).
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Whether the tab can be closed by the user.
    /// </summary>
    public bool IsClosable { get; set; } = true;

    /// <summary>
    /// Whether this tab is currently active (selected).
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, IsActiveChangedArgs);
            }
        }
    }

    /// <summary>
    /// Creates a bottom-panel tab with the given identifier and display title.
    /// </summary>
    /// <param name="id">Unique identifier for the tab (used for activation and lookup).</param>
    /// <param name="title">Display title shown in the panel tab strip.</param>
    public BottomPanelTabItem(string id, string title)
    {
        Id = id;
        Title = title;
    }
    
    /// <summary>
    /// Occurs when PropertyChanged is raised.
    /// </summary>

    public event PropertyChangedEventHandler? PropertyChanged;
}
