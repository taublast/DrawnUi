# Pre-Recording Technology - Technical Documentation

## Overview

The pre-recording system allows capturing video frames continuously in memory before the user presses "Record". When recording starts, the last N seconds of buffered frames are written to an MP4 file and seamlessly muxed with the live recording.

**Key Characteristics:**
- Zero-drop frame capture (no frames lost during buffer rotation)
- Fixed memory footprint (~27 MB for 5 seconds @ 1080p)
- Hardware H.264 encoding via VTCompressionSession (iOS/Mac)
- Automatic circular buffer rotation
- Keyframe-aware pruning for valid H.264 output

## Architecture

### Two-Phase Recording Flow

```
Phase 1: Pre-Recording (Before user presses Record)
  Camera → Frame Processor → VTCompressionSession → H.264 NAL units → Circular Buffer (Memory)

Phase 2: Live Recording (After user presses Record)
  Step 1: Write circular buffer to pre_rec_*.mp4
  Step 2: Start AVAssetWriter → live_rec_*.mp4
  Step 3: On stop, mux both files → final output
```

### Component Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       SkiaCamera                             │
│  - Manages recording state                                   │
│  - Coordinates encoder lifecycle                             │
│  - Tracks file paths and durations                           │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│               AppleVideoToolboxEncoder                       │
│  - VTCompressionSession (H.264 hardware encoding)            │
│  - Manages PrerecordingEncodedBuffer                         │
│  - Writes pre-recorded buffer to MP4                         │
│  - AVAssetWriter for live recording                          │
│  - Muxes pre-recording + live → final output                 │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│            PrerecordingEncodedBuffer                         │
│  - Two fixed-size byte[] buffers (~13.5 MB each)             │
│  - Circular buffer with automatic rotation                   │
│  - Stores encoded H.264 frames with metadata                 │
│  - Keyframe detection and pruning                            │
└─────────────────────────────────────────────────────────────┘
```

## Two-Buffer Circular System

### Memory Layout

```
Buffer A: [=============================] 13.5 MB
Buffer B: [=============================] 13.5 MB

Current: A (active for writes)

Total Memory: 27 MB (fixed, pre-allocated at startup)
```

### How Rotation Works

**Timeline Example (5 second max duration):**

```
Time 0-5s:  Buffer A active, accumulating frames
Time 5s:    Rotation trigger!
            - Switch to Buffer B
            - Prune frames older than 5s from metadata list
            - Buffer A now contains stale data (will be overwritten)

Time 5-10s: Buffer B active, accumulating frames
Time 10s:   Rotation trigger!
            - Switch to Buffer A (overwrites old data)
            - Prune frames older than 5s from metadata list

Result: At any point, only last 5 seconds are kept
```

### Frame Metadata Storage

```csharp
private class EncodedFrame
{
    public byte[] Data;              // H.264 NAL units (complete frame)
    public TimeSpan Timestamp;       // Video PTS timestamp
    public CMTime PresentationTime;  // CoreMedia presentation time
    public CMTime Duration;          // Frame duration
    public DateTime AddedAt;         // Wall-clock time (for diagnostics)
    public bool IsKeyFrame;          // Critical for pruning
}

private List<EncodedFrame> _frames; // Metadata only, not the bulk data
```

**Why separate metadata from bulk data:**
- Bulk data goes in pre-allocated byte[] buffers (fast, no GC)
- Metadata in List for quick timestamp-based queries and pruning
- During pruning, we only manipulate the List, not the 13.5 MB buffers

## H.264 Encoding and Keyframe Handling

### VTCompressionSession Configuration

```csharp
// Hardware-accelerated H.264 encoding
var session = VTCompressionSession.Create(
    width, height,
    CMVideoCodecType.H264,
    encoderSpec: null,  // Use hardware encoder
    sourceImageBufferAttributes: null,
    outputCallback: OnFrameEncoded
);

// Configure for low latency
session.SetProperty(
    VTCompressionPropertyKey.RealTime,
    true
);

// Set max keyframe interval (every 30 frames @ 30fps = 1 keyframe/second)
session.SetProperty(
    VTCompressionPropertyKey.MaxKeyFrameInterval,
    30
);
```

### Keyframe Detection

H.264 uses NAL (Network Abstraction Layer) units. Each frame consists of one or more NAL units:

```
Frame Data Format:
[4-byte length][NAL header][payload][4-byte length][NAL header][payload]...

NAL Header (1 byte):
  bit 7:     forbidden_zero_bit (must be 0)
  bits 6-5:  nal_ref_idc (reference priority)
  bits 4-0:  nal_unit_type

NAL Unit Types:
  1: Non-IDR slice (P-frame, B-frame)
  5: IDR slice (I-frame, KEYFRAME) ← We detect this!
  7: SPS (Sequence Parameter Set)
  8: PPS (Picture Parameter Set)
