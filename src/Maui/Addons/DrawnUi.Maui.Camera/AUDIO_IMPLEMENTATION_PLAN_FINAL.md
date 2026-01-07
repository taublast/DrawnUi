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

**File:** `Shared/AudioBitDepth.cs`
```csharp
namespace DrawnUi.Camera.Shared
{
    /// <summary>
    /// Supported audio bit depths for recording.
    /// </summary>
    public enum AudioBitDepth
    {
        Pcm8Bit = 8,      // Low quality, smallest size
        Pcm16Bit = 16,    // Default - good quality, standard size
        Pcm24Bit = 24,    // High quality, larger size
        Float32Bit = 32   // Professional quality, largest size
    }
}
```

**File:** `Shared/AudioSample.cs`
```csharp
namespace DrawnUi.Camera.Shared
{
    public struct AudioSample
    {
        public byte[] Data;           // Raw PCM data
        public long TimestampNs;      // Nanoseconds since capture epoch
        public int SampleRate;        // e.g., 44100
        public int Channels;          // 1 = Mono, 2 = Stereo
        public AudioBitDepth BitDepth; // Bit depth of the sample

        public int BytesPerSample => BitDepth switch
        {
            AudioBitDepth.Pcm8Bit => 1,
            AudioBitDepth.Pcm16Bit => 2,
            AudioBitDepth.Pcm24Bit => 3,
            AudioBitDepth.Float32Bit => 4,
            _ => 2
        };

        public TimeSpan Timestamp => TimeSpan.FromTicks(TimestampNs / 100);
        public int SampleCount => Data.Length / (Channels * BytesPerSample);
    }
}
```

**Platform Bit Depth Support:**
| Platform | 8-bit | 16-bit | 24-bit | 32-bit float |
|----------|-------|--------|--------|--------------|
| Android | `Encoding.Pcm8Bit` | `Encoding.Pcm16Bit` | ❌ | `Encoding.PcmFloat` |
| iOS | `AVAudioCommonFormat` | ✅ All formats supported | ✅ | ✅ |
| Windows | `MF_MT_AUDIO_BITS_PER_SAMPLE` | ✅ All formats supported | ✅ | ✅ |

**Note:** Android does not natively support 24-bit; use 32-bit float and convert if needed.

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

### 2.4 Pre-Recording Audio Flow

The audio circular buffer works in tandem with the video pre-recording buffer. Here's the detailed flow:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        PRE-RECORDING PHASE                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Microphone ──► AudioCapture ──► CircularAudioBuffer (5s rolling window)   │
│                                         │                                   │
│  Camera ─────► VideoEncoder ──► PrerecordingEncodedBuffer (5s keyframes)   │
│                                         │                                   │
│                              [Both buffers filling continuously]            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ User presses RECORD
┌─────────────────────────────────────────────────────────────────────────────┐
│                        TRANSITION PHASE                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  1. LOCK both buffers (stop accepting new samples momentarily)             │
│  2. Find video keyframe closest to (Now - PreRecordDuration)               │
│  3. Find audio sample closest to that keyframe timestamp                   │
│  4. DRAIN video buffer → write to encoder/muxer                            │
│  5. DRAIN audio buffer → write to encoder/muxer (aligned timestamps)       │
│  6. UNLOCK and switch to LIVE mode                                         │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        LIVE RECORDING PHASE                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Microphone ──► AudioCapture ──► [Direct to Encoder] ──► Muxer ──► File    │
│                                                              │              │
│  Camera ─────► VideoEncoder ──► [Direct to Encoder] ────────┘              │
│                                                                             │
│                     [No more buffering, direct write]                       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Detailed Buffer Implementation:**
```csharp
public class CircularAudioBuffer
{
    private readonly Queue<AudioSample> _samples = new();
    private readonly object _lock = new();
    private readonly TimeSpan _maxDuration;
    private long _oldestTimestampNs;
    private long _newestTimestampNs;

    public CircularAudioBuffer(TimeSpan maxDuration)
    {
        _maxDuration = maxDuration;
    }

    /// <summary>
    /// Called from microphone capture thread. Thread-safe.
    /// Automatically trims samples older than MaxDuration.
    /// </summary>
    public void Write(AudioSample sample)
    {
        lock (_lock)
        {
            _samples.Enqueue(sample);
            _newestTimestampNs = sample.TimestampNs;

            // Trim old samples beyond max duration
            var cutoffNs = _newestTimestampNs - (long)(_maxDuration.TotalSeconds * 1_000_000_000);
            while (_samples.Count > 0 && _samples.Peek().TimestampNs < cutoffNs)
            {
                _samples.Dequeue();
            }

            if (_samples.Count > 0)
                _oldestTimestampNs = _samples.Peek().TimestampNs;
        }
    }

    /// <summary>
    /// Called at pre-recording → live transition.
    /// Returns all samples from cutPointNs onwards, then clears buffer.
    /// </summary>
    public AudioSample[] DrainFrom(long cutPointNs)
    {
        lock (_lock)
        {
            var result = _samples
                .Where(s => s.TimestampNs >= cutPointNs)
                .OrderBy(s => s.TimestampNs)
                .ToArray();

            _samples.Clear();
            return result;
        }
    }

    /// <summary>
    /// Find the timestamp of the sample closest to the target.
    /// Used to align with video keyframe.
    /// </summary>
    public long GetSampleTimestampClosestTo(long targetNs)
    {
        lock (_lock)
        {
            if (_samples.Count == 0) return targetNs;

            return _samples
                .OrderBy(s => Math.Abs(s.TimestampNs - targetNs))
                .First()
                .TimestampNs;
        }
    }

    public TimeSpan BufferedDuration => TimeSpan.FromTicks((_newestTimestampNs - _oldestTimestampNs) / 100);
    public int SampleCount { get { lock (_lock) return _samples.Count; } }
    public void Clear() { lock (_lock) _samples.Clear(); }
}
```

