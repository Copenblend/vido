using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Layout;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the activity bar. Tracks which sidebar panel is active
/// and whether the sidebar is visible.
/// </summary>
public partial class ActivityBarViewModel : ObservableObject
{
    private readonly ISettingsService? _settingsService;

    [ObservableProperty]
    private SidebarPanelKind _activePanel = SidebarPanelKind.Explorer;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    public ActivityBarViewModel() : this(null) { }

    public ActivityBarViewModel(ISettingsService? settingsService)
    {
        _settingsService = settingsService;
        if (settingsService is not null)
            _isSidebarVisible = settingsService.Current.SidebarVisible;
    }

    /// <summary>
    /// Selects a panel. If the panel is already active, toggles the sidebar.
    /// Otherwise, switches to the requested panel and ensures the sidebar is visible.
    /// </summary>
    [RelayCommand]
    private void SelectPanel(SidebarPanelKind panel)
    {
        if (ActivePanel == panel && IsSidebarVisible)
        {
            IsSidebarVisible = false;
        }
        else
        {
            ActivePanel = panel;
            IsSidebarVisible = true;
        }
    }

    /// <summary>
    /// Sets the active panel without toggling visibility. Used for state restoration.
    /// </summary>
    public void SetActivePanel(SidebarPanelKind panel)
    {
        ActivePanel = panel;
    }

    partial void OnIsSidebarVisibleChanged(bool value)
    {
        if (_settingsService is null) return;
        _settingsService.Current.SidebarVisible = value;
        _settingsService.QueueSave();
    }

    /// <summary>
    /// Returns true if the given panel is the currently active one.
    /// Helper used by the view to determine icon highlight state.
    /// </summary>
    public bool IsPanelActive(SidebarPanelKind panel) => ActivePanel == panel;
}
