# Pre-Recording Feature

The pre-recording feature, also known as "look-back" recording, allows you to capture video footage starting from a few seconds *before* the record button is pressed. This is incredibly useful for capturing spontaneous moments without missing the beginning of the action.

## How it Works

When the pre-recording feature is enabled, the camera doesn't just display a live preview; it actively captures video frames into a temporary, in-memory rolling buffer. This buffer is designed to hold a fixed duration of the most recent video data (e.g., the last 5 seconds).

When you start a recording:
1. The content of this pre-record buffer is immediately written to the beginning of the final video file.
2. The recording then continues seamlessly by appending the live camera feed to the same file.

This is achieved by using more advanced, platform-specific APIs that provide frame-by-frame control over video encoding and file writing, rather than a simple start-to-finish recording method.

## How to Use

You can control the feature using two simple properties on the `SkiaCamera` control:

- **`EnablePreRecording` (bool):** Set this to `true` to activate the pre-recording functionality. The default value is `false`.
- **`PreRecordDuration` (TimeSpan):** This property determines the duration of the look-back period. The default is 5 seconds.

### Example in XAML
```xml
<camera:SkiaCamera
    EnablePreRecording="True"
    PreRecordDuration="0:0:5" />
```

### Example in C#
```csharp
var myCamera = new SkiaCamera
{
    EnablePreRecording = true,
    PreRecordDuration = TimeSpan.FromSeconds(5)
};
```

When you call `myCamera.StartVideoRecording()`, the resulting video file will automatically include the footage from the specified duration before the call was made.

## Implementation Details & Current Status

The implementation of this feature is platform-specific. Here is the current status:

- **iOS / MacCatalyst:**
    - **Status:** **Implemented and functional.**
    - **Method:** The implementation uses `AVAssetWriter` to manually append `CMSampleBuffer` frames to a video file. A `Queue<CMSampleBuffer>` serves as the rolling buffer to store recent frames. When recording starts, these buffered frames are written first, followed by the live frames from the camera.

- **Android:**
    - **Status:** **Not yet implemented.**
    - **Plan:** A similar architecture will be required, likely using `MediaCodec` for encoding and `MediaMuxer` for writing frames to a file. A rolling buffer will be maintained to store frame data before the recording is initiated.

- **Windows:**
    - **Status:** **Not yet implemented.**
    - **Plan:** The implementation will likely involve using the `Windows.Media.Transcoding.MediaTranscoder` or `Windows.Media.Editing.MediaComposition` APIs to gain frame-level control for creating the video file.

When `EnablePreRecording` is `false` (the default setting), the camera uses the standard, direct-to-file recording method on all platforms. This ensures there is no performance overhead from the buffering mechanism when the feature is not in use.
