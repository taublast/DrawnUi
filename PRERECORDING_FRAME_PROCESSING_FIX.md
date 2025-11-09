# Pre-Recording Frame Processing Fix

## Problem

During pre-recording, frames were **not being sent to FrameProcessor** because of a condition check in `CaptureFrame()`.

### Root Cause

In `SkiaCamera.cs` line 1018:

```csharp
private async void CaptureFrame(object state)
{
    if (!IsRecordingVideo || _captureVideoEncoder == null)
        return;  // ❌ WRONG - returns during pre-recording!
    // Frame processing skipped during IsPreRecording phase
}
```

**The Logic Flow:**
- Pre-Recording Phase: `IsRecordingVideo=false`, `IsPreRecording=true`
- `!IsRecordingVideo` is `true` → early return
- **FrameProcessor callback never invoked!**
- **No frames sent to encoder!**

The log showed:
```
[NativeCameraiOS] Frame stats - Processed: 0, Skipped: 0
```

(No frames being processed because FrameProcessor wasn't being called)

## Solution

Change the condition to include `IsPreRecording`:

```csharp
private async void CaptureFrame(object state)
{
    if (!(IsRecordingVideo || IsPreRecording) || _captureVideoEncoder == null)
        return;  // ✅ CORRECT - processes frames during both phases!
}
```

**The Fixed Logic Flow:**
- Pre-Recording Phase: `IsRecordingVideo=false`, `IsPreRecording=true`
- `!(false || true)` = `!true` = `false` → continue processing
- **FrameProcessor callback invoked**
- **Frames processed through Skia**
- **Frames encoded and buffered**

## Impact

Now during pre-recording:
1. Frames are captured from camera
2. FrameProcessor callback is invoked ✅
3. User's Skia drawing code executes ✅
4. Frames go to hardware encoder ✅
5. Encoded frames buffered in memory ✅
6. When transitioning to file recording, all pre-recorded frames are available ✅

## Testing

The fix enables:
- Pre-recording to actually capture and process frames
- Frame statistics to show non-zero processed frame counts
- Full pre-recorded video content when transitioning to file recording
- Smooth video playback with buffered + live frames

## Related Code Paths

The frame processing happens in several platform-specific sections within `CaptureFrame()`:

```csharp
#if WINDOWS
    // Windows GPU-first path
    using (winEnc.BeginFrame(...))
    {
        // Draw camera frame
        // Apply overlay via FrameProcessor
        FrameProcessor?.Invoke(frame);
        // Submit frame to encoder
    }

#elif ANDROID
    // Android GPU path via MediaCodec
    // Similar flow through EGLSurface

#elif IOS
    // Apple GPU path via AVAssetWriter
    // Similar flow through GPU surface
```

All paths now correctly process frames during both `IsRecordingVideo` AND `IsPreRecording` phases.
