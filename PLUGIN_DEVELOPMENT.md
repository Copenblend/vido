# Vido Plugin Development Guide

This guide covers everything you need to create, test, and distribute Vido plugins.

## Overview

A Vido plugin is a .NET 8 class library that implements `IVidoPlugin` and declares its capabilities in a `plugin.json` manifest. Plugins can contribute UI elements, respond to events, handle files, and add keyboard shortcuts.

### Extension Points

| Extension Point | Description |
|----------------|-------------|
| Sidebar Panel | Activity bar icon + sidebar content panel |
| Bottom Panel Tab | Tab in the bottom panel (alongside Output) |
| Right Panel Tab | Tab in the right panel (alongside Video Info) |
| Status Bar Item | Left or right-aligned item in the status bar |
| Toolbar Button | Button in the title bar area (right of menu) |
| Context Menu Item | Right-click menu entry on files/folders |
| File Handler | Double-click handler for specific file extensions |
| File Icon | Custom icon for specific file extensions |
| Keyboard Shortcut | Custom key binding for a plugin command |
| Settings | Plugin-specific settings shown in the Settings page |

---

## Quick Start

### 1. Create the Project

```powershell
dotnet new classlib -n MyVidoPlugin -f net8.0
cd MyVidoPlugin
```

Add a reference to `Vido.Core.dll` (copy from the Vido build output):

```xml
<!-- MyVidoPlugin.csproj -->
<ItemGroup>
  <Reference Include="Vido.Core">
    <HintPath>..\path\to\Vido.Core.dll</HintPath>
  </Reference>
</ItemGroup>
```

### 2. Implement the Plugin

```csharp
using Vido.Core.Plugin;

namespace MyVidoPlugin;

public class MyPlugin : IVidoPlugin
{
    private IPluginContext? _context;

    public void Activate(IPluginContext context)
    {
        _context = context;
        context.Logger.Info("MyPlugin activated!", "MyPlugin");

        // Subscribe to events
        context.Events.Subscribe<Vido.Core.Events.VideoLoadedEvent>(OnVideoLoaded);

        // Register UI contributions (declared in plugin.json)
        context.RegisterBottomPanel("my-panel", () => new MyPanelControl());
    }

    public void Deactivate()
    {
        _context?.Logger.Info("MyPlugin deactivated", "MyPlugin");
    }

    private void OnVideoLoaded(Vido.Core.Events.VideoLoadedEvent e)
    {
        _context?.Logger.Info($"Video loaded: {e.FilePath}", "MyPlugin");
    }
}
```

### 3. Create the Manifest

Create `plugin.json` in your output directory:

```json
{
  "id": "com.example.my-plugin",
  "name": "my-plugin",
  "displayName": "My Plugin",
  "version": "1.0.0",
  "description": "A sample Vido plugin",
  "author": "Your Name",
  "license": "MIT",
  "entryPoint": "MyVidoPlugin.dll",
  "pluginClass": "MyVidoPlugin.MyPlugin",
  "minVidoVersion": "0.1.0",
  "tags": ["example"],
  "contributes": {
    "bottomPanel": [
      {
        "id": "my-panel",
        "title": "My Panel",
        "order": 200
      }
    ]
  }
}
```

### 4. Install Locally

```powershell
# Build the plugin
dotnet build -c Release

# Copy to Vido plugins directory
$dest = "$env:APPDATA\Vido\plugins\com.example.my-plugin"
New-Item -ItemType Directory -Force $dest
Copy-Item bin\Release\net8.0\* $dest -Recurse
Copy-Item plugin.json $dest
```

Launch Vido — the plugin loads automatically on startup.

---

## Plugin Manifest Reference (`plugin.json`)

### Root Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | string | Yes | Unique identifier (reverse-domain, e.g. `com.author.name`) |
| `name` | string | Yes | Internal name (no spaces) |
| `displayName` | string | Yes | User-facing display name |
| `version` | string | Yes | Semantic version (e.g. `1.0.0`) |
| `description` | string | Yes | Short description |
| `author` | string | Yes | Author name |
| `license` | string | No | License identifier (e.g. `MIT`) |
| `entryPoint` | string | Yes | Relative path to plugin DLL |
| `pluginClass` | string | Yes | Fully-qualified class name implementing `IVidoPlugin` |
| `minVidoVersion` | string | No | Minimum Vido version required |
| `repository` | string | No | Source code URL |
| `tags` | string[] | No | Search/categorization tags |
| `contributes` | object | No | UI contributions (see below) |

### Contributions

#### `contributes.sidebar`

```json
{
  "id": "my-sidebar",
  "title": "My Panel",
  "icon": "Resources/icon.png",
  "order": 200
}
```

- `icon`: Path relative to plugin directory (24x24 recommended, auto-scaled)
- `order`: Sort priority (lower = earlier, default 100)

#### `contributes.bottomPanel` / `contributes.rightPanel`

