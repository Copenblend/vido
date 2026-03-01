using Vido.Core.FileSystem;
using Vido.Core.Menus;
using Vido.Services.Menus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="ContextMenuRegistry"/>.
/// </summary>
public sealed class ContextMenuRegistryTests
{
    private readonly ContextMenuRegistry _sut = new();

    /// <summary>
    /// Verifies that Get Entries returns empty when no registrations.
    /// </summary>
    [Fact]
    public void GetEntries_ReturnsEmpty_WhenNoRegistrations()
    {
        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.Empty(entries);
    }

    /// <summary>
    /// Verifies that Register entry appears in get entries.
    /// </summary>
    [Fact]
    public void Register_EntryAppearsInGetEntries()
    {
        var entry = MakeEntry("test-1", ContextMenuTarget.File);
        _sut.Register(entry);

        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.Single(entries);
        Assert.Equal("test-1", entries[0].Id);
    }

    /// <summary>
    /// Verifies that Get Entries filtersby target.
    /// </summary>
    [Fact]
    public void GetEntries_FiltersbyTarget()
    {
        _sut.Register(MakeEntry("file-1", ContextMenuTarget.File));
        _sut.Register(MakeEntry("folder-1", ContextMenuTarget.Folder));
        _sut.Register(MakeEntry("bg-1", ContextMenuTarget.Background));

        Assert.Single(_sut.GetEntries(ContextMenuTarget.File));
        Assert.Single(_sut.GetEntries(ContextMenuTarget.Folder));
        Assert.Single(_sut.GetEntries(ContextMenuTarget.Background));
    }

    /// <summary>
    /// Verifies that Unregister removes entry.
    /// </summary>
    [Fact]
    public void Unregister_RemovesEntry()
    {
        _sut.Register(MakeEntry("to-remove", ContextMenuTarget.File));
        Assert.Single(_sut.GetEntries(ContextMenuTarget.File));

        _sut.Unregister("to-remove");
        Assert.Empty(_sut.GetEntries(ContextMenuTarget.File));
    }

    /// <summary>
    /// Verifies that Unregister no op when id not found.
    /// </summary>
    [Fact]
    public void Unregister_NoOp_WhenIdNotFound()
    {
        _sut.Register(MakeEntry("keep", ContextMenuTarget.File));
        _sut.Unregister("nonexistent");

        Assert.Single(_sut.GetEntries(ContextMenuTarget.File));
    }

    /// <summary>
    /// Verifies that Get Entries orders by group then order.
    /// </summary>
    [Fact]
    public void GetEntries_OrdersByGroupThenOrder()
    {
        _sut.Register(MakeEntry("b-2", ContextMenuTarget.File, group: "b", order: 2));
        _sut.Register(MakeEntry("a-1", ContextMenuTarget.File, group: "a", order: 1));
        _sut.Register(MakeEntry("b-1", ContextMenuTarget.File, group: "b", order: 1));
        _sut.Register(MakeEntry("a-2", ContextMenuTarget.File, group: "a", order: 2));

        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.Equal(4, entries.Count);
        Assert.Equal("a-1", entries[0].Id);
        Assert.Equal("a-2", entries[1].Id);
        Assert.Equal("b-1", entries[2].Id);
        Assert.Equal("b-2", entries[3].Id);
    }

    /// <summary>
    /// Verifies that Get Entries returns read only copy.
    /// </summary>
    [Fact]
    public void GetEntries_ReturnsReadOnlyCopy()
    {
        _sut.Register(MakeEntry("test", ContextMenuTarget.File));

        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.IsAssignableFrom<IReadOnlyList<ContextMenuEntry>>(entries);
    }

    /// <summary>
    /// Verifies that GetEntries returns the same snapshot reference when unchanged.
    /// </summary>
    [Fact]
    public void GetEntries_ReturnsSameSnapshotReference_WhenUnchanged()
    {
        _sut.Register(MakeEntry("test", ContextMenuTarget.File));

        var a = _sut.GetEntries(ContextMenuTarget.File);
        var b = _sut.GetEntries(ContextMenuTarget.File);

        Assert.Same(a, b);
    }

    /// <summary>
    /// Verifies that Register rebuilds snapshots and changes returned reference.
    /// </summary>
    [Fact]
    public void Register_RebuildsSnapshotReference()
    {
        _sut.Register(MakeEntry("a", ContextMenuTarget.File));
        var before = _sut.GetEntries(ContextMenuTarget.File);

        _sut.Register(MakeEntry("b", ContextMenuTarget.File));
        var after = _sut.GetEntries(ContextMenuTarget.File);

        Assert.NotSame(before, after);
    }

    /// <summary>
    /// Verifies that Unregister rebuilds snapshots and changes returned reference.
    /// </summary>
    [Fact]
    public void Unregister_RebuildsSnapshotReference()
    {
        _sut.Register(MakeEntry("a", ContextMenuTarget.File));
        _sut.Register(MakeEntry("b", ContextMenuTarget.File));
        var before = _sut.GetEntries(ContextMenuTarget.File);

        _sut.Unregister("a");
        var after = _sut.GetEntries(ContextMenuTarget.File);

        Assert.NotSame(before, after);
    }

    /// <summary>
    /// Verifies that Register multiple entries same target.
    /// </summary>
    [Fact]
    public void Register_MultipleEntriesSameTarget()
    {
        _sut.Register(MakeEntry("f1", ContextMenuTarget.File));
        _sut.Register(MakeEntry("f2", ContextMenuTarget.File));
        _sut.Register(MakeEntry("f3", ContextMenuTarget.File));

        Assert.Equal(3, _sut.GetEntries(ContextMenuTarget.File).Count);
    }

    /// <summary>
    /// Verifies that Handler is invoked.
    /// </summary>
    [Fact]
    public void Handler_IsInvoked()
    {
        FileNode? received = null;
        var entry = new ContextMenuEntry
        {
            Id = "handler-test",
            Label = "Test",
            Target = ContextMenuTarget.File,
            Handler = n => received = n
        };

        _sut.Register(entry);
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        _sut.GetEntries(ContextMenuTarget.File)[0].Handler(node);

        Assert.NotNull(received);
        Assert.Equal(@"C:\test.mp4", received!.FullPath);
    }

    /// <summary>
    /// Verifies that Is Enabled defaults to always true.
    /// </summary>
    [Fact]
    public void IsEnabled_DefaultsToAlwaysTrue()
    {
        var entry = MakeEntry("test", ContextMenuTarget.File);
        var node = new FileNode(@"C:\test.txt", isDirectory: false);
        Assert.True(entry.IsEnabled(node));
        Assert.True(entry.IsEnabled(null));
    }

    private static ContextMenuEntry MakeEntry(
        string id, ContextMenuTarget target,
        string group = "default", int order = 0)
    {
        return new ContextMenuEntry
        {
            Id = id,
            Label = id,
            Target = target,
            Group = group,
            Order = order,
            Handler = _ => { }
        };
    }
}