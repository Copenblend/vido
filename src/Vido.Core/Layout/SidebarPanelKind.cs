namespace Vido.Core.Layout;

/// <summary>
/// Identifies the sidebar panel types available in the activity bar.
/// </summary>
public enum SidebarPanelKind
{
    /// <summary>
    /// File explorer tree view.
    /// </summary>
    Explorer,

    /// <summary>
    /// Playlist management panel.
    /// </summary>
    Playlists,

    /// <summary>
    /// OSR2+ haptic device configuration panel.
    /// </summary>
    Osr2Plus,

    /// <summary>
    /// Pulse audio-driven haptic engine panel.
    /// </summary>
    Pulse,

    /// <summary>
    /// Application settings.
    /// </summary>
    Settings
}
