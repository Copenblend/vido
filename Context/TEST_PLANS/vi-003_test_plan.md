# vi-003: Menu Bar — Test Plan

## Manual Tests

### MT-1: Menu Bar is Visible in Title Bar
**Steps:**
1. Launch the app
2. Observe the title bar area
**Expected Result:** Menu items (File, Edit, View, Playback, Help) appear in the title bar between the app icon and the title text.

### MT-2: File Menu Opens with Correct Items
**Steps:**
1. Click "File" in the menu bar
**Expected Result:** Dropdown opens with: Open File (Ctrl+O), Open Folder (Ctrl+Shift+O), Close Folder, separator, Recent Files ▸, separator, Exit (Alt+F4). All items except Exit are disabled (grayed out).

### MT-3: Edit Menu Opens
**Steps:**
1. Click "Edit" in the menu bar
**Expected Result:** Dropdown opens with a single disabled item: "No actions available".

### MT-4: View Menu Opens with Correct Items
**Steps:**
1. Click "View" in the menu bar
**Expected Result:** Dropdown opens with: Toggle Sidebar (Ctrl+B), Toggle Status Bar, Toggle Bottom Panel (Ctrl+J), Toggle Right Panel, separator, Fullscreen (F11), separator, Zoom In (Ctrl+=), Zoom Out (Ctrl+-). All items are disabled.

### MT-5: Playback Menu Opens with Correct Items
**Steps:**
1. Click "Playback" in the menu bar
**Expected Result:** Dropdown opens with: Play/Pause (Space), Stop, separator, Skip Forward, Skip Backward, separator, Loop, separator, Playback Speed ▸. All items are disabled.

### MT-6: Playback Speed Submenu Expands
**Steps:**
1. Click "Playback" in the menu bar
2. Hover over "Playback Speed"
**Expected Result:** A submenu appears to the right with: 0.25x, 0.5x, 1.0x, 1.5x, 2.0x. All items are disabled.

### MT-7: Recent Files Submenu Expands
**Steps:**
1. Click "File" in the menu bar
2. Hover over "Recent Files"
**Expected Result:** A submenu appears to the right with: "No recent files" (disabled).

### MT-8: Help Menu Opens with Correct Items
**Steps:**
1. Click "Help" in the menu bar
**Expected Result:** Dropdown opens with: About Vido, separator, Check for Updates. Both items are disabled.

### MT-9: Keyboard Shortcut Hints are Right-Aligned
**Steps:**
1. Click "File" in the menu bar
2. Observe the shortcut hints
**Expected Result:** Shortcut hints (e.g., "Ctrl+O") appear right-aligned within each menu item.

### MT-10: Menu Hover Effects
**Steps:**
1. Open any menu dropdown
2. Move mouse over the menu items
**Expected Result:** Enabled items show a blue selection highlight on hover (#04395e). Disabled items do not highlight. Top-level items show gray highlight (#2a2d2e).

### MT-11: Menu Closes on Outside Click
**Steps:**
1. Click "File" to open the menu
2. Click anywhere outside the menu
**Expected Result:** The dropdown closes.

### MT-12: Exit Menu Item Works
**Steps:**
1. Click "File" > "Exit"
**Expected Result:** The application closes.

### MT-13: Dropdown Styling Matches VS Code
**Steps:**
1. Open any menu dropdown
2. Observe the visual styling
**Expected Result:** Dark background (#1f1f1f), border (#2b2b2b), rounded corners (2px), drop shadow, separators are subtle horizontal lines.

### MT-14: Menu Does Not Interfere with Title Bar Drag
**Steps:**
1. Click and drag the title area to the right of the menus
**Expected Result:** The window moves. Dragging on the menu items opens them instead.

## Regression Tests

### RT-1: Window Controls Still Work
**Precondition:** App is running.
**Steps:**
1. Click minimize, then restore from taskbar
2. Click maximize, then restore
3. Click close
**Expected Result:** All window controls function correctly.

### RT-2: Double-Click Title Bar Still Toggles Maximize
**Precondition:** App is running in normal state.
**Steps:**
1. Double-click the draggable title area (right of menus)
**Expected Result:** Window toggles between maximized and normal.

### RT-3: No Resize Flicker
**Steps:**
1. Resize the window by dragging edges
**Expected Result:** No flicker or white trailing edges.

### RT-4: All Tests Pass
**Steps:**
1. Run `dotnet test Vido.sln`
**Expected Result:** All 13 tests pass.

## Unit Tests
- [x] All existing TitleBarViewModel tests still pass (12 tests)
- [x] SmokeTests.TestInfrastructure_IsWorking
- Note: Menu items are currently XAML-only with no ViewModel logic to test. Unit tests will be added when menu commands are wired to services in future tickets.
