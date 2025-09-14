# Capture Video Flow Implementation Plan

## Overview

The "Capture Video Flow" is a new video recording system that captures individual camera frames, processes them in real-time through user-defined callbacks, and encodes them into a video file. This is different from the existing "Record Video Flow" which uses native video recording APIs directly.

## Use Cases

- Real-time watermark overlay on video
- Apply filters/effects during recording
- Custom frame processing (drawing, annotations, etc.)
- Multi-layer video composition
- Performance monitoring overlays

## Architecture

### High-Level Flow

```
Camera Frame -> Frame Capture -> User Processing -> Video Encoder -> Output File
     ↓              ↓                   ↓               ↓              ↓
  Native API    SKImage/SKBitmap    Action<SKCanvas>   FFmpeg/Native   MP4 File
```

### Core Components

1. **Frame Capture System**: Extracts individual frames from camera stream
2. **Processing Pipeline**: User callback to modify frames
3. **Video Encoder**: Encodes processed frames into video file
4. **Synchronization**: Ensures proper timing and frame rate
5. **Audio Integration**: Optional audio recording alongside frame capture

## API Design

### New Properties

```csharp
// Enable capture video mode (static bindable property)
public static readonly BindableProperty UseCaptureVideoFlowProperty = BindableProperty.Create(
    nameof(UseCaptureVideoFlow),
    typeof(bool),
    typeof(SkiaCamera),
    false);

public bool UseCaptureVideoFlow
{
    get { return (bool)GetValue(UseCaptureVideoFlowProperty); }
    set { SetValue(UseCaptureVideoFlowProperty, value); }
}

// Frame processing callback
public Action<SKCanvas, SKImageInfo, TimeSpan> FrameProcessor { get; set; }
```

### Existing Methods (Reused)

```csharp
// Use existing video recording methods
await camera.StartVideoRecording();  // Automatically detects UseCaptureVideoFlow
await camera.StopVideoRecording();   // Works for both flows
```

### Existing Events (Reused)

```csharp
// Use existing video recording events
camera.VideoRecordingSuccess += OnVideoRecordingSuccess;
camera.VideoRecordingFailed += OnVideoRecordingFailed;
camera.VideoRecordingProgress += OnVideoRecordingProgress;
```

### Existing Properties (Reused)

```csharp
// Reuse existing video format properties
camera.VideoQuality = VideoQuality.High;
camera.VideoFormatIndex = 0;
camera.RecordAudio = true;
```


### Proposed GPU-friendly encoder API (for approval)

To avoid CPU readbacks, add a GPU-path overload (keep existing bitmap API for debug/fallback only):

```csharp
// Proposed addition to ICaptureVideoEncoder
Task AddFrameAsync(SKImage gpuImage, TimeSpan timestamp);
```

Encoders should prefer the SKImage-based path and receive GPU-backed images/textures where possible.


Note: On Windows we will implement an encoder-owned render-target pattern first (BeginFrame/SubmitFrame) so Skia draws directly into the encoder surface. The existing ICaptureVideoEncoder API remains unchanged; the Windows encoder provides this GPU path as an optional, platform-specific extension used by SkiaCamera under WINDOWS.

### Implementation Logic

```csharp
// Inside StartVideoRecording()
if (UseCaptureVideoFlow && FrameProcessor != null)
{
    // Use frame capture + custom encoding
    await StartCaptureVideoFlow();
}
else
{
    // Use existing native video recording
    await StartNativeVideoRecording();
}
```

## Implementation Strategy

### Phase 1: Core Infrastructure

1. **SkiaCamera API Extensions**
   - Properties `UseCaptureVideoFlow` and `FrameProcessor` are already present and wired in `StartVideoRecording()`.
   - Maintain a single code path that auto-selects capture flow when enabled and callback is set.
   - Guard against conflicts with native record flow (mutual exclusion, state flags).

2. **Frame Processing Pipeline**
   - Use `NativeControl.GetPreviewImage()` to pull the most recent frame (SKImage) from the camera.
   - Convert once per frame to a reusable `SKBitmap` for CPU-side encoding; reuse `SKCanvas`/`SKBitmap` buffers to minimize GC.
   - Throttle via timer to target the desired FPS and drop frames if encoder falls behind.

3. **Video Encoder Interface**
   - `ICaptureVideoEncoder` exists (Initialize, Start, AddFrame, Stop, Progress) and is used by `SkiaCamera`.
   - Platform encoders implement this interface (Windows present; Android/Apple pending).

