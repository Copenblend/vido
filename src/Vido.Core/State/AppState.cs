namespace Vido.Core.State;

/// <summary>
/// Application state that is restored between sessions.
/// Persisted to state.json. Distinct from settings — state is
/// implicit (window position, last file) rather than user-configured.
/// </summary>
public sealed class AppState
{
    // --- Window Geometry ---
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 720;
    public bool IsMaximized { get; set; } = false;

    // --- Last Session ---
    public string? LastOpenFolder { get; set; }
    public string? LastVideoPath { get; set; }
    public double LastVideoPosition { get; set; } = 0;

    // --- Active Panel ---
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

    /// <summary>Maximum number of recent files to retain.</summary>
    public const int MaxRecentFiles = 10;

    /// <summary>
    /// Adds a file to the recent files list, moving it to the front if it already exists.
    /// Trims the list to <see cref="MaxRecentFiles"/>.
    /// </summary>
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