```

**Keyframe Detection Algorithm:**

```csharp
private static bool IsKeyFrame(byte[] nalUnits, int size)
{
    int offset = 0;
    while (offset + 4 < size)
    {
        // Read 4-byte network order length
        int nalLength = (nalUnits[offset] << 24) |
                       (nalUnits[offset + 1] << 16) |
                       (nalUnits[offset + 2] << 8) |
                       nalUnits[offset + 3];
        offset += 4;

        // Check NAL unit type (bits 0-4 of first byte)
        byte nalHeader = nalUnits[offset];
        int nalType = nalHeader & 0x1F;

        if (nalType == 5) // IDR frame
            return true;

        offset += nalLength;
    }
    return false;
}
```

## Keyframe-Aware Pruning

**The Critical Problem:**
When pruning frames to maintain the 5-second limit, we MUST ensure the first remaining frame is a keyframe. Otherwise, the video is undecodable.

**Example Scenario:**

```
Before Pruning (10 seconds of video):
Frame 0:   0.000s [IDR] ← Keyframe
Frame 1:   0.033s [P]
Frame 2:   0.066s [P]
...
Frame 150: 5.000s [P]   ← NOT a keyframe!
Frame 151: 5.033s [P]
Frame 152: 5.066s [IDR] ← Keyframe here
...
Frame 300: 10.000s [P]

Pruning Goal: Keep last 5 seconds (5.0s - 10.0s)

❌ WRONG: Remove frames < 5.0s
    Result: First frame is Frame 150 (P-frame)
    Problem: P-frame references Frame 149 (deleted!) → CORRUPT VIDEO

✅ CORRECT: Remove frames < 5.0s, then remove non-keyframes until we hit a keyframe
    Result: First frame is Frame 152 (IDR)
    Duration: 4.934s instead of exactly 5.0s, but video is VALID
```

**Implementation:**

```csharp
public void PruneToMaxDuration()
{
    // Step 1: Remove frames older than max duration based on timestamps
    var lastFrameTimestamp = _frames[_frames.Count - 1].Timestamp;
    var cutoffTimestamp = lastFrameTimestamp - _maxDuration;
    _frames.RemoveAll(f => f.Timestamp < cutoffTimestamp);

    // Step 2: CRITICAL - Ensure first frame is a keyframe
    while (_frames.Count > 0 && !_frames[0].IsKeyFrame)
    {
        _frames.RemoveAt(0);  // Keep removing until we hit a keyframe
    }

    // Now video starts with keyframe → valid H.264!
}
```

## Timestamp Adjustment for Valid MP4

**The Problem:**
After pruning, frames might have timestamps like 5.0s, 5.033s, 5.066s... but MP4 files must start at timestamp 0.0s.

**Example:**

```
After pruning:
Frame 0: PTS = 5.000s [IDR]
Frame 1: PTS = 5.033s [P]
Frame 2: PTS = 5.066s [P]
...

AVAssetWriter session:
writer.StartSessionAtSourceTime(CMTime.Zero)  // Session starts at 0.0s

❌ WRONG: Write frames with original timestamps
    Session: 0.0s
    First frame: 5.0s
    Gap: 5 seconds of missing data → AVMutableComposition: "Cannot Decode"

✅ CORRECT: Adjust all timestamps by subtracting first frame's PTS
    First frame PTS: 5.000s
    Adjusted timestamps: 0.000s, 0.033s, 0.066s...
    Session: 0.0s, First frame: 0.0s → Valid MP4!
```

**Implementation:**

```csharp
// Get offset from first frame
CMTime firstFramePts = frames[0].PresentationTime;

foreach (var (data, presentationTime, duration) in frames)
{
    // Adjust timestamp to start from zero
    var adjustedPts = CMTime.Subtract(presentationTime, firstFramePts);

    var timing = new CMSampleTimingInfo
    {
        PresentationTimeStamp = adjustedPts,  // 0.0s, 0.033s, 0.066s...
        Duration = duration,
        DecodeTimeStamp = CMTime.Invalid
    };

    // Create CMSampleBuffer and append to AVAssetWriter
    // ...
}
```

## File Path Synchronization

**The Problem:**
Two components need to reference the same pre-recording file:
1. **Encoder** creates and writes the file
2. **SkiaCamera** needs the path for muxing

**Solution Flow:**

```
1. SkiaCamera.InitializePreRecordingBuffer():
   - Generate base path (used for encoder initialization)
   - Store in _preRecordingFilePath (temporary)

2. Encoder.InitializeAsync(basePath):
   - Receive base path
   - Generate actual path: pre_rec_{guid}.mp4
   - Store in encoder's _preRecordingFilePath