### Phase 2: Platform Implementation

#### Windows (First)
Windows encoder path (GPU-prioritized):
- Plan A (preferred): Media Foundation H.264/HEVC hardware encoder (MFT/SinkWriter)
  - Use IMFDXGIDeviceManager with a D3D11 device; submit ID3D11Texture2D frames (NV12/BGRA as supported).
  - Render overlays on a GPU-backed SKSurface via existing GRContext (no CPU readback) and share to encoder via DXGI.
  - Provide accurate timestamps; implement frame pacing and frame-drop when under load.
- Plan B (optional, if approved): External FFmpeg
  - Use hardware acceleration when available; keep rendering on GPU and avoid readbacks.
  - Only when Plan A is unavailable; respect packaging/Path constraints.
- Plan C (dev-only fallback): Uncompressed AVI writer
  - Debug-only; large files; no audio. Not for production.

Interop: Microsoft.Windows.CsWin32 (Win32 source generators)
- We generate strongly-typed P/Invoke for Media Foundation and DXGI using CsWin32.
- Configuration:
  - File: src/Maui/Addons/DrawnUi.Maui.Camera/NativeMethods.txt
  - Contains the list of Win32 APIs/consts CsWin32 should project, e.g.:
    - MFStartup, MFShutdown, MFCreateSinkWriterFromURL, MFCreateMediaType, MFCreateSample, MFCreateMemoryBuffer
    - MFCreateDXGIDeviceManager, MFCreateDXGISurfaceBuffer
    - MF_VERSION and MF_MT_* keys, MFMediaType_Video, MFVideoFormat_* (RGB32/H264)
- Why:
  - Avoids manual COM interop and GUID plumbing
  - Keeps WinUI/MAUI-friendly interop without adding TerraFX or SharpDX
- Notes:
  - If new MF/DXGI APIs are needed, append their names to NativeMethods.txt and rebuild.
  - Keep the list minimal to reduce compile time.

Windows capture specifics:
- Frame source: `NativeControl.GetPreviewImage()` (SKImage) → convert to a single reused `SKBitmap` buffer.
- Composition: draw camera frame first, then `FrameProcessor(canvas, info, timestamp)` overlay.
- Timing: system timer targeting FPS; compute timestamp = now - start; drop when busy.
- Memory: keep at most one working frame; avoid frame queues; no UI-thread blocking.
- Audio (phase 2.2):
  - A: With MediaStreamSource, add audio track from microphone in real-time.
  - B: Or record audio in parallel (WASAPI/MediaCapture) and mux later (FFmpeg) if Plan B is used.

#### Android (Second)
- GPU path: Camera2 provides a Surface/SurfaceTexture; use MediaCodec with Surface input (hardware H.264/HEVC)
- Render watermark/overlays on GPU via SkiaSharp (OpenGL ES/Vulkan) into the codec input Surface or a shared EGL image
- No CPU bitmaps; accurate timestamps; frame pacing and drop policy under load

#### iOS/macOS (Third)
- GPU path: Use AVCaptureVideoDataOutput for frames, render overlays via Metal-backed SKSurface into an IOSurface-backed CVPixelBuffer using CVMetalTextureCache (zero/low-copy)
- Encode via AVAssetWriter (hardware H.264/HEVC) with AVAssetWriterInputPixelBufferAdaptor; avoid CPU conversions
- Follow Apple memory patterns; reuse buffers; ensure no GC spikes

### Phase 3: Integration & Polish
### GPU-first Requirements (hard requirement)
- All platforms must use GPU for frame composition and feed hardware encoders without CPU readbacks when possible.
- Compose overlays on a GPU-backed SKSurface (reuse GRContext from DrawnUi) and hand frames to platform encoders via GPU-native surfaces:
  - Windows: D3D11 textures via Media Foundation (IMFDXGIDeviceManager, ID3D11Texture2D) to H.264/HEVC hardware encoder MFT/SinkWriter.
  - Android: MediaCodec with Surface input; render via SkiaSharp GPU (OpenGL ES/Vulkan) into the codec's input surface or an EGL image shared with it.
  - iOS/macOS: AVAssetWriter with hardware encoder; render via Metal-backed SKSurface to a CVPixelBuffer backed by IOSurface using CVMetalTextureCache (GPU path). Avoid CPU CVPixelBuffer copies.
