namespace Vido.ViewModels;

/// <summary>
/// Represents a named category of settings in the Settings panel.
/// Groups related <see cref="SettingDisplayItem"/> instances under a heading.
/// </summary>
public sealed class SettingsCategoryViewModel
{
    /// <summary>
    /// Category display name (e.g. "Playback", "File Explorer", plugin name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Settings within this category.
    /// </summary>
    public IReadOnlyList<SettingDisplayItem> Settings { get; }

    /// <summary>
    /// Whether this category represents plugin-contributed settings.
    /// </summary>
    public bool IsPlugin { get; }
    
    /// <summary>
    /// Creates a settings category with the given name, settings, and plugin flag.
    /// </summary>
    /// <param name="name">Display name for the category heading.</param>
    /// <param name="settings">Settings items belonging to this category.</param>
    /// <param name="isPlugin">Whether this category represents plugin-contributed settings.</param>
    public SettingsCategoryViewModel(string name, IReadOnlyList<SettingDisplayItem> settings, bool isPlugin = false)
    {
        Name = name;
        Settings = settings;
        IsPlugin = isPlugin;
    }
}
