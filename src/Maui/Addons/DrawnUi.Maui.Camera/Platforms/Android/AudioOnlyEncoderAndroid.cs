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
                // Get input buffer
                int inputBufferIndex = _audioEncoder.DequeueInputBuffer(10000); // 10ms timeout
                if (inputBufferIndex < 0)
                {
                    Debug.WriteLine("[AudioOnlyEncoderAndroid] No input buffer available");
                    return;
                }

                var inputBuffer = _audioEncoder.GetInputBuffer(inputBufferIndex);
                if (inputBuffer == null)
                {
                    _audioEncoder.QueueInputBuffer(inputBufferIndex, 0, 0, 0, MediaCodecBufferFlags.None);
                    return;
                }

                inputBuffer.Clear();
                inputBuffer.Put(sample.Data);

                long presentationTimeUs = sample.TimestampNs / 1000; // ns to us
                _audioEncoder.QueueInputBuffer(inputBufferIndex, 0, sample.Data.Length, presentationTimeUs, MediaCodecBufferFlags.None);

                // Drain encoded output
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
            // Signal end of input
            int inputBufferIndex = _audioEncoder.DequeueInputBuffer(10000);
            if (inputBufferIndex >= 0)
            {
                _audioEncoder.QueueInputBuffer(inputBufferIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
            }
        }

        while (true)
        {
            int outputBufferIndex = _audioEncoder.DequeueOutputBuffer(_bufferInfo, endOfStream ? 5000 : 0);

            if (outputBufferIndex == (int)MediaCodecInfoState.TryAgainLater)
            {
                if (!endOfStream) break;
                continue;
            }

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

                _muxer.WriteSampleData(_audioTrackIndex, outputBuffer, _bufferInfo);
            }

            _audioEncoder.ReleaseOutputBuffer(outputBufferIndex, false);

            if ((_bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
            {
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

                    _audioEncoder?.Stop();
                    _audioEncoder?.Release();
                    _audioEncoder = null;

                    if (_muxerStarted)
                    {
                        _muxer?.Stop();
                    }
                    _muxer?.Release();
                    _muxer = null;
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
                    _audioEncoder?.Stop();
                    _audioEncoder?.Release();
                    _audioEncoder = null;

                    _muxer?.Release();
                    _muxer = null;
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
            _audioEncoder?.Release();
            _audioEncoder = null;
            _muxer?.Release();
            _muxer = null;
        }
    }
}
