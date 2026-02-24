using System.Diagnostics;

namespace Vido.Services.Updates;

/// <summary>
/// Downloads a release installer to a temp directory and optionally launches it.
/// </summary>
public sealed class UpdateDownloader
{
    private readonly HttpClient _httpClient;

    public UpdateDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Vido-UpdateChecker/1.0");
        return client;
    }

    /// <summary>
    /// Downloads the installer from <paramref name="downloadUrl"/> to a temp folder.
    /// Returns the full local path to the downloaded file.
    /// Reports progress via <paramref name="onProgress"/> (0.0–1.0).
    /// </summary>
    public async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        string fileName,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Vido", "Updates");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, fileName);

        // Delete stale file if it exists
        if (File.Exists(localPath))
            File.Delete(localPath);

        using var response = await _httpClient.GetAsync(
            downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        long bytesRead = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

        var buffer = new byte[8192];
        int read;
        while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesRead += read;
            if (totalBytes > 0)
                onProgress?.Invoke((double)bytesRead / totalBytes);
        }

        onProgress?.Invoke(1.0);
        return localPath;
    }

    /// <summary>
    /// Launches the downloaded installer and returns <c>true</c> if the process started.
    /// The caller should close the application after this returns <c>true</c>.
    /// </summary>
    public static bool LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
            return false;

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });
        return true;
    }
}