- CPU fallbacks are for debug only and must be disabled in production builds.

## Windows learnings applied (preview + recording) — optimizations summary

- One-frame-in-flight policy for preview and capture
  - Prevents backlog and UI stutter by dropping frames when processing is busy
- Event-driven preview on Windows
  - Preview updates are driven by MediaFrameReader events; avoids timer jitter
- Preview-from-Recording latch
  - While recording, preview shows only composed frames (no fallback to raw feed), eliminating occasional overlay “blinks”
- Allocation-free/reuse patterns
  - Reuse a single GPU surface for composition during recording
  - Reuse a single CPU readback bitmap (Windows) for the temporary bridge to MF encoder
  - Reuse a single InMemoryRandomAccessStream and byte[] in preview’s rare managed BMP fallback
- Accurate timestamps
  - Use real elapsed time since Start to timestamp samples; fixes duration mismatch
- Minimal memory footprint
  - Keep at most 1 composed frame in memory; renderer takes ownership and disposes
- Error/edge handling
  - Defensive checks for missing preview image; no cross-thread UI calls

These patterns are platform-agnostic and inform Android/iOS implementations.

## Android implementation plan (GPU-first, Surface input)

- Encoder: MediaCodec (H.264/HEVC) with Surface input + MediaMuxer for MP4
- Rendering path: create an EGL context bound to the codec’s input Surface
  - Make SkiaSharp draw directly into the EGLSurface by creating a GRContext (GL) and an SKSurface over the current FBO
  - No CPU readbacks; overlays drawn via existing FrameProcessor on the SKCanvas
- Timing:
  - Drive frames from a timer targeting the selected FPS (event-driven preview differs per device)
  - Use elapsed time as presentation timestamps via EGLExt.eglPresentationTimeANDROID
- Draining:
  - Drain MediaCodec output after each submitted frame and write to MediaMuxer once the output format is announced
- Memory:
  - No frame queues; at most one frame composed at a time
- Preview:
  - Initial step focuses on encoding; preview remains driven by the existing camera pipeline
  - A “preview-from-recording” mirror for Android can be added after basic GPU path is verified

Status: Implemented. MediaCodec (Surface) + EGL + Skia GL; mirror-to-preview enabled; warmup drop for first camera frame; sensor-based PTS; single-frame policy enforced.


#### iOS/macOS (Third) — Implementation plan (GPU-first, AVAssetWriter)

- Encoder: AVAssetWriter (H.264/HEVC) + AVAssetWriterInputPixelBufferAdaptor
- Rendering: Skia Metal-backed SKSurface; reuse a single surface and enforce one-frame-in-flight
- Buffering: Reuse CVPixelBuffer from PixelBufferPool (BGRA premul); lock/unlock; no per-frame allocations
- Timestamps: Use sensor-based PTS (CapturedImage.Time baseline) → CMTime with 1,000,000 timescale
- Preview: Publish composed frames via PreviewAvailable + TryAcquirePreviewImage; mirror-to-preview during recording
- Memory: Single-frame policy; renderer takes ownership of SKImage returned by TryAcquirePreviewImage
- Audio: Defer for iteration 2; plan AVAssetWriterInput (Audio) with A/V sync by timestamp
- Fallback: If Metal GRContext unavailable, fall back to CPU SKSurface with buffer reuse only for dev/testing



1. **Audio Integration**
   - Implement per-platform audio capture and A/V sync (timestamp alignment).
   - Provide post-processing mux fallback when live mux isn’t available.

2. **Performance Optimization**
   - Frame dropping under load; reuse `SKBitmap`/`SKCanvas`; avoid allocations per frame.
   - Prefer GPU-backed preview; only perform CPU readback for encode path.

3. **Error Handling & Recovery**
   - Robust encoder lifecycle; safe cleanup on exceptions.
   - Graceful fallbacks (Plan B/C) with clear telemetry and user feedback.

## Technical Considerations

### Frame Synchronization
- Use high-precision timers for frame capture
- Implement frame dropping when processing can't keep up
- Maintain consistent frame rate output

### Memory Management
- Reuse SKBitmap/SKCanvas objects to minimize GC
- Implement frame buffer pools
- Monitor memory usage during long recordings

### Performance
- Background thread processing for frame manipulation
- GPU acceleration where possible (SkiaSharp GPU backend)
- Configurable quality vs performance trade-offs

