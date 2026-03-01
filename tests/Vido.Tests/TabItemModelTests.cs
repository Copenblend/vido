using System.ComponentModel;
using Vido.Core.Layout;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="TabItemModel"/> property change notifications and cached event args.
/// </summary>
public sealed class TabItemModelTests
{
    /// <summary>
    /// Verifies that IsActive change raises PropertyChanged with the expected name.
    /// </summary>
    [Fact]
    public void IsActive_Changed_RaisesPropertyChanged()
    {
        var tab = new TabItemModel("id", "title");
        PropertyChangedEventArgs? captured = null;
        tab.PropertyChanged += (_, args) => captured = args;

        tab.IsActive = true;

        Assert.NotNull(captured);
        Assert.Equal(nameof(TabItemModel.IsActive), captured!.PropertyName);
    }

    /// <summary>
    /// Verifies that IsActive raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void IsActive_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var tab = new TabItemModel("id", "title");
        var events = new List<PropertyChangedEventArgs>();
        tab.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TabItemModel.IsActive))
                events.Add(args);
        };

        tab.IsActive = true;
        tab.IsActive = false;

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }

    /// <summary>
    /// Verifies that setting IsActive to the same value does not raise PropertyChanged.
    /// </summary>
    [Fact]
    public void IsActive_SameValue_DoesNotRaisePropertyChanged()
    {
        var tab = new TabItemModel("id", "title");
        var raised = false;
        tab.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TabItemModel.IsActive))
                raised = true;
        };

        tab.IsActive = false;

        Assert.False(raised);
    }
}
