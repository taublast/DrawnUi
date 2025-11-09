# Pre-Recording Implementation Status

## Current Status

### ? iOS / MacCatalyst - COMPLETE
- **Status**: Implemented and functional
- **Location**: `Platforms/Apple/NativeCamera.Apple.cs`
- **Method**: Uses `AVAssetWriter` with `CMSampleBuffer` frame buffering
- **Architecture**: `Queue<CMSampleBuffer>` rolling buffer maintains recent frames
- **Integration**: Properly hooked into `StartVideoRecording()` and `StopVideoRecording()`

### ? Android - TODO
- **Status**: Not yet implemented
- **Location**: `Platforms/Android/NativeCamera.Android.cs`
- **Planned Architecture**:
  - Use `MediaCodec` for frame encoding
  - Use `MediaMuxer` for file writing
  - Implement `Queue<EncodedFrame>` rolling buffer for frame storage
  - Rolling buffer should maintain `PreRecordDuration` worth of frames
  - When recording starts: write buffered frames first, then continue with live frames

- **Implementation Steps**:
  1. Add frame buffer management (`_preRecordingBuffer`, `_preRecordingFrames`)
  2. Implement frame capture during preview (when `EnablePreRecording=true`)
  3. Store encoded frames with timestamps in the rolling buffer
  4. Implement buffer cleanup when exceeding `PreRecordDuration`
  5. In `StartVideoRecording()`: write buffered frames before live video
  6. In `StopVideoRecording()`: finalize the video file properly

- **Key Considerations**:
  - Use `SurfaceTexture` for efficient frame handling
  - Implement proper thread synchronization for buffer access
  - Handle memory efficiently - only store encoded frame data, not full bitmaps
  - Ensure proper synchronization between preview frames and buffered frames

### ? Windows - TODO
- **Status**: Not yet implemented
- **Location**: `Platforms/Windows/NativeCamera.Windows.cs`
- **Planned Architecture**:
  - Use `MediaFrameReader` to capture Direct3D frames (already available in current code)
  - Implement `Queue<Direct3DFrameData>` rolling buffer
  - Store frame data with timestamps
  - When recording starts: encode and write buffered frames to output file
  - Then continue with live frame encoding

- **Implementation Steps**:
  1. Add frame buffer management (`_preRecordingBuffer`, `_preRecordingFrames`)
  2. In `OnFrameArrived()`: store frame data when `EnablePreRecording=true` and not recording
  3. Implement buffer size management based on `PreRecordDuration`
  4. In `StartVideoRecording()`: encode and write buffered frames to MediaFile
  5. Continue with live video frames as normal
  6. In `StopVideoRecording()`: finalize the file

- **Key Considerations**:
  - Leverage existing Direct3D frame capture infrastructure
  - Use reusable buffers to minimize allocations
  - Properly handle `MediaEncodingProfile` for pre-recorded frames
  - Ensure timestamps are continuous between buffered and live frames

## Implementation Checklist

### Android
- [ ] Add `_preRecordingBuffer` (Queue or circular buffer)
- [ ] Add `_preRecordingFrames` counter
- [ ] Add `_maxPreRecordingFrames` calculation based on `PreRecordDuration`
- [ ] Implement frame capture during preview when `EnablePreRecording=true`
- [ ] Implement buffer overflow handling (remove oldest frames)
- [ ] Integrate with `StartVideoRecording()` to write buffered frames first
- [ ] Add frame data structure to hold encoded data + timestamp
- [ ] Handle `EnablePreRecording` and `PreRecordDuration` property changes
- [ ] Test with various pre-recording durations (2s, 5s, 10s, 15s)

### Windows
- [ ] Add `_preRecordingBuffer` (Queue or circular buffer)
- [ ] Add `_preRecordingFrames` counter
- [ ] Add `_maxPreRecordingFrames` calculation based on `PreRecordDuration`
- [ ] Modify `OnFrameArrived()` to capture frames when `EnablePreRecording=true`
- [ ] Implement buffer overflow handling
- [ ] Integrate with `StartVideoRecording()` to write buffered frames
- [ ] Add proper frame data structure for Direct3D frames
- [ ] Handle `EnablePreRecording` and `PreRecordDuration` property changes
- [ ] Test with various pre-recording durations

## INativeCamera Interface Requirements

The following pre-recording properties are already defined in `INativeCamera` interface:
- `EnablePreRecording` { get; set; } - bool property to enable/disable feature
- `PreRecordDuration` { get; set; } - TimeSpan property for lookback duration (default 5 seconds)

These are already bound in `SkiaCamera` as `BindableProperty` instances and automatically synchronized with the native control via `OnNativeControlCreated()`.

## Notes

- The feature must have **zero performance overhead when `EnablePreRecording=false`** (the default)
- Platform-specific implementations should handle memory efficiently
- Rolling buffers should automatically manage their size based on `PreRecordDuration`
- Frame timestamps must remain consistent across buffered and live frames
- The feature works seamlessly with all existing recording modes and settings
