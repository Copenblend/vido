using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vido.Core.Layout;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Tab strip control displaying all open tabs. Supports click-to-activate,
/// close button, and drag-to-reorder.
/// </summary>
public partial class TabWell : UserControl
{
    private Point _dragStart;
    private bool _isDragging;
    private TabItemModel? _dragTab;
    private const double DragThreshold = 6;

    /// <summary>
    /// Sets up the tab strip control and registers mouse event handlers for tab interaction.
    /// </summary>
    public TabWell()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    // ── Tab click to activate ──

    private void OnTabMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not TabItemModel tab) return;

        ViewModel?.ActivateTab(tab.Id);

        // Record drag start for potential reorder
        _dragStart = e.GetPosition(this);
        _dragTab = tab;
        _isDragging = false;
        element.CaptureMouse();

        e.Handled = true;
    }

    private void OnTabMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab is null || _dragTab.IsPinned) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);
        var delta = pos - _dragStart;

        if (!_isDragging && Math.Abs(delta.X) > DragThreshold)
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            // Find the tab under the cursor and reorder
            var hitTab = FindTabAtPosition(pos);
            if (hitTab is not null && hitTab != _dragTab && ViewModel is not null)
            {
                var fromIndex = ViewModel.Tabs.IndexOf(_dragTab);
                var toIndex = ViewModel.Tabs.IndexOf(hitTab);
                if (fromIndex >= 0 && toIndex >= 0)
                {
                    ViewModel.ReorderTab(fromIndex, toIndex);
                }
            }
        }
    }

    private void OnTabMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
            element.ReleaseMouseCapture();

        _dragTab = null;
        _isDragging = false;
    }

    // ── Tab close ──

    private void OnTabCloseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not TabItemModel tab) return;

        ViewModel?.CloseTab(tab.Id);
        e.Handled = true;
    }

    // ── Helpers ──

    /// <summary>
    /// Finds the TabItemModel under the given position by hit-testing
    /// the visual tree for a FrameworkElement with a TabItemModel Tag.
    /// </summary>
    private TabItemModel? FindTabAtPosition(Point position)
    {
        var hit = InputHitTest(position) as DependencyObject;
        while (hit is not null)
        {
            if (hit is FrameworkElement fe && fe.Tag is TabItemModel tab)
                return tab;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }
}
