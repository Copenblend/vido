using NSubstitute;
using Vido.Core.Logging;
using Vido.Core.Models.Osr2Plus;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

public sealed class FillProfileServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;
    private readonly ILogService _logService = Substitute.For<ILogService>();

    public FillProfileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"VidoTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "fill-profiles.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private FillProfileService CreateService() => new(_logService, _filePath);

    private static Dictionary<string, FillAxisSettings> MakeAxes(string fillMode = "None")
    {
        return new()
        {
            ["L0"] = new() { FillMode = fillMode },
            ["R0"] = new() { FillMode = fillMode },
            ["R1"] = new() { FillMode = fillMode },
            ["R2"] = new() { FillMode = fillMode },
        };
    }

    // ── Load ──────────────────────────────────────────────────────────

    [Fact]
    public void Load_FileNotExists_ReturnsBuiltInOnly()
    {
        var svc = CreateService();
        svc.Load();

        Assert.Equal(3, svc.Profiles.Count);
        Assert.All(svc.Profiles, p => Assert.True(p.IsBuiltIn));
    }

    [Fact]
    public void Load_ValidJson_LoadsUserProfiles()
    {
        File.WriteAllText(_filePath, """
        {
            "profiles": [
                {
                    "name": "My Profile",
                    "axes": {
                        "L0": { "enabled": true, "min": 0, "max": 100, "fillMode": "None", "syncWithStroke": false, "fillSpeedHz": 1.0 }
                    }
                }
            ]
        }
        """);

        var svc = CreateService();
        svc.Load();

        var user = svc.GetUserProfiles();
        Assert.Single(user);
        Assert.Equal("My Profile", user[0].Name);
        Assert.False(user[0].IsBuiltIn);
    }

    [Fact]
    public void Load_MalformedJson_LogsWarning_ReturnsBuiltInOnly()
    {
        File.WriteAllText(_filePath, "{ not valid json }}}");

        var svc = CreateService();
        svc.Load();

        Assert.Equal(3, svc.Profiles.Count);
        Assert.All(svc.Profiles, p => Assert.True(p.IsBuiltIn));
        _logService.Received(1).Warning(Arg.Is<string>(s => s.Contains("Failed to load fill profiles")), "FillProfiles");
    }

    // ── CreateProfile ─────────────────────────────────────────────────

    [Fact]
    public void CreateProfile_ValidName_AddsToList()
    {
        var svc = CreateService();
        var axes = MakeAxes("Sine");

        var profile = svc.CreateProfile("My Custom", axes);

        Assert.Equal("My Custom", profile.Name);
        Assert.False(profile.IsBuiltIn);
        Assert.Contains(svc.Profiles, p => p.Name == "My Custom");
    }

    [Fact]
    public void CreateProfile_DuplicateName_ThrowsArgumentException()
    {
        var svc = CreateService();
        svc.CreateProfile("Test", MakeAxes());

        Assert.Throws<ArgumentException>(() => svc.CreateProfile("Test", MakeAxes()));
    }

    [Fact]
    public void CreateProfile_BuiltInDuplicate_ThrowsArgumentException()
    {
        var svc = CreateService();

        Assert.Throws<ArgumentException>(() => svc.CreateProfile("Default", MakeAxes()));
    }

    [Fact]
    public void CreateProfile_EmptyName_ThrowsArgumentException()
    {
        var svc = CreateService();

        Assert.Throws<ArgumentException>(() => svc.CreateProfile("", MakeAxes()));
        Assert.Throws<ArgumentException>(() => svc.CreateProfile("   ", MakeAxes()));
    }

    [Fact]
    public void CreateProfile_NameTooLong_ThrowsArgumentException()
    {
        var svc = CreateService();
        var longName = new string('A', 51);

        Assert.Throws<ArgumentException>(() => svc.CreateProfile(longName, MakeAxes()));
    }

    [Fact]
    public void CreateProfile_TrimsWhitespace()
    {
        var svc = CreateService();

        var profile = svc.CreateProfile("  Padded Name  ", MakeAxes());

        Assert.Equal("Padded Name", profile.Name);
    }

    // ── DeleteProfile ─────────────────────────────────────────────────

    [Fact]
    public void DeleteProfile_UserProfile_RemovesFromList()
    {
        var svc = CreateService();
        svc.CreateProfile("ToDelete", MakeAxes());
        Assert.Contains(svc.Profiles, p => p.Name == "ToDelete");

        svc.DeleteProfile("ToDelete");

        Assert.DoesNotContain(svc.Profiles, p => p.Name == "ToDelete");
    }

    [Fact]
    public void DeleteProfile_BuiltIn_ThrowsInvalidOperationException()
    {
        var svc = CreateService();

        Assert.Throws<InvalidOperationException>(() => svc.DeleteProfile("Default"));
    }

    [Fact]
    public void DeleteProfile_NotFound_ThrowsInvalidOperationException()
    {
        var svc = CreateService();

        Assert.Throws<InvalidOperationException>(() => svc.DeleteProfile("Nonexistent"));
    }

    // ── RenameProfile ─────────────────────────────────────────────────

    [Fact]
    public void RenameProfile_ValidNewName_RenamesInPlace()
    {
        var svc = CreateService();
        svc.CreateProfile("OldName", MakeAxes());

        svc.RenameProfile("OldName", "NewName");

        Assert.DoesNotContain(svc.Profiles, p => p.Name == "OldName");
        Assert.Contains(svc.Profiles, p => p.Name == "NewName");
    }

    [Fact]
    public void RenameProfile_BuiltIn_ThrowsInvalidOperationException()
    {
        var svc = CreateService();

        Assert.Throws<InvalidOperationException>(() => svc.RenameProfile("Default", "Custom Default"));
    }

    [Fact]
    public void RenameProfile_DuplicateName_ThrowsArgumentException()
    {
        var svc = CreateService();
        svc.CreateProfile("First", MakeAxes());
        svc.CreateProfile("Second", MakeAxes());

        Assert.Throws<ArgumentException>(() => svc.RenameProfile("First", "Second"));
    }

    // ── UpdateProfile ─────────────────────────────────────────────────

    [Fact]
    public void UpdateProfile_UserProfile_UpdatesAxes()
    {
        var svc = CreateService();
        svc.CreateProfile("Updatable", MakeAxes("None"));

        var newAxes = MakeAxes("Sine");
        svc.UpdateProfile("Updatable", newAxes);

        var updated = svc.FindByName("Updatable")!;
        Assert.Equal("Sine", updated.Axes["L0"].FillMode);
    }

    [Fact]
    public void UpdateProfile_BuiltIn_ThrowsInvalidOperationException()
    {
        var svc = CreateService();

        Assert.Throws<InvalidOperationException>(() => svc.UpdateProfile("Default", MakeAxes()));
    }

    // ── Profiles ordering ─────────────────────────────────────────────

    [Fact]
    public void Profiles_ReturnsBuiltInFirst_ThenUserAlphabetical()
    {
        var svc = CreateService();
        svc.CreateProfile("Zebra", MakeAxes());
        svc.CreateProfile("Alpha", MakeAxes());

        var profiles = svc.Profiles;

        // First 3 are built-in
        Assert.True(profiles[0].IsBuiltIn);
        Assert.True(profiles[2].IsBuiltIn);

        // User profiles sorted alphabetically after built-ins
        Assert.Equal("Alpha", profiles[3].Name);
        Assert.Equal("Zebra", profiles[4].Name);
    }

    // ── FindByName ────────────────────────────────────────────────────

    [Fact]
    public void FindByName_CaseInsensitive_FindsProfile()
    {
        var svc = CreateService();

        Assert.NotNull(svc.FindByName("default"));
        Assert.NotNull(svc.FindByName("DEFAULT"));
        Assert.NotNull(svc.FindByName("Default"));
    }

    [Fact]
    public void FindByName_NotFound_ReturnsNull()
    {
        var svc = CreateService();

        Assert.Null(svc.FindByName("Nonexistent"));
    }

    // ── SaveAsync / RoundTrip ─────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_WritesJsonToFile()
    {
        var svc = CreateService();
        svc.CreateProfile("Saved", MakeAxes("Sine"));

        await svc.SaveAsync();

        Assert.True(File.Exists(_filePath));
        var json = File.ReadAllText(_filePath);
        Assert.Contains("Saved", json);
        Assert.Contains("fillMode", json); // camelCase property names
    }

    [Fact]
    public async Task SaveAsync_RoundTrip_LoadAfterSave_Preserves()
    {
        var svc1 = CreateService();
        var axes = new Dictionary<string, FillAxisSettings>
        {
            ["L0"] = new() { Enabled = false, Min = 10, Max = 90, FillMode = "Sine", SyncWithStroke = true, FillSpeedHz = 2.5 },
            ["R0"] = new() { Min = 5, Max = 95, FillMode = "Random" },
        };
        svc1.CreateProfile("RoundTrip", axes);
        await svc1.SaveAsync();

        var svc2 = CreateService();
        svc2.Load();

        var loaded = svc2.FindByName("RoundTrip");
        Assert.NotNull(loaded);
        Assert.False(loaded.IsBuiltIn);

        var l0 = loaded.Axes["L0"];
        Assert.False(l0.Enabled);
        Assert.Equal(10, l0.Min);
        Assert.Equal(90, l0.Max);
        Assert.Equal("Sine", l0.FillMode);
        Assert.True(l0.SyncWithStroke);
        Assert.Equal(2.5, l0.FillSpeedHz);
    }

    // ── GetBuiltInProfiles ────────────────────────────────────────────

    [Fact]
    public void GetBuiltInProfiles_ReturnsThreeDefaults()
    {
        var svc = CreateService();
        var builtIns = svc.GetBuiltInProfiles();

        Assert.Equal(3, builtIns.Count);
        Assert.Equal("Default", builtIns[0].Name);
        Assert.Equal("Gentle Wave", builtIns[1].Name);
        Assert.Equal("Full Random", builtIns[2].Name);
        Assert.All(builtIns, p => Assert.True(p.IsBuiltIn));
    }

    // ── ProfilesChanged event ─────────────────────────────────────────

    [Fact]
    public void CreateProfile_RaisesProfilesChanged()
    {
        var svc = CreateService();
        var raised = false;
        svc.ProfilesChanged += () => raised = true;

        svc.CreateProfile("EventTest", MakeAxes());

        Assert.True(raised);
    }

    [Fact]
    public void DeleteProfile_RaisesProfilesChanged()
    {
        var svc = CreateService();
        svc.CreateProfile("EventTest", MakeAxes());
        var raised = false;
        svc.ProfilesChanged += () => raised = true;

        svc.DeleteProfile("EventTest");

        Assert.True(raised);
    }

    [Fact]
    public void RenameProfile_RaisesProfilesChanged()
    {
        var svc = CreateService();
        svc.CreateProfile("EventTest", MakeAxes());
        var raised = false;
        svc.ProfilesChanged += () => raised = true;

        svc.RenameProfile("EventTest", "Renamed");

        Assert.True(raised);
    }

    // ── Built-in profile value tests (VI-0016) ────────────────────────

    [Fact]
    public void GetBuiltInProfiles_Default_HasCorrectValues()
    {
        var svc = CreateService();
        var profile = svc.FindByName("Default")!;

        Assert.All(profile.Axes.Values, a =>
        {
            Assert.True(a.Enabled);
            Assert.Equal(0, a.Min);
            Assert.Equal(100, a.Max);
            Assert.Equal("None", a.FillMode);
            Assert.False(a.SyncWithStroke);
            Assert.Equal(1.0, a.FillSpeedHz);
        });
    }

    [Fact]
    public void GetBuiltInProfiles_GentleWave_HasCorrectValues()
    {
        var svc = CreateService();
        var profile = svc.FindByName("Gentle Wave")!;

        var l0 = profile.Axes["L0"];
        Assert.Equal("None", l0.FillMode);

        foreach (var axis in new[] { "R0", "R1", "R2" })
        {
            var a = profile.Axes[axis];
            Assert.Equal(25, a.Min);
            Assert.Equal(75, a.Max);
            Assert.Equal("Sine", a.FillMode);
            Assert.False(a.SyncWithStroke);
            Assert.Equal(0.5, a.FillSpeedHz);
        }
    }

    [Fact]
    public void GetBuiltInProfiles_FullRandom_HasCorrectValues()
    {
        var svc = CreateService();
        var profile = svc.FindByName("Full Random")!;

        var l0 = profile.Axes["L0"];
        Assert.Equal("None", l0.FillMode);

        foreach (var axis in new[] { "R0", "R1", "R2" })
        {
            var a = profile.Axes[axis];
            Assert.Equal(0, a.Min);
            Assert.Equal(100, a.Max);
            Assert.Equal("Random", a.FillMode);
            Assert.Equal(1.0, a.FillSpeedHz);
        }
    }

    [Fact]
    public void GetBuiltInProfiles_AllAreBuiltIn()
    {
        var svc = CreateService();
        Assert.All(svc.GetBuiltInProfiles(), p => Assert.True(p.IsBuiltIn));
    }
}
