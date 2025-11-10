# Pre-Recording Architecture Documentation

## Implementation Status

**Current Status (as of 2025-11-10):**

✅ **iOS Encoder Foundation Complete:**
- AppleVideoToolboxEncoder integrated (replaces AppleCaptureVideoEncoder)
- Hardware H.264 encoding via VTCompressionSession
- MP4 output via AVAssetWriter (pass-through mode)
- Normal recording flow operational

⏳ **Not Yet Implemented:**
- Circular buffer for pre-recording (iOS, Android, Windows)
- Pre-recorded frame buffering in memory
- Two-file muxing when recording starts
- `PrependBufferedEncodedDataAsync` implementation

**Next Steps:**
1. Test normal recording with AppleVideoToolboxEncoder
2. Implement circular buffer in iOS encoder
3. Add two-file muxing on recording start

---

## Overview

The pre-recording feature implements a **"look-back" recording** system that continuously captures and stores the most recent N seconds of video in a circular buffer. When the user starts recording, this buffered content is automatically prepended to the final video, creating a seamless recording that includes footage from BEFORE the record button was pressed.

### Real-World Use Case: Performance Driving Recorder

**Scenario:** User is driving their car with the camera running in preview mode. The app monitors vehicle speed via GPS/sensors.

```
Timeline:
  T-10s: Driving at 60 mph, camera running, circular buffer active
  T-5s:  Slowing down to 30 mph, buffer continuously rotating (drops old frames)
  T-0s:  Speed = 0 mph → TRIGGER: Main recording starts automatically
  T+15s: Accelerating 0→100 mph (main recording capturing)
  T+20s: User clicks "Stop" → Recording finishes

Result: Final video contains:
  - 5 seconds BEFORE speed=0 (from circular buffer)
  - 20 seconds of acceleration run (main recording)
  Total: 25 second video showing the complete performance run
```

### Key Design Principles

1. **Circular Buffer with Encoded Data**: Pre-recording stores H.264 encoded frames (NOT raw pixels) in a memory-efficient circular buffer
2. **Time-Based Rotation**: Buffer automatically drops frames older than `PreRecordDuration` (e.g., 5 seconds)
3. **Memory Efficiency**: ~10-15 MB for 5 seconds @ 1080p (vs 1.2 GB uncompressed)
4. **Two-File Muxing**: Pre-recording written to temp file when main recording starts, then muxed with main recording file
5. **Zero Raw Pixel Buffering**: No uncompressed frame data stored in memory

---

## Architecture

### Circular Buffer + Two-File Strategy

```
Phase 1: Camera Running (Pre-Recording Active)
    ↓
Circular Buffer (in memory)
    - Continuously encodes incoming frames to H.264
    - Stores encoded data in memory (Queue<EncodedFrame>)
    - Auto-drops frames older than PreRecordDuration
    - Buffer size: ~10-15 MB for 5 seconds @ 1080p

Phase 2: User Triggers Recording (or automated trigger)
    ↓
Step 2.1: Flush circular buffer to file
    ├─→ Create temporary encoder
    ├─→ Write ALL buffered frames to "pre_recorded.mp4"
    └─→ Finalize and close file

Step 2.2: Start live recording
    └─→ Create main encoder → "recording.mp4"
        └─ Captures frames from trigger onwards

Phase 3: Recording Stops (user clicks stop)
    ↓
Step 3.1: Finalize live recording
    └─→ Stop encoder, close "recording.mp4"

Step 3.2: Muxing Phase
    ├─→ Combine: "pre_recorded.mp4" + "recording.mp4"
    ├─→ Output: "final_output.mp4"
    └─→ Cleanup: Delete temporary files
```

### Why This Works

