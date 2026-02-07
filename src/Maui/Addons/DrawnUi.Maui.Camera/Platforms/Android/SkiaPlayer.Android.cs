#if ANDROID

using Android.Media;
using Java.Nio;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace DrawnUi.Camera;

public partial class SkiaPlayer
{
    private MediaExtractor _extractor;
    private MediaCodec _videoDecoder;
    private MediaCodec _audioDecoder;
    private SKBitmap _currentFrame;
    private Thread _playbackThread;
    private CancellationTokenSource _cancellation;
    private bool _videoLoaded;
    private int _videoTrackIndex = -1;
    private int _audioTrackIndex = -1;
    private long _videoDurationUs;
    private double _frameRate = 30.0;

    partial void OnSourceChanged(string source)
    {
        if (!string.IsNullOrEmpty(source))
        {
            Task.Run(() => LoadAsync());
        }
    }

    partial void OnIsPlayingChanged(bool isPlaying)
    {
        if (isPlaying)
        {
            StartPlayback();
        }
        else
        {
            StopPlayback();
        }
    }

    partial void OnPositionChanged(TimeSpan position)
    {
        // Seek will be handled by platform-specific implementation
    }

    partial void OnVolumeChanged(double volume)
    {
        // Volume control would need audio track implementation
    }

    private async Task LoadVideoPlatformAsync(string source)
    {
        try
        {
            _extractor = new MediaExtractor();
            _extractor.SetDataSource(source);

            // Find video and audio tracks
            for (int i = 0; i < _extractor.TrackCount; i++)
            {
                var format = _extractor.GetTrackFormat(i);
                var mime = format.GetString(MediaFormat.KeyMime);

                if (mime.StartsWith("video/") && _videoTrackIndex == -1)
                {
                    _videoTrackIndex = i;
                    _videoDurationUs = format.GetLong(MediaFormat.KeyDuration, 0);

                    // Try to get frame rate
                    if (format.ContainsKey(MediaFormat.KeyFrameRate))
                    {
                        _frameRate = format.GetInteger(MediaFormat.KeyFrameRate, 30);
                    }

                    // Initialize video decoder
                    _videoDecoder = MediaCodec.CreateDecoderByType(mime);
                    _videoDecoder.Configure(format, null, null, MediaCodecConfigFlags.None);
                    _videoDecoder.Start();
                }
                else if (mime.StartsWith("audio/") && _audioTrackIndex == -1)
                {
                    _audioTrackIndex = i;

                    // Initialize audio decoder
                    _audioDecoder = MediaCodec.CreateDecoderByType(mime);
                    _audioDecoder.Configure(format, null, null, MediaCodecConfigFlags.None);
                    _audioDecoder.Start();
                }
            }

            if (_videoTrackIndex >= 0)
            {
                _extractor.SelectTrack(_videoTrackIndex);
                Duration = TimeSpan.FromTicks(_videoDurationUs * 10); // Convert microseconds to ticks
                _videoLoaded = true;
            }

            Debug.WriteLine($"[SkiaPlayer] Loaded video: duration={Duration.TotalSeconds:F2}s, fps={_frameRate}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaPlayer] Load error: {ex.Message}");
            throw;
        }
    }

    async partial void PlayPlatformAsync()
    {
        if (_videoLoaded)
        {
            StartPlayback();
        }
    }

    async partial void PausePlatformAsync()
    {
        StopPlayback();
    }

    async partial void StopPlatformAsync()
    {
        StopPlayback();
        Position = TimeSpan.Zero;
        _extractor?.SeekTo(0, MediaExtractorSeekTo.ClosestSync);
    }

    async partial void SeekPlatformAsync(TimeSpan position)
    {
        if (_extractor != null)
        {
            var positionUs = (long)(position.TotalSeconds * 1_000_000);
            _extractor.SeekTo(positionUs, MediaExtractorSeekTo.ClosestSync);
        }
    }

    private void StartPlayback()
    {
        if (_playbackThread != null && _playbackThread.IsAlive)
            return;

        _cancellation = new CancellationTokenSource();
        _playbackThread = new Thread(PlaybackLoop);
        _playbackThread.Start();
    }

    private void StopPlayback()
    {
        _cancellation?.Cancel();
        _playbackThread?.Join(100); // Wait up to 100ms
    }

    private void PlaybackLoop()
    {
        try
        {
            var bufferInfo = new MediaCodec.BufferInfo();
            var frameDurationMs = 1000.0 / _frameRate;

            while (!_cancellation.IsCancellationRequested && IsPlaying)
            {
                // Read encoded sample
                var sampleSize = _extractor.ReadSampleData(Java.Nio.ByteBuffer.Allocate(1024 * 1024), 0);
                if (sampleSize < 0)
                {
                    // End of stream
                    if (IsLooping)
                    {
                        _extractor.SeekTo(0, MediaExtractorSeekTo.ClosestSync);
                        Position = TimeSpan.Zero;
                        continue;
                    }
                    else
                    {
                        IsPlaying = false;
                        PlaybackEnded?.Invoke(this, EventArgs.Empty);
                        break;
                    }
                }

                var presentationTimeUs = _extractor.SampleTime;
                Position = TimeSpan.FromTicks(presentationTimeUs * 10);

                // Feed to decoder
                var inputIndex = _videoDecoder.DequeueInputBuffer(10000);
                if (inputIndex >= 0)
                {
                    var inputBuffer = _videoDecoder.GetInputBuffer(inputIndex);
                    inputBuffer.Clear();
                    _extractor.ReadSampleData(inputBuffer, 0);

                    _videoDecoder.QueueInputBuffer(inputIndex, 0, sampleSize, presentationTimeUs, 0);
                }

                // Get decoded frame
                var outputIndex = _videoDecoder.DequeueOutputBuffer(bufferInfo, 10000);
                if (outputIndex >= 0)
                {
                    var outputBuffer = _videoDecoder.GetOutputBuffer(outputIndex);

                    // Convert YUV to RGB bitmap (simplified - would need proper YUV->RGB conversion)
                    _currentFrame = DecodeYuvToBitmap(outputBuffer, bufferInfo.Size);

                    // Trigger UI update on main thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Invalidate(); // Redraw the control
                    });

                    _videoDecoder.ReleaseOutputBuffer(outputIndex, false);
                }

                _extractor.Advance();

                // Wait for next frame
                Thread.Sleep((int)frameDurationMs);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaPlayer] Playback error: {ex.Message}");
        }
    }

    private SKBitmap DecodeYuvToBitmap(ByteBuffer yuvData, int size)
    {
        // This is a simplified placeholder - real implementation would need
        // proper YUV to RGB conversion using RenderScript or similar
        // For now, return a placeholder bitmap
        return new SKBitmap(640, 480);
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        if (_currentFrame != null)
        {
            // Draw current video frame
            var rect = new SKRect(0, 0, ctx.Destination.Width, ctx.Destination.Height);
            ctx.Context.Canvas.DrawBitmap(_currentFrame, rect);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        StopPlayback();

        _videoDecoder?.Stop();
        _videoDecoder?.Release();
        _audioDecoder?.Stop();
        _audioDecoder?.Release();
        _extractor?.Release();

        _currentFrame?.Dispose();
    }
}

#endif
