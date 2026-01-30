using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace DrawnUi.Camera.Platforms.Windows;

/// <summary>
/// Audio-only encoder for Windows using Media Foundation.
/// Writes M4A files (AAC audio in MP4 container).
/// </summary>
public class AudioOnlyEncoderWindows : IAudioOnlyEncoder
{
    private IMFSinkWriter _sinkWriter;
    private uint _audioStreamIndex;
    private string _outputPath;
    private DateTime _startTime;
    private bool _isRecording;
    private bool _disposed;
    private int _sampleRate;
    private int _channels;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long _totalSamplesWritten;

    // MF GUIDs - same as in WindowsCaptureVideoEncoder
    private static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid MFMediaType_Audio = new("73646961-0000-0010-8000-00aa00389b71");
    private static readonly Guid MFAudioFormat_AAC = new("00001610-0000-0010-8000-00aa00389b71");
    private static readonly Guid MFAudioFormat_PCM = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    private static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    private static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new("f2deb57f-330f-481f-986a-4301d512cf9f");
    private static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    private static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new("1aab75c8-29bb-443f-95bb-584637e66c9f");
    private static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new("a634a91c-822b-41b9-a494-4ae46436892d");

    public bool IsRecording => _isRecording;

    public TimeSpan RecordingDuration
    {
        get
        {
            if (!_isRecording) return TimeSpan.Zero;
            return DateTime.Now - _startTime;
        }
    }

    public async Task InitializeAsync(string outputPath, int sampleRate, int channels, AudioBitDepth bitDepth)
    {
        _outputPath = outputPath;
        _sampleRate = sampleRate;
        _channels = channels;

        Debug.WriteLine($"[AudioOnlyEncoderWindows] Initializing: {outputPath}, {sampleRate}Hz, {channels}ch");

        await Task.Run(() =>
        {
            unsafe
            {
                // Initialize Media Foundation
                var hr = PInvoke.MFStartup(PInvoke.MF_VERSION, 0);
                if (hr.Failed)
                    throw new InvalidOperationException($"MFStartup failed: 0x{hr.Value:X8}");

                // Create attributes for sink writer
                IMFAttributes attributes;
                hr = PInvoke.MFCreateAttributes(out attributes, 1);
                if (hr.Failed)
                    throw new InvalidOperationException($"MFCreateAttributes failed: 0x{hr.Value:X8}");

                // Enable hardware transforms
                attributes.SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

                // Create sink writer for M4A output
                fixed (char* p = _outputPath)
                {
                    hr = PInvoke.MFCreateSinkWriterFromURL(new PCWSTR(p), null, attributes, out _sinkWriter);
                    if (hr.Failed)
                        throw new InvalidOperationException($"MFCreateSinkWriterFromURL failed: 0x{hr.Value:X8}");
                }

                // Configure AAC output type
                IMFMediaType audioOutputType;
                hr = PInvoke.MFCreateMediaType(out audioOutputType);
                if (hr.Failed)
                    throw new InvalidOperationException($"MFCreateMediaType (output) failed: 0x{hr.Value:X8}");

                audioOutputType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                audioOutputType.SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
                audioOutputType.SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
                audioOutputType.SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)sampleRate);
                audioOutputType.SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, (uint)channels);
                audioOutputType.SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 16000); // ~128 kbps

                _sinkWriter.AddStream(audioOutputType, out _audioStreamIndex);

                // Configure PCM input type
                IMFMediaType audioInputType;
                hr = PInvoke.MFCreateMediaType(out audioInputType);
                if (hr.Failed)
                    throw new InvalidOperationException($"MFCreateMediaType (input) failed: 0x{hr.Value:X8}");

