using System.Windows.Media.Imaging;

namespace Vido.Views.Services;

/// <summary>
/// Provides the pre-generated 64×64 video file icon from an embedded PNG resource.
/// The icon is loaded once and cached as a frozen BitmapImage for reuse.
/// </summary>
public static class VideoIconProvider
{
    private static BitmapImage? _cachedIcon;
    private static readonly object _lock = new();

    private const string ResourceUri =
        "pack://application:,,,/Vido.Views;component/Assets/video-file-icon.png";

    /// <summary>
    /// Returns the cached 64×64 video file icon as a frozen BitmapImage.
    /// Thread-safe — loads on first call, returns cached instance thereafter.
    /// </summary>
    public static BitmapImage GetVideoFileIcon()
    {
        if (_cachedIcon is not null) return _cachedIcon;
        lock (_lock)
        {
            if (_cachedIcon is not null) return _cachedIcon;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(ResourceUri, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            _cachedIcon = bitmap;
            return _cachedIcon;
        }
    }
}
