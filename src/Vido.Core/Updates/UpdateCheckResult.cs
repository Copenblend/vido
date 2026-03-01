namespace Vido.Core.Updates;

/// <summary>
/// Result of an update check against a remote release source.
/// </summary>
public sealed record UpdateCheckResult
{
    /// <summary>
    /// Whether a newer version is available.
    /// </summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>
    /// The currently running version string.
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// The latest available version string from the remote source.
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// URL to the release page (for manual download).
    /// </summary>
    public string? ReleaseUrl { get; init; }

    /// <summary>
    /// Release notes / changelog body.
    /// </summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// Direct download URL for the installer asset (.msi).
    /// </summary>
    public string? InstallerDownloadUrl { get; init; }

    /// <summary>
    /// Error message if the check failed; null on success.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
