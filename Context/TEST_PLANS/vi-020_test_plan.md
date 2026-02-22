# vi-020 Test Plan — Settings Panel (Tab-Based)

## Manual Tests

### MT-1: Settings Tab Opens from Activity Bar
1. Launch the application
2. Click the gear icon at the bottom of the activity bar
3. **Expected**: A "Settings" tab opens in the main content area
4. **Expected**: The tab shows a search bar at the top and categorized settings below

### MT-2: Settings Tab Can Be Closed and Reopened
1. Open the Settings tab via the activity bar gear icon
2. Click the close (X) button on the Settings tab
3. **Expected**: The Settings tab closes and the Player tab becomes active
4. Click the gear icon again
5. **Expected**: The Settings tab reopens with the same content

### MT-3: Settings Tab Caching
1. Open Settings, change the search text to "volume"
2. Switch to the Player tab
3. Switch back to the Settings tab
4. **Expected**: The search text "volume" is still present (tab is cached)

### MT-4: Playback Settings
1. Open Settings
2. Locate the "Playback" category
3. **Expected**: Three settings are visible:
   - Default Volume (number input)
   - Default Playback Speed (dropdown: 0.25x, 0.5x, 1.0x, 1.5x, 2.0x)
   - Loop Playback (checkbox)
4. Change Default Volume to 75
5. Close and reopen the Settings tab
6. **Expected**: Default Volume still shows 75

### MT-5: File Explorer Settings
1. Open Settings
2. Locate the "File Explorer" category
3. **Expected**: One setting is visible:
   - Show Hidden Files (checkbox)
4. Toggle the checkbox
5. **Expected**: The setting persists after reopening Settings

### MT-6: Plugins Settings
1. Open Settings
2. Locate the "Plugins" category
3. **Expected**: One setting is visible:
   - Custom Plugin Registry URL (text input)
4. Enter a URL like "file://C:/test/registry.json"
5. **Expected**: The URL is saved to settings

### MT-7: Plugin Settings Section
1. Install and enable a plugin that declares settings in its manifest
2. Open Settings
3. **Expected**: A new category appears with the plugin's display name and a puzzle piece icon
4. **Expected**: The plugin's settings are displayed with appropriate controls

### MT-8: Search Filtering — By Title
1. Open Settings
2. Type "volume" in the search bar
3. **Expected**: Only the Playback category is shown, with the "Default Volume" setting visible
4. Clear the search bar
5. **Expected**: All categories are shown again

### MT-9: Search Filtering — By Description
1. Open Settings
2. Type "hidden" in the search bar
3. **Expected**: The File Explorer category appears with "Show Hidden Files"

### MT-10: Search Filtering — By Category Name
1. Open Settings
2. Type "Playback" in the search bar
3. **Expected**: The entire Playback category is shown with all 3 settings

### MT-11: Search Filtering — No Results
1. Open Settings
2. Type "xyznonexistent" in the search bar
3. **Expected**: "No settings found matching your search." message is displayed

### MT-12: Settings Persist Across Restarts
1. Open Settings
2. Change Default Volume to 80
3. Set Default Playback Speed to 2.0x
4. Enable Loop Playback
5. Close the application
6. Reopen the application
7. Open Settings
8. **Expected**: Volume is 80, Speed is 2.0x, Loop is enabled

### MT-13: Number Input Validation
1. Open Settings
2. Locate the Default Volume number input
3. Try typing non-numeric characters (e.g., letters)
4. **Expected**: Non-numeric input is rejected, only digits and decimal points accepted

### MT-14: Visual Styling
1. Open Settings
2. **Expected**: The page uses the Dark Modern theme
3. **Expected**: Search bar has a magnifying glass icon
4. **Expected**: Category headers have separator lines above them
5. **Expected**: Plugin categories show a puzzle piece icon next to the name
6. **Expected**: CheckBoxes have rounded corners and blue accent when checked
7. **Expected**: TextBoxes have rounded corners and blue border on focus
8. **Expected**: ComboBoxes have dark dropdown with hover highlights

