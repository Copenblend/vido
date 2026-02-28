using System.Windows;
using Vido.Core.Windowing;

namespace Vido.Views.Services;

/// <summary>
/// WPF implementation of <see cref="IWindowService"/>.
/// Forwards window management calls to the actual WPF Window instance.
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly Window _window;
    /// <summary>
    /// Wraps a WPF Window to expose window management operations through the <see cref="IWindowService"/> interface.
    /// </summary>
    /// <param name="window">The WPF Window instance to manage.</param>
    public WindowService(Window window)
    {
        _window = window;
    }

    /// <summary>
    /// Returns the current window state (Normal, Maximized, or Minimized) mapped from the WPF WindowState.
    /// </summary>
    public AppWindowState CurrentState => _window.WindowState switch
    {
        WindowState.Maximized => AppWindowState.Maximized,
        WindowState.Minimized => AppWindowState.Minimized,
        _ => AppWindowState.Normal
    };

    /// <summary>
    /// Minimizes the window to the taskbar using the system command.
    /// </summary>
    public void Minimize()
    {
        SystemCommands.MinimizeWindow(_window);
    }

    /// <summary>
    /// Toggles the window between maximized and restored states using system commands.
    /// </summary>
    public void ToggleMaximize()
    {
        if (_window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(_window);
        }
        else
        {
            SystemCommands.MaximizeWindow(_window);
        }
    }
    
    /// <summary>
    /// Closes the window using the system close command, triggering shutdown.
    /// </summary>
    public void Close()
    {
        SystemCommands.CloseWindow(_window);
    }
}
