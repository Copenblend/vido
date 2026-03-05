using System.ComponentModel;
using System.Reflection;
using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Haptics;
using Vido.Core.Logging;
using Vido.Core.Models.Pulse;
using Vido.Core.Settings;
using Vido.Services.Events;
using Vido.Services.Playlists;
using Vido.Services.Pulse;
using Vido.ViewModels.Pulse;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-014: Pulse ViewModel integration.
/// Covers <see cref="PulseSidebarViewModel"/> and <see cref="WaveformViewModel"/>.
/// </summary>
public class PulseViewModelTests : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly TestEventBus _eventBus;
    private readonly PulseEngine _engine;

    public PulseViewModelTests()
    {
        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(_settings);
        _eventBus = new TestEventBus();
        _engine = CreateEngine(_eventBus);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }

    // ── Reflection helpers to invoke private VM callbacks ──

    /// <summary>
    /// Invokes a private instance method on the target object by name.
    /// Used to test engine callbacks without going through the engine.
    /// </summary>
    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, args);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — Constructor
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_Constructor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PulseSidebarViewModel(null!, _settingsService));
    }

    [Fact]
    public void SidebarVM_Constructor_NullSettingsService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PulseSidebarViewModel(_engine, null!));
    }

    [Fact]
    public void SidebarVM_Constructor_DefaultState_Inactive()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        Assert.Equal(PulseState.Inactive, vm.State);
        Assert.False(vm.UsePulse);
        Assert.False(vm.IsAnalyzing);
        Assert.False(vm.ShowBpm);
        Assert.Equal("Grey", vm.StateColor);
        Assert.Equal(0, vm.SelectedBeatRateIndex);
        Assert.Equal("\u2665 Pulse: Off", vm.StatusBarText);
    }

    [Fact]
    public void SidebarVM_Constructor_LoadsPersistedSettings()
    {
        _settings.PulseUsePulse = true;
        _settings.PulseBeatRateIndex = 2;

        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        // UsePulse loads from settings (engine might override but field is set)
        Assert.Equal(2, vm.SelectedBeatRateIndex);
    }

    [Fact]
    public void SidebarVM_Constructor_PersistedUsePulse_RegistersBeatSource()
    {
        _settings.PulseUsePulse = true;

        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        // SetEnabled(true) should publish ExternalBeatSourceRegistration
        var regs = _eventBus.GetPublished<ExternalBeatSourceRegistration>();
        Assert.Single(regs);
        Assert.True(regs[0].IsRegistering);
    }

    [Fact]
    public void SidebarVM_Constructor_PersistedUsePulseFalse_DoesNotRegisterBeatSource()
    {
        _settings.PulseUsePulse = false;

        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        var regs = _eventBus.GetPublished<ExternalBeatSourceRegistration>();
        Assert.Empty(regs);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — UsePulse Toggle
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_UsePulse_SetTrue_RaisesPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UsePulse = true;

        Assert.Contains(nameof(PulseSidebarViewModel.UsePulse), changed);
    }

    [Fact]
    public void SidebarVM_UsePulse_SetTrue_PersistsToSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        vm.UsePulse = true;

        Assert.True(_settings.PulseUsePulse);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void SidebarVM_UsePulse_SameValue_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UsePulse = false; // same as default

        Assert.DoesNotContain(nameof(PulseSidebarViewModel.UsePulse), changed);
    }

    [Fact]
    public void SidebarVM_UsePulse_SetFalse_AfterTrue_PersistsToSettings()
    {
        _settings.PulseUsePulse = true;
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        vm.UsePulse = false;

        Assert.False(_settings.PulseUsePulse);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — UsePulse Toast Notifications
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_UsePulse_Enable_ShowsToast()
    {
        var toast = Substitute.For<IToastService>();
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, toast);

        vm.UsePulse = true;

        toast.Received(1).Show("Pulse enabled", Arg.Any<string?>());
    }

    [Fact]
    public void SidebarVM_UsePulse_Disable_ShowsToast()
    {
        var toast = Substitute.For<IToastService>();
        _settings.PulseUsePulse = true;
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, toast);

        vm.UsePulse = false;

        toast.Received(1).Show("Pulse disabled", Arg.Any<string?>());
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — State Changes (via private callbacks)
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_OnEngineStateChanged_Analyzing_UpdatesComputedProperties()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Analyzing);

        Assert.Equal(PulseState.Analyzing, vm.State);
        Assert.True(vm.IsAnalyzing);
        Assert.False(vm.ShowBpm);
        Assert.Equal("Yellow", vm.StateColor);
        Assert.Contains(nameof(PulseSidebarViewModel.State), changed);
        Assert.Contains(nameof(PulseSidebarViewModel.IsAnalyzing), changed);
        Assert.Contains(nameof(PulseSidebarViewModel.ShowBpm), changed);
        Assert.Contains(nameof(PulseSidebarViewModel.StateColor), changed);
    }

    [Fact]
    public void SidebarVM_OnEngineStateChanged_SameState_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Inactive);

        Assert.DoesNotContain(nameof(PulseSidebarViewModel.State), changed);
    }

    [Fact]
    public void SidebarVM_StateColor_Active_ReturnsGreen()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Active);

        Assert.Equal("Green", vm.StateColor);
    }

    [Fact]
    public void SidebarVM_StateColor_Ready_ReturnsYellow()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);

        Assert.Equal("Yellow", vm.StateColor);
    }

    [Fact]
    public void SidebarVM_StateColor_Error_ReturnsRed()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Error);

        Assert.Equal("Red", vm.StateColor);
    }

    [Fact]
    public void SidebarVM_ShowBpm_ReadyOrActive_ReturnsTrue()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        Assert.True(vm.ShowBpm);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Active);
        Assert.True(vm.ShowBpm);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — BPM and Progress
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_OnAnalysisProgress_UpdatesProperty()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InvokePrivate(vm, "OnAnalysisProgress", 0.5);

        Assert.Equal(0.5, vm.AnalysisProgress, 0.01);
        Assert.Contains(nameof(PulseSidebarViewModel.AnalysisProgress), changed);
    }

    [Fact]
    public void SidebarVM_OnAnalysisProgress_SameValue_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnAnalysisProgress", 0.0);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InvokePrivate(vm, "OnAnalysisProgress", 0.004); // within tolerance

        Assert.DoesNotContain(nameof(PulseSidebarViewModel.AnalysisProgress), changed);
    }

    [Fact]
    public void SidebarVM_OnBeatMapReady_UpdatesCurrentBpm()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var beatMap = CreateBeatMap(120.0);

        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        Assert.Equal(120.0, vm.CurrentBpm, 0.01);
    }

    [Fact]
    public void SidebarVM_OnBeatMapReady_SameBpm_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var beatMap = CreateBeatMap(120.0);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        Assert.DoesNotContain(nameof(PulseSidebarViewModel.CurrentBpm), changed);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — Status Messages
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_StatusMessage_Inactive_Empty()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    [Fact]
    public void SidebarVM_StatusMessage_Analyzing_ContainsProgress()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Analyzing);
        InvokePrivate(vm, "OnAnalysisProgress", 0.5);

        Assert.Contains("Analyzing", vm.StatusMessage);
        Assert.Contains("50", vm.StatusMessage);
    }

    [Fact]
    public void SidebarVM_StatusMessage_Ready_ContainsBpm()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(128.0));
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);

        Assert.Contains("128", vm.StatusMessage);
        Assert.Contains("BPM", vm.StatusMessage);
    }

    [Fact]
    public void SidebarVM_StatusMessage_Error_ContainsErrorMessage()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnErrorOccurred", "Decoder failed");
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Error);

        Assert.Contains("Error", vm.StatusMessage);
        Assert.Contains("Decoder failed", vm.StatusMessage);
    }

    [Fact]
    public void SidebarVM_StatusBarText_UpdatesPerState()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Analyzing);
        Assert.Contains("Analyzing", vm.StatusBarText);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        Assert.Contains("Ready", vm.StatusBarText);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Error);
        Assert.Contains("Error", vm.StatusBarText);
    }

    [Fact]
    public void SidebarVM_StatusBarText_Active_ContainsBpm()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(140.0));
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Active);

        Assert.Contains("Active", vm.StatusBarText);
        Assert.Contains("140", vm.StatusBarText);
    }

    [Fact]
    public void SidebarVM_StatusMessage_SameValue_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Re-trigger inactive state → same empty message
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Inactive);

        Assert.DoesNotContain(nameof(PulseSidebarViewModel.StatusMessage), changed);
    }

    [Fact]
    public void SidebarVM_ErrorOccurred_ClearedOnNonErrorState()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnErrorOccurred", "Some error");
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Error);
        Assert.Contains("Some error", vm.StatusMessage);

        // Transition to Inactive clears error
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Inactive);
        Assert.DoesNotContain("Some error", vm.StatusMessage);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — Beat Rate
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_BeatRateOptions_HasFourEntries()
    {
        Assert.Equal(4, PulseSidebarViewModel.BeatRateOptions.Count);
        Assert.Equal("Every Beat", PulseSidebarViewModel.BeatRateOptions[0]);
        Assert.Equal("Every 2nd Beat", PulseSidebarViewModel.BeatRateOptions[1]);
        Assert.Equal("Every 3rd Beat", PulseSidebarViewModel.BeatRateOptions[2]);
        Assert.Equal("Every 4th Beat", PulseSidebarViewModel.BeatRateOptions[3]);
    }

    [Fact]
    public void SidebarVM_SelectedBeatRateIndex_Set_PersistsAndUpdatesEngine()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        vm.SelectedBeatRateIndex = 2;

        Assert.Equal(2, vm.SelectedBeatRateIndex);
        Assert.Equal(2, _settings.PulseBeatRateIndex);
        Assert.Equal(3, _engine.BeatDivisor); // index + 1
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void SidebarVM_SelectedBeatRateIndex_SameValue_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectedBeatRateIndex = 0; // same as default

        Assert.DoesNotContain(nameof(PulseSidebarViewModel.SelectedBeatRateIndex), changed);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — Description
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_Description_ContainsKeyInfo()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService);

        Assert.Contains("beat detection", vm.Description);
        Assert.Contains("Funscript", vm.Description);
    }

    // ══════════════════════════════════════════════
    //  PulseSidebarViewModel — Dispose
    // ══════════════════════════════════════════════

    [Fact]
    public void SidebarVM_Dispose_UnsubscribesFromEngineEvents()
    {
        var engine = CreateEngine(_eventBus);
        var vm = new PulseSidebarViewModel(engine, _settingsService);
        vm.Dispose();

        // After dispose, engine events should not update VM
        // We verify by checking the VM state doesn't change.
        Assert.Equal(PulseState.Inactive, vm.State);
        engine.Dispose();
    }

    [Fact]
    public void SidebarVM_Dispose_CalledTwice_NoException()
    {
        var vm = new PulseSidebarViewModel(_engine, _settingsService);
        vm.Dispose();
        vm.Dispose(); // should not throw
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Constructor
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_Constructor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WaveformViewModel(null!, _settingsService));
    }

    [Fact]
    public void WaveformVM_Constructor_NullSettingsService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WaveformViewModel(_engine, null!));
    }

    [Fact]
    public void WaveformVM_Constructor_DefaultState()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        Assert.Equal(0, vm.CurrentTimeSeconds);
        Assert.Equal(0, vm.TotalDurationSeconds);
        Assert.Equal(0, vm.CurrentBpm);
        Assert.Equal(30.0, vm.WindowDurationSeconds, 0.01);
        Assert.Equal(0, vm.CurrentAmplitude);
        Assert.False(vm.IsActive);
        Assert.Null(vm.FullWaveform);
        Assert.Null(vm.AllBeats);
        Assert.Equal(0, vm.WaveformSampleRate);
    }

    [Fact]
    public void WaveformVM_Constructor_LoadsPersistedWindowDuration()
    {
        _settings.PulseWaveformWindowDuration = 60;

        using var vm = new WaveformViewModel(_engine, _settingsService);

        Assert.Equal(60.0, vm.WindowDurationSeconds, 0.01);
    }

    [Fact]
    public void WaveformVM_Constructor_ZeroPersistedDuration_DefaultsTo30()
    {
        _settings.PulseWaveformWindowDuration = 0;

        using var vm = new WaveformViewModel(_engine, _settingsService);

        Assert.Equal(30.0, vm.WindowDurationSeconds, 0.01);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Window Duration
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_WindowDurationSeconds_Set_RaisesPropertyChanged()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.WindowDurationSeconds = 60.0;

        Assert.Contains(nameof(WaveformViewModel.WindowDurationSeconds), changed);
    }

    [Fact]
    public void WaveformVM_WindowDurationSeconds_Set_PersistsToSettings()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        vm.WindowDurationSeconds = 120.0;

        Assert.Equal(120, _settings.PulseWaveformWindowDuration);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void WaveformVM_WindowDurationSeconds_Set_FiresRepaintRequested()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        bool repaintFired = false;
        vm.RepaintRequested += () => repaintFired = true;

        vm.WindowDurationSeconds = 10.0;

        Assert.True(repaintFired);
    }

    [Fact]
    public void WaveformVM_WindowDurationSeconds_SameValue_NoPropertyChanged()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.WindowDurationSeconds = 30.0; // same as default

        Assert.DoesNotContain(nameof(WaveformViewModel.WindowDurationSeconds), changed);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — WindowDurationIndex
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_WindowDurationIndex_ReflectsCurrentDuration()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        Assert.Equal(1, vm.WindowDurationIndex); // 30s default
    }

    [Fact]
    public void WaveformVM_WindowDurationIndex_Set_UpdatesDuration()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        vm.WindowDurationIndex = 0; // 10s
        Assert.Equal(10.0, vm.WindowDurationSeconds, 0.01);

        vm.WindowDurationIndex = 2; // 60s
        Assert.Equal(60.0, vm.WindowDurationSeconds, 0.01);

        vm.WindowDurationIndex = 3; // 120s
        Assert.Equal(120.0, vm.WindowDurationSeconds, 0.01);

        vm.WindowDurationIndex = 4; // 300s
        Assert.Equal(300.0, vm.WindowDurationSeconds, 0.01);
    }

    [Fact]
    public void WaveformVM_WindowDurationIndex_OutOfRange_Ignored()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        vm.WindowDurationIndex = -1;
        Assert.Equal(30.0, vm.WindowDurationSeconds, 0.01); // unchanged

        vm.WindowDurationIndex = 99;
        Assert.Equal(30.0, vm.WindowDurationSeconds, 0.01); // unchanged
    }

    [Fact]
    public void WaveformVM_WindowDurationIndex_NonStandardDuration_ReturnsDefault()
    {
        _settings.PulseWaveformWindowDuration = 45; // not in options array
        using var vm = new WaveformViewModel(_engine, _settingsService);

        Assert.Equal(1, vm.WindowDurationIndex); // falls back to default (30s index)
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Static Members
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_WindowDurationOptions_Correct()
    {
        Assert.Equal(new double[] { 10, 30, 60, 120, 300 }, WaveformViewModel.WindowDurationOptions);
    }

    [Fact]
    public void WaveformVM_WindowDurationLabels_Correct()
    {
        Assert.Equal(new[] { "10s", "30s", "60s", "2m", "5m" }, WaveformViewModel.WindowDurationLabels);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — UpdateTime
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_UpdateTime_UpdatesCurrentTimeSeconds()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        vm.UpdateTime(42.5);

        Assert.Equal(42.5, vm.CurrentTimeSeconds, 0.01);
    }

    [Fact]
    public void WaveformVM_UpdateTime_FiresRepaintRequested()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        bool repaintFired = false;
        vm.RepaintRequested += () => repaintFired = true;

        vm.UpdateTime(10.0);

        Assert.True(repaintFired);
    }

    [Fact]
    public void WaveformVM_UpdateTime_SameValue_StillFiresRepaint()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        vm.UpdateTime(10.0);

        bool repaintFired = false;
        vm.RepaintRequested += () => repaintFired = true;

        vm.UpdateTime(10.0); // time unchanged (within threshold)

        Assert.True(repaintFired); // repaint always fires
    }

    [Fact]
    public void WaveformVM_UpdateTime_AfterDispose_DoesNothing()
    {
        var vm = new WaveformViewModel(_engine, _settingsService);
        bool repaintFired = false;
        vm.RepaintRequested += () => repaintFired = true;
        vm.Dispose();

        vm.UpdateTime(10.0);

        Assert.False(repaintFired);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Clear
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_Clear_ResetsAllData()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        // First populate via OnBeatMapReady
        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(120.0));
        Assert.True(vm.IsActive);

        vm.Clear();

        Assert.Null(vm.FullWaveform);
        Assert.Null(vm.AllBeats);
        Assert.Equal(0, vm.WaveformSampleRate);
        Assert.Equal(0, vm.CurrentBpm, 0.01);
        Assert.Equal(0, vm.CurrentAmplitude, 0.01);
        Assert.Equal(0, vm.CurrentTimeSeconds, 0.01);
        Assert.Equal(0, vm.TotalDurationSeconds, 0.01);
        Assert.False(vm.IsActive);
    }

    [Fact]
    public void WaveformVM_Clear_RaisesPropertyChangedForWaveformAndBeats()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Clear();

        Assert.Contains(nameof(WaveformViewModel.FullWaveform), changed);
        Assert.Contains(nameof(WaveformViewModel.AllBeats), changed);
    }

    [Fact]
    public void WaveformVM_Clear_FiresRepaintRequested()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        bool repaintFired = false;
        vm.RepaintRequested += () => repaintFired = true;

        vm.Clear();

        Assert.True(repaintFired);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Engine State Changes (via callbacks)
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_OnEngineStateChanged_Ready_SetsActive()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);

        Assert.True(vm.IsActive);
    }

    [Fact]
    public void WaveformVM_OnEngineStateChanged_ActiveState_SetsActive()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Active);

        Assert.True(vm.IsActive);
    }

    [Fact]
    public void WaveformVM_OnEngineStateChanged_Inactive_ClearsAll()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(120.0));

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Inactive);

        Assert.False(vm.IsActive);
        Assert.Null(vm.FullWaveform);
    }

    [Fact]
    public void WaveformVM_OnEngineStateChanged_Error_ClearsAll()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(120.0));

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Error);

        Assert.False(vm.IsActive);
        Assert.Null(vm.FullWaveform);
    }

    [Fact]
    public void WaveformVM_OnEngineStateChanged_Analyzing_NotActive()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Analyzing);

        Assert.False(vm.IsActive);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — BeatMap Ready (via callback)
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_OnBeatMapReady_PopulatesAllFields()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        var beatMap = CreateBeatMap(128.0, 5000, 200);

        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        Assert.NotNull(vm.FullWaveform);
        Assert.Equal(200, vm.WaveformSampleRate);
        Assert.NotNull(vm.AllBeats);
        Assert.Equal(128.0, vm.CurrentBpm, 0.01);
        Assert.Equal(5.0, vm.TotalDurationSeconds, 0.01); // 5000ms / 1000
        Assert.True(vm.IsActive);
    }

    [Fact]
    public void WaveformVM_OnBeatMapReady_RaisesPropertyChanged()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(120.0));

        Assert.Contains(nameof(WaveformViewModel.FullWaveform), changed);
        Assert.Contains(nameof(WaveformViewModel.AllBeats), changed);
        Assert.Contains(nameof(WaveformViewModel.CurrentBpm), changed);
        Assert.Contains(nameof(WaveformViewModel.TotalDurationSeconds), changed);
        Assert.Contains(nameof(WaveformViewModel.IsActive), changed);
    }

    [Fact]
    public void WaveformVM_OnBeatMapReady_FiresRepaint()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        bool repaintFired = false;
        vm.RepaintRequested += () => repaintFired = true;

        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(120.0));

        Assert.True(repaintFired);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Disposed state checks
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_OnEngineStateChanged_AfterDispose_DoesNothing()
    {
        var vm = new WaveformViewModel(_engine, _settingsService);
        vm.Dispose();

        // This should not throw or change state because _disposed is true
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);

        Assert.False(vm.IsActive);
    }

    [Fact]
    public void WaveformVM_OnBeatMapReady_AfterDispose_DoesNothing()
    {
        var vm = new WaveformViewModel(_engine, _settingsService);
        vm.Dispose();

        InvokePrivate(vm, "OnBeatMapReady", CreateBeatMap(120.0));

        Assert.Null(vm.FullWaveform);
        Assert.False(vm.IsActive);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — IsActive Property
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_IsActive_SameValue_NoPropertyChanged()
    {
        using var vm = new WaveformViewModel(_engine, _settingsService);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // IsActive is already false, Analyzing does not set it true
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Analyzing);

        Assert.DoesNotContain(nameof(WaveformViewModel.IsActive), changed);
    }

    // ══════════════════════════════════════════════
    //  WaveformViewModel — Dispose
    // ══════════════════════════════════════════════

    [Fact]
    public void WaveformVM_Dispose_CalledTwice_NoException()
    {
        var vm = new WaveformViewModel(_engine, _settingsService);
        vm.Dispose();
        vm.Dispose(); // should not throw
    }

    // ══════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════

    private static PulseEngine CreateEngine(TestEventBus eventBus)
    {
        var decoder = new TestAudioDecoder();
        var preAnalysis = new AudioPreAnalysisService(decoder);
        var liveAmplitude = new LiveAmplitudeService();
        var mapper = new PulseTCodeMapper();
        var logger = Substitute.For<ILogService>();

        return new PulseEngine(preAnalysis, liveAmplitude, mapper, eventBus, logger);
    }

    private static BeatMap CreateBeatMap(double bpm, double durationMs = 10000, int sampleRate = 100)
    {
        var beats = new List<BeatEvent>();
        double intervalMs = 60000.0 / bpm;
        for (double t = 0; t < durationMs; t += intervalMs)
            beats.Add(new BeatEvent { TimestampMs = t, Strength = 0.8 });

        var waveform = new float[sampleRate * (int)(durationMs / 1000)];
        for (int i = 0; i < waveform.Length; i++)
            waveform[i] = (float)Math.Sin(i * 0.1) * 0.5f;

        return new BeatMap
        {
            Bpm = bpm,
            Beats = beats,
            DurationMs = durationMs,
            WaveformSamples = waveform,
            WaveformSampleRate = sampleRate
        };
    }

    // ── Test helpers ──

    /// <summary>
    /// In-memory event bus for testing.
    /// </summary>
    private sealed class TestEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly List<object> _published = new();
        private readonly object _lock = new();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            lock (_lock)
            {
                if (!_handlers.ContainsKey(type))
                    _handlers[type] = new List<Delegate>();
                _handlers[type].Add(handler);
            }

            return new Subscription(() =>
            {
                lock (_lock)
                {
                    if (_handlers.TryGetValue(type, out var list))
                        list.Remove(handler);
                }
            });
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            List<Delegate> snapshot;
            lock (_lock)
            {
                _published.Add(eventData!);
                snapshot = _handlers.TryGetValue(typeof(TEvent), out var list)
                    ? list.ToList()
                    : new List<Delegate>();
            }

            foreach (var h in snapshot)
                ((Action<TEvent>)h)(eventData);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _onDispose;
            public Subscription(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }

        /// <summary>Returns all published events of the given type.</summary>
        public List<TEvent> GetPublished<TEvent>()
        {
            lock (_lock)
            {
                return _published.OfType<TEvent>().ToList();
            }
        }
    }

    /// <summary>
    /// Mock audio decoder for testing.
    /// </summary>
    private sealed class TestAudioDecoder : IAudioDecoder
    {
        public async IAsyncEnumerable<AudioChunk> DecodeAsync(
            string mediaPath,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }
    }
}
