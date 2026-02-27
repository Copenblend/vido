using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Layout;
using KeyBinding = Vido.Core.Keyboard.KeyBinding;
using Vido.Core.Menus;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.Core.Windowing;
using Vido.ViewModels;
using Vido.Views.Panels;
using Vido.Views.Services;
using Vido.Core.Logging;
using Vido.Core.Updates;
using Vido.Views.Controls;

namespace Vido.Views;

/// <summary>
/// Main application window. Frameless with custom resize/move behavior
/// and Dark Modern theme matching VS Code.
/// </summary>
public partial class MainWindow : Window
{
    private const int MinWindowWidth = 800;
    private const int MinWindowHeight = 600;

    private readonly IStateService _stateService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly FileExplorerViewModel _fileExplorerViewModel;
    private readonly VideoPlayerViewModel _videoPlayerViewModel;
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly OutputLogViewModel _outputLogViewModel;
    private readonly VideoDetailsViewModel _videoDetailsViewModel;
    private readonly StatusBarViewModel _statusBarViewModel;
    private readonly IKeyboardShortcutService _keyboardShortcutService;
    private readonly IContributionRegistry _contributionRegistry;
    private readonly IContextMenuRegistry _contextMenuRegistry;
    private readonly IPluginHost _pluginHost;
    private readonly IUpdateService _updateService;

    private string[]? _pendingCommandLineArgs;
    private string? _pendingInstallerPath;

    /// <summary>
    /// FFmpeg version string, set by App.xaml.cs after initialization.
    /// Used by the About dialog.
    /// </summary>
    public string? FFmpegVersion { get; set; }

    private TitleBarViewModel? _titleBarViewModel;
    private ActivityBarViewModel? _activityBarViewModel;
    private SidebarViewModel? _sidebarViewModel;
    private FileExplorerPanel? _fileExplorerPanel;
    private PluginManagerPanel? _pluginManagerPanel;
    private PluginManagerViewModel? _pluginManagerViewModel;
    private SettingsPage? _settingsPage;
    private readonly AppSettingsStore _appSettingsStore;

    // Remembered panel dimensions for toggle persistence
    private double _bottomPanelHeight = 200;
    private double _rightPanelWidth = 300;

    // ── Fullscreen state ──
    private bool _isFullscreen;
    private WindowState _preFullscreenWindowState;
    private double _preFullscreenLeft;
    private double _preFullscreenTop;
    private double _preFullscreenWidth;
    private double _preFullscreenHeight;
    private Thickness _preFullscreenBorderThickness;

    // Pre-fullscreen UI visibility state
    private bool _preFullscreenSidebarVisible;
    private bool _preFullscreenBottomPanelVisible;
    private bool _preFullscreenBottomPanelCollapsed;
    private bool _preFullscreenRightPanelVisible;
    private bool _preFullscreenRightPanelCollapsed;
    private bool _preFullscreenStatusBarVisible;
    private string? _preFullscreenActiveTabId;

    // Fullscreen auto-hide timer
    private DispatcherTimer? _fullscreenHideTimer;
    private bool _controlsVisible = true;
    private const int FullscreenHideDelayMs = 3000;
    private const int ControlsFadeDurationMs = 200;

    public MainWindow(
        IStateService stateService,
        ISettingsService settingsService,
        ILogService logService,
        IKeyboardShortcutService keyboardShortcutService,
        IContributionRegistry contributionRegistry,
        IContextMenuRegistry contextMenuRegistry,
        IPluginInstaller pluginInstaller,
        IPluginHost pluginHost,
        IUpdateService updateService,
        FileExplorerViewModel fileExplorerViewModel,
        VideoPlayerViewModel videoPlayerViewModel,
        MainWindowViewModel mainWindowViewModel,
        OutputLogViewModel outputLogViewModel,
        VideoDetailsViewModel videoDetailsViewModel,
        StatusBarViewModel statusBarViewModel)
    {
        _stateService = stateService;
        _settingsService = settingsService;
        _logService = logService;
        _keyboardShortcutService = keyboardShortcutService;
        _contributionRegistry = contributionRegistry;
        _contextMenuRegistry = contextMenuRegistry;
        _pluginHost = pluginHost;
        _updateService = updateService;
        _fileExplorerViewModel = fileExplorerViewModel;
        _videoPlayerViewModel = videoPlayerViewModel;
        _mainWindowViewModel = mainWindowViewModel;
        _outputLogViewModel = outputLogViewModel;
        _videoDetailsViewModel = videoDetailsViewModel;
        _statusBarViewModel = statusBarViewModel;

        // Shared settings store — used by SettingsPage and for direct change monitoring
        _appSettingsStore = new AppSettingsStore(settingsService);
        _appSettingsStore.SettingChanged += OnAppSettingChanged;

        // Create the Plugin Manager ViewModel
        _pluginManagerViewModel = new PluginManagerViewModel(
            pluginHost, pluginInstaller, settingsService, logService);

        InitializeComponent();
        SetupWindowChrome();
        SetupTitleBar();
        SetupLayout();
        SetupTabSystem();
        SetupVideoPlayer();
        SetupOutputLog();
        SetupVideoDetails();
        SetupStatusBar();
        SetupKeyboardShortcuts();
        SetupFileExplorer();
        SetupDragDrop();
        SetupPluginContributions();
        RestoreWindowState();
        RestoreLayoutState();

        _logService.Info("Vido started", "App");
    }

    /// <summary>
    /// Stores command-line arguments for deferred processing after the window
    /// has fully loaded (so the video engine and UI are ready).
    /// </summary>
    public void ProcessCommandLineArgs(string[] args)
    {
        if (args.Length == 0) return;
        _pendingCommandLineArgs = args;
    }