1. **Circular buffer in memory**: Stores only encoded H.264 frames (~100:1 compression vs raw pixels)
2. **Time-based rotation**: Queue automatically discards frames older than `PreRecordDuration`
3. **Fast flush to file**: When recording starts, buffered frames written to file in <100ms
4. **Two-file muxing**: Platform-native APIs (AVAssetComposition, MediaMuxer) handle seamless concatenation
5. **No frame loss**: Pre-recorded content transitions smoothly into live recording

---

## Memory Optimization

### Evolution of Approaches

#### Approach 1: Raw Pixel Buffering (FAILED - OOM)
- Buffered 46MB of raw BGRA8888 pixels in memory per frame
- 5 seconds @ 30fps @ 1080p = **1.2 GB of RAM**
- Problem: OOM crashes on devices with <2GB available memory
- **Status: REJECTED**

#### Approach 2: Encoded Frame Circular Buffer (CURRENT)
- Pre-recording circular buffer: Stores **encoded H.264 frames** in memory
  - 5 seconds @ 30fps @ 1080p ≈ **10-15 MB** (compressed)
  - Memory reduction: **~100:1 vs raw pixels**
- When recording starts: Write buffer to `pre_recorded.mp4` (~2-5MB file)
- Live recording: Stream to `recording.mp4` (grows as user records)
- Encoder working buffer: ~1-2MB (reusable pixel buffers for encoding)
- **Total peak memory: Circular buffer (~15 MB) + Encoder overhead (~2 MB) = ~17 MB**

#### Why Encoded Frames in Memory?

**Comparison:**
| Storage Type | 1 Frame (1080p) | 5 sec @ 30fps | Notes |
|--------------|----------------|---------------|-------|
| Raw BGRA8888 | 8.3 MB | 1.2 GB | Uncompressed pixels |
| H.264 Encoded | ~75 KB | ~11 MB | Hardware-compressed |
| **Savings** | **110:1** | **109:1** | Acceptable for mobile |

**Key Insight:** Modern mobile devices have hardware H.264 encoders that can compress frames in real-time with minimal CPU overhead. Storing compressed frames allows us to buffer 5+ seconds without memory concerns.

---

## The Circular Recording Challenge

### The Core Problem

**Question:** How do you implement circular recording that keeps only the last N seconds?

**Why this is hard:**
- MP4 file format has a complex structure: `[header][video_data][footer/index]`
- You cannot simply "delete first 2 seconds from a file" - this breaks the entire index
- Continuously writing to a file means it grows forever (not circular!)

### Solution Options

#### ❌ Option 1: Direct Circular File Writing (IMPOSSIBLE)
```
Attempt: Write frames to file, when duration > 5s, delete old data
Problem: MP4 format doesn't support in-place deletion
Result: File corruption, broken video
```
**Status: Not feasible with MP4 format**

#### ❌ Option 2: Segmented Files (COMPLEX)
```
Approach:
  - Write 1-second segments (seg_001.mp4, seg_002.mp4, ...)
  - Keep only last 5 segments
  - Delete oldest segment when creating new one
  - On recording start, mux all segments → pre_recorded.mp4

Pros: True circular behavior
Cons:
  - High disk I/O (5 files created/deleted per second)
  - Muxing 5 files adds latency when recording starts
  - More complex state management
```
**Status: Possible but over-engineered for this use case**

#### ✅ Option 3: Memory Circular Buffer + Write Once (RECOMMENDED)
```
Approach:
  - Encode frames in real-time using hardware encoder
  - Store ENCODED H.264 NAL units in memory (Queue<byte[]>)
  - Queue automatically drops frames older than PreRecordDuration
  - When recording starts: Write entire buffer to file in one shot
  - Continue with live recording to separate file

Pros:
  ✅ Simple implementation (Queue.Dequeue for old frames)
  ✅ No disk I/O during pre-recording phase (battery efficient)
  ✅ Fast: Buffer flush to file takes <100ms
  ✅ Memory efficient: ~10-15 MB for 5 seconds

Cons:
  ⚠ Requires platform encoder to extract encoded bytes
  ⚠ iOS AVAssetWriter doesn't directly expose H.264 NAL units
```
**Status: CURRENT IMPLEMENTATION TARGET**

