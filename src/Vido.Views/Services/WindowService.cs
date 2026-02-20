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

    public WindowService(Window window)
    {
        _window = window;
    }

    public AppWindowState CurrentState => _window.WindowState switch
    {
        WindowState.Maximized => AppWindowState.Maximized,
        WindowState.Minimized => AppWindowState.Minimized,
        _ => AppWindowState.Normal
    };

    public void Minimize()
    {
        SystemCommands.MinimizeWindow(_window);
    }

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

    public void Close()
    {
        SystemCommands.CloseWindow(_window);
    }
}
