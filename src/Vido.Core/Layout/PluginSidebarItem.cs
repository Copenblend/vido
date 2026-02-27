namespace Vido.Core.Layout;

/// <summary>
/// Represents a plugin-contributed sidebar panel entry in the activity bar.
/// Tracks the panel's ID and its display order (used for drag-and-drop reordering).
/// </summary>
public sealed class PluginSidebarItem
{
    /// <summary>Full sidebar panel ID (e.g. "plugin.com.vido.osr2-plus.beatbar").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display order index. Lower values appear higher in the activity bar.</summary>
    public int Order { get; set; }
}
