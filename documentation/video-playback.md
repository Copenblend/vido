# Video Playback

← [Back to Home](../README.md)

Everything you need to know about playing videos in Vido — transport controls, speed, volume, looping, shuffle, fullscreen, and resuming playback.

---

## Transport Controls

The transport bar sits at the bottom of the video area and contains all playback controls:

| Control | Action | Shortcut |
|---------|--------|----------|
| **Play / Pause** | Toggle playback | **Space** |
| **Stop** | Stop playback completely | **S** |
| **Previous** | Go to the previous video | **Page Up** |
| **Next** | Go to the next video | **Page Down** |
| **Seek Bar** | Click or drag to jump to any position | — |
| **Volume** | Adjust volume level (0–100%) | **Up / Down** |
| **Mute** | Toggle audio on/off | **M** |
| **Playback Speed** | Change playback speed | — |
| **Loop** | Toggle looping for the current video | — |
| **Shuffle** | Randomize play order in current folder | — |

---

## Opening Videos

There are many ways to open a video:

| Method | How To |
|--------|--------|
| **Drag and drop** | Drop a video file onto the Vido window |
| **Open File** | **File** → **Open File**, or press **Ctrl+O** |
| **Open Folder** | **File** → **Open Folder**, or press **Ctrl+Shift+O** — then browse the file explorer |
| **Double-click** | Click a video in the [File Explorer](file-explorer.md) sidebar |
| **Recent Files** | **File** → **Recent Files** — pick from your history |
| **File association** | Double-click a video in Windows Explorer (MSI install) |
| **Command line** | Run `Vido.exe "path\to\video.mp4"` |

**Supported formats:** `.mp4` · `.mkv` · `.avi` · `.mov` · `.wmv` · `.flv` · `.webm`

---

## Playback Speed

Click the speed button in the transport bar (shows "1x" by default) to cycle through speeds:

| Speed | Description |
|-------|-------------|
| 0.25x | Quarter speed (slow motion) |
| 0.5x | Half speed |
| 1.0x | Normal speed (default) |
| 1.5x | 50% faster |
| 2.0x | Double speed |

You can also set the default speed in **Settings** → **Playback** → **Default Playback Speed**.

The speed menu is also available under **Playback** → **Playback Speed** in the menu bar.

---

## Volume

- Use the **Up** and **Down** arrow keys to adjust volume by 5%
- Press **M** to mute/unmute
- The volume level is remembered between sessions

You can set the default starting volume in **Settings** → **Playback** → **Default Volume**.

---

## Loop Playback

Click the **loop icon** in the transport bar to toggle looping. When enabled, the video restarts automatically when it reaches the end.

You can also set this as the default in **Settings** → **Playback** → **Loop Playback**.

---

## Shuffle

Click the **shuffle icon** in the transport bar to randomize playback order. When you reach the end of a video, the next video in the shuffled order plays automatically. Every video plays once before the order resets.

---

## Fullscreen

| Action | How To |
|--------|--------|
| **Enter fullscreen** | Press **F11**, press **F**, or use **View** → **Fullscreen** |
| **Exit fullscreen** | Press **Escape**, press **F11** again, or press **F** again |

In fullscreen mode:
- The title bar, sidebar, status bar, and panels are hidden
- The video fills the entire screen
- Transport controls appear at the bottom when you move the mouse, and fade away after a few seconds of inactivity
- The video filename appears in the top-left corner (can be disabled in Settings)

> **Tip:** You can adjust how long the controls stay visible before fading in **Settings** → **Playback** → **Fullscreen Auto-Hide Delay**.

---

## Resume Playback

When you reopen a video you've watched before, a bar appears at the top asking if you'd like to resume from where you left off:

- Click **Yes** to jump to your previous position
- Click **No** to start from the beginning

> **Tip:** You can disable this prompt in **Settings** → **Playback** → **Resume Playback Prompt**.

---

## Navigating Between Videos

Use **Page Down** to skip to the next video and **Page Up** for the previous one. The behavior depends on what's active:

- **With a playlist open** — skips to the next/previous playlist item
- **With a folder open** — skips to the next/previous video in the folder

---

## Video Details

Press **Ctrl+H** to open the right panel, which shows detailed information about the current video:

- File name and full path
- File size
- Duration
- Resolution (width × height)
- Video and audio codecs
- Frame rate
- Bitrate
- Container format
- Audio channels and sample rate

---

## Related

- [User Interface](user-interface.md) — learn the full window layout
- [Playlists](playlists.md) — organize your videos into playlists
- [Keyboard Shortcuts](keyboard-shortcuts.md) — all shortcuts at a glance
- [Settings](settings.md) — customize playback defaults

---

← [Back to Home](../README.md)
