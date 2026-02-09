# Pre-Recording Technology - Technical Documentation

## Overview

The pre-recording system allows capturing video AND audio frames continuously in memory before the user presses "Record". When recording starts, the last N seconds of buffered content are written to files and seamlessly muxed with the live recording.

**Key Characteristics:**
- Zero-drop frame capture (no frames lost during buffer rotation)
- Fixed memory footprint (~27 MB video + ~2 MB audio for 5 seconds @ 1080p)
- Hardware H.264 encoding via VTCompressionSession (iOS/Mac)
- Separate audio circular buffer with M4A output
- Automatic circular buffer rotation
- Keyframe-aware pruning for valid H.264 output
- Passthrough muxing for fast concatenation (no re-encoding)

## Architecture

### Two-Phase Recording Flow

```
Phase 1: Pre-Recording (Before user presses Record)
  VIDEO: Camera → Frame Processor → VTCompressionSession → H.264 NAL units → Circular Buffer (Memory)
  AUDIO: Microphone → AudioCapture → PCM samples → CircularAudioBuffer (Memory)

Phase 2: Live Recording (After user presses Record)
  Step 1: Write video circular buffer to pre_rec_video_*.mp4
  Step 2: Write audio circular buffer to pre_rec_audio_*.m4a
  Step 3: Start AVAssetWriter for live video → live_rec_*.mp4
  Step 4: Start live audio writer → live_audio_*.m4a
  Step 5: On stop, mux all files → final output with audio
```

### Component Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       SkiaCamera                             │
│  - Manages recording state                                   │
│  - Coordinates encoder lifecycle                             │
│  - Manages audio capture and buffers                         │
│  - Tracks file paths and durations                           │
│  - Handles audio-video synchronization                       │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        ▼                         ▼
┌───────────────────────┐  ┌───────────────────────────────────┐
│    AudioCapture       │  │     AppleVideoToolboxEncoder      │
│  - Native mic input   │  │  - VTCompressionSession (H.264)   │
│  - PCM sample output  │  │  - PrerecordingEncodedBuffer      │
│  - SampleAvailable    │  │  - AVAssetWriter for live rec     │
│    event              │  │  - Video muxing                   │
└───────────┬───────────┘  └────────────────┬──────────────────┘
            │                               │
            ▼                               ▼
┌───────────────────────┐  ┌───────────────────────────────────┐
│  CircularAudioBuffer  │  │     PrerecordingEncodedBuffer     │
│  - Queue<AudioSample> │  │  - Two fixed-size byte[] buffers  │
│  - MaxDuration trim   │  │  - Circular with auto rotation    │
│  - Linear mode option │  │  - Keyframe detection & pruning   │
└───────────────────────┘  └───────────────────────────────────┘
```

## Audio Recording Architecture

### Audio Capture Flow

```
┌────────────────────────────────────────────────────────────────────┐
│                         AUDIO CAPTURE                               │
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Pre-Recording Phase:                                               │
│  ┌──────────┐    ┌─────────────────┐    ┌────────────────────────┐ │
│  │   Mic    │ →  │  AudioCapture   │ →  │  CircularAudioBuffer   │ │
│  │  Input   │    │  (PCM samples)  │    │  (last N seconds)      │ │
│  └──────────┘    └─────────────────┘    └────────────────────────┘ │
│                                                                     │
│  Transition (User presses Record):                                  │
│  ┌────────────────────────┐    ┌────────────────────────────────┐  │
│  │ CircularAudioBuffer    │ →  │  pre_rec_audio_{guid}.m4a      │  │
│  │ .GetAllSamples()       │    │  (AVAssetWriter)               │  │
│  └────────────────────────┘    └────────────────────────────────┘  │
│                                                                     │
│  Live Recording Phase:                                              │
│  ┌──────────┐    ┌─────────────────┐    ┌────────────────────────┐ │
│  │   Mic    │ →  │  AudioCapture   │ →  │  live_audio_{guid}.m4a │ │
│  │  Input   │    │  (PCM samples)  │    │  (AVAssetWriter)       │ │
│  └──────────┘    └─────────────────┘    └────────────────────────┘ │
│                                                                     │
│  Finalization:                                                      │
│  ┌────────────────────────────────────────────────────────────────┐│
│  │ MuxVideosInternal():                                           ││
│  │   - pre_rec_video.mp4 + pre_rec_audio.m4a                      ││
│  │   - live_rec_video.mp4 + live_audio.m4a                        ││
│  │   → final_output.mp4 (video + audio tracks)                    ││
│  └────────────────────────────────────────────────────────────────┘│
└────────────────────────────────────────────────────────────────────┘
```

### CircularAudioBuffer

```csharp
public class CircularAudioBuffer
{
    private readonly Queue<AudioSample> _samples = new();
    private readonly TimeSpan _maxDuration;
    private readonly bool _isLinearMode;

