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
    public double WindowWidth { get; set; } = 2560;
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
}
