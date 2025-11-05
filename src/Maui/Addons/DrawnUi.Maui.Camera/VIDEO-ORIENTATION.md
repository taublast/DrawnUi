# Video Orientation Handling Across Platforms

This document describes how video orientation is handled during recording on different platforms in the DrawnUI Camera component.

## Overview

Video orientation handling differs significantly across platforms due to different native APIs and behaviors. Understanding these differences is crucial for ensuring videos play correctly in gallery apps and video players.

## Key Concepts

### Camera Native Format
- **Android**: Most cameras provide video in **landscape format** (e.g., 1920×1080) regardless of device orientation
- **iOS**: Cameras typically provide video in **portrait format** (e.g., 1080×1920) and require rotation
- **Windows**: MediaCapture API handles orientation automatically

### Orientation Metadata
Video files contain rotation metadata that tells video players how to display the video. This metadata doesn't change the actual pixel data - it just tells the player to rotate during playback.

### Two Recording Paths

Both Android and iOS support two video recording implementations:

1. **Native Recording** (`UseCaptureVideoFlow = false`)
   - Uses platform's built-in video recording APIs
   - Simpler, more efficient
   - Requires manual orientation handling

2. **Custom Capture Flow** (`UseCaptureVideoFlow = true`)
   - Uses custom encoder with SkiaSharp rendering
   - Allows frame processing and custom drawing
   - Different orientation handling approach

---

## Android

### Native Recording (`UseCaptureVideoFlow = false`)

**API Used**: `MediaRecorder`

**Implementation**: `NativeCamera.Android.cs` → `SetupMediaRecorder()`

**Camera Behavior**:
- Camera provides video in **landscape format** (1280×720) regardless of device orientation
- Video dimensions do NOT rotate with device

**Orientation Handling**:
```csharp
// Get device rotation when recording starts
var deviceRotation = FormsControl.RecordingLockedRotation;

// Determine if rotation compensation is needed
bool deviceIsLandscape = (deviceRotation == 90 || deviceRotation == 270);
bool videoIsLandscape = (profile.VideoFrameWidth > profile.VideoFrameHeight);

int orientationHint;
if (deviceIsLandscape && videoIsLandscape)
{
    // Both landscape - no rotation needed
    orientationHint = 0;
}
else if (!deviceIsLandscape && !videoIsLandscape)
{
    // Both portrait - no rotation needed
    orientationHint = 0;
}
else
{
    // Orientations don't match - apply device rotation
    orientationHint = deviceRotation;
}

_mediaRecorder.SetOrientationHint(orientationHint);
```

**Key Points**:
- Must check if video format matches device orientation
- Only apply rotation metadata when they DON'T match
- Prevents double-rotation (rotating an already-correct video)

**Example Scenario - Recording in Landscape**:
1. Device rotation: 270° (landscape)
2. Camera format: 1280×720 (landscape)
3. Both are landscape → Set orientation hint to **0** (no rotation)
4. Video plays correctly in landscape

### Custom Capture Flow (`UseCaptureVideoFlow = true`)

**API Used**: `MediaCodec` + `MediaMuxer`

**Implementation**: `AndroidCaptureVideoEncoder.cs`

**Approach**:
- Encoder dimensions are **NOT swapped** to match device orientation
- Records video in camera's native format
- Sets rotation metadata via `MediaMuxer.SetOrientationHint()`

```csharp
_muxer.SetOrientationHint(_deviceRotation);
```

**Key Points**:
- Always uses `DeviceRotation` value directly
- No dimension swapping logic
- Simpler approach - let player handle rotation

---

## iOS

### Native Recording (`UseCaptureVideoFlow = false`)

**API Used**: `AVCaptureMovieFileOutput`

**Implementation**: `NativeCamera.Apple.cs` → `StartVideoRecording()`

**Orientation Handling**:
```csharp
// Get the video connection
var videoConnection = _movieFileOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant());

if (videoConnection != null && videoConnection.SupportsVideoOrientation)
{
    // Map device rotation to AVCaptureVideoOrientation
    var orientation = DeviceRotationToVideoOrientation(FormsControl.DeviceRotation);
    videoConnection.VideoOrientation = orientation;
}
```

**Rotation Mapping**:
```csharp
private AVCaptureVideoOrientation DeviceRotationToVideoOrientation(int deviceRotation)
{
    return (deviceRotation % 360) switch
    {
        0   => AVCaptureVideoOrientation.Portrait,
        90  => AVCaptureVideoOrientation.LandscapeRight,
        180 => AVCaptureVideoOrientation.PortraitUpsideDown,
        270 => AVCaptureVideoOrientation.LandscapeLeft,
        _   => AVCaptureVideoOrientation.Portrait
    };
}
```

