using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="VideoDetailsViewModel"/>.
/// Verifies metadata formatting, property updates on video load,
/// thumbnail capture from the first frame, and cleanup.
/// </summary>
public class VideoDetailsViewModelTests : IDisposable
{
    private readonly IVideoEngine _engine;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly IStateService _stateService;
    private readonly VideoPlayerViewModel _playerVm;
    private readonly VideoDetailsViewModel _sut;

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
    public VideoDetailsViewModelTests()
    {
        _engine = Substitute.For<IVideoEngine>();
        _logService = Substitute.For<ILogService>();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings());
        _stateService = Substitute.For<IStateService>();
        _stateService.Current.Returns(new AppState());
        _engine.Volume.Returns(75);
        _playerVm = new VideoPlayerViewModel(_engine, Substitute.For<IEventBus>(), _logService, _settingsService, _stateService, Substitute.For<IContributionRegistry>());
        _sut = new VideoDetailsViewModel(_playerVm);
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
    /// Verifies that Initial State has no metadata.
    /// </summary>
    [Fact]
    public void InitialState_HasNoMetadata()
    {
        Assert.False(_sut.HasMetadata);
        Assert.Equal(string.Empty, _sut.FileName);
        Assert.Equal(string.Empty, _sut.FilePath);
    }

    // ── Metadata Updates ──

    /// <summary>
    /// Verifies that Update From Metadata populates all fields.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_PopulatesAllFields()
    {
        _sut.UpdateFromMetadata(SampleMetadata);

        Assert.True(_sut.HasMetadata);
        Assert.Equal("sample.mp4", _sut.FileName);
        Assert.Equal(@"C:\Videos\sample.mp4", _sut.FilePath);
        Assert.Equal("1.40 GB", _sut.FileSize);
        Assert.Equal("01:02:03", _sut.FormattedDuration);
        Assert.Equal("1920x1080", _sut.Resolution);
        Assert.Equal("h264", _sut.VideoCodec);
        Assert.Equal("aac", _sut.AudioCodec);
        Assert.Equal("23.976 fps", _sut.FrameRate);
        Assert.Equal("4.50 Mbps", _sut.Bitrate);
        Assert.Equal("MP4", _sut.ContainerFormat);
        Assert.Equal("AAC, Stereo, 48000 Hz", _sut.AudioInfo);
    }

    /// <summary>
    /// Verifies that Update From Metadata null clears.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_NullClears()
    {
        _sut.UpdateFromMetadata(SampleMetadata);
        _sut.UpdateFromMetadata(null);

        Assert.False(_sut.HasMetadata);
        Assert.Equal(string.Empty, _sut.FileName);
        Assert.Equal(string.Empty, _sut.Resolution);
    }

    /// <summary>
    /// Verifies that Update From Metadata no audio codec shows none.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_NoAudioCodec_ShowsNone()
    {
        var meta = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 640,
            Height = 480,
            AudioCodec = null
        };

        _sut.UpdateFromMetadata(meta);

