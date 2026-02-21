using Vido.PluginHost;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginSettingsStore"/> — per-plugin JSON settings persistence.
/// </summary>
public class PluginSettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsFile;

    public PluginSettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-settings-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _settingsFile = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PluginSettingsStore CreateStore() => new("test-plugin", _settingsFile);

    [Fact]
    public void Get_UnsetKey_ReturnsDefault()
    {
        var store = CreateStore();

        Assert.Equal(42, store.Get("missing", 42));
        Assert.Equal("fallback", store.Get("missing", "fallback"));
        Assert.True(store.Get("missing", true));
    }

    [Fact]
    public void Set_ThenGet_ReturnsSetValue()
    {
        var store = CreateStore();

        store.Set("key1", "hello");
        store.Set("key2", 99);
        store.Set("key3", true);

        Assert.Equal("hello", store.Get("key1", ""));
        Assert.Equal(99, store.Get("key2", 0));
        Assert.True(store.Get("key3", false));
    }

    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        var store = CreateStore();

        store.Set("key", "old");
        store.Set("key", "new");

        Assert.Equal("new", store.Get("key", ""));
    }

    [Fact]
    public void Settings_PersistedToDisk()
    {
        var store = CreateStore();
        store.Set("persistent", "value");

        // Create a fresh store instance pointing to the same file
        var store2 = new PluginSettingsStore("test-plugin", _settingsFile);

        Assert.Equal("value", store2.Get("persistent", ""));
    }

    [Fact]
    public void SettingChanged_FiredOnSet()
    {
        var store = CreateStore();
        string? changedKey = null;
        store.SettingChanged += key => changedKey = key;

        store.Set("myKey", 42);

        Assert.Equal("myKey", changedKey);
    }

    [Fact]
    public void Get_CorruptedFile_ReturnsDefault()
    {
        // Write corrupt JSON
        File.WriteAllText(_settingsFile, "{{not valid json");

        var store = CreateStore();

        Assert.Equal("default", store.Get("any", "default"));
    }

    [Fact]
    public void Get_ComplexType_DeserializesCorrectly()
    {
        var store = CreateStore();
        var list = new List<string> { "a", "b", "c" };

        store.Set("list", list);
        var result = store.Get<List<string>>("list", []);

        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("c", result[2]);
    }

    [Fact]
    public void Get_TypeMismatch_ReturnsDefault()
    {
        var store = CreateStore();

        store.Set("stringValue", "hello");

        // Try to read as int — should return default
        Assert.Equal(0, store.Get("stringValue", 0));
    }

    [Fact]
    public void Set_CreatesDirectoryIfNotExists()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "settings.json");
        var store = new PluginSettingsStore("test", nestedPath);

        store.Set("key", "value");

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void EmptyStore_NoFileCreated()
    {
        var path = Path.Combine(_tempDir, "notouch", "settings.json");
        _ = new PluginSettingsStore("test", path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MultipleSettings_AllPersisted()
    {
        var store = CreateStore();

        store.Set("a", 1);
        store.Set("b", "two");
        store.Set("c", 3.14);

        var store2 = new PluginSettingsStore("test", _settingsFile);

        Assert.Equal(1, store2.Get("a", 0));
        Assert.Equal("two", store2.Get("b", ""));
        Assert.Equal(3.14, store2.Get("c", 0.0));
    }
}
