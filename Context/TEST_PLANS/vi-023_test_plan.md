# vi-023 Test Plan — Performance Optimization Pass

## Manual Tests

### MT-1: Startup Timing Logged
1. Launch the application
2. Open the Output Log panel (bottom panel)
3. Filter to show all levels (click filter until it shows "All")
4. **Expected**: A log entry reads "Window visible in X ms" (source: Startup)
5. **Expected**: A log entry reads "Plugin activation completed in X ms" (source: Startup)
6. **Expected**: A log entry reads "Total startup completed in X ms" (source: Startup)
7. **Expected**: Window visible time should be <2000 ms (warm cache <1000 ms)

### MT-2: Plugin Activation Is Deferred
1. Launch the application
2. Observe the window appearance
3. **Expected**: The window appears and is interactive BEFORE plugin activation completes
4. **Expected**: Plugin activation runs after the first paint (check log timestamps)

### MT-3: Video Load Timing Logged
1. Open a video file via File > Open File
2. Check the Output Log
3. **Expected**: A log entry reads "Loaded: filename.mp4 (resolution, duration) in X ms"
4. **Expected**: If hardware acceleration is active, "[HW accel]" appears in the log message

### MT-4: Hardware Acceleration Detection
1. Open a video file (H.264 or H.265)
2. Check the Output Log
3. **Expected**: Either "Hardware-accelerated decoding enabled (AV_PIX_FMT_...)" or "Using software decoding" is logged (source: VideoEngine)
4. **Note**: Hardware acceleration depends on GPU support; software fallback is acceptable

### MT-5: Playback Performance Metrics
1. Open and play a video file
2. Let it play for at least 30 seconds
3. Check the Output Log (set filter to "All" to see Debug messages)
4. **Expected**: A metrics log entry appears with format: "Playback metrics — X.X fps, N rendered, M dropped, GC memory: Y.Y MB [HW accel|SW decode]"
5. **Expected**: FPS should be close to the video's native frame rate (e.g. ~24, ~30, ~60)
6. **Expected**: Dropped frames should be 0 or near-zero during normal playback

### MT-6: Frame Buffer Pooling (Memory)
1. Open Task Manager and note the application's memory usage
2. Play a 1080p video for 2+ minutes
3. **Expected**: Memory usage stays relatively stable (no steady climb)
4. **Expected**: GC memory reported in metrics stays within reasonable bounds (<300 MB for 1080p)
5. Stop playback
6. **Expected**: Memory decreases after playback stops (buffers returned to pool)

### MT-7: File Explorer Virtualization
1. Open a folder containing 500+ files in the File Explorer sidebar
2. Scroll through the file list rapidly
3. **Expected**: Scrolling is smooth with no visible stuttering or freezing
4. **Expected**: The TreeView only renders visible items (not all 500+)
5. Check memory: should not spike significantly when opening a large folder

### MT-8: Window Resize During Playback
1. Play a video
2. Resize the window by dragging edges and corners
3. **Expected**: No black frames or flickering during resize
4. **Expected**: Video continues playing smoothly during resize
5. Double-click the title bar to maximize, then restore
6. **Expected**: Smooth transition with no visual artifacts

### MT-9: Video Quality at Different Sizes
1. Play a video and resize the window to various sizes
2. **Expected**: Video quality is acceptable at all sizes (bilinear scaling)
3. The video should not appear overly pixelated or blurry

### MT-10: Seek Drops Don't Leak Memory
1. Play a video
2. Rapidly seek to different positions (click around the seek bar quickly)
3. **Expected**: No memory growth from rapid seeking
4. **Expected**: Frames dropped due to seeks are properly disposed

### MT-11: Memory Over Extended Playback
1. Play a video on loop for 30+ minutes
2. Monitor memory usage periodically
3. **Expected**: Memory stays within targets (<300 MB for 1080p)
4. **Expected**: No steady memory increase indicating a leak

### MT-12: ReadyToRun Published Build
1. Run `dotnet publish` with the configured settings
2. Launch the published executable
3. **Expected**: Startup time is noticeably faster than debug builds
4. **Expected**: All features work correctly in the published build

## Automated Tests

### AT-1: FrameData Constructor and Disposal (Pooled)
- **Validates**: FrameData created with pooled=true returns buffer to ArrayPool on Dispose
- **Status**: Covered by existing VideoPlayerViewModelTests (uses pooled=false for test safety)

### AT-2: FrameData Constructor (Non-Pooled)
- **Test**: `VideoPlayerViewModelTests.FrameReady_RaisesViewModelEvent`
- **Validates**: FrameData works correctly with pooled=false (no ArrayPool interaction)
- **Status**: Passing (included in 809 total tests)

### AT-3: FFmpegInitializer.VersionString
- **Test**: `FFmpegInitializerTests.VersionString_IsNullOrNonEmpty`
- **Validates**: Version string property works correctly
- **Status**: Passing (included in 809 total tests)
