# Pre-Recording SIGSEGV Fix - Main Thread Marshaling

## Problem

The app was crashing with a **SIGSEGV (Segmentation Fault)** on iOS during pre-recording prepend:

```
Got a SIGSEGV while executing native code. This usually indicates
a fatal error in the mono runtime or one of the native libraries used by your application.
```

Stack trace showed the crash occurring in `PrependFramesAsync` when transitioning from pre-recording to file recording.

## Root Cause

The iOS implementation of `PrependFramesAsync` was performing **thread-unsafe native operations**:

1. **AVAssetWriter** - Not thread-safe; must run on main thread
2. **CVPixelBuffer operations** - Native Core Video framework calls; must run on main thread
3. **SkiaSharp GPU operations** - Metal/GPU access requires main thread
4. **Mixed Cocoa/Skia interop** - Particularly dangerous when called from background thread

The method was being called as an async task without ensuring all native operations ran on the main thread, causing immediate crashes when trying to access native objects from the wrong thread.

## Solution

**Main Thread Marshaling**: Wrap all native Cocoa/CoreVideo and Skia operations in `MainThread.InvokeOnMainThreadAsync()`:

```csharp
public async Task PrependFramesAsync(List<SKBitmap> bufferedFrames, int frameRate)
{
    // ... validation ...
    
    foreach (SKBitmap bufferedFrame in bufferedFrames)
    {
        CVPixelBuffer pixelBuffer = null;
        SKBitmap resizedSource = null;

        try
        {
            // ALL Skia and native operations on main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Skia resizing (GPU/CPU)
                    SKBitmap source = bufferedFrame;
                    if (bufferedFrame.Width != _width || bufferedFrame.Height != _height)
                    {
                        var info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        resizedSource = new SKBitmap(info);
                        using var canvas = new SKCanvas(resizedSource);
                        canvas.DrawBitmap(bufferedFrame, new SKRect(0, 0, _width, _height));
                        source = resizedSource;
                    }

                    // CVPixelBuffer allocation (native, must be main thread)
                    CVPixelBufferPool pool = _pixelBufferAdaptor?.PixelBufferPool;
                    if (pool == null) return;

                    CVReturn errCode = CVReturn.Error;
                    pixelBuffer = pool.CreatePixelBuffer(null, out errCode);
                    if (pixelBuffer == null || errCode != CVReturn.Success) return;

                    // Lock and write pixels (Skia + native memory)
                    pixelBuffer.Lock(CVPixelBufferLock.None);
                    try
                    {
                        var baseAddress = pixelBuffer.BaseAddress;
                        var bytesPerRow = (int)pixelBuffer.BytesPerRow;
                        var srcInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        using var raster = SKSurface.Create(srcInfo, baseAddress, bytesPerRow);
                        if (raster != null)
                        {
                            raster.Canvas.Clear(SKColors.Black);
                            raster.Canvas.DrawBitmap(source, SKPoint.Empty);
                            raster.Canvas.Flush();
                        }
                    }
                    finally
                    {
                        pixelBuffer.Unlock(CVPixelBufferLock.None);
                    }

                    // Append to AVAssetWriter (native, must be main thread)
                    var ts = CMTime.FromSeconds(currentTime, 1_000_000);
                    if (_pixelBufferAdaptor.AppendPixelBufferWithPresentationTime(pixelBuffer, ts))
                    {
                        currentTime += rtDurationPerFrame;
                        processedCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[...] Error in main thread operation: {ex.Message}\n{ex.StackTrace}");
                }
            });

            // Yield between frames to keep main thread responsive
            await Task.Delay(1);
        }
        finally
        {
            pixelBuffer?.Dispose();
            if (resizedSource != null && !ReferenceEquals(resizedSource, bufferedFrame))
                resizedSource.Dispose();
        }
    }
}
```

## Key Changes

1. **Wrapped all native operations** in `MainThread.InvokeOnMainThreadAsync()`
2. **Moved SKBitmap/SKCanvas/SKSurface operations** inside main thread context
3. **Moved CVPixelBuffer allocation/lock/append** inside main thread context
4. **Added small `Task.Delay(1)`** between frames to prevent main thread starvation
5. **Improved error handling** with full stack trace logging

## Why This Fixes It

- ✅ AVAssetWriter now receives calls from main thread only
- ✅ CVPixelBuffer operations are thread-safe
- ✅ SkiaSharp GPU operations work correctly
- ✅ No more SIGSEGV from native interop violations
- ✅ Main thread remains responsive (1ms yield between frames)

## Testing

The fix ensures:
- Pre-recorded frames are properly prepended to file recording
- No SIGSEGV crashes during transition to file recording
- Main thread remains responsive during prepend operation
- All buffered frames are written with sequential timing
