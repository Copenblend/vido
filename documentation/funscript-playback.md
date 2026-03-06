# Funscript Playback

← [Back to Home](../README.md)

Funscripts are files that control haptic device movement in sync with video content. Vido automatically loads matching funscripts and provides a real-time visualizer to see what's happening across all axes.

---

## What Is a Funscript?

A `.funscript` file contains timed position data that tells the device where to move at each moment during a video. Each axis can have its own funscript file.

---

## Auto-Loading

When you open a video, Vido automatically searches the same folder for matching funscript files:

| Axis | Auto-Detection Pattern |
|------|----------------------|
| **L0 (Stroke)** | `video.funscript` |
| **R0 (Twist)** | `video.twist.funscript` |
| **R1 (Roll)** | `video.roll.funscript` |
| **R2 (Pitch)** | `video.pitch.funscript` |

If matching files are found, they're loaded automatically — no action needed.

> **Tip:** If [Pulse](pulse-beat-detection.md) is enabled, auto-loading for the Stroke (L0) axis is suppressed because Pulse takes over L0 control.

---

## Manual Loading

To manually assign a funscript to any axis:

1. Open the **OSR2+** sidebar panel
2. Find the axis card you want (L0, R0, R1, or R2)
3. Click the **Load Funscript** button
4. Browse to your `.funscript` file and click **Open**

Manually loaded scripts take priority over auto-detected ones and persist until you clear them.

---

## Funscript Visualizer

When funscripts are loaded, a **Funscript Visualizer** tab appears in the bottom panel showing your scripts in real time.

### Graph Mode

- Multi-axis line graph showing position over time
- Color-coded lines for each axis
- A playback cursor tracks the current position
- Axis legend identifies each line

### Heatmap Mode

- Speed-based color heatmap for the Stroke (L0) axis
- Colors range from cool (slow) to hot (fast)
- Useful for seeing intensity patterns at a glance

### Window Duration

Control how much time is visible in the visualizer. Options:

| Duration | Best For |
|----------|----------|
| 30 seconds | Detailed view of nearby movements |
| 60 seconds | Default — good balance |
| 2 minutes | Broader context |
| 5 minutes | Full overview |

Change the window duration in **Settings** → **OSR2+** → **Visualizer Window Duration**.

---

## Timing Adjustment

If movements feel out of sync with the video, adjust the **Global Funscript Offset** in **Settings** → **OSR2+**. Negative values shift movements earlier, positive values shift them later (range: −500 to +500 ms).

---

## Related

- [OSR2+ Device Control](osr2-device-control.md) — connecting your device and axis configuration
- [Pulse Beat Detection](pulse-beat-detection.md) — auto-generating movement from audio
- [Fill Profiles](fill-profiles.md) — saving axis configurations

---

← [Back to Home](../README.md)
