# vi-013 Test Plan — Status Bar

## Manual Tests

### MT-1: Status Bar Visible by Default
1. Launch Vido
2. **Expected**: A blue status bar appears at the very bottom of the window
3. **Expected**: Height is 22px, background is VS Code blue (#007acc)
4. **Expected**: A thin border line separates the status bar from the content above

### MT-2: No Video State
1. Launch Vido without opening any video
2. **Expected**: Left side shows "No file" in white text
3. **Expected**: No items visible on the right side (resolution, duration, codec are hidden)

### MT-3: Video Loaded State
1. Open a folder containing video files
2. Double-click a video file to play it
3. **Expected**: Left side shows the file name (e.g., "sample.mp4")
4. **Expected**: Right side shows duration (e.g., "01:23:45"), resolution (e.g., "1920x1080"), and codec (e.g., "H264")
5. **Expected**: Right items are separated by small dot separators
6. **Expected**: Duration (first right item) does NOT have a preceding dot separator

### MT-4: Metadata Updates on Video Switch
1. Play one video file
2. Double-click a different video file
3. **Expected**: All status bar items update to reflect the new video's metadata
4. **Expected**: File name changes, resolution/duration/codec update accordingly

### MT-5: Tooltips
1. Hover over the file name in the status bar
2. **Expected**: Tooltip shows the full file path (e.g., "C:\Videos\sample.mp4")
3. Hover over the duration item
4. **Expected**: Tooltip shows "Video duration"
5. Hover over the resolution item
6. **Expected**: Tooltip shows "Video resolution"
7. Hover over the codec item
8. **Expected**: Tooltip shows "Video codec"

### MT-6: Toggle Status Bar via View Menu
1. Click View > Toggle Status Bar
2. **Expected**: Status bar disappears, content area expands to fill the space
3. Click View > Toggle Status Bar again
4. **Expected**: Status bar reappears with the same content as before

### MT-7: Short Duration Format
1. Play a video shorter than 1 hour
2. **Expected**: Duration shows in mm:ss format (e.g., "02:30")
3. Play a video longer than 1 hour
4. **Expected**: Duration shows in hh:mm:ss format (e.g., "01:15:30")

### MT-8: Video Stopped
1. Play a video file (status bar shows metadata)
2. Click the Stop button
3. **Expected**: Status bar shows "No file" on the left, right items become hidden

### MT-9: Text Styling
1. With a video loaded, observe the status bar
2. **Expected**: Text is white (#ffffff), font is Segoe UI at 12px
3. **Expected**: Separator dots between right items are semi-transparent white (#99ffffff)
4. **Expected**: Text is vertically centered in the 22px bar

## Automated Tests (StatusBarViewModelTests — 31 tests)

| # | Test Name | Verifies |
|---|-----------|----------|
| 1 | `InitialState_HasBuiltInLeftItems` | Single left item (FileName) registered |
| 2 | `InitialState_HasBuiltInRightItems` | Three right items: Duration, Resolution, Codec in priority order |
| 3 | `InitialState_FileNameShowsNoFile` | Default text is "No file" |
| 4 | `InitialState_RightItemsAreHidden` | Resolution, Duration, Codec are not visible initially |
| 5 | `UpdateFromMetadata_SetsFileName` | File name updates from metadata |
| 6 | `UpdateFromMetadata_SetsResolution` | Resolution text and visibility set from metadata |
| 7 | `UpdateFromMetadata_SetsDuration` | Duration formatted and visibility set from metadata |
| 8 | `UpdateFromMetadata_SetsCodec` | Codec shown uppercase from metadata |
| 9 | `UpdateFromMetadata_NullCodec_ShowsUnknown` | Null codec displays "UNKNOWN" |
| 10 | `UpdateFromMetadata_Null_ResetsToNoFile` | Null metadata resets to default state |
| 11 | `UpdateFromMetadata_SetsFilePathAsTooltip` | Full file path used as tooltip |
| 12 | `MetadataChangedOnPlayer_UpdatesStatusBar` | PropertyChanged on PlayerVM propagates to status bar |
| 13 | `FormatDuration_CorrectlyFormats` (Theory×4) | Duration formatting: 0s, 65s, 3661s, 3723s |
| 14 | `RegisterItem_AddsLeftItem` | Plugin left item added to LeftItems |
| 15 | `RegisterItem_AddsRightItem` | Plugin right item added to RightItems |
| 16 | `RegisterItem_InsertsInPriorityOrder` | Priority ordering places item between existing items |
| 17 | `RegisterItem_DuplicateId_ThrowsArgumentException` | Duplicate ID throws |
| 18 | `UnregisterItem_RemovesItem` | Item removed from collection |
| 19 | `UnregisterItem_NonexistentId_NoOp` | No-op for unknown ID |
| 20 | `FindItem_ReturnsCorrectItem` | Returns same reference as registered |
| 21 | `FindItem_NonexistentId_ReturnsNull` | Returns null for unknown ID |
| 22 | `FindItem_FindsBuiltInItems` | All 4 built-in items findable |
| 23 | `StatusBarItem_TextChange_RaisesPropertyChanged` | Text setter fires INPC |
| 24 | `StatusBarItem_IsVisibleChange_RaisesPropertyChanged` | IsVisible setter fires INPC |
| 25 | `StatusBarItem_SameValue_DoesNotRaisePropertyChanged` | Same value suppresses INPC |
| 26 | `Dispose_UnsubscribesFromPlayerEvents` | Dispose removes event subscription |
| 27 | `Dispose_DoesNotThrowOnMultipleCalls` | Idempotent dispose |
| 28 | `ShortDuration_OmitsHours` | Durations <1hr use mm:ss format |

## MainWindowViewModelTests (3 new tests)

| # | Test Name | Verifies |
|---|-----------|----------|
| 1 | `Constructor_StatusBarVisibleByDefault` | IsStatusBarVisible defaults to true |
| 2 | `ToggleStatusBar_TogglesVisibility` | Toggle flips visibility both ways |
| 3 | `IsStatusBarVisible_RaisesPropertyChanged` | PropertyChanged fires on toggle |

## Regression Tests

- All 337 pre-existing tests pass unchanged
- Total test count: 371 (337 prior + 31 StatusBarViewModelTests + 3 MainWindowViewModelTests)
