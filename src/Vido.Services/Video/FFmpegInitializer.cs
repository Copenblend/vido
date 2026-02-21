using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using Vido.Core.Logging;

namespace Vido.Services.Video;

/// <summary>
/// Locates and initializes FFmpeg shared libraries.
/// Must be called once at application startup before any FFmpeg operations.
/// </summary>
public static class FFmpegInitializer
{
    private static bool _isInitialized;
    private static readonly object _lock = new();

    /// <summary>
    /// Whether FFmpeg has been successfully initialized.
    /// </summary>
    public static bool IsInitialized
    {
        get { lock (_lock) return _isInitialized; }
    }

    /// <summary>
    /// Initializes FFmpeg by locating DLLs in the standard search paths.
    /// Search order:
    ///   1. The application's base directory (where NuGet runtimes DLLs are copied)
    ///   2. runtimes/win-x64/native/ subdirectory (NuGet native package fallback)
    /// </summary>
    /// <param name="logService">Optional log service for diagnostic output.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    public static bool Initialize(ILogService? logService = null)
    {
        lock (_lock)
        {
            if (_isInitialized)
                return true;

            var ffmpegPath = ResolveFFmpegPath();
            if (ffmpegPath is null)
            {
                logService?.Warning("FFmpeg DLLs not found. Video playback will be unavailable.");
                return false;
            }

            try
            {
                DynamicallyLoadedBindings.LibrariesPath = ffmpegPath;
                DynamicallyLoadedBindings.Initialize();
                _isInitialized = true;

                logService?.Info($"FFmpeg initialized from: {ffmpegPath}");
                return true;
            }
            catch (Exception ex)
            {
                logService?.Error($"FFmpeg initialization failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Resolves the path to the directory containing FFmpeg DLLs.
    /// Returns null if no valid path is found.
    /// </summary>
    internal static string? ResolveFFmpegPath()
    {
        var baseDir = AppContext.BaseDirectory;

        // Priority 1: Application base directory (NuGet runtimes DLLs are copied here)
        if (ContainsFFmpegLibraries(baseDir))
            return baseDir;

        // Priority 2: runtimes/win-x64/native/ subdirectory (NuGet native package fallback)
        var runtimesDir = Path.Combine(baseDir, "runtimes", "win-x64", "native");
        if (ContainsFFmpegLibraries(runtimesDir))
            return runtimesDir;

        return null;
    }

    /// <summary>
    /// Checks whether a directory contains at least the core FFmpeg library (avcodec).
    /// </summary>
    internal static bool ContainsFFmpegLibraries(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return false;

        // Check for the core FFmpeg library — avcodec is always required
        return File.Exists(Path.Combine(directoryPath, "avcodec-62.dll"))
            || File.Exists(Path.Combine(directoryPath, "avcodec.dll"))
            || Directory.GetFiles(directoryPath, "avcodec*.dll").Length > 0;
    }
}
