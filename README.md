# Vido

A video player for Windows with built-in haptic device control, beat-synchronized haptics, and playlist management. Inspired by the VS Code Dark Modern aesthetic.

Available as a **portable zip** (extract and run) or **MSI installer** (with file associations and Start Menu shortcut).

## Requirements

- **Windows 10/11** (x64)
- **.NET 8 Desktop Runtime** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Video Playback

Hardware-accelerated video decoding via FFmpeg with D3D11VA / DXVA2 GPU acceleration and automatic software fallback.

**Supported formats:** `.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.flv`, `.webm` — and anything else FFmpeg supports.

### Opening Videos

- **File > Open File** (Ctrl+O) — browse for a video file
- **File > Open Folder** (Ctrl+Shift+O) — open a folder in the file explorer sidebar
- **Drag & drop** — drop video files or folders onto the window
- **Command line** — `Vido.exe path/to/video.mp4` or `Vido.exe path/to/folder`
- **Double-click** — click a video in the file explorer sidebar
- **File association** — associate `.mp4`, `.mkv`, etc. with Vido and double-click from Windows Explorer

### Playback Controls

Standard transport controls in the bottom control bar: play/pause, stop, seek bar, volume slider, playback speed selector (0.25x – 2.0x), and mute toggle. Loop playback can be enabled in Settings.

### Fullscreen

Enter fullscreen with **F11**, **F**, or by double-clicking the video. The control bar auto-hides and reappears on mouse movement. Press **Escape** to exit.

---

## Interface

The UI follows the VS Code layout — a frameless window with an activity bar on the left, collapsible sidebar, bottom panel, right panel, and status bar.

### Activity Bar (Left Edge)

Five icons from top to bottom:

| Icon | Panel | Description |
|------|-------|-------------|
| Files | Explorer | File/folder tree view |
| Device | OSR2+ | Haptic device connection and axis control |
| Heart | Pulse | Audio beat detection and haptic output |
| List | Playlists | Playlist management |
| Gear | Settings | Application settings (pinned to bottom) |

Click an icon to open its sidebar panel. Click again to collapse the sidebar.

### File Explorer

Tree view of the currently opened folder with lazy-loading, context menus, and drag-and-drop. Supports showing/hiding hidden files (configurable in Settings). Right-click files to add them to the current playlist.

### Bottom Panel

Collapsible panel below the video with tabs:

- **Log Output** — application log messages (always present)
- **Funscript Visualizer** — real-time funscript graph or heatmap (added when OSR2+ is active)
- **Pulse Waveform** — pre-analyzed audio waveform with beat markers (added when Pulse analyzes a file)

Toggle with **Ctrl+J**. Tabs can be closed individually (except Log Output).

### Right Panel

- **Video Details** — file name, path, size, duration, resolution, video/audio codec, frame rate, bitrate, container format, audio channels, and sample rate.

Toggle with **Ctrl+H**.

### Status Bar

Bottom edge showing:

| Position | Content |
|----------|---------|
| Left | Current video filename, playlist status |
| Right | Duration, resolution, codec, OSR2+ connection status, Pulse state/BPM |

### Screenshots

Enable in Settings > Screenshot. A camera button appears in the title bar. Click it to capture the current frame — saved to your configured screenshot directory (defaults to `Pictures\Screenshots`). Plays a shutter sound on capture.

---

## OSR2+ Haptic Device Control

Control an OSR2+ haptic device via funscript playback with real-time axis visualization.

### Connecting

Open the **OSR2+** sidebar panel and choose a connection mode:

- **UDP** — network connection to `127.0.0.1` on a configurable port (default: 7777)
- **Serial** — direct COM port connection with configurable baud rate (9600 – 250,000)

Click **Connect**. The status bar shows connection state (e.g., `UDP:7777:Connected`). On connect, axes gradually home to their midpoint for a safe start.

### Axes

Four axes are supported, each with its own collapsible card in the sidebar:

