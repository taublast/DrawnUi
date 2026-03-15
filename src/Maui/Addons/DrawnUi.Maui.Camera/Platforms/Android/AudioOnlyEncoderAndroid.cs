using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;

namespace DrawnUi.Camera.Platforms.Android;

/// <summary>
/// Audio-only encoder for Android using MediaCodec and MediaMuxer.
/// Writes M4A files (AAC audio in MP4 container).
/// </summary>
public class AudioOnlyEncoderAndroid : IAudioOnlyEncoder
{
    private MediaCodec _audioEncoder;
    private MediaMuxer _muxer;
    private int _audioTrackIndex = -1;
    private string _outputPath;
    private DateTime _startTime;
    private bool _isRecording;
    private bool _muxerStarted;
    private bool _disposed;
    private int _sampleRate;
    private int _channels;
    private readonly object _writeLock = new();
    private long _totalSamplesWritten;
    private long _firstInputTimestampNs = -1;
    private long _lastQueuedInputPresentationTimeUs = -1;
    private long _lastWrittenOutputPresentationTimeUs = -1;
    private bool _sawOutputEos;
    private bool _wroteEncodedSamples;
    private MediaCodec.BufferInfo _bufferInfo;

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
        _bufferInfo = new MediaCodec.BufferInfo();

        Debug.WriteLine($"[AudioOnlyEncoderAndroid] Initializing: {outputPath}, {sampleRate}Hz, {channels}ch");

