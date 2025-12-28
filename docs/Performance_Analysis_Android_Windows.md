# Performance Analysis: Android & Windows Camera

## Executive Summary
The critical CPU bottleneck found on iOS (software-based rotation of every frame) **does not exist** on Android or Windows in the same form. Both platforms already utilize more efficient pipelines for frame orientation.

- **iOS (Fixed Issue):** Was performing `SKCanvas.RotateDegrees` on the CPU for every frame, causing massive overhead.
- **Android:** Uses **RenderScript** (hardware accelerated) for rotation and YUV conversion. This is significantly faster than the CPU fallback.
- **Windows:** Does **not rotate pixels** at all. It passes rotation metadata to the renderer, which handles it during the final draw. This is the most efficient approach (zero-cost rotation).

## Detailed Findings

### Android Implementation
- **Mechanism:** Uses `RenderScript` (`Rendering.BlitAdjust`) to process YUV frames.
- **Performance:** RenderScript runs on the GPU or DSP, avoiding the main CPU thread.
- **Bottleneck Potential:** While faster than CPU, it still involves a processing step and a memory copy to create `SKImage`.
- **Recommendation:** The current implementation is likely sufficient. Adopting the `GetRawPreviewImage` pattern would unify the architecture but is not urgently required for performance.

### Windows Implementation
- **Mechanism:** Uses `Metadata.ApplyRotation` to set the `Rotation` property on the `CapturedImage`.
- **Performance:** Zero-copy rotation. The pixel data remains in its original orientation.
- **Bottleneck Potential:** The primary cost is `SoftwareBitmap.CreateCopyFromSurfaceAsync` (GPU to CPU copy), which is unavoidable for software-based Skia rendering without using Direct3D interop (which is partially implemented but complex).
- **Recommendation:** Already optimized for rotation. No changes needed.

## iOS Resolution Explanation
The selection of **960x540** resolution on iOS for "Medium" quality is due to an explicit pixel limit in `NativeCamera.Apple.cs`:

```csharp
// NativeCamera.Apple.cs
private bool IsPreviewSizeSuitable(CMVideoDimensions dimensions)
{
    // ...
    var maxVideoPixels = 1024 * 768; // 786,432 pixels
    if (DeviceInfo.Idiom != DeviceIdiom.Phone)
    {
        maxVideoPixels = 1920 * 1080;
    }
    // ...
}
```

- **720p (1280x720):** 921,600 pixels (Exceeds limit on Phones)
- **qHD (960x540):** 518,400 pixels (Allowed)

This limit forces the selection logic to discard 720p and 1080p formats on iPhones, resulting in the selection of the next best format (often 960x540 or 640x480).

## Future Optimization Plan (Optional)
To unify the codebase and potentially squeeze out more performance, the `GetRawPreviewImage` pattern could be implemented on Android/Windows to:
1. **Android:** Skip RenderScript rotation if Skia can handle it (GPU draw).
2. **Windows:** Unify the API surface (already behaves like "Raw" mode).

**Proposed Interface:**
```csharp
public (SKImage Image, int Rotation, bool Flip) GetRawPreviewImage();
```
This would allow the `SkiaCamera` control to handle rotation uniformly across all platforms using the GPU.
