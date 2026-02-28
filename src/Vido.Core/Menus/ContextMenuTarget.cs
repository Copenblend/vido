namespace Vido.Core.Menus;

/// <summary>
/// Defines where a context menu item should appear in the file explorer.
/// </summary>
public enum ContextMenuTarget
{
    /// <summary>
    /// Right-click on a file node.
    /// </summary>
    File,

    /// <summary>
    /// Right-click on a folder node.
    /// </summary>
    Folder,

    /// <summary>
    /// Right-click on the empty area (background) of the explorer.
    /// </summary>
    Background
}
