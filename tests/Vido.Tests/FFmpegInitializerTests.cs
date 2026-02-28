using Vido.Services.Video;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for FFmpegInitializer path resolution and library detection logic.
/// These tests verify path resolution without requiring actual FFmpeg DLLs.
/// </summary>
public class FFmpegInitializerTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public FFmpegInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vido_ffmpeg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Verifies that Contains F Fmpeg Libraries returns false when directory does not exist.
    /// </summary>
    [Fact]
    public void ContainsFFmpegLibraries_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        var result = FFmpegInitializer.ContainsFFmpegLibraries(
            Path.Combine(_tempDir, "nonexistent"));

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Contains F Fmpeg Libraries returns false when directory is empty.
    /// </summary>
    [Fact]
    public void ContainsFFmpegLibraries_ReturnsFalse_WhenDirectoryIsEmpty()
    {
        var result = FFmpegInitializer.ContainsFFmpegLibraries(_tempDir);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Contains F Fmpeg Libraries returns false when no avcodec dll.
    /// </summary>
    [Fact]
    public void ContainsFFmpegLibraries_ReturnsFalse_WhenNoAvcodecDll()
    {
        // Create other DLLs but not avcodec
        File.WriteAllText(Path.Combine(_tempDir, "avformat-62.dll"), "");
        File.WriteAllText(Path.Combine(_tempDir, "avutil-60.dll"), "");

        var result = FFmpegInitializer.ContainsFFmpegLibraries(_tempDir);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Contains F Fmpeg Libraries returns true when avcodec versioned dll exists.
    /// </summary>
    [Fact]
    public void ContainsFFmpegLibraries_ReturnsTrue_WhenAvcodecVersionedDllExists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "avcodec-62.dll"), "");

        var result = FFmpegInitializer.ContainsFFmpegLibraries(_tempDir);

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Contains F Fmpeg Libraries returns true when avcodec dll exists.
    /// </summary>
    [Fact]
    public void ContainsFFmpegLibraries_ReturnsTrue_WhenAvcodecDllExists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "avcodec.dll"), "");

        var result = FFmpegInitializer.ContainsFFmpegLibraries(_tempDir);

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Contains F Fmpeg Libraries returns true when avcodec wildcard match.
    /// </summary>
    [Fact]
    public void ContainsFFmpegLibraries_ReturnsTrue_WhenAvcodecWildcardMatch()
    {
        // Matches avcodec*.dll pattern (different version number)
        File.WriteAllText(Path.Combine(_tempDir, "avcodec-60.dll"), "");

        var result = FFmpegInitializer.ContainsFFmpegLibraries(_tempDir);

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Version String is null or non empty.
    /// </summary>
    [Fact]
    public void VersionString_IsNullOrNonEmpty()
    {
        // VersionString is null if FFmpeg hasn't been initialized in this test run,
        // or a non-empty string if it has (e.g. in integration tests).
        var version = FFmpegInitializer.VersionString;
        if (version is not null)
        {
            Assert.NotEmpty(version);
        }
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}