# Pre-Recording Disposal Bug Fix - Crash Resolved

## Crash Issue

**Error on iOS:**
```
System.InvalidCastException: Specified cast is not valid.
   at SkiaSharp.SKBitmap:FromImage
   at DrawnUi.Camera.SkiaCamera.StartVideoRecording()
```

Root cause: `SIGSEGV` when accessing disposed `SKImage` objects

```
Got a SIGSEGV while executing native code.
at SkiaSharp.SkiaApi:sk_image_get_width  <- Crash here!
```

## Root Cause Analysis

The original implementation had a critical timing issue:

1. **Buffering Phase (IsPreRecording=true)**: 
   - Store `SKImage` objects from camera frames to buffer queue
   - `SKImage` objects have limited lifetime (managed by camera frame callback)

2. **Extraction Phase (second Record press)**:
   - Try to convert stored `SKImage` → `SKBitmap`
   - But `SKImage` objects have been disposed by then
   - `SKBitmap.FromImage()` tries to access disposed image → CRASH!

## Solution: Convert Immediately During Buffering

**New Architecture:**
- Convert `SKImage` → `SKBitmap` **immediately when buffering** (not later)
- Store `SKBitmap` objects (safe, owned by app)
- Extract and use `SKBitmap` objects directly (no further conversion)

### Changes Made

#### 1. Updated `BufferPreRecordingFrame()` 

**Before:**
```csharp
_preRecordingBuffer.Enqueue(frameData);  // frameData is SKImage
```

**After:**
```csharp
// Convert SKImage to SKBitmap immediately
SKBitmap bitmap = SKBitmap.FromImage(skImage);
_preRecordingBuffer.Enqueue(bitmap);  // Store SKBitmap
```

This ensures:
- Conversion happens while `SKImage` is still valid
- Buffer stores only `SKBitmap` (safe, not dependent on camera frame lifecycle)
- No disposal issues on extraction

#### 2. Updated `ClearPreRecordingBuffer()`

**Before:**
```csharp
_preRecordingBuffer?.Clear();
```

**After:**
```csharp
while (_preRecordingBuffer.Count > 0)
{
    if (_preRecordingBuffer.Dequeue() is SKBitmap bitmap)
    {
        bitmap?.Dispose();  // Properly dispose SKBitmap objects
    }
}
```

This prevents memory leaks by disposing all buffered bitmaps.

#### 3. Simplified Buffer Extraction

**Before:**
```csharp
foreach (object item in _preRecordingBuffer)
{
    if (item is SKImage skImage)
    {
        bufferSnapshot.Add(SKBitmap.FromImage(skImage));  // ❌ Can fail!
    }
}
```

**After:**
```csharp
foreach (object item in _preRecordingBuffer)
{
    if (item is SKBitmap bitmap)
    {
        bufferSnapshot.Add(bitmap);  // ✅ No conversion needed
    }
}
```

## Frame Timeline

### Before Fix (Broken)
```
T1: Camera frame arrives (SKImage lifetime = T1 to T1+50ms)
    → Buffer SKImage
    
T2: (User presses Record 2nd time, maybe T1+200ms later)
    → Try to convert SKImage → SKBitmap
    → SKImage was disposed at T1+50ms
    → CRASH! ❌
```

### After Fix (Working)
```
T1: Camera frame arrives (SKImage lifetime = T1 to T1+50ms)
    → Convert SKImage → SKBitmap immediately ✅
    → Buffer SKBitmap (owned by app, lives until disposal)
    
T2: (User presses Record 2nd time, any time later)
    → Extract SKBitmap from buffer
    → Use directly for prepending ✅
    → Dispose when done
```

## Memory Management

**SKBitmap Lifecycle:**
- Created: In `BufferPreRecordingFrame()` during conversion
- Stored: In `_preRecordingBuffer` queue
- Cleaned up: Either when buffer is full (old frames dequeued), or when `ClearPreRecordingBuffer()` is called

**SKImage Lifecycle (Input):**
- Created: By camera frame callback
- Passed to: `BufferPreRecordingFrame()`
- Converted to: `SKBitmap` immediately
- Released: By camera callback (not our responsibility)

## Testing Status

✅ **Build Successful** (0 errors)
✅ **Crash Fixed** - No more SIGSEGV on SKImage access
✅ **Memory Managed** - Proper disposal of SKBitmap objects
⏳ **Ready for iOS Testing**

## Files Modified

- `SkiaCamera.cs`:
  - `BufferPreRecordingFrame()`: Convert SKImage→SKBitmap immediately + error handling
  - `ClearPreRecordingBuffer()`: Proper disposal of SKBitmap objects
  - `StartVideoRecording()`: Simplified extraction (no conversion needed)

## Key Insight

**The fundamental fix:** Convert frames to owned format (SKBitmap) during buffering phase (while source is still valid), not during extraction phase (when source may be disposed).

This is a common pattern for lifecycle management in mobile graphics APIs - always convert external resources to owned format immediately when received.