```json
{
  "id": "my-tab",
  "title": "Tab Title",
  "order": 200
}
```

#### `contributes.statusBar`

```json
{
  "id": "my-status",
  "name": "Status Name",
  "position": "right",
  "order": 500
}
```

- `position`: `"left"` or `"right"` (default `"right"`)

#### `contributes.toolbarButtons`

```json
{
  "id": "my-button",
  "tooltip": "Click me",
  "icon": "Resources/btn-icon.png",
  "order": 100
}
```

#### `contributes.contextMenu`

```json
{
  "id": "my-action",
  "label": "Do Something",
  "fileExtensions": [".mp4", ".mkv"],
  "order": 100
}
```

- `fileExtensions`: Empty array = all files

#### `contributes.fileHandlers`

```json
{
  "extensions": [".funscript"],
  "action": "open"
}
```

#### `contributes.fileIcons`

```json
{
  ".funscript": "Resources/funscript-icon.png"
}
```

- Icons should be 16x16 (auto-scaled if larger)

#### `contributes.settings`

```json
{
  "id": "my-setting",
  "type": "enum",
  "default": "option1",
  "title": "My Setting",
  "description": "Choose a value",
  "enumValues": ["option1", "option2", "option3"],
  "section": "General",
  "forceOverride": false
}
```

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | Unique setting key |
| `type` | string | `"boolean"`, `"string"`, `"number"`, or `"enum"` |
| `default` | any | Default value |
| `title` | string | Display label |
| `description` | string | Help text shown below the control |
| `enumValues` | string[] | Required when `type` is `"enum"` |
| `section` | string? | Optional grouping header |
| `forceOverride` | bool | If `true`, default overwrites user value on every load |

---

## Plugin API Reference

### `IVidoPlugin`

```csharp
public interface IVidoPlugin
{
    void Activate(IPluginContext context);
    void Deactivate();
}
```

### `IPluginContext`

Provided to `Activate()`. All plugin interaction happens through this interface.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Manifest` | `PluginManifest` | The plugin's parsed manifest |
| `PluginDirectory` | `string` | Absolute path to plugin install directory |
| `Events` | `IEventBus` | Subscribe to and publish application events |
| `VideoEngine` | `IVideoEngine` | Control video playback |
| `Logger` | `ILogService` | Write to the Output log |
| `Settings` | `IPluginSettingsStore` | Read/write plugin settings |

#### Registration Methods

All registration methods validate inputs and throw `ArgumentException` for invalid parameters.

```csharp
// UI panels — contributionId must match an entry in plugin.json contributes
void RegisterSidebarPanel(string contributionId, Func<object> viewFactory);
void RegisterBottomPanel(string contributionId, Func<object> viewFactory);
void RegisterRightPanel(string contributionId, Func<object> viewFactory);
void RegisterStatusBarItem(string contributionId, Func<object> viewFactory);

// Toolbar buttons
void RegisterToolbarButtonHandler(string contributionId, Action clickHandler);

// Context menus
void RegisterContextMenuHandler(string contributionId, Action<FileNode> handler);

// File handling
void RegisterFileHandler(string[] extensions, Action<FileNode> handler);
void RegisterFileIcons(Dictionary<string, string> extensionToIconPath);

// Keyboard shortcuts
void RegisterKeyBinding(KeyBinding binding, Action handler);
```

View factories must return a WPF `FrameworkElement` (cast as `object` since `Vido.Core` is platform-agnostic). If a factory throws, a fallback error placeholder is shown.

### `IEventBus`

```csharp
// Subscribe — returns IDisposable for unsubscription
IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

// Publish
void Publish<TEvent>(TEvent eventData) where TEvent : class;
```

#### Available Events

| Event | Properties | When |
|-------|-----------|------|
| `VideoLoadedEvent` | `FilePath`, `Metadata` | Video file opened |
| `VideoUnloadedEvent` | — | Video closed |
| `PlaybackStateChangedEvent` | `State` | Play/Pause/Stop |
| `PlaybackPositionChangedEvent` | `Position`, `Duration` | ~60Hz during playback |
| `VolumeChangedEvent` | `Volume`, `IsMuted` | Volume or mute changed |
| `FileExplorerFolderOpenedEvent` | `FolderPath` | Folder opened in explorer |
| `FileExplorerFolderClosedEvent` | — | Folder closed |
| `FileExplorerSelectionChangedEvent` | `SelectedNode` | Explorer selection changed |
| `FileDoubleClickedEvent` | `Node` | File double-clicked in explorer |
| `PluginLoadedEvent` | `PluginId` | Plugin activated |
| `PluginUnloadedEvent` | `PluginId` | Plugin deactivated |

### `IVideoEngine`

```csharp
// Playback control
Task LoadAsync(string filePath);
void Play();
void Pause();
void Stop();
void Seek(TimeSpan position);

