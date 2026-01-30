#if IOS || MACCATALYST

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using Foundation;
using AudioToolbox;

namespace DrawnUi.Camera;

/// <summary>
/// Audio-only encoder for Apple platforms using AVAssetWriter.
/// Writes M4A files (AAC audio in MP4 container).
/// </summary>
public class AudioOnlyEncoderApple : IAudioOnlyEncoder
{
    private AVAssetWriter _writer;
    private AVAssetWriterInput _audioInput;
    private string _outputPath;
    private DateTime _startTime;
    private bool _isRecording;
    private bool _disposed;
    private int _sampleRate;
    private int _channels;
    private readonly object _writeLock = new();
    private long _totalSamplesWritten;
    private CMAudioFormatDescription _audioFormatDescription;

    // Track memory allocations for cleanup
    private readonly List<IntPtr> _memoryToFree = new();

    public bool IsRecording => _isRecording;

    public TimeSpan RecordingDuration
    {
        get
        {
            if (!_isRecording) return TimeSpan.Zero;
            return DateTime.Now - _startTime;
        }
    }

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMAudioFormatDescriptionCreate(
        IntPtr allocator,
        ref AudioStreamBasicDescription asbd,
        nuint layoutSize,
        IntPtr layout,
        nuint magicCookieSize,
        IntPtr magicCookie,
        IntPtr extensions,
        out IntPtr formatDescriptionOut
    );

    public async Task InitializeAsync(string outputPath, int sampleRate, int channels, AudioBitDepth bitDepth)
    {
        _outputPath = outputPath;
        _sampleRate = sampleRate;
        _channels = channels;

        Debug.WriteLine($"[AudioOnlyEncoderApple] Initializing: {outputPath}, {sampleRate}Hz, {channels}ch");

        await Task.Run(() =>
        {
            // Delete existing file if present
            if (File.Exists(_outputPath))
            {
                try { File.Delete(_outputPath); } catch { }
            }

            var outputUrl = NSUrl.FromFilename(_outputPath);

            // M4A file type string
            _writer = new AVAssetWriter(outputUrl, "com.apple.m4a-audio", out var error);
            if (error != null)
            {
                throw new InvalidOperationException($"Failed to create AVAssetWriter: {error.LocalizedDescription}");
            }

            // Configure AAC output settings
            var audioSettings = new AudioSettings
            {
                Format = AudioFormatType.MPEG4AAC,
                SampleRate = sampleRate,
                NumberChannels = channels,
                EncoderBitRate = 128000 // 128 kbps
            };

            _audioInput = new AVAssetWriterInput(AVMediaTypes.Audio.GetConstant(), audioSettings);
            _audioInput.ExpectsMediaDataInRealTime = true;

            if (_writer.CanAddInput(_audioInput))
            {
                _writer.AddInput(_audioInput);
            }
            else
            {
                throw new InvalidOperationException("Cannot add audio input to writer");
            }

            Debug.WriteLine("[AudioOnlyEncoderApple] Initialized successfully");
        });
    }

    public Task StartAsync()
    {
        if (_writer == null)
            throw new InvalidOperationException("Encoder not initialized");

        if (!_writer.StartWriting())
        {
            throw new InvalidOperationException($"Failed to start writing: {_writer.Error?.LocalizedDescription}");
        }

        _writer.StartSessionAtSourceTime(CMTime.Zero);
        _startTime = DateTime.Now;
        _isRecording = true;
        _totalSamplesWritten = 0;

        Debug.WriteLine("[AudioOnlyEncoderApple] Started recording");
        return Task.CompletedTask;
    }

