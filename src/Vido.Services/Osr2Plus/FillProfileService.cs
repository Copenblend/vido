using System.Text.Json;
using Vido.Core.Logging;
using Vido.Core.Models.Osr2Plus;

namespace Vido.Services.Osr2Plus;

/// <summary>
/// Manages the lifecycle of fill profiles — CRUD operations, JSON persistence,
/// and built-in profile definitions.
/// </summary>
public sealed class FillProfileService : IFillProfileService
{
    private static readonly string DefaultDir =
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "Vido");

    private static readonly string DefaultFilePath =
        Path.Combine(DefaultDir, "fill-profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly ILogService _logService;
    private readonly List<FillProfile> _builtInProfiles;
    private readonly List<FillProfile> _userProfiles = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <inheritdoc />
    public event Action? ProfilesChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="FillProfileService"/> class.
    /// </summary>
    /// <param name="logService">Logging service.</param>
    /// <param name="filePath">Optional override for the JSON file path (used in tests).</param>
    public FillProfileService(ILogService logService, string? filePath = null)
    {
        _logService = logService;
        _filePath = filePath ?? DefaultFilePath;
        _builtInProfiles = CreateBuiltInProfiles();
    }

    /// <inheritdoc />
    public IReadOnlyList<FillProfile> Profiles
    {
        get
        {
            lock (_lock)
            {
                return _builtInProfiles
                    .Concat(_userProfiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                    .ToList()
                    .AsReadOnly();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FillProfile> GetBuiltInProfiles()
    {
        return _builtInProfiles.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<FillProfile> GetUserProfiles()
    {
        lock (_lock)
        {
            return _userProfiles.ToList().AsReadOnly();
        }
    }

    /// <inheritdoc />
    public FillProfile CreateProfile(string name, Dictionary<string, FillAxisSettings> axes)
    {
        name = ValidateName(name);

        FillProfile profile;
        lock (_lock)
        {
            if (FindByNameInternal(name) is not null)
                throw new ArgumentException($"A profile named \"{name}\" already exists.", nameof(name));

            profile = new FillProfile
            {
                Name = name,
                IsBuiltIn = false,
                Axes = axes,
            };
            _userProfiles.Add(profile);
        }

        ProfilesChanged?.Invoke();
        _ = SaveAsync();
        return profile;
    }

    /// <inheritdoc />
    public void UpdateProfile(string name, Dictionary<string, FillAxisSettings> axes)
    {
        lock (_lock)
        {
            var profile = FindByNameInternal(name)
                ?? throw new InvalidOperationException($"Profile \"{name}\" not found.");

            if (profile.IsBuiltIn)
                throw new InvalidOperationException("Cannot modify a built-in profile.");

            profile.Axes.Clear();
            foreach (var (key, value) in axes)
            {
                profile.Axes[key] = value;
            }
        }

        ProfilesChanged?.Invoke();
        _ = SaveAsync();
    }

    /// <inheritdoc />
    public void RenameProfile(string currentName, string newName)
    {
        newName = ValidateName(newName);

        lock (_lock)
        {
            var profile = FindByNameInternal(currentName)
                ?? throw new InvalidOperationException($"Profile \"{currentName}\" not found.");

            if (profile.IsBuiltIn)
                throw new InvalidOperationException("Cannot modify a built-in profile.");

            if (FindByNameInternal(newName) is not null)
                throw new ArgumentException($"A profile named \"{newName}\" already exists.", nameof(newName));

            profile.Name = newName;
        }

        ProfilesChanged?.Invoke();
        _ = SaveAsync();
    }

    /// <inheritdoc />
    public void DeleteProfile(string name)
    {
        lock (_lock)
        {
            var profile = FindByNameInternal(name)
                ?? throw new InvalidOperationException($"Profile \"{name}\" not found.");

            if (profile.IsBuiltIn)
                throw new InvalidOperationException("Cannot modify a built-in profile.");

            _userProfiles.Remove(profile);
        }

        ProfilesChanged?.Invoke();
        _ = SaveAsync();
    }

    /// <inheritdoc />
    public FillProfile? FindByName(string name)
    {
        lock (_lock)
        {
            return FindByNameInternal(name);
        }
    }

    /// <inheritdoc />
    public void Load()
    {
        lock (_lock)
        {
            _userProfiles.Clear();
            if (!File.Exists(_filePath)) return;

            try
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<FillProfileFileData>(json, JsonOptions);
                if (data?.Profiles is not null)
                {
                    foreach (var p in data.Profiles)
                    {
                        _userProfiles.Add(p);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logService.Warning($"Failed to load fill profiles: {ex.Message}", "FillProfiles");
            }
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync()
    {
        List<FillProfile> snapshot;
        lock (_lock)
        {
            snapshot = [.. _userProfiles];
        }

        var data = new FillProfileFileData { Profiles = snapshot };
        var json = JsonSerializer.Serialize(data, JsonOptions);

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await _saveLock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));

        name = name.Trim();

        if (name.Length > 50)
            throw new ArgumentException("Profile name cannot exceed 50 characters.", nameof(name));

        return name;
    }

    private FillProfile? FindByNameInternal(string name)
    {
        foreach (var p in _builtInProfiles)
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p;
        }

        foreach (var p in _userProfiles)
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p;
        }

        return null;
    }

    private static List<FillProfile> CreateBuiltInProfiles()
    {
        return
        [
            new FillProfile
            {
                Name = "Default",
                IsBuiltIn = true,
                Axes = new()
                {
                    ["L0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R2"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                },
            },
            new FillProfile
            {
                Name = "Gentle Wave",
                IsBuiltIn = true,
                Axes = new()
                {
                    ["L0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R0"] = new() { Enabled = true, Min = 25, Max = 75, FillMode = "Sine", SyncWithStroke = false, FillSpeedHz = 0.5 },
                    ["R1"] = new() { Enabled = true, Min = 25, Max = 75, FillMode = "Sine", SyncWithStroke = false, FillSpeedHz = 0.5 },
                    ["R2"] = new() { Enabled = true, Min = 25, Max = 75, FillMode = "Sine", SyncWithStroke = false, FillSpeedHz = 0.5 },
                },
            },
            new FillProfile
            {
                Name = "Full Random",
                IsBuiltIn = true,
                Axes = new()
                {
                    ["L0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "Random", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "Random", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R2"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "Random", SyncWithStroke = false, FillSpeedHz = 1.0 },
                },
            },
            new FillProfile
            {
                Name = "Grinding",
                IsBuiltIn = true,
                Axes = new()
                {
                    ["L0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R2"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "Square", SyncWithStroke = true, FillSpeedHz = 1.0 },
                },
            },
            new FillProfile
            {
                Name = "Reverse Grinding",
                IsBuiltIn = true,
                Axes = new()
                {
                    ["L0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R0"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R1"] = new() { Enabled = true, Min = 0, Max = 100, FillMode = "None", SyncWithStroke = false, FillSpeedHz = 1.0 },
                    ["R2"] = new() { Enabled = true, Min = 100, Max = 0, FillMode = "Square", SyncWithStroke = true, FillSpeedHz = 1.0 },
                },
            },
        ];
    }

    private sealed class FillProfileFileData
    {
        public List<FillProfile>? Profiles { get; set; }
    }
}
