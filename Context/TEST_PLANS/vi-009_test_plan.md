# vi-009 Test Plan: Video Player Tab — UI & Controls

## Unit Tests (Automated)

All automated tests are in `tests/Vido.Tests/`:

### VideoPlayerViewModelTests (38 tests)

**Initial State (12 tests)**
- Initial state is PlaybackState.None
- Initial position is TimeSpan.Zero
- Initial duration is TimeSpan.Zero
- Initial volume inherits from engine (75)
- Initial isMuted inherits from engine (false)
- Initial isLooping inherits from engine (false)
- Initial hasMedia is false
- Initial showPlayIcon is true
- Initial positionText is "00:00"
- Initial durationText is "00:00"
- Initial currentFilePath is null
- Initial currentMetadata is null

**Volume (5 tests)**
- SetVolume forwards to engine
- Volume clamps: -10 → 0, 0 → 0, 50 → 50, 100 → 100, 150 → 100

**Mute / Loop Toggles (4 tests)**
- ToggleMute sets IsMuted true
- ToggleMute forwards to engine
- ToggleLoop sets IsLooping true
- ToggleLoop forwards to engine

**Commands Without Media (2 tests)**
- PlayPause does nothing when no media
- Stop does nothing when no media

**Engine Event Handling (5 tests)**
- StateChanged updates ViewModel state and ShowPlayIcon
- StateChanged to Paused shows play icon
- PositionChanged updates position and text
- FrameReady raises ViewModel event
- FormatTime formats correctly: 0→"00:00", 65→"01:05", 3599→"59:59", 3600→"1:00:00", 7261→"2:01:01"

**Skip Navigation (1 test)**
- GetAdjacentVideoFile returns null when no current file

**Seek (2 tests)**
- BeginSeek suppresses position updates
- EndSeek resumes position updates

**Dispose (2 tests)**
- Dispose can be called multiple times safely
- Dispose unsubscribes from engine events

---

## Manual Tests

### Prerequisites
1. Build the solution — FFmpeg native DLLs are provided automatically by the FFmpeg.LGPL NuGet package
2. Open a folder containing video files

### Test 1: Video Tab Always Present
1. Launch the application
2. **Expected**: "Player" tab is visible at the top-left of the editor area with a play icon
3. **Expected**: Tab has bottom accent line (blue)
4. **Expected**: Tab cannot be closed (no close button)

### Test 2: Empty State
1. Launch the application without loading any video
2. **Expected**: Video area shows film reel icon and "Open a video file to begin" centered text
3. **Expected**: Transport controls bar is visible at the bottom

### Test 3: Load Video via Double-Click
1. Open a folder containing video files
2. Double-click a video file in the file explorer
3. **Expected**: Video begins playing in the player tab
4. **Expected**: Empty state text disappears, video frames render
5. **Expected**: Seek bar shows position progressing, time labels update

### Test 4: Load Video via Context Menu
1. Right-click a video file in the file explorer
2. Click "Play" from the context menu
3. **Expected**: Video loads and begins playing

### Test 5: Transport Controls
1. Load a video file
2. Click the Pause button (⏸) — **Expected**: Video pauses, button changes to Play (▶)
3. Click the Play button (▶) — **Expected**: Video resumes
4. Click the Stop button (⏹) — **Expected**: Video stops, position resets to 00:00
5. Play again — **Expected**: Video plays from the beginning

### Test 6: Seek Bar
1. Load a video file and let it play
2. Drag the seek slider to a different position
3. **Expected**: Video seeks to the new position
4. **Expected**: Position time label updates
5. **Expected**: Slider doesn't fight with playback while dragging

### Test 7: Volume and Mute
1. Load a video file with audio
2. Drag the volume slider — **Expected**: Volume changes
3. Click the mute button — **Expected**: Audio mutes, icon changes to muted
4. Click the mute button again — **Expected**: Audio unmutes, icon restores

### Test 8: Loop Toggle
1. Load a short video file
2. Click the loop button — **Expected**: Button highlights with accent color
3. Let the video reach the end — **Expected**: Video loops back to the beginning
4. Click the loop button again — **Expected**: Button returns to normal color

### Test 9: Skip Previous / Next
1. Open a folder with multiple video files
2. Load a video file (not the first or last alphabetically)
3. Click Skip Next (⏭) — **Expected**: Next video file (alphabetically) loads and plays
4. Click Skip Previous (⏮) — **Expected**: Previous video file loads and plays
5. Navigate to the first file — **Expected**: Skip Previous does nothing
6. Navigate to the last file — **Expected**: Skip Next does nothing

### Test 10: Auto-Advance on End (Non-Loop)
1. Ensure loop is OFF
2. Play a short video file that is not the last in the folder
3. Let it play to the end
4. **Expected**: Next video file (alphabetically) loads and plays automatically

### Test 11: Non-Video Files
1. Double-click a non-video file (e.g., .txt) — **Expected**: Nothing happens
2. Double-click a hidden video file — **Expected**: Nothing happens

---

## Regression Tests

### Test R1: Existing Functionality Unaffected
1. Launch the application
2. Open a folder via File > Open Folder
3. Expand directories in file explorer
4. Right-click files — verify context menus work
5. Toggle Show Hidden Files — verify hidden behavior
6. Close folder — verify cleanup
7. **Expected**: All existing vi-001 through vi-008 functionality works normally

### Test R2: Build and Test Suite
1. Run `dotnet build` — **Expected**: 0 errors, 0 warnings
2. Run `dotnet test` — **Expected**: 210 tests passing, 0 failures
