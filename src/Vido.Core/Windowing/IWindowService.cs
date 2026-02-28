namespace Vido.Core.Windowing;

/// <summary>
/// Abstracts window management operations so ViewModels remain platform-agnostic.
/// Implemented by the WPF layer to forward calls to the actual Window.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Minimizes the application window.
    /// </summary>
    void Minimize();

    /// <summary>
    /// Toggles between maximized and restored window states.
    /// </summary>
    void ToggleMaximize();

    /// <summary>
    /// Closes the application window.
    /// </summary>
    void Close();

    /// <summary>
    /// Gets the current window state (Normal, Minimized, or Maximized).
    /// </summary>
    AppWindowState CurrentState { get; }
}
