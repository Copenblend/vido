namespace Vido.Core.Plugin;

/// <summary>
/// Allows a plugin to override Vido's built-in next/previous file navigation.
/// When a provider is registered and active, <see cref="IPluginContext.RegisterPlaylistProvider"/>
/// causes <c>VideoPlayerViewModel</c> to delegate skip-next, skip-previous, and
/// auto-advance-on-media-ended to this provider instead of using the default
/// sibling-file-list logic.
/// </summary>
public interface IPlaylistProvider
{
    /// <summary>
    /// Returns the file path of the next item in the playlist,
    /// or <c>null</c> if there is no next item (e.g. end of playlist).
    /// </summary>
    string? GetNextFile();

    /// <summary>
    /// Returns the file path of the previous item in the playlist,
    /// or <c>null</c> if there is no previous item (e.g. start of playlist).
    /// </summary>
    string? GetPreviousFile();

    /// <summary>
    /// Whether this provider is currently controlling playback.
    /// When <c>false</c>, Vido falls back to its built-in navigation logic.
    /// </summary>
    bool IsActive { get; }
}
