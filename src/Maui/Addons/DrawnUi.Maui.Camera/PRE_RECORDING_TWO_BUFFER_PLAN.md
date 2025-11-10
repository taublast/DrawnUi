# Two-Buffer Pre-Recording Implementation Plan

## Overview

Implement a **two-buffer pre-recording system** for SkiaCamera that captures the last N seconds of video before the user explicitly starts recording. This plan focuses on **Apple (iOS/MacCatalyst)** implementation with a generic architecture suitable for cross-platform extension.

### Key Objectives

- ? Maintain exactly `PreRecordDuration` seconds of encoded video in memory (~11MB for 5 seconds @ 1080p)
- ? Zero frame drops during transition from pre-recording to live recording
- ? No lag spikes or stuttering during buffer rotation
- ? Memory-efficient (H.264 encoding, not uncompressed bitmaps)
- ? Clean muxing: Pre-recorded + Live ? Final Output

---

## Architecture Overview

### State Machine

```
???????????????????????????????????????????????????????????
? Three States: Idle ? PreRecording ? Live Recording    ?
???????????????????????????????????????????????????????????

State 1: Idle
?? No buffers allocated
?? EnablePreRecording=false OR IsRecordingVideo=false

        ? (User: StartVideoRecording + EnablePreRecording=true)

State 2: IsPreRecording=true
?? Buffer A: Active (0s-5s)
?? Buffer B: Standby
?? State indicator: IsPreRecording=true, IsRecordingVideo=false
?? Every 5s: Swap (Buffer B active, Buffer A reset)

        ? (User: StartVideoRecording again)

State 3: IsRecordingVideo=true
?? Stop pre-recording encoder
?? Finalize File A (combined buffers A+B)
?? Start live recording encoder ? File B
?? State indicator: IsPreRecording=false, IsRecordingVideo=true

        ? (User: StopVideoRecording)

Stop/Mux Phase
?? Stop live encoder (finalize File B)
?? Mux: File A + File B ? Final.mp4
?? Cleanup temp files
?? Return: CapturedVideo (final output path)
```

---

## Technical Implementation

### 1. Data Structures

#### A. PrerecordingEncodedBuffer Class

**Location**: `DrawnUi.Maui.Camera/Models/PrerecordingEncodedBuffer.cs` (new file)

**Purpose**: Manage two fixed-size H.264 encoded buffers with automatic rotation

**Key Features**:
- Thread-safe frame appending
- Time-based buffer swapping
- Atomic operations (no GC pressure)
- Diagnostics tracking

**Structure**:
```csharp
public class PrerecordingEncodedBuffer : IDisposable
{
    // Two fixed-size buffers (pre-allocated at init)
    private byte[] _bufferA;           // ~11.25 MB
    private byte[] _bufferB;           // ~11.25 MB
    private int _currentBuffer = 0;    // 0=A, 1=B (atomic toggle)
    
    // Track buffer state (separate for each)
    private struct BufferState
    {
        public int BytesUsed;           // How much of buffer is filled
        public DateTime StartTime;      // When this buffer started filling
        public int FrameCount;          // Diagnostics
        public bool IsLocked;           // Prevent writes during finalization
    }
    private BufferState _stateA;
    private BufferState _stateB;
    
    // Thread safety
    private readonly object _swapLock = new();
    
    // Configuration
    private TimeSpan _maxDuration;      // Typically 5 seconds
    
    // Public API
    public void AppendEncodedFrame(byte[] nalUnits, int size, TimeSpan timestamp);
    public async Task<(string fileA, string fileB)> FlushToFilesAsync();
    public void SwapBuffersAtDurationBoundary(TimeSpan currentTime);
    public TimeSpan GetBufferedDuration();
    public int GetBufferUtilization();  // Diagnostics
}
```

**Memory Calculation**:
```
Estimated encoded H.264 size per frame:
- @ 1080p 30fps: ~75 KB/frame
- 5 seconds = 150 frames = 11.25 MB

Buffer allocation:
- Buffer A: 11.25 MB × 1.2 (headroom for IDR frames) = ~13.5 MB
- Buffer B: ~13.5 MB
- Total: ~27 MB (acceptable on modern devices)

vs. Uncompressed bitmaps:
- 1080p RGBA8888: 8.3 MB per frame
- 5 seconds = 1.245 GB (? OOM crash)
```

