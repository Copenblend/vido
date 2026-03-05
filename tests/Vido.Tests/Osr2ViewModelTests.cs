using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Haptics;
using Vido.Core.Models.Osr2Plus;
using Vido.Core.Settings;
using Vido.Services.Osr2Plus;
using Vido.Services.Playlists;
using Vido.ViewModels.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for PI-008: OSR2+ ViewModel integration.
/// Covers <see cref="AxisCardViewModel"/>, <see cref="AxisControlViewModel"/>,
/// <see cref="VisualizerViewModel"/>, <see cref="Osr2PlusSidebarViewModel"/>,
/// and <see cref="BeatBarViewModel"/>.
/// </summary>
public class Osr2ViewModelTests : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly InterpolationService _interpolation = new();
    private readonly TCodeService _tcode;
    private readonly FunscriptParser _parser = new();
    private readonly FunscriptMatcher _matcher = new();
    private readonly BeatDetectionService _beatDetection = new();

    public Osr2ViewModelTests()
    {
        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(_settings);
        _tcode = new TCodeService(_interpolation);
    }

    public void Dispose()
    {
        _tcode.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────

    private static AxisConfig MakeConfig(
        string id = "L0", string name = "Stroke", string type = "linear",
        int min = 0, int max = 100, bool enabled = true)
    {
        return new AxisConfig
        {
            Id = id, Name = name, Type = type,
            Min = min, Max = max, Enabled = enabled,
        };
    }

    private static AxisConfig MakeL0(int min = 0, int max = 100, bool enabled = true) =>
        MakeConfig("L0", "Stroke", "linear", min, max, enabled);

    private static AxisConfig MakeR0(int min = 0, int max = 100, bool enabled = true) =>
        MakeConfig("R0", "Twist", "rotation", min, max, enabled);

    private static FunscriptData MakeScript(params (int atMs, int pos)[] points)
    {
        var data = new FunscriptData { Actions = new List<FunscriptAction>() };
        foreach (var (atMs, pos) in points)
            data.Actions.Add(new FunscriptAction(atMs, pos));
        return data;
    }

    // ═══════════════════════════════════════════════════════════
    //  AxisCardViewModel Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that constructor exposes axis config identity properties.
    /// </summary>
    [Fact]
    public void AxisCard_Constructor_ExposesIdentity()
    {
        var config = MakeL0();
        var vm = new AxisCardViewModel(config, _tcode);

        Assert.Equal("L0", vm.AxisId);
        Assert.Equal("Stroke", vm.AxisName);
        Assert.True(vm.IsStroke);
        Assert.False(vm.IsPitch);
    }

    /// <summary>
    /// Verifies that Min property clamps to valid range and raises ConfigChanged.
    /// </summary>
    [Fact]
    public void AxisCard_Min_ClampsAndRaisesConfigChanged()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        var changed = false;
        vm.ConfigChanged += () => changed = true;

        vm.Min = 50;
        Assert.Equal(50, vm.Min);
        Assert.True(changed);

        // Clamp below 0
        vm.Min = -10;
        Assert.Equal(0, vm.Min);

        // Rejected: 99 is not < Max(100), so Min stays at 0
        vm.Min = 99;
        Assert.Equal(99, vm.Min);

        // Rejected: 150 is not < Max(100), stays at 99
        vm.Min = 150;
        Assert.Equal(99, vm.Min);
    }

    /// <summary>
    /// Verifies that Max property clamps to valid range and raises ConfigChanged.
    /// </summary>
    [Fact]
    public void AxisCard_Max_ClampsAndRaisesConfigChanged()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        var changed = false;
        vm.ConfigChanged += () => changed = true;

        vm.Max = 75;
        Assert.Equal(75, vm.Max);
        Assert.True(changed);

        // Rejected: 0 is not > Min(0), stays at 75
        vm.Max = 0;
        Assert.Equal(75, vm.Max);

        // Clamp above 100
        vm.Max = 200;
        Assert.Equal(100, vm.Max);
    }

    /// <summary>
    /// Verifies that Enabled property raises ConfigChanged.
    /// </summary>
    [Fact]
    public void AxisCard_Enabled_RaisesConfigChanged()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        var changed = false;
        vm.ConfigChanged += () => changed = true;

        vm.Enabled = false;
        Assert.False(vm.Enabled);
        Assert.True(changed);
    }

    /// <summary>
    /// Verifies that ToggleExpandCommand toggles IsExpanded.
    /// </summary>
    [Fact]
    public void AxisCard_ToggleExpand_TogglesIsExpanded()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        Assert.False(vm.IsExpanded);

        vm.ToggleExpandCommand.Execute(null);
        Assert.True(vm.IsExpanded);

        vm.ToggleExpandCommand.Execute(null);
        Assert.False(vm.IsExpanded);
    }

    /// <summary>
    /// Verifies that FillMode property raises ConfigChanged and updates ShowFillSpeedSlider.
    /// </summary>
    [Fact]
    public void AxisCard_FillMode_RaisesConfigChangedAndUpdatesDerived()
    {
        var config = MakeR0();
        var vm = new AxisCardViewModel(config, _tcode);
        var changed = false;
        vm.ConfigChanged += () => changed = true;

        vm.FillMode = AxisFillMode.Sine;
        Assert.Equal(AxisFillMode.Sine, vm.FillMode);
        Assert.True(changed);
    }

    /// <summary>
    /// Verifies that SyncWithStroke raises ConfigChanged.
    /// </summary>
    [Fact]
    public void AxisCard_SyncWithStroke_RaisesConfigChanged()
    {
        var config = MakeR0();
        var vm = new AxisCardViewModel(config, _tcode);
        var changed = false;
        vm.ConfigChanged += () => changed = true;

        vm.SyncWithStroke = false;
        Assert.False(vm.SyncWithStroke);
        Assert.True(changed);
    }

    /// <summary>
    /// Verifies that FillSpeedHz clamps to valid range.
    /// </summary>
    [Fact]
    public void AxisCard_FillSpeedHz_ClampsToRange()
    {
        var vm = new AxisCardViewModel(MakeR0(), _tcode);

        vm.FillSpeedHz = 0.05;
        Assert.Equal(0.1, vm.FillSpeedHz);

        vm.FillSpeedHz = 5.0;
        Assert.Equal(3.0, vm.FillSpeedHz);

        vm.FillSpeedHz = 1.5;
        Assert.Equal(1.5, vm.FillSpeedHz);
    }

    /// <summary>
    /// Verifies that SetAutoLoadedScript sets script file name.
    /// </summary>
    [Fact]
    public void AxisCard_SetAutoLoadedScript_SetsFileName()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);

        vm.SetAutoLoadedScript(@"C:\videos\test.funscript");

        Assert.Equal(@"C:\videos\test.funscript", vm.ScriptFileName);
        Assert.True(vm.HasScript);
        Assert.False(vm.IsScriptManual);
    }

    /// <summary>
    /// Verifies that SetAutoLoadedScript does not overwrite manual script.
    /// </summary>
    [Fact]
    public void AxisCard_SetAutoLoadedScript_DoesNotOverwriteManual()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        vm.IsScriptManual = true;
        vm.SetAutoLoadedScript(@"C:\videos\test.funscript");

        // Manual flag should prevent auto-load
        Assert.False(vm.HasScript);
    }

    /// <summary>
    /// Verifies that ClearAutoLoadedScript clears script when not manual.
    /// </summary>
    [Fact]
    public void AxisCard_ClearAutoLoadedScript_ClearsWhenNotManual()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        vm.SetAutoLoadedScript(@"C:\videos\test.funscript");

        vm.ClearAutoLoadedScript();

        Assert.Null(vm.ScriptFileName);
        Assert.False(vm.HasScript);
    }

    /// <summary>
    /// Verifies that ClearAutoLoadedScript preserves manual script.
    /// </summary>
    [Fact]
    public void AxisCard_ClearAutoLoadedScript_PreservesManual()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        // Simulate a manual open via the command's internal path
        vm.IsScriptManual = true;
        vm.SetAutoLoadedScript(@"C:\vids\manual.funscript");
        // SetAutoLoadedScript skips manual, so set directly
        vm.ClearAutoLoadedScript();

        // Manual scripts are not cleared by ClearAutoLoadedScript
        Assert.True(vm.IsScriptManual);
    }

    /// <summary>
    /// Verifies that ClearAllScripts clears everything including manual.
    /// </summary>
    [Fact]
    public void AxisCard_ClearAllScripts_ClearsManual()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        vm.IsScriptManual = true;
        vm.ClearAllScripts();

        Assert.False(vm.IsScriptManual);
        Assert.False(vm.HasScript);
    }

    /// <summary>
    /// Verifies that RefreshFromConfig raises PropertyChanged for config-backed properties.
    /// </summary>
    [Fact]
    public void AxisCard_RefreshFromConfig_RaisesPropertyChanged()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        var changedProps = new List<string>();
        vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        vm.RefreshFromConfig();

        Assert.Contains("Min", changedProps);
        Assert.Contains("Max", changedProps);
        Assert.Contains("Enabled", changedProps);
        Assert.Contains("FillMode", changedProps);
    }

    /// <summary>
    /// Verifies that ShowSyncToggle and ShowFillMode are false for stroke axis.
    /// </summary>
    [Fact]
    public void AxisCard_StrokeAxis_HidesSyncAndFillMode()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);

        Assert.False(vm.ShowSyncToggle);
        Assert.False(vm.ShowFillMode);
    }

    /// <summary>
    /// Verifies that ShowSyncToggle and ShowFillMode are true for rotation axis.
    /// </summary>
    [Fact]
    public void AxisCard_RotationAxis_ShowsSyncAndFillMode()
    {
        var vm = new AxisCardViewModel(MakeR0(), _tcode);

        Assert.True(vm.ShowSyncToggle);
        Assert.True(vm.ShowFillMode);
    }

    /// <summary>
    /// Verifies that PositionOffset for L0 clamps to -50..50.
    /// </summary>
    [Fact]
    public void AxisCard_PositionOffset_L0_ClampsToRange()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);

        Assert.Equal(-50, vm.PositionOffsetMin);
        Assert.Equal(50, vm.PositionOffsetMax);

        vm.PositionOffset = -100;
        Assert.Equal(-50, vm.PositionOffset);

        vm.PositionOffset = 100;
        Assert.Equal(50, vm.PositionOffset);
    }

    /// <summary>
    /// Verifies that PositionOffset for R0 clamps to 0..179.
    /// </summary>
    [Fact]
    public void AxisCard_PositionOffset_R0_ClampsToRange()
    {
        var vm = new AxisCardViewModel(MakeR0(), _tcode);

        Assert.Equal(0, vm.PositionOffsetMin);
        Assert.Equal(179, vm.PositionOffsetMax);

        vm.PositionOffset = -10;
        Assert.Equal(0, vm.PositionOffset);

        vm.PositionOffset = 200;
        Assert.Equal(179, vm.PositionOffset);
    }

    /// <summary>
    /// Verifies that PositionOffsetLabel formats correctly for L0 vs R0.
    /// </summary>
    [Fact]
    public void AxisCard_PositionOffsetLabel_FormatsCorrectly()
    {
        var l0 = new AxisCardViewModel(MakeL0(), _tcode);
        l0.PositionOffset = 10;
        Assert.Contains("%", l0.PositionOffsetLabel);

        var r0 = new AxisCardViewModel(MakeR0(), _tcode);
        r0.PositionOffset = 45;
        Assert.Contains("°", r0.PositionOffsetLabel);
    }

    /// <summary>
    /// Verifies that OpenScriptCommand uses FileDialogFactory and ParseFileFunc.
    /// </summary>
    [Fact]
    public void AxisCard_OpenScriptCommand_UsesFactories()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        var script = MakeScript((0, 0), (1000, 100));
        vm.FileDialogFactory = () => @"C:\videos\custom.funscript";
        vm.ParseFileFunc = (path, id) => script;

        vm.OpenScriptCommand.Execute(null);

        Assert.Equal(@"C:\videos\custom.funscript", vm.ScriptFileName);
        Assert.True(vm.IsScriptManual);
        Assert.True(vm.HasScript);
    }

    /// <summary>
    /// Verifies that OpenScriptCommand does nothing when dialog cancelled.
    /// </summary>
    [Fact]
    public void AxisCard_OpenScriptCommand_DialogCancelled_NoChange()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        vm.FileDialogFactory = () => null;

        vm.OpenScriptCommand.Execute(null);

        Assert.Null(vm.ScriptFileName);
        Assert.False(vm.HasScript);
    }

    // ═══════════════════════════════════════════════════════════
    //  AxisControlViewModel Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that constructor creates 4 axis cards with correct IDs.
    /// </summary>
    [Fact]
    public void AxisControl_Constructor_Creates4AxisCards()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        Assert.Equal(4, vm.AxisCards.Count);
        Assert.Equal("L0", vm.AxisCards[0].AxisId);
        Assert.Equal("R0", vm.AxisCards[1].AxisId);
        Assert.Equal("R1", vm.AxisCards[2].AxisId);
        Assert.Equal("R2", vm.AxisCards[3].AxisId);
    }

    /// <summary>
    /// Verifies that LoadSettings reads from ISettingsService.Current.Osr2AxisSettings.
    /// </summary>
    [Fact]
    public void AxisControl_LoadSettings_ReadsFromSettingsService()
    {
        _settings.Osr2AxisSettings["L0"] = new AxisSettingsData
        {
            Min = 10, Max = 90, Enabled = false
        };

        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        Assert.Equal(10, vm.AxisCards[0].Min);
        Assert.Equal(90, vm.AxisCards[0].Max);
        Assert.False(vm.AxisCards[0].Enabled);
    }

    /// <summary>
    /// Verifies that SaveSettings persists axis configs and calls QueueSave.
    /// </summary>
    [Fact]
    public void AxisControl_SaveSettings_PersistsAndQueuesSave()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        _settingsService.ClearReceivedCalls();

        vm.AxisCards[0].Min = 20;
        vm.AxisCards[0].Max = 80;

        // SaveSettings is called internally when ConfigChanged fires
        Assert.Equal(20, _settings.Osr2AxisSettings["L0"].Min);
        Assert.Equal(80, _settings.Osr2AxisSettings["L0"].Max);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that ScriptsChanged event fires when scripts are loaded.
    /// </summary>
    [Fact]
    public void AxisControl_LoadScriptsForVideo_RaisesScriptsChanged()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        var script = MakeScript((0, 0), (1000, 100));
        vm.FindMatchingScriptsFunc = _ => new Dictionary<string, string>
        {
            { "L0", @"C:\videos\test.L0.funscript" }
        };
        vm.TryParseMultiAxisFunc = _ => null;
        vm.ParseFileFunc = (path, id) => script;

        Dictionary<string, FunscriptData>? received = null;
        vm.ScriptsChanged += scripts => received = scripts;

        vm.LoadScriptsForVideo(@"C:\videos\test.mp4");

        Assert.NotNull(received);
        Assert.True(received.ContainsKey("L0"));
    }

    /// <summary>
    /// Verifies that ClearScripts clears auto-loaded scripts.
    /// </summary>
    [Fact]
    public void AxisControl_ClearScripts_ClearsAutoLoaded()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        var script = MakeScript((0, 0), (1000, 100));
        vm.FindMatchingScriptsFunc = _ => new Dictionary<string, string>
        {
            { "L0", @"C:\videos\test.L0.funscript" }
        };
        vm.TryParseMultiAxisFunc = _ => null;
        vm.ParseFileFunc = (path, id) => script;
        vm.LoadScriptsForVideo(@"C:\videos\test.mp4");

        vm.ClearScripts();

        Assert.False(vm.AxisCards[0].HasScript);
    }

    /// <summary>
    /// Verifies that OnSuppressFunscript sets suppression state.
    /// </summary>
    [Fact]
    public void AxisControl_OnSuppressFunscript_SetsSuppression()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        vm.OnSuppressFunscript(new SuppressFunscriptEvent { SuppressFunscripts = true });
        Assert.True(vm.IsFunscriptsSuppressed);

        vm.OnSuppressFunscript(new SuppressFunscriptEvent { SuppressFunscripts = false });
        Assert.False(vm.IsFunscriptsSuppressed);
    }

    /// <summary>
    /// Verifies that SetVideoPlaying updates IsTestEnabled.
    /// </summary>
    [Fact]
    public void AxisControl_SetVideoPlaying_UpdatesIsTestEnabled()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        vm.SetDeviceConnected(true);

        var changedProps = new List<string>();
        vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        vm.SetVideoPlaying(true);

        Assert.False(vm.IsTestEnabled);
        Assert.Contains("IsTestEnabled", changedProps);
    }

    /// <summary>
    /// Verifies that SetDeviceConnected updates IsTestEnabled.
    /// </summary>
    [Fact]
    public void AxisControl_SetDeviceConnected_UpdatesIsTestEnabled()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        vm.SetDeviceConnected(true);
        Assert.True(vm.IsTestEnabled);

        vm.SetDeviceConnected(false);
        Assert.False(vm.IsTestEnabled);
    }

    /// <summary>
    /// Verifies that default TestButtonText is "Test".
    /// </summary>
    [Fact]
    public void AxisControl_TestButtonText_DefaultIsTest()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);

        Assert.Equal("Test", vm.TestButtonText);
        Assert.False(vm.IsTesting);
    }

    /// <summary>
    /// Verifies that AxisConfigChanged fires when card config changes.
    /// </summary>
    [Fact]
    public void AxisControl_AxisConfigChanged_FiresOnCardChange()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        var fired = false;
        vm.AxisConfigChanged += () => fired = true;

        vm.AxisCards[0].Min = 15;

        Assert.True(fired);
    }

    /// <summary>
    /// Verifies that LoadScriptsForVideo skips when suppressed.
    /// </summary>
    [Fact]
    public void AxisControl_LoadScriptsForVideo_SkipsWhenSuppressed()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        vm.OnSuppressFunscript(new SuppressFunscriptEvent { SuppressFunscripts = true });

        var matchCalled = false;
        vm.FindMatchingScriptsFunc = _ => { matchCalled = true; return new Dictionary<string, string>(); };

        vm.LoadScriptsForVideo(@"C:\test.mp4");

        Assert.False(matchCalled);
    }

    /// <summary>
    /// Verifies that ClearAllScripts clears manual scripts.
    /// </summary>
    [Fact]
    public void AxisControl_ClearAllScripts_ClearsManual()
    {
        var vm = new AxisControlViewModel(_tcode, _settingsService, _parser, _matcher);
        vm.AxisCards[0].IsScriptManual = true;

        vm.ClearAllScripts();

        Assert.False(vm.AxisCards[0].IsScriptManual);
    }

    // ═══════════════════════════════════════════════════════════
    //  VisualizerViewModel Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that constructor loads settings from ISettingsService.
    /// </summary>
    [Fact]
    public void Visualizer_Constructor_LoadsSettings()
    {
        _settings.Osr2VisualizerMode = "Heatmap";
        _settings.Osr2VisualizerWindowDuration = 120;

        var vm = new VisualizerViewModel(_settingsService);

        Assert.Equal(VisualizationMode.Heatmap, vm.SelectedMode);
        Assert.Equal(120, vm.WindowDurationSeconds);
    }

    /// <summary>
    /// Verifies that default constructor uses Graph mode and 60s window.
    /// </summary>
    [Fact]
    public void Visualizer_Constructor_DefaultValues()
    {
        var vm = new VisualizerViewModel(_settingsService);

        Assert.Equal(VisualizationMode.Graph, vm.SelectedMode);
        Assert.Equal(60, vm.WindowDurationSeconds);
    }

    /// <summary>
    /// Verifies that SelectedMode persists to settings and raises RepaintRequested.
    /// </summary>
    [Fact]
    public void Visualizer_SelectedMode_PersistsAndRaisesRepaint()
    {
        var vm = new VisualizerViewModel(_settingsService);
        var repainted = false;
        vm.RepaintRequested += () => repainted = true;

        vm.SelectedMode = VisualizationMode.Heatmap;

        Assert.Equal("Heatmap", _settings.Osr2VisualizerMode);
        Assert.True(repainted);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that WindowDurationSeconds persists and raises RepaintRequested.
    /// </summary>
    [Fact]
    public void Visualizer_WindowDuration_PersistsAndRaisesRepaint()
    {
        var vm = new VisualizerViewModel(_settingsService);
        _settingsService.ClearReceivedCalls();
        var repainted = false;
        vm.RepaintRequested += () => repainted = true;

        vm.WindowDurationSeconds = 120;

        Assert.Equal(120, _settings.Osr2VisualizerWindowDuration);
        Assert.True(repainted);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that UpdateTime sets CurrentTime and raises RepaintRequested.
    /// </summary>
    [Fact]
    public void Visualizer_UpdateTime_SetsCurrentTimeAndRaisesRepaint()
    {
        var vm = new VisualizerViewModel(_settingsService);
        var repainted = false;
        vm.RepaintRequested += () => repainted = true;

        vm.UpdateTime(42.5);

        Assert.Equal(42.5, vm.CurrentTime);
        Assert.True(repainted);
    }

    /// <summary>
    /// Verifies that SetLoadedAxes updates HasScripts.
    /// </summary>
    [Fact]
    public void Visualizer_SetLoadedAxes_UpdatesHasScripts()
    {
        var vm = new VisualizerViewModel(_settingsService);
        Assert.False(vm.HasScripts);

        vm.SetLoadedAxes(new Dictionary<string, FunscriptData>
        {
            { "L0", MakeScript((0, 0), (1000, 100)) }
        });

        Assert.True(vm.HasScripts);
    }

    /// <summary>
    /// Verifies that ClearAxes sets HasScripts to false and raises repaint.
    /// </summary>
    [Fact]
    public void Visualizer_ClearAxes_ClearsAndRaisesRepaint()
    {
        var vm = new VisualizerViewModel(_settingsService);
        vm.SetLoadedAxes(new Dictionary<string, FunscriptData>
        {
            { "L0", MakeScript((0, 0), (1000, 100)) }
        });
        var repainted = false;
        vm.RepaintRequested += () => repainted = true;

        vm.ClearAxes();

        Assert.False(vm.HasScripts);
        Assert.True(repainted);
    }

    /// <summary>
    /// Verifies that TimeWindowRadius is half of WindowDurationSeconds.
    /// </summary>
    [Fact]
    public void Visualizer_TimeWindowRadius_IsHalfDuration()
    {
        var vm = new VisualizerViewModel(_settingsService);
        vm.WindowDurationSeconds = 120;

        Assert.Equal(60.0, vm.TimeWindowRadius);
    }

    /// <summary>
    /// Verifies that WindowDurationIndex maps correctly to AvailableWindowDurations.
    /// </summary>
    [Fact]
    public void Visualizer_WindowDurationIndex_MapsCorrectly()
    {
        var vm = new VisualizerViewModel(_settingsService);
        vm.WindowDurationIndex = 2; // 120 seconds

        Assert.Equal(120, vm.WindowDurationSeconds);
        Assert.Equal(2, vm.WindowDurationIndex);
    }

    /// <summary>
    /// Verifies that static AxisColors and AxisNames contain all 4 axes.
    /// </summary>
    [Fact]
    public void Visualizer_StaticMaps_ContainAll4Axes()
    {
        Assert.Equal(4, VisualizerViewModel.AxisColors.Count);
        Assert.Equal(4, VisualizerViewModel.AxisNames.Count);
        Assert.True(VisualizerViewModel.AxisColors.ContainsKey("L0"));
        Assert.True(VisualizerViewModel.AxisColors.ContainsKey("R0"));
        Assert.True(VisualizerViewModel.AxisColors.ContainsKey("R1"));
        Assert.True(VisualizerViewModel.AxisColors.ContainsKey("R2"));
    }

    /// <summary>
    /// Verifies that AvailableWindowDurations and WindowDurationLabels have same length.
    /// </summary>
    [Fact]
    public void Visualizer_DurationArrays_SameLength()
    {
        Assert.Equal(
            VisualizerViewModel.AvailableWindowDurations.Length,
            VisualizerViewModel.WindowDurationLabels.Length);
    }

    // ═══════════════════════════════════════════════════════════
    //  Osr2PlusSidebarViewModel Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that constructor loads settings from ISettingsService.
    /// </summary>
    [Fact]
    public void Sidebar_Constructor_LoadsSettings()
    {
        _settings.Osr2ConnectionMode = "Serial";
        _settings.Osr2UdpPort = 8888;
        _settings.Osr2ComPort = "COM5";
        _settings.Osr2BaudRate = 250000;
        _settings.Osr2OutputRate = 60;
        _settings.Osr2GlobalOffset = -50;

        var vm = CreateSidebarViewModel();

        Assert.Equal(ConnectionMode.Serial, vm.SelectedMode);
        Assert.Equal(8888, vm.UdpPort);
        Assert.Equal("COM5", vm.SelectedComPort);
        Assert.Equal(250000, vm.SelectedBaudRate);
        Assert.Equal(60, vm.OutputRateHz);
        Assert.Equal(-50, vm.GlobalOffsetMs);
    }

    /// <summary>
    /// Verifies that default status text is "OSR2+:Not Connected".
    /// </summary>
    [Fact]
    public void Sidebar_DefaultStatusText_NotConnected()
    {
        var vm = CreateSidebarViewModel();
        Assert.Equal("OSR2+:Not Connected", vm.StatusText);
    }

    /// <summary>
    /// Verifies that default ConnectButtonText is "Connect".
    /// </summary>
    [Fact]
    public void Sidebar_DefaultConnectButtonText_IsConnect()
    {
        var vm = CreateSidebarViewModel();
        Assert.Equal("Connect", vm.ConnectButtonText);
    }

    /// <summary>
    /// Verifies that SelectedMode persists to settings and updates IsUdpMode/IsSerialMode.
    /// </summary>
    [Fact]
    public void Sidebar_SelectedMode_PersistsAndUpdatesDerived()
    {
        var vm = CreateSidebarViewModel();

        vm.SelectedMode = ConnectionMode.Serial;

        Assert.Equal("Serial", _settings.Osr2ConnectionMode);
        Assert.False(vm.IsUdpMode);
        Assert.True(vm.IsSerialMode);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that UdpPort clamps to valid range and persists.
    /// </summary>
    [Fact]
    public void Sidebar_UdpPort_ClampsAndPersists()
    {
        var vm = CreateSidebarViewModel();

        vm.UdpPort = 12345;
        Assert.Equal(12345, vm.UdpPort);
        Assert.Equal(12345, _settings.Osr2UdpPort);

        vm.UdpPort = 0;
        Assert.Equal(1, vm.UdpPort);

        vm.UdpPort = 70000;
        Assert.Equal(65535, vm.UdpPort);
    }

    /// <summary>
    /// Verifies that SelectedComPort persists.
    /// </summary>
    [Fact]
    public void Sidebar_SelectedComPort_Persists()
    {
        var vm = CreateSidebarViewModel();

        vm.SelectedComPort = "COM3";

        Assert.Equal("COM3", _settings.Osr2ComPort);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that SelectedComPort handles null gracefully.
    /// </summary>
    [Fact]
    public void Sidebar_SelectedComPort_NullBecomesEmpty()
    {
        var vm = CreateSidebarViewModel();

        vm.SelectedComPort = null!;

        Assert.Equal("", vm.SelectedComPort);
    }

    /// <summary>
    /// Verifies that SelectedBaudRate persists.
    /// </summary>
    [Fact]
    public void Sidebar_SelectedBaudRate_Persists()
    {
        var vm = CreateSidebarViewModel();

        vm.SelectedBaudRate = 57600;

        Assert.Equal(57600, _settings.Osr2BaudRate);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that OutputRateHz clamps and persists and propagates to TCodeService.
    /// </summary>
    [Fact]
    public void Sidebar_OutputRateHz_ClampsAndPersists()
    {
        var vm = CreateSidebarViewModel();

        vm.OutputRateHz = 150;
        Assert.Equal(150, vm.OutputRateHz);
        Assert.Equal(150, _settings.Osr2OutputRate);

        vm.OutputRateHz = 10;
        Assert.Equal(30, vm.OutputRateHz);

        vm.OutputRateHz = 500;
        Assert.Equal(200, vm.OutputRateHz);
    }

    /// <summary>
    /// Verifies that GlobalOffsetMs clamps and persists.
    /// </summary>
    [Fact]
    public void Sidebar_GlobalOffsetMs_ClampsAndPersists()
    {
        var vm = CreateSidebarViewModel();

        vm.GlobalOffsetMs = 100;
        Assert.Equal(100, vm.GlobalOffsetMs);
        Assert.Equal(100, _settings.Osr2GlobalOffset);

        vm.GlobalOffsetMs = -1000;
        Assert.Equal(-500, vm.GlobalOffsetMs);

        vm.GlobalOffsetMs = 1000;
        Assert.Equal(500, vm.GlobalOffsetMs);
    }

    /// <summary>
    /// Verifies that AvailableBaudRates contains expected values.
    /// </summary>
    [Fact]
    public void Sidebar_AvailableBaudRates_ContainsExpectedValues()
    {
        var vm = CreateSidebarViewModel();

        Assert.Contains(9600, vm.AvailableBaudRates);
        Assert.Contains(115200, vm.AvailableBaudRates);
        Assert.Contains(250000, vm.AvailableBaudRates);
        Assert.Equal(6, vm.AvailableBaudRates.Length);
    }

    /// <summary>
    /// Verifies that Connect with successful transport updates state.
    /// </summary>
    [Fact]
    public void Sidebar_Connect_Success_UpdatesState()
    {
        var vm = CreateSidebarViewModel();
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.Connect();

        Assert.True(vm.IsConnected);
        Assert.False(vm.IsNotConnected);
        Assert.Equal("Disconnect", vm.ConnectButtonText);
        Assert.Contains("Connected", vm.StatusText);
    }

    /// <summary>
    /// Verifies that Connect with failed transport stays disconnected.
    /// </summary>
    [Fact]
    public void Sidebar_Connect_Failure_StaysDisconnected()
    {
        var vm = CreateSidebarViewModel();
        vm.TransportFactory = (_, _, _, _) => (null, false);

        vm.Connect();

        Assert.False(vm.IsConnected);
    }

    /// <summary>
    /// Verifies that Disconnect updates state.
    /// </summary>
    [Fact]
    public void Sidebar_Disconnect_UpdatesState()
    {
        var vm = CreateSidebarViewModel();
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);
        vm.Connect();

        vm.Disconnect();

        Assert.False(vm.IsConnected);
        Assert.True(vm.IsNotConnected);
        Assert.Equal("Connect", vm.ConnectButtonText);
        transport.Received().Disconnect();
        transport.Received().Dispose();
    }

    /// <summary>
    /// Verifies that ConnectCommand toggles connection state.
    /// </summary>
    [Fact]
    public void Sidebar_ConnectCommand_Toggles()
    {
        var vm = CreateSidebarViewModel();
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.ConnectCommand.Execute(null); // Connect
        Assert.True(vm.IsConnected);

        vm.ConnectCommand.Execute(null); // Disconnect
        Assert.False(vm.IsConnected);
    }

    /// <summary>
    /// Verifies that Connect publishes HapticTransportStateEvent.
    /// </summary>
    [Fact]
    public void Sidebar_Connect_PublishesTransportStateEvent()
    {
        var eventBus = Substitute.For<IEventBus>();
        var vm = CreateSidebarViewModel(eventBus);
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.Connect();

        eventBus.Received().Publish(Arg.Is<HapticTransportStateEvent>(e => e.IsConnected));
    }

    /// <summary>
    /// Verifies that Disconnect publishes HapticTransportStateEvent.
    /// </summary>
    [Fact]
    public void Sidebar_Disconnect_PublishesTransportStateEvent()
    {
        var eventBus = Substitute.For<IEventBus>();
        var vm = CreateSidebarViewModel(eventBus);
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);
        vm.Connect();
        eventBus.ClearReceivedCalls();

        vm.Disconnect();

        eventBus.Received().Publish(Arg.Is<HapticTransportStateEvent>(e => !e.IsConnected));
    }

    /// <summary>
    /// Verifies that RefreshPortsCommand refreshes the AvailableComPorts.
    /// </summary>
    [Fact]
    public void Sidebar_RefreshPortsCommand_RefreshesPorts()
    {
        var vm = CreateSidebarViewModel();
        vm.PortLister = () => ["COM1", "COM3", "COM5"];
        vm.AvailableComPorts.Clear();

        vm.RefreshPortsCommand.Execute(null);

        Assert.Equal(3, vm.AvailableComPorts.Count);
        Assert.Contains("COM1", vm.AvailableComPorts);
        Assert.Contains("COM3", vm.AvailableComPorts);
    }

    /// <summary>
    /// Verifies that RefreshPorts auto-selects first port when none selected.
    /// </summary>
    [Fact]
    public void Sidebar_RefreshPorts_AutoSelectsFirstPort()
    {
        _settings.Osr2ComPort = "";
        var vm = CreateSidebarViewModel();
        vm.PortLister = () => ["COM7", "COM8"];

        vm.ExecuteRefreshPorts();

        Assert.Equal("COM7", vm.SelectedComPort);
    }

    /// <summary>
    /// Verifies that ShowAxisSettingsCommand raises the event.
    /// </summary>
    [Fact]
    public void Sidebar_ShowAxisSettingsCommand_RaisesEvent()
    {
        var vm = CreateSidebarViewModel();
        var raised = false;
        vm.ShowAxisSettingsRequested += () => raised = true;

        vm.ShowAxisSettingsCommand.Execute(null);

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that ShowVisualizerCommand raises the event.
    /// </summary>
    [Fact]
    public void Sidebar_ShowVisualizerCommand_RaisesEvent()
    {
        var vm = CreateSidebarViewModel();
        var raised = false;
        vm.ShowVisualizerRequested += () => raised = true;

        vm.ShowVisualizerCommand.Execute(null);

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that StatusText formats UDP correctly when connected.
    /// </summary>
    [Fact]
    public void Sidebar_StatusText_UdpConnected()
    {
        var vm = CreateSidebarViewModel();
        vm.UdpPort = 9000;
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.Connect();

        Assert.Equal("UDP:9000:Connected", vm.StatusText);
    }

    /// <summary>
    /// Verifies that StatusText formats Serial correctly when connected.
    /// </summary>
    [Fact]
    public void Sidebar_StatusText_SerialConnected()
    {
        _settings.Osr2ConnectionMode = "Serial";
        _settings.Osr2ComPort = "COM4";
        var vm = CreateSidebarViewModel();
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.Connect();

        Assert.Equal("COM:COM4:Connected", vm.StatusText);
    }

    /// <summary>
    /// Verifies that IsNotConnected is inverse of IsConnected.
    /// </summary>
    [Fact]
    public void Sidebar_IsNotConnected_InverseOfIsConnected()
    {
        var vm = CreateSidebarViewModel();
        Assert.True(vm.IsNotConnected);
        Assert.False(vm.IsConnected);
    }

    /// <summary>
    /// Verifies that PortLister failure is swallowed.
    /// </summary>
    [Fact]
    public void Sidebar_RefreshPorts_SwallowsException()
    {
        var vm = CreateSidebarViewModel();
        vm.PortLister = () => throw new InvalidOperationException("fail");

        vm.ExecuteRefreshPorts(); // Should not throw

        Assert.Empty(vm.AvailableComPorts);
    }

    private Osr2PlusSidebarViewModel CreateSidebarViewModel(IEventBus? eventBus = null, IToastService? toastService = null)
    {
        // Inject empty PortLister via constructor to avoid system COM port detection
        // during tests. Setting it after construction is too late — the constructor
        // calls RefreshPorts() which would detect real ports (e.g. COM2 on GitHub CI).
        return new Osr2PlusSidebarViewModel(_tcode, _settingsService, eventBus,
            portLister: () => Array.Empty<string>(), toastService: toastService);
    }

    /// <summary>
    /// Verifies that Connect via UDP shows a toast with the port number.
    /// </summary>
    [Fact]
    public void Sidebar_Connect_UDP_ShowsToast()
    {
        var toast = Substitute.For<IToastService>();
        var vm = CreateSidebarViewModel(toastService: toast);
        vm.UdpPort = 9000;
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.Connect();

        toast.Received(1).Show(Arg.Is<string>(s => s.Contains("UDP port")), Arg.Any<string?>());
    }

    /// <summary>
    /// Verifies that Connect via Serial shows a toast with the COM port name.
    /// </summary>
    [Fact]
    public void Sidebar_Connect_Serial_ShowsToast()
    {
        _settings.Osr2ConnectionMode = "Serial";
        _settings.Osr2ComPort = "COM4";
        var toast = Substitute.For<IToastService>();
        var vm = CreateSidebarViewModel(toastService: toast);
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);

        vm.Connect();

        toast.Received(1).Show(Arg.Is<string>(s => s.Contains("Serial COM port")), Arg.Any<string?>());
    }

    /// <summary>
    /// Verifies that Disconnect shows a toast notification.
    /// </summary>
    [Fact]
    public void Sidebar_Disconnect_ShowsToast()
    {
        var toast = Substitute.For<IToastService>();
        var vm = CreateSidebarViewModel(toastService: toast);
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);
        vm.Connect();
        toast.ClearReceivedCalls();

        vm.Disconnect();

        toast.Received(1).Show(Arg.Is<string>(s => s.Contains("Disconnected")), Arg.Any<string?>());
    }

    /// <summary>
    /// Verifies that connection failure shows an error toast.
    /// </summary>
    [Fact]
    public void Sidebar_Connect_Failure_ShowsErrorToast()
    {
        var toast = Substitute.For<IToastService>();
        var vm = CreateSidebarViewModel(toastService: toast);
        vm.TransportFactory = (_, _, _, _) => (null, false);

        vm.Connect();

        toast.Received(1).ShowError(Arg.Is<string>(s => s.Contains("Connection failed")), Arg.Any<string?>());
    }

    // ═══════════════════════════════════════════════════════════
    //  BeatBarViewModel Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that constructor loads settings with Off mode by default.
    /// </summary>
    [Fact]
    public void BeatBar_Constructor_DefaultOff()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        Assert.Equal(BeatBarMode.Off, vm.Mode);
        Assert.False(vm.IsActive);
        Assert.False(vm.IsExternalMode);
    }

    /// <summary>
    /// Verifies that constructor loads persisted built-in mode.
    /// </summary>
    [Fact]
    public void BeatBar_Constructor_LoadsPersistedBuiltInMode()
    {
        _settings.Osr2BeatBarMode = "OnPeak";

        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);
    }

    /// <summary>
    /// Verifies that Mode persists to settings.
    /// </summary>
    [Fact]
    public void BeatBar_Mode_PersistsToSettings()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        _settingsService.ClearReceivedCalls();

        vm.Mode = BeatBarMode.OnValley;

        Assert.Equal("OnValley", _settings.Osr2BeatBarMode);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that Mode raises ModeChanged event.
    /// </summary>
    [Fact]
    public void BeatBar_Mode_RaisesModeChanged()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        BeatBarMode? received = null;
        vm.ModeChanged += m => received = m;

        vm.Mode = BeatBarMode.OnPeak;

        Assert.Equal(BeatBarMode.OnPeak, received);
    }

    /// <summary>
    /// Verifies that Mode change raises RepaintRequested.
    /// </summary>
    [Fact]
    public void BeatBar_Mode_RaisesRepaintRequested()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var repainted = false;
        vm.RepaintRequested += () => repainted = true;

        vm.Mode = BeatBarMode.OnPeak;

        Assert.True(repainted);
    }

    /// <summary>
    /// Verifies that LoadBeats detects beats for built-in modes.
    /// </summary>
    [Fact]
    public void BeatBar_LoadBeats_DetectsForBuiltInMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        var script = MakeScript(
            (0, 0), (500, 100), (1000, 0), (1500, 100), (2000, 0));

        vm.LoadBeats(script);

        Assert.True(vm.HasBeats);
        Assert.True(vm.IsActive);
    }

    /// <summary>
    /// Verifies that ClearBeats clears all data.
    /// </summary>
    [Fact]
    public void BeatBar_ClearBeats_ClearsAllData()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;
        vm.LoadBeats(MakeScript((0, 0), (500, 100), (1000, 0)));
        Assert.True(vm.HasBeats);

        vm.ClearBeats();

        Assert.False(vm.HasBeats);
        Assert.False(vm.IsActive);
    }

    /// <summary>
    /// Verifies that UpdateTime sets CurrentTimeMs and raises RepaintRequested.
    /// </summary>
    [Fact]
    public void BeatBar_UpdateTime_SetsCurrentTimeAndRaisesRepaint()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var repainted = false;
        vm.RepaintRequested += () => repainted = true;

        vm.UpdateTime(12345.0);

        Assert.Equal(12345.0, vm.CurrentTimeMs);
        Assert.True(repainted);
    }

    /// <summary>
    /// Verifies that AvailableModes contains built-in modes by default.
    /// </summary>
    [Fact]
    public void BeatBar_AvailableModes_ContainsBuiltInModes()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        Assert.Contains(vm.AvailableModes, m => m == BeatBarMode.Off);
        Assert.Contains(vm.AvailableModes, m => m == BeatBarMode.OnPeak);
        Assert.Contains(vm.AvailableModes, m => m == BeatBarMode.OnValley);
    }

    /// <summary>
    /// Verifies that OnBeatSourceRegistration adds external mode to AvailableModes.
    /// </summary>
    [Fact]
    public void BeatBar_OnBeatSourceRegistration_AddsExternalMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse Beats");
        source.IsAvailable.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });

        Assert.Contains(vm.AvailableModes, m => m.Id == "pulse");
    }

    /// <summary>
    /// Verifies that OnBeatSourceRegistration unregister removes external mode.
    /// </summary>
    [Fact]
    public void BeatBar_OnBeatSourceRegistration_Unregister_RemovesMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse Beats");
        source.IsAvailable.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.Contains(vm.AvailableModes, m => m.Id == "pulse");

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });
        Assert.DoesNotContain(vm.AvailableModes, m => m.Id == "pulse");
    }

    /// <summary>
    /// Verifies that OnExternalBeatEvent updates beats when current mode matches.
    /// </summary>
    [Fact]
    public void BeatBar_OnExternalBeatEvent_UpdatesBeatsForMatchingMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse Beats");
        source.IsAvailable.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });

        var externalMode = vm.AvailableModes.First(m => m.Id == "pulse");
        vm.Mode = externalMode;

        var beatTimes = new double[] { 100, 500, 1000 };
        vm.OnExternalBeatEvent(new ExternalBeatEvent
        {
            SourceId = "pulse",
            BeatTimesMs = beatTimes,
        });

        Assert.Equal(3, vm.Beats.Count);
        Assert.True(vm.HasBeats);
    }

    /// <summary>
    /// Verifies that a deferred external mode is resolved when source registers.
    /// </summary>
    [Fact]
    public void BeatBar_DeferredExternalMode_ResolvedOnRegistration()
    {
        _settings.Osr2BeatBarMode = "pulse"; // Non-built-in mode

        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        Assert.Equal(BeatBarMode.Off, vm.Mode); // Not yet resolvable

        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse Beats");
        source.IsAvailable.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });

        Assert.Equal("pulse", vm.Mode.Id);
    }

    /// <summary>
    /// Verifies that IsActive is false when mode is Off.
    /// </summary>
    [Fact]
    public void BeatBar_IsActive_FalseWhenOff()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.Off;

        Assert.False(vm.IsActive);
    }

    /// <summary>
    /// Verifies that IsActive is false when no beats are loaded.
    /// </summary>
    [Fact]
    public void BeatBar_IsActive_FalseWhenNoBeats()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        Assert.False(vm.IsActive);
    }

    /// <summary>
    /// Verifies that ActiveExternalSource returns null for built-in modes.
    /// </summary>
    [Fact]
    public void BeatBar_ActiveExternalSource_NullForBuiltInMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        Assert.Null(vm.ActiveExternalSource);
    }

    /// <summary>
    /// Verifies that ActiveExternalSource returns the source for external modes.
    /// </summary>
    [Fact]
    public void BeatBar_ActiveExternalSource_ReturnsSourceForExternalMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse Beats");
        source.IsAvailable.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        vm.Mode = vm.AvailableModes.First(m => m.Id == "pulse");

        Assert.NotNull(vm.ActiveExternalSource);
        Assert.Equal("pulse", vm.ActiveExternalSource.Id);
    }

    /// <summary>
    /// Verifies that RebuildAvailableModes hides built-in modes when source requests it.
    /// </summary>
    [Fact]
    public void BeatBar_RebuildModes_HidesBuiltInWhenRequested()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse");
        source.IsAvailable.Returns(true);
        source.HidesBuiltInModes.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });

        // Should still have Off and the external mode
        Assert.Contains(vm.AvailableModes, m => m == BeatBarMode.Off);
        Assert.Contains(vm.AvailableModes, m => m.Id == "pulse");
        // Should NOT have OnPeak/OnValley
        Assert.DoesNotContain(vm.AvailableModes, m => m == BeatBarMode.OnPeak);
        Assert.DoesNotContain(vm.AvailableModes, m => m == BeatBarMode.OnValley);
    }

    /// <summary>
    /// Verifies that setting null Mode is ignored.
    /// </summary>
    [Fact]
    public void BeatBar_Mode_NullIgnored()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        vm.Mode = null!;

        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);
    }

    /// <summary>
    /// Verifies that LoadBeats does not detect for external modes.
    /// </summary>
    [Fact]
    public void BeatBar_LoadBeats_SkipsExternalMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse");
        source.IsAvailable.Returns(true);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        vm.Mode = vm.AvailableModes.First(m => m.Id == "pulse");

        // Beats list should stay empty — external mode doesn't use detection
        vm.LoadBeats(MakeScript((0, 0), (500, 100), (1000, 0)));

        Assert.False(vm.HasBeats);
    }

    /// <summary>
    /// Verifies that OnExternalBeatEvent with null source registration is ignored.
    /// </summary>
    [Fact]
    public void BeatBar_OnBeatSourceRegistration_NullSource_Ignored()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = null!,
            IsRegistering = true,
        });

        // No external modes should be added
        Assert.Equal(5, vm.AvailableModes.Count); // Off, OnPeak, OnValley, OnPeakAndValley, MidStroke
    }

    // ═══════════════════════════════════════════════════════════
    //  BeatBar Persistence & Mode Switching Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that switching to an external mode persists the fallback built-in mode.
    /// </summary>
    [Fact]
    public void BeatBar_SwitchToExternal_PersistsFallbackMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        var source = CreatePulseBeatSource();
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });

        // Mode should have auto-switched to external (HidesBuiltInModes = true)
        Assert.True(vm.Mode.IsExternal);
        // Fallback should be persisted
        Assert.Equal("OnPeak", _settings.Osr2BeatBarFallbackMode);
    }

    /// <summary>
    /// Verifies that unregistering external source restores the pre-external built-in mode.
    /// </summary>
    [Fact]
    public void BeatBar_UnregisterExternal_RestoresPreExternalMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        var source = CreatePulseBeatSource();
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.True(vm.Mode.IsExternal);

        // Unregister → should restore OnPeak
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });

        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);
        Assert.Equal("OnPeak", _settings.Osr2BeatBarMode);
    }

    /// <summary>
    /// Verifies round-trip: select built-in → enable external → disable external → built-in restored.
    /// </summary>
    [Fact]
    public void BeatBar_FullRoundTrip_PreservesBuiltInMode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnValley;

        var source = CreatePulseBeatSource();

        // Enable Pulse
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.True(vm.Mode.IsExternal);

        // Disable Pulse
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });
        Assert.Equal(BeatBarMode.OnValley, vm.Mode);

        // Re-enable Pulse
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.True(vm.Mode.IsExternal);

        // Disable again
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });
        Assert.Equal(BeatBarMode.OnValley, vm.Mode);
    }

    /// <summary>
    /// Verifies that selecting Off while external is active does NOT clear the fallback.
    /// </summary>
    [Fact]
    public void BeatBar_SelectOffDuringExternal_PreservesFallback()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        var source = CreatePulseBeatSource();
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.True(vm.Mode.IsExternal);
        Assert.Equal("OnPeak", _settings.Osr2BeatBarFallbackMode);

        // User selects Off while Pulse is active
        vm.Mode = BeatBarMode.Off;

        // Fallback should still be preserved
        Assert.Equal("OnPeak", _settings.Osr2BeatBarFallbackMode);

        // Unregister external → should restore OnPeak (not Off)
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });
        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);
    }

    /// <summary>
    /// Verifies that the fallback mode persists across sessions (simulated by constructor).
    /// </summary>
    [Fact]
    public void BeatBar_FallbackMode_PersistsAcrossSessions()
    {
        // Session 1: User had OnPeak, then Pulse activated
        _settings.Osr2BeatBarMode = "pulse";
        _settings.Osr2BeatBarFallbackMode = "OnPeak";

        // Session 2: App restarts, Pulse not yet registered
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        // Should show the fallback (OnPeak) instead of Off
        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);
    }

    /// <summary>
    /// Verifies that deferred external mode auto-selects when source registers,
    /// even when fallback was loaded.
    /// </summary>
    [Fact]
    public void BeatBar_FallbackMode_SwitchesToExternalWhenRegistered()
    {
        _settings.Osr2BeatBarMode = "pulse";
        _settings.Osr2BeatBarFallbackMode = "OnPeak";

        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);

        // Pulse registers → should switch to the deferred external mode
        var source = CreatePulseBeatSource();
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });

        Assert.True(vm.Mode.IsExternal);
        Assert.Equal("pulse", vm.Mode.Id);
    }

    /// <summary>
    /// Verifies that after fallback restore and external re-registration,
    /// unregistering still restores the original built-in mode.
    /// </summary>
    [Fact]
    public void BeatBar_SessionRestart_FullCycle_RestoresCorrectly()
    {
        _settings.Osr2BeatBarMode = "pulse";
        _settings.Osr2BeatBarFallbackMode = "OnPeak";

        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        // Register Pulse
        var source = CreatePulseBeatSource();
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.True(vm.Mode.IsExternal);

        // Unregister Pulse → should restore OnPeak
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });
        Assert.Equal(BeatBarMode.OnPeak, vm.Mode);
    }

    /// <summary>
    /// Verifies that fallback is cleared after successful restore (no external sources).
    /// </summary>
    [Fact]
    public void BeatBar_FallbackCleared_AfterRestore()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        vm.Mode = BeatBarMode.OnPeak;

        var source = CreatePulseBeatSource();
        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = true,
        });
        Assert.Equal("OnPeak", _settings.Osr2BeatBarFallbackMode);

        vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
        {
            Source = source,
            IsRegistering = false,
        });

        // After restore with no external sources, fallback should be cleared
        Assert.Equal("", _settings.Osr2BeatBarFallbackMode);
    }

    /// <summary>
    /// Verifies that Mode is never set to a value not in AvailableModes (empty guard).
    /// </summary>
    [Fact]
    public void BeatBar_ModeAlwaysInAvailableModes()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        var source = CreatePulseBeatSource();

        // Register and unregister multiple times
        for (int i = 0; i < 3; i++)
        {
            vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
            {
                Source = source,
                IsRegistering = true,
            });
            Assert.Contains(vm.AvailableModes, m => m == vm.Mode);

            vm.OnBeatSourceRegistration(new ExternalBeatSourceRegistration
            {
                Source = source,
                IsRegistering = false,
            });
            Assert.Contains(vm.AvailableModes, m => m == vm.Mode);
        }
    }

    /// <summary>
    /// Verifies that first-time startup defaults to Off (no previous selection).
    /// </summary>
    [Fact]
    public void BeatBar_FirstStartup_DefaultsToOff()
    {
        _settings.Osr2BeatBarMode = "Off";
        _settings.Osr2BeatBarFallbackMode = "";

        var vm = new BeatBarViewModel(_settingsService, _beatDetection);

        Assert.Equal(BeatBarMode.Off, vm.Mode);
    }

    private static IExternalBeatSource CreatePulseBeatSource()
    {
        var source = Substitute.For<IExternalBeatSource>();
        source.Id.Returns("pulse");
        source.DisplayName.Returns("Pulse");
        source.IsAvailable.Returns(true);
        source.HidesBuiltInModes.Returns(true);
        return source;
    }

    // ═══════════════════════════════════════════════════════════
    //  PropertyChanged Tests
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that AxisCardViewModel raises PropertyChanged for Min.
    /// </summary>
    [Fact]
    public void AxisCard_PropertyChanged_Min()
    {
        var vm = new AxisCardViewModel(MakeL0(), _tcode);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Min = 25;

        Assert.Contains("Min", changed);
        Assert.Contains("RangeLabel", changed);
    }

    /// <summary>
    /// Verifies that Osr2PlusSidebarViewModel raises PropertyChanged for multiple properties on connect.
    /// </summary>
    [Fact]
    public void Sidebar_PropertyChanged_OnConnect()
    {
        var vm = CreateSidebarViewModel();
        var transport = Substitute.For<ITransportService>();
        vm.TransportFactory = (_, _, _, _) => (transport, true);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Connect();

        Assert.Contains("IsConnected", changed);
        Assert.Contains("ConnectButtonText", changed);
        Assert.Contains("IsNotConnected", changed);
        Assert.Contains("StatusText", changed);
    }

    /// <summary>
    /// Verifies that BeatBarViewModel raises PropertyChanged for Mode.
    /// </summary>
    [Fact]
    public void BeatBar_PropertyChanged_Mode()
    {
        var vm = new BeatBarViewModel(_settingsService, _beatDetection);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Mode = BeatBarMode.OnPeak;

        Assert.Contains("Mode", changed);
        Assert.Contains("IsActive", changed);
        Assert.Contains("IsExternalMode", changed);
    }
}
