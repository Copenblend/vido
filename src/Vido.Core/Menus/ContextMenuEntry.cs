using Vido.Core.FileSystem;

namespace Vido.Core.Menus;

/// <summary>
/// Represents one registered context menu item contributed by the core app or a plugin.
/// </summary>
public sealed class ContextMenuEntry
{
    /// <summary>
    /// Gets the unique identifier for this menu entry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display text shown in the menu.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the explorer target context in which this item appears.
    /// </summary>
    public required ContextMenuTarget Target { get; init; }

    /// <summary>
    /// Gets the sort order within the target context (lower values appear first).
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the optional group key used to insert separators between groups.
    /// </summary>
    public string Group { get; init; } = "default";

    /// <summary>
    /// Gets optional keyboard shortcut hint text displayed in the menu.
    /// </summary>
    public string? InputGestureText { get; init; }

    /// <summary>
    /// Gets the callback invoked when the item is clicked.
    /// Receives the clicked file node, or <c>null</c> for background clicks.
    /// </summary>
    public required Action<FileNode?> Handler { get; init; }

    /// <summary>
    /// Gets the optional predicate that determines whether the item is enabled for a node.
    /// Defaults to always enabled.
    /// </summary>
    public Func<FileNode?, bool> IsEnabled { get; init; } = _ => true;
}
