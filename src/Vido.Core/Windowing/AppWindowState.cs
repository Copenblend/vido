namespace Vido.Core.Windowing;

/// <summary>
/// Defines the possible states of the application window.
/// Platform-agnostic equivalent of System.Windows.WindowState.
/// </summary>
public enum AppWindowState
{
    /// <summary>Window is in its normal (restored) state.</summary>
    Normal,

    /// <summary>Window is minimized to the taskbar.</summary>
    Minimized,

    /// <summary>Window fills the screen's working area.</summary>
    Maximized
}
