# vi-008 Test Plan: FFmpeg Integration — Video Playback Engine

## Unit Tests (Automated)

All automated tests are in `tests/Vido.Tests/`:

### PlaybackStateTests (3 tests)
- PlaybackState enum has expected integer values (None=0, Playing=1, Paused=2, Stopped=3)
- Enum has exactly 4 members
- ToString returns correct names

### VideoMetadataTests (4 tests)
- Required properties (FilePath, FileName) are properly set
- Optional properties default to zero/null
- All properties can be initialized with values
- Resolution computed property formats correctly (e.g., "1920x1080")

### FrameDataTests (3 tests)
- Required properties (PixelData, Width, Height, Stride) are properly set
- Pts defaults to TimeSpan.Zero
- Pts can be set via init

### FFmpegInitializerTests (9 tests)
- ContainsFFmpegLibraries returns false for nonexistent directory
- ContainsFFmpegLibraries returns false for empty directory
- ContainsFFmpegLibraries returns false when no avcodec DLL present
- ContainsFFmpegLibraries returns true for versioned avcodec DLL (avcodec-62.dll)
- ContainsFFmpegLibraries returns true for plain avcodec.dll
- ContainsFFmpegLibraries returns true for wildcard match (avcodec-60.dll)
- ResolveFFmpegPath returns null or valid directory (does not throw)
- IsInitialized returns bool in test environment
- Initialize returns bool without throwing

### FFmpegVideoEngineTests (13 tests)
- Initial state is PlaybackState.None
- Initial position is TimeSpan.Zero
- Initial duration is TimeSpan.Zero
- Initial volume is 75
- Initial muted is false
- Initial looping is false
- Initial metadata is null
- Volume clamps to 0-100 range
- IsMuted can be toggled
- IsLooping can be toggled
- LoadAsync throws InvalidOperationException when FFmpeg not initialized
- Play/Pause/Stop/Seek are no-ops when no media loaded
- Events can be subscribed without throwing
- Dispose can be called multiple times safely

---

## Manual Tests

### Prerequisites
1. Build the solution — FFmpeg native DLLs are provided automatically by the FFmpeg.LGPL NuGet package
2. Run the application

### Test 1: FFmpeg Initialization (with NuGet DLLs)
1. Build the solution (`dotnet build`)
2. Launch the application
3. **Expected**: Application starts without errors, no FFmpeg warning in console/log
4. **Expected**: FFmpeg DLLs (avcodec-62.dll, avformat-62.dll, etc.) are in the output directory automatically

### Test 2: FFmpeg DLL Discovery
1. Build and check `src/Vido.App/bin/Debug/net8.0-windows/` directory
2. **Expected**: FFmpeg DLLs (avcodec-62.dll, avformat-62.dll, avutil-60.dll, swscale-9.dll, swresample-6.dll) are present in the output directory via NuGet runtimes convention

---

## Regression Tests

### Test R1: Existing Functionality Unaffected
1. Launch the application
2. Open a folder via File > Open Folder
3. Expand directories in file explorer
4. Right-click files — verify context menus work
5. Toggle Show Hidden Files — verify hidden behavior
6. Close folder — verify cleanup
7. **Expected**: All existing vi-001 through vi-007 functionality works normally

### Test R2: Build and Test Suite
1. Run `dotnet build` — **Expected**: 0 errors, 0 warnings
2. Run `dotnet test` — **Expected**: 172 tests passing, 0 failures
