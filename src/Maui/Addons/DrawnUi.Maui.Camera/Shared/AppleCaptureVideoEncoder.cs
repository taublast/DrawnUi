#if IOS || MACCATALYST
using System;
using System.IO;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using CoreVideo;
using Foundation;
using SkiaSharp;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Apple implementation of capture video encoding using AVAssetWriter.
    /// GPU-first: Compose overlays via Skia; read back into a reused CVPixelBuffer for hardware encoding.
    /// Provides PreviewAvailable event and TryAcquirePreviewImage for mirror-to-preview like Windows/Android.
    /// </summary>
    public class AppleCaptureVideoEncoder : ICaptureVideoEncoder
    {
        private AVAssetWriter _writer;
        private AVAssetWriterInput _videoInput;
        private AVAssetWriterInputPixelBufferAdaptor _pixelBufferAdaptor;

        private string _outputPath;
        private int _width;
        private int _height;
        private int _frameRate;
        private bool _recordAudio;

        private bool _isRecording;
        private DateTime _startTime;
        private System.Threading.Timer _progressTimer;

        // Composition surface
        private GRContext _grContext;
        private SKSurface _surface;
        private SKImageInfo _info;
        private readonly object _frameLock = new();
        private TimeSpan _pendingTimestamp;

        // Mirror-to-preview support
        private readonly object _previewLock = new();
        private SKImage _latestPreviewImage; // ownership transferred to caller on TryAcquire
        public event EventHandler PreviewAvailable;

        public bool IsRecording => _isRecording;
        public event EventHandler<TimeSpan> ProgressReported;

        public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio)
        {
            _outputPath = outputPath;
            _width = Math.Max(16, width);
            _height = Math.Max(16, height);
            _frameRate = Math.Max(1, frameRate);
            _recordAudio = recordAudio; // audio not handled in this first Apple iteration

            // Prepare output directory
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            if (File.Exists(_outputPath))
            {
                try { File.Delete(_outputPath); } catch { }
            }

            var url = NSUrl.FromFilename(_outputPath);
            _writer = new AVAssetWriter(url, "public.mpeg-4", out var err);
            if (_writer == null || err != null)
                throw new InvalidOperationException($"AVAssetWriter failed: {err?.LocalizedDescription}");

            // Video settings (H.264)
            var videoSettings = new AVVideoSettingsCompressed
            {
                Codec = AVVideoCodec.H264,
                Width = _width,
                Height = _height
            };

            _videoInput = new AVAssetWriterInput(AVMediaTypes.Video.GetConstant(), videoSettings)
            {
                ExpectsMediaDataInRealTime = true
            };
            if (!_writer.CanAddInput(_videoInput))
                throw new InvalidOperationException("Cannot add video input to AVAssetWriter");
            _writer.AddInput(_videoInput);

            _pixelBufferAdaptor = new AVAssetWriterInputPixelBufferAdaptor(_videoInput,
                new CVPixelBufferAttributes
                {
                    PixelFormatType = CVPixelFormatType.CV32BGRA,
                    Width = _width,
                    Height = _height
                });

            await Task.CompletedTask;
        }

        public Task StartAsync()
        {
            if (_isRecording) return Task.CompletedTask;

            if (!_writer.StartWriting())
                throw new InvalidOperationException($"AVAssetWriter StartWriting failed: {_writer.Error?.LocalizedDescription}");

            var start = CMTime.FromSeconds(0, 1_000_000);
            _writer.StartSessionAtSourceTime(start);

            _isRecording = true;
            _startTime = DateTime.Now;
            _progressTimer = new System.Threading.Timer(_ =>
            {
                if (_isRecording)
                {
                    var elapsed = DateTime.Now - _startTime;
                    ProgressReported?.Invoke(this, elapsed);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Begin a GPU frame for overlay composition. Returns a canvas bound to the encoder's surface.
        /// </summary>
        public IDisposable BeginFrame(TimeSpan timestamp, out SKCanvas canvas, out SKImageInfo info, int orientation)
        {
            lock (_frameLock)
            {
                _pendingTimestamp = timestamp;

                if (_surface == null || _info.Width != _width || _info.Height != _height)
                {
                    _info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    _surface?.Dispose();
                    // CPU surface to maximize compatibility; GPU hookup can be added later
                    _surface = SKSurface.Create(_info);
                }

                canvas = _surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                info = _info;

                return new FrameScope();
            }
        }

        /// <summary>
        /// Flush Skia, publish preview snapshot, and append to AVAssetWriter using a reused CVPixelBuffer.
        /// </summary>
        public async Task SubmitFrameAsync()
        {
            if (!_isRecording) return;

            SKImage snapshot = null;
            CVPixelBuffer pixelBuffer = null;
            try
            {
                lock (_frameLock)
                {
                    if (_surface == null) return;

                    _surface.Canvas.Flush();

                    // Publish CPU-backed preview snapshot (small, to reduce pressure)
                    using var gpuSnap = _surface.Snapshot();
                    if (gpuSnap != null)
                    {
                        int pw = Math.Min(_width, 480);
                        int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));
                        var pInfo = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
                        using var raster = SKSurface.Create(pInfo);
                        raster.Canvas.Clear(SKColors.Transparent);
                        raster.Canvas.DrawImage(gpuSnap, new SKRect(0, 0, pw, ph));
                        snapshot = raster.Snapshot();
                        lock (_previewLock)
                        {
                            _latestPreviewImage?.Dispose();
                            _latestPreviewImage = snapshot;
                            snapshot = null; // transfer ownership
                        }
                        PreviewAvailable?.Invoke(this, EventArgs.Empty);
                    }
                }

                if (!_videoInput.ReadyForMoreMediaData)
                    return; // backpressure: drop frame, keep stream real-time

                // Allocate from pool and write pixels
                CVReturn errCode = CVReturn.Error;
                pixelBuffer = null;
                var pool = _pixelBufferAdaptor?.PixelBufferPool;
                if (pool == null)
                    return;
                pixelBuffer = pool.CreatePixelBuffer(null, out errCode);
                if (pixelBuffer == null || errCode != CVReturn.Success)
                    return;

                pixelBuffer.Lock(CVPixelBufferLock.None);
                try
                {
                    var baseAddress = pixelBuffer.BaseAddress;
                    var bytesPerRow = (int)pixelBuffer.BytesPerRow;

                    // Read pixels into CVPixelBuffer (BGRA, premul). Reuse the same buffer object from pool to avoid GC churn.
                    lock (_frameLock)
                    {
                        if (_surface == null) return;
                        var srcInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        if (!_surface.ReadPixels(srcInfo, baseAddress, bytesPerRow, 0, 0))
                            return;
                    }
                }
                finally
                {
                    pixelBuffer.Unlock(CVPixelBufferLock.None);
                }

                var ts = CMTime.FromSeconds(_pendingTimestamp.TotalSeconds, 1_000_000);
                if (!_pixelBufferAdaptor.AppendPixelBufferWithPresentationTime(pixelBuffer, ts))
                {
                    // Drop silently on failure (keep real-time)
                }
            }
            finally
            {
                snapshot?.Dispose();
                pixelBuffer?.Dispose();
            }

            await Task.CompletedTask;
        }

        public bool TryAcquirePreviewImage(out SKImage image)
        {
            lock (_previewLock)
            {
                image = _latestPreviewImage;
                _latestPreviewImage = null;
                return image != null;
            }
        }

        public Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp)
        {
            // CPU fallback not used in Apple GPU path; keep for interface compatibility.
            return Task.CompletedTask;
        }

        public async Task<CapturedVideo> StopAsync()
        {
            _isRecording = false;
            _progressTimer?.Dispose();

            try
            {
                _videoInput?.MarkAsFinished();
                if (_writer?.Status == AVAssetWriterStatus.Writing)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _writer?.FinishWriting(() => tcs.TrySetResult(true));
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                _pixelBufferAdaptor = null;
                _videoInput?.Dispose(); _videoInput = null;
                _writer?.Dispose(); _writer = null;

                lock (_previewLock)
                {
                    _latestPreviewImage?.Dispose();
                    _latestPreviewImage = null;
                }

                _surface?.Dispose(); _surface = null;
                _grContext?.Dispose(); _grContext = null;
            }

            var info = new FileInfo(_outputPath);
            return new CapturedVideo
            {
                FilePath = _outputPath,
                FileSizeBytes = info.Exists ? info.Length : 0,
                Duration = DateTime.Now - _startTime,
                Time = _startTime
            };
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                try { StopAsync().GetAwaiter().GetResult(); } catch { }
            }
            _progressTimer?.Dispose();
        }

        private sealed class FrameScope : IDisposable { public void Dispose() { } }
    }
}
#endif

