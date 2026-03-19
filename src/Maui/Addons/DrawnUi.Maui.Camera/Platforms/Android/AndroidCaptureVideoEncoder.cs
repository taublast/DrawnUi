#if ANDROID
using System;
using System.Buffers;
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
        public bool SupportsAudio => true;

        private string _preferredAudioCodecName;
        private string _selectedAudioMimeType = MediaFormat.MimetypeAudioAac; // Default to AAC

        public void SetAudioBuffer(CircularAudioBuffer buffer)
        {
            _audioBuffer = buffer;
        }

        /// <summary>
        /// Sets the preferred audio codec name to use.
        /// Android primarily supports AAC, but this allows future codec expansion.
        /// </summary>
        public void SetAudioCodec(string codecName)
        {
            _preferredAudioCodecName = codecName;
            // For now, Android only supports AAC variants
            // Future: parse codecName to determine appropriate MIME type
            if (!string.IsNullOrEmpty(codecName))
            {
                if (codecName.Contains("AAC") || codecName.Contains("aac"))
                {
                    _selectedAudioMimeType = MediaFormat.MimetypeAudioAac;
                }
                // Add other codec support here as needed
            }
        }

        public void WriteAudio(AudioSample sample)
        {
            if (!_isRecording && !IsPreRecordingMode) return;

            // In pre-recording mode, buffer PCM and start background AAC encoding
            if (IsPreRecordingMode && _audioBuffer != null)
            {
                _audioBuffer.Write(sample);

                // Start background encoding if not already running
                if (_backgroundEncodingTask == null || _backgroundEncodingTask.IsCompleted)
                {
                    StartBackgroundAudioEncoding();
                }
            }
            // In live recording, feed encoder directly
            else if (_isRecording)
            {
                // Call synchronously on the audio capture thread to guarantee FIFO ordering.
                // Task.Run causes non-deterministic task scheduling — under GC pressure, tasks pile up
                // and execute out of order, producing backwards PTS that the MediaMuxer rejects.
                // FeedAudioEncoder is fast (~1-2ms), safe to call inline (PCM buffer = ~79ms interval).
                FeedAudioEncoder(sample);
            }
        }
        private string _outputPath;
        private int _width;
        private int _height;
        private int _frameRate;
        private bool _recordAudio;
        private int _deviceRotation = 0;

        private MediaCodec _videoCodec;

        // Audio Fields
        private MediaCodec _audioCodec;
        private int _audioTrackIndex = -1;
        private MediaFormat _audioFormat;
        private CircularAudioBuffer _audioBuffer;
        private CircularEncodedAudioBuffer _encodedAudioBuffer; // Pre-encoded AAC for zero-lag transitions
        private readonly SemaphoreSlim _audioSemaphore = new(1, 1);
        private readonly SemaphoreSlim _videoSemaphore = new(1, 1);
        private long _audioPresentationTimeUs = 0;
        private long _lastMuxerAudioPtsUs = 0; // last PTS actually written to muxer
        private long _maxExpectedEncoderOutputPtsUs = 0; // max PTS still pending in encoder pipeline
        private long _audioPtsBaseNs = -1;
        private long _audioPtsOffsetUs = 0;
        private TimeSpan _deferredPreRecordingFirstFrameOffset = TimeSpan.MinValue;

        // AAC codec constants (matches audio codec config: 44100 Hz mono 16-bit)
        private const long AacFrameDurationUs = 23220; // 1024 samples @ 44100 Hz
        private const int AacSamplesPerFrame = 1024;
        private const int AudioBytesPerSample = 2; // mono 16-bit

        /// <summary>
        /// CLOCK_MONOTONIC (ElapsedRealtimeNanos) captured at the same instant as _captureVideoStartTime.
        /// Bridges the gap between video PTS (wall-clock elapsed from recording start) and audio
        /// timestamps (absolute CLOCK_MONOTONIC from AudioRecord.GetTimestamp).
        /// Set by SkiaCamera right after _captureVideoStartTime = DateTime.Now.
        /// </summary>
        public long CaptureStartAbsoluteNs { get; set; } = 0;
        private Task _backgroundEncodingTask;
        private CancellationTokenSource _encodingCancellation;

        private MediaMuxer _muxer;
        private int _videoTrackIndex = -1;
        private bool _muxerStarted = false;
        private Surface _inputSurface;
        private MediaFormat _videoFormat;  // Save format for later use

        // Pre-recording support (mirrors iOS implementation)
        private PrerecordingEncodedBuffer _preRecordingBuffer;
        private TimeSpan _preRecordingDuration = TimeSpan.Zero;
        private TimeSpan _firstEncodedFrameOffset = TimeSpan.MinValue;  // Offset to subtract from all frames

        /// <summary>
        /// Optional pre-allocated buffer provided by SkiaCamera.
        /// When set, encoder will reuse this buffer instead of allocating a new one,
        /// eliminating the ~27MB allocation lag spike when pre-recording starts.
        /// </summary>
        public PrerecordingEncodedBuffer SharedPreRecordingBuffer { get; set; }

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

        // Drop only the warm-up dummy encoded sample before recording actually begins.
        private bool _skipFirstEncodedSample = true;

        // Timeout/Failsafe for audio initialization
        private int _framesWaitingForAudio = 0;
        private const int MaxFramesWaitingForAudio = 45; // ~1.5 seconds at 30fps

        // Keyframe request timing for pre-recording (request every ~1 second to keep buffer fresh)
        private DateTime _lastKeyframeRequest = DateTime.MinValue;
        private readonly TimeSpan _keyframeRequestInterval = TimeSpan.FromMilliseconds(900); // Slightly less than 1s I-frame interval

        // When transitioning from pre-recording to live, ensure the first written live frame is a keyframe (IDR)
        private volatile bool _waitForFirstLiveKeyframe;

        // Preview-from-recording support
        private readonly object _previewLock = new();
        private SKImage _latestPreviewImage;
        private SKSurface _previewRasterSurface;  // Cached to avoid allocation every frame
        private int _previewWidth, _previewHeight;
        private int _previewFrameCounter = 0;    // Used by legacy SubmitFrameAsync path
        private int _previewFrameInterval = 1;  // Legacy path throttle: 1-of-N frames → ~30fps preview
        public event EventHandler PreviewAvailable;

        // GPU preview scaler (glBlitFramebuffer-based, mirrors MetalPreviewScaler)
        private GlPreviewScaler _glPreviewScaler;
        private bool _glPreviewScalerInitAttempted;

        // ML scaler — dedicated scaler at ML dimensions, lazily created on GPU thread
        private GlPreviewScaler _mlGlScaler;
        private int _mlGlScalerTargetWidth;
        private int _mlGlScalerTargetHeight;

        // GPU camera path support (SurfaceTexture zero-copy)
        private GpuCameraFrameProvider _gpuFrameProvider;
        private bool _useGpuCameraPath;
        private bool _isFrontCamera;
        public bool UseGpuCameraPath => _useGpuCameraPath;
        public GpuCameraFrameProvider GpuFrameProvider => _gpuFrameProvider;

        // Cached BufferInfo objects to avoid per-frame Java allocations (reduces GC pressure)
        private MediaCodec.BufferInfo _drainBufferInfo;
        private MediaCodec.BufferInfo _muxerBufferInfo;

        // GPU resource management (prevent Skia GPU memory accumulation during long recordings)
        private int _gpuFrameCounter = 0;
        private const int GpuPurgeInterval = 300;  // Purge every 300 frames (~10 seconds at 30fps)

        // GPU encoding thread (async processing - decouples camera from encoding)
        private System.Threading.Thread _gpuEncodingThread;
        private System.Threading.ManualResetEventSlim _gpuFrameSignal;
        private volatile bool _stopGpuThread = false;
        private volatile bool _gpuFrameReady = false;

        // Frame context passed from callback to encoding thread
        private TimeSpan _pendingGpuFrameTimestamp;
        private Action<DrawableFrame> _pendingFrameProcessor;
        private bool _pendingDiagnosticsOn;
        private Action<SKCanvas, int, int> _pendingDrawDiagnostics;
        private readonly object _gpuFrameLock = new();

        /// <summary>
        /// Event fired when a GPU frame has been successfully processed and encoded.
        /// Used by SkiaCamera to track recording FPS.
        /// </summary>
        public event Action OnGpuFrameProcessed;

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

        /// <summary>Gets the current output file path for this encoder.</summary>
        public string OutputPath => _outputPath;

        /// <summary>
        /// Redirects the output file path before StartAsync is called.
        /// Used by the 2-encoder transition so the old pre-recording encoder writes
        /// its buffer to a temp file rather than the final output path.
        /// </summary>
        public void OverrideOutputPath(string newPath) => _outputPath = newPath;

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
            _previewFrameInterval = Math.Max(1, _frameRate / 30); // Legacy SubmitFrameAsync throttle
            _deviceRotation = deviceRotation;
            _recordAudio = recordAudio;
            _preRecordingDuration = TimeSpan.Zero;
            _firstEncodedFrameOffset = TimeSpan.MinValue;
            _skipFirstEncodedSample = true;  // Reset for new session
            _gpuFrameCounter = 0;  // Reset GPU purge counter
            ResetAudioTiming();

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

            if (_recordAudio)
            {
                try
                {
                    // Audio Setup - respect codec selection
                    // TODO: Get actual sample rate/channels from SkiaCamera properties if available
                    int aSampleRate = 44100;
                    int aChannels = 1;

                    MediaFormat aFormat = MediaFormat.CreateAudioFormat(_selectedAudioMimeType, aSampleRate, aChannels);

                    // Configure based on codec type
                    if (_selectedAudioMimeType == MediaFormat.MimetypeAudioAac)
                    {
                        aFormat.SetInteger(MediaFormat.KeyAacProfile, (int)MediaCodecProfileType.Aacobjectlc);
                    }
                    // Add other codec configurations here as needed

                    aFormat.SetInteger(MediaFormat.KeyBitRate, 128000);
                    aFormat.SetInteger(MediaFormat.KeyMaxInputSize, 16384 * 2);

                    _audioCodec = MediaCodec.CreateEncoderByType(_selectedAudioMimeType);
                    _audioCodec.Configure(aFormat, null, null, MediaCodecConfigFlags.Encode);
                    _audioCodec.Start();

                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Audio Codec initialized ({_selectedAudioMimeType} {aSampleRate}Hz)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Audio Codec init failed: {ex.Message}");
                    _recordAudio = false;
                }
            }

            // Set up EGL context bound to the codec surface
            SetupEglForCodecSurface();

            // Start GPU encoding thread for async frame processing (decouples camera from encoding)
            StartGpuEncodingThread();

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
                // Pre-recording mode: Use shared buffer if available, else allocate new one
                if (SharedPreRecordingBuffer != null)
                {
                    _preRecordingBuffer = SharedPreRecordingBuffer;
                    _preRecordingBuffer.Reset();
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Using pre-allocated shared buffer (no allocation lag)");
                }
                else
                {
                    _preRecordingBuffer?.Dispose();
                    var preRecordDuration = ParentCamera.PreRecordDuration;
                    _preRecordingBuffer = new PrerecordingEncodedBuffer(preRecordDuration);
                }

                // CRITICAL: Start recording immediately to buffer frames (mirrors iOS)
                _isRecording = true;
                _startTime = DateTime.Now;
                EncodedFrameCount = 0;
                EncodedDataSize = 0;
                EncodingDuration = TimeSpan.Zero;
                EncodingStatus = "Buffering";

                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Pre-recording mode initialized and started:");
                System.Diagnostics.Debug.WriteLine($"  Final output file: {_outputPath}");
                System.Diagnostics.Debug.WriteLine($"  Buffer duration: {ParentCamera.PreRecordDuration.TotalSeconds}s");
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

                    // Warm up audio codec so _audioFormat is set before StartAsync.
                    // During pre-recording, WriteAudio() stores PCM to _audioBuffer and never calls
                    // FeedAudioEncoder(), so _audioCodec never receives input and OutputFormatChanged
                    // never fires. Without _audioFormat, StartAsync's muxer-start condition fails and
                    // the entire pre-recorded flush is skipped — producing a live-only (unusable) file.
                    if (_recordAudio && _audioCodec != null && _audioFormat == null)
                    {
                        try
                        {
                            // Feed one silent AAC-LC frame (1024 samples × 2 bytes) to trigger OutputFormatChanged.
                            // Produced AAC output is discarded by DrainAudioEncoder because muxer isn't started yet.
                            int audioInputIndex = _audioCodec.DequeueInputBuffer(5000);
                            if (audioInputIndex >= 0)
                            {
                                var audioInputBuffer = _audioCodec.GetInputBuffer(audioInputIndex);
                                audioInputBuffer.Clear();
                                var silentFrame = new byte[1024 * 2]; // 1024 samples × 16-bit
                                audioInputBuffer.Put(silentFrame);
                                _audioCodec.QueueInputBuffer(audioInputIndex, 0, silentFrame.Length, 0, 0);
                            }

                            for (int i = 0; i < 20 && _audioFormat == null; i++)
                            {
                                DrainAudioEncoder();
                                if (_audioFormat == null)
                                    await Task.Delay(25);
                            }

                            System.Diagnostics.Debug.WriteLine(_audioFormat != null
                                ? $"[AndroidEncoder] ✓ Audio codec ready: {_audioFormat}"
                                : "[AndroidEncoder] ⚠️ Audio codec format not ready after warmup");
                        }
                        catch (Exception exAudio)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Audio warmup error: {exAudio.Message}");
                        }
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

                    // Attempt to get audio format if missing
                    if (_recordAudio && _audioFormat == null)
                    {
                        // Resurrect failed audio: try to drain again
                        DrainAudioEncoder();

                        // Increased timeout logic handled in DrainEncoder loop for frame-by-frame waiting
                        if (_audioFormat == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Audio format missing at StartAsync. Determining strategy in DrainEncoder loop.");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] Muxer condition: videoFormat={_videoFormat != null}, recordAudio={_recordAudio}, audioFormat={_audioFormat != null}, _firstEncodedFrameOffset={_firstEncodedFrameOffset.TotalMilliseconds:F0}ms");

                    if (_videoFormat != null && (!_recordAudio || _audioFormat != null))
                    {
                        // Add track and start muxer
                        _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                        if (_recordAudio && _audioFormat != null)
                        {
                            _audioTrackIndex = _muxer.AddTrack(_audioFormat);
                        }

                        _muxer.Start();
                        _muxerStarted = true;

                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] MediaMuxer started (Audio={(_audioTrackIndex >= 0)}), writing {bufferFrameCount} buffered frames...");

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
                            // Reuse cached BufferInfo to avoid per-frame Java allocations
                            _muxerBufferInfo ??= new MediaCodec.BufferInfo();
                            _muxerBufferInfo.Set(0, data.Length, (long)normalizedTimestamp.TotalMicroseconds, flags);

                            _muxer.WriteSampleData(_videoTrackIndex, buffer, _muxerBufferInfo);
                            writtenCount++;

                            // Log first/last frame
                            if (writtenCount == 1 || writtenCount == bufferedFrames.Count)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Buffered frame {writtenCount}: PTS={normalizedTimestamp.TotalSeconds:F3}s (was {timestamp.TotalSeconds:F3}s), Size={data.Length}, KeyFrame={isKeyFrame}");
                            }
                        }

                        // Flush pre-recorded audio - prefer pre-encoded AAC for zero lag
                        if (_recordAudio)
                        {
                            // CLOCK ALIGNMENT:
                            // Video PTS = DateTime.Now - _captureVideoStartTime (wall-clock elapsed, ~0-based).
                            // Audio TimestampNs = absolute CLOCK_MONOTONIC from AudioRecord.GetTimestamp (~device uptime).
                            // Bridge: CaptureStartAbsoluteNs = ElapsedRealtimeNanos() captured at the same instant
                            // as _captureVideoStartTime = DateTime.Now in SkiaCamera.
                            //
                            // Absolute ns of the oldest buffered video frame (muxer PTS=0):
                            //   CaptureStartAbsoluteNs = UptimeMillis at camera start (CLOCK_MONOTONIC)
                            //   _firstEncodedFrameOffset = elapsed from camera start to first encoded frame
                            //     (frames are normalized by subtracting this, so muxer PTS=0 corresponds to
                            //      the actual time of the first encoded frame, not camera start)
                            //   firstBufferedFrameTimestamp = normalized PTS of oldest buffered frame (= 0ms)
                            long absoluteFirstVideoFrameNs = CaptureStartAbsoluteNs
                                + (long)(_firstEncodedFrameOffset.Ticks * 100L) // TotalNanoseconds
                                + (long)(firstBufferedFrameTimestamp.TotalMilliseconds * 1_000_000.0);
                            long absoluteFirstVideoFrameUs = absoluteFirstVideoFrameNs / 1000L;

                            // Diagnostics
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] CaptureStartAbsoluteNs={CaptureStartAbsoluteNs / 1_000_000:F0}ms");
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] firstBufferedFrameTimestamp={firstBufferedFrameTimestamp.TotalMilliseconds:F0}ms");
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] absoluteFirstVideoFrameNs={absoluteFirstVideoFrameNs / 1_000_000:F0}ms absolute");
                            if (_audioBuffer != null)
                            {
                                var allAudio = _audioBuffer.GetAllSamples();
                                long firstAudioNs = allAudio.Length > 0 ? allAudio[0].TimestampNs : -1;
                                long lastAudioNs  = allAudio.Length > 0 ? allAudio[allAudio.Length - 1].TimestampNs : -1;
                                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] audio buffer: {allAudio.Length} samples, first={firstAudioNs / 1_000_000:F0}ms, last={lastAudioNs / 1_000_000:F0}ms absolute");
                                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] audio-video cut offset: {(firstAudioNs - absoluteFirstVideoFrameNs) / 1_000_000:F0}ms (should be ~0-100ms)");
                            }

                            bool usedPreEncoded = false;

                            // First, try to use pre-encoded AAC (zero lag!)
                            if (_encodedAudioBuffer != null && _encodedAudioBuffer.FrameCount > 0)
                            {
                                try
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Using pre-encoded AAC - ZERO lag transition! ({_encodedAudioBuffer.FrameCount} frames)");
                                    var encodedFrames = _encodedAudioBuffer.GetAllFrames();

                                    int skippedCount = 0;
                                    long lastWrittenPtsUs = -1;
                                    foreach (var (aacData, timestampUs, size) in encodedFrames)
                                    {
                                        // timestampUs is absolute CLOCK_MONOTONIC μs (stored that way in background encoding).
                                        // Subtract the absolute first video frame μs to get a video-relative PTS.
                                        long nTsUs = timestampUs - absoluteFirstVideoFrameUs;

                                        // Skip audio before the first video keyframe — do NOT clamp to 0.
                                        // Clamping makes many frames share PTS=0 which the muxer treats as
                                        // out-of-order and silently corrupts the audio track (no exception thrown).
                                        if (nTsUs < 0)
                                        {
                                            skippedCount++;
                                            continue;
                                        }

                                        // Guard against duplicate/backwards timestamps within the buffer
                                        // (should not happen but protects against any edge case)
                                        if (nTsUs <= lastWrittenPtsUs)
                                            nTsUs = lastWrittenPtsUs + 1;
                                        lastWrittenPtsUs = nTsUs;

                                        var buffer = Java.Nio.ByteBuffer.Wrap(aacData);
                                        var info = new MediaCodec.BufferInfo();
                                        info.Set(0, size, nTsUs, MediaCodecBufferFlags.KeyFrame);

                                        _muxer.WriteSampleData(_audioTrackIndex, buffer, info);
                                    }
                                    if (skippedCount > 0)
                                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Skipped {skippedCount} pre-encoded AAC frames before video start");

                                    usedPreEncoded = true;
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Wrote {encodedFrames.Count} pre-encoded AAC frames");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Error using pre-encoded AAC, falling back to PCM: {ex.Message}");
                                }
                            }

                            // Fallback to PCM encoding if pre-encoded AAC failed or unavailable
                            if (!usedPreEncoded && _audioBuffer != null)
                            {
                                try
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Falling back to PCM→AAC encoding");
                                    // Use ABSOLUTE clock reference so audio aligns with video
                                    var audioSamples = _audioBuffer.DrainFrom(absoluteFirstVideoFrameNs);

                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Draining {audioSamples.Length} PCM audio samples (cut at {absoluteFirstVideoFrameNs / 1_000_000:F0}ms absolute)");
                                    if (audioSamples.Length > 0)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder-DIAG] First PCM sample abs={audioSamples[0].TimestampNs / 1_000_000:F0}ms, offset-from-video={(audioSamples[0].TimestampNs - absoluteFirstVideoFrameNs) / 1_000_000:F0}ms");
                                    }

                                    foreach (var sample in audioSamples)
                                    {
                                        // Pass with original absolute CLOCK_MONOTONIC timestamp.
                                        // CalculateAudioPts normalizes from the first sample it sees,
                                        // so audio PTS 0 aligns with the first audio sample after the
                                        // video keyframe. _audioPtsBaseNs is reset after this flush
                                        // (line below), so live audio sets its own base independently.
                                        FeedAudioEncoder(sample);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Error draining PCM audio buffer: {ex}");
                                }
                            }

                            // Drain any remaining AAC frames the codec buffered during the flush loop above.
                            // The AAC encoder holds ~1024 samples internally; without this drain, the last
                            // ~23ms of pre-recorded audio is lost and _lastMuxerAudioPtsUs lags behind.
                            DrainAudioEncoder();
                        }

                        // CRITICAL: Use actual last frame timestamp + one frame duration for live frame offset
                        // This ensures live frames start AFTER the last buffered frame (no overlap)
                        double frameDurationMs = 1000.0 / _frameRate;  // e.g., 33.33ms @ 30fps
                        _preRecordingDuration = lastNormalizedTimestamp + TimeSpan.FromMilliseconds(frameDurationMs);
                        _audioPtsOffsetUs = (long)_preRecordingDuration.TotalMicroseconds;
                        if (_audioPresentationTimeUs < _audioPtsOffsetUs)
                        {
                            _audioPresentationTimeUs = _audioPtsOffsetUs;
                        }
                        _audioPtsBaseNs = -1;
                        // NOTE: _maxExpectedEncoderOutputPtsUs is intentionally NOT reset here.
                        // The last pre-rec PCM input may have left deferred AAC frames in the encoder
                        // pipeline (encoder delay: one 4096-sample input can yield 4 AAC frames but
                        // DrainAudioEncoder(timeout=0) only captures ~3 immediately). The ceiling value
                        // from the last pre-rec input (lastPreRecPTS + 3*23220µs) guards against the
                        // deferred frame appearing *after* the first live audio input in the muxer.
                        // Resetting to 0 here would allow the first live audio to be queued at a PTS
                        // lower than the deferred pre-rec frame, producing out-of-order frames that
                        // cause MPEG4Writer to silently stop the audio track.
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

                // Step 4: Clear buffer (keep for reuse), switch to live recording mode
                _preRecordingBuffer?.Clear();

                // Ensure seam starts on an IDR: wait for the first keyframe to establish clean timestamp base.
                // This makes the transition robust without skipping frames.
                _waitForFirstLiveKeyframe = true;
                RequestKeyFrame();
                _lastKeyframeRequest = DateTime.Now;

                // CRITICAL: Disable pre-recording mode so live frames go to muxer, not buffer!
                IsPreRecordingMode = false;

                // Stop background audio encoding since we're transitioning to live
                StopBackgroundAudioEncoding();

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

            _audioPtsBaseNs = -1;
            _audioPtsOffsetUs = 0;
            _audioPresentationTimeUs = 0;

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

            // Check Audio Format first
            if (_recordAudio && _audioFormat == null)
            {
                // Try drain to catch up
                DrainAudioEncoder();
            }

            if (_videoFormat != null && (!_recordAudio || _audioFormat != null))
            {
                _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                if (_recordAudio && _audioFormat != null)
                {
                    _audioTrackIndex = _muxer.AddTrack(_audioFormat);
                }

                _muxer.Start();
                _muxerStarted = true;
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] MediaMuxer started for normal recording (Audio={(_audioTrackIndex >= 0)})");
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
        /// Starts live recording to a standalone temp file without flushing the in-memory pre-record
        /// buffer. This keeps the current GPU encoder/camera session alive and moves the expensive
        /// buffer dump work to StopAsync time.
        /// </summary>
        public async Task StartLiveRecordingOnlyAsync(string liveOutputPath)
        {
            _outputPath = liveOutputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            if (File.Exists(_outputPath))
            {
                try { File.Delete(_outputPath); } catch { }
            }

            _audioPtsBaseNs = -1;
            _audioPtsOffsetUs = 0;
            _audioPresentationTimeUs = 0;
            _lastMuxerAudioPtsUs = 0;
            _maxExpectedEncoderOutputPtsUs = 0;
            _preRecordingDuration = TimeSpan.Zero;

            _muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
            _muxer.SetOrientationHint(_deviceRotation);

            RequestKeyFrame();
            _lastKeyframeRequest = DateTime.Now;

            if (_recordAudio && _audioFormat == null)
            {
                DrainAudioEncoder();
            }

            if (_videoFormat != null && (!_recordAudio || _audioFormat != null))
            {
                _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                if (_recordAudio && _audioFormat != null)
                {
                    _audioTrackIndex = _muxer.AddTrack(_audioFormat);
                }

                _muxer.Start();
                _muxerStarted = true;
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Live-only muxer started for deferred pre-record flush (Audio={(_audioTrackIndex >= 0)})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] Warning: live-only deferred flush start is waiting for media formats");
            }

            IsPreRecordingMode = false;
            StopBackgroundAudioEncoding(clearEncodedBuffer: false);
            _waitForFirstLiveKeyframe = true;
            _deferredPreRecordingFirstFrameOffset = _firstEncodedFrameOffset;
            _firstEncodedFrameOffset = TimeSpan.MinValue;

            _isRecording = true;
            _startTime = DateTime.Now;
            EncodedFrameCount = 0;
            EncodedDataSize = 0;
            EncodingDuration = TimeSpan.Zero;
            EncodingStatus = "Recording Live";

            _progressTimer?.Dispose();
            _progressTimer = new System.Threading.Timer(_ =>
            {
                if (_isRecording)
                {
                    var elapsed = DateTime.Now - _startTime;
                    ProgressReported?.Invoke(this, elapsed);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

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

            // Publish preview snapshot (throttled to ~30fps, skipped entirely if nobody is listening)
            bool shouldGeneratePreview = PreviewAvailable != null &&
                (System.Threading.Interlocked.Increment(ref _previewFrameCounter) % _previewFrameInterval) == 0;
            if (shouldGeneratePreview)
            {
                SKImage keepAlive = null;
                try
                {
                    using var gpuSnap = _skSurface?.Snapshot();
                    if (gpuSnap != null)
                    {
                        int maxPreviewWidth = ParentCamera?.NativeControl?.PreviewWidth ?? 800;

                        int pw = Math.Min(_width, maxPreviewWidth);
                        int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));

                        // Reuse cached surface to avoid GC pressure (was allocating ~2MB per frame)
                        if (_previewRasterSurface == null || _previewWidth != pw || _previewHeight != ph)
                        {
                            _previewRasterSurface?.Dispose();
                            var pInfo = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
                            _previewRasterSurface = SKSurface.Create(pInfo);
                            _previewWidth = pw;
                            _previewHeight = ph;
                        }

                        var pCanvas = _previewRasterSurface.Canvas;
                        pCanvas.Clear(SKColors.Transparent);
                        pCanvas.DrawImage(gpuSnap, new SKRect(0, 0, pw, ph));

                        keepAlive = _previewRasterSurface.Snapshot();
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
            }

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

        /// <summary>
        /// Scale the raw camera frame (currently in FBO 0) into outputBuffer using the GPU scaler.
        /// MUST be called synchronously from OnRawFrameAcquired while EGL context is current.
        /// Returns false if the scaler is not ready or outputBuffer is too small.
        /// </summary>
        public bool TryScaleRawFrameForML(int targetWidth, int targetHeight, byte[] outputBuffer)
        {
            // Lazily init / recreate when target dimensions change
            if (_mlGlScaler == null || _mlGlScalerTargetWidth != targetWidth || _mlGlScalerTargetHeight != targetHeight)
            {
                _mlGlScaler?.Dispose();
                _mlGlScaler = new GlPreviewScaler();
                if (!_mlGlScaler.Initialize(_width, _height, targetWidth, targetHeight))
                {
                    _mlGlScaler.Dispose();
                    _mlGlScaler = null;
                    return false;
                }
                _mlGlScalerTargetWidth = targetWidth;
                _mlGlScalerTargetHeight = targetHeight;
            }

            return _mlGlScaler.ScaleAndReadbackTo(outputBuffer);
        }

        #region GPU Encoding Thread (Async Processing)

        /// <summary>
        /// Start the GPU encoding thread for async frame processing.
        /// Called when recording starts to decouple camera from encoding.
        /// </summary>
        private void StartGpuEncodingThread()
        {
            if (_gpuEncodingThread != null) return;

            _gpuFrameSignal = new System.Threading.ManualResetEventSlim(false);
            _stopGpuThread = false;
            _gpuFrameReady = false;
            _gpuEncodingThread = new System.Threading.Thread(GpuEncodingLoop)
            {
                IsBackground = true,
                Name = "AndroidGpuEncoder",
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            _gpuEncodingThread.Start();
            System.Diagnostics.Debug.WriteLine("[AndroidEncoder] GPU encoding thread started");
        }

        /// <summary>
        /// Stop the GPU encoding thread.
        /// Called when recording stops.
        /// </summary>
        private void StopGpuEncodingThread()
        {
            if (_gpuEncodingThread == null) return;

            _stopGpuThread = true;
            _gpuFrameSignal?.Set(); // Wake thread to exit
            _gpuEncodingThread?.Join(1000);
            _gpuEncodingThread = null;
            _gpuFrameSignal?.Dispose();
            _gpuFrameSignal = null;
            _gpuFrameReady = false;
            System.Diagnostics.Debug.WriteLine("[AndroidEncoder] GPU encoding thread stopped");
        }

        /// <summary>
        /// GPU encoding loop - runs on dedicated background thread.
        /// Waits for signal from camera callback, then processes frame.
        /// </summary>
        private void GpuEncodingLoop()
        {
            while (!_stopGpuThread)
            {
                try
                {
                    _gpuFrameSignal?.Wait(100); // Wait for signal with timeout
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (_stopGpuThread) break;

                // Check if frame is ready
                if (!_gpuFrameReady)
                {
                    _gpuFrameSignal?.Reset();
                    continue;
                }

                // Get frame context atomically
                TimeSpan timestamp;
                Action<DrawableFrame> frameProcessor;
                bool diagOn;
                Action<SKCanvas, int, int> drawDiag;

                lock (_gpuFrameLock)
                {
                    timestamp = _pendingGpuFrameTimestamp;
                    frameProcessor = _pendingFrameProcessor;
                    diagOn = _pendingDiagnosticsOn;
                    drawDiag = _pendingDrawDiagnostics;
                    _gpuFrameReady = false;
                }

                _gpuFrameSignal?.Reset();

                try
                {
                    // ALL heavy work happens here on background thread
                    ProcessGpuFrameOnThread(timestamp, frameProcessor, diagOn, drawDiag);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] GpuEncodingLoop error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Signal the GPU encoding thread that a new frame is available.
        /// Called from camera OnFrameAvailable callback - MUST BE FAST!
        /// Just stores context and signals, no heavy work here.
        /// </summary>
        public void SignalGpuFrame(
            TimeSpan timestamp,
            Action<DrawableFrame> frameProcessor,
            bool videoDiagnosticsOn,
            Action<SKCanvas, int, int> drawDiagnostics)
        {
            if (!_isRecording || !_useGpuCameraPath || _gpuFrameProvider == null)
                return;

            // Store frame context for background thread
            lock (_gpuFrameLock)
            {
                _pendingGpuFrameTimestamp = timestamp;
                _pendingFrameProcessor = frameProcessor;
                _pendingDiagnosticsOn = videoDiagnosticsOn;
                _pendingDrawDiagnostics = drawDiagnostics;
                _gpuFrameReady = true;
            }

            // Signal encoding thread - non-blocking
            _gpuFrameSignal?.Set();

            // EXIT IMMEDIATELY - callback returns to camera in microseconds
        }

        /// <summary>
        /// Process GPU frame on background thread - all heavy work here.
        /// Called from GpuEncodingLoop on dedicated thread.
        /// </summary>
        private void ProcessGpuFrameOnThread(
            TimeSpan timestamp,
            Action<DrawableFrame> frameProcessor,
            bool videoDiagnosticsOn,
            Action<SKCanvas, int, int> drawDiagnostics)
        {
            if (!_isRecording || !_useGpuCameraPath || _gpuFrameProvider == null) return;

            // Defensive EGL check
            if (_eglDisplay == EGL14.EglNoDisplay || _eglSurface == EGL14.EglNoSurface || _eglContext == EGL14.EglNoContext)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] ProcessGpuFrameOnThread: EGL torn down");
                return;
            }

            if (!_encoderReady)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] ProcessGpuFrameOnThread: Encoder not ready");
                return;
            }

            try
            {
                _pendingTimestamp = timestamp;

                // Make EGL context current - CRITICAL: Must be done before UpdateTexImage!
                MakeCurrent();

                // CRITICAL: Update the SurfaceTexture on the EGL context thread
                // This calls UpdateTexImage() which MUST run on the thread that owns the EGL context
                if (!_gpuFrameProvider.TryProcessFrameNoWait(out long timestampNs))
                {
                    // No frame available yet
                    return;
                }

                // 1. Render camera texture to the encoder's framebuffer
                // CRITICAL: Reset GL state before rendering OES texture!
                // Skia modifies many GL states (blend, depth, stencil, scissor, program, etc.)
                // We MUST reset to clean state or subsequent frames will render black
                ResetGlStateForOesRendering();

                GLES20.GlViewport(0, 0, _width, _height);
                GLES20.GlClear(GLES20.GlColorBufferBit);

                // Render OES texture from SurfaceTexture
                _gpuFrameProvider.RenderToFramebuffer(_width, _height, _isFrontCamera);

                // Fire ML hook — raw camera frame in FBO 0, EGL current, before FrameProcessor overlays
                // Override OnRawFrameAcquired and call TryGetMLFrame synchronously (EGL context must be current)
                ParentCamera?.OnRawFrameAcquired(null, ParentCamera.DeviceRotation);

                // 2. Ensure Skia surface is ready for overlays
                if (_grContext == null)
                {
                    var glInterface = GRGlInterface.Create();
                    _grContext = GRContext.CreateGl(glInterface);
                }
                else
                {
                    // Full reset required - OES rendering touches many GL states
                    _grContext.ResetContext();
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

                    var fbInfo = new GRGlFramebufferInfo((uint)fb[0], 0x8058);
                    var backendRT = new GRBackendRenderTarget(_width, _height, samples[0], stencil[0], fbInfo);

                    _skSurface?.Dispose();
                    _skSurface = SKSurface.Create(_grContext, backendRT, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
                }

                // 3. Apply FrameProcessor overlays (user can draw on top of camera frame)
                var canvas = _skSurface.Canvas;
                // Note: Camera frame is already rendered to framebuffer, don't clear!

                if (frameProcessor != null || videoDiagnosticsOn)
                {
                    var frame = new DrawableFrame
                    {
                        Width = _width,
                        Height = _height,
                        Canvas = canvas,
                        Time = timestamp,
                        Scale = 1f  // Recording frame - full size
                    };

                    frameProcessor?.Invoke(frame);

                    if (videoDiagnosticsOn)
                    {
                        drawDiagnostics?.Invoke(canvas, _width, _height);
                    }
                }

                // 4. Flush and submit to encoder
                canvas.Flush();
                _grContext.Flush();

                if (!AndroidCameraExperimentalFlags.IsLegacyDualStreamPreviewDuringRecordingEnabled())
                {
                    // GPU-native preview generation (glBlitFramebuffer + async PBO, mirrors MetalPreviewScaler).
                    // Downscale on GPU first, then read back only the small buffer (~900KB vs ~8MB).
                    // Backpressure is handled by GlPreviewScaler's internal semaphore (MaxFramesInFlight=2):
                    // ScaleAndReadback returns null immediately when GPU is busy — no counter needed.

                    // Lazy-init scaler on first preview frame (EGL context is current here)
                    if (!_glPreviewScalerInitAttempted)
                    {
                        _glPreviewScalerInitAttempted = true;
                        int maxPw = ParentCamera?.NativeControl?.PreviewWidth ?? 800;
                        int pw = Math.Min(_width, maxPw);
                        int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));
                        _glPreviewScaler = new GlPreviewScaler();
                        if (!_glPreviewScaler.Initialize(_width, _height, pw, ph))
                        {
                            _glPreviewScaler?.Dispose();
                            _glPreviewScaler = null;
                        }
                    }

                    if (_glPreviewScaler != null)
                    {
                        // No ResetContext needed before/after: canvas was already flushed above,
                        // ScaleAndReadback only touches FBO bindings and restores FBO 0 on exit,
                        // and the next frame's ResetContext (after OES render) covers any residual state.
                        var previewImage = _glPreviewScaler.ScaleAndReadback();
                        if (previewImage != null)
                        {
                            lock (_previewLock)
                            {
                                _latestPreviewImage?.Dispose();
                                _latestPreviewImage = previewImage;
                            }

                            PreviewAvailable?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }

                // Periodic GPU resource cleanup to prevent memory accumulation during long recordings
                _gpuFrameCounter++;
                // if (_gpuFrameCounter >= GpuPurgeInterval)
                // {
                //     _gpuFrameCounter = 0;
                //     _grContext.PurgeUnlockedResources(false);  // false = don't scratchResourcesOnly
                // }

                // 5. Set presentation timestamp and swap to encoder
                long ptsNanos = (long)(timestamp.TotalMilliseconds * 1_000_000.0);
                EGLExt.EglPresentationTimeANDROID(_eglDisplay, _eglSurface, ptsNanos);
                EGL14.EglSwapBuffers(_eglDisplay, _eglSurface);

                // 6. Drain encoder
                bool bufferingMode = IsPreRecordingMode && _preRecordingBuffer != null;

                if (DateTime.Now - _lastKeyframeRequest >= _keyframeRequestInterval)
                {
                    RequestKeyFrame();
                    _lastKeyframeRequest = DateTime.Now;
                }

                if (bufferingMode)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        DrainEncoder(endOfStream: false, bufferingMode: true);
                    }
                }
                else
                {
                    DrainEncoder(endOfStream: false, bufferingMode: false);
                }

                EncodedFrameCount++;

                // Notify SkiaCamera for FPS tracking
                OnGpuFrameProcessed?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ProcessGpuFrameOnThread error: {ex.Message}");
            }
            finally
            {
                // Unbind context
                try { EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext); } catch { }
            }
        }

        #endregion

        /// <summary>
        /// Initialize GPU camera path with SurfaceTexture.
        /// Must be called on GL thread with valid EGL context (after SetupEglForCodecSurface).
        /// </summary>
        /// <param name="isFrontCamera">True if using front/selfie camera</param>
        /// <param name="cameraWidth">Camera's native output width (before rotation correction)</param>
        /// <param name="cameraHeight">Camera's native output height (before rotation correction)</param>
        public bool InitializeGpuCameraPath(bool isFrontCamera, int cameraWidth = 0, int cameraHeight = 0)
        {
            if (!GpuCameraFrameProvider.IsSupported())
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] GPU camera path not supported on this device");
                return false;
            }

            try
            {
                // Ensure EGL context is current
                MakeCurrent();

                // Use camera's native dimensions for SurfaceTexture if provided,
                // otherwise use encoder dimensions. Camera frames come in camera's
                // native orientation, and the transform matrix handles rotation.
                int surfaceW = cameraWidth > 0 ? cameraWidth : _width;
                int surfaceH = cameraHeight > 0 ? cameraHeight : _height;

                _gpuFrameProvider = new GpuCameraFrameProvider();
                if (!_gpuFrameProvider.Initialize(surfaceW, surfaceH))
                {
                    System.Diagnostics.Debug.WriteLine("[AndroidEncoder] Failed to initialize GpuCameraFrameProvider");
                    _gpuFrameProvider?.Dispose();
                    _gpuFrameProvider = null;
                    return false;
                }

                _isFrontCamera = isFrontCamera;
                _useGpuCameraPath = true;

                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] GPU camera path initialized: encoder={_width}x{_height}, camera={surfaceW}x{surfaceH}, frontCamera={isFrontCamera}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] InitializeGpuCameraPath error: {ex.Message}");
                _gpuFrameProvider?.Dispose();
                _gpuFrameProvider = null;
                _useGpuCameraPath = false;
                return false;
            }
            finally
            {
                // Unbind context
                try { EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext); } catch { }
            }
        }

        /// <summary>
        /// Process a frame from the GPU camera path.
        /// Renders camera texture to encoder surface and applies FrameProcessor overlays.
        /// </summary>
        /// <param name="timestamp">Frame timestamp relative to recording start</param>
        /// <param name="frameProcessor">Optional frame processor for overlays</param>
        /// <param name="videoDiagnosticsOn">Whether to draw diagnostics</param>
        /// <param name="drawDiagnostics">Diagnostics drawing callback</param>
        public async Task ProcessGpuCameraFrameAsync(
            TimeSpan timestamp,
            Action<DrawableFrame> frameProcessor,
            bool videoDiagnosticsOn,
            Action<SKCanvas, int, int> drawDiagnostics)
        {
            if (!_isRecording || !_useGpuCameraPath || _gpuFrameProvider == null) return;

            // Defensive EGL check
            if (_eglDisplay == EGL14.EglNoDisplay || _eglSurface == EGL14.EglNoSurface || _eglContext == EGL14.EglNoContext)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] ProcessGpuCameraFrame: EGL torn down");
                return;
            }

            if (!_encoderReady)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] ProcessGpuCameraFrame: Encoder not ready");
                return;
            }

            try
            {
                _pendingTimestamp = timestamp;

                // Make EGL context current - CRITICAL: Must be done before UpdateTexImage!
                MakeCurrent();

                // CRITICAL: Update the SurfaceTexture on the EGL context thread
                // This calls UpdateTexImage() which MUST run on the thread that owns the EGL context
                if (!_gpuFrameProvider.TryProcessFrameNoWait(out long timestampNs))
                {
                    // No frame available yet
                    return;
                }

                // 1. Render camera texture to the encoder's framebuffer
                // CRITICAL: Reset GL state before rendering OES texture!
                // Skia modifies many GL states (blend, depth, stencil, scissor, program, etc.)
                // We MUST reset to clean state or subsequent frames will render black
                ResetGlStateForOesRendering();

                GLES20.GlViewport(0, 0, _width, _height);
                GLES20.GlClear(GLES20.GlColorBufferBit);

                // Render OES texture from SurfaceTexture
                _gpuFrameProvider.RenderToFramebuffer(_width, _height, _isFrontCamera);

                // 2. Ensure Skia surface is ready for overlays
                if (_grContext == null)
                {
                    var glInterface = GRGlInterface.Create();
                    _grContext = GRContext.CreateGl(glInterface);
                }
                else
                {
                    // Full reset required - OES rendering touches many GL states
                    _grContext.ResetContext();
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

                    var fbInfo = new GRGlFramebufferInfo((uint)fb[0], 0x8058);
                    var backendRT = new GRBackendRenderTarget(_width, _height, samples[0], stencil[0], fbInfo);

                    _skSurface?.Dispose();
                    _skSurface = SKSurface.Create(_grContext, backendRT, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
                }

                // 3. Apply FrameProcessor overlays (user can draw on top of camera frame)
                var canvas = _skSurface.Canvas;
                // Note: Camera frame is already rendered to framebuffer, don't clear!

                if (frameProcessor != null || videoDiagnosticsOn)
                {
                    var frame = new DrawableFrame
                    {
                        Width = _width,
                        Height = _height,
                        Canvas = canvas,
                        Time = timestamp,
                        Scale = 1f  // Recording frame - full size
                    };

                    frameProcessor?.Invoke(frame);

                    if (videoDiagnosticsOn)
                    {
                        drawDiagnostics?.Invoke(canvas, _width, _height);
                    }
                }

                // 4. Flush and submit to encoder
                canvas.Flush();
                _grContext.Flush();

                // Preview is now provided by ImageReader stream, not encoder output
                // This avoids expensive GPU->CPU transfer during recording
                // Keeping code commented for reference:
                /*
                _previewFrameCounter++;
                bool needsPreview = false;
                if (_previewFrameCounter >= 3)
                {
                    _previewFrameCounter = 0;
                    needsPreview = true;
                }

                if (needsPreview)
                {
                    SKImage keepAlive = null;
                    try
                    {
                        using var gpuSnap = _skSurface.Snapshot();
                        if (gpuSnap != null)
                        {
                            int maxPreviewWidth = ParentCamera?.NativeControl?.PreviewWidth ?? 800;
                            int pw = Math.Min(_width, maxPreviewWidth);
                            int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));

                            if (_previewRasterSurface == null || _previewWidth != pw || _previewHeight != ph)
                            {
                                _previewRasterSurface?.Dispose();
                                var pInfo = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
                                _previewRasterSurface = SKSurface.Create(pInfo);
                                _previewWidth = pw;
                                _previewHeight = ph;
                            }

                            var pCanvas = _previewRasterSurface.Canvas;
                            pCanvas.Clear(SKColors.Transparent);
                            pCanvas.DrawImage(gpuSnap, new SKRect(0, 0, pw, ph));

                            keepAlive = _previewRasterSurface.Snapshot();
                            lock (_previewLock)
                            {
                                _latestPreviewImage?.Dispose();
                                _latestPreviewImage = keepAlive;
                                keepAlive = null;
                            }
                            PreviewAvailable?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    catch { keepAlive?.Dispose(); }
                    finally { keepAlive?.Dispose(); }
                }
                */

                // 5. Set presentation timestamp and swap to encoder
                long ptsNanos = (long)(timestamp.TotalMilliseconds * 1_000_000.0);
                EGLExt.EglPresentationTimeANDROID(_eglDisplay, _eglSurface, ptsNanos);
                EGL14.EglSwapBuffers(_eglDisplay, _eglSurface);

                // 6. Drain encoder
                bool bufferingMode = IsPreRecordingMode && _preRecordingBuffer != null;

                if (DateTime.Now - _lastKeyframeRequest >= _keyframeRequestInterval)
                {
                    RequestKeyFrame();
                    _lastKeyframeRequest = DateTime.Now;
                }

                if (bufferingMode)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        DrainEncoder(endOfStream: false, bufferingMode: true);
                    }
                }
                else
                {
                    DrainEncoder(endOfStream: false, bufferingMode: false);
                }

                EncodedFrameCount++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ProcessGpuCameraFrame error: {ex.Message}");
            }
            finally
            {
                // Unbind context
                try { EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext); } catch { }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Dispose GPU camera path resources.
        /// </summary>
        public void DisposeGpuCameraPath()
        {
            _gpuFrameProvider?.Stop();
            _gpuFrameProvider?.Dispose();
            _gpuFrameProvider = null;
            _useGpuCameraPath = false;
            System.Diagnostics.Debug.WriteLine("[AndroidEncoder] GPU camera path disposed");
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

            // Stop GPU encoding thread first
            StopGpuEncodingThread();

            _progressTimer?.Dispose();

            if (IsPreRecordingMode && _preRecordingBuffer != null && _videoCodec != null)
            {
                // Only dispose if not a shared buffer (SkiaCamera owns shared buffers)
                if (_preRecordingBuffer != SharedPreRecordingBuffer)
                {
                    _preRecordingBuffer?.Dispose();
                }
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
            ResetAudioTiming();
        }

        /// <summary>
        /// Writes the pre-recording buffer to disk after draining any remaining encoded tail
        /// frames that were already submitted before the handoff finished.
        /// The caller is responsible for disposing this encoder afterwards.
        /// </summary>
        public async Task<CapturedVideo> FlushPreRecBufferAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] FlushPreRecBufferAsync: {_preRecordingBuffer?.GetFrameCount() ?? 0} buffered frames → {_outputPath}");

            if (_preRecordingBuffer == null || _preRecordingBuffer.GetFrameCount() == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] FlushPreRecBufferAsync: buffer empty, skipping write");
                return new CapturedVideo { FilePath = _outputPath };
            }

            // The handoff may leave a small number of encoded frames queued in MediaCodec even after
            // the camera has been rerouted to the new encoder. Drain those outputs into the pre-rec
            // buffer before writing the file so the final mux has no dead gap at the seam.
            int drainedTailFrames = await DrainBufferedTailFramesAsync();
            if (drainedTailFrames > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] FlushPreRecBufferAsync: drained {drainedTailFrames} buffered tail frames before writing");
            }

            var bufferedFrames = _preRecordingBuffer.GetAllFrames();
            if (bufferedFrames.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] FlushPreRecBufferAsync: buffer empty after prune");
                return new CapturedVideo { FilePath = _outputPath };
            }

            // Wait for video format if not yet received (codec may still be warming up)
            if (_videoFormat == null)
            {
                for (int i = 0; i < 10 && _videoFormat == null; i++)
                {
                    DrainEncoder(endOfStream: false, bufferingMode: true);
                    if (_videoFormat == null)
                        await Task.Delay(50);
                }
            }

            if (_videoFormat == null)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] FlushPreRecBufferAsync: no video format — cannot write file");
                return new CapturedVideo { FilePath = _outputPath };
            }

            // Write buffer to file. Deliberately do NOT drain the codec pipeline (EOS) here:
            // any frames still in the codec's pipeline were submitted DURING the 2-encoder transition
            // and have timestamps beyond the 3s pre-rec window. Including them would extend preLastPts
            // and push the live chunk offset past the expected join point.
            var muxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
            muxer.SetOrientationHint(_deviceRotation);

            int videoTrack = muxer.AddTrack(_videoFormat);
            muxer.Start();

            _muxerBufferInfo ??= new MediaCodec.BufferInfo();
            foreach (var (data, timestamp, isKeyFrame) in bufferedFrames)
            {
                if (data == null || data.Length == 0) continue;
                var buf = Java.Nio.ByteBuffer.Wrap(data);
                var flags = isKeyFrame ? MediaCodecBufferFlags.KeyFrame : MediaCodecBufferFlags.None;
                _muxerBufferInfo.Set(0, data.Length, (long)timestamp.TotalMicroseconds, flags);
                muxer.WriteSampleData(videoTrack, buf, _muxerBufferInfo);
            }

            muxer.Stop();
            muxer.Release();

            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] FlushPreRecBufferAsync: wrote {bufferedFrames.Count} frames");
            return new CapturedVideo { FilePath = _outputPath };
        }

        /// <summary>
        /// Writes the currently retained pre-record buffer to a standalone MP4 file without draining
        /// any additional codec output. Intended for the deferred-flush path where live recording has
        /// already completed to a temp file and the pre-record segment is serialized at stop time.
        /// </summary>
        public async Task<CapturedVideo> WriteBufferedPreRecordingToFileAsync(string outputPath)
        {
            if (_preRecordingBuffer == null || _preRecordingBuffer.GetFrameCount() == 0)
            {
                return new CapturedVideo { FilePath = outputPath };
            }

            _preRecordingBuffer.PruneToMaxDuration();
            var bufferedFrames = _preRecordingBuffer.GetAllFrames();
            if (bufferedFrames.Count == 0)
            {
                return new CapturedVideo { FilePath = outputPath };
            }

            TimeSpan firstBufferedFrameTimestamp = bufferedFrames[0].Timestamp;

            if (_videoFormat == null)
            {
                System.Diagnostics.Debug.WriteLine("[AndroidEncoder] Deferred pre-record write skipped: no video format available");
                return new CapturedVideo { FilePath = outputPath };
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); } catch { }
            }

            using var muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
            muxer.SetOrientationHint(_deviceRotation);
            int videoTrack = muxer.AddTrack(_videoFormat);
            int audioTrack = -1;

            List<(byte[] Data, long TimestampUs, int Size)> encodedAudioFramesToWrite = null;
            if (_recordAudio &&
                _audioFormat != null &&
                _encodedAudioBuffer != null &&
                _encodedAudioBuffer.FrameCount > 0 &&
                CaptureStartAbsoluteNs > 0 &&
                _deferredPreRecordingFirstFrameOffset != TimeSpan.MinValue)
            {
                long absoluteFirstVideoFrameUs = (CaptureStartAbsoluteNs / 1000L)
                    + (long)_deferredPreRecordingFirstFrameOffset.TotalMicroseconds
                    + (long)firstBufferedFrameTimestamp.TotalMicroseconds;
                long lastWrittenAudioPtsUs = -1;
                encodedAudioFramesToWrite = new List<(byte[] Data, long TimestampUs, int Size)>();

                foreach (var (aacData, timestampUs, size) in _encodedAudioBuffer.GetAllFrames())
                {
                    long normalizedPtsUs = timestampUs - absoluteFirstVideoFrameUs;
                    if (normalizedPtsUs < 0)
                    {
                        continue;
                    }

                    if (normalizedPtsUs <= lastWrittenAudioPtsUs)
                    {
                        normalizedPtsUs = lastWrittenAudioPtsUs + 1;
                    }

                    lastWrittenAudioPtsUs = normalizedPtsUs;
                    encodedAudioFramesToWrite.Add((aacData, normalizedPtsUs, size));
                }

                if (encodedAudioFramesToWrite.Count > 0)
                {
                    audioTrack = muxer.AddTrack(_audioFormat);
                }
            }
            muxer.Start();

            _muxerBufferInfo ??= new MediaCodec.BufferInfo();
            foreach (var (data, timestamp, isKeyFrame) in bufferedFrames)
            {
                if (data == null || data.Length == 0)
                    continue;

                var buf = Java.Nio.ByteBuffer.Wrap(data);
                var flags = isKeyFrame ? MediaCodecBufferFlags.KeyFrame : MediaCodecBufferFlags.None;
                long normalizedVideoPtsUs = (long)(timestamp - firstBufferedFrameTimestamp).TotalMicroseconds;
                _muxerBufferInfo.Set(0, data.Length, normalizedVideoPtsUs, flags);
                muxer.WriteSampleData(videoTrack, buf, _muxerBufferInfo);
            }

            if (audioTrack >= 0 && encodedAudioFramesToWrite != null)
            {
                foreach (var (aacData, normalizedPtsUs, size) in encodedAudioFramesToWrite)
                {
                    var audioBuffer = Java.Nio.ByteBuffer.Wrap(aacData);
                    _muxerBufferInfo.Set(0, size, normalizedPtsUs, MediaCodecBufferFlags.None);
                    muxer.WriteSampleData(audioTrack, audioBuffer, _muxerBufferInfo);
                }
            }

            muxer.Stop();
            return new CapturedVideo { FilePath = outputPath };
        }

        private async Task<int> DrainBufferedTailFramesAsync(int maxWaitMs = 1500, int quietPeriodMs = 200)
        {
            if (_preRecordingBuffer == null || _videoCodec == null)
                return 0;

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int totalAddedFrames = 0;
            long lastGrowthMs = 0;

            while (stopwatch.ElapsedMilliseconds < maxWaitMs)
            {
                int beforeCount = _preRecordingBuffer.GetFrameCount();

                // Drain several times per iteration because a single non-EOS call only dequeues
                // one output buffer. This lets the old encoder flush any remaining queued frames.
                for (int i = 0; i < 6; i++)
                {
                    DrainEncoder(endOfStream: false, bufferingMode: true);
                }

                int afterCount = _preRecordingBuffer.GetFrameCount();
                if (afterCount > beforeCount)
                {
                    totalAddedFrames += afterCount - beforeCount;
                    lastGrowthMs = stopwatch.ElapsedMilliseconds;
                }

                if (stopwatch.ElapsedMilliseconds - lastGrowthMs >= quietPeriodMs)
                    break;

                await Task.Delay(25);
            }

            return totalAddedFrames;
        }

        public async Task<CapturedVideo> StopAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] StopAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}, BufferFrames={(_preRecordingBuffer?.GetFrameCount() ?? 0)}");

            _isRecording = false;

            // Stop GPU encoding thread first (ensures no more frames are processed)
            StopGpuEncodingThread();

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
                            // Reuse cached BufferInfo to avoid per-frame Java allocations
                            _muxerBufferInfo ??= new MediaCodec.BufferInfo();
                            _muxerBufferInfo.Set(0, data.Length, (long)timestamp.TotalMicroseconds, flags);
                            _muxer.WriteSampleData(_videoTrackIndex, buffer, _muxerBufferInfo);
                        }

                        EncodingDuration = _preRecordingBuffer.GetBufferedDuration();
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Wrote buffered frames, duration: {EncodingDuration.TotalSeconds:F3}s");
                    }
                }

                // Only dispose if not a shared buffer (SkiaCamera owns shared buffers)
                if (_preRecordingBuffer != SharedPreRecordingBuffer)
                {
                    _preRecordingBuffer?.Dispose();
                }
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

            ResetAudioTiming();

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
            // Reuse cached BufferInfo to avoid per-call Java allocations
            _drainBufferInfo ??= new MediaCodec.BufferInfo();
            var bufferInfo = _drainBufferInfo;
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

                    if (_recordAudio && _audioFormat == null)
                    {
                        // Increment wait counter
                        _framesWaitingForAudio++;

                        // Force start if timeout exceeded
                        bool forceStart = _framesWaitingForAudio > MaxFramesWaitingForAudio;

                        if (!forceStart)
                        {
                            if (_framesWaitingForAudio % 10 == 0)
                                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Video format ready, waiting for audio format ({_framesWaitingForAudio}/{MaxFramesWaitingForAudio})...");

                            // Check audio again (maybe it just arrived)
                            DrainAudioEncoder();
                            if (_audioFormat == null)
                                continue; // Keep waiting
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ⚠️ Audio initialization timed out ({MaxFramesWaitingForAudio} frames). Starting muxer VIDEO ONLY.");
                        }
                    }

                    _videoTrackIndex = _muxer.AddTrack(newFormat);
                    // Add audio track if present (check again in case it arrived last moment)
                    if (_recordAudio && _audioFormat != null)
                    {
                        _audioTrackIndex = _muxer.AddTrack(_audioFormat);
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Added audio track at index {_audioTrackIndex}");
                    }
                    else if (_recordAudio)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Warning: Starting muxer WITHOUT requested audio track (Timed out)!");
                        // We must ensure we don't try to write audio later
                        _audioTrackIndex = -1;
                    }

                    _muxer.Start();
                    _muxerStarted = true;
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] MediaMuxer started (OnFormatChanged)");
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
                        bool skipWarmupSample = _skipFirstEncodedSample && !_isRecording;
                        if (_skipFirstEncodedSample && _isRecording)
                        {
                            _skipFirstEncodedSample = false;
                        }

                        // Skip the warm-up sample only while recording has not started yet.
                        if (skipWarmupSample)
                        {
                            _skipFirstEncodedSample = false;
                            System.Diagnostics.Debug.WriteLine("[AndroidEncoder] Skipped warm-up encoded sample before recording start");
                        }
                        else
                        {
                            // BUFFERING MODE: Append to circular buffer
                            if (bufferingMode)
                            {
                                // Use ArrayPool to avoid per-frame allocations (reduces GC pressure)
                                byte[] frameData = ArrayPool<byte>.Shared.Rent(bufferInfo.Size);
                                try
                                {
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
                                    // AppendEncodedFrame copies data internally, so buffer can be returned immediately
                                    _preRecordingBuffer.AppendEncodedFrame(frameData, bufferInfo.Size, normalizedTimestamp);

                                    EncodedFrameCount++;
                                    EncodedDataSize += bufferInfo.Size;
                                    EncodingDuration = DateTime.Now - _startTime;
                                    EncodingStatus = "Buffering";
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(frameData);
                                }
                            }
                            // LIVE RECORDING MODE: Write to muxer
                            else
                            {
                                // Lazy Start Failsafe: If Audio arrived late
                                if (!_muxerStarted && _videoFormat != null)
                                {
                                    _framesWaitingForAudio++;
                                    bool forceStart = _framesWaitingForAudio > MaxFramesWaitingForAudio;

                                    if (!_recordAudio || _audioFormat != null || forceStart)
                                    {
                                        try
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Lazy Muxer Start triggered in Data block (Force={forceStart})");
                                            _videoTrackIndex = _muxer.AddTrack(_videoFormat);
                                            if (_recordAudio && _audioFormat != null)
                                                _audioTrackIndex = _muxer.AddTrack(_audioFormat);
                                            else
                                                _audioTrackIndex = -1;

                                            _muxer.Start();
                                            _muxerStarted = true;
                                        }
                                        catch (Exception exMux)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Lazy Start Failed: {exMux.Message}");
                                        }
                                    }
                                }

                                if (_muxerStarted)
                                {
                                    long pts = bufferInfo.PresentationTimeUs;

                                    // For live frames after transition, prefer the first keyframe as the seam marker,
                                    // but do not drop earlier frames. Dropping them creates visible dead gaps on
                                    // slower devices while the fresh encoder is still spinning up.
                                    if (_waitForFirstLiveKeyframe)
                                    {
                                        bool isKeyFrame = (bufferInfo.Flags & MediaCodecBufferFlags.KeyFrame) != 0;
                                        if (isKeyFrame)
                                        {
                                            // If no live samples were written yet, anchor the timeline to this keyframe.
                                            // Otherwise keep the existing base so timestamps stay monotonic.
                                            if (_firstEncodedFrameOffset == TimeSpan.MinValue)
                                            {
                                                _firstEncodedFrameOffset = TimeSpan.FromMicroseconds(pts);
                                            }
                                            _waitForFirstLiveKeyframe = false;
                                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] ✓ First live keyframe at {pts / 1000.0:F2}ms - established timestamp base");
                                        }
                                    }

                                    if (_firstEncodedFrameOffset == TimeSpan.MinValue)
                                    {
                                        _firstEncodedFrameOffset = TimeSpan.FromMicroseconds(pts);
                                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Using current live frame as timestamp base at {pts / 1000.0:F2}ms");
                                    }

                                    // Normalize to start from 0, then add pre-recording offset (if any)
                                    long normalizedPts = pts - (long)_firstEncodedFrameOffset.TotalMicroseconds;
                                    long finalPts = normalizedPts + (long)_preRecordingDuration.TotalMicroseconds;

                                    // Reuse cached BufferInfo to avoid per-frame Java allocations
                                    _muxerBufferInfo ??= new MediaCodec.BufferInfo();
                                    _muxerBufferInfo.Set(0, bufferInfo.Size, finalPts, bufferInfo.Flags);

                                    // Copy frame data out of the codec buffer and release the codec output slot
                                    // immediately — frees the encoder regardless of muxer timing.
                                    byte[] frameData = ArrayPool<byte>.Shared.Rent(bufferInfo.Size);
                                    try
                                    {
                                        encodedData.Position(bufferInfo.Offset);
                                        encodedData.Get(frameData, 0, bufferInfo.Size);

                                        _videoCodec.ReleaseOutputBuffer(outIndex, false);
                                        outIndex = -1; // mark released — outer ReleaseOutputBuffer will be skipped

                                        var writeBuf = Java.Nio.ByteBuffer.Wrap(frameData, 0, bufferInfo.Size);
                                        _muxer.WriteSampleData(_videoTrackIndex, writeBuf, _muxerBufferInfo);

                                        EncodedFrameCount++;
                                        EncodedDataSize += bufferInfo.Size;
                                        EncodingDuration = DateTime.Now - _startTime;
                                        EncodingStatus = "Encoding";
                                    }
                                    finally
                                    {
                                        ArrayPool<byte>.Shared.Return(frameData);
                                    }
                                }
                            }
                        }
                    }

                    if (outIndex >= 0)
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

        /// <summary>
        /// Reset OpenGL ES state to clean defaults before rendering OES texture.
        /// CRITICAL: Skia modifies many GL states during overlay rendering.
        /// Without resetting, subsequent frames render black because blend/stencil/etc are wrong.
        /// </summary>
        private void ResetGlStateForOesRendering()
        {
            // Unbind any program Skia left bound
            GLES20.GlUseProgram(0);

            // Disable blending (Skia enables this for transparency)
            GLES20.GlDisable(GLES20.GlBlend);

            // Disable depth test
            GLES20.GlDisable(GLES20.GlDepthTest);

            // Disable stencil test (Skia uses stencil for clipping paths)
            GLES20.GlDisable(GLES20.GlStencilTest);

            // Disable scissor test (Skia may use this for clipping)
            GLES20.GlDisable(GLES20.GlScissorTest);

            // Reset color mask to write all channels
            GLES20.GlColorMask(true, true, true, true);

            // Reset depth mask
            GLES20.GlDepthMask(true);

            // Unbind any texture from texture unit 0
            GLES20.GlActiveTexture(GLES20.GlTexture0);
            GLES20.GlBindTexture(GLES20.GlTexture2d, 0);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);

            // Bind default framebuffer
            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0);

            // Unbind VBO and VAO that Skia may have left bound
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);
            GLES20.GlBindBuffer(GLES20.GlElementArrayBuffer, 0);

            // Clear any errors that accumulated
            while (GLES20.GlGetError() != GLES20.GlNoError) { }
        }

        private sealed class FrameScope : IDisposable { public void Dispose() { } }

        private void TryReleaseCodec()
        {
            try { _videoCodec?.Stop(); } catch { }
            try { _videoCodec?.Release(); } catch { }
            _videoCodec = null;

            try { _audioCodec?.Stop(); } catch { }
            try { _audioCodec?.Release(); } catch { }
            _audioCodec = null;

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

        private void ResetAudioTiming()
        {
            _audioPtsBaseNs = -1;
            _audioPtsOffsetUs = 0;
            _audioPresentationTimeUs = 0;
            _lastMuxerAudioPtsUs = 0;
            _maxExpectedEncoderOutputPtsUs = 0;
        }

        private long CalculateAudioPts(long timestampNs)
        {
            if (timestampNs < 0)
            {
                timestampNs = 0;
            }

            if (_audioPtsBaseNs < 0)
            {
                _audioPtsBaseNs = timestampNs;
            }

            long relativeUs = (timestampNs - _audioPtsBaseNs) / 1000;
            if (relativeUs < 0)
            {
                relativeUs = 0;
            }

            long finalPtsUs = relativeUs + _audioPtsOffsetUs;

            // Guard: new input PTS must exceed every PTS the encoder might still emit from
            // previous inputs (pipeline delay causes deferred frames at input_N + k*23220µs).
            // Using only _lastMuxerAudioPtsUs is insufficient because deferred frames not yet
            // written to the muxer can have higher PTS than what was actually written.
            // _maxExpectedEncoderOutputPtsUs tracks the ceiling of all in-flight encoder outputs.
            long minNextPtsUs = Math.Max(_audioPresentationTimeUs,
                Math.Max(_lastMuxerAudioPtsUs, _maxExpectedEncoderOutputPtsUs));
            if (finalPtsUs <= minNextPtsUs)
            {
                finalPtsUs = minNextPtsUs + 1;
            }

            _audioPresentationTimeUs = finalPtsUs;
            return finalPtsUs;
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
                // NOTE: Do NOT call EglTerminate — it destroys the process-wide default display,
                // which invalidates EGL contexts used by other components (e.g. GlPreviewRenderer).
                // EglReleaseThread is sufficient to clean up per-thread EGL state.
                EGL14.EglReleaseThread();
            }
            _eglDisplay = EGL14.EglNoDisplay;
            _eglSurface = EGL14.EglNoSurface;
            _eglContext = EGL14.EglNoContext;

            lock (_previewLock)
            {
                _latestPreviewImage?.Dispose();
                _latestPreviewImage = null;
            }

            _previewRasterSurface?.Dispose();
            _previewRasterSurface = null;

            _glPreviewScaler?.Dispose();
            _glPreviewScaler = null;
            _glPreviewScalerInitAttempted = false;

            _mlGlScaler?.Dispose();
            _mlGlScaler = null;
        }

        private void FeedAudioEncoder(AudioSample sample)
        {
            if (_audioCodec == null) return;

            try
            {
                // Lock audio semaphore
                _audioSemaphore.Wait();

                int index = _audioCodec.DequeueInputBuffer(1000); // 1ms wait
                if (index >= 0)
                {
                    var buffer = _audioCodec.GetInputBuffer(index);
                    buffer.Clear();

                    var data = sample.Data;
                    if (data.Length > buffer.Remaining())
                    {
                        // Truncate if too large (shouldn't happen if sized correctly)
                        // data = data.Take(buffer.Remaining()).ToArray(); // Need using System.Linq
                        // Better use Array.Copy for perf and no linq allocations
                        var newData = new byte[buffer.Remaining()];
                        Array.Copy(data, newData, newData.Length);
                        data = newData;
                    }

                    buffer.Put(data);

                    long ptsUs = CalculateAudioPts(sample.TimestampNs);

                    _audioCodec.QueueInputBuffer(index, 0, data.Length, ptsUs, 0);

                    // Track the ceiling PTS the encoder may still emit for this input.
                    // AAC encoder has ~1 frame of internal buffering: a chunk with N AAC frames
                    // may produce only N-1 frames immediately; the last one is deferred.
                    // Guard in CalculateAudioPts must account for this, not just _lastMuxerAudioPtsUs.
                    int numSamples = data.Length / AudioBytesPerSample;
                    int numAacFrames = Math.Max(1, (numSamples + AacSamplesPerFrame - 1) / AacSamplesPerFrame);
                    _maxExpectedEncoderOutputPtsUs = Math.Max(_maxExpectedEncoderOutputPtsUs,
                        ptsUs + (long)(numAacFrames - 1) * AacFrameDurationUs);
                }

                DrainAudioEncoder();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] FeedAudio error: {ex.Message}");
            }
            finally
            {
                _audioSemaphore.Release();
            }
        }

        private void DrainAudioEncoder()
        {
            if (_audioCodec == null) return;

            var info = new MediaCodec.BufferInfo();
            while (true)
            {
                int encoderStatus = _audioCodec.DequeueOutputBuffer(info, 0); // No wait
                if (encoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                {
                    break;
                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    if (_muxerStarted)
                    {
                        // This can happen if audio starts later? 
                        // But Muxer doesn't support adding tracks after start.
                        // We must ensure this happens before Muxer.Start() in StartAsync logic
                        _audioFormat = _audioCodec.OutputFormat;
                    }
                    else
                    {
                        _audioFormat = _audioCodec.OutputFormat;
                        System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Audio Format Changed: {_audioFormat}");
                    }
                }
                else if (encoderStatus >= 0)
                {
                    var encodedData = _audioCodec.GetOutputBuffer(encoderStatus);
                    if (encodedData == null)
                    {
                        _audioCodec.ReleaseOutputBuffer(encoderStatus, false);
                        continue;
                    }

                    if ((info.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                    {
                        // Codec config, ignore
                        info.Size = 0;
                    }

                    if (info.Size > 0 && _muxerStarted && _audioTrackIndex >= 0)
                    {
                        // Write to muxer. Muxer is shared resource.
                        // Use a lock. _warmupLock is used for warmup, maybe not best.
                        // Use new lock object
                        lock (_warmupLock)
                        {
                            _muxer.WriteSampleData(_audioTrackIndex, encodedData, info);
                            // Track highest PTS written — codec extrapolates output PTS beyond input PTS
                            // (one PCM input can yield multiple AAC outputs at input_PTS + n*23220µs).
                            // CalculateAudioPts must guard against this, not just against _audioPresentationTimeUs.
                            if (info.PresentationTimeUs > _lastMuxerAudioPtsUs)
                                _lastMuxerAudioPtsUs = info.PresentationTimeUs;
                        }
                    }

                    _audioCodec.ReleaseOutputBuffer(encoderStatus, false);

                    if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Starts background encoding of PCM to AAC to eliminate transition lag
        /// </summary>
        private void StartBackgroundAudioEncoding()
        {
            if (_backgroundEncodingTask != null && !_backgroundEncodingTask.IsCompleted)
                return;

            _encodingCancellation = new CancellationTokenSource();
            _encodedAudioBuffer = new CircularEncodedAudioBuffer(TimeSpan.FromSeconds(10)); // Longer buffer for encoded data

            // Capture token locally — StopBackgroundAudioEncoding nulls _encodingCancellation after
            // Wait(500) even if the task is still running, causing NullReferenceException on the field.
            var token = _encodingCancellation.Token;

            _backgroundEncodingTask = Task.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[AndroidEncoder] Started background PCM→AAC encoding");

                    // Track the last encoded timestamp so each loop only processes NEW samples.
                    // Using GetAllSamples() would re-encode every buffered sample every 10ms, producing
                    // thousands of duplicate frames and wasting CPU/JVM objects.
                    long lastEncodedTimestampNs = long.MinValue;

                    while (!token.IsCancellationRequested && IsPreRecordingMode)
                    {
                        try
                        {
                            // Only fetch samples that arrived after the last encoded one
                            var pcmSamples = _audioBuffer?.GetSamplesAfter(lastEncodedTimestampNs);
                            if (pcmSamples == null || pcmSamples.Length == 0)
                            {
                                await Task.Delay(50, token);
                                continue;
                            }

                            // Encode PCM chunks to AAC in background
                            foreach (var pcmSample in pcmSamples)
                            {
                                if (token.IsCancellationRequested)
                                    break;

                                var aacData = await EncodePcmToAacAsync(pcmSample);
                                if (aacData.Length > 0)
                                {
                                    // Store absolute timestamp in μs — renormalized to video timeline during StartAsync flush
                                    long absoluteTimestampUs = pcmSample.TimestampNs / 1000;

                                    _encodedAudioBuffer?.AppendEncodedFrame(aacData, aacData.Length, absoluteTimestampUs);
                                }

                                // Advance cursor even if encoding produced no output (avoids re-processing)
                                lastEncodedTimestampNs = pcmSample.TimestampNs;
                            }

                            await Task.Delay(10, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] Background encoding error: {ex.Message}");
                            await Task.Delay(100);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    System.Diagnostics.Debug.WriteLine("[AndroidEncoder] Background PCM→AAC encoding stopped");
                }
            }, token);
        }

        /// <summary>
        /// Asynchronously encode PCM sample to AAC using a temporary audio encoder
        /// </summary>
        private async Task<byte[]> EncodePcmToAacAsync(AudioSample pcmSample)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Create temporary audio encoder for background encoding
                    using var tempAudioCodec = MediaCodec.CreateEncoderByType(_selectedAudioMimeType);

                    var audioFormat = new MediaFormat();
                    audioFormat.SetString(MediaFormat.KeyMime, _selectedAudioMimeType);
                    audioFormat.SetInteger(MediaFormat.KeySampleRate, 44100);
                    audioFormat.SetInteger(MediaFormat.KeyChannelCount, 1);
                    audioFormat.SetInteger(MediaFormat.KeyBitRate, 128000);
                    audioFormat.SetInteger(MediaFormat.KeyMaxInputSize, 16384 * 2);

                    // Configure based on codec type
                    if (_selectedAudioMimeType == MediaFormat.MimetypeAudioAac)
                    {
                        audioFormat.SetInteger(MediaFormat.KeyAacProfile, (int)MediaCodecProfileType.Aacobjectlc);
                    }
                    // Add other codec configurations here as needed

                    tempAudioCodec.Configure(audioFormat, null, null, MediaCodecConfigFlags.Encode);
                    tempAudioCodec.Start();

                    // Feed PCM data
                    int inputIndex = tempAudioCodec.DequeueInputBuffer(10000); // 10ms timeout
                    if (inputIndex >= 0)
                    {
                        var inputBuffer = tempAudioCodec.GetInputBuffer(inputIndex);
                        inputBuffer.Clear();

                        var data = pcmSample.Data;
                        if (data.Length > inputBuffer.Remaining())
                        {
                            var newData = new byte[inputBuffer.Remaining()];
                            Array.Copy(data, newData, newData.Length);
                            data = newData;
                        }
                        inputBuffer.Put(data);

                        long ptsUs = pcmSample.TimestampNs / 1000;
                        tempAudioCodec.QueueInputBuffer(inputIndex, 0, data.Length, ptsUs, 0);
                    }

                    // Get encoded AAC data
                    var info = new MediaCodec.BufferInfo();
                    int outputIndex = tempAudioCodec.DequeueOutputBuffer(info, 10000);
                    if (outputIndex >= 0)
                    {
                        var outputBuffer = tempAudioCodec.GetOutputBuffer(outputIndex);
                        byte[] aacData = new byte[info.Size];
                        outputBuffer.Get(aacData);
                        tempAudioCodec.ReleaseOutputBuffer(outputIndex, false);
                        return aacData;
                    }

                    tempAudioCodec.Stop();
                    return Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidEncoder] AAC encoding error: {ex.Message}");
                    return Array.Empty<byte>();
                }
            });
        }

        /// <summary>
        /// Stops background encoding
        /// </summary>
        private void StopBackgroundAudioEncoding(bool clearEncodedBuffer = true)
        {
            try
            {
                _encodingCancellation?.Cancel();
                _backgroundEncodingTask?.Wait(500); // Wait up to 500ms for clean shutdown
            }
            catch
            {
                // Ignore cleanup errors
            }
            finally
            {
                _encodingCancellation?.Dispose();
                _encodingCancellation = null;
                _backgroundEncodingTask = null;
                if (clearEncodedBuffer)
                {
                    _encodedAudioBuffer?.Clear();
                    _encodedAudioBuffer = null;
                }
            }
        }

        public void Dispose()
        {
            // Ensure GPU encoding thread is stopped
            StopGpuEncodingThread();

            if (_isRecording)
            {
                try { StopAsync().GetAwaiter().GetResult(); } catch { }
            }
            DisposeGpuCameraPath();
            _progressTimer?.Dispose();

            // Stop background audio encoding
            StopBackgroundAudioEncoding();

            // Only dispose if not a shared buffer (SkiaCamera owns shared buffers)
            if (_preRecordingBuffer != SharedPreRecordingBuffer)
            {
                _preRecordingBuffer?.Dispose();
            }
            _preRecordingBuffer = null;

            // Dispose cached Java objects to avoid native memory leaks
            _drainBufferInfo?.Dispose();
            _drainBufferInfo = null;
            _muxerBufferInfo?.Dispose();
            _muxerBufferInfo = null;

            TryReleaseCodec();
            TryReleaseMuxer();
            TearDownEgl();
        }
    }
}
#endif