**Key Points**:
- Sets orientation on the **capture connection** before recording starts
- Uses iOS-specific enum values
- AVFoundation handles the actual rotation internally

### Custom Capture Flow (`UseCaptureVideoFlow = true`)

**API Used**: `AVAssetWriter` with custom video input

**Implementation**: `AppleCaptureVideoEncoder.cs`

**Orientation Handling**:
```csharp
// Set video track transform for rotation
var transform = GetTransformForRotation(_deviceRotation);
_videoTrackInput.Transform = transform;
```

**Transform Calculation**:
```csharp
private static CGAffineTransform GetTransformForRotation(int rotation)
{
    return (rotation % 360) switch
    {
        90  => CGAffineTransform.MakeRotation((float)(Math.PI / 2)),
        180 => CGAffineTransform.MakeRotation((float)Math.PI),
        270 => CGAffineTransform.MakeRotation((float)(3 * Math.PI / 2)),
        _   => CGAffineTransform.MakeIdentity()
    };
}
```

**Key Points**:
- Uses `CGAffineTransform` matrix for rotation
- Applied to the video track during setup
- More flexible than native recording

---

## Windows

### Native Recording

**API Used**: `MediaCapture.StartRecordToStorageFileAsync()`

**Implementation**: `NativeCamera.Windows.cs` → `StartVideoRecording()`

**Orientation Handling**:
```csharp
public void ApplyDeviceOrientation(int orientation)
{
    // Windows handles orientation automatically in most cases
}
```

**Key Points**:
- **No manual orientation handling required**
- Windows Runtime API automatically detects device sensors
- Applies correct rotation metadata to video file
- Most robust solution across all platforms

---

## Comparison Summary

| Platform | Native Recording | Custom Capture Flow | Auto-Orientation |
|----------|-----------------|---------------------|------------------|
| **Android** | `MediaRecorder.SetOrientationHint()` with compensation logic | `MediaMuxer.SetOrientationHint()` direct value | ❌ No |
| **iOS** | `AVCaptureConnection.VideoOrientation` | `AVAssetWriter` video track transform | ❌ No |
| **Windows** | Built-in to `MediaCapture` API | N/A (uses native) | ✅ Yes |

---

## Common Issues and Solutions

### Issue 1: Video appears rotated 90° in gallery (Android)
**Cause**: Setting orientation hint without checking if video format already matches device orientation

**Solution**: Compare device orientation with video format dimensions before applying rotation hint

### Issue 2: Landscape video appears as portrait (iOS)
**Cause**: Not setting `VideoOrientation` on capture connection

**Solution**: Set `videoConnection.VideoOrientation` based on device rotation before starting recording

### Issue 3: Different behavior between recording modes
**Cause**: Native and custom capture flows use different orientation approaches

**Solution**: Ensure both paths set appropriate rotation metadata for the platform

---

## Best Practices

1. **Lock device rotation when recording starts**
   - Store `DeviceRotation` value when recording begins
   - Use locked value throughout recording session
   - Prevents orientation changes mid-recording

2. **Test in all orientations**
   - Portrait (0°)
   - Landscape right (90°)
   - Landscape left (270°)
   - Upside down (180°)

3. **Verify in gallery apps**
   - Don't rely only on in-app preview
   - Check how videos appear in system gallery
   - Test on multiple devices/OS versions

4. **Consider camera native format**
   - Know whether camera provides landscape or portrait format
   - Adjust orientation logic accordingly
   - Avoid double-rotation scenarios

---

## Testing Checklist

- [ ] Record video in portrait (0°) - verify plays correctly
- [ ] Record video in landscape right (90°) - verify plays correctly
- [ ] Record video in landscape left (270°) - verify plays correctly
- [ ] Switch between recording modes - verify both work
- [ ] Test on front and back cameras
- [ ] Verify videos in system gallery app
- [ ] Test on different device models
- [ ] Check orientation metadata in video file properties

---

## Implementation References

**Android Native**: `NativeCamera.Android.cs` lines 2192-2216
**Android Custom**: `AndroidCaptureVideoEncoder.cs` line 94
**iOS Native**: `NativeCamera.Apple.cs` lines 2163-2172
**iOS Custom**: `AppleCaptureVideoEncoder.cs` lines 118-145
**Windows**: `NativeCamera.Windows.cs` line 1828 (automatic)

---

*Last updated: 2025-10-24*