### iOS-Specific Challenge: AVAssetWriter Limitations

**Problem:** `AVAssetWriter` API doesn't allow extracting encoded H.264 bytes. It's designed for:
1. Input: `CVPixelBuffer` (raw pixels)
2. Internal: Hardware H.264 encoding
3. Output: Direct write to MP4 file (no intermediate buffer access)

**Workaround for iOS (Two-File Approach):**

Since we can't extract encoded bytes from AVAssetWriter, we use a modified circular approach:

```
Phase 1: Pre-Recording Active
  ├─→ Create AVAssetWriter → writes to "pre_temp.mp4"
  ├─→ Continuously append frames
  ├─→ Problem: File grows forever, not circular!
  └─→ Solution: Periodically restart encoder to keep file small

Implementation:
  - Track frame count during pre-recording
  - When frame_count > (PreRecordDuration * fps):
      1. Stop current encoder, finalize "pre_temp.mp4"
      2. Delete old file (if exists)
      3. Start new encoder
      4. Reset frame counter
  - This creates a "rolling file" that never exceeds ~5 seconds

Tradeoff: Brief gap (1-2 frames) during encoder restart
          Acceptable for pre-recording use case
```

**This is the current iOS implementation strategy.**

---

## Platform Implementations

### 1. iOS/macOS (AVAssetComposition)

**File**: `Apple/SkiaCamera.Apple.cs` - `MuxVideosInternal()` method

Uses **AVAssetComposition** API for muxing:

```csharp
// Load both video files as AVAsset
var preAsset = AVAsset.FromUrl(NSUrl.FromFilename(preRecordedPath));
var liveAsset = AVAsset.FromUrl(NSUrl.FromFilename(liveRecordingPath));

// Create composition
var composition = new AVMutableComposition();
var videoTrack = composition.AddMutableTrack(AVMediaType.Video, AVMediaTypes.Video);

// Insert pre-recorded first
videoTrack.InsertTimeRange(preRange, preTrack, currentTime, out var error);
currentTime = CMTimeAdd(currentTime, preAsset.Duration);

// Then insert live recording
videoTrack.InsertTimeRange(liveRange, liveTrack, currentTime, out var error);

// Export to final output
var exporter = new AVAssetExportSession(composition, AVAssetExportSessionPreset.MediumQuality);
exporter.ExportAsynchronously(() => { /* complete */ });
```

**Why this works**:
- Native iOS framework, zero external dependencies
- AVAssetComposition handles the sequencing
- Exporter uses hardware H.264 encoding (GPU-accelerated)
- Async completion callback avoids blocking UI

---

### 2. Android (MediaMuxer + MediaExtractor)

**File**: `Platforms/Android/SkiaCamera.Android.cs` - `MuxVideosInternal()` method

Uses **MediaMuxer** API for muxing:

```csharp
// Extract tracks from both files
var extractor1 = new MediaExtractor();
extractor1.SetDataSource(preRecordedPath);

var extractor2 = new MediaExtractor();
extractor2.SetDataSource(liveRecordingPath);

// Create muxer for output
var muxer = new MediaMuxer(outputFileDescriptor, MediaMuxer.OutputFormatMpeg4);

// Copy all tracks from both inputs to output
ExtractTracksAndWriteToMuxer(extractor1, muxer);
ExtractTracksAndWriteToMuxer(extractor2, muxer);

muxer.Stop();
muxer.Release();
```

**Why this works**:
- Native Android API, no external dependencies
- MediaExtractor reads encoded frames (NOT decoding, stays as H.264)
- MediaMuxer writes re-encoded frames in sequence
- More efficient than frame-by-frame re-encoding

**Important**: For Android, you need a FileDescriptor (not a string path) for the output.

---

### 3. Windows (FFmpeg CLI)

