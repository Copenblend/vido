using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for vi-053: AppSettings.ResolveRepositoryCode — maps known codes
/// and direct URLs to registry URLs.
/// </summary>
public sealed class RepositoryCodeTests
{
    [Fact]
    public void ResolveRepositoryCode_Nsfw_Returns_NsfwUrl()
    {
        var result = AppSettings.ResolveRepositoryCode("NSFW");
        Assert.Equal(AppSettings.NsfwRegistryUrl, result);
    }

    [Theory]
    [InlineData("nsfw")]
    [InlineData("Nsfw")]
    [InlineData("NSFW")]
    [InlineData("nSfW")]
    public void ResolveRepositoryCode_Nsfw_CaseInsensitive(string code)
    {
        var result = AppSettings.ResolveRepositoryCode(code);
        Assert.Equal(AppSettings.NsfwRegistryUrl, result);
    }

    [Fact]
    public void ResolveRepositoryCode_HttpsUrl_Returns_Url()
    {
        var url = "https://example.com/registry.json";
        Assert.Equal(url, AppSettings.ResolveRepositoryCode(url));
    }

    [Fact]
    public void ResolveRepositoryCode_HttpUrl_Returns_Url()
    {
        var url = "http://example.com/registry.json";
        Assert.Equal(url, AppSettings.ResolveRepositoryCode(url));
    }

    [Fact]
    public void ResolveRepositoryCode_FileUrl_Returns_Url()
    {
        var url = "file:///C:/test/registry.json";
        Assert.Equal(url, AppSettings.ResolveRepositoryCode(url));
    }

    [Fact]
    public void ResolveRepositoryCode_Unknown_Returns_Null()
    {
        Assert.Null(AppSettings.ResolveRepositoryCode("unknown"));
    }

    [Fact]
    public void ResolveRepositoryCode_Empty_Returns_Null()
    {
        Assert.Null(AppSettings.ResolveRepositoryCode(""));
    }

    [Fact]
    public void ResolveRepositoryCode_RandomText_Returns_Null()
    {
        Assert.Null(AppSettings.ResolveRepositoryCode("some random text"));
    }

    [Fact]
    public void ResolveRepositoryCode_UrlCaseInsensitive()
    {
        var url = "HTTPS://example.com/registry.json";
        Assert.Equal(url, AppSettings.ResolveRepositoryCode(url));
    }
}
