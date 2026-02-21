# vi-011 Test Plan — Bottom Panel: Output Log

## Manual Tests

### MT-1: Bottom Panel Shows Output Tab
1. Launch Vido
2. Press Ctrl+J (or View > Toggle Bottom Panel)
3. **Expected**: Bottom panel appears with "OUTPUT" tab header
4. **Expected**: The output log panel is displayed below the editor area

### MT-2: Log Entries Appear on Startup
1. Launch Vido
2. Open the bottom panel (Ctrl+J)
3. **Expected**: At least one entry visible: "Vido started" from source "App"
4. **Expected**: If FFmpeg initializes, an FFmpeg entry also appears

### MT-3: Log Entries Are Color-Coded
1. Open the bottom panel
2. Observe entries of different levels
3. **Expected**: Debug entries appear in grey (#9d9d9d)
4. **Expected**: Info entries appear in white/light grey (#cccccc)
5. **Expected**: Warning entries appear in yellow (#cca700)
6. **Expected**: Error entries appear in red (#f44747)

### MT-4: Log Entries Are Timestamped
1. Open the bottom panel
2. Observe any entry
3. **Expected**: Each entry has format `[HH:mm:ss.fff] [LVL] message` or `[HH:mm:ss.fff] [LVL] [Source] message`

### MT-5: Folder Open/Close Logged
1. Open the bottom panel
2. File > Open Folder, select any folder
3. **Expected**: Entry appears: "Folder opened: <path>" from source "Explorer"
4. File > Close Folder
5. **Expected**: Entry appears: "Folder closed" from source "Explorer"

### MT-6: Video Playback Events Logged
1. Open a folder with video files, open the bottom panel
2. Double-click a video file
3. **Expected**: Entry: "Loading video: <filename>" from "Player"
4. **Expected**: Entry: "Playing: <filename> (<resolution>, <duration>)" from "Player"
5. Click pause button
6. **Expected**: Entry: "Playback paused" from "Player"
7. Click play button
8. **Expected**: Entry: "Playback resumed" from "Player"
9. Click stop button
10. **Expected**: Entry: "Playback stopped" from "Player"

### MT-7: Auto-Scroll
1. Open the bottom panel
2. Perform several actions to generate many log entries
3. **Expected**: Log automatically scrolls to show the latest entry
4. Click the "Auto-Scroll" toggle button to disable it
5. Scroll up manually, perform more actions
6. **Expected**: Log does NOT auto-scroll; remains at the manually-scrolled position
7. Re-enable auto-scroll
8. **Expected**: Next new entry causes scroll to bottom

### MT-8: Clear Button
1. Open the bottom panel with some entries visible
2. Click the "Clear" button (X icon + "Clear" text)
3. **Expected**: All entries are removed
4. **Expected**: Empty state text "No output yet." appears centered

### MT-9: Filter Cycling
1. Open the bottom panel with entries at various levels
2. Click the filter button (shows "All")
3. **Expected**: Filter changes to "Info+" — Debug entries disappear
4. Click again → "Warn+" — Debug and Info entries disappear
5. Click again → "Errors" — Only Error entries remain
6. Click again → "All" — All entries reappear

### MT-10: Panel Resizable and Collapsible
1. Open the bottom panel
2. Drag the splitter above the panel to resize it
3. **Expected**: Panel height changes smoothly
4. Toggle panel off (Ctrl+J)
5. Toggle panel on again
6. **Expected**: Panel reopens at the same height it was before closing

### MT-11: Monospace Font
1. Open the bottom panel
2. **Expected**: Log entries use a monospace font (Cascadia Code, Consolas, or Courier New)

### MT-12: Shutdown Logging
1. Open the bottom panel
2. Close the application
3. **Note**: The "Vido shutting down" entry is logged but may not be visible since the app is closing. This is for verification in future state persistence scenarios.

## Automated Tests (OutputLogViewModelTests)

| # | Test Name | Verifies |
|---|-----------|----------|
| 1 | `InitialState_HasNoEntries` | Empty entries on construction with no existing logs |
| 2 | `InitialState_AutoScrollEnabled` | Auto-scroll is on by default |
| 3 | `InitialState_FilterIsAll` | Filter starts at "All" / Debug level |
| 4 | `Constructor_LoadsExistingEntries` | Pre-existing LogService entries loaded on construction |
| 5 | `EntryAdded_AppendsToEntries` | New LogEntry callback adds to collection |
| 6 | `EntryAdded_SetsHasEntries` | HasEntries becomes true on first entry |
| 7 | `ClearLog_RemovesAllEntries` | Clear empties collection, resets HasEntries, calls LogService.Clear |
| 8 | `ToggleAutoScroll_TogglesState` | Auto-scroll toggles on/off repeatedly |
| 9 | `CycleFilter_CyclesFromAllToInfoPlusToWarnPlusToErrorsToAll` | Full filter cycle through all levels |
| 10 | `CycleFilter_RebuildsList_OnlyShowsMatchingEntries` | Filter rebuild excludes below-level entries |
| 11 | `Filter_ExcludesNewEntriesBelowLevel` | New entries below filter level are not added |
| 12 | `SetFilter_SetsLevelAndText` | SetFilter updates SelectedLevel and FilterText |
| 13 | `SetFilter_Debug_ShowsAll` | Debug level shows "All" filter text |
| 14 | `LogEntryViewModel_FormatsTimestamp` | Timestamp format HH:mm:ss.fff, correct level tag |
| 15 | `LogEntryViewModel_FormattedLine_WithoutSource` | FormattedLine without source excludes source bracket |
| 16 | `LogEntryViewModel_FormattedLine_WithSource` | FormattedLine with source includes [Source] bracket |
| 17-20 | `LogEntryViewModel_LevelTags` (Theory×4) | DBG/INF/WRN/ERR tags for each LogLevel |

## Regression Tests

- FileExplorerViewModelTests: Constructor updated with ILogService — all 26 tests still pass
- VideoPlayerViewModelTests: Constructor updated with ILogService — all 38 tests still pass
- All 259 pre-existing tests pass unchanged
