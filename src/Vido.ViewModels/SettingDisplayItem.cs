using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// Represents a single setting for display in the Settings panel.
/// Binds to the appropriate control based on <see cref="SettingType"/> and
/// persists changes immediately via the <see cref="SettingDefinition"/> getter/setter delegates.
/// </summary>
public partial class SettingDisplayItem : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISettingsStore? _settingsStore;
    private readonly SettingDefinition _definition;
    private bool _suppressSave;

    /// <summary>
    /// Setting unique identifier.
    /// </summary>
    public string Id => _definition.Key;

    /// <summary>
    /// Display title.
    /// </summary>
    public string Title => _definition.Title;

    /// <summary>
    /// Description text shown below the control.
    /// </summary>
    public string Description => _definition.Description;

    /// <summary>
    /// Setting type: boolean, string, number, enum, stringList.
    /// </summary>
    public string SettingType => _definition.Type;

    /// <summary>
    /// Section name for grouping (may be null).
    /// </summary>
    public string? Section => _definition.Section;

    /// <summary>
    /// Enum values (only for enum type).
    /// </summary>
    public IReadOnlyList<string> EnumValues => _definition.EnumValues ?? [];

    /// <summary>
    /// Whether this is a boolean setting.
    /// </summary>
    public bool IsBoolean => SettingType.Equals("boolean", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a string setting.
    /// </summary>
    public bool IsString => SettingType.Equals("string", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a number setting.
    /// </summary>
    public bool IsNumber => SettingType.Equals("number", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is an enum setting.
    /// </summary>
    public bool IsEnum => SettingType.Equals("enum", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a string list setting (e.g., multiple URLs).
    /// </summary>
    public bool IsStringList => SettingType.Equals("stringList", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a folder path setting (browse button).
    /// </summary>
    public bool IsFolderPath => SettingType.Equals("folderPath", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Raised when the user clicks the Browse button for a <c>folderPath</c> setting.
    /// The handler should show a folder browser dialog and call <see cref="SetFolderPath"/>
    /// with the selected path (or null to cancel).
    /// </summary>
    public event Action<SettingDisplayItem>? BrowseFolderRequested;

    /// <summary>
    /// Controls visibility of this setting in the UI. Used for conditional settings
    /// that depend on another setting's value (e.g., directory path only shown when feature enabled).
    /// </summary>
    [ObservableProperty]
    private bool _isSettingVisible = true;

    /// <summary>
    /// Boolean value options for the ComboBox.
    /// </summary>
    public static IReadOnlyList<string> BooleanOptions { get; } = ["True", "False"];

    /// <summary>
    /// The string value of the setting (used for string/number TextBox binding).
    /// </summary>
    [ObservableProperty]
    private string _stringValue = string.Empty;

    partial void OnStringValueChanged(string value)
    {
        if (_suppressSave) return;
        if (IsNumber)
        {
            if (double.TryParse(value, out var num))
            {
                if (_settingsStore is not null)
                {
                    _settingsStore.Set(_definition.Key, num);
                }
                else
                {
                    _definition.Setter?.Invoke(_settingsService.Current, num);
                    _settingsService.QueueSave();
                }
            }
        }
        else if (IsString || IsFolderPath)
        {
            if (_settingsStore is not null)
            {
                _settingsStore.Set(_definition.Key, value);
            }
            else
            {
                _definition.Setter?.Invoke(_settingsService.Current, value);
                _settingsService.QueueSave();
            }
        }
    }

    /// <summary>
    /// The selected boolean string ("True"/"False") for boolean ComboBox binding.
    /// </summary>
    [ObservableProperty]
    private string _selectedBooleanValue = "False";

    partial void OnSelectedBooleanValueChanged(string value)
    {
        if (_suppressSave) return;
        var boolValue = value.Equals("True", StringComparison.OrdinalIgnoreCase);
        if (_settingsStore is not null)
        {
            _settingsStore.Set(_definition.Key, boolValue);
        }
        else
        {
            _definition.Setter?.Invoke(_settingsService.Current, boolValue);
            _settingsService.QueueSave();
        }
    }

    /// <summary>
    /// The selected enum value for enum ComboBox binding.
    /// </summary>
    [ObservableProperty]
    private string _selectedEnumValue = string.Empty;

    partial void OnSelectedEnumValueChanged(string value)
    {
        if (_suppressSave) return;
        if (!string.IsNullOrEmpty(value))
        {
            if (_settingsStore is not null)
            {
                _settingsStore.Set(_definition.Key, value);
            }
            else
            {
                _definition.Setter?.Invoke(_settingsService.Current, value);
                _settingsService.QueueSave();
            }
        }
    }

    /// <summary>
    /// Items for stringList-type settings.
    /// </summary>
    public ObservableCollection<string> ListItems { get; } = [];

    /// <summary>
    /// Text for the "add new item" input field.
    /// </summary>
    [ObservableProperty]
    private string _newListItemText = string.Empty;

    /// <summary>
    /// Validation error message shown below the add-item input.
    /// </summary>
    [ObservableProperty]
    private string _validationError = string.Empty;

    /// <summary>
    /// Adds a new item to the string list and persists.
    /// </summary>
    [RelayCommand]
    public void AddListItem()
    {
        var text = NewListItemText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (ListItems.Contains(text)) return; // no duplicates

        ValidationError = string.Empty;
        ListItems.Add(text);
        NewListItemText = string.Empty;
        SaveListToStore();
    }

    /// <summary>
    /// Removes an item from the string list and persists.
    /// </summary>
    /// <param name="item">The list entry to remove.</param>
    [RelayCommand]
    public void RemoveListItem(string item)
    {
        if (ListItems.Remove(item))
            SaveListToStore();
    }

    /// <summary>
    /// Persists the current list items to the store.
    /// </summary>
    private void SaveListToStore()
    {
        if (_settingsStore is not null)
        {
            _settingsStore.Set(_definition.Key, ListItems.ToList());
        }
        else
        {
            _definition.Setter?.Invoke(_settingsService.Current, ListItems.ToList());
            _settingsService.QueueSave();
        }
    }

    /// <summary>
    /// Requests the view to show a folder browser dialog.
    /// </summary>
    [RelayCommand]
    public void BrowseFolder()
    {
        BrowseFolderRequested?.Invoke(this);
    }

    /// <summary>
    /// Sets the folder path from a browse dialog result.
    /// Called by the view after the user selects a folder.
    /// </summary>
    public void SetFolderPath(string? path)
    {
        if (path is null) return;
        StringValue = path;
    }
    
    /// <summary>
    /// Creates a setting display item backed by the given definition and settings service,
    /// loading the current persisted value (or the default) for UI binding.
    /// </summary>
    /// <param name="definition">Setting definition describing type, title, default, and getter/setter delegates.</param>
    /// <param name="settingsService">Settings service for reading current values and persisting changes.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="definition"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="settingsService"/> is null.</exception>
    public SettingDisplayItem(SettingDefinition definition, ISettingsService settingsService, ISettingsStore? settingsStore = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsStore = settingsStore;

        _suppressSave = true;
        LoadCurrentValue();
        _suppressSave = false;
    }

    /// <summary>
    /// Reloads the current value from the backing store, refreshing the UI.
    /// </summary>
    public void Reload()
    {
        _suppressSave = true;
        LoadCurrentValue();
        _suppressSave = false;
    }

    /// <summary>
    /// Loads the current value from the settings service via the getter delegate (or falls back to default).
    /// </summary>
    private void LoadCurrentValue()
    {
        var rawValue = _definition.Getter?.Invoke(_settingsService.Current) ?? _definition.DefaultValue;

        if (IsBoolean)
        {
            var val = rawValue is true;
            SelectedBooleanValue = val ? "True" : "False";
        }
        else if (IsString || IsFolderPath)
        {
            StringValue = rawValue?.ToString() ?? string.Empty;
        }
        else if (IsNumber)
        {
            StringValue = rawValue switch
            {
                double d => d.ToString(),
                int i => i.ToString(),
                float f => f.ToString(),
                _ => (_definition.DefaultValue ?? 0).ToString()!
            };
        }
        else if (IsEnum)
        {
            var val = rawValue?.ToString()
                ?? _definition.EnumValues?.FirstOrDefault() ?? string.Empty;
            SelectedEnumValue = val;
        }
        else if (IsStringList)
        {
            ListItems.Clear();
            if (rawValue is IEnumerable<string> items)
            {
                foreach (var item in items)
                    ListItems.Add(item);
            }
        }
    }

}