| Axis | Name | Description |
|------|------|-------------|
| L0 | Stroke | Primary linear axis (±50% offset) |
| R0 | Twist | Rotational axis (0–179° offset) |
| R1 | Roll | Roll axis (±50% offset) |
| R2 | Pitch | Pitch axis (±50% offset) |

Per-axis controls:
- **Enable/Disable** toggle
- **Min/Max** amplitude range (0–100)
- **Position Offset** slider for real-time adjustment
- **Load Funscript** — manually assign a `.funscript` file to any axis
- **Sync with Stroke** — lock secondary axes to L0's timing

### Funscript Playback

When you load a video, Vido automatically looks for matching `.funscript` files in the same directory (e.g., `video.mp4` → `video.funscript` for L0, `video.twist.funscript` for R0, etc.). Manually loaded funscripts take priority over auto-detected ones.

### Fill Modes

When an axis has no funscript loaded, fill modes generate continuous movement patterns:

| Mode | Description |
|------|-------------|
| None | No movement — holds at midpoint |
| Random | Smooth random movement (cosine-interpolated) |
| Triangle | Linear ascending/descending wave |
| Sine | Smooth sinusoidal wave |
| Saw | Linear ramp up, instant drop |
| Reverse Saw | Instant snap up, linear ramp down |
| Square | Instant alternation between min and max |
| Pulse | Holds at extremes with quick transitions |
| Ease In/Out | Sine-like with sharper acceleration at extremes |

Fill speed is configurable (0.1–3.0 Hz) and can be independent or synced with L0.

### Beat Bar

A visual beat indicator overlay on the video. Modes:

- **Off** — no beat bar
- **On Peak** — markers at upstroke peaks
- **On Valley** — markers at downstroke valleys

When Pulse is active, it can register its own beat bar mode (red hearts synchronized to detected beats).

### Funscript Visualizer

A bottom panel tab showing real-time funscript data:

- **Graph mode** — multi-axis polyline graph with playback cursor and axis legend
- **Heatmap mode** — speed-based color heatmap for the Stroke axis

Window duration is configurable (30s, 60s, 2m, 5m).

### OSR2+ Settings

Available in Settings > OSR2+:

| Setting | Default | Range |
|---------|---------|-------|
| Default Connection Mode | UDP | UDP, Serial |
| Default UDP Port | 7777 | 1–65535 |
| Default Baud Rate | 115200 | 9600–250000 |
| TCode Output Rate | 100 Hz | 30–200 Hz |
| Global Funscript Offset | 0 ms | −500 to +500 ms |
| Visualizer Window Duration | 60s | 30s, 60s, 120s, 300s |

---

## Pulse — Audio Beat Detection

Pulse analyzes video audio to automatically detect beats and drive haptic output in sync with the music. When Pulse is enabled, it takes over the Stroke (L0) axis — other axes continue with their fill modes.

### How It Works

1. Open the **Pulse** sidebar panel
2. Toggle Pulse **on**
3. Load a video — Pulse automatically analyzes the audio track
4. Once analysis completes, play the video — L0 moves in sync with detected beats

### States

| State | Indicator | Description |
|-------|-----------|-------------|
| Inactive | Grey | Pulse is off |
| Analyzing | Yellow | Processing audio (progress bar shown) |
| Ready | Yellow | Analysis complete, waiting for playback |
| Active | Green | Driving haptics in sync with beats |
| Error | Red | Analysis failed |

The status bar shows the current state and BPM when active (e.g., `♥ Pulse: Active 128 BPM`).

### Beat Rate

Control how often the device responds to beats:

- Every Beat
- Every 2nd Beat
- Every 3rd Beat
- Every 4th Beat

### Waveform Panel

When Pulse analyzes a file, a **Pulse Waveform** tab appears in the bottom panel showing:

- Pre-analyzed RMS waveform envelope
- Beat marker overlay
- BPM readout
- Current playback position cursor
- Live amplitude display

Window duration options: 10s, 30s, 60s, 2m, 5m.

### Pulse Settings

Available in Settings > Pulse:

