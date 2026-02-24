using Vido.Services.Updates;
using Xunit;

namespace Vido.Tests;

public class GitHubUpdateServiceTests
{
    // ── IsNewerVersion ──

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

    [Fact]
    public void IsNewerVersion_DifferentUnparseableStrings_ReturnsTrue()
    {
        Assert.True(GitHubUpdateService.IsNewerVersion("abc", "def"));
    }

    [Fact]
    public void IsNewerVersion_SameUnparseableStrings_ReturnsFalse()
    {
        Assert.False(GitHubUpdateService.IsNewerVersion("abc", "abc"));
    }

    [Fact]
    public void IsNewerVersion_CaseInsensitiveForUnparseable()
    {
        Assert.False(GitHubUpdateService.IsNewerVersion("ABC", "abc"));
    }

    // ── CheckForUpdateAsync with mock HTTP ──

    [Fact]
    public async Task CheckForUpdateAsync_ParsesGitHubResponse()
    {
        // Arrange: mock a GitHub /releases/latest JSON response
        const string json = """
        {
          "tag_name": "v1.0.0",
          "html_url": "https://github.com/Copenblend/vido/releases/tag/v1.0.0",
          "body": "Release notes here",
          "assets": [
            {
              "name": "Vido-Setup-1.0.0.msi",
              "browser_download_url": "https://github.com/Copenblend/vido/releases/download/v1.0.0/Vido-Setup-1.0.0.msi"
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
            "https://github.com/Copenblend/vido/releases/download/v1.0.0/Vido-Setup-1.0.0.msi",
            result.InstallerDownloadUrl);
        Assert.Null(result.ErrorMessage);
    }

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
}
