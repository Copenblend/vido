namespace Vido.Core.Settings;

/// <summary>
/// Application settings model. Persisted to settings.json.
/// Contains user-configurable preferences with sensible defaults.
/// </summary>
public sealed class AppSettings
{
    // --- Video Playback ---

    /// <summary>Default volume level (0.0 to 1.0).</summary>
    public double Volume { get; set; } = 0.50;

    /// <summary>Whether audio output is muted.</summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>Default playback speed multiplier (e.g. 1.0 = normal, 2.0 = 2x).</summary>
    public double PlaybackSpeed { get; set; } = 1.0;

    /// <summary>Whether playback loops back to the start when the video ends.</summary>
    public bool LoopPlayback { get; set; } = false;

    // --- UI Layout ---

    /// <summary>Whether the sidebar panel is visible.</summary>
    public bool SidebarVisible { get; set; } = true;

    /// <summary>Width of the sidebar panel in pixels.</summary>
    public double SidebarWidth { get; set; } = 300;

    /// <summary>Whether the status bar is visible.</summary>
    public bool StatusBarVisible { get; set; } = true;

    /// <summary>Whether the bottom panel area is visible.</summary>
    public bool BottomPanelVisible { get; set; } = true;

    /// <summary>Whether the bottom panel is in its collapsed state.</summary>
    public bool BottomPanelCollapsed { get; set; } = false;

    /// <summary>Height of the bottom panel in pixels.</summary>
    public double BottomPanelHeight { get; set; } = 200;

    /// <summary>Whether the right panel area is visible.</summary>
    public bool RightPanelVisible { get; set; } = true;

    /// <summary>Whether the right panel is in its collapsed state.</summary>
    public bool RightPanelCollapsed { get; set; } = false;

    /// <summary>Width of the right panel in pixels.</summary>
    public double RightPanelWidth { get; set; } = 300;

    // --- File Explorer ---

    /// <summary>Whether hidden (user-removed) files are displayed in the explorer.</summary>
    public bool ShowHiddenFiles { get; set; } = false;

    // --- Plugins ---

    /// <summary>Whether the Installed section in the Plugin Manager is expanded.</summary>
    public bool PluginInstalledSectionExpanded { get; set; } = true;

    /// <summary>Whether the Available section in the Plugin Manager is expanded.</summary>
    public bool PluginAvailableSectionExpanded { get; set; } = true;

    /// <summary>Additional directories to scan for plugins (besides %APPDATA%/Vido/plugins/).</summary>
    public List<string> PluginDirectories { get; set; } = [];

    /// <summary>Plugin IDs that the user has explicitly disabled.</summary>
    public List<string> DisabledPluginIds { get; set; } = [];

    /// <summary>
    /// Plugin registry URLs. The official Vido registry is always the first entry
    /// and cannot be removed. Users may add custom URLs (including <c>file://</c>
    /// paths for local development).
    /// </summary>
    public List<string> PluginRegistryUrls { get; set; } = [OfficialRegistryUrl];

    /// <summary>The official Vido plugin registry URL (always present).</summary>
    public const string OfficialRegistryUrl = "https://raw.githubusercontent.com/Copenblend/vido-plugin-registry/refs/heads/master/registry.json";

    /// <summary>The official NSFW Vido plugin registry URL.</summary>
    public const string NsfwRegistryUrl = "https://raw.githubusercontent.com/Copenblend/vido-nsfw-plugin-registry/refs/heads/main/registry.json";

    /// <summary>All official Vido registry URLs. Plugins from these registries show a verified badge.</summary>
    public static readonly HashSet<string> OfficialRegistryUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        OfficialRegistryUrl,
        NsfwRegistryUrl
    };

    /// <summary>
    /// Resolves a repository code or URL to a registry URL.
    /// Known codes (e.g. "NSFW") map to predefined URLs.
    /// Direct URLs (https://, http://, file://) are returned as-is.
    /// Returns <c>null</c> if the input is not recognised.
    /// </summary>
    public static string? ResolveRepositoryCode(string input)
    {
        if (string.Equals(input, "NSFW", StringComparison.OrdinalIgnoreCase))
            return NsfwRegistryUrl;

        if (input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        return null;
    }

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
        PluginInstalledSectionExpanded = true;
        PluginAvailableSectionExpanded = true;
        PluginDirectories = [];
        DisabledPluginIds = [];
        PluginRegistryUrls = [OfficialRegistryUrl];
    }
}
