# vi-017: Drag and Drop — Test Plan

## Manual Tests

### 1. Video File Drop on Player Area
1. Open an Explorer window containing video files (.mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
2. Drag a video file from Windows Explorer onto the video player area
3. **Expected**: A blue border overlay appears with "Drop video file to play" text during drag-over
4. Drop the file
5. **Expected**: The video loads and plays automatically
6. **Expected**: The file's parent folder opens in the file explorer sidebar
7. **Expected**: The Player tab is activated
8. **Expected**: Log entry appears: "Playing dropped file: filename.mp4"

### 2. Video File Drop on File Explorer
1. Open a folder in the file explorer
2. Drag a video file from a different Windows Explorer folder onto the file explorer panel
3. **Expected**: A blue border overlay appears with "Drop to open folder" text during drag-over
4. Drop the file
5. **Expected**: The video's parent folder opens in the file explorer
6. **Expected**: The video loads and plays automatically

### 3. Folder Drop
1. Drag a folder from Windows Explorer onto any area of the Vido window
2. **Expected**: Drag-over overlay appears (blue border)
3. Drop the folder
4. **Expected**: The folder opens in the file explorer sidebar
5. **Expected**: Log entry appears: "Opened dropped folder: foldername"

### 4. Non-Video File Drop
1. Drag a non-video file (.txt, .jpg, .pdf, etc.) from Windows Explorer onto the video player area
2. **Expected**: Drop overlay appears during drag
3. Drop the file
4. **Expected**: A "File type not supported" notification appears at the top center of the window
5. **Expected**: The notification auto-hides after 3 seconds
6. **Expected**: Log entry with warning: "Dropped file type is not supported"

### 5. Drag-Over Visual Feedback
1. Drag a file over the video player area
2. **Expected**: Blue border (FocusBorderBrush #007fd4) and semi-transparent blue background appear
3. **Expected**: Text "Drop video file to play" is visible
4. Drag the file away (without dropping)
5. **Expected**: Overlay immediately disappears
6. Repeat for the file explorer area — should show "Drop to open folder" text

### 6. Drop on Title Bar / Status Bar (Window Fallback)
1. Drag a video file onto the title bar area
2. **Expected**: Copy cursor appears
3. Drop the file
4. **Expected**: The video loads and plays (fallback handler catches it)
5. Repeat with a folder — should open in explorer

### 7. Multiple Files Drop
1. Select multiple files in Windows Explorer and drag them onto the player
2. **Expected**: Only the first file is processed (regardless of type)

### 8. Case-Insensitive Extensions
1. Rename a video file to have uppercase extension (e.g., "video.MP4" or "video.Mkv")
2. Drag and drop it onto the player
3. **Expected**: File is recognized as a video and plays normally

### 9. No-Folder-Open State
1. Start the app without any folder open
2. Drag a video file onto the player area
3. **Expected**: The video's parent folder opens in the explorer sidebar
4. **Expected**: The video loads and plays

### 10. Already-Open Folder
1. Open folder A in the file explorer
2. Drag a video file from folder B onto the player
3. **Expected**: Folder B replaces folder A in the file explorer
4. **Expected**: The video plays

## Automated Tests

All 28 automated tests are in `DropClassifierTests.cs`:

### Classify Method (12 tests)
- `Classify_NullPath_ReturnsInvalid`
- `Classify_EmptyString_ReturnsInvalid`
- `Classify_WhitespaceOnly_ReturnsInvalid`
- `Classify_NonExistentPath_ReturnsInvalid`
- `Classify_ExistingDirectory_ReturnsFolder`
- `Classify_VideoFile_ReturnsVideoFile` (7 extensions: .mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
- `Classify_VideoFile_CaseInsensitive` (3 extensions: .MP4, .Mkv, .AVI)
- `Classify_NonVideoFile_ReturnsUnsupportedFile` (6 extensions: .txt, .jpg, .pdf, .exe, .docx, .zip)

### ClassifyFirst Method (6 tests)
- `ClassifyFirst_NullArray_ReturnsInvalid`
- `ClassifyFirst_EmptyArray_ReturnsInvalid`
- `ClassifyFirst_FolderFirst_ReturnsFolder`
- `ClassifyFirst_VideoFileFirst_ReturnsVideoFile`
- `ClassifyFirst_UnsupportedFileFirst_ReturnsUnsupported`
- `ClassifyFirst_MultipleFiles_UsesFirstOnly`
- `ClassifyFirst_InvalidPath_ReturnsInvalidWithNullPath`
