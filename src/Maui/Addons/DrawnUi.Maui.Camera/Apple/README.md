# SkiaCamera iOS & macCatalyst Shared Implementation

This folder contains the shared implementation for both iOS and macCatalyst platforms.

## Conditional Compilation

All files use `#if IOS || MACCATALYST` directives to ensure they compile for both platforms:

```csharp
#if IOS || MACCATALYST
// Shared implementation code
#endif
```

## Platform-Specific Differences

While most code is shared, there are small platform-specific differences handled via conditional compilation:

### Metadata Creation
```csharp
public virtual Metadata CreateMetadata()
{
    return new Metadata()
    {
#if IOS
        Software = "SkiaCamera iOS",
#elif MACCATALYST
        Software = "SkiaCamera macCatalyst",
#endif
        Vendor = UIDevice.CurrentDevice.Model,
        Model = UIDevice.CurrentDevice.Name,
    };
}
```

## Technical Implementation

### Camera Pipeline
1. **AVCaptureSession** manages the camera session
2. **AVCaptureDeviceInput** handles camera device input
3. **AVCaptureVideoDataOutput** provides real-time preview frames
4. **AVCaptureStillImageOutput** captures high-resolution photos
5. **IAVCaptureVideoDataOutputSampleBufferDelegate** processes frames

### Frame Processing
- Real-time frame processing using Core Video
- BGRA pixel format for optimal SkiaSharp integration
- Background queue processing to maintain smooth performance
- Automatic memory management with proper disposal

### Camera Selection Logic
1. **Triple Camera** (iPhone Pro models, iOS 13+)
2. **Dual Camera** (iPhone Plus/Pro models, iOS 10.2+)
3. **Single Camera** (fallback for all devices)

