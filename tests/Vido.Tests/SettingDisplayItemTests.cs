using NSubstitute;
using Vido.Core.Plugin;
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

    private static IPluginSettingsStore CreateStore(List<string>? initialList = null)
    {
        var store = Substitute.For<IPluginSettingsStore>();
        store.Get(Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns(initialList ?? []);
        return store;
    }

    [Fact]
    public void IsStringList_TrueForStringListType()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);
        Assert.True(item.IsStringList);
    }

    [Fact]
    public void IsStringList_FalseForOtherTypes()
    {
        var store = CreateStore();
        var def = new SettingContribution { Id = "test", Type = "string", Title = "T", Description = "D" };
        var item = new SettingDisplayItem(def, store);
        Assert.False(item.IsStringList);
    }

    [Fact]
    public void Constructor_LoadsExistingListItems()
    {
        var store = CreateStore(["https://one.com", "https://two.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        Assert.Equal(2, item.ListItems.Count);
        Assert.Equal("https://one.com", item.ListItems[0]);
        Assert.Equal("https://two.com", item.ListItems[1]);
    }

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

    [Fact]
    public void AddListItem_TrimsWhitespace()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "  https://new.com  ";
        item.AddListItemCommand.Execute(null);

        Assert.Equal("https://new.com", item.ListItems[0]);
    }

    [Fact]
    public void AddListItem_ClearsNewListItemText()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        Assert.Equal(string.Empty, item.NewListItemText);
    }

    [Fact]
    public void AddListItem_IgnoresEmptyText()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "   ";
        item.AddListItemCommand.Execute(null);

        Assert.Empty(item.ListItems);
    }

    [Fact]
    public void AddListItem_IgnoresDuplicates()
    {
        var store = CreateStore(["https://existing.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.NewListItemText = "https://existing.com";
        item.AddListItemCommand.Execute(null);

        Assert.Single(item.ListItems);
    }

    [Fact]
    public void AddListItem_PersistsToStore()
    {
        var store = CreateStore();
        var item = new SettingDisplayItem(MakeStringListContribution("test.list"), store);

        item.NewListItemText = "https://new.com";
        item.AddListItemCommand.Execute(null);

        store.Received().Set("test.list", Arg.Is<List<string>>(l => l.Count == 1 && l[0] == "https://new.com"));
    }

    [Fact]
    public void RemoveListItem_RemovesFromCollection()
    {
        var store = CreateStore(["https://one.com", "https://two.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.RemoveListItemCommand.Execute("https://one.com");

        Assert.Single(item.ListItems);
        Assert.Equal("https://two.com", item.ListItems[0]);
    }

    [Fact]
    public void RemoveListItem_PersistsToStore()
    {
        var store = CreateStore(["https://one.com", "https://two.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution("test.list"), store);

        item.RemoveListItemCommand.Execute("https://one.com");

        store.Received().Set("test.list", Arg.Is<List<string>>(l => l.Count == 1 && l[0] == "https://two.com"));
    }

    [Fact]
    public void RemoveListItem_NonexistentItem_DoesNothing()
    {
        var store = CreateStore(["https://one.com"]);
        var item = new SettingDisplayItem(MakeStringListContribution(), store);

        item.RemoveListItemCommand.Execute("https://nonexistent.com");

        // List unchanged, no store call for removal
        Assert.Single(item.ListItems);
    }

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

    // ── URL validation tests ──

    private static SettingContribution MakeUrlValidatedContribution(string id = "test.urls") =>
        new() { Id = id, Type = "stringList", Title = "URLs", Description = "URL list", Validation = "url" };

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
}