**File**: `Platforms/Windows/SkiaCamera.Windows.cs` - `MuxVideosInternal()` method

Uses **FFmpeg command-line tool** for muxing:

```csharp
// Use FFmpeg to concat videos
var ffmpegPath = "ffmpeg.exe"; // or full path if bundled
var concatFile = Path.Combine(Path.GetDirectoryName(outputPath), "concat.txt");

// Create concat demuxer file
File.WriteAllText(concatFile, $"file '{preRecordedPath}'\nfile '{liveRecordingPath}'");

// Run FFmpeg
var process = new Process();
process.StartInfo = new ProcessStartInfo(ffmpegPath, 
    $"-f concat -safe 0 -i {concatFile} -c copy {outputPath}");
process.Start();
process.WaitForExit();
```

**Why this works**:
- FFmpeg is the standard for video operations on Windows
- `-c copy` means "copy frames without re-encoding" (fast)
- Concat demuxer is the most reliable way to sequence videos

**Note**: You must ensure FFmpeg is installed or bundled with the app.

---

## Platform-Specific File Organization

Each platform has a partial class file with its muxing implementation:

```
DrawnUi.Maui.Camera/
├── SkiaCamera.cs                          ← Shared dispatcher (throws PlatformNotSupportedException)
├── Apple/
│   ├── SkiaCamera.Apple.cs                ← iOS/macOS muxing implementation
│   ├── AppleVideoToolboxEncoder.cs        ← iOS/macOS encoder (current)
│   └── AppleCaptureVideoEncoder.cs        ← iOS/macOS encoder (legacy, replaced)
├── Platforms/
│   ├── Android/
│   │   ├── SkiaCamera.Android.cs          ← Android muxing implementation
│   │   └── AndroidCaptureVideoEncoder.cs  ← Android encoder
│   └── Windows/
│       ├── SkiaCamera.Windows.cs          ← Windows muxing implementation
│       └── WindowsCaptureVideoEncoder.cs  ← Windows encoder
```

**Rule**: All platform-specific code MUST be in the platform-specific file, never in the shared `SkiaCamera.cs`.

**iOS Encoder Note**: AppleVideoToolboxEncoder replaced AppleCaptureVideoEncoder for hardware H.264 encoding via VTCompressionSession.

---

## Control Flow

### RecordingMode.PreRecording

1. User clicks "Start Recording" with pre-recording enabled
2. `SkiaCamera.StartAsync()` is called
3. Creates **pre-recording encoder** → outputs to `pre_recorded.mp4`
4. Pre-recording encoder captures frames happening at that moment
5. Pre-recording data is NOT buffered in memory—it's written to disk in real-time
6. (Optional) User may set `IsPreRecordingMode = false` to switch to live-only

### During Recording

1. Live recording encoder captures frames → outputs to `recording.mp4`
2. Both encoders may be running simultaneously (if pre-recording was on)
3. Each encoder has its own `AppleVideoToolboxEncoder`/`AndroidCaptureVideoEncoder`/`WindowsCaptureVideoEncoder` instance
4. No frame data is shared between them

### When User Stops Recording

1. `SkiaCamera.StopAsync()` is called
2. Both encoders finish writing (sync point)
3. Both output files are now complete
4. **Muxing phase begins**: Call `MuxVideosInternal(preRecordedPath, liveRecordingPath, outputPath)`
5. Muxing uses platform-specific API (AVAssetComposition, MediaMuxer, FFmpeg)
6. Wait for muxing to complete (async)
7. Delete temporary files: `pre_recorded.mp4`, `recording.mp4`
8. Return final `outputPath` to user

---

## File Paths

The file system layout during recording:

```
App Cache/Recordings/
├── session_12345/
│   ├── pre_recorded.mp4       ← Pre-recording output (deleted after mux)
│   ├── recording.mp4          ← Live recording output (deleted after mux)
│   └── final_output.mp4       ← Final muxed output (returned to user)
```

