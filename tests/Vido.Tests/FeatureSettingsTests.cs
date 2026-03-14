using Vido.Core.Layout;
using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-003: Feature Settings and Core Layout Changes.
/// Covers <see cref="AxisSettingsData"/>, <see cref="SettingDefinition"/>,
/// <see cref="SettingValidation"/>, <see cref="SidebarPanelKind"/>,
/// and new <see cref="AppSettings"/> feature properties.
/// </summary>
public sealed class FeatureSettingsTests
{
    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ AxisSettingsData Tests                                         ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that AxisSettingsData default property values are correct.
    /// </summary>
    [Fact]
    public void AxisSettingsData_DefaultValues_AreCorrect()
    {
        var axis = new AxisSettingsData();

        Assert.Equal(0, axis.Min);
        Assert.Equal(100, axis.Max);
        Assert.True(axis.Enabled);
        Assert.Equal("None", axis.FillMode);
        Assert.True(axis.SyncWithStroke);
        Assert.Equal(1.0, axis.FillSpeedHz);
        Assert.Equal(0.0, axis.PositionOffset);
    }

    /// <summary>
    /// Verifies that CreateDefaults returns exactly 4 axes.
    /// </summary>
    [Fact]
    public void CreateDefaults_Returns_FourAxes()
    {
        var defaults = AxisSettingsData.CreateDefaults();

        Assert.Equal(4, defaults.Count);
        Assert.Contains("L0", defaults.Keys);
        Assert.Contains("R0", defaults.Keys);
        Assert.Contains("R1", defaults.Keys);
        Assert.Contains("R2", defaults.Keys);
    }

    /// <summary>
    /// Verifies that CreateDefaults L0 axis has standard defaults.
    /// </summary>
    [Fact]
    public void CreateDefaults_L0_HasStandardDefaults()
    {
        var defaults = AxisSettingsData.CreateDefaults();
        var l0 = defaults["L0"];

        Assert.Equal(0, l0.Min);
        Assert.Equal(100, l0.Max);
        Assert.True(l0.Enabled);
        Assert.True(l0.SyncWithStroke);
    }

    /// <summary>
    /// Verifies that CreateDefaults R0 axis has standard defaults.
    /// </summary>
    [Fact]
    public void CreateDefaults_R0_HasStandardDefaults()
    {
        var defaults = AxisSettingsData.CreateDefaults();
        var r0 = defaults["R0"];

        Assert.Equal(0, r0.Min);
        Assert.Equal(100, r0.Max);
        Assert.True(r0.SyncWithStroke);
    }

    /// <summary>
    /// Verifies that CreateDefaults R1 axis has standard defaults.
    /// </summary>
    [Fact]
    public void CreateDefaults_R1_HasStandardDefaults()
    {
        var defaults = AxisSettingsData.CreateDefaults();
        var r1 = defaults["R1"];

        Assert.Equal(0, r1.Min);
        Assert.Equal(100, r1.Max);
        Assert.True(r1.SyncWithStroke);
    }

    /// <summary>
    /// Verifies that CreateDefaults R2 axis has SyncWithStroke false.
    /// </summary>
    [Fact]
    public void CreateDefaults_R2_SyncWithStrokeFalse()
    {
        var defaults = AxisSettingsData.CreateDefaults();
        var r2 = defaults["R2"];

        Assert.Equal(0, r2.Min);
        Assert.Equal(100, r2.Max);
        Assert.False(r2.SyncWithStroke);
    }

