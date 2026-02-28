using NSubstitute;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="AppSettingsStore"/> — the adapter that maps
/// typed <see cref="AppSettings"/> properties to the <see cref="IPluginSettingsStore"/> interface.
/// </summary>
public sealed class AppSettingsStoreTests
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly AppSettingsStore _store;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public AppSettingsStoreTests()
    {
        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(_settings);
        _store = new AppSettingsStore(_settingsService);
    }

    // ── Get ──

    /// <summary>
    /// Verifies that Get volume returns scaled value.
    /// </summary>
    [Fact]
    public void Get_Volume_ReturnsScaledValue()
    {
        _settings.Volume = 0.75;
        var result = _store.Get("playback.volume", 0.0);
        Assert.Equal(75.0, result);
    }

    /// <summary>
    /// Verifies that Get volume rounds to whole number.
    /// </summary>
    [Fact]
    public void Get_Volume_RoundsToWholeNumber()
    {
        _settings.Volume = 0.333;
        var result = _store.Get("playback.volume", 0.0);
        Assert.Equal(33.0, result);
    }

    /// <summary>
    /// Verifies that Get speed returns formatted string.
    /// </summary>
    [Fact]
    public void Get_Speed_ReturnsFormattedString()
    {
        _settings.PlaybackSpeed = 1.5;
        var result = _store.Get("playback.speed", "");
        Assert.Equal("1.5x", result);
    }

    /// <summary>
    /// Verifies that Get loop returns bool value.
    /// </summary>
    [Fact]
    public void Get_Loop_ReturnsBoolValue()
    {
        _settings.LoopPlayback = true;
        Assert.True(_store.Get("playback.loop", false));

        _settings.LoopPlayback = false;
        Assert.False(_store.Get("playback.loop", true));
    }

    /// <summary>
    /// Verifies that Get show hidden files returns bool value.
    /// </summary>
    [Fact]
    public void Get_ShowHiddenFiles_ReturnsBoolValue()
    {
        _settings.ShowHiddenFiles = true;
        Assert.True(_store.Get("explorer.showHiddenFiles", false));
    }

    /// <summary>
    /// Verifies that Get registry urls returns empty list when no custom urls.
    /// </summary>
    [Fact]
    public void Get_RegistryUrls_ReturnsEmptyListWhenNoCustomUrls()
    {
        _settings.PluginRegistryUrls = [AppSettings.OfficialRegistryUrl];
        var result = _store.Get("plugins.registryUrls", new List<string>());
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that Get registry urls returns custom urls only.
    /// </summary>
    [Fact]
    public void Get_RegistryUrls_ReturnsCustomUrlsOnly()
    {
        _settings.PluginRegistryUrls = [AppSettings.OfficialRegistryUrl, "https://custom1.com", "https://custom2.com"];
        var result = _store.Get("plugins.registryUrls", new List<string>());
        Assert.Equal(2, result.Count);
        Assert.Equal("https://custom1.com", result[0]);
        Assert.Equal("https://custom2.com", result[1]);
    }

    /// <summary>
    /// Verifies that Get registry urls ensures official url when missing.
    /// </summary>
    [Fact]
    public void Get_RegistryUrls_EnsuresOfficialUrlWhenMissing()
    {
        _settings.PluginRegistryUrls = ["https://custom-only.com"];
        var result = _store.Get("plugins.registryUrls", new List<string>());
        // After ensuring official URL is present, "custom-only.com" becomes a custom URL
        Assert.Single(result);
        Assert.Equal("https://custom-only.com", result[0]);
        // The official URL should now be at index 0 in the underlying list
        Assert.Equal(AppSettings.OfficialRegistryUrl, _settings.PluginRegistryUrls[0]);
    }

    /// <summary>
    /// Verifies that Get unknown key returns default.
    /// </summary>
    [Fact]
    public void Get_UnknownKey_ReturnsDefault()
    {
        var result = _store.Get("nonexistent.key", "fallback");
        Assert.Equal("fallback", result);
    }

    /// <summary>
    /// Verifies that Get is case insensitive.
    /// </summary>
    [Fact]
    public void Get_IsCaseInsensitive()
    {
        _settings.Volume = 0.5;
        Assert.Equal(50.0, _store.Get("Playback.Volume", 0.0));
        Assert.Equal(50.0, _store.Get("PLAYBACK.VOLUME", 0.0));
    }

    // ── Set ──

    /// <summary>
    /// Verifies that Set volume updates settings and queues save.
    /// </summary>
    [Fact]
    public void Set_Volume_UpdatesSettingsAndQueuesSave()
    {
        _store.Set("playback.volume", 80.0);
        Assert.Equal(0.8, _settings.Volume, precision: 2);
        _settingsService.Received(1).QueueSave();
    }

    /// <summary>
    /// Verifies that Set volume clamps to range.
    /// </summary>
    [Fact]
    public void Set_Volume_ClampsToRange()
    {
        _store.Set("playback.volume", 150.0);
        Assert.Equal(1.0, _settings.Volume, precision: 2);

        _store.Set("playback.volume", -10.0);
        Assert.Equal(0.0, _settings.Volume, precision: 2);
    }

    /// <summary>
    /// Verifies that Set speed parses formatted string.
    /// </summary>
    [Fact]
    public void Set_Speed_ParsesFormattedString()
    {
        _store.Set("playback.speed", "2.0x");
        Assert.Equal(2.0, _settings.PlaybackSpeed);
        _settingsService.Received(1).QueueSave();
    }

    /// <summary>
    /// Verifies that Set speed unknown value defaults to1.
    /// </summary>
    [Fact]
    public void Set_Speed_UnknownValueDefaultsTo1()
    {
        _store.Set("playback.speed", "invalid");
        Assert.Equal(1.0, _settings.PlaybackSpeed);
    }

    /// <summary>
    /// Verifies that Set loop updates boolean.
    /// </summary>
    [Fact]
    public void Set_Loop_UpdatesBoolean()
    {
        _store.Set("playback.loop", true);
        Assert.True(_settings.LoopPlayback);
        _settingsService.Received(1).QueueSave();
    }

    /// <summary>
    /// Verifies that Set show hidden files updates boolean.
    /// </summary>
    [Fact]
    public void Set_ShowHiddenFiles_UpdatesBoolean()
    {
        _store.Set("explorer.showHiddenFiles", true);
        Assert.True(_settings.ShowHiddenFiles);
    }

    /// <summary>
    /// Verifies that Set registry urls adds custom urls.
    /// </summary>
    [Fact]
    public void Set_RegistryUrls_AddsCustomUrls()
    {
        _settings.PluginRegistryUrls = [AppSettings.OfficialRegistryUrl];
        _store.Set("plugins.registryUrls", new List<string> { "https://custom1.com", "https://custom2.com" });
        Assert.Equal(3, _settings.PluginRegistryUrls.Count);
        Assert.Equal(AppSettings.OfficialRegistryUrl, _settings.PluginRegistryUrls[0]);
        Assert.Equal("https://custom1.com", _settings.PluginRegistryUrls[1]);
        Assert.Equal("https://custom2.com", _settings.PluginRegistryUrls[2]);
    }

    /// <summary>
    /// Verifies that Set registry urls replaces existing custom urls.
    /// </summary>
    [Fact]
    public void Set_RegistryUrls_ReplacesExistingCustomUrls()
    {
        _settings.PluginRegistryUrls = [AppSettings.OfficialRegistryUrl, "old-url"];
        _store.Set("plugins.registryUrls", new List<string> { "new-url" });
        Assert.Equal(2, _settings.PluginRegistryUrls.Count);
        Assert.Equal(AppSettings.OfficialRegistryUrl, _settings.PluginRegistryUrls[0]);
        Assert.Equal("new-url", _settings.PluginRegistryUrls[1]);
    }

    /// <summary>
    /// Verifies that Set registry urls empty list removes all custom urls.
    /// </summary>
    [Fact]
    public void Set_RegistryUrls_EmptyListRemovesAllCustomUrls()
    {
        _settings.PluginRegistryUrls = [AppSettings.OfficialRegistryUrl, "old-url"];
        _store.Set("plugins.registryUrls", new List<string>());
        Assert.Single(_settings.PluginRegistryUrls);
        Assert.Equal(AppSettings.OfficialRegistryUrl, _settings.PluginRegistryUrls[0]);
    }

    /// <summary>
    /// Verifies that Set unknown key does nothing.
    /// </summary>
    [Fact]
    public void Set_UnknownKey_DoesNothing()
    {
        _store.Set("nonexistent.key", "value");
        _settingsService.DidNotReceive().QueueSave();
    }

    // ── SettingChanged event ──

    /// <summary>
    /// Verifies that Set fires setting changed event.
    /// </summary>
    [Fact]
    public void Set_FiresSettingChangedEvent()
    {
        string? changedKey = null;
        _store.SettingChanged += key => changedKey = key;

        _store.Set("playback.loop", true);

        Assert.Equal("playback.loop", changedKey);
    }

    /// <summary>
    /// Verifies that Set unknown key does not fire event.
    /// </summary>
    [Fact]
    public void Set_UnknownKey_DoesNotFireEvent()
    {
        bool eventFired = false;
        _store.SettingChanged += _ => eventFired = true;

        _store.Set("unknown", "value");

        Assert.False(eventFired);
    }

    // ── Reset ──

    /// <summary>
    /// Verifies that Reset returns false.
    /// </summary>
    [Fact]
    public void Reset_ReturnsFalse()
    {
        Assert.False(_store.Reset("playback.volume"));
    }

    // ── Constructor validation ──

    /// <summary>
    /// Verifies that Constructor throws on null settings service.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullSettingsService()
    {
        Assert.Throws<ArgumentNullException>(() => new AppSettingsStore(null!));
    }
}