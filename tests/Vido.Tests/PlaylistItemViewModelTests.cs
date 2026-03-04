using System.IO;
using Vido.Core.Models.Playlists;
using Vido.ViewModels.Playlists;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="PlaylistItemViewModel"/> — PI-018.
/// Covers constructor, property accessors, IsPlaying change notification,
/// file-exists caching, and tooltip text.
/// </summary>
public sealed class PlaylistItemViewModelTests
{
    // ── Constructor ──

    /// <summary>
    /// Verifies that Constructor sets FileName and FilePath from model.
    /// </summary>
    [Fact]
    public void Constructor_SetsPropertiesFromModel()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");

        var vm = new PlaylistItemViewModel(item);

        Assert.Equal("test.mp4", vm.FileName);
        Assert.Equal(@"C:\Videos\test.mp4", vm.FilePath);
        Assert.False(vm.IsPlaying);
    }

    /// <summary>
    /// Verifies that Constructor throws on null item.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PlaylistItemViewModel(null!));
    }

    /// <summary>
    /// Verifies that Constructor sets internal Model property.
    /// </summary>
    [Fact]
    public void Constructor_SetsModel()
    {
        var item = new PlaylistItem(@"C:\Videos\clip.mp4");

        var vm = new PlaylistItemViewModel(item);

        Assert.Same(item, vm.Model);
    }

    // ── IsPlaying ──

    /// <summary>
    /// Verifies that IsPlaying raises PropertyChanged.
    /// </summary>
    [Fact]
    public void IsPlaying_RaisesPropertyChanged()
    {
        var vm = new PlaylistItemViewModel(new PlaylistItem(@"C:\Videos\video.mp4"));
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistItemViewModel.IsPlaying))
                raised = true;
        };

        vm.IsPlaying = true;

        Assert.True(raised);
        Assert.True(vm.IsPlaying);
    }

    /// <summary>
    /// Verifies that IsPlaying same value does not raise PropertyChanged.
    /// </summary>
    [Fact]
    public void IsPlaying_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = new PlaylistItemViewModel(new PlaylistItem(@"C:\Videos\video.mp4"));
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        vm.IsPlaying = false; // already false

        Assert.False(raised);
    }

    // ── FileExists ──

    /// <summary>
    /// Verifies that FileExists returns false for nonexistent file.
    /// </summary>
    [Fact]
    public void FileExists_ReturnsFalseForNonexistentFile()
    {
        var vm = new PlaylistItemViewModel(new PlaylistItem(@"C:\NonExistent\does_not_exist.mp4"));

        Assert.False(vm.FileExists);
    }

    /// <summary>
    /// Verifies that FileExists caches result until RefreshFileExists is called.
    /// </summary>
    [Fact]
    public void FileExists_CachedUntilRefresh()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"playlist-item-{Guid.NewGuid():N}.mp4");
        var vm = new PlaylistItemViewModel(new PlaylistItem(tempPath));

        Assert.False(vm.FileExists);

        File.WriteAllText(tempPath, "test");
        try
        {
            // Still false because cached
            Assert.False(vm.FileExists);

            vm.RefreshFileExists();

            Assert.True(vm.FileExists);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that RefreshFileExists raises PropertyChanged for FileExists and ToolTipText
    /// when file state changes.
    /// </summary>
    [Fact]
    public void RefreshFileExists_RaisesPropertyChanged_WhenStateChanges()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"playlist-item-{Guid.NewGuid():N}.mp4");
        File.WriteAllText(tempPath, "test");

        try
        {
            var vm = new PlaylistItemViewModel(new PlaylistItem(tempPath));
            Assert.True(vm.FileExists);

            File.Delete(tempPath);

            var changedProperties = new List<string>();
            vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

            vm.RefreshFileExists();

            Assert.False(vm.FileExists);
            Assert.Contains(nameof(PlaylistItemViewModel.FileExists), changedProperties);
            Assert.Contains(nameof(PlaylistItemViewModel.ToolTipText), changedProperties);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that RefreshFileExists does not raise PropertyChanged when state unchanged.
    /// </summary>
    [Fact]
    public void RefreshFileExists_NoPropertyChanged_WhenStateUnchanged()
    {
        var vm = new PlaylistItemViewModel(new PlaylistItem(@"C:\NonExistent\nope.mp4"));
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        vm.RefreshFileExists();

        Assert.False(raised);
    }

    // ── ToolTipText ──

    /// <summary>
    /// Verifies that ToolTipText includes "file not found" for missing files.
    /// </summary>
    [Fact]
    public void ToolTipText_IncludesNotFoundForMissingFile()
    {
        var vm = new PlaylistItemViewModel(new PlaylistItem(@"C:\Fake\missing.mp4"));

        Assert.Contains("file not found", vm.ToolTipText);
        Assert.Contains(@"C:\Fake\missing.mp4", vm.ToolTipText);
    }

    /// <summary>
    /// Verifies that ToolTipText returns path for existing file.
    /// </summary>
    [Fact]
    public void ToolTipText_ReturnsPathForExistingFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"playlist-item-{Guid.NewGuid():N}.mp4");
        File.WriteAllText(tempPath, "test");

        try
        {
            var vm = new PlaylistItemViewModel(new PlaylistItem(tempPath));

            Assert.Equal(tempPath, vm.ToolTipText);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that ToolTipText is recalculated after RefreshFileExists changes state.
    /// </summary>
    [Fact]
    public void ToolTipText_UpdatesAfterRefreshChangesState()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"playlist-item-{Guid.NewGuid():N}.mp4");
        var vm = new PlaylistItemViewModel(new PlaylistItem(tempPath));

        Assert.Contains("file not found", vm.ToolTipText);

        File.WriteAllText(tempPath, "test");
        try
        {
            vm.RefreshFileExists();

            Assert.Equal(tempPath, vm.ToolTipText);
            Assert.DoesNotContain("file not found", vm.ToolTipText);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
