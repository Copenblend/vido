using Vido.Core.Menus;

namespace Vido.Services.Menus;

/// <summary>
/// Thread-safe registry for context menu items.
/// The app registers built-in items; plugins can add their own at activation time.
/// </summary>
public sealed class ContextMenuRegistry : IContextMenuRegistry
{
    private readonly List<ContextMenuEntry> _entries = [];
    private readonly object _lock = new();
    private IReadOnlyDictionary<ContextMenuTarget, IReadOnlyList<ContextMenuEntry>> _snapshots =
        new Dictionary<ContextMenuTarget, IReadOnlyList<ContextMenuEntry>>();

    /// <summary>
    /// Adds a context menu entry to the registry so it appears when the matching target is right-clicked.
    /// </summary>
    /// <param name="entry">The menu entry to register, including its target, group, and click handler.</param>
    public void Register(ContextMenuEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
            RebuildSnapshots();
        }
    }

    /// <summary>
    /// Removes all context menu entries whose <see cref="ContextMenuEntry.Id"/> matches the specified value.
    /// </summary>
    /// <param name="id">The identifier of the menu entry (or entries) to remove.</param>
    public void Unregister(string id)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e => e.Id == id);
            RebuildSnapshots();
        }
    }

    /// <summary>
    /// Returns all registered menu entries matching the given target, ordered by group then display order.
    /// </summary>
    /// <param name="target">The UI context (e.g. file browser, playlist) for which to retrieve menu entries.</param>
    public IReadOnlyList<ContextMenuEntry> GetEntries(ContextMenuTarget target)
    {
        var snapshots = Volatile.Read(ref _snapshots);
        return snapshots.TryGetValue(target, out var entries)
            ? entries
            : Array.Empty<ContextMenuEntry>();
    }

    private void RebuildSnapshots()
    {
        var rebuilt = _entries
            .GroupBy(e => e.Target)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ContextMenuEntry>)g
                    .OrderBy(e => e.Group, StringComparer.Ordinal)
                    .ThenBy(e => e.Order)
                    .ToList()
                    .AsReadOnly());

        Volatile.Write(ref _snapshots, rebuilt);
    }
}
