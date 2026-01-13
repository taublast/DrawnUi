# Pre-Recording Feature

The pre-recording feature, also known as "look-back" recording, allows you to capture video footage starting from a few seconds *before* the record button is pressed. This is incredibly useful for capturing spontaneous moments without missing the beginning of the action.

## Current Implementation Status (2025-01)

✅ **Windows Implementation Complete:**
- Circular buffer architecture implemented using file-based buffers (`bufferA.mp4`, `bufferB.mp4`)
- Seamless muxing of pre-recorded buffers with live recording
- Trimming logic ensures exact duration compliance
- Timestamp normalization fixes applied

✅ **Android Implementation Complete:**
- Single-file approach (no muxing needed)
- Zero frame loss transition from pre-recording to live recording

✅ **iOS/MacCatalyst Implementation Complete:**
- AppleVideoToolboxEncoder with hardware H.264 encoding via VTCompressionSession
- In-memory circular buffer (`PrerecordingEncodedBufferApple`) for encoded frames
- AVAssetWriter-based muxing for seamless video output
- Audio recording with synchronized circular buffer
- Passthrough muxing (no re-encoding) for optimal performance
- Proper resource disposal to prevent memory leaks

**Recent Updates:**
- Added safety mechanism: Changing `EnablePreRecording` or `PreRecordDuration` while recording is active will automatically abort the recording to prevent instability.
- Fixed memory leaks in AVFoundation resource disposal (AVAsset, AVMutableComposition, AVAssetExportSession)
- Fixed PrerecordingEncodedBufferApple frame Data cleanup in Dispose() and pruning operations
- Added event handler unsubscription for PreviewAvailable to prevent memory leaks
- Changed AddAudioToVideoAsync to use Passthrough preset (no re-encoding, faster muxing)

---

## How it Works (Architecture)

### iOS/MacCatalyst: Memory-Based Circular Buffer

When pre-recording is enabled, the camera maintains a **circular buffer of encoded H.264 frames** in memory:

1. **Pre-Recording Phase:** Frames are hardware-encoded and stored in `PrerecordingEncodedBufferApple`
2. **Live Recording Phase:** User presses record → live frames write to `recording.mp4` while circular buffer is frozen
3. **Muxing Phase:** Circular buffer frames + live recording → combined output via AVAssetWriter
4. **Audio Sync:** CircularAudioBuffer maintains synchronized audio data

### Windows/Android: File-Based Approach

When pre-recording is enabled, the camera maintains a **separate pre-recording encoder** that continuously captures frames to temporary files. When recording stops, files are muxed together.

### Key Design Principles

**iOS/MacCatalyst - Memory Buffering:**
- Stores only H.264 encoded frames (not raw pixels)
- ~20-40KB per frame at 1080p (vs ~8MB raw)
- 5 seconds at 30fps ≈ 3-6MB total
- Instant muxing via AVAssetWriter (no file I/O during buffer phase)

**Windows - Zero Memory Buffering:**
- Streams all video directly to disk files (H.264 encoded)
- Uses only 1-2MB working buffer in memory for encoder operations
- Pre-recorded file typically 2-5MB for 5 seconds at 30fps

## How to Use

You can control the feature using these properties on the `SkiaCamera` control:

- **`EnablePreRecording` (bool):** Set to `true` to activate pre-recording. Default is `false`.
- **`PreRecordDuration` (TimeSpan):** Duration of the look-back period. Default is 5 seconds.
- **`IsPreRecording` (bool):** Read-only. Shows if currently in pre-recording phase. If `true`, the next `StartVideoRecording` call will transition to live recording.

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

When you call `myCamera.StartVideoRecording()`, the resulting video file will automatically include the footage from the specified duration before the call was made.

## Implementation Details

### Architecture Overview

```
Pre-Recording Running (EnablePreRecording=true)
    ↓
User clicks "Start Recording"
    ↓
├─→ iOS: Circular buffer frozen, live encoder starts
│   └─→ Audio: CircularAudioBuffer continues with timestamp offset
│
└─→ Windows/Android: Pre-recording encoder + Live encoder running

User clicks "Stop Recording"
    ↓
Finalization Phase
    ↓
├─→ iOS: Write circular buffer → AVAssetWriter → append live → mux audio
│
└─→ Windows/Android: Close encoders → mux files together
    ↓
Cleanup: Delete temporary files, dispose resources
    ↓
Return final video to user
```

### Platform Implementation Details

#### iOS / MacCatalyst

- **Status:** ✅ **Complete**
- **Encoder:** AppleVideoToolboxEncoder (hardware H.264 via VTCompressionSession)
- **Buffer:** PrerecordingEncodedBufferApple (in-memory circular buffer of encoded frames)
- **Audio:** CircularAudioBuffer with synchronized timestamps
- **Muxing:** AVAssetWriter for video, AVAssetExportSession (Passthrough) for audio
- **Location:** `Apple/SkiaCamera.Apple.cs`, `Apple/AppleVideoToolboxEncoder.cs`
- **Memory:** ~3-6MB for 5 seconds of 1080p H.264 frames + ~1MB audio

**Resource Management:**
- All AVFoundation objects properly disposed via `using` statements
- Frame Data explicitly nulled before removal from collections
- Event handlers unsubscribed before encoder disposal
- Metal resources (command queue, texture cache) shared statically

#### Android

- **Status:** ✅ **Complete**
- **Method:** MediaExtractor + MediaMuxer for stream copying (no re-decoding)
- **Location:** `Platforms/Android/SkiaCamera.Android.cs` - `MuxVideosInternal()`
- **Memory:** ~1-2MB working buffer per encoder

#### Windows

- **Status:** ✅ **Complete**
- **Method:** FFmpeg `-c copy` for efficient stream concatenation
- **Location:** `Platforms/Windows/SkiaCamera.Windows.cs` - `MuxVideosInternal()`
- **Note:** Requires FFmpeg installed or bundled
- **Memory:** Minimal - uses ffmpeg subprocess

## Performance Characteristics

- **Frame Rate:** Maintains target FPS without additional overhead
- **Memory:**
  - iOS: ~5-7MB (circular buffer + audio buffer)
  - Windows/Android: ~2-4MB (encoder working buffers)
- **CPU:** H.264 encoding delegated to hardware accelerators
- **Muxing:**
  - iOS: AVAssetExportSession with Passthrough (no re-encoding)
  - Windows: FFmpeg stream copy
  - Android: MediaMuxer stream copy

## When Pre-Recording is Disabled

When `EnablePreRecording` is `false` (default):
- Only one encoder runs (live recording only)
- No muxing phase occurs
- No circular buffers allocated
- Minimal memory overhead
- Identical behavior to non-pre-recording cameras

## Audio Recording

Audio is recorded alongside video on iOS/MacCatalyst:

- **CircularAudioBuffer:** Maintains synchronized audio samples
- **Timestamp Synchronization:** Audio buffer tracks relative timestamps for proper alignment
- **Muxing:** AddAudioToVideoAsync combines video and audio using AVAssetExportSession with Passthrough preset
- **Format:** AAC audio in M4A container, combined into final MP4

## See Also

For detailed technical documentation including encoding parameters and buffer management, see `PreRecordingTech.md` in this directory.