        await Task.Run(() =>
        {
            try
            {
                // Create AAC encoder
                var mimeType = MediaFormat.MimetypeAudioAac;
                _audioEncoder = MediaCodec.CreateEncoderByType(mimeType);

                var format = MediaFormat.CreateAudioFormat(mimeType, sampleRate, channels);
                format.SetInteger(MediaFormat.KeyAacProfile, (int)MediaCodecProfileType.Aacobjectlc);
                format.SetInteger(MediaFormat.KeyBitRate, 128000); // 128 kbps
                format.SetInteger(MediaFormat.KeyMaxInputSize, 16384);

                _audioEncoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);

                // Create MediaMuxer for M4A output
                _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);

                Debug.WriteLine("[AudioOnlyEncoderAndroid] Initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioOnlyEncoderAndroid] Initialize error: {ex.Message}");
                throw;
            }
        });
    }

    public Task StartAsync()
    {
        if (_audioEncoder == null)
            throw new InvalidOperationException("Encoder not initialized");

        _audioEncoder.Start();
        _startTime = DateTime.Now;
        _isRecording = true;
        _totalSamplesWritten = 0;
        _firstInputTimestampNs = -1;
        _lastQueuedInputPresentationTimeUs = -1;
        _lastWrittenOutputPresentationTimeUs = -1;
        _sawOutputEos = false;
        _wroteEncodedSamples = false;
        _audioTrackIndex = -1;
        _muxerStarted = false;

        Debug.WriteLine("[AudioOnlyEncoderAndroid] Started recording");
        return Task.CompletedTask;
    }

    public void WriteAudio(AudioSample sample)
    {
        if (!_isRecording || _audioEncoder == null || sample.Data == null || sample.Data.Length == 0)
            return;

        lock (_writeLock)
        {
            try
            {
                int bytesPerFrame = Math.Max(1, Math.Max(1, sample.Channels) * Math.Max(1, sample.BytesPerSample));
                int sampleRate = sample.SampleRate > 0 ? sample.SampleRate : Math.Max(1, _sampleRate);
                int dataOffset = 0;

                while (dataOffset < sample.Data.Length)
                {
                    int inputBufferIndex = _audioEncoder.DequeueInputBuffer(10000);
                    if (inputBufferIndex < 0)
                    {
                        DrainEncoder(false);
                        inputBufferIndex = _audioEncoder.DequeueInputBuffer(10000);
                        if (inputBufferIndex < 0)
                        {
                            Debug.WriteLine("[AudioOnlyEncoderAndroid] No input buffer available");
                            break;
                        }
                    }

                    var inputBuffer = _audioEncoder.GetInputBuffer(inputBufferIndex);
                    if (inputBuffer == null)
                    {
                        _audioEncoder.QueueInputBuffer(inputBufferIndex, 0, 0, 0, MediaCodecBufferFlags.None);
                        break;
                    }

                    inputBuffer.Clear();

                    int availableBytes = inputBuffer.Remaining();
                    if (availableBytes < bytesPerFrame)
                    {
                        Debug.WriteLine($"[AudioOnlyEncoderAndroid] Input buffer too small ({availableBytes} bytes, frame size {bytesPerFrame})");
                        break;
                    }

                    int bytesRemaining = sample.Data.Length - dataOffset;
                    int chunkSize = Math.Min(bytesRemaining, availableBytes);
                    chunkSize -= chunkSize % bytesPerFrame;

                    if (chunkSize <= 0)
                    {
                        Debug.WriteLine($"[AudioOnlyEncoderAndroid] Could not align {bytesRemaining} bytes to frame size {bytesPerFrame}");
                        break;
                    }

                    inputBuffer.Put(sample.Data, dataOffset, chunkSize);

                    long chunkTimestampNs = sample.TimestampNs + ((long)(dataOffset / bytesPerFrame) * 1_000_000_000L / sampleRate);
                    long presentationTimeUs = CalculateInputPresentationTimeUs(chunkTimestampNs);
                    _lastQueuedInputPresentationTimeUs = presentationTimeUs;
                    _audioEncoder.QueueInputBuffer(inputBufferIndex, 0, chunkSize, presentationTimeUs, MediaCodecBufferFlags.None);

                    dataOffset += chunkSize;

                    DrainEncoder(false);
                }

                if (dataOffset < sample.Data.Length)
                {
                    Debug.WriteLine($"[AudioOnlyEncoderAndroid] Dropped {sample.Data.Length - dataOffset} trailing bytes after chunked submit");
                }

                DrainEncoder(false);
                _totalSamplesWritten++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioOnlyEncoderAndroid] WriteAudio error: {ex.Message}");
            }
        }
    }

    private void DrainEncoder(bool endOfStream)
    {
        if (endOfStream)
        {
            if (!QueueEndOfStreamInputBuffer())
            {
                Debug.WriteLine("[AudioOnlyEncoderAndroid] Warning: could not queue EOS input buffer before stop");
            }
        }

        int tryAgainCount = 0;
        while (true)
        {
            int outputBufferIndex = _audioEncoder.DequeueOutputBuffer(_bufferInfo, endOfStream ? 5000 : 0);

            if (outputBufferIndex == (int)MediaCodecInfoState.TryAgainLater)
            {
                if (!endOfStream || ++tryAgainCount >= 10)
                {
                    break;
                }

                continue;
            }

            tryAgainCount = 0;

            if (outputBufferIndex == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                // Add audio track to muxer when format is known
                if (!_muxerStarted)
                {
                    var newFormat = _audioEncoder.OutputFormat;
                    _audioTrackIndex = _muxer.AddTrack(newFormat);
                    _muxer.Start();
                    _muxerStarted = true;
                    Debug.WriteLine($"[AudioOnlyEncoderAndroid] Muxer started, audio track: {_audioTrackIndex}");
                }
                continue;
            }

            if (outputBufferIndex < 0)
            {
                break;
            }

            var outputBuffer = _audioEncoder.GetOutputBuffer(outputBufferIndex);
            if (outputBuffer != null && _muxerStarted && _bufferInfo.Size > 0)
            {
                outputBuffer.Position(_bufferInfo.Offset);
                outputBuffer.Limit(_bufferInfo.Offset + _bufferInfo.Size);

                if (_bufferInfo.PresentationTimeUs <= _lastWrittenOutputPresentationTimeUs)
                {
                    _bufferInfo.PresentationTimeUs = _lastWrittenOutputPresentationTimeUs + 1;
                }

                _muxer.WriteSampleData(_audioTrackIndex, outputBuffer, _bufferInfo);
                _lastWrittenOutputPresentationTimeUs = _bufferInfo.PresentationTimeUs;
                _wroteEncodedSamples = true;
            }

            _audioEncoder.ReleaseOutputBuffer(outputBufferIndex, false);

            if ((_bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
            {
                _sawOutputEos = true;
                break;
            }
        }
    }

    public async Task<CapturedAudio> StopAsync()
    {
        if (!_isRecording)
            return null;

        _isRecording = false;

        Debug.WriteLine("[AudioOnlyEncoderAndroid] Stopping recording...");

        await Task.Run(() =>
        {
            lock (_writeLock)
            {
                try
                {
                    // Drain remaining encoded data
                    DrainEncoder(true);

                    StopAndReleaseMuxer(_muxerStarted && _wroteEncodedSamples);
                    StopAndReleaseAudioEncoder();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AudioOnlyEncoderAndroid] Stop error: {ex.Message}");
                }
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

        Debug.WriteLine($"[AudioOnlyEncoderAndroid] Stopped. Duration: {duration}, Size: {result.FileSizeBytes} bytes");

        return result;
    }

    public async Task AbortAsync()
    {
        _isRecording = false;

        await Task.Run(() =>
        {
            lock (_writeLock)
            {
                try
                {
                    StopAndReleaseAudioEncoder();
                    StopAndReleaseMuxer(false);
                }
                catch { }
            }
        });

        // Delete partial file
        if (File.Exists(_outputPath))
        {
            try { File.Delete(_outputPath); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_writeLock)
        {
            StopAndReleaseAudioEncoder();
            StopAndReleaseMuxer(false);
        }
    }

    private long GetSampleDurationUs(AudioSample sample)
    {
        int sampleCount = sample.SampleCount;
        if (sampleCount <= 0)
        {
            return 1;
        }

        return Math.Max(1L, (sampleCount * 1_000_000L) / Math.Max(1, sample.SampleRate));
    }

    private long CalculateInputPresentationTimeUs(long timestampNs)
    {
        if (timestampNs < 0)
        {
            timestampNs = 0;
        }

        if (_firstInputTimestampNs < 0)
        {
            _firstInputTimestampNs = timestampNs;
        }

        long relativeUs = (timestampNs - _firstInputTimestampNs) / 1000;
        if (relativeUs < 0)
        {
            relativeUs = 0;
        }

        if (relativeUs <= _lastQueuedInputPresentationTimeUs)
        {
            relativeUs = _lastQueuedInputPresentationTimeUs + 1;
        }

        return relativeUs;
    }

    private bool QueueEndOfStreamInputBuffer()
    {
        if (_audioEncoder == null)
        {
            return false;
        }

        long eosPresentationTimeUs = Math.Max(0, _lastQueuedInputPresentationTimeUs + 1);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int inputBufferIndex = _audioEncoder.DequeueInputBuffer(10000);
            if (inputBufferIndex >= 0)
            {
                _audioEncoder.QueueInputBuffer(inputBufferIndex, 0, 0, eosPresentationTimeUs, MediaCodecBufferFlags.EndOfStream);
                return true;
            }

            DrainEncoder(false);
        }

        return false;
    }

    private void StopAndReleaseAudioEncoder()
    {
        var encoder = _audioEncoder;
        _audioEncoder = null;

        if (encoder == null)
        {
            return;
        }

        try
        {
            encoder.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioOnlyEncoderAndroid] Encoder stop warning: {ex.Message}");
        }

        try
        {
            encoder.Release();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioOnlyEncoderAndroid] Encoder release warning: {ex.Message}");
        }
    }

    private void StopAndReleaseMuxer(bool stopMuxer)
    {
        var muxer = _muxer;
        _muxer = null;
        _muxerStarted = false;

        if (muxer == null)
        {
            return;
        }

        if (stopMuxer)
        {
            try
            {
                muxer.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioOnlyEncoderAndroid] Muxer stop warning: {ex.Message}");
            }
        }

        try
        {
            muxer.Release();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioOnlyEncoderAndroid] Muxer release warning: {ex.Message}");
        }
    }
}
