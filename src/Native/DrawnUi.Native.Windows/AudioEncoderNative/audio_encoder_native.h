// AudioEncoderNative - Native Windows audio encoder for .NET MAUI
// Provides PCM to AAC encoding via Media Foundation

#ifndef AUDIO_ENCODER_NATIVE_H
#define AUDIO_ENCODER_NATIVE_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifdef AUDIO_ENCODER_EXPORTS
#define AUDIO_ENCODER_API __declspec(dllexport)
#else
#define AUDIO_ENCODER_API __declspec(dllimport)
#endif

// Opaque encoder handle
typedef struct AudioEncoder AudioEncoder;

// Error codes
#define AUDIO_ENCODER_SUCCESS           0
#define AUDIO_ENCODER_ERROR_INIT        -1
#define AUDIO_ENCODER_ERROR_WRITE       -2
#define AUDIO_ENCODER_ERROR_FINALIZE    -3
#define AUDIO_ENCODER_ERROR_INVALID     -4

/// <summary>
/// Creates an audio encoder for AAC encoding.
/// </summary>
/// <param name="sink_writer">IMFSinkWriter pointer from .NET (as IntPtr)</param>
/// <param name="stream_index">Audio stream index in the sink writer</param>
/// <param name="sample_rate">Sample rate (e.g., 48000)</param>
/// <param name="channels">Number of channels (1 or 2)</param>
/// <param name="input_is_float">True if input PCM is Float32, false if PCM16</param>
/// <returns>Encoder handle or NULL on failure</returns>
AUDIO_ENCODER_API AudioEncoder* audio_encoder_create(
    void* sink_writer,
    uint32_t stream_index,
    int32_t sample_rate,
    int32_t channels,
    int32_t input_is_float);

/// <summary>
/// Writes PCM audio data to the encoder.
/// </summary>
/// <param name="encoder">Encoder handle</param>
/// <param name="pcm_data">PCM audio data (16-bit or Float32 depending on encoder config)</param>
/// <param name="data_size">Size of PCM data in bytes</param>
/// <param name="timestamp_hns">Timestamp in 100-nanosecond units (HNS)</param>
/// <returns>AUDIO_ENCODER_SUCCESS or error code</returns>
AUDIO_ENCODER_API int32_t audio_encoder_write_pcm(
    AudioEncoder* encoder,
    const uint8_t* pcm_data,
    uint32_t data_size,
    int64_t timestamp_hns);

/// <summary>
/// Flushes any buffered audio samples from the resampler.
/// Call this before destroying the encoder to ensure all audio is written.
/// </summary>
/// <param name="encoder">Encoder handle</param>
/// <returns>AUDIO_ENCODER_SUCCESS or error code</returns>
AUDIO_ENCODER_API int32_t audio_encoder_finalize(AudioEncoder* encoder);

/// <summary>
/// Cleans up encoder resources (but not the SinkWriter - caller owns it).
/// </summary>
/// <param name="encoder">Encoder handle</param>
AUDIO_ENCODER_API void audio_encoder_destroy(AudioEncoder* encoder);

/// <summary>
/// Gets the last error message from the encoder.
/// </summary>
/// <param name="encoder">Encoder handle</param>
/// <returns>Error message string (valid until next operation or destroy)</returns>
AUDIO_ENCODER_API const char* audio_encoder_get_error(AudioEncoder* encoder);

#ifdef __cplusplus
}
#endif

#endif // AUDIO_ENCODER_NATIVE_H
