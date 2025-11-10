#if IOS || MACCATALYST
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using CoreVideo;
using Foundation;
using VideoToolbox;
using SkiaSharp;
using ObjCRuntime;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Apple encoder using AVAssetWriter for MP4 output.
    ///
    /// Two Modes:
    /// 1. Normal Recording: AVAssetWriterInputPixelBufferAdaptor (AVAssetWriter compresses)
    /// 2. Pre-Recording: VTCompressionSession → Circular buffer (compressed H.264 in memory)
    ///
    /// Pipeline (Normal Recording):
    /// Skia → CVPixelBuffer → AVAssetWriterInputPixelBufferAdaptor → MP4
    ///
    /// Pipeline (Pre-Recording):
    /// Skia → CVPixelBuffer → VTCompressionSession → H.264 → PrerecordingEncodedBuffer (memory)
    /// </summary>
    public class AppleVideoToolboxEncoder : ICaptureVideoEncoder
    {
        private string _outputPath;
        private int _width;
        private int _height;
        private int _frameRate;
        private int _deviceRotation;
        private bool _recordAudio;
        private bool _isRecording;
        private DateTime _startTime;

        // Skia composition surface
        private SKSurface _surface;
        private SKImageInfo _info;
        private readonly object _frameLock = new();
        private TimeSpan _pendingTimestamp;

        // AVAssetWriter for MP4 output (normal recording)
        private AVAssetWriter _writer;
        private AVAssetWriterInput _videoInput;
        private AVAssetWriterInputPixelBufferAdaptor _pixelBufferAdaptor;
        private System.Threading.Timer _progressTimer;

        // VTCompressionSession for pre-recording buffer
        private VTCompressionSession _compressionSession;
        private PrerecordingEncodedBuffer _preRecordingBuffer;

        // Mirror-to-preview support
        private readonly object _previewLock = new();
        private SKImage _latestPreviewImage;
        public event EventHandler PreviewAvailable;

        // Statistics
        public int EncodedFrameCount { get; private set; }
        public long EncodedDataSize { get; private set; }
        public TimeSpan EncodingDuration { get; private set; }
        public string EncodingStatus { get; private set; } = "Idle";

        // ✅ No GCHandle needed - callback is instance method

        public bool IsRecording => _isRecording;

        // Interface properties
        public bool IsPreRecordingMode { get; set; }
        public SkiaCamera ParentCamera { get; set; }
        public event EventHandler<TimeSpan> ProgressReported;

        // ICaptureVideoEncoder interface
        public Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio)
        {
            return InitializeAsync(outputPath, width, height, frameRate, recordAudio, 0);
        }

        public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio, int deviceRotation)
        {
            _outputPath = outputPath;
            _width = Math.Max(16, width);
            _height = Math.Max(16, height);
            _frameRate = Math.Max(1, frameRate);
            _recordAudio = recordAudio;
            _deviceRotation = deviceRotation;

            // Prepare output directory
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            if (File.Exists(_outputPath))
            {
                try { File.Delete(_outputPath); } catch { }
            }

            // Initialize AVAssetWriter for MP4 output (normal recording)
            InitializeAssetWriter();

            // Initialize VTCompressionSession for pre-recording if needed
            if (IsPreRecordingMode && ParentCamera != null)
            {
                InitializeCompressionSession();

                // Initialize circular buffer for storing encoded frames
                var preRecordDuration = ParentCamera.PreRecordDuration;
                _preRecordingBuffer = new PrerecordingEncodedBuffer(preRecordDuration);

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording enabled: {preRecordDuration.TotalSeconds}s buffer");
            }

            await Task.CompletedTask;
        }

        private void InitializeAssetWriter()
        {
            // Create AVAssetWriter for MP4 container
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

            // Set transform based on device rotation to ensure correct playback orientation
            _videoInput.Transform = GetTransformForRotation(_deviceRotation);

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

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] AVAssetWriter initialized: {_width}x{_height} @ {_frameRate}fps");
        }

        private void InitializeCompressionSession()
        {
            // Source pixel buffer attributes (input format from Skia)
            var sourceAttributes = new CVPixelBufferAttributes
            {
                PixelFormatType = CVPixelFormatType.CV32BGRA,
                Width = _width,
                Height = _height
            };

            // Create VTCompressionSession for H.264 encoding
            _compressionSession = VTCompressionSession.Create(
                _width,
                _height,
                CMVideoCodecType.H264,
                CompressionOutputCallback,
                encoderSpecification: null,
                sourceImageBufferAttributes: sourceAttributes.Dictionary);

            if (_compressionSession == null)
                throw new InvalidOperationException($"VTCompressionSession.Create failed");

            // Enable real-time encoding
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.RealTime,
                new NSNumber(true));

            // Set H.264 High Profile
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.ProfileLevel,
                new NSNumber((int)VTProfileLevel.H264HighAutoLevel));

            // Set bitrate
            int bitrate = _width * _height * _frameRate / 10;
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.AverageBitRate,
                new NSNumber(bitrate));

            // Keyframe interval (1 per second)
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.MaxKeyFrameInterval,
                new NSNumber(_frameRate));

            // Disable B-frames
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.AllowFrameReordering,
                new NSNumber(false));

            _compressionSession.PrepareToEncodeFrames();

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] VTCompressionSession created for pre-recording");
        }

        private void CompressionOutputCallback(
            nint sourceFrame,
            VTStatus status,
            VTEncodeInfoFlags infoFlags,
            CMSampleBuffer sampleBuffer)
        {
            if (status != VTStatus.Ok)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Compression failed: {status}");
                return;
            }

            if (sampleBuffer == null || sampleBuffer.Handle == IntPtr.Zero)
                return;

            try
            {
                // Buffer the encoded H.264 frame in circular buffer
                BufferEncodedFrame(sampleBuffer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Callback error: {ex.Message}");
            }
        }

        private void BufferEncodedFrame(CMSampleBuffer sampleBuffer)
        {
            if (_preRecordingBuffer == null)
                return;

            // Extract H.264 data from sample buffer
            var blockBuffer = sampleBuffer.GetDataBuffer();
            if (blockBuffer == null || blockBuffer.DataLength == 0)
                return;

            try
            {
                // Get pointer to encoded data
                nint dataPointer = IntPtr.Zero;
                var result = blockBuffer.GetDataPointer(
                    offset: 0,
                    lengthAtOffset: out nuint lengthAtOffset,
                    totalLength: out nuint totalLength,
                    dataPointer: ref dataPointer);

                if (result != CMBlockBufferError.None || dataPointer == IntPtr.Zero)
                    return;

                // Copy H.264 bytes to managed array
                byte[] h264Data = new byte[totalLength];
                Marshal.Copy(dataPointer, h264Data, 0, (int)totalLength);

                // Add to circular buffer (will auto-drop old frames)
                // Timestamp is stored automatically as DateTime.UtcNow inside AddFrame
                _preRecordingBuffer.AddFrame(h264Data);

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Buffered frame: {h264Data.Length} bytes, buffer: {_preRecordingBuffer.Count} frames");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] BufferEncodedFrame error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a CGAffineTransform for the given device rotation.
        /// This sets video metadata for proper playback orientation without re-encoding.
        /// </summary>
        private CoreGraphics.CGAffineTransform GetTransformForRotation(int rotation)
        {
            var normalizedRotation = rotation % 360;
            if (normalizedRotation < 0)
                normalizedRotation += 360;

            var transform = CoreGraphics.CGAffineTransform.MakeIdentity();

            switch (normalizedRotation)
            {
                case 90:
                    // Rotate 90° clockwise: rotate then translate
                    transform = CoreGraphics.CGAffineTransform.MakeRotation((float)(Math.PI / 2));
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, 0, -_width);
                    break;

                case 180:
                    // Rotate 180°: rotate then translate
                    transform = CoreGraphics.CGAffineTransform.MakeRotation((float)Math.PI);
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, -_width, -_height);
                    break;

                case 270:
                    // Rotate 270° clockwise (90° counter-clockwise): rotate then translate
                    transform = CoreGraphics.CGAffineTransform.MakeRotation((float)(-Math.PI / 2));
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, -_height, 0);
                    break;

                default:
                    // 0° - no rotation needed
                    break;
            }

            return transform;
        }


        public async Task StartAsync()
        {
            if (_isRecording)
                return;

            if (!_writer.StartWriting())
                throw new InvalidOperationException($"AVAssetWriter StartWriting failed: {_writer.Error?.LocalizedDescription}");

            var start = CMTime.FromSeconds(0, 1_000_000);
            _writer.StartSessionAtSourceTime(start);

            _isRecording = true;
            _startTime = DateTime.Now;

            // Initialize statistics
            EncodedFrameCount = 0;
            EncodedDataSize = 0;
            EncodingDuration = TimeSpan.Zero;
            EncodingStatus = "Started";

            _progressTimer = new System.Threading.Timer(_ =>
            {
                if (_isRecording)
                {
                    var elapsed = DateTime.Now - _startTime;
                    ProgressReported?.Invoke(this, elapsed);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Recording started");

            // If we have pre-recorded buffered frames, prepend them to the output
            if (_preRecordingBuffer != null && _preRecordingBuffer.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Prepending {_preRecordingBuffer.Count} pre-recorded frames");
                await PrependBufferedEncodedDataAsync(_preRecordingBuffer);

                // Switch from pre-recording mode to normal recording mode
                IsPreRecordingMode = false;
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Switched to normal recording mode");
            }
        }

        /// <summary>
        /// Begin a frame for Skia composition. Returns canvas to draw on.
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
                    _surface = SKSurface.Create(_info);
                }

                canvas = _surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                info = _info;

                return new FrameScope();
            }
        }

        /// <summary>
        /// Submit the composed frame for encoding
        /// Routes to VTCompressionSession (pre-recording) or AVAssetWriter (normal recording)
        /// </summary>
        public async Task SubmitFrameAsync()
        {
            if (!_isRecording)
                return;

            SKImage snapshot = null;
            CVPixelBuffer pixelBuffer = null;

            try
            {
                // ============================================================================
                // CREATE PREVIEW SNAPSHOT
                // ============================================================================
                lock (_frameLock)
                {
                    if (_surface == null)
                        return;

                    _surface.Canvas.Flush();

                    // Create CPU-backed preview snapshot (downscaled to reduce memory)
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
                            snapshot = null; // Transfer ownership
                        }
                        PreviewAvailable?.Invoke(this, EventArgs.Empty);
                    }
                }

                // ============================================================================
                // ROUTE TO CORRECT ENCODER
                // ============================================================================

                if (IsPreRecordingMode && _compressionSession != null)
                {
                    // PRE-RECORDING MODE: Write to BOTH circular buffer AND file for muxing
                    // 1. VTCompressionSession → circular buffer in memory (for fast prepending)
                    await SubmitFrameToCompressionSession();

                    // 2. AVAssetWriter → pre_recording.mp4 file (for muxing with live recording)
                    await SubmitFrameToAssetWriter();
                }
                else
                {
                    // NORMAL RECORDING MODE: Use AVAssetWriter only
                    await SubmitFrameToAssetWriter();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] SubmitFrameAsync error: {ex.Message}");
            }
            finally
            {
                snapshot?.Dispose();
            }

            await Task.CompletedTask;
        }

        private async Task SubmitFrameToCompressionSession()
        {
            CVPixelBuffer pixelBuffer = null;

            try
            {
                // Create pixel buffer
                var attrs = new CVPixelBufferAttributes
                {
                    PixelFormatType = CVPixelFormatType.CV32BGRA,
                    Width = _width,
                    Height = _height
                };

                pixelBuffer = new CVPixelBuffer(_width, _height, CVPixelFormatType.CV32BGRA, attrs);
                if (pixelBuffer == null)
                    return;

                // Copy pixels
                pixelBuffer.Lock(CVPixelBufferLock.None);
                try
                {
                    IntPtr baseAddress = pixelBuffer.BaseAddress;
                    nint bytesPerRow = pixelBuffer.BytesPerRow;

                    lock (_frameLock)
                    {
                        if (_surface == null) return;
                        var srcInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        if (!_surface.ReadPixels(srcInfo, baseAddress, (int)bytesPerRow, 0, 0))
                            return;
                    }
                }
                finally
                {
                    pixelBuffer.Unlock(CVPixelBufferLock.None);
                }

                // Submit to VTCompressionSession
                CMTime presentationTime = CMTime.FromSeconds(_pendingTimestamp.TotalSeconds, 1_000_000);
                CMTime duration = CMTime.FromSeconds(1.0 / _frameRate, 1_000_000);

                var status = _compressionSession.EncodeFrame(
                    imageBuffer: pixelBuffer,
                    presentationTimestamp: presentationTime,
                    duration: duration,
                    frameProperties: null,
                    sourceFrame: 0,
                    infoFlags: out VTEncodeInfoFlags infoFlags);

                if (status != VTStatus.Ok)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] EncodeFrame failed: {status}");
                }
            }
            finally
            {
                pixelBuffer?.Dispose();
            }

            await Task.CompletedTask;
        }

        private async Task SubmitFrameToAssetWriter()
        {
            CVPixelBuffer pixelBuffer = null;

            try
            {
                if (!_videoInput.ReadyForMoreMediaData)
                    return; // Backpressure: drop frame

                // Allocate from pool
                CVReturn errCode = CVReturn.Error;
                var pool = _pixelBufferAdaptor?.PixelBufferPool;
                if (pool == null)
                    return;
                pixelBuffer = pool.CreatePixelBuffer(null, out errCode);
                if (pixelBuffer == null || errCode != CVReturn.Success)
                    return;

                // Copy pixels
                pixelBuffer.Lock(CVPixelBufferLock.None);
                try
                {
                    IntPtr baseAddress = pixelBuffer.BaseAddress;
                    int bytesPerRow = (int)pixelBuffer.BytesPerRow;

                    lock (_frameLock)
                    {
                        if (_surface == null) return;
                        SKImageInfo srcInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        if (!_surface.ReadPixels(srcInfo, baseAddress, bytesPerRow, 0, 0))
                            return;
                    }
                }
                finally
                {
                    pixelBuffer.Unlock(CVPixelBufferLock.None);
                }

                // Append to AVAssetWriter
                CMTime ts = CMTime.FromSeconds(_pendingTimestamp.TotalSeconds, 1_000_000);

                if (!_pixelBufferAdaptor.AppendPixelBufferWithPresentationTime(pixelBuffer, ts))
                {
                    // Drop silently on failure
                }
                else
                {
                    // Update statistics
                    EncodedFrameCount++;
                    if (pixelBuffer != null)
                    {
                        EncodedDataSize += (long)pixelBuffer.DataSize;
                    }
                    EncodingDuration = DateTime.Now - _startTime;
                    EncodingStatus = "Encoding";
                }
            }
            finally
            {
                pixelBuffer?.Dispose();
            }

            await Task.CompletedTask;
        }

        public async Task<CapturedVideo> StopAsync()
        {
            _isRecording = false;
            _progressTimer?.Dispose();

            // Update status
            EncodingStatus = "Stopping";

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
            }

            var info = new FileInfo(_outputPath);

            // Update final statistics
            EncodingStatus = "Completed";
            EncodingDuration = DateTime.Now - _startTime;

            return new CapturedVideo
            {
                FilePath = _outputPath,
                FileSizeBytes = info.Exists ? info.Length : 0,
                Duration = EncodingDuration,
                Time = _startTime
            };
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
            // CPU fallback not used in VideoToolbox GPU path; keep for interface compatibility
            return Task.CompletedTask;
        }

        public async Task PrependBufferedEncodedDataAsync(PrerecordingEncodedBuffer prerecordingBuffer)
        {
            if (prerecordingBuffer == null || prerecordingBuffer.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] No buffered frames to prepend");
                return;
            }

            try
            {
                // Get buffer stats for logging
                byte[] allData = prerecordingBuffer.GetBufferedData();
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording buffer: {prerecordingBuffer.Count} frames, {allData?.Length ?? 0} bytes");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Prepending buffered data not yet fully implemented - requires two-file muxing");

                // TODO: Implement one of these approaches:
                // 1. Write buffered H.264 to temporary MP4 file, then mux with main recording using AVAssetExportSession
                // 2. Reconstruct CMSampleBuffers from H.264 bytes and append to current AVAssetWriter
                //    (Requires proper .NET iOS API bindings for CMSampleBuffer creation from raw data)
                // 3. Use separate pre-recording encoder that writes to temp file continuously

                // For now, the circular buffer successfully stores H.264 frames in memory,
                // but prepending them requires additional muxing implementation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] PrependBufferedEncodedDataAsync error: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                try { StopAsync().GetAwaiter().GetResult(); } catch { }
            }
            _progressTimer?.Dispose();
            _compressionSession?.Dispose();
            _compressionSession = null;
            _preRecordingBuffer?.Dispose();
            _preRecordingBuffer = null;
        }

        private sealed class FrameScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
#endif
