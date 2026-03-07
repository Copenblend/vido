using Vido.Core.Settings;
using Vido.Services.Updates;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="GitHubUpdateService"/>.
/// </summary>
public class GitHubUpdateServiceTests
{
    // ── IsNewerVersion ──

    /// <summary>
    /// Verifies that Is Newer Version compares correctly.
    /// </summary>
    /// <param name="latest">The latest available version string.</param>
    /// <param name="current">The current installed version string.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData("1.0.0", "0.6.0", true)]
    [InlineData("0.7.0", "0.6.0", true)]
    [InlineData("0.6.1", "0.6.0", true)]
    [InlineData("0.6.0", "0.6.0", false)]
    [InlineData("0.5.0", "0.6.0", false)]
    [InlineData("0.6.0", "0.7.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("2.0.0", "1.9.9", true)]
    public void IsNewerVersion_ComparesCorrectly(string latest, string current, bool expected)
    {
        Assert.Equal(expected, GitHubUpdateService.IsNewerVersion(latest, current));
    }

    /// <summary>
    /// Verifies that Is Newer Version different unparseable strings returns true.
    /// </summary>
    [Fact]
    public void IsNewerVersion_DifferentUnparseableStrings_ReturnsTrue()
    {
        Assert.True(GitHubUpdateService.IsNewerVersion("abc", "def"));
    }

    /// <summary>
    /// Verifies that Is Newer Version same unparseable strings returns false.
    /// </summary>
    [Fact]
    public void IsNewerVersion_SameUnparseableStrings_ReturnsFalse()
    {
        Assert.False(GitHubUpdateService.IsNewerVersion("abc", "abc"));
    }

    /// <summary>
    /// Verifies that Is Newer Version case insensitive for unparseable.
    /// </summary>
    [Fact]
    public void IsNewerVersion_CaseInsensitiveForUnparseable()
    {
        Assert.False(GitHubUpdateService.IsNewerVersion("ABC", "abc"));
    }

    // ── CheckForUpdateAsync with mock HTTP ──

    /// <summary>
    /// Verifies that Check For Update Async parses git hub response.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_ParsesGitHubResponse()
    {
        // Arrange: mock a GitHub /releases/latest JSON response with portable zip
        const string json = """
        {
          "tag_name": "v1.0.0",
          "html_url": "https://github.com/Copenblend/vido/releases/tag/v1.0.0",
          "body": "Release notes here",
          "assets": [
            {
              "name": "Vido-1.0.0-win-x64-portable.zip",
              "browser_download_url": "https://github.com/Copenblend/vido/releases/download/v1.0.0/Vido-1.0.0-win-x64-portable.zip"
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        // Act
        var result = await sut.CheckForUpdateAsync();

        // Assert
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("0.6.0", result.CurrentVersion);
        Assert.Equal("1.0.0", result.LatestVersion);
        Assert.Equal("https://github.com/Copenblend/vido/releases/tag/v1.0.0", result.ReleaseUrl);
        Assert.Equal("Release notes here", result.ReleaseNotes);
        Assert.Equal(
            "https://github.com/Copenblend/vido/releases/download/v1.0.0/Vido-1.0.0-win-x64-portable.zip",
            result.InstallerDownloadUrl);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// Verifies that Check For Update Async same version not available.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_SameVersion_NotAvailable()
    {
        const string json = """
        {
          "tag_name": "v0.6.0",
          "html_url": "https://github.com/Copenblend/vido/releases/tag/v0.6.0",
          "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("0.6.0", result.LatestVersion);
        Assert.Null(result.InstallerDownloadUrl);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// Verifies that Check For Update Async older version not available.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_OlderVersion_NotAvailable()
    {
        const string json = """
        {
          "tag_name": "v0.5.0",
          "html_url": "https://github.com/Copenblend/vido/releases/tag/v0.5.0",
          "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    /// <summary>
    /// Verifies that Check For Update Async network error returns error message.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_NetworkError_ReturnsErrorMessage()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Network unreachable"));
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("0.6.0", result.CurrentVersion);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Network unreachable", result.ErrorMessage);
    }

    /// <summary>
    /// Verifies that Check For Update Async no installer asset returns null installer url.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_NoInstallerAsset_ReturnsNullInstallerUrl()
    {
        const string json = """
        {
          "tag_name": "v1.0.0",
          "html_url": "https://github.com/Copenblend/vido/releases/tag/v1.0.0",
          "assets": [
            {
              "name": "source-code.zip",
              "browser_download_url": "https://example.com/source.zip"
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.InstallerDownloadUrl);
    }

    /// <summary>
    /// Verifies that Check For Update Async strips leading v from tag.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_StripsLeadingV_FromTag()
    {
        const string json = """
        {
          "tag_name": "v2.1.3",
          "html_url": "https://example.com",
          "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.Equal("2.1.3", result.LatestVersion);
    }

    /// <summary>
    /// Verifies that Check For Update Async no body returns null release notes.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_NoBody_ReturnsNullReleaseNotes()
    {
        const string json = """
        {
          "tag_name": "v1.0.0",
          "html_url": "https://example.com",
          "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.Null(result.ReleaseNotes);
    }

    // ── Fake HTTP handler ──

    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that returns a fixed JSON response
    /// or throws a given exception — used for unit testing without network access.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string? _responseContent;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        public FakeHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception is not null)
                throw _exception;

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent!, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    // ── Asset search ──

    /// <summary>
    /// Verifies that CheckForUpdateAsync finds a portable zip asset and ignores MSI installers.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_FindsPortableZip_IgnoresMsi()
    {
        const string json = """
        {
          "tag_name": "v2.0.0",
          "html_url": "https://example.com",
          "assets": [
            {
              "name": "Vido-Setup-2.0.0.msi",
              "browser_download_url": "https://example.com/setup.msi"
            },
            {
              "name": "Vido-2.0.0-win-x64-portable.zip",
              "browser_download_url": "https://example.com/portable.zip"
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("1.0.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.Equal("https://example.com/portable.zip", result.InstallerDownloadUrl);
    }

    /// <summary>
    /// Verifies that CheckForUpdateAsync returns null when only MSI assets are present.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_OnlyMsiAsset_ReturnsNullUrl()
    {
        const string json = """
        {
          "tag_name": "v2.0.0",
          "html_url": "https://example.com",
          "assets": [
            {
              "name": "Vido-Setup-2.0.0.msi",
              "browser_download_url": "https://example.com/setup.msi"
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("1.0.0", httpClient);

        var result = await sut.CheckForUpdateAsync();

        Assert.Null(result.InstallerDownloadUrl);
    }

    // ── GenerateApplyUpdateScript ──

    /// <summary>
    /// Verifies that the generated update script contains the correct PID.
    /// </summary>
    [Fact]
    public void GenerateApplyUpdateScript_ContainsCorrectPid()
    {
        var script = GitHubUpdateService.GenerateApplyUpdateScript(
            @"C:\temp\update.zip", @"C:\Vido", 12345);

        Assert.Contains("12345", script);
        Assert.Contains("Wait-Process -Id 12345", script);
    }

    /// <summary>
    /// Verifies that the generated update script contains the correct paths.
    /// </summary>
    [Fact]
    public void GenerateApplyUpdateScript_ContainsCorrectPaths()
    {
        var zipPath = @"C:\temp\Vido\Updates\Vido-1.0.0-portable.zip";
        var installDir = @"C:\Users\Test\AppData\Local\Vido";
        var script = GitHubUpdateService.GenerateApplyUpdateScript(
            zipPath, installDir, 99999);

        Assert.Contains($"Expand-Archive -Path '{zipPath}'", script);
        Assert.Contains($"-DestinationPath '{installDir}'", script);
        Assert.Contains($"Remove-Item '{zipPath}'", script);
        Assert.Contains($"Join-Path '{installDir}' 'Vido.exe'", script);
    }

    /// <summary>
    /// Verifies that the generated update script has valid PowerShell structure.
    /// </summary>
    [Fact]
    public void GenerateApplyUpdateScript_HasValidPowerShellStructure()
    {
        var script = GitHubUpdateService.GenerateApplyUpdateScript(
            @"C:\update.zip", @"C:\Vido", 1);

        Assert.Contains("param()", script);
        Assert.Contains("Wait-Process", script);
        Assert.Contains("Start-Sleep", script);
        Assert.Contains("Expand-Archive", script);
        Assert.Contains("Start-Process", script);
        Assert.Contains("-Force", script);
    }

    // ── DownloadUpdateAsync ──

    /// <summary>
    /// Verifies that DownloadUpdateAsync reports progress during download.
    /// </summary>
    [Fact]
    public async Task DownloadUpdateAsync_ReportsProgress()
    {
        var content = new byte[1024];
        Array.Fill(content, (byte)'A');
        var handler = new FakeDownloadHandler(content);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);

        var progressValues = new List<double>();
        var progress = new Progress<double>(p => progressValues.Add(p));

        var fileName = $"test-download-{Guid.NewGuid():N}.zip";
        try
        {
            var path = await sut.DownloadUpdateAsync(
                "https://example.com/update.zip", fileName, progress);

            // Allow progress callbacks to fire
            await Task.Delay(100);

            Assert.NotEmpty(progressValues);
            Assert.True(File.Exists(path));
        }
        finally
        {
            // Cleanup
            var tempPath = Path.Combine(Path.GetTempPath(), "Vido", "Updates", fileName);
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that DownloadUpdateAsync supports cancellation.
    /// </summary>
    [Fact]
    public async Task DownloadUpdateAsync_SupportsCancellation()
    {
        var content = new byte[1024 * 1024]; // Large enough to not complete instantly
        var handler = new FakeDownloadHandler(content);
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Test/1.0");
        using var sut = new GitHubUpdateService("0.6.0", httpClient);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var fileName = $"test-cancel-{Guid.NewGuid():N}.zip";
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.DownloadUpdateAsync(
                "https://example.com/update.zip", fileName, cancellationToken: cts.Token));
    }

    // ── AppSettings.AutoCheckUpdates ──

    /// <summary>
    /// Verifies that AutoCheckUpdates defaults to true.
    /// </summary>
    [Fact]
    public void AppSettings_AutoCheckUpdates_DefaultsToTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.AutoCheckUpdates);
    }

    /// <summary>
    /// Verifies that ResetToDefaults restores AutoCheckUpdates to true.
    /// </summary>
    [Fact]
    public void AppSettings_ResetToDefaults_RestoresAutoCheckUpdates()
    {
        var settings = new AppSettings { AutoCheckUpdates = false };
        settings.ResetToDefaults();
        Assert.True(settings.AutoCheckUpdates);
    }

    // ── Fake download handler ──

    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that returns binary content with a Content-Length
    /// header — used for testing download with progress reporting.
    /// </summary>
    private sealed class FakeDownloadHandler : HttpMessageHandler
    {
        private readonly byte[] _content;

        public FakeDownloadHandler(byte[] content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            };
            response.Content.Headers.ContentLength = _content.Length;
            return Task.FromResult(response);
        }
    }
}