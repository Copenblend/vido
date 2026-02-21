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

    /// <inheritdoc />
    public void Register(ContextMenuEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    /// <inheritdoc />
    public void Unregister(string id)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e => e.Id == id);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContextMenuEntry> GetEntries(ContextMenuTarget target)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Target == target)
                .OrderBy(e => e.Group, StringComparer.Ordinal)
                .ThenBy(e => e.Order)
                .ToList()
                .AsReadOnly();
        }
    }
}