**Important**: 
- Pre-recorded file is created **before** the user presses record
- Both files must be complete and closed before muxing starts
- Temporary files (pre_recorded, recording) are automatically deleted after successful mux

---

## Error Handling

### Pre-Recording Disabled
If `IsPreRecordingMode = false` during recording start:
- Only live recording encoder is used
- `pre_recorded.mp4` is never created
- Muxing still works (just processes one file)

### File Not Found
If either input file doesn't exist at mux time:
- Platform muxer will throw an exception
- Exception is caught and rethrown to caller
- User sees error: "Failed to mux videos"

### Disk Space
If disk runs out during recording:
- Encoder will fail to write frames
- User should check available space before recording

### Concurrent Muxing
Only ONE mux operation can happen at a time per SkiaCamera instance:
- Subsequent `StopAsync()` calls will queue
- Previous mux must complete before next recording can start

---

## Performance Requirements

**Original design goal**: "Very fast code, fastest fps, no additional mem allocations, no reflection, no shit"

### What We Do
- ✅ H.264 encoding streams to disk (no memory buffering)
- ✅ Each platform uses native APIs (no reflection)
- ✅ Reuse CVPixelBuffer/MediaCodec buffers (no allocations)
- ✅ Muxing delegates to platform (AVAssetComposition/MediaMuxer/FFmpeg)

### What We Don't Do
- ❌ No buffering of raw pixel data
- ❌ No reflection for platform detection
- ❌ No unnecessary allocations (buffer pooling)
- ❌ No slow managed code in hot paths

---

## Testing Checklist

When implementing or fixing pre-recording, verify:

- [ ] Pre-recorded file is created with correct data
- [ ] Live recording file is created with correct data
- [ ] Muxed output contains BOTH pre-recorded + live content (in order)
- [ ] Pre-recorded frames appear FIRST in the output
- [ ] Live frames appear SECOND in the output
- [ ] Temporary files are cleaned up after mux
- [ ] No memory leaks (check peak memory usage)
- [ ] Muxing works on all platforms (iOS, Android, Windows)
- [ ] Error cases are handled gracefully
- [ ] No OOM crashes (original failure mode)

---

## Troubleshooting

### "MuxVideosInternal not implemented"
- Cause: Running on a platform where muxing wasn't implemented
- Fix: Add `MuxVideosInternal()` method to the platform-specific file (e.g., `SkiaCamera.Android.cs`)

### "File not found" during mux
- Cause: Pre-recorded or recording file was deleted before mux started
- Fix: Ensure encoders finish and close files before calling mux

### "PlatformNotSupportedException"
- Cause: Code is calling muxing on wrong platform
- Fix: Check the shared `SkiaCamera.cs` dispatcher—it should route to platform code, not handle muxing itself

### Duplicate definition errors (CS0111)
- Cause: `MuxVideosInternal()` defined in multiple files
- Fix: Keep only ONE `MuxVideosInternal()` per platform file. Delete any in root directory.

---

## Code Review Checklist

Before approving changes to pre-recording:

1. **Shared file** (`SkiaCamera.cs`): Contains ONLY dispatcher, throws `PlatformNotSupportedException`
2. **Platform files** (`SkiaCamera.*.cs`): Contains implementation using native APIs
3. **No duplicates**: Only ONE `MuxVideosInternal()` per platform
4. **Memory**: No 46MB buffers, files stream to disk
5. **Error handling**: All exceptions are caught and logged
6. **File cleanup**: Temporary files are deleted after mux
7. **Performance**: Uses hardware encoders (H.264 GPU) where available

---

## References

- **iOS**: [AVAssetComposition](https://developer.apple.com/documentation/avfoundation/avassetcomposition)
- **Android**: [MediaMuxer](https://developer.android.com/reference/android/media/MediaMuxer)
- **Windows**: [FFmpeg Documentation](https://ffmpeg.org/documentation.html)