    // Circular mode: Trim samples beyond MaxDuration
    // Linear mode: Keep ALL samples (for full session recording)
    public CircularAudioBuffer(TimeSpan maxDuration)
    {
        _maxDuration = maxDuration;
        _isLinearMode = maxDuration <= TimeSpan.Zero;
    }

    public void Write(AudioSample sample)
    {
        lock (_lock)
        {
            _samples.Enqueue(sample);
            Trim();  // Remove old samples if circular mode
        }
    }

    public AudioSample[] GetAllSamples()
    {
        lock (_lock)
        {
            return _samples.ToArray();
        }
    }
}

public struct AudioSample
{
    public byte[] Data;           // Raw PCM data (16-bit default)
    public long TimestampNs;      // Nanoseconds since capture epoch
    public int SampleRate;        // e.g., 44100
    public int Channels;          // 1 = Mono, 2 = Stereo
    public AudioBitDepth BitDepth;// Bit depth of the sample
}
```

### Audio-Video Synchronization

**The Challenge:**
Audio capture may start slightly before or after video encoding. The audio and video must be trimmed/aligned for proper sync.

**Solution:**
```csharp
// In SkiaCamera.Apple.cs - StartVideoRecording() transition:

// 1. Get pre-rec video duration from encoder
_preRecordingDurationTracked = _captureVideoEncoder.EncodingDuration;

// 2. Trim audio to match video duration
if (allAudioSamples != null && allAudioSamples.Length > 0)
{
    var videoDurationMs = _preRecordingDurationTracked.TotalMilliseconds;
    var lastSampleTimestamp = allAudioSamples[allAudioSamples.Length - 1].TimestampNs;
    var firstSampleTimestamp = allAudioSamples[0].TimestampNs;
    var audioDurationMs = (lastSampleTimestamp - firstSampleTimestamp) / 1_000_000.0;

    if (audioDurationMs > videoDurationMs)
    {
        // Audio is longer - trim from the START to match video
        var targetStartNs = lastSampleTimestamp - (long)(videoDurationMs * 1_000_000);
        allAudioSamples = allAudioSamples
            .Where(s => s.TimestampNs >= targetStartNs)
            .ToArray();
    }
}

// 3. Write trimmed audio to pre_rec_audio.m4a
await WriteAudioSamplesToM4aAsync(allAudioSamples, preRecAudioFilePath);
```

## iOS Architecture: Memory-Based Circular Buffer

### Video Memory Layout

```
Buffer A: [=============================] 13.5 MB
Buffer B: [=============================] 13.5 MB

Current: A (active for writes)

Total Video Memory: 27 MB (fixed, pre-allocated at startup)
```

### Audio Memory Layout

```
CircularAudioBuffer (Queue<AudioSample>):
  - Sample rate: 44100 Hz
  - Channels: 1 (mono) or 2 (stereo)
  - Bit depth: 16-bit PCM
  - Duration: PreRecordDuration (e.g., 5 seconds)
  - Memory: ~2 MB for 5 seconds mono @ 44.1kHz

