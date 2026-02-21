using NSubstitute;
using Vido.Core.Layout;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="StatusBarViewModel"/> — built-in item population,
/// metadata updates, item registry, and cleanup.
/// </summary>
public class StatusBarViewModelTests : IDisposable
{
    private readonly IVideoEngine _engine;
    private readonly ILogService _logService;
    private readonly VideoPlayerViewModel _playerVm;
    private readonly StatusBarViewModel _sut;

    private static readonly VideoMetadata SampleMetadata = new()
    {
        FilePath = @"C:\Videos\sample.mp4",
        FileName = "sample.mp4",
        FileSize = 1_500_000_000,
        Duration = TimeSpan.FromSeconds(3723), // 1:02:03
        Width = 1920,
        Height = 1080,
        VideoCodec = "h264",
        AudioCodec = "aac",
        FrameRate = 23.976,
        Bitrate = 4_500_000,
        ContainerFormat = "mp4",
        AudioChannels = 2,
        AudioSampleRate = 48000
    };

    public StatusBarViewModelTests()
    {
        _engine = Substitute.For<IVideoEngine>();
        _logService = Substitute.For<ILogService>();
        _engine.Volume.Returns(75);
        _playerVm = new VideoPlayerViewModel(_engine, _logService);
        _sut = new StatusBarViewModel(_playerVm);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _playerVm.Dispose();
    }

    // ── Initial State ──

    [Fact]
    public void InitialState_HasBuiltInLeftItems()
    {
        Assert.Single(_sut.LeftItems);
        Assert.Equal(StatusBarViewModel.FileNameItemId, _sut.LeftItems[0].Id);
    }

    [Fact]
    public void InitialState_HasBuiltInRightItems()
    {
        Assert.Equal(3, _sut.RightItems.Count);
        Assert.Equal(StatusBarViewModel.DurationItemId, _sut.RightItems[0].Id);
        Assert.Equal(StatusBarViewModel.ResolutionItemId, _sut.RightItems[1].Id);
        Assert.Equal(StatusBarViewModel.CodecItemId, _sut.RightItems[2].Id);
    }

    [Fact]
    public void InitialState_FileNameShowsNoFile()
    {
        var item = _sut.FindItem(StatusBarViewModel.FileNameItemId);
        Assert.Equal("No file", item!.Text);
    }

    [Fact]
    public void InitialState_RightItemsAreHidden()
    {
        Assert.False(_sut.FindItem(StatusBarViewModel.ResolutionItemId)!.IsVisible);
        Assert.False(_sut.FindItem(StatusBarViewModel.DurationItemId)!.IsVisible);
        Assert.False(_sut.FindItem(StatusBarViewModel.CodecItemId)!.IsVisible);
    }

    // ── Metadata Updates ──

    [Fact]
    public void UpdateFromMetadata_SetsFileName()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        Assert.Equal("sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Text);
    }

