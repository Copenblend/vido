# Plugin Testing Guide

> **vi-019** — Plugin Manager UI, Sample Plugin & End-to-End Validation

This document provides step-by-step instructions for testing the Plugin Manager
UI and the Sample Plugin in a running instance of Vido.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Setting Up the Sample Plugin](#2-setting-up-the-sample-plugin)
3. [Configuring the Registry URL](#3-configuring-the-registry-url)
4. [Browsing & Installing from the Plugin Manager](#4-browsing--installing-from-the-plugin-manager)
5. [Verifying All Contributed UI Elements](#5-verifying-all-contributed-ui-elements)
6. [Testing Plugin Settings](#6-testing-plugin-settings)
7. [Testing Enable / Disable Toggle](#7-testing-enable--disable-toggle)
8. [Testing Uninstall](#8-testing-uninstall)
9. [Testing the Detail Panel](#9-testing-the-detail-panel)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Prerequisites

| Requirement | Details |
|-------------|---------|
| .NET 8 SDK | Required to build both Vido and the sample plugin |
| PowerShell 5.1+ | Required to run `package.ps1` |
| Vido source tree | Checked out at `c:\source\vido` (or equivalent) |
| Sample plugin source | Checked out at `c:\source\vido-sample-plugin` |

---

## 2. Setting Up the Sample Plugin

### 2a — Build the Plugin Zip

```powershell
cd c:\source\vido-sample-plugin
.\package.ps1
```

This produces `vido-sample-plugin-1.0.0.zip` in the project root.

### 2b — Create a Local Registry

The sample plugin ships with a `registry.json` in its root directory. For local
testing, we use a `file://` URL pointing to this file.

Determine the absolute path of the registry file:

```powershell
# Example on Windows:
$registryPath = (Resolve-Path "c:\source\vido-sample-plugin\registry.json").Path
$fileUrl = "file:///$($registryPath -replace '\\','/')"
Write-Host "Registry URL: $fileUrl"
```

**Important**: Update the `downloadUrl` in `registry.json` to point to the local
zip file using a `file://` URL:

```json
"downloadUrl": "file:///C:/source/vido-sample-plugin/vido-sample-plugin-1.0.0.zip"
```

Save the file. The Plugin Manager will fetch this registry and use the local zip
for installation.

---

## 3. Configuring the Registry URL

1. Launch Vido.
2. Open **Settings** (click the gear icon in the activity bar, or press `Ctrl+,`).
3. Scroll to the **Plugins** section.
4. In the **Plugin Registry URLs** list, click **Add** and paste the `file://`
   URL from step 2b.
5. Save settings.

Alternatively, edit `%APPDATA%\Vido\settings.json` directly and add the URL to
the `PluginRegistryUrls` array:

```json
{
  "PluginRegistryUrls": [
    "https://plugins.vido.app/registry",
    "file:///C:/source/vido-sample-plugin/registry.json"
  ]
}
```

---

## 4. Browsing & Installing from the Plugin Manager

### 4a — Open the Plugin Manager

1. Click the **Extensions** icon (puzzle piece) in the activity bar on the left.
2. The Plugin Manager sidebar panel should appear.

**Expected**:
- A search bar at the top with a magnifying glass icon.
- A registry source dropdown (defaulting to "All").
- An **INSTALLED** section (may be empty) with a chevron and count badge.
- An **AVAILABLE** section listing the Sample Plugin with a count badge of `1`.

### 4b — Search

1. Type `sample` in the search bar.
2. The AVAILABLE section should show only the "Sample Plugin" entry.
3. Clear the search bar — all plugins should reappear.
4. Type `nonexistent` — both sections should be empty (count badges show `0`).

### 4c — Registry Dropdown

1. Click the registry dropdown.
2. You should see: **All**, **Vido Plugin Registry** (from the local file).
3. Select the specific registry name — only plugins from that registry appear.
4. Select **All** again to restore the full list.

### 4d — Install the Sample Plugin

1. In the AVAILABLE section, click the blue **Install** button on the Sample
   Plugin item.
2. The button should show a busy state briefly.

**Expected after install**:
- The plugin moves from AVAILABLE to INSTALLED.
- The INSTALLED count badge increments.
- The AVAILABLE count badge decrements.
- A detail tab opens for the Sample Plugin in the main panel.
- The plugin status shows "Enabled".

---

## 5. Verifying All Contributed UI Elements

After installing, verify each of the nine extension points:

| # | Extension Point | How to Verify | Expected Result |
|---|----------------|---------------|-----------------|
| 1 | **Sidebar Panel** | Look for a new icon in the activity bar labeled "Sample Panel" | A panel displays with plugin info text |
| 2 | **Bottom Panel Tab** | Check the bottom panel tabs for "Sample Log" | Tab shows timestamped activation message |
| 3 | **Right Panel Tab** | Check the right panel tabs for "Sample Info" | Tab shows read-only plugin properties |
| 4 | **Status Bar Item** | Look at the right side of the status bar | Displays "Hello, Vido!" (or "Sample Plugin v1.0" if greeting disabled) |
| 5 | **Toolbar Button** | Look for the Sample Plugin button in the title bar | Click it → message appears in Output log |
| 6 | **Context Menu** | Right-click any file in the File Explorer | "Hello from Plugin" menu item appears; clicking logs the filename |
| 7 | **File Handler** | Create a file named `test.sample` in the explorer and double-click it | Message "Opened sample file: test.sample" appears in Output log |
| 8 | **File Icon** | Look at the `test.sample` file in the explorer | It should display the custom icon (or a fallback if icon file is absent) |
| 9 | **Keyboard Shortcut** | Press `Ctrl+Shift+H` | "Hello from keyboard shortcut!" appears in Output log |

---

## 6. Testing Plugin Settings

### 6a — Open Settings

1. In the Plugin Manager sidebar, click the **settings cog** icon on the Sample Plugin.
2. The detail tab should open and switch to the **Settings** tab.

Alternatively:
1. Click the plugin item to open the detail panel.
2. Click the **Settings** tab.

### 6b — Verify Settings Display

**Expected sections and controls**:

| Section | Setting | Control Type | Default |
|---------|---------|-------------|---------|
| Display | Enable Greeting | Boolean dropdown (True/False) | `True` |
| Display | Greeting Text | Text input | `Hello, Vido!` |
| Advanced | Refresh Interval (seconds) | Number input | `30` |
| Advanced | Log Level | Enum dropdown (Debug, Info, Warning, Error) | `Info` |

Each setting should show its **title** in bold and its **description** below in
secondary text. Sections should have a divider line and header text.

### 6c — Modify Settings

1. Change **Enable Greeting** from `True` to `False`.
2. Change **Greeting Text** to `Hey there!`.
3. Change **Refresh Interval** to `10`.
4. Change **Log Level** to `Debug`.

**Expected**:
- Each change is auto-saved immediately (no save button needed).
- The Output log shows "Setting changed: 'enableGreeting'" etc.
- The status bar item updates (if greeting is disabled, shows "Sample Plugin v1.0").

### 6d — Verify Persistence

1. Close and reopen Vido.
2. Open the Sample Plugin settings.
3. All four settings should retain the values you set.
4. **Exception**: `refreshInterval` has `forceOverride: true`, so it resets to `30`
   on every plugin activation.

---

## 7. Testing Enable / Disable Toggle

1. In the Plugin Manager sidebar, find the Sample Plugin in the INSTALLED
   section.
2. The status should show "Enabled".
3. Click the **Disable** button (or toggle).

**Expected**:
- Status changes to "Disabled".
- The Output log shows "Sample Plugin deactivated."
- All contributed UI elements (sidebar panel, tabs, status bar item, context
  menu item, toolbar button, keyboard shortcut) are removed / no longer
  functional.

4. Click **Enable**.

**Expected**:
- Status returns to "Enabled".
- All contributed UI elements reappear.
- The Output log shows "Sample Plugin activated successfully."

---

## 8. Testing Uninstall

1. In the Plugin Manager sidebar, click the red **Uninstall** button on the
   Sample Plugin.

**Expected**:
- The plugin moves from INSTALLED to AVAILABLE.
- Count badges update.
- All contributed UI elements are removed.
- The plugin directory at `%APPDATA%\Vido\plugins\com.vido.sample-plugin\` is
  deleted (or has a `.uninstall` marker if DLLs are locked).

2. If a `.uninstall` marker was created, restart Vido.

**Expected after restart**:
- The plugin directory is fully removed on startup.
- The plugin appears only in AVAILABLE.

---

## 9. Testing the Detail Panel

### 9a — Open the Detail Panel

1. Click on any plugin item in the Plugin Manager sidebar.
2. A new tab opens in the main panel area with the plugin's detail view.

### 9b — Header

**Expected**:
- 64×64 icon (or placeholder).
- Plugin name in large bold text.
- Publisher name with a verified badge (blue circle + checkmark) if the plugin
  came from the official registry.
- Description text.
- Action buttons:
  - **Available plugin**: Blue "Install" button.
  - **Installed + enabled**: Red "Uninstall" button, red "Disable" button, settings gear.
  - **Installed + disabled**: Red "Uninstall" button, blue "Enable" button, settings gear.

### 9c — Tabbed Content

| Tab | Expected Content |
|-----|-----------------|
| **Details** | Plugin's `README.md` content (rendered as plain text), or placeholder if absent |
| **Changelog** | Plugin's `CHANGELOG.md` content, or placeholder if absent |
| **Settings** | All four settings with proper type-specific controls (see Section 6) |

### 9d — Right Metadata Pane

**Expected** (at ~25% width on the right side):
- **Version**: e.g., `1.0.0`
- **Tags**: e.g., `sample, demo, reference, all-extensions`
- **Last Updated**: e.g., `2025-01-15`
- **License**: e.g., `MIT`

### 9e — Action Button Reactivity

1. Click "Install" on an available plugin → buttons should change to "Uninstall" + "Disable" + settings gear.
2. Click "Disable" → button changes to "Enable".
3. Click "Enable" → button changes back to "Disable".
4. Click "Uninstall" → buttons change to "Install".

---

## 10. Troubleshooting

### DLL Locking on Uninstall

**Symptom**: Uninstall fails; a `.uninstall` marker file is created.

**Cause**: .NET holds assembly locks on loaded plugin DLLs.

**Solution**: Restart Vido. The `CleanupPendingUninstalls()` method runs on
startup and removes marked directories.

### Registry Fetch Fails

**Symptom**: AVAILABLE section is empty; no error visible.

**Cause**: Network error or malformed URL.

**Solution**:
1. Check the Output log for errors like `Failed to fetch registry from '...'`.
2. Verify the URL is correct and accessible.
3. For `file://` URLs, ensure the path uses forward slashes (`/`) and the file
   exists.
4. Verify `registry.json` is valid JSON (run `Test-Json (Get-Content registry.json -Raw)`).

### Plugin Doesn't Appear After Install

**Symptom**: Plugin zip extracted but plugin doesn't load.

**Cause**: Missing or malformed `plugin.json`, or wrong `entryPoint` / `pluginClass`.

**Solution**:
1. Navigate to `%APPDATA%\Vido\plugins\com.vido.sample-plugin\`.
2. Verify `plugin.json` exists and is valid.
3. Verify the DLL named in `entryPoint` exists.
4. Verify `pluginClass` matches the fully-qualified class name.
5. Check the Output log for activation errors.

### Settings Not Persisting

**Symptom**: Settings revert to defaults on restart.

**Cause**: The setting may have `forceOverride: true` in the manifest.

**Solution**: Check the plugin's `plugin.json` → `contributes.settings` for the
`forceOverride` flag on the affected setting.

### Icon Not Displaying

**Symptom**: Plugin icon shows as a placeholder rectangle.

**Cause**: Icon file does not exist at the declared path.

**Solution**: Ensure the icon files declared in the manifest (`icons/sample-sidebar.png`,
`icons/sample-toolbar.png`, `icons/sample-file.png`) exist in the plugin directory.
For the sample plugin, create placeholder PNG files in the `icons/` folder.
