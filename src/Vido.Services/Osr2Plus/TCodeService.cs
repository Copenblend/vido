using System.Diagnostics;
using Vido.Core.Models.Osr2Plus;

namespace Vido.Services.Osr2Plus;

/// <summary>
/// Generates and sends TCode commands to the connected device at a configurable output rate.
/// Uses a dedicated background thread with Stopwatch-based precise timing and time extrapolation
/// to produce smooth, in-sync TCode output independent of the UI thread refresh rate.
/// </summary>
public class TCodeService : IDisposable
{
    private readonly InterpolationService _interpolation;

    // Transport
    private ITransportService? _transport;

    // Output thread
    private Thread? _outputThread;
    private volatile bool _threadRunning;

    // Pending direct command: written by UI thread (HomeAxes, SendMidpoint, SendPositionWithOffset),
    // consumed by the output thread to avoid cross-thread serial writes.
    private string? _pendingDirectCommand;

    // Playback state (written by UI thread, read by output thread)
    private volatile bool _isPlaying;
    private volatile float _playbackSpeed = 1.0f;
    private volatile int _outputRateHz = 100;

    // Time extrapolation: the UI thread periodically sets the "sync point".
    // The output thread uses Stopwatch to extrapolate actual time between sync points.
    private readonly object _timeLock = new();
    private double _syncTimeMs;           // media time at last sync point
    private long _syncTicks;              // Stopwatch ticks at last sync point
    private bool _syncPlaying;            // whether media was playing at sync point

    // Axis data
    private FunscriptData?[] _scriptsByAxis = Array.Empty<FunscriptData?>();
    private List<AxisConfig> _axisConfigs = new();
    private AxisConfig? _cachedStrokeConfig;
    private bool _hasActiveFillConfigs;
    private int _offsetMs;
    private int _axisCount;

    // Dirty value tracking: only send axes whose TCode value changed
    private int[] _lastSentByAxis = Array.Empty<int>();

    // Reused output command buffer for allocation-free hot-path formatting.
    // 4 axes × (~12 bytes per command) + separators/newline fits comfortably.
    private readonly byte[] _commandBuffer = new byte[128];
    private int _commandLength;

    // ===== Fill Mode State =====

    /// <summary>Maximum position (0–100) for any pitch (R2) fill mode.</summary>
    internal const double PitchFillMaxPosition = 100.0;

    // Random pattern generators — one per axis
    private RandomPatternGenerator?[] _randomGenByAxis = Array.Empty<RandomPatternGenerator?>();

    // Stroke tracking for random synchronization
    private double _lastStrokePosition = 50.0;
    private double _cumulativeStrokeDistance;

    // Per-axis cumulative fill time (for independent pattern fill)
    private double[] _cumulativeFillByAxis = Array.Empty<double>();

    // Return-to-center: axis ordinal → current interpolated TCode position (NaN = not returning)
    private double[] _returningByAxis = Array.Empty<double>();

    // Ramp-up: axis ordinal → blend factor (NaN = not ramping, 0.0 = midpoint, 1.0 = fully active)
    private double[] _rampingByAxis = Array.Empty<double>();

    // Previous axis state snapshot for detecting transitions
    private (bool Enabled, AxisFillMode FillMode, bool HasValue)[] _prevStateByAxis =
        Array.Empty<(bool, AxisFillMode, bool)>();

    // ===== Test Mode State =====
    private readonly object _testLock = new();
    private TestAxisState?[] _testingByAxis = Array.Empty<TestAxisState?>();
    private readonly List<string> _stoppedTestIds = new(4);

    /// <summary>Raised when a test axis finishes ramping down.</summary>
    public event Action<string>? TestAxisStopped;

    /// <summary>Raised when all test axes are auto-stopped (e.g. playback starts).</summary>
    public event Action? AllTestsStopped;

    /// <summary>Gets the current output rate in Hz.</summary>
    public int OutputRateHz => _outputRateHz;

    /// <summary>Gets a value indicating whether any axis has funscript data loaded.</summary>
    public bool HasScriptsLoaded
    {
        get
        {
            for (int i = 0; i < _axisCount; i++)
                if (_scriptsByAxis[i] != null) return true;
            return false;
        }
    }

    /// <summary>Gets a value indicating whether funscript is actively playing (blocks test mode).</summary>
    public bool IsFunscriptPlaying => _isPlaying && HasScriptsLoaded;

