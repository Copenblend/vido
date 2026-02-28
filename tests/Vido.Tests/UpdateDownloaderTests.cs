using Vido.Services.Updates;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Verifies the behavior of <see cref="UpdateDownloader"/>.
/// </summary>
public class UpdateDownloaderTests
{
    /// <summary>
    /// Verifies that Launch Installer returns false when file missing.
    /// </summary>
    [Fact]
    public void LaunchInstaller_ReturnsFalse_WhenFileMissing()
    {
        Assert.False(UpdateDownloader.LaunchInstaller(@"C:\nonexistent\fake.msi"));
    }

    /// <summary>
    /// Verifies that Download Installer Async creates file.
    /// </summary>
    [Fact]
    public async Task DownloadInstallerAsync_CreatesFile()
    {
        // Arrange: serve a small payload via a fake handler
        var content = "fake installer content"u8.ToArray();
        var handler = new FakeDownloadHandler(content);
        using var httpClient = new HttpClient(handler);

        var downloader = new UpdateDownloader(httpClient);

        // Act
        var path = await downloader.DownloadInstallerAsync(
            "https://example.com/Vido-Setup.msi", "test-installer.msi");

        try
        {
            // Assert
            Assert.True(File.Exists(path));
            Assert.Equal(content.Length, new FileInfo(path).Length);
            Assert.EndsWith("test-installer.msi", path);
        }
        finally
        {
            // Cleanup
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies that Download Installer Async reports progress.
    /// </summary>
    [Fact]
    public async Task DownloadInstallerAsync_ReportsProgress()
    {
        var content = new byte[16384]; // 16 KB
        Array.Fill(content, (byte)0xAA);
        var handler = new FakeDownloadHandler(content);
        using var httpClient = new HttpClient(handler);

        var downloader = new UpdateDownloader(httpClient);
        var progressValues = new List<double>();

        var path = await downloader.DownloadInstallerAsync(
            "https://example.com/setup.msi", "progress-test.msi",
            onProgress: p => progressValues.Add(p));

        try
        {
            Assert.NotEmpty(progressValues);
            Assert.Equal(1.0, progressValues[^1]); // Last value should be 1.0
            Assert.All(progressValues, p => Assert.InRange(p, 0.0, 1.0));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies that Download Installer Async overwrites existing file.
    /// </summary>
    [Fact]
    public async Task DownloadInstallerAsync_OverwritesExistingFile()
    {
        var content = "new content"u8.ToArray();
        var handler = new FakeDownloadHandler(content);
        using var httpClient = new HttpClient(handler);

        var downloader = new UpdateDownloader(httpClient);

        // Create a stale file first
        var tempDir = Path.Combine(Path.GetTempPath(), "Vido", "Updates");
        Directory.CreateDirectory(tempDir);
        var stalePath = Path.Combine(tempDir, "overwrite-test.msi");
        await File.WriteAllTextAsync(stalePath, "old content");

        var path = await downloader.DownloadInstallerAsync(
            "https://example.com/setup.msi", "overwrite-test.msi");

        try
        {
            Assert.Equal(content.Length, new FileInfo(path).Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that returns a fixed byte array
    /// as the response body with a known Content-Length.
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
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            };
            response.Content.Headers.ContentLength = _content.Length;
            return Task.FromResult(response);
        }
    }
}