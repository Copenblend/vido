# Fill Profiles

← [Back to Home](../README.md)

Fill profiles let you save and switch between complete axis configurations with a single click. Instead of manually adjusting each axis every time, save your favorite setups as named profiles and switch between them instantly.

---

## What's in a Profile?

A profile stores the following settings for **all four axes** (Stroke, Twist, Roll, Pitch):

- Enabled / Disabled state
- Min and Max range
- Fill mode
- Sync with Stroke setting
- Fill speed (Hz)

---

## Using Profiles

The **Profile** dropdown appears at the top of the axis control panel in the OSR2+ sidebar.

1. Click the **Profile** dropdown
2. Select a profile — all axis settings are applied immediately

---

## Built-In Profiles

Vido includes three ready-to-use profiles:

| Profile | Description |
|---------|-------------|
| **Default** | All axes set to no fill mode (movement only from funscripts) |
| **Gentle Wave** | Twist, Roll, and Pitch use a gentle Sine wave (25–75 range at 0.5 Hz) |
| **Full Random** | Twist, Roll, and Pitch use smooth Random movement (full 0–100 range) |

> **Tip:** The **Default** profile is automatically selected when you first connect your device.

Built-in profiles cannot be deleted or renamed.

---

## Creating a Custom Profile

1. Adjust the axis settings to your liking (enable/disable axes, set ranges, choose fill modes, etc.)
2. Click the **+** button next to the profile dropdown
3. Enter a name for your profile and click **OK**

Your profile is saved and appears in the dropdown. Custom profiles are stored in `%APPDATA%\Vido\fill-profiles.json`.

> **Tip:** Profile names are limited to 50 characters and must be unique.

---

## Renaming a Profile

1. Select the profile you want to rename
2. Click the **rename button** (pencil icon) next to the dropdown
3. Enter the new name and click **OK**

Only custom profiles can be renamed — built-in profiles are fixed.

---

## Deleting a Profile

1. Select the profile you want to delete
2. Click the **delete button** (trash icon) next to the dropdown
3. The profile is removed and the selection is cleared

Only custom profiles can be deleted — built-in profiles are permanent.

---

## Modification Detection

When you modify axis settings after selecting a profile, an amber **(modified)** indicator appears next to the profile name. This tells you the current settings no longer match the saved profile.

To save your changes:
- Click the **+** button and save as a new profile, or
- Click the **+** button and enter the same name to update the existing profile

---

## Related

- [OSR2+ Device Control](osr2-device-control.md) — axis settings and fill modes
- [Funscript Playback](funscript-playback.md) — loading funscripts
- [Settings](settings.md) — all OSR2+ settings

---

← [Back to Home](../README.md)
