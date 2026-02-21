namespace Vido.Core.Plugin;

/// <summary>
/// Defines expected icon dimensions for plugin-provided icons.
/// Plugins should provide icons at these sizes; the UI will scale down
/// oversized images but may lose quality if the source is far from these sizes.
/// </summary>
public static class PluginIconConstants
{
    /// <summary>
    /// Maximum icon dimension for sidebar / activity bar icons (24×24 px).
    /// </summary>
    public const int SidebarIconSize = 24;

    /// <summary>
    /// Maximum icon dimension for file explorer file-type icons (16×16 px).
    /// </summary>
    public const int FileIconSize = 16;

    /// <summary>
    /// Maximum icon dimension for toolbar buttons (16×16 px).
    /// </summary>
    public const int ToolbarIconSize = 16;
}
