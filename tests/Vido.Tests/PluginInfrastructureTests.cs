using Vido.Core.Plugin;
using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for vi-b-002 additions: AppSettings.PluginRegistryUrls,
/// PluginIconConstants, and SettingContribution model properties.
/// </summary>
public sealed class PluginInfrastructureTests
{
    // ── AppSettings.PluginRegistryUrls ──

    [Fact]
    public void PluginRegistryUrls_DefaultContainsOfficialUrl()
    {
        var settings = new AppSettings();

        Assert.Single(settings.PluginRegistryUrls);
        Assert.Equal(AppSettings.OfficialRegistryUrl, settings.PluginRegistryUrls[0]);
    }

    [Fact]
    public void PluginRegistryUrls_CanAddCustomUrl()
    {
        var settings = new AppSettings();
        settings.PluginRegistryUrls.Add("https://custom.example.com/plugins");

        Assert.Equal(2, settings.PluginRegistryUrls.Count);
        Assert.Equal(AppSettings.OfficialRegistryUrl, settings.PluginRegistryUrls[0]);
    }

    [Fact]
    public void PluginRegistryUrls_SupportsFileProtocol()
    {
        var settings = new AppSettings();
        settings.PluginRegistryUrls.Add("file:///C:/my-plugins/registry.json");

        Assert.Equal(2, settings.PluginRegistryUrls.Count);
    }

    [Fact]
    public void OfficialRegistryUrl_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppSettings.OfficialRegistryUrl));
        Assert.StartsWith("https://", AppSettings.OfficialRegistryUrl);
    }

    [Fact]
    public void NsfwRegistryUrl_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppSettings.NsfwRegistryUrl));
        Assert.StartsWith("https://", AppSettings.NsfwRegistryUrl);
    }

    [Fact]
    public void OfficialRegistryUrls_Contains_Both_Urls()
    {
        Assert.Contains(AppSettings.OfficialRegistryUrl, AppSettings.OfficialRegistryUrls);
        Assert.Contains(AppSettings.NsfwRegistryUrl, AppSettings.OfficialRegistryUrls);
        Assert.Equal(2, AppSettings.OfficialRegistryUrls.Count);
    }

    [Fact]
    public void OfficialRegistryUrls_IsCaseInsensitive()
    {
        Assert.Contains(AppSettings.OfficialRegistryUrl.ToUpperInvariant(), AppSettings.OfficialRegistryUrls);
        Assert.Contains(AppSettings.NsfwRegistryUrl.ToUpperInvariant(), AppSettings.OfficialRegistryUrls);
    }

    [Fact]
    public void PluginRegistryUrls_Default_DoesNotContainNsfwUrl()
    {
        var settings = new AppSettings();
        Assert.DoesNotContain(AppSettings.NsfwRegistryUrl, settings.PluginRegistryUrls);
    }

    // ── SettingContribution model ──

    [Fact]
    public void ValidTypes_ContainsAllFiveTypes()
    {
        Assert.Contains("boolean", SettingContribution.ValidTypes);
        Assert.Contains("string", SettingContribution.ValidTypes);
        Assert.Contains("number", SettingContribution.ValidTypes);
        Assert.Contains("enum", SettingContribution.ValidTypes);
        Assert.Contains("folderPath", SettingContribution.ValidTypes);
        Assert.Equal(5, SettingContribution.ValidTypes.Count);
    }

    [Fact]
    public void ValidTypes_CaseInsensitive()
    {
        Assert.Contains("Boolean", SettingContribution.ValidTypes);
        Assert.Contains("STRING", SettingContribution.ValidTypes);
        Assert.Contains("FolderPath", SettingContribution.ValidTypes);
    }

    [Fact]
    public void SettingContribution_DefaultPropertyValues()
    {
        var s = new SettingContribution();

        Assert.Equal(string.Empty, s.Id);
        Assert.Equal("string", s.Type);
        Assert.Null(s.Default);
        Assert.Equal(string.Empty, s.Title);
        Assert.Equal(string.Empty, s.Description);
        Assert.Empty(s.EnumValues);
        Assert.Null(s.Section);
        Assert.False(s.ForceOverride);
    }

    [Fact]
    public void SettingContribution_Section_CanBeSet()
    {
        var s = new SettingContribution { Section = "Advanced" };
        Assert.Equal("Advanced", s.Section);
    }

    [Fact]
    public void SettingContribution_ForceOverride_CanBeSet()
    {
        var s = new SettingContribution { ForceOverride = true };
        Assert.True(s.ForceOverride);
    }

    [Fact]
    public void SettingContribution_EnumValues_CanBePopulated()
    {
        var s = new SettingContribution
        {
            Type = "enum",
            EnumValues = ["fast", "slow", "auto"]
        };

        Assert.Equal(3, s.EnumValues.Count);
        Assert.Contains("auto", s.EnumValues);
    }
}
