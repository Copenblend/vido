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
