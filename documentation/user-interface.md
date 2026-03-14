# User Interface

← [Back to Home](../README.md)

Vido's interface is inspired by a modern code editor layout — clean, dark, and fully customizable. This guide walks through every part of the window.

---

## Window Layout

The main window is divided into these areas:

```
┌──────────────────────────────────────────────┐
│  Title Bar (menus, toolbar buttons, window)  │
├──┬───────────────────────────────────┬───────┤
│  │                                   │       │
│A │         Video Area                │ Right │
│c │                                   │ Panel │
│t │                                   │       │
│. │                                   │       │
│  │                                   │       │
│B ├───────────────────────────────────┤       │
│a │      Transport Controls           │       │
│r ├───────────────────────────────────┼───────┤
│  │      Bottom Panel (tabs)          │       │
├──┴───────────────────────────────────┴───────┤
│  Status Bar                                  │
└──────────────────────────────────────────────┘
```

---

## Title Bar

The title bar spans the top of the window and contains:

| Area | Contents |
|------|----------|
| **Left** | Application icon, menu bar (**File**, **View**, **Playback**, **Help**) |
| **Center** | "Vido" + current video filename (draggable to move the window) |
| **Right** | Toolbar buttons, window controls (minimize, maximize, close) |

### Title Bar Toolbar Buttons

These quick-access buttons appear in the title bar:

| Button | Icon | Description |
|--------|------|-------------|
| **OSR2+ Connect** | Device icon | Connect or disconnect your OSR2+ device |
| **Screenshot** | Camera icon | Capture the current video frame (only visible when enabled in Settings) |

---

## Activity Bar

The activity bar is the narrow column of icons on the far left edge. It controls which panel appears in the sidebar.

| Position | Icon | Panel | Description |
|----------|------|-------|-------------|
| 1 | 📁 Folder | [File Explorer](file-explorer.md) | Browse video files in a folder |
| 2 | 🔌 Device | [OSR2+ Control](osr2-device-control.md) | Connect and control your haptic device |
| 3 | 📋 List | [Playlists](playlists.md) | Manage playlists |
| 4 | ⚙️ Gear | [Settings](settings.md) | Application settings (pinned to bottom) |

- **Click an icon** to open that panel in the sidebar
- **Click the active icon again** to collapse the sidebar
- The active panel is highlighted

---

## Sidebar

The sidebar fills the space between the activity bar and the video area. Its contents change based on which activity bar icon is selected.

| Shortcut | Action |
|----------|--------|
| **Ctrl+B** | Toggle sidebar visibility |

The sidebar width is resizable — drag the edge to adjust. Your preferred width is remembered between sessions.

---

## Bottom Panel

A collapsible panel below the video area with tabbed content:

| Tab | When It Appears | Contents |
|-----|-----------------|----------|
| **Log Output** | Always present | Application log messages |
| **Funscript Visualizer** | When OSR2+ has scripts loaded | Real-time funscript graph or heatmap |

| Shortcut | Action |
|----------|--------|
| **Ctrl+J** | Toggle bottom panel visibility |

- Individual tabs can be closed (except Log Output)
- The panel can be collapsed to show only the tab bar
- Height is resizable and remembered between sessions

---

## Right Panel

The right panel shows detailed information about the currently playing video — file name, path, size, duration, resolution, codecs, frame rate, bitrate, container format, and audio details.

| Shortcut | Action |
|----------|--------|
| **Ctrl+H** | Toggle right panel visibility |

Width is resizable and remembered between sessions.

---

## Status Bar

A thin bar at the very bottom of the window showing contextual information:

| Position | Content |
|----------|---------|
| **Left** | Current video filename, playlist status |
| **Right** | Video duration, resolution, codec, OSR2+ connection |

| Shortcut | Action |
|----------|--------|
| **Ctrl+Shift+S** | Toggle status bar visibility |

---

## Menus

### File Menu

| Item | Shortcut | Description |
|------|----------|-------------|
| Open File | Ctrl+O | Browse for a video file |
| Open Folder | Ctrl+Shift+O | Open a folder in the file explorer |
| Close Folder | Ctrl+K | Close the current folder |
| Rescan Folder | Ctrl+Shift+R | Refresh the file explorer tree |
| Add File | — | Add a video file to the current context |
| Add Folder | — | Add a folder to the current context |
| Recent Files | — | Submenu of recently opened videos |
| Exit | Alt+F4 | Close Vido |

### View Menu

| Item | Shortcut | Description |
|------|----------|-------------|
| Show Sidebar | Ctrl+B | Toggle sidebar |
| Fullscreen | F11 | Toggle fullscreen mode |
| Right Panel → Show Right Panel | Ctrl+H | Toggle right panel |
| Bottom Panel → Show Bottom Panel | Ctrl+J | Toggle bottom panel |
| Status Bar → Show Status Bar | Ctrl+Shift+S | Toggle status bar |
| Show Hidden Files | — | Toggle hidden files in file explorer |

### Playback Menu

| Item | Shortcut | Description |
|------|----------|-------------|
| Play/Pause | Space | Toggle playback |
| Stop | S | Stop playback |
| Skip Forward | Page Down | Next video |
| Skip Backward | Page Up | Previous video |
| Loop | — | Toggle loop mode |
| Playback Speed | — | Submenu with speed options |

### Help Menu

| Item | Description |
|------|-------------|
| About | Shows version, runtime, and FFmpeg information |

---

## Fullscreen Mode

Press **F11** or **F** to enter fullscreen. The entire window is hidden except for:

- The **video** fills the screen
- **Transport controls** appear at the bottom when you move the mouse
- **Video filename** appears top-left (if enabled in Settings)

Controls auto-hide after a few seconds of inactivity. Press **Escape** or **F11** to exit.

---

## Toast Notifications

Vido shows brief notification popups for important events:

- Device connected/disconnected
- Funscript generated
- Errors and warnings

Toast notifications auto-dismiss after a configurable duration (see **Settings** → **General** → **Toast Notification Duration**).

---

## Single Instance

Only one copy of Vido can run at a time. If you try to open a second instance (e.g., by double-clicking another video), the file is sent to the already-running window, which comes to the foreground and plays it.

---

## Related

- [Keyboard Shortcuts](keyboard-shortcuts.md) — complete shortcut reference
- [Settings](settings.md) — customize the interface
- [Video Playback](video-playback.md) — all playback controls

---

← [Back to Home](../README.md)