#### B. Integration into SkiaCamera

**New Fields**:
```csharp
private PrerecordingEncodedBuffer _preRecordingBuffer;
private string _preRecordingFileA;          // Temp file after flush
private string _preRecordingFileB;          // Temp file after flush
private DateTime _preRecordingStartTime;    // For swap timing
```

### 2. Implementation Flow

#### Phase 1: Initialize Pre-Recording Buffer

**Trigger**: `EnablePreRecording=true` OR `StartVideoRecording()` with `EnablePreRecording=true`

**In `InitializePreRecordingBuffer()`**:
```
1. Create PrerecordingEncodedBuffer(PreRecordDuration)
   ?? Allocate two byte[] buffers (pre-allocated, no GC)
   ?? Initialize _stateA and _stateB
   
2. Store PreRecordDuration locally (for swap timing)
   ?? Default: 5 seconds
   
3. Log: "[InitializePreRecordingBuffer] Created buffers, max duration: 5s"
```

**Code Location**: Modify existing `InitializePreRecordingBuffer()` in `SkiaCamera.cs`

#### Phase 2: Append Frames During Pre-Recording

**Trigger**: Encoder produces H.264 NAL units during pre-recording phase

**Location**: `AppleVideoToolboxEncoder.OnEncodedFrameCallback()`

**Flow**:
```
1. Encoder generates H.264 NAL units
   ?? Size: ~75 KB per frame
   
2. Check if IsPreRecordingMode:
   if (IsPreRecordingMode && _preRecordingBuffer != null)
   {
       _preRecordingBuffer.AppendEncodedFrame(
           encodedData, 
           encodedDataLength, 
           presentationTime
       );
   }
   
3. AppendEncodedFrame():
   a. Acquire _swapLock (brief, ~100ns)
   b. Check if current buffer duration exceeded
      ?? currentTime - bufferStartTime > PreRecordDuration
   c. If YES:
      - Toggle _currentBuffer (0 ? 1)
      - Reset new current buffer state
      - Log: "[PreRecording] Swapped buffers. Active=A"
   d. Append frame bytes to current buffer
      ?? Buffer.BlockCopy(nalUnits, 0, currentBuffer, offset, size)
      ?? Increment BytesUsed, FrameCount
   e. Release _swapLock
```

**Why This Is Fast**:
- Lock duration: ~100 nanoseconds
- No allocation (pre-allocated buffers)
- No GC pressure (no new objects)
- Single int toggle for swap

#### Phase 3: Transition to Live Recording

**Trigger**: User calls `StartVideoRecording()` while `IsPreRecording=true`

**In `StartVideoRecording()` (State 2?3 transition)**:

```
1. Stop pre-recording encoder
   ?? Dispose _captureVideoEncoder
   ?? Stop frame capture timer (_frameCaptureTimer)
   
2. Flush pre-recording buffers to disk
   ?? Call: (fileA, fileB) = await _preRecordingBuffer.FlushToFilesAsync()
   ?? File A: Contains both swaps (full duration)
   ?? File B: Backup file (may be empty if only one swap occurred)
   
3. Store file paths
   ?? _preRecordingFileA = fileA
   ?? _preRecordingFileB = fileB
   
4. Transition states
   ?? IsPreRecording = false
   ?? IsRecordingVideo = true
   
5. Start live recording encoder
   ?? Create new encoder instance
   ?? Initialize with new output path (NOT pre-recorded file)
   ?? Begin encoding live frames to File B (live)
```

**Why Two Temp Files**:
- File A: Pre-recorded segment (complete)
- File B: Live recording segment (growing)
- Both are concatenated by muxing phase

#### Phase 4: Stop and Mux

**Trigger**: User calls `StopVideoRecording()`

**In `StopCaptureVideoFlow()`**:

