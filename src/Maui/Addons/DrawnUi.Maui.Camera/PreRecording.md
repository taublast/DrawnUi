# Pre-Recording Feature

**IMPORTANT:** This feature is currently being redesigned. The documentation below describes the intended architecture. See "Current Implementation Status" section for actual status.

The pre-recording feature, also known as "look-back" recording, allows you to capture video footage starting from a few seconds *before* the record button is pressed. This is incredibly useful for capturing spontaneous moments without missing the beginning of the action.

## Current Implementation Status (2025-12-06)

✅ **Windows Implementation Complete:**
- Circular buffer architecture implemented using file-based buffers (`bufferA.mp4`, `bufferB.mp4`)
- Seamless muxing of pre-recorded buffers with live recording
- Trimming logic ensures exact duration compliance
- Timestamp normalization fixes applied

✅ **Android Implementation Complete:**
- Single-file approach (no muxing needed)
- Zero frame loss transition from pre-recording to live recording

✅ **iOS Foundation Complete:**
- AppleVideoToolboxEncoder integrated for hardware H.264 encoding
- Normal recording works with frame processing and preview
- MP4 output functional

**Recent Updates:**
- Added safety mechanism: Changing `EnablePreRecording` or `PreRecordDuration` while recording is active will automatically abort the recording to prevent instability.

---

## How it Works (Architecture)

When the pre-recording feature is enabled, the camera maintains a **separate pre-recording encoder** that continuously captures frames to a temporary `pre_recorded.mp4` file. When you press the record button, a **live recording encoder** starts capturing to a `recording.mp4` file. 

When you stop recording:
1. Both encoders finish writing their respective files
2. The files are automatically **muxed** (merged) into a single output file with pre-recorded content first, then live content
3. Temporary files are cleaned up
4. The final combined video is returned to the user

### Key Design Principle: **Zero Memory Buffering**

Unlike previous approaches that buffered raw pixel data in memory (causing 46MB OOM crashes), the current implementation:
- Streams all video directly to disk files (H.264 encoded)
- Uses only 1-2MB working buffer in memory for encoder operations
- Pre-recorded file typically 2-5MB for 5 seconds at 30fps
- Completely eliminates the OOM risk

## How to Use

You can control the feature using two simple properties on the `SkiaCamera` control:

- **`EnablePreRecording` (bool):** Set this to `true` to activate the pre-recording functionality. The default value is `false`.
- **`PreRecordDuration` (TimeSpan):** This property determines the duration of the look-back period. The default is 5 seconds.
-  **`IsPreRecording` (bool):** This will be set by camera to show if actually recording the pre-recording step. If `true` the next time we invoke `StartVideoRecording` this property will go `false` and you will be recording the live step.

### Example in XAML
```xml
<camera:SkiaCamera
    EnablePreRecording="True"
    PreRecordDuration="0:0:5" />
```

### Example in C#
```csharp
var myCamera = new SkiaCamera
{
    EnablePreRecording = true,
    PreRecordDuration = TimeSpan.FromSeconds(5)
};
```

When you call `myCamera.StartVideoRecording()`, the resulting video file will automatically include the footage from the specified duration before the call was made. The footage is captured in real-time to disk without any memory buffering.

## Implementation Details & Current Status

The implementation uses a **two-file streaming + muxing** architecture that is platform-specific:

### Architecture Overview

```
Pre-Recording Running (EnablePreRecording=true)
    ↓
User clicks "Start Recording"
    ↓
├─→ Pre-Recording Encoder: Creates "pre_recorded.mp4" (background, keeps running)
│
└─→ Live Recording Encoder: Creates "recording.mp4" (user-initiated recording)
    
User clicks "Stop Recording"
    ↓
Both encoders finish and close files
    ↓
Muxing Phase (Platform-Specific)
    ↓
Combine: "pre_recorded.mp4" + "recording.mp4" → "final_output.mp4"
    ↓
Cleanup: Delete temporary files
    ↓
Return final video to user
```

### Platform Implementation Status

- **iOS / MacCatalyst:**
    - **Status:** ⏳ **In Development** (encoder complete, pre-recording buffer not yet implemented)
    - **Encoder:** AppleVideoToolboxEncoder (hardware H.264 via VTCompressionSession)
    - **Planned Method:** Circular buffer in memory + AVAssetComposition for two-file muxing
    - **Current:** Normal recording operational, pre-recording feature pending
    - **Location:** `Apple/SkiaCamera.Apple.cs` - `MuxVideosInternal()` method
    - **Memory:** Pre-recording and live encoders each use ~1-2MB working buffer (reusable pixel buffers)

- **Android:**
    - **Status:** ✅ **Implemented (muxing complete, API fixes applied).**
    - **Method:** Uses `MediaExtractor` to read both video files and `MediaMuxer` to write them sequentially to the output file. Tracks are extracted from both files without re-decoding (stays as H.264), then muxed together with proper time offsets.
    - **Location:** `Platforms/Android/SkiaCamera.Android.cs` - `MuxVideosInternal()` method
    - **Memory:** Similar to iOS - each encoder maintains its own working buffer

- **Windows:**
    - **Status:** ✅ **Implemented (file concatenation fallback).**
    - **Method:** Uses FFmpeg command-line tool to concatenate the two video files efficiently with `-c copy` flag (no re-encoding, just stream copying).
    - **Location:** `Platforms/Windows/SkiaCamera.Windows.cs` - `MuxVideosInternal()` method
    - **Note:** Requires FFmpeg to be installed or bundled with the application
    - **Memory:** Minimal - uses ffmpeg subprocess

## Performance Characteristics

- **Frame Rate:** Maintains target FPS without additional overhead
- **Memory:** Constant ~2MB per encoder (pre-recording + live), no frame accumulation
- **CPU:** H.264 encoding delegated to hardware accelerators where available
- **Disk I/O:** Real-time streaming, no buffering phase
- **File I/O:** Pre-recorded and live files finalized immediately; muxing is only metadata reorganization (fast)

## When Pre-Recording is Disabled

When `EnablePreRecording` is `false` (the default setting), the camera uses standard direct-to-file recording on all platforms:
- Only one encoder runs (live recording only)
- No muxing phase occurs
- Identical behavior to non-pre-recording cameras
- Minimal memory overhead

## See Also

For detailed technical documentation, see `Prerecording.md` in this directory.
