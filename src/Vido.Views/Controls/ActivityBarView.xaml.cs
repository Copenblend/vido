using System.Windows;
using System.Windows.Controls;
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
        SetButtonActive(ExtensionsButton, vm.IsPanelActive(SidebarPanelKind.Extensions) && vm.IsSidebarVisible);
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

    private void OnExtensionsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityBarViewModel vm)
        {
            vm.SelectPanelCommand.Execute(SidebarPanelKind.Extensions);
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

    // ── Plugin sidebar buttons ──

    /// <summary>
    /// Adds a plugin button to the activity bar, positioned between the built-in
    /// top buttons (Explorer, Extensions) and the bottom Settings button.
    /// Applies the local ActivityBarButtonStyle since it's not in the global resources.
    /// </summary>
    public void AddPluginButton(Button button)
    {
        // Apply the style from local resources
        if (TryFindResource("ActivityBarButtonStyle") is Style style)
            button.Style = style;

        // Wire hover events so the icon brightens on hover from the start
        button.MouseEnter += OnIconMouseEnter;
        button.MouseLeave += OnIconMouseLeave;

        // Find the top StackPanel (DockPanel.Dock="Top") and add the button there
        if (Content is DockPanel dock)
        {
            foreach (var child in dock.Children)
            {
                if (child is StackPanel panel && DockPanel.GetDock(panel) == Dock.Top)
                {
                    panel.Children.Add(button);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Removes a plugin button from the activity bar.
    /// </summary>
    public void RemovePluginButton(Button button)
    {
        if (Content is DockPanel dock)
        {
            foreach (var child in dock.Children)
            {
                if (child is StackPanel panel && DockPanel.GetDock(panel) == Dock.Top)
                {
                    panel.Children.Remove(button);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Sets the visual active/inactive state of a plugin sidebar button.
    /// Uses the same icon coloring approach as built-in buttons.
    /// </summary>
    public void SetPluginButtonActive(Button button, bool isActive)
    {
        SetButtonActive(button, isActive);
    }
}
