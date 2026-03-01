using Vido.Core.Updates;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="UpdateCheckResult"/> record semantics and defaults.
/// </summary>
public sealed class UpdateCheckResultTests
{
    /// <summary>
    /// Verifies two instances with identical values are equal.
    /// </summary>
    [Fact]
    public void ValueEquality_SameValues_AreEqual()
    {
        var a = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.12.1",
            LatestVersion = "0.13.0",
            ReleaseUrl = "https://example.com/release",
            ReleaseNotes = "notes",
            InstallerDownloadUrl = "https://example.com/installer.msi"
        };

        var b = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.12.1",
            LatestVersion = "0.13.0",
            ReleaseUrl = "https://example.com/release",
            ReleaseNotes = "notes",
            InstallerDownloadUrl = "https://example.com/installer.msi"
        };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// Verifies different values are not equal.
    /// </summary>
    [Fact]
    public void ValueEquality_DifferentValues_AreNotEqual()
    {
        var a = new UpdateCheckResult { IsUpdateAvailable = false };
        var b = new UpdateCheckResult { IsUpdateAvailable = true };

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies with-expression copy-and-mutate behavior.
    /// </summary>
    [Fact]
    public void WithExpression_CopiesAndMutates()
    {
        var original = new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = "0.12.1",
            LatestVersion = "0.13.0"
        };

        var mutated = original with { IsUpdateAvailable = true };

        Assert.False(original.IsUpdateAvailable);
        Assert.True(mutated.IsUpdateAvailable);
        Assert.Equal(original.CurrentVersion, mutated.CurrentVersion);
        Assert.Equal(original.LatestVersion, mutated.LatestVersion);
    }

    /// <summary>
    /// Verifies default property values match model defaults.
    /// </summary>
    [Fact]
    public void DefaultValues_AreExpected()
    {
        var result = new UpdateCheckResult();

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal(string.Empty, result.CurrentVersion);
        Assert.Equal(string.Empty, result.LatestVersion);
        Assert.Null(result.ReleaseUrl);
        Assert.Null(result.ReleaseNotes);
        Assert.Null(result.InstallerDownloadUrl);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// Verifies ToString includes useful property information.
    /// </summary>
    [Fact]
    public void ToString_ContainsPropertyData()
    {
        var result = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.12.1",
            LatestVersion = "0.13.0"
        };

        var text = result.ToString();

        Assert.Contains(nameof(UpdateCheckResult.IsUpdateAvailable), text);
        Assert.Contains("0.12.1", text);
        Assert.Contains("0.13.0", text);
    }
}
