# Vido

A performant video player for Windows built with WPF, .NET 8, and FFmpeg. Features a VS Code Dark Modern-inspired UI with built-in OSR2+ haptic device control, Pulse audio-to-haptics, and playlist management.

## Features

- **Hardware-accelerated video playback** — D3D11VA / DXVA2 GPU decoding with automatic software fallback
- **VS Code-inspired UI** — custom frameless window, activity bar, sidebar, bottom/right panels, tab system, status bar
- **File explorer** — tree view with lazy-loading, context menus, drag-and-drop, hidden file management
- **OSR2+ haptic device control** — TCode output via serial/UDP, funscript playback, axis control with fill modes, beat bar visualization, funscript visualizer (graph + heatmap)
- **Pulse audio-to-haptics** — real-time BPM detection, onset analysis, waveform visualization, automatic beat-driven haptic output
- **Playlist management** — create/save/load `.vidpl` playlists, drag-and-drop reordering, shuffle, auto-save, skip next/prev navigation
- **Full state persistence** — window geometry, open folder, last video + position, panel layout, volume, playback speed, recent files
- **Keyboard shortcuts** — comprehensive default bindings
- **Fullscreen mode** — F11/F/double-click with auto-hiding overlay controls
- **Performance optimized** — frame buffer pooling, TreeView virtualization, zero-allocation TCode hot paths, lock-free audio ring buffers, ReadyToRun compilation

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
Vido.Core          → Interfaces, models, events, haptic types
Vido.Services      → FFmpeg engine, file system, settings, OSR2+, Pulse, Playlists
Vido.ViewModels    → MVVM ViewModels (CommunityToolkit.Mvvm)
Vido.Views         → WPF XAML views, themes, controls
Vido.Tests         → xUnit tests (1617 tests)
```

### Key Technologies

| Technology | Purpose |
|-----------|---------|
| WPF / .NET 8 | UI framework |
| FFmpeg.AutoGen 8.0 | Video decoding (P/Invoke bindings) |
| NAudio | WASAPI audio output |
| SkiaSharp | Hardware-accelerated 2D rendering (visualizers, waveforms, beat bars) |
| CommunityToolkit.Mvvm | MVVM source generators |
| Microsoft.Extensions.DI | Dependency injection |
| System.IO.Ports | Serial port communication (OSR2+ device) |
| xUnit + NSubstitute | Testing |

## Configuration

Settings are stored in `%APPDATA%/Vido/`:
- `settings.json` — user preferences (volume, playback speed, layout, OSR2+, Pulse, Playlist settings)
- `state.json` — session state (window position, last video, recent files)

## License

See [LICENSE](LICENSE) for details.
