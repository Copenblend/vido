# vi-022 Test Plan — About Dialog & Help Menu

## Manual Tests

### MT-1: About Dialog Opens from Help Menu
1. Launch the application
2. Click the "Help" menu in the title bar
3. Click "About Vido"
4. **Expected**: A modal About dialog appears centered on the main window

### MT-2: About Dialog Content
1. Open the About dialog via Help > About Vido
2. **Expected**: The dialog shows:
   - The Vido logo (small icon)
   - "Vido" title in large text
   - "Ultra-Performant Video Player" tagline
   - Version number (e.g. "0.1.0") — read from the assembly version
   - .NET Runtime version (e.g. ".NET 8.0.x")
   - FFmpeg version (e.g. "7.1.1" or similar)
3. **Expected**: All version strings are non-empty and look reasonable

### MT-3: About Dialog Styling
1. Open the About dialog
2. **Expected**: Dialog matches the VS Code Dark Modern theme:
   - Dark background (editor background color)
   - Light text (primary foreground color)
   - Blue accent-colored OK button
   - Rounded border with subtle accent stroke
   - No Windows chrome (custom frameless dialog)

### MT-4: About Dialog Close — OK Button
1. Open the About dialog
2. Click the "OK" button
3. **Expected**: The dialog closes and the main window is accessible again

### MT-5: About Dialog Close — Escape Key
1. Open the About dialog
2. Press the Escape key
3. **Expected**: The dialog closes and the main window is accessible again

### MT-6: About Dialog Is Modal
1. Open the About dialog
2. Try to click on the main window behind the dialog
3. **Expected**: The main window does not respond to clicks while the dialog is open
4. Close the dialog
5. **Expected**: The main window is responsive again

### MT-7: Check for Updates Menu Item
1. Click the "Help" menu in the title bar
2. Click "Check for Updates..."
3. **Expected**: A message box appears with the text "You are running the latest version of Vido."
4. Click "OK" on the message box
5. **Expected**: The message box closes

### MT-8: Help Menu Items Are Enabled
1. Click the "Help" menu in the title bar
2. **Expected**: Both "About Vido" and "Check for Updates..." menu items are enabled (not grayed out)
3. **Expected**: Both items are clickable

### MT-9: FFmpeg Version After Initialization
1. Launch the application (FFmpeg initializes on startup)
2. Open the About dialog via Help > About Vido
3. **Expected**: The FFmpeg version field shows a valid version string (not empty, not "N/A")
4. **Note**: If FFmpeg fails to initialize, the field may show "N/A" — this is acceptable

## Automated Tests

### AT-1: FFmpegInitializer.VersionString Property
- **Test**: `FFmpegInitializerTests.VersionString_IsNullOrNonEmpty`
- **Validates**: The `VersionString` property is either null (before initialization) or a non-empty string
- **Status**: Passing (included in 809 total tests)
