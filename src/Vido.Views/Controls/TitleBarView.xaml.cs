using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Custom title bar matching VS Code Dark Modern style.
/// Supports drag-to-move, double-click maximize/restore, and window control buttons.
/// </summary>
public partial class TitleBarView : UserControl
{
    /// <summary>Raised when the user selects File > Open Folder and picks a valid path.</summary>
    public event Action<string>? FolderOpened;

    /// <summary>Raised when the user selects File > Close Folder.</summary>
    public event Action? FolderClosed;

    /// <summary>Raised when the user selects File > Rescan Folder.</summary>
    public event Action? FolderRescanned;

    public TitleBarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the Close Folder menu item enabled state based on whether a folder is open.
    /// </summary>
    public void SetCloseFolderEnabled(bool enabled)
    {
        CloseFolderMenuItem.IsEnabled = enabled;
        RescanFolderMenuItem.IsEnabled = enabled;
    }

    /// <summary>
    /// Updates the maximize/restore icon and tooltip when the window state changes.
    /// </summary>
    public void UpdateWindowState(bool isMaximized)
    {
        MaximizeIcon.Children.Clear();

        if (isMaximized)
        {
            // Restore icon: two overlapping rectangles
            var backRect = new Rectangle
            {
                Width = 8, Height = 8,
                StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 2)
            };
            backRect.SetResourceReference(Shape.StrokeProperty, "PrimaryForegroundBrush");

            var frontRect = new Rectangle
            {
                Width = 8, Height = 8,
                StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 2, 0, 0)
            };
            frontRect.SetResourceReference(Shape.StrokeProperty, "PrimaryForegroundBrush");

            MaximizeIcon.Children.Add(backRect);
            MaximizeIcon.Children.Add(frontRect);

            MaximizeRestoreButton.ToolTip = "Restore Down";
        }
        else
        {
            // Maximize icon: single rectangle
            var rect = new Rectangle
            {
                Width = 9, Height = 9,
                StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent
            };
            rect.SetResourceReference(Shape.StrokeProperty, "PrimaryForegroundBrush");

            MaximizeIcon.Children.Add(rect);

            MaximizeRestoreButton.ToolTip = "Maximize";
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ClickCount == 2)
        {
            if (DataContext is TitleBarViewModel vm)
            {
                vm.ToggleMaximizeCommand.Execute(null);
            }
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Open Folder"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FolderName))
        {
            FolderOpened?.Invoke(dialog.FolderName);
        }
    }

    private void OnCloseFolderClick(object sender, RoutedEventArgs e)
    {
        FolderClosed?.Invoke();
    }

    private void OnRescanFolderClick(object sender, RoutedEventArgs e)
    {
        FolderRescanned?.Invoke();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // ── View menu events ──

    /// <summary>Raised when View > Bottom Panel > Show/Hide is clicked.</summary>
    public event Action? ToggleBottomPanelRequested;

    /// <summary>Raised when View > Bottom Panel > Log Output is clicked to show the output panel.</summary>
    public event Action? ShowOutputRequested;

    /// <summary>Raised when View > Right Panel > Show/Hide is clicked.</summary>
    public event Action? ToggleRightPanelRequested;

    /// <summary>Raised when View > Right Panel > Video Info is clicked.</summary>
    public event Action? ShowVideoInfoRequested;

    /// <summary>Raised when View > Toggle Status Bar is clicked.</summary>
    public event Action? ToggleStatusBarRequested;

    /// <summary>Raised when View > Toggle Sidebar is clicked.</summary>
    public event Action? ToggleSidebarRequested;

    // ── Playback menu events ──

    /// <summary>Raised when Playback > Play/Pause is clicked.</summary>
    public event Action? PlayPauseRequested;

    /// <summary>Raised when Playback > Stop is clicked.</summary>
    public event Action? StopRequested;

    /// <summary>Raised when Playback > Skip Forward is clicked.</summary>
    public event Action? SkipForwardRequested;

    /// <summary>Raised when Playback > Skip Backward is clicked.</summary>
    public event Action? SkipBackwardRequested;

    /// <summary>Raised when Playback > Loop is clicked.</summary>
    public event Action? LoopRequested;

    /// <summary>Raised when View > Fullscreen is clicked.</summary>
    public event Action? FullscreenRequested;

    /// <summary>Whether the bottom panel is currently visible. Used to update the submenu text.</summary>
    private bool _isBottomPanelVisible;

    /// <summary>Whether the right panel is currently visible. Used to update the submenu text.</summary>
    private bool _isRightPanelVisible;

    /// <summary>
    /// Updates the Bottom Panel submenu state based on current panel visibility.
    /// Called by MainWindow when the panel visibility changes.
    /// </summary>
    public void SetBottomPanelVisible(bool visible)
    {
        _isBottomPanelVisible = visible;
    }

    /// <summary>
    /// Updates the Right Panel submenu state based on current panel visibility.
    /// Called by MainWindow when the panel visibility changes.
    /// </summary>
    public void SetRightPanelVisible(bool visible)
    {
        _isRightPanelVisible = visible;
    }

    private void OnBottomPanelSubmenuOpened(object sender, RoutedEventArgs e)
    {
        ShowHideBottomPanelMenuItem.Header = _isBottomPanelVisible
            ? "_Hide Bottom Panel"
            : "_Show Bottom Panel";
    }

    private void OnRightPanelSubmenuOpened(object sender, RoutedEventArgs e)
    {
        ShowHideRightPanelMenuItem.Header = _isRightPanelVisible
            ? "_Hide Right Panel"
            : "_Show Right Panel";
    }

    private void OnToggleBottomPanelClick(object sender, RoutedEventArgs e)
    {
        ToggleBottomPanelRequested?.Invoke();
    }

    private void OnShowOutputClick(object sender, RoutedEventArgs e)
    {
        ShowOutputRequested?.Invoke();
    }

    private void OnToggleRightPanelClick(object sender, RoutedEventArgs e)
    {
        ToggleRightPanelRequested?.Invoke();
    }

    private void OnShowVideoInfoClick(object sender, RoutedEventArgs e)
    {
        ShowVideoInfoRequested?.Invoke();
    }

    private void OnToggleStatusBarClick(object sender, RoutedEventArgs e)
    {
        ToggleStatusBarRequested?.Invoke();
    }

    private void OnToggleSidebarClick(object sender, RoutedEventArgs e)
    {
        ToggleSidebarRequested?.Invoke();
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        PlayPauseRequested?.Invoke();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        StopRequested?.Invoke();
    }

    private void OnSkipForwardClick(object sender, RoutedEventArgs e)
    {
        SkipForwardRequested?.Invoke();
    }

    private void OnSkipBackwardClick(object sender, RoutedEventArgs e)
    {
        SkipBackwardRequested?.Invoke();
    }

    private void OnLoopClick(object sender, RoutedEventArgs e)
    {
        LoopRequested?.Invoke();
    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e)
    {
        FullscreenRequested?.Invoke();
    }
}