### Thread Safety
- Camera frame callbacks on camera thread
- User processing on background thread
- Encoder operations on dedicated encoder thread

## File Structure (current vs target)

Current in repo:
```
DrawnUi.Maui.Camera/
├── ICaptureVideoEncoder.cs               # Cross-platform encoder interface (root)
├── SkiaCamera.cs                         # Contains capture flow wiring & timer
└── Platforms/
    └── Windows/
        └── WindowsCaptureVideoEncoder.cs # Windows encoder (scaffold present)
```

Target (optional future refactor):
```
DrawnUi.Maui.Camera/
├── CaptureVideo/
│   ├── ICaptureVideoEncoder.cs           # (move here in a refactor)
│   ├── CaptureVideoManager.cs            # Core capture video logic (optional)
│   ├── Models/
│   │   ├── CaptureVideoFormat.cs         # Optional: video format specs
│   │   ├── CaptureVideoProgress.cs       # Optional: progress
│   │   └── VideoCodec.cs                 # Optional: codec enumeration
│   └── Platforms/
│       ├── Android/AndroidCaptureVideoEncoder.cs
│       ├── Windows/WindowsCaptureVideoEncoder.cs
│       └── Apple/AppleCaptureVideoEncoder.cs
└── Extensions/SKCanvasExtensions.cs      # Optional helpers for overlays
```

## Usage Example

```csharp
var camera = new SkiaCamera
{
    // Enable capture video flow
    UseCaptureVideoFlow = true,

    // Use existing properties for format/audio
    VideoQuality = VideoQuality.High,
    RecordAudio = true,

    // Real-time frame processing
    FrameProcessor = (canvas, info, timestamp) =>
    {
        // Draw watermark
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(128),
            TextSize = 48,
            IsAntialias = true
        };

        canvas.DrawText("LIVE", 50, 100, paint);
        canvas.DrawText($"{timestamp:mm\\:ss}", 50, 160, paint);
    }
};

// Use existing video recording methods
await camera.StartVideoRecording();  // Automatically uses capture flow

// Stop after 10 seconds
await Task.Delay(10000);
await camera.StopVideoRecording();   // Same method for both flows
```
## Open Questions (please confirm)

1. Windows encoder path: Are we allowed to rely on an external FFmpeg (Plan B) as an optional fallback? If yes, can we assume it’s available on PATH, or should we bundle it?
2. Output format/codec preference for Windows: MP4/H.264 baseline OK, or H.265/HEVC preferred when available?
3. Is audio required in the first Windows iteration, or can we ship video-only first and add audio (A/V sync) next?
4. Target FPS/resolution caps for capture flow (e.g., 30 fps at 720p/1080p) to guide throttling and buffer sizes?
5. Packaging constraints (MSIX/Store) that would prohibit shipping an ffmpeg binary or launching external processes?


## Compatibility

- **Minimum Requirements**:
  - Android API 21+ (Camera2 API)
  - iOS 11+ (AVAssetWriter improvements)
  - Windows 10+ (MediaFoundation)

- **Conflicts**:
  - UseCaptureVideoFlow=true switches to frame-by-frame recording
  - UseCaptureVideoFlow=false (default) uses native video recording
  - Same API for both flows - no breaking changes

## Testing Strategy

1. **Unit Tests**: Frame processing pipeline, encoder interface
2. **Integration Tests**: End-to-end capture video recording
3. **Performance Tests**: Long recording sessions, memory usage
4. **Platform Tests**: Each platform-specific encoder implementation

## Status

- [/] Phase 1: Core Infrastructure (SkiaCamera wiring + ICaptureVideoEncoder present)
- [/] Phase 2: Windows Implementation (Media Foundation path in progress)
- [x] Phase 2: Android Implementation
- [/] Phase 2: iOS/macOS Implementation
- [ ] Phase 3: Integration & Polish
- [ ] Testing & Documentation

---

**Last Updated**: 2025-09-14
**Next Steps**:
- Confirm Open Questions (Windows Plan A vs B, codec, audio scope, packaging).
- Wire Apple encoder into SkiaCamera (begin/submit/mirror) and validate on iPhone/iPad test app.

- Implement Windows Plan A (MediaStreamSource + MediaTranscoder) or Plan B (FFmpeg) accordingly; keep AVI as dev-only fallback.
- Add buffer reuse + throttling in capture loop; validate timing and stability.
- Add Windows audio track support and A/V sync (if in scope for first iteration).