| Setting | Default | Range |
|---------|---------|-------|
| Beat Detection Sensitivity | 1.5 | 0.5–5.0 |
| Enable BPM Phase Lock | On | On/Off |
| Waveform Window Duration | 30s | 15s, 30s, 60s, 120s |

---

## Playlists

Create and manage playlists of video files with playback integration.

### Creating a Playlist

Open the **Playlists** sidebar panel. Use the toolbar buttons to:

- **New** — create an empty playlist
- **Open** — load a `.vidpl` playlist file
- **Save** / **Save As** — save the current playlist
- **Recent** — dropdown of up to 10 recently opened playlists

### Adding Files

- Click **Add Files** in the playlist toolbar
- Right-click files or folders in the File Explorer → **Add to Playlist**
- Drag and drop files from Windows Explorer into the playlist panel
- Drag and drop folders (files are added recursively)

Duplicate files are automatically skipped. Non-video files are rejected with an error toast.

### Organizing

- **Move Up / Down / Top / Bottom** — reorder selected items
- **Drag and drop** — reorder items within the playlist
- **Remove** — delete selected items from the playlist

### Playback

Double-click a playlist item to play it. Use **Page Up / Page Down** to skip to the previous/next item (playlist-aware navigation replaces the default folder-based skip). The currently playing item is highlighted.

The status bar shows playlist status: `Playing 2 of 10 — My Playlist` during playback, or `My Playlist — 10 items` when idle.

### Shuffle

Toggle shuffle to randomize playback order. The shuffled order has no repeats until all items have been played.

### Auto-Save & Restore

- **Auto-Save** (configurable in Settings > Playlists) — automatically saves the playlist on every add, remove, or reorder
- **Auto-Restore** — the last opened playlist is automatically restored when Vido starts

### Dropping Playlist Files

Drop a `.vidpl` file onto the playlist panel to open it directly.

### Playlist Settings

Available in Settings > Playlists:

| Setting | Default |
|---------|---------|
| Auto-Save Playlists | Off |

---

## Settings

Open Settings from the gear icon at the bottom of the activity bar. All settings are organized into categories with a search bar for filtering.

### Playback

| Setting | Default | Description |
|---------|---------|-------------|
| Default Volume | 50 | Starting volume (0–100) |
| Default Playback Speed | 1.0x | 0.25x, 0.5x, 1.0x, 1.5x, 2.0x |
| Loop Playback | Off | Auto-loop videos |

### File Explorer

| Setting | Default | Description |
|---------|---------|-------------|
| Show Hidden Files | Off | Show hidden files and folders |

### Screenshot

| Setting | Default | Description |
|---------|---------|-------------|
| Enable Screenshot Capture | Off | Shows camera button in title bar |
| Screenshot Save Directory | Pictures\Screenshots | Where captures are saved |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Space | Play / Pause |
| S | Stop |
| M | Toggle Mute |
| Up / Down | Volume ±5% |
| Page Up / Page Down | Previous / Next (playlist-aware) |
| F11 / F | Toggle Fullscreen |
| Escape | Exit Fullscreen |
| Ctrl+O | Open File |
| Ctrl+Shift+O | Open Folder |
| Ctrl+K | Close Folder |
| Ctrl+Shift+R | Rescan Folder |
| Ctrl+B | Toggle Sidebar |
| Ctrl+J | Toggle Bottom Panel |
| Ctrl+H | Toggle Right Panel |
| Ctrl+Shift+S | Toggle Status Bar |

Shortcuts are suppressed when typing in a text field.

---

## State Persistence

Vido remembers everything between sessions:

- Window position, size, and maximized state
- Open folder and last played video + position
- Sidebar, bottom panel, and right panel visibility and sizes
- Active sidebar panel
- Volume and playback speed
- Recent files list
- Last opened playlist
- All OSR2+, Pulse, and Playlist runtime state

Settings are stored in `%APPDATA%/Vido/`.

## License

See [LICENSE](LICENSE) for details.
