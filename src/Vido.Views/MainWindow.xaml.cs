using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Vido.Views;

/// <summary>
/// Main application window. Frameless with custom resize/move behavior
/// and Dark Modern theme matching VS Code.
/// </summary>
public partial class MainWindow : Window
{
    private const int MinWindowWidth = 800;
    private const int MinWindowHeight = 600;

    public MainWindow()
    {
        InitializeComponent();
        SetupWindowChrome();
    }

    private void SetupWindowChrome()
    {
        var chrome = new WindowChrome
        {
            CaptionHeight = 30,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        };
        WindowChrome.SetWindowChrome(this, chrome);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnforceMinimumSize();
    }

    private void EnforceMinimumSize()
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;

        if (msg == WM_GETMINMAXINFO)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var minWidth = (int)(MinWindowWidth * dpi.DpiScaleX);
            var minHeight = (int)(MinWindowHeight * dpi.DpiScaleY);

            var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.X = minWidth;
            mmi.ptMinTrackSize.Y = minHeight;
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }

        return IntPtr.Zero;
    }

    #region Native Interop Types

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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