    /// <summary>
    /// Executes the stored command-line arguments. Called from the Loaded event
    /// to ensure the video engine and visual tree are fully initialized.
    /// </summary>
    private async Task ExecutePendingCommandLineArgsAsync()
    {
        var args = _pendingCommandLineArgs;
        _pendingCommandLineArgs = null;
        if (args is null || args.Length == 0) return;

        var arg = args[0].Trim('"');
        if (string.IsNullOrWhiteSpace(arg)) return;

        if (File.Exists(arg))
        {
            _logService.Info($"Opening file from command line: {arg}", "App");

            // Open the file's parent directory in explorer
            var parentDir = Path.GetDirectoryName(arg);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var currentFolder = _fileExplorerViewModel.FolderPath;
                if (currentFolder is null ||
                    !string.Equals(currentFolder, parentDir, StringComparison.OrdinalIgnoreCase))
                {
                    OnFolderOpened(parentDir);
                }
            }

            // Switch to Player tab and load the video
            _logService.Info($"Activating Player tab", "App");
            _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
            _logService.Info($"Calling LoadAndPlayAsync for: {arg}", "App");
            await _videoPlayerViewModel.LoadAndPlayAsync(arg);
            _logService.Info($"LoadAndPlayAsync completed for: {arg}", "App");
        }
        else if (Directory.Exists(arg))
        {
            _logService.Info($"Opening folder from command line: {arg}", "App");
            OnFolderOpened(arg);
        }
        else
        {
            _logService.Warning($"Command-line argument is not a valid file or folder: {arg}", "App");
        }
    }

    private void SetupWindowChrome()
    {
        var chrome = new WindowChrome
        {
            CaptionHeight = 30,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false
        };
        WindowChrome.SetWindowChrome(this, chrome);
    }

    private void SetupTitleBar()
    {
        var windowService = new WindowService(this);
        _titleBarViewModel = new TitleBarViewModel(windowService);
        TitleBar.DataContext = _titleBarViewModel;

        StateChanged += OnWindowStateChanged;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        // In fullscreen mode, skip normal state sync — fullscreen manages its own chrome
        if (_isFullscreen) return;

        var appState = WindowState switch
        {
            WindowState.Maximized => AppWindowState.Maximized,
            WindowState.Minimized => AppWindowState.Minimized,
            _ => AppWindowState.Normal
        };

        _titleBarViewModel?.SyncWindowState(appState);
        TitleBar.UpdateWindowState(appState == AppWindowState.Maximized);

        // Hide window border when maximized (no visible frame at screen edges)
        WindowBorder.BorderThickness = new Thickness(appState == AppWindowState.Maximized ? 0 : 1);
    }

    private void SetupLayout()
    {
        _activityBarViewModel = new ActivityBarViewModel(_settingsService);
        _sidebarViewModel = new SidebarViewModel();

        ActivityBar.DataContext = _activityBarViewModel;
        Sidebar.DataContext = _sidebarViewModel;

        // Settings gear opens as a tab, not in the sidebar
        ActivityBar.SettingsRequested += (_, _) => _mainWindowViewModel.OpenSettings();

        // Initialize visual states
        ActivityBar.UpdateActiveStates();
    }

    private void SetupVideoPlayer()
    {
        VideoPlayer.DataContext = _videoPlayerViewModel;
        VideoPlayer.FullscreenToggleRequested += ToggleFullscreen;
    }

    private void SetupOutputLog()
    {
        var outputLogPanel = new OutputLogPanel
        {
            DataContext = _outputLogViewModel
        };

        BottomPanelContent.Content = outputLogPanel;

        // Store panel content mapping for bottom panel tab switching
        _bottomPanelContents[MainWindowViewModel.OutputTabId] = outputLogPanel;

        // Wire bottom panel tab content switching
        _mainWindowViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ActiveBottomPanelTab))
                UpdateBottomPanelContent();
        };
    }

    /// <summary>Map of bottom panel tab IDs to their content controls.</summary>
    private readonly Dictionary<string, UIElement> _bottomPanelContents = [];

    private void SetupVideoDetails()
    {
        var videoDetailsPanel = new VideoDetailsPanel
        {
            DataContext = _videoDetailsViewModel
        };

        RightPanelContent.Content = videoDetailsPanel;
    }

    private void SetupStatusBar()
    {
        StatusBar.DataContext = _statusBarViewModel;
    }

    // ── Keyboard Shortcuts ──

    /// <summary>
    /// Registers all default keyboard shortcuts and hooks PreviewKeyDown.
    /// </summary>
    private void SetupKeyboardShortcuts()
    {
        // Playback
        _keyboardShortcutService.Register(
            new KeyBinding("Space"), "vido.playPause", () => _videoPlayerViewModel.PlayPause());
        _keyboardShortcutService.Register(
            new KeyBinding("S"), "vido.stop", () => _videoPlayerViewModel.Stop());
        _keyboardShortcutService.Register(
            new KeyBinding("M"), "vido.toggleMute", () => _videoPlayerViewModel.ToggleMute());

        // Volume
        _keyboardShortcutService.Register(
            new KeyBinding("Up"), "vido.volumeUp", () =>
            {
                _videoPlayerViewModel.Volume = Math.Min(100, _videoPlayerViewModel.Volume + 5);
            });
        _keyboardShortcutService.Register(
            new KeyBinding("Down"), "vido.volumeDown", () =>
            {
                _videoPlayerViewModel.Volume = Math.Max(0, _videoPlayerViewModel.Volume - 5);
            });

        // Navigation
        _keyboardShortcutService.Register(
            new KeyBinding("PageUp"), "vido.skipPrevious", () => SafeFireAndForget(_videoPlayerViewModel.SkipPrevious()));
        _keyboardShortcutService.Register(
            new KeyBinding("PageDown"), "vido.skipNext", () => SafeFireAndForget(_videoPlayerViewModel.SkipNext()));

        // Panels & layout
        _keyboardShortcutService.Register(
            new KeyBinding("B", ctrl: true), "vido.toggleSidebar", ToggleSidebar);
        _keyboardShortcutService.Register(
            new KeyBinding("J", ctrl: true), "vido.toggleBottomPanel", () => _mainWindowViewModel.ToggleBottomPanel());
        _keyboardShortcutService.Register(
            new KeyBinding("H", ctrl: true), "vido.toggleRightPanel", () => _mainWindowViewModel.ToggleRightPanel());

        _keyboardShortcutService.Register(
            new KeyBinding("S", ctrl: true, shift: true), "vido.toggleStatusBar", () => _mainWindowViewModel.ToggleStatusBar());

        // File operations
        _keyboardShortcutService.Register(
            new KeyBinding("O", ctrl: true), "vido.openFile", ShowOpenFileDialog);
        _keyboardShortcutService.Register(
            new KeyBinding("O", ctrl: true, shift: true), "vido.openFolder", ShowOpenFolderDialog);
        _keyboardShortcutService.Register(
            new KeyBinding("K", ctrl: true), "vido.closeFolder", () =>
            {
                if (_fileExplorerViewModel.HasFolderOpen)
                    OnFolderClosed();
            });
        _keyboardShortcutService.Register(
            new KeyBinding("R", ctrl: true, shift: true), "vido.rescanFolder", () =>
            {
                if (_fileExplorerViewModel.HasFolderOpen)
                    OnFolderRescanned();
            });

        // Fullscreen
        _keyboardShortcutService.Register(
            new KeyBinding("F11"), "vido.toggleFullscreen", ToggleFullscreen);
        _keyboardShortcutService.Register(
            new KeyBinding("F"), "vido.toggleFullscreenF", ToggleFullscreen);
        _keyboardShortcutService.Register(
            new KeyBinding("Escape"), "vido.exitFullscreen", () =>
            {
                if (_isFullscreen) ExitFullscreen();
            });

        // Wire PreviewKeyDown
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// Routes keyboard input through the shortcut service.
    /// Suppresses shortcuts when focus is inside a text input control.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't intercept shortcuts when the user is typing in a text input
        if (IsTextInputFocused())
            return;

        // Don't intercept system keys like Alt+F4 (they arrive as Key.System)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Map WPF Key to our string representation
        var keyString = MapWpfKey(key);
        if (keyString is null) return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        var binding = new KeyBinding(keyString, ctrl, shift, alt);

        if (_keyboardShortcutService.TryExecute(binding))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Checks if the currently focused element is a text input control.
    /// Shortcuts should not fire when the user is typing.
    /// </summary>
    private static bool IsTextInputFocused()
    {
        var focused = System.Windows.Input.Keyboard.FocusedElement;
        return focused is TextBox or System.Windows.Controls.Primitives.TextBoxBase;
    }

    /// <summary>
    /// Safely fires and forgets an async task, logging any exceptions.
    /// Avoids unobserved async void delegates.
    /// </summary>
    private async void SafeFireAndForget(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            _logService.Error($"Async shortcut handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps a WPF <see cref=\"Key\"/> to the string representation used by <see cref=\"KeyBinding\"/>.
    /// Returns null for keys we don't handle.
    /// </summary>
    private static string? MapWpfKey(Key key)
    {
        return key switch
        {
            // Letters
            >= Key.A and <= Key.Z => key.ToString(),

            // Function keys
            Key.F1 => "F1",
            Key.F2 => "F2",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F6 => "F6",
            Key.F7 => "F7",
            Key.F8 => "F8",
            Key.F9 => "F9",
            Key.F10 => "F10",
            Key.F11 => "F11",
            Key.F12 => "F12",

            // Navigation
            Key.Space => "Space",
            Key.Escape => "Escape",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Home => "Home",
            Key.End => "End",

            // Special
            Key.OemPlus => "=",
            Key.OemMinus => "-",
            Key.Add => "=",
            Key.Subtract => "-",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",

            _ => null
        };
    }

    /// <summary>
    /// Toggles sidebar visibility via the activity bar view model.
    /// </summary>
    private void ToggleSidebar()
    {
        if (_activityBarViewModel is null) return;
        _activityBarViewModel.IsSidebarVisible = !_activityBarViewModel.IsSidebarVisible;
        OnPanelChanged(this, new RoutedEventArgs());
    }

    // ── Fullscreen ──

    /// <summary>
    /// Toggles fullscreen mode on/off.
    /// </summary>
    private void ToggleFullscreen()
    {
        if (_isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    /// <summary>
    /// Enters fullscreen mode. Saves current UI state, hides all chrome,
    /// and shows the video filling the entire screen with overlay controls.
    /// </summary>
    private void EnterFullscreen()
    {
        if (_isFullscreen) return;
        _isFullscreen = true;
        _mainWindowViewModel.IsFullscreen = true;

        // Save pre-fullscreen window geometry
        _preFullscreenWindowState = WindowState;
        if (WindowState == WindowState.Normal)
        {
            _preFullscreenLeft = Left;
            _preFullscreenTop = Top;
            _preFullscreenWidth = Width;
            _preFullscreenHeight = Height;
        }
        else
        {
            var bounds = RestoreBounds;
            _preFullscreenLeft = bounds.Left;
            _preFullscreenTop = bounds.Top;
            _preFullscreenWidth = bounds.Width;
            _preFullscreenHeight = bounds.Height;
        }
        _preFullscreenBorderThickness = WindowBorder.BorderThickness;

        // Save pre-fullscreen UI visibility
        _preFullscreenSidebarVisible = _activityBarViewModel?.IsSidebarVisible ?? false;
        _preFullscreenBottomPanelVisible = _mainWindowViewModel.IsBottomPanelVisible;
        _preFullscreenBottomPanelCollapsed = _mainWindowViewModel.IsBottomPanelCollapsed;
        _preFullscreenRightPanelVisible = _mainWindowViewModel.IsRightPanelVisible;
        _preFullscreenRightPanelCollapsed = _mainWindowViewModel.IsRightPanelCollapsed;
        _preFullscreenStatusBarVisible = _mainWindowViewModel.IsStatusBarVisible;

        // Hide all UI chrome
        TitleBar.Visibility = Visibility.Collapsed;
        TitleBarDivider.Visibility = Visibility.Collapsed;
        TitleBarRow.Height = new GridLength(0);
        ActivityBar.Visibility = Visibility.Collapsed;
        ActivityBarDivider.Visibility = Visibility.Collapsed;
        TabWell.Visibility = Visibility.Collapsed;
        StatusBarRow.Height = new GridLength(0);
        WindowBorder.BorderThickness = new Thickness(0);

        // Set all backgrounds to black for cinema-style fullscreen
        Background = System.Windows.Media.Brushes.Black;
        EditorAreaGrid.Background = System.Windows.Media.Brushes.Black;

        // Hide sidebar
        Sidebar.Visibility = Visibility.Collapsed;
        SidebarSplitter.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);
        SidebarColumn.MinWidth = 0;
        SidebarColumn.MaxWidth = 0;

        // Hide bottom panel, right panel, and status bar via VM properties
        // Suppress settings save — these are transient fullscreen changes, not user preferences
        _mainWindowViewModel.SuppressSettingsSave = true;
        _mainWindowViewModel.IsBottomPanelVisible = false;
        _mainWindowViewModel.IsRightPanelVisible = false;
        _mainWindowViewModel.IsStatusBarVisible = false;
        _mainWindowViewModel.SuppressSettingsSave = false;

        // Force video tab active — fullscreen should always show the video player
        _preFullscreenActiveTabId = _mainWindowViewModel.ActiveTab?.Id;
        if (_preFullscreenActiveTabId != MainWindowViewModel.PlayerTabId)
        {
            _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
            UpdateTabContent();
        }

        // Switch to fullscreen overlay mode for controls
        VideoPlayer.EnterFullscreenOverlay();

        // Remove WindowChrome caption so there's no drag area
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome is not null)
        {
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(0);
        }

        // Go to maximized (covers entire screen with taskbar hidden due to WindowStyle.None)
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal; // Reset first so WPF reapplies
        WindowState = WindowState.Maximized;

        // Setup auto-hide timer for controls
        SetupFullscreenAutoHide();

        // Wire mouse move for control visibility
        MouseMove += OnFullscreenMouseMove;

        _logService.Info("Entered fullscreen mode", "App");
    }

    /// <summary>
    /// Exits fullscreen mode. Restores all UI chrome and window geometry
    /// to their pre-fullscreen state.
    /// </summary>
    private void ExitFullscreen()
    {
        if (!_isFullscreen) return;
        _isFullscreen = false;
        _mainWindowViewModel.IsFullscreen = false;

        // Stop auto-hide timer and unwire mouse
        _fullscreenHideTimer?.Stop();
        MouseMove -= OnFullscreenMouseMove;

        // Ensure controls are visible and cursor is shown
        ShowFullscreenControls(animate: false);
        Mouse.OverrideCursor = null;

        // Restore WindowChrome
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome is not null)
        {
            chrome.CaptionHeight = 30;
            chrome.ResizeBorderThickness = new Thickness(6);
        }

        // Restore controls to normal mode
        VideoPlayer.ExitFullscreenOverlay();

        // Restore UI chrome
        TitleBar.Visibility = Visibility.Visible;
        TitleBarDivider.Visibility = Visibility.Visible;
        TitleBarRow.Height = new GridLength(30);
        ActivityBar.Visibility = Visibility.Visible;
        ActivityBarDivider.Visibility = Visibility.Visible;
        TabWell.Visibility = Visibility.Visible;
        StatusBarRow.Height = GridLength.Auto;
        WindowBorder.BorderThickness = _preFullscreenBorderThickness;

        // Restore backgrounds from fullscreen black
        SetResourceReference(BackgroundProperty, "EditorBackgroundBrush");
        EditorAreaGrid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "EditorBackgroundBrush");

        // Restore active tab if it was changed for fullscreen
        if (_preFullscreenActiveTabId is not null
            && _preFullscreenActiveTabId != MainWindowViewModel.PlayerTabId)
        {
            _mainWindowViewModel.ActivateTab(_preFullscreenActiveTabId);
            UpdateTabContent();
        }

        // Restore sidebar
        if (_activityBarViewModel is not null)
        {
            _activityBarViewModel.IsSidebarVisible = _preFullscreenSidebarVisible;
            OnPanelChanged(this, new RoutedEventArgs());
            ActivityBar.UpdateActiveStates();
        }

        // Restore panels — suppress save since we're restoring to the already-persisted state
        _mainWindowViewModel.SuppressSettingsSave = true;
        _mainWindowViewModel.IsBottomPanelVisible = _preFullscreenBottomPanelVisible;
        _mainWindowViewModel.IsBottomPanelCollapsed = _preFullscreenBottomPanelCollapsed;
        _mainWindowViewModel.IsRightPanelVisible = _preFullscreenRightPanelVisible;
        _mainWindowViewModel.IsRightPanelCollapsed = _preFullscreenRightPanelCollapsed;
        _mainWindowViewModel.IsStatusBarVisible = _preFullscreenStatusBarVisible;
        _mainWindowViewModel.SuppressSettingsSave = false;

        // Restore window geometry
        WindowState = _preFullscreenWindowState;
        if (_preFullscreenWindowState == WindowState.Normal)
        {
            Left = _preFullscreenLeft;
            Top = _preFullscreenTop;
            Width = _preFullscreenWidth;
            Height = _preFullscreenHeight;
        }

        _logService.Info("Exited fullscreen mode", "App");
    }

    /// <summary>
    /// Sets up the timer that auto-hides controls and cursor after inactivity.
    /// </summary>
    private void SetupFullscreenAutoHide()
    {
        if (_fullscreenHideTimer is null)
        {
            _fullscreenHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(FullscreenHideDelayMs)
            };
            _fullscreenHideTimer.Tick += (_, _) =>
            {
                if (_isFullscreen)
                    HideFullscreenControls();
            };
        }

        _fullscreenHideTimer.Start();
        _controlsVisible = true;
    }

    /// <summary>
    /// Handles mouse movement during fullscreen — shows controls and resets hide timer.
    /// </summary>
    private void OnFullscreenMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isFullscreen) return;

        if (!_controlsVisible)
            ShowFullscreenControls(animate: true);

        // Reset the auto-hide timer
        _fullscreenHideTimer?.Stop();
        _fullscreenHideTimer?.Start();
    }

    /// <summary>
    /// Fades in the controls overlay and shows the mouse cursor.
    /// </summary>
    private void ShowFullscreenControls(bool animate)
    {
        if (_controlsVisible && animate) return;
        _controlsVisible = true;

        Mouse.OverrideCursor = null;

        var overlay = VideoPlayer.ControlsOverlayElement;
        if (animate)
        {
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(ControlsFadeDurationMs));
            overlay.BeginAnimation(OpacityProperty, fadeIn);
        }
        else
        {
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.Opacity = 1.0;
        }
        overlay.IsHitTestVisible = true;
    }

    /// <summary>
    /// Fades out the controls overlay and hides the mouse cursor.
    /// </summary>
    private void HideFullscreenControls()
    {
        if (!_controlsVisible) return;
        _controlsVisible = false;

        _fullscreenHideTimer?.Stop();

        var overlay = VideoPlayer.ControlsOverlayElement;
        var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(ControlsFadeDurationMs));
        fadeOut.Completed += (_, _) =>
        {
            if (!_controlsVisible)
            {
                overlay.IsHitTestVisible = false;
                Mouse.OverrideCursor = Cursors.None;
            }
        };
        overlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// Shows or hides the status bar based on the ViewModel state.
    /// </summary>
    private void UpdateStatusBarVisibility()
    {
        StatusBar.Visibility = _mainWindowViewModel.IsStatusBarVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>Creates a placeholder panel for tabs without real content yet.</summary>
    private static Border CreatePlaceholderPanel(string tabTitle)
    {
        var border = new Border { Padding = new Thickness(12, 8, 12, 8) };
        var text = new TextBlock
        {
            Text = $"{tabTitle} will be available in a future update.",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DisabledForegroundBrush");
        border.Child = text;
        return border;
    }

    /// <summary>
    /// Switches the bottom panel content to match the active bottom panel tab.
    /// </summary>
    private void UpdateBottomPanelContent()
    {
        var activeTab = _mainWindowViewModel.ActiveBottomPanelTab;
        if (activeTab is null) return;

        if (!_bottomPanelContents.TryGetValue(activeTab.Id, out var content))
        {
            // Create placeholder for tabs without real implementations
            content = CreatePlaceholderPanel(activeTab.Title);
            _bottomPanelContents[activeTab.Id] = content;
        }

        BottomPanelContent.Content = content;
    }

    /// <summary>Bottom panel tab click handler — activates the clicked tab.</summary>
    private void OnBottomPanelTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BottomPanelTabItem tab)
        {
            _mainWindowViewModel.ActivateBottomPanelTab(tab.Id);
        }
    }

    /// <summary>Bottom panel collapse/expand chevron click handler.</summary>
    private void OnBottomPanelCollapseClick(object sender, RoutedEventArgs e)
    {
        _mainWindowViewModel.ToggleBottomPanelCollapse();
    }

    /// <summary>Right panel collapse/expand chevron click handler.</summary>
    private void OnRightPanelCollapseClick(object sender, RoutedEventArgs e)
    {
        _mainWindowViewModel.ToggleRightPanelCollapse();
    }

    private void SetupTabSystem()
    {
        TabWell.DataContext = _mainWindowViewModel;
        BottomPanel.DataContext = _mainWindowViewModel;
        RightPanel.DataContext = _mainWindowViewModel;

        // Listen for active tab changes to switch content
        _mainWindowViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab))
                UpdateTabContent();
            else if (e.PropertyName == nameof(MainWindowViewModel.IsBottomPanelVisible))
                UpdateBottomPanelVisibility();
            else if (e.PropertyName == nameof(MainWindowViewModel.IsBottomPanelCollapsed))
                UpdateBottomPanelVisibility();
            else if (e.PropertyName == nameof(MainWindowViewModel.IsRightPanelVisible))
                UpdateRightPanelVisibility();
            else if (e.PropertyName == nameof(MainWindowViewModel.IsRightPanelCollapsed))
                UpdateRightPanelVisibility();
            else if (e.PropertyName == nameof(MainWindowViewModel.IsStatusBarVisible))
                UpdateStatusBarVisibility();
        };

        // Apply initial panel states (both panels start visible+collapsed)
        UpdateBottomPanelVisibility();
        UpdateRightPanelVisibility();
        UpdateStatusBarVisibility();
    }

    private void SetupFileExplorer()
    {
        _fileExplorerPanel = new FileExplorerPanel
        {
            DataContext = _fileExplorerViewModel
        };

        // Wire title bar folder events
        TitleBar.FileOpened += OnFileOpened;
        TitleBar.FolderOpened += OnFolderOpened;
        TitleBar.FolderClosed += OnFolderClosed;
        TitleBar.FolderRescanned += OnFolderRescanned;

        // Wire title bar Add File / Add Folder events
        TitleBar.FilesAdded += OnFilesAddedFromMenu;
        TitleBar.FolderAddedToExplorer += OnFilesAddedFromMenu;

        // Wire View menu panel toggles
        TitleBar.ToggleBottomPanelRequested += () => _mainWindowViewModel.ToggleBottomPanel();
        TitleBar.ToggleRightPanelRequested += () => _mainWindowViewModel.ToggleRightPanel();
        TitleBar.ShowOutputRequested += () => _mainWindowViewModel.ActivateBottomPanelTab(MainWindowViewModel.OutputTabId);
        TitleBar.ShowVideoInfoRequested += () => SwitchRightPanel("vido.videoInfo");
        TitleBar.ToggleStatusBarRequested += () => _mainWindowViewModel.ToggleStatusBar();
        TitleBar.ToggleSidebarRequested += ToggleSidebar;

        // Wire fullscreen menu event
        TitleBar.FullscreenRequested += ToggleFullscreen;

        // Wire Help menu events
        TitleBar.AboutRequested += ShowAboutDialog;
        TitleBar.CheckForUpdatesRequested += ShowCheckForUpdatesMessage;
        TitleBar.EnterRepositoryCodeRequested += OnEnterRepositoryCode;

        // Wire screenshot button
        TitleBar.ScreenshotRequested += OnScreenshotRequested;
        TitleBar.SetScreenshotButtonVisible(_settingsService.Current.ScreenshotEnabled);

        // Wire recent files
        TitleBar.GetRecentFiles = () => _stateService.Current.RecentFiles;
        TitleBar.RecentFileSelected += path => SafeFireAndForget(OnRecentFileSelected(path));
        TitleBar.ClearWatchHistoryRequested += OnClearWatchHistory;

        // Wire show hidden files
        TitleBar.GetShowHiddenFiles = () => _fileExplorerViewModel.ShowHiddenFiles;
        TitleBar.ToggleShowHiddenFilesRequested += () => SafeFireAndForget(_fileExplorerViewModel.ToggleShowHiddenFilesAsync());

        // Wire status bar item and bottom panel tab show/hide toggles
        TitleBar.ToggleStatusBarItemRequested += (registrationId, visible) =>
        {
            var item = _statusBarViewModel.FindItem(registrationId);
            if (item is not null) item.IsVisible = visible;
        };
        TitleBar.ToggleBottomPanelTabRequested += (tabId, visible) =>
        {
            if (visible)
                _mainWindowViewModel.OpenBottomPanelTab(tabId, null);
            else
                _mainWindowViewModel.CloseBottomPanelTab(tabId);
        };

        // Wire playback menu events
        TitleBar.PlayPauseRequested += () => _videoPlayerViewModel.PlayPause();
        TitleBar.StopRequested += () => _videoPlayerViewModel.Stop();
        TitleBar.SkipForwardRequested += () => SafeFireAndForget(_videoPlayerViewModel.SkipNext());
        TitleBar.SkipBackwardRequested += () => SafeFireAndForget(_videoPlayerViewModel.SkipPrevious());
        TitleBar.LoopRequested += () => _videoPlayerViewModel.ToggleLoop();
        TitleBar.PlaybackSpeedSelected += speed => _videoPlayerViewModel.SetPlaybackSpeed(speed);
        TitleBar.GetPlaybackSpeed = () => _videoPlayerViewModel.PlaybackSpeed;

        // Sync panel visibility state to title bar for dynamic menu text
        _mainWindowViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsBottomPanelVisible))
                TitleBar.SetBottomPanelVisible(_mainWindowViewModel.IsBottomPanelVisible);
            else if (e.PropertyName == nameof(MainWindowViewModel.IsRightPanelVisible))
                TitleBar.SetRightPanelVisible(_mainWindowViewModel.IsRightPanelVisible);
            else if (e.PropertyName == nameof(MainWindowViewModel.IsStatusBarVisible))
                TitleBar.SetStatusBarVisible(_mainWindowViewModel.IsStatusBarVisible);
        };

        // Sync sidebar visibility to title bar
        if (_activityBarViewModel is not null)
        {
            _activityBarViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ActivityBarViewModel.IsSidebarVisible))
                    TitleBar.SetSidebarVisible(_activityBarViewModel.IsSidebarVisible);
            };
            TitleBar.SetSidebarVisible(_activityBarViewModel.IsSidebarVisible);
        }

        // Initialize title bar panel visibility state
        TitleBar.SetBottomPanelVisible(_mainWindowViewModel.IsBottomPanelVisible);
        TitleBar.SetRightPanelVisible(_mainWindowViewModel.IsRightPanelVisible);
        TitleBar.SetStatusBarVisible(_mainWindowViewModel.IsStatusBarVisible);

        // Wire the "Open Folder" button inside the explorer panel
        _fileExplorerPanel.OpenFolderRequested += ShowOpenFolderDialog;

        // Wire play file from context menu
        _fileExplorerPanel.PlayFileRequested += OnPlayFileRequested;

        // Wire double-click on video file to play
        _fileExplorerPanel.VideoFileDoubleClicked += OnPlayFileRequested;

        // Wire file handler for plugin-registered extensions
        _fileExplorerPanel.FileHandlerRequested += OnFileHandlerRequested;

        // Inject context menu registry for plugin-contributed menu items
        _fileExplorerPanel.ContextMenuRegistry = _contextMenuRegistry;

        // Inject contribution registry for plugin file icons
        _fileExplorerPanel.ContributionRegistry = _contributionRegistry;

        // Subscribe to VM changes to update Close Folder menu state
        _fileExplorerViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FileExplorerViewModel.HasFolderOpen))
                TitleBar.SetCloseFolderEnabled(_fileExplorerViewModel.HasFolderOpen);
        };

        // Set the explorer panel as the initial sidebar content
        Sidebar.SetPanelContent(_fileExplorerPanel);

        // Restore last opened folder from state
        SafeFireAndForget(RestoreLastFolderAndSyncAsync());
    }

    /// <summary>
    /// Restores the last opened folder asynchronously, then syncs dependent UI state.
    /// </summary>
    private async Task RestoreLastFolderAndSyncAsync()
    {
        await _fileExplorerViewModel.RestoreLastFolderAsync();
        TitleBar.SetCloseFolderEnabled(_fileExplorerViewModel.HasFolderOpen);

        // Sync explorer root to video player for skip prev/next across all folders
        if (_fileExplorerViewModel.FolderPath is not null)
            await _videoPlayerViewModel.SetExplorerRootAsync(_fileExplorerViewModel.FolderPath);
    }

    private void ShowOpenFolderDialog()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Open Folder"
        };

        if (dialog.ShowDialog(this) == true && !string.IsNullOrEmpty(dialog.FolderName))
        {
            OnFolderOpened(dialog.FolderName);
        }
    }

    /// <summary>
    /// Shows the Open File dialog (Ctrl+O).
    /// </summary>
    private void ShowOpenFileDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Video File",
            Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm|All Files|*.*"
        };

        if (dialog.ShowDialog(this) == true && !string.IsNullOrEmpty(dialog.FileName))
        {
            OnFileOpened(dialog.FileName);
        }
    }

    /// <summary>
    /// Opens and plays a video file. If the file's parent folder differs from
    /// the current explorer folder, the parent folder is opened in the explorer.
    /// </summary>
    private async void OnFileOpened(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logService.Error($"File not found: {filePath}", "App");
            return;
        }

        // Open the file's parent directory if different from the current explorer folder
        var parentDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            var currentFolder = _fileExplorerViewModel.FolderPath;
            if (currentFolder is null ||
                !string.Equals(currentFolder, parentDir, StringComparison.OrdinalIgnoreCase))
            {
                OnFolderOpened(parentDir);
            }
        }

        // Switch to Player tab and load the video
        _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
        await _videoPlayerViewModel.LoadAndPlayAsync(filePath);
    }

    private async void OnFolderOpened(string path)
    {
        await _fileExplorerViewModel.OpenFolderAsync(path);
        await _videoPlayerViewModel.SetExplorerRootAsync(path);

        // Ensure sidebar is visible and Explorer panel is active
        if (_activityBarViewModel is not null)
        {
            _activityBarViewModel.ActivePanel = SidebarPanelKind.Explorer;
            _activityBarViewModel.IsSidebarVisible = true;
            OnPanelChanged(this, new RoutedEventArgs());
        }
    }

    private async void OnFolderClosed()
    {
        _fileExplorerViewModel.CloseFolder();
        await _videoPlayerViewModel.SetExplorerRootAsync(null);
    }

    private async void OnFolderRescanned()
    {
        await _fileExplorerViewModel.RescanFolderAsync();
        _logService.Info("Folder rescanned", "Explorer");
    }

    /// <summary>
    /// Handles files or folders added via File > Add File… / Add Folder… menu items.
    /// Works the same as dropping files on the explorer panel (additive insert).
    /// </summary>
    private void OnFilesAddedFromMenu(string[] paths)
    {
        var hasUnsupported = _fileExplorerViewModel.AddItems(paths);
        if (hasUnsupported)
            ShowUnsupportedFileNotification();

        EnsureExplorerVisible();
    }

    private async Task OnRecentFileSelected(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logService.Error($"Recent file not found: {filePath}", "App");
            return;
        }

        _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
        await _videoPlayerViewModel.LoadAndPlayAsync(filePath);
    }

    private void OnClearWatchHistory()
    {
        _stateService.Current.RecentFiles.Clear();
        _stateService.QueueSave();
    }

    private async void OnPlayFileRequested(Core.FileSystem.FileNode node)
    {
        if (!node.IsVideoFile || node.IsHidden) return;

        try
        {
            // Ensure the Player tab is active when playing a video
            _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
            await _videoPlayerViewModel.LoadAndPlayAsync(node.FullPath);
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to play {node.Name}: {ex.Message}", "Player");
        }
    }

    /// <summary>
    /// Handles double-click on a non-video file. Checks if any plugin has registered
    /// a file handler for the file's extension and invokes it.
    /// </summary>
    private void OnFileHandlerRequested(FileNode node)
    {
        var ext = Path.GetExtension(node.Name);
        if (string.IsNullOrEmpty(ext)) return;

        if (_pluginFileHandlers.TryGetValue(ext, out var handler))
        {
            try
            {
                handler(node);
            }
            catch (Exception ex)
            {
                _logService.Error($"Plugin file handler error for '{node.Name}': {ex.Message}", "PluginHost");
            }
        }
    }

    // ── Tab content switching ──

    /// <summary>
    /// Switches the visible content area based on the active tab.
    /// The VideoPlayerControl is always present but hidden when another tab is active.
    /// Settings and other tabs use the DynamicTabContent presenter.
    /// </summary>
    private void UpdateTabContent()
    {
        var activeTab = _mainWindowViewModel.ActiveTab;
        if (activeTab is null) return;

        if (activeTab.Id == MainWindowViewModel.PlayerTabId)
        {
            // Show the video player, hide dynamic content
            VideoPlayer.Visibility = Visibility.Visible;
            DynamicTabContent.Visibility = Visibility.Collapsed;
        }
        else if (activeTab.Id == MainWindowViewModel.SettingsTabId)
        {
            // Show settings page (cached to preserve state across tab switches)
            VideoPlayer.Visibility = Visibility.Collapsed;
            if (_settingsPage is null)
            {
                _settingsPage = new SettingsPage(_settingsService, _pluginHost, _appSettingsStore);
            }
            DynamicTabContent.Content = _settingsPage;
            DynamicTabContent.Visibility = Visibility.Visible;
        }
        else if (activeTab.Id.StartsWith("plugin.detail.", StringComparison.Ordinal))
        {
            // Show plugin detail panel
            VideoPlayer.Visibility = Visibility.Collapsed;
            var pluginId = activeTab.Id["plugin.detail.".Length..];
            var panel = GetOrCreatePluginDetailPanel(pluginId);
            DynamicTabContent.Content = panel;
            DynamicTabContent.Visibility = Visibility.Visible;
        }
        else
        {
            // Future tabs — show empty for now
            VideoPlayer.Visibility = Visibility.Collapsed;
            DynamicTabContent.Content = null;
            DynamicTabContent.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Gets or creates a PluginDetailPanel for the given plugin ID.
    /// </summary>
    private PluginDetailPanel GetOrCreatePluginDetailPanel(string pluginId)
    {
        // Use an already-prepared panel if available (set by Open*Requested handlers)
        if (_pluginDetailPanels.TryGetValue(pluginId, out var existing))
            return existing;

        // Fallback: look up the item from the manager VM (e.g. tab switching)
        PluginItemViewModel? item = null;
        if (_pluginManagerViewModel is not null)
        {
            item = _pluginManagerViewModel.InstalledPlugins.FirstOrDefault(p => p.Id == pluginId)
                ?? _pluginManagerViewModel.AvailablePlugins.FirstOrDefault(p => p.Id == pluginId);
        }

        var panel = new PluginDetailPanel(item, _pluginManagerViewModel, _pluginHost, _logService);
        _pluginDetailPanels[pluginId] = panel;
        return panel;
    }

    // ── Panel visibility ──

    private void UpdateBottomPanelVisibility()
    {
        if (_mainWindowViewModel.IsBottomPanelVisible)
        {
            BottomPanel.Visibility = Visibility.Visible;

            if (_mainWindowViewModel.IsBottomPanelCollapsed)
            {
                // Collapsed: show only the tab strip bar (no splitter, fixed height)
                // Remember current height before collapsing
                if (BottomPanelRow.Height.Value > 40)
                    _bottomPanelHeight = BottomPanelRow.Height.Value;

                BottomPanelSplitter.Visibility = Visibility.Collapsed;
                BottomPanelSplitterRow.Height = new GridLength(0);
                BottomPanelRow.Height = new GridLength(29); // Tab strip height only
                BottomPanelContent.Visibility = Visibility.Collapsed;
                BottomPanelTabStrip.Visibility = Visibility.Collapsed;
                BottomPanel.BorderThickness = new Thickness(0, 1, 0, 0);
            }
            else
            {
                // Expanded: full panel with splitter
                BottomPanelSplitter.Visibility = Visibility.Visible;
                BottomPanelSplitterRow.Height = GridLength.Auto;
                BottomPanelRow.Height = new GridLength(_bottomPanelHeight);
                BottomPanelContent.Visibility = Visibility.Visible;
                BottomPanelTabStrip.Visibility = Visibility.Visible;
                BottomPanel.BorderThickness = new Thickness(0);
            }
        }
        else
        {
            // Remember current height before hiding
            if (BottomPanelRow.Height.Value > 40)
                _bottomPanelHeight = BottomPanelRow.Height.Value;

            BottomPanel.Visibility = Visibility.Collapsed;
            BottomPanelSplitter.Visibility = Visibility.Collapsed;
            BottomPanelRow.Height = new GridLength(0);
            BottomPanelSplitterRow.Height = new GridLength(0);
        }
    }

    private void UpdateRightPanelVisibility()
    {
        if (_mainWindowViewModel.IsRightPanelVisible)
        {
            RightPanel.Visibility = Visibility.Visible;

            if (_mainWindowViewModel.IsRightPanelCollapsed)
            {
                // Collapsed: show only the tab strip bar (no splitter, fixed width)
                if (RightPanelColumn.Width.Value > 50)
                    _rightPanelWidth = RightPanelColumn.Width.Value;

                RightPanelSplitter.Visibility = Visibility.Collapsed;
                RightPanelSplitterColumn.Width = new GridLength(0);
                RightPanelColumn.Width = new GridLength(29); // Match bottom panel collapsed height
                RightPanelColumn.MinWidth = 0;
                RightPanelColumn.MaxWidth = double.PositiveInfinity;
                RightPanelContent.Visibility = Visibility.Collapsed;
                RightPanelTitle.Visibility = Visibility.Collapsed;
                RightPanel.BorderThickness = new Thickness(1, 0, 0, 0);

                // Single column so chevron centers in full width
                RightPanelTabStrip.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                RightPanelTabStrip.ColumnDefinitions[1].Width = new GridLength(0);
            }
            else
            {
                // Expanded: full panel with splitter
                RightPanelSplitter.Visibility = Visibility.Visible;
                RightPanelSplitterColumn.Width = GridLength.Auto;
                RightPanelColumn.Width = new GridLength(_rightPanelWidth);
                RightPanelColumn.MinWidth = 170;
                RightPanelColumn.MaxWidth = 600;
                RightPanelContent.Visibility = Visibility.Visible;
                RightPanelTitle.Visibility = Visibility.Visible;
                RightPanel.BorderThickness = new Thickness(0);

                // Two columns: auto-sized chevron + remaining space for title
                RightPanelTabStrip.ColumnDefinitions[0].Width = GridLength.Auto;
                RightPanelTabStrip.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        else
        {
            // Remember current width before hiding
            if (RightPanelColumn.Width.Value > 40)
                _rightPanelWidth = RightPanelColumn.Width.Value;

            RightPanel.Visibility = Visibility.Collapsed;
            RightPanelSplitter.Visibility = Visibility.Collapsed;
            RightPanelColumn.Width = new GridLength(0);
            RightPanelColumn.MinWidth = 0;
            RightPanelColumn.MaxWidth = double.PositiveInfinity;
            RightPanelSplitterColumn.Width = new GridLength(0);
        }
    }

    private void OnPanelChanged(object sender, RoutedEventArgs e)
    {
        if (_activityBarViewModel is null || _sidebarViewModel is null)
            return;

        // Clear plugin sidebar state when a built-in panel is selected
        _activePluginSidebarId = null;
        UpdatePluginSidebarButtonStates();

        // Update sidebar visibility
        if (_activityBarViewModel.IsSidebarVisible)
        {
            Sidebar.Visibility = Visibility.Visible;
            SidebarSplitter.Visibility = Visibility.Visible;
            SidebarColumn.Width = new GridLength(_settingsService.Current.SidebarWidth);
            SidebarColumn.MinWidth = 170;
            SidebarColumn.MaxWidth = 600;
            _sidebarViewModel.SetPanel(_activityBarViewModel.ActivePanel);

            // Switch panel content based on active panel
            switch (_activityBarViewModel.ActivePanel)
            {
                case SidebarPanelKind.Explorer:
                    Sidebar.SetPanelContent(_fileExplorerPanel);
                    break;
                case SidebarPanelKind.Extensions:
                    if (_pluginManagerPanel is null)
                    {
                        _pluginManagerPanel = new PluginManagerPanel
                        {
                            DataContext = _pluginManagerViewModel
                        };
                        WirePluginManagerEvents();
                    }
                    Sidebar.SetPanelContent(_pluginManagerPanel);
                    // Load plugin data when switching to extensions
                    _ = _pluginManagerViewModel?.LoadAsync();
                    break;
                default:
                    Sidebar.SetPanelContent(null);
                    break;
            }
        }
        else
        {
            Sidebar.Visibility = Visibility.Collapsed;
            SidebarSplitter.Visibility = Visibility.Collapsed;
            SidebarColumn.Width = new GridLength(0);
            SidebarColumn.MinWidth = 0;
            SidebarColumn.MaxWidth = 0;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        ApplyDarkDwmSurface(hwnd);
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _logService.Info("Vido shutting down", "App");
        SaveWindowState();
        await _stateService.SaveAsync();
        await _settingsService.SaveAsync();
        base.OnClosing(e);
    }

    // ── Drag and drop ──

    /// <summary>Timer for auto-hiding the unsupported file notification.</summary>
    private DispatcherTimer? _notificationTimer;

    /// <summary>
    /// Wires drag-and-drop handlers for the video player, file explorer, and main window fallback.
    /// </summary>
    private void SetupDragDrop()
    {
        // Video player area: dropped items are added to explorer + first video plays
        VideoPlayer.FilesDropped += OnFilesDroppedOnPlayer;

        // File explorer: dropped items are added additively to the tree
        if (_fileExplorerPanel is not null)
            _fileExplorerPanel.FilesDroppedOnExplorer += OnFilesDroppedOnExplorer;

        // Main window fallback: handles drops on title bar, status bar, etc.
        Drop += OnWindowDrop;
        DragEnter += OnWindowDragEnter;
        DragOver += OnWindowDragOver;
    }

    // ── Plugin Manager (Extensions sidebar) wiring ──

    /// <summary>
    /// Map of plugin ID → detail panel content for reuse (avoids re-creating panels).
    /// </summary>
    private readonly Dictionary<string, PluginDetailPanel> _pluginDetailPanels = [];

    /// <summary>
    /// Wires events from the Plugin Manager ViewModel to open detail panels and settings.
    /// </summary>
    private void WirePluginManagerEvents()
    {
        if (_pluginManagerViewModel is null) return;

        _pluginManagerViewModel.OpenDetailRequested += item =>
        {
            var tabId = $"plugin.detail.{item.Id}";
            var tabAlreadyOpen = _mainWindowViewModel.Tabs.Any(t => t.Id == tabId);

            if (tabAlreadyOpen && _pluginDetailPanels.TryGetValue(item.Id, out var existingPanel))
            {
                // Tab already open — refresh the existing panel to reflect
                // install/uninstall/update state changes (e.g. settings tab)
                existingPanel.Refresh();
                _mainWindowViewModel.OpenTab(tabId, item.DisplayName, isClosable: true);
            }
            else
            {
                // Create a new panel with the current item state
                var panel = new PluginDetailPanel(item, _pluginManagerViewModel, _pluginHost, _logService);
                _pluginDetailPanels[item.Id] = panel;
                _mainWindowViewModel.OpenTab(tabId, item.DisplayName, isClosable: true);
            }
        };

        _pluginManagerViewModel.OpenSettingsRequested += item =>
        {
            var tabId = $"plugin.detail.{item.Id}";
            var panel = new PluginDetailPanel(item, _pluginManagerViewModel, _pluginHost, _logService);
            _pluginDetailPanels[item.Id] = panel;

            _mainWindowViewModel.OpenTab(tabId, item.DisplayName, isClosable: true);
            // Scroll to settings tab
            panel.SwitchToSettings();
        };

        _pluginManagerViewModel.RestartRequired += message =>
        {
            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    this,
                    $"{message}\n\nWould you like to restart Vido now?",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Restart the application
                    var exePath = Environment.ProcessPath;
                    if (exePath is not null)
                    {
                        System.Diagnostics.Process.Start(exePath);
                        Application.Current.Shutdown();
                    }
                }
            });
        };
    }

    // ── Plugin contribution wiring ──

    /// <summary>
    /// Subscribes to contribution registry changes and wires any already-registered
    /// plugin UI contributions (bottom panel tabs, status bar items, etc.).
    /// Called during setup; plugins may be activated later, at which point
    /// the <see cref="IContributionRegistry.ContributionsChanged"/> callback
    /// picks up new contributions.
    /// </summary>
    private void SetupPluginContributions()
    {
        _contributionRegistry.ContributionsChanged += OnPluginContributionsChanged;
        _contributionRegistry.RightPanelShowRequested += OnRightPanelShowRequested;
        _contributionRegistry.BottomPanelShowRequested += OnBottomPanelShowRequested;
        _contributionRegistry.ToolbarButtonHighlightChanged += OnToolbarButtonHighlightChanged;
        _contributionRegistry.ControlBarOverlayToggled += OnControlBarOverlayToggled;
        WirePluginContributions();
    }

    /// <summary>
    /// Callback fired when plugins register or unregister UI contributions.
    /// Dispatches to the UI thread to apply changes safely.
    /// </summary>
    private void OnPluginContributionsChanged()
    {
        if (Dispatcher.CheckAccess())
            WirePluginContributions();
        else
            Dispatcher.BeginInvoke(WirePluginContributions);
    }

    /// <summary>
    /// Callback fired when a plugin sets or clears a toolbar button highlight.
    /// Updates the button's background to AccentBrush (highlighted) or Transparent (normal).
    /// </summary>
    private void OnToolbarButtonHighlightChanged(string fullButtonId, bool highlighted)
    {
        void Apply()
        {
            if (!_pluginToolbarButtons.TryGetValue(fullButtonId, out var button)) return;

            // Walk the visual tree to find the named "Bd" Border inside the template
            if (button.Template?.FindName("Bd", button) is Border bd)
            {
                bd.Background = highlighted
                    ? (Brush)FindResource("AccentBrush")
                    : Brushes.Transparent;
            }
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }

    /// <summary>
    /// Callback fired when a plugin toggles a control-bar overlay on or off.
    /// Dispatches to the UI thread to apply the visibility change.
    /// </summary>
    private void OnControlBarOverlayToggled(string fullId, bool visible)
    {
        void Apply() => VideoPlayer.SetPluginOverlayVisible(fullId, visible);

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }

    /// <summary>
    /// Applies all current plugin contributions to the UI. Idempotent — safe to call
    /// multiple times. Only adds contributions that aren't already wired.
    /// Also removes contributions that are no longer registered (e.g. after disable/uninstall).
    /// </summary>
    private void WirePluginContributions()
    {
        UnwireStaleContributions();
        WirePluginBottomPanels();
        WirePluginStatusBarItems();
        WirePluginSidebarPanels();
        WirePluginToolbarButtons();
        WirePluginRightPanels();
        WirePluginFileHandlers();
        WirePluginControlBarItems();
    }

    /// <summary>
    /// Removes UI elements for contributions that are no longer in the registry.
    /// This handles live removal when a plugin is disabled or uninstalled.
    /// </summary>
    private void UnwireStaleContributions()
    {
        // Build sets of currently registered full IDs
        var currentBottomPanels = new HashSet<string>(
            _contributionRegistry.GetBottomPanels().Select(p => $"plugin.{p.PluginId}.{p.ContributionId}"));
        var currentStatusBars = new HashSet<string>(
            _contributionRegistry.GetStatusBarItems().Select(i => $"plugin.{i.PluginId}.{i.ContributionId}"));
        var currentSidebars = new HashSet<string>(
            _contributionRegistry.GetSidebarPanels().Select(p => $"plugin.{p.PluginId}.{p.ContributionId}"));
        var currentToolbars = new HashSet<string>(
            _contributionRegistry.GetToolbarButtons().Select(b => $"plugin.{b.PluginId}.{b.ContributionId}"));
        var currentRightPanels = new HashSet<string>(
            _contributionRegistry.GetRightPanels().Select(p => $"plugin.{p.PluginId}.{p.ContributionId}"));
        var currentFileHandlers = new HashSet<string>(
            _contributionRegistry.GetFileHandlers().Select(h => $"plugin.{h.PluginId}.fileHandler"));
        var currentControlBarItems = new HashSet<string>(
            _contributionRegistry.GetControlBarItems().Select(c => $"plugin.{c.PluginId}.{c.ContributionId}"));

        // Remove stale bottom panels
        foreach (var id in _wiredBottomPanelIds.Where(id => !currentBottomPanels.Contains(id)).ToList())
        {
            _mainWindowViewModel.CloseBottomPanelTab(id);
            _bottomPanelContents.Remove(id);
            _wiredBottomPanelIds.Remove(id);
            TitleBar.RemoveBottomPanelTabMenuItem(id);
        }

        // Remove stale status bar items
        foreach (var id in _wiredStatusBarIds.Where(id => !currentStatusBars.Contains(id)).ToList())
        {
            _statusBarViewModel.UnregisterItem(id);
            _wiredStatusBarIds.Remove(id);
            TitleBar.RemoveStatusBarMenuItem(id);
        }

        // Remove stale sidebar panels
        foreach (var id in _wiredSidebarPanelIds.Where(id => !currentSidebars.Contains(id)).ToList())
        {
            if (_pluginSidebarButtons.TryGetValue(id, out var button))
            {
                ActivityBar.RemovePluginButton(button);
                _pluginSidebarButtons.Remove(id);
            }
            _pluginSidebarContents.Remove(id);
            if (_activePluginSidebarId == id)
            {
                _activePluginSidebarId = null;
                // Switch sidebar back to explorer if it was showing this plugin panel
                if (_activityBarViewModel is not null)
                {
                    _activityBarViewModel.ActivePanel = Core.Layout.SidebarPanelKind.Explorer;
                    OnPanelChanged(this, new RoutedEventArgs());
                }
            }
            _wiredSidebarPanelIds.Remove(id);
        }

        // Remove stale toolbar buttons
        foreach (var id in _wiredToolbarButtonIds.Where(id => !currentToolbars.Contains(id)).ToList())
        {
            if (_pluginToolbarButtons.TryGetValue(id, out var button))
            {
                TitleBar.RemovePluginToolbarButton(button);
                _pluginToolbarButtons.Remove(id);
            }
            _wiredToolbarButtonIds.Remove(id);
        }

        // Remove stale right panels
        foreach (var id in _wiredRightPanelIds.Where(id => !currentRightPanels.Contains(id)).ToList())
        {
            TitleBar.RemoveRightPanelMenuItem(id);
            _rightPanelContents.Remove(id);
            _rightPanelTitles.Remove(id);
            _wiredRightPanelIds.Remove(id);
        }

        // Remove stale file handlers
        foreach (var id in _wiredFileHandlerIds.Where(id => !currentFileHandlers.Contains(id)).ToList())
        {
            _wiredFileHandlerIds.Remove(id);
        }

        // Remove stale control bar items
        foreach (var id in _wiredControlBarIds.Where(id => !currentControlBarItems.Contains(id)).ToList())
        {
            VideoPlayer.RemovePluginControlBarItem(id);
            VideoPlayer.RemovePluginOverlay(id);
            _wiredControlBarIds.Remove(id);
        }
    }

    /// <summary>Tracking sets for idempotent wiring — prevent re-adding contributions.</summary>
    private readonly HashSet<string> _wiredBottomPanelIds = [];
    private readonly HashSet<string> _wiredStatusBarIds = [];
    private readonly HashSet<string> _wiredSidebarPanelIds = [];
    private readonly HashSet<string> _wiredToolbarButtonIds = [];
    private readonly HashSet<string> _wiredRightPanelIds = [];
    private readonly HashSet<string> _wiredFileHandlerIds = [];
    private readonly HashSet<string> _wiredControlBarIds = [];

    /// <summary>
    /// Wraps an object returned by a plugin's view factory into a UIElement.
    /// Plugins may return a WPF control (UIElement), a string, or any other object.
    /// Non-UIElement values are wrapped in a styled TextBlock so they display in the UI.
    /// </summary>
    private static UIElement WrapAsUIElement(object? content, string fallbackText = "")
    {
        return content switch
        {
            UIElement el => el,
            string text => CreatePluginTextBlock(text),
            null => CreatePluginTextBlock(fallbackText),
            _ => CreatePluginTextBlock(content.ToString() ?? fallbackText)
        };
    }

    /// <summary>Creates a themed TextBlock for displaying plugin text content.</summary>
    private static TextBlock CreatePluginTextBlock(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = (Brush)Application.Current.FindResource("PrimaryForegroundBrush"),
            Padding = new Thickness(12, 8, 12, 8),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    // ── Bottom Panel wiring ──

    /// <summary>
    /// Invokes a plugin wiring action inside a try/catch that logs errors in a consistent format.
    /// Prevents a single plugin contribution from crashing the whole wiring process.
    /// </summary>
    private void SafeWireContribution(string pluginId, string contributionId, string area, Action wireAction)
    {
        try
        {
            wireAction();
        }
        catch (Exception ex)
        {
            _logService.Error(
                $"Plugin '{pluginId}' {area} '{contributionId}' failed: {ex.Message}",
                "PluginHost");
        }
    }

    private void WirePluginBottomPanels()
    {
        foreach (var panel in _contributionRegistry.GetBottomPanels())
        {
            var fullId = $"plugin.{panel.PluginId}.{panel.ContributionId}";
            if (_wiredBottomPanelIds.Contains(fullId)) continue;

            SafeWireContribution(panel.PluginId, panel.ContributionId, "bottom panel", () =>
            {
                _wiredBottomPanelIds.Add(fullId);
                var view = panel.ViewFactory();
                var uiElement = WrapAsUIElement(view, $"Plugin: {panel.Title}");
                _bottomPanelContents[fullId] = uiElement;
                _mainWindowViewModel.OpenBottomPanelTab(fullId, panel.Title.ToUpperInvariant());

                // Add show/hide menu item for this tab
                TitleBar.AddBottomPanelTabMenuItem(fullId, panel.Title);
            });
        }
    }

    // ── Status Bar wiring ──

    private void WirePluginStatusBarItems()
    {
        var statusBarItems = _contributionRegistry.GetStatusBarItems();

        foreach (var item in statusBarItems)
        {
            var fullId = $"plugin.{item.PluginId}.{item.ContributionId}";
            if (_wiredStatusBarIds.Contains(fullId)) continue;

            SafeWireContribution(item.PluginId, item.ContributionId, "status bar item", () =>
            {
                var alignment = item.Position.Equals("left", StringComparison.OrdinalIgnoreCase)
                    ? Core.Layout.StatusBarAlignment.Left
                    : Core.Layout.StatusBarAlignment.Right;

                // Invoke the view factory FIRST — before registering the item.
                // If the factory throws, we must not leave a half-wired item
                // in RightItems with no content (which renders as invisible
                // zero-width and is never retried).
                object? content;
                try
                {
                    content = item.ViewFactory();
                }
                catch (Exception factoryEx)
                {
                    _logService.Error(
                        $"Status bar view factory for '{fullId}' threw: {factoryEx}", "PluginHost");
                    // Use fallback text content so the item is still visible
                    content = null;
                }

                // Use FindItem to handle retry after partial failure — if a prior
                // attempt registered the item but threw before marking it as wired,
                // reuse the existing item instead of throwing on duplicate.
                var statusBarItem = _statusBarViewModel.FindItem(fullId)
                    ?? _statusBarViewModel.RegisterItem(fullId, alignment, item.Order);

                if (content is System.Windows.FrameworkElement fe)
                {
                    // Plugin returned a custom WPF element — host it directly
                    statusBarItem.ContentView = fe;
                    statusBarItem.Text = item.Name; // fallback text
                }
                else
                {
                    // Factory returned non-UIElement or threw — show text
                    statusBarItem.Text = content switch
                    {
                        string text => text,
                        null => item.Name, // fallback to contribution name
                        _ => content.ToString() ?? item.Name
                    };
                }
                statusBarItem.Tooltip = $"Plugin: {item.PluginId}";
                statusBarItem.IsVisible = true;

                // Store reference so plugins can push text updates via UpdateStatusBarItem
                _contributionRegistry.SetStatusBarItemReference(fullId, statusBarItem);

                // Mark as wired AFTER successful registration
                _wiredStatusBarIds.Add(fullId);

                // Add show/hide menu item under Status Bar submenu
                TitleBar.AddStatusBarMenuItem(fullId, item.Name);
            });
        }
    }

    // ── Sidebar Panel wiring ──

    /// <summary>Map of plugin sidebar panel ID → content UIElement.</summary>
    private readonly Dictionary<string, UIElement> _pluginSidebarContents = [];

    /// <summary>Map of plugin sidebar panel ID → dynamically-added activity bar Button.</summary>
    private readonly Dictionary<string, Button> _pluginSidebarButtons = [];

    /// <summary>The currently active plugin sidebar panel ID, or null if none is active.</summary>
    private string? _activePluginSidebarId;

    private void WirePluginSidebarPanels()
    {
        foreach (var panel in _contributionRegistry.GetSidebarPanels())
        {
            var fullId = $"plugin.{panel.PluginId}.{panel.ContributionId}";
            if (_wiredSidebarPanelIds.Contains(fullId)) continue;

            SafeWireContribution(panel.PluginId, panel.ContributionId, "sidebar panel", () =>
            {
                _wiredSidebarPanelIds.Add(fullId);
                var view = panel.ViewFactory();
                var uiElement = WrapAsUIElement(view, $"Plugin: {panel.Title}");
                _pluginSidebarContents[fullId] = uiElement;

                // Create an activity bar button for this panel
                var panelId = fullId; // capture for closure
                var button = CreatePluginActivityBarButton(panel.Title, panel.IconPath, () =>
                {
                    OnPluginSidebarButtonClick(panelId, panel.Title);
                });
                _pluginSidebarButtons[fullId] = button;

                // Add to activity bar top section
                ActivityBar.AddPluginButton(button);
            });
        }
    }

    /// <summary>
    /// Creates a Button styled for the activity bar with a plugin icon.
    /// Uses the plugin's custom icon if <paramref name="iconPath"/> points to a valid image file,
    /// otherwise falls back to a generic puzzle-piece icon.
    /// Style is applied by ActivityBarView.AddPluginButton since it's a local resource.
    /// </summary>
    private static Button CreatePluginActivityBarButton(string tooltip, string? iconPath, Action onClick)
    {
        UIElement icon;

        if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
        {
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                icon = new Image
                {
                    Source = bitmap,
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true,
                    Opacity = 0.6
                };
            }
            catch
            {
                // Fall back to default puzzle icon on load failure
                icon = CreatePuzzlePieceIcon24();
            }
        }
        else
        {
            icon = CreatePuzzlePieceIcon24();
        }

        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Creates a 24×24 puzzle-piece Canvas icon for plugins without a custom icon.</summary>
    private static Canvas CreatePuzzlePieceIcon24()
    {
        var canvas = new Canvas { Width = 24, Height = 24 };
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 5,4 L 11,4 L 11,7 L 14,7 L 14,13 L 17,13 L 17,19 L 11,19 L 11,16 L 8,16 L 8,19 L 2,19 L 2,13 L 5,13 L 5,10 L 2,10 L 2,4 Z"),
            Stroke = (Brush)Application.Current.FindResource("InactiveIconBrush"),
            StrokeThickness = 1.2,
            Fill = Brushes.Transparent,
            StrokeLineJoin = PenLineJoin.Round
        };
        canvas.Children.Add(path);
        return canvas;
    }

    /// <summary>Handles click on a plugin sidebar panel button in the activity bar.</summary>
    private void OnPluginSidebarButtonClick(string panelId, string title)
    {
        if (_activePluginSidebarId == panelId && Sidebar.Visibility == Visibility.Visible)
        {
            // Toggle off — hide sidebar
            _activePluginSidebarId = null;
            _activityBarViewModel!.IsSidebarVisible = false;
            Sidebar.Visibility = Visibility.Collapsed;
            SidebarSplitter.Visibility = Visibility.Collapsed;
            SidebarColumn.Width = new GridLength(0);
            SidebarColumn.MinWidth = 0;
            SidebarColumn.MaxWidth = 0;
            UpdatePluginSidebarButtonStates();
            ActivityBar.UpdateActiveStates();
            return;
        }

        // Activate this plugin panel
        _activePluginSidebarId = panelId;
        _activityBarViewModel!.IsSidebarVisible = true;

        // Clear the built-in active panel to avoid conflict
        _activityBarViewModel.ClearActivePanel();

        Sidebar.Visibility = Visibility.Visible;
        SidebarSplitter.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(_settingsService.Current.SidebarWidth);
        SidebarColumn.MinWidth = 170;
        SidebarColumn.MaxWidth = 600;

        _sidebarViewModel!.SetPanel(title.ToUpperInvariant());

        if (_pluginSidebarContents.TryGetValue(panelId, out var content))
            Sidebar.SetPanelContent(content);

        UpdatePluginSidebarButtonStates();
        ActivityBar.UpdateActiveStates();
    }

    /// <summary>Updates visual active state for all plugin sidebar buttons.</summary>
    private void UpdatePluginSidebarButtonStates()
    {
        foreach (var (id, button) in _pluginSidebarButtons)
        {
            var isActive = id == _activePluginSidebarId && Sidebar.Visibility == Visibility.Visible;
            button.Tag = isActive ? "Active" : null;
            ActivityBar.SetPluginButtonActive(button, isActive);
        }
    }

    // ── Toolbar Button wiring ──

    /// <summary>Map of toolbar button ID → Button element for removal.</summary>
    private readonly Dictionary<string, Button> _pluginToolbarButtons = [];

    private void WirePluginToolbarButtons()
    {
        foreach (var toolbarBtn in _contributionRegistry.GetToolbarButtons())
        {
            var fullId = $"plugin.{toolbarBtn.PluginId}.{toolbarBtn.ContributionId}";
            if (_wiredToolbarButtonIds.Contains(fullId)) continue;

            SafeWireContribution(toolbarBtn.PluginId, toolbarBtn.ContributionId, "toolbar button", () =>
            {
                _wiredToolbarButtonIds.Add(fullId);
                var handler = toolbarBtn.ClickHandler; // capture for closure
                var button = CreatePluginToolbarButton(toolbarBtn.Tooltip, toolbarBtn.IconPath, handler);
                _pluginToolbarButtons[fullId] = button;
                TitleBar.AddPluginToolbarButton(button);
            });
        }
    }

    /// <summary>Creates a small toolbar button for the title bar with menu-matching hover highlight.</summary>
    private static Button CreatePluginToolbarButton(string tooltip, string? iconPath, Action clickHandler)
    {
        UIElement icon;

        if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
        {
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                icon = new Image
                {
                    Source = bitmap,
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true
                };
            }
            catch
            {
                // Fall back to default puzzle icon on load failure
                icon = CreatePuzzlePieceIcon16();
            }
        }
        else
        {
            icon = CreatePuzzlePieceIcon16();
        }

        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip,
            Height = 22,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2)
        };

        // Build a template — button stretches to fill container, highlight fills entire space
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;

        // Hover trigger matching TitleBarMenuItemStyle
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new DynamicResourceExtension("HoverBackgroundBrush"), "Bd"));
        template.Triggers.Add(hoverTrigger);

        button.Template = template;

        button.Click += (_, _) =>
        {
            try { clickHandler(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Plugin toolbar button handler error: {ex.Message}"); }
        };

        return button;
    }

    /// <summary>Creates a 16×16 puzzle-piece Canvas icon for plugins without a custom icon.</summary>
    private static Canvas CreatePuzzlePieceIcon16()
    {
        var canvas = new Canvas { Width = 16, Height = 16 };
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 3,2 L 7,2 L 7,4 L 9,4 L 9,8 L 11,8 L 11,12 L 7,12 L 7,10 L 5,10 L 5,12 L 1,12 L 1,8 L 3,8 L 3,6 L 1,6 L 1,2 Z"),
            Stroke = (Brush)Application.Current.FindResource("PrimaryForegroundBrush"),
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent,
            StrokeLineJoin = PenLineJoin.Round
        };
        canvas.Children.Add(path);
        return canvas;
    }

    // ── Right Panel wiring ──

    /// <summary>Map of right panel ID → content UIElement.</summary>
    private readonly Dictionary<string, UIElement> _rightPanelContents = [];

    /// <summary>Map of right panel ID → display title.</summary>
    private readonly Dictionary<string, string> _rightPanelTitles = [];

    private void WirePluginRightPanels()
    {
        foreach (var panel in _contributionRegistry.GetRightPanels())
        {
            var fullId = $"plugin.{panel.PluginId}.{panel.ContributionId}";
            if (_wiredRightPanelIds.Contains(fullId)) continue;

            SafeWireContribution(panel.PluginId, panel.ContributionId, "right panel", () =>
            {
                _wiredRightPanelIds.Add(fullId);
                var view = panel.ViewFactory();
                var uiElement = WrapAsUIElement(view, $"Plugin: {panel.Title}");
                _rightPanelContents[fullId] = uiElement;
                _rightPanelTitles[fullId] = panel.Title;

                // Add menu item to View → Right Panel submenu
                TitleBar.AddRightPanelMenuItem(fullId, panel.Title, () => SwitchRightPanel(fullId));
            });
        }
    }

    /// <summary>Switches the right panel to show the specified panel's content.</summary>
    private void SwitchRightPanel(string panelId)
    {
        _mainWindowViewModel.IsRightPanelVisible = true;
        _mainWindowViewModel.IsRightPanelCollapsed = false;

        if (panelId == "vido.videoInfo")
        {
            var videoDetailsPanel = new VideoDetailsPanel { DataContext = _videoDetailsViewModel };
            RightPanelContent.Content = videoDetailsPanel;
            RightPanelTitle.Text = "VIDEO INFO";
        }
        else if (_rightPanelContents.TryGetValue(panelId, out var content))
        {
            RightPanelContent.Content = content;
            RightPanelTitle.Text = _rightPanelTitles.TryGetValue(panelId, out var title)
                ? title.ToUpperInvariant()
                : "PLUGIN";
        }
    }

    /// <summary>
    /// Handles a plugin's request to show a specific right panel.
    /// Dispatches to the UI thread if needed.
    /// </summary>
    private void OnRightPanelShowRequested(string fullPanelId)
    {
        if (Dispatcher.CheckAccess())
            SwitchRightPanel(fullPanelId);
        else
            Dispatcher.BeginInvoke(() => SwitchRightPanel(fullPanelId));
    }

    /// <summary>
    /// Handles a plugin's request to show a specific bottom panel tab.
    /// Dispatches to the UI thread if needed, activates the tab
    /// and shows its cached content.
    /// </summary>
    private void OnBottomPanelShowRequested(string fullPanelId)
    {
        void Activate()
        {
            _mainWindowViewModel.ActivateBottomPanelTab(fullPanelId);

            if (_bottomPanelContents.TryGetValue(fullPanelId, out var content))
                BottomPanelContent.Content = content;
        }

        if (Dispatcher.CheckAccess())
            Activate();
        else
            Dispatcher.BeginInvoke(Activate);
    }

    // ── File Handler wiring ──

    /// <summary>Registered plugin file handlers, keyed by extension (lowercase).</summary>
    private readonly Dictionary<string, Action<FileNode>> _pluginFileHandlers = new(StringComparer.OrdinalIgnoreCase);

    private void WirePluginFileHandlers()
    {
        foreach (var handler in _contributionRegistry.GetFileHandlers())
        {
            var fullId = $"plugin.{handler.PluginId}.fileHandler";
            if (_wiredFileHandlerIds.Contains(fullId)) continue;
            _wiredFileHandlerIds.Add(fullId);

            foreach (var ext in handler.Extensions)
            {
                var normalizedExt = ext.StartsWith('.') ? ext : $".{ext}";
                _pluginFileHandlers[normalizedExt] = handler.Handler;
                _fileExplorerViewModel.AdditionalAcceptedExtensions.Add(normalizedExt);
            }
        }
    }

    private void WirePluginControlBarItems()
    {
        foreach (var item in _contributionRegistry.GetControlBarItems())
        {
            var fullId = $"plugin.{item.PluginId}.{item.ContributionId}";
            if (_wiredControlBarIds.Contains(fullId)) continue;

            SafeWireContribution(item.PluginId, item.ContributionId, "control bar item", () =>
            {
                _wiredControlBarIds.Add(fullId);
                var view = WrapAsUIElement(item.ViewFactory());
                VideoPlayer.AddPluginControlBarItem(fullId, view);

                if (item.OverlayFactory is not null)
                {
                    var overlay = WrapAsUIElement(item.OverlayFactory());
                    VideoPlayer.AddPluginOverlay(fullId, overlay);
                }
            });
        }
    }

    /// <summary>
    /// Handles files/folders dropped onto the video player area.
    /// All items are added to the explorer additively. The first video file plays.
    /// </summary>
    private void OnFilesDroppedOnPlayer(string[] paths)
    {
        var hasUnsupported = _fileExplorerViewModel.AddItems(paths);
        if (hasUnsupported)
            ShowUnsupportedFileNotification();

        // Play the first video file found in the drop
        var firstVideo = FindFirstVideoFile(paths);
        if (firstVideo is not null)
        {
            _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
            SafeFireAndForget(PlayDroppedVideoAsync(firstVideo));
        }

        EnsureExplorerVisible();
    }

    /// <summary>
    /// Handles files/folders dropped onto the file explorer panel.
    /// All items are added to the tree additively. No video playback is triggered.
    /// </summary>
    private void OnFilesDroppedOnExplorer(string[] paths)
    {
        var hasUnsupported = _fileExplorerViewModel.AddItems(paths);
        if (hasUnsupported)
            ShowUnsupportedFileNotification();

        EnsureExplorerVisible();
    }

    /// <summary>
    /// Loads and plays a dropped video file.
    /// </summary>
    private async Task PlayDroppedVideoAsync(string filePath)
    {
        try
        {
            await _videoPlayerViewModel.LoadAndPlayAsync(filePath);
            _logService.Info($"Playing dropped file: {Path.GetFileName(filePath)}", "DragDrop");
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to play dropped file: {ex.Message}", "DragDrop");
        }
    }

    /// <summary>
    /// Returns the first recognized video file path from an array, or null.
    /// </summary>
    private static string? FindFirstVideoFile(string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && FileNode.VideoExtensions.Contains(Path.GetExtension(path)))
                return path;
        }
        return null;
    }

    /// <summary>
    /// Ensures the sidebar is visible and the Explorer panel is active.
    /// </summary>
    private void EnsureExplorerVisible()
    {
        if (_activityBarViewModel is not null)
        {
            _activityBarViewModel.ActivePanel = SidebarPanelKind.Explorer;
            _activityBarViewModel.IsSidebarVisible = true;
            OnPanelChanged(this, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Shows a brief notification when an unsupported file type is dropped.
    /// The notification auto-hides after 3 seconds.
    /// </summary>
    private void ShowUnsupportedFileNotification()
    {
        UnsupportedDropNotification.Visibility = Visibility.Visible;
        _logService.Warning("Dropped file type is not supported", "DragDrop");

        // Dispose existing timer if any
        _notificationTimer?.Stop();
        _notificationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _notificationTimer.Tick += (_, _) =>
        {
            UnsupportedDropNotification.Visibility = Visibility.Collapsed;
            _notificationTimer.Stop();
        };
        _notificationTimer.Start();
    }

    /// <summary>
    /// Fallback handler: processes drops on the main window that were not
    /// caught by the video player or file explorer (e.g., title bar, status bar).
    /// Behaves the same as a drop on the player area.
    /// </summary>
    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        // Already handled by child controls
        if (e.Handled) return;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            OnFilesDroppedOnPlayer(paths);

        e.Handled = true;
    }

    private void OnWindowDragEnter(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    #region Window State Persistence

    /// <summary>
    /// Restores window position, size, and maximized state from persisted AppState.
    /// On first run (NaN values), the window uses its XAML defaults at CenterScreen.
    /// </summary>
    private void RestoreWindowState()
    {
        var state = _stateService.Current;

        // Only restore position if we have valid saved values
        if (!double.IsNaN(state.WindowLeft) && !double.IsNaN(state.WindowTop))
        {
            Left = state.WindowLeft;
            Top = state.WindowTop;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        Width = state.WindowWidth;
        Height = state.WindowHeight;

        if (state.IsMaximized)
        {
            // Defer maximize so WindowChrome applies correctly
            Loaded += (_, _) => WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// Restores layout dimensions (sidebar width, panel heights) from persisted settings.
    /// Called after RestoreWindowState so the window geometry is already set.
    /// </summary>
    private void RestoreLayoutState()
    {
        var settings = _settingsService.Current;

        // Restore remembered dimensions for panels
        _bottomPanelHeight = settings.BottomPanelHeight;
        _rightPanelWidth = settings.RightPanelWidth;

        // Restore sidebar width — applied when sidebar visibility fires via OnPanelChanged
        // The sidebar visibility itself is restored by the activity bar view model.

        // Restore active sidebar panel from state
        var state = _stateService.Current;
        if (_activityBarViewModel is not null && !string.IsNullOrEmpty(state.ActiveSidebarPanel))
        {
            if (Enum.TryParse<SidebarPanelKind>(state.ActiveSidebarPanel, out var panel))
            {
                _activityBarViewModel.SetActivePanel(panel);
                OnPanelChanged(this, new RoutedEventArgs());
                ActivityBar.UpdateActiveStates();
            }
        }

        // After Loaded: process command-line args (if any) or restore last video.
        // Deferred to Loaded so the visual tree / video engine are fully ready.
        Loaded += async (_, _) =>
        {
            if (_pendingCommandLineArgs is { Length: > 0 })
            {
                // Command-line file/folder takes priority over restoring last video.
                try
                {
                    await ExecutePendingCommandLineArgsAsync();
                }
                catch (Exception ex)
                {
                    _logService.Error($"Failed to process command-line args: {ex.Message}", "App");
                }
            }
            else
            {
                try
                {
                    await _videoPlayerViewModel.RestoreLastVideoAsync();
                }
                catch (Exception ex)
                {
                    _logService.Error($"Failed to restore last video: {ex.Message}", "App");
                }
            }
        };
    }

    /// <summary>
    /// Captures current window geometry into AppState for persistence.
    /// Uses RestoreBounds when maximized to save the normal-state geometry.
    /// If in fullscreen, saves the pre-fullscreen geometry instead.
    /// </summary>
    private void SaveWindowState()
    {
        var state = _stateService.Current;

        if (_isFullscreen)
        {
            // Save the pre-fullscreen state, not the fullscreen dimensions
            state.IsMaximized = _preFullscreenWindowState == WindowState.Maximized;
            state.WindowLeft = _preFullscreenLeft;
            state.WindowTop = _preFullscreenTop;
            state.WindowWidth = _preFullscreenWidth;
            state.WindowHeight = _preFullscreenHeight;
        }
        else
        {
            state.IsMaximized = WindowState == WindowState.Maximized;

            // Save the restore bounds (normal position/size), not the maximized dimensions
            var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
            state.WindowLeft = bounds.Left;
            state.WindowTop = bounds.Top;
            state.WindowWidth = bounds.Width;
            state.WindowHeight = bounds.Height;
        }

        // Save layout dimensions to settings
        var settings = _settingsService.Current;
        if (_activityBarViewModel is not null)
        {
            state.ActiveSidebarPanel = _activityBarViewModel.ActivePanel.ToString();
        }
        if (SidebarColumn.Width.Value > 0)
            settings.SidebarWidth = SidebarColumn.Width.Value;
        settings.BottomPanelHeight = _bottomPanelHeight;
        settings.RightPanelWidth = _rightPanelWidth;
    }

    #endregion

    /// <summary>
    /// Configures the DWM composition surface to use dark colors.
    /// GlassFrameThickness=-1 extends the DWM surface over the entire client area,
    /// eliminating flicker during resize. These DWM attributes ensure that surface
    /// is dark instead of the default white, so fast resizing doesn't reveal
    /// bright edges before WPF can render.
    /// </summary>
    private static void ApplyDarkDwmSurface(IntPtr hwnd)
    {
        // Enable immersive dark mode — makes DWM use dark surface (Win10 1809+)
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        var darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // Set caption color to our exact dark background (Win11 22000+, ignored on older)
        const int DWMWA_CAPTION_COLOR = 35;
        var captionColor = 0x001F1F1F; // COLORREF: 0x00BBGGRR — matches #1f1f1f
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

        // Set class background brush as fallback for any Win32 background painting
        const int GclpHbrBackground = -10;
        var darkBrush = CreateSolidBrush(0x001F1F1F);
        SetClassLongPtr(hwnd, GclpHbrBackground, darkBrush);

        // Ensure WS_SYSMENU is set so the taskbar shows our Window.Icon.
        // WindowStyle="None" can strip this flag, causing a blank taskbar icon.
        const int GWL_STYLE = -16;
        const int WS_SYSMENU = 0x00080000;
        var style = GetWindowLongPtr(hwnd, GWL_STYLE);
        SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style.ToInt64() | WS_SYSMENU));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        const int WM_ERASEBKGND = 0x0014;

        if (msg == WM_ERASEBKGND)
        {
            handled = true;
            return new IntPtr(1);
        }

        if (msg == WM_GETMINMAXINFO)
        {
            var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hMonitor != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    // In fullscreen mode, use the full monitor rect (covers taskbar).
                    // In normal mode, use the work area (respects taskbar).
                    var bounds = _isFullscreen ? mi.rcMonitor : mi.rcWork;
                    var monRect = mi.rcMonitor;

                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMaxPosition.X = bounds.Left - monRect.Left;
                    mmi.ptMaxPosition.Y = bounds.Top - monRect.Top;
                    mmi.ptMaxSize.X = bounds.Right - bounds.Left;
                    mmi.ptMaxSize.Y = bounds.Bottom - bounds.Top;

                    var dpi = VisualTreeHelper.GetDpi(this);
                    mmi.ptMinTrackSize.X = (int)(MinWindowWidth * dpi.DpiScaleX);
                    mmi.ptMinTrackSize.Y = (int)(MinWindowHeight * dpi.DpiScaleY);

                    Marshal.StructureToPtr(mmi, lParam, true);
                }
            }
            handled = true;
        }

        return IntPtr.Zero;
    }

    #region Native Interop

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    private static extern IntPtr SetClassLongPtr(IntPtr hwnd, int index, IntPtr newLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newLong);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    #endregion

    // ── App Setting Changes ─────────────────────────────────────────

    private void OnAppSettingChanged(string key)
    {
        if (key.Equals("screenshot.enabled", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = _settingsService.Current.ScreenshotEnabled;
            if (Dispatcher.CheckAccess())
                TitleBar.SetScreenshotButtonVisible(enabled);
            else
                Dispatcher.BeginInvoke(() => TitleBar.SetScreenshotButtonVisible(enabled));

            // Populate the default screenshot directory the first time the user enables
            if (enabled && string.IsNullOrWhiteSpace(_settingsService.Current.ScreenshotDirectory))
            {
                var defaultDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Screenshots");
                _appSettingsStore.Set("screenshot.directory", defaultDir);
            }
        }
    }

    // ── Help Menu ───────────────────────────────────────────────────

    private void OnScreenshotRequested()
    {
        try
        {
            // Determine save directory
            var dir = _settingsService.Current.ScreenshotDirectory;
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Screenshots");

            Directory.CreateDirectory(dir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filePath = Path.Combine(dir, $"Vido_{timestamp}.png");

            // Get DPI scaling
            var source = PresentationSource.FromVisual(this);
            double dpiX = 96.0, dpiY = 96.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
            }

            // Render the window's visual tree directly — pixel-perfect, no DWM border issues
            var target = WindowBorder; // the root Border element inside the window
            int pixelWidth = (int)Math.Ceiling(target.ActualWidth * dpiX / 96.0);
            int pixelHeight = (int)Math.Ceiling(target.ActualHeight * dpiY / 96.0);

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
            rtb.Render(target);
            rtb.Freeze();

            // Save as PNG
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var stream = new FileStream(filePath, FileMode.Create);
            encoder.Save(stream);

            // Play shutter feedback
            PlayScreenshotFlash();
            PlayScreenshotSound();

            _logService.Info($"Screenshot saved: {filePath}", "Screenshot");
        }
        catch (Exception ex)
        {
            _logService.Error($"Screenshot failed: {ex.Message}", "Screenshot");
        }
    }

    /// <summary>
    /// Plays a subtle white flash animation over the entire window to
    /// give visual feedback that a screenshot was captured.
    /// </summary>
    private void PlayScreenshotFlash()
    {
        // Quick flash: fade from semi-transparent white to fully transparent
        var flash = new DoubleAnimation
        {
            From = 0.45,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(350),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        flash.Completed += (_, _) => ScreenshotFlashOverlay.Opacity = 0;
        ScreenshotFlashOverlay.BeginAnimation(OpacityProperty, flash);
    }

    /// <summary>
    /// Plays a synthesized camera shutter click sound ("ka-click").
    /// Generated in-memory — no packaged audio file required.
    /// </summary>
    private static void PlayScreenshotSound()
    {
        try
        {
            const int sampleRate = 22050;
            const int channels = 1;
            const int bitsPerSample = 16;
            const double totalSeconds = 0.12;
            int totalSamples = (int)(sampleRate * totalSeconds);
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;
            int dataSize = totalSamples * blockAlign;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // WAV header
            bw.Write("RIFF"u8);
            bw.Write(36 + dataSize);
            bw.Write("WAVE"u8);
            bw.Write("fmt "u8);
            bw.Write(16);            // chunk size
            bw.Write((short)1);      // PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)bitsPerSample);
            bw.Write("data"u8);
            bw.Write(dataSize);

            // Synthesize a mechanical shutter click:
            // Phase 1 (0-40ms): sharp attack noise burst (mirror slap)
            // Phase 2 (40-60ms): quiet gap
            // Phase 3 (60-90ms): softer click (shutter curtain)
            // Phase 4 (90-120ms): rapid decay
            var rng = new Random(42); // deterministic seed for consistent sound
            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double amplitude;

                if (t < 0.005)
                {
                    // Sharp attack ramp
                    amplitude = t / 0.005 * 0.9;
                }
                else if (t < 0.040)
                {
                    // First click body — exponential decay
                    amplitude = 0.9 * Math.Exp(-(t - 0.005) * 60);
                }
                else if (t < 0.060)
                {
                    // Quiet gap between clicks
                    amplitude = 0.02;
                }
                else if (t < 0.065)
                {
                    // Second click attack
                    amplitude = (t - 0.060) / 0.005 * 0.5;
                }
                else if (t < 0.090)
                {
                    // Second click body
                    amplitude = 0.5 * Math.Exp(-(t - 0.065) * 80);
                }
                else
                {
                    // Tail decay
                    amplitude = 0.1 * Math.Exp(-(t - 0.090) * 100);
                }

                // Filtered noise: mix of broadband noise and a low thunk tone
                double noise = (rng.NextDouble() * 2 - 1);
                double thunk = Math.Sin(2 * Math.PI * 180 * t) * 0.4;
                double sample = (noise * 0.6 + thunk) * amplitude;

                // Clamp and write 16-bit PCM
                short pcm = (short)Math.Clamp(sample * 16000, short.MinValue, short.MaxValue);
                bw.Write(pcm);
            }

            bw.Flush();
            ms.Position = 0;
            var player = new System.Media.SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            // Sound is non-critical — swallow any errors
        }
    }

    private void ShowAboutDialog()
    {
        var dialog = new AboutDialog(FFmpegVersion)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async void ShowCheckForUpdatesMessage()
    {
        // If an installer was already downloaded, offer to install it
        if (_pendingInstallerPath is not null && File.Exists(_pendingInstallerPath))
        {
            var install = MessageBox.Show(
                this,
                "An update has already been downloaded. Install now and restart?",
                "Update Ready",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (install == MessageBoxResult.Yes)
            {
                _updateService.LaunchInstaller(_pendingInstallerPath);
                Application.Current.Shutdown();
            }
            return;
        }

        var result = await _updateService.CheckForUpdateAsync();

        if (result.ErrorMessage is not null)
        {
            MessageBox.Show(
                this,
                $"Could not check for updates:\n\n{result.ErrorMessage}",
                "Check for Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (result.IsUpdateAvailable)
        {
            var msg = $"A new version of Vido is available!\n\n" +
                      $"Current: v{result.CurrentVersion}\nLatest: v{result.LatestVersion}\n\n";

            if (result.InstallerDownloadUrl is not null)
            {
                msg += "Download and install the update?";
                var mbResult = MessageBox.Show(
                    this, msg, "Update Available",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (mbResult == MessageBoxResult.Yes)
                    await DownloadAndPromptRestartAsync(result);
            }
            else
            {
                // No installer asset — fall back to opening the release page
                msg += "Open the release page to download manually?";
                var mbResult = MessageBox.Show(
                    this, msg, "Update Available",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (mbResult == MessageBoxResult.Yes && result.ReleaseUrl is not null)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = result.ReleaseUrl,
                        UseShellExecute = true
                    });
                }
            }
        }
        else
        {
            MessageBox.Show(
                this,
                "You are running the latest version of Vido.",
                "Check for Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async Task DownloadAndPromptRestartAsync(UpdateCheckResult result)
    {
        try
        {
            _logService.Info("Downloading update...", "Updates");

            var fileName = Path.GetFileName(new Uri(result.InstallerDownloadUrl!).LocalPath);
            _pendingInstallerPath = await _updateService.DownloadInstallerAsync(
                result.InstallerDownloadUrl!, fileName);

            _logService.Info($"Update downloaded to {_pendingInstallerPath}", "Updates");

            var restart = MessageBox.Show(
                this,
                $"Vido v{result.LatestVersion} has been downloaded.\n\n" +
                "Install now and restart?",
                "Update Downloaded",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (restart == MessageBoxResult.Yes)
            {
                _updateService.LaunchInstaller(_pendingInstallerPath);
                Application.Current.Shutdown();
            }
            // If No — the installer stays in temp. Next "Check for Updates" will
            // offer to install it.
        }
        catch (Exception ex)
        {
            _pendingInstallerPath = null;
            _logService.Error($"Failed to download update: {ex.Message}", "Updates");

            MessageBox.Show(
                this,
                $"Failed to download update: {ex.Message}\n\n" +
                "You can download it manually from the release page.",
                "Download Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            if (result.ReleaseUrl is not null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.ReleaseUrl,
                    UseShellExecute = true
                });
            }
        }
    }

    private void OnEnterRepositoryCode()
    {
        var input = InputDialog.ShowInputDialog(
            this,
            "Enter Repository Code",
            "Enter a repository code or URL to add a plugin registry:");

        if (string.IsNullOrWhiteSpace(input)) return;

        var url = AppSettings.ResolveRepositoryCode(input.Trim());

        if (url is null)
        {
            MessageBox.Show(
                this,
                $"Unknown repository code: '{input}'.\n\n" +
                "You can enter a known code (e.g. NSFW) or a direct URL " +
                "(starting with https:// or file://).",
                "Unknown Code",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Check if already added
        var urls = _settingsService.Current.PluginRegistryUrls;
        if (urls.Any(u => u.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                this,
                "This registry is already in your list.",
                "Already Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Add the URL and save
        urls.Add(url);
        _settingsService.QueueSave();

        MessageBox.Show(
            this,
            "Registry added successfully! The plugin list will refresh.",
            "Registry Added",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        // Refresh the plugin manager if it's loaded
        if (_pluginManagerViewModel is not null)
            _ = _pluginManagerViewModel.RefreshAsync();

        // Refresh the settings panel if it's open so the URL list updates
        _settingsPage?.RefreshRegistryUrls();
    }
}