    public void WriteAudio(AudioSample sample)
    {
        if (!_isRecording || _audioInput == null || sample.Data == null || sample.Data.Length == 0)
            return;

        if (!_audioInput.ReadyForMoreMediaData)
            return;

        lock (_writeLock)
        {
            try
            {
                // Create sample buffer from PCM data
                var sampleBuffer = CreateSampleBuffer(sample);
                if (sampleBuffer == null)
                    return;

                if (!_audioInput.AppendSampleBuffer(sampleBuffer))
                {
                    Debug.WriteLine($"[AudioOnlyEncoderApple] Failed to append sample: {_writer.Error?.LocalizedDescription}");
                }
                else
                {
                    _totalSamplesWritten++;
                }

                sampleBuffer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioOnlyEncoderApple] WriteAudio error: {ex.Message}");
            }
        }
    }

    private CMSampleBuffer CreateSampleBuffer(AudioSample sample)
    {
        try
        {
            int bytesPerSample = sample.BytesPerSample > 0 ? sample.BytesPerSample : 2;
            int bytesPerFrame = bytesPerSample * sample.Channels;
            int numSamples = sample.Data.Length / bytesPerFrame;

            if (_audioFormatDescription == null)
            {
                var audioFormat = new AudioStreamBasicDescription
                {
                    SampleRate = sample.SampleRate,
                    Format = AudioFormatType.LinearPCM,
                    FormatFlags = AudioFormatFlags.LinearPCMIsPacked | AudioFormatFlags.LinearPCMIsSignedInteger,
                    ChannelsPerFrame = sample.Channels,
                    BytesPerPacket = bytesPerFrame,
                    FramesPerPacket = 1,
                    BytesPerFrame = bytesPerFrame,
                    BitsPerChannel = bytesPerSample * 8
                };

                IntPtr formatDescPtr;
                var result = CMAudioFormatDescriptionCreate(
                    IntPtr.Zero,
                    ref audioFormat,
                    0,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out formatDescPtr
                );

                if (result != 0 || formatDescPtr == IntPtr.Zero)
                {
                    Debug.WriteLine($"[AudioOnlyEncoderApple] Failed to create format description: {result}");
                    return null;
                }

                var desc = CMFormatDescription.Create(formatDescPtr, true);
                if (desc == null) return null;
                _audioFormatDescription = (CMAudioFormatDescription)desc;
            }

            var unmanagedPtr = Marshal.AllocHGlobal(sample.Data.Length);
            _memoryToFree.Add(unmanagedPtr);
            Marshal.Copy(sample.Data, 0, unmanagedPtr, sample.Data.Length);

            var blockBuffer = CMBlockBuffer.FromMemoryBlock(
                unmanagedPtr,
                (nuint)sample.Data.Length,
                null,
                0,
                (nuint)sample.Data.Length,
                CMBlockBufferFlags.AssureMemoryNow,
                out var blockStatus);

            if (blockStatus != CMBlockBufferError.None || blockBuffer == null)
            {
                Debug.WriteLine($"[AudioOnlyEncoderApple] Failed to create block buffer: {blockStatus}");
                return null;
            }

            double timeSec = (double)sample.TimestampNs / 1_000_000_000;
            var presentationTime = CMTime.FromSeconds(timeSec, sample.SampleRate);

            var sampleBuffer = CMSampleBuffer.CreateWithPacketDescriptions(
                blockBuffer,
                _audioFormatDescription,
                numSamples,
                presentationTime,
                null,
                out var sampleStatus);

            if (sampleStatus != CMSampleBufferError.None || sampleBuffer == null)
            {
                Debug.WriteLine($"[AudioOnlyEncoderApple] Failed to create sample buffer: {sampleStatus}");
                return null;
            }

            return sampleBuffer;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioOnlyEncoderApple] CreateSampleBuffer error: {ex.Message}");
            return null;
        }
    }

    public async Task<CapturedAudio> StopAsync()
    {
        if (!_isRecording)
            return null;

        _isRecording = false;

        Debug.WriteLine("[AudioOnlyEncoderApple] Stopping recording...");

        var tcs = new TaskCompletionSource<bool>();

        lock (_writeLock)
        {
            _audioInput?.MarkAsFinished();
        }

        _writer?.FinishWriting(() =>
        {
            tcs.TrySetResult(true);
        });

        await tcs.Task;

        // Free allocated memory
        foreach (var ptr in _memoryToFree)
        {
            try { Marshal.FreeHGlobal(ptr); } catch { }
        }
        _memoryToFree.Clear();

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

        Debug.WriteLine($"[AudioOnlyEncoderApple] Stopped. Duration: {duration}, Size: {result.FileSizeBytes} bytes");

        Cleanup();
        return result;
    }

    public async Task AbortAsync()
    {
        _isRecording = false;

        lock (_writeLock)
        {
            _audioInput?.MarkAsFinished();
        }

        var tcs = new TaskCompletionSource<bool>();
        _writer?.FinishWriting(() => tcs.TrySetResult(true));
        await tcs.Task;

        Cleanup();

        // Delete partial file
        if (File.Exists(_outputPath))
        {
            try { File.Delete(_outputPath); } catch { }
        }
    }

    private void Cleanup()
    {
        foreach (var ptr in _memoryToFree)
        {
            try { Marshal.FreeHGlobal(ptr); } catch { }
        }
        _memoryToFree.Clear();

        _audioInput?.Dispose();
        _audioInput = null;
        _writer?.Dispose();
        _writer = null;
        _audioFormatDescription = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
    }
}

#endif
