# Vido — Implementation Plan

> **Target Developer:** Claude Opus 4.6 (AI developer agent)
> **Technology Stack:** WPF / .NET 8 / C# — Windows only
> **Video Engine:** FFmpeg via FFmpeg.AutoGen (native P/Invoke bindings)
> **Architecture:** MVVM with Dependency Injection, modular plugin system
> **Design:** VS Code Dark Modern visual clone

---

## Table of Contents

1. [Decisions Summary](#1-decisions-summary)
2. [Architecture Overview](#2-architecture-overview)
3. [Project Structure](#3-project-structure)
4. [Design System — VS Code Dark Modern](#4-design-system--vs-code-dark-modern)
5. [Plugin System Architecture](#5-plugin-system-architecture)
6. [Plugin API Specification](#6-plugin-api-specification)
7. [Plugin Distribution & Registry](#7-plugin-distribution--registry)
8. [Ticket Breakdown](#8-ticket-breakdown)
9. [Developer Instructions](#9-developer-instructions)

---

## 1. Decisions Summary

All decisions from the requirements clarification process:

| # | Question | Decision |
|---|----------|----------|
| Q1 | Tech Stack | **WPF / WinUI 3 / C# / .NET 8** — Windows only, ultra-performant |
| Q2 | Platform | **Windows only** |
| Q3 | Video Engine | **FFmpeg via FFmpeg.AutoGen** (best .NET/Windows native option) |
| Q4 | Title Bar | **Custom frameless window** matching VS Code exactly |
| Q5 | Skip Forward/Backward | **Skip to next/previous video** in folder (alphanumerically), NOT time-skip within video |
| Q6 | Fullscreen | **Yes** — F11 or double-click, fade-in overlay controls on mouse move |
| Q7 | Multiple Videos | **Single video tab** — always leftmost, cannot be closed. Other tabs (settings, etc.) overlay it |
| Q8 | Playlists | **Plugin only** — not in base player |
| Q9 | Subtitles | **Plugin only** — not in base player |
| Q10 | Drag & Drop | **Yes** — drag files onto window to open/play |
| Q11 | Audio Files | **No** — video only in base player |
| Q12 | Themes | **Dark Modern only** — extensible via design tokens for future plugin themes |
| Q13 | State Persistence | **Full** — window geometry, open folder, panel layout, volume, last video + position |
| Q14 | Keyboard Shortcuts | **Comprehensive set** with architecture for future customization |
| Q15 | Plugin Distribution | **GitHub JSON registry** — `registry.json` in a public repo, plugins as GitHub releases |
| Q16 | Plugin Security | **Full access** (like VS Code extensions) — no sandboxing in v1 |
| Q17 | File Explorer | **Single folder** at a time |
| Q18 | Non-Video Files | **Show all files** — generic icon for non-video. Tooltip on hover/double-click for unsupported. Extensible file handlers via plugins. Context menu on all files with at minimum "Remove" (hides from explorer, does NOT delete from disk) |
| Q19 | Distribution | **Both** portable zip AND installer (MSI via WiX) |
| Q20 | App Auto-Update | **No** — manual updates only for v1 |
| Q21 | Testing | **Unit tests for core logic** — plugin system, event bus, state management. Tests written per ticket including regression |
| Q22 | TCode Plugin | Future plugin adding bottom tab (funscript viewer), side panel (device controls), context menu items, COM/UDP serial communication. Plugin API must accommodate: custom bottom tabs, custom side panels, custom context menu items, video position events, file association handlers |
| Q23 | File Associations | **Yes** — optionally during install |
| Q24 | Status Bar | **Filename, resolution, duration, codec** — extensible via plugin API |
| Q25 | Top Menu | **File, Edit, View, Playback, Help** — plugins can add top-level buttons (like VS Code Chat button) but NOT modify existing menus |

---

## 2. Architecture Overview

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Vido.App                             │
│  (WPF Application — Entry point, DI container, App.xaml)    │
├─────────────────────────────────────────────────────────────┤
│                      Vido.Core                              │
│  (Interfaces, Models, Events, Plugin API contracts)         │
├─────────────────────────────────────────────────────────────┤
│                    Vido.Services                            │
│  (Implementations: Video, FileSystem, Settings, Plugins)    │
├─────────────────────────────────────────────────────────────┤
│                   Vido.ViewModels                           │
│  (MVVM ViewModels for all views)                            │
├─────────────────────────────────────────────────────────────┤
│                     Vido.Views                              │
│  (WPF XAML views, themes, styles, controls)                 │
├─────────────────────────────────────────────────────────────┤
│                   Vido.PluginHost                           │
│  (Plugin loading, lifecycle, API bridge)                    │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Project Assemblies

| Assembly | Purpose | Dependencies |
|----------|---------|--------------|
| **Vido.Core** | Interfaces, models, events, plugin API contracts. Zero external dependencies. This is the contract layer. | None |
| **Vido.Services** | Service implementations (video engine, file system, settings, state persistence, keyboard shortcuts). | Vido.Core, FFmpeg.AutoGen |
| **Vido.ViewModels** | All MVVM ViewModels. | Vido.Core, CommunityToolkit.Mvvm |
| **Vido.Views** | All XAML views, themes, styles, custom controls. | Vido.Core, Vido.ViewModels |
| **Vido.PluginHost** | Plugin discovery, loading, lifecycle management, registry client. | Vido.Core |
| **Vido.App** | Entry point. Composes DI container, wires everything. | All above |
| **Vido.Tests** | Unit and integration tests. | Vido.Core, Vido.Services, Vido.PluginHost, xUnit, Moq |

### 2.3 Key Frameworks & NuGet Packages (All Free/Open Source)

| Package | Purpose | License |
|---------|---------|---------|
| **CommunityToolkit.Mvvm** (8.4+) | MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) | MIT |
| **Microsoft.Extensions.DependencyInjection** (8.0+) | DI container | MIT |
| **Microsoft.Extensions.Hosting** (8.0+) | Generic host for app lifecycle | MIT |
| **FFmpeg.AutoGen.Abstractions** + **FFmpeg.AutoGen.Bindings.DynamicallyLoaded** (8.0) | FFmpeg P/Invoke bindings for decoding/demuxing | LGPL |
| **FFmpeg.LGPL** (NuGet) | Native FFmpeg DLLs (`avcodec-62`, `avformat-62`, `avutil-60`, `swscale-9`, `swresample-6`) provided automatically via NuGet runtimes convention | LGPL |
| **SharpDX** or **Vortice.Windows** (DirectX) | Hardware-accelerated video rendering to WPF via D3DImage | MIT |
| **System.Text.Json** (built-in) | JSON serialization for settings, plugin manifests | MIT |
| **xUnit** + **xUnit.runner** | Unit testing framework | Apache 2.0 |
| **Moq** (4.x) | Mocking for unit tests | BSD |
| **WiX Toolset** (5.x) | MSI installer generation | MS-RL (free) |

### 2.4 FFmpeg Integration Strategy

Since the user asked for "FFmpeg unless there's a better .NET/Windows option" — **FFmpeg.AutoGen** is the best choice for .NET on Windows. It provides direct P/Invoke bindings to native FFmpeg libraries with zero managed overhead. The integration strategy:

1. **Decoding**: Use `avformat` to demux, `avcodec` to decode frames. Use hardware acceleration via DXVA2/D3D11VA when available.
2. **Rendering**: Decode frames to `AVFrame`, convert via `swscale` to BGRA32, render to a `WriteableBitmap` or `D3DImage` (Direct3D interop for zero-copy GPU rendering).
3. **Audio**: Decode audio with `avcodec`, resample via `swresample`, output via WASAPI (Windows Audio Session API) for low-latency audio. Use `NAudio` (MIT license) as a managed WASAPI wrapper if needed.
4. **Threading**: Dedicated decode thread, separate render thread, separate audio thread. Use lock-free ring buffers for frame passing.
5. **Performance targets**: Frame-accurate seeking, <50ms startup for cached files, gapless playback at native frame rate, hardware-accelerated decode where GPU supports it.

### 2.5 MVVM & Event Architecture

- **Event Bus**: A central `IEventBus` (publish/subscribe) for decoupled communication between services, ViewModels, and plugins. Events like `VideoLoadedEvent`, `PlaybackPositionChanged`, `FileDoubleClicked`, `PluginLoaded`, etc.
- **Service Locator**: Only for plugin runtime resolution. All internal code uses constructor injection.
- **Commands**: All user actions route through `IRelayCommand` on ViewModels, which call into services via interfaces.
- **Data Binding**: All UI state flows through ViewModel properties via `INotifyPropertyChanged` (source-generated by CommunityToolkit.Mvvm).

---

## 3. Project Structure

```
Vido.sln
├── src/
│   ├── Vido.Core/
│   │   ├── Events/
│   │   │   ├── IEventBus.cs
│   │   │   ├── EventBus.cs
│   │   │   ├── VideoLoadedEvent.cs
│   │   │   ├── PlaybackStateChangedEvent.cs
│   │   │   ├── PlaybackPositionChangedEvent.cs
│   │   │   ├── FileExplorerSelectionChangedEvent.cs
│   │   │   └── PluginLoadedEvent.cs
│   │   ├── Interfaces/
│   │   │   ├── IVideoEngine.cs
│   │   │   ├── IFileSystemService.cs
│   │   │   ├── ISettingsService.cs
│   │   │   ├── IStateService.cs
│   │   │   ├── IKeyboardShortcutService.cs
│   │   │   ├── IPluginManager.cs
│   │   │   └── ILogService.cs
│   │   ├── Models/
│   │   │   ├── AppSettings.cs
│   │   │   ├── FileNode.cs
│   │   │   ├── VideoMetadata.cs
│   │   │   ├── LogEntry.cs
│   │   │   └── KeyBinding.cs
│   │   ├── Plugin/
│   │   │   ├── IVidoPlugin.cs
│   │   │   ├── IPluginContext.cs
│   │   │   ├── PluginManifest.cs
│   │   │   ├── Contributions/
│   │   │   │   ├── SidebarContribution.cs
│   │   │   │   ├── BottomPanelContribution.cs
│   │   │   │   ├── RightPanelContribution.cs
│   │   │   │   ├── StatusBarContribution.cs
│   │   │   │   ├── ToolbarButtonContribution.cs
│   │   │   │   ├── FileIconContribution.cs
│   │   │   │   ├── FileHandlerContribution.cs
│   │   │   │   └── ContextMenuContribution.cs
│   │   │   └── Events/
│   │   │       ├── IPluginEventSink.cs
│   │   │       └── PluginVideoEvents.cs
│   │   └── Vido.Core.csproj
│   ├── Vido.Services/
│   │   ├── Video/
│   │   │   ├── FFmpegVideoEngine.cs
│   │   │   ├── FFmpegInitializer.cs
│   │   │   ├── FrameConverter.cs
│   │   │   └── AudioRenderer.cs
│   │   ├── FileSystem/
│   │   │   └── FileSystemService.cs
│   │   ├── Settings/
│   │   │   ├── SettingsService.cs
│   │   │   └── StateService.cs
│   │   ├── Keyboard/
│   │   │   └── KeyboardShortcutService.cs
│   │   ├── Log/
│   │   │   └── LogService.cs
│   │   └── Vido.Services.csproj
│   ├── Vido.ViewModels/
│   │   ├── MainWindowViewModel.cs
│   │   ├── VideoPlayerViewModel.cs
│   │   ├── FileExplorerViewModel.cs
│   │   ├── PluginManagerViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── VideoDetailsViewModel.cs
│   │   ├── OutputLogViewModel.cs
│   │   ├── StatusBarViewModel.cs
│   │   └── Vido.ViewModels.csproj
│   ├── Vido.Views/
│   │   ├── MainWindow.xaml / .cs
│   │   ├── Controls/
│   │   │   ├── VideoPlayerControl.xaml / .cs
│   │   │   ├── PlayerControlsBar.xaml / .cs
│   │   │   ├── ActivityBar.xaml / .cs
│   │   │   ├── SidebarPanel.xaml / .cs
│   │   │   ├── TabWell.xaml / .cs
│   │   │   ├── BottomPanel.xaml / .cs
│   │   │   ├── RightPanel.xaml / .cs
│   │   │   ├── StatusBar.xaml / .cs
│   │   │   ├── TitleBar.xaml / .cs
│   │   │   └── DockingManager.xaml / .cs
│   │   ├── Panels/
│   │   │   ├── FileExplorerPanel.xaml / .cs
│   │   │   ├── PluginManagerPanel.xaml / .cs
│   │   │   ├── SettingsPanel.xaml / .cs
│   │   │   ├── VideoDetailsPanel.xaml / .cs
│   │   │   └── OutputLogPanel.xaml / .cs
│   │   ├── Themes/
│   │   │   ├── DarkModern.xaml          (master resource dictionary)
│   │   │   ├── Colors.xaml              (color palette)
│   │   │   ├── Brushes.xaml             (brush definitions)
│   │   │   ├── Typography.xaml          (font styles)
│   │   │   ├── ButtonStyles.xaml
│   │   │   ├── MenuStyles.xaml
│   │   │   ├── TabStyles.xaml
│   │   │   ├── TreeViewStyles.xaml
│   │   │   ├── ScrollBarStyles.xaml
│   │   │   ├── ContextMenuStyles.xaml
│   │   │   ├── SliderStyles.xaml
│   │   │   ├── TextBoxStyles.xaml
│   │   │   └── TooltipStyles.xaml
│   │   ├── Converters/
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   ├── TimeSpanToStringConverter.cs
│   │   │   └── FileSizeConverter.cs
│   │   ├── Resources/
│   │   │   ├── Icons/
│   │   │   │   ├── video-file.png
│   │   │   │   ├── generic-file.png
│   │   │   │   ├── folder.png
│   │   │   │   ├── folder-open.png
│   │   │   │   ├── explorer.png
│   │   │   │   ├── extensions.png
│   │   │   │   ├── settings-gear.png
│   │   │   │   ├── play.png
│   │   │   │   ├── pause.png
│   │   │   │   ├── stop.png
│   │   │   │   ├── skip-next.png
│   │   │   │   ├── skip-prev.png
│   │   │   │   ├── volume.png
│   │   │   │   ├── volume-mute.png
│   │   │   │   ├── loop.png
│   │   │   │   ├── fullscreen.png
│   │   │   │   ├── minimize.png
│   │   │   │   ├── maximize.png
│   │   │   │   ├── restore.png
│   │   │   │   ├── close.png
│   │   │   │   └── vido-logo.png
│   │   │   └── Fonts/
│   │   └── Vido.Views.csproj
│   ├── Vido.PluginHost/
│   │   ├── PluginLoader.cs
│   │   ├── PluginContext.cs
│   │   ├── PluginManager.cs
│   │   ├── PluginRegistry.cs
│   │   ├── PluginInstaller.cs
│   │   └── Vido.PluginHost.csproj
│   └── Vido.App/
│       ├── App.xaml / .cs
│       ├── Startup.cs
│       └── Vido.App.csproj
├── tests/
│   └── Vido.Tests/
│       ├── Services/
│       ├── PluginHost/
│       ├── ViewModels/
│       └── Vido.Tests.csproj
├── Context/
│   ├── Vido_Requirements.md
│   ├── REQUIREMENTS_QUESTIONS.md
│   ├── IMPLEMENTATION_PLAN.md
│   └── TEST_PLANS/
│       ├── vi-001_test_plan.md
│       ├── vi-002_test_plan.md
│       └── ...
└── (FFmpeg native DLLs provided automatically via FFmpeg.LGPL NuGet package)
```

---

## 4. Design System — VS Code Dark Modern

### 4.1 Color Palette

All colors must precisely match VS Code's Dark Modern theme. The source of truth is [VS Code's Dark Modern theme](https://github.com/microsoft/vscode/blob/main/extensions/theme-defaults/themes/dark_modern.json).

```
Background (Editor):        #1f1f1f
Background (Sidebar):       #181818
Background (Activity Bar):  #181818
Background (Title Bar):     #1f1f1f
Background (Status Bar):    #181818
Background (Panel):         #1f1f1f
Background (Input):         #313131
Background (Dropdown):      #313131
Background (Tab Active):    #1f1f1f
Background (Tab Inactive):  #181818
Background (Menu):          #2f2f2f
Background (Hover):         #2a2d2e
Background (Selection):     #04395e
Background (Button):        #0078d4
Background (Button Hover):  #026ec1
Background (Scrollbar):     #4a4a4a80
Background (Context Menu):  #1f1f1f

Foreground (Primary):       #cccccc
Foreground (Secondary):     #9d9d9d
Foreground (Disabled):      #6b6b6b
Foreground (Active Icon):   #ffffff
Foreground (Inactive Icon): #9d9d9d
Foreground (Link):          #4fc1ff
Foreground (Error):         #f44747
Foreground (Warning):       #cca700
Foreground (Success):       #89d185

Accent:                     #007acc  (universal accent — status bar, activity bar indicator, menu selection highlight, focus rings, active tab indicators, all interactive accents)

Border (Primary):           #2b2b2b
Border (Focus):             #007fd4
Border (Active Tab):        #1f1f1f (bottom border of active tab, matches editor bg)
Border (Panel Separator):   #2b2b2b

Corner Radius (Menu Popup): 6px  (dropdown menus, context menus)
Corner Radius (Menu Item):  4px  (top-level menu highlight, dropdown item highlight)

Font Family:                Segoe UI (UI), Cascadia Code / Consolas (monospace)
Font Size:                  13px (UI), 14px (editor/console)
Font Weight:                400 (normal), 600 (bold for labels)
```

### 4.2 Layout Dimensions

```
Title Bar Height:           30px
Activity Bar Width:         48px
Sidebar Width:              Default 300px, min 170px, max 600px (resizable via splitter)
Tab Bar Height:             35px
Status Bar Height:          22px
Bottom Panel Height:        Default 200px, min 100px, max 500px (resizable via splitter)
Right Panel Width:          Default 300px, min 150px, max 500px (resizable via splitter)
Context Menu Item Height:   28px
Menu Bar Item Padding:      8px horizontal
Scrollbar Width:            10px (thin, appears on hover: 14px)
```

### 4.3 Icon Strategy

Use **Segoe Fluent Icons** (built into Windows 10/11) as the primary icon font for toolbar and UI controls where appropriate. For file-type icons and activity bar icons, use custom PNG/SVG resources (16x16 and 24x24). Icons should match the VS Code Codicon style — simple, monochrome, lightweight.

Where possible, reference the [VS Code Codicons](https://github.com/microsoft/vscode-codicons) (CC-BY-4.0 license) as design references. Since Codicons are a web font, re-create the most critical icons as XAML vector paths or PNG assets that visually match.

### 4.4 Component Styling Requirements

Each styled component must match VS Code pixel-for-pixel where feasible:

- **Title Bar**: Custom frameless window. Contains: app icon (left), menu items (File, Edit, View, Playback, Help), draggable space, window controls (minimize, maximize/restore, close — with hover effects matching VS Code).
- **Menu Bar**: Each menu item is a flat label. Hover shows subtle highlight. Click opens dropdown. Dropdown has separator lines, keyboard shortcut hints right-aligned, hover highlight per item.
- **Activity Bar**: Vertical strip on the far left. Each icon is 48x48 hit area with 24x24 icon centered. Active icon has a left border indicator (2px, white). Inactive icons are dimmed.
- **Sidebar**: Content area adjacent to activity bar. Has a header with title text. Content scrolls independently. Resizable right edge (cursor changes to resize on hover).
- **Tab Well**: Horizontal tab strip. Each tab shows icon + filename. Active tab has colored bottom border (or different background). Close button (x) appears on hover (except the Video tab which has no close button). Tabs can be reordered by drag.
- **Editor Area / Video Player**: Fills all remaining space. The video player tab is always present and always the leftmost tab.
- **Bottom Panel**: Collapsible area below the editor. Has its own tab strip (initially: "Output"). Resizable top edge.
- **Right Panel**: Collapsible area to the right of the editor. Has its own tab strip (initially: "Video Info"). Resizable left edge.
- **Status Bar**: Full-width bottom bar. Left-aligned items (file info). Right-aligned items (codec, resolution). Items are clickable where appropriate.
- **Context Menus**: Dark background, rounded corners (2px), subtle shadow, separator lines, keyboard shortcut hints.
- **Scrollbars**: Thin (10px), semi-transparent track, brighter thumb on hover. Matches VS Code scrollbar style.
- **Tooltips**: Dark background (#2f2f2f), light text, subtle border, appears after 500ms hover delay.

---

## 5. Plugin System Architecture

### 5.1 Plugin Structure

A Vido plugin is a .NET class library (DLL) with the following structure:

```
MyPlugin/
├── MyPlugin.dll              (compiled plugin assembly)
├── plugin.json               (plugin manifest)
├── Resources/                (optional: icons, assets)
│   └── my-icon.png
└── (any additional DLLs the plugin depends on)
```

### 5.2 Plugin Manifest (`plugin.json`)

```json
{
  "id": "com.example.my-plugin",
  "name": "My Plugin",
  "displayName": "My Awesome Plugin",
  "version": "1.0.0",
  "description": "Adds awesome functionality to Vido",
  "author": "Author Name",
  "license": "MIT",
  "entryPoint": "MyPlugin.dll",
  "pluginClass": "MyPlugin.MyPluginEntry",
  "minVidoVersion": "1.0.0",
  "repository": "https://github.com/author/my-plugin",
  "tags": ["utility", "enhancement"],
  "contributes": {
    "sidebar": [
      {
        "id": "my-panel",
        "title": "My Panel",
        "icon": "Resources/my-icon.png",
        "order": 100
      }
    ],
    "bottomPanel": [
      {
        "id": "my-bottom-tab",
        "title": "My Tab",
        "order": 100
      }
    ],
    "rightPanel": [],
    "statusBar": [
      {
        "id": "my-status",
        "position": "right",
        "order": 100
      }
    ],
    "toolbarButtons": [
      {
        "id": "my-button",
        "tooltip": "Open My Feature",
        "icon": "Resources/my-icon.png",
        "order": 100
      }
    ],
    "fileIcons": {
      ".funscript": "Resources/funscript-icon.png"
    },
    "contextMenu": [
      {
        "id": "my-action",
        "label": "My Action",
        "fileExtensions": [".funscript"],
        "order": 100
      }
    ],
    "fileHandlers": [
      {
        "extensions": [".funscript"],
        "action": "open"
      }
    ],
    "settings": [
      {
        "id": "my-plugin.someSetting",
        "type": "boolean",
        "default": true,
        "title": "Enable Some Feature",
        "description": "When enabled, does something awesome"
      }
    ]
  }
}
```

### 5.3 Plugin Lifecycle

```
1. DISCOVER   → PluginLoader scans %APPDATA%/Vido/plugins/ for directories containing plugin.json
2. VALIDATE   → Manifest is parsed, version compatibility checked, dependencies resolved
3. LOAD       → Assembly loaded into default AssemblyLoadContext (full trust, no sandbox)
4. ACTIVATE   → IVidoPlugin.Activate(IPluginContext) called — plugin registers handlers, views, etc.
5. RUNNING    → Plugin responds to events, contributes UI elements, processes commands
6. DEACTIVATE → IVidoPlugin.Deactivate() called on app shutdown or plugin disable
7. UNLOAD     → Assembly references released (full unload requires app restart for v1)
```

### 5.4 Plugin Loading Strategy

- Plugins are stored in `%APPDATA%/Vido/plugins/<plugin-id>/`
- On startup, `PluginLoader` enumerates all subdirectories, reads `plugin.json`, validates manifests
- Assemblies are loaded using `AssemblyLoadContext.Default` (full trust)
- The plugin's entry class (implementing `IVidoPlugin`) is instantiated via reflection
- `Activate()` is called with an `IPluginContext` that provides access to all Vido APIs
- Plugin contributions declared in the manifest are registered with the host automatically (sidebar icons, etc.)
- Plugin-created views (UserControls) are hosted in ContentPresenters at the declared positions

---

## 6. Plugin API Specification

### 6.1 Core Interface: `IVidoPlugin`

```csharp
namespace Vido.Core.Plugin;

/// <summary>
/// Entry point for all Vido plugins. Implement this interface and declare
/// the fully-qualified class name in plugin.json's "pluginClass" field.
/// </summary>
public interface IVidoPlugin
{
    /// <summary>
    /// Called when the plugin is activated. Use the context to register
    /// event handlers, contribute UI elements, and access Vido services.
    /// </summary>
    void Activate(IPluginContext context);

    /// <summary>
    /// Called when the plugin is deactivated (app shutdown or manual disable).
    /// Clean up any resources, unsubscribe from events, close connections.
    /// </summary>
    void Deactivate();
}
```

### 6.2 Plugin Context: `IPluginContext`

```csharp
namespace Vido.Core.Plugin;

/// <summary>
/// Provides plugins with access to all Vido extension points.
/// Passed to IVidoPlugin.Activate().
/// </summary>
public interface IPluginContext
{
    /// <summary>Plugin's own manifest data.</summary>
    PluginManifest Manifest { get; }

    /// <summary>Path to the plugin's installation directory on disk.</summary>
    string PluginDirectory { get; }

    /// <summary>Access to the Vido event bus for subscribing/publishing events.</summary>
    IEventBus Events { get; }

    /// <summary>Access to the video playback engine.</summary>
    IVideoEngine VideoEngine { get; }

    /// <summary>Access to application logging.</summary>
    ILogService Logger { get; }

    /// <summary>Access to the settings store (for reading/writing plugin settings).</summary>
    IPluginSettingsStore Settings { get; }

    // ── UI Contribution Registration ──

    /// <summary>Register a sidebar panel (activity bar icon + content view).</summary>
    void RegisterSidebarPanel(string contributionId, Func<FrameworkElement> viewFactory);

    /// <summary>Register a bottom panel tab.</summary>
    void RegisterBottomPanel(string contributionId, Func<FrameworkElement> viewFactory);

    /// <summary>Register a right panel tab.</summary>
    void RegisterRightPanel(string contributionId, Func<FrameworkElement> viewFactory);

    /// <summary>Register a status bar item (provides live content).</summary>
    void RegisterStatusBarItem(string contributionId, Func<FrameworkElement> viewFactory);

    /// <summary>Register a toolbar button click handler.</summary>
    void RegisterToolbarButtonHandler(string contributionId, Action clickHandler);

    /// <summary>Register a context menu action handler.</summary>
    void RegisterContextMenuHandler(string contributionId, Action<FileNode> handler);

    /// <summary>Register a file double-click handler for specific extensions.</summary>
    void RegisterFileHandler(string[] extensions, Action<FileNode> handler);

    /// <summary>Register custom file icons for specific extensions.</summary>
    void RegisterFileIcons(Dictionary<string, string> extensionToIconPath);

    /// <summary>Register a keyboard shortcut.</summary>
    void RegisterKeyBinding(KeyBinding binding, Action handler);
}
```

### 6.3 Event Bus: `IEventBus`

```csharp
namespace Vido.Core.Events;

public interface IEventBus
{
    /// <summary>Subscribe to an event type. Returns an IDisposable to unsubscribe.</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>Publish an event to all subscribers.</summary>
    void Publish<TEvent>(TEvent eventData) where TEvent : class;
}

// ── Core Events ──

public record VideoLoadedEvent(string FilePath, VideoMetadata Metadata);
public record VideoUnloadedEvent();
public record PlaybackStateChangedEvent(PlaybackState State); // Playing, Paused, Stopped
public record PlaybackPositionChangedEvent(TimeSpan Position, TimeSpan Duration);
public record VolumeChangedEvent(int Volume, bool IsMuted);
public record FileExplorerFolderOpenedEvent(string FolderPath);
public record FileExplorerFolderClosedEvent();
public record FileExplorerSelectionChangedEvent(FileNode? SelectedNode);
public record FileDoubleClickedEvent(FileNode Node);
public record LogEntryAddedEvent(LogEntry Entry);
public record PluginLoadedEvent(string PluginId);
public record PluginUnloadedEvent(string PluginId);
```

### 6.4 Video Engine Interface: `IVideoEngine`

```csharp
namespace Vido.Core.Interfaces;

public interface IVideoEngine
{
    // ── State ──
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    int Volume { get; set; }
    bool IsMuted { get; set; }
    bool IsLooping { get; set; }
    VideoMetadata? CurrentMetadata { get; }

    // ── Commands ──
    Task LoadAsync(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);

    // ── Events (for high-frequency position updates, separate from EventBus) ──
    event Action<TimeSpan>? PositionChanged;
    event Action<PlaybackState>? StateChanged;
}
```

### 6.5 Plugin Settings Store: `IPluginSettingsStore`

```csharp
namespace Vido.Core.Plugin;

public interface IPluginSettingsStore
{
    /// <summary>Get a setting value by key, returning default if not set.</summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>Set a setting value by key. Persisted automatically.</summary>
    void Set<T>(string key, T value);

    /// <summary>Event raised when any setting changes.</summary>
    event Action<string>? SettingChanged;
}
```

### 6.6 TCode Plugin Compatibility Verification

The plugin API must support all of the following TCode plugin requirements (verified against FunSVP analysis):

| TCode Requirement | Plugin API Coverage |
|-------------------|-------------------|
| Bottom tab for funscript viewer | `RegisterBottomPanel()` — plugin provides a UserControl with SkiaSharp canvas or similar |
| Sidebar panel for device controls | `RegisterSidebarPanel()` — plugin provides a UserControl with connection/axis controls |
| Context menu items on files | `contributes.contextMenu` in manifest + `RegisterContextMenuHandler()` for handling clicks |
| File handler for .funscript files | `contributes.fileHandlers` + `RegisterFileHandler()` — handles double-click of .funscript files |
| File icon for .funscript | `contributes.fileIcons` in manifest |
| Status bar items (connection status, offset) | `RegisterStatusBarItem()` — plugin provides live-updating status elements |
| Video position events (high-frequency) | `IVideoEngine.PositionChanged` event — fires at ~60Hz for smooth TCode interpolation |
| Video state events (play/pause/stop) | `Events.Subscribe<PlaybackStateChangedEvent>()` |
| Plugin settings (COM port, baud rate, axis config, etc.) | `contributes.settings` in manifest + `IPluginSettingsStore` for read/write |
| Keyboard shortcuts | `RegisterKeyBinding()` for plugin-specific shortcuts |

---

## 7. Plugin Distribution & Registry

### 7.1 Registry Repository

Create a public GitHub repository: `vido-plugin-registry`

It contains a single file at the root:

**`registry.json`**:
```json
{
  "schemaVersion": 1,
  "lastUpdated": "2026-02-20T00:00:00Z",
  "plugins": [
    {
      "id": "com.example.tcode",
      "name": "TCode Controller",
      "displayName": "TCode Controller",
      "description": "Send TCode commands to stroker devices via serial/UDP",
      "author": "Author Name",
      "version": "1.0.0",
      "minVidoVersion": "1.0.0",
      "repository": "https://github.com/author/vido-tcode-plugin",
      "downloadUrl": "https://github.com/author/vido-tcode-plugin/releases/download/v1.0.0/vido-tcode-plugin-1.0.0.zip",
      "tags": ["tcode", "hardware", "device"],
      "iconUrl": "https://raw.githubusercontent.com/author/vido-tcode-plugin/main/icon.png"
    }
  ]
}
```

### 7.2 Plugin Installation Flow

1. **Browse**: User opens Plugin Manager sidebar panel → Vido fetches `registry.json` from GitHub (cached locally, refreshed on demand)
2. **Search**: User types a name → client-side filter on name/displayName/description/tags
3. **Install**: User clicks Install → Vido downloads the `.zip` from `downloadUrl`, extracts to `%APPDATA%/Vido/plugins/<plugin-id>/`, validates `plugin.json`
4. **Enable**: Plugin is loaded immediately (or on next restart if load fails)
5. **Update**: On startup (or when user clicks "Check for Updates"), Vido compares local `plugin.json` version with registry version. If newer exists, shows update badge. User clicks Update → new zip downloaded, old files replaced, plugin reloaded
6. **Uninstall**: User clicks Uninstall → plugin deactivated, directory deleted, restart may be needed

### 7.3 Registry Configuration

The registry URL is configurable in settings (default: the GitHub raw URL). This allows:
- Private registries for internal use
- Local file:// URLs for development
- Multiple registries in the future

### 7.4 Plugin Developer Workflow

For a developer to create a Vido plugin:

1. Create a new .NET 8 class library project
2. Add a reference to `Vido.Core.dll` (or the Vido.Core NuGet package once published)
3. Create a `plugin.json` manifest
4. Implement `IVidoPlugin` in your entry class
5. Build → produces `MyPlugin.dll`
6. Create a folder with `plugin.json` + `MyPlugin.dll` + any resources
7. For local testing: copy folder to `%APPDATA%/Vido/plugins/`
8. For distribution: zip the folder, create a GitHub release with the zip, add entry to `registry.json`

---

## 8. Ticket Breakdown

### Conventions for the AI Developer

**CRITICAL — Read before starting any ticket:**

1. **After EVERY ticket**, output:
   - **Changelog**: Bullet list of what was added/changed/removed
   - **Git commit message**: In conventional commit format (see Rule 8 below for detail requirements)
   - **Update `CHANGELOG.md`** in the workspace root (see Rule 9 below)

2. **After EVERY ticket**, create a markdown test plan at `Context/TEST_PLANS/vi-XXX_test_plan.md` containing:
   - **Manual Tests**: Step-by-step instructions a human can follow to verify the ticket's functionality
   - **Regression Tests**: Tests to ensure previous functionality still works
   - Include expected results for each test step

3. **Unit tests**: Write unit tests for all testable logic in each ticket. Place tests in the `Vido.Tests` project. Include regression tests for any changed functionality. If any changes made during the ticket drift from the original scope (e.g., refactoring an existing service to support a new feature), you MUST write tests covering those changes too, even if they weren't part of the ticket's original specification.

4. **Code quality**: After completing each ticket, review ALL changed files and:
   - Remove any dead code, unused usings, commented-out code
   - Ensure consistent naming (PascalCase for public, _camelCase for private fields)
   - If a method/class is getting long, extract logically — but only if it improves readability
   - Code should read like a well-organized human wrote it

5. **Revision tracking & dead code elimination**: When a ticket requires multiple revisions or iterations:
   - Keep a mental (or explicit) list of every file, class, method, property, and using directive that was added, renamed, moved, or deleted across ALL revisions
   - After the final revision, do a FULL sweep of every file touched during the ticket to ensure:
     - No orphaned methods, properties, or classes remain from earlier revisions
     - No unused `using` directives exist
     - No commented-out code from previous attempts remains
     - No references to renamed/moved symbols survive (compile will catch most, but check string references, XAML bindings, and DI registrations too)
     - No duplicate logic was introduced (e.g., a method was extracted but the original inline code was left behind)
   - If a file was created in an earlier revision and is no longer needed, DELETE it entirely
   - Run `dotnet build` after the sweep to ensure nothing was broken by cleanup
   - This discipline applies to EVERY ticket, not just ones with obvious revisions — even a "clean" first pass can leave unnecessary code

6. **Build verification**: Every ticket must leave the solution in a compilable, runnable state. Run `dotnet build` after every ticket to verify.

7. **Incremental visibility**: Every ticket must produce visible, testable functionality. After each ticket, a human should be able to launch the app and observe something new.

8. **Detailed git commit messages**: Every commit message must use conventional commit format with a **descriptive body**. The subject line follows `type(scope): short summary`. The body must list every meaningful change, grouped logically. Example:
   ```
   feat(vi-002): implement custom title bar with window controls

   - Add TitleBarView UserControl with app icon, title text, and window control buttons
   - Implement minimize, maximize/restore, and close button functionality
   - Add double-click title bar to toggle maximize/restore
   - Create TitleBarViewModel with window state tracking
   - Register TitleBarView in DI container and integrate into MainWindow layout
   - Add VS Code Dark Modern hover/active states for window control buttons
   - Add unit tests for TitleBarViewModel window state logic
   ```
   **Never** use single-line commit messages that just restate the ticket title. The commit message should tell a developer exactly what changed without reading the diff.

9. **CHANGELOG.md maintenance**: After EVERY completed ticket, update the `CHANGELOG.md` file in the workspace root. The changelog uses the following format:
   ```
   ## [Unreleased]

   ### vi-XXX
   - change 1
   - change 2
   - change 3

   ### vi-YYY
   - change 1
   - change 2
   ```
   Rules:
   - Group entries under an `## [Unreleased]` section at the top
   - Each ticket gets its own `### vi-XXX` subsection header (using the ticket number)
   - List each meaningful change as a bullet point under that ticket
   - When a release is cut (human action), the `[Unreleased]` section becomes a versioned section and a new empty `[Unreleased]` is added above

10. **Post-ticket dead code cleanup**: After implementing every ticket, the developer AI MUST perform a dedicated code cleanup pass. This means searching for dead code that was added as part of that ticket only — methods, properties, classes, using directives, XAML elements, resource entries, or DI registrations that may have become unnecessary over the course of developing that ticket. The goal is to ensure the solution stays very clean and contains no vestigial code from mid-ticket iterations. This cleanup is mandatory even if the ticket was implemented in a single pass — review all new code with fresh eyes before marking the ticket complete.

---

### vi-001: Solution Scaffold & Empty Window

**Goal**: Create the complete solution structure, NuGet references, and a launchable WPF application that shows an empty dark window.

**Tasks**:
1. Create `Vido.sln` with all project references as defined in Section 3
2. Create all `.csproj` files with correct target frameworks (net8.0-windows for WPF projects, net8.0 for Core)
3. Add all NuGet package references as defined in Section 2.3
4. Create `App.xaml` and `App.xaml.cs` with DI container setup (using `Microsoft.Extensions.DependencyInjection`)
5. Create `MainWindow.xaml` as a frameless window (`WindowStyle="None"`, `AllowsTransparency="True"`) with dark background (#1f1f1f)
6. The window should be resizable and movable (implement custom resize/move for frameless window)
7. Set minimum window size to 800x600
8. Create `Context/TEST_PLANS/` directory

**Acceptance Criteria**:
- `dotnet build` succeeds with zero warnings
- `dotnet run --project src/Vido.App` launches a dark, empty, frameless window
- Window can be moved by dragging, resized from edges/corners
- Window minimum size is enforced
- All projects compile and reference each other correctly

---

### vi-002: Custom Title Bar

**Goal**: Implement the custom title bar matching VS Code's Dark Modern style exactly.

**Tasks**:
1. Create `TitleBar.xaml` custom control
2. Implement: App icon (left), draggable title area, window controls (minimize, maximize/restore, close)
3. Window controls must match VS Code styling:
   - Minimize: `_` icon, hover background #3d3d3d
   - Maximize/Restore: `□` / `❐` icons, hover background #3d3d3d
   - Close: `✕` icon, hover background #c42b1c (red), foreground white
4. Double-click title bar to maximize/restore
5. Title bar height: 30px
6. Start building the theme system: Create `Colors.xaml` and `Brushes.xaml` with the core Dark Modern palette from Section 4.1

**Acceptance Criteria**:
- Title bar visually matches VS Code Dark Modern
- All three window control buttons work correctly
- Double-click toggles maximize/restore
- Title bar dragging moves the window
- Snap to screen edges works naturally (Windows Aero Snap)
- Colors are defined in resource dictionaries (not hardcoded in controls)

---

### vi-003: Menu Bar

**Goal**: Add the menu bar to the title bar, matching VS Code's integrated menu style.

**Tasks**:
1. Create `MenuStyles.xaml` theme resource dictionary
2. Implement menu bar inline within the title bar (left side, after app icon)
3. Menu items: **File**, **Edit**, **View**, **Playback**, **Help**
4. File menu: Open File, Open Folder, Close Folder, (separator), Recent Files ▸ (submenu, empty for now), (separator), Exit
5. Edit menu: (empty/reserved for future, show "No actions available" disabled item)
6. View menu: Toggle Sidebar (Ctrl+B), Toggle Status Bar, Toggle Bottom Panel (Ctrl+J), Toggle Right Panel, (separator), Fullscreen (F11), (separator), Zoom In (Ctrl+=), Zoom Out (Ctrl+-)
7. Playback menu: Play/Pause (Space), Stop, (separator), Skip Forward, Skip Backward, (separator), Loop, (separator), Playback Speed ▸ (submenu: 0.25x, 0.5x, 1.0x, 1.5x, 2.0x)
8. Help menu: About Vido, (separator), Check for Updates
9. All menu items must show keyboard shortcut hints right-aligned
10. Style dropdown menus to match VS Code exactly (dark background, hover highlight, separator lines, rounded corners, shadow)

**Acceptance Criteria**:
- Menu bar appears in title bar after app icon
- All menus open on click with correct items
- Keyboard shortcut hints are visible and right-aligned
- Hover effects match VS Code
- Menu closes when clicking elsewhere
- Submenus (Recent Files, Playback Speed) expand on hover
- Menu items that have no handler yet are disabled but visible

---

### vi-004: Core Layout — Activity Bar, Sidebar, Editor Area, Status Bar

**Goal**: Implement the main application layout with all major regions, matching VS Code structure.

**Tasks**:
1. Create `ActivityBar.xaml` — vertical icon strip on far left (48px wide)
   - Three icons: Explorer (files), Extensions (puzzle piece), Settings (gear)
   - Active icon has blue left border indicator (2px)
   - Inactive icons are dimmed (#9d9d9d)
   - Click toggles active sidebar panel (or hides sidebar if clicking active icon again)
2. Create `SidebarPanel.xaml` — container adjacent to activity bar
   - Has a header showing the active panel name
   - Resizable right edge via `GridSplitter`
   - Default width 300px, min 170px, max 600px
3. Create `StatusBar.xaml` — full-width bottom bar (22px)
   - Empty for now (content added in later tickets)
   - Correct background color (#181818)
4. Create the main layout grid in `MainWindow.xaml`:
   - Title bar (top)
   - Activity bar (left) | Sidebar (left, collapsible) | Editor area (center, fills remaining) | Right panel (right, collapsible, hidden by default)
   - Bottom panel (below editor, collapsible, hidden by default)
   - Status bar (bottom)
5. Editor area shows placeholder text "Open a video file to begin" centered in the space
6. All splitters must have correct cursor feedback on hover

**Acceptance Criteria**:
- Layout matches VS Code structure: title bar → activity bar | sidebar | editor | status bar
- Clicking activity bar icons switches sidebar content (panels are empty but switching works)
- Clicking the active activity bar icon hides/shows the sidebar
- Sidebar is resizable via splitter
- Editor area fills remaining space and adjusts when sidebar is resized
- Status bar is visible at the bottom
- All colors match VS Code Dark Modern

---

### vi-005: Event Bus & Core Services Infrastructure

**Goal**: Implement the event bus, logging service, and settings service foundations.

**Tasks**:
1. Implement `IEventBus` / `EventBus` with thread-safe pub/sub (use `ConcurrentDictionary` of handlers)
2. Implement `ILogService` / `LogService` — thread-safe observable log with dispatcher marshalling to UI thread
3. Implement `ISettingsService` / `SettingsService` — JSON-based settings persistence to `%APPDATA%/Vido/settings.json` with debounced saving (500ms)
4. Implement `IStateService` / `StateService` — manages window geometry, last folder, last video, panel layout, volume. Persists to `%APPDATA%/Vido/state.json`
5. Implement `AppSettings` model with all settings fields (with sensible defaults)
6. Register all services in the DI container
7. Write unit tests for EventBus (subscribe, publish, unsubscribe, multi-subscriber, thread safety)
8. Write unit tests for SettingsService (save, load, defaults, debounce)

**Acceptance Criteria**:
- EventBus correctly delivers events to subscribers on the correct thread
- SettingsService persists to disk and loads on startup
- StateService saves/restores window position and size
- App remembers its window position across restarts
- All unit tests pass
- `dotnet test` runs successfully

---

### vi-006: File Explorer Panel — Tree View

**Goal**: Implement the file explorer sidebar panel with folder tree view, matching VS Code style.

**Tasks**:
1. Create `FileNode` model with lazy-loading children (only load subdirectory contents when expanded)
2. Implement `IFileSystemService` / `FileSystemService` — reads directory contents, returns `FileNode` tree
3. Create `FileExplorerViewModel` — manages open folder, tree state, selection
4. Create `FileExplorerPanel.xaml` — TreeView with VS Code styling:
   - Folder icons (open/closed states)
   - Video file icons (for: .mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
   - Generic file icon for all other files
   - Proper indentation (per-level indent matching VS Code)
   - Selection highlight matching VS Code
   - Hover highlight
5. Wire "File > Open Folder" menu item to open a `FolderBrowserDialog` and populate the tree
6. Wire "File > Close Folder" to clear the tree
7. Persist last opened folder in state; restore on startup
8. Create `TreeViewStyles.xaml` matching VS Code's tree appearance

**Acceptance Criteria**:
- File > Open Folder opens a folder picker dialog
- Selected folder contents appear in the sidebar tree
- Folders expand/collapse with lazy loading
- Video files show a video icon; other files show a generic icon
- Folder open/close persists across app restarts
- Tree styling matches VS Code (indentation, icons, selection highlight, hover)
- File > Close Folder clears the tree

---

### vi-007: File Explorer — Context Menus & File Interactions

**Goal**: Add context menus to the file explorer and implement file interaction behaviors.

**Tasks**:
1. Create `ContextMenuStyles.xaml` — style matching VS Code (dark, rounded, shadow)
2. **Background context menu** (right-click on empty area):
   - Open Folder
   - Close Folder
   - Rescan Folder
3. **File context menu** (right-click on a file):
   - Play (for video files, disabled for non-video)
   - Remove (hides file from explorer view — does NOT delete from disk)
   - (separator)
   - Reveal in File Explorer (opens Windows Explorer to that file)
4. **Folder context menu** (right-click on a folder):
   - Expand / Collapse
   - Reveal in File Explorer
5. Implement "Rescan Folder" — re-reads directory from disk, preserving expanded state
6. Implement "Remove" — adds file to a hidden list (persisted in state), filters it from view
7. Non-video file double-click: Show tooltip "File type not supported"
8. Non-video file hover: Show tooltip with filename and "Not a supported video format"
9. Make context menu extensible internally — use a registry pattern so plugins can add items later (the registration mechanism, not the plugin loading which comes in a later ticket)

**Acceptance Criteria**:
- Right-clicking empty area shows background context menu with correct items
- Right-clicking a file shows file context menu
- Right-clicking a folder shows folder context menu
- "Rescan Folder" refreshes the tree
- "Remove" hides the file (not deleted from disk) and persists across restarts
- "Reveal in File Explorer" opens Windows Explorer at the correct location
- Non-video files show tooltip on hover and "not supported" on double-click
- Context menu styling matches VS Code

---

### vi-008: FFmpeg Integration — Video Playback Engine

**Goal**: Implement the core video playback engine using FFmpeg.AutoGen with hardware-accelerated rendering.

**Tasks**:
1. Create `FFmpegInitializer` — locates and initializes FFmpeg DLLs from the app base directory (provided by FFmpeg.LGPL NuGet package)
2. Create `FFmpegVideoEngine` implementing `IVideoEngine`:
   - `LoadAsync(filePath)`: Open file, detect streams, setup decoders
   - `Play()` / `Pause()` / `Stop()`: Control playback state
   - `Seek(position)`: Frame-accurate seeking
   - Volume/Mute control
   - Loop toggle
   - Decode thread: demux + decode video frames on background thread
   - Audio thread: decode + output audio via WASAPI (use `NAudio` for managed WASAPI wrapper)
   - Frame conversion: `swscale` to BGRA32 for WPF rendering
3. Create `FrameConverter` — converts `AVFrame` to `WriteableBitmap` for display
4. Implement frame timing: maintain correct PTS-based presentation timing
5. Register `IVideoEngine` in DI container
6. Write unit tests for engine initialization and metadata extraction
7. FFmpeg native DLLs provided automatically via `FFmpeg.LGPL` NuGet package (no manual downloads needed)

**Note on FFmpeg DLLs**: The `FFmpeg.LGPL` NuGet package provides all required native DLLs automatically via the standard `runtimes/win-x64/native` convention. No manual download or directory setup is required.

**Acceptance Criteria**:
- FFmpeg initializes without errors on startup
- Engine can load a video file and extract metadata (duration, resolution, codec, etc.)
- Engine can decode frames and produce bitmaps
- Audio plays correctly synchronized with video
- Play/Pause/Stop/Seek work correctly
- Volume and mute work
- Unit tests pass

---

### vi-009: Video Player Tab — UI & Controls

**Goal**: Create the video player tab with transport controls, integrated into the main layout.

**Tasks**:
1. Create `VideoPlayerControl.xaml` — displays decoded video frames in an `Image` element (bound to `WriteableBitmap`)
2. Create `PlayerControlsBar.xaml` — transport controls bar below the video:
   - Skip Previous (⏮) — skips to previous video file in folder (alphabetically)
   - Play/Pause toggle (▶/⏸)
   - Stop (⏹)
   - Skip Next (⏭) — skips to next video file in folder (alphabetically)
   - Seek bar (slider matching VS Code slider style) with elapsed/total time labels
   - Volume slider with mute toggle icon
   - Loop toggle button
3. Create `PlayerStyles.xaml` — style all player controls to match VS Code aesthetic
4. Create `VideoPlayerViewModel` — binds to engine state, exposes commands
5. Create the **Video tab** — always the leftmost tab, named "Player", no close button (x)
6. Wire the tab system: Video tab is always present. Settings tab can open alongside it.
7. Wire file explorer: double-clicking a video file loads and plays it in the video tab
8. Video area shows "Open a video file to begin" centered text when no video is loaded with a subtle icon

**Acceptance Criteria**:
- Video tab is always the leftmost tab and cannot be closed
- Double-clicking a video file in the explorer loads and plays it
- All transport controls work: play/pause, stop, skip prev/next, seek, volume, mute, loop
- Skip forward/backward navigates to next/previous video file in the current folder (alphabetically)
- Seek bar updates with playback position and is draggable
- Volume slider and mute toggle work
- Controls are styled to match VS Code Dark Modern aesthetic
- Empty state shows placeholder text when no video is loaded

---

### vi-010: Tab System — TabWell with Docking Foundation

**Goal**: Implement the tab system with proper tab management, drag-to-reorder, and the foundation for dockable panels.

**Tasks**:
1. Create `TabWell.xaml` — horizontal tab strip control:
   - Each tab has: icon, title text, close button (x) on hover
   - The Video tab ("Player") is pinned leftmost with no close button
   - Active tab has bottom border accent or different background (matching VS Code)
   - Tabs can be reordered by dragging (except the Video tab stays leftmost)
   - Tab overflow: when too many tabs, show scroll arrows or dropdown
2. Create `TabStyles.xaml` — all tab styling matching VS Code
3. Implement tab management in `MainWindowViewModel`:
   - `OpenTab(tabId, title, icon, viewFactory)` — opens a new tab
   - `CloseTab(tabId)` — closes a tab (not the Video tab)
   - `ActivateTab(tabId)` — switches to a tab
4. When Settings is opened (activity bar gear icon), it opens as a tab (not in the sidebar) — just like VS Code
5. Bottom panel area: stub with tab strip and collapsible behavior (resizable top edge via splitter)
6. Right panel area: stub with tab strip and collapsible behavior (resizable left edge via splitter)
7. View > Toggle Bottom Panel (Ctrl+J) and View > Toggle Right Panel work
8. Both bottom and right panels are hidden by default on first launch

**Acceptance Criteria**:
- Tab strip appears above the editor area
- Video tab is always leftmost and has no close button
- Settings opens as a new tab when gear icon is clicked
- Tabs can be reordered by dragging
- Tabs can be closed with the X button (except Video tab)
- Bottom panel can be toggled via menu or Ctrl+J
- Right panel can be toggled via menu
- Panel heights/widths are remembered when toggling

---

### vi-011: Bottom Panel — Output Log

**Goal**: Implement the bottom panel with an Output Log tab that logs application events.

**Tasks**:
1. Create `OutputLogPanel.xaml` — scrollable log view matching VS Code's Output panel:
   - Timestamped entries
   - Color-coded by level: Info (#cccccc), Warning (#cca700), Error (#f44747)
   - Auto-scroll to bottom (with option to unlock scroll)
   - Clear log button in the tab header
   - Filter by level (optional dropdown or toggle buttons)
2. Create `OutputLogViewModel` — observes `ILogService`, formats entries for display
3. Wire the bottom panel tab strip: "Output" is the first tab
4. Log meaningful events through the app: folder opened, video loaded, video played/paused/stopped, errors
5. During development, log click actions and user interactions for debugging purposes
6. Bottom panel is collapsible, resizable, and remembers its state

**Acceptance Criteria**:
- Bottom panel shows with "Output" tab
- Log entries appear with timestamps and color coding
- Auto-scroll works (scrolls to bottom on new entries)
- Clear button empties the log
- Opening a folder, loading a video, play/pause all generate log entries
- Panel is resizable and collapsible
- Panel state persists across restarts

---

### vi-012: Right Panel — Video Details

**Goal**: Implement the right panel showing metadata about the currently playing video.

**Tasks**:
1. Create `VideoDetailsPanel.xaml` — metadata display panel:
   - Section header: "Video Information"
   - Fields displayed in a clean label/value layout:
     - File Name
     - File Path
     - File Size (formatted: KB/MB/GB)
     - Duration (HH:MM:SS)
     - Resolution (width x height)
     - Codec (video codec name, e.g., H.264)
     - Audio Codec
     - Frame Rate (FPS)
     - Bitrate (formatted: Kbps/Mbps)
     - Container Format
   - Empty state: "No video loaded" centered text
2. Create `VideoDetailsViewModel` — subscribes to `VideoLoadedEvent`, updates metadata
3. Create `VideoMetadata` model with all fields
4. Extract metadata from FFmpeg during video load and publish via event bus
5. Wire right panel tab strip: "Video Info" is the first tab
6. View menu: Toggle Right Panel works and remembers state

**Acceptance Criteria**:
- Right panel shows with "Video Info" tab
- When a video is loaded, all metadata fields populate correctly
- Metadata is formatted nicely (human-readable sizes, durations, etc.)
- When no video is loaded, shows empty state message
- Panel is resizable and collapsible
- Fields update when switching to a different video

---

### vi-013: Status Bar

**Goal**: Implement the status bar with video information, matching VS Code style.

**Tasks**:
1. Create `StatusBarViewModel` — manages status bar items
2. Implement left-aligned items:
   - Current file name (or "No file" when nothing loaded)
3. Implement right-aligned items:
   - Resolution (e.g., "1920×1080")
   - Duration (e.g., "01:23:45")
   - Codec (e.g., "H.264")
4. Items appear/disappear based on whether a video is loaded
5. Style the status bar to match VS Code exactly (background, text size, padding, separator dots)
6. Implement the status bar item registry — internal mechanism for adding/removing/updating items from code (will be exposed to plugins later)

**Acceptance Criteria**:
- Status bar shows at the bottom of the window
- When no video loaded: shows "No file" or similar
- When video loaded: shows filename (left), resolution, duration, codec (right)
- Info updates when switching videos
- Status bar styling matches VS Code Dark Modern
- View > Toggle Status Bar works

---

### vi-014: Keyboard Shortcuts System

**Goal**: Implement comprehensive keyboard shortcuts with a registry system for future extensibility.

**Tasks**:
1. Implement `IKeyboardShortcutService` / `KeyboardShortcutService`:
   - Registry of `KeyBinding` → `Action` mappings
   - Support for modifier keys (Ctrl, Shift, Alt)
   - Conflict detection (warn on duplicate bindings)
2. Register all default bindings:
   - `Space` → Play/Pause
   - `S` → Stop
   - `M` → Mute/Unmute
   - `F` or `F11` → Toggle Fullscreen
   - `Escape` → Exit Fullscreen
   - `Ctrl+O` → Open File
   - `Ctrl+Shift+O` → Open Folder
   - `Ctrl+B` → Toggle Sidebar
   - `Ctrl+J` → Toggle Bottom Panel
   - `Ctrl+=` → Zoom In
   - `Ctrl+-` → Zoom Out
   - `Up Arrow` → Volume Up (by 5%)
   - `Down Arrow` → Volume Down (by 5%)
   - `Page Up` → Skip to Previous Video
   - `Page Down` → Skip to Next Video
3. Wire keyboard input in the main window (PreviewKeyDown) to route through the shortcut service
4. Ensure shortcuts don't fire when the user is typing in a text input
5. Write unit tests for the shortcut registry, binding, and conflict detection

**Acceptance Criteria**:
- All listed keyboard shortcuts work correctly
- Shortcuts do not fire when typing in text fields (e.g., search or settings)
- Shortcut service is extensible — plugins will be able to register new bindings
- Unit tests pass for registry operations

---

### vi-015: Fullscreen Mode

**Goal**: Implement fullscreen video playback with overlay controls.

**Tasks**:
1. Fullscreen toggle via F11, F key, or double-click on video area
2. When entering fullscreen:
   - Hide: title bar, menu bar, activity bar, sidebar, status bar, bottom panel, right panel, tab strip
   - Video fills the entire screen
   - Player controls overlay at the bottom, initially visible
   - Controls fade out after 3 seconds of no mouse movement
   - Controls fade in when mouse moves
   - Mouse cursor hides with controls (after 3 seconds of inactivity)
3. When exiting fullscreen (Escape, F11, F, or double-click):
   - Restore all UI elements to previous state
   - Window returns to previous size/position
4. Smooth fade animation for controls (200ms fade in/out)
5. Controls overlay should have semi-transparent dark gradient background at the bottom

**Acceptance Criteria**:
- F11, F, and double-click all toggle fullscreen
- All UI chrome hides in fullscreen
- Controls overlay appears on mouse movement, fades out after 3 seconds
- Mouse cursor hides with controls
- Escape exits fullscreen
- Previous window state is restored correctly
- Video fills the entire monitor
- Fullscreen works with multi-monitor setups (fullscreens on the current monitor)

---

### vi-016: State Persistence — Full Implementation

**Goal**: Complete the full state persistence system so Vido remembers everything between sessions.

**Tasks**:
1. Persist and restore:
   - Window position (X, Y) and size (Width, Height)
   - Window maximized state
   - Open folder path
   - Last played video file path
   - Last playback position (TimeSpan)
   - Sidebar visibility and width
   - Bottom panel visibility and height
   - Right panel visibility and width
   - Active sidebar tab (Explorer, Extensions, Settings)
   - Volume level
   - Mute state
   - Loop state
   - Hidden files list (from "Remove" action in explorer)
   - Recent files list (last 10)
2. State saves on a 500ms debounce (not on every change)
3. On startup: restore window → restore folder → restore video (paused at last position) → restore panels
4. Write unit tests for state serialization/deserialization

**Acceptance Criteria**:
- Close the app, reopen: window is in the same position/size
- Close the app with a folder open, reopen: same folder is open in explorer
- Close while playing a video at 5:30, reopen: video is loaded, paused at 5:30
- All panel states (visibility, size) persist
- Volume, mute, loop state persist
- Recent Files shows the last 10 opened files
- Hidden files remain hidden after restart

---

### vi-017: Drag and Drop

**Goal**: Implement drag-and-drop support for video files.

**Tasks**:
1. Enable drag-and-drop on the main window
2. When a video file is dropped on the **video player area**: load and play the video
3. When a video file is dropped on the **file explorer area**: open the file's parent folder and select the file
4. When a non-video file is dropped: show a tooltip/notification "File type not supported"
5. Visual feedback during drag:
   - Drag-over effect: subtle border highlight on the drop target area
   - Drag cursor shows appropriate icon (copy/move indicator)
6. Support both single file and folder drops (folder drop = Open Folder)
7. Support dropping from Windows Explorer

**Acceptance Criteria**:
- Dragging a video file onto the player area loads and plays it
- Dragging a video file onto the explorer opens its parent folder
- Dragging a folder onto the window opens it in the explorer
- Dragging a non-video file shows "not supported" feedback
- Visual feedback is shown during drag-over
- Files from Windows Explorer work correctly

---

### vi-018: Plugin System — Core Infrastructure

**Goal**: Implement the complete plugin loading, lifecycle, and API infrastructure. This is the foundation for all future extensibility.

**Tasks**:
1. Implement `PluginLoader`:
   - Scan `%APPDATA%/Vido/plugins/` for subdirectories
   - Parse `plugin.json` manifests
   - Validate manifest schema and version compatibility
   - Load assemblies from plugin directories
   - Instantiate `IVidoPlugin` implementations via reflection
2. Implement `PluginContext` (implements `IPluginContext`):
   - Provides access to EventBus, VideoEngine, LogService, SettingsStore
   - All UI registration methods (sidebar, bottom panel, right panel, status bar, toolbar buttons, context menus, file handlers, file icons, key bindings)
3. Implement `IPluginSettingsStore` / per-plugin settings stored in `%APPDATA%/Vido/plugins/<id>/settings.json`
4. Implement plugin activation lifecycle: discover → validate → load → activate
5. Implement plugin deactivation on shutdown
6. Create the contribution registry — all UI contributions from all plugins are collected and dispatched to the appropriate UI containers
7. Wire plugin-contributed sidebar panels into the activity bar (new icons appear)
8. Wire plugin-contributed bottom/right panels into respective tab strips
9. Wire plugin-contributed status bar items into the status bar
10. Wire plugin-contributed toolbar buttons into the title bar area (right of menu bar)
11. Wire plugin-contributed context menu items into the file explorer context menus
12. Wire plugin-contributed file handlers into file double-click dispatch
13. Wire plugin-contributed file icons into the explorer's icon resolution
14. Write extensive unit tests for PluginLoader, PluginContext, and contribution registry

**Acceptance Criteria**:
- If a plugin directory exists with valid `plugin.json` and DLL, it is loaded and activated on startup
- Plugin's `Activate()` is called with a valid `IPluginContext`
- Plugin's `Deactivate()` is called on shutdown
- Plugin-contributed UI elements appear in their correct locations
- Invalid plugins are logged as errors but don't crash the app
- All unit tests pass

---

### vi-019: Plugin Manager — Sidebar Panel

**Goal**: Implement the Plugin Manager sidebar panel for browsing, installing, and managing plugins.

**Tasks**:
1. Create `PluginManagerViewModel`:
   - Fetches `registry.json` from configured GitHub URL
   - Parses plugin entries
   - Compares with locally installed plugins (shows installed/available/update available)
   - Search/filter by name, description, tags
2. Create `PluginManagerPanel.xaml` — matching VS Code's Extensions panel:
   - Search text box at the top with magnifying glass icon
   - "INSTALLED" section listing installed plugins with version, enable/disable toggle, uninstall button
   - "AVAILABLE" section listing registry plugins with install button
   - Each plugin entry shows: icon, name, version, author, short description
   - Update badge on plugins with available updates
3. Implement `PluginInstaller`:
   - Download `.zip` from `downloadUrl`
   - Extract to `%APPDATA%/Vido/plugins/<plugin-id>/`
   - Validate `plugin.json` exists in extracted content
   - Reload plugins (for newly installed) or flag for restart
4. Implement uninstall: Deactivate plugin, delete directory, flag for cleanup on restart
5. Implement enable/disable toggle (persisted in settings)
6. Implement "Check for Updates" — compare local versions with registry versions, offer update

**Acceptance Criteria**:
- Plugin Manager panel appears in sidebar when Extensions icon is clicked in activity bar
- Search filters plugins by name/description
- Installed plugins show with correct version and status
- Available plugins from registry are displayed (requires internet and a valid registry URL)
- Install downloads and extracts plugin correctly
- Uninstall removes plugin directory
- Enable/disable toggle works (disabled plugins are not loaded on next startup)
- Update detection works when registry has newer versions

---

### vi-020: Settings Panel — Tab-Based

**Goal**: Implement the Settings panel that opens as a tab (like VS Code Settings).

**Tasks**:
1. Create `SettingsViewModel` — manages all application settings as observable properties
2. Create `SettingsPanel.xaml` — matching VS Code Settings UI:
   - Search bar at top ("Search settings...")
   - Categorized sections with collapsible headers:
     - **Playback**: Volume default, Loop default
     - **Appearance**: (reserved for future theme selection)
     - **File Explorer**: Show hidden files toggle
     - **Plugins**: Registry URL, auto-update plugins toggle, update check interval
   - Each setting shows: label, description text, and appropriate input (toggle, slider, text box, dropdown)
   - Settings save immediately on change (debounced)
3. When the Settings activity bar icon is clicked, open settings as a **tab** (not in the sidebar)
4. Settings tab can be closed like any other tab
5. Plugin settings: when a plugin declares settings in its manifest, they appear under a "[Plugin Name]" section in the Settings tab
6. Settings search filters visible settings by matching against label and description text

**Acceptance Criteria**:
- Clicking Settings gear in activity bar opens a Settings tab
- Settings tab shows categorized settings with search
- Changing a setting persists immediately
- Plugin settings appear under their own section
- Settings tab can be closed and reopened
- Search filters settings correctly
- Settings styling matches VS Code's Settings page

---

### vi-021: Dockable Panels — Drag to Dock

**Goal**: Implement drag-to-dock functionality for bottom and right panels.

**Tasks**:
1. Implement drag-to-dock for panel tabs:
   - User can drag a tab from the bottom panel to the right panel (and vice versa)
   - Visual feedback during drag: show drop target indicator (highlighted border on target panel)
   - When a tab is dropped on a different panel, it moves there
   - The tab's content renders correctly in its new location
2. Panel tabs from either panel can be dragged to the other:
   - Bottom panel tab → Right panel: tab moves to right panel tab strip
   - Right panel tab → Bottom panel: tab moves to bottom panel tab strip
3. If a panel has no tabs left, it collapses automatically
4. If a tab is dragged to a collapsed panel, the panel expands automatically
5. Persist docked positions in state

**Acceptance Criteria**:
- Dragging a tab from bottom to right panel moves it correctly
- Dragging a tab from right to bottom panel moves it correctly
- Content renders correctly in the new location
- Empty panels collapse automatically
- Panel expansion works when dragging to a collapsed panel
- Docked positions persist across restarts
- Visual feedback (drop indicators) appears during drag

---

### vi-022: Sample Plugin — Hello World

**Goal**: Create a sample "Hello World" plugin that demonstrates the entire plugin API. This validates that the plugin system works end-to-end and serves as documentation for plugin developers.

**Tasks**:
1. Create a separate project: `Vido.SamplePlugin` (a .NET 8 class library)
2. The plugin should demonstrate ALL extension points:
   - **Sidebar panel**: A "Hello World" panel with a text message and a button
   - **Bottom panel tab**: A "Sample Log" tab that logs custom messages
   - **Status bar item**: Shows "Sample Plugin v1.0" on the right side of status bar
   - **Toolbar button**: A button in the title bar area that, when clicked, shows a message in the log
   - **Context menu item**: "Hello from Plugin" on all files — when clicked, logs the filename
   - **File handler**: Handles `.sample` files — when double-clicked, logs "Opened sample file: <name>"
   - **File icon**: Custom icon for `.sample` files
   - **Settings**: One boolean setting "Enable Greeting" and one string setting "Greeting Text"
   - **Key binding**: Ctrl+Shift+H → logs "Hello from keyboard shortcut!"
3. Create proper `plugin.json` manifest
4. Document the plugin's code with comments explaining each API usage
5. Build plugin output goes to `%APPDATA%/Vido/plugins/com.vido.sample-plugin/`

**Acceptance Criteria**:
- Plugin loads automatically on app startup
- All contributed UI elements appear in their correct locations
- Sidebar panel shows in activity bar with custom icon
- Bottom panel tab appears in bottom panel tab strip
- Status bar item appears
- Toolbar button appears and works
- Context menu item appears on right-click in file explorer
- File handler works for `.sample` files
- Custom icon appears for `.sample` files in explorer
- Settings appear in Settings tab under "Sample Plugin" section
- Keyboard shortcut works
- Plugin can be disabled/enabled from the Plugin Manager
- Plugin serves as complete documentation of the plugin API

---

### vi-023: File Associations & Open File Command

**Goal**: Implement opening files from the command line, File > Open File, and prepare for installer file associations.

**Tasks**:
1. Implement `File > Open File` menu item:
   - Opens a file dialog filtered to video file types (.mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
   - Selected file loads and plays in the video tab
   - Also opens the file's parent directory in the explorer (if no folder is open, or if different from current)
2. Implement command-line argument handling:
   - `Vido.exe "C:\path\to\video.mp4"` → opens and plays the file
   - `Vido.exe "C:\path\to\folder\"` → opens the folder in explorer
3. Update `File > Recent Files` submenu to show last 10 opened files
   - Clicking a recent file opens and plays it
   - Recent files list persists in state
4. Prepare file association registry keys (used by installer):
   - Define which extensions to associate (.mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
   - Write a helper method that registers associations (called by installer, not by app directly)

**Acceptance Criteria**:
- File > Open File opens a dialog and plays the selected video
- Command-line file argument loads and plays the video
- Command-line folder argument opens the folder in explorer
- Recent Files submenu shows last 10 files and works
- Recent files persist across app restarts

---

### vi-024: Playback Speed Control

**Goal**: Implement playback speed control throughout the application.

**Tasks**:
1. Add playback speed control to the video engine:
   - Supported speeds: 0.25x, 0.5x, 0.75x, 1.0x, 1.25x, 1.5x, 2.0x
   - Speed affects both video and audio presentation rate
   - Audio pitch correction at non-1.0x speeds (maintain pitch or allow pitch shift — maintain pitch preferred using FFmpeg's `atempo` filter or resampling)
2. Add speed selector to player controls bar:
   - Small dropdown/button showing current speed (e.g., "1.0x")
   - Click opens a popup with speed options
3. Wire `Playback > Playback Speed` submenu to the speed options
4. Persist current speed in state

**Acceptance Criteria**:
- All speed options work correctly
- Audio pitch is maintained at different speeds (no chipmunk/slow-motion audio)
- Speed indicator shows current speed in the controls bar
- Playback Speed submenu works from the menu bar
- Speed persists across app restart

---

### vi-025: Zoom In/Out

**Goal**: Implement zoom in/out functionality for the video display.

**Tasks**:
1. Implement zoom levels: 50%, 75%, 100%, 125%, 150%, 200%, Fit (default)
2. "Fit" mode scales the video to fit the available area while maintaining aspect ratio (default)
3. When zoomed beyond fit, show scrollbars to pan around the video
4. Ctrl+= zooms in, Ctrl+- zooms out, Ctrl+0 resets to Fit
5. Wire `View > Zoom In` and `View > Zoom Out` menu items
6. Show current zoom level in status bar (optional)

**Acceptance Criteria**:
- Zoom in/out works via keyboard shortcuts and menu
- Video scales correctly at all zoom levels
- Aspect ratio is maintained
- Scrollbars appear when zoomed beyond fit
- Fit mode correctly fills available space
- Ctrl+0 resets to Fit

---

### vi-026: About Dialog & Help Menu

**Goal**: Implement the Help menu items including an About dialog.

**Tasks**:
1. Create `AboutDialog.xaml` — modal dialog showing:
   - Vido logo
   - Version number (read from assembly version)
   - "Vido — Ultra-Performant Video Player"
   - .NET version
   - FFmpeg version
   - Link to project repository (if applicable)
   - OK button to close
   - Styled to match VS Code's About dialog
2. Wire `Help > About Vido` to show the dialog
3. Wire `Help > Check for Updates`:
   - For v1: shows a message "You are running the latest version" (no actual update check)
   - Placeholder for future auto-update functionality

**Acceptance Criteria**:
- About dialog shows correctly with all information
- Version number is accurate
- Dialog styling matches Dark Modern theme
- Check for Updates shows placeholder message
- Dialog is modal and closes with OK or Escape

---

### vi-027: Performance Optimization Pass

**Goal**: Profile and optimize the application for ultra-performance.

**Tasks**:
1. **Startup time**: 
   - Measure cold start time. Target: <1 second to visible window
   - Lazy-load non-critical services (plugin loading, registry fetch)
   - Use Ngen or ReadyToRun for faster JIT
2. **Video rendering**:
   - Ensure hardware-accelerated decoding (DXVA2/D3D11VA) is being used when GPU supports it
   - Consider D3DImage for zero-copy GPU → WPF rendering (if not already implemented)
   - Profile frame drop rate — target 0 dropped frames at native frame rate
3. **Window resizing**:
   - Ensure smooth resize with no flickering (use `RenderOptions.ProcessRenderMode = RenderMode.Default`)
   - Video should not flicker or show black frames during resize
4. **Memory**:
   - Profile memory usage during playback
   - Ensure no memory leaks (check for unsubscribed event handlers, undisposed resources)
   - Target: <150MB base memory, <300MB during 1080p playback
5. **Scrolling & Animation**:
   - File explorer tree scrolling should be butter-smooth (virtualized tree)
   - All animations should be GPU-accelerated where possible
   - Log panel should use virtualization for large log counts
6. Log performance metrics to the Output panel for transparency

**Acceptance Criteria**:
- App starts in <1 second (warm cache) / <2 seconds (cold start)
- Video playback has zero dropped frames at native frame rate
- Window resizing is smooth with no black frames or flickering
- Memory stays within targets
- File explorer with 1000+ files scrolls smoothly
- No memory leaks over 30-minute playback sessions

---

### vi-028: Installer & Portable Distribution

**Goal**: Create both a portable zip distribution and an MSI installer.

**Tasks**:
1. **Portable build**:
   - Configure `dotnet publish` for self-contained, single-directory (not single-file, since FFmpeg DLLs are separate), win-x64, trimmed where safe
   - Include FFmpeg DLLs in the output
   - Include the sample plugin in `plugins/` subdirectory
   - Create a PowerShell script or batch file to create the zip
   - Target: smallest possible zip size
   - Test: unzip anywhere and run `Vido.exe` — everything should work
2. **Installer (WiX 5)**:
   - Create a WiX project for MSI generation
   - Installer includes: all binaries, FFmpeg DLLs, sample plugin
   - Optional: register file associations for video types (user-selectable checkboxes during install)
   - Creates Start Menu shortcut
   - Creates Desktop shortcut (optional checkbox)
   - Uninstaller removes everything cleanly (but preserves `%APPDATA%/Vido/` for user data)
   - Installer UI should be minimal and clean
3. Document the build process in a `BUILD.md` file at the solution root

**Acceptance Criteria**:
- `dotnet publish` produces a working portable distribution
- Portable zip runs from any directory without installation
- MSI installer installs correctly
- File associations work when selected during install
- Shortcuts are created
- Uninstall is clean
- Portable zip is as small as possible (document final size)
- `BUILD.md` documents the entire build/publish process

---

### vi-029: Final Review & Polish

**Goal**: Final pass over the entire codebase for quality, consistency, and completeness.

**Tasks**:
1. **Code review pass**:
   - Remove ALL dead code, unused usings, commented-out code
   - Ensure consistent naming conventions throughout
   - Ensure all public APIs have XML doc comments
   - Check for any TODO/HACK/FIXME comments and resolve them
   - Verify every interface has a corresponding implementation registered in DI
2. **UI polish pass**:
   - Pixel-compare every UI element with VS Code Dark Modern screenshots
   - Fix any color, spacing, font, or alignment discrepancies
   - Ensure all hover/focus/active states match VS Code
   - Test with different Windows DPI settings (100%, 125%, 150%, 200%)
   - Test with different window sizes (minimum, maximized, various aspect ratios)
3. **Testing pass**:
   - Run all unit tests and ensure 100% pass rate
   - Execute all manual test plans from all previous tickets
   - Test with various video formats: .mp4 (H.264), .mp4 (H.265/HEVC), .avi, .mkv, .mov, .wmv, .flv, .webm
   - Test with large files (>4GB)
   - Test with very short files (<1 second)
4. **Documentation**:
   - Create `README.md` in the solution root with:
     - Project overview
     - Build instructions
     - Usage guide
     - Plugin development guide (brief, linking to full Plugin API doc)
   - Create `PLUGIN_DEVELOPMENT.md` with complete plugin development guide:
     - Step-by-step from creating a project to publishing
     - Full API reference
     - Code examples for every extension point
     - Manifest schema reference
5. **Changelog**: Create `CHANGELOG.md` with all changes from every ticket

**Acceptance Criteria**:
- Zero dead code in the entire solution
- All public APIs are documented
- UI matches VS Code Dark Modern at all tested DPI settings
- All unit tests pass
- All video formats play correctly
- README.md is comprehensive
- PLUGIN_DEVELOPMENT.md is complete and accurate
- CHANGELOG.md lists all changes

---

## 9. Developer Instructions

### 9.1 Getting Started (Before vi-001)

Before starting the first ticket, ensure you have:

1. **.NET 8 SDK** installed (verify with `dotnet --version`)
2. **Visual Studio 2022** or **VS Code with C# DevKit** (either works)
3. **FFmpeg native DLLs** — provided automatically by the `FFmpeg.LGPL` NuGet package (no manual download needed)
4. **Git** initialized: `git init` in the `c:\source\vido` directory

### 9.2 Per-Ticket Workflow

For every ticket, follow this exact workflow:

```
1. Read the ticket requirements fully
2. Implement the feature(s)
3. Write unit tests (if applicable)
4. Run `dotnet build` — must succeed with zero errors
5. Run `dotnet test` — all tests must pass
6. Launch the app and manually verify the new functionality
7. Review ALL changed files:
   - Remove dead code
   - Remove unused usings
   - Clean up formatting
   - Ensure consistent naming
8. Create `Context/TEST_PLANS/vi-XXX_test_plan.md` with manual + regression tests
9. Output the Changelog (bullet list of changes)
10. Output the Git commit message (conventional commit format)
```

### 9.3 Commit Message Format

```
feat(vi-XXX): <short description>

- <detail 1>
- <detail 2>
- <detail 3>
```

Example:
```
feat(vi-002): implement custom title bar with window controls

- Custom frameless title bar matching VS Code Dark Modern
- Minimize, maximize/restore, close buttons with correct hover effects
- Double-click title bar to maximize/restore
- Created Colors.xaml and Brushes.xaml theme foundations
```

For bug fix tickets:
```
fix(vi-b-XXX): <short description>
```

### 9.4 Test Plan Template

Each `Context/TEST_PLANS/vi-XXX_test_plan.md` should follow this structure:

```markdown
# vi-XXX: <Ticket Title> — Test Plan

## Manual Tests

### MT-1: <Test Name>
**Steps:**
1. <Step 1>
2. <Step 2>
**Expected Result:** <What should happen>

### MT-2: <Test Name>
...

## Regression Tests

### RT-1: <Test Name>
**Precondition:** <What must be true before testing>
**Steps:**
1. <Step 1>
**Expected Result:** <Previous functionality still works>

## Unit Tests
- [ ] <TestClassName>.<TestMethodName> — <what it tests>
- [ ] ...
```

### 9.5 Quality Standards

- **No dead code**: Every function must be called. Every variable must be used. Every import must be needed.
- **No magic numbers**: Use named constants or settings for all numerical values.
- **No string literals in logic**: Use constants or resource files for strings shown to users.
- **Consistent error handling**: Log errors via `ILogService`, never swallow exceptions silently.
- **Thread safety**: All cross-thread access must be properly marshalled. Use `Dispatcher.Invoke/BeginInvoke` for UI updates from background threads.
- **Performance first**: When in doubt, choose the faster approach. Profile hot paths.
- **Readability**: Code should look like a senior human developer wrote it — clear names, logical organization, appropriate comments (explain "why", not "what").

### 9.6 Plugin Registry Setup (Human Action Required)

To test the full plugin lifecycle (browse, install, update, uninstall), a human must:

1. Create a GitHub repository (e.g., `vido-plugin-registry`)
2. Add a `registry.json` file following the schema in Section 7.1
3. For each plugin you want listed:
   - Build the plugin
   - Create a `.zip` of the plugin directory
   - Create a GitHub release on the plugin's repo with the zip attached
   - Add the plugin entry to `registry.json` with the correct `downloadUrl`
4. In Vido's Settings tab, set the Registry URL to: `https://raw.githubusercontent.com/<owner>/vido-plugin-registry/main/registry.json`

For **local development/testing** without internet:
- Place plugin directories directly in `%APPDATA%/Vido/plugins/<plugin-id>/`
- The Plugin Manager's "Installed" section will show them regardless of registry

### 9.7 Supported Video Formats (Base Player)

The following formats must work in the base player via FFmpeg:

| Extension | Container | Notes |
|-----------|-----------|-------|
| `.mp4` | MPEG-4 Part 14 | H.264/H.265 video, AAC audio. Most common format. |
| `.avi` | AVI | Various codecs. Legacy but still widely used. |
| `.mkv` | Matroska | H.264/H.265/VP9 video. Popular for downloads. |
| `.mov` | QuickTime | H.264 video, AAC audio. Common from Apple devices. |
| `.wmv` | ASF | Windows Media Video. Legacy format. |
| `.flv` | Flash Video | H.264/FLV1 video. Legacy but still encountered. |
| `.webm` | WebM | VP8/VP9/AV1 video, Opus/Vorbis audio. Web format. |

### 9.8 File Icon Mapping

| File Type | Icon | Description |
|-----------|------|-------------|
| `.mp4`, `.avi`, `.mkv`, `.mov`, `.wmv`, `.flv`, `.webm` | `video-file.png` | Video file icon (film strip or play button motif) |
| Directories | `folder.png` / `folder-open.png` | Closed/open folder icons |
| All other files | `generic-file.png` | Simple generic document icon |

Icons should be 16x16 for tree view and 24x24 for activity bar. Use monochrome/duotone style matching VS Code Codicons.

---

## Appendix A: VS Code Dark Modern — Complete Color Reference

For exhaustive color reference, consult the VS Code source:
- Theme definition: https://github.com/microsoft/vscode/blob/main/extensions/theme-defaults/themes/dark_modern.json
- Color registry: https://github.com/microsoft/vscode/blob/main/src/vs/platform/theme/common/colorRegistry.ts

The colors listed in Section 4.1 cover the most critical UI elements. When implementing any component not listed, cross-reference the VS Code source above.

## Appendix B: FFmpeg.AutoGen Integration Notes

- NuGet packages: `FFmpeg.AutoGen.Abstractions` + `FFmpeg.AutoGen.Bindings.DynamicallyLoaded` (8.0.0)
- Native DLLs: `FFmpeg.LGPL` NuGet package (provides avcodec-62, avformat-62, avutil-60, swscale-9, swresample-6 automatically)
- Set `DynamicallyLoadedBindings.LibrariesPath` to the directory containing FFmpeg DLLs, then call `DynamicallyLoadedBindings.Initialize()`
- All FFmpeg functions are accessed via static methods on `ffmpeg` class (e.g., `ffmpeg.avformat_open_input(...)`)
- Key type mappings in v8.0: `byte_ptr4` (not `byte_ptrArray4`), `int4` (not `int_array4`), `sws_scale` takes `byte*[]` and `int[]`
- Use `AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA` for hardware acceleration on Windows
- Decode loop pattern: `av_read_frame()` → `avcodec_send_packet()` → `avcodec_receive_frame()` → `sws_scale()` → copy to WriteableBitmap
- For audio: `avcodec_receive_frame()` → `swr_convert()` → write to WASAPI buffer via NAudio

## Appendix C: Ticket Dependency Graph

```
vi-001 (Scaffold)
  └→ vi-002 (Title Bar)
       └→ vi-003 (Menu Bar)
  └→ vi-004 (Core Layout)
       └→ vi-005 (Event Bus & Services)
            └→ vi-006 (File Explorer)
                 └→ vi-007 (Context Menus)
            └→ vi-008 (FFmpeg Engine)
                 └→ vi-009 (Video Player UI)
                      └→ vi-015 (Fullscreen)
                      └→ vi-024 (Playback Speed)
                      └→ vi-025 (Zoom)
            └→ vi-010 (Tab System)
                 └→ vi-011 (Output Log)
                 └→ vi-012 (Video Details)
                 └→ vi-020 (Settings Panel)
                 └→ vi-021 (Docking)
            └→ vi-013 (Status Bar)
            └→ vi-014 (Keyboard Shortcuts)
            └→ vi-016 (State Persistence)
            └→ vi-017 (Drag & Drop)
       └→ vi-018 (Plugin System)
            └→ vi-019 (Plugin Manager)
            └→ vi-022 (Sample Plugin)
  └→ vi-023 (File Associations)
  └→ vi-026 (About Dialog)
  └→ vi-027 (Performance)
  └→ vi-028 (Distribution)
  └→ vi-029 (Final Review)
```

Tickets should be executed in the order listed (vi-001 through vi-029). Some tickets have no hard dependency on the immediately preceding ticket, but the listed order provides the most logical incremental build-up of functionality.
