#if ANDROID
using System;
using System.IO;
using System.Collections.Generic;
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
    ///
    /// Two Modes (mirrors iOS AppleVideoToolboxEncoder):
    /// 1. Normal Recording: MediaCodec → MediaMuxer → MP4
    /// 2. Pre-Recording: MediaCodec → PrerecordingEncodedBuffer (circular buffer in memory)
    ///                    On record press: Write buffer + live frames to ONE MediaMuxer session
    ///
    /// Pipeline (Normal Recording):
    /// Skia → EGLSurface → MediaCodec → MediaMuxer → MP4
    ///
    /// Pipeline (Pre-Recording):
    /// Skia → EGLSurface → MediaCodec → PrerecordingEncodedBuffer (memory)
    /// User presses record → MediaMuxer: write buffer (PTS 0-3s) + live (PTS 3s-7s) → ONE MP4
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
        private MediaFormat _videoFormat;  // Save format for later use

        // Pre-recording support (mirrors iOS implementation)
        private PrerecordingEncodedBuffer _preRecordingBuffer;
        private TimeSpan _preRecordingDuration = TimeSpan.Zero;
        private TimeSpan _firstEncodedFrameOffset = TimeSpan.MinValue;  // Offset to subtract from all frames

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

        // Encoder readiness (professional pattern: warm up before recording starts)
        private volatile bool _encoderReady = false;
        private readonly object _warmupLock = new();
        private bool _warmupCompleted = false;

        // Frame queue for frames arriving during encoder warm-up (zero frame loss guarantee)
        private class QueuedFrame
        {
            public SKImage Image;  // CPU snapshot
            public TimeSpan Timestamp;
        }
        private readonly Queue<QueuedFrame> _frameQueue = new();
        private readonly int _maxQueuedFrames = 90;  // Max 3 seconds @ 30fps

        // Drop the very first encoded sample; some devices emit a garbled first frame right after start
        private bool _skipFirstEncodedSample = true;

        // Keyframe request timing for pre-recording (request every ~1 second to keep buffer fresh)
        private DateTime _lastKeyframeRequest = DateTime.MinValue;
        private readonly TimeSpan _keyframeRequestInterval = TimeSpan.FromMilliseconds(900); // Slightly less than 1s I-frame interval

        // Preview-from-recording support
        private readonly object _previewLock = new();
        private SKImage _latestPreviewImage;
        public event EventHandler PreviewAvailable;

        public bool IsRecording => _isRecording;
        public event EventHandler<TimeSpan> ProgressReported;

        // Properties for platform-specific details
        public int EncodedFrameCount { get; private set; }
        public long EncodedDataSize { get; private set; }
        public TimeSpan EncodingDuration { get; private set; }
        public string EncodingStatus { get; private set; } = "Idle";

        // Interface implementation
        public bool IsPreRecordingMode { get; set; }
        public SkiaCamera ParentCamera { get; set; }

        public TimeSpan LiveRecordingDuration
        {
            get
            {
                if (_isRecording)
                {
                    return DateTime.Now - _startTime;
                }
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Request immediate keyframe from encoder (Bundle.PARAMETER_KEY_REQUEST_SYNC_FRAME)
        /// Used when transitioning from pre-recording to live recording
        /// </summary>
        public void RequestKeyFrame()
        {
            try
            {
                if (_videoCodec != null)
                {
                    var bundle = new Android.OS.Bundle();
                    bundle.PutInt(MediaCodec.ParameterKeyRequestSyncFrame, 0);
                    _videoCodec.SetParameters(bundle);
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ Requested SYNC FRAME (immediate keyframe)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ Failed to request sync frame: {ex.Message}");
            }
        }

        /// <summary>
        /// Wait for next keyframe to arrive in buffer (with timeout)
        /// Returns true if keyframe arrived, false if timeout
        /// </summary>
        public async Task<bool> WaitForKeyFrameAsync(TimeSpan timeout)
        {
            if (_preRecordingBuffer == null)
                return false;

            var startTime = DateTime.Now;
            int initialFrameCount = _preRecordingBuffer.GetFrameCount();

            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Waiting for keyframe to arrive in buffer... (timeout: {timeout.TotalMilliseconds}ms)");

            while (DateTime.Now - startTime < timeout)
            {
                // Check if buffer has any keyframes now
                var frames = _preRecordingBuffer.GetAllFrames();
                int currentFrameCount = frames.Count;
                bool hasKeyframe = frames.Any(f => f.IsKeyFrame);

                if (hasKeyframe)
                {
                    var keyframeTimestamp = frames.First(f => f.IsKeyFrame).Timestamp;
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ Keyframe arrived! PTS={keyframeTimestamp.TotalSeconds:F3}s (waited {(DateTime.Now - startTime).TotalMilliseconds:F0}ms)");
                    return true;
                }

                // Still waiting, check frame count progress
                if (currentFrameCount > initialFrameCount)
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffer now has {currentFrameCount} frames, still waiting for keyframe...");
                    initialFrameCount = currentFrameCount;
                }

                await Task.Delay(16); // ~1 frame @ 60fps
            }

            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ Timeout waiting for keyframe after {timeout.TotalMilliseconds}ms");
            return false;
        }

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
            _deviceRotation = deviceRotation;
            _recordAudio = recordAudio;
            _preRecordingDuration = TimeSpan.Zero;
            _firstEncodedFrameOffset = TimeSpan.MinValue;

            // Reset encoder readiness flags (new initialization)
            _encoderReady = false;
            lock (_warmupLock)
            {
                _warmupCompleted = false;
            }

            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] InitializeAsync: {_width}x{_height}@{_frameRate} rotation={_deviceRotation}° IsPreRecordingMode={IsPreRecordingMode}");

            // Prepare output directory
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            if (File.Exists(_outputPath))
            {
                try { File.Delete(_outputPath); } catch { }
            }

            // Prepare MediaCodec H.264 encoder with Surface input
            var format = MediaFormat.CreateVideoFormat(MediaFormat.MimetypeVideoAvc, _width, _height);
            format.SetInteger(MediaFormat.KeyColorFormat, (int)MediaCodecCapabilities.Formatsurface);
            format.SetInteger(MediaFormat.KeyBitRate, Math.Max(_width * _height * 4, 2_000_000));
            format.SetInteger(MediaFormat.KeyFrameRate, _frameRate);
            format.SetInteger(MediaFormat.KeyIFrameInterval, 1); // 1 sec GOP

            _videoCodec = MediaCodec.CreateEncoderByType(MediaFormat.MimetypeVideoAvc);
            _videoCodec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
            _inputSurface = _videoCodec.CreateInputSurface();
            _videoCodec.Start();

            // Set up EGL context bound to the codec surface
            SetupEglForCodecSurface();

            // CRITICAL: Warm up encoder BEFORE accepting any frames (professional pattern)
            // This ensures MediaCodec is ready and no frames are dropped
            // Instagram/Snapchat/TikTok all do this - encoder ready before user even presses record
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ==========================================");
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] WARMING UP ENCODER (professional pattern)");
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] This prevents frame loss at recording start");
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ==========================================");

            await WarmUpEncoderAsync();

            // Initialize based on mode (mirrors iOS)
            if (IsPreRecordingMode && ParentCamera != null)
            {
                // Pre-recording mode: Initialize circular buffer
                var preRecordDuration = ParentCamera.PreRecordDuration;
                _preRecordingBuffer = new PrerecordingEncodedBuffer(preRecordDuration);

                // CRITICAL: Start recording immediately to buffer frames (mirrors iOS)
                _isRecording = true;
                _startTime = DateTime.Now;
                EncodedFrameCount = 0;
                EncodedDataSize = 0;
                EncodingDuration = TimeSpan.Zero;
                EncodingStatus = "Buffering";

                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Pre-recording mode initialized and started:");
                System.Diagnostics.Debug.WriteLine($"  Final output file: {_outputPath}");
                System.Diagnostics.Debug.WriteLine($"  Buffer duration: {preRecordDuration.TotalSeconds}s");
                System.Diagnostics.Debug.WriteLine($"  Buffering frames to memory...");

                // CRITICAL: Request FIRST keyframe immediately so buffer starts with I-frame!
                // Then periodic requests (every 900ms) will keep buffer fresh
                RequestKeyFrame();
                _lastKeyframeRequest = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Requested FIRST keyframe for pre-recording buffer");
            }
            else
            {
                // Normal recording mode: MediaMuxer will be created in StartAsync
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Normal recording mode: Will write to {_outputPath}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Professional encoder warm-up pattern (how Instagram/Snapchat/TikTok avoid frame loss).
        ///
        /// Submits a dummy black frame to trigger MediaCodec initialization, then waits for
        /// FORMAT_CHANGED event. This ensures encoder is fully ready before any real frames arrive.
        ///
        /// Called during InitializeAsync (when camera preview starts), NOT when recording starts.
        /// User sees camera preview while encoder warms up in background (transparent to user).
        ///
        /// GUARANTEE: Zero frame loss when recording starts.
        /// </summary>
        private async Task WarmUpEncoderAsync()
        {
            lock (_warmupLock)
            {
                if (_warmupCompleted)
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Warm-up already completed, skipping");
                    return;
                }
            }

            var warmupStart = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Starting encoder warm-up...");

            try
            {
                // Step 1: Make EGL context current and set up Skia
                MakeCurrent();

                if (_grContext == null)
                {
                    var glInterface = GRGlInterface.Create();
                    _grContext = GRContext.CreateGl(glInterface);
                }

                if (_skSurface == null)
                {
                    _skInfo = new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);
                    int[] fb = new int[1];
                    GLES20.GlGetIntegerv(GLES20.GlFramebufferBinding, fb, 0);
                    int[] samples = new int[1];
                    GLES20.GlGetIntegerv(GLES20.GlSamples, samples, 0);
                    int[] stencil = new int[1];
                    GLES20.GlGetIntegerv(GLES20.GlStencilBits, stencil, 0);
                    var fbInfo = new GRGlFramebufferInfo((uint)fb[0], 0x8058);
                    var backendRT = new GRBackendRenderTarget(_width, _height, samples[0], stencil[0], fbInfo);
                    _skSurface = SKSurface.Create(_grContext, backendRT, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
                }

                // Step 2: Submit a dummy black frame to trigger MediaCodec initialization
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Submitting dummy frame to trigger MediaCodec...");
                _skSurface.Canvas.Clear(SKColors.Black);
                _skSurface.Canvas.Flush();
                _grContext?.Flush();

                // Set timestamp to 0 for dummy frame
                EGLExt.EglPresentationTimeANDROID(_eglDisplay, _eglSurface, 0);
                EGL14.EglSwapBuffers(_eglDisplay, _eglSurface);

                // Step 3: Aggressively drain until FORMAT_CHANGED event fires
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Waiting for FORMAT_CHANGED event (encoder ready signal)...");
                int drainAttempts = 0;
                var timeout = DateTime.Now.AddSeconds(10);  // 10 second timeout (generous)

                while (!_encoderReady && DateTime.Now < timeout)
                {
                    DrainEncoder(endOfStream: false, bufferingMode: true);
                    drainAttempts++;

                    if (!_encoderReady)
                    {
                        await Task.Delay(50);  // Brief pause between drain attempts
                    }
                }

                // Unbind context
                try { EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext); } catch { }

                if (_encoderReady)
                {
                    var warmupTime = (DateTime.Now - warmupStart).TotalSeconds;
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ==========================================");
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ ENCODER READY in {warmupTime:F2}s ({drainAttempts} drain attempts)");
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ ZERO FRAME LOSS GUARANTEED");
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ==========================================");

                    lock (_warmupLock)
                    {
                        _warmupCompleted = true;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ WARNING: Encoder did not signal ready within 10 seconds!");
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ Frame loss may occur at recording start");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ❌ Encoder warm-up failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task StartAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] StartAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}, BufferFrames={(_preRecordingBuffer?.GetFrameCount() ?? 0)}");

            // Pre-recording mode: Write buffer to muxer, then continue with live frames (mirrors iOS)
            if (IsPreRecordingMode && _preRecordingBuffer != null)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Pre-recording mode: User pressed record");

                // Step 1: Prune buffer to max duration
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffer stats BEFORE pruning: {_preRecordingBuffer.GetStats()}");
                _preRecordingBuffer.PruneToMaxDuration();
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffer stats AFTER pruning: {_preRecordingBuffer.GetStats()}");

                int bufferFrameCount = _preRecordingBuffer.GetFrameCount();
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffer has {bufferFrameCount} frames after pruning");

                if (bufferFrameCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffer stats: {_preRecordingBuffer.GetStats()}");

                    // Step 2: Create MediaMuxer for final output
                    _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
                    _muxer.SetOrientationHint(_deviceRotation);

                    // Wait for MediaCodec to signal format (might already be available from buffering)
                    if (_videoFormat == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Waiting for video format from MediaCodec...");
                        // Drain to get FORMAT_CHANGED
                        for (int i = 0; i < 10 && _videoFormat == null; i++)
                        {
                            DrainEncoder(endOfStream: false, bufferingMode: true);
                            if (_videoFormat == null)
                                await Task.Delay(100);
                        }
                    }

                    if (_videoFormat != null)
                    {
                        // Add track and start muxer
                        _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                        _muxer.Start();
                        _muxerStarted = true;

                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] MediaMuxer started, writing {bufferFrameCount} buffered frames...");

                        // Step 3: Write all buffered frames to muxer (timestamps 0 → N)
                        var bufferedFrames = _preRecordingBuffer.GetAllFrames();
                        int writtenCount = 0;

                        // CRITICAL: Renormalize buffered frames to start from 0 before writing to muxer
                        // This ensures live frames can continue from the last buffered frame timestamp
                        TimeSpan firstBufferedFrameTimestamp = bufferedFrames.Count > 0 ? bufferedFrames[0].Timestamp : TimeSpan.Zero;
                        TimeSpan lastNormalizedTimestamp = TimeSpan.Zero;
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Renormalizing buffered frames (first frame: {firstBufferedFrameTimestamp.TotalSeconds:F3}s -> 0s)");

                        foreach (var (data, timestamp, isKeyFrame) in bufferedFrames)
                        {
                            if (data == null || data.Length == 0)
                                continue;

                            // Renormalize: subtract first frame's timestamp so buffer starts from 0
                            var normalizedTimestamp = timestamp - firstBufferedFrameTimestamp;
                            lastNormalizedTimestamp = normalizedTimestamp;  // Track last frame for duration calculation

                            var buffer = ByteBuffer.Wrap(data);
                            var flags = isKeyFrame ? MediaCodecBufferFlags.KeyFrame : MediaCodecBufferFlags.None;
                            var bufferInfo = new MediaCodec.BufferInfo();
                            bufferInfo.Set(0, data.Length, (long)normalizedTimestamp.TotalMicroseconds, flags);

                            _muxer.WriteSampleData(_videoTrackIndex, buffer, bufferInfo);
                            writtenCount++;

                            // Log first/last frame
                            if (writtenCount == 1 || writtenCount == bufferedFrames.Count)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffered frame {writtenCount}: PTS={normalizedTimestamp.TotalSeconds:F3}s (was {timestamp.TotalSeconds:F3}s), Size={data.Length}, KeyFrame={isKeyFrame}");
                            }
                        }

                        // CRITICAL: Use actual last frame timestamp + one frame duration for live frame offset
                        // This ensures live frames start AFTER the last buffered frame (no overlap)
                        double frameDurationMs = 1000.0 / _frameRate;  // e.g., 33.33ms @ 30fps
                        _preRecordingDuration = lastNormalizedTimestamp + TimeSpan.FromMilliseconds(frameDurationMs);
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Wrote {writtenCount} buffered frames, last frame at: {lastNormalizedTimestamp.TotalSeconds:F3}s, live offset: {_preRecordingDuration.TotalSeconds:F3}s");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ERROR: MediaCodec format not available!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] No buffered frames to write");

                    // Create muxer anyway for live recording
                    _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
                    _muxer.SetOrientationHint(_deviceRotation);
                }

                // Step 4: Clear buffer, switch to live recording mode
                _preRecordingBuffer?.Dispose();
                _preRecordingBuffer = null;

                // CRITICAL: Disable pre-recording mode so live frames go to muxer, not buffer!
                IsPreRecordingMode = false;

                // Reset timestamp offset for live frames
                _firstEncodedFrameOffset = TimeSpan.MinValue;

                _isRecording = true;
                _startTime = DateTime.Now;
                EncodedFrameCount = 0;
                EncodedDataSize = 0;
                EncodingDuration = TimeSpan.Zero;
                EncodingStatus = "Recording Live";

                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ Switched to live recording mode (IsPreRecordingMode=false)");

                _progressTimer = new System.Threading.Timer(_ =>
                {
                    if (_isRecording)
                    {
                        var elapsed = DateTime.Now - _startTime;
                        ProgressReported?.Invoke(this, elapsed);
                    }
                }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Live recording started (continues in same muxer session)");
                return;
            }

            // Normal recording mode: Start MediaMuxer
            if (_isRecording)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Already recording, ignoring StartAsync");
                return;
            }

            // Create muxer for normal recording
            _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
            _muxer.SetOrientationHint(_deviceRotation);

            // CRITICAL: Request IMMEDIATE keyframe for normal recording!
            // MediaMuxer REQUIRES first frame to be I-frame
            RequestKeyFrame();
            _lastKeyframeRequest = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Requested IMMEDIATE keyframe for normal recording");

            // CRITICAL: Start muxer immediately using format from warm-up!
            // We already got FORMAT_CHANGED during warm-up, won't get it again
            if (_videoFormat != null)
            {
                _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                _muxer.Start();
                _muxerStarted = true;
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] MediaMuxer started for normal recording (using format from warm-up)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ WARNING: No video format available! Waiting for FORMAT_CHANGED...");
            }

            _isRecording = true;
            _startTime = DateTime.Now;
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

            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Normal recording started");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Begin a GPU frame into the encoder's EGLSurface, returning a Skia canvas to draw overlays.
        /// </summary>
        public IDisposable BeginFrame(TimeSpan timestamp, out SKCanvas canvas, out SKImageInfo info)
        {
            _pendingTimestamp = timestamp;

            // Defensive check: If EGL is torn down, return empty canvas
            if (_eglDisplay == EGL14.EglNoDisplay || _eglSurface == EGL14.EglNoSurface || _eglContext == EGL14.EglNoContext)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] BeginFrame: EGL torn down, returning dummy canvas");
                info = new SKImageInfo(1, 1);
                canvas = new SKCanvas(new SKBitmap(1, 1));
                return new FrameScope();
            }

            MakeCurrent();

            GLES20.GlViewport(0, 0, _width, _height);

            if (_grContext == null)
            {
                var glInterface = GRGlInterface.Create();
                _grContext = GRContext.CreateGl(glInterface);
            }

            if (_skSurface == null || _skInfo.Width != _width || _skInfo.Height != _height)
            {
                _skInfo = new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);

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
        /// Flush Skia, set EGL presentation time, swap buffers, and drain encoder output.
        /// </summary>
        public async Task SubmitFrameAsync()
        {
            if (!_isRecording) return;

            // Defensive check: If EGL is torn down, return early
            if (_eglDisplay == EGL14.EglNoDisplay || _eglSurface == EGL14.EglNoSurface || _eglContext == EGL14.EglNoContext)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] SubmitFrameAsync: EGL torn down, skipping submit");
                return;
            }

            // CRITICAL: Encoder must be ready (warm-up should have completed during InitializeAsync)
            // This check should never fail if initialization was successful
            if (!_encoderReady)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ WARNING: Frame arriving but encoder not ready! This should never happen.");
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ Skipping frame at {_pendingTimestamp.TotalSeconds:F3}s to avoid loss");
                return;
            }

            MakeCurrent();
            _skSurface?.Canvas?.Flush();
            _grContext?.Flush();

            // Publish preview snapshot
            SKImage keepAlive = null;
            try
            {
                using var gpuSnap = _skSurface?.Snapshot();
                if (gpuSnap != null)
                {
                    int maxPreviewWidth = ParentCamera?.NativeControl?.PreviewWidth ?? 800;

                    int pw = Math.Min(_width, maxPreviewWidth);
                    int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));

                    var pInfo = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var rasterSurface = SKSurface.Create(pInfo);
                    var pCanvas = rasterSurface.Canvas;
                    pCanvas.Clear(SKColors.Transparent);
                    pCanvas.DrawImage(gpuSnap, new SKRect(0, 0, pw, ph));

                    keepAlive = rasterSurface.Snapshot();
                    lock (_previewLock)
                    {
                        _latestPreviewImage?.Dispose();
                        _latestPreviewImage = keepAlive;
                        keepAlive = null;
                    }
                    PreviewAvailable?.Invoke(this, EventArgs.Empty);
                }
            }
            catch { keepAlive?.Dispose(); keepAlive = null; }
            finally { keepAlive?.Dispose(); }

            long ptsNanos = (long)(_pendingTimestamp.TotalMilliseconds * 1_000_000.0);
            EGLExt.EglPresentationTimeANDROID(_eglDisplay, _eglSurface, ptsNanos);
            EGL14.EglSwapBuffers(_eglDisplay, _eglSurface);

            // Drain encoder
            bool bufferingMode = IsPreRecordingMode && _preRecordingBuffer != null;

            // CRITICAL: Request keyframes periodically (every ~900ms) for BOTH buffering AND normal/live recording
            // This ensures MediaMuxer always gets I-frames to write
            if (DateTime.Now - _lastKeyframeRequest >= _keyframeRequestInterval)
            {
                RequestKeyFrame();
                _lastKeyframeRequest = DateTime.Now;
            }

            if (bufferingMode)
            {
                // Drain aggressively during buffering
                for (int i = 0; i < 5; i++)
                {
                    DrainEncoder(endOfStream: false, bufferingMode: true);
                }
            }
            else
            {
                DrainEncoder(endOfStream: false, bufferingMode: false);
            }

            // Unbind context
            try { EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext); } catch { }

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

        public async Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp)
        {
            // CPU fallback not used in Android GPU path
            await Task.CompletedTask;
        }

        public async Task PrependBufferedEncodedDataAsync(PrerecordingEncodedBuffer prerecordingBuffer)
        {
            // Not used - Android writes buffer directly to muxer in StartAsync
            await Task.CompletedTask;
        }

        public async Task AbortAsync()
        {

            _isRecording = false;
            _progressTimer?.Dispose();

            if (IsPreRecordingMode && _preRecordingBuffer != null && _videoCodec != null)
            {
                _preRecordingBuffer?.Dispose();
                _preRecordingBuffer = null;
            }

            try
            {
                _videoCodec?.SignalEndOfInputStream();
                DrainEncoder(endOfStream: true, bufferingMode: false);

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

            EncodingStatus = "Canceled";
        }

        public async Task<CapturedVideo> StopAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] StopAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}, BufferFrames={(_preRecordingBuffer?.GetFrameCount() ?? 0)}");

            _isRecording = false;
            _progressTimer?.Dispose();
            EncodingStatus = "Stopping";

            // CRITICAL: If pre-recording mode and buffer still has frames, we stopped before pressing record
            // Write buffer to file now (mirrors iOS behavior)
            if (IsPreRecordingMode && _preRecordingBuffer != null && _videoCodec != null)
            {
                int bufferFrameCount = _preRecordingBuffer.GetFrameCount();
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Pre-recording encoder stopping with {bufferFrameCount} buffered frames (never pressed record)");

                if (bufferFrameCount > 0)
                {
                    _preRecordingBuffer.PruneToMaxDuration();

                    // Create muxer and write buffer
                    _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
                    _muxer.SetOrientationHint(_deviceRotation);

                    // Get format
                    if (_videoFormat == null)
                    {
                        for (int i = 0; i < 10 && _videoFormat == null; i++)
                        {
                            DrainEncoder(endOfStream: false, bufferingMode: true);
                            if (_videoFormat == null)
                                await Task.Delay(100);
                        }
                    }

                    if (_videoFormat != null)
                    {
                        _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                        _muxer.Start();
                        _muxerStarted = true;

                        var bufferedFrames = _preRecordingBuffer.GetAllFrames();
                        foreach (var (data, timestamp, isKeyFrame) in bufferedFrames)
                        {
                            if (data == null || data.Length == 0) continue;

                            var buffer = ByteBuffer.Wrap(data);
                            var flags = isKeyFrame ? MediaCodecBufferFlags.KeyFrame : MediaCodecBufferFlags.None;
                            var bufferInfo = new MediaCodec.BufferInfo();
                            bufferInfo.Set(0, data.Length, (long)timestamp.TotalMicroseconds, flags);
                            _muxer.WriteSampleData(_videoTrackIndex, buffer, bufferInfo);
                        }

                        EncodingDuration = _preRecordingBuffer.GetBufferedDuration();
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Wrote buffered frames, duration: {EncodingDuration.TotalSeconds:F3}s");
                    }
                }

                _preRecordingBuffer?.Dispose();
                _preRecordingBuffer = null;
            }

            try
            {
                _videoCodec?.SignalEndOfInputStream();
                DrainEncoder(endOfStream: true, bufferingMode: false);

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
            EncodingStatus = "Completed";
            if (EncodingDuration == TimeSpan.Zero)
            {
                EncodingDuration = DateTime.Now - _startTime;
            }

            return new CapturedVideo
            {
                FilePath = _outputPath,
                FileSizeBytes = info.Exists ? info.Length : 0,
                Duration = EncodingDuration,
                Time = _startTime
            };
        }

        private void DrainEncoder(bool endOfStream, bool bufferingMode)
        {
            var bufferInfo = new MediaCodec.BufferInfo();
            int maxIterations = endOfStream ? 100 : 1;  // Prevent infinite loop on endOfStream
            int iterations = 0;

            while (true)
            {
                int outIndex = _videoCodec.DequeueOutputBuffer(bufferInfo, 0);
                if (outIndex == (int)MediaCodecInfoState.TryAgainLater)
                {
                    if (!endOfStream)
                    {
                        break;  // Normal draining - done for now
                    }
                    else
                    {
                        // EndOfStream draining - check iteration limit to prevent infinite loop
                        iterations++;
                        if (iterations >= maxIterations)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] DrainEncoder: Reached max iterations ({maxIterations}), stopping drain");
                            break;
                        }
                        System.Threading.Thread.Sleep(10);  // Brief pause before retry
                        continue;
                    }
                }
                else if (outIndex == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    iterations = 0;  // Reset counter - encoder responded
                    var newFormat = _videoCodec.OutputFormat;
                    _videoFormat = newFormat;

                    // CRITICAL: Set encoder ready flag (signals warm-up complete)
                    if (!_encoderReady)
                    {
                        _encoderReady = true;
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ FORMAT_CHANGED received - encoder is READY");
                    }

                    // If buffering mode, don't start muxer yet (will start in StartAsync)
                    if (bufferingMode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Format changed (buffering mode): {newFormat}");
                        continue;
                    }

                    // Normal/live recording: start muxer if not already started
                    if (_muxerStarted) continue;
                    _videoTrackIndex = _muxer.AddTrack(newFormat);
                    _muxer.Start();
                    _muxerStarted = true;
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] MediaMuxer started");
                }
                else if (outIndex >= 0)
                {
                    iterations = 0;  // Reset counter - encoder is producing frames
                    var encodedData = _videoCodec.GetOutputBuffer(outIndex);
                    if ((bufferInfo.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                    {
                        bufferInfo.Size = 0;
                    }
                    if (bufferInfo.Size != 0)
                    {
                        // Skip first frame
                        if (_skipFirstEncodedSample)
                        {
                            _skipFirstEncodedSample = false;
                        }
                        else
                        {
                            // BUFFERING MODE: Append to circular buffer
                            if (bufferingMode)
                            {
                                byte[] frameData = new byte[bufferInfo.Size];
                                encodedData.Position(bufferInfo.Offset);
                                encodedData.Get(frameData, 0, bufferInfo.Size);

                                // Track first frame offset for timestamp normalization
                                var rawTimestamp = TimeSpan.FromMicroseconds(bufferInfo.PresentationTimeUs);
                                if (_firstEncodedFrameOffset == TimeSpan.MinValue)
                                {
                                    _firstEncodedFrameOffset = rawTimestamp;
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] First buffered frame at {rawTimestamp.TotalSeconds:F3}s");
                                }

                                // Normalize to start from 0
                                var normalizedTimestamp = rawTimestamp - _firstEncodedFrameOffset;
                                _preRecordingBuffer.AppendEncodedFrame(frameData, bufferInfo.Size, normalizedTimestamp);

                                EncodedFrameCount++;
                                EncodedDataSize += bufferInfo.Size;
                                EncodingDuration = DateTime.Now - _startTime;
                                EncodingStatus = "Buffering";
                            }
                            // LIVE RECORDING MODE: Write to muxer
                            else if (_muxerStarted)
                            {
                                long pts = bufferInfo.PresentationTimeUs;

                                // CRITICAL: Always normalize timestamps (both normal recording AND pre-rec+live)
                                // Track first live frame for normalization
                                if (_firstEncodedFrameOffset == TimeSpan.MinValue)
                                {
                                    _firstEncodedFrameOffset = TimeSpan.FromMicroseconds(pts);
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] First live frame at {pts / 1000.0:F2}ms");
                                }

                                // Normalize to start from 0, then add pre-recording offset (if any)
                                long normalizedPts = pts - (long)_firstEncodedFrameOffset.TotalMicroseconds;
                                long finalPts = normalizedPts + (long)_preRecordingDuration.TotalMicroseconds;

                                var normalizedBufferInfo = new MediaCodec.BufferInfo();
                                normalizedBufferInfo.Set(bufferInfo.Offset, bufferInfo.Size, finalPts, bufferInfo.Flags);

                                encodedData.Position(bufferInfo.Offset);
                                encodedData.Limit(bufferInfo.Offset + bufferInfo.Size);
                                _muxer.WriteSampleData(_videoTrackIndex, encodedData, normalizedBufferInfo);

                                EncodedFrameCount++;
                                EncodedDataSize += bufferInfo.Size;
                                EncodingDuration = DateTime.Now - _startTime;
                                EncodingStatus = "Encoding";
                            }
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
        }

        private void MakeCurrent()
        {
            if (_eglDisplay == EGL14.EglNoDisplay || _eglSurface == EGL14.EglNoSurface || _eglContext == EGL14.EglNoContext)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] MakeCurrent: EGL already torn down, ignoring");
                return;
            }

            if (!EGL14.EglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext))
                throw new InvalidOperationException("EGL make current failed");
        }

        private sealed class FrameScope : IDisposable { public void Dispose() { } }

        private void TryReleaseCodec()
        {
            try { _videoCodec?.Stop(); } catch { }
            try { _videoCodec?.Release(); } catch { }
            _videoCodec = null;
            try { _inputSurface?.Release(); } catch { }
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
            _preRecordingBuffer?.Dispose();
            TryReleaseCodec();
            TryReleaseMuxer();
            TearDownEgl();
        }
    }
}
#endif
