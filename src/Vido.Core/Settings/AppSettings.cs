namespace Vido.Core.Settings;

/// <summary>
/// Application settings model. Persisted to settings.json.
/// Contains user-configurable preferences with sensible defaults.
/// </summary>
public sealed class AppSettings
{
    // --- Video Playback ---

    /// <summary>
    /// Default volume level (0.0 to 1.0).
    /// </summary>
    public double Volume { get; set; } = 0.50;

    /// <summary>
    /// Whether audio output is muted.
    /// </summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>
    /// Default playback speed multiplier (e.g. 1.0 = normal, 2.0 = 2x).
    /// </summary>
    public double PlaybackSpeed { get; set; } = 1.0;

    /// <summary>
    /// Whether playback loops back to the start when the video ends.
    /// </summary>
    public bool LoopPlayback { get; set; } = false;

    // --- UI Layout ---

    /// <summary>
    /// Whether the sidebar panel is visible.
    /// </summary>
    public bool SidebarVisible { get; set; } = true;

    /// <summary>
    /// Width of the sidebar panel in pixels.
    /// </summary>
    public double SidebarWidth { get; set; } = 300;

    /// <summary>
    /// Whether the status bar is visible.
    /// </summary>
    public bool StatusBarVisible { get; set; } = true;

    /// <summary>
    /// Whether the bottom panel area is visible.
    /// </summary>
    public bool BottomPanelVisible { get; set; } = true;

    /// <summary>
    /// Whether the bottom panel is in its collapsed state.
    /// </summary>
    public bool BottomPanelCollapsed { get; set; } = false;

    /// <summary>
    /// Height of the bottom panel in pixels.
    /// </summary>
    public double BottomPanelHeight { get; set; } = 200;

    /// <summary>
    /// Whether the right panel area is visible.
    /// </summary>
    public bool RightPanelVisible { get; set; } = true;

    /// <summary>
    /// Whether the right panel is in its collapsed state.
    /// </summary>
    public bool RightPanelCollapsed { get; set; } = false;

    /// <summary>
    /// Width of the right panel in pixels.
    /// </summary>
    public double RightPanelWidth { get; set; } = 300;

    /// <summary>
    /// Whether the Log Output tab is visible in the bottom panel. Default: false (hidden).
    /// </summary>
    public bool LogOutputVisible { get; set; } = false;

    // --- File Explorer ---

    /// <summary>
    /// Whether hidden (user-removed) files are displayed in the explorer.
    /// </summary>
    public bool ShowHiddenFiles { get; set; } = false;

    // --- Screenshot ---

    /// <summary>
    /// Whether the screenshot capture button is shown in the title bar.
    /// </summary>
    public bool ScreenshotEnabled { get; set; } = false;

    /// <summary>
    /// Directory where screenshots are saved.
    /// When empty, defaults to <c>%USERPROFILE%\Pictures\Screenshots</c>.
    /// </summary>
    public string ScreenshotDirectory { get; set; } = string.Empty;

    // --- OSR2+ Connection Settings ---

    /// <summary>
    /// Connection mode for the OSR2+ device ("UDP" or "Serial").
    /// </summary>
    public string Osr2ConnectionMode { get; set; } = "UDP";

    /// <summary>
    /// UDP port for OSR2+ T-Code output.
    /// </summary>
    public int Osr2UdpPort { get; set; } = 7777;

    /// <summary>
    /// Serial COM port name for OSR2+ communication.
    /// </summary>
    public string Osr2ComPort { get; set; } = "";

    /// <summary>
    /// Baud rate for OSR2+ serial communication.
    /// </summary>
    public int Osr2BaudRate { get; set; } = 115200;

    // --- OSR2+ Output Settings ---

    /// <summary>
    /// T-Code output rate in Hz (updates per second).
    /// </summary>
    public int Osr2OutputRate { get; set; } = 100;

    /// <summary>
    /// Global timing offset in milliseconds applied to all axes.
    /// </summary>
    public int Osr2GlobalOffset { get; set; } = 0;

    // --- OSR2+ Visualizer Settings ---

    /// <summary>
    /// Visualizer display mode (e.g. "Graph", "Bars").
    /// </summary>
    public string Osr2VisualizerMode { get; set; } = "Graph";

    /// <summary>
    /// Duration of the visualizer time window in seconds.
    /// </summary>
    public int Osr2VisualizerWindowDuration { get; set; } = 60;

    // --- OSR2+ Runtime Settings ---

    /// <summary>
    /// Beat bar display mode (e.g. "Off", "OnPeak", "OnValley").
    /// </summary>
    public string Osr2BeatBarMode { get; set; } = "Off";

