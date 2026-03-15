using NSubstitute;
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

    // Plugin registry URL tests removed — PluginRegistryUrls and OfficialRegistryUrl
    // properties deleted from AppSettings in PI-003.

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

    // Plugin registry URL setter tests removed — PluginRegistryUrls deleted from AppSettings in PI-003.

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

    // ── Toast Duration ──

    [Fact]
    public void Get_ToastDuration_ReturnsValue()
    {
        _settings.ToastDurationSeconds = 5.0;
        Assert.Equal(5.0, _store.Get("general.toastDuration", 0.0));
    }

    [Fact]
    public void Set_ToastDuration_ClampsAndSaves()
    {
        _store.Set("general.toastDuration", 15.0);
        Assert.Equal(10.0, _settings.ToastDurationSeconds);
        _settingsService.Received().QueueSave();
    }

    // ── Fullscreen Auto-Hide ──

    [Fact]
    public void Get_FullscreenAutoHide_ReturnsValue()
    {
        _settings.FullscreenAutoHideSeconds = 7.0;
        Assert.Equal(7.0, _store.Get("playback.fullscreenAutoHide", 0.0));
    }

    [Fact]
    public void Set_FullscreenAutoHide_ClampsAndSaves()
    {
        _store.Set("playback.fullscreenAutoHide", 50.0);
        Assert.Equal(30.0, _settings.FullscreenAutoHideSeconds);
        _settingsService.Received().QueueSave();
    }

    // ── Fullscreen Show Video Name ──

    [Fact]
    public void Get_FullscreenShowVideoName_ReturnsValue()
    {
        _settings.FullscreenShowVideoName = false;
        Assert.False(_store.Get("playback.fullscreenShowVideoName", true));
    }

    [Fact]
    public void Set_FullscreenShowVideoName_UpdatesAndSaves()
    {
        _store.Set("playback.fullscreenShowVideoName", false);
        Assert.False(_settings.FullscreenShowVideoName);
        _settingsService.Received().QueueSave();
    }

    // ── Resume Playback Prompt ──

    [Fact]
    public void Get_ResumePlaybackPrompt_ReturnsValue()
    {
        _settings.ResumePlaybackPrompt = false;
        Assert.False(_store.Get("playback.resumePlaybackPrompt", true));
    }

    [Fact]
    public void Set_ResumePlaybackPrompt_UpdatesAndSaves()
    {
        _store.Set("playback.resumePlaybackPrompt", false);
        Assert.False(_settings.ResumePlaybackPrompt);
        _settingsService.Received().QueueSave();
    }

    // ── Playlist Auto-Save ──

    [Fact]
    public void Get_PlaylistAutoSave_ReturnsValue()
    {
        _settings.PlaylistAutoSave = true;
        Assert.True(_store.Get("playlist.autoSave", false));
    }

    [Fact]
    public void Set_PlaylistAutoSave_UpdatesAndSaves()
    {
        _store.Set("playlist.autoSave", true);
        Assert.True(_settings.PlaylistAutoSave);
        _settingsService.Received().QueueSave();
    }
}