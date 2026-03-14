# Settings

← [Back to Home](../README.md)

Vido's settings let you customize every aspect of the application. Open Settings by clicking the **gear icon** at the bottom of the activity bar.

All settings are organized by category. Use the **search bar** at the top to quickly find any setting by name or description.

---

## General

| Setting | Default | Description |
|---------|---------|-------------|
| **Toast Notification Duration** | 3 seconds | How long notification popups stay visible before auto-dismissing (1–10 seconds) |

---

## Playback

| Setting | Default | Description |
|---------|---------|-------------|
| **Default Volume** | 50 | Starting volume level for new videos (0–100) |
| **Default Playback Speed** | 1.0x | Starting playback speed. Options: 0.25x, 0.5x, 1.0x, 1.5x, 2.0x |
| **Loop Playback** | Off | When enabled, videos automatically restart when they reach the end |
| **Fullscreen Auto-Hide Delay** | 3 seconds | How long fullscreen controls stay visible after you stop moving the mouse (1–30 seconds) |
| **Show Video Name in Fullscreen** | On | Display the video filename in the top-left corner during fullscreen playback |
| **Resume Playback Prompt** | On | Show a bar asking whether to resume from your previous position when reopening a video |

---

## File Explorer

| Setting | Default | Description |
|---------|---------|-------------|
| **Show Hidden Files** | Off | When enabled, files and folders marked as hidden in Windows are visible in the file explorer (shown dimmed) |

---

## Screenshot

| Setting | Default | Description |
|---------|---------|-------------|
| **Enable Screenshot Capture** | Off | When enabled, a camera button appears in the title bar for capturing video frames |
| **Screenshot Save Directory** | Pictures\Screenshots | The folder where captured screenshots are saved. Only visible when screenshot capture is enabled. |

---

## OSR2+

| Setting | Default | Description |
|---------|---------|-------------|
| **Connection Mode** | UDP | Default connection method for the OSR2+ device (UDP or Serial) |
| **Default UDP Port** | 7777 | Port number for UDP connections (1–65535) |
| **Default Baud Rate** | 115200 | Baud rate for Serial connections. Options: 9600, 19200, 38400, 57600, 115200, 250000 |
| **TCode Output Rate** | 100 Hz | How many position updates per second are sent to the device (30–200 Hz). Higher values give smoother motion. |
| **Global Funscript Offset** | 0 ms | Shift all funscript timing earlier (negative) or later (positive). Range: −500 to +500 ms. |
| **Visualizer Window Duration** | 60 seconds | How much time is visible in the funscript visualizer. Options: 30s, 60s, 2 min, 5 min |

---

## Playlists

| Setting | Default | Description |
|---------|---------|-------------|
| **Auto-Save Playlists** | Off | When enabled, playlists are automatically saved whenever you add, remove, or reorder items |

---

## Where Settings Are Stored

All settings are saved to:

```
%APPDATA%\Vido\
```

This includes your settings file, fill profiles, and other application data. Settings are saved automatically when you change them — there's no "Save" button needed.

---

## Related

- [User Interface](user-interface.md) — window layout and panels
- [OSR2+ Device Control](osr2-device-control.md) — device connection details
- [Playlists](playlists.md) — playlist features

---

← [Back to Home](../README.md)
