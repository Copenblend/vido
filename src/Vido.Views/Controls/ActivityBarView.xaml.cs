using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Vido.Core.Layout;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Activity bar — vertical icon strip on the far left.
/// Manages active/inactive icon states and hover brightening behavior.
/// </summary>
public partial class ActivityBarView : UserControl
{
    /// <summary>
    /// Sets up the activity bar UI, including icon buttons and panel selection handlers.
    /// </summary>
    public ActivityBarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates all icon visual states to reflect which panel is active.
    /// Called after the ViewModel processes a panel selection.
    /// </summary>
    public void UpdateActiveStates()
    {
        if (DataContext is not ActivityBarViewModel vm)
            return;

        SetButtonActive(ExplorerButton, vm.IsPanelActive(SidebarPanelKind.Explorer) && vm.IsSidebarVisible);
        SetButtonActive(Osr2PlusButton, vm.IsPanelActive(SidebarPanelKind.Osr2Plus) && vm.IsSidebarVisible);
        SetButtonActive(PlaylistsButton, vm.IsPanelActive(SidebarPanelKind.Playlists) && vm.IsSidebarVisible);
        SetButtonActive(SettingsButton, vm.IsPanelActive(SidebarPanelKind.Settings) && vm.IsSidebarVisible);
    }

    private void OnExplorerClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityBarViewModel vm)
        {
            vm.SelectPanelCommand.Execute(SidebarPanelKind.Explorer);
            UpdateActiveStates();
            RaisePanelChanged();
        }
    }

    private void OnOsr2PlusClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityBarViewModel vm)
        {
            vm.SelectPanelCommand.Execute(SidebarPanelKind.Osr2Plus);
            UpdateActiveStates();
            RaisePanelChanged();
        }
    }

    private void OnPlaylistsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityBarViewModel vm)
        {
            vm.SelectPanelCommand.Execute(SidebarPanelKind.Playlists);
            UpdateActiveStates();
            RaisePanelChanged();
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        // Settings opens as a tab (like VS Code), not in the sidebar.
        // The MainWindow handles this via the SettingsRequested event.
        SettingsRequested?.Invoke(this, e);
    }

    /// <summary>
    /// Raised when the Settings gear icon is clicked.
    /// The MainWindow opens Settings as a tab instead of a sidebar panel.
    /// </summary>
    public event RoutedEventHandler? SettingsRequested;

    /// <summary>
    /// Routed event raised when the active panel or sidebar visibility changes.
    /// The parent <see cref="MainWindow"/> listens to this to update layout.
    /// </summary>
    public static readonly RoutedEvent PanelChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PanelChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ActivityBarView));

    /// <summary>
    /// Occurs when PanelChanged is raised.
    /// </summary>
    public event RoutedEventHandler PanelChanged
    {
        add => AddHandler(PanelChangedEvent, value);
        remove => RemoveHandler(PanelChangedEvent, value);
    }

    private void RaisePanelChanged()
    {
        RaiseEvent(new RoutedEventArgs(PanelChangedEvent));
    }

    private void SetButtonActive(Button button, bool isActive)
    {
        button.Tag = isActive ? "Active" : null;
        SetIconColor(button, isActive);

        // Wire hover events for non-active icons to "light up" on hover
        button.MouseEnter -= OnIconMouseEnter;
        button.MouseLeave -= OnIconMouseLeave;
        if (!isActive)
        {
            button.MouseEnter += OnIconMouseEnter;
            button.MouseLeave += OnIconMouseLeave;
        }
    }

    private void OnIconMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Button button)
            SetIconColor(button, bright: true);
    }

    private void OnIconMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Button button)
            SetIconColor(button, bright: false);
    }

    /// <summary>
    /// Sets all stroke elements inside a button's Canvas to the
    /// active (white) or inactive (grey) icon brush.
    /// For plugin bitmap icons (Image elements), adjusts opacity
    /// to simulate the same active/inactive visual states.
    /// Only updates Stroke — Fill is left unchanged to preserve
    /// any background-colored occlusion fills.
    /// </summary>
    private static void SetIconColor(Button button, bool bright)
    {
        var brushKey = bright ? "ActiveIconBrush" : "InactiveIconBrush";
        var brush = (Brush)button.FindResource(brushKey);

        if (button.Content is Canvas canvas)
        {
            foreach (var child in canvas.Children)
            {
                if (child is Shape shape && shape.Stroke != null)
                {
                    shape.Stroke = brush;
                }
            }
        }
        else if (button.Content is Image image)
        {
            image.Opacity = bright ? 1.0 : 0.6;
        }
    }

}