```
1. Stop live encoder
   ?? Call: capturedVideo = await encoder.StopAsync()
   ?? capturedVideo.FilePath = File B (live recording)
   
2. Check if pre-recording files exist
   if (_preRecordingFileA exists AND File B exists)
   {
       3. Mux Phase
          ?? Call: finalPath = await MuxVideosAsync(
               _preRecordingFileA,
               capturedVideo.FilePath
             )
          ?? MuxVideosInternal() uses AVAssetComposition
          ?? Result: Single MP4 with:
             - 0:00-5:00 = Pre-recorded
             - 5:00-... = Live recorded
       
       4. Update captured video metadata
          ?? capturedVideo.FilePath = finalPath
          ?? Update duration, file size
       
       5. Cleanup temp files
          ?? File.Delete(_preRecordingFileA)
          ?? File.Delete(File B) [original live file]
   }
   
6. Return final CapturedVideo
   ?? Event: VideoRecordingSuccess(capturedVideo)
```

---

## File Structure

### Files to Create

#### 1. `PrerecordingEncodedBuffer.cs` (New)

**Location**: `DrawnUi.Maui.Camera/Models/PrerecordingEncodedBuffer.cs`

**Responsibilities**:
- Buffer allocation and rotation
- Thread-safe frame appending
- Duration tracking
- File flushing

**Key Methods**:
```csharp
public PrerecordingEncodedBuffer(TimeSpan maxDuration)
public void AppendEncodedFrame(byte[] nalUnits, int size, TimeSpan timestamp)
public async Task<(string fileA, string fileB)> FlushToFilesAsync()
public void Dispose()
```

### Files to Modify

#### 1. `SkiaCamera.cs`

**Changes**:
- Add `_preRecordingBuffer` field
- Add `_preRecordingFileA`, `_preRecordingFileB` fields
- Modify `InitializePreRecordingBuffer()` to create `PrerecordingEncodedBuffer`
- Modify `ClearPreRecordingBuffer()` to dispose buffer
- Modify `StartVideoRecording()` for State 2?3 transition
- Modify `StopCaptureVideoFlow()` for muxing phase

#### 2. `AppleVideoToolboxEncoder.cs` (or `AppleCaptureVideoEncoder.cs`)

**Changes**:
- Add reference to parent camera's `PrerecordingEncodedBuffer`
- In encoded frame callback: if `IsPreRecordingMode`, append to buffer
- Ensure thread-safe access to buffer

**Integration Point**:
```csharp
// In OnEncodedFrameCallback (called from compression thread)
if (IsPreRecordingMode && ParentCamera?._preRecordingBuffer != null)
{
    ParentCamera._preRecordingBuffer.AppendEncodedFrame(
        encodedData, 
        encodedDataLength, 
        presentationTime
    );
}
```

---

## Performance Characteristics

### Thread Timing Analysis

| Operation | Duration | Frequency | Impact |
|-----------|----------|-----------|--------|
| **Lock acquisition** | ~100ns | Every frame (30/sec) | <0.003ms/frame |
| **Buffer.BlockCopy** | ~0.1ms | Every frame | <0.1ms/frame |
| **Buffer swap** | <1ns | Every 5 sec | <0.001ms |
| **Frame append total** | ~0.1ms | 30×/sec | ? Invisible |
| **Flush to disk** | ~50-100ms | Once (off-thread) | ? Background |
| **Mux operation** | 1-2 sec | Once at stop | ? Expected wait |

### Memory Profile

| Metric | Value | Notes |
|--------|-------|-------|
| Buffer A size | 13.5 MB | Pre-allocated |
| Buffer B size | 13.5 MB | Pre-allocated |
| Frame metadata | ~1 KB | Per swap tracking |
| **Total peak** | **~27 MB** | Constant, no growth |
| **vs. Uncompressed** | **1.245 GB** | ? Would crash |
| **Compression ratio** | **46:1** | H.264 vs. RGBA8888 |

### Frame Drop Rate

Expected during buffer operations: **ZERO**

**Why**:
- Lock-free critical path for frame appending
- Pre-allocated buffers (no GC)
- Swap occurs at buffer boundary (not mid-frame)
- No re-encoding or format conversion

---

## State Transitions (Detailed)

### EnablePreRecording Property Changed