3. User presses Record → Encoder.StopAsync():
   - Write buffer to pre_rec_{guid}.mp4
   - Return CapturedVideo with FilePath = pre_rec_{guid}.mp4

4. SkiaCamera receives result:
   - Update _preRecordingFilePath from result.FilePath
   - Now both reference the SAME file!

5. Muxing:
   - SkiaCamera uses _preRecordingFilePath → Correct file ✓
```

## Muxing Pre-Recording + Live Recording

**Process:**

```
Input Files:
  pre_rec_abc123.mp4    (5.0s, starts at PTS 0.0s)
  live_rec_abc123.mp4   (3.0s, starts at PTS 4.98s with offset)

Output:
  muxed_timestamp_guid.mp4  (8.0s total)
```

**AVMutableComposition Timeline:**

```
Timeline:
┌──────────────────────┬────────────────┐
│  Pre-recording       │  Live recording│
│  0.0s - 5.0s        │  5.0s - 8.0s   │
└──────────────────────┴────────────────┘

Implementation:
var composition = AVMutableComposition.Create();
var videoTrack = composition.AddMutableTrack(AVMediaTypes.Video, 0);

// Insert pre-recording at time 0
videoTrack.InsertTimeRange(
    new CMTimeRange { Start = CMTime.Zero, Duration = preRecAsset.Duration },
    preRecVideoTrack,
    CMTime.Zero,
    out error
);

// Insert live recording after pre-recording
CMTime insertTime = preRecAsset.Duration;
videoTrack.InsertTimeRange(
    new CMTimeRange { Start = CMTime.Zero, Duration = liveRecAsset.Duration },
    liveRecVideoTrack,
    insertTime,
    out error
);

// Export to final file
var exporter = new AVAssetExportSession(composition, AVAssetExportSessionPreset.HighestQuality);
exporter.OutputUrl = outputUrl;
exporter.OutputFileType = AVFileType.Mpeg4;
await exporter.ExportTaskAsync();
```

## Live Recording Timestamp Offset

**The Problem:**
Live recording starts AFTER pre-recording. If we don't offset timestamps, frames overlap in time.

**Solution:**

```
Pre-recording duration: 5.0s

Live Recording without offset:
Frame 0: PTS = 0.000s  ❌ Overlaps with pre-recording!
Frame 1: PTS = 0.033s
...

Live Recording with offset:
Frame 0: PTS = 5.000s  ✓ Continues after pre-recording
Frame 1: PTS = 5.033s
...

Implementation:
appleEncoder.SetPreRecordingDuration(TimeSpan.FromSeconds(5.0));

// In AddFrameAsync():
double timestamp = _pendingTimestamp.TotalSeconds;
if (_preRecordingDuration > TimeSpan.Zero)
{
    timestamp += _preRecordingDuration.TotalSeconds;  // Add offset
}
CMTime ts = CMTime.FromSeconds(timestamp, 1_000_000);
```

## Memory Efficiency Comparison

**Uncompressed Video (5 seconds @ 1080p 30fps):**
```
Frame size: 1920 × 1080 × 4 bytes (RGBA) = 8,294,400 bytes ≈ 8.3 MB
Total: 8.3 MB × 150 frames = 1,245 MB (1.2 GB)
```

**H.264 Compressed (Our Implementation):**
```
Frame size: ~75 KB average (H.264 compressed)
Buffer A: 13.5 MB (fixed)
Buffer B: 13.5 MB (fixed)
Total: 27 MB (fixed, never grows)

Compression ratio: 1,245 MB / 27 MB ≈ 46:1
```

## Performance Characteristics

**Frame Append (30 fps):**
- Lock duration: ~100 nanoseconds (atomic buffer swap check)
- Memory copy: ~75 KB per frame (Buffer.BlockCopy)
- Keyframe detection: ~50 microseconds (NAL unit scan)
- Total: <1 millisecond per frame
- CPU impact: <3% @ 30 fps

**Buffer Rotation (every 5 seconds):**
- Swap buffers: ~100 nanoseconds (atomic int toggle)
- Prune metadata list: ~5 milliseconds (150 frames)
- Keyframe search: ~1 millisecond (scan until keyframe found)
- Total: ~6 milliseconds (once every 5 seconds, imperceptible)

**Buffer to MP4 Write (on Record button press):**
- Create AVAssetWriter: ~50 milliseconds
- Write 150 frames: ~200 milliseconds
- Finalize file: ~100 milliseconds
- Total: ~350 milliseconds (one-time cost when starting recording)

## Error Handling and Edge Cases

### Case 1: No Keyframe in Buffer
```
Scenario: User records for 0.5 seconds (no keyframe generated yet)
Result: PruneToMaxDuration() removes all frames
Solution: Check _frames.Count after pruning
  - If empty: Skip pre-recording, use live recording only
  - Log warning: "No keyframe found in pre-recording buffer"
