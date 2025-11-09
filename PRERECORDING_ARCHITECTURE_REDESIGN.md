# Pre-Recording Architecture Redesign - Encoded Buffer Strategy

## Overview

You were absolutely right - the previous approach of trying to prepend SKBitmap objects during recording was problematic. The new architecture follows the **standard video recording pipeline** but with pre-encoded buffering:

**Old (Broken) Flow:**
```
Pre-Recording: Capture → Skia → Store SKBitmap in memory
File Recording: PrependFramesAsync → Encode SKBitmaps on-the-fly ❌ SIGSEGV!
```

**New (Correct) Flow:**
```
Pre-Recording: Capture → Skia → Hardware Encode → Store Encoded Bytes
File Recording: Write Encoded Bytes → Continue Live Encoding ✅ Clean!
```

## Key Changes

### 1. **PrerecordingEncodedBuffer Class**
A new memory buffer that stores pre-encoded video data (raw bytes):

```csharp
public class PrerecordingEncodedBuffer
{
    private MemoryStream _encodedData;
    
    // Append encoded frames during pre-recording
    public void AppendEncodedData(byte[] data, int offset, int length);
    
    // Write all buffered data to file during transition
    public async Task WriteToFileAsync(FileStream fileStream);
    
    // Get copy of data
    public byte[] GetBufferedData();
}
```

### 2. **Updated ICaptureVideoEncoder Interface**
```csharp
public interface ICaptureVideoEncoder
{
    // Initialize (same as before)
    Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio);
    
    // Start encoding (same as before)
    Task StartAsync();
    
    // Add frame (same as before)
    Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp);
    
    // NEW: Prepend encoded data (simple memory dump)
    Task PrependBufferedEncodedDataAsync(PrerecordingEncodedBuffer prerecordingBuffer);
    
    // Stop and finalize (same as before)
    Task<CapturedVideo> StopAsync();
}
```

### 3. **Implementation Strategy by Platform**

#### **iOS (AppleCaptureVideoEncoder)**
```
Pre-Recording Phase:
1. Initialize encoder with memory-only mode
2. Frames: Capture → Skia GPU → CVPixelBuffer → AVAssetWriter (to memory)
3. Collect encoded bytes in PrerecordingEncodedBuffer
4. No thread issues - all on main thread already!

File Recording Phase:
1. Write PrerecordingEncodedBuffer to file stream
2. Continue live frame encoding to same file
3. Done - frames already properly formatted!
```

#### **Windows (MediaFoundation)**
```
Pre-Recording Phase:
1. Initialize encoder with memory sink
2. Frames: Capture → Skia → MF Sample → MediaSink (to memory)
3. Collect encoded bytes

File Recording Phase:
1. Dump buffer to file
2. Continue encoding
```

#### **Android (MediaCodec)**
```
Pre-Recording Phase:
1. Initialize encoder with memory output
2. Frames: Capture → Skia GPU → EGLSurface → MediaCodec → Extract NAL units
3. Store encoded NAL units in buffer

File Recording Phase:
1. Write buffered NAL units to MP4 container
2. Continue encoding live frames
```

## Implementation Steps

### Step 1: Pre-Recording Mode
Each encoder should support an optional pre-recording mode:

```csharp
// iOS Example
public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio, bool preRecordingOnly = false)
{
    _isPreRecordingMode = preRecordingOnly;
    
    if (preRecordingOnly)
    {
        // Don't create file writer yet
        // Just setup encoder pipeline to memory
        _preRecordingBuffer = new PrerecordingEncodedBuffer();
    }
    else
    {
        // Normal file setup
    }
}
```

### Step 2: Capture Encoded Bytes
During pre-recording, intercept the encoded output and save to buffer:

```csharp
// In SubmitFrameAsync or equivalent
if (_isPreRecordingMode && _preRecordingBuffer != null)
{
    // Extract encoded bytes from CVPixelBuffer or encoder output
    byte[] encodedFrame = GetEncodedBytes(...);
    _preRecordingBuffer.AppendEncodedData(encodedFrame, 0, encodedFrame.Length);
}
```

### Step 3: Transition to File Recording
When switching from pre-recording to file recording:

```csharp
// In SkiaCamera.cs
if (_bufferToPrepend != null && _bufferToPrepend.Count > 0)
{
    // Encode the buffered frames
    PrerecordingEncodedBuffer encodedBuffer = EncodeBufferedBitmaps(_bufferToPrepend, fps);
    
    // Prepend to file
    await _captureVideoEncoder.PrependBufferedEncodedDataAsync(encodedBuffer);
    
    _bufferToPrepend = null;
}
```

Or better yet - if using pre-recording mode:

```csharp
// In SkiaCamera.cs
if (_preRecordingBuffer != null && _preRecordingBuffer.SizeBytes > 0)
{
    // Prepend the already-encoded buffer
    await _captureVideoEncoder.PrependBufferedEncodedDataAsync(_preRecordingBuffer);
    _preRecordingBuffer = null;
}
```

## Advantages

1. **No Thread Safety Issues** ✅
   - Pre-recording encodes to memory using existing codec paths
   - All native operations happen on correct threads already
   - No cross-thread boundary violations

2. **No SIGSEGV Crashes** ✅
   - No SKBitmap/SkiaSharp operations during prepend
   - Just memory-to-file copying
   - Pure data manipulation, no native interop

3. **Minimal Prepend Latency** ✅
   - Prepend is just `Array.Copy` or file stream write
   - Microseconds, not milliseconds
   - Zero re-encoding cost

4. **Proper Video Format** ✅
   - Pre-recorded frames are already properly encoded
   - NAL units already formatted
   - No frame validation or format conversion needed

5. **Easy to Extend** ✅
   - PrerecordingEncodedBuffer can track frame timing
   - Easy to add MP4 container support
   - Works for all platforms with same interface

## Next Steps

1. Add `PrerecordingEncodedBuffer` parameter tracking to each encoder
2. Implement buffer collection during pre-recording (platform-specific)
3. Implement `PrependBufferedEncodedDataAsync` to write buffer to output file
4. Test transition: pre-record 5 seconds, then file record 5 seconds
5. Verify final MP4 plays smoothly with no artifacts

## Testing Checklist

- [ ] Pre-record 100 frames to buffer
- [ ] Transition to file recording
- [ ] Verify output contains all 100 buffered frames followed by live frames
- [ ] Play video and verify no glitches/artifacts
- [ ] Verify timings are correct (buffered frames at start, then live)
- [ ] Test across iOS, Android, Windows