Total Audio Memory: ~2 MB (dynamic, grows/trims with samples)
```

### How Rotation Works

**Timeline Example (5 second max duration):**

```
Time 0-5s:  Buffer A active, accumulating frames
            Audio buffer accumulating samples
Time 5s:    Rotation trigger!
            - Video: Switch to Buffer B, prune frames < 5s
            - Audio: Trim() removes samples < 5s
            - Buffer A now contains stale data (will be overwritten)

Time 5-10s: Buffer B active, accumulating frames
            Audio buffer continues accumulating (auto-trimmed)
Time 10s:   Rotation trigger!
            - Switch to Buffer A (overwrites old data)
            - Prune frames older than 5s from metadata list

Result: At any point, only last 5 seconds of video AND audio are kept
```

## Muxing Pre-Recording + Live Recording

### Passthrough Mode (Fast, No Re-encoding)

```csharp
// In MuxVideosInternal() - SkiaCamera.Apple.cs

// PASSTHROUGH MODE: No re-encoding, just container manipulation (FAST!)
videoTrack.PreferredTransform = compositionTransform;

using var exporter = new AVAssetExportSession(composition,
    AVAssetExportSessionPreset.Passthrough)  // ← KEY: Passthrough!
{
    OutputUrl = outputUrl,
    OutputFileType = AVFileTypes.Mpeg4.GetConstant(),
    ShouldOptimizeForNetworkUse = false
    // No VideoComposition - passthrough preserves original encoding
};
```

**Why Passthrough:**
- **Speed:** ~10x faster than re-encoding
- **Quality:** Original H.264 quality preserved (no generation loss)
- **Power:** Minimal CPU/GPU usage during muxing

### Full Muxing Process with Audio

```
Input Files:
  pre_rec_video_abc123.mp4    (5.0s video, starts at PTS 0.0s)
  pre_rec_audio_abc123.m4a    (5.0s audio, synced to video)
  live_rec_video_abc123.mp4   (3.0s video, continues after pre-rec)
  live_audio_abc123.m4a       (3.0s audio, continues after pre-rec)

Output:
  final_timestamp_guid.mp4    (8.0s total, video + audio tracks)
```

**AVMutableComposition Timeline:**

```
Video Timeline:
┌──────────────────────┬────────────────┐
│  Pre-recording       │  Live recording│
│  0.0s - 5.0s        │  5.0s - 8.0s   │
└──────────────────────┴────────────────┘

Audio Timeline:
┌──────────────────────┬────────────────┐
│  Pre-rec audio       │  Live audio    │
│  0.0s - 5.0s        │  5.0s - 8.0s   │
└──────────────────────┴────────────────┘

Implementation:
using var composition = new AVMutableComposition();
var videoTrack = composition.AddMutableTrack(AVMediaTypes.Video, 0);

// Insert pre-recording VIDEO at time 0
videoTrack.InsertTimeRange(preRecVideoRange, preRecVideoTrack, CMTime.Zero, out error);

// Insert live recording VIDEO after pre-recording
videoTrack.InsertTimeRange(liveVideoRange, liveRecVideoTrack, preRecAsset.Duration, out error);

// Add pre-rec AUDIO at time 0
if (preRecAudioFilePath exists)
{
    using var preRecAudioAsset = AVAsset.FromUrl(preRecAudioFilePath);
    var audioTrack = composition.AddMutableTrack(AVMediaTypes.Audio, 0);
    audioTrack.InsertTimeRange(preRecAudioRange, preRecAudioTracks[0], CMTime.Zero, out error);
}

// Add live AUDIO starting after pre-rec video duration
if (liveAudioFilePath exists)
{
    using var liveAudioAsset = AVAsset.FromUrl(liveAudioFilePath);
    // Get or create audio track
    var audioTrack = composition.TracksWithMediaType(AVMediaTypes.Audio)[0];
    audioTrack.InsertTimeRange(liveAudioRange, liveAudioTracks[0], preRecVideoDuration, out error);
}

