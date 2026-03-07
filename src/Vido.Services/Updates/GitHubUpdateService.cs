using System.Diagnostics;
using System.Text.Json;
using Vido.Core.Updates;

namespace Vido.Services.Updates;

/// <summary>
/// Checks for application updates by querying the GitHub Releases API
/// for the <c>Copenblend/vido</c> repository.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    private const string GitHubApiUrl =
        "https://api.github.com/repos/Copenblend/vido/releases/latest";

    private readonly string _currentVersion;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates an update service that compares the running application version
    /// against the latest GitHub release for the <c>Copenblend/vido</c> repository.
    /// </summary>
    /// <param name="currentVersion">The semantic version string of the currently running application (e.g. "1.2.3").</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for API requests; if <c>null</c>, a default client is created and owned by this instance.</param>
    public GitHubUpdateService(string currentVersion, HttpClient? httpClient = null)
    {
        _currentVersion = currentVersion;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Vido-UpdateChecker/1.0");
            _ownsHttpClient = true;
        }
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync(GitHubApiUrl);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString();
            var body = root.TryGetProperty("body", out var bodyProp)
                ? bodyProp.GetString()
                : null;

            // Find the portable zip asset
            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains("portable", StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        installerUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            var isNewer = IsNewerVersion(tagName, _currentVersion);

            return new UpdateCheckResult
            {
                IsUpdateAvailable = isNewer,
                CurrentVersion = _currentVersion,
                LatestVersion = tagName,
                ReleaseUrl = htmlUrl,
                ReleaseNotes = body,
                InstallerDownloadUrl = installerUrl
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = _currentVersion,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Compares two semantic version strings. Returns <c>true</c> if
    /// <paramref name="latest"/> is strictly greater than <paramref name="current"/>.
    /// </summary>
    internal static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) &&
            Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }

        // Unparseable â€” different strings treated as "newer" only if they differ
        return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<string> DownloadUpdateAsync(
        string downloadUrl, string fileName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloader = new UpdateDownloader(_httpClient);
        return await downloader.DownloadInstallerAsync(
            downloadUrl, fileName,
            p => progress?.Report(p),
            cancellationToken);
    }

    /// <inheritdoc />
    public bool ApplyUpdate(string updateZipPath)
    {
        var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar);
        var pid = Environment.ProcessId;

        var scriptDir = Path.Combine(Path.GetTempPath(), "Vido", "Updates");
        Directory.CreateDirectory(scriptDir);
        var scriptPath = Path.Combine(scriptDir, "apply-update.ps1");

        var script = GenerateApplyUpdateScript(updateZipPath, installDir, pid);
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        return true; // Caller should exit
    }

    /// <summary>
    /// Generates the PowerShell script content for applying an update.
    /// Separated for testability.
    /// </summary>
    internal static string GenerateApplyUpdateScript(
        string updateZipPath, string installDir, int pid)
    {
        return $$"""
            param()
            try { Wait-Process -Id {{pid}} -Timeout 30 -ErrorAction SilentlyContinue } catch {}
            Start-Sleep -Seconds 1
            Expand-Archive -Path '{{updateZipPath}}' -DestinationPath '{{installDir}}' -Force
            Remove-Item '{{updateZipPath}}' -Force -ErrorAction SilentlyContinue
            Start-Process (Join-Path '{{installDir}}' 'Vido.exe')
            """;
    }

    /// <summary>
    /// Downloads the installer from the given URL to a temp directory and returns the local file path.
    /// </summary>
    /// <param name="downloadUrl">The URL of the installer asset to download.</param>
    /// <param name="fileName">The file name to use when saving the downloaded installer locally.</param>
    public async Task<string> DownloadInstallerAsync(string downloadUrl, string fileName)
    {
        var downloader = new UpdateDownloader(_httpClient);
        return await downloader.DownloadInstallerAsync(downloadUrl, fileName);
    }

    /// <summary>
    /// Starts the installer process via shell execution so the user can complete the update.
    /// Returns <c>true</c> if the process was launched; the caller should exit the application afterward.
    /// </summary>
    /// <param name="installerPath">The local file path of the downloaded installer to launch.</param>
    public bool LaunchInstaller(string installerPath)
    {
        return UpdateDownloader.LaunchInstaller(installerPath);
    }
    
    /// <summary>
    /// Releases the internally-created <see cref="HttpClient"/> if this instance owns it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
