using NSubstitute;
using Vido.Core.Settings;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="SettingDisplayItem"/>, focusing on the stringList type
/// used for multi-value settings like plugin registry URLs.
/// </summary>
public sealed class SettingDisplayItemTests
{
    private static SettingContribution MakeStringListContribution(string id = "test.list") =>
        new() { Id = id, Type = "stringList", Title = "Test List", Description = "A list setting" };

    private static ISettingsStore CreateStore(List<string>? initialList = null)
    {
        var store = Substitute.For<ISettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(initialList ?? []);
        return store;
    }

    /// <summary>
    /// Verifies that Is String List true for string list type.
    /// </summary>
    [Fact]
    public void IsStringList_TrueForStringListType()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);
        Assert.True(item.IsStringList);
    }

    /// <summary>
    /// Verifies that Is String List false for other types.
    /// </summary>
    [Fact]
    public void IsStringList_FalseForOtherTypes()
    {
        var store = CreateStore();
        var def = new SettingContribution { Id = "test", Type = "string", Title = "T", Description = "D" };
        var item = new SettingDisplayItem(def, store);
        Assert.False(item.IsStringList);
    }

    /// <summary>
    /// Verifies that Constructor loads existing list items.
    /// </summary>
    [Fact]
    public void Constructor_LoadsExistingListItems()
    {
        var store = CreateStore(["https://one.com", "https://two.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        Assert.Equal(2, item.ListItems.Count);
        Assert.Equal("https://one.com", item.ListItems[0]);
        Assert.Equal("https://two.com", item.ListItems[1]);
    }

    /// <summary>
    /// Verifies that Add List Item adds to collection.
    /// </summary>
    [Fact]
    public void AddListItem_AddsToCollection()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal("https://new.com", item.ListItems[0]);
    }

    /// <summary>
    /// Verifies that Add List Item trims whitespace.
    /// </summary>
    [Fact]
    public void AddListItem_TrimsWhitespace()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "  https://new.com  ";
        item.AddListItemCommand.Execute(null);

        Assert.Equal("https://new.com", item.ListItems[0]);
    }

    /// <summary>
    /// Verifies that Add List Item clears new list item text.
    /// </summary>
    [Fact]
    public void AddListItem_ClearsNewListItemText()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        Assert.Equal(string.Empty, item.NewListItemText);
    }

    /// <summary>
    /// Verifies that Add List Item ignores empty text.
    /// </summary>
    [Fact]
    public void AddListItem_IgnoresEmptyText()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "   ";
        item.AddListItemCommand.Execute(null);

        Assert.Empty(item.ListItems);
    }

    /// <summary>
    /// Verifies that Add List Item ignores duplicates.
    /// </summary>
    [Fact]
    public void AddListItem_IgnoresDuplicates()
    {
        var store = CreateStore(["https://existing.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "https://existing.com";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
    }

    /// <summary>
    /// Verifies that Add List Item persists to store.
    /// </summary>
    [Fact]
    public void AddListItem_PersistsToStore()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution("test.list"), store);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        store.Received().Set("test.list", Arg.Is<List<string>>(l => l.Count == 1 && l[0] == "https://new.com"));
    }

    /// <summary>
    /// Verifies that Remove List Item removes from collection.
    /// </summary>
    [Fact]
    public void RemoveListItem_RemovesFromCollection()
    {
        var store = CreateStore(["https://one.com", "https://two.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.RemoveListItemCommand.Execute("https://one.com");

        Assert.Single(item.ListItems);
        Assert.Equal("https://two.com", item.ListItems[0]);
    }

    /// <summary>
    /// Verifies that Remove List Item persists to store.
    /// </summary>
    [Fact]
    public void RemoveListItem_PersistsToStore()
    {
        var store = CreateStore(["https://one.com", "https://two.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution("test.list"), store);

        item.RemoveListItemCommand.Execute("https://one.com");

        store.Received().Set("test.list", Arg.Is<List<string>>(l => l.Count == 1 && l[0] == "https://two.com"));
    }

    /// <summary>
    /// Verifies that Remove List Item nonexistent item does nothing.
    /// </summary>
    [Fact]
    public void RemoveListItem_NonexistentItem_DoesNothing()
    {
        var store = CreateStore(["https://one.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.RemoveListItemCommand.Execute("https://nonexistent.com");

        // List unchanged, no store call for removal
        Assert.Single(item.ListItems);
    }

    /// <summary>
    /// Verifies that Other Type Properties false for string list.
    /// </summary>
    [Fact]
    public void OtherTypeProperties_FalseForStringList()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        Assert.False(item.IsBoolean);
        Assert.False(item.IsString);
        Assert.False(item.IsNumber);
        Assert.False(item.IsEnum);
    }

    // â”€â”€ URL validation tests â”€â”€

    private static SettingContribution MakeUrlValidatedContribution(string id = "test.urls") =>
        new() { Id = id, Type = "stringList", Title = "URLs", Description = "URL list", Validation = "url" };

    /// <summary>
    /// Verifies that Add List Item with url validation accepts https url.
    /// </summary>
    [Fact]
    public void AddListItem_WithUrlValidation_AcceptsHttpsUrl()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeUrlValidatedContribution(), store);

        item.NewListItemText = "https://example.com/plugins.json";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal(string.Empty, item.ValidationError);
    }

    /// <summary>
    /// Verifies that Add List Item with url validation accepts file url.
    /// </summary>
    [Fact]
    public void AddListItem_WithUrlValidation_AcceptsFileUrl()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeUrlValidatedContribution(), store);

        item.NewListItemText = "file:///C:/local/registry.json";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal(string.Empty, item.ValidationError);
    }

    /// <summary>
    /// Verifies that Add List Item with url validation rejects plain text.
    /// </summary>
    [Fact]
    public void AddListItem_WithUrlValidation_RejectsPlainText()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeUrlValidatedContribution(), store);

        item.NewListItemText = "sdfsfsdfsdfs";
        item.AddListItemCommand.Execute(null);

        Assert.Empty(item.ListItems);
        Assert.NotEmpty(item.ValidationError);
    }

    /// <summary>
    /// Verifies that Add List Item with url validation rejects http url.
    /// </summary>
    [Fact]
    public void AddListItem_WithUrlValidation_RejectsHttpUrl()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeUrlValidatedContribution(), store);

        item.NewListItemText = "http://insecure.com/registry.json";
        item.AddListItemCommand.Execute(null);

        Assert.Empty(item.ListItems);
        Assert.NotEmpty(item.ValidationError);
    }

    /// <summary>
    /// Verifies that Add List Item with url validation rejects ftp url.
    /// </summary>
    [Fact]
    public void AddListItem_WithUrlValidation_RejectsFtpUrl()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeUrlValidatedContribution(), store);

        item.NewListItemText = "ftp://files.example.com/registry.json";
        item.AddListItemCommand.Execute(null);

        Assert.Empty(item.ListItems);
        Assert.NotEmpty(item.ValidationError);
    }

    /// <summary>
    /// Verifies that Add List Item with url validation clears error on success.
    /// </summary>
    [Fact]
    public void AddListItem_WithUrlValidation_ClearsErrorOnSuccess()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeUrlValidatedContribution(), store);

        // First add invalid text
        item.NewListItemText = "not-a-url";
        item.AddListItemCommand.Execute(null);
        Assert.NotEmpty(item.ValidationError);

        // Then add a valid URL
        item.NewListItemText = "https://valid.com/registry.json";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal(string.Empty, item.ValidationError);
    }

    /// <summary>
    /// Verifies that Add List Item without validation accepts any text.
    /// </summary>
    [Fact]
    public void AddListItem_WithoutValidation_AcceptsAnyText()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "any arbitrary text";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
        Assert.Equal(string.Empty, item.ValidationError);
    }

    // â”€â”€ FolderPath type â”€â”€

    /// <summary>
    /// Verifies that Is Folder Path true for folder path type.
    /// </summary>
    [Fact]
    public void IsFolderPath_TrueForFolderPathType()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);
        var def = new SettingContribution { Id = "test.folder", Type = "folderPath", Title = "Dir", Description = "D" };
        var item = new SettingDisplayItem(def, store);
        Assert.True(item.IsFolderPath);
        Assert.False(item.IsString);
    }

    /// <summary>
    /// Verifies that Is Folder Path false for string type.
    /// </summary>
    [Fact]
    public void IsFolderPath_FalseForStringType()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);
        var def = new SettingContribution { Id = "test.str", Type = "string", Title = "T", Description = "D" };
        var item = new SettingDisplayItem(def, store);
        Assert.False(item.IsFolderPath);
        Assert.True(item.IsString);
    }

    /// <summary>
    /// Verifies that Set Folder Path updates string value.
    /// </summary>
    [Fact]
    public void SetFolderPath_UpdatesStringValue()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);
        var def = new SettingContribution { Id = "test.folder", Type = "folderPath", Title = "Dir", Description = "D" };
        var item = new SettingDisplayItem(def, store);

        item.SetFolderPath(@"C:\Users\test\Screenshots");
        Assert.Equal(@"C:\Users\test\Screenshots", item.StringValue);
        store.Received().Set("test.folder", @"C:\Users\test\Screenshots");
    }

    /// <summary>
    /// Verifies that Set Folder Path null does nothing.
    /// </summary>
    [Fact]
    public void SetFolderPath_NullDoesNothing()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(@"C:\existing");
        var def = new SettingContribution { Id = "test.folder", Type = "folderPath", Title = "Dir", Description = "D" };
        var item = new SettingDisplayItem(def, store);

        item.SetFolderPath(null);
        Assert.Equal(@"C:\existing", item.StringValue);
    }

    /// <summary>
    /// Verifies that Browse Folder raises browse folder requested event.
    /// </summary>
    [Fact]
    public void BrowseFolder_RaisesBrowseFolderRequestedEvent()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);
        var def = new SettingContribution { Id = "test.folder", Type = "folderPath", Title = "Dir", Description = "D" };
        var item = new SettingDisplayItem(def, store);

        SettingDisplayItem? received = null;
        item.BrowseFolderRequested += s => received = s;
        item.BrowseFolderCommand.Execute(null);

        Assert.Same(item, received);
    }
}