## Automated Tests

### Unit Tests (SettingsViewModelTests.cs — 20 tests)
| Test | Description |
|------|-------------|
| Constructor_CreatesAppSettingsCategories | Verifies 3+ app categories exist |
| Constructor_PlaybackCategory_HasExpectedSettings | Volume, Speed, Loop present |
| Constructor_FileExplorerCategory_HasExpectedSettings | ShowHiddenFiles present |
| Constructor_PluginsCategory_HasExpectedSettings | CustomRegistryUrl present |
| Constructor_AppCategories_AreNotMarkedAsPlugin | IsPlugin=false for all app categories |
| FilteredCategories_ShowsAll_WhenSearchEmpty | All categories visible with empty search |
| SearchText_FiltersSettingsByTitle | Filters by title match |
| SearchText_FiltersSettingsByDescription | Filters by description match |
| SearchText_MatchesCategoryName_ShowsEntireCategory | Category name match shows all settings |
| SearchText_NoMatch_ShowsNoCategories | No results for no match |
| SearchText_EmptyAfterFilter_ShowsAll | Clearing search restores all |
| SearchText_IsCaseInsensitive | Case-insensitive search |
| NoResults_FalseWhenSearchEmpty | NoResults property correct |
| Constructor_WithPluginHost_AddsPluginCategories | Plugin categories added |
| Constructor_WithPluginHost_PluginCategoryHasCorrectSettings | Plugin settings correct |
| Constructor_SkipsInactivePlugins | Disabled plugins excluded |
| Constructor_SkipsPluginsWithNoSettings | No-settings plugins excluded |
| RefreshPluginSettings_RebuildsPluginCategories | Refresh rebuilds plugin section |
| SearchText_FiltersPluginSettings | Search includes plugin settings |
| Constructor_ThrowsOnNullSettingsService | Null validation |

### Unit Tests (AppSettingsStoreTests.cs — 25 tests)
| Test | Description |
|------|-------------|
| Get_Volume_ReturnsScaledValue | Volume 0.75 → 75.0 |
| Get_Volume_RoundsToWholeNumber | Volume 0.333 → 33.0 |
| Get_Speed_ReturnsFormattedString | Speed 1.5 → "1.5x" |
| Get_Loop_ReturnsBoolValue | Boolean round-trip |
| Get_ShowHiddenFiles_ReturnsBoolValue | Boolean round-trip |
| Get_CustomRegistryUrl_ReturnsEmptyWhenNoCustomUrl | Single URL list |
| Get_CustomRegistryUrl_ReturnsCustomUrlWhenPresent | Two URL list |
| Get_UnknownKey_ReturnsDefault | Unknown key handling |
| Get_IsCaseInsensitive | Case-insensitive keys |
| Set_Volume_UpdatesSettingsAndQueuesSave | Volume set + save |
| Set_Volume_ClampsToRange | Volume >100 or <0 clamped |
| Set_Speed_ParsesFormattedString | "2.0x" → 2.0 |
| Set_Speed_UnknownValueDefaultsTo1 | Invalid speed defaults |
| Set_Loop_UpdatesBoolean | Boolean set |
| Set_ShowHiddenFiles_UpdatesBoolean | Boolean set |
| Set_CustomRegistryUrl_AddsUrl | Adds custom URL |
| Set_CustomRegistryUrl_ReplacesExisting | Replaces custom URL |
| Set_CustomRegistryUrl_RemovesWhenEmpty | Removes custom URL |
| Set_UnknownKey_DoesNothing | No save for unknown key |
| Set_FiresSettingChangedEvent | Event fired on set |
| Set_UnknownKey_DoesNotFireEvent | No event for unknown key |
| Reset_ReturnsFalse | Reset not supported |
| Constructor_ThrowsOnNullSettingsService | Null validation |

**Total**: 45 new tests, all passing (779 total with existing 734)
