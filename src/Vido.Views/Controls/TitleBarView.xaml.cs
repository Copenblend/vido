using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Shapes;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Custom title bar matching VS Code Dark Modern style.
/// Supports drag-to-move, double-click maximize/restore, and window control buttons.
/// </summary>
public partial class TitleBarView : UserControl
{
    /// <summary>File filter for video file dialogs.</summary>
    private const string VideoFileFilter =
        "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm|All Files|*.*";

    /// <summary>Raised when the user selects File > Open File and picks a video file.</summary>
    public event Action<string>? FileOpened;

    /// <summary>Raised when the user selects File > Open Folder and picks a valid path.</summary>
    public event Action<string>? FolderOpened;

    /// <summary>Raised when the user selects File > Close Folder.</summary>
    public event Action? FolderClosed;

    /// <summary>Raised when the user selects File > Rescan Folder.</summary>
    public event Action? FolderRescanned;

    /// <summary>Raised when the user selects File > Add File and picks one or more files.</summary>
    public event Action<string[]>? FilesAdded;

    /// <summary>Raised when the user selects File > Add Folder and picks a folder.</summary>
    public event Action<string[]>? FolderAddedToExplorer;

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

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Video File",
            Filter = VideoFileFilter
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FileName))
        {
            FileOpened?.Invoke(dialog.FileName);
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

    private void OnAddFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add File to Explorer",
            Multiselect = true,
            Filter = VideoFileFilter
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            FilesAdded?.Invoke(dialog.FileNames);
        }
    }

    private void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Add Folder to Explorer"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FolderName))
        {
            FolderAddedToExplorer?.Invoke([dialog.FolderName]);
        }
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

    /// <summary>Raised when View > Bottom Panel > Show/Hide Log Output is toggled.</summary>
    public event Action? ToggleLogOutputRequested;

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

    /// <summary>Raised when Help > About Vido is clicked.</summary>
    public event Action? AboutRequested;

    /// <summary>Raised when Help > Check for Updates is clicked.</summary>
    public event Action? CheckForUpdatesRequested;

    /// <summary>Raised when Help > Enter Repository Code is clicked.</summary>
    public event Action? EnterRepositoryCodeRequested;

    /// <summary>Raised when the screenshot button is clicked.</summary>
    public event Action? ScreenshotRequested;

    /// <summary>
    /// Function that returns the current list of recent files.
    /// Set by MainWindow so TitleBarView can populate the submenu without
    /// directly depending on IStateService.
    /// </summary>
    public Func<IReadOnlyList<string>>? GetRecentFiles { get; set; }

    /// <summary>Whether the bottom panel is currently visible. Used to update the submenu text.</summary>
    private bool _isBottomPanelVisible;

    /// <summary>Whether the Log Output tab is currently visible. Used to update the submenu text.</summary>
    private bool _isLogOutputVisible;

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
    /// Updates the Log Output submenu state based on current tab visibility.
    /// Called by MainWindow when the Log Output tab is toggled.
    /// </summary>
    public void SetLogOutputVisible(bool visible)
    {
        _isLogOutputVisible = visible;
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
        ShowHideLogOutputMenuItem.Header = _isLogOutputVisible
            ? "Hide _Log Output"
            : "Show _Log Output";
    }

    private void OnStatusBarSubmenuOpened(object sender, RoutedEventArgs e)
    {
        ShowHideStatusBarMenuItem.Header = _isStatusBarVisible
            ? "Hide S_tatus Bar"
            : "Show S_tatus Bar";
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

    private void OnToggleLogOutputClick(object sender, RoutedEventArgs e)
    {
        ToggleLogOutputRequested?.Invoke();
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

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        AboutRequested?.Invoke();
    }

    private void OnCheckForUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesRequested?.Invoke();
    }

    private void OnEnterRepositoryCodeClick(object sender, RoutedEventArgs e)
    {
        EnterRepositoryCodeRequested?.Invoke();
    }

    private void OnScreenshotClick(object sender, RoutedEventArgs e)
    {
        ScreenshotRequested?.Invoke();
    }

    /// <summary>The screenshot button instance, created on demand.</summary>
    private Button? _screenshotButton;

    /// <summary>Shows or hides the screenshot button in the title bar toolbar area.</summary>
    public void SetScreenshotButtonVisible(bool visible)
    {
        if (visible)
        {
            if (_screenshotButton is null)
            {
                _screenshotButton = CreateScreenshotButton();
            }

            EnsureToolbarPanelExists();

            // Remove first to avoid duplicate, then add at end
            _pluginToolbarPanel!.Children.Remove(_screenshotButton);
            _pluginToolbarPanel.Children.Add(_screenshotButton);
            UpdatePluginToolbarVisibility();
        }
        else if (_screenshotButton is not null && _pluginToolbarPanel is not null)
        {
            _pluginToolbarPanel.Children.Remove(_screenshotButton);
            UpdatePluginToolbarVisibility();
        }
    }

    /// <summary>Creates the screenshot button styled identically to plugin toolbar buttons.</summary>
    private Button CreateScreenshotButton()
    {
        // Camera icon: body path + lens ellipse
        var bodyPath = new System.Windows.Shapes.Path
        {
            Data = System.Windows.Media.Geometry.Parse("M 2,5 L 5,5 6,3 10,3 11,5 14,5 14,13 2,13 Z"),
            StrokeThickness = 1,
            Fill = System.Windows.Media.Brushes.Transparent,
        };
        bodyPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PrimaryForegroundBrush");

        var lensEllipse = new System.Windows.Shapes.Ellipse
        {
            Width = 5,
            Height = 5,
            StrokeThickness = 1,
            Fill = System.Windows.Media.Brushes.Transparent,
        };
        lensEllipse.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PrimaryForegroundBrush");
        Canvas.SetLeft(lensEllipse, 5.5);
        Canvas.SetTop(lensEllipse, 7);

        var canvas = new Canvas { Width = 16, Height = 16 };
        canvas.Children.Add(bodyPath);
        canvas.Children.Add(lensEllipse);

        var button = new Button
        {
            ToolTip = "Take Screenshot",
            Content = canvas,
            Height = 22,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        // Custom template matching plugin toolbar button style
        var bdName = "Bd";
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border), bdName);
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
        borderFactory.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new DynamicResourceExtension("HoverBackgroundBrush"), bdName));
        template.Triggers.Add(hoverTrigger);
        template.VisualTree = borderFactory;
        button.Template = template;

        button.Click += OnScreenshotClick;
        return button;
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

    // ── Plugin toolbar buttons ──

    // ── Status bar plugin items submenu ──

    /// <summary>Map of status bar registration ID → menu item for removal.</summary>
    private readonly Dictionary<string, MenuItem> _statusBarMenuItems = [];

    /// <summary>Raised when a plugin status bar item's show/hide is toggled.</summary>
    public event Action<string, bool>? ToggleStatusBarItemRequested;

    /// <summary>
    /// Adds a "Show/Hide {name}" menu item to View > Bottom Panel > Status Bar submenu.
    /// </summary>
    public void AddStatusBarMenuItem(string registrationId, string name)
    {
        var menuItem = new MenuItem
        {
            Header = $"Hide {name}",
            Tag = registrationId,
            IsCheckable = false,
        };
        menuItem.SetResourceReference(StyleProperty, "DropdownMenuItemStyle");

        bool visible = true;
        menuItem.Click += (_, _) =>
        {
            visible = !visible;
            menuItem.Header = visible ? $"Hide {name}" : $"Show {name}";
            ToggleStatusBarItemRequested?.Invoke(registrationId, visible);
        };

        // Insert before the Show/Hide Status Bar item (last item)
        var insertIndex = StatusBarMenu.Items.Count - 1;
        if (insertIndex < 0) insertIndex = 0;
        StatusBarMenu.Items.Insert(insertIndex, menuItem);
        _statusBarMenuItems[registrationId] = menuItem;
    }

    /// <summary>
    /// Removes a plugin's status bar menu item by registration ID.
    /// </summary>
    public void RemoveStatusBarMenuItem(string registrationId)
    {
        if (_statusBarMenuItems.TryGetValue(registrationId, out var menuItem))
        {
            StatusBarMenu.Items.Remove(menuItem);
            _statusBarMenuItems.Remove(registrationId);
        }
    }

    // ── Bottom panel tab show/hide items ──

    /// <summary>Map of bottom panel tab ID → menu item for removal.</summary>
    private readonly Dictionary<string, MenuItem> _bottomPanelTabMenuItems = [];

    /// <summary>Raised when a bottom panel tab's show/hide is toggled. Params: tabId, visible.</summary>
    public event Action<string, bool>? ToggleBottomPanelTabRequested;

    /// <summary>
    /// Adds a "Hide/Show {name}" menu item to View > Bottom Panel submenu for a plugin tab.
    /// Inserted before the Show/Hide Bottom Panel toggle (last item).
    /// </summary>
    public void AddBottomPanelTabMenuItem(string tabId, string name)
    {
        var menuItem = new MenuItem
        {
            Header = $"Hide {name}",
            Tag = tabId,
        };
        menuItem.SetResourceReference(StyleProperty, "DropdownMenuItemStyle");

        bool visible = true;
        menuItem.Click += (_, _) =>
        {
            visible = !visible;
            menuItem.Header = visible ? $"Hide {name}" : $"Show {name}";
            ToggleBottomPanelTabRequested?.Invoke(tabId, visible);
        };

        // Insert before the last item (Show/Hide Bottom Panel)
        var insertIndex = BottomPanelMenu.Items.Count - 1;
        if (insertIndex < 0) insertIndex = 0;
        BottomPanelMenu.Items.Insert(insertIndex, menuItem);
        _bottomPanelTabMenuItems[tabId] = menuItem;
    }

    /// <summary>
    /// Removes a plugin's bottom panel tab menu item by tab ID.
    /// </summary>
    public void RemoveBottomPanelTabMenuItem(string tabId)
    {
        if (_bottomPanelTabMenuItems.TryGetValue(tabId, out var menuItem))
        {
            BottomPanelMenu.Items.Remove(menuItem);
            _bottomPanelTabMenuItems.Remove(tabId);
        }
    }

    /// <summary>Lazy-initialized container panel for plugin toolbar buttons.</summary>
    private StackPanel? _pluginToolbarPanel;

    /// <summary>Styled border wrapping the plugin toolbar panel.</summary>
    private Border? _pluginToolbarBorder;

    /// <summary>
    /// Adds a plugin toolbar button to the title bar. Creates a styled, bordered
    /// container panel in the drag area (column 2) if needed.
    /// The screenshot button (if present) is always kept as the rightmost item.
    /// </summary>
    public void AddPluginToolbarButton(Button button)
    {
        EnsureToolbarPanelExists();

        // Insert before the screenshot button if it's present, otherwise append
        if (_screenshotButton is not null && _pluginToolbarPanel!.Children.Contains(_screenshotButton))
        {
            var idx = _pluginToolbarPanel.Children.IndexOf(_screenshotButton);
            _pluginToolbarPanel.Children.Insert(idx, button);
        }
        else
        {
            _pluginToolbarPanel!.Children.Add(button);
        }

        UpdatePluginToolbarVisibility();
    }

    /// <summary>
    /// Ensures the toolbar panel and border container exist, creating them on first use.
    /// </summary>
    private void EnsureToolbarPanelExists()
    {
        if (_pluginToolbarPanel is not null)
            return;

        _pluginToolbarPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _pluginToolbarBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 8, 2),
            Child = _pluginToolbarPanel,
        };
        _pluginToolbarBorder.SetResourceReference(Border.BackgroundProperty, "EditorBackgroundBrush");
        _pluginToolbarBorder.SetResourceReference(Border.BorderBrushProperty, "PrimaryBorderBrush");

        Grid.SetColumn(_pluginToolbarBorder, 2);
        WindowChrome.SetIsHitTestVisibleInChrome(_pluginToolbarBorder, true);

        if (Content is Grid grid)
        {
            grid.Children.Add(_pluginToolbarBorder);
        }
    }

    /// <summary>
    /// Removes a plugin toolbar button from the title bar.
    /// </summary>
    public void RemovePluginToolbarButton(Button button)
    {
        _pluginToolbarPanel?.Children.Remove(button);
        UpdatePluginToolbarVisibility();
    }

    /// <summary>
    /// Shows/hides the plugin toolbar border based on whether any buttons remain.
    /// </summary>
    private void UpdatePluginToolbarVisibility()
    {
        if (_pluginToolbarBorder is not null)
        {
            _pluginToolbarBorder.Visibility = _pluginToolbarPanel?.Children.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    // ── Plugin right panel menu items ──

    /// <summary>Map of panel ID → menu item for removal.</summary>
    private readonly Dictionary<string, MenuItem> _rightPanelMenuItems = [];

    /// <summary>
    /// Adds a menu item to the View → Right Panel submenu for a plugin-contributed panel.
    /// Inserted before the Show/Hide toggle item.
    /// </summary>
    public void AddRightPanelMenuItem(string title, Action onSelected)
    {
        AddRightPanelMenuItem(null, title, onSelected);
    }

    /// <summary>
    /// Adds a menu item to the View → Right Panel submenu for a plugin-contributed panel.
    /// Inserted before the Show/Hide toggle item. Optionally tracked by panelId for removal.
    /// </summary>
    public void AddRightPanelMenuItem(string? panelId, string title, Action onSelected)
    {
        var menuItem = new MenuItem
        {
            Header = $"_{title}",
        };
        menuItem.SetResourceReference(StyleProperty, "DropdownMenuItemStyle");
        menuItem.Click += (_, _) => onSelected();

        // Insert before the Show/Hide toggle (last item) and separator (second-to-last)
        var insertIndex = RightPanelMenu.Items.Count - 1;
        if (insertIndex < 0) insertIndex = 0;
        RightPanelMenu.Items.Insert(insertIndex, menuItem);

        if (panelId is not null)
            _rightPanelMenuItems[panelId] = menuItem;
    }

    /// <summary>
    /// Removes a plugin's right panel menu item by panel ID.
    /// </summary>
    public void RemoveRightPanelMenuItem(string panelId)
    {
        if (_rightPanelMenuItems.TryGetValue(panelId, out var menuItem))
        {
            RightPanelMenu.Items.Remove(menuItem);
            _rightPanelMenuItems.Remove(panelId);
        }
    }
}
