namespace Vido.Core.Settings;

/// <summary>
/// Application settings model. Persisted to settings.json.
/// Contains user-configurable preferences with sensible defaults.
/// </summary>
public sealed class AppSettings
{
    // --- Video Playback ---
    public double Volume { get; set; } = 0.50;
    public bool IsMuted { get; set; } = false;
    public double PlaybackSpeed { get; set; } = 1.0;
    public bool LoopPlayback { get; set; } = false;

    // --- UI Layout ---
    public bool SidebarVisible { get; set; } = true;
    public double SidebarWidth { get; set; } = 300;
    public bool StatusBarVisible { get; set; } = true;
    public bool BottomPanelVisible { get; set; } = true;
    public bool BottomPanelCollapsed { get; set; } = false;
    public double BottomPanelHeight { get; set; } = 200;
    public bool RightPanelVisible { get; set; } = true;
    public bool RightPanelCollapsed { get; set; } = false;
    public double RightPanelWidth { get; set; } = 300;

    // --- File Explorer ---
    public bool ShowHiddenFiles { get; set; } = false;

    /// <summary>
    /// Resets every property to its default value.
    /// Call after tests that mutate settings to prevent pollution.
    /// </summary>
    public void ResetToDefaults()
    {
        Volume = 0.50;
        IsMuted = false;
        PlaybackSpeed = 1.0;
        LoopPlayback = false;
        SidebarVisible = true;
        SidebarWidth = 300;
        StatusBarVisible = true;
        BottomPanelVisible = true;
        BottomPanelCollapsed = false;
        BottomPanelHeight = 200;
        RightPanelVisible = true;
        RightPanelCollapsed = false;
        RightPanelWidth = 300;
        ShowHiddenFiles = false;
    }
}