```

### Case 2: Buffer Overflow
```
Scenario: Extremely high bitrate exceeds 13.5 MB buffer
Detection: currentState.BytesUsed + size > bufferSize
Action: Drop frame and log warning
Result: Small gap in pre-recording, but continues operating
```

### Case 3: Muxing Failure
```
Scenario: Pre-recorded file is corrupted or missing
Detection: AVAssetExportSession.Status == .Failed
Fallback: Use live recording only
Code:
  if (exportSession.Status == AVAssetExportSessionStatus.Failed)
  {
      Debug.WriteLine("Muxing failed, using live recording only");
      File.Copy(liveRecordingPath, outputPath, true);
  }
```

### Case 4: Duration Exceeds Maximum
```
Scenario: User records pre-recording for 10 seconds (max is 5)
Action:
  1. Automatic pruning during buffer swap (removes 0-5s range)
  2. Final pruning before file write (ensures exactly last 5s)
  3. Keyframe adjustment (may reduce to 4.9s to start with keyframe)
Result: Valid 4.9s pre-recording + live recording
```

## Configuration

**SkiaCamera Properties:**

```csharp
// Enable pre-recording mode
camera.EnablePreRecording = true;

// Set maximum pre-recording duration (default: 5 seconds)
camera.PreRecordDuration = TimeSpan.FromSeconds(5);

// Auto-enable for first StartVideoRecording() call
// Second call starts live recording
```

**Buffer Size Calculation:**

```csharp
// Formula:
// bufferSize = expectedBytesPerSecond × duration × safetyMargin

// Example for 1080p @ 30fps:
// - H.264 bitrate: ~15 Mbps = 1.875 MB/s
// - 5 seconds: 1.875 × 5 = 9.375 MB
// - Safety margin: 1.44x (for IDR frame spikes)
// - Total: 13.5 MB per buffer

const int bufferSize = (int)(11.25 * 1024 * 1024 * 1.2); // ~13.5 MB
_bufferA = new byte[bufferSize];
_bufferB = new byte[bufferSize];
```

## Platform-Specific Notes

### iOS/MacCatalyst
- Uses **VTCompressionSession** for hardware H.264 encoding
- Uses **AVAssetWriter** with null output settings (pass-through mode)
- Uses **CMSampleBuffer** with timing information
- Uses **AVMutableComposition** for muxing

### Android (Future)
- Use **MediaCodec** for hardware H.264 encoding
- Use **MediaMuxer** for MP4 writing
- Use **MediaExtractor** + **MediaMuxer** for concatenation

### Windows (Future)
- Use **MediaFoundation** MFT (Media Foundation Transform)
- Use **IMFSinkWriter** for MP4 writing
- Use **IMFSourceReader** + **IMFSinkWriter** for concatenation

## Diagnostics and Logging

**Key Log Messages:**

```
[PreRecording] AppendEncodedFrame: size=75123, timestamp=3.456s, KeyFrame=true
  → Frame added to buffer

[PreRecording] Swapped buffers. Active=B, Pruned frames: 150 -> 145 (first is KEYFRAME)
  → Buffer rotation occurred

[PreRecording] PruneToMaxDuration: 180 -> 150 frames (first frame now at 5.123s is KEYFRAME)
  → Final pruning before file write

[AppleVideoToolboxEncoder] Frame 1: Adjusted PTS=0.000s (original was 5.123s)
  → Timestamp adjustment applied

[MuxVideosApple] Export completed: 8.0s total (pre: 5.0s, live: 3.0s)
  → Successful muxing
```

## Future Optimizations

1. **GPU-based encoding** (currently CPU-based VTCompressionSession)
   - Use Metal shaders for color space conversion
   - Direct texture-to-encoder pipeline

2. **Adaptive buffer sizing**
   - Calculate buffer size based on actual bitrate
   - Dynamically adjust for resolution changes

3. **Smart keyframe insertion**
   - Request keyframe when approaching max duration
   - Reduces pruning waste (less frames discarded)

4. **Multi-threaded encoding**
   - Encode frames on background thread
   - Reduce main thread impact

5. **Zero-copy buffer management**
   - Use IOSurface for direct GPU→Encoder transfer
   - Eliminate CPU memory copies

## References

- [VTCompressionSession Documentation](https://developer.apple.com/documentation/videotoolbox/vtcompressionsession)
- [AVAssetWriter Pass-through Mode](https://developer.apple.com/documentation/avfoundation/avassetwriter)
- [H.264 NAL Unit Format (ITU-T H.264)](https://www.itu.int/rec/T-REC-H.264)
- [AVMutableComposition for Video Editing](https://developer.apple.com/documentation/avfoundation/avmutablecomposition)
- [CoreMedia Timing](https://developer.apple.com/documentation/coremedia/cmtime)
