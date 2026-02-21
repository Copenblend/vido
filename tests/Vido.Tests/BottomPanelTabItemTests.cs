using Vido.Core.Layout;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="BottomPanelTabItem"/> — property change notifications
/// and default values.
/// </summary>
public class BottomPanelTabItemTests
{
    [Fact]
    public void Constructor_SetsIdAndTitle()
    {
        var tab = new BottomPanelTabItem("test", "TEST");

        Assert.Equal("test", tab.Id);
        Assert.Equal("TEST", tab.Title);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        var tab = new BottomPanelTabItem("test", "TEST");

        Assert.True(tab.IsClosable);
        Assert.False(tab.IsActive);
    }

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
}
