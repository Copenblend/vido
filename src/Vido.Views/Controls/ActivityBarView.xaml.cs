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
        // TODO PI-024: Replace ExtensionsButton with feature panel buttons
        SetButtonActive(ExtensionsButton, false);
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
        // TODO PI-024: This button will be replaced with feature panel buttons
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

    // ── Plugin sidebar buttons (vb-007: drag-drop reordering) ──

    private Point _dragStartPoint;
    private Button? _dragSourceButton;

    /// <summary>
    /// Adds a plugin button to the dedicated plugin buttons panel.
    /// The button's Uid property is used as the panel ID
    /// for drag-drop identification.
    /// </summary>
    /// <param name="button">The plugin button to add to the panel.</param>
    public void AddPluginButton(Button button)
    {
        // Apply the style from local resources
        if (TryFindResource("ActivityBarButtonStyle") is Style style)
            button.Style = style;

        // Wire hover events so the icon brightens on hover from the start
        button.MouseEnter += OnIconMouseEnter;
        button.MouseLeave += OnIconMouseLeave;

        // Wire drag-drop initiation
        button.PreviewMouseLeftButtonDown += OnPluginButtonMouseDown;
        button.PreviewMouseMove += OnPluginButtonMouseMove;

        PluginButtonsPanel.Children.Add(button);
    }

    /// <summary>
    /// Inserts a plugin button at a specific index within the plugin buttons panel.
    /// Used when adding buttons in a persisted order.
    /// </summary>
    /// <param name="button">The plugin button to insert.</param>
    /// <param name="index">The zero-based position at which to insert the button (clamped to valid range).</param>
    public void InsertPluginButton(Button button, int index)
    {
        if (TryFindResource("ActivityBarButtonStyle") is Style style)
            button.Style = style;

        button.MouseEnter += OnIconMouseEnter;
        button.MouseLeave += OnIconMouseLeave;
        button.PreviewMouseLeftButtonDown += OnPluginButtonMouseDown;
        button.PreviewMouseMove += OnPluginButtonMouseMove;

        var clampedIndex = Math.Clamp(index, 0, PluginButtonsPanel.Children.Count);
        PluginButtonsPanel.Children.Insert(clampedIndex, button);
    }

    /// <summary>
    /// Removes a plugin button from the plugin buttons panel.
    /// </summary>
    /// <param name="button">The plugin button to remove.</param>
    public void RemovePluginButton(Button button)
    {
        button.PreviewMouseLeftButtonDown -= OnPluginButtonMouseDown;
        button.PreviewMouseMove -= OnPluginButtonMouseMove;
        PluginButtonsPanel.Children.Remove(button);
    }

    /// <summary>
    /// Sets the visual active/inactive state of a plugin sidebar button.
    /// Uses the same icon coloring approach as built-in buttons.
    /// </summary>
    /// <param name="button">The plugin button whose visual state to update.</param>
    /// <param name="isActive">Whether the button should appear active (bright) or inactive (dimmed).</param>
    public void SetPluginButtonActive(Button button, bool isActive)
    {
        SetButtonActive(button, isActive);
    }

    // ── Drag-and-drop for plugin button reordering ──

    /// <summary>
    /// Raised after a successful drag-drop reorder of plugin buttons.
    /// </summary>
    public event Action<int, int>? PluginButtonReordered;

    private void OnPluginButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dragSourceButton = sender as Button;
    }

    private void OnPluginButtonMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceButton is null)
            return;

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        // Only start drag after a minimum distance to avoid accidental drags
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = new DataObject("PluginButton", _dragSourceButton);
        DragDrop.DoDragDrop(_dragSourceButton, data, DragDropEffects.Move);
        _dragSourceButton = null;
    }

    private void OnPluginDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("PluginButton"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnPluginDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("PluginButton"))
            return;

        var source = e.Data.GetData("PluginButton") as Button;
        if (source is null) return;

        var oldIndex = PluginButtonsPanel.Children.IndexOf(source);
        if (oldIndex < 0) return;

        // Determine drop target index based on mouse Y position
        var newIndex = GetDropIndex(e.GetPosition(PluginButtonsPanel));
        if (newIndex < 0) newIndex = PluginButtonsPanel.Children.Count - 1;

        // Clamp to valid range
        newIndex = Math.Clamp(newIndex, 0, PluginButtonsPanel.Children.Count - 1);

        if (oldIndex == newIndex) return;

        // Physically reorder in the panel
        PluginButtonsPanel.Children.RemoveAt(oldIndex);
        PluginButtonsPanel.Children.Insert(newIndex, source);

        // Notify MainWindow to persist the new order
        PluginButtonReordered?.Invoke(oldIndex, newIndex);

        e.Handled = true;
    }

    /// <summary>
    /// Determines the drop index based on the Y position within the PluginButtonsPanel.
    /// Returns the index of the slot the item should be inserted at.
    /// </summary>
    private int GetDropIndex(Point position)
    {
        for (var i = 0; i < PluginButtonsPanel.Children.Count; i++)
        {
            if (PluginButtonsPanel.Children[i] is FrameworkElement child)
            {
                var childTop = child.TranslatePoint(new Point(0, 0), PluginButtonsPanel).Y;
                var childMid = childTop + child.ActualHeight / 2;
                if (position.Y < childMid)
                    return i;
            }
        }

        return PluginButtonsPanel.Children.Count - 1;
    }
}