    /// <summary>
    /// Verifies that all properties on AxisSettingsData are mutable.
    /// </summary>
    [Fact]
    public void AxisSettingsData_Properties_AreMutable()
    {
        var axis = new AxisSettingsData
        {
            Min = 10,
            Max = 90,
            Enabled = false,
            FillMode = "Sin",
            SyncWithStroke = false,
            FillSpeedHz = 2.5,
            PositionOffset = 15.0
        };

        Assert.Equal(10, axis.Min);
        Assert.Equal(90, axis.Max);
        Assert.False(axis.Enabled);
        Assert.Equal("Sin", axis.FillMode);
        Assert.False(axis.SyncWithStroke);
        Assert.Equal(2.5, axis.FillSpeedHz);
        Assert.Equal(15.0, axis.PositionOffset);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SettingDefinition Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that SettingDefinition record construction sets all required properties.
    /// </summary>
    [Fact]
    public void SettingDefinition_Construction_SetsRequiredProperties()
    {
        var def = new SettingDefinition(
            Key: "test.key",
            Type: "boolean",
            DefaultValue: true,
            Title: "Test Setting",
            Description: "A test setting");

        Assert.Equal("test.key", def.Key);
        Assert.Equal("boolean", def.Type);
        Assert.Equal(true, def.DefaultValue);
        Assert.Equal("Test Setting", def.Title);
        Assert.Equal("A test setting", def.Description);
    }

    /// <summary>
    /// Verifies that SettingDefinition optional properties default to null.
    /// </summary>
    [Fact]
    public void SettingDefinition_OptionalProperties_DefaultNull()
    {
        var def = new SettingDefinition("key", "string", "default", "Title", "Desc");

        Assert.Null(def.Section);
        Assert.Null(def.EnumValues);
        Assert.Null(def.Validation);
        Assert.Null(def.Getter);
        Assert.Null(def.Setter);
    }

    /// <summary>
    /// Verifies that SettingDefinition with all optional params set.
    /// </summary>
    [Fact]
    public void SettingDefinition_AllOptionalParams_Set()
    {
        var enumValues = new List<string> { "A", "B", "C" };
        var validation = new SettingValidation(Min: 0, Max: 100);
        Func<AppSettings, object?> getter = s => s.Osr2UdpPort;
        Action<AppSettings, object?> setter = (s, v) => s.Osr2UdpPort = (int)v!;

        var def = new SettingDefinition(
            Key: "osr2.udpPort",
            Type: "number",
            DefaultValue: 7777,
            Title: "UDP Port",
            Description: "Port number",
            Section: "Connection",
            EnumValues: enumValues,
            Validation: validation,
            Getter: getter,
            Setter: setter);

        Assert.Equal("Connection", def.Section);
        Assert.Equal(3, def.EnumValues!.Count);
        Assert.Equal(0, def.Validation!.Min);
        Assert.Equal(100, def.Validation!.Max);
        Assert.NotNull(def.Getter);
        Assert.NotNull(def.Setter);
    }

    /// <summary>
    /// Verifies that SettingDefinition record equality works by value.
    /// </summary>
    [Fact]
    public void SettingDefinition_Equality_ByValue()
    {
        var a = new SettingDefinition("key", "boolean", true, "Title", "Desc");
        var b = new SettingDefinition("key", "boolean", true, "Title", "Desc");

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies that SettingDefinition record inequality on different keys.
    /// </summary>
    [Fact]
    public void SettingDefinition_Inequality_DifferentKeys()
    {
        var a = new SettingDefinition("key1", "boolean", true, "Title", "Desc");
        var b = new SettingDefinition("key2", "boolean", true, "Title", "Desc");

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies that Getter and Setter delegates work correctly.
    /// </summary>
    [Fact]
    public void SettingDefinition_GetterSetter_WorkCorrectly()
    {
        var def = new SettingDefinition(
            Key: "osr2.udpPort",
            Type: "number",
            DefaultValue: 7777,
            Title: "UDP Port",
            Description: "Port number",
            Getter: s => s.Osr2UdpPort,
            Setter: (s, v) => s.Osr2UdpPort = (int)v!);

        var settings = new AppSettings();

        Assert.Equal(7777, def.Getter!(settings));

        def.Setter!(settings, 8888);
        Assert.Equal(8888, settings.Osr2UdpPort);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SettingValidation Tests                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that SettingValidation defaults to null bounds.
    /// </summary>
    [Fact]
    public void SettingValidation_Defaults_NullBounds()
    {
        var v = new SettingValidation();

        Assert.Null(v.Min);
        Assert.Null(v.Max);
    }

    /// <summary>
    /// Verifies that SettingValidation with explicit bounds.
    /// </summary>
    [Fact]
    public void SettingValidation_ExplicitBounds()
    {
        var v = new SettingValidation(Min: 1, Max: 65535);

        Assert.Equal(1, v.Min);
        Assert.Equal(65535, v.Max);
    }

    /// <summary>
    /// Verifies that SettingValidation record equality.
    /// </summary>
    [Fact]
    public void SettingValidation_Equality()
    {
        var a = new SettingValidation(0, 100);
        var b = new SettingValidation(0, 100);

        Assert.Equal(a, b);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ SidebarPanelKind Tests                                         ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that SidebarPanelKind has exactly 5 members.
    /// </summary>
    [Fact]
    public void SidebarPanelKind_HasFiveMembers()
    {
        var values = Enum.GetValues<SidebarPanelKind>();

        Assert.Equal(4, values.Length);
    }

    /// <summary>
    /// Verifies that SidebarPanelKind contains all expected panel types.
    /// </summary>
    [Theory]
    [InlineData(SidebarPanelKind.Explorer)]
    [InlineData(SidebarPanelKind.Playlists)]
    [InlineData(SidebarPanelKind.Osr2Plus)]
    [InlineData(SidebarPanelKind.Settings)]
    public void SidebarPanelKind_ContainsExpectedMembers(SidebarPanelKind kind)
    {
        Assert.True(Enum.IsDefined(kind));
    }

    /// <summary>
    /// Verifies that Extensions enum value no longer exists.
    /// </summary>
    [Fact]
    public void SidebarPanelKind_Extensions_DoesNotExist()
    {
        Assert.False(Enum.TryParse<SidebarPanelKind>("Extensions", out _));
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ AppSettings Feature Property Defaults Tests                    ║
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Verifies that OSR2+ connection settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_Osr2ConnectionDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal("UDP", settings.Osr2ConnectionMode);
        Assert.Equal(7777, settings.Osr2UdpPort);
        Assert.Equal("", settings.Osr2ComPort);
        Assert.Equal(115200, settings.Osr2BaudRate);
    }

    /// <summary>
    /// Verifies that OSR2+ output settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_Osr2OutputDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal(100, settings.Osr2OutputRate);
        Assert.Equal(0, settings.Osr2GlobalOffset);
    }

    /// <summary>
    /// Verifies that OSR2+ visualizer settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_Osr2VisualizerDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal("Graph", settings.Osr2VisualizerMode);
        Assert.Equal(60, settings.Osr2VisualizerWindowDuration);
    }

    /// <summary>
    /// Verifies that OSR2+ runtime settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_Osr2RuntimeDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal("Off", settings.Osr2BeatBarMode);
        Assert.Equal("", settings.Osr2LastRightPanel);
    }

    /// <summary>
    /// Verifies that OSR2+ per-axis settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_Osr2AxisSettings_DefaultsToFourAxes()
    {
        var settings = new AppSettings();

        Assert.Equal(4, settings.Osr2AxisSettings.Count);
        Assert.True(settings.Osr2AxisSettings.ContainsKey("L0"));
        Assert.True(settings.Osr2AxisSettings.ContainsKey("R0"));
        Assert.True(settings.Osr2AxisSettings.ContainsKey("R1"));
        Assert.True(settings.Osr2AxisSettings.ContainsKey("R2"));
    }

    /// <summary>
    /// Verifies that Pulse detection settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_PulseDetectionDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal(1.5, settings.PulseBeatSensitivity);
        Assert.True(settings.PulseEnableBpmPhaseLock);
    }

    /// <summary>
    /// Verifies that Pulse visualizer settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_PulseVisualizerDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal(30, settings.PulseWaveformWindowDuration);
    }

