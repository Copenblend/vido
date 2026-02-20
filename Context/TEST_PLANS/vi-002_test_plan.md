# vi-002: Custom Title Bar — Test Plan

## Manual Tests

### MT-1: Title Bar is Visible
**Steps:**
1. Launch the app via Run & Debug
2. Observe the top of the window
**Expected Result:** A 30px title bar is visible with an app icon on the left, "Vido" title text, and three window control buttons (minimize, maximize/restore, close) on the right.

### MT-2: Minimize Button Works
**Steps:**
1. Launch the app
2. Click the minimize button (`_` icon, leftmost of the three window controls)
**Expected Result:** The window minimizes to the taskbar. Clicking the taskbar icon restores it.

### MT-3: Maximize Button Works
**Steps:**
1. Launch the app (starts in normal/restored state)
2. Click the maximize/restore button (square icon, middle of the three window controls)
**Expected Result:** The window maximizes to fill the screen. The icon changes to a "restore" icon (two overlapping rectangles). Tooltip changes to "Restore Down".

### MT-4: Restore Button Works
**Steps:**
1. With the window maximized (from MT-3)
2. Click the maximize/restore button again
**Expected Result:** The window restores to its previous size and position. The icon changes back to a single square. Tooltip changes to "Maximize".

### MT-5: Close Button Works
**Steps:**
1. Launch the app
2. Click the close button (✕ icon, rightmost)
**Expected Result:** The application closes.

### MT-6: Close Button Hover is Red
**Steps:**
1. Launch the app
2. Hover over the close button without clicking
**Expected Result:** The close button background turns red (#c42b1c).

### MT-7: Minimize/Maximize Hover is Gray
**Steps:**
1. Launch the app
2. Hover over the minimize button, then the maximize button
**Expected Result:** Each button's background turns gray (#3d3d3d) on hover.

### MT-8: Double-Click Title Bar Toggles Maximize
**Steps:**
1. Launch the app in normal state
2. Double-click the title bar area (between the icon and window controls)
**Expected Result:** The window maximizes. Double-clicking again restores it.

### MT-9: Title Bar Dragging Moves Window
**Steps:**
1. Launch the app
2. Click and drag the title bar area
**Expected Result:** The window moves with the mouse.

### MT-10: Aero Snap Integration
**Steps:**
1. Launch the app
2. Drag the window to the top edge of the screen
**Expected Result:** The window maximizes via Aero Snap. The maximize/restore icon updates to the restore icon.

### MT-11: Theme Colors Applied
**Steps:**
1. Launch the app
2. Observe the title bar and window background
**Expected Result:** Title bar background is #1f1f1f. Window border is #2b2b2b. Title text is #9d9d9d. Window control icons are #cccccc.

## Regression Tests

### RT-1: Window Resize Still Works
**Precondition:** App is running in normal (non-maximized) state.
**Steps:**
1. Hover over window edges and corners
2. Drag to resize
**Expected Result:** Window resizes from all edges and corners. Minimum size 800x600 is enforced.

### RT-2: No Resize Flicker
**Precondition:** App is running.
**Steps:**
1. Quickly resize the window by dragging a corner
**Expected Result:** No white/bright flicker or trailing edges during resize.

### RT-3: Build Succeeds
**Steps:**
1. Run `dotnet build Vido.sln`
**Expected Result:** Build succeeds with 0 warnings and 0 errors.

### RT-4: All Tests Pass
**Steps:**
1. Run `dotnet test Vido.sln`
**Expected Result:** All 13 tests pass (1 smoke + 12 TitleBarViewModel).

## Unit Tests
- [x] SmokeTests.TestInfrastructure_IsWorking — Verifies test infrastructure
- [x] TitleBarViewModelTests.DefaultTitle_IsVido
- [x] TitleBarViewModelTests.DefaultIsMaximized_IsFalse
- [x] TitleBarViewModelTests.MinimizeCommand_CallsWindowServiceMinimize
- [x] TitleBarViewModelTests.ToggleMaximizeCommand_CallsWindowServiceToggleMaximize
- [x] TitleBarViewModelTests.ToggleMaximizeCommand_SetsIsMaximized_WhenStateBecomesMaximized
- [x] TitleBarViewModelTests.ToggleMaximizeCommand_ClearsIsMaximized_WhenStateBecomesNormal
- [x] TitleBarViewModelTests.CloseCommand_CallsWindowServiceClose
- [x] TitleBarViewModelTests.SyncWindowState_Maximized_SetsIsMaximizedTrue
- [x] TitleBarViewModelTests.SyncWindowState_Normal_SetsIsMaximizedFalse
- [x] TitleBarViewModelTests.SyncWindowState_Minimized_SetsIsMaximizedFalse
- [x] TitleBarViewModelTests.Title_RaisesPropertyChanged
- [x] TitleBarViewModelTests.IsMaximized_RaisesPropertyChanged
