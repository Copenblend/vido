using System.ComponentModel;
using Vido.Core.Layout;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="StatusBarItem"/> property change notifications and cached event args.
/// </summary>
public sealed class StatusBarItemTests
{
    /// <summary>
    /// Verifies that Text change raises PropertyChanged with the expected property name.
    /// </summary>
    [Fact]
    public void Text_Changed_RaisesPropertyChanged()
    {
        var item = new StatusBarItem("id", StatusBarAlignment.Left, 0);
        PropertyChangedEventArgs? captured = null;
        item.PropertyChanged += (_, args) => captured = args;

        item.Text = "updated";

        Assert.NotNull(captured);
        Assert.Equal(nameof(StatusBarItem.Text), captured!.PropertyName);
    }

    /// <summary>
    /// Verifies that Text raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void Text_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var item = new StatusBarItem("id", StatusBarAlignment.Left, 0);
        var events = new List<PropertyChangedEventArgs>();
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarItem.Text))
                events.Add(args);
        };

        item.Text = "one";
        item.Text = "two";

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }

    /// <summary>
    /// Verifies that Tooltip raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void Tooltip_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var item = new StatusBarItem("id", StatusBarAlignment.Left, 0);
        var events = new List<PropertyChangedEventArgs>();
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarItem.Tooltip))
                events.Add(args);
        };

        item.Tooltip = "one";
        item.Tooltip = "two";

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }

    /// <summary>
    /// Verifies that IsVisible raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void IsVisible_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var item = new StatusBarItem("id", StatusBarAlignment.Left, 0);
        var events = new List<PropertyChangedEventArgs>();
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarItem.IsVisible))
                events.Add(args);
        };

        item.IsVisible = false;
        item.IsVisible = true;

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }

    /// <summary>
    /// Verifies that ContentView and HasContentView each raise PropertyChanged with cached event args.
    /// </summary>
    [Fact]
    public void ContentView_Changed_UsesCachedPropertyChangedEventArgs_ForBothProperties()
    {
        var item = new StatusBarItem("id", StatusBarAlignment.Left, 0);
        var contentViewEvents = new List<PropertyChangedEventArgs>();
        var hasContentViewEvents = new List<PropertyChangedEventArgs>();

        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarItem.ContentView))
                contentViewEvents.Add(args);
            if (args.PropertyName == nameof(StatusBarItem.HasContentView))
                hasContentViewEvents.Add(args);
        };

        item.ContentView = new object();
        item.ContentView = new object();

        Assert.Equal(2, contentViewEvents.Count);
        Assert.Equal(2, hasContentViewEvents.Count);
        Assert.True(ReferenceEquals(contentViewEvents[0], contentViewEvents[1]));
        Assert.True(ReferenceEquals(hasContentViewEvents[0], hasContentViewEvents[1]));
    }

    /// <summary>
    /// Verifies that ShowSeparator raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void ShowSeparator_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var item = new StatusBarItem("id", StatusBarAlignment.Left, 0);
        var events = new List<PropertyChangedEventArgs>();
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarItem.ShowSeparator))
                events.Add(args);
        };

        item.ShowSeparator = true;
        item.ShowSeparator = false;

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }
}
