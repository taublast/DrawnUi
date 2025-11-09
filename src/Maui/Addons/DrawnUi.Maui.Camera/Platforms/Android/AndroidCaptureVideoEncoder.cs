#if ANDROID
using System;
using System.IO;
using System.Threading.Tasks;
using Android.Media;
using Android.Opengl;
using Android.Views;
using Java.Nio;
using SkiaSharp;
using SkiaSharp.Views.Android;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Android implementation of capture video encoding using MediaCodec with Surface input (GPU path).
    /// Pattern matches Windows encoder: BeginFrame/SubmitFrame to draw overlays into the encoder surface via Skia/GL.
    /// </summary>
    public class AndroidCaptureVideoEncoder : ICaptureVideoEncoder
    {
        private string _outputPath;
        private int _width;
        private int _height;
        private int _frameRate;
        private bool _recordAudio;
        private int _deviceRotation = 0;

        private MediaCodec _videoCodec;
        private MediaMuxer _muxer;
        private int _videoTrackIndex = -1;
        private bool _muxerStarted = false;
        private Surface _inputSurface;

        // EGL / GL / Skia
        private EGLDisplay _eglDisplay = EGL14.EglNoDisplay;
        private EGLContext _eglContext = EGL14.EglNoContext;
        private EGLSurface _eglSurface = EGL14.EglNoSurface;
        private GRContext _grContext;
        private SKSurface _skSurface;
        private SKImageInfo _skInfo;

        private bool _isRecording;
        private DateTime _startTime;
        private System.Threading.Timer _progressTimer;
        private TimeSpan _pendingTimestamp;

        // Drop the very first encoded sample; some devices emit a garbled first frame right after start
        private bool _skipFirstEncodedSample = true;

        // Preview-from-recording support (Android uses CPU raster mirror to avoid cross-context GPU sharing)
        private readonly object _previewLock = new();
        private SKImage _latestPreviewImage; // CPU-backed, ownership transferred to UI on TryAcquire
        public event EventHandler PreviewAvailable;

        public bool IsRecording => _isRecording;
        public event EventHandler<TimeSpan> ProgressReported;

        // Interface implementation - required by ICaptureVideoEncoder
        public Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio)
        {
            return InitializeAsync(outputPath, width, height, frameRate, recordAudio, 0);
        }

        // Extended version with device rotation support
        public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio, int deviceRotation)
        {
            _outputPath = outputPath;
            _width = Math.Max(16, width);
            _height = Math.Max(16, height);
            _frameRate = Math.Max(1, frameRate);
            _deviceRotation = deviceRotation;
            System.Diagnostics.Debug.WriteLine($"[ENC INIT] {_width}x{_height}@{_frameRate} rotation={_deviceRotation}° output={_outputPath} recordAudio={recordAudio}");

            _recordAudio = recordAudio; // not handled in this first Android step

            // Prepare MediaCodec H.264 encoder with Surface input
            var format = MediaFormat.CreateVideoFormat(MediaFormat.MimetypeVideoAvc, _width, _height);
            format.SetInteger(MediaFormat.KeyColorFormat, (int)MediaCodecCapabilities.Formatsurface);
            format.SetInteger(MediaFormat.KeyBitRate, Math.Max(_width * _height * 4, 2_000_000)); // simple bitrate rule
            format.SetInteger(MediaFormat.KeyFrameRate, _frameRate);
            format.SetInteger(MediaFormat.KeyIFrameInterval, 1); // 1 sec GOP

            _videoCodec = MediaCodec.CreateEncoderByType(MediaFormat.MimetypeVideoAvc);
            _videoCodec.Configure(format, null, null, MediaCodecConfigFlags.Encode);

            _inputSurface = _videoCodec.CreateInputSurface();

            // MediaMuxer for MP4
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);

            // Set orientation hint for proper video playback (zero performance cost, metadata only)
            // This tells video players how to rotate the video for display
            _muxer.SetOrientationHint(_deviceRotation);

            _videoCodec.Start();

            // Set up EGL context bound to the codec surface
            SetupEglForCodecSurface();

            await Task.CompletedTask;
        }

        public Task StartAsync()
        {
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
        /// Begin a GPU frame into the encoder's EGLSurface, returning a Skia canvas to draw overlays.
        /// </summary>
        public IDisposable BeginFrame(TimeSpan timestamp, out SKCanvas canvas, out SKImageInfo info)
        {
            _pendingTimestamp = timestamp;
            MakeCurrent();
            System.Diagnostics.Debug.WriteLine($"[ENC BEGIN] viewport={_width}x{_height} ts={_pendingTimestamp.TotalMilliseconds:F0}ms");

            GLES20.GlViewport(0, 0, _width, _height);

            if (_grContext == null)
            {
                var glInterface = GRGlInterface.Create();
                _grContext = GRContext.CreateGl(glInterface);
            }

            if (_skSurface == null || _skInfo.Width != _width || _skInfo.Height != _height)
            {
                _skInfo = new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);

                // Build a backend render target for the current framebuffer
                int[] fb = new int[1];
                GLES20.GlGetIntegerv(GLES20.GlFramebufferBinding, fb, 0);
                int[] samples = new int[1];
                GLES20.GlGetIntegerv(GLES20.GlSamples, samples, 0);
                int[] stencil = new int[1];
                GLES20.GlGetIntegerv(GLES20.GlStencilBits, stencil, 0);

                var fbInfo = new GRGlFramebufferInfo((uint)fb[0], 0x8058); // GL_RGBA8 = 0x8058
                var backendRT = new GRBackendRenderTarget(_width, _height, samples[0], stencil[0], fbInfo);

                _skSurface?.Dispose();
                _skSurface = SKSurface.Create(_grContext, backendRT, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
            }

            canvas = _skSurface.Canvas;
            canvas.Clear(SKColors.Transparent);
            info = _skInfo;
            return new FrameScope();
        }

        /// <summary>
        /// Flush Skia, set EGL presentation time, swap buffers, and drain encoder output to muxer.
        /// </summary>
        public async Task SubmitFrameAsync()
        {
            if (!_isRecording) return;

            MakeCurrent();
            _skSurface?.Canvas?.Flush();
            _grContext?.Flush();

            // Publish a small CPU-backed preview snapshot for UI (mirror of composed frame)
            SKImage keepAlive = null;
            try
            {
                using var gpuSnap = _skSurface?.Snapshot();
                if (gpuSnap != null)
                {
                    const int maxPreviewWidth = 480; // keep light
                    int pw = Math.Min(_width, maxPreviewWidth);
                    int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));

                    var pInfo = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var rasterSurface = SKSurface.Create(pInfo); // CPU surface
                    var pCanvas = rasterSurface.Canvas;
                    pCanvas.Clear(SKColors.Transparent);
                    pCanvas.DrawImage(gpuSnap, new SKRect(0, 0, pw, ph));

                    keepAlive = rasterSurface.Snapshot(); // CPU-backed image
                    lock (_previewLock)
                    {
                        _latestPreviewImage?.Dispose();
                        _latestPreviewImage = keepAlive;
                        keepAlive = null; // ownership transferred
                    }
                    PreviewAvailable?.Invoke(this, EventArgs.Empty);
                }
            }
            catch { keepAlive?.Dispose(); keepAlive = null; }
            finally { keepAlive?.Dispose(); }

            long ptsNanos = (long)(_pendingTimestamp.TotalMilliseconds * 1_000_000.0);
            EGLExt.EglPresentationTimeANDROID(_eglDisplay, _eglSurface, ptsNanos);
            EGL14.EglSwapBuffers(_eglDisplay, _eglSurface);

            DrainEncoder(endOfStream: false);

            // Unbind so the context isn't current to this thread between frames.
            try { EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext); } catch { }

            await Task.CompletedTask;
        }

        public bool TryAcquirePreviewImage(out SKImage image)
        {
            lock (_previewLock)
            {
                image = _latestPreviewImage;
                _latestPreviewImage = null; // transfer ownership
                return image != null;
            }
        }

        public async Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp)
        {
            // CPU fallback not used in Android GPU path; keep as no-op for interface compatibility.
            await Task.CompletedTask;
        }

        public async Task PrependBufferedEncodedDataAsync(PrerecordingEncodedBuffer prerecordingBuffer)
        {
            if (!_isRecording || _videoCodec == null || prerecordingBuffer == null)
                return;

            try
            {
                // Write pre-encoded data - this is a no-op for current implementation
                // which requires GPU encoding. Full implementation would write prerecordingBuffer data.
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidCaptureVideoEncoder] PrependBufferedEncodedDataAsync failed: {ex.Message}");
            }
        }

        public async Task<CapturedVideo> StopAsync()
        {
            _isRecording = false;
            _progressTimer?.Dispose();

            try
            {
                // Signal end-of-stream to flush codec
                _videoCodec?.SignalEndOfInputStream();
                DrainEncoder(endOfStream: true);

                if (_muxerStarted)
                {
                    try { _muxer.Stop(); } catch { }
                }
            }
            finally
            {
                TryReleaseCodec();
                TryReleaseMuxer();
                TearDownEgl();
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

        private void DrainEncoder(bool endOfStream)
        {
            var bufferInfo = new MediaCodec.BufferInfo();
            while (true)
            {
                int outIndex = _videoCodec.DequeueOutputBuffer(bufferInfo, 0);
                if (outIndex == (int)MediaCodecInfoState.TryAgainLater)
                {
                    if (!endOfStream) break;
                }
                else if (outIndex == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    if (_muxerStarted) continue;
                    var newFormat = _videoCodec.OutputFormat;
                    _videoTrackIndex = _muxer.AddTrack(newFormat);
                    _muxer.Start();
                    _muxerStarted = true;
                }
                else if (outIndex >= 0)
                {
                    var encodedData = _videoCodec.GetOutputBuffer(outIndex);
                    if ((bufferInfo.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                    {
                        // Codec config data; ignore
                        bufferInfo.Size = 0;
                    }
                    if (bufferInfo.Size != 0 && _muxerStarted)
                    {
                        // On some devices the very first sample after start is visually corrupt.
                        // Skip exactly one sample to avoid a glitch at t=0.
                        if (_skipFirstEncodedSample)
                        {
                            _skipFirstEncodedSample = false;
                        }
                        else
                        {
                            encodedData.Position(bufferInfo.Offset);
                            encodedData.Limit(bufferInfo.Offset + bufferInfo.Size);
                            _muxer.WriteSampleData(_videoTrackIndex, encodedData, bufferInfo);
                        }
                    }
                    _videoCodec.ReleaseOutputBuffer(outIndex, false);

                    if ((bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                        break;
                }
                else
                {
                    break;
                }
            }
        }

        private void SetupEglForCodecSurface()
        {
            _eglDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
            int[] version = new int[2];
            if (!EGL14.EglInitialize(_eglDisplay, version, 0, version, 1))
                throw new InvalidOperationException("EGL initialize failed");

            int[] attribList = {
                EGL14.EglRedSize, 8,
                EGL14.EglGreenSize, 8,
                EGL14.EglBlueSize, 8,
                EGL14.EglAlphaSize, 8,
                EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
                EGL14.EglSurfaceType, EGL14.EglWindowBit,
                EGL14.EglNone
            };
            EGLConfig[] configs = new EGLConfig[1];
            int[] numConfigs = new int[1];
            if (!EGL14.EglChooseConfig(_eglDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0))
                throw new InvalidOperationException("EGL choose config failed");

            int[] ctxAttribs = { EGL14.EglContextClientVersion, 2, EGL14.EglNone };
            _eglContext = EGL14.EglCreateContext(_eglDisplay, configs[0], EGL14.EglNoContext, ctxAttribs, 0);
            if (_eglContext == null)
                throw new InvalidOperationException("EGL create context failed");

            int[] surfaceAttribs = { EGL14.EglNone };
            _eglSurface = EGL14.EglCreateWindowSurface(_eglDisplay, configs[0], _inputSurface, surfaceAttribs, 0);
            if (_eglSurface == null)
                throw new InvalidOperationException("EGL create window surface failed");

            // Do NOT bind here. We'll bind per-frame on the calling thread to avoid EGL_BAD_ACCESS
        }

        private void MakeCurrent()
        {
            if (!EGL14.EglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext))
                throw new InvalidOperationException("EGL make current failed");
        }

        private sealed class FrameScope : IDisposable { public void Dispose() { } }

        private void TryReleaseCodec()
        {
            try
            {
                _videoCodec?.Stop();
            }
            catch { }
            try
            {
                _videoCodec?.Release();
            }
            catch { }
            _videoCodec = null;
            try
            {
                _inputSurface?.Release();
            }
            catch { }
            _inputSurface = null;
        }

        private void TryReleaseMuxer()
        {
            try { _muxer?.Release(); } catch { }
            _muxer = null;
            _muxerStarted = false;
            _videoTrackIndex = -1;
        }

        private void TearDownEgl()
        {
            try { MakeCurrent(); } catch { }
            try { _skSurface?.Dispose(); } catch { }
            _skSurface = null;
            try { _grContext?.Dispose(); } catch { }
            _grContext = null;

            if (_eglDisplay != EGL14.EglNoDisplay)
            {
                EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
                if (_eglSurface != EGL14.EglNoSurface) EGL14.EglDestroySurface(_eglDisplay, _eglSurface);
                if (_eglContext != EGL14.EglNoContext) EGL14.EglDestroyContext(_eglDisplay, _eglContext);
                EGL14.EglTerminate(_eglDisplay);
            }
            _eglDisplay = EGL14.EglNoDisplay;
            _eglSurface = EGL14.EglNoSurface;
            _eglContext = EGL14.EglNoContext;

            // Release any pending preview image
            lock (_previewLock)
            {
                _latestPreviewImage?.Dispose();
                _latestPreviewImage = null;
            }
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                try { StopAsync().GetAwaiter().GetResult(); } catch { }
            }
            _progressTimer?.Dispose();
            TryReleaseCodec();
            TryReleaseMuxer();
            TearDownEgl();
        }
    }
}
#endif