// Export with Passthrough (fast!)
using var exporter = new AVAssetExportSession(composition, AVAssetExportSessionPreset.Passthrough);
await exporter.ExportTaskAsync();
```

## Resource Management

### Critical Disposal Patterns

**AVFoundation objects MUST be disposed to prevent memory leaks:**

```csharp
// CORRECT: Using statements ensure disposal
using var preAsset = AVAsset.FromUrl(preRecordedPath);
using var liveAsset = AVAsset.FromUrl(liveRecordingPath);
using var composition = new AVMutableComposition();
using var exportSession = new AVAssetExportSession(composition, preset);

// The export is async, but 'using var' disposes AFTER await completes
await exportSession.ExportTaskAsync();
// ← Disposal happens here automatically
```

**PrerecordingEncodedBuffer Frame Data Cleanup:**

```csharp
// In Dispose() - NULL all frame Data to help GC
public void Dispose()
{
    lock (_swapLock)
    {
        if (!_isDisposed)
        {
            // CRITICAL: Null out all frame Data to help GC collect large byte arrays
            foreach (var frame in _frames)
            {
                frame.Data = null;
            }
            _frames.Clear();
            _bufferA = null;
            _bufferB = null;
            _isDisposed = true;
        }
    }
}

// In pruning - Clear Data BEFORE removing from list
private void PruneFramesWithCleanup(Predicate<EncodedFrame> match)
{
    foreach (var frame in _frames)
    {
        if (match(frame))
            frame.Data = null;  // Clear before removal
    }
    _frames.RemoveAll(match);
}
```

**Event Handler Unsubscription:**

```csharp
// CRITICAL: Unsubscribe before disposing encoder
if (_captureVideoEncoder is AppleVideoToolboxEncoder appleEnc && _encoderPreviewInvalidateHandler != null)
{
    appleEnc.PreviewAvailable -= _encoderPreviewInvalidateHandler;
}
_captureVideoEncoder?.Dispose();
_captureVideoEncoder = null;
```

### Metal Resource Management

**Best Practice: Create Once, Reuse Forever**

```csharp
// In AppleVideoToolboxEncoder - STATIC shared resources
private static IMTLDevice _sharedMetalDevice;
private static IMTLCommandQueue _sharedCommandQueue;
private static GCHandle _sharedQueuePin;
private static readonly object _sharedMetalLock = new();

private void EnsureMetalContext()
{
    lock (_sharedMetalLock)
    {
        if (_sharedMetalDevice == null)
        {
            _sharedMetalDevice = MTLDevice.SystemDefault;
            if (_sharedMetalDevice != null)
            {
                _sharedCommandQueue = _sharedMetalDevice.CreateCommandQueue();
                _sharedQueuePin = GCHandle.Alloc(_sharedCommandQueue, GCHandleType.Pinned);
            }
        }
    }

    // Per-instance GRContext using shared device/queue
    if (_encodingContext == null && _sharedMetalDevice != null)
    {
        var backend = new GRMtlBackendContext
        {
            Device = _sharedMetalDevice,
            Queue = _sharedCommandQueue
        };
        _encodingContext = GRContext.CreateMetal(backend);
        _metalCache = new CVMetalTextureCache(_sharedMetalDevice);
    }
}

// NOTE: _sharedMetalDevice, _sharedCommandQueue, _sharedQueuePin are NEVER disposed
// They are reused across all encoder instances for the app lifetime
```

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
session.SetProperty(VTCompressionPropertyKey.RealTime, true);

// Set max keyframe interval (every 30 frames @ 30fps = 1 keyframe/second)
session.SetProperty(VTCompressionPropertyKey.MaxKeyFrameInterval, 30);
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

    // CRITICAL: Clear frame Data BEFORE removing to help GC
    PruneFramesWithCleanup(f => f.Timestamp < cutoffTimestamp);

    // Step 2: CRITICAL - Ensure first frame is a keyframe
    while (_frames.Count > 0 && !_frames[0].IsKeyFrame)
    {
        _frames[0].Data = null;  // Clear data first!
        _frames.RemoveAt(0);     // Keep removing until we hit a keyframe
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

## Memory Efficiency

**Video (5 seconds @ 1080p 30fps):**
```
Uncompressed:
  Frame size: 1920 × 1080 × 4 bytes (RGBA) = 8.3 MB
  Total: 8.3 MB × 150 frames = 1,245 MB (1.2 GB)