// State
TimeSpan Position { get; }
TimeSpan Duration { get; }
double Volume { get; set; }       // 0.0–1.0
bool IsMuted { get; set; }
bool IsLooping { get; set; }
double SpeedRatio { get; set; }   // 0.25–4.0
PlaybackState State { get; }
VideoMetadata? CurrentMetadata { get; }

// Events
event Action<TimeSpan>? PositionChanged;  // ~60Hz
event Action<PlaybackState>? StateChanged;
event Action<FrameData>? FrameReady;
event Action? MediaEnded;
```

### `IPluginSettingsStore`

```csharp
T Get<T>(string key, T defaultValue);
void Set<T>(string key, T value);
bool Reset(string key);    // Remove single setting
void ResetAll();            // Remove all settings

event Action<string>? SettingChanged;  // Fires with the key that changed
```

Settings are persisted to `%APPDATA%/Vido/plugins/<plugin-id>/settings.json`.

---

## Examples

### Subscribing to Video Events

```csharp
public void Activate(IPluginContext context)
{
    context.Events.Subscribe<PlaybackPositionChangedEvent>(e =>
    {
        // Called ~60 times per second during playback
        var position = e.Position;
        var duration = e.Duration;
    });

    context.Events.Subscribe<PlaybackStateChangedEvent>(e =>
    {
        if (e.State == PlaybackState.Playing)
            context.Logger.Info("Video started playing", "MyPlugin");
    });
}
```

### Adding a Bottom Panel

```json
// plugin.json
{
  "contributes": {
    "bottomPanel": [{ "id": "my-view", "title": "My View" }]
  }
}
```

```csharp
// Plugin code
public void Activate(IPluginContext context)
{
    context.RegisterBottomPanel("my-view", () =>
    {
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = "Hello from my plugin!",
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new System.Windows.Thickness(8)
        };
        return textBlock;
    });
}
```

### Adding Context Menu Items

```json
// plugin.json
{
  "contributes": {
    "contextMenu": [{
      "id": "analyze-file",
      "label": "Analyze File",
      "fileExtensions": [".mp4", ".mkv"],
      "order": 200
    }]
  }
}
```

```csharp
// Plugin code
public void Activate(IPluginContext context)
{
    context.RegisterContextMenuHandler("analyze-file", node =>
    {
        context.Logger.Info($"Analyzing: {node.FullPath}", "MyPlugin");
    });
}
```

### Reading and Writing Settings

```json
// plugin.json
{
  "contributes": {
    "settings": [
      {
        "id": "port",
        "type": "string",
        "default": "COM3",
        "title": "Serial Port",
        "description": "COM port for device connection"
      },
      {
        "id": "enabled",
        "type": "boolean",
        "default": true,
        "title": "Auto-Connect"
      }
    ]
  }
}
```

```csharp
public void Activate(IPluginContext context)
{
    var port = context.Settings.Get("port", "COM3");
    var enabled = context.Settings.Get("enabled", true);

    context.Settings.SettingChanged += key =>
    {
        if (key == "port")
        {
            var newPort = context.Settings.Get("port", "COM3");
            context.Logger.Info($"Port changed to {newPort}", "MyPlugin");
        }
    };
}
```

### Registering Keyboard Shortcuts

```csharp
public void Activate(IPluginContext context)
{
    context.RegisterKeyBinding(
        new Vido.Core.Keyboard.KeyBinding("T", ctrl: true),
        () => context.Logger.Info("Ctrl+T pressed!", "MyPlugin")
    );
}
```

---

## Plugin Isolation & Safety

- All plugin code (`Activate`, `Deactivate`, event handlers, view factories) is wrapped in try-catch by the host
- A failing plugin is set to `Error` state and does not crash the application
- If a view factory throws, a "Plugin error" placeholder is shown in the UI slot
- Plugins cannot modify the host's DI container, replace core services, or alter base UI structure
- Icons provided by plugins are automatically scaled to the correct size (24x24 sidebar, 16x16 file explorer)

## Plugin Registries

Vido supports multiple plugin registries:

- **Official registry** — always present, cannot be removed
- **Custom registries** — any HTTP(S) URL serving a `registry.json`
- **Local registries** — `file://` paths for development testing

Configure registries in Settings > Plugins > Custom Plugin Registry URLs.

### Registry Format

```json
{
  "schemaVersion": 1,
  "plugins": [
    {
      "id": "com.example.my-plugin",
      "displayName": "My Plugin",
      "description": "...",
      "author": "Your Name",
      "version": "1.0.0",
      "downloadUrl": "https://github.com/.../releases/download/v1.0.0/my-plugin.zip",
      "minVidoVersion": "0.1.0",
      "tags": ["example"],
      "verified": false
    }
  ]
}
```

## Distribution

1. Build your plugin in Release mode
2. Create a zip containing: `plugin.json`, your DLL(s), and any resources
3. Publish as a GitHub release
4. Add an entry to a registry `registry.json` pointing to the zip download URL
5. Users can install via Vido's Plugin Manager or by manually extracting to `%APPDATA%/Vido/plugins/`
