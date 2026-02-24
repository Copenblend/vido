using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Plugin;

namespace Vido.ViewModels;

/// <summary>
/// Represents a single setting for display in the Plugin Detail Settings tab.
/// Binds to the appropriate control based on <see cref="SettingType"/> and
/// persists changes immediately to the <see cref="IPluginSettingsStore"/>.
/// </summary>
public partial class SettingDisplayItem : ObservableObject
{
    private readonly IPluginSettingsStore _store;
    private readonly SettingContribution _definition;
    private bool _suppressSave;

    /// <summary>Setting unique identifier.</summary>
    public string Id => _definition.Id;

    /// <summary>Display title.</summary>
    public string Title => _definition.Title;

    /// <summary>Description text shown below the control.</summary>
    public string Description => _definition.Description;

    /// <summary>Setting type: boolean, string, number, enum, stringList.</summary>
    public string SettingType => _definition.Type;

    /// <summary>Section name for grouping (may be null).</summary>
    public string? Section => _definition.Section;

    /// <summary>Enum values (only for enum type).</summary>
    public IReadOnlyList<string> EnumValues => _definition.EnumValues;

    /// <summary>Whether this is a boolean setting.</summary>
    public bool IsBoolean => SettingType.Equals("boolean", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is a string setting.</summary>
    public bool IsString => SettingType.Equals("string", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is a number setting.</summary>
    public bool IsNumber => SettingType.Equals("number", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is an enum setting.</summary>
    public bool IsEnum => SettingType.Equals("enum", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is a string list setting (e.g., multiple URLs).</summary>
    public bool IsStringList => SettingType.Equals("stringList", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is a folder path setting (browse button).</summary>
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

    /// <summary>Boolean value options for the ComboBox.</summary>
    public static IReadOnlyList<string> BooleanOptions { get; } = ["True", "False"];

    /// <summary>The string value of the setting (used for string/number TextBox binding).</summary>
    [ObservableProperty]
    private string _stringValue = string.Empty;

    partial void OnStringValueChanged(string value)
    {
        if (_suppressSave) return;
        if (IsNumber)
        {
            if (double.TryParse(value, out var num))
                _store.Set(Id, num);
        }
        else if (IsString || IsFolderPath)
        {
            _store.Set(Id, value);
        }
    }

    /// <summary>The selected boolean string ("True"/"False") for boolean ComboBox binding.</summary>
    [ObservableProperty]
    private string _selectedBooleanValue = "False";

    partial void OnSelectedBooleanValueChanged(string value)
    {
        if (_suppressSave) return;
        _store.Set(Id, value.Equals("True", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The selected enum value for enum ComboBox binding.</summary>
    [ObservableProperty]
    private string _selectedEnumValue = string.Empty;

    partial void OnSelectedEnumValueChanged(string value)
    {
        if (_suppressSave) return;
        if (!string.IsNullOrEmpty(value))
            _store.Set(Id, value);
    }

    /// <summary>Items for stringList-type settings.</summary>
    public ObservableCollection<string> ListItems { get; } = [];

    /// <summary>Text for the "add new item" input field.</summary>
    [ObservableProperty]
    private string _newListItemText = string.Empty;

    /// <summary>Validation error message shown below the add-item input.</summary>
    [ObservableProperty]
    private string _validationError = string.Empty;

    /// <summary>Adds a new item to the string list and persists.</summary>
    [RelayCommand]
    public void AddListItem()
    {
        var text = NewListItemText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (ListItems.Contains(text)) return; // no duplicates

        // Validate if a validation rule is specified
        if (!string.IsNullOrEmpty(_definition.Validation) &&
            _definition.Validation.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile))
            {
                ValidationError = "Enter a valid URL (https:// or file://)";
                return;
            }
        }

        ValidationError = string.Empty;
        ListItems.Add(text);
        NewListItemText = string.Empty;
        SaveListToStore();
    }

    /// <summary>Removes an item from the string list and persists.</summary>
    [RelayCommand]
    public void RemoveListItem(string item)
    {
        if (ListItems.Remove(item))
            SaveListToStore();
    }

    /// <summary>Persists the current list items to the store.</summary>
    private void SaveListToStore()
    {
        _store.Set(Id, ListItems.ToList());
    }

    /// <summary>Requests the view to show a folder browser dialog.</summary>
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

    public SettingDisplayItem(SettingContribution definition, IPluginSettingsStore store)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _store = store ?? throw new ArgumentNullException(nameof(store));

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
    /// Loads the current value from the store (or falls back to default).
    /// </summary>
    private void LoadCurrentValue()
    {
        if (IsBoolean)
        {
            var defaultVal = ConvertDefault(_definition.Default) is true;
            var val = _store.Get(Id, defaultVal);
            SelectedBooleanValue = val ? "True" : "False";
        }
        else if (IsString || IsFolderPath)
        {
            var defaultVal = ConvertDefault(_definition.Default)?.ToString() ?? string.Empty;
            StringValue = _store.Get(Id, defaultVal);
        }
        else if (IsNumber)
        {
            var defaultNum = ConvertDefault(_definition.Default) is double d ? d : 0.0;
            try
            {
                var val = _store.Get(Id, defaultNum);
                StringValue = val.ToString();
            }
            catch
            {
                StringValue = defaultNum.ToString();
            }
        }
        else if (IsEnum)
        {
            var defaultVal = ConvertDefault(_definition.Default)?.ToString()
                ?? _definition.EnumValues.FirstOrDefault() ?? string.Empty;
            var val = _store.Get(Id, defaultVal);
            SelectedEnumValue = val;
        }
        else if (IsStringList)
        {
            var defaultList = new List<string>();
            var val = _store.Get(Id, defaultList);
            ListItems.Clear();
            foreach (var item in val)
                ListItems.Add(item);
        }
    }

    /// <summary>
    /// Converts a default value from the manifest (which may be a JsonElement) to the appropriate type.
    /// </summary>
    private static object? ConvertDefault(object? value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => je.GetDouble(),
                JsonValueKind.String => je.GetString(),
                _ => null
            };
        }
        return value;
    }
}
