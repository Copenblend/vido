namespace Vido.Core.State;

/// <summary>
/// Application state that is restored between sessions.
/// Persisted to state.json. Distinct from settings — state is
/// implicit (window position, last file) rather than user-configured.
/// </summary>
public sealed class AppState
{
    // --- Window Geometry ---

    /// <summary>
    /// Left edge of the window position in device-independent pixels.
    /// </summary>
    public double WindowLeft { get; set; } = double.NaN;

    /// <summary>
    /// Top edge of the window position in device-independent pixels.
    /// </summary>
    public double WindowTop { get; set; } = double.NaN;

    /// <summary>
    /// Width of the window in device-independent pixels.
    /// </summary>
    public double WindowWidth { get; set; } = 1280;

    /// <summary>
    /// Height of the window in device-independent pixels.
    /// </summary>
    public double WindowHeight { get; set; } = 720;

    /// <summary>
    /// Whether the window was maximized when last closed.
    /// </summary>
    public bool IsMaximized { get; set; } = false;

    // --- Last Session ---

    /// <summary>
    /// Path of the last folder opened in the file explorer.
    /// </summary>
    public string? LastOpenFolder { get; set; }

    /// <summary>
    /// Path of the last video file that was playing.
    /// </summary>
    public string? LastVideoPath { get; set; }

    /// <summary>
    /// Playback position (in seconds) of the last video when the app closed.
    /// </summary>
    public double LastVideoPosition { get; set; } = 0;

    // --- Active Panel ---

    /// <summary>
    /// Name of the sidebar panel that was active (e.g. "Explorer", "Extensions").
    /// </summary>
    public string ActiveSidebarPanel { get; set; } = "Explorer";

    // --- Hidden Files ---
    /// <summary>
    /// Full paths of files the user has "removed" from the explorer view.
    /// These files are hidden (not deleted from disk) and persist across restarts.
    /// </summary>
    public List<string> HiddenFiles { get; set; } = [];

    // --- Recent Files ---
    /// <summary>
    /// Most recently opened video file paths, newest first. Capped at 10.
    /// </summary>
    public List<string> RecentFiles { get; set; } = [];

    /// <summary>
    /// Maximum number of recent files to retain.
    /// </summary>
    public const int MaxRecentFiles = 10;

    /// <summary>
    /// Adds a file to the recent files list, moving it to the front if it already exists.
    /// Trims the list to <see cref="MaxRecentFiles"/>.
    /// </summary>
    /// <param name="filePath">Absolute path of the video file to record as recently opened.</param>
    public void AddRecentFile(string filePath)
    {
        RecentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, filePath);
        if (RecentFiles.Count > MaxRecentFiles)
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
    }

    /// <summary>
    /// Resets every property to its default value.
    /// Call after tests that mutate state to prevent pollution.
    /// </summary>
    public void ResetToDefaults()
    {
        WindowLeft = double.NaN;
        WindowTop = double.NaN;
        WindowWidth = 1280;
        WindowHeight = 720;
        IsMaximized = false;
        LastOpenFolder = null;
        LastVideoPath = null;
        LastVideoPosition = 0;
        ActiveSidebarPanel = "Explorer";
        HiddenFiles = [];
        RecentFiles = [];
    }
}
