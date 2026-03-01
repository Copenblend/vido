using System.ComponentModel;
using Vido.Core.FileSystem;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="FileNode"/>.
/// </summary>
public sealed class FileNodeTests
{
    /// <summary>
    /// Verifies that Constructor sets name from full path.
    /// </summary>
    [Fact]
    public void Constructor_SetsName_FromFullPath()
    {
        var node = new FileNode(@"C:\Videos\test.mp4", isDirectory: false);
        Assert.Equal("test.mp4", node.Name);
    }

    /// <summary>
    /// Verifies that Constructor sets name for directory.
    /// </summary>
    [Fact]
    public void Constructor_SetsName_ForDirectory()
    {
        var node = new FileNode(@"C:\Videos\Movies", isDirectory: true);
        Assert.Equal("Movies", node.Name);
    }

    /// <summary>
    /// Verifies that Constructor directory has dummy child.
    /// </summary>
    [Fact]
    public void Constructor_Directory_HasDummyChild()
    {
        var node = new FileNode(@"C:\Videos\Movies", isDirectory: true);
        Assert.Single(node.Children);
        Assert.True(node.NeedsLoading);
    }

    /// <summary>
    /// Verifies that Constructor file has no children.
    /// </summary>
    [Fact]
    public void Constructor_File_HasNoChildren()
    {
        var node = new FileNode(@"C:\Videos\test.mp4", isDirectory: false);
        Assert.Empty(node.Children);
    }

    /// <summary>
    /// Verifies that Is Directory true for directory.
    /// </summary>
    [Fact]
    public void IsDirectory_True_ForDirectory()
    {
        var node = new FileNode(@"C:\Videos", isDirectory: true);
        Assert.True(node.IsDirectory);
    }

    /// <summary>
    /// Verifies that Is Directory false for file.
    /// </summary>
    [Fact]
    public void IsDirectory_False_ForFile()
    {
        var node = new FileNode(@"C:\Videos\test.mp4", isDirectory: false);
        Assert.False(node.IsDirectory);
    }

    /// <summary>
    /// Verifies that Is Video File detects video extensions.
    /// </summary>
    /// <param name="path">The file path to evaluate.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(@"C:\test.mp4", true)]
    [InlineData(@"C:\test.avi", true)]
    [InlineData(@"C:\test.mkv", true)]
    [InlineData(@"C:\test.mov", true)]
    [InlineData(@"C:\test.wmv", true)]
    [InlineData(@"C:\test.flv", true)]
    [InlineData(@"C:\test.webm", true)]
    [InlineData(@"C:\test.MP4", true)]
    [InlineData(@"C:\test.txt", false)]
    [InlineData(@"C:\test.jpg", false)]
    [InlineData(@"C:\test.exe", false)]
    public void IsVideoFile_DetectsVideoExtensions(string path, bool expected)
    {
        var node = new FileNode(path, isDirectory: false);
        Assert.Equal(expected, node.IsVideoFile);
    }

    /// <summary>
    /// Verifies that Is Video File always false for directories.
    /// </summary>
    [Fact]
    public void IsVideoFile_AlwaysFalse_ForDirectories()
    {
        var node = new FileNode(@"C:\folder.mp4", isDirectory: true);
        Assert.False(node.IsVideoFile);
    }

    /// <summary>
    /// Verifies that Needs Loading false after children cleared.
    /// </summary>
    [Fact]
    public void NeedsLoading_False_AfterChildrenCleared()
    {
        var node = new FileNode(@"C:\Videos", isDirectory: true);
        Assert.True(node.NeedsLoading);

        node.Children.Clear();
        Assert.False(node.NeedsLoading);
    }

    /// <summary>
    /// Verifies that Needs Loading false for files.
    /// </summary>
    [Fact]
    public void NeedsLoading_False_ForFiles()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        Assert.False(node.NeedsLoading);
    }

    /// <summary>
    /// Verifies that Empty Path produces empty name.
    /// </summary>
    [Fact]
    public void EmptyPath_ProducesEmptyName()
    {
        var node = new FileNode(string.Empty, isDirectory: false);
        Assert.Equal(string.Empty, node.Name);
    }

    /// <summary>
    /// Verifies that Is Hidden defaults false.
    /// </summary>
    [Fact]
    public void IsHidden_DefaultsFalse()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        Assert.False(node.IsHidden);
    }

    /// <summary>
    /// Verifies that Is Hidden raises property changed.
    /// </summary>
    [Fact]
    public void IsHidden_RaisesPropertyChanged()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        var raised = new List<string?>();
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        node.IsHidden = true;

        Assert.Contains(nameof(FileNode.IsHidden), raised);
    }

    /// <summary>
    /// Verifies that Is Hidden does not raise property changed when value unchanged.
    /// </summary>
    [Fact]
    public void IsHidden_DoesNotRaisePropertyChanged_WhenValueUnchanged()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        var raised = new List<string?>();
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        node.IsHidden = false; // same as default

        Assert.Empty(raised);
    }

    /// <summary>
    /// Verifies that Is Hidden raises PropertyChanged with a cached event args instance.
    /// </summary>
    [Fact]
    public void IsHidden_Changed_UsesCachedPropertyChangedEventArgs()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        var events = new List<PropertyChangedEventArgs>();
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FileNode.IsHidden))
                events.Add(e);
        };

        node.IsHidden = true;
        node.IsHidden = false;

        Assert.Equal(2, events.Count);
        Assert.True(ReferenceEquals(events[0], events[1]));
    }
}