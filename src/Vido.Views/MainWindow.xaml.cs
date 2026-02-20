using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Vido.Core.Layout;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.Core.Windowing;
using Vido.ViewModels;
using Vido.Views.Panels;
using Vido.Views.Services;

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
    private readonly FileExplorerViewModel _fileExplorerViewModel;

    private TitleBarViewModel? _titleBarViewModel;
    private ActivityBarViewModel? _activityBarViewModel;
    private SidebarViewModel? _sidebarViewModel;
    private FileExplorerPanel? _fileExplorerPanel;

    public MainWindow(
        IStateService stateService,
        ISettingsService settingsService,
        FileExplorerViewModel fileExplorerViewModel)
    {
        _stateService = stateService;
        _settingsService = settingsService;
        _fileExplorerViewModel = fileExplorerViewModel;

        InitializeComponent();
        SetupWindowChrome();
        SetupTitleBar();
        SetupLayout();
        SetupFileExplorer();
        RestoreWindowState();
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
        _activityBarViewModel = new ActivityBarViewModel();
        _sidebarViewModel = new SidebarViewModel();

        ActivityBar.DataContext = _activityBarViewModel;
        Sidebar.DataContext = _sidebarViewModel;

        // Initialize visual states
        ActivityBar.UpdateActiveStates();
    }

    private void SetupFileExplorer()
    {
        _fileExplorerPanel = new FileExplorerPanel
        {
            DataContext = _fileExplorerViewModel
        };

        // Wire title bar folder events
        TitleBar.FolderOpened += OnFolderOpened;
        TitleBar.FolderClosed += OnFolderClosed;
        TitleBar.FolderRescanned += OnFolderRescanned;

        // Wire the "Open Folder" button inside the explorer panel
        _fileExplorerPanel.OpenFolderRequested += ShowOpenFolderDialog;

        // Subscribe to VM changes to update Close Folder menu state
        _fileExplorerViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FileExplorerViewModel.HasFolderOpen))
                TitleBar.SetCloseFolderEnabled(_fileExplorerViewModel.HasFolderOpen);
        };

        // Set the explorer panel as the initial sidebar content
        Sidebar.SetPanelContent(_fileExplorerPanel);

        // Restore last opened folder from state
        _fileExplorerViewModel.RestoreLastFolder();
        TitleBar.SetCloseFolderEnabled(_fileExplorerViewModel.HasFolderOpen);
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

    private void OnFolderOpened(string path)
    {
        _fileExplorerViewModel.OpenFolder(path);

        // Ensure sidebar is visible and Explorer panel is active
        if (_activityBarViewModel is not null)
        {
            _activityBarViewModel.ActivePanel = SidebarPanelKind.Explorer;
            _activityBarViewModel.IsSidebarVisible = true;
            OnPanelChanged(this, new RoutedEventArgs());
        }
    }

    private void OnFolderClosed()
    {
        _fileExplorerViewModel.CloseFolder();
    }

    private void OnFolderRescanned()
    {
        _fileExplorerViewModel.RescanFolder();
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
            SidebarColumn.Width = new GridLength(300);
            SidebarColumn.MinWidth = 170;
            SidebarColumn.MaxWidth = 600;
            _sidebarViewModel.SetPanel(_activityBarViewModel.ActivePanel);

            // Switch panel content based on active panel
            switch (_activityBarViewModel.ActivePanel)
            {
                case SidebarPanelKind.Explorer:
                    Sidebar.SetPanelContent(_fileExplorerPanel);
                    break;
                default:
                    // Future panels (Extensions, Settings) will be added here
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
        SaveWindowState();
        await _stateService.SaveAsync();
        await _settingsService.SaveAsync();
        base.OnClosing(e);
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
    /// Captures current window geometry into AppState for persistence.
    /// Uses RestoreBounds when maximized to save the normal-state geometry.
    /// </summary>
    private void SaveWindowState()
    {
        var state = _stateService.Current;
        state.IsMaximized = WindowState == WindowState.Maximized;

        // Save the restore bounds (normal position/size), not the maximized dimensions
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        state.WindowLeft = bounds.Left;
        state.WindowTop = bounds.Top;
        state.WindowWidth = bounds.Width;
        state.WindowHeight = bounds.Height;
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
                    var work = mi.rcWork;
                    var monRect = mi.rcMonitor;

                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMaxPosition.X = work.Left - monRect.Left;
                    mmi.ptMaxPosition.Y = work.Top - monRect.Top;
                    mmi.ptMaxSize.X = work.Right - work.Left;
                    mmi.ptMaxSize.Y = work.Bottom - work.Top;

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
}
