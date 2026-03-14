using CommunityToolkit.Mvvm.ComponentModel;
using Vido.Core.Layout;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the sidebar panel. Derives its header text from the active panel kind.
/// Panel content will be populated in later tickets (vi-006 for Explorer, etc.).
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    [ObservableProperty]
    private string _headerText = "EXPLORER";

    /// <summary>
    /// Updates the header to match the currently selected panel.
    /// </summary>
    /// <param name="panel">Built-in panel kind to derive the header text from.</param>
    public void SetPanel(SidebarPanelKind panel)
    {
        HeaderText = panel switch
        {
            SidebarPanelKind.Explorer => "EXPLORER",
            SidebarPanelKind.Playlists => "PLAYLISTS",
            SidebarPanelKind.Osr2Plus => "OSR2+",
            SidebarPanelKind.Settings => "SETTINGS",
            _ => "EXPLORER"
        };
    }

    /// <summary>
    /// Sets the header to a custom title. Used for plugin sidebar panels
    /// that don't correspond to a built-in <see cref="SidebarPanelKind"/>.
    /// </summary>
    /// <param name="headerText">Custom header text to display.</param>
    public void SetPanel(string headerText)
    {
        HeaderText = headerText;
    }
}
