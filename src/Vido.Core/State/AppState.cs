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
}
