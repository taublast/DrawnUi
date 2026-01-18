# AudioEncoderNative

Native Windows audio encoder DLL for DrawnUi.Maui.Camera

## Purpose

Provides PCM to AAC audio encoding for .NET MAUI Windows applications using Media Foundation APIs that are inaccessible from managed code due to COM access restrictions in packaged MAUI apps.

## Architecture

- **Key Component**: `CLSID_CResamplerMediaObject` - Audio resampler MFT that normalizes device audio formats
- **Why Native?**: .NET 9 MAUI cannot access this COM object via `CoCreateInstance`, `MFTEnumEx`, or `QueryInterface`

## Building

### Requirements
- Visual Studio 2022 with C++ Desktop Development workload
- Windows 10 SDK (10.0.19041.0 or higher)

### Build Steps
```bash
cd native/AudioEncoderNative
msbuild AudioEncoderNative.vcxproj /p:Configuration=Release /p:Platform=x64
```

Or open `AudioEncoderNative.sln` in Visual Studio and build.

### Output
- `native/bin/x64/Release/AudioEncoderNative.dll`
- `native/bin/x64/Release/AudioEncoderNative.pdb`

## API

### C API (audio_encoder_native.h)

```c
// Create encoder (pass IMFSinkWriter pointer from .NET)
AudioEncoder* audio_encoder_create(
    void* sink_writer,      // IMFSinkWriter* from Marshal.GetIUnknownForObject()
    uint32_t stream_index,  // Audio stream index
    int32_t sample_rate,    // e.g., 48000
    int32_t channels,       // 1 or 2
    int32_t input_is_float  // 0 for PCM16, 1 for Float32
);

// Write PCM audio frame
int32_t audio_encoder_write_pcm(
    AudioEncoder* encoder,
    const uint8_t* pcm_data,
    uint32_t data_size,
    int64_t timestamp_hns  // Timestamp in 100-nanosecond units
);

// Cleanup (does not release SinkWriter - caller owns it)
void audio_encoder_destroy(AudioEncoder* encoder);

// Get error message
const char* audio_encoder_get_error(AudioEncoder* encoder);
```

### .NET P/Invoke (AudioEncoderNative.cs)

```csharp
IntPtr encoder = AudioEncoderNative.audio_encoder_create(
    Marshal.GetIUnknownForObject(sinkWriter),
    audioStreamIndex,
    48000,  // sample rate
    2,      // channels
    0);     // PCM16

int result = AudioEncoderNative.audio_encoder_write_pcm(
    encoder,
    pcmData,
    (uint)pcmData.Length,
    timestampHns);

AudioEncoderNative.audio_encoder_destroy(encoder);
```

## Implementation Details

1. **Resampler Creation**  
   - `CoCreateInstance(CLSID_CResamplerMediaObject)` - works in native C++, fails in .NET MAUI

2. **Input Type Configuration** (lines 45-59)
   - Sets device audio format (PCM16 or Float32)
   - Configures sample rate, channels, block alignment

3. **Output Type Configuration** (lines 65-78)
   - Always outputs PCM16 for SinkWriter
   - Normalized format for AAC transcoding

4. **ProcessInput** (lines 134-140)
   - Feeds PCM to resampler
   - Resampler buffers and normalizes format

5. **ProcessOutput** (lines 143-173)
   - Retrieves resampled PCM from resampler
   - Writes to IMFSinkWriter for AAC encoding
   - Handles `MF_E_TRANSFORM_NEED_MORE_INPUT` gracefully

## Thread Safety

- Uses `std::mutex` for write operations
- Safe to call from audio callback threads
- All COM objects properly reference counted

## Error Handling

- Returns error codes (`AUDIO_ENCODER_SUCCESS = 0`, negatives for errors)
- Stores HRESULT and error messages in encoder struct
- Accessible via `audio_encoder_get_error()`

## NuGet Packaging

The DLL is automatically included in the NuGet package:
- Path: `runtimes/win-x64/native/AudioEncoderNative.dll`
- Copied to output directory during build
- Works in both Debug and Release configurations

## License

MIT License
