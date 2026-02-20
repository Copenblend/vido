using Vido.Core.FileSystem;

namespace Vido.Core.Menus;

/// <summary>
/// Defines the target context for a context menu item.
/// </summary>
public enum ContextMenuTarget
{
    /// <summary>Right-click on a file node.</summary>
    File,

    /// <summary>Right-click on a folder node.</summary>
    Folder,

    /// <summary>Right-click on the empty area (background) of the explorer.</summary>
    Background
}

/// <summary>
/// A registered context menu item contributed by the app or a plugin.
/// </summary>
public sealed class ContextMenuEntry
{
    /// <summary>Unique identifier for this menu entry.</summary>
    public required string Id { get; init; }

    /// <summary>Display text shown in the menu.</summary>
    public required string Label { get; init; }

    /// <summary>Which context(s) this item appears in.</summary>
    public required ContextMenuTarget Target { get; init; }

    /// <summary>Sort order within the group (lower = higher).</summary>
    public int Order { get; init; }

    /// <summary>Optional group key used to insert separators between groups.</summary>
    public string Group { get; init; } = "default";

    /// <summary>Optional keyboard shortcut hint text.</summary>
    public string? InputGestureText { get; init; }

    /// <summary>
    /// Callback invoked when the item is clicked. Receives the clicked <see cref="FileNode"/>
    /// (or <c>null</c> for background clicks).
    /// </summary>
    public required Action<FileNode?> Handler { get; init; }

    /// <summary>
    /// Optional predicate to determine if the item is enabled for the given node.
    /// Returns <c>true</c> by default (always enabled).
    /// </summary>
    public Func<FileNode?, bool> IsEnabled { get; init; } = _ => true;
}

/// <summary>
/// Registry for context menu items. Items can be added by the app core
/// or by plugins at activation time.
/// </summary>
public interface IContextMenuRegistry
{
    /// <summary>Registers a context menu entry.</summary>
    void Register(ContextMenuEntry entry);

    /// <summary>Removes a previously registered entry by its <see cref="ContextMenuEntry.Id"/>.</summary>
    void Unregister(string id);

    /// <summary>
    /// Gets all entries matching the given <paramref name="target"/>, ordered by
    /// <see cref="ContextMenuEntry.Group"/> then <see cref="ContextMenuEntry.Order"/>.
    /// </summary>
    IReadOnlyList<ContextMenuEntry> GetEntries(ContextMenuTarget target);
}
