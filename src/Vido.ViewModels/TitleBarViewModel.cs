using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Windowing;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the custom title bar. Manages window state and title display.
/// </summary>
public partial class TitleBarViewModel : ObservableObject
{
    private readonly IWindowService _windowService;

    [ObservableProperty]
    private string _title = "Vido";

    [ObservableProperty]
    private bool _isMaximized;
    
    /// <summary>
    /// Creates the title bar view model bound to the given window service for minimize/maximize/close commands.
    /// </summary>
    /// <param name="windowService">Service controlling the application window state.</param>
    public TitleBarViewModel(IWindowService windowService)
    {
        _windowService = windowService;
    }

    [RelayCommand]
    private void Minimize()
    {
        _windowService.Minimize();
    }

    [RelayCommand]
    private void ToggleMaximize()
    {
        _windowService.ToggleMaximize();
        IsMaximized = _windowService.CurrentState == AppWindowState.Maximized;
    }

    [RelayCommand]
    private void Close()
    {
        _windowService.Close();
    }

    /// <summary>
    /// Synchronizes the <see cref="IsMaximized"/> property with the actual window state.
    /// Called from the view when the window state changes externally (e.g., Aero Snap).
    /// </summary>
    /// <param name="state">Current window state to synchronize with.</param>
    public void SyncWindowState(AppWindowState state)
    {
        IsMaximized = state == AppWindowState.Maximized;
    }
}
