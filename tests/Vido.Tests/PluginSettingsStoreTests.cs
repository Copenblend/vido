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

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public PluginSettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vido-settings-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _settingsFile = Path.Combine(_tempDir, "settings.json");
    }

    /// <summary>
    /// Cleans up test resources after each test run.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PluginSettingsStore CreateStore() => PluginSettingsStore.ForTesting(_settingsFile);

    /// <summary>
    /// Verifies that Get unset key returns default.
    /// </summary>
    [Fact]
    public void Get_UnsetKey_ReturnsDefault()
    {
        var store = CreateStore();

        Assert.Equal(42, store.Get("missing", 42));
        Assert.Equal("fallback", store.Get("missing", "fallback"));
        Assert.True(store.Get("missing", true));
    }

    /// <summary>
    /// Verifies that Set then get returns set value.
    /// </summary>
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

    /// <summary>
    /// Verifies that Set overwrites previous value.
    /// </summary>
    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        var store = CreateStore();

        store.Set("key", "old");
        store.Set("key", "new");

        Assert.Equal("new", store.Get("key", ""));
    }

    /// <summary>
    /// Verifies that Settings persisted to disk.
    /// </summary>
    [Fact]
    public void Settings_PersistedToDisk()
    {
        var store = CreateStore();
        store.Set("persistent", "value");
        store.Flush();

        // Create a fresh store instance pointing to the same file
        var store2 = PluginSettingsStore.ForTesting(_settingsFile);

        Assert.Equal("value", store2.Get("persistent", ""));
    }

    /// <summary>
    /// Verifies that Setting Changed fired on set.
    /// </summary>
    [Fact]
    public void SettingChanged_FiredOnSet()
    {
        var store = CreateStore();
        string? changedKey = null;
        store.SettingChanged += key => changedKey = key;

        store.Set("myKey", 42);

        Assert.Equal("myKey", changedKey);
    }

    /// <summary>
    /// Verifies that Get corrupted file returns default.
    /// </summary>
    [Fact]
    public void Get_CorruptedFile_ReturnsDefault()
    {
        // Write corrupt JSON
        File.WriteAllText(_settingsFile, "{{not valid json");

        var store = CreateStore();

        Assert.Equal("default", store.Get("any", "default"));
    }

    /// <summary>
    /// Verifies that Get complex type deserializes correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that Get type mismatch returns default.
    /// </summary>
    [Fact]
    public void Get_TypeMismatch_ReturnsDefault()
    {
        var store = CreateStore();

        store.Set("stringValue", "hello");

        // Try to read as int — should return default
        Assert.Equal(0, store.Get("stringValue", 0));
    }

    /// <summary>
    /// Verifies that Set creates directory if not exists.
    /// </summary>
    [Fact]
    public void Set_CreatesDirectoryIfNotExists()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "settings.json");
        var store = PluginSettingsStore.ForTesting(nestedPath);

        store.Set("key", "value");
        store.Flush();

        Assert.True(File.Exists(nestedPath));
    }

    /// <summary>
    /// Verifies that Empty Store no file created.
    /// </summary>
    [Fact]
    public void EmptyStore_NoFileCreated()
    {
        var path = Path.Combine(_tempDir, "notouch", "settings.json");
        _ = PluginSettingsStore.ForTesting(path);

        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// Verifies that Multiple Settings all persisted.
    /// </summary>
    [Fact]
    public void MultipleSettings_AllPersisted()
    {
        var store = CreateStore();

        store.Set("a", 1);
        store.Set("b", "two");
        store.Set("c", 3.14);
        store.Flush();

        var store2 = PluginSettingsStore.ForTesting(_settingsFile);

        Assert.Equal(1, store2.Get("a", 0));
        Assert.Equal("two", store2.Get("b", ""));
        Assert.Equal(3.14, store2.Get("c", 0.0));
    }

    // ── Reset / ResetAll ──

    /// <summary>
    /// Verifies that Reset existing key removes and returns true.
    /// </summary>
    [Fact]
    public void Reset_ExistingKey_RemovesAndReturnsTrue()
    {
        var store = CreateStore();
        store.Set("key", "value");

        var result = store.Reset("key");

        Assert.True(result);
        Assert.Equal("default", store.Get("key", "default"));
    }

    /// <summary>
    /// Verifies that Reset missing key returns false.
    /// </summary>
    [Fact]
    public void Reset_MissingKey_ReturnsFalse()
    {
        var store = CreateStore();

        Assert.False(store.Reset("nonexistent"));
    }

    /// <summary>
    /// Verifies that Reset fires setting changed for removed key.
    /// </summary>
    [Fact]
    public void Reset_FiresSettingChangedForRemovedKey()
    {
        var store = CreateStore();
        store.Set("key", 42);
        string? changedKey = null;
        store.SettingChanged += k => changedKey = k;

        store.Reset("key");

        Assert.Equal("key", changedKey);
    }

    /// <summary>
    /// Verifies that Reset does not fire setting changed for missing key.
    /// </summary>
    [Fact]
    public void Reset_DoesNotFireSettingChangedForMissingKey()
    {
        var store = CreateStore();
        var fired = false;
        store.SettingChanged += _ => fired = true;

        store.Reset("nonexistent");

        Assert.False(fired);
    }

    /// <summary>
    /// Verifies that Reset persists removal.
    /// </summary>
    [Fact]
    public void Reset_PersistsRemoval()
    {
        var store = CreateStore();
        store.Set("key", "value");
        store.Reset("key");
        store.Flush();

        var store2 = PluginSettingsStore.ForTesting(_settingsFile);
        Assert.Equal("default", store2.Get("key", "default"));
    }

    /// <summary>
    /// Verifies that Reset All clears all settings.
    /// </summary>
    [Fact]
    public void ResetAll_ClearsAllSettings()
    {
        var store = CreateStore();
        store.Set("a", 1);
        store.Set("b", "two");
        store.Set("c", true);

        store.ResetAll();

        Assert.Equal(0, store.Get("a", 0));
        Assert.Equal("", store.Get("b", ""));
        Assert.False(store.Get("c", false));
    }

    /// <summary>
    /// Verifies that Reset All fires setting changed for each key.
    /// </summary>
    [Fact]
    public void ResetAll_FiresSettingChangedForEachKey()
    {
        var store = CreateStore();
        store.Set("x", 1);
        store.Set("y", 2);
        var changedKeys = new List<string>();
        store.SettingChanged += k => changedKeys.Add(k);

        store.ResetAll();

        Assert.Equal(2, changedKeys.Count);
        Assert.Contains("x", changedKeys);
        Assert.Contains("y", changedKeys);
    }

    /// <summary>
    /// Verifies that Reset All persists empty store.
    /// </summary>
    [Fact]
    public void ResetAll_PersistsEmptyStore()
    {
        var store = CreateStore();
        store.Set("key", "val");
        store.ResetAll();
        store.Flush();

        var store2 = PluginSettingsStore.ForTesting(_settingsFile);
        Assert.Equal("default", store2.Get("key", "default"));
    }

    /// <summary>
    /// Verifies that Multiple Set calls are coalesced into one debounced save.
    /// </summary>
    [Fact]
    public async Task Set_MultipleTimes_CoalescesToSingleDebouncedSave()
    {
        var saveCount = 0;
        var store = PluginSettingsStore.ForTesting(_settingsFile, debounceMs: 50, onSave: () => saveCount++);

        for (int i = 0; i < 10; i++)
            store.Set("key", i);

        await Task.Delay(200);

        Assert.Equal(1, saveCount);
    }

    /// <summary>
    /// Verifies that Flush writes pending values immediately.
    /// </summary>
    [Fact]
    public void Flush_WritesPendingValuesImmediately()
    {
        var store = CreateStore();
        store.Set("flush-key", "flush-value");

        store.Flush();

        var store2 = PluginSettingsStore.ForTesting(_settingsFile);
        Assert.Equal("flush-value", store2.Get("flush-key", string.Empty));
    }

    /// <summary>
    /// Verifies that Dispose flushes pending changes.
    /// </summary>
    [Fact]
    public void Dispose_FlushesPendingValues()
    {
        var store = CreateStore();
        store.Set("dispose-key", "dispose-value");

        store.Dispose();

        var store2 = PluginSettingsStore.ForTesting(_settingsFile);
        Assert.Equal("dispose-value", store2.Get("dispose-key", string.Empty));
    }
}