    /// <summary>
    /// Persisted built-in beat bar mode to restore when an external source
    /// (e.g. Pulse) is deactivated. Empty string means no fallback saved.
    /// </summary>
    public string Osr2BeatBarFallbackMode { get; set; } = "";

    /// <summary>
    /// Last active right panel ID for OSR2+ (used to restore panel state).
    /// </summary>
    public string Osr2LastRightPanel { get; set; } = "";

    // --- OSR2+ Per-Axis Settings ---

    /// <summary>
    /// Per-axis configuration for the OSR2+ device, keyed by axis ID (L0, R0, R1, R2).
    /// </summary>
    public Dictionary<string, AxisSettingsData> Osr2AxisSettings { get; set; } = AxisSettingsData.CreateDefaults();

    // --- Pulse Detection Settings ---

    /// <summary>
    /// Beat detection sensitivity multiplier for Pulse audio analysis.
    /// Higher values detect more beats (including quieter ones).
    /// </summary>
    public double PulseBeatSensitivity { get; set; } = 1.5;

    /// <summary>
    /// Whether BPM-based phase locking is enabled for Pulse beat detection.
    /// </summary>
    public bool PulseEnableBpmPhaseLock { get; set; } = true;

    // --- Pulse Visualizer Settings ---

    /// <summary>
    /// Duration of the waveform display window in seconds.
    /// </summary>
    public int PulseWaveformWindowDuration { get; set; } = 30;

    // --- Pulse Runtime Settings ---

    /// <summary>
    /// Whether the Pulse audio-driven haptic engine is active.
    /// </summary>
    public bool PulseUsePulse { get; set; } = false;

    /// <summary>
    /// Index of the selected beat rate in the Pulse rate selector.
    /// </summary>
    public int PulseBeatRateIndex { get; set; } = 0;

    /// <summary>
    /// Index of the selected beat rate for funscript generation (independent from live beat rate).
    /// </summary>
    public int PulseFunscriptBeatRateIndex { get; set; } = 0;

    // --- General ---

    /// <summary>
    /// Duration in seconds before toast notifications auto-dismiss. Clamped 1.0–10.0.
    /// </summary>
    public double ToastDurationSeconds { get; set; } = 3.0;

    // --- Playback (continued) ---

    /// <summary>
    /// Seconds of mouse inactivity before fullscreen controls auto-hide. Clamped 1.0–30.0.
    /// </summary>
    public double FullscreenAutoHideSeconds { get; set; } = 3.0;

    /// <summary>
    /// Whether to show the video filename in the fullscreen overlay.
    /// </summary>
    public bool FullscreenShowVideoName { get; set; } = true;

    /// <summary>
    /// Whether to show the resume playback prompt when re-opening a previously played video.
    /// </summary>
    public bool ResumePlaybackPrompt { get; set; } = true;

    // --- Playlist Settings ---

    /// <summary>
    /// Whether playlists are automatically saved when modified.
    /// </summary>
    public bool PlaylistAutoSave { get; set; } = false;

    /// <summary>
    /// Most recently opened playlist file paths.
    /// </summary>
    public List<string> PlaylistRecentPlaylists { get; set; } = [];

    /// <summary>
    /// Path of the last loaded playlist file (restored on startup).
    /// </summary>
    public string PlaylistLastPlaylistPath { get; set; } = "";

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
        LogOutputVisible = false;
        ShowHiddenFiles = false;
        ScreenshotEnabled = false;
        ScreenshotDirectory = string.Empty;

        // OSR2+ settings
        Osr2ConnectionMode = "UDP";
        Osr2UdpPort = 7777;
        Osr2ComPort = "";
        Osr2BaudRate = 115200;
        Osr2OutputRate = 100;
        Osr2GlobalOffset = 0;
        Osr2VisualizerMode = "Graph";
        Osr2VisualizerWindowDuration = 60;
        Osr2BeatBarMode = "Off";
        Osr2BeatBarFallbackMode = "";
        Osr2LastRightPanel = "";
        Osr2AxisSettings = AxisSettingsData.CreateDefaults();

        // Pulse settings
        PulseBeatSensitivity = 1.5;
        PulseEnableBpmPhaseLock = true;
        PulseWaveformWindowDuration = 30;
        PulseUsePulse = false;
        PulseBeatRateIndex = 0;
        PulseFunscriptBeatRateIndex = 0;

        // General settings
        ToastDurationSeconds = 3.0;

        // Playback (continued)
        FullscreenAutoHideSeconds = 3.0;
        FullscreenShowVideoName = true;
        ResumePlaybackPrompt = true;

        // Playlist settings
        PlaylistAutoSave = false;
        PlaylistRecentPlaylists = [];
        PlaylistLastPlaylistPath = "";
    }
}
