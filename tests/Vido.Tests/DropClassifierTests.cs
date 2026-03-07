using Vido.Core.DragDrop;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="DropClassifier"/> — validates file/folder/unsupported classification
/// for the drag-and-drop feature.
/// </summary>
public sealed class DropClassifierTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public DropClassifierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vido_drop_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { /* temp dir locked momentarily */ }
    }

    // ── Classify ──

    /// <summary>
    /// Verifies that Classify null path returns invalid.
    /// </summary>
    [Fact]
    public void Classify_NullPath_ReturnsInvalid()
    {
        Assert.Equal(DropClassification.Invalid, DropClassifier.Classify(null));
    }

    /// <summary>
    /// Verifies that Classify empty string returns invalid.
    /// </summary>
    [Fact]
    public void Classify_EmptyString_ReturnsInvalid()
    {
        Assert.Equal(DropClassification.Invalid, DropClassifier.Classify(""));
    }

    /// <summary>
    /// Verifies that Classify whitespace only returns invalid.
    /// </summary>
    [Fact]
    public void Classify_WhitespaceOnly_ReturnsInvalid()
    {
        Assert.Equal(DropClassification.Invalid, DropClassifier.Classify("   "));
    }

    /// <summary>
    /// Verifies that Classify non existent path returns invalid.
    /// </summary>
    [Fact]
    public void Classify_NonExistentPath_ReturnsInvalid()
    {
        Assert.Equal(DropClassification.Invalid,
            DropClassifier.Classify(@"C:\nonexistent_path_abc123\file.mp4"));
    }

    /// <summary>
    /// Verifies that Classify existing directory returns folder.
    /// </summary>
    [Fact]
    public void Classify_ExistingDirectory_ReturnsFolder()
    {
        Assert.Equal(DropClassification.Folder, DropClassifier.Classify(_tempDir));
    }

    /// <summary>
    /// Verifies that Classify video file returns video file.
    /// </summary>
    /// <param name="extension">The file extension to classify.</param>
    [Theory]
    [InlineData(".mp4")]
    [InlineData(".avi")]
    [InlineData(".mkv")]
    [InlineData(".mov")]
    [InlineData(".wmv")]
    [InlineData(".flv")]
    [InlineData(".webm")]
    public void Classify_VideoFile_ReturnsVideoFile(string extension)
    {
        var filePath = Path.Combine(_tempDir, $"test{extension}");
        File.WriteAllText(filePath, "dummy");

        Assert.Equal(DropClassification.VideoFile, DropClassifier.Classify(filePath));
    }

    /// <summary>
    /// Verifies that Classify video file case insensitive.
    /// </summary>
    /// <param name="extension">The file extension to classify.</param>
    [Theory]
    [InlineData(".MP4")]
    [InlineData(".Mkv")]
    [InlineData(".AVI")]
    public void Classify_VideoFile_CaseInsensitive(string extension)
    {
        var filePath = Path.Combine(_tempDir, $"test{extension}");
        File.WriteAllText(filePath, "dummy");

        Assert.Equal(DropClassification.VideoFile, DropClassifier.Classify(filePath));
    }

    /// <summary>
    /// Verifies that Classify non video file returns unsupported file.
    /// </summary>
    /// <param name="extension">The file extension to classify.</param>
    [Theory]
    [InlineData(".txt")]
    [InlineData(".jpg")]
    [InlineData(".pdf")]
    [InlineData(".exe")]
    [InlineData(".docx")]
    [InlineData(".zip")]
    public void Classify_NonVideoFile_ReturnsUnsupportedFile(string extension)
    {
        var filePath = Path.Combine(_tempDir, $"test{extension}");
        File.WriteAllText(filePath, "dummy");

        Assert.Equal(DropClassification.UnsupportedFile, DropClassifier.Classify(filePath));
    }

    // ── ClassifyAll ──

    /// <summary>
    /// Verifies that Classify All null array returns empty.
    /// </summary>
    [Fact]
    public void ClassifyAll_NullArray_ReturnsEmpty()
    {
        var results = DropClassifier.ClassifyAll(null);
        Assert.Empty(results);
        Assert.Same(Array.Empty<(DropClassification Classification, string Path)>(), results);
    }

    /// <summary>
    /// Verifies that Classify All empty array returns empty.
    /// </summary>
    [Fact]
    public void ClassifyAll_EmptyArray_ReturnsEmpty()
    {
        var results = DropClassifier.ClassifyAll([]);
        Assert.Empty(results);
        Assert.Same(Array.Empty<(DropClassification Classification, string Path)>(), results);
    }

    /// <summary>
    /// Verifies that Classify All mixed items returns all valid.
    /// </summary>
    [Fact]
    public void ClassifyAll_MixedItems_ReturnsAllValid()
    {
        var videoPath = Path.Combine(_tempDir, "video.mp4");
        var textPath = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(videoPath, "dummy");
        File.WriteAllText(textPath, "dummy");

        var results = DropClassifier.ClassifyAll(new[] { videoPath, _tempDir, textPath });

        Assert.Equal(3, results.Length);
        Assert.Equal(DropClassification.VideoFile, results[0].Classification);
        Assert.Equal(videoPath, results[0].Path);
        Assert.Equal(DropClassification.Folder, results[1].Classification);
        Assert.Equal(_tempDir, results[1].Path);
        Assert.Equal(DropClassification.UnsupportedFile, results[2].Classification);
        Assert.Equal(textPath, results[2].Path);
    }

    /// <summary>
    /// Verifies that Classify All skips invalid paths.
    /// </summary>
    [Fact]
    public void ClassifyAll_SkipsInvalidPaths()
    {
        var videoPath = Path.Combine(_tempDir, "video.mp4");
        File.WriteAllText(videoPath, "dummy");
        var fakePath = @"C:\nonexistent_path_xyz\fake.mp4";

        var results = DropClassifier.ClassifyAll(new[] { fakePath, videoPath });

        Assert.Single(results);
        Assert.Equal(DropClassification.VideoFile, results[0].Classification);
        Assert.Equal(videoPath, results[0].Path);
    }

    /// <summary>
    /// Verifies that Classify All multiple folders returns all.
    /// </summary>
    [Fact]
    public void ClassifyAll_MultipleFolders_ReturnsAll()
    {
        var sub1 = Path.Combine(_tempDir, "sub1");
        var sub2 = Path.Combine(_tempDir, "sub2");
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);

        var results = DropClassifier.ClassifyAll(new[] { sub1, sub2 });

        Assert.Equal(2, results.Length);
        Assert.All(results, r => Assert.Equal(DropClassification.Folder, r.Classification));
    }

    /// <summary>
    /// Verifies that Classify All multiple video files returns all.
    /// </summary>
    [Fact]
    public void ClassifyAll_MultipleVideoFiles_ReturnsAll()
    {
        var video1 = Path.Combine(_tempDir, "a.mp4");
        var video2 = Path.Combine(_tempDir, "b.mkv");
        File.WriteAllText(video1, "dummy");
        File.WriteAllText(video2, "dummy");

        var results = DropClassifier.ClassifyAll(new[] { video1, video2 });

        Assert.Equal(2, results.Length);
        Assert.All(results, r => Assert.Equal(DropClassification.VideoFile, r.Classification));
    }
}