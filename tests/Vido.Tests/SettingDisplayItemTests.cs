using NSubstitute;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="SettingDisplayItem"/>, verifying setting types,
/// value loading, persistence via getter/setter delegates, and string list operations.
/// </summary>
public sealed class SettingDisplayItemTests
{
    private static AppSettings CreateAppSettings() => new();

    private static ISettingsService CreateSettingsService(AppSettings? settings = null)
    {
        var svc = Substitute.For<ISettingsService>();
        svc.Current.Returns(settings ?? CreateAppSettings());
        return svc;
    }

    private static SettingDefinition MakeStringListDefinition(
        List<string>? backingList = null,
        string key = "test.list") =>
        new(
            Key: key,
            Type: "stringList",
            DefaultValue: new List<string>(),
            Title: "Test List",
            Description: "A list setting",
            Getter: _ => backingList ?? new List<string>(),
            Setter: (_, v) =>
            {
                if (backingList is not null && v is List<string> items)
                {
                    backingList.Clear();
                    backingList.AddRange(items);
                }
            });

    private static SettingDefinition MakeBooleanDefinition(
        string key = "test.bool",
        bool defaultValue = false,
        Func<AppSettings, object?>? getter = null,
        Action<AppSettings, object?>? setter = null) =>
        new(
            Key: key,
            Type: "boolean",
            DefaultValue: defaultValue,
            Title: "Test Bool",
            Description: "A bool setting",
            Getter: getter ?? (_ => defaultValue),
            Setter: setter ?? ((_, _) => { }));

    private static SettingDefinition MakeNumberDefinition(
        string key = "test.num",
        double defaultValue = 0.0,
        Func<AppSettings, object?>? getter = null,
        Action<AppSettings, object?>? setter = null) =>
        new(
            Key: key,
            Type: "number",
            DefaultValue: defaultValue,
            Title: "Test Number",
            Description: "A number setting",
            Getter: getter ?? (_ => defaultValue),
            Setter: setter ?? ((_, _) => { }));

    private static SettingDefinition MakeEnumDefinition(
        string key = "test.enum",
        string defaultValue = "A",
        IReadOnlyList<string>? enumValues = null,
        Func<AppSettings, object?>? getter = null,
        Action<AppSettings, object?>? setter = null) =>
        new(
            Key: key,
            Type: "enum",
            DefaultValue: defaultValue,
            Title: "Test Enum",
            Description: "An enum setting",
            EnumValues: enumValues ?? new List<string> { "A", "B", "C" },
            Getter: getter ?? (_ => defaultValue),
            Setter: setter ?? ((_, _) => { }));

    private static SettingDefinition MakeFolderPathDefinition(
        string key = "test.folder",
        string defaultValue = "",
        Func<AppSettings, object?>? getter = null,
        Action<AppSettings, object?>? setter = null) =>
        new(
            Key: key,
            Type: "folderPath",
            DefaultValue: defaultValue,
            Title: "Test Folder",
            Description: "A folder path setting",
            Getter: getter ?? (_ => defaultValue),
            Setter: setter ?? ((_, _) => { }));

    // ── Type detection ──

