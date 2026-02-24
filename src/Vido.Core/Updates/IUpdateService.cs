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
    /// Launches the downloaded installer executable.
    /// Returns <c>true</c> if the process started successfully.
    /// </summary>
    bool LaunchInstaller(string installerPath);
}