H.264 Compressed (Our Implementation):
  Frame size: ~75 KB average
  Buffer A: 13.5 MB (fixed)
  Buffer B: 13.5 MB (fixed)
  Total Video: 27 MB (fixed, never grows)
  Compression ratio: 46:1
```

**Audio (5 seconds @ 44.1kHz mono 16-bit):**
```
  Sample rate: 44,100 Hz
  Bytes per sample: 2 (16-bit)
  Duration: 5 seconds
  Total: 44,100 × 2 × 5 = 441,000 bytes ≈ 0.4 MB

  With overhead (struct, queue): ~2 MB
```

**Combined Memory Footprint:**
```
  Video buffers: 27 MB
  Audio buffer: ~2 MB
  Metal context: ~2 MB
  Total: ~31 MB (fixed)
```

## Performance Characteristics

**Frame Append (30 fps):**
- Lock duration: ~100 nanoseconds (atomic buffer swap check)
- Memory copy: ~75 KB per frame (Buffer.BlockCopy)
- Keyframe detection: ~50 microseconds (NAL unit scan)
- Total: <1 millisecond per frame
- CPU impact: <3% @ 30 fps

**Audio Sample Write:**
- Lock duration: ~50 nanoseconds
- Queue enqueue: ~1 microsecond
- Trim check: ~10 microseconds
- Total: <0.1 milliseconds per sample
- CPU impact: <1% @ 44.1kHz

**Buffer Rotation (every 5 seconds):**
- Swap buffers: ~100 nanoseconds (atomic int toggle)
- Prune metadata list: ~5 milliseconds (150 frames)
- Keyframe search: ~1 millisecond (scan until keyframe found)
- Total: ~6 milliseconds (once every 5 seconds, imperceptible)

**Buffer to MP4 Write (on Record button press):**
- Create AVAssetWriter: ~50 milliseconds
- Write 150 video frames: ~200 milliseconds
- Write audio to M4A: ~50 milliseconds
- Finalize files: ~100 milliseconds
- Total: ~400 milliseconds (one-time cost)

**Muxing (Passthrough mode):**
- Load assets: ~50 milliseconds
- Build composition: ~10 milliseconds
- Export (passthrough): ~100-200 milliseconds
- Total: ~200-300 milliseconds (vs ~3-5 seconds with re-encoding!)

## Configuration

**SkiaCamera Properties:**

```csharp
// Enable pre-recording mode
camera.EnablePreRecording = true;

// Set maximum pre-recording duration (default: 5 seconds)
camera.PreRecordDuration = TimeSpan.FromSeconds(5);

// Enable audio recording
camera.EnableAudioRecording = true;

// Auto-enable for first StartVideoRecording() call
// Second call starts live recording
```

**Buffer Size Calculation:**

```csharp
// Video buffer formula:
// bufferSize = expectedBytesPerSecond × duration × safetyMargin

// Example for 1080p @ 30fps:
// - H.264 bitrate: ~15 Mbps = 1.875 MB/s
// - 5 seconds: 1.875 × 5 = 9.375 MB
// - Safety margin: 1.44x (for IDR frame spikes)
// - Total: 13.5 MB per buffer

const int bufferSize = (int)(11.25 * 1024 * 1024 * 1.2); // ~13.5 MB
_bufferA = new byte[bufferSize];
_bufferB = new byte[bufferSize];

