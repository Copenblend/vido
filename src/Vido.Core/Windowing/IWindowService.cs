namespace Vido.Core.Windowing;

/// <summary>
/// Abstracts window management operations so ViewModels remain platform-agnostic.
/// Implemented by the WPF layer to forward calls to the actual Window.
/// </summary>
public interface IWindowService
{
    void Minimize();
    void ToggleMaximize();
    void Close();
    AppWindowState CurrentState { get; }
}
