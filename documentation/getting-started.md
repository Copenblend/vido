# Getting Started

← [Back to Home](../README.md)

Get up and running with Vido in under two minutes. This guide covers installation, launching the app, and playing your first video.

---

## System Requirements

| Requirement | Details |
|-------------|---------|
| **Operating System** | Windows 10 or Windows 11 (64-bit) |
| **Runtime** | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Disk Space** | ~100 MB |

---

## Installation

### Option 1: MSI Installer (Recommended)

1. Download the latest MSI installer from the [Releases page](https://github.com/AnotherFoxGuy/vido/releases/latest)
2. Double-click the `.msi` file and follow the prompts
3. Vido is added to your Start Menu and file associations are configured automatically

### Option 2: Portable ZIP

1. Download the latest portable ZIP from the [Releases page](https://github.com/AnotherFoxGuy/vido/releases/latest)
2. Extract the ZIP to any folder (e.g., `C:\Tools\Vido\`)
3. Run `Vido.exe` from the extracted folder — no installation needed

> **Tip:** The portable version stores all settings in `%APPDATA%\Vido\`, so your data is separate from the application files.

---

## First Launch

When you open Vido for the first time, you'll see:

- A dark-themed window with a **title bar** at the top (menus, toolbar buttons)
- An **activity bar** on the far left (five icons for switching panels)
- A **video area** in the center (empty until you open a video)
- A **transport bar** at the bottom (play, pause, seek, volume)

---

## Opening Your First Video

You can open a video in several ways:

### Drag and Drop
Drag a video file from Windows Explorer and drop it onto the Vido window.

### File Menu
1. Click **File** in the menu bar
2. Choose **Open File** (or press **Ctrl+O**)
3. Browse to your video and click **Open**

### Open a Folder
1. Click **File** → **Open Folder** (or press **Ctrl+Shift+O**)
2. Choose a folder containing videos
3. A file explorer tree appears in the sidebar — double-click any video to play it

### Command Line
Launch Vido with a file path:
```
Vido.exe "C:\Videos\my-video.mp4"
```

### File Associations (MSI Install Only)
After installing with the MSI, you can double-click any supported video file in Windows Explorer to open it in Vido.

---

## Supported Video Formats

Vido plays these formats out of the box:

`.mp4` · `.mkv` · `.avi` · `.mov` · `.wmv` · `.flv` · `.webm`

---

## Playing a Video

Once a video is open:

1. Press **Space** (or click the play button) to start playback
2. Click anywhere on the **seek bar** to jump to a position
3. Use **Up/Down arrows** to adjust volume
4. Press **F11** to go fullscreen — press **Escape** to exit

---

## What's Next?

Now that you're up and running, explore these guides:

- [Video Playback](video-playback.md) — all playback controls, speed, looping, and fullscreen
- [User Interface](user-interface.md) — learn the layout: sidebar, panels, activity bar
- [File Explorer](file-explorer.md) — browse folders of videos
- [Settings](settings.md) — customize Vido to your preferences

---

← [Back to Home](../README.md)
