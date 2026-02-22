# Vido

A performant, extensible video player for Windows built with WPF, .NET 8, and FFmpeg. Designed as a VS Code Dark Modern visual clone with a fully modular plugin system.

## Features

- **Hardware-accelerated video playback** — D3D11VA / DXVA2 GPU decoding with automatic software fallback
- **VS Code-inspired UI** — custom frameless window, activity bar, sidebar, bottom/right panels, tab system, status bar
- **File explorer** — tree view with lazy-loading, context menus, drag-and-drop, hidden file management
- **Plugin system** — extensible architecture supporting sidebar panels, bottom/right tabs, status bar items, toolbar buttons, context menus, file handlers, custom file icons, keyboard shortcuts, and per-plugin settings
- **Plugin manager** — browse, install, update, and uninstall plugins from configurable registries (including local `file://` paths for development)
- **Full state persistence** — window geometry, open folder, last video + position, panel layout, volume, playback speed, recent files
- **Keyboard shortcuts** — comprehensive default bindings with extensible registry
- **Fullscreen mode** — F11/F/double-click with auto-hiding overlay controls
- **Performance optimized** — frame buffer pooling (ArrayPool), TreeView virtualization, deferred plugin activation, ReadyToRun compilation, playback metrics logging

## Requirements

- **Windows 10/11** (x64)
- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)

FFmpeg native DLLs are provided automatically via the `FFmpeg.LGPL` NuGet package.

## Building

```powershell
# Clone and build
git clone https://github.com/your-org/vido.git
cd vido
dotnet build Vido.sln

# Run
dotnet run --project src/Vido.App

# Run tests
dotnet test

# Publish (self-contained, portable)
dotnet publish src/Vido.App -c Release -r win-x64 --self-contained
```

## Usage

### Opening Videos

- **File > Open File** (Ctrl+O) — browse for a video file
- **File > Open Folder** (Ctrl+Shift+O) — open a folder in the explorer sidebar
- **Drag & drop** — drop video files or folders onto the window
- **Command line** — `Vido.exe path/to/video.mp4` or `Vido.exe path/to/folder`
- **Double-click** — double-click a video file in the explorer sidebar

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Space | Play / Pause |
| S | Stop |
| M | Toggle Mute |
| Up / Down | Volume ±5% |
| Page Up / Page Down | Previous / Next video in folder |
| F11 / F | Toggle Fullscreen |
| Escape | Exit Fullscreen |
| Ctrl+O | Open File |
| Ctrl+Shift+O | Open Folder |
| Ctrl+K | Close Folder |
| Ctrl+Shift+R | Rescan Folder |
| Ctrl+B | Toggle Sidebar |
| Ctrl+J | Toggle Bottom Panel |
| Ctrl+H | Toggle Right Panel |

### Supported Video Formats

All formats supported by FFmpeg, including:
`.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.flv`, `.webm`

## Architecture

```
Vido.App           → Entry point, DI container, startup
Vido.Core          → Interfaces, models, events, plugin API (zero dependencies)
Vido.Services      → FFmpeg engine, file system, settings, state persistence
Vido.ViewModels    → MVVM ViewModels (CommunityToolkit.Mvvm)
Vido.Views         → WPF XAML views, themes, controls
Vido.PluginHost    → Plugin loading, lifecycle, API bridge
Vido.Tests         → xUnit tests (809 tests)
```

### Key Technologies

| Technology | Purpose |
|-----------|---------|
| WPF / .NET 8 | UI framework |
| FFmpeg.AutoGen 8.0 | Video decoding (P/Invoke bindings) |
| NAudio | WASAPI audio output |
| CommunityToolkit.Mvvm | MVVM source generators |
| Microsoft.Extensions.DI | Dependency injection |
| xUnit + Moq | Testing |

## Plugin Development

See [PLUGIN_DEVELOPMENT.md](PLUGIN_DEVELOPMENT.md) for a complete guide to creating Vido plugins.

### Quick Start

1. Create a .NET 8 class library referencing `Vido.Core.dll`
2. Implement `IVidoPlugin` with `Activate()` and `Deactivate()`
3. Create a `plugin.json` manifest
4. Copy to `%APPDATA%/Vido/plugins/your-plugin-id/`
5. Launch Vido — the plugin loads automatically

## Configuration

Settings are stored in `%APPDATA%/Vido/`:
- `settings.json` — user preferences (volume, playback speed, layout, plugin registries)
- `state.json` — session state (window position, last video, recent files)
- `plugins/` — installed plugins

## License

See [LICENSE](LICENSE) for details.