**Transition Coordination (in SkiaCamera):**
```csharp
private async Task TransitionToLiveRecording()
{
    // 1. Calculate cut point aligned with video keyframe
    var (videoStartNs, audioStartNs) = PreRecordingTransition.CalculateCutPoint(
        _videoPreRecordingBuffer,
        _audioBuffer,
        PreRecordDuration
    );

    // 2. Drain and write video buffer (existing logic)
    await _videoEncoder.DrainPreRecordingBuffer(videoStartNs);

    // 3. Drain and write audio buffer (aligned with video)
    var audioSamples = _audioBuffer.DrainFrom(audioStartNs);
    foreach (var sample in audioSamples)
    {
        // Adjust timestamp to be relative to video start (t=0 in output file)
        var adjustedSample = sample;
        adjustedSample.TimestampNs -= videoStartNs;
        _audioEncoder.WriteAudioSample(adjustedSample);
    }

    // 4. Switch to live mode - direct write, no buffering
    _isPreRecording = false;
    _isLiveRecording = true;
}
```

### 2.5 Unified Timestamp Strategy (Solving the Drift Problem)

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

### 2.6 Audio Device Selection

Just like video cameras, the user must be able to select which microphone to use (e.g., internal mic, headset, USB mic).

**Shared API:**
```csharp
// In SkiaCamera.Shared.cs

/// <summary>
/// Index of the audio device to use.
/// -1 = System Default (auto-select)
/// 0+ = Index in the list returned by GetAvailableAudioDevicesAsync
/// </summary>
public static readonly BindableProperty AudioDeviceIndexProperty = BindableProperty.Create(
    nameof(AudioDeviceIndex), typeof(int), typeof(SkiaCamera), -1);

public int AudioDeviceIndex
{
    get => (int)GetValue(AudioDeviceIndexProperty);
    set => SetValue(AudioDeviceIndexProperty, value);
}

/// <summary>
/// Returns list of available audio input devices.
/// </summary>
public Task<List<string>> GetAvailableAudioDevicesAsync();
```

**Platform Implementation:**
*   **Windows:** Use `DeviceInformation.FindAllAsync(DeviceClass.AudioCapture)` to list devices. Map selection index to `DeviceInformation.Id` when initializing `MediaCapture`.
*   **Android:** Use `AudioManager.GetDevices(GetDevicesTargets.Inputs)` (API 23+) or iteration. When starting `AudioRecord` or `MediaRecorder`, specify the source. Note: Android audio routing is complex; usually preferring `AudioSource.Mic` or `AudioSource.Camcorder` and letting OS handle routing is safest, but explicit device selection is possible via `setPreferredDevice`.
*   **iOS:** Use `AVAudioSession.SharedInstance().AvailableInputs`. Set `AVAudioSession.SharedInstance().SetPreferredInput()` based on selection.

