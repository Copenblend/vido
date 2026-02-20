using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Layout;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the activity bar. Tracks which sidebar panel is active
/// and whether the sidebar is visible.
/// </summary>
public partial class ActivityBarViewModel : ObservableObject
{
    [ObservableProperty]
    private SidebarPanelKind _activePanel = SidebarPanelKind.Explorer;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

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
    /// Returns true if the given panel is the currently active one.
    /// Helper used by the view to determine icon highlight state.
    /// </summary>
    public bool IsPanelActive(SidebarPanelKind panel) => ActivePanel == panel;
}
