// AudioEncoderNative - Native Windows audio encoder for .NET MAUI
// Based on wcap (MIT) - https://github.com/mmozeiko/wcap

#include "audio_encoder_native.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <string>
#include <mutex>

// CLSID for Microsoft Audio Resampler MFT (not accessible from .NET MAUI)
static const GUID CLSID_CResamplerMediaObject = { 0xf447b69e, 0x1884, 0x4a7e, { 0x80, 0x55, 0x34, 0x6f, 0x74, 0xd6, 0xed, 0xb3 } };

struct AudioEncoder {
    IMFTransform* resampler;        // Audio resampler MFT
    IMFSinkWriter* sinkWriter;      // Borrowed reference (not owned)
    DWORD streamIndex;
    IMFSample* resamplerInputSample;
    std::mutex writeMutex;
    std::string lastError;
    bool initialized;
    int32_t sampleRate;
    int32_t channels;
    int32_t inputIsFloat;
};

AUDIO_ENCODER_API AudioEncoder* audio_encoder_create(
    void* sink_writer,
    uint32_t stream_index,
    int32_t sample_rate,
    int32_t channels,
    int32_t input_is_float)
{
    auto encoder = new AudioEncoder();
    encoder->resampler = nullptr;
    encoder->sinkWriter = (IMFSinkWriter*)sink_writer;
    encoder->streamIndex = stream_index;
    encoder->resamplerInputSample = nullptr;
    encoder->initialized = false;
    encoder->sampleRate = sample_rate;
    encoder->channels = channels;
    encoder->inputIsFloat = input_is_float;

    // Create Audio Resampler MFT (wcap pattern - wcap_encoder.h lines 537-542)
    HRESULT hr = CoCreateInstance(
        CLSID_CResamplerMediaObject,
        NULL,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&encoder->resampler));

    if (FAILED(hr))
    {
        encoder->lastError = "CoCreateInstance(CLSID_CResamplerMediaObject) failed: 0x" + 
            std::to_string(hr);
        return encoder;
    }

    // Configure resampler INPUT type (device format) - wcap pattern (lines 547-561)
    IMFMediaType* inputType = nullptr;
    hr = MFCreateMediaType(&inputType);
    if (SUCCEEDED(hr))
    {
        inputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        inputType->SetGUID(MF_MT_SUBTYPE, input_is_float ? MFAudioFormat_Float : MFAudioFormat_PCM);
        inputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, sample_rate);
        inputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, channels);
        inputType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, input_is_float ? 32 : 16);
        
        uint32_t blockAlign = channels * (input_is_float ? 4 : 2);
        inputType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
        inputType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, sample_rate * blockAlign);

        hr = encoder->resampler->SetInputType(0, inputType, 0);
        inputType->Release();
    }

    if (FAILED(hr))
    {
        encoder->lastError = "Resampler SetInputType failed: 0x" + std::to_string(hr);
        return encoder;
    }

    // Configure resampler OUTPUT type (PCM16 for SinkWriter) - wcap pattern (lines 562-569)
    IMFMediaType* outputType = nullptr;
    hr = MFCreateMediaType(&outputType);
    if (SUCCEEDED(hr))
    {
        outputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        outputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
        outputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, sample_rate);
        outputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, channels);
        outputType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
        
        uint32_t blockAlign = channels * 2;
        outputType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
        outputType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, sample_rate * blockAlign);

        hr = encoder->resampler->SetOutputType(0, outputType, 0);
        outputType->Release();
    }

    if (FAILED(hr))
    {
        encoder->lastError = "Resampler SetOutputType failed: 0x" + std::to_string(hr);
        return encoder;
    }

    // Start resampler streaming (wcap pattern)
    encoder->resampler->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    encoder->resampler->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);

    // Create reusable input sample
    hr = MFCreateSample(&encoder->resamplerInputSample);
    if (FAILED(hr))
    {
        encoder->lastError = "MFCreateSample failed: 0x" + std::to_string(hr);
        return encoder;
    }

    // CRITICAL: Set PCM input type on SinkWriter so it knows how to transcode to AAC
    // This call fails in .NET MAUI with 0xC00D5212, but works in native C++
    IMFMediaType* sinkInputType = nullptr;
    hr = MFCreateMediaType(&sinkInputType);
    if (SUCCEEDED(hr))
    {
        sinkInputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        sinkInputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
        sinkInputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, sample_rate);
        sinkInputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, channels);
        sinkInputType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
        
        uint32_t blockAlign = channels * 2;
        sinkInputType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
        sinkInputType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, sample_rate * blockAlign);

        hr = encoder->sinkWriter->SetInputMediaType(encoder->streamIndex, sinkInputType, nullptr);
        sinkInputType->Release();

        if (FAILED(hr))
        {
            encoder->lastError = "SinkWriter->SetInputMediaType failed: 0x" + std::to_string(hr);
            return encoder;
        }

        OutputDebugStringA("[NativeEncoder] SetInputMediaType SUCCESS - SinkWriter knows input is PCM16\n");
    }

    encoder->initialized = true;
    return encoder;
}

