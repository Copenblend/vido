using System.ComponentModel;
using Vido.Core.Layout;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="BottomPanelTabItem"/> — property change notifications
/// and default values.
/// </summary>
public class BottomPanelTabItemTests
{
    /// <summary>
    /// Verifies that Constructor sets id and title.
    /// </summary>
    [Fact]
    public void Constructor_SetsIdAndTitle()
    {
        var tab = new BottomPanelTabItem("test", "TEST");

        Assert.Equal("test", tab.Id);
        Assert.Equal("TEST", tab.Title);
    }

    /// <summary>
    /// Verifies that Constructor default values.
    /// </summary>
    [Fact]
    public void Constructor_DefaultValues()
    {
        var tab = new BottomPanelTabItem("test", "TEST");

        Assert.True(tab.IsClosable);
        Assert.False(tab.IsActive);
    }

    /// <summary>
    /// Verifies that Is Active raises property changed.
    /// </summary>
    [Fact]
    public void IsActive_RaisesPropertyChanged()
    {
        var tab = new BottomPanelTabItem("test", "TEST");
        var raised = false;
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BottomPanelTabItem.IsActive))
                raised = true;
        };

        tab.IsActive = true;

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that Is Active same value does not raise.
    /// </summary>
    [Fact]
    public void IsActive_SameValue_DoesNotRaise()
    {
        var tab = new BottomPanelTabItem("test", "TEST");
        tab.IsActive = false; // Already false

        var raised = false;
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BottomPanelTabItem.IsActive))
                raised = true;
        };

        tab.IsActive = false;

        Assert.False(raised);
    }

    /// <summary>
    /// Verifies that Is Active raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void IsActive_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var tab = new BottomPanelTabItem("test", "TEST");
        var events = new List<PropertyChangedEventArgs>();
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BottomPanelTabItem.IsActive))
                events.Add(e);
        };

        tab.IsActive = true;
        tab.IsActive = false;

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }
}