namespace Vido.Core.Menus;

/// <summary>
/// Registry for context menu items. Items can be added by the app core
/// or by plugins at activation time.
/// </summary>
public interface IContextMenuRegistry
{
    /// <summary>
    /// Registers a context menu entry.
    /// </summary>
    void Register(ContextMenuEntry entry);

    /// <summary>
    /// Removes a previously registered entry by its <see cref="ContextMenuEntry.Id"/>.
    /// </summary>
    /// <param name="id">The unique identifier of the context menu entry to remove.</param>
    void Unregister(string id);

    /// <summary>
    /// Gets all entries matching the given <paramref name="target"/>, ordered by
    /// <see cref="ContextMenuEntry.Group"/> then <see cref="ContextMenuEntry.Order"/>.
    /// </summary>
    IReadOnlyList<ContextMenuEntry> GetEntries(ContextMenuTarget target);
}
