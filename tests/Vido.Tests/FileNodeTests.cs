using System.ComponentModel;
using Vido.Core.FileSystem;
using Xunit;

namespace Vido.Tests;

public sealed class FileNodeTests
{
    [Fact]
    public void Constructor_SetsName_FromFullPath()
    {
        var node = new FileNode(@"C:\Videos\test.mp4", isDirectory: false);
        Assert.Equal("test.mp4", node.Name);
    }

    [Fact]
    public void Constructor_SetsName_ForDirectory()
    {
        var node = new FileNode(@"C:\Videos\Movies", isDirectory: true);
        Assert.Equal("Movies", node.Name);
    }

    [Fact]
    public void Constructor_Directory_HasDummyChild()
    {
        var node = new FileNode(@"C:\Videos\Movies", isDirectory: true);
        Assert.Single(node.Children);
        Assert.True(node.NeedsLoading);
    }

    [Fact]
    public void Constructor_File_HasNoChildren()
    {
        var node = new FileNode(@"C:\Videos\test.mp4", isDirectory: false);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void IsDirectory_True_ForDirectory()
    {
        var node = new FileNode(@"C:\Videos", isDirectory: true);
        Assert.True(node.IsDirectory);
    }

    [Fact]
    public void IsDirectory_False_ForFile()
    {
        var node = new FileNode(@"C:\Videos\test.mp4", isDirectory: false);
        Assert.False(node.IsDirectory);
    }

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

    [Fact]
    public void IsVideoFile_AlwaysFalse_ForDirectories()
    {
        var node = new FileNode(@"C:\folder.mp4", isDirectory: true);
        Assert.False(node.IsVideoFile);
    }

    [Fact]
    public void NeedsLoading_False_AfterChildrenCleared()
    {
        var node = new FileNode(@"C:\Videos", isDirectory: true);
        Assert.True(node.NeedsLoading);

        node.Children.Clear();
        Assert.False(node.NeedsLoading);
    }

    [Fact]
    public void NeedsLoading_False_ForFiles()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        Assert.False(node.NeedsLoading);
    }

    [Fact]
    public void EmptyPath_ProducesEmptyName()
    {
        var node = new FileNode(string.Empty, isDirectory: false);
        Assert.Equal(string.Empty, node.Name);
    }

    [Fact]
    public void IsHidden_DefaultsFalse()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        Assert.False(node.IsHidden);
    }

    [Fact]
    public void IsHidden_RaisesPropertyChanged()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        var raised = new List<string?>();
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        node.IsHidden = true;

        Assert.Contains(nameof(FileNode.IsHidden), raised);
    }

    [Fact]
    public void IsHidden_DoesNotRaisePropertyChanged_WhenValueUnchanged()
    {
        var node = new FileNode(@"C:\test.mp4", isDirectory: false);
        var raised = new List<string?>();
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        node.IsHidden = false; // same as default

        Assert.Empty(raised);
    }
}
