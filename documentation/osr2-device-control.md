# OSR2+ Device Control

← [Back to Home](../README.md)

Vido has built-in support for the OSR2+ haptic device. Connect over USB (Serial) or network (UDP), control four axes, assign funscripts, and use fill modes to generate continuous movement.

---

## Connecting Your Device

1. Click the **device icon** in the activity bar (second icon from top) to open the OSR2+ sidebar
2. Choose your **connection mode**:

| Mode | When to Use |
|------|-------------|
| **UDP** | Network connection (default port: 7777) |
| **Serial** | Direct USB/COM port connection |

3. Configure the connection:
   - **UDP** — set the port number (default: 7777)
   - **Serial** — select your COM port from the dropdown and choose a baud rate
4. Click **Connect**

The status bar shows your connection state (e.g., `UDP:7777:Connected`).

> **Tip:** For Serial connections, click the refresh button next to the COM port dropdown to rescan for available ports.

When you connect, all axes gradually move to their midpoint for a safe start.

### Quick Connect from Title Bar

You can also click the **device icon** in the title bar toolbar to quickly connect or disconnect without opening the sidebar.

---

## Axes

Vido controls four axes on the OSR2+, each shown as a collapsible card in the sidebar:

| Axis | Name | What It Controls |
|------|------|------------------|
| **L0** | Stroke | Primary up/down movement |
| **R0** | Twist | Rotational twist |
| **R1** | Roll | Side-to-side tilt |
| **R2** | Pitch | Front-to-back tilt |

### Per-Axis Controls

Each axis card offers these controls:

| Control | Description |
|---------|-------------|
| **Enable/Disable** | Toggle whether this axis is active |
| **Min / Max** | Set the range of motion (0–100) |
| **Position Offset** | Fine-tune the position in real time |
| **Load Funscript** | Manually assign a `.funscript` file |
| **Fill Mode** | Choose a movement pattern for when no script is loaded |
| **Sync with Stroke** | Lock this axis's fill timing to the Stroke (L0) axis |
| **Fill Speed** | How fast the fill pattern runs (0.1–3.0 Hz) |

---

## Fill Modes

When an axis has no funscript loaded, fill modes generate continuous movement within the Min/Max range:

| Mode | Description |
|------|-------------|
| **None** | No movement — holds still |
| **Random** | Smooth random movement |
| **Triangle** | Linear up-and-down wave |
| **Sine** | Smooth sinusoidal wave |
| **Saw** | Linear ramp up, instant drop |
| **Reverse Saw** | Instant snap up, linear ramp down |
| **Square** | Instant alternation between min and max |
| **Pulse** | Holds at extremes with quick transitions |
| **Ease In/Out** | Sine-like with sharper acceleration |

### Fill Speed

Set the speed of the fill pattern from **0.1 Hz** (very slow) to **3.0 Hz** (fast). If **Sync with Stroke** is enabled, the fill pattern follows the Stroke axis timing instead.

---

## Fill Profiles

Profiles let you save and restore complete axis configurations (enable state, min/max, fill mode, sync, and speed for all axes) with a single click. See the full [Fill Profiles](fill-profiles.md) guide.

---

## Test Mode

Test your device without playing a video:

1. Make sure the device is connected and no video is playing
2. Click the **Test** button in the axis control panel
3. The device cycles through enabled axes with test movements
4. Click **Stop** to end the test — all axes return to midpoint

---

## Beat Bar

The beat bar is a visual overlay on the video that marks rhythmic points in the funscript:

| Mode | What It Shows |
|------|---------------|
| **Off** | No beat bar |
| **On Peak** | Marks at upstroke peaks |
| **On Valley** | Marks at downstroke valleys |
| **On Peak & Valley** | Marks at all direction changes |
| **Mid Stroke** | Marks at the midpoint of descending strokes |

When [Pulse](pulse-beat-detection.md) is active, it can add its own beat bar mode showing red heart markers synchronized to detected beats.

Select the beat bar mode from the dropdown in the OSR2+ sidebar or axis control panel.

---

## Timing Offset

If the device movements feel slightly early or late compared to the video, adjust the **Global Funscript Offset** in **Settings** → **OSR2+**:

- **Negative values** (e.g., −50 ms) make movements happen earlier
- **Positive values** (e.g., +50 ms) make movements happen later
- Range: −500 ms to +500 ms

---

## Output Rate

The **TCode Output Rate** controls how many position updates per second are sent to the device. Higher rates give smoother motion but use more bandwidth:

| Rate | Best For |
|------|----------|
| 30–60 Hz | Basic movement, lower bandwidth |
| 100 Hz | Recommended default — good balance |
| 150–200 Hz | Maximum smoothness for fast movements |

Adjust in **Settings** → **OSR2+** → **TCode Output Rate**.

---

## Connection Notifications

Vido shows toast notifications for connection events:

- **Connected** — shows the connection mode and port
- **Disconnected** — confirms the device is disconnected
- **Connection failed** — shows an error message with details

---

## Related

- [Funscript Playback](funscript-playback.md) — loading and managing funscripts
- [Fill Profiles](fill-profiles.md) — saving and switching axis configurations
- [Pulse Beat Detection](pulse-beat-detection.md) — automatic beat-driven haptics
- [Settings](settings.md) — all OSR2+ settings

---

← [Back to Home](../README.md)