                audioInputType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                audioInputType.SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
                audioInputType.SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
                audioInputType.SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)sampleRate);
                audioInputType.SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, (uint)channels);
                audioInputType.SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, (uint)(channels * 2));
                audioInputType.SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, (uint)(sampleRate * channels * 2));

                _sinkWriter.SetInputMediaType(_audioStreamIndex, audioInputType, null);

                Debug.WriteLine($"[AudioOnlyEncoderWindows] Initialized successfully, stream index: {_audioStreamIndex}");
            }
        });
    }

    public Task StartAsync()
    {
        if (_sinkWriter == null)
            throw new InvalidOperationException("Encoder not initialized");

        unsafe
        {
            _sinkWriter.BeginWriting();
        }

        _startTime = DateTime.Now;
        _isRecording = true;
        _totalSamplesWritten = 0;

        Debug.WriteLine("[AudioOnlyEncoderWindows] Started recording");
        return Task.CompletedTask;
    }

    public void WriteAudio(AudioSample sample)
    {
        if (!_isRecording || _sinkWriter == null || sample.Data == null || sample.Data.Length == 0)
            return;

        _writeLock.Wait();
        try
        {
            unsafe
            {
                // Create media buffer
                IMFMediaBuffer buffer;
                var hr = PInvoke.MFCreateMemoryBuffer((uint)sample.Data.Length, out buffer);
                if (hr.Failed)
                {
                    Debug.WriteLine($"[AudioOnlyEncoderWindows] MFCreateMemoryBuffer failed: 0x{hr.Value:X8}");
                    return;
                }

                // Lock and copy data
                byte* bufferData;
                uint maxLength;
                buffer.Lock(&bufferData, &maxLength, null);
                Marshal.Copy(sample.Data, 0, (IntPtr)bufferData, sample.Data.Length);
                buffer.Unlock();
                buffer.SetCurrentLength((uint)sample.Data.Length);

                // Create sample
                IMFSample mfSample;
                hr = PInvoke.MFCreateSample(out mfSample);
                if (hr.Failed)
                {
                    Debug.WriteLine($"[AudioOnlyEncoderWindows] MFCreateSample failed: 0x{hr.Value:X8}");
                    return;
                }

                mfSample.AddBuffer(buffer);

                // Calculate timestamp (100-nanosecond units)
                long timestampHns = sample.TimestampNs / 100;
                mfSample.SetSampleTime(timestampHns);

                // Calculate duration based on sample count
                int samplesPerChannel = sample.Data.Length / (sample.Channels * (sample.BytesPerSample > 0 ? sample.BytesPerSample : 2));
                long durationHns = (long)samplesPerChannel * 10_000_000 / sample.SampleRate;
                mfSample.SetSampleDuration(durationHns);

                // Write to sink
                _sinkWriter.WriteSample(_audioStreamIndex, mfSample);
                _totalSamplesWritten += samplesPerChannel;

                // Release COM objects
                Marshal.ReleaseComObject(mfSample);
                Marshal.ReleaseComObject(buffer);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<CapturedAudio> StopAsync()
    {
        if (!_isRecording)
            return null;

        _isRecording = false;

        Debug.WriteLine("[AudioOnlyEncoderWindows] Stopping recording...");

        await Task.Run(() =>
        {
            _writeLock.Wait();
            try
            {
                if (_sinkWriter != null)
                {
                    unsafe
                    {
                        // Finalize the sink writer
                        _sinkWriter.Finalize();
                    }
                }
            }
            finally
            {
                _writeLock.Release();
            }
        });

        var duration = DateTime.Now - _startTime;
        var fileInfo = File.Exists(_outputPath) ? new FileInfo(_outputPath) : null;

        var result = new CapturedAudio
        {
            FilePath = _outputPath,
            Duration = duration,
            SampleRate = _sampleRate,
            Channels = _channels,
            FileSizeBytes = fileInfo?.Length ?? 0,
            Time = _startTime
        };

        Debug.WriteLine($"[AudioOnlyEncoderWindows] Stopped. Duration: {duration}, Size: {result.FileSizeBytes} bytes");

        Cleanup();
        return result;
    }

    public async Task AbortAsync()
    {
        _isRecording = false;
        Cleanup();

        // Delete partial file
        if (File.Exists(_outputPath))
        {
            try { File.Delete(_outputPath); } catch { }
        }

        await Task.CompletedTask;
    }

    private void Cleanup()
    {
        if (_sinkWriter != null)
        {
            Marshal.ReleaseComObject(_sinkWriter);
            _sinkWriter = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Cleanup();
        _writeLock?.Dispose();
    }
}
