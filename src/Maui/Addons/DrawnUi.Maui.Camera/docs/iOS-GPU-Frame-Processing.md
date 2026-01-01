# iOS Camera Preview - Metal Texture Pattern

This document describes the implementation of smooth camera preview on iOS using the "ArtOfFoto pattern" - creating a Metal texture ONCE and letting it auto-update via iOS's IOSurface pool.

## Problem

Original implementation had lag spikes in camera preview (especially in Video mode) caused by heavy processing inside `DidOutputSampleBuffer`:

```
Camera callback (DidOutputSampleBuffer):
  → GetImageBuffer() [CPU work]
  → Create CVMetalTexture [CPU work]
  → Copy pixels to byte[] [CPU COPY]
  → Create SKImage [CPU work]
  → Scale for preview [CPU work]
  → Update display

Result: Camera callback blocked → pipeline backup → lag spikes
```

**Key insight**: The lag spikes only appeared when running WITHOUT debugger attached (debugger masks timing issues).

## Solution: ArtOfFoto Pattern

The fix separates camera callback from frame processing using Metal's zero-copy texture access:

```
Camera callback (DidOutputSampleBuffer):
  → IF first frame: Create Metal texture ONCE
  → Set _hasNewFrame = true
  → Signal processing thread
  → RETURN IMMEDIATELY (almost no work!)

Processing thread (separate):
  → Wait for signal
  → Read from Metal texture (auto-updates via IOSurface pool!)
  → Scale with MetalPreviewScaler (GPU)
  → Copy scaled data to preview buffer
```

**Why it works**: iOS camera reuses a pool of IOSurface-backed buffers. When you create a CVMetalTexture from a CVPixelBuffer, the texture becomes a view into that IOSurface. On subsequent frames, the camera writes new data to the same IOSurface, and the Metal texture automatically shows the new content - no need to recreate anything!

## Implementation Details

### Key Files

| File | Purpose |
|------|---------|
| `Apple/NativeCamera.Apple.cs` | Camera capture with ArtOfFoto pattern |
| `Apple/MetalPreviewScaler.cs` | GPU-based image scaling for preview |

### NativeCamera.Apple.cs - Core Pattern

```csharp
// Fields - Metal texture created ONCE
private CVMetalTextureCache _previewTextureCache;
private IMTLTexture _previewTexture;  // Created once, auto-updates!
private readonly object _lockPreviewTexture = new();

// Camera callback - minimal work
[Export("captureOutput:didOutputSampleBuffer:fromConnection:")]
public void DidOutputSampleBuffer(...)
{
    if (_previewTexture == null)
    {
        // FIRST FRAME ONLY: Create texture cache and texture
        pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
        _previewTextureCache = new CVMetalTextureCache(_metalDevice);
        var cvTexture = _previewTextureCache.TextureFromImage(
            pixelBuffer, MTLPixelFormat.BGRA8Unorm, width, height, 0, out _);
        _previewTexture = cvTexture.Texture;
    }
    // SUBSEQUENT FRAMES: Just signal - texture auto-updates!

    lock (_lockPendingBuffer)
    {
        _hasNewFrame = true;
        Monitor.Pulse(_lockPendingBuffer);
    }
}
```

### Processing Thread - Reads from Auto-Updating Texture

```csharp
private void FrameProcessingLoop()
{
    while (!_stopProcessing)
    {
        lock (_lockPendingBuffer)
        {
            while (!_hasNewFrame && !_stopProcessing)
                Monitor.Wait(_lockPendingBuffer);
            _hasNewFrame = false;
        }

        // Read from Metal texture (shows latest camera frame!)
        lock (_lockPreviewTexture)
        {
            var texture = _previewTexture;

            // Scale on GPU if needed (reduces data to copy)
            if (needsScaling && _metalScaler != null)
            {
                _metalScaler.ScaleFromTexture(texture, pixelData, out bytesPerRow);
            }
            else
            {
                texture.GetBytes(ptr, bytesPerRow, region, 0);
            }
        }

        // Update preview display
        UpdatePreview(pixelData, width, height);
    }
}
```

### MetalPreviewScaler - GPU Scaling

Uses Metal compute shader to scale high-resolution frames for preview:

```csharp
public bool ScaleFromTexture(IMTLTexture inputTexture, byte[] outputData, out int bytesPerRow)
{
    // Semaphore prevents GPU backlog (drop frames vs queue them)
    if (!_gpuSemaphore.Wait(0))
        return false;  // GPU busy, skip frame

    // Metal compute shader scales the texture
    computeEncoder.SetTexture(inputTexture, 0);
    computeEncoder.SetTexture(_outputTexture, 1);
    computeEncoder.DispatchThreadgroups(threadGroups, threadGroupSize);

    // Copy scaled result to output array
    _outputTexture.GetBytes(ptr, bytesPerRow, region, 0);
}
```

## Key Principles

1. **Create Metal texture ONCE** - Don't recreate on every frame
2. **Minimal camera callback** - Just signal and return
3. **Separate processing thread** - All heavy work happens off camera thread
4. **GPU scaling** - Reduces data to copy for preview
5. **Drop frames vs queue** - Semaphore prevents GPU backlog

## Performance

| Before | After |
|--------|-------|
| Lag spikes every ~0.3s | Smooth preview |
| Heavy camera callback | Minimal callback |
| CPU scaling | GPU scaling |
| Multiple CPU copies | Single copy at end |

## Requirements

- iOS 11+ (stable Metal + Skia)
- Metal support (all 64-bit iOS devices)
- SkiaSharp v3

## Reference

This pattern is derived from the ArtOfFoto app's `CameraCapture.cs` implementation which uses the same principle:

```csharp
// From ArtOfFoto - key insight
if (CapturedTexture == null)
{
    pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
    CapturedTexture = _textureCache.MakeTextureFromCVPixelBuffer(...);
}
// After first frame: entire block is SKIPPED - texture auto-updates!
```