```
Handler: OnPreRecordingEnabledChanged()

if (enabled && !IsRecordingVideo)
{
    InitializePreRecordingBuffer()  // Create buffer object
    // Don't start encoder yet
    // Encoder starts when StartVideoRecording() is called
}
else if (!enabled)
{
    ClearPreRecordingBuffer()       // Dispose buffer
}
```

### StartVideoRecording() - State Machine

```
Condition 1: EnablePreRecording=true && !IsPreRecording && !IsRecordingVideo
?? Action: Enter pre-recording phase
?? IsPreRecording ? true
?? Create encoder (pre-recording mode)
?? Start timer/callback

Condition 2: IsPreRecording=true && !IsRecordingVideo
?? Action: Transition to live recording
?? Stop encoder (finalize File A)
?? Flush buffers to disk
?? IsPreRecording ? false
?? IsRecordingVideo ? true
?? Create new encoder (live mode)
?? Start timer/callback

Condition 3: !IsRecordingVideo (no pre-recording)
?? Action: Normal recording
?? IsRecordingVideo ? true
?? Create encoder (normal mode)
?? Start timer/callback
```

### StopVideoRecording() - Muxing

```
if (IsRecordingVideo || IsPreRecording)
{
    Stop encoder ? File B
    
    if (PreRecordingFileA exists AND File B exists)
    {
        Mux(FileA, FileB) ? FinalOutput
        Update metadata
        Delete temp files
    }
    
    OnVideoRecordingSuccess(capturedVideo)
}
```

---

## Diagnostics & Logging

### Key Log Points

```csharp
[InitializePreRecordingBuffer] Pre-recording buffer created
    Duration: 5s
    EstimatedSize per buffer: 13.5 MB

[PreRecording] Frame appended
    BufferSize: 1023/13500 KB (7%)
    Timestamp: 2.345s

[PreRecording] Swapped buffers
    Active: B
    Elapsed: 5.034s

[StartVideoRecording] Transitioning to IsPreRecording
    Frames buffered: 152
    Duration: 5.04s

[StartVideoRecording] Flushed pre-recording to File A
    Size: 11.2 MB
    Duration: 5.04s

[StopCaptureVideoFlow] Muxing pre-recorded + live
    FileA: 11.2 MB (5.04s)
    FileB: 23.5 MB (12.16s)
    Output: 34.7 MB (17.20s total)
    Duration: 1.8s
```

### Diagnostics Properties

Add to `PrerecordingEncodedBuffer`:
```csharp
public int GetBufferUtilization()        // Percentage used
public TimeSpan GetBufferedDuration()    // Current duration
public int GetFrameCount()               // Total frames buffered
```

---

## Error Handling

### Scenarios

| Scenario | Handling |
|----------|----------|
| Buffer full (frame dropped) | Log warning, skip frame, continue |
| File write fails during flush | Throw exception, cleanup, abort recording |
| Mux fails (corrupted pre-rec) | Use live file only, log error |
| Memory pressure (low on device) | Reduce buffer size, warn user |

### Cleanup Strategy

```csharp
try
{
    // Recording operation
}
catch (Exception ex)
{
    // Always cleanup
    ClearPreRecordingBuffer();      // Delete File A
    File.Delete(liveRecordingPath); // Delete File B if temp
    
    // Notify
    VideoRecordingFailed?.Invoke(this, ex);
    throw;
}
```

---

## Testing Strategy

### Unit Tests

#### Test 1: Buffer Rotation at Duration Boundary
```csharp
[Test]
public async Task BufferRotatesAtDurationBoundary()
{
    var buffer = new PrerecordingEncodedBuffer(TimeSpan.FromSeconds(1));
    
    // Add 1.5 seconds of data
    // Verify swap occurred around 1.0s
}
```

#### Test 2: FlushToFiles Generates Valid Data
```csharp
[Test]
public async Task FlushToFilesCreatesValidH264()
{
    var buffer = new PrerecordingEncodedBuffer(TimeSpan.FromSeconds(5));
    
    // Append mock H.264 frames
    // Flush
    // Verify files exist and are readable
}
```

