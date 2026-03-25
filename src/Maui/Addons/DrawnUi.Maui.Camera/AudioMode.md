# CameraAudioMode Implementation Plan

## Problem
On iOS, `AudioCaptureApple` (used for preview/monitoring audio) hardcodes `SetVoiceProcessingEnabled(true)` on `AVAudioEngine.InputNode`, which applies AGC, echo cancellation, and noise suppression. This makes recorded audio sound "voice-processed" compared to the native iOS Camera app, which uses `VideoRecording` mode with no voice processing.

## Solution
Add a `CameraAudioMode` property to `SkiaCamera` that controls how the audio session is configured on each platform.

## Enum Values

| Value | iOS | Android (TODO) | Description |
|-------|-----|----------------|-------------|
| `Default` | `AVAudioSessionModeDefault`, no voice processing | `AudioSource.Mic` | Standard system audio, light processing. **Default value.** |
| `VideoRecording` | `AVAudioSessionModeVideoRecording`, no voice processing | `AudioSource.Camcorder` | Matches native Camera app. |
| `Voice` | `AVAudioSessionModeDefault` + `SetVoiceProcessingEnabled(true)` | `AudioSource.VoiceCommunication` | AGC, echo cancellation, noise suppression |
| `Flat` | `AVAudioSessionModeMeasurement`, no voice processing | `AudioSource.Unprocessed` (API 29+, else `Mic`) | Minimal processing, raw signal |

## Files Changed

### Cross-platform
- `Enums/CameraAudioMode.cs` — new enum
- `Interfaces/IAudioCapture.cs` — added `CameraAudioMode AudioMode { get; set; }`
- `Interfaces/INativeCamera.cs` — added `void SetAudioMode(CameraAudioMode mode)`
- `SkiaCamera.cs` — added `AudioMode` bindable property (default `VideoRecording`); synced to `NativeControl` and preview audio capture instances

### iOS
- `Apple/AudioCapture.Apple.cs` — reads `AudioMode` to set `AVAudioSession` mode and toggle `SetVoiceProcessingEnabled`
- `Apple/NativeCamera.Apple.cs` — stores audio mode, applies correct `AVAudioSession` mode in video recording setup

### Stubs (implementation TODO)
- `Platforms/Android/NativeCamera.Android.cs` — `SetAudioMode` stub (NativeCamera path, not used for realtime recording)
- `Platforms/Windows/NativeCamera.Windows.cs` — `SetAudioMode` stub

### Android (implemented)
- `Platforms/Android/AudioCaptureAndroid.cs` — `AudioSource` selected based on `AudioMode`:
  - `Default` → `AudioSource.Mic`
  - `VideoRecording` → `AudioSource.Camcorder`
  - `Voice` → `AudioSource.VoiceCommunication`
  - `Flat` → `AudioSource.Unprocessed` (API 29+/Q+), fallback to `AudioSource.Mic`
