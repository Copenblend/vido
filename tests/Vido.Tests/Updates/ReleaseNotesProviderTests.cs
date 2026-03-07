using Vido.Views.Updates;
using Xunit;

namespace Vido.Tests.Updates;

/// <summary>
/// Tests for <see cref="ReleaseNotesProvider.ExtractVersionSection"/>.
/// </summary>
public sealed class ReleaseNotesProviderTests
{
    private const string SampleContent = """
        # Release Notes

        ## [0.20.0]

        ### What's New
        - Feature A
        - Feature B

        ### Bug Fixes
        - Fixed bug X

        ## [0.19.0]

        ### What's New
        - Feature C

        ## [0.18.0]

        ### What's New
        - Feature D

        ### Bug Fixes
        - Fixed bug Y
        """;

    // ── Exact version match ─────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_ReturnsCorrectSection_ForExactMatch()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "0.20.0");

        Assert.NotNull(result);
        Assert.Contains("Feature A", result);
        Assert.Contains("Feature B", result);
        Assert.Contains("Fixed bug X", result);
        Assert.DoesNotContain("Feature C", result);
        Assert.DoesNotContain("Feature D", result);
    }

    [Fact]
    public void ExtractVersionSection_ReturnsMiddleVersion()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "0.19.0");

        Assert.NotNull(result);
        Assert.Contains("Feature C", result);
        Assert.DoesNotContain("Feature A", result);
        Assert.DoesNotContain("Feature D", result);
    }

    [Fact]
    public void ExtractVersionSection_ReturnsLastVersion()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "0.18.0");

        Assert.NotNull(result);
        Assert.Contains("Feature D", result);
        Assert.Contains("Fixed bug Y", result);
        Assert.DoesNotContain("Feature A", result);
    }

    // ── v prefix handling ───────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_HandlesVPrefix()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "v0.20.0");

        Assert.NotNull(result);
        Assert.Contains("Feature A", result);
    }

    // ── Version not found ───────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_ReturnsNull_WhenVersionNotFound()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "9.9.9");

        Assert.Null(result);
    }

    // ── Empty / null content ────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_ReturnsNull_ForEmptyContent()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection("", "0.20.0");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractVersionSection_ReturnsNull_ForWhitespaceOnlyContent()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection("   \n\n  ", "0.20.0");

        Assert.Null(result);
    }

    // ── Stops at next ## header ─────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_StopsAtNextVersionHeader()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "0.20.0");

        Assert.NotNull(result);
        // Must not contain content from 0.19.0 section
        Assert.DoesNotContain("Feature C", result);
    }

    // ── Trims whitespace ────────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_TrimsLeadingAndTrailingWhitespace()
    {
        var content = """
            ## [1.0.0]

            ### What's New
            - Something

            """;

        var result = ReleaseNotesProvider.ExtractVersionSection(content, "1.0.0");

        Assert.NotNull(result);
        Assert.StartsWith("### What's New", result);
        Assert.EndsWith("- Something", result);
        // No leading/trailing blank lines
        Assert.False(result.StartsWith("\n") || result.StartsWith("\r"));
        Assert.False(result.EndsWith("\n") || result.EndsWith("\r"));
    }

    // ── Empty section ───────────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_ReturnsNull_ForEmptyVersionSection()
    {
        var content = """
            ## [1.0.0]

            ## [0.9.0]

            ### What's New
            - Something
            """;

        var result = ReleaseNotesProvider.ExtractVersionSection(content, "1.0.0");

        Assert.Null(result);
    }

    // ── Case insensitivity ──────────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_IsCaseInsensitive()
    {
        var content = """
            ## [V1.0.0-Beta]

            ### What's New
            - Beta feature
            """;

        var result = ReleaseNotesProvider.ExtractVersionSection(content, "v1.0.0-beta");

        Assert.NotNull(result);
        Assert.Contains("Beta feature", result);
    }

    // ── Single version in file ──────────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_WorksWithSingleVersion()
    {
        var content = """
            # Release Notes

            ## [0.1.0]

            ### What's New
            - First release
            """;

        var result = ReleaseNotesProvider.ExtractVersionSection(content, "0.1.0");

        Assert.NotNull(result);
        Assert.Contains("First release", result);
    }

    // ── Preserves subsection headers ────────────────────────────────────

    [Fact]
    public void ExtractVersionSection_PreservesSubsectionHeaders()
    {
        var result = ReleaseNotesProvider.ExtractVersionSection(SampleContent, "0.20.0");

        Assert.NotNull(result);
        Assert.Contains("### What's New", result);
        Assert.Contains("### Bug Fixes", result);
    }

    // ── No header match for partial version ─────────────────────────────

    [Fact]
    public void ExtractVersionSection_ReturnsNull_WhenNoHeaderContainsVersion()
    {
        var content = """
            # Release Notes

            Some intro text

            No version headers here
            """;

        var result = ReleaseNotesProvider.ExtractVersionSection(content, "1.0.0");

        Assert.Null(result);
    }
}
