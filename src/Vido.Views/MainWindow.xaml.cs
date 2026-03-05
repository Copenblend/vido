using System.IO;
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
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.Core.Windowing;
using Vido.ViewModels;
using Vido.Views.Panels;
using Vido.Views.Services;
using Vido.Core.Events;
using Vido.Core.Haptics;
using Vido.Core.Logging;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Playback;
using Vido.Core.Updates;
using Vido.Services.Osr2Plus;
using Vido.Services.Playlists;
using Vido.Services.Pulse;
using Vido.ViewModels.Osr2Plus;
using Vido.ViewModels.Playlists;
using Vido.ViewModels.Pulse;
using Vido.Views.Controls;
using Vido.Views.Osr2Plus;
using Vido.Views.Playlists;
using Vido.Views.Pulse;

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
    private readonly IContextMenuRegistry _contextMenuRegistry;
    private readonly IUpdateService _updateService;
    private readonly IEventBus _eventBus;

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
    private SettingsPage? _settingsPage;
    private readonly AppSettingsStore _appSettingsStore;

    // â”€â”€ OSR2+ integrated feature â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private TCodeService? _tcode;
    private Osr2PlusSidebarViewModel? _osr2SidebarVm;
    private AxisControlViewModel? _axisControlVm;
    private VisualizerViewModel? _visualizerVm;
    private BeatBarViewModel? _beatBarVm;
    private BeatDetectionService? _beatDetection;
    private readonly List<IDisposable> _osr2Subscriptions = [];
    private UIElement? _osr2SidebarContent;
    private double _lastSpeedRatio = 1.0;

    // â”€â”€ Pulse integrated feature â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private PulseEngine? _pulseEngine;
    private AudioPreAnalysisService? _pulsePreAnalysis;
    private PulseSidebarViewModel? _pulseSidebarVm;
    private WaveformViewModel? _waveformVm;
    private UIElement? _pulseSidebarContent;
    private UIElement? _pulseBeatRateControl;
    private Button? _pulseToolbarButton;
    private readonly List<IDisposable> _pulseSubscriptions = [];

    // â”€â”€ Playlists integrated feature â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private PlaylistViewModel? _playlistVm;
    private PlaylistProvider? _playlistProvider;
    private UIElement? _playlistSidebarContent;
    private ToastService? _toastService;
    private IVideoEngine? _videoEngine;

    // Remembered panel dimensions for toggle persistence
    private double _bottomPanelHeight = 200;
    private double _rightPanelWidth = 300;

    // â”€â”€ Fullscreen state â”€â”€
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
    private const int ControlsFadeDurationMs = 200;

    /// <summary>
    /// Lazily generated WAV data for the screenshot shutter sound.
    /// Deterministic synthesis means this can be generated once and reused.
    /// </summary>
    private static readonly Lazy<byte[]> s_shutterWav = new(GenerateShutterWav);
    
    /// <summary>
    /// Creates the main application window, wiring up all services, view models, and UI subsystems
    /// including the video player, file explorer, plugin host, keyboard shortcuts, and layout persistence.
    /// </summary>
    /// <param name="stateService">Persists and restores application state (window position, recent files).</param>
    /// <param name="settingsService">Persists and restores user-configurable application settings.</param>
    /// <param name="logService">Centralized logging service for writing diagnostic messages.</param>
    /// <param name="keyboardShortcutService">Manages registration and dispatch of keyboard shortcuts.</param>
    /// <param name="contextMenuRegistry">Registry of plugin context menu items for the file explorer.</param>
    /// <param name="updateService">Service for checking and downloading application updates.</param>
    /// <param name="eventBus">Event bus for publishing and subscribing to application-wide events.</param>
    /// <param name="fileExplorerViewModel">View model for the file explorer sidebar panel.</param>
    /// <param name="videoPlayerViewModel">View model controlling video playback and transport.</param>
    /// <param name="mainWindowViewModel">View model managing tabs, panels, and overall window layout.</param>
    /// <param name="outputLogViewModel">View model for the output log bottom panel.</param>
    /// <param name="videoDetailsViewModel">View model for the video details right panel.</param>
    /// <param name="statusBarViewModel">View model for the status bar at the bottom of the window.</param>
    public MainWindow(
        IStateService stateService,
        ISettingsService settingsService,
        ILogService logService,
        IKeyboardShortcutService keyboardShortcutService,
        IContextMenuRegistry contextMenuRegistry,
        IUpdateService updateService,
        IEventBus eventBus,
        IVideoEngine videoEngine,
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
        _contextMenuRegistry = contextMenuRegistry;
        _updateService = updateService;
        _eventBus = eventBus;
        _videoEngine = videoEngine;
        _fileExplorerViewModel = fileExplorerViewModel;
        _videoPlayerViewModel = videoPlayerViewModel;
        _mainWindowViewModel = mainWindowViewModel;
        _outputLogViewModel = outputLogViewModel;
        _videoDetailsViewModel = videoDetailsViewModel;
        _statusBarViewModel = statusBarViewModel;

        // Shared settings store â€” used by SettingsPage and for direct change monitoring
        _appSettingsStore = new AppSettingsStore(settingsService);
        _appSettingsStore.SettingChanged += OnAppSettingChanged;

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
        _toastService = new ToastService(_settingsService);
        SetupOsr2Plus();
        SetupPulse();
        SetupPlaylists();
        RestoreWindowState();
        RestoreLayoutState();

        _logService.Info("Vido started", "App");
    }

    /// <summary>
    /// Stores command-line arguments for deferred processing after the window
    /// has fully loaded (so the video engine and UI are ready).
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application (file paths or folder paths).</param>
    public void ProcessCommandLineArgs(string[] args)
    {
        if (args.Length == 0) return;
        _pendingCommandLineArgs = args;
    }

    /// <summary>
    /// Handles a file path received from a secondary application instance via
    /// the single-instance named pipe. Brings the window to the foreground and
    /// loads the specified video file.
    /// </summary>
    /// <param name="filePath">The absolute file path forwarded by the secondary instance.</param>
    public void HandleExternalFileOpen(string filePath)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            // Bring window to foreground
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();

            // Open the file using the same logic as command-line args
            if (File.Exists(filePath))
            {
                _logService.Info($"Opening file from external instance: {filePath}", "App");

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

                _mainWindowViewModel.ActivateTab(MainWindowViewModel.PlayerTabId);
                await _videoPlayerViewModel.LoadAndPlayAsync(filePath);
            }
        });
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
        // In fullscreen mode, skip normal state sync â€” fullscreen manages its own chrome
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

        // Subscribe to video load/unload events to update video name overlay
        _eventBus.Subscribe<VideoLoadedEvent>(e =>
        {
            var videoName = Path.GetFileNameWithoutExtension(e.FilePath);
            Dispatcher.BeginInvoke(() => VideoPlayer.SetVideoName(videoName));
        });
        _eventBus.Subscribe<VideoUnloadedEvent>(_ =>
        {
            Dispatcher.BeginInvoke(() => VideoPlayer.SetVideoName(null));
        });
    }

    private void SetupOutputLog()
    {
        var outputLogPanel = new OutputLogPanel
        {
            DataContext = _outputLogViewModel
        };

        // Store panel content mapping for bottom panel tab switching
        // (don't set BottomPanelContent.Content here â€” let UpdateBottomPanelContent
        //  choose the right content based on active tab, which may not be Log Output)
        _bottomPanelContents[MainWindowViewModel.OutputTabId] = outputLogPanel;

        // Show whatever tab is currently active (may be nothing if log is hidden)
        UpdateBottomPanelContent();

        // Wire bottom panel tab content switching
        _mainWindowViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ActiveBottomPanelTab))
                UpdateBottomPanelContent();
        };
    }

    /// <summary>
    /// Map of bottom panel tab IDs to their content controls.
    /// </summary>
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

    // â”€â”€ Keyboard Shortcuts â”€â”€

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
    /// Maps a WPF <see cref="Key"/> to the string representation used by <see cref="KeyBinding"/>.
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

    // â”€â”€ Fullscreen â”€â”€

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
        // Suppress settings save â€” these are transient fullscreen changes, not user preferences
        _mainWindowViewModel.SuppressSettingsSave = true;
        _mainWindowViewModel.IsBottomPanelVisible = false;
        _mainWindowViewModel.IsRightPanelVisible = false;
        _mainWindowViewModel.IsStatusBarVisible = false;
        _mainWindowViewModel.SuppressSettingsSave = false;

        // Force video tab active â€” fullscreen should always show the video player
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

        // Restore panels â€” suppress save since we're restoring to the already-persisted state
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
                Interval = TimeSpan.FromSeconds(_settingsService.Current.FullscreenAutoHideSeconds)
            };
            _fullscreenHideTimer.Tick += (_, _) =>
            {
                if (_isFullscreen)
                    HideFullscreenControls();
            };
        }

        // Re-read setting each time fullscreen is entered (setting may have changed)
        _fullscreenHideTimer.Interval = TimeSpan.FromSeconds(_settingsService.Current.FullscreenAutoHideSeconds);
        _fullscreenHideTimer.Start();
        _controlsVisible = true;
    }

    /// <summary>
    /// Handles mouse movement during fullscreen â€” shows controls and resets hide timer.
    /// </summary>
    private void OnFullscreenMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isFullscreen) return;

        if (!_controlsVisible)
            ShowFullscreenControls(animate: true);

        // Reset the auto-hide timer with current setting value
        if (_fullscreenHideTimer is not null)
            _fullscreenHideTimer.Interval = TimeSpan.FromSeconds(_settingsService.Current.FullscreenAutoHideSeconds);
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

        // Show the video name overlay if the setting is enabled
        if (_settingsService.Current.FullscreenShowVideoName)
        {
            var nameOverlay = VideoPlayer.VideoNameOverlayElement;
            nameOverlay.Visibility = Visibility.Visible;
            if (animate)
            {
                var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(ControlsFadeDurationMs));
                nameOverlay.BeginAnimation(OpacityProperty, fadeIn);
            }
            else
            {
                nameOverlay.BeginAnimation(OpacityProperty, null);
                nameOverlay.Opacity = 1.0;
            }
        }
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

        // Also fade out the video name overlay
        var nameOverlay = VideoPlayer.VideoNameOverlayElement;
        var nameFadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(ControlsFadeDurationMs));
        nameFadeOut.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
        nameFadeOut.Completed += (_, _) => nameOverlay.Visibility = Visibility.Collapsed;
        nameOverlay.BeginAnimation(OpacityProperty, nameFadeOut);
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

    /// <summary>
    /// Creates a placeholder panel for tabs without real content yet.
    /// </summary>
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
        if (activeTab is null)
        {
            BottomPanelContent.Content = null;
            return;
        }

        if (!_bottomPanelContents.TryGetValue(activeTab.Id, out var content))
        {
            // Create placeholder for tabs without real implementations
            content = CreatePlaceholderPanel(activeTab.Title);
            _bottomPanelContents[activeTab.Id] = content;
        }

        BottomPanelContent.Content = content;
    }

    /// <summary>
    /// Bottom panel tab click handler â€” activates the clicked tab.
    /// </summary>
    private void OnBottomPanelTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BottomPanelTabItem tab)
        {
            _mainWindowViewModel.ActivateBottomPanelTab(tab.Id);
        }
    }

    /// <summary>
    /// Bottom panel collapse/expand chevron click handler.
    /// </summary>
    private void OnBottomPanelCollapseClick(object sender, RoutedEventArgs e)
    {
        _mainWindowViewModel.ToggleBottomPanelCollapse();
    }

    /// <summary>
    /// Right panel collapse/expand chevron click handler.
    /// </summary>
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
        TitleBar.ToggleLogOutputRequested += () =>
        {
            _mainWindowViewModel.ToggleLogOutput();
            TitleBar.SetLogOutputVisible(_mainWindowViewModel.IsLogOutputVisible);
        };
        TitleBar.ShowVideoInfoRequested += () => SwitchRightPanel("vido.videoInfo");
        TitleBar.ToggleStatusBarRequested += () => _mainWindowViewModel.ToggleStatusBar();
        TitleBar.ToggleSidebarRequested += ToggleSidebar;

        // Wire fullscreen menu event
        TitleBar.FullscreenRequested += ToggleFullscreen;

        // Wire Help menu events
        TitleBar.AboutRequested += ShowAboutDialog;
        TitleBar.CheckForUpdatesRequested += ShowCheckForUpdatesMessage;

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
        TitleBar.SetLogOutputVisible(_mainWindowViewModel.IsLogOutputVisible);

        // Wire the "Open Folder" button inside the explorer panel
        _fileExplorerPanel.OpenFolderRequested += ShowOpenFolderDialog;

        // Wire play file from context menu
        _fileExplorerPanel.PlayFileRequested += OnPlayFileRequested;

        // Wire double-click on video file to play
        _fileExplorerPanel.VideoFileDoubleClicked += OnPlayFileRequested;

        // Wire file handler for plugin-registered extensions
        _fileExplorerPanel.FileHandlerRequested += OnFileHandlerRequested;

        // Inject context menu registry for context menu items
        _fileExplorerPanel.ContextMenuRegistry = _contextMenuRegistry;

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
    /// Handles files or folders added via File > Add Fileâ€¦ / Add Folderâ€¦ menu items.
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

    // â”€â”€ Tab content switching â”€â”€

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
                _settingsPage = new SettingsPage(_settingsService);
            }
            DynamicTabContent.Content = _settingsPage;
            DynamicTabContent.Visibility = Visibility.Visible;
        }
        else
        {
            // Future tabs â€” show empty for now
            VideoPlayer.Visibility = Visibility.Collapsed;
            DynamicTabContent.Content = null;
            DynamicTabContent.Visibility = Visibility.Visible;
        }
    }

    // â”€â”€ Panel visibility â”€â”€

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
                case SidebarPanelKind.Osr2Plus:
                    Sidebar.SetPanelContent(_osr2SidebarContent);
                    break;
                case SidebarPanelKind.Pulse:
                    Sidebar.SetPanelContent(_pulseSidebarContent);
                    break;
                case SidebarPanelKind.Playlists:
                    Sidebar.SetPanelContent(_playlistSidebarContent);
                    break;
                // TODO PI-024: Add case for Settings
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

    /// <summary>
    /// Finalizes native window initialization and installs Win32 message hooks.
    /// </summary>
    /// <param name="e">Event arguments for source initialization.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        ApplyDarkDwmSurface(hwnd);
    }

    /// <summary>
    /// Persists application state and settings before the window closes.
    /// </summary>
    /// <param name="e">Cancel event arguments for the close operation.</param>
    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _logService.Info("Vido shutting down", "App");

        // Dispose OSR2+ resources
        foreach (var sub in _osr2Subscriptions) sub.Dispose();
        _osr2Subscriptions.Clear();
        _tcode?.Dispose();

        // Dispose Pulse resources
        if (_videoEngine is not null)
        {
            _videoEngine.AudioSamplesAvailable -= OnPulseAudioSamplesAvailable;
            _videoEngine.PositionChanged -= OnPulsePositionChanged;
            _videoEngine.SeekCompleted -= OnPulseSeekCompleted;
        }
        foreach (var sub in _pulseSubscriptions) sub.Dispose();
        _pulseSubscriptions.Clear();
        _waveformVm?.Dispose();
        _pulseSidebarVm?.Dispose();
        _pulseEngine?.Dispose();
        _pulsePreAnalysis?.Dispose();

        SaveWindowState();
        await _stateService.SaveAsync();
        await _settingsService.SaveAsync();
        base.OnClosing(e);
    }

    // â”€â”€ Drag and drop â”€â”€

    /// <summary>
    /// Timer for auto-hiding the unsupported file notification.
    /// </summary>
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

    // â”€â”€ OSR2+ Integrated Feature â”€â”€

    /// <summary>
    /// Creates and wires all OSR2+ services, view models, views, event bus subscriptions,
    /// and UI contribution points. This replaces the plugin-based <c>Osr2PlusPlugin.Activate()</c>
    /// logic with direct integration into the main window.
    /// </summary>
    private void SetupOsr2Plus()
    {
        // â”€â”€ Create Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var interpolation = new InterpolationService();
        _tcode = new TCodeService(interpolation);
        var parser = new FunscriptParser();
        var matcher = new FunscriptMatcher();
        _beatDetection = new BeatDetectionService();

        // â”€â”€ Create ViewModels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _osr2SidebarVm = new Osr2PlusSidebarViewModel(_tcode, _settingsService, _eventBus, toastService: _toastService);
        _axisControlVm = new AxisControlViewModel(_tcode, _settingsService, parser, matcher);
        _visualizerVm = new VisualizerViewModel(_settingsService);
        _beatBarVm = new BeatBarViewModel(_settingsService, _beatDetection);

        // â”€â”€ Wire Sidebar Panel Requests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _osr2SidebarVm.ShowAxisSettingsRequested += () =>
        {
            SwitchRightPanel("osr2.axis-control");
            _settingsService.Current.Osr2LastRightPanel = "osr2.axis-control";
            _settingsService.QueueSave();
        };

        _osr2SidebarVm.ShowVisualizerRequested += () =>
        {
            _mainWindowViewModel.ActivateBottomPanelTab("osr2.visualizer");
            if (_bottomPanelContents.TryGetValue("osr2.visualizer", out var content))
                BottomPanelContent.Content = content;
        };

        // â”€â”€ Wire Device Connection State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _osr2SidebarVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Osr2PlusSidebarViewModel.IsConnected))
            {
                _axisControlVm.SetDeviceConnected(_osr2SidebarVm.IsConnected);
                SetOsr2ToolbarButtonHighlight(_osr2SidebarVm.IsConnected);
            }

            if (e.PropertyName == nameof(Osr2PlusSidebarViewModel.StatusText))
            {
                var statusItem = _statusBarViewModel.FindItem("osr2.status");
                if (statusItem is not null)
                    statusItem.Text = _osr2SidebarVm.StatusText;
            }
        };

        // â”€â”€ Wire Script Changes to Visualizer â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _axisControlVm.ScriptsChanged += scripts => _visualizerVm.SetLoadedAxes(scripts);

        // â”€â”€ Wire Beat Bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _axisControlVm.ScriptsChanged += scripts =>
        {
            if (scripts.TryGetValue("L0", out var l0Script))
                _beatBarVm.LoadBeats(l0Script);
            else
                _beatBarVm.ClearBeats();

            SetOsr2BeatBarOverlayVisible(_beatBarVm.IsActive);
        };

        _beatBarVm.ModeChanged += mode =>
        {
            SetOsr2BeatBarOverlayVisible(mode != BeatBarMode.Off);
        };

        // â”€â”€ Haptic Event Bus Subscriptions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _osr2Subscriptions.Add(_eventBus.Subscribe<ExternalBeatSourceRegistration>(
            reg => _beatBarVm.OnBeatSourceRegistration(reg)));

        _osr2Subscriptions.Add(_eventBus.Subscribe<ExternalBeatEvent>(
            evt => _beatBarVm.OnExternalBeatEvent(evt)));

        _osr2Subscriptions.Add(_eventBus.Subscribe<SuppressFunscriptEvent>(evt =>
        {
            _axisControlVm.OnSuppressFunscript(evt);

            // When switching back to funscript, show the Funscript Visualizer
            if (!evt.SuppressFunscripts)
            {
                _mainWindowViewModel.ActivateBottomPanelTab("osr2.visualizer");
                if (_bottomPanelContents.TryGetValue("osr2.visualizer", out var content))
                    BottomPanelContent.Content = content;
            }
        }));

        _osr2Subscriptions.Add(_eventBus.Subscribe<ExternalAxisPositionsEvent>(
            evt => _tcode.SetExternalPositions(evt.Positions)));

        // â”€â”€ Publish Script & Config Changes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _axisControlVm.ScriptsChanged += scripts =>
        {
            var scriptLoadedMap = new Dictionary<string, bool>(scripts.Count, StringComparer.Ordinal);
            foreach (var key in scripts.Keys)
                scriptLoadedMap[key] = true;

            _eventBus.Publish(new HapticScriptsChangedEvent
            {
                HasAnyScripts = scripts.Count > 0,
                AxisScriptLoaded = scriptLoadedMap,
            });

            // Auto-show funscript visualizer when scripts load
            if (scripts.Count > 0)
            {
                _mainWindowViewModel.ActivateBottomPanelTab("osr2.visualizer");
                if (_bottomPanelContents.TryGetValue("osr2.visualizer", out var content))
                    BottomPanelContent.Content = content;
            }
        };

        PublishOsr2AxisConfig();
        _axisControlVm.AxisConfigChanged += PublishOsr2AxisConfig;

        // â”€â”€ Wire File Dialog Factory â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        foreach (var card in _axisControlVm.AxisCards)
        {
            card.FileDialogFactory = () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Funscript Files (*.funscript)|*.funscript|All Files (*.*)|*.*",
                    Title = $"Open Funscript for {card.AxisName} ({card.AxisId})"
                };
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            };
        }

        // â”€â”€ Subscribe to Playback Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _osr2Subscriptions.Add(_eventBus.Subscribe<VideoLoadedEvent>(OnOsr2VideoLoaded));
        _osr2Subscriptions.Add(_eventBus.Subscribe<VideoUnloadedEvent>(OnOsr2VideoUnloaded));
        _osr2Subscriptions.Add(_eventBus.Subscribe<PlaybackStateChangedEvent>(OnOsr2PlaybackStateChanged));
        _osr2Subscriptions.Add(_eventBus.Subscribe<PlaybackPositionChangedEvent>(OnOsr2PositionChanged));

        // â”€â”€ Register UI Contributions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Sidebar content
        _osr2SidebarContent = new Osr2Plus.SidebarView { DataContext = _osr2SidebarVm };

        // Bottom panel tab: Funscript Visualizer
        var visualizerView = new VisualizerView { DataContext = _visualizerVm };
        _bottomPanelContents["osr2.visualizer"] = visualizerView;
        _mainWindowViewModel.OpenBottomPanelTab("osr2.visualizer", "FUNSCRIPT VISUALIZER");
        TitleBar.AddBottomPanelTabMenuItem("osr2.visualizer", "Funscript Visualizer");

        // Right panel: Axis Control
        var axisControlView = new AxisControlView { DataContext = _axisControlVm };
        _rightPanelContents["osr2.axis-control"] = axisControlView;
        _rightPanelTitles["osr2.axis-control"] = "Axis Settings";
        TitleBar.AddRightPanelMenuItem("osr2.axis-control", "Axis Settings", () => SwitchRightPanel("osr2.axis-control"));

        // Status bar item
        var statusItem = _statusBarViewModel.RegisterItem("osr2.status", StatusBarAlignment.Right, 500);
        statusItem.Text = _osr2SidebarVm.StatusText;
        statusItem.Tooltip = "OSR2+ Connection Status";
        statusItem.IsVisible = true;
        TitleBar.AddStatusBarMenuItem("osr2.status", "OSR2+ Status");

        // Toolbar button: Quick Connect
        SetupOsr2ToolbarButton();

        // Control bar: BeatBar ComboBox + Overlay
        var beatBarComboBox = new BeatBarComboBox { DataContext = _beatBarVm };
        VideoPlayer.AddPluginControlBarItem("osr2.beat-bar", beatBarComboBox);

        var beatBarOverlay = new BeatBarOverlay { DataContext = _beatBarVm };
        VideoPlayer.AddPluginOverlay("osr2.beat-bar", beatBarOverlay);

        // File icons â€” register .funscript extensions in file explorer
        _fileExplorerViewModel.AdditionalAcceptedExtensions.Add(".funscript");

        // Register funscript file icons for explorer display
        const string iconBase = "pack://application:,,,/Vido.Views;component/Assets/Osr2Plus/";
        _fileExplorerPanel!.FileIcons = new Dictionary<string, string>
        {
            { ".funscript", iconBase + "funscript-stroke.png" },
            { ".twist.funscript", iconBase + "funscript-twist.png" },
            { ".roll.funscript", iconBase + "funscript-roll.png" },
            { ".pitch.funscript", iconBase + "funscript-pitch.png" },
        };

        // â”€â”€ Restore Right Panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var lastPanel = _settingsService.Current.Osr2LastRightPanel;
        if (!string.IsNullOrEmpty(lastPanel))
            SwitchRightPanel(lastPanel);

        _logService.Info("OSR2+ feature initialized", "OSR2+");
    }

    /// <summary>
    /// Creates and adds the OSR2+ quick connect toolbar button to the title bar.
    /// Uses the connect icon from embedded resources.
    /// </summary>
    private void SetupOsr2ToolbarButton()
    {
        var icon = new Image
        {
            Source = LoadEmbeddedResource("Assets/Osr2Plus/connect-icon.png"),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        };

        var button = new Button
        {
            Content = icon,
            ToolTip = "OSR2+ Connect",
            Tag = "osr2-quick-connect",
            Height = 22,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        // Custom template matching the snapshot toolbar button style
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

        button.Click += (_, _) =>
        {
            try { _osr2SidebarVm?.ConnectCommand.Execute(null); }
            catch (Exception ex) { _logService.Error($"Quick connect error: {ex.Message}", "OSR2+"); }
        };

        TitleBar.AddPluginToolbarButton(button);
    }

    /// <summary>
    /// Sets the OSR2+ toolbar button highlight state based on connection status.
    /// </summary>
    private void SetOsr2ToolbarButtonHighlight(bool highlighted)
    {
        void Apply()
        {
            TitleBar.SetToolbarButtonHighlight("osr2-quick-connect", highlighted);
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }

    /// <summary>
    /// Sets the BeatBar overlay visibility on the video player.
    /// </summary>
    private void SetOsr2BeatBarOverlayVisible(bool visible)
    {
        void Apply() => VideoPlayer.SetPluginOverlayVisible("osr2.beat-bar", visible);

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }


    // ── Pulse Toolbar Button ──────────────────────────────────

    /// <summary>
    /// Creates and adds the Pulse toggle toolbar button to the title bar.
    /// Uses a Path-based heart outline matching the screenshot button style.
    /// </summary>
    private void SetupPulseToolbarButton()
    {
        // Heart outline path matching the line-art style of other toolbar icons
        var heartPath = new System.Windows.Shapes.Path
        {
            Data = System.Windows.Media.Geometry.Parse(
                "M 8,14 C 5,11 1,8.5 1,5.5 C 1,3 3,1 5.5,1 C 6.8,1 7.8,1.5 8,2.5 C 8.2,1.5 9.2,1 10.5,1 C 13,1 15,3 15,5.5 C 15,8.5 11,11 8,14 Z"),
            StrokeThickness = 1.2,
            Fill = System.Windows.Media.Brushes.Transparent,
            Stretch = System.Windows.Media.Stretch.Uniform,
        };
        heartPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PrimaryForegroundBrush");

        var canvas = new Canvas { Width = 16, Height = 16 };
        canvas.Children.Add(heartPath);
        Canvas.SetLeft(heartPath, 1);
        Canvas.SetTop(heartPath, 1);
        heartPath.Width = 14;
        heartPath.Height = 14;

        _pulseToolbarButton = new Button
        {
            Content = canvas,
            ToolTip = "Toggle Pulse",
            Tag = "pulse-toggle",
            Height = 22,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        // Custom template matching the toolbar button style
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
        _pulseToolbarButton.Template = template;

        // Click toggles PulseUsePulse via the sidebar ViewModel
        _pulseToolbarButton.Click += (_, _) =>
        {
            if (_pulseSidebarVm is not null)
                _pulseSidebarVm.UsePulse = !_pulseSidebarVm.UsePulse;
        };

        // Sync icon when sidebar ViewModel changes (bidirectional)
        if (_pulseSidebarVm is not null)
        {
            _pulseSidebarVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PulseSidebarViewModel.UsePulse))
                    UpdatePulseToolbarIcon(_pulseSidebarVm.UsePulse);
            };

            // Set initial icon state
            UpdatePulseToolbarIcon(_pulseSidebarVm.UsePulse);
        }

        TitleBar.AddPluginToolbarButton(_pulseToolbarButton);
    }

    /// <summary>
    /// Updates the Pulse toolbar button highlight based on the active state.
    /// </summary>
    /// <param name="isActive">Whether Pulse is currently enabled.</param>
    private void UpdatePulseToolbarIcon(bool isActive)
    {
        void Apply()
        {
            TitleBar.SetToolbarButtonHighlight("pulse-toggle", isActive);
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }
    // â”€â”€ Pulse Integrated Feature â”€â”€

    /// <summary>
    /// Creates and wires all Pulse services, view models, views, event bus subscriptions,
    /// and UI contribution points. This replaces the plugin-based <c>PulsePlugin.Activate()</c>
    /// logic with direct integration into the main window.
    /// </summary>
    private void SetupPulse()
    {
        // â”€â”€ Create Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var decoder = new FfmpegAudioDecoder();
        _pulsePreAnalysis = new AudioPreAnalysisService(decoder);
        var liveAmplitude = new LiveAmplitudeService();
        var mapper = new PulseTCodeMapper();

        _pulseEngine = new PulseEngine(
            _pulsePreAnalysis, liveAmplitude, mapper, _eventBus, _logService);

        // â”€â”€ Create ViewModels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _pulseSidebarVm = new PulseSidebarViewModel(_pulseEngine, _settingsService, _toastService);
        _waveformVm = new WaveformViewModel(_pulseEngine, _settingsService);

        // â”€â”€ Wire Status Bar Updates â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _pulseSidebarVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PulseSidebarViewModel.StatusBarText))
            {
                var statusItem = _statusBarViewModel.FindItem("pulse.status");
                if (statusItem is not null)
                    statusItem.Text = _pulseSidebarVm.StatusBarText;
            }

            if (e.PropertyName == nameof(PulseSidebarViewModel.UsePulse))
            {
                if (_pulseBeatRateControl is not null)
                    _pulseBeatRateControl.Visibility = _pulseSidebarVm.UsePulse
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
        };

        // â”€â”€ Wire IVideoEngine Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (_videoEngine is not null)
        {
            _videoEngine.AudioSamplesAvailable += OnPulseAudioSamplesAvailable;
            _videoEngine.PositionChanged += OnPulsePositionChanged;
            _videoEngine.SeekCompleted += OnPulseSeekCompleted;
        }

        // â”€â”€ Wire SuppressFunscript â†’ Auto-show Pulse Waveform â”€
        _pulseSubscriptions.Add(_eventBus.Subscribe<SuppressFunscriptEvent>(evt =>
        {
            if (evt.SuppressFunscripts)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _mainWindowViewModel.ActivateBottomPanelTab("pulse.waveform");
                    if (_bottomPanelContents.TryGetValue("pulse.waveform", out var content))
                        BottomPanelContent.Content = content;
                });
            }
        }));

        // â”€â”€ Register UI Contributions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Sidebar content
        _pulseSidebarContent = new PulseSidebarView { DataContext = _pulseSidebarVm };

        // Bottom panel tab: Pulse Waveform
        var waveformView = new WaveformPanelView { DataContext = _waveformVm };
        _bottomPanelContents["pulse.waveform"] = waveformView;
        _mainWindowViewModel.OpenBottomPanelTab("pulse.waveform", "PULSE WAVEFORM");
        TitleBar.AddBottomPanelTabMenuItem("pulse.waveform", "Pulse Waveform");

        // Status bar item
        var statusItem = _statusBarViewModel.RegisterItem("pulse.status", StatusBarAlignment.Right, 600);
        statusItem.Text = _pulseSidebarVm.StatusBarText;
        statusItem.Tooltip = "Pulse Beat Detection Status";
        statusItem.IsVisible = true;
        TitleBar.AddStatusBarMenuItem("pulse.status", "Pulse Status");

        // Control bar: BeatRate ComboBox
        var beatRateComboBox = new BeatRateComboBox { DataContext = _pulseSidebarVm };
        beatRateComboBox.Visibility = _pulseSidebarVm.UsePulse
            ? Visibility.Visible
            : Visibility.Collapsed;
        _pulseBeatRateControl = beatRateComboBox;
        VideoPlayer.AddPluginControlBarItem("pulse.beat-rate", beatRateComboBox);

        // â”€â”€ Restore Persisted State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (_settingsService.Current.PulseUsePulse)
            _pulseSidebarVm.UsePulse = true;

        // Toolbar button: Toggle Pulse
        SetupPulseToolbarButton();

        _logService.Info("Pulse feature initialized", "Pulse");
    }

    // â”€â”€ Playlists Integrated Feature â”€â”€

    /// <summary>
    /// Creates and wires all Playlist services, view models, views,
    /// and UI contribution points. This replaces the plugin-based <c>PlaylistPlugin.Activate()</c>
    /// logic with direct integration into the main window.
    /// </summary>
    private void SetupPlaylists()
    {
        // â”€â”€ Create Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var fileService = new PlaylistFileService();
        var dialogService = new Playlists.DialogService();
        _playlistProvider = new PlaylistProvider();

        // â”€â”€ Create ViewModel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _playlistVm = new PlaylistViewModel(
            fileService,
            _videoEngine!,
            _eventBus,
            dialogService,
            _settingsService,
            _toastService,
            _playlistProvider);

        // â”€â”€ Wire Status Bar Updates â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _playlistVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.StatusText))
            {
                var statusItem = _statusBarViewModel.FindItem("playlists.status");
                if (statusItem is not null)
                    statusItem.Text = _playlistVm.StatusText;
            }
        };

        // â”€â”€ Register UI Contributions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Sidebar content
        _playlistSidebarContent = new PlaylistSidebarView { DataContext = _playlistVm };

        // Status bar item
        var statusItem = _statusBarViewModel.RegisterItem("playlists.status", StatusBarAlignment.Left, 100);
        statusItem.Text = _playlistVm.StatusText;
        statusItem.Tooltip = "Playlist Status";
        statusItem.IsVisible = true;
        TitleBar.AddStatusBarMenuItem("playlists.status", "Playlist Status");

        // â”€â”€ Register Context Menu â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _contextMenuRegistry.Register(new ContextMenuEntry
        {
            Id = "playlists.add-to-playlist",
            Label = "Add to Playlist",
            Target = ContextMenuTarget.File,
            Group = "playlist",
            Order = 100,
            Handler = node =>
            {
                if (node is not null)
                    _playlistVm.AddFromFileNode(node.FullPath, node.IsDirectory);
            },
            IsEnabled = node => node is not null && node.IsVideoFile
        });

        // â”€â”€ Register accepted file extensions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        _fileExplorerViewModel.AdditionalAcceptedExtensions.Add(".vidpl");

        _logService.Info("Playlists feature initialized", "Playlists");
    }

    /// <summary>
    /// Forwards decoded audio samples from the video engine to the Pulse engine.
    /// </summary>
    private void OnPulseAudioSamplesAvailable(AudioSampleEventArgs args)
    {
        _pulseEngine?.OnAudioSamplesAvailable(args);
    }

    /// <summary>
    /// Forwards playback position from the video engine to the Pulse engine and waveform view model.
    /// </summary>
    private void OnPulsePositionChanged(TimeSpan position)
    {
        _pulseEngine?.OnPositionChanged(position.TotalMilliseconds);
        _waveformVm?.UpdateTime(position.TotalSeconds);
    }

    /// <summary>
    /// Forwards seek completed event from the video engine to the Pulse engine.
    /// </summary>
    private void OnPulseSeekCompleted()
    {
        _pulseEngine?.OnSeekCompleted();
    }

    /// <summary>
    /// Loads a BitmapImage from an embedded WPF resource URI.
    /// </summary>
    private static System.Windows.Media.Imaging.BitmapImage LoadEmbeddedResource(string relativePath)
    {
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/Vido.Views;component/{relativePath}", UriKind.Absolute);
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Publishes the current axis configurations as a <see cref="HapticAxisConfigEvent"/>
    /// so other features can read axis constraints.
    /// </summary>
    private void PublishOsr2AxisConfig()
    {
        if (_axisControlVm is null) return;

        var axisSnapshots = new List<HapticAxisSnapshot>(_axisControlVm.AxisCards.Count);
        foreach (var card in _axisControlVm.AxisCards)
        {
            axisSnapshots.Add(new HapticAxisSnapshot
            {
                Id = card.AxisId,
                Min = card.Min,
                Max = card.Max,
                Enabled = card.Enabled,
            });
        }

        _eventBus.Publish(new HapticAxisConfigEvent { Axes = axisSnapshots });
    }

    // â”€â”€ OSR2+ Event Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Handles <see cref="VideoLoadedEvent"/> â€” loads matching funscripts
    /// and syncs speed ratio.
    /// </summary>
    private void OnOsr2VideoLoaded(VideoLoadedEvent e)
    {
        try
        {
            _logService.Debug($"[OSR2+] Video loaded: {e.FilePath}", "OSR2+");

            if (_axisControlVm is null) return;

            _axisControlVm.LoadScriptsForVideo(e.FilePath);
            SyncOsr2SpeedRatio();

            _logService.Info($"Scripts loaded for: {e.FilePath}", "OSR2+");
        }
        catch (Exception ex)
        {
            _logService.Error($"[OSR2+] VideoLoaded handler error: {ex.Message}", "OSR2+");
        }
    }

    /// <summary>
    /// Handles <see cref="VideoUnloadedEvent"/> â€” clears scripts, stops TCode,
    /// and homes axes.
    /// </summary>
    private void OnOsr2VideoUnloaded(VideoUnloadedEvent e)
    {
        try
        {
            _logService.Debug("[OSR2+] Video unloaded", "OSR2+");

            if (_axisControlVm is null || _tcode is null) return;

            _axisControlVm.ClearScripts();
            _beatBarVm?.ClearBeats();
            _tcode.SetPlaying(false);
            _axisControlVm.SetVideoPlaying(false);
            _visualizerVm?.ClearAxes();

            // Recenter device to home position
            _tcode.HomeAxes();
            _logService.Debug("[OSR2+] Device recentered after video unload", "OSR2+");
        }
        catch (Exception ex)
        {
            _logService.Error($"[OSR2+] VideoUnloaded handler error: {ex.Message}", "OSR2+");
        }
    }

    /// <summary>
    /// Handles <see cref="PlaybackStateChangedEvent"/> â€” starts/stops TCode output.
    /// </summary>
    private void OnOsr2PlaybackStateChanged(PlaybackStateChangedEvent e)
    {
        try
        {
            _logService.Debug($"[OSR2+] Playback state: {e.State}", "OSR2+");

            if (_tcode is null || _axisControlVm is null) return;

            var isPlaying = e.State == PlaybackState.Playing;
            _tcode.SetPlaying(isPlaying);
            _axisControlVm.SetVideoPlaying(isPlaying);

            // Recenter device when playback stops
            if (e.State == PlaybackState.Stopped)
            {
                _tcode.HomeAxes();
                _logService.Debug("[OSR2+] Device recentered on stop", "OSR2+");
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"[OSR2+] PlaybackStateChanged handler error: {ex.Message}", "OSR2+");
        }
    }

    /// <summary>
    /// Handles <see cref="PlaybackPositionChangedEvent"/> â€” updates time for
    /// TCode interpolation, visualizer, and beat bar. Also syncs speed ratio.
    /// </summary>
    private void OnOsr2PositionChanged(PlaybackPositionChangedEvent e)
    {
        try
        {
            _tcode?.SetTime(e.Position.TotalMilliseconds);
            _visualizerVm?.UpdateTime(e.Position.TotalSeconds);
            _beatBarVm?.UpdateTime(e.Position.TotalMilliseconds);

            // Poll speed ratio from the video engine on each position tick
            SyncOsr2SpeedRatio();
        }
        catch (Exception ex)
        {
            _logService.Error($"[OSR2+] PositionChanged handler error: {ex.Message}", "OSR2+");
        }
    }

    /// <summary>
    /// Reads the current speed ratio from the video player and forwards it
    /// to <see cref="TCodeService.SetPlaybackSpeed"/> if it has changed.
    /// </summary>
    private void SyncOsr2SpeedRatio()
    {
        if (_tcode is null) return;

        var speed = _videoPlayerViewModel.PlaybackSpeed;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (speed != _lastSpeedRatio)
        {
            _lastSpeedRatio = speed;
            _tcode.SetPlaybackSpeed((float)speed);
            _logService.Debug($"[OSR2+] Playback speed updated: {speed:F2}Ã—", "OSR2+");
        }
    }

    /// <summary>
    /// Map of file extensions to handler actions for non-video file double-clicks.
    /// </summary>
    private readonly Dictionary<string, Action<FileNode>> _pluginFileHandlers = new(StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// Map of right panel ID → content UIElement.
    /// </summary>
    private readonly Dictionary<string, UIElement> _rightPanelContents = [];

    /// <summary>
    /// Map of right panel ID → display title.
    /// </summary>
    private readonly Dictionary<string, string> _rightPanelTitles = [];

    /// <summary>
    /// Switches the right panel to show the specified panel's content.
    /// </summary>
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
                : "PANEL";
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

        // Restore sidebar width â€” applied when sidebar visibility fires via OnPanelChanged
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
        // Enable immersive dark mode â€” makes DWM use dark surface (Win10 1809+)
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        var darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // Set caption color to our exact dark background (Win11 22000+, ignored on older)
        const int DWMWA_CAPTION_COLOR = 35;
        var captionColor = 0x001F1F1F; // COLORREF: 0x00BBGGRR â€” matches #1f1f1f
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

    #endregion

    // â”€â”€ App Setting Changes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ Help Menu â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            // Render the window's visual tree directly â€” pixel-perfect, no DWM border issues
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
    /// Generated in-memory â€” no packaged audio file required.
    /// </summary>
    private static void PlayScreenshotSound()
    {
        try
        {
            using var ms = new MemoryStream(s_shutterWav.Value, writable: false);
            var player = new System.Media.SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            // Sound is non-critical â€” swallow any errors
        }
    }

    /// <summary>
    /// Generates deterministic WAV data for the screenshot shutter sound.
    /// Called once via <see cref="s_shutterWav"/>.
    /// </summary>
    private static byte[] GenerateShutterWav()
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

        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bitsPerSample);
        bw.Write("data"u8);
        bw.Write(dataSize);

        var rng = new Random(42);
        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double amplitude;

            if (t < 0.005)
            {
                amplitude = t / 0.005 * 0.9;
            }
            else if (t < 0.040)
            {
                amplitude = 0.9 * Math.Exp(-(t - 0.005) * 60);
            }
            else if (t < 0.060)
            {
                amplitude = 0.02;
            }
            else if (t < 0.065)
            {
                amplitude = (t - 0.060) / 0.005 * 0.5;
            }
            else if (t < 0.090)
            {
                amplitude = 0.5 * Math.Exp(-(t - 0.065) * 80);
            }
            else
            {
                amplitude = 0.1 * Math.Exp(-(t - 0.090) * 100);
            }

            double noise = (rng.NextDouble() * 2 - 1);
            double thunk = Math.Sin(2 * Math.PI * 180 * t) * 0.4;
            double sample = (noise * 0.6 + thunk) * amplitude;

            short pcm = (short)Math.Clamp(sample * 16000, short.MinValue, short.MaxValue);
            bw.Write(pcm);
        }

        bw.Flush();
        return ms.ToArray();
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
                // No installer asset â€” fall back to opening the release page
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
            // If No â€” the installer stays in temp. Next "Check for Updates" will
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

}
