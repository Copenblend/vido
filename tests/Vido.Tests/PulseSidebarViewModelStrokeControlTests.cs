using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
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
/// Comprehensive stroke control tests for <see cref="PulseSidebarViewModel"/>
/// covering initialization, persistence, engine propagation, combined controls,
/// and funscript generation with stroke settings applied.
/// </summary>
public sealed class PulseSidebarViewModelStrokeControlTests : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly TestEventBus _eventBus;
    private readonly PulseEngine _engine;
    private readonly IToastService _toastService;

    public PulseSidebarViewModelStrokeControlTests()
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

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Initialization — All Controls Load from AppSettings            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void Constructor_LoadsAllStrokeSettingsFromAppSettings()
    {
        _settings.PulseAmplitudeOffset = 0.3;
        _settings.PulseEasingBlend = -0.5;
        _settings.PulseStrokePattern = "HoldBottom";
        _settings.PulseRandomness = 0.2;

        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(0.3, vm.AmplitudeOffset, 3);
        Assert.Equal(-0.5, vm.EasingBlend, 3);
        Assert.Equal(StrokePattern.HoldBottom, vm.SelectedStrokePattern);
        Assert.Equal(4, vm.SelectedStrokePatternIndex);
        Assert.Equal(0.2, vm.Randomness, 3);
    }

    [Fact]
    public void Constructor_DefaultSettings_AllZero()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(0.0, vm.AmplitudeOffset);
        Assert.Equal(0.0, vm.EasingBlend);
        Assert.Equal(StrokePattern.Classic, vm.SelectedStrokePattern);
        Assert.Equal(0, vm.SelectedStrokePatternIndex);
        Assert.Equal(0.0, vm.Randomness);
    }

    [Fact]
    public void Constructor_InvalidPatternString_FallsBackToClassic()
    {
        _settings.PulseStrokePattern = "InvalidPattern";

        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(StrokePattern.Classic, vm.SelectedStrokePattern);
        Assert.Equal(0, vm.SelectedStrokePatternIndex);
    }

    [Fact]
    public void Constructor_EmptyPatternString_FallsBackToClassic()
    {
        _settings.PulseStrokePattern = "";

        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(StrokePattern.Classic, vm.SelectedStrokePattern);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Persistence — All Controls Save to AppSettings                 ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void AllControls_PersistToAppSettingsAndQueueSave()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.AmplitudeOffset = 0.7;
        vm.EasingBlend = -0.3;
        vm.SelectedStrokePattern = StrokePattern.TripleTap;
        vm.Randomness = 0.9;

        Assert.Equal(0.7, _settings.PulseAmplitudeOffset, 3);
        Assert.Equal(-0.3, _settings.PulseEasingBlend, 3);
        Assert.Equal("TripleTap", _settings.PulseStrokePattern);
        Assert.Equal(0.9, _settings.PulseRandomness, 3);

        // QueueSave called for each property change (4 times)
        _settingsService.Received(4).QueueSave();
    }

    [Fact]
    public void PatternIndex_PersistsViaSetter()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.SelectedStrokePatternIndex = 2; // TripleTap

        Assert.Equal("TripleTap", _settings.PulseStrokePattern);
        Assert.Equal(StrokePattern.TripleTap, vm.SelectedStrokePattern);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Engine Propagation — All Controls Flow to PulseEngine           ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void Constructor_PropagatesAllSettingsToEngine()
    {
        _settings.PulseAmplitudeOffset = 0.6;
        _settings.PulseEasingBlend = 0.4;
        _settings.PulseStrokePattern = "DoubleTap";
        _settings.PulseRandomness = 0.75;

        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Equal(0.6, _engine.StrokeSettings.AmplitudeOffset, 3);
        Assert.Equal(0.4, _engine.StrokeSettings.EasingBlend, 3);
        Assert.Equal(StrokePattern.DoubleTap, _engine.StrokeSettings.Pattern);
        Assert.Equal(0.75, _engine.StrokeSettings.Randomness, 3);
    }

    [Fact]
    public void EachControl_ChangePropagatesImmediately()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        vm.AmplitudeOffset = 0.4;
        Assert.Equal(0.4, _engine.StrokeSettings.AmplitudeOffset, 3);

        vm.EasingBlend = -0.8;
        Assert.Equal(-0.8, _engine.StrokeSettings.EasingBlend, 3);

        vm.SelectedStrokePattern = StrokePattern.HoldTop;
        Assert.Equal(StrokePattern.HoldTop, _engine.StrokeSettings.Pattern);

        vm.Randomness = 0.55;
        Assert.Equal(0.55, _engine.StrokeSettings.Randomness, 3);
    }

    [Fact]
    public void PatternIndex_PropagatesCorrectEnum()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        for (int i = 0; i <= 4; i++)
        {
            vm.SelectedStrokePatternIndex = i;
            Assert.Equal((StrokePattern)i, _engine.StrokeSettings.Pattern);
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Clamping — All Numeric Controls Clamp to Valid Ranges          ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Theory]
    [InlineData(5.0, 1.0)]
    [InlineData(-5.0, -1.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(-1.0, -1.0)]
    public void AmplitudeOffset_ClampsToMinusOnePlusOne(double input, double expected)
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.AmplitudeOffset = input;
        Assert.Equal(expected, vm.AmplitudeOffset, 3);
    }

    [Theory]
    [InlineData(5.0, 1.0)]
    [InlineData(-5.0, -1.0)]
    [InlineData(0.0, 0.0)]
    public void EasingBlend_ClampsToMinusOnePlusOne(double input, double expected)
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.EasingBlend = input;
        Assert.Equal(expected, vm.EasingBlend, 3);
    }

    [Theory]
    [InlineData(5.0, 1.0)]
    [InlineData(-5.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void Randomness_ClampsToZeroOne(double input, double expected)
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.Randomness = input;
        Assert.Equal(expected, vm.Randomness, 3);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10, 4)]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    public void PatternIndex_ClampsToValidRange(int input, int expected)
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.SelectedStrokePatternIndex = input;
        Assert.Equal(expected, vm.SelectedStrokePatternIndex);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ PropertyChanged — All Controls Raise Notifications             ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void AmplitudeOffset_RaisesAllRelatedProperties()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.AmplitudeOffset = 0.5;

        Assert.Contains(nameof(vm.AmplitudeOffset), changed);
        Assert.Contains(nameof(vm.AmplitudeOffsetLabel), changed);
    }

    [Fact]
    public void EasingBlend_RaisesPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.EasingBlend = -0.5;

        Assert.Contains(nameof(vm.EasingBlend), changed);
    }

    [Fact]
    public void StrokePattern_RaisesPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.SelectedStrokePattern = StrokePattern.DoubleTap;

        Assert.Contains(nameof(vm.SelectedStrokePattern), changed);
    }

    [Fact]
    public void Randomness_RaisesAllRelatedProperties()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Randomness = 0.75;

        Assert.Contains(nameof(vm.Randomness), changed);
        Assert.Contains(nameof(vm.RandomnessLabel), changed);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Same-Value Guards — No Spurious Notifications                  ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void AllControls_SameValue_NoPropertyChanged()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.AmplitudeOffset = 0.5;
        vm.EasingBlend = -0.3;
        vm.SelectedStrokePattern = StrokePattern.DoubleTap;
        vm.Randomness = 0.7;

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.AmplitudeOffset = 0.5;
        vm.EasingBlend = -0.3;
        vm.SelectedStrokePattern = StrokePattern.DoubleTap;
        vm.Randomness = 0.7;

        Assert.DoesNotContain(nameof(vm.AmplitudeOffset), changed);
        Assert.DoesNotContain(nameof(vm.EasingBlend), changed);
        Assert.DoesNotContain(nameof(vm.SelectedStrokePattern), changed);
        Assert.DoesNotContain(nameof(vm.Randomness), changed);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Display Labels — Correct Formatting                            ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Theory]
    [InlineData(0.5, "+0.5")]
    [InlineData(-0.3, "-0.3")]
    [InlineData(0.0, "0.0")]
    [InlineData(1.0, "+1.0")]
    [InlineData(-1.0, "-1.0")]
    public void AmplitudeOffsetLabel_FormatsCorrectly(double value, string expected)
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.AmplitudeOffset = value;
        Assert.Equal(expected, vm.AmplitudeOffsetLabel);
    }

    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(0.5, "50%")]
    [InlineData(1.0, "100%")]
    [InlineData(0.33, "33%")]
    public void RandomnessLabel_FormatsAsPercentage(double value, string expected)
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);
        vm.Randomness = value;
        Assert.Equal(expected, vm.RandomnessLabel);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ StrokePatternOptions Static List                               ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void StrokePatternOptions_HasCorrectCount()
    {
        Assert.Equal(5, PulseSidebarViewModel.StrokePatternOptions.Count);
    }

    [Fact]
    public void StrokePatternOptions_ContainsAllPatterns()
    {
        var options = PulseSidebarViewModel.StrokePatternOptions;
        Assert.Equal("Classic", options[0]);
        Assert.Equal("Double Tap", options[1]);
        Assert.Equal("Triple Tap", options[2]);
        Assert.Equal("Hold Top", options[3]);
        Assert.Equal("Hold Bottom", options[4]);
    }

    [Fact]
    public void SelectedStrokePatternIndex_MapsToAllEnumValues()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        var expectedMapping = new[]
        {
            (0, StrokePattern.Classic),
            (1, StrokePattern.DoubleTap),
            (2, StrokePattern.TripleTap),
            (3, StrokePattern.HoldTop),
            (4, StrokePattern.HoldBottom),
        };

        foreach (var (index, pattern) in expectedMapping)
        {
            vm.SelectedStrokePatternIndex = index;
            Assert.Equal(pattern, vm.SelectedStrokePattern);
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Generate Funscript — Settings Applied                          ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public async Task GenerateFunscript_WithDoubleTap_ProducesMoreActions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_vm_stroke_test_" + Guid.NewGuid().ToString("N")[..8]);
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

            // Classic first
            vm.SelectedStrokePattern = StrokePattern.Classic;
            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");
            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            var classicJson = File.ReadAllText(fsPath);
            int classicCount = JsonDocument.Parse(classicJson)
                .RootElement.GetProperty("actions").GetArrayLength();

            // Now DoubleTap
            vm.SelectedStrokePattern = StrokePattern.DoubleTap;
            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");
            var doubleTapJson = File.ReadAllText(fsPath);
            int doubleTapCount = JsonDocument.Parse(doubleTapJson)
                .RootElement.GetProperty("actions").GetArrayLength();

            Assert.True(doubleTapCount > classicCount,
                $"DoubleTap ({doubleTapCount}) should produce more actions than Classic ({classicCount})");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_WithMaxAmplitude_ProducesFullRange()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_vm_stroke_test_" + Guid.NewGuid().ToString("N")[..8]);
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

            vm.AmplitudeOffset = 1.0;
            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            var json = File.ReadAllText(fsPath);
            var actions = JsonDocument.Parse(json)
                .RootElement.GetProperty("actions").EnumerateArray()
                .Select(a => a.GetProperty("pos").GetInt32())
                .ToList();

            // With max amplitude, should reach near 95 and 5
            Assert.Contains(actions, p => p >= 90);
            Assert.Contains(actions, p => p <= 10);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_WithMinAmplitude_CollapsesToCenter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_vm_stroke_test_" + Guid.NewGuid().ToString("N")[..8]);
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

            vm.AmplitudeOffset = -1.0;
            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            var json = File.ReadAllText(fsPath);
            var actions = JsonDocument.Parse(json)
                .RootElement.GetProperty("actions").EnumerateArray()
                .Select(a => a.GetProperty("pos").GetInt32())
                .ToList();

            // With min amplitude, all actions should be at center (50)
            Assert.All(actions, p => Assert.Equal(50, p));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_WithHoldTop_ProducesHoldKeyframes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_vm_stroke_test_" + Guid.NewGuid().ToString("N")[..8]);
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

            vm.SelectedStrokePattern = StrokePattern.HoldTop;
            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            var json = File.ReadAllText(fsPath);
            int actionCount = JsonDocument.Parse(json)
                .RootElement.GetProperty("actions").GetArrayLength();

            // HoldTop: 3 actions per beat
            Assert.Equal(beatMap.Beats.Count * 3, actionCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateFunscript_BeatRateApplied_ReducesBeatsBeforeStrokeSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vido_vm_stroke_test_" + Guid.NewGuid().ToString("N")[..8]);
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

            // Divisor 2, TripleTap pattern → each filtered beat produces 6 actions
            vm.SelectedBeatRateIndex = 1; // divisor 2
            vm.SelectedStrokePattern = StrokePattern.TripleTap;
            await InvokePrivateAsync(vm, "GenerateFunscriptAsync");

            var fsPath = Path.ChangeExtension(videoPath, ".funscript");
            var json = File.ReadAllText(fsPath);
            int actionCount = JsonDocument.Parse(json)
                .RootElement.GetProperty("actions").GetArrayLength();

            int filteredBeats = (beatMap.Beats.Count + 1) / 2; // ceiling division
            Assert.Equal(filteredBeats * 6, actionCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Description Text — Documents Stroke Controls                   ║
    // ╚══════════════════════════════════════════════════════════════════╝

    [Fact]
    public void Description_ContainsStrokeControlsDocumentation()
    {
        using var vm = new PulseSidebarViewModel(_engine, _settingsService, _eventBus, _toastService);

        Assert.Contains("Stroke Controls:", vm.Description);
        Assert.Contains("Beat Rate", vm.Description);
        Assert.Contains("Amplitude", vm.Description);
        Assert.Contains("Speed", vm.Description);
        Assert.Contains("Pattern", vm.Description);
        Assert.Contains("Randomness", vm.Description);
        Assert.Contains("Generate Funscript:", vm.Description);
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Helpers                                                        ║
    // ╚══════════════════════════════════════════════════════════════════╝

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