    /// <summary>
    /// Verifies IsStringList is true for stringList type.
    /// </summary>
    [Fact]
    public void IsStringList_TrueForStringListType()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);
        Assert.True(item.IsStringList);
    }

    /// <summary>
    /// Verifies IsStringList is false for other types.
    /// </summary>
    [Fact]
    public void IsStringList_FalseForOtherTypes()
    {
        var svc = CreateSettingsService();
        var def = new SettingDefinition(Key: "test", Type: "string", DefaultValue: "", Title: "T", Description: "D",
            Getter: _ => "", Setter: (_, _) => { });
        var item = new SettingDisplayItem(def, svc);
        Assert.False(item.IsStringList);
    }

    // ── StringList operations ──

    /// <summary>
    /// Verifies constructor loads existing list items via getter.
    /// </summary>
    [Fact]
    public void Constructor_LoadsExistingListItems()
    {
        var list = new List<string> { "https://one.com", "https://two.com" };
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(list), svc);

        Assert.Equal(2, item.ListItems.Count);
        Assert.Equal("https://one.com", item.ListItems[0]);
        Assert.Equal("https://two.com", item.ListItems[1]);
    }

    /// <summary>
    /// Verifies AddListItem adds to collection.
    /// </summary>
    [Fact]
    public void AddListItem_AddsToCollection()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal("https://new.com", item.ListItems[0]);
    }

    /// <summary>
    /// Verifies AddListItem trims whitespace.
    /// </summary>
    [Fact]
    public void AddListItem_TrimsWhitespace()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);

        item.NewListItemText = "  https://new.com  ";
        item.AddListItemCommand.Execute(null);

        Assert.Equal("https://new.com", item.ListItems[0]);
    }

    /// <summary>
    /// Verifies AddListItem clears NewListItemText.
    /// </summary>
    [Fact]
    public void AddListItem_ClearsNewListItemText()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        Assert.Equal(string.Empty, item.NewListItemText);
    }

    /// <summary>
    /// Verifies AddListItem ignores empty text.
    /// </summary>
    [Fact]
    public void AddListItem_IgnoresEmptyText()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);

        item.NewListItemText = "   ";
        item.AddListItemCommand.Execute(null);

        Assert.Empty(item.ListItems);
    }

    /// <summary>
    /// Verifies AddListItem ignores duplicates.
    /// </summary>
    [Fact]
    public void AddListItem_IgnoresDuplicates()
    {
        var list = new List<string> { "https://existing.com" };
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(list), svc);

        item.NewListItemText = "https://existing.com";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
    }

    /// <summary>
    /// Verifies AddListItem calls setter and QueueSave.
    /// </summary>
    [Fact]
    public void AddListItem_PersistsViaSetter()
    {
        var backingList = new List<string>();
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(backingList, "test.list"), svc);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        Assert.Single(backingList);
        Assert.Equal("https://new.com", backingList[0]);
        svc.Received().QueueSave();
    }

    /// <summary>
    /// Verifies RemoveListItem removes from collection.
    /// </summary>
    [Fact]
    public void RemoveListItem_RemovesFromCollection()
    {
        var list = new List<string> { "https://one.com", "https://two.com" };
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(list), svc);

        item.RemoveListItemCommand.Execute("https://one.com");

        Assert.Single(item.ListItems);
        Assert.Equal("https://two.com", item.ListItems[0]);
    }

    /// <summary>
    /// Verifies RemoveListItem calls setter and QueueSave.
    /// </summary>
    [Fact]
    public void RemoveListItem_PersistsViaSetter()
    {
        var backingList = new List<string> { "https://one.com", "https://two.com" };
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(backingList, "test.list"), svc);

        item.RemoveListItemCommand.Execute("https://one.com");

        Assert.Single(backingList);
        Assert.Equal("https://two.com", backingList[0]);
        svc.Received().QueueSave();
    }

    /// <summary>
    /// Verifies RemoveListItem with nonexistent item does nothing.
    /// </summary>
    [Fact]
    public void RemoveListItem_NonexistentItem_DoesNothing()
    {
        var list = new List<string> { "https://one.com" };
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(list), svc);

        item.RemoveListItemCommand.Execute("https://nonexistent.com");

        Assert.Single(item.ListItems);
    }

    /// <summary>
    /// Verifies type flags are mutually exclusive for stringList.
    /// </summary>
    [Fact]
    public void OtherTypeProperties_FalseForStringList()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);

        Assert.False(item.IsBoolean);
        Assert.False(item.IsString);
        Assert.False(item.IsNumber);
        Assert.False(item.IsEnum);
    }

    // ── Boolean type ──

    /// <summary>
    /// Verifies boolean type loads "True" when getter returns true.
    /// </summary>
    [Fact]
    public void Boolean_LoadsValueFromGetter()
    {
        var svc = CreateSettingsService();
        var def = MakeBooleanDefinition(getter: _ => true);
        var item = new SettingDisplayItem(def, svc);

        Assert.Equal("True", item.SelectedBooleanValue);
    }

    /// <summary>
    /// Verifies changing boolean value calls setter and QueueSave.
    /// </summary>
    [Fact]
    public void Boolean_SetValue_PersistsViaSetter()
    {
        bool captured = false;
        var svc = CreateSettingsService();
        var def = MakeBooleanDefinition(
            getter: _ => false,
            setter: (_, v) => captured = v is true);
        var item = new SettingDisplayItem(def, svc);

        item.SelectedBooleanValue = "True";

        Assert.True(captured);
        svc.Received().QueueSave();
    }

    // ── Number type ──

    /// <summary>
    /// Verifies number type loads value from getter as string.
    /// </summary>
    [Fact]
    public void Number_LoadsValueFromGetter()
    {
        var svc = CreateSettingsService();
        var def = MakeNumberDefinition(getter: _ => 42.5);
        var item = new SettingDisplayItem(def, svc);

        Assert.Equal("42.5", item.StringValue);
    }

    /// <summary>
    /// Verifies changing number value calls setter with parsed double.
    /// </summary>
    [Fact]
    public void Number_SetValue_PersistsViaSetter()
    {
        double captured = 0;
        var svc = CreateSettingsService();
        var def = MakeNumberDefinition(
            getter: _ => 0.0,
            setter: (_, v) => captured = Convert.ToDouble(v));
        var item = new SettingDisplayItem(def, svc);

        item.StringValue = "99.5";

        Assert.Equal(99.5, captured);
        svc.Received().QueueSave();
    }

    /// <summary>
    /// Verifies number type handles int getter values.
    /// </summary>
    [Fact]
    public void Number_LoadsIntValueFromGetter()
    {
        var svc = CreateSettingsService();
        var def = MakeNumberDefinition(getter: _ => 7777);
        var item = new SettingDisplayItem(def, svc);

        Assert.Equal("7777", item.StringValue);
    }

    // ── Enum type ──

    /// <summary>
    /// Verifies enum type loads value from getter.
    /// </summary>
    [Fact]
    public void Enum_LoadsValueFromGetter()
    {
        var svc = CreateSettingsService();
        var def = MakeEnumDefinition(getter: _ => "B");
        var item = new SettingDisplayItem(def, svc);

        Assert.Equal("B", item.SelectedEnumValue);
    }

    /// <summary>
    /// Verifies changing enum value calls setter and QueueSave.
    /// </summary>
    [Fact]
    public void Enum_SetValue_PersistsViaSetter()
    {
        string captured = "";
        var svc = CreateSettingsService();
        var def = MakeEnumDefinition(
            getter: _ => "A",
            setter: (_, v) => captured = v?.ToString() ?? "");
        var item = new SettingDisplayItem(def, svc);

        item.SelectedEnumValue = "C";

        Assert.Equal("C", captured);
        svc.Received().QueueSave();
    }

    // ── FolderPath type ──

    /// <summary>
    /// Verifies IsFolderPath is true for folderPath type.
    /// </summary>
    [Fact]
    public void IsFolderPath_TrueForFolderPathType()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeFolderPathDefinition(), svc);
        Assert.True(item.IsFolderPath);
        Assert.False(item.IsString);
    }

    /// <summary>
    /// Verifies IsFolderPath is false for string type.
    /// </summary>
    [Fact]
    public void IsFolderPath_FalseForStringType()
    {
        var svc = CreateSettingsService();
        var def = new SettingDefinition(Key: "test.str", Type: "string", DefaultValue: "", Title: "T", Description: "D",
            Getter: _ => "", Setter: (_, _) => { });
        var item = new SettingDisplayItem(def, svc);
        Assert.False(item.IsFolderPath);
        Assert.True(item.IsString);
    }

    /// <summary>
    /// Verifies SetFolderPath updates StringValue and persists.
    /// </summary>
    [Fact]
    public void SetFolderPath_UpdatesStringValue()
    {
        string captured = "";
        var svc = CreateSettingsService();
        var def = MakeFolderPathDefinition(
            getter: _ => "",
            setter: (_, v) => captured = v?.ToString() ?? "");
        var item = new SettingDisplayItem(def, svc);

        item.SetFolderPath(@"C:\Users\test\Screenshots");
        Assert.Equal(@"C:\Users\test\Screenshots", item.StringValue);
        Assert.Equal(@"C:\Users\test\Screenshots", captured);
        svc.Received().QueueSave();
    }

    /// <summary>
    /// Verifies SetFolderPath with null does nothing.
    /// </summary>
    [Fact]
    public void SetFolderPath_NullDoesNothing()
    {
        var svc = CreateSettingsService();
        var def = MakeFolderPathDefinition(getter: _ => @"C:\existing");
        var item = new SettingDisplayItem(def, svc);

        item.SetFolderPath(null);
        Assert.Equal(@"C:\existing", item.StringValue);
    }

    /// <summary>
    /// Verifies BrowseFolder raises BrowseFolderRequested event.
    /// </summary>
    [Fact]
    public void BrowseFolder_RaisesBrowseFolderRequestedEvent()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeFolderPathDefinition(), svc);

        SettingDisplayItem? received = null;
        item.BrowseFolderRequested += s => received = s;
        item.BrowseFolderCommand.Execute(null);

        Assert.Same(item, received);
    }

    // ── Constructor validation ──

    /// <summary>
    /// Verifies constructor throws on null definition.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullDefinition()
    {
        var svc = CreateSettingsService();
        Assert.Throws<ArgumentNullException>(() => new SettingDisplayItem(null!, svc));
    }

    /// <summary>
    /// Verifies constructor throws on null settings service.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullSettingsService()
    {
        var def = MakeBooleanDefinition();
        Assert.Throws<ArgumentNullException>(() => new SettingDisplayItem(def, null!));
    }

    // ── Reload ──

    /// <summary>
    /// Verifies Reload refreshes value from getter without triggering saves.
    /// </summary>
    [Fact]
    public void Reload_RefreshesValueFromGetter()
    {
        int callCount = 0;
        var svc = CreateSettingsService();
        var def = MakeNumberDefinition(
            getter: _ => ++callCount * 10.0,
            setter: (_, _) => { });
        var item = new SettingDisplayItem(def, svc);

        // Initial load
        Assert.Equal("10", item.StringValue);

        item.Reload();
        Assert.Equal("20", item.StringValue);

        // Reload should not trigger QueueSave (only the initial constructor might,
        // but _suppressSave prevents it)
    }

    /// <summary>
    /// Verifies that Id returns the definition Key.
    /// </summary>
    [Fact]
    public void Id_ReturnsDefinitionKey()
    {
        var svc = CreateSettingsService();
        var def = MakeBooleanDefinition(key: "my.setting.key");
        var item = new SettingDisplayItem(def, svc);

        Assert.Equal("my.setting.key", item.Id);
    }

    /// <summary>
    /// Verifies AddListItem accepts any text (no URL validation).
    /// </summary>
    [Fact]
    public void AddListItem_AcceptsAnyText()
    {
        var svc = CreateSettingsService();
        var item = new SettingDisplayItem(MakeStringListDefinition(), svc);

        item.NewListItemText = "any arbitrary text";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal(string.Empty, item.ValidationError);
    }
}