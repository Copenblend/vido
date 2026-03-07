namespace Vido.Core.Updates;

/// <summary>
/// Checks for application updates via a remote source (e.g., GitHub Releases)
/// and provides download / launch functionality.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for a newer version of the application.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdateAsync();

    /// <summary>
    /// Downloads the installer from the given URL to a temp directory.
    /// Returns the full local path to the downloaded file.
    /// </summary>
    Task<string> DownloadInstallerAsync(string downloadUrl, string fileName);

    /// <summary>
    /// Downloads the update package (portable zip) with progress reporting.
    /// Returns the local path to the downloaded file.
    /// </summary>
    Task<string> DownloadUpdateAsync(
        string downloadUrl,
        string fileName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an update-apply script to temp, launches it, and returns true
    /// if the caller should exit the application.
    /// The script waits for the current process to exit, extracts the zip
    /// to the install directory, and relaunches Vido.exe.
    /// </summary>
    bool ApplyUpdate(string updateZipPath);

    /// <summary>
    /// Launches the downloaded installer executable.
    /// Returns <c>true</c> if the process started successfully.
    /// </summary>
    bool LaunchInstaller(string installerPath);
}