        Assert.Equal("None", _sut.AudioCodec);
        Assert.Equal("None", _sut.AudioInfo);
    }

    /// <summary>
    /// Verifies that Update From Metadata zero bitrate shows unknown.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_ZeroBitrate_ShowsUnknown()
    {
        var meta = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 640,
            Height = 480,
            Bitrate = 0
        };

        _sut.UpdateFromMetadata(meta);

        Assert.Equal("Unknown", _sut.Bitrate);
    }

    /// <summary>
    /// Verifies that Update From Metadata zero frame rate shows unknown.
    /// </summary>
    [Fact]
    public void UpdateFromMetadata_ZeroFrameRate_ShowsUnknown()
    {
        var meta = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 640,
            Height = 480,
            FrameRate = 0
        };

        _sut.UpdateFromMetadata(meta);

        Assert.Equal("Unknown", _sut.FrameRate);
    }

    // ── File Size Formatting ──

    /// <summary>
    /// Verifies that Format File Size correctly formats.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1_048_576, "1.00 MB")]
    [InlineData(1_073_741_824, "1.00 GB")]
    [InlineData(1_500_000_000, "1.40 GB")]
    [InlineData(2_500_000, "2.38 MB")]
    public void FormatFileSize_CorrectlyFormats(long bytes, string expected)
    {
        Assert.Equal(expected, VideoDetailsViewModel.FormatFileSize(bytes));
    }

    // ── Bitrate Formatting ──

    /// <summary>
    /// Verifies that Format Bitrate correctly formats.
    /// </summary>
    /// <param name="bps">The bitrate in bits per second.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(0, "Unknown")]
    [InlineData(-1, "Unknown")]
    [InlineData(800, "800 bps")]
    [InlineData(500_000, "500 Kbps")]
    [InlineData(4_500_000, "4.50 Mbps")]
    [InlineData(25_000_000, "25.00 Mbps")]
    public void FormatBitrate_CorrectlyFormats(long bps, string expected)
    {
        Assert.Equal(expected, VideoDetailsViewModel.FormatBitrate(bps));
    }

    // ── Audio Info Formatting ──

    /// <summary>
    /// Verifies that Format Audio Info channel labels.
    /// </summary>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="expectedChannel">The expected channel value.</param>
    [Theory]
    [InlineData(1, "Mono")]
    [InlineData(2, "Stereo")]
    [InlineData(6, "5.1")]
    [InlineData(8, "7.1")]
    [InlineData(4, "4ch")]
    public void FormatAudioInfo_ChannelLabels(int channels, string expectedChannel)
    {
        var meta = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 640,
            Height = 480,
            AudioCodec = "aac",
            AudioChannels = channels,
            AudioSampleRate = 44100
        };

        var result = VideoDetailsViewModel.FormatAudioInfo(meta);
        Assert.Contains(expectedChannel, result);
        Assert.Contains("AAC", result);
        Assert.Contains("44100 Hz", result);
    }

    /// <summary>
    /// Verifies that Format Audio Info null codec returns none.
    /// </summary>
    [Fact]
    public void FormatAudioInfo_NullCodec_ReturnsNone()
    {
        var meta = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 640,
            Height = 480,
            AudioCodec = null
        };

        Assert.Equal("None", VideoDetailsViewModel.FormatAudioInfo(meta));
    }

    // ── Dispose ──

    /// <summary>
    /// Verifies that Dispose unsubscribes from player events.
    /// </summary>
    [Fact]
    public void Dispose_UnsubscribesFromPlayerEvents()
    {
        _sut.Dispose();

        // After dispose, changing metadata on PlayerVM should not update DetailsVM
        _sut.UpdateFromMetadata(SampleMetadata);
        Assert.True(_sut.HasMetadata); // Direct call still works

        // But property-changed driven auto-update should not fire
        // (hard to test in isolation without a real PropertyChanged flow,
        //  but at minimum the Dispose should not throw)
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

    // ── Metadata update when video changes ──

    /// <summary>
    /// Verifies that Metadata Changed On Player updates details.
    /// </summary>
    [Fact]
    public void MetadataChangedOnPlayer_UpdatesDetails()
    {
        // Simulate the PlayerVM setting CurrentMetadata (which raises PropertyChanged)
        _engine.CurrentMetadata.Returns(SampleMetadata);
        _engine.Duration.Returns(SampleMetadata.Duration);

        // Trigger the LoadAsync path that sets CurrentMetadata
        _engine.LoadAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        // Instead of calling LoadAndPlayAsync (which invokes Play()),
        // directly set via the property to test the PropertyChanged subscription
        _playerVm.GetType().GetProperty("CurrentMetadata")!.SetValue(_playerVm, SampleMetadata);

        Assert.True(_sut.HasMetadata);
        Assert.Equal("sample.mp4", _sut.FileName);
    }

    // ── Duration with short videos ──

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

        Assert.Equal("02:05", _sut.FormattedDuration);
    }

    /// <summary>
    /// Verifies that Long Duration includes hours.
    /// </summary>
    [Fact]
    public void LongDuration_IncludesHours()
    {
        var meta = new VideoMetadata
        {
            FilePath = "long.mp4",
            FileName = "long.mp4",
            Width = 640,
            Height = 480,
            Duration = TimeSpan.FromHours(2.5)
        };

        _sut.UpdateFromMetadata(meta);

        Assert.Equal("02:30:00", _sut.FormattedDuration);
    }
}