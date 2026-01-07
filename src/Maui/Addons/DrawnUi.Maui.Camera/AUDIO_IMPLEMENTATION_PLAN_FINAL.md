# Comprehensive Audio Recording Implementation Plan
## Live & Pre-Recording for iOS, Android, Windows

---

## 1. Executive Summary

This plan adds audio recording support to the **Capture Video Flow** (frame-by-frame encoding with overlay support) for both **Live Recording** and **Pre-Recording** modes across all platforms.

**Key Challenge:** The capture video flow encodes video frames individually with overlays. Audio must be captured separately, buffered for pre-recording, and muxed with synchronized timestamps.

**Target Platforms:**
- **iOS/macOS:** AVFoundation (AVCaptureSession, AVAssetWriter)
- **Android:** AudioRecord, MediaCodec (AAC), MediaMuxer
- **Windows:** MediaCapture + IMFSinkWriter (Live first, Pre-rec Phase 2)

---

## 2. Shared Architecture

### 2.1 Core Principle
Audio cannot be piped directly to file encoder for pre-recording. We must:
1. Capture raw PCM samples continuously.
2. Store in circular memory buffer (for pre-recording).
3. Encode to AAC and mux with video when recording starts.

### 2.2 Shared Data Structures

**File:** `Shared/AudioSample.cs`
```csharp
namespace DrawnUi.Camera.Shared
{
    public struct AudioSample
    {
        public byte[] Data;           // Raw PCM data (16-bit)
        public long TimestampNs;      // Nanoseconds since capture epoch
        public int SampleRate;        // e.g., 44100
        public int Channels;          // 1 = Mono, 2 = Stereo
        public int BytesPerSample;    // 2 for 16-bit PCM

        public TimeSpan Timestamp => TimeSpan.FromTicks(TimestampNs / 100);
        public int SampleCount => Data.Length / (Channels * BytesPerSample);
    }
}
```

### 2.3 Shared Circular Audio Buffer

**File:** `Shared/CircularAudioBuffer.cs`
```csharp
namespace DrawnUi.Camera.Shared
{
    /// <summary>
    /// Thread-safe circular buffer for audio samples.
    /// Automatically trims old samples beyond MaxDuration.
    /// </summary>
    public class CircularAudioBuffer
    {
        private readonly Queue<AudioSample> _samples = new();
        private readonly object _lock = new();
        private readonly TimeSpan _maxDuration;

        public CircularAudioBuffer(TimeSpan maxDuration)
        {
            _maxDuration = maxDuration;
        }

        public void Write(AudioSample sample);           // Called from mic thread
        public AudioSample[] DrainFrom(long cutPointNs); // Called at recording start
        public void Clear();
        public TimeSpan BufferedDuration { get; }
        public int SampleCount { get; }
    }
}
```

**Memory footprint:** ~1MB for 5 seconds of 44.1kHz mono 16-bit PCM (negligible).

### 2.4 Unified Timestamp Strategy (Solving the Drift Problem)

Audio and video run on different clocks. This drift is the #1 risk.
- **Video:** ~33ms intervals (Variable; System/Stopwatch).
- **Audio:** ~23ms intervals (Fixed; Hardware Clock).

**Solution: Shared Epoch Timestamp**

```csharp
// In SkiaCamera - set when camera starts
private long _captureEpochNs;

public void StartCapture()
{
    // Use High-Resolution Stopwatch, normalized to System Nanoseconds
    _captureEpochNs = Stopwatch.GetTimestamp() * (1_000_000_000L / Stopwatch.Frequency);
}
```

**Platform Drift Correction:**
*   **Android:** Use `AudioTimestamp` API where available, or fallback to `System.NanoTime()` on `Read()` entry, rather than `Stopwatch.GetTimestamp()` after the fact.
*   **iOS:** `CMSampleBuffer.PresentationTimeStamp` is relative to system boot (Mach Time). We must map this to our `Stopwatch` baseline by capturing the Mach Time offset at start.
*   **Windows:** `QPC` (QueryPerformanceCounter) is standard.

**iOS Mach Time Offset Implementation:**
```csharp
// In SkiaCamera.Apple.cs - capture offset when camera starts
private long _machTimeOffsetNs;

public void StartCapture()
{
    // Capture both clocks simultaneously
    var stopwatchNs = Stopwatch.GetTimestamp() * (1_000_000_000L / Stopwatch.Frequency);
    var machTimeNs = GetMachAbsoluteTimeNs(); // Platform call to mach_absolute_time()

    _machTimeOffsetNs = stopwatchNs - machTimeNs;
    _captureEpochNs = stopwatchNs;
}

// When converting CMSampleBuffer timestamp:
private long ConvertMachTimeToEpoch(CMTime presentationTime)
{
    long machTimeNs = (long)(presentationTime.Seconds * 1_000_000_000);
    return machTimeNs + _machTimeOffsetNs - _captureEpochNs;
}
```

---

## 3. Interface Updates

**File:** `SkiaCamera.Shared.cs`
```csharp
// Existing property (line 154-167)
public bool RecordAudio { get; set; }

// Add audio capture coordination
private IAudioCapture _audioCapture;
private CircularAudioBuffer _audioBuffer;
```

**File:** `Interfaces/IAudioCapture.cs` (NEW)
```csharp
namespace DrawnUi.Camera.Interfaces
{
    public interface IAudioCapture : IDisposable
    {
        bool IsCapturing { get; }
        int SampleRate { get; }
        int Channels { get; }

        event EventHandler<AudioSample> SampleAvailable;

        Task<bool> StartAsync(int sampleRate = 44100, int channels = 1);
        Task StopAsync();
    }
}
```

**File:** `Interfaces/ICaptureVideoEncoder.cs` - Add:
```csharp
// Existing
Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio);

// Add for audio support
void SetAudioBuffer(CircularAudioBuffer buffer);
void WriteAudioSample(AudioSample sample);
bool SupportsAudio { get; }
```

---

## 4. Platform Implementations

### 4.1 iOS/macOS (AVFoundation)

**Files to modify:**
- `Apple/NativeCamera.Apple.cs`
- `Apple/AppleVideoToolboxEncoder.cs`
- `Apple/SkiaCamera.Apple.cs`

**Specific Nuance:**
iOS `CMSampleBuffer` timestamps are heavily optimized for AVFoundation playback. For pre-recording stitching, we must ensure we don't accidentally "reset" the clock when switching from buffered samples to live samples.

**Audio Capture Setup:**
```csharp
// In NativeCamera.Apple.cs - extend existing SetupAudioInput()
private AVCaptureAudioDataOutput _audioDataOutput;
private AudioSampleDelegate _audioDelegate;

private async Task SetupAudioCapture()
{
    // Existing code gets audio device and creates _audioInput

    // ADD: Audio data output for sample access
    _audioDataOutput = new AVCaptureAudioDataOutput();
    _audioDelegate = new AudioSampleDelegate(OnAudioSampleReceived);
    _audioDataOutput.SetSampleBufferDelegate(_audioDelegate,
        new DispatchQueue("AudioCaptureQueue"));

    if (_session.CanAddOutput(_audioDataOutput))
    {
        _session.AddOutput(_audioDataOutput);
    }
}

private void OnAudioSampleReceived(CMSampleBuffer sampleBuffer)
{
    if (_isPreRecording)
    {
        // Convert CMSampleBuffer to AudioSample, add to circular buffer
        var audioSample = ConvertToAudioSample(sampleBuffer);
        _audioBuffer.Write(audioSample);
    }
    else if (_isRecording)
    {
        // Pass directly to encoder
        _encoder.WriteAudioSampleBuffer(sampleBuffer);
    }
}
```

**Code for Format:** `AAC, 44.1kHz, 128kbps` via `AVAssetWriterInput`.

### 4.2 Android (MediaCodec)

**Files to modify:**
- `Platforms/Android/NativeCamera.Android.cs`
- `Platforms/Android/AndroidCaptureVideoEncoder.cs`
- `Platforms/Android/SkiaCamera.Android.cs`

**Specific Nuance:**
`AudioRecord.Read()` is blocking. We must run this on a dedicated thread.
Use `AudioTimestamp` API (Android N+) to get accurate capture times, rather than just `System.NanoTime()`, to avoid jitter.

