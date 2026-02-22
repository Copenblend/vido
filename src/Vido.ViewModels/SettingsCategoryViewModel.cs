namespace Vido.ViewModels;

/// <summary>
/// Represents a named category of settings in the Settings panel.
/// Groups related <see cref="SettingDisplayItem"/> instances under a heading.
/// </summary>
public sealed class SettingsCategoryViewModel
{
    /// <summary>Category display name (e.g. "Playback", "File Explorer", plugin name).</summary>
    public string Name { get; }

    /// <summary>Settings within this category.</summary>
    public IReadOnlyList<SettingDisplayItem> Settings { get; }

    /// <summary>Whether this category represents plugin-contributed settings.</summary>
    public bool IsPlugin { get; }

    public SettingsCategoryViewModel(string name, IReadOnlyList<SettingDisplayItem> settings, bool isPlugin = false)
    {
        Name = name;
        Settings = settings;
        IsPlugin = isPlugin;
    }
}