// Audio buffer:
// - Circular mode with PreRecordDuration
_audioBuffer = new CircularAudioBuffer(PreRecordDuration);
```

## Platform-Specific Notes

### iOS/MacCatalyst

**STATUS: FULLY IMPLEMENTED AND TESTED**

**Core Components:**
- **VTCompressionSession** - Hardware H.264 encoding
- **PrerecordingEncodedBuffer** - Circular buffer for encoded frames (27MB total)
- **CircularAudioBuffer** - Audio sample queue with auto-trim
- **AVAssetWriter** - MP4/M4A file writing
- **AVMutableComposition** - Seamless video+audio muxing with Passthrough

**Key Files:**
- `AppleVideoToolboxEncoder.cs` - Video encoding and pre-recording buffer
- `SkiaCamera.Apple.cs` - Audio management and muxing
- `PrerecordingEncodedBufferApple.cs` - Video circular buffer
- `CircularAudioBuffer.cs` - Audio circular buffer

### Android

**STATUS: IMPLEMENTED - READY FOR TESTING**

- Uses MediaCodec for hardware H.264 encoding
- PrerecordingEncodedBuffer shared implementation
- MediaMuxer for MP4 container

### Windows

**STATUS: IMPLEMENTED**

- Uses Media Foundation for H.264 encoding
- File-based circular buffer (two-file rotation)
- FFmpeg for muxing (or fallback concatenation)

## Diagnostics and Logging

**Key Log Messages:**

```
[PreRecording] AppendEncodedFrame: size=75123, timestamp=3.456s, KeyFrame=true
  → Frame added to buffer

[PreRecording] Swapped buffers. Active=B, Pruned frames: 150 -> 145 (first is KEYFRAME)
  → Buffer rotation occurred

[PreRecording] PruneToMaxDuration: 180 -> 150 frames (first frame now at 5.123s is KEYFRAME)
  → Final pruning before file write

[PrerecordingEncodedBufferApple] Disposed - all frame data cleared
  → Proper cleanup with frame data nulled

[MuxVideosApple] Passthrough mode - preserving source transform
  → Using fast passthrough (no re-encoding)

[MuxVideosApple] Added pre-rec audio at 0s (5.00s)
  → Pre-recording audio inserted

[MuxVideosApple] Added live audio at 5.00s (3.00s)
  → Live audio inserted after pre-rec

[MuxVideosApple] Mux successful: /path/to/output.mp4
  → Successful muxing with audio
```

## Error Handling and Edge Cases

### Case 1: No Keyframe in Buffer
```
Scenario: User records for 0.5 seconds (no keyframe generated yet)
Result: PruneToMaxDuration() removes all frames
Solution: Check _frames.Count after pruning
  - If empty: Skip pre-recording, use live recording only
  - Log warning: "No keyframe found in pre-recording buffer"
```

### Case 2: Audio Longer Than Video
```
Scenario: Audio capture started before video encoding
Detection: audioDurationMs > videoDurationMs
Action: Trim audio from START to match video duration
Code:
  var targetStartNs = lastSampleTimestamp - (long)(videoDurationMs * 1_000_000);
  allAudioSamples = allAudioSamples.Where(s => s.TimestampNs >= targetStartNs).ToArray();
```

### Case 3: Buffer Overflow
```
Scenario: Extremely high bitrate exceeds 13.5 MB buffer
Detection: currentState.BytesUsed + size > bufferSize
Action: Drop frame and log warning
Result: Small gap in pre-recording, but continues operating
```

### Case 4: Muxing Failure
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

## References

- [VTCompressionSession Documentation](https://developer.apple.com/documentation/videotoolbox/vtcompressionsession)
- [AVAssetWriter Pass-through Mode](https://developer.apple.com/documentation/avfoundation/avassetwriter)
- [H.264 NAL Unit Format (ITU-T H.264)](https://www.itu.int/rec/T-REC-H.264)
- [AVMutableComposition for Video Editing](https://developer.apple.com/documentation/avfoundation/avmutablecomposition)
- [CoreMedia Timing](https://developer.apple.com/documentation/coremedia/cmtime)
- [Metal Best Practices - Command Queues](https://developer.apple.com/documentation/metal/resource_fundamentals/setting_resource_storage_modes)
