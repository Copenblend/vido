using NSubstitute;
using Vido.Core.Plugin;
using Vido.PluginHost;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for playlist provider registration, unregistration, and query
/// in <see cref="ContributionRegistry"/>.
/// </summary>
public class PlaylistProviderRegistryTests
{
    private readonly ContributionRegistry _registry = new();

    [Fact]
    public void GetPlaylistProvider_ReturnsNull_WhenNoneRegistered()
    {
        Assert.Null(_registry.GetPlaylistProvider());
    }

    [Fact]
    public void RegisterPlaylistProvider_MakesItAvailable()
    {
        var provider = Substitute.For<IPlaylistProvider>();

        _registry.RegisterPlaylistProvider("plugin1", provider);

        Assert.Same(provider, _registry.GetPlaylistProvider());
    }

    [Fact]
    public void UnregisterPlaylistProvider_RemovesIt()
    {
        var provider = Substitute.For<IPlaylistProvider>();
        _registry.RegisterPlaylistProvider("plugin1", provider);

        _registry.UnregisterPlaylistProvider("plugin1");

        Assert.Null(_registry.GetPlaylistProvider());
    }

    [Fact]
    public void UnregisterPlaylistProvider_NoOp_WhenDifferentPlugin()
    {
        var provider = Substitute.For<IPlaylistProvider>();
        _registry.RegisterPlaylistProvider("plugin1", provider);

        _registry.UnregisterPlaylistProvider("plugin2");

        Assert.Same(provider, _registry.GetPlaylistProvider());
    }

    [Fact]
    public void RegisterPlaylistProvider_LastWriteWins()
    {
        var provider1 = Substitute.For<IPlaylistProvider>();
        var provider2 = Substitute.For<IPlaylistProvider>();

        _registry.RegisterPlaylistProvider("plugin1", provider1);
        _registry.RegisterPlaylistProvider("plugin2", provider2);

        Assert.Same(provider2, _registry.GetPlaylistProvider());
    }

    [Fact]
    public void UnregisterAll_RemovesPlaylistProvider()
    {
        var provider = Substitute.For<IPlaylistProvider>();
        _registry.RegisterPlaylistProvider("plugin1", provider);

        _registry.UnregisterAll("plugin1");

        Assert.Null(_registry.GetPlaylistProvider());
    }

    [Fact]
    public void UnregisterAll_DoesNotRemoveOtherPluginProvider()
    {
        var provider = Substitute.For<IPlaylistProvider>();
        _registry.RegisterPlaylistProvider("plugin1", provider);

        _registry.UnregisterAll("plugin2");

        Assert.Same(provider, _registry.GetPlaylistProvider());
    }
}