AUDIO_ENCODER_API int32_t audio_encoder_write_pcm(
    AudioEncoder* encoder,
    const uint8_t* pcm_data,
    uint32_t data_size,
    int64_t timestamp_hns)
{
    if (!encoder || !encoder->initialized)
        return AUDIO_ENCODER_ERROR_INVALID;

    std::lock_guard<std::mutex> lock(encoder->writeMutex);

    // Create IMFSample directly and write to SinkWriter (bypass resampler for now)
    IMFSample* sample = nullptr;
    HRESULT hr = MFCreateSample(&sample);
    if (FAILED(hr))
    {
        encoder->lastError = "MFCreateSample failed: 0x" + std::to_string(hr);
        return AUDIO_ENCODER_ERROR_WRITE;
    }

    // Create buffer and copy PCM data
    IMFMediaBuffer* buffer = nullptr;
    hr = MFCreateMemoryBuffer(data_size, &buffer);
    if (FAILED(hr))
    {
        sample->Release();
        encoder->lastError = "MFCreateMemoryBuffer failed: 0x" + std::to_string(hr);
        return AUDIO_ENCODER_ERROR_WRITE;
    }

    BYTE* bufferData = nullptr;
    hr = buffer->Lock(&bufferData, nullptr, nullptr);
    if (SUCCEEDED(hr))
    {
        memcpy(bufferData, pcm_data, data_size);
        buffer->Unlock();
        buffer->SetCurrentLength(data_size);
    }

    sample->AddBuffer(buffer);
    buffer->Release();

    sample->SetSampleTime(timestamp_hns);

    // Calculate duration
    int32_t bytesPerSample = encoder->channels * (encoder->inputIsFloat ? 4 : 2);
    double samplesCount = data_size / (double)bytesPerSample;
    int64_t duration = (int64_t)((samplesCount / encoder->sampleRate) * 10000000.0);
    sample->SetSampleDuration(duration);

    // Write directly to SinkWriter (skip resampler passthrough)
    hr = encoder->sinkWriter->WriteSample(encoder->streamIndex, sample);
    sample->Release();

    if (FAILED(hr))
    {
        std::string msg = "[NativeEncoder] WriteSample FAILED: 0x" + std::to_string(hr) + "\n";
        OutputDebugStringA(msg.c_str());
        encoder->lastError = "SinkWriter WriteSample failed: 0x" + std::to_string(hr);
        return AUDIO_ENCODER_ERROR_WRITE;
    }

    return AUDIO_ENCODER_SUCCESS;
}

AUDIO_ENCODER_API int32_t audio_encoder_finalize(AudioEncoder* encoder)
{
    if (!encoder || !encoder->initialized)
        return AUDIO_ENCODER_ERROR_INVALID;

    // No-op since we're writing directly to SinkWriter (no resampler buffer to drain)
    std::string msg = "[NativeEncoder] Finalize: No resampler drain needed (direct write mode)\n";
    OutputDebugStringA(msg.c_str());

    return AUDIO_ENCODER_SUCCESS;
}

AUDIO_ENCODER_API void audio_encoder_destroy(AudioEncoder* encoder)
{
    if (!encoder)
        return;

    if (encoder->resampler)
    {
        encoder->resampler->ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        encoder->resampler->Release();
    }

    if (encoder->resamplerInputSample)
    {
        encoder->resamplerInputSample->Release();
    }

    // Note: We DON'T release sinkWriter - caller owns it

    delete encoder;
}

AUDIO_ENCODER_API const char* audio_encoder_get_error(AudioEncoder* encoder)
{
    return encoder ? encoder->lastError.c_str() : "Invalid encoder";
}
