# Pre-Recording Feature

The pre-recording feature, also known as "look-back" recording, allows you to capture video footage starting from a few seconds *before* the record button is pressed. 
This is incredibly useful for capturing spontaneous moments without missing the beginning of the action.

This can is incredibly useful for capturing spontaneous moments without missing the beginning of the action. Imagine you want to trigger recording a video on some conditions. Like a rabbit appeared within camera range and live recording is triggered by movement/AI detection, you would have 5 seconds preceding that triggered event in the final video result, making it look natural.

## How it Works (Architecture)

### iOS/MacCatalyst: Memory-Based Circular Buffer

When pre-recording is enabled, the camera maintains a **circular buffer of encoded H.264 frames** in memory:

1. **Pre-Recording Phase:** Frames are hardware-encoded and stored in memory inside a circular buffer to always hold the last N seconds of footage (configurable via `PreRecordDuration`)
2. **Live Recording Phase:** User presses record → live frames start recording, last N seconds from the circular buffer will be muxxed with live footage.

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