**Audio Capture (AudioRecord):**
```csharp
// In NativeCamera.Android.cs
private AudioRecord _audioRecord;
private Thread _audioThread;
private volatile bool _captureAudio;

private const int SAMPLE_RATE = 44100;
private const ChannelIn CHANNEL_CONFIG = ChannelIn.Mono;
private const Encoding AUDIO_FORMAT = Encoding.Pcm16bit;

public void StartAudioCapture()
{
    // ... Initialization (min buffer size check) ...
    _audioRecord = new AudioRecord(AudioSource.Mic, SAMPLE_RATE, CHANNEL_CONFIG, AUDIO_FORMAT, bufferSize);
    
    _captureAudio = true;
    _audioThread = new Thread(AudioCaptureLoop) { IsBackground = true };
    _audioThread.Start();
}

private void AudioCaptureLoop()
{
    var buffer = new byte[2048]; // ~23ms of audio
    _audioRecord.StartRecording();

    // Use AudioTimestamp for high-precision
    var timestamp = new AudioTimestamp();

    while (_captureAudio)
    {
        int bytesRead = _audioRecord.Read(buffer, 0, buffer.Length);
        if (bytesRead > 0)
        {
             // Try get hardware timestamp, else fallback to system
             long timeNs = (_audioRecord.GetTimestamp(timestamp, AudioTimestampTimebase.Monotonic) == AudioApi.Success)
                 ? timestamp.NanoTime
                 : Java.Lang.System.NanoTime();

             // Ensure timeNs is relative to our Shared Epoch if necessary, or just store raw Monotonic
             // and offset it during Encoder write.

            var sample = new AudioSample
            {
                Data = buffer.Take(bytesRead).ToArray(),
                TimestampNs = timeNs,
                SampleRate = SAMPLE_RATE,
                /* ... */
            };

            OnAudioSampleCaptured(sample);
        }
    }
}
```

**Performance Note:**
`buffer.Take(bytesRead).ToArray()` creates allocations on every read (~43 times/second). For high-performance scenarios, consider:
- Buffer pooling with `ArrayPool<byte>.Shared`
- Reusing `AudioSample` instances
- Pre-allocated ring buffer for samples

**Permissions Required:**
`<uses-permission android:name="android.permission.RECORD_AUDIO" />`

### 4.3 Windows (Phased Approach)

**Phase 1: Live Recording Only**
Windows pre-recording with audio is complex. Implement live recording first.

**Files to modify:**
- `Platforms/Windows/NativeCamera.Windows.cs`
- `Platforms/Windows/WindowsCaptureVideoEncoder.cs`

**Implementation:**
Use `MediaCapture` configured for `AudioAndVideo`.
Pipe audio samples to `IMFSinkWriter` on a new stream index.
**Important:** Timestamps must be converted to `100-nanosecond units` (HNS).

**Phase 2: Pre-Recording (Future Work)**
Windows pre-recording with audio will require one of:
- `MediaFrameReader` for audio frames + custom `MediaStreamSource` to replay buffers
- Separate audio file recording + post-process merge with FFmpeg/MediaComposition
- Accept video-only pre-recording on Windows initially

This complexity is deferred to a future iteration.

---

## 5. Pre-Recording Cut-Point Logic

When transitioning from pre-recording to live recording:

```csharp
public class PreRecordingTransition
{
    /// <summary>
    /// Calculates the optimal cut point aligning video keyframe with audio.
    /// </summary>
    public static (long videoStartNs, long audioStartNs) CalculateCutPoint(
        PrerecordingEncodedBuffer videoBuffer,
        CircularAudioBuffer audioBuffer,
        TimeSpan requestedDuration)
    {
        // Logic:
        // 1. Target Cut Time = Now - Duration
        // 2. Find Keyframe >= Target
        // 3. Find Audio Sample closest to Keyframe
        // 4. Return tuple for drain start.
    }
}
```

---

## 6. Implementation Phases

### Phase 1: Infrastructure (All Platforms)
1.  Create `Shared/AudioSample.cs`.
2.  Create `Shared/CircularAudioBuffer.cs`.
3.  Create `Interfaces/IAudioCapture.cs`.
4.  Add audio-related methods to `ICaptureVideoEncoder`.
5.  Add `_captureEpochNs` timestamp to `SkiaCamera.Shared.cs`.

### Phase 2: iOS/macOS Implementation
1.  Extend `SetupAudioInput()` with `AVCaptureAudioDataOutput`.
2.  Implement audio sample delegate.
3.  Add audio track to `AVAssetWriter` in encoder.
4.  Implement pre-recording audio buffer drain.
5.  Test live + pre-recording modes.

