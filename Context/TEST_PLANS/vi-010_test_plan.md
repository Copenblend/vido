# vi-010 Test Plan — Tab System with Docking Foundation

## Unit Tests (Automated)

**Test Class:** `MainWindowViewModelTests` — **32 tests**

### Constructor / Initial State
- Constructor creates Player tab as only tab
- Constructor sets Player tab as active by default
- Player tab is pinned and not closable
- Bottom and right panels are hidden by default

### OpenTab
- Opens new tab and activates it
- Opening existing ID activates without duplicating
- Sets icon geometry on new tab
- Sets IsClosable on new tab

### CloseTab
- Removes closable tab
- Cannot close Player tab (non-closable)
- Closing active tab activates neighbor
- Closing last closable tab activates Player
- Nonexistent ID does nothing
- Closing inactive tab does not change active tab

### ActivateTab
- Switches to existing tab
- Nonexistent ID does nothing

### ReorderTab
- Moves tab to new position
- Cannot move pinned tab
- Cannot move tab before pinned tab
- Invalid indices do nothing
- Same index does nothing

### OpenSettings
- Creates Settings tab and activates it
- Calling twice does not duplicate
- Settings tab is closable

### Panel Toggles
- ToggleBottomPanel toggles visibility on/off
- ToggleRightPanel toggles visibility on/off

### FindTab
- Returns correct tab for valid ID
- Returns null for nonexistent ID

### TabItemModel
- Default property values are correct

### PropertyChanged Notifications
- ActiveTab raises PropertyChanged
- IsBottomPanelVisible raises PropertyChanged
- IsRightPanelVisible raises PropertyChanged

---

## Manual Tests

**Prerequisites:** Build and run Vido. Open a folder with at least one video file.

### Test 1: Tab Strip Visible
1. Launch the application.
2. Observe the tab strip above the editor area.
**Expected:** A single "Player" tab appears with a play triangle icon. The tab has the editor background and an accent bottom border.

### Test 2: Player Tab Cannot Be Closed
1. Hover over the "Player" tab.
**Expected:** No close (×) button appears on the Player tab.

### Test 3: Settings Opens As Tab
1. Click the gear icon (⚙) in the activity bar at the bottom.
**Expected:** A "Settings" tab opens to the right of the Player tab. The Settings tab becomes active. The content area shows "Settings will be available in a future update."

### Test 4: Settings Tab Close Button
1. Open Settings (gear icon).
2. Hover over the "Settings" tab.
**Expected:** A close (×) button appears on hover. Click it — the Settings tab closes and Player becomes active again.

### Test 5: Tab Switching
1. Open Settings.
2. Click the "Player" tab.
**Expected:** The Player tab becomes active, the video player is visible. Click "Settings" — the settings page reappears.

### Test 6: Tab Drag Reorder
1. Open Settings (so you have Player + Settings).
2. Open another tab if available, or verify with Settings only.
3. Click and drag the Settings tab leftward.
**Expected:** The Settings tab cannot be moved before the pinned Player tab.

### Test 7: Video Still Plays After Tab Switching
1. Load a video file (double-click in explorer).
2. Click "Settings" tab — video is hidden.
3. Click "Player" tab — video reappears.
**Expected:** Video continues playing in the background. Position advances while on the Settings tab.

### Test 8: Toggle Bottom Panel via Menu
1. Go to View > Toggle Bottom Panel (or press Ctrl+J).
**Expected:** A bottom panel appears with an "OUTPUT" tab header and a placeholder message. The panel is separated by a draggable splitter.

### Test 9: Bottom Panel Resizable
1. Open the bottom panel.
2. Drag the splitter between the editor and bottom panel.
**Expected:** The panel resizes smoothly. The editor area adjusts.

### Test 10: Bottom Panel Remembers Height
1. Open the bottom panel and resize it to a specific height.
2. Toggle it off (View > Toggle Bottom Panel).
3. Toggle it back on.
**Expected:** The panel reopens at the previously set height.

### Test 11: Toggle Right Panel via Menu
1. Go to View > Toggle Right Panel.
**Expected:** A right panel appears with a "VIDEO INFO" tab header and a placeholder message.

### Test 12: Right Panel Resizable
1. Open the right panel.
2. Drag the splitter between the editor and right panel.
**Expected:** The panel resizes smoothly. The editor area adjusts.

### Test 13: Right Panel Remembers Width
1. Open the right panel and resize it.
2. Toggle off then back on.
**Expected:** Reopens at the same width.

### Test 14: Both Panels Open Simultaneously
1. Open both bottom and right panels.
**Expected:** All three areas (editor, bottom panel, right panel) are visible and resizable.

### Test 15: Tab Overflow Scrolling
1. Open many tabs (if multiple tab sources exist, or test by reducing window width).
**Expected:** When tabs exceed the available width, scroll arrows (◀ ▶) appear on the right side of the tab strip.

---

## Regression Tests

### R1: Existing Functionality
- File explorer opens folders and displays files correctly
- Double-click to play video works
- Video playback (play/pause/stop/seek/volume) works
- Context menus work
- Activity bar Explorer/Extensions switching works
- Window resize, maximize, restore, close work
- Title bar menus open and display correctly

### R2: Build & Test Suite
- Build: 0 errors, 1 pre-existing warning (xUnit1031)
- Tests: 259 passed (227 prior + 32 new), 0 failed, 0 skipped