### 2.7 Audio Codec Selection

Users may want to choose a specific audio codec (e.g., AAC, MP3, FLAC, PCM) or use the system default.

**Shared API:**
```csharp
// In SkiaCamera.Shared.cs

/// <summary>
/// Index of the audio codec to use.
/// -1 = System Default (AAC or platform preferred)
/// 0+ = Index in the list returned by GetAvailableAudioCodecsAsync
/// </summary>
public static readonly BindableProperty AudioCodecIndexProperty = BindableProperty.Create(
    nameof(AudioCodecIndex), typeof(int), typeof(SkiaCamera), -1);

public int AudioCodecIndex
{
    get => (int)GetValue(AudioCodecIndexProperty);
    set => SetValue(AudioCodecIndexProperty, value);
}

/// <summary>
/// Returns list of available audio encoder codecs.
/// </summary>
public Task<List<string>> GetAvailableAudioCodecsAsync();
```

**Platform Implementation:**
*   **Windows:** Use `CodecQuery.FindAllAsync(CodecKind.Audio, CodecCategory.Encoder, null)`. Return `DisplayName`.
*   **Android:** Use `MediaCodecList` to find encoders supporting audio types (AAC, AMR, FLAC in container).
*   **iOS:** AVAssetWriter supported file types (usually tied to container, but can specify `AudioSettings`).

---

## 3. Interface Updates

**File:** `SkiaCamera.Shared.cs`
```csharp
// Existing property (line 154-167)
public bool RecordAudio { get; set; }

// NEW: Audio configuration properties
public AudioBitDepth AudioBitDepth { get; set; } = AudioBitDepth.Pcm16Bit;
public int AudioSampleRate { get; set; } = 44100;        // 44100, 48000, etc.
public int AudioChannels { get; set; } = 1;              // 1 = Mono, 2 = Stereo

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
        AudioBitDepth BitDepth { get; }

        event EventHandler<AudioSample> SampleAvailable;

        Task<bool> StartAsync(
            int sampleRate = 44100,
            int channels = 1,
            AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit);
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
6.  Add `GetAvailableAudioDevicesAsync` and `AudioDeviceIndex` to `SkiaCamera.Shared.cs`.

### Phase 2: iOS/macOS Implementation
1.  Extend `SetupAudioInput()` with `AVCaptureAudioDataOutput`.
2.  Implement audio sample delegate.
3.  Add audio track to `AVAssetWriter` in encoder.
4.  Implement pre-recording audio buffer drain.
5.  Implement `GetAvailableAudioDevicesAsync` and selection.
6.  Test live + pre-recording modes.

### Phase 3: Android Implementation
1.  Add `RECORD_AUDIO` permission handling.
2.  Implement `AudioRecord` capture thread.
3.  Implement `MediaCodec` AAC encoder.
4.  Add `AudioTimestamp` logic.
5.  Implement `GetAvailableAudioDevicesAsync` and selection.
6.  Test live + pre-recording modes.

### Phase 4: Windows Implementation (Live Only)
1.  Add microphone capability.
2.  Verify `MediaCapture` audio initialization.
3.  Implement `GetAvailableAudioDevicesAsync` and selection.
4.  Add audio stream to `IMFSinkWriter`.
5.  Test live recording mode.

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
| `Shared/AudioBitDepth.cs` | Enum for supported bit depths (8/16/24/32-bit) |
| `Shared/AudioSample.cs` | Cross-platform audio data structure |
| `Shared/CircularAudioBuffer.cs` | Thread-safe pre-recording buffer with drain logic |
| `Interfaces/IAudioCapture.cs` | Platform audio capture interface |

### Modified Files
| File | Changes |
|------|---------|
| `SkiaCamera.Shared.cs` | Capture epoch, audio buffer coordination, error handling, AudioBitDepth/SampleRate/Channels properties |
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