### Phase 3: Android Implementation
1.  Add `RECORD_AUDIO` permission handling.
2.  Implement `AudioRecord` capture thread.
3.  Implement `MediaCodec` AAC encoder.
4.  Add `AudioTimestamp` logic.
5.  Test live + pre-recording modes.

### Phase 4: Windows Implementation (Live Only)
1.  Add microphone capability.
2.  Verify `MediaCapture` audio initialization.
3.  Add audio stream to `IMFSinkWriter`.
4.  Test live recording mode.

---

## 7. Testing Checklist

### Verification
-   **Clapperboard Test:** Physically clap hands in front of camera. Verify the sound spike aligns exactly with the frames where hands touch in the resulting video player.
-   **Drift Test:** Record for > 5 minutes. Ensure audio does not slowly desync from video (common if clocks differ by 0.1%).
-   **Pre-Record Bound:** Shout "NOW" and press record immediately. Ensure the shout is captured in the pre-recording buffer.

### Per Platform
-   [ ] Live recording captures audio.
-   [ ] Pre-recording buffer holds audio (iOS/Android).
-   [ ] A/V sync within 50ms tolerance.
-   [ ] `RecordAudio = false` produces silent video.
-   [ ] Memory stays bounded.
-   [ ] Permission denied handled gracefully.
-   [ ] Long recording (>5 min) stability.

---

## 8. Error Handling

```csharp
public enum AudioCaptureError
{
    None,
    PermissionDenied,
    NoAudioDevice,
    InitializationFailed,
    CaptureInterrupted
}
```

**Graceful Fallback Strategy:**
1. **Permission denied** → Record video only, log warning, notify user if UI available
2. **No audio device** → Record video only, log warning
3. **Capture fails mid-recording** → Continue video, mark audio track as incomplete
4. **Encoder fails** → Attempt recovery once, then fallback to video-only

**Implementation:**
```csharp
// In SkiaCamera - wrap audio initialization
private async Task<bool> TryInitializeAudio()
{
    try
    {
        if (!RecordAudio) return false;

        var hasPermission = await CheckMicrophonePermissionAsync();
        if (!hasPermission)
        {
            Debug.WriteLine("[Audio] Permission denied - recording video only");
            return false;
        }

        await _audioCapture.StartAsync();
        return true;
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Audio] Init failed: {ex.Message} - recording video only");
        return false;
    }
}
```

---

## 9. File Changes Summary

### New Files
| File | Purpose |
|------|---------|
| `Shared/AudioSample.cs` | Cross-platform audio data structure |
| `Shared/CircularAudioBuffer.cs` | Thread-safe pre-recording buffer |
| `Interfaces/IAudioCapture.cs` | Platform audio capture interface |

### Modified Files
| File | Changes |
|------|---------|
| `SkiaCamera.Shared.cs` | Capture epoch, audio buffer coordination, error handling |
| `Interfaces/ICaptureVideoEncoder.cs` | Audio support methods |
| `Apple/NativeCamera.Apple.cs` | AVCaptureAudioDataOutput setup, Mach time offset |
| `Apple/AppleVideoToolboxEncoder.cs` | AVAssetWriterInput for audio track |
| `Apple/SkiaCamera.Apple.cs` | Audio capture coordination |
| `Platforms/Android/NativeCamera.Android.cs` | AudioRecord capture thread |
| `Platforms/Android/AndroidCaptureVideoEncoder.cs` | MediaCodec AAC encoder + muxer |
| `Platforms/Android/SkiaCamera.Android.cs` | Audio coordination |
| `Platforms/Windows/WindowsCaptureVideoEncoder.cs` | IMFSinkWriter audio stream |
| `Platforms/Windows/SkiaCamera.Windows.cs` | Audio coordination |

### App Permissions (Consuming Apps)
| Platform | File | Addition |
|----------|------|----------|
| iOS | `Info.plist` | Already has `NSMicrophoneUsageDescription` |
| Android | `AndroidManifest.xml` | `<uses-permission android:name="android.permission.RECORD_AUDIO" />` |
| Windows | `Package.appxmanifest` | `<DeviceCapability Name="microphone" />`|
