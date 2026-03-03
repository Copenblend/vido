using Vido.Core.Plugin;
using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for vi-b-002 additions: SettingContribution model properties.
/// Plugin registry URL tests removed in PI-003 (plugin settings removed from AppSettings).
/// </summary>
public sealed class PluginInfrastructureTests
{
    // ── SettingContribution model ──

    /// <summary>
    /// Verifies that Valid Types contains all five types.
    /// </summary>
    [Fact]
    public void ValidTypes_ContainsAllFiveTypes()
    {
        Assert.Contains("boolean", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Contains("string", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Contains("number", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Contains("enum", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Contains("folderPath", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Equal(5, SettingContribution.ValidTypes.Count);
    }

    /// <summary>
    /// Verifies that Valid Types case insensitive.
    /// </summary>
    [Fact]
    public void ValidTypes_CaseInsensitive()
    {
        Assert.Contains("Boolean", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Contains("STRING", (ISet<string>)SettingContribution.ValidTypes);
        Assert.Contains("FolderPath", (ISet<string>)SettingContribution.ValidTypes);
    }

    /// <summary>
    /// Verifies that Setting Contribution default property values.
    /// </summary>
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

    /// <summary>
    /// Verifies that Setting Contribution section can be set.
    /// </summary>
    [Fact]
    public void SettingContribution_Section_CanBeSet()
    {
        var s = new SettingContribution { Section = "Advanced" };
        Assert.Equal("Advanced", s.Section);
    }

    /// <summary>
    /// Verifies that Setting Contribution force override can be set.
    /// </summary>
    [Fact]
    public void SettingContribution_ForceOverride_CanBeSet()
    {
        var s = new SettingContribution { ForceOverride = true };
        Assert.True(s.ForceOverride);
    }

    /// <summary>
    /// Verifies that Setting Contribution enum values can be populated.
    /// </summary>
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