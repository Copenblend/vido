using System.Linq;
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

    /// <summary>Raised when View > Show Hidden Files is toggled.</summary>
    public event Action? ToggleShowHiddenFilesRequested;

    /// <summary>
    /// Function that returns whether hidden files are currently shown.
    /// Set by MainWindow so TitleBarView can sync the checkmark state.
    /// </summary>
    public Func<bool>? GetShowHiddenFiles { get; set; }

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

    /// <summary>Raised when a playback speed is selected from the menu.</summary>
    public event Action<double>? PlaybackSpeedSelected;

    /// <summary>
    /// Function that returns the current playback speed.
    /// Set by MainWindow so TitleBarView can sync checkmarks.
    /// </summary>
    public Func<double>? GetPlaybackSpeed { get; set; }

    /// <summary>Raised when View > Fullscreen is clicked.</summary>
    public event Action? FullscreenRequested;

    /// <summary>Raised when a recent file is selected from File > Recent Files.</summary>
    public event Action<string>? RecentFileSelected;

    /// <summary>Raised when File > Recent Files > Clear Watch History is clicked.</summary>
    public event Action? ClearWatchHistoryRequested;

    /// <summary>
    /// Function that returns the current list of recent files.
    /// Set by MainWindow so TitleBarView can populate the submenu without
    /// directly depending on IStateService.
    /// </summary>
    public Func<IReadOnlyList<string>>? GetRecentFiles { get; set; }

    /// <summary>Whether the bottom panel is currently visible. Used to update the submenu text.</summary>
    private bool _isBottomPanelVisible;

    /// <summary>Whether the right panel is currently visible. Used to update the submenu text.</summary>
    private bool _isRightPanelVisible;

    /// <summary>Whether the sidebar is currently visible. Used to update the submenu text.</summary>
    private bool _isSidebarVisible;

    /// <summary>Whether the status bar is currently visible. Used to update the submenu text.</summary>
    private bool _isStatusBarVisible;

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

    /// <summary>
    /// Updates the Sidebar submenu state based on current sidebar visibility.
    /// Called by MainWindow when the sidebar visibility changes.
    /// </summary>
    public void SetSidebarVisible(bool visible)
    {
        _isSidebarVisible = visible;
    }

    /// <summary>
    /// Updates the Status Bar submenu state based on current status bar visibility.
    /// Called by MainWindow when the status bar visibility changes.
    /// </summary>
    public void SetStatusBarVisible(bool visible)
    {
        _isStatusBarVisible = visible;
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

    private void OnViewSubmenuOpened(object sender, RoutedEventArgs e)
    {
        ShowHiddenFilesMenuItem.IsChecked = GetShowHiddenFiles?.Invoke() ?? false;
        ShowHideSidebarMenuItem.Header = _isSidebarVisible ? "_Hide Sidebar" : "_Show Sidebar";
        ShowHideStatusBarMenuItem.Header = _isStatusBarVisible ? "Hide S_tatus Bar" : "Show S_tatus Bar";
    }

    private void OnToggleShowHiddenFilesClick(object sender, RoutedEventArgs e)
    {
        ToggleShowHiddenFilesRequested?.Invoke();
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

    private void OnSpeedSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem parent) return;
        var currentSpeed = GetPlaybackSpeed?.Invoke() ?? 1.0;

        foreach (var item in parent.Items.OfType<MenuItem>())
        {
            if (item.Tag is string tagStr && double.TryParse(tagStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed))
            {
                item.IsChecked = Math.Abs(currentSpeed - speed) < 0.01;
            }
        }
    }

    private void OnSpeedMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tagStr
            && double.TryParse(tagStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            PlaybackSpeedSelected?.Invoke(speed);
        }
    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e)
    {
        FullscreenRequested?.Invoke();
    }

    private void OnRecentFilesSubmenuOpened(object sender, RoutedEventArgs e)
    {
        RecentFilesMenu.Items.Clear();

        var recentFiles = GetRecentFiles?.Invoke();
        if (recentFiles is null || recentFiles.Count == 0)
        {
            var empty = new MenuItem
            {
                Header = "No recent files",
                IsEnabled = false,
            };
            empty.SetResourceReference(StyleProperty, "DropdownMenuItemStyle");
            RecentFilesMenu.Items.Add(empty);
            return;
        }

        foreach (var filePath in recentFiles)
        {
            var item = new MenuItem
            {
                Header = System.IO.Path.GetFileName(filePath),
                ToolTip = filePath,
            };
            item.SetResourceReference(StyleProperty, "DropdownMenuItemStyle");
            var path = filePath; // capture for closure
            item.Click += (_, _) => RecentFileSelected?.Invoke(path);
            RecentFilesMenu.Items.Add(item);
        }

        // Add separator and Clear Watch History
        var separator = new Separator();
        separator.SetResourceReference(StyleProperty, "MenuSeparatorStyle");
        RecentFilesMenu.Items.Add(separator);

        var clearItem = new MenuItem
        {
            Header = "Clear Watch History",
        };
        clearItem.SetResourceReference(StyleProperty, "DropdownMenuItemStyle");
        clearItem.Click += (_, _) => ClearWatchHistoryRequested?.Invoke();
        RecentFilesMenu.Items.Add(clearItem);
    }
}
