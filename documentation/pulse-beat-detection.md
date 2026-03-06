# Pulse Beat Detection

← [Back to Home](../README.md)

Pulse analyzes the audio track of your video to detect beats and automatically drive your haptic device in sync with the music. No funscript required — just enable Pulse, play a video, and the device moves to the beat.

---

## Enabling Pulse

You can toggle Pulse in two ways:

- **Sidebar** — Click the heart icon in the activity bar, then toggle the Pulse switch
- **Title bar** — Click the heart button in the title bar toolbar

When Pulse is on:
- The heart icon in the title bar fills with color
- A toast notification confirms "Pulse enabled"
- The status bar shows the Pulse state

> **Tip:** Pulse takes over the **Stroke (L0)** axis. Other axes (Twist, Roll, Pitch) continue using their [fill modes](osr2-device-control.md) or funscripts as normal.

---

## How It Works

1. **Enable Pulse** using either toggle
2. **Load a video** — Pulse automatically analyzes the audio track
3. **Wait for analysis** — a progress bar shows the analysis status
4. **Play the video** — your device moves in sync with detected beats

Pulse automatically re-analyzes when you load a different video.

---

## Pulse States

The Pulse indicator shows the current status with color:

| State | Color | What's Happening |
|-------|-------|------------------|
| **Inactive** | Grey | Pulse is turned off |
| **Analyzing** | Yellow | Audio is being processed (progress bar visible) |
| **Ready** | Yellow | Analysis complete — press play to activate |
| **Active** | Green | Device is moving to detected beats |
| **Error** | Red | Analysis failed |

The status bar displays the state and BPM (e.g., `♥ Pulse: Active 128 BPM`).

---

## Beat Rate

Control how aggressively the device responds to beats. Two separate beat rate selectors are available in the Pulse sidebar:

### Live Playback Beat Rate

Controls which beats drive the device during playback:

| Option | Effect |
|--------|--------|
| **Every Beat** | Device responds to every detected beat (default) |
| **Every 2nd Beat** | Responds to every other beat |
| **Every 3rd Beat** | Responds to every third beat |
| **Every 4th Beat** | Responds to every fourth beat |

### Funscript Generation Beat Rate

A separate selector that controls which beats are included when generating funscript files (see below). This is independent of the live playback rate, so you can play with "Every Beat" but generate funscripts with "Every 2nd Beat".

---

## Generating Funscripts

Pulse can convert its beat data into a standard `.funscript` file that you can use without Pulse enabled:

1. Make sure Pulse has analyzed the current video (state should be **Ready** or **Active**)
2. Set the **funscript generation beat rate** to your preferred density
3. Click **Generate Funscript** in the Pulse sidebar

The generated funscript:
- Is saved next to the video file (e.g., `video.funscript`)
- Uses amplitude data from the audio waveform for natural-feeling strokes
- If a funscript already exists, you'll be asked to confirm before overwriting
- A toast notification confirms success

> **Tip:** After generating, disable Pulse and the auto-loaded funscript will drive the device with the same beat pattern — useful for sharing or for devices that don't support real-time control.

---

## Waveform Panel

When Pulse analyzes a video, a **Pulse Waveform** tab appears in the bottom panel:

- **Audio waveform** — the RMS envelope of the audio track
- **Beat markers** — vertical lines at each detected beat
- **BPM readout** — the detected tempo
- **Playback cursor** — current position in the video
- **Amplitude display** — live audio level

### Waveform Window Duration

Control how much time is visible in the waveform display. Set this in **Settings** → **Pulse** → **Waveform Window Duration**:

| Duration | Best For |
|----------|----------|
| 15 seconds | Close-up beat viewing |
| 30 seconds | Default — good detail |
| 60 seconds | Wider overview |
| 2 minutes | Broad context |

---

## Pulse Settings

Available in **Settings** → **Pulse**:

| Setting | Default | Description |
|---------|---------|-------------|
| **Beat Detection Sensitivity** | 1.5 | How sensitive beat detection is (0.5–5.0). Higher values detect more beats, including quieter ones. |
| **Enable BPM Phase Lock** | On | Stabilizes beat timing to a consistent BPM for steadier motion. |
| **Waveform Window Duration** | 30s | How much time is visible in the waveform display. |

---

## Related

- [OSR2+ Device Control](osr2-device-control.md) — device connection and axis settings
- [Funscript Playback](funscript-playback.md) — using funscript files
- [Fill Profiles](fill-profiles.md) — saving axis configurations
- [Settings](settings.md) — all Pulse settings

---

← [Back to Home](../README.md)