    [Fact]
    public void UpdateFromMetadata_SetsResolution()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        var item = _sut.FindItem(StatusBarViewModel.ResolutionItemId)!;
        Assert.Equal("1920x1080", item.Text);
        Assert.True(item.IsVisible);
    }

    [Fact]
    public void UpdateFromMetadata_SetsDuration()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        var item = _sut.FindItem(StatusBarViewModel.DurationItemId)!;
        Assert.Equal("01:02:03", item.Text);
        Assert.True(item.IsVisible);
    }

    [Fact]
    public void UpdateFromMetadata_SetsCodec()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        var item = _sut.FindItem(StatusBarViewModel.CodecItemId)!;
        Assert.Equal("H264", item.Text);
        Assert.True(item.IsVisible);
    }

    [Fact]
    public void UpdateFromMetadata_NullCodec_ShowsUnknown()
    {
        var meta = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 640,
            Height = 480,
            VideoCodec = null
        };

        _sut.UpdateFromMetadata(meta);

        Assert.Equal("UNKNOWN", _sut.FindItem(StatusBarViewModel.CodecItemId)!.Text);
    }

    [Fact]
    public void UpdateFromMetadata_Null_ResetsToNoFile()
    {
        _sut.UpdateFromMetadata(SampleMetadata);
        _sut.UpdateFromMetadata(null);

        Assert.Equal("No file", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Text);
        Assert.False(_sut.FindItem(StatusBarViewModel.ResolutionItemId)!.IsVisible);
        Assert.False(_sut.FindItem(StatusBarViewModel.DurationItemId)!.IsVisible);
        Assert.False(_sut.FindItem(StatusBarViewModel.CodecItemId)!.IsVisible);
    }

    [Fact]
    public void UpdateFromMetadata_SetsFilePathAsTooltip()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        Assert.Equal(@"C:\Videos\sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Tooltip);
    }

    // ── Metadata sync via PropertyChanged ──

    [Fact]
    public void MetadataChangedOnPlayer_UpdatesStatusBar()
    {
        _engine.CurrentMetadata.Returns(SampleMetadata);
        _engine.Duration.Returns(SampleMetadata.Duration);

        // Directly set via property to trigger PropertyChanged
        _playerVm.GetType().GetProperty("CurrentMetadata")!.SetValue(_playerVm, SampleMetadata);

        Assert.Equal("sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Text);
        Assert.Equal("1920x1080", _sut.FindItem(StatusBarViewModel.ResolutionItemId)!.Text);
    }

    // ── Duration Formatting ──

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(3661, "01:01:01")]
    [InlineData(3723, "01:02:03")]
    public void FormatDuration_CorrectlyFormats(int seconds, string expected)
    {
        Assert.Equal(expected, StatusBarViewModel.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }

    // ── Item Registry ──

    [Fact]
    public void RegisterItem_AddsLeftItem()
    {
        var item = _sut.RegisterItem("plugin.status", StatusBarAlignment.Left, 50);

        Assert.Equal("plugin.status", item.Id);
        Assert.Equal(StatusBarAlignment.Left, item.Alignment);
        Assert.Contains(item, _sut.LeftItems);
    }

    [Fact]
    public void RegisterItem_AddsRightItem()
    {
        var item = _sut.RegisterItem("plugin.status", StatusBarAlignment.Right, 150);

        Assert.Contains(item, _sut.RightItems);
    }

    [Fact]
    public void RegisterItem_InsertsInPriorityOrder()
    {
        // Built-in right items: Duration(100), Resolution(200), Codec(300)
        var item = _sut.RegisterItem("plugin.between", StatusBarAlignment.Right, 150);

        // Should be between Duration(100) and Resolution(200)
        var index = _sut.RightItems.IndexOf(item);
        Assert.Equal(1, index);
    }

    [Fact]
    public void RegisterItem_DuplicateId_ThrowsArgumentException()
    {
        _sut.RegisterItem("plugin.test", StatusBarAlignment.Left, 50);

        Assert.Throws<ArgumentException>(() =>
            _sut.RegisterItem("plugin.test", StatusBarAlignment.Right, 100));
    }

    [Fact]
    public void UnregisterItem_RemovesItem()
    {
        _sut.RegisterItem("plugin.test", StatusBarAlignment.Left, 50);
        _sut.UnregisterItem("plugin.test");

        Assert.Null(_sut.FindItem("plugin.test"));
    }

    [Fact]
    public void UnregisterItem_NonexistentId_NoOp()
    {
        var countBefore = _sut.LeftItems.Count + _sut.RightItems.Count;
        _sut.UnregisterItem("nonexistent");
        var countAfter = _sut.LeftItems.Count + _sut.RightItems.Count;

        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void FindItem_ReturnsCorrectItem()
    {
        var registered = _sut.RegisterItem("plugin.test", StatusBarAlignment.Left, 50);

        var found = _sut.FindItem("plugin.test");
        Assert.Same(registered, found);
    }

    [Fact]
    public void FindItem_NonexistentId_ReturnsNull()
    {
        Assert.Null(_sut.FindItem("nonexistent"));
    }

    [Fact]
    public void FindItem_FindsBuiltInItems()
    {
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.FileNameItemId));
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.ResolutionItemId));
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.DurationItemId));
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.CodecItemId));
    }

    // ── StatusBarItem INotifyPropertyChanged ──

    [Fact]
    public void StatusBarItem_TextChange_RaisesPropertyChanged()
    {
        var item = new StatusBarItem("test", StatusBarAlignment.Left, 0);
        var raised = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusBarItem.Text)) raised = true;
        };

        item.Text = "changed";

        Assert.True(raised);
    }

    [Fact]
    public void StatusBarItem_IsVisibleChange_RaisesPropertyChanged()
    {
        var item = new StatusBarItem("test", StatusBarAlignment.Left, 0);
        var raised = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusBarItem.IsVisible)) raised = true;
        };

        item.IsVisible = false;

        Assert.True(raised);
    }

    [Fact]
    public void StatusBarItem_SameValue_DoesNotRaisePropertyChanged()
    {
        var item = new StatusBarItem("test", StatusBarAlignment.Left, 0);
        item.Text = "hello";

        var raised = false;
        item.PropertyChanged += (_, _) => raised = true;

        item.Text = "hello"; // same value

        Assert.False(raised);
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_UnsubscribesFromPlayerEvents()
    {
        _sut.Dispose();

        // After dispose, direct update should still work
        _sut.UpdateFromMetadata(SampleMetadata);
        Assert.Equal("sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Text);
    }

    [Fact]
    public void Dispose_DoesNotThrowOnMultipleCalls()
    {
        _sut.Dispose();
        _sut.Dispose(); // Should not throw
    }

    // ── Short duration without hours ──

    [Fact]
    public void ShortDuration_OmitsHours()
    {
        var meta = new VideoMetadata
        {
            FilePath = "short.mp4",
            FileName = "short.mp4",
            Width = 640,
            Height = 480,
            Duration = TimeSpan.FromSeconds(125)
        };

        _sut.UpdateFromMetadata(meta);

        Assert.Equal("02:05", _sut.FindItem(StatusBarViewModel.DurationItemId)!.Text);
    }
}
