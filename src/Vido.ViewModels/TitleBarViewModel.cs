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
    public void SyncWindowState(AppWindowState state)
    {
        IsMaximized = state == AppWindowState.Maximized;
    }
}