    /// <summary>
    /// Verifies that Pulse runtime settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_PulseRuntimeDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.False(settings.PulseUsePulse);
        Assert.Equal(0, settings.PulseBeatRateIndex);
    }

    /// <summary>
    /// Verifies that Playlist settings defaults are correct.
    /// </summary>
    [Fact]
    public void AppSettings_PlaylistDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.False(settings.PlaylistAutoSave);
        Assert.Empty(settings.PlaylistRecentPlaylists);
        Assert.Equal("", settings.PlaylistLastPlaylistPath);
    }

    /// <summary>
    /// Verifies that ResetToDefaults restores all feature settings to defaults.
    /// </summary>
    [Fact]
    public void AppSettings_ResetToDefaults_RestoresFeatureSettings()
    {
        var settings = new AppSettings
        {
            Osr2ConnectionMode = "Serial",
            Osr2UdpPort = 9999,
            Osr2ComPort = "COM3",
            Osr2BaudRate = 9600,
            Osr2OutputRate = 200,
            Osr2GlobalOffset = 50,
            Osr2VisualizerMode = "Bars",
            Osr2VisualizerWindowDuration = 120,
            Osr2BeatBarMode = "OnPeak",
            Osr2LastRightPanel = "SomePanel",
            PulseBeatSensitivity = 3.0,
            PulseEnableBpmPhaseLock = false,
            PulseWaveformWindowDuration = 10,
            PulseUsePulse = true,
            PulseBeatRateIndex = 5,
            PlaylistAutoSave = true,
            PlaylistRecentPlaylists = ["a.m3u", "b.m3u"],
            PlaylistLastPlaylistPath = "/some/path.m3u"
        };

        settings.ResetToDefaults();

        Assert.Equal("UDP", settings.Osr2ConnectionMode);
        Assert.Equal(7777, settings.Osr2UdpPort);
        Assert.Equal("", settings.Osr2ComPort);
        Assert.Equal(115200, settings.Osr2BaudRate);
        Assert.Equal(100, settings.Osr2OutputRate);
        Assert.Equal(0, settings.Osr2GlobalOffset);
        Assert.Equal("Graph", settings.Osr2VisualizerMode);
        Assert.Equal(60, settings.Osr2VisualizerWindowDuration);
        Assert.Equal("Off", settings.Osr2BeatBarMode);
        Assert.Equal("", settings.Osr2LastRightPanel);
        Assert.Equal(4, settings.Osr2AxisSettings.Count);
        Assert.Equal(1.5, settings.PulseBeatSensitivity);
        Assert.True(settings.PulseEnableBpmPhaseLock);
        Assert.Equal(30, settings.PulseWaveformWindowDuration);
        Assert.False(settings.PulseUsePulse);
        Assert.Equal(0, settings.PulseBeatRateIndex);
        Assert.False(settings.PlaylistAutoSave);
        Assert.Empty(settings.PlaylistRecentPlaylists);
        Assert.Equal("", settings.PlaylistLastPlaylistPath);
    }

    /// <summary>
    /// Verifies that plugin properties no longer exist on AppSettings.
    /// </summary>
    [Fact]
    public void AppSettings_PluginProperties_DoNotExist()
    {
        var type = typeof(AppSettings);

        Assert.Null(type.GetProperty("PluginInstalledSectionExpanded"));
        Assert.Null(type.GetProperty("PluginAvailableSectionExpanded"));
        Assert.Null(type.GetProperty("PluginDirectories"));
        Assert.Null(type.GetProperty("DisabledPluginIds"));
        Assert.Null(type.GetProperty("PluginSidebarOrder"));
        Assert.Null(type.GetProperty("PluginRegistryUrls"));
    }
}