    /// <summary>
    /// Gets or sets the active transport (Serial or UDP). Set by the connection logic on connect.
    /// </summary>
    public ITransportService? Transport
    {
        get => _transport;
        set => _transport = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TCodeService"/> class.
    /// </summary>
    /// <param name="interpolation">The interpolation service for resolving funscript positions.</param>
    public TCodeService(InterpolationService interpolation)
    {
        _interpolation = interpolation;
        _syncTicks = Stopwatch.GetTimestamp();
    }

    // ===== Public API — thread-safe, called from UI thread =====

    /// <summary>
    /// Sets the loaded funscript data for all axes. Resets interpolation indices,
    /// stroke tracking, and random generators.
    /// </summary>
    /// <param name="scripts">Dictionary of axisId → funscript data.</param>
    public void SetScripts(Dictionary<string, FunscriptData> scripts)
    {
        // Clear existing scripts
        Array.Clear(_scriptsByAxis);

        // Map incoming scripts to axis ordinals
        foreach (var (axisId, data) in scripts)
        {
            var idx = GetOrdinalForId(axisId);
            if (idx >= 0) _scriptsByAxis[idx] = data;
        }

        Array.Fill(_lastSentByAxis, -1);
        _interpolation.ResetIndices();
        // Reset stroke tracking and random generators on script change
        _cumulativeStrokeDistance = 0;
        _lastStrokePosition = 50.0;
        Array.Clear(_cumulativeFillByAxis);
        for (int i = 0; i < _axisCount; i++)
            _randomGenByAxis[i]?.Reset();
    }

    /// <summary>
    /// Sets the axis configurations (min/max/enabled/fill mode).
    /// Assigns ordinals, allocates per-axis state arrays, and detects
    /// transitions to trigger ramp-up and return-to-center animations.
    /// </summary>
    /// <param name="configs">List of axis configurations.</param>
    public void SetAxisConfigs(List<AxisConfig> configs)
    {
        var count = configs.Count;
        for (int i = 0; i < count; i++)
            configs[i].Ordinal = i;

        // Snapshot old state before reallocating
        var oldPrevState = _prevStateByAxis;
        var oldAxisConfigs = _axisConfigs;
        var oldLastSent = _lastSentByAxis;
        var oldTestingByAxis = _testingByAxis;

        // Allocate state arrays (only on config change, not hot path)
        _scriptsByAxis = new FunscriptData?[count];
        _lastSentByAxis = new int[count];
        Array.Fill(_lastSentByAxis, -1);
        _randomGenByAxis = new RandomPatternGenerator?[count];
        _cumulativeFillByAxis = new double[count];
        _returningByAxis = new double[count];
        Array.Fill(_returningByAxis, double.NaN);
        _rampingByAxis = new double[count];
        Array.Fill(_rampingByAxis, double.NaN);
        _prevStateByAxis = new (bool, AxisFillMode, bool)[count];
        _testingByAxis = new TestAxisState?[count];
        _axisCount = count;
        _interpolation.SetAxisCount(count);

        AxisConfig? cachedStrokeConfig = null;
        bool hasActiveFillConfigs = false;

        foreach (var cfg in configs)
        {
            var idx = cfg.Ordinal;

            if (cachedStrokeConfig == null && cfg.Id == "L0" && cfg.Enabled)
                cachedStrokeConfig = cfg;

            if (!hasActiveFillConfigs && cfg.Enabled && cfg.FillMode != AxisFillMode.None)
                hasActiveFillConfigs = true;

            // Find previous state by matching axis ID in old configs
            bool hasPrev = false;
            (bool Enabled, AxisFillMode FillMode, bool HasValue) prev = default;
            int oldIdx = -1;
            for (int j = 0; j < oldAxisConfigs.Count; j++)
            {
                if (oldAxisConfigs[j].Id == cfg.Id)
                {
                    oldIdx = j;
                    if (j < oldPrevState.Length && oldPrevState[j].HasValue)
                    {
                        prev = oldPrevState[j];
                        hasPrev = true;
                    }

                    // Carry forward test state
                    if (j < oldTestingByAxis.Length)
                        _testingByAxis[idx] = oldTestingByAxis[j];

                    break;
                }
            }

            if (hasPrev)
            {
                bool wasEnabled = prev.Enabled;
                bool wasActiveFill = prev.Enabled && prev.FillMode != AxisFillMode.None;
                bool nowEnabled = cfg.Enabled;
                bool nowActiveFill = cfg.Enabled && cfg.FillMode != AxisFillMode.None;

                // Return-to-center when: axis disabled, OR fill mode set to None
                bool justDisabled = wasEnabled && !nowEnabled;
                bool fillJustCleared = wasActiveFill && nowEnabled && cfg.FillMode == AxisFillMode.None;
                if (justDisabled || fillJustCleared)
                {
                    if (oldIdx >= 0 && oldIdx < oldLastSent.Length)
                    {
                        var lastVal = oldLastSent[oldIdx];
                        if (lastVal != -1 && Math.Abs(lastVal - 500) >= 1)
                        {
                            _returningByAxis[idx] = lastVal;
                        }
                    }
                    _rampingByAxis[idx] = double.NaN;
                }

                // Ramp-up when: axis activated with active fill from inactive state
                bool justActivated = nowActiveFill && (!wasActiveFill || !wasEnabled);
                if (justActivated)
                {
                    _returningByAxis[idx] = double.NaN;
                    _rampingByAxis[idx] = 0.0;
                }
            }

            _prevStateByAxis[idx] = (cfg.Enabled, cfg.FillMode, true);
        }

        _axisConfigs = configs;
        _cachedStrokeConfig = cachedStrokeConfig;
        _hasActiveFillConfigs = hasActiveFillConfigs;
    }

    /// <summary>
    /// Sets the TCode output rate in Hz. Clamped to 30–200.
    /// </summary>
    /// <param name="hz">Desired output rate in hertz.</param>
    public void SetOutputRate(int hz)
    {
        _outputRateHz = Math.Clamp(hz, 30, 200);
    }

    /// <summary>
    /// Called from the UI thread with the media player's current time (~60 Hz).
    /// Updates the sync point for time extrapolation on the output thread.
    /// </summary>
    /// <param name="timeMs">Current media time in milliseconds.</param>
    public void SetTime(double timeMs)
    {
        lock (_timeLock)
        {
            _syncTimeMs = timeMs;
            _syncTicks = Stopwatch.GetTimestamp();
            _syncPlaying = _isPlaying;
        }
    }

    /// <summary>
    /// Sets the playback state. Re-anchors the sync point to preserve continuity.
    /// Auto-stops all test axes when funscript playback starts.
    /// </summary>
    /// <param name="playing">Whether media is currently playing.</param>
    public void SetPlaying(bool playing)
    {
        lock (_timeLock)
        {
            _syncTimeMs = GetExtrapolatedTimeMsLocked();
            _syncTicks = Stopwatch.GetTimestamp();
            _syncPlaying = playing;
        }
        _isPlaying = playing;

        // Auto-stop all test axes when funscript playback starts
        if (playing && HasScriptsLoaded)
        {
            StopAllTestAxes();
        }
    }

    /// <summary>
    /// Sets the playback speed multiplier. Re-anchors the sync point.
    /// </summary>
    /// <param name="speed">Playback speed multiplier (e.g. 1.0 = normal, 2.0 = double speed).</param>
    public void SetPlaybackSpeed(float speed)
    {
        lock (_timeLock)
        {
            _syncTimeMs = GetExtrapolatedTimeMsLocked();
            _syncTicks = Stopwatch.GetTimestamp();
        }
        _playbackSpeed = speed;
    }

    /// <summary>
    /// Sets the funscript offset in milliseconds. Positive = script plays later, negative = earlier.
    /// </summary>
    /// <param name="offsetMs">Offset in milliseconds.</param>
    public void SetOffset(int offsetMs)
    {
        _offsetMs = offsetMs;
    }

    // ===== Thread Lifecycle =====

    /// <summary>
    /// Starts the TCode output thread. No-op if already running.
    /// The thread runs at <see cref="ThreadPriority.AboveNormal"/> for timing precision.
    /// </summary>
    public void Start()
    {
        if (_outputThread != null) return;

        _threadRunning = true;
        _outputThread = new Thread(OutputLoop)
        {
            IsBackground = true,
            Name = "TCodeOutput",
            Priority = ThreadPriority.AboveNormal
        };
        _outputThread.Start();
    }

    /// <summary>
    /// Stops the TCode output thread and clears all state.
    /// Uses a 1500ms join timeout to avoid blocking the UI thread indefinitely
    /// if the background thread is stuck in a serial write.
    /// </summary>
    public void StopTimer()
    {
        _threadRunning = false;
        if (_outputThread is not null)
        {
            if (!_outputThread.Join(1500))
            {
                // Thread is stuck in a serial write — it will exit when the write completes
                // or when the process exits. Do not block the UI.
            }
            _outputThread = null;
        }
        Array.Fill(_lastSentByAxis, -1);
        lock (_testLock) Array.Clear(_testingByAxis);
    }

    // ===== Test Mode API =====

    /// <summary>
    /// Starts test oscillation on the given axis at the specified speed.
    /// </summary>
    /// <param name="axisId">The axis to test (e.g. "L0", "R0").</param>
    /// <param name="speedHz">Oscillation speed in Hz (clamped to 0.1–5.0).</param>
    public void StartTestAxis(string axisId, double speedHz)
    {
        speedHz = Math.Clamp(speedHz, 0.1, 5.0);
        var idx = GetOrdinalForId(axisId);
        if (idx < 0) return;
        lock (_testLock)
        {
            _testingByAxis[idx] = new TestAxisState
            {
                Phase = 0,
                CurrentSpeedHz = speedHz,
                TargetSpeedHz = speedHz,
                CurrentAmplitude = 0,       // Ramps up smoothly
                TargetAmplitude = 50,       // Full range: ±50 around center
                LastTickAt = Stopwatch.GetTimestamp()
            };
        }

        // Reset cached random generator so it doesn't have stale _transitionStart
        _randomGenByAxis[idx]?.Reset();
    }

    /// <summary>
    /// Stops test oscillation on the given axis immediately and sends a midpoint command.
    /// </summary>
    /// <param name="axisId">The axis to stop testing.</param>
    public void StopTestAxis(string axisId)
    {
        var idx = GetOrdinalForId(axisId);
        bool wasRemoved = false;
        if (idx >= 0)
        {
            lock (_testLock)
            {
                wasRemoved = _testingByAxis[idx] != null;
                _testingByAxis[idx] = null;
            }
        }

        if (wasRemoved)
        {
            SendMidpoint(axisId);
        }

        TestAxisStopped?.Invoke(axisId);
    }

    /// <summary>
    /// Updates the test speed for an axis currently under test.
    /// </summary>
    /// <param name="axisId">The axis to update.</param>
    /// <param name="speedHz">New target speed in Hz (clamped to 0.1–5.0).</param>
    public void UpdateTestSpeed(string axisId, double speedHz)
    {
        speedHz = Math.Clamp(speedHz, 0.1, 5.0);
        var idx = GetOrdinalForId(axisId);
        if (idx < 0) return;
        lock (_testLock)
        {
            var state = _testingByAxis[idx];
            if (state != null)
            {
                state.TargetSpeedHz = speedHz;
            }
        }
    }

    /// <summary>
    /// Checks whether an axis is currently under test.
    /// </summary>
    /// <param name="axisId">The axis to check.</param>
    /// <returns><c>true</c> if the axis is currently being tested.</returns>
    public bool IsAxisTesting(string axisId)
    {
        var idx = GetOrdinalForId(axisId);
        if (idx < 0) return false;
        lock (_testLock) return _testingByAxis[idx] != null;
    }

    /// <summary>
    /// Stops all test axes immediately (e.g. on disconnect or playback start).
    /// Sends midpoint commands for all stopped axes and raises <see cref="AllTestsStopped"/>.
    /// </summary>
    public void StopAllTestAxes()
    {
        _stoppedTestIds.Clear();
        lock (_testLock)
        {
            for (int i = 0; i < _axisCount; i++)
            {
                if (_testingByAxis[i] != null)
                {
                    _stoppedTestIds.Add(_axisConfigs[i].Id);
                    _testingByAxis[i] = null;
                }
            }
        }

        // Send midpoints outside the lock
        foreach (var id in _stoppedTestIds)
        {
            SendMidpoint(id);
        }

        if (_stoppedTestIds.Count > 0)
        {
            AllTestsStopped?.Invoke();
        }
    }

    /// <summary>
    /// Disposes the service by stopping the output thread.
    /// </summary>
    public void Dispose()
    {
        StopTimer();
        GC.SuppressFinalize(this);
    }

    // ===== Time Extrapolation =====

    /// <summary>
    /// Returns the extrapolated media time in milliseconds.
    /// Must be called while holding _timeLock.
    /// </summary>
    private double GetExtrapolatedTimeMsLocked()
    {
        if (!_syncPlaying) return _syncTimeMs;
        var elapsedTicks = Stopwatch.GetTimestamp() - _syncTicks;
        var elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        return _syncTimeMs + elapsedMs * _playbackSpeed;
    }

    /// <summary>
    /// Returns the extrapolated media time in milliseconds (thread-safe).
    /// </summary>
    internal double GetExtrapolatedTimeMs()
    {
        lock (_timeLock) return GetExtrapolatedTimeMsLocked();
    }

    // ===== Output Loop =====

    private void OutputLoop()
    {
        var stopwatch = Stopwatch.StartNew();

        while (_threadRunning)
        {
            var targetIntervalMs = 1000.0 / _outputRateHz;

            var elapsedSec = stopwatch.ElapsedTicks / (double)Stopwatch.Frequency;
            stopwatch.Restart();

            try
            {
                // Drain any pending direct command (HomeAxes, SendMidpoint, etc.)
                var pending = Interlocked.Exchange(ref _pendingDirectCommand, null);
                if (pending is not null)
                    _transport?.Send(pending);

                if (_transport?.IsConnected == true)
                {
                    bool hasTestAxes = false;
                    lock (_testLock)
                    {
                        for (int i = 0; i < _axisCount; i++)
                            if (_testingByAxis[i] != null) { hasTestAxes = true; break; }
                    }

                    bool hasFillOrReturn = HasActiveFillModes();
                    if (_isPlaying || hasFillOrReturn || hasTestAxes)
                    {
                        OutputTick(elapsedSec, hasTestAxes);
                    }
                }
            }
            catch
            {
                // Swallow to keep the output thread alive; next tick may succeed
            }

            SleepPrecise(stopwatch, targetIntervalMs);
        }
    }

    /// <summary>
    /// Precise sleep using Stopwatch + Thread.Sleep tiers.
    /// Uses graduated sleep intervals to balance CPU usage and timing accuracy.
    /// </summary>
    internal static void SleepPrecise(Stopwatch stopwatch, double millisecondsTimeout)
    {
        var frequencyInverse = 1.0 / Stopwatch.Frequency;

        while (true)
        {
            var elapsedMs = stopwatch.ElapsedTicks * frequencyInverse * 1000.0;
            var remaining = millisecondsTimeout - elapsedMs;

            if (remaining <= 0) break;

            if (remaining < 5)
                Thread.Sleep(1);
            else if (remaining < 15)
                Thread.Sleep(5);
            else
                Thread.Sleep(10);
        }
    }

    // ===== Output Tick =====

    private void OutputTick(double elapsedSec, bool hasTestAxes)
    {
        var rawTimeMs = GetExtrapolatedTimeMs();
        var currentTimeMs = rawTimeMs - _offsetMs;

        // Interval for TCode I parameter: actual elapsed time since last tick.
        // This tells the device how long to take reaching the target position.
        var intervalMs = (int)Math.Floor(elapsedSec * 1000.0 + 0.75);
        intervalMs = Math.Max(1, intervalMs);
        _commandLength = 0;

        // === First pass: compute L0 stroke position for random sync ===
        double strokePosition = 50.0;
        bool hasStrokeScript = false;
        var strokeConfig = _cachedStrokeConfig;
        if (strokeConfig != null)
        {
            var strokeScript = _scriptsByAxis[strokeConfig.Ordinal];
            if (strokeScript != null)
            {
                strokePosition = _interpolation.GetPosition(strokeScript, currentTimeMs, strokeConfig.Ordinal);
                hasStrokeScript = true;
            }
        }
        // Accumulate stroke travel distance for random/sync fill speed
        var strokeDelta = strokePosition - _lastStrokePosition;
        _cumulativeStrokeDistance += Math.Abs(strokeDelta);
        _lastStrokePosition = strokePosition;

        // === Per-axis loop ===
        foreach (var config in _axisConfigs)
        {
            // Disabled axis: finish return-to-center, skip everything else
            if (!config.Enabled)
            {
                ProcessReturnToCenter(config, intervalMs);
                continue;
            }

            // === Test mode: uses selected fill mode pattern with smooth ramp-up ===
            TestAxisState? testState = null;
            if (hasTestAxes)
            {
                lock (_testLock) testState = _testingByAxis[config.Ordinal];
            }

            if (testState != null)
            {
                // L0 always uses simple triangle (up/down) for test mode
                // Other axes require a fill mode selection
                if (config.Id != "L0" && config.FillMode == AxisFillMode.None)
                {
                    continue;
                }

                var now = Stopwatch.GetTimestamp();
                var testDeltaSec = Math.Min(
                    (now - testState.LastTickAt) / (double)Stopwatch.Frequency, 0.1);
                testState.LastTickAt = now;

                // Time-independent exponential smoothing for speed transition (~3.0/s convergence rate)
                var speedSmoothing = 1.0 - Math.Exp(-3.0 * testDeltaSec);
                testState.CurrentSpeedHz += (testState.TargetSpeedHz - testState.CurrentSpeedHz) * speedSmoothing;

                // Time-independent amplitude ramp-up (~5.0/s convergence rate)
                var ampSmoothing = 1.0 - Math.Exp(-5.0 * testDeltaSec);
                testState.CurrentAmplitude += (testState.TargetAmplitude - testState.CurrentAmplitude) * ampSmoothing;

                // Advance phase (cumulative — no jumps on speed change)
                // Scale by output rate ratio so higher rates = faster test motion
                var rateMultiplier = _outputRateHz / 100.0;
                testState.Phase += testState.CurrentSpeedHz * rateMultiplier * testDeltaSec;
                testState.Phase %= 1.0;

                // Use the selected fill mode pattern (L0 always uses Triangle)
                var effectiveFillMode = config.Id == "L0" ? AxisFillMode.Triangle : config.FillMode;
                double patternInput = testState.Phase;

                double position;
                if (effectiveFillMode == AxisFillMode.Random)
                {
                    // Random uses its own cosine-interpolated generator
                    var idx = config.Ordinal;
                    var generator = _randomGenByAxis[idx];
                    if (generator == null)
                    {
                        generator = new RandomPatternGenerator(config.Min, config.Max);
                        _randomGenByAxis[idx] = generator;
                    }
                    generator.SetRange(config.Min, config.Max);

                    testState.CumulativeProgress += testState.CurrentSpeedHz * rateMultiplier * testDeltaSec * 200.0;
                    var rawPos = generator.GetPosition(testState.CumulativeProgress);
                    // rawPos is already in min..max range — map to 0-100 for consistent blending
                    position = rawPos;
                }
                else
                {
                    var patternValue = PatternGenerator.Calculate(effectiveFillMode, patternInput);
                    // patternValue is 0.0–1.0, map to position 0–100
                    position = patternValue * 100.0;
                }

                // Blend with midpoint for ramp-up
                var blend = Math.Clamp(testState.CurrentAmplitude / 50.0, 0.0, 1.0);
                var testPosition = 50.0 + (position - 50.0) * blend;

                // Safety cap: limit all pitch fills to 0-PitchFillMaxPosition
                testPosition = ClampPitchFillPosition(config, testPosition);

                // Scale through axis min/max and apply offset
                var testTcode = ApplyPositionOffset(config, PositionToTCode(config, testPosition));
                if (IsDirty(config.Ordinal, testTcode))
                {
                    AppendCommand(config, testTcode, intervalMs);
                    _lastSentByAxis[config.Ordinal] = testTcode;
                }
                continue; // Skip normal playback for this axis
            }

            // Scripted axis: interpolate position from funscript
            var axisScript = _scriptsByAxis[config.Ordinal];
            if (_isPlaying && axisScript != null)
            {
                var position = _interpolation.GetPosition(axisScript, currentTimeMs, config.Ordinal);
                var tcodeValue = ApplyPositionOffset(config, PositionToTCode(config, position));

                if (IsDirty(config.Ordinal, tcodeValue))
                {
                    AppendCommand(config, tcodeValue, intervalMs);
                    _lastSentByAxis[config.Ordinal] = tcodeValue;
                }
                continue;
            }

            // === Fill mode (no script for this axis) ===
            // Fill patterns only generate output during playback
            if (config.FillMode == AxisFillMode.None || !_isPlaying)
            {
                ProcessReturnToCenter(config, intervalMs);
                continue;
            }

            // --- Random fill ---
            if (config.FillMode == AxisFillMode.Random)
            {
                var idx = config.Ordinal;
                var generator = _randomGenByAxis[idx];
                if (generator == null)
                {
                    generator = new RandomPatternGenerator(config.Min, config.Max);
                    _randomGenByAxis[idx] = generator;
                }
                generator.SetRange(config.Min, config.Max);

                // Use cumulative stroke distance when synced; otherwise time-based (Hz-normalized)
                double progress;
                if (config.SyncWithStroke && config.Id != "L0")
                {
                    if (!hasStrokeScript)
                    {
                        ProcessReturnToCenter(config, intervalMs);
                        continue;
                    }
                    progress = _cumulativeStrokeDistance;
                }
                else
                {
                    // Normalize time to same scale as stroke distance (~200 units per cycle)
                    // At 1 Hz fill speed, one second = 200 progress units
                    var cumTime = _cumulativeFillByAxis[idx];
                    cumTime += config.FillSpeedHz * elapsedSec * 200.0;
                    _cumulativeFillByAxis[idx] = cumTime;
                    progress = cumTime;
                }

                var randomPos = generator.GetPosition(progress);
                // Safety cap: limit pitch fills to 0-PitchFillMaxPosition
                randomPos = ClampPitchFillPosition(config, randomPos);
                var targetVal = (int)(randomPos / 100.0 * 999);
                targetVal = Math.Clamp(targetVal, 0, 999);
                targetVal = ApplyPositionOffset(config, targetVal);

                var randomVal = ApplyRampUp(idx, targetVal);
                if (IsDirty(idx, randomVal))
                {
                    AppendCommand(config, randomVal, intervalMs);
                    _lastSentByAxis[idx] = randomVal;
                }
                continue;
            }

            // --- Waveform fill (Triangle, Sine, Saw, etc.) ---
            {
                var idx = config.Ordinal;

                // When synced to stroke, only move if stroke script is loaded
                if (config.SyncWithStroke && config.Id != "L0" && !hasStrokeScript)
                {
                    ProcessReturnToCenter(config, intervalMs);
                    continue;
                }

                // Advance fill time
                double fillTime;
                if (config.SyncWithStroke && config.Id != "L0" && hasStrokeScript)
                {
                    // Sync with stroke: use cumulative stroke distance as time base
                    // Normalize: a full stroke cycle ≈ 200 distance units → 1.0 period
                    fillTime = _cumulativeStrokeDistance * config.FillSpeedHz / 200.0;
                }
                else
                {
                    // Independent: accumulate time based on fill speed
                    var cumTime = _cumulativeFillByAxis[idx];
                    cumTime += config.FillSpeedHz * elapsedSec;
                    _cumulativeFillByAxis[idx] = cumTime;
                    fillTime = cumTime;
                }

                // PatternGenerator.Calculate returns 0.0–1.0
                var patternValue = PatternGenerator.Calculate(config.FillMode, fillTime);
                // Map 0.0–1.0 to position 0–100, then cap pitch fills for safety
                var position = patternValue * 100.0;
                position = ClampPitchFillPosition(config, position);
                var targetVal = ApplyPositionOffset(config, PositionToTCode(config, position));

                var finalVal = ApplyRampUp(idx, targetVal);
                if (IsDirty(idx, finalVal))
                {
                    AppendCommand(config, finalVal, intervalMs);
                    _lastSentByAxis[idx] = finalVal;
                }
            }
        }

        if (_commandLength > 0)
        {
            WriteByte((byte)'\n');
            _transport?.Send(_commandBuffer.AsSpan(0, _commandLength));
        }
    }

    // ===== Fill Mode Helpers =====

    /// <summary>
    /// Applies exponential ramp-up blend from midpoint (500) to target.
    /// </summary>
    private int ApplyRampUp(int ordinal, int targetVal)
    {
        var blend = _rampingByAxis[ordinal];
        if (double.IsNaN(blend))
            return targetVal;

        blend += (1.0 - blend) * 0.04;

        if (blend >= 0.99)
        {
            _rampingByAxis[ordinal] = double.NaN;
            return targetVal;
        }

        _rampingByAxis[ordinal] = blend;
        var blendedVal = (int)Math.Round(500.0 + (targetVal - 500.0) * blend);
        return Math.Clamp(blendedVal, 0, 999);
    }

    /// <summary>
    /// Processes return-to-center animation for disabled or fill-mode-None axes.
    /// Exponential smoothing glide to midpoint (500) at factor 0.04/tick.
    /// </summary>
    private void ProcessReturnToCenter(AxisConfig config, int intervalMs)
    {
        var idx = config.Ordinal;
        var currentPos = _returningByAxis[idx];
        if (double.IsNaN(currentPos))
            return;

        var newPos = currentPos + (500.0 - currentPos) * 0.04;
        var newVal = (int)Math.Round(newPos);
        newVal = Math.Clamp(newVal, 0, 999);

        if (Math.Abs(newPos - 500.0) < 1.0)
        {
            _returningByAxis[idx] = double.NaN;
            newVal = 500;
        }
        else
        {
            _returningByAxis[idx] = newPos;
        }

        if (IsDirty(idx, newVal))
        {
            AppendCommand(config, newVal, intervalMs);
            _lastSentByAxis[idx] = newVal;
        }
    }

    /// <summary>
    /// Appends one axis command to the output buffer, inserting a separator when needed.
    /// </summary>
    private void AppendCommand(AxisConfig config, int tcodeValue, int intervalMs)
    {
        if (_commandLength > 0)
            WriteByte((byte)' ');

        FormatTCodeCommandToBuffer(config, tcodeValue, intervalMs);
    }

    /// <summary>
    /// Writes one TCode command directly into the reusable output buffer.
    /// Zero-allocation hot path — all formatting is done via byte manipulation.
    /// </summary>
    private void FormatTCodeCommandToBuffer(AxisConfig config, int tcodeValue, int intervalMs)
    {
        WriteByte((byte)(config.Type == "rotation" ? 'R' : 'L'));
        WriteByte((byte)config.Id[1]);
        WriteInt3(tcodeValue);
        WriteByte((byte)'I');
        WriteInt(intervalMs);
    }

    private void WriteByte(byte value)
    {
        _commandBuffer[_commandLength++] = value;
    }

    private void WriteInt(int value)
    {
        if (value <= 0)
        {
            WriteByte((byte)'0');
            return;
        }

        var start = _commandLength;
        var working = value;
        while (working > 0)
        {
            WriteByte((byte)('0' + (working % 10)));
            working /= 10;
        }

        var end = _commandLength - 1;
        while (start < end)
        {
            (_commandBuffer[start], _commandBuffer[end]) = (_commandBuffer[end], _commandBuffer[start]);
            start++;
            end--;
        }
    }

    private void WriteInt3(int value)
    {
        var clamped = Math.Clamp(value, 0, 999);
        WriteByte((byte)('0' + (clamped / 100)));
        WriteByte((byte)('0' + ((clamped / 10) % 10)));
        WriteByte((byte)('0' + (clamped % 10)));
    }

    /// <summary>
    /// Returns <c>true</c> if any axis has an active fill mode, or if there are
    /// axes animating return-to-center. Used to keep the output thread active.
    /// </summary>
    private bool HasActiveFillModes()
    {
        for (var i = 0; i < _axisCount; i++)
        {
            if (!double.IsNaN(_returningByAxis[i]))
                return true;
        }
        return _hasActiveFillConfigs;
    }

    /// <summary>
    /// Finds an axis configuration by axis ID.
    /// </summary>
    private AxisConfig? FindAxisConfig(string axisId)
    {
        for (var index = 0; index < _axisConfigs.Count; index++)
        {
            var config = _axisConfigs[index];
            if (config.Id == axisId)
                return config;
        }

        return null;
    }

    /// <summary>
    /// Returns the ordinal index for a given axis ID string.
    /// Used by non-hot-path methods that receive axis IDs from external callers.
    /// Returns -1 if the axis is not found.
    /// </summary>
    private int GetOrdinalForId(string axisId)
    {
        for (var i = 0; i < _axisConfigs.Count; i++)
        {
            if (_axisConfigs[i].Id == axisId)
                return _axisConfigs[i].Ordinal;
        }
        return -1;
    }

    // ===== Helpers =====

    /// <summary>
    /// Converts a position (0–100) to a TCode value (0–999) applying the axis min/max range.
    /// </summary>
    /// <param name="config">Axis configuration with Min and Max.</param>
    /// <param name="position">Position value in range 0–100.</param>
    /// <returns>TCode value in range 0–999.</returns>
    internal static int PositionToTCode(AxisConfig config, double position)
    {
        var normalized = position / 100.0;
        var scaled = config.Min + normalized * (config.Max - config.Min);
        var tcodeValue = (int)(scaled / 100.0 * 999);
        return Math.Clamp(tcodeValue, 0, 999);
    }

    /// <summary>
    /// Clamps the fill-mode position to 0–<see cref="PitchFillMaxPosition"/> for pitch (R2) axes.
    /// Non-pitch axes are returned unchanged.
    /// </summary>
    /// <param name="config">Axis configuration.</param>
    /// <param name="position">Position to clamp.</param>
    /// <returns>Clamped position value.</returns>
    internal static double ClampPitchFillPosition(AxisConfig config, double position)
    {
        if (config.IsPitch)
            return Math.Clamp(position, 0, PitchFillMaxPosition);
        return position;
    }

    /// <summary>
    /// Applies per-axis position offset to a TCode value.
    /// L0: offset is -50 to +50 (percentage points), added after min/max scaling, result clamped 0–999.
    /// R0: offset is 0–359 (degrees), rotated via modular wrapping.
    /// R1, R2: offset is -50 to +50 (percentage points), same as L0, clamped 0–999.
    /// </summary>
    /// <param name="config">Axis configuration with PositionOffset.</param>
    /// <param name="tcodeValue">Input TCode value (0–999).</param>
    /// <returns>Offset-adjusted TCode value.</returns>
    internal static int ApplyPositionOffset(AxisConfig config, int tcodeValue)
    {
        if (config.PositionOffset == 0 || !config.HasPositionOffset)
            return tcodeValue;

        if (config.Id is "L0" or "R1" or "R2")
        {
            // L0/R1/R2: offset is percentage points (-50 to +50) added to the scaled position
            var offsetTcode = (int)(config.PositionOffset / 100.0 * 999);
            return Math.Clamp(tcodeValue + offsetTcode, 0, 999);
        }

        if (config.Id == "R0")
        {
            // R0: offset is degrees (0–359), wrapping via modulo
            var offsetTcode = (int)(config.PositionOffset / 360.0 * 999);
            var result = (tcodeValue + offsetTcode) % 1000;
            if (result < 0) result += 1000;
            return result;
        }

        return tcodeValue;
    }

    /// <summary>
    /// Returns <c>true</c> if the axis value changed by ≥1 since last transmission.
    /// </summary>
    /// <param name="ordinal">The axis ordinal to check.</param>
    /// <param name="tcodeValue">The proposed TCode value.</param>
    /// <returns><c>true</c> if the value differs from the last sent value.</returns>
    internal bool IsDirty(int ordinal, int tcodeValue)
    {
        var last = _lastSentByAxis[ordinal];
        if (last < 0)
            return true;
        return Math.Abs(last - tcodeValue) >= 1;
    }

    /// <summary>
    /// Formats a TCode command string: {prefix}{axisNum}{value:D3}I{intervalMs}.
    /// </summary>
    /// <param name="config">Axis configuration for prefix and axis number.</param>
    /// <param name="tcodeValue">Three-digit TCode value (0-999).</param>
    /// <param name="intervalMs">Move interval in milliseconds.</param>
    /// <returns>Formatted TCode command string.</returns>
    internal static string FormatTCodeCommand(AxisConfig config, int tcodeValue, int intervalMs)
    {
        var prefix = config.Type == "rotation" ? "R" : "L";
        var axisNum = config.Id[1];
        return $"{prefix}{axisNum}{tcodeValue:D3}I{intervalMs}";
    }

    /// <summary>
    /// Enqueues a midpoint command (500) for the given axis.
    /// The command is sent by the output thread to avoid cross-thread serial writes.
    /// </summary>
    private void SendMidpoint(string axisId)
    {
        var config = FindAxisConfig(axisId);
        if (config == null || _transport?.IsConnected != true) return;
        var command = FormatTCodeCommand(config, 500, 500) + "\n";
        _lastSentByAxis[config.Ordinal] = -1;
        Volatile.Write(ref _pendingDirectCommand, command);
    }

    /// <summary>
    /// Sends an immediate position command for the given axis using its current offset.
    /// Uses midpoint (50%) as the base position, applies offset, and sends over the transport.
    /// Called when the user adjusts the position offset slider.
    /// </summary>
    /// <param name="axisId">The axis to send the position for.</param>
    public void SendPositionWithOffset(string axisId)
    {
        var config = FindAxisConfig(axisId);
        if (config == null || _transport?.IsConnected != true) return;

        // Use midpoint (50% position) as the base, apply offset
        var baseTcode = PositionToTCode(config, 50.0);
        var offsetTcode = ApplyPositionOffset(config, baseTcode);

        var command = FormatTCodeCommand(config, offsetTcode, 200) + "\n";
        _lastSentByAxis[config.Ordinal] = offsetTcode;
        Volatile.Write(ref _pendingDirectCommand, command);
    }

    /// <summary>
    /// Duration in milliseconds for the homing movement on connect.
    /// All axes glide from their current position to midpoint (500) over this duration.
    /// </summary>
    internal const int HomingDurationMs = 2000;

    /// <summary>
    /// Gradually moves all configured axes to their offset-adjusted midpoint over <see cref="HomingDurationMs"/>.
    /// Sends a single compound TCode command with a long interval so the device
    /// smoothly glides from whatever position it is currently in to center.
    /// Respects per-axis position offsets (e.g. L0 stroke offset, R0 twist offset).
    /// Called immediately after connecting, before normal output begins.
    /// </summary>
    public void HomeAxes()
    {
        if (_transport?.IsConnected != true || _axisConfigs.Count == 0) return;

        var parts = new List<string>();
        foreach (var config in _axisConfigs)
        {
            // Start from the 50% midpoint, then apply the user's position offset
            var homeTcode = ApplyPositionOffset(config, PositionToTCode(config, 50.0));
            parts.Add(FormatTCodeCommand(config, homeTcode, HomingDurationMs));
            _lastSentByAxis[config.Ordinal] = homeTcode;
        }

        Volatile.Write(ref _pendingDirectCommand, string.Join(" ", parts) + "\n");
    }
}
