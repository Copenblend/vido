namespace Vido.ViewModels;

/// <summary>
/// Represents a named category of settings in the Settings panel.
/// Groups related <see cref="SettingDisplayItem"/> instances under a heading.
/// </summary>
public sealed class SettingsCategoryViewModel
{
    /// <summary>
    /// Category display name (e.g. "Playback", "File Explorer", "OSR2+").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Settings within this category.
    /// </summary>
    public IReadOnlyList<SettingDisplayItem> Settings { get; }

    /// <summary>
    /// Creates a settings category with the given name and settings.
    /// </summary>
    /// <param name="name">Display name for the category heading.</param>
    /// <param name="settings">Settings items belonging to this category.</param>
    public SettingsCategoryViewModel(string name, IReadOnlyList<SettingDisplayItem> settings)
    {
        Name = name;
        Settings = settings;
    }
}