#### Test 3: Thread Safety
```csharp
[Test]
public async Task AppendFrameIsThreadSafe()
{
    var buffer = new PrerecordingEncodedBuffer(TimeSpan.FromSeconds(5));
    
    // Spawn 10 tasks appending frames simultaneously
    // Verify no corruption or exceptions
}
```

### Integration Tests

#### Test 1: Full Pre-Recording Flow (Manually)
1. Enable pre-recording
2. Start camera (IsOn=true)
3. Wait 3 seconds
4. Call StartVideoRecording() (State 1?2)
5. Wait 2 seconds
6. Call StartVideoRecording() again (State 2?3)
7. Wait 2 seconds
8. Call StopVideoRecording()
9. Verify final file contains: 3 (pre) + 4 (live) = 7 seconds

#### Test 2: Muxing Correctness
1. Create mock File A (5 seconds of H.264)
2. Create mock File B (10 seconds of H.264)
3. Call MuxVideosAsync()
4. Verify output file plays correctly with FFProbe

---

## Cross-Platform Extension

### Android Implementation Path

**Same Architecture, Different Encoder**:
- Use `MediaCodec` instead of `VTCompressionSession`
- Append frames to `PrerecordingEncodedBuffer` in same way
- Mux using `MediaMuxer`

### Windows Implementation Path

**Same Architecture, Different Encoder**:
- Use `MediaFoundation` instead of `VTCompressionSession`
- Append frames to `PrerecordingEncodedBuffer` in same way
- Mux using `Windows.Media.MediaProperties`

---

## Implementation Checklist

### Phase 1: Core Buffer Class
- [ ] Create `PrerecordingEncodedBuffer.cs`
- [ ] Implement allocation and rotation
- [ ] Add thread-safe append
- [ ] Add flush-to-files
- [ ] Add diagnostics properties

### Phase 2: SkiaCamera Integration
- [ ] Add buffer field to `SkiaCamera`
- [ ] Modify `InitializePreRecordingBuffer()`
- [ ] Modify `ClearPreRecordingBuffer()`
- [ ] Update `StartVideoRecording()` state machine
- [ ] Update `StopCaptureVideoFlow()` for muxing

### Phase 3: Apple Implementation
- [ ] Integrate buffer into `AppleVideoToolboxEncoder`
- [ ] Pass parent reference from encoder creation
- [ ] Append frames in encoded callback
- [ ] Verify thread safety

### Phase 4: Testing & Validation
- [ ] Write unit tests
- [ ] Manual integration testing
- [ ] Verify muxing produces valid MP4
- [ ] Profile memory and performance
- [ ] Stress test (rapid start/stop cycles)

### Phase 5: Documentation
- [ ] Update README
- [ ] Add code comments
- [ ] Create user guide (enable pre-recording)
- [ ] Document diagnostics

---

## Expected Outcomes

### ? Success Criteria

| Criterion | Target | Mechanism |
|-----------|--------|-----------|
| **Frame drops** | Zero | Pre-allocated buffers, atomic swap |
| **Lag spikes** | <1ms | Lock duration ~100ns |
| **Memory** | <30 MB | Compressed H.264 (46:1 ratio) |
| **Mux time** | 1-2 sec | AVAssetComposition GPU acceleration |
| **Final video** | Valid MP4 | Correct AVAssetComposition muxing |

### ?? User Experience

```
Timeline:
  0.0s: User opens camera (IsOn=true)
  3.5s: Camera stabilized, user realizes they want to record
  3.5s: User hits "Record" button ? StartVideoRecording()
        ? Magic: Last 3.5 seconds INCLUDED automatically
  3.5s: Live recording begins (from 3.5s onward)
 15.0s: User hits "Stop" ? StopVideoRecording()
        ? Result: 11.5-second video (3.5 pre + 11.5 live = 15 total)
```

---

## References

- **SkiaCamera.cs**: State machine logic
- **AppleVideoToolboxEncoder.cs**: Frame encoding callback
- **MuxVideosInternal()**: AVAssetComposition implementation
- **PreRecordDuration Property**: Duration configuration

---

**Version**: 1.0  
**Status**: Ready for Implementation  
**Last Updated**: 2024
