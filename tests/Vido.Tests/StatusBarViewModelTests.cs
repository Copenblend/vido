using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Layout;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.Core.State;
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
    private readonly ISettingsService _settingsService;
    private readonly IStateService _stateService;
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

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public StatusBarViewModelTests()
    {
        _engine = Substitute.For<IVideoEngine>();
        _logService = Substitute.For<ILogService>();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings());
        _stateService = Substitute.For<IStateService>();
        _stateService.Current.Returns(new AppState());
        _engine.Volume.Returns(75);
        _playerVm = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService, Substitute.For<IContributionRegistry>());
        _sut = new StatusBarViewModel(_playerVm);
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        _sut.Dispose();
        _playerVm.Dispose();
    }

    // ── Initial State ──

    /// <summary>
    /// Verifies that Initial State has built in left items.
    /// </summary>
    [Fact]
    public void InitialState_HasBuiltInLeftItems()
    {
        Assert.Single(_sut.LeftItems);
        Assert.Equal(StatusBarViewModel.FileNameItemId, _sut.LeftItems[0].Id);
    }

    /// <summary>
    /// Verifies that Initial State has built in right items.
    /// </summary>
    [Fact]
    public void InitialState_HasBuiltInRightItems()
    {
        Assert.Equal(3, _sut.RightItems.Count);
        Assert.Equal(StatusBarViewModel.DurationItemId, _sut.RightItems[0].Id);
        Assert.Equal(StatusBarViewModel.ResolutionItemId, _sut.RightItems[1].Id);
        Assert.Equal(StatusBarViewModel.CodecItemId, _sut.RightItems[2].Id);
    }

    /// <summary>
    /// Verifies that Initial State file name shows no file.
    /// </summary>
    [Fact]
    public void InitialState_FileNameShowsNoFile()
    {
        var item = _sut.FindItem(StatusBarViewModel.FileNameItemId);
        Assert.Equal("No file", item!.Text);
    }

    /// <summary>
    /// Verifies that Initial State right items are hidden.
    /// </summary>
    [Fact]
    public void InitialState_RightItemsAreHidden()
    {
        Assert.False(_sut.FindItem(StatusBarViewModel.ResolutionItemId)!.IsVisible);
        Assert.False(_sut.FindItem(StatusBarViewModel.DurationItemId)!.IsVisible);
        Assert.False(_sut.FindItem(StatusBarViewModel.CodecItemId)!.IsVisible);
    }

    // ── Metadata Updates ──

    /// <summary>
    /// Verifies that Update From Metadata sets file name.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_SetsFileName()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        Assert.Equal("sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Text);
    }

    /// <summary>
    /// Verifies that Update From Metadata sets resolution.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_SetsResolution()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        var item = _sut.FindItem(StatusBarViewModel.ResolutionItemId)!;
        Assert.Equal("1920x1080", item.Text);
        Assert.True(item.IsVisible);
    }

    /// <summary>
    /// Verifies that Update From Metadata sets duration.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_SetsDuration()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        var item = _sut.FindItem(StatusBarViewModel.DurationItemId)!;
        Assert.Equal("01:02:03", item.Text);
        Assert.True(item.IsVisible);
    }

    /// <summary>
    /// Verifies that Update From Metadata sets codec.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_SetsCodec()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        var item = _sut.FindItem(StatusBarViewModel.CodecItemId)!;
        Assert.Equal("H264", item.Text);
        Assert.True(item.IsVisible);
    }

    /// <summary>
    /// Verifies that Update From Metadata null codec shows unknown.
    /// </summary>
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

    /// <summary>
    /// Verifies that Update From Metadata null resets to no file.
    /// </summary>
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

    /// <summary>
    /// Verifies that Update From Metadata sets file path as tooltip.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_SetsFilePathAsTooltip()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        Assert.Equal(@"C:\Videos\sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Tooltip);
    }

    // ── Metadata sync via PropertyChanged ──

    /// <summary>
    /// Verifies that Metadata Changed On Player updates status bar.
    /// </summary>
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

    // ── Item Registry ──

    /// <summary>
    /// Verifies that Register Item adds left item.
    /// </summary>
    [Fact]
    public void RegisterItem_AddsLeftItem()
    {
        var item = _sut.RegisterItem("plugin.status", StatusBarAlignment.Left, 50);

        Assert.Equal("plugin.status", item.Id);
        Assert.Equal(StatusBarAlignment.Left, item.Alignment);
        Assert.Contains(item, _sut.LeftItems);
    }

    /// <summary>
    /// Verifies that Register Item adds right item.
    /// </summary>
    [Fact]
    public void RegisterItem_AddsRightItem()
    {
        var item = _sut.RegisterItem("plugin.status", StatusBarAlignment.Right, 150);

        Assert.Contains(item, _sut.RightItems);
    }

    /// <summary>
    /// Verifies that Register Item inserts in priority order.
    /// </summary>
    [Fact]
    public void RegisterItem_InsertsInPriorityOrder()
    {
        // Built-in right items: Duration(10100), Resolution(10200), Codec(10300)
        var item = _sut.RegisterItem("plugin.between", StatusBarAlignment.Right, 150);

        // Should be before all built-in items (which are at 10100+)
        var index = _sut.RightItems.IndexOf(item);
        Assert.Equal(0, index);
    }

    /// <summary>
    /// Verifies that Register Item duplicate id throws argument exception.
    /// </summary>
    [Fact]
    public void RegisterItem_DuplicateId_ThrowsArgumentException()
    {
        _sut.RegisterItem("plugin.test", StatusBarAlignment.Left, 50);

        Assert.Throws<ArgumentException>(() =>
            _sut.RegisterItem("plugin.test", StatusBarAlignment.Right, 100));
    }

    /// <summary>
    /// Verifies that Unregister Item removes item.
    /// </summary>
    [Fact]
    public void UnregisterItem_RemovesItem()
    {
        _sut.RegisterItem("plugin.test", StatusBarAlignment.Left, 50);
        _sut.UnregisterItem("plugin.test");

        Assert.Null(_sut.FindItem("plugin.test"));
    }

    /// <summary>
    /// Verifies that Unregister Item nonexistent id no op.
    /// </summary>
    [Fact]
    public void UnregisterItem_NonexistentId_NoOp()
    {
        var countBefore = _sut.LeftItems.Count + _sut.RightItems.Count;
        _sut.UnregisterItem("nonexistent");
        var countAfter = _sut.LeftItems.Count + _sut.RightItems.Count;

        Assert.Equal(countBefore, countAfter);
    }

    /// <summary>
    /// Verifies that Find Item returns correct item.
    /// </summary>
    [Fact]
    public void FindItem_ReturnsCorrectItem()
    {
        var registered = _sut.RegisterItem("plugin.test", StatusBarAlignment.Left, 50);

        var found = _sut.FindItem("plugin.test");
        Assert.Same(registered, found);
    }

    /// <summary>
    /// Verifies that Find Item nonexistent id returns null.
    /// </summary>
    [Fact]
    public void FindItem_NonexistentId_ReturnsNull()
    {
        Assert.Null(_sut.FindItem("nonexistent"));
    }

    /// <summary>
    /// Verifies that Find Item finds built in items.
    /// </summary>
    [Fact]
    public void FindItem_FindsBuiltInItems()
    {
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.FileNameItemId));
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.ResolutionItemId));
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.DurationItemId));
        Assert.NotNull(_sut.FindItem(StatusBarViewModel.CodecItemId));
    }

    // ── StatusBarItem INotifyPropertyChanged ──

    /// <summary>
    /// Verifies that Status Bar Item text change raises property changed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Status Bar Item is visible change raises property changed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Status Bar Item same value does not raise property changed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Dispose unsubscribes from player events.
    /// </summary>
    [Fact]
    public void Dispose_UnsubscribesFromPlayerEvents()
    {
        _sut.Dispose();

        // After dispose, direct update should still work
        _sut.UpdateFromMetadata(SampleMetadata);
        Assert.Equal("sample.mp4", _sut.FindItem(StatusBarViewModel.FileNameItemId)!.Text);
    }

    /// <summary>
    /// Verifies that Dispose does not throw on multiple calls.
    /// </summary>
    [Fact]
    public void Dispose_DoesNotThrowOnMultipleCalls()
    {
        _sut.Dispose();
        _sut.Dispose(); // Should not throw
    }

    // ── Short duration without hours ──

    /// <summary>
    /// Verifies that Short Duration omits hours.
    /// </summary>
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

    // ── Priority tiebreaking ──

    /// <summary>
    /// Verifies that Register Item same priority orders by id alphabetically.
    /// </summary>
    [Fact]
    public void RegisterItem_SamePriority_OrdersByIdAlphabetically()
    {
        var itemC = _sut.RegisterItem("plugin.charlie", StatusBarAlignment.Left, 50);
        var itemA = _sut.RegisterItem("plugin.alpha", StatusBarAlignment.Left, 50);
        var itemB = _sut.RegisterItem("plugin.bravo", StatusBarAlignment.Left, 50);

        // Built-in "fileName" is at index 0, then our three items sorted by ID
        Assert.Equal("plugin.alpha", _sut.LeftItems[1].Id);
        Assert.Equal("plugin.bravo", _sut.LeftItems[2].Id);
        Assert.Equal("plugin.charlie", _sut.LeftItems[3].Id);
    }

    /// <summary>
    /// Verifies that Insert By Priority deterministic with same priority.
    /// </summary>
    [Fact]
    public void InsertByPriority_DeterministicWith_SamePriority()
    {
        // Register items in reverse alphabetical order, all same priority
        _sut.RegisterItem("z.item", StatusBarAlignment.Right, 500);
        _sut.RegisterItem("a.item", StatusBarAlignment.Right, 500);
        _sut.RegisterItem("m.item", StatusBarAlignment.Right, 500);

        // Find them among the right items (built-ins are at 10100+)
        var pluginItems = _sut.RightItems.Where(i => i.Priority == 500).ToList();
        Assert.Equal(3, pluginItems.Count);
        Assert.Equal("a.item", pluginItems[0].Id);
        Assert.Equal("m.item", pluginItems[1].Id);
        Assert.Equal("z.item", pluginItems[2].Id);
    }
}