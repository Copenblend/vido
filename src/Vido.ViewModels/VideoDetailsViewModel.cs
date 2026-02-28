using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vido.Core.Formatting;
using Vido.Core.Playback;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the Video Details panel in the right panel.
/// Observes <see cref="VideoPlayerViewModel"/> to display formatted metadata
/// about the currently loaded video.
/// </summary>
public partial class VideoDetailsViewModel : ObservableObject, IDisposable
{
    private readonly VideoPlayerViewModel _playerViewModel;
    private bool _disposed;

    // ── Metadata display properties ──

    /// <summary>
    /// Whether a video is currently loaded and metadata is available.
    /// </summary>
    [ObservableProperty]
    private bool _hasMetadata;

    /// <summary>
    /// Video file name.
    /// </summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>
    /// Full file path.
    /// </summary>
    [ObservableProperty]
    private string _filePath = string.Empty;

    /// <summary>
    /// Formatted file size (e.g., "1.23 GB").
    /// </summary>
    [ObservableProperty]
    private string _fileSize = string.Empty;

    /// <summary>
    /// Formatted duration (e.g., "01:23:45").
    /// </summary>
    [ObservableProperty]
    private string _formattedDuration = string.Empty;

    /// <summary>
    /// Resolution string (e.g., "1920x1080").
    /// </summary>
    [ObservableProperty]
    private string _resolution = string.Empty;

    /// <summary>
    /// Video codec name.
    /// </summary>
    [ObservableProperty]
    private string _videoCodec = string.Empty;

    /// <summary>
    /// Audio codec name.
    /// </summary>
    [ObservableProperty]
    private string _audioCodec = string.Empty;

    /// <summary>
    /// Formatted frame rate (e.g., "23.976 fps").
    /// </summary>
    [ObservableProperty]
    private string _frameRate = string.Empty;

    /// <summary>
    /// Formatted bitrate (e.g., "4.50 Mbps").
    /// </summary>
    [ObservableProperty]
    private string _bitrate = string.Empty;

    /// <summary>
    /// Container format name (e.g., "mp4").
    /// </summary>
    [ObservableProperty]
    private string _containerFormat = string.Empty;

    /// <summary>
    /// Formatted audio info (e.g., "AAC, Stereo, 48000 Hz").
    /// </summary>
    [ObservableProperty]
    private string _audioInfo = string.Empty;

    /// <summary>
    /// Creates the video details view model, subscribing to player metadata changes
    /// and initializing display properties from the current video (if any).
    /// </summary>
    /// <param name="playerViewModel">Video player view model whose metadata is formatted for display.</param>
    public VideoDetailsViewModel(VideoPlayerViewModel playerViewModel)
    {
        _playerViewModel = playerViewModel;
        _playerViewModel.PropertyChanged += OnPlayerPropertyChanged;

        // Initialize from current state
        UpdateFromMetadata(_playerViewModel.CurrentMetadata);
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPlayerViewModel.CurrentMetadata))
        {
            UpdateFromMetadata(_playerViewModel.CurrentMetadata);
        }
    }

    internal void UpdateFromMetadata(VideoMetadata? metadata)
    {
        if (metadata is null)
        {
            HasMetadata = false;
            FileName = string.Empty;
            FilePath = string.Empty;
            FileSize = string.Empty;
            FormattedDuration = string.Empty;
            Resolution = string.Empty;
            VideoCodec = string.Empty;
            AudioCodec = string.Empty;
            FrameRate = string.Empty;
            Bitrate = string.Empty;
            ContainerFormat = string.Empty;
            AudioInfo = string.Empty;
            return;
        }

        HasMetadata = true;
        FileName = metadata.FileName;
        FilePath = metadata.FilePath;
        FileSize = FormatFileSize(metadata.FileSize);
        FormattedDuration = TimeFormatter.FormatPadded(metadata.Duration);
        Resolution = metadata.Resolution;
        VideoCodec = metadata.VideoCodec ?? "Unknown";
        AudioCodec = metadata.AudioCodec ?? "None";
        FrameRate = metadata.FrameRate > 0
            ? $"{metadata.FrameRate:F3} fps"
            : "Unknown";
        Bitrate = FormatBitrate(metadata.Bitrate);
        ContainerFormat = metadata.ContainerFormat?.ToUpperInvariant() ?? "Unknown";
        AudioInfo = FormatAudioInfo(metadata);
    }

    internal static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024L => $"{bytes} B",
            < 1024L * 1024 => $"{bytes / 1024.0:F2} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F2} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    internal static string FormatBitrate(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0) return "Unknown";
        return bitsPerSecond switch
        {
            < 1_000L => $"{bitsPerSecond} bps",
            < 1_000_000L => $"{bitsPerSecond / 1_000.0:F0} Kbps",
            _ => $"{bitsPerSecond / 1_000_000.0:F2} Mbps"
        };
    }

    internal static string FormatAudioInfo(VideoMetadata metadata)
    {
        if (metadata.AudioCodec is null) return "None";

        var channels = metadata.AudioChannels switch
        {
            1 => "Mono",
            2 => "Stereo",
            6 => "5.1",
            8 => "7.1",
            _ => $"{metadata.AudioChannels}ch"
        };

        var sampleRate = metadata.AudioSampleRate > 0
            ? $"{metadata.AudioSampleRate} Hz"
            : "";

        return string.Join(", ",
            new[] { metadata.AudioCodec?.ToUpperInvariant(), channels, sampleRate }
                .Where(s => !string.IsNullOrEmpty(s)));
    }
    
    /// <summary>
    /// Unsubscribes from player property change events to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _playerViewModel.PropertyChanged -= OnPlayerPropertyChanged;
    }
}
