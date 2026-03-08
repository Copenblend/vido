using System.ComponentModel;
using System.Reflection;
using NSubstitute;
using Vido.Core.Events;
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
/// Tests for VI-0020: Generate Funscript from Pulse Beat Data.
/// Covers <see cref="PulseSidebarViewModel.CanGenerateFunscript"/>,
/// <see cref="PulseSidebarViewModel.GenerateFunscriptCommand"/>,
/// and video-path tracking via event bus subscriptions.
/// </summary>
public class PulseSidebarViewModelTests : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly TestEventBus _eventBus;
    private readonly PulseEngine _engine;
    private readonly IToastService _toastService;

    public PulseSidebarViewModelTests()
    {
        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(_settings);
        _eventBus = new TestEventBus();
        _engine = CreateEngine(_eventBus);
        _toastService = Substitute.For<IToastService>();
    }

    public void Dispose()
    {
        _engine.Dispose();
    }

    // ══════════════════════════════════════════════
    //  CanGenerateFunscript
    // ══════════════════════════════════════════════

    [Fact]
    public void CanGenerateFunscript_NoBeatMap_ReturnsFalse()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        // State is Inactive, no beatmap, no video path
        Assert.False(vm.CanGenerateFunscript);
    }

    [Fact]
    public void CanGenerateFunscript_NoVideoPath_ReturnsFalse()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        var beatMap = CreateBeatMap(120);
        SetPrivateField(_engine, "_currentBeatMap", beatMap);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        // No VideoLoadedEvent has been published, so _currentVideoPath is null
        Assert.False(vm.CanGenerateFunscript);
    }

    [Fact]
    public void CanGenerateFunscript_ReadyWithBeatMapAndVideo_ReturnsTrue()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        _eventBus.Publish(new VideoLoadedEvent { FilePath = @"C:\videos\test.mp4" });

        // Set beatmap AFTER VideoLoadedEvent (engine clears it on video load)
        var beatMap = CreateBeatMap(120);
        SetPrivateField(_engine, "_currentBeatMap", beatMap);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        Assert.True(vm.CanGenerateFunscript);
    }

    [Fact]
    public void CanGenerateFunscript_ActiveState_ReturnsTrue()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Active);
        _eventBus.Publish(new VideoLoadedEvent { FilePath = @"C:\videos\test.mp4" });
        var beatMap = CreateBeatMap(120);
        SetPrivateField(_engine, "_currentBeatMap", beatMap);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        Assert.True(vm.CanGenerateFunscript);
    }

    [Fact]
    public void CanGenerateFunscript_VideoUnloaded_ReturnsFalse()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        _eventBus.Publish(new VideoLoadedEvent { FilePath = @"C:\videos\test.mp4" });
        var beatMap = CreateBeatMap(120);
        SetPrivateField(_engine, "_currentBeatMap", beatMap);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);
        Assert.True(vm.CanGenerateFunscript);

        _eventBus.Publish(new VideoUnloadedEvent());
        Assert.False(vm.CanGenerateFunscript);
    }

    [Fact]
    public void CanGenerateFunscript_RaisesPropertyChanged_OnStateChange()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);

        Assert.Contains(nameof(vm.CanGenerateFunscript), changed);
    }

    [Fact]
    public void CanGenerateFunscript_RaisesPropertyChanged_OnBeatMapReady()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var beatMap = CreateBeatMap(120);
        SetPrivateField(_engine, "_currentBeatMap", beatMap);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);

        Assert.Contains(nameof(vm.CanGenerateFunscript), changed);
    }

    [Fact]
    public void CanGenerateFunscript_RaisesPropertyChanged_OnVideoLoaded()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        _eventBus.Publish(new VideoLoadedEvent { FilePath = @"C:\videos\test.mp4" });

        Assert.Contains(nameof(vm.CanGenerateFunscript), changed);
    }

    // ══════════════════════════════════════════════
    //  GenerateFunscriptCommand
    // ══════════════════════════════════════════════

    [Fact]
    public void GenerateFunscriptCommand_IsNotNull()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        Assert.NotNull(vm.GenerateFunscriptCommand);
    }

    [Fact]
    public async Task GenerateFunscript_PublishesFunscriptGeneratedEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_fsgen_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(videoPath, "dummy");

            using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
            InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
            _eventBus.Publish(new VideoLoadedEvent { FilePath = videoPath });
            var beatMap = CreateBeatMap(120);
            SetPrivateField(_engine, "_currentBeatMap", beatMap);
            InvokePrivate(vm, "OnBeatMapReady", beatMap);

            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var events = _eventBus.GetPublished<FunscriptGeneratedEvent>();
            Assert.Single(events);
            Assert.Equal(videoPath, events[0].VideoPath);
            Assert.Equal(Path.ChangeExtension(videoPath, ".funscript"), events[0].FilePath);
            Assert.True(File.Exists(events[0].FilePath));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_ShowsToast()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_fsgen_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(videoPath, "dummy");

            using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
            InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
            _eventBus.Publish(new VideoLoadedEvent { FilePath = videoPath });
            var beatMap = CreateBeatMap(120);
            SetPrivateField(_engine, "_currentBeatMap", beatMap);
            InvokePrivate(vm, "OnBeatMapReady", beatMap);

            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            _toastService.Received(1).Show("Funscript generated:", Arg.Any<string?>());
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_OverwriteConfirmed_WritesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_fsgen_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(videoPath, "dummy");
            var funscriptPath = Path.ChangeExtension(videoPath, ".funscript");
            File.WriteAllText(funscriptPath, "old content");

            Func<string, string, Task<bool>> confirmOverwrite = (_, _) => Task.FromResult(true);
            using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService, confirmOverwrite);
            InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
            _eventBus.Publish(new VideoLoadedEvent { FilePath = videoPath });
            var beatMap = CreateBeatMap(120);
            SetPrivateField(_engine, "_currentBeatMap", beatMap);
            InvokePrivate(vm, "OnBeatMapReady", beatMap);

            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var content = File.ReadAllText(funscriptPath);
            Assert.DoesNotContain("old content", content);
            Assert.Contains("actions", content);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_OverwriteDeclined_DoesNotWrite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_fsgen_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(videoPath, "dummy");
            var funscriptPath = Path.ChangeExtension(videoPath, ".funscript");
            File.WriteAllText(funscriptPath, "old content");

            Func<string, string, Task<bool>> confirmOverwrite = (_, _) => Task.FromResult(false);
            using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService, confirmOverwrite);
            InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
            _eventBus.Publish(new VideoLoadedEvent { FilePath = videoPath });
            var beatMap = CreateBeatMap(120);
            SetPrivateField(_engine, "_currentBeatMap", beatMap);
            InvokePrivate(vm, "OnBeatMapReady", beatMap);

            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            // Should not overwrite — file content unchanged
            var content = File.ReadAllText(funscriptPath);
            Assert.Equal("old content", content);
            var events = _eventBus.GetPublished<FunscriptGeneratedEvent>();
            Assert.Empty(events);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_NoBeatMap_DoesNothing()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        _eventBus.Publish(new VideoLoadedEvent { FilePath = @"C:\videos\test.mp4" });
        // No beatmap set

        await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

        var events = _eventBus.GetPublished<FunscriptGeneratedEvent>();
        Assert.Empty(events);
    }

    [Fact]
    public async Task GenerateFunscript_NoVideoPath_DoesNothing()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
        var beatMap = CreateBeatMap(120);
        SetPrivateField(_engine, "_currentBeatMap", beatMap);
        InvokePrivate(vm, "OnBeatMapReady", beatMap);
        // No video loaded

        await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

        var events = _eventBus.GetPublished<FunscriptGeneratedEvent>();
        Assert.Empty(events);
    }

    // ══════════════════════════════════════════════
    //  Stroke Controls — AmplitudeOffset (vido-198)
    // ══════════════════════════════════════════════

    [Fact]
    public void AmplitudeOffset_DefaultsFromSettings()
    {
        _settings.PulseAmplitudeOffset = 0.7;
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(0.7, vm.AmplitudeOffset, 3);
    }

    [Fact]
    public void AmplitudeOffset_PersistsToSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.AmplitudeOffset = 0.5;

        Assert.Equal(0.5, _settings.PulseAmplitudeOffset, 3);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void AmplitudeOffset_ClampsToRange()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.AmplitudeOffset = 2.0;
        Assert.Equal(1.0, vm.AmplitudeOffset, 3);

        vm.AmplitudeOffset = -5.0;
        Assert.Equal(-1.0, vm.AmplitudeOffset, 3);
    }

    [Fact]
    public void AmplitudeOffset_RaisesPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.AmplitudeOffset = 0.3;

        Assert.Contains(nameof(vm.AmplitudeOffset), changed);
        Assert.Contains(nameof(vm.AmplitudeOffsetLabel), changed);
    }

    [Fact]
    public void AmplitudeOffsetLabel_FormatsCorrectly()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.AmplitudeOffset = 0.5;
        Assert.Equal("+0.5", vm.AmplitudeOffsetLabel);

        vm.AmplitudeOffset = -0.3;
        Assert.Equal("-0.3", vm.AmplitudeOffsetLabel);

        vm.AmplitudeOffset = 0.0;
        Assert.Equal("0.0", vm.AmplitudeOffsetLabel);
    }

    [Fact]
    public void AmplitudeOffset_PropagatesStrokeSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.AmplitudeOffset = 0.6;

        Assert.Equal(0.6, _engine.StrokeSettings.AmplitudeOffset, 3);
    }

    [Fact]
    public void AmplitudeOffset_SameValue_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.AmplitudeOffset = 0.5;

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.AmplitudeOffset = 0.5;

        Assert.DoesNotContain(nameof(vm.AmplitudeOffset), changed);
    }

    // ══════════════════════════════════════════════
    //  Stroke Controls — EasingBlend (vido-198)
    // ══════════════════════════════════════════════

    [Fact]
    public void EasingBlend_DefaultsFromSettings()
    {
        _settings.PulseEasingBlend = -0.4;
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(-0.4, vm.EasingBlend, 3);
    }

    [Fact]
    public void EasingBlend_PersistsToSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.EasingBlend = 0.8;

        Assert.Equal(0.8, _settings.PulseEasingBlend, 3);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void EasingBlend_ClampsToRange()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.EasingBlend = 3.0;
        Assert.Equal(1.0, vm.EasingBlend, 3);

        vm.EasingBlend = -2.0;
        Assert.Equal(-1.0, vm.EasingBlend, 3);
    }

    [Fact]
    public void EasingBlend_PropagatesStrokeSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.EasingBlend = -0.7;

        Assert.Equal(-0.7, _engine.StrokeSettings.EasingBlend, 3);
    }

    // ══════════════════════════════════════════════
    //  Stroke Controls — StrokePattern (vido-198)
    // ══════════════════════════════════════════════

    [Fact]
    public void SelectedStrokePattern_DefaultsFromSettings()
    {
        _settings.PulseStrokePattern = "DoubleTap";
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(StrokePattern.DoubleTap, vm.SelectedStrokePattern);
    }

    [Fact]
    public void SelectedStrokePattern_InvalidString_DefaultsToClassic()
    {
        _settings.PulseStrokePattern = "Nonexistent";
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(StrokePattern.Classic, vm.SelectedStrokePattern);
    }

    [Fact]
    public void SelectedStrokePattern_PersistsToSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.SelectedStrokePattern = StrokePattern.TripleTap;

        Assert.Equal("TripleTap", _settings.PulseStrokePattern);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void SelectedStrokePattern_PropagatesStrokeSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.SelectedStrokePattern = StrokePattern.HoldTop;

        Assert.Equal(StrokePattern.HoldTop, _engine.StrokeSettings.Pattern);
    }

    [Fact]
    public void SelectedStrokePatternIndex_MapsToEnum()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.SelectedStrokePatternIndex = 3; // HoldTop

        Assert.Equal(StrokePattern.HoldTop, vm.SelectedStrokePattern);
        Assert.Equal(3, vm.SelectedStrokePatternIndex);
    }

    [Fact]
    public void SelectedStrokePatternIndex_ClampsToRange()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.SelectedStrokePatternIndex = 10; // Out of range
        Assert.Equal(4, vm.SelectedStrokePatternIndex); // Clamped to max (HoldBottom)

        vm.SelectedStrokePatternIndex = -1;
        Assert.Equal(0, vm.SelectedStrokePatternIndex); // Clamped to 0 (Classic)
    }

    [Fact]
    public void StrokePatternOptions_HasFiveEntries()
    {
        Assert.Equal(5, PulseSidebarViewModel.StrokePatternOptions.Count);
        Assert.Equal("Classic", PulseSidebarViewModel.StrokePatternOptions[0]);
        Assert.Equal("Double Tap", PulseSidebarViewModel.StrokePatternOptions[1]);
        Assert.Equal("Triple Tap", PulseSidebarViewModel.StrokePatternOptions[2]);
        Assert.Equal("Hold Top", PulseSidebarViewModel.StrokePatternOptions[3]);
        Assert.Equal("Hold Bottom", PulseSidebarViewModel.StrokePatternOptions[4]);
    }

    // ══════════════════════════════════════════════
    //  Stroke Controls — Randomness (vido-198)
    // ══════════════════════════════════════════════

    [Fact]
    public void Randomness_DefaultsFromSettings()
    {
        _settings.PulseRandomness = 0.4;
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(0.4, vm.Randomness, 3);
    }

    [Fact]
    public void Randomness_PersistsToSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.Randomness = 0.6;

        Assert.Equal(0.6, _settings.PulseRandomness, 3);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public void Randomness_ClampsToRange()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.Randomness = 2.0;
        Assert.Equal(1.0, vm.Randomness, 3);

        vm.Randomness = -0.5;
        Assert.Equal(0.0, vm.Randomness, 3);
    }

    [Fact]
    public void Randomness_RaisesPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Randomness = 0.5;

        Assert.Contains(nameof(vm.Randomness), changed);
        Assert.Contains(nameof(vm.RandomnessLabel), changed);
    }

    [Fact]
    public void RandomnessLabel_FormatsAsPercentage()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.Randomness = 0.0;
        Assert.Equal("0%", vm.RandomnessLabel);

        vm.Randomness = 0.5;
        Assert.Equal("50%", vm.RandomnessLabel);

        vm.Randomness = 1.0;
        Assert.Equal("100%", vm.RandomnessLabel);
    }

    [Fact]
    public void Randomness_PropagatesStrokeSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.Randomness = 0.8;

        Assert.Equal(0.8, _engine.StrokeSettings.Randomness, 3);
    }

    // ══════════════════════════════════════════════
    //  Stroke Controls — Combined / Constructor (vido-198)
    // ══════════════════════════════════════════════

    [Fact]
    public void Constructor_PropagatesAllStrokeSettings()
    {
        _settings.PulseAmplitudeOffset = 0.3;
        _settings.PulseEasingBlend = -0.5;
        _settings.PulseStrokePattern = "HoldBottom";
        _settings.PulseRandomness = 0.2;

        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(0.3, _engine.StrokeSettings.AmplitudeOffset, 3);
        Assert.Equal(-0.5, _engine.StrokeSettings.EasingBlend, 3);
        Assert.Equal(StrokePattern.HoldBottom, _engine.StrokeSettings.Pattern);
        Assert.Equal(0.2, _engine.StrokeSettings.Randomness, 3);
    }

    [Fact]
    public void Description_ContainsStrokeControlsSection()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Contains("Stroke Controls:", vm.Description);
        Assert.Contains("Amplitude", vm.Description);
        Assert.Contains("Speed", vm.Description);
        Assert.Contains("Pattern", vm.Description);
        Assert.Contains("Randomness", vm.Description);
    }

    [Fact]
    public async Task GenerateFunscript_UsesCurrentStrokeSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_fsgen_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(videoPath, "dummy");

            using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
            InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
            _eventBus.Publish(new VideoLoadedEvent { FilePath = videoPath });
            var beatMap = CreateBeatMap(120);
            SetPrivateField(_engine, "_currentBeatMap", beatMap);
            InvokePrivate(vm, "OnBeatMapReady", beatMap);

            // Set stroke pattern to DoubleTap — should produce more actions than Classic
            vm.SelectedStrokePattern = StrokePattern.DoubleTap;

            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            Assert.True(File.Exists(fsPath));

            var json = File.ReadAllText(fsPath);
            int actionCount = System.Text.Json.JsonDocument.Parse(json)
                .RootElement.GetProperty("actions").GetArrayLength();

            // DoubleTap should produce more actions than the beat count
            Assert.True(actionCount > beatMap.Beats.Count,
                $"Expected more actions than beats ({beatMap.Beats.Count}) with DoubleTap, got {actionCount}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ══════════════════════════════════════════════
    //  Funscript Beat Rate Selector (VI-0024)
    // ══════════════════════════════════════════════

    [Fact]
    public void SelectedFunscriptBeatRateIndex_DefaultsFromSettings()
    {
        _settings.PulseFunscriptBeatRateIndex = 2;
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(2, vm.SelectedFunscriptBeatRateIndex);
    }

    [Fact]
    public void SelectedFunscriptBeatRateIndex_PersistsToSettings()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.SelectedFunscriptBeatRateIndex = 3;

        Assert.Equal(3, _settings.PulseFunscriptBeatRateIndex);
        _settingsService.Received().QueueSave();
    }

    [Fact]
    public async Task GenerateFunscript_WithBeatRate2_GeneratesFilteredFunscript()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_fsgen_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(videoPath, "dummy");

            using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
            InvokePrivate(vm, "OnEngineStateChanged", PulseState.Ready);
            _eventBus.Publish(new VideoLoadedEvent { FilePath = videoPath });
            var beatMap = CreateBeatMap(120);
            SetPrivateField(_engine, "_currentBeatMap", beatMap);
            InvokePrivate(vm, "OnBeatMapReady", beatMap);

            vm.SelectedBeatRateIndex = 1; // divisor 2

            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            Assert.True(File.Exists(fsPath));

            var json = File.ReadAllText(fsPath);
            // Count actions in the JSON — the filtered count should be half (rounded up) of the original
            int totalBeats = beatMap.Beats.Count;
            int expectedFiltered = (totalBeats + 1) / 2; // ceiling division for step-by-2
            int actionCount = System.Text.Json.JsonDocument.Parse(json)
                .RootElement.GetProperty("actions").GetArrayLength();
            Assert.Equal(expectedFiltered, actionCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ══════════════════════════════════════════════
    //  Dispose — subscription cleanup
    // ══════════════════════════════════════════════

    [Fact]
    public void Dispose_CleansUpSubscriptions()
    {
        var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.Dispose();

        // Publishing after dispose should not update internal state
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        _eventBus.Publish(new VideoLoadedEvent { FilePath = @"C:\videos\after.mp4" });

        Assert.DoesNotContain(nameof(vm.CanGenerateFunscript), changed);
    }

    // ══════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, args);
    }

    private static async Task InvokePrivateAsync(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(target, args);
        if (result is Task task)
            await task;
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static PulseEngine CreateEngine(TestEventBus eventBus)
    {
        var decoder = new TestAudioDecoder();
        var preAnalysis = new AudioPreAnalysisService(decoder);
        var liveAmplitude = new LiveAmplitudeService();
        var mapper = new PulseTCodeMapper();
        var logger = Substitute.For<ILogService>();
        return new PulseEngine(preAnalysis, liveAmplitude, mapper, eventBus, logger);
    }

    private static BeatMap CreateBeatMap(double bpm, double durationMs = 10000)
    {
        var beats = new List<BeatEvent>();
        double intervalMs = 60000.0 / bpm;
        for (double t = 0; t < durationMs; t += intervalMs)
            beats.Add(new BeatEvent { TimestampMs = t, Strength = 0.8 });

        return new BeatMap
        {
            Bpm = bpm,
            Beats = beats,
            DurationMs = durationMs,
            WaveformSamples = Array.Empty<float>(),
            WaveformSampleRate = 100
        };
    }

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

        public List<TEvent> GetPublished<TEvent>()
        {
            lock (_lock)
            {
                return _published.OfType<TEvent>().ToList();
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _onDispose;
            public Subscription(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }

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
