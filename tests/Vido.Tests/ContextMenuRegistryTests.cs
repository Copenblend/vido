using Vido.Core.FileSystem;
using Vido.Core.Menus;
using Vido.Services.Menus;
using Xunit;

namespace Vido.Tests;

public sealed class ContextMenuRegistryTests
{
    private readonly ContextMenuRegistry _sut = new();

    [Fact]
    public void GetEntries_ReturnsEmpty_WhenNoRegistrations()
    {
        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.Empty(entries);
    }

    [Fact]
    public void Register_EntryAppearsInGetEntries()
    {
        var entry = MakeEntry("test-1", ContextMenuTarget.File);
        _sut.Register(entry);

        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.Single(entries);
        Assert.Equal("test-1", entries[0].Id);
    }

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

    [Fact]
    public void Unregister_RemovesEntry()
    {
        _sut.Register(MakeEntry("to-remove", ContextMenuTarget.File));
        Assert.Single(_sut.GetEntries(ContextMenuTarget.File));

        _sut.Unregister("to-remove");
        Assert.Empty(_sut.GetEntries(ContextMenuTarget.File));
    }

    [Fact]
    public void Unregister_NoOp_WhenIdNotFound()
    {
        _sut.Register(MakeEntry("keep", ContextMenuTarget.File));
        _sut.Unregister("nonexistent");

        Assert.Single(_sut.GetEntries(ContextMenuTarget.File));
    }

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

    [Fact]
    public void GetEntries_ReturnsReadOnlyCopy()
    {
        _sut.Register(MakeEntry("test", ContextMenuTarget.File));

        var entries = _sut.GetEntries(ContextMenuTarget.File);
        Assert.IsAssignableFrom<IReadOnlyList<ContextMenuEntry>>(entries);
    }

    [Fact]
    public void Register_MultipleEntriesSameTarget()
    {
        _sut.Register(MakeEntry("f1", ContextMenuTarget.File));
        _sut.Register(MakeEntry("f2", ContextMenuTarget.File));
        _sut.Register(MakeEntry("f3", ContextMenuTarget.File));

        Assert.Equal(3, _sut.GetEntries(ContextMenuTarget.File).Count);
    }

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
