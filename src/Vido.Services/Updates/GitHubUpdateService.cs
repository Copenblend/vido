using System.Net.Http.Json;
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

            // Find the installer asset (.msi or *Setup* file)
            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
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

        // Unparseable — different strings treated as "newer" only if they differ
        return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<string> DownloadInstallerAsync(string downloadUrl, string fileName)
    {
        var downloader = new UpdateDownloader(_httpClient);
        return await downloader.DownloadInstallerAsync(downloadUrl, fileName);
    }

    /// <inheritdoc />
    public bool LaunchInstaller(string installerPath)
    {
        return UpdateDownloader.LaunchInstaller(installerPath);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
