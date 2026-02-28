#if IOS || MACCATALYST

using System.Diagnostics;
using AVFoundation;
using AVFoundation;
using AVKit;
using DrawnUi.Maui.Navigation;
using Foundation;
using Foundation;
using HealthKit;
using Metal; // Added for Zero-Copy path
using Photos;
using Photos;
using SkiaSharp.Views.Maui.Controls; // For SKGLView
using UIKit;

namespace DrawnUi.Camera;

public partial class SkiaCamera
{
    private const bool ShouldOptimizeForNetworkUse = false; //might reduce heat and throttling

    private Task _preRecFlushTask;
    private AudioSample[] _preRecordedAudioSamples;  // Saved at pre-rec → live transition

    // Pre-allocated buffer for pre-recording (avoids lag spike on record button press)
    // Allocated once when EnablePreRecording=true, reused across recording sessions
    private PrerecordingEncodedBufferApple _sharedPreRecordingBuffer;

    // Cached zero-copy SKImage to avoid per-frame GPU allocations during recording.
    // The image wraps the live Metal texture — Skia reads current texture data at draw time.
    private SKImage _cachedZeroCopyImage;
    private IntPtr _cachedZeroCopyTextureHandle;
    private GRContext _cachedZeroCopyContext;

    // Streaming audio writer for OOM-safe live recording (audio goes to file, not memory)
    private AVAssetWriter _liveAudioWriter;
    private AVAssetWriterInput _liveAudioInput;
    private string _liveAudioFilePath;
    private long _liveAudioFirstTimestampNs = -1;
    private readonly object _liveAudioWriterLock = new object();
    private List<IntPtr> _liveAudioMemoryToFree;  // Memory to free after writer finishes
    private bool _liveAudioWriterPreAllocated;  // True if writer is created but not yet started

    /// <summary>
    /// iOS/MacCatalyst implementation: Pre-allocates the circular buffer for pre-recording.
    /// Called when EnablePreRecording is set to true, before user presses record.
    /// This eliminates the ~20MB allocation lag spike on record button press.
    /// </summary>
    partial void EnsurePreRecordingBufferPreAllocated()
    {
        if (_sharedPreRecordingBuffer == null)
        {
            // Default to 12 Mbps if we don't have encoder bitrate yet
            long estimatedBitrate = 12_000_000;
            _sharedPreRecordingBuffer = new PrerecordingEncodedBufferApple(PreRecordDuration, estimatedBitrate);
            Debug.WriteLine($"[SkiaCamera.Apple] Pre-allocated shared pre-recording buffer: {PreRecordDuration.TotalSeconds}s @ ~{estimatedBitrate / 1_000_000}Mbps");
        }
        else
        {
            Debug.WriteLine("[SkiaCamera.Apple] Shared pre-recording buffer already allocated, reusing");
        }
    }

    /// <summary>
    /// Pre-allocates the AVAssetWriter and AVAssetWriterInput for live audio recording.
    /// Called during pre-recording start to avoid lag spike when transitioning to live recording.
    /// The writer is created but NOT started - StartWriting() is called later in ActivateLiveAudioWriter().
    /// </summary>
    private void EnsureLiveAudioWriterPreAllocated(int sampleRate, int channels)
    {
        lock (_liveAudioWriterLock)
        {
            // Already pre-allocated or actively writing
            if (_liveAudioWriter != null)
            {
                Debug.WriteLine("[EnsureLiveAudioWriterPreAllocated] Writer already exists, skipping");
                return;
            }

            try
            {
                // Create temp file path
                var tempDir = Path.GetTempPath();
                _liveAudioFilePath = Path.Combine(tempDir, $"live_audio_{Guid.NewGuid():N}.m4a");

                // Delete existing file
                if (File.Exists(_liveAudioFilePath))
                {
                    File.Delete(_liveAudioFilePath);
                }

                var url = NSUrl.FromFilename(_liveAudioFilePath);
                _liveAudioWriter = new AVAssetWriter(url, "com.apple.m4a-audio", out var writerError);

                if (_liveAudioWriter == null || writerError != null)
                {
                    Debug.WriteLine($"[EnsureLiveAudioWriterPreAllocated] AVAssetWriter creation failed: {writerError?.LocalizedDescription}");
                    return;
                }

                // Configure audio output (AAC)
                var audioSettings = new NSDictionary(
                    AVAudioSettings.AVFormatIDKey, NSNumber.FromInt32((int)AudioToolbox.AudioFormatType.MPEG4AAC),
                    AVAudioSettings.AVSampleRateKey, NSNumber.FromDouble(sampleRate),
                    AVAudioSettings.AVNumberOfChannelsKey, NSNumber.FromInt32(channels),
                    AVAudioSettings.AVEncoderBitRateKey, NSNumber.FromInt32(128000)
                );

                _liveAudioInput = new AVAssetWriterInput(AVMediaTypes.Audio.GetConstant(), new AVFoundation.AudioSettings(audioSettings));
                _liveAudioInput.ExpectsMediaDataInRealTime = true;

                if (!_liveAudioWriter.CanAddInput(_liveAudioInput))
                {
                    Debug.WriteLine("[EnsureLiveAudioWriterPreAllocated] Cannot add audio input to writer");
                    CleanupLiveAudioWriter();
                    return;
                }
                _liveAudioWriter.AddInput(_liveAudioInput);

                // Mark as pre-allocated but NOT started
                _liveAudioWriterPreAllocated = true;
                _liveAudioFirstTimestampNs = -1;
                _liveAudioMemoryToFree = new List<IntPtr>();

                Debug.WriteLine($"[EnsureLiveAudioWriterPreAllocated] Pre-allocated writer for: {_liveAudioFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EnsureLiveAudioWriterPreAllocated] Exception: {ex.Message}");
                CleanupLiveAudioWriter();
            }
        }
    }

    /// <summary>
    /// Activates a pre-allocated live audio writer by calling StartWriting().
    /// Called during pre-recording → live transition.
    /// </summary>
    private bool ActivateLiveAudioWriter()
    {
        lock (_liveAudioWriterLock)
        {
            if (_liveAudioWriter == null || _liveAudioInput == null)
            {
                Debug.WriteLine("[ActivateLiveAudioWriter] No pre-allocated writer available");
                return false;
            }

            if (!_liveAudioWriterPreAllocated)
            {
                // Already activated (actively writing)
                Debug.WriteLine("[ActivateLiveAudioWriter] Writer already active");
                return true;
            }

            try
            {
                // Start writing
                if (!_liveAudioWriter.StartWriting())
                {
                    Debug.WriteLine($"[ActivateLiveAudioWriter] StartWriting failed: {_liveAudioWriter.Error?.LocalizedDescription}");
                    CleanupLiveAudioWriter();
                    return false;
                }
                _liveAudioWriter.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);

                _liveAudioWriterPreAllocated = false;  // Now actively writing
                Debug.WriteLine($"[ActivateLiveAudioWriter] Activated writer, now streaming to: {_liveAudioFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActivateLiveAudioWriter] Exception: {ex.Message}");
                CleanupLiveAudioWriter();
                return false;
            }
        }
    }

    public virtual void SetZoom(double value)
    {
        TextureScale = value;
        NativeControl?.SetZoom((float)value);

        if (Display != null)
        {
            Display.ZoomX = TextureScale;
            Display.ZoomY = TextureScale;
        }

        Zoomed?.Invoke(this, value);
    }

    private void OnRecordingFrameAvailable()
    {
        // CRITICAL: Do synchronous checks BEFORE creating any Task to avoid async state machine
        // and Task allocation overhead when frames are dropped (fixes memory/GC pressure)
        if (!(IsRecording || IsPreRecording) || _captureVideoEncoder == null)
            return;

        // Make sure we never queue more than one frame — drop if previous is still processing
        if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) != 0)
        {
            System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            return;
        }

        _ = CaptureFrameCore();
    }

    private async Task CaptureFrameCore()
    {
        try
        {
            // Double-check encoder still exists (race condition protection)
            if (_captureVideoEncoder == null || (!IsRecording && !IsPreRecording))
                return;

            var elapsed = DateTime.Now - _captureVideoStartTime;

            // GPU path on Apple: draw via Skia into VideoToolbox encoder's pixel buffer
            if (_captureVideoEncoder is DrawnUi.Camera.AppleVideoToolboxEncoder appleEnc)
            {
                SKImage imageToDraw = null;
                bool shouldDisposeImage = true;
                int imageRotation = 0;
                bool imageFlip = false;

                // Try to get raw frame (faster)
                if (NativeControl is NativeCamera nativeCam)
                {
                    // ZERO-COPY PATH (Metal)
                    // Check EncodingContext (preferred) or legacy Context
                    var encoderContext = appleEnc.EncodingContext ?? appleEnc.Context;

                    if (imageToDraw == null && encoderContext != null && nativeCam.PreviewTexture != null)
                    {
                        try
                        {
                            var texture = nativeCam.PreviewTexture;
                            var handle = texture.Handle;

                            // Cache the SKImage wrapping the Metal texture — avoids per-frame
                            // GRMtlTextureInfo + GRBackendTexture + SKImage.FromTexture allocations.
                            // The image wraps a live GPU texture, so each draw reads current frame data.
                            if (_cachedZeroCopyImage == null || _cachedZeroCopyTextureHandle != handle || _cachedZeroCopyContext != encoderContext)
                            {
                                _cachedZeroCopyImage?.Dispose();
                                var textureInfo = new GRMtlTextureInfo(handle);
                                using var backendTexture = new GRBackendTexture(
                                    (int)texture.Width, (int)texture.Height, false, textureInfo);
                                _cachedZeroCopyImage = SKImage.FromTexture(
                                    encoderContext, backendTexture, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                                _cachedZeroCopyTextureHandle = handle;
                                _cachedZeroCopyContext = encoderContext;
                            }

                            if (_cachedZeroCopyImage != null)
                            {
                                imageToDraw = _cachedZeroCopyImage;
                                shouldDisposeImage = false;
                                imageRotation = (int)nativeCam.CurrentRotation;
                                imageFlip = (CameraDevice?.Facing ?? Facing) == CameraPosition.Selfie;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CaptureFrame] Zero-copy texture failed: {ex.Message}");
                        }
                    }

                    // No fallback to GetRawFullImage - it reads _latestRecordingFrame which was
                    // populated earlier and can be older than what zero-copy previously encoded,
                    // causing out-of-order frames in the video. Better to drop a frame than glitch.
                }

                // Fallback to standard preview image (slower, already rotated)
                if (imageToDraw == null)
                {
                    imageToDraw = NativeControl?.GetPreviewImage();
                    // GetPreviewImage returns already rotated image
                }

                if (imageToDraw == null)
                {
                    Debug.WriteLine("[CaptureFrame] No preview image available from camera");
                    return;
                }

                try
                {
                    using (appleEnc.BeginFrame(elapsed, out var canvas, out var info, DeviceRotation))
                    {
                        // If we have raw image, we need to handle rotation here
                        if (imageRotation != 0 || imageFlip)
                        {
                            canvas.Save();

                            // Apply transform to match HandleOrientationForPreview
                            switch (imageRotation)
                            {
                                case 90:
                                    canvas.Translate(info.Width, 0);
                                    canvas.RotateDegrees(90);
                                    if (imageFlip)
                                    {
                                        canvas.Scale(1, -1);
                                        canvas.Translate(0, -imageToDraw.Height);
                                    }

                                    break;
                                case 180:
                                    canvas.Translate(info.Width, info.Height);
                                    canvas.RotateDegrees(180);
                                    if (imageFlip)
                                    {
                                        canvas.Scale(1, -1);
                                        canvas.Translate(0, -imageToDraw.Height);
                                    }

                                    break;
                                case 270:
                                    canvas.Translate(0, info.Height);
                                    canvas.RotateDegrees(270);
                                    if (imageFlip)
                                    {
                                        canvas.Scale(1, -1);
                                        canvas.Translate(0, -imageToDraw.Height);
                                    }

                                    break;
                                default:
                                    if (imageFlip)
                                    {
                                        canvas.Translate(0, info.Height);
                                        canvas.Scale(1, -1);
                                    }

                                    break;
                            }

                            // Calculate virtual canvas size (swapped if 90/270)
                            int virtualW = info.Width;
                            int virtualH = info.Height;
                            if (imageRotation == 90 || imageRotation == 270)
                            {
                                virtualW = info.Height;
                                virtualH = info.Width;
                            }

                            var __rectsA = GetAspectFillRects(imageToDraw.Width, imageToDraw.Height, virtualW,
                                virtualH);
                            canvas.DrawImage(imageToDraw, __rectsA.src, __rectsA.dst);

                            canvas.Restore();
                        }
                        else
                        {
                            var __rectsA = GetAspectFillRects(imageToDraw.Width, imageToDraw.Height, info.Width,
                                info.Height);
                            canvas.DrawImage(imageToDraw, __rectsA.src, __rectsA.dst);
                        }

                        if (FrameProcessor != null || VideoDiagnosticsOn)
                        {
                            // Apply rotation based on device orientation
                            //var rotation = GetActiveRecordingRotation();
                            var checkpoint = canvas.Save();
                            //ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                            //var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                            var frame = new DrawableFrame
                            {
                                Width = info.Width,
                                Height = info.Height,
                                Canvas = canvas,
                                Time = elapsed,
                                Scale = 1f
                            };
                            FrameProcessor?.Invoke(frame);

                            if (VideoDiagnosticsOn)
                                DrawDiagnostics(canvas, info.Width, info.Height);

                            canvas.RestoreToCount(checkpoint);
                        }
                    }

                    var __swA = System.Diagnostics.Stopwatch.StartNew();
                    await appleEnc.SubmitFrameAsync();
                    __swA.Stop();
                    _diagLastSubmitMs = __swA.Elapsed.TotalMilliseconds;
                    System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                    CalculateRecordingFps();
                }
                finally
                {
                    if (shouldDisposeImage)
                        imageToDraw?.Dispose();
                }

                return;
            }

        }
        catch (Exception ex)
        {
            // Silently ignore exceptions during frame capture - this can happen during disposal/shutdown
            // The frame will simply be dropped and the next one processed
            if (ex is NullReferenceException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                // These are expected during shutdown - just return silently
            }
            else
            {
                Debug.WriteLine($"[CaptureFrame] Error: {ex.Message}");
            }
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
        }
    }

    /// <summary>
    /// Creates a platform-specific audio capture using AVAudioEngine.
    /// CRITICAL: This is completely separate from the camera session to avoid video freezing!
    /// Using NativeCamera (AVCaptureSession) for audio would cause BeginConfiguration/CommitConfiguration
    /// during recording, which disrupts video frames.
    /// </summary>
    protected IAudioCapture CreateAudioCapturePlatform()
    {
        // Use AVAudioEngine-based capture - completely independent from camera
        return new AudioCaptureApple();
    }

    private void OnAudioSampleAvailable(object sender, AudioSample sample)
    {
        var useSample = OnAudioSampleAvailable(sample);

        WriteAudioSample(useSample);
    }

    public virtual void WriteAudioSample(AudioSample sample)
    {
        // OOM-SAFE AUDIO HANDLING:
        // - Pre-recording phase: Write to circular buffer (bounded memory, ~5 sec max)
        // - Live recording phase: Stream directly to file (zero memory growth)
        if (IsPreRecording && !IsRecording)
        {
            // Pre-recording: Circular buffer keeps last N seconds (bounded memory)
            _audioBuffer?.Write(sample);
        }
        else if (IsRecording && _liveAudioWriter != null)
        {
            // Live recording: Stream directly to file (OOM-safe)
            WriteSampleToLiveAudioWriter(sample);
        }
    }

    private async Task<ICaptureVideoEncoder> StartRealtimeVideoProcessing(bool preserveCurrentEncoder = false)
    {
        // Stop preview audio - recording will take over with its own audio capture
        StopPreviewAudioCapture();

        // 1. Create Apple encoder using VideoToolbox for hardware H.264 encoding
        // Note: We create and configure the NEW encoder first, before touching the old one
        // This allows for seamless transition/overlap
        var appleEncoder = new AppleVideoToolboxEncoder();

        ICaptureVideoEncoder oldEncoderToReturn = null;
        
        // Configuration phase - working with local 'appleEncoder' variable
        if (EnableAudioRecording)
        {
            try
            {
                // ARCHITECTURAL FIX: Audio capture runs continuously through entire session
                // Create audio capture ONCE at session start (first call)
                if (_audioCapture == null)
                {
                    _audioCapture = CreateAudioCapturePlatform();
                    if (_audioCapture != null)
                        _audioCapture.SampleAvailable += OnAudioSampleAvailable;
                }

                // AUDIO HANDLING MODE (OOM-SAFE):
                // - Pre-recording phase: CIRCULAR buffer (bounded memory, keeps last N seconds)
                // - Live recording phase: STREAMING to file (zero memory growth)
                // Buffer/writer is switched at pre-rec → live transition (see StartVideoRecording)
                if (EnablePreRecording && !IsRecording)
                {
                    // Pre-recording phase: Circular buffer matching video duration
                    if (_audioBuffer == null)
                    {
                        _audioBuffer = new CircularAudioBuffer(PreRecordDuration);
                        Debug.WriteLine($"[StartRealtimeVideoProcessing] Created CIRCULAR audio buffer ({PreRecordDuration.TotalSeconds:F1}s)");
                    }

                    // Pre-allocate live audio writer to avoid lag spike at pre-rec → live transition
                    // Creates AVAssetWriter/Input but doesn't start writing yet
                    EnsureLiveAudioWriterPreAllocated(AudioSampleRate, AudioChannels);
                }
                else if (IsRecording && _liveAudioWriter == null)
                {
                    // Live-only (no pre-recording): Start streaming writer immediately
                    if (StartLiveAudioWriter(AudioSampleRate, AudioChannels))
                    {
                        Debug.WriteLine("[StartRealtimeVideoProcessing] Started STREAMING audio writer for live recording (OOM-safe)");
                    }
                    else
                    {
                        Debug.WriteLine("[StartRealtimeVideoProcessing] Warning: Failed to start streaming audio writer");
                    }
                }

                // ARCHITECTURAL FIX: Encoder handles VIDEO ONLY - never pass audio buffer to encoder
                appleEncoder.SetAudioBuffer(null);

                // Start audio capture if not already running (survives transition)
                if (_audioCapture != null && !_audioCapture.IsCapturing)
                {
                    await _audioCapture.StartAsync(AudioSampleRate, AudioChannels, AudioBitDepth, AudioDeviceIndex);
                    Debug.WriteLine("[StartRealtimeVideoProcessing] Audio capture started");
                }
                else if (_audioCapture?.IsCapturing == true)
                {
                    Debug.WriteLine("[StartRealtimeVideoProcessing] Audio capture already running (surviving transition)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] Audio init error: {ex}");
            }
        }

        // Set parent reference and pre-recording mode
        appleEncoder.ParentCamera = this;
        appleEncoder.IsPreRecordingMode = IsPreRecording;

        // Pass pre-allocated buffer if available (avoids lag spike on record start)
        if (IsPreRecording && _sharedPreRecordingBuffer != null)
        {
            appleEncoder.SharedPreRecordingBuffer = _sharedPreRecordingBuffer;
            Debug.WriteLine($"[StartRealtimeVideoProcessing] Using pre-allocated shared buffer (no allocation lag)");
        }

        Debug.WriteLine($"[StartRealtimeVideoProcessing] iOS encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Use encoder's processed frames for preview — FrameProcessor overlay is already baked in,
        // so PreviewProcessor can be skipped, eliminating duplicate GPU overlay work.
        UseRecordingFramesForPreview = true;
        
        if (MirrorRecordingToPreview)
        {
            _encoderPreviewInvalidateHandler = (s, e) =>
            {
                try
                {
                    SafeAction(() => UpdatePreview());
                }
                catch
                {
                }
            };
            appleEncoder.PreviewAvailable += _encoderPreviewInvalidateHandler;
        }

        // Output path (Documents) or pre-recording file path
        string outputPath;
        if (IsPreRecording)
        {
            outputPath = _preRecordingFilePath;
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.WriteLine("[StartRealtimeVideoProcessing] ERROR: Pre-recording file path not initialized");
                return null;
            }
            Debug.WriteLine($"[StartRealtimeVideoProcessing] iOS pre-recording to file: {outputPath}");
        }
        else
        {
            var documentsPath = GetAppVideoFolder(string.Empty);
            outputPath = Path.Combine(documentsPath, $"vid{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            Debug.WriteLine($"[StartRealtimeVideoProcessing] iOS recording to file: {outputPath}");
        }

        // Treat DeviceRotation 0/180 as portrait, 90/270 as landscape
        var rot = DeviceRotation % 360;
        bool portrait = (rot == 0 || rot == 180);

        // Use camera format if available; fallback to preview size or 1280x720
        var currentFormat = NativeControl?.GetCurrentVideoFormat();

        // on ios video format is defined for landscape width x height
        // so our width/height are swapped below to orient resulting video.

        var width = (int)PreviewSize.Height;
        var height = (int)PreviewSize.Width;

        if (currentFormat != null)
        {
            width = currentFormat.Height;
            height = currentFormat.Width;
        }

        var fps = currentFormat?.FrameRate > 0 ? currentFormat.FrameRate : 30;

        _diagEncWidth = (int)width;
        _diagEncHeight = (int)height;
        _diagBitrate = (long)Math.Max((long)width * height * 4, 2_000_000L);
        SetSourceFrameDimensions(width, height);

        // Pass locked rotation to encoder for proper video orientation metadata (iOS-specific)
        // Initialize the NEW encoder
        await appleEncoder.InitializeAsync(outputPath, width, height, fps, EnableAudioRecording, RecordingLockedRotation);

        // ✅ CRITICAL: If transitioning from pre-recording to live, set the duration offset BEFORE StartAsync
        // BUT ONLY if pre-recording file actually exists and has content (otherwise standalone live recording will be corrupted!)
        if (!IsPreRecording && _preRecordingDurationTracked > TimeSpan.Zero)
        {
            // Verify pre-recording file exists and has content before setting offset
            bool hasValidPreRecording = !string.IsNullOrEmpty(_preRecordingFilePath) &&
                                       File.Exists(_preRecordingFilePath) &&
                                       new FileInfo(_preRecordingFilePath).Length > 0;
            
            // If we are preserving encoder (Overlap Mode), the file might not be flushed yet!
            // In that case, we TRUST _preRecordingDurationTracked which should have been estimated from the old encoder.
           
            // GLOBAL TIMELINE: Don't set offset for overlap mode - timestamps are already continuous
            // Live file will be normalized to start at 0, concatenation handles sequencing
            if (preserveCurrentEncoder)
            {
                // Overlap mode: DON'T set offset - live will normalize its timestamps to 0
                Debug.WriteLine($"[StartRealtimeVideoProcessing] GLOBAL TIMELINE: No offset needed, live will normalize timestamps (pre-rec duration: {_preRecordingDurationTracked.TotalSeconds:F2}s)");
            }
            else if (hasValidPreRecording)
            {
                // Non-overlap mode with pre-recording: set offset for gap elimination
                appleEncoder.SetPreRecordingDuration(_preRecordingDurationTracked);
                Debug.WriteLine($"[StartRealtimeVideoProcessing] Set pre-recording duration offset: {_preRecordingDurationTracked.TotalSeconds:F2}s");
            }
            else if (_preRecordingDurationTracked > TimeSpan.Zero)
            {
                Debug.WriteLine($"[StartRealtimeVideoProcessing] WARNING: Pre-recording duration tracked ({_preRecordingDurationTracked.TotalSeconds:F2}s) but file is invalid/empty. NOT setting offset to avoid corrupting standalone live recording!");
                _preRecordingDurationTracked = TimeSpan.Zero; // Reset to avoid muxing attempt later
            }
        }

        // CRITICAL: In pre-recording mode, do NOT call StartAsync during initialization
        // Pre-recording mode should just buffer frames in memory without starting file writing
        // StartAsync will be called later when transitioning to live recording
        if (!IsPreRecording)
        {
            await appleEncoder.StartAsync();
            Debug.WriteLine($"[StartRealtimeVideoProcessing] StartAsync called for live/normal recording");
        }
        else
        {
            Debug.WriteLine($"[StartRealtimeVideoProcessing] Skipping StartAsync - pre-recording mode will buffer frames in memory");
        }
        
        // Progress reporting
        appleEncoder.ProgressReported += (sender, duration) =>
        {
            OnRecordingProgress(duration);
        };
        
        // Dispose previous encoder OR Preserve it
        if (_captureVideoEncoder is AppleVideoToolboxEncoder prevAppleEnc && _encoderPreviewInvalidateHandler != null)
        {
            prevAppleEnc.PreviewAvailable -= _encoderPreviewInvalidateHandler;
        }

        if (preserveCurrentEncoder)
        {
             oldEncoderToReturn = _captureVideoEncoder;
        }
        else
        {
             _captureVideoEncoder?.Dispose();
        }

        // 2. SWAP to new encoder - atomic assignment
        _captureVideoEncoder = appleEncoder;

        _capturePtsBaseTime = null;

        // GLOBAL TIMELINE: Only reset start time for fresh recordings, NOT when transitioning from pre-rec
        // This ensures live frames continue seamlessly from pre-rec timestamps
        if (!preserveCurrentEncoder || _preRecordingDurationTracked == TimeSpan.Zero)
        {
            _captureVideoStartTime = DateTime.Now;
        }
        // else: Keep original _captureVideoStartTime for seamless pre-rec → live transition

        // Diagnostics
        if (IsPreRecording || (!IsPreRecording && _preRecordingDurationTracked == TimeSpan.Zero))
        {
            _diagStartTime = DateTime.Now;
            _diagDroppedFrames = 0;
            _diagSubmittedFrames = 0;
            _diagLastSubmitMs = 0;
            ResetRecordingFps();
        }

        _targetFps = fps;

        // Start frame capture for Apple (drive encoder frames)
        if (NativeControl is NativeCamera nativeCam)
        {
            nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            nativeCam.RecordingFrameAvailable += OnRecordingFrameAvailable;
        }

        return oldEncoderToReturn;
    }

    /// <summary>
    /// Opens a file or displays a photo from assets-library URL
    /// </summary>
    /// <param name="imageFilePathOrUrl">File path or assets-library:// URL</param>
    public static void OpenFileInGallery(string imageFilePathOrUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageFilePathOrUrl))
            {
                System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] Invalid path/URL: {imageFilePathOrUrl}");
                return;
            }

            // Check if it's an assets-library URL
            if (imageFilePathOrUrl.StartsWith("assets-library://"))
            {
                var photosUrl = NSUrl.FromString("photos:albums");
                if (UIApplication.SharedApplication.CanOpenUrl(photosUrl))
                {
                    UIApplication.SharedApplication.OpenUrl(photosUrl, new UIApplicationOpenUrlOptions(), null);
                    return;
                }

                // Fallback to general photos-redirect scheme
                var fallbackUrl = NSUrl.FromString("photos-redirect://");
                if (UIApplication.SharedApplication.CanOpenUrl(fallbackUrl))
                {
                    UIApplication.SharedApplication.OpenUrl(fallbackUrl, new UIApplicationOpenUrlOptions(), null);
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SkiaCamera Apple] Photos app not available");
                }

                ShowPhotoFromAssetsLibrary(imageFilePathOrUrl);
            }
            else if (File.Exists(imageFilePathOrUrl))
            {
                // It's a regular file path
                var fileUrl = NSUrl.FromFilename(imageFilePathOrUrl);
                var documentController = UIDocumentInteractionController.FromUrl(fileUrl);
                var viewController = Platform.GetCurrentUIViewController();

                if (viewController != null)
                {
                    documentController.PresentPreview(true);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SkiaCamera Apple] Could not get current view controller");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SkiaCamera Apple] File not found and not a valid assets-library URL: {imageFilePathOrUrl}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] Error opening file/URL: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows photo from assets-library URL using PHImageManager
    /// </summary>
    /// <param name="assetsLibraryUrl">assets-library:// URL</param>
    public static void ShowPhotoFromAssetsLibrary(string assetsLibraryUrl)
    {
        try
        {
            // Extract the local identifier from the assets-library URL
            var idIndex = assetsLibraryUrl.IndexOf("id=");
            if (idIndex == -1)
            {
                System.Diagnostics.Debug.WriteLine("[SkiaCamera Apple] Invalid assets-library URL format");
                return;
            }

            var localIdentifier = assetsLibraryUrl.Substring(idIndex + 3);

            // Fetch the asset
            var fetchResult = PHAsset.FetchAssetsUsingLocalIdentifiers(new string[] { localIdentifier }, null);
            if (fetchResult.Count > 0)
            {
                var asset = fetchResult.FirstObject as PHAsset;
                ShowPhotoDirectly(asset);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SkiaCamera Apple] Asset not found for identifier: {localIdentifier}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SkiaCamera Apple] Error showing photo from assets-library: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows photo directly in full-screen viewer using PHAsset and PHImageManager
    /// </summary>
    /// <param name="asset">PHAsset to display</param>
    private static void ShowPhotoDirectly(PHAsset asset)
    {
        var viewController = Platform.GetCurrentUIViewController();
        if (viewController == null) return;

        // Create fullscreen photo viewer with zoom capability
        var photoViewController = new FullScreenPhotoViewController();

        // Make it truly fullscreen
        photoViewController.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;

        // Request the full-resolution image using PHImageManager
        var requestOptions = new PHImageRequestOptions
        {
            DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
            NetworkAccessAllowed = true,
            Synchronous = false
        };

        PHImageManager.DefaultManager.RequestImageForAsset(asset,
            PHImageManager.MaximumSize,
            PHImageContentMode.AspectFit,
            requestOptions,
            (image, info) =>
            {
                if (image != null)
                {
                    MainThread.BeginInvokeOnMainThread(() => { photoViewController.SetImage(image); });
                }
            });

        viewController.PresentViewController(photoViewController, true, null);
    }

    /// <summary>
    /// Shows video directly in full-screen viewer using PHAsset and PHImageManager
    /// </summary>
    /// <param name="assetId">Local identifier of the PHAsset to display</param>
    public static void PlayVideoDirectly(string assetId)
    {
        try
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            var fetchResult = PHAsset.FetchAssetsUsingLocalIdentifiers(new[] { assetId }, null);

            if (fetchResult.Count == 0)
            {
                Debug.WriteLine("[SkiaCamera Apple] Video not found for identifier: " + assetId);
                return;
            }

            var asset = fetchResult[0] as PHAsset;

            var viewController = Platform.GetCurrentUIViewController();
            if (viewController == null) return;

            var playerVC = new AVPlayerViewController();
            playerVC.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;

            var options = new PHVideoRequestOptions
            {
                NetworkAccessAllowed = true,
                DeliveryMode = PHVideoRequestOptionsDeliveryMode.HighQualityFormat
            };

            PHImageManager.DefaultManager.RequestAVAsset(asset, options, (avAsset, audioMix, info) =>
            {
                if (avAsset != null)
                {
                    var player = AVPlayer.FromPlayerItem(new AVPlayerItem(avAsset));
                    playerVC.Player = player;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        viewController.PresentViewController(playerVC, true, () =>
                        {
                            player.Play();
                        });
                    });
                }
                else
                {
                    Debug.WriteLine("[SkiaCamera Apple] Could not get AVAsset for video");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera Apple] Error playing video: {ex.Message}");
        }
    }


    /// <summary>
    /// Full-screen zoomable photo viewer controller
    /// </summary>
    private class FullScreenPhotoViewController : UIViewController, IUIScrollViewDelegate
    {
        private UIScrollView scrollView;
        private UIImageView imageView;
        private UIImage currentImage;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View.BackgroundColor = UIColor.Black;

            // Create scroll view for zooming
            scrollView = new UIScrollView
            {
                Frame = View.Bounds,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
                MinimumZoomScale = 1.0f,
                MaximumZoomScale = 3.0f,
                ZoomScale = 1.0f,
                ShowsHorizontalScrollIndicator = false,
                ShowsVerticalScrollIndicator = false,
                BackgroundColor = UIColor.Black
            };
            scrollView.Delegate = this;

            // Create image view
            imageView = new UIImageView
            {
                ContentMode = UIViewContentMode.ScaleAspectFit,
                BackgroundColor = UIColor.Clear
            };

            scrollView.AddSubview(imageView);
            View.AddSubview(scrollView);

            // Add tap gesture to close
            var tapGesture = new UITapGestureRecognizer(() => { DismissViewController(true, null); });
            View.AddGestureRecognizer(tapGesture);

            // Add double tap to zoom
            var doubleTapGesture = new UITapGestureRecognizer(HandleDoubleTap) { NumberOfTapsRequired = 2 };
            View.AddGestureRecognizer(doubleTapGesture);

            // Make sure single tap doesn't interfere with double tap
            tapGesture.RequireGestureRecognizerToFail(doubleTapGesture);
        }

        /// <summary>
        /// Sets the image to display
        /// </summary>
        /// <param name="image">UIImage to display</param>
        public void SetImage(UIImage image)
        {
            currentImage = image;
            imageView.Image = image;

            if (image != null)
            {
                // Size the image view to fit the image
                var imageSize = image.Size;
                var viewSize = View.Bounds.Size;

                // Calculate the scale to fit
                var scale = Math.Min(viewSize.Width / imageSize.Width, viewSize.Height / imageSize.Height);
                var scaledSize = new CoreGraphics.CGSize(imageSize.Width * scale, imageSize.Height * scale);

                imageView.Frame = new CoreGraphics.CGRect(0, 0, scaledSize.Width, scaledSize.Height);
                scrollView.ContentSize = scaledSize;

                // Center the image
                CenterImage();
            }
        }

        /// <summary>
        /// Centers the image in the scroll view
        /// </summary>
        private void CenterImage()
        {
            var scrollViewSize = scrollView.Bounds.Size;
            var imageViewSize = imageView.Frame.Size;

            var horizontalPadding = imageViewSize.Width < scrollViewSize.Width
                ? (scrollViewSize.Width - imageViewSize.Width) / 2
                : 0;
            var verticalPadding = imageViewSize.Height < scrollViewSize.Height
                ? (scrollViewSize.Height - imageViewSize.Height) / 2
                : 0;

            scrollView.ContentInset =
                new UIEdgeInsets(verticalPadding, horizontalPadding, verticalPadding, horizontalPadding);
        }

        /// <summary>
        /// Handles double tap to zoom in/out
        /// </summary>
        /// <param name="gesture">Tap gesture recognizer</param>
        private void HandleDoubleTap(UITapGestureRecognizer gesture)
        {
            if (scrollView.ZoomScale > scrollView.MinimumZoomScale)
            {
                // Zoom out
                scrollView.SetZoomScale(scrollView.MinimumZoomScale, true);
            }
            else
            {
                // Zoom in to double tap location
                var tapPoint = gesture.LocationInView(imageView);
                var newZoomScale = scrollView.MaximumZoomScale;
                var size = new CoreGraphics.CGSize(scrollView.Frame.Size.Width / newZoomScale,
                    scrollView.Frame.Size.Height / newZoomScale);
                var origin = new CoreGraphics.CGPoint(tapPoint.X - size.Width / 2,
                    tapPoint.Y - size.Height / 2);
                scrollView.ZoomToRect(new CoreGraphics.CGRect(origin, size), true);
            }
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            scrollView.Frame = View.Bounds;
            CenterImage();
        }

        // UIScrollViewDelegate methods
        [Export("viewForZoomingInScrollView:")]
        public UIView ViewForZoomingInScrollView(UIScrollView scrollView)
        {
            return imageView;
        }

        [Export("scrollViewDidZoom:")]
        public void DidZoom(UIScrollView scrollView)
        {
            CenterImage();
        }
    }

    public virtual Metadata CreateMetadata()
    {
        return new Metadata()
        {
#if IOS
            Software = "SkiaCamera iOS",
#elif MACCATALYST
            Software = "SkiaCamera MacCatalyst",
#endif
            Vendor = UIDevice.CurrentDevice.Model,
            Model = UIDevice.CurrentDevice.Name,
        };
    }

    protected virtual void CreateNative()
    {
        if (!IsOn || NativeControl != null)
            return;

        NativeControl = new NativeCamera(this);

        NativeControl?.ApplyDeviceOrientation(DeviceRotation);
    }

    /// <summary>
    /// Returns the device types used for camera discovery on this iOS version.
    /// Must be consistent everywhere we enumerate or look up cameras.
    /// </summary>
    internal static AVFoundation.AVCaptureDeviceType[] GetDiscoveryDeviceTypes()
    {
        var deviceTypes = new List<AVFoundation.AVCaptureDeviceType>
        {
            AVFoundation.AVCaptureDeviceType.BuiltInWideAngleCamera,
            AVFoundation.AVCaptureDeviceType.BuiltInTelephotoCamera,
            AVFoundation.AVCaptureDeviceType.BuiltInUltraWideCamera
        };

        if (UIKit.UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            deviceTypes.Add(AVFoundation.AVCaptureDeviceType.BuiltInDualCamera);
            deviceTypes.Add(AVFoundation.AVCaptureDeviceType.BuiltInTripleCamera);
            deviceTypes.Add(AVFoundation.AVCaptureDeviceType.BuiltInTrueDepthCamera);
            deviceTypes.Add(AVFoundation.AVCaptureDeviceType.BuiltInLiDarDepthCamera);
            deviceTypes.Add(AVFoundation.AVCaptureDeviceType.BuiltInDualWideCamera);
        }

        return deviceTypes.ToArray();
    }

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform(bool refresh)
    {
        var cameras = new List<CameraInfo>();

        try
        {
            var discoverySession = AVFoundation.AVCaptureDeviceDiscoverySession.Create(
                GetDiscoveryDeviceTypes(),
                AVFoundation.AVMediaTypes.Video,
                AVFoundation.AVCaptureDevicePosition.Unspecified);

            var devices = discoverySession.Devices;

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                var position = device.Position switch
                {
                    AVFoundation.AVCaptureDevicePosition.Front => CameraPosition.Selfie,
                    AVFoundation.AVCaptureDevicePosition.Back => CameraPosition.Default,
                    _ => CameraPosition.Default
                };

                var supportsVideo = device.SupportsAVCaptureSessionPreset(AVFoundation.AVCaptureSession.PresetHigh);
                var supportsPhoto = device.SupportsAVCaptureSessionPreset(AVFoundation.AVCaptureSession.PresetPhoto);

                cameras.Add(new CameraInfo
                {
                    Id = device.UniqueID,
                    Name = device.LocalizedName,
                    Position = position,
                    Index = i,
                    HasFlash = device.HasFlash,
                    SupportsVideo = supportsVideo,
                    SupportsPhoto = supportsPhoto
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraApple] Error enumerating cameras: {ex.Message}");
        }

        return cameras;
    }

    protected async Task<List<CaptureFormat>> GetAvailableCaptureFormatsPlatform()
    {
        var formats = new List<CaptureFormat>();

        try
        {
            if (NativeControl is NativeCamera native)
            {
                formats = native.StillFormats;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraApple] Error getting capture formats: {ex.Message}");
        }

        return formats;
    }

    protected async Task<List<VideoFormat>> GetAvailableVideoFormatsPlatform()
    {
        var formats = new List<VideoFormat>();

        try
        {
            if (NativeControl is NativeCamera native)
            {
                // Get formats from the native camera's predefined formats method
                formats = native.GetPredefinedVideoFormats();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraApple] Error getting video formats: {ex.Message}");
        }

        return formats;
    }

    /// <summary>
    /// Updates preview format to match current capture format aspect ratio.
    /// iOS implementation: Reselects device format since iOS uses single format for both preview and capture.
    /// </summary>
    protected virtual void UpdatePreviewFormatForAspectRatio()
    {
        if (NativeControl is NativeCamera appleCamera)
        {
            Console.WriteLine("[SkiaCameraApple] Updating preview format for aspect ratio match");

            // iOS uses a single AVCaptureDeviceFormat for both preview and capture
            // We need to reselect the optimal format based on new capture quality settings
            Task.Run(async () =>
            {
                try
                {
                    // Trigger format reselection by restarting camera session
                    appleCamera.Stop();

                    // Small delay to ensure cleanup
                    await Task.Delay(100);

                    // Restart - this will call SelectOptimalFormat() with new settings
                    appleCamera.Start();

                    Console.WriteLine("[SkiaCameraApple] Camera session restarted for format change");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SkiaCameraApple] Error updating preview format: {ex.Message}");
                }
            });
        }
    }

    /// <summary>
    /// Call on UI thread only. Called by CheckPermissions.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> RequestPermissions()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

        if (status == PermissionStatus.Granted && this.CaptureMode == CaptureModeType.Video)
        {
            status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
        }

        if (status == PermissionStatus.Granted && this.CaptureMode == CaptureModeType.Video && this.EnableAudioRecording)
        {
            var s = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
            if (s == AVAuthorizationStatus.NotDetermined)
            {
                var granted = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Audio);
                if (!granted)
                {
                    status = PermissionStatus.Denied;
                }
            }
            else
            {
                status = PermissionStatus.Granted;
            }
        }

        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Request Photos library access up front so saving does not trigger a late system prompt.
    /// Call on UI thread only.
    /// </summary>
    public static async Task<bool> RequestGalleryPermissions()
    {
        // Photos AddOnly (iOS 14+) returns Authorized or Limited when the user allows adding media
        var authStatus = await Photos.PHPhotoLibrary.RequestAuthorizationAsync(Photos.PHAccessLevel.ReadWrite);
        return authStatus == Photos.PHAuthorizationStatus.Authorized ||
               authStatus == Photos.PHAuthorizationStatus.Limited;
    }


    //public SKBitmap GetPreviewBitmap()
    //{
    //    var preview = NativeControl?.GetPreviewImage();
    //    if (preview?.Image != null)
    //    {
    //        return SKBitmap.FromImage(preview.Image);
    //    }
    //    return null;
    //}

    /// <summary>
    /// Mux pre-recorded and live video files using AVAssetComposition.
    /// Optionally includes a separate audio file that spans the entire recording session.
    /// </summary>
    /// <param name="preRecordedPath">Path to pre-recorded video file (video only)</param>
    /// <param name="liveRecordingPath">Path to live recording video file (video only)</param>
    /// <param name="outputPath">Output file path for muxed result</param>
    /// <param name="audioFilePath">Optional: Path to audio file (M4A) to include in output</param>
    private async Task<string> MuxVideosInternal(string preRecordedPath, string liveRecordingPath, string outputPath, string audioFilePath = null, string preRecAudioFilePath = null)
    {
        try
        {
            // If pre-recorded is raw H.264 files, convert to MP4 first
            if (preRecordedPath.EndsWith(".h264"))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MuxVideosApple] Pre-recorded file is H.264, converting to MP4 first");
                preRecordedPath = await ConvertH264ToMp4Async(preRecordedPath, outputPath + ".prec.mp4");
                if (string.IsNullOrEmpty(preRecordedPath))
                {
                    throw new InvalidOperationException("Failed to convert H.264 to MP4");
                }
            }

            // Log input/output file paths for debugging
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Input files:");
            System.Diagnostics.Debug.WriteLine(
                $"  Pre-recorded: {preRecordedPath} (exists: {File.Exists(preRecordedPath)})");
            System.Diagnostics.Debug.WriteLine(
                $"  Live recording: {liveRecordingPath} (exists: {File.Exists(liveRecordingPath)})");
            System.Diagnostics.Debug.WriteLine(
                $"  Audio file: {audioFilePath ?? "none"} (exists: {audioFilePath != null && File.Exists(audioFilePath)})");
            System.Diagnostics.Debug.WriteLine($"  Output: {outputPath}");

            using (var preAsset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(preRecordedPath)))
            using (var liveAsset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(liveRecordingPath)))
            {
                if (preAsset == null || liveAsset == null)
                    throw new InvalidOperationException("Failed to load video assets");

                using var composition = new AVFoundation.AVMutableComposition();
                var videoTrack = composition.AddMutableTrack(AVMediaTypes.Video.GetConstant(), 0);

                if (videoTrack == null)
                    throw new InvalidOperationException("Failed to create video track");

                var currentTime = CoreMedia.CMTime.Zero;

                // Add pre-recorded video
                var preTracks = preAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant());
                if (preTracks != null && preTracks.Length > 0)
                {
                    var preTrack = preTracks[0];
                    var preRange =
                        new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = preAsset.Duration };
                    videoTrack.InsertTimeRange(preRange, preTrack, currentTime, out var error);
                    if (error != null)
                        throw new InvalidOperationException(
                            $"Failed to insert pre-recorded track: {error.LocalizedDescription}");

                    currentTime = CoreMedia.CMTime.Add(currentTime, preAsset.Duration);
                }

                // Add live recording video
                var liveTracks = liveAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant());
                AVFoundation.AVAssetTrack liveTrack = null;
                if (liveTracks != null && liveTracks.Length > 0)
                {
                    liveTrack = liveTracks[0];
                    // Variant 2 (sync-sample-aware splice): start the live segment at its first sync sample
                    // to avoid decode artifacts when the seam lands on a non-sync sample.
                    // This is codec-agnostic (H.264/HEVC) because it relies on sync-sample metadata.
                    var liveSyncStart = FindFirstSyncSampleTime(liveAsset, liveTrack);
                    var liveStartSeconds = Math.Max(0, liveSyncStart.Seconds);
                    var totalLiveSeconds = Math.Max(0, liveAsset.Duration.Seconds);
                    var liveDurationSeconds = Math.Max(0, totalLiveSeconds - liveStartSeconds);

                    if (liveStartSeconds > 0.0 && liveDurationSeconds > 0.0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MuxVideosApple] Variant2: Trimming live video start to first sync sample at {liveStartSeconds:F3}s (duration {liveDurationSeconds:F3}s)");
                    }

                    var liveRange = new CoreMedia.CMTimeRange
                    {
                        Start = CoreMedia.CMTime.FromSeconds(liveStartSeconds, 600),
                        Duration = CoreMedia.CMTime.FromSeconds(liveDurationSeconds, 600)
                    };
                    videoTrack.InsertTimeRange(liveRange, liveTrack, currentTime, out var error);
                    if (error != null)
                        throw new InvalidOperationException(
                            $"Failed to insert live track: {error.LocalizedDescription}");
                }

                // ========================= AUDIO TRACK HANDLING =========================
                // OPTIMIZED: Add pre-rec audio + live audio directly without concatenation step
                // This saves one AVAssetExportSession call (faster muxing)

                bool hasAnyAudio = false;
                var preRecVideoDuration = preAsset.Duration;  // Where live audio should start

                // Add pre-rec audio at the start (time 0)
                if (!string.IsNullOrEmpty(preRecAudioFilePath) && File.Exists(preRecAudioFilePath))
                {
                    using var preRecAudioAsset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(preRecAudioFilePath));
                    var preRecAudioTracks = preRecAudioAsset?.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());

                    if (preRecAudioTracks != null && preRecAudioTracks.Length > 0)
                    {
                        var audioTrack = composition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0);
                        if (audioTrack != null)
                        {
                            var audioDuration = preRecAudioAsset.Duration;
                            // Don't exceed pre-rec video duration
                            var insertDuration = audioDuration.Seconds <= preRecVideoDuration.Seconds ? audioDuration : preRecVideoDuration;
                            var audioRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = insertDuration };
                            audioTrack.InsertTimeRange(audioRange, preRecAudioTracks[0], CoreMedia.CMTime.Zero, out var audioError);

                            if (audioError != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to insert pre-rec audio: {audioError.LocalizedDescription}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Added pre-rec audio at 0s ({insertDuration.Seconds:F2}s)");
                                hasAnyAudio = true;
                            }
                        }
                    }
                }

                // Add live audio starting after pre-rec video
                if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
                {
                    using var liveAudioAsset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(audioFilePath));
                    var liveAudioTracks = liveAudioAsset?.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());

                    if (liveAudioTracks != null && liveAudioTracks.Length > 0)
                    {
                        // Get or create audio track
                        var existingAudioTracks = composition.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());
                        var audioTrack = existingAudioTracks?.Length > 0 ? (AVMutableCompositionTrack)existingAudioTracks[0]
                            : composition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0);

                        if (audioTrack != null)
                        {
                            var liveVideoDuration = liveAsset.Duration;
                            var audioDuration = liveAudioAsset.Duration;
                            // Variant 2: If we trimmed the live video start to a sync sample, trim live audio by the same amount
                            // so A/V remains aligned in the final composition.
                            double liveStartSeconds = 0;
                            if (liveTrack != null)
                            {
                                var liveSyncStart = FindFirstSyncSampleTime(liveAsset, liveTrack);
                                liveStartSeconds = Math.Max(0, liveSyncStart.Seconds);
                            }

                            var trimmedVideoSeconds = Math.Max(0, liveVideoDuration.Seconds - liveStartSeconds);
                            var trimmedAudioSeconds = Math.Max(0, audioDuration.Seconds - liveStartSeconds);
                            var insertSeconds = Math.Min(trimmedAudioSeconds, trimmedVideoSeconds);

                            var audioRange = new CoreMedia.CMTimeRange
                            {
                                Start = CoreMedia.CMTime.FromSeconds(liveStartSeconds, 600),
                                Duration = CoreMedia.CMTime.FromSeconds(insertSeconds, 600)
                            };

                            // Insert at the end of pre-rec video
                            audioTrack.InsertTimeRange(audioRange, liveAudioTracks[0], preRecVideoDuration, out var audioError);

                            if (audioError != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to insert live audio: {audioError.LocalizedDescription}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Added live audio at {preRecVideoDuration.Seconds:F2}s ({insertSeconds:F2}s)");
                                hasAnyAudio = true;
                            }
                        }
                    }
                }

                // Legacy single audio file support (backward compatibility)
                bool hasExternalAudio = hasAnyAudio;

                // Fallback: Check if source video files have audio tracks (legacy behavior for backward compatibility)
                if (!hasExternalAudio)
                {
                    var preAudioTracks = preAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());
                    var liveAudioTracks = liveAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());

                    bool hasPreAudio = preAudioTracks != null && preAudioTracks.Length > 0;
                    bool hasLiveAudio = liveAudioTracks != null && liveAudioTracks.Length > 0;

                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Source video audio tracks: Pre-recording={hasPreAudio}, Live={hasLiveAudio}");

                    if (hasPreAudio || hasLiveAudio)
                    {
                        var audioTrack = composition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0);
                        if (audioTrack != null)
                        {
                            var audioCurrentTime = CoreMedia.CMTime.Zero;

                            // Add pre-recorded audio
                            if (hasPreAudio)
                            {
                                var preAudioTrack = preAudioTracks[0];
                                var preAudioRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = preAsset.Duration };
                                audioTrack.InsertTimeRange(preAudioRange, preAudioTrack, audioCurrentTime, out var audioError);
                                if (audioError != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to insert pre-recorded audio: {audioError.LocalizedDescription}");
                                }
                                else
                                {
                                    audioCurrentTime = CoreMedia.CMTime.Add(audioCurrentTime, preAsset.Duration);
                                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Added pre-recording audio track ({preAsset.Duration.Seconds:F2}s)");
                                }
                            }

                            // Add live recording audio
                            if (hasLiveAudio)
                            {
                                var liveAudioTrack = liveAudioTracks[0];
                                // Variant 2: Keep embedded live audio aligned with trimmed live video start.
                                double liveStartSeconds = 0;
                                if (liveTrack != null)
                                {
                                    var liveSyncStart = FindFirstSyncSampleTime(liveAsset, liveTrack);
                                    liveStartSeconds = Math.Max(0, liveSyncStart.Seconds);
                                }
                                var liveDurationSeconds = Math.Max(0, liveAsset.Duration.Seconds - liveStartSeconds);
                                var liveAudioRange = new CoreMedia.CMTimeRange
                                {
                                    Start = CoreMedia.CMTime.FromSeconds(liveStartSeconds, 600),
                                    Duration = CoreMedia.CMTime.FromSeconds(liveDurationSeconds, 600)
                                };
                                audioTrack.InsertTimeRange(liveAudioRange, liveAudioTrack, audioCurrentTime, out var audioError);
                                if (audioError != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to insert live audio: {audioError.LocalizedDescription}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Added live audio track ({liveAsset.Duration.Seconds:F2}s)");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to create audio track in composition");
                        }
                    }
                }

                // ========================================================================

                // CRITICAL: Copy transform from source track to composition to preserve orientation
                // Both source files have correct transform, so copy from live track (or pre-track if live is null)
                CoreGraphics.CGAffineTransform compositionTransform = CoreGraphics.CGAffineTransform.MakeIdentity();
                CoreGraphics.CGSize sourceSize = CoreGraphics.CGSize.Empty;

                if (liveTrack != null)
                {
                    compositionTransform = liveTrack.PreferredTransform;
                    sourceSize = liveTrack.NaturalSize;
                    System.Diagnostics.Debug.WriteLine(
                        $"[MuxVideosApple] Live track: {sourceSize.Width}x{sourceSize.Height}, transform: {compositionTransform}");
                }
                else if (preTracks != null && preTracks.Length > 0)
                {
                    compositionTransform = preTracks[0].PreferredTransform;
                    sourceSize = preTracks[0].NaturalSize;
                    System.Diagnostics.Debug.WriteLine(
                        $"[MuxVideosApple] Pre-recording track: {sourceSize.Width}x{sourceSize.Height}, transform: {compositionTransform}");
                }

                // PASSTHROUGH MODE: No re-encoding, just container manipulation (FAST!)
                // Orientation is preserved via track's PreferredTransform metadata
                // The source videos already have correct transform set by the encoder
                videoTrack.PreferredTransform = compositionTransform;

                System.Diagnostics.Debug.WriteLine(
                    $"[MuxVideosApple] Passthrough mode - preserving source transform: {compositionTransform}");

                // Export composition to file
                // CRITICAL: AVAssetExportSession fails if output file already exists
                if (File.Exists(outputPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Deleting existing output file: {outputPath}");
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MuxVideosApple] Warning: Failed to delete existing output file: {ex.Message}");
                    }
                }

                var outputUrl = Foundation.NSUrl.FromFilename(outputPath);
                var exporter =
                    new AVFoundation.AVAssetExportSession(composition,
                        AVFoundation.AVAssetExportSessionPreset.Passthrough)
                    {
                        OutputUrl = outputUrl,
                        OutputFileType = AVFoundation.AVFileTypes.Mpeg4.GetConstant(),
                        ShouldOptimizeForNetworkUse = ShouldOptimizeForNetworkUse
                        // No VideoComposition - passthrough preserves original encoding
                    };

                var tcs = new TaskCompletionSource<string>();

                exporter.ExportAsynchronously(() =>
                {
                    if (exporter.Status == AVFoundation.AVAssetExportSessionStatus.Completed)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Mux successful: {outputPath}");
                        tcs.TrySetResult(outputPath);
                    }
                    else if (exporter.Status == AVFoundation.AVAssetExportSessionStatus.Failed)
                    {
                        var error = exporter.Error?.LocalizedDescription ?? "Unknown error";
                        System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Export failed: {error}");
                        tcs.TrySetException(new InvalidOperationException($"Export failed: {error}"));
                    }
                    else if (exporter.Status == AVFoundation.AVAssetExportSessionStatus.Cancelled)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Export cancelled");
                        tcs.TrySetCanceled();
                    }

                    exporter.Dispose();
                });

                var result = await tcs.Task;

                // Small delay to ensure file is fully flushed to disk before returning
                await Task.Delay(50);

                return result;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Finds the first sync sample (keyframe) presentation timestamp in a compressed track.
    /// Returns CMTime.Zero if not found quickly or if reader setup fails.
    ///
    /// This is used by Variant 2 muxing: start the inserted live segment on a sync sample
    /// so decoding does not start mid-GOP (which can cause “old frames” near the splice).
    /// </summary>
    private static CoreMedia.CMTime FindFirstSyncSampleTime(AVFoundation.AVAsset asset, AVFoundation.AVAssetTrack videoTrack, double maxSearchSeconds = 2.0)
    {
        if (asset == null || videoTrack == null || maxSearchSeconds <= 0)
            return CoreMedia.CMTime.Zero;

        try
        {
            using var reader = new AVFoundation.AVAssetReader(asset, out var error);
            if (reader == null || error != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Variant2: AVAssetReader creation failed: {error?.LocalizedDescription}");
                return CoreMedia.CMTime.Zero;
            }

            // Null output settings => compressed samples (no decode) when supported.
            // Cast null to NSDictionary to disambiguate overloads on some targets (e.g., MacCatalyst).
            using var output = new AVFoundation.AVAssetReaderTrackOutput(videoTrack, (Foundation.NSDictionary)null);

            try
            {
                output.AlwaysCopiesSampleData = false;
            }
            catch
            {
                // Some OS/bindings may not expose this; safe to ignore.
            }

            if (!reader.CanAddOutput(output))
                return CoreMedia.CMTime.Zero;

            reader.AddOutput(output);

            if (!reader.StartReading())
                return CoreMedia.CMTime.Zero;

            while (true)
            {
                using var sample = output.CopyNextSampleBuffer();
                if (sample == null)
                    break;

                var pts = sample.PresentationTimeStamp;
                if (pts.Seconds > maxSearchSeconds)
                    break;

                if (IsSyncSample(sample))
                    return pts;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Variant2: Error scanning sync samples: {ex.Message}");
        }

        return CoreMedia.CMTime.Zero;
    }

    private static bool IsSyncSample(CoreMedia.CMSampleBuffer sample)
    {
        try
        {
            // kCMSampleAttachmentKey_NotSync is CFString("NotSync")
            // If NotSync is true => NOT a sync sample.
            // If attachments are missing => treat as sync.
            var attachments = GetSampleAttachmentsArray(sample, createIfNecessary: false);
            if (attachments == null || attachments.Count == 0)
                return true;

            var dict = attachments.GetItem<Foundation.NSDictionary>(0);
            if (dict == null)
                return true;

            var notSyncKey = new Foundation.NSString("NotSync");
            if (!dict.ContainsKey(notSyncKey))
                return true;

            var value = dict[notSyncKey];
            if (value is Foundation.NSNumber n)
                return !n.BoolValue;

            return true;
        }
        catch
        {
            return true;
        }
    }

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern IntPtr CMSampleBufferGetSampleAttachmentsArray(IntPtr sampleBuffer, bool createIfNecessary);

    private static Foundation.NSArray GetSampleAttachmentsArray(CoreMedia.CMSampleBuffer sample, bool createIfNecessary)
    {
        if (sample == null)
            return null;

        var ptr = CMSampleBufferGetSampleAttachmentsArray(sample.Handle, createIfNecessary);
        if (ptr == IntPtr.Zero)
            return null;

        // Returned array is owned by the sample buffer; do not dispose.
        return ObjCRuntime.Runtime.GetNSObject<Foundation.NSArray>(ptr);
    }

    /// <summary>
    /// Converts H.264 raw file to MP4 container using AVAssetWriter
    /// </summary>
    private async Task<string> ConvertH264ToMp4Async(string h264FilePath, string outputMp4Path)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MuxVideosApple] Converting H.264 to MP4: {h264FilePath} → {outputMp4Path}");

            // Delete output if exists
            if (File.Exists(outputMp4Path))
            {
                try
                {
                    File.Delete(outputMp4Path);
                }
                catch
                {
                }
            }

            // Create AVAssetWriter for MP4 output
            var url = Foundation.NSUrl.FromFilename(outputMp4Path);
            var writer = new AVFoundation.AVAssetWriter(url, "public.mpeg-4", out var err);

            if (writer == null || err != null)
                throw new InvalidOperationException($"Failed to create AVAssetWriter: {err?.LocalizedDescription}");

            // Configure video output
            var videoSettings = new AVFoundation.AVVideoSettingsCompressed
            {
                Codec = AVFoundation.AVVideoCodec.H264,
                Width = 1920, // Will be overridden by source
                Height = 1080 // Will be overridden by source
            };

            var videoInput = new AVFoundation.AVAssetWriterInput(AVMediaTypes.Video.GetConstant(), videoSettings)
            {
                ExpectsMediaDataInRealTime = false
            };

            if (!writer.CanAddInput(videoInput))
                throw new InvalidOperationException("Cannot add video input to writer");

            writer.AddInput(videoInput);

            // Start writing
            if (!writer.StartWriting())
                throw new InvalidOperationException("Failed to start writing");

            writer.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);

            // Read H.264 file and write to MP4
            byte[] h264Data = File.ReadAllBytes(h264FilePath);
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Read {h264Data.Length} bytes from H.264 file");

            // Note: This is a simplified approach. A full implementation would need to parse H.264 NAL units
            // and create proper CMSampleBuffers. For now, log that conversion was attempted.
            System.Diagnostics.Debug.WriteLine(
                $"[MuxVideosApple] H.264 conversion: Note - Full NAL unit parsing not yet implemented. Using pre-recorded MP4 directly if available.");

            // For now, just copy the file if it exists as MP4
            // In production, you'd parse H.264 and reconstruct CMSampleBuffers
            writer.FinishWriting();
            writer.Dispose();
            videoInput.Dispose();

            // Return the output path
            return outputMp4Path;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] H.264 conversion error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Combines two H.264 files into a single MP4 container
    /// </summary>
    private async Task<string> CombineH264FilesToMp4Async(string fileA, string fileB, string outputMp4Path)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Combining H.264 files to MP4:");
            System.Diagnostics.Debug.WriteLine($"  FileA: {fileA}");
            System.Diagnostics.Debug.WriteLine($"  FileB: {fileB}");
            System.Diagnostics.Debug.WriteLine($"  Output: {outputMp4Path}");

            // Delete output if exists
            if (File.Exists(outputMp4Path))
            {
                try
                {
                    File.Delete(outputMp4Path);
                }
                catch
                {
                }
            }

            // Combine H.264 files into single byte array
            byte[] combinedH264 = new byte[0];

            if (File.Exists(fileA))
            {
                byte[] dataA = File.ReadAllBytes(fileA);
                Array.Resize(ref combinedH264, combinedH264.Length + dataA.Length);
                Array.Copy(dataA, 0, combinedH264, combinedH264.Length - dataA.Length, dataA.Length);
                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] FileA: {dataA.Length} bytes");
            }

            if (File.Exists(fileB))
            {
                byte[] dataB = File.ReadAllBytes(fileB);
                Array.Resize(ref combinedH264, combinedH264.Length + dataB.Length);
                Array.Copy(dataB, 0, combinedH264, combinedH264.Length - dataB.Length, dataB.Length);
                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] FileB: {dataB.Length} bytes");
            }

            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Combined H.264 size: {combinedH264.Length} bytes");

            // Write combined H.264 to temporary file
            string tempH264Path = outputMp4Path + ".h264";
            File.WriteAllBytes(tempH264Path, combinedH264);

            // Now wrap the combined H.264 in an MP4 container using AVAsset
            // Note: This is a workaround. In production, you'd use ffmpeg or similar
            // For now, just return the path to the combined H.264
            // The muxing code will need to handle H.264 files directly

            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Combined H.264 saved to: {tempH264Path}");

            return tempH264Path; // Return the combined H.264 file path
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Error combining H.264 files: {ex.Message}");
            throw;
        }
    }



    private async Task StopRealtimeVideoProcessingInternal()
    {
        ICaptureVideoEncoder encoder = null;
        string tempAudioFilePath = null;

        // Set busy while muxing - prevents user actions during file processing
        IsBusy = true;

        try
        {
            // CRITICAL: Stop frame capture FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            if (NativeControl is NativeCamera nativeCam)
            {
                nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            }

            // Give any in-flight CaptureFrame calls time to complete
            // Wait for in-flight frame to complete before stopping encoder
            // Prevents race condition where CaptureFrameCore still uses encoder being disposed
            int retries = 0;
            while (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 0, 0) != 0 && retries < 50)
            {
                await Task.Delay(10);
                retries++;
            }

            // Release cached GPU resources before encoder disposal
            _cachedZeroCopyImage?.Dispose();
            _cachedZeroCopyImage = null;
            _cachedZeroCopyTextureHandle = IntPtr.Zero;
            _cachedZeroCopyContext = null;

            // OOM-SAFE AUDIO HANDLING:
            // 1. Stop live audio writer (instant - audio already on disk)
            // 2. Write pre-rec audio to file if present (fast - only ~5 sec max)
            // 3. Concatenate pre-rec + live audio files if needed
            string liveAudioFilePath = null;
            string preRecAudioFilePath = null;
            var stopwatchTotal = System.Diagnostics.Stopwatch.StartNew();
            var stopwatchStep = new System.Diagnostics.Stopwatch();

            if (EnableAudioRecording)
            {
                // Stop the streaming audio writer and get its file path (instant - already on disk)
                stopwatchStep.Restart();
                liveAudioFilePath = await StopLiveAudioWriterAsync();
                stopwatchStep.Stop();
                if (!string.IsNullOrEmpty(liveAudioFilePath))
                {
                    var liveAudioSize = File.Exists(liveAudioFilePath) ? new FileInfo(liveAudioFilePath).Length / 1024.0 : 0;
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] TIMING: StopLiveAudioWriter took {stopwatchStep.ElapsedMilliseconds}ms, file: {liveAudioSize:F1} KB");
                }

                // Write pre-recorded audio samples to file if present (small, ~5 sec max)
                if (_preRecordedAudioSamples != null && _preRecordedAudioSamples.Length > 0)
                {
                    stopwatchStep.Restart();
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Writing {_preRecordedAudioSamples.Length} pre-rec audio samples");
                    preRecAudioFilePath = Path.Combine(
                        Path.GetTempPath(),
                        $"prerec_audio_{Guid.NewGuid():N}.m4a"
                    );
                    preRecAudioFilePath = await WriteAudioSamplesToM4AAsync(_preRecordedAudioSamples, preRecAudioFilePath);
                    stopwatchStep.Stop();
                    var preRecAudioSize = File.Exists(preRecAudioFilePath) ? new FileInfo(preRecAudioFilePath).Length / 1024.0 : 0;
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] TIMING: WritePreRecAudio took {stopwatchStep.ElapsedMilliseconds}ms, file: {preRecAudioSize:F1} KB");
                    _preRecordedAudioSamples = null;  // Clean up
                }

                // Clear any remaining buffer
                _audioBuffer = null;
            }

            // Stop audio capture
            if (_audioCapture != null)
            {
                Debug.WriteLine($"[StopRealtimeVideoProcessing] Stopping audio capture");
                _audioCapture.SampleAvailable -= OnAudioSampleAvailable;
                await _audioCapture.StopAsync();
                _audioCapture.Dispose();
                _audioCapture = null;
            }

            // CRITICAL: Unsubscribe event handlers before disposal to prevent memory leaks
            if (_captureVideoEncoder is AppleVideoToolboxEncoder appleEncToStop && _encoderPreviewInvalidateHandler != null)
            {
                appleEncToStop.PreviewAvailable -= _encoderPreviewInvalidateHandler;
            }

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;

            // Stop encoder and get result
            stopwatchStep.Restart();
            CapturedVideo capturedVideo = await encoder?.StopAsync();
            stopwatchStep.Stop();
            Debug.WriteLine($"[StopRealtimeVideoProcessing] TIMING: StopEncoder took {stopwatchStep.ElapsedMilliseconds}ms");

            // Wait for overlap background flush if pending
            if (_preRecFlushTask != null)
            {
                Debug.WriteLine("[StopRealtimeVideoProcessing] Waiting for background pre-recording flush to complete...");
                await _preRecFlushTask;
                _preRecFlushTask = null;
                Debug.WriteLine("[StopRealtimeVideoProcessing] Pre-recording flush completed.");
            }

            // OPTIMIZED: Skip audio concatenation - pass both files directly to MuxVideosInternal
            // This saves one AVAssetExportSession call (much faster)
            Debug.WriteLine($"[StopRealtimeVideoProcessing] Audio files: preRec={preRecAudioFilePath ?? "none"}, live={liveAudioFilePath ?? "none"}");

            // If we have both pre-recorded and live recording, mux them together
            if (capturedVideo != null && !string.IsNullOrEmpty(_preRecordingFilePath) &&
                File.Exists(_preRecordingFilePath))
            {
                Debug.WriteLine($"[StopRealtimeVideoProcessing] Muxing pre-recorded file with live recording");
                try
                {
                    // Save original live recording path before overwriting capturedVideo
                    string originalLiveRecordingPath = capturedVideo.FilePath;

                    // Mux pre-recorded file + live file + BOTH audio files into final output
                    // Audio files are added directly without concatenation (faster)
                    string muxedOutputPath = Path.Combine(
                        Path.GetDirectoryName(originalLiveRecordingPath),
                        $"muxed_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4"
                    );
                    stopwatchStep.Restart();
                    // Pass live audio as audioFilePath, pre-rec audio as preRecAudioFilePath
                    string finalOutputPath = await MuxVideosInternal(_preRecordingFilePath, originalLiveRecordingPath, muxedOutputPath, liveAudioFilePath, preRecAudioFilePath);
                    stopwatchStep.Stop();
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] TIMING: MuxVideos took {stopwatchStep.ElapsedMilliseconds}ms");
                    if (!string.IsNullOrEmpty(finalOutputPath) && File.Exists(finalOutputPath))
                    {
                        // Update captured video to point to muxed file
                        var muxedInfo = new FileInfo(finalOutputPath);
                        capturedVideo = new CapturedVideo
                        {
                            FilePath = finalOutputPath,
                            FileSizeBytes = muxedInfo.Length,
                            Duration = capturedVideo.Duration,
                            Time = capturedVideo.Time
                        };

                        // Delete temp live recording file (NOT the muxed file!)
                        try
                        {
                            File.Delete(originalLiveRecordingPath);
                        }
                        catch
                        {
                        }

                        Debug.WriteLine($"[StopRealtimeVideoProcessing] Muxing successful: {finalOutputPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Muxing failed: {ex.Message}. Using live recording only.");
                }
                finally
                {
                    ClearPreRecordingBuffer();
                }
            }
            else
            {
                // LIVE-ONLY recording (no pre-recording) - add audio if present
                if (capturedVideo != null && !string.IsNullOrEmpty(liveAudioFilePath))
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Live-only recording with audio - adding audio track");
                    try
                    {
                        string originalVideoPath = capturedVideo.FilePath;

                        // Add audio to video using composition (audio already on disk - OOM-safe)
                        string outputPath = Path.Combine(
                            Path.GetDirectoryName(originalVideoPath),
                            $"final_{Guid.NewGuid():N}.mp4"
                        );
                        string finalPath = await AddAudioToVideoAsync(originalVideoPath, liveAudioFilePath, outputPath);

                        if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                        {
                            var muxedInfo = new FileInfo(finalPath);
                            capturedVideo = new CapturedVideo
                            {
                                FilePath = finalPath,
                                FileSizeBytes = muxedInfo.Length,
                                Duration = capturedVideo.Duration,
                                Time = capturedVideo.Time
                            };

                            // Delete original video-only file
                            try { File.Delete(originalVideoPath); } catch { }
                            Debug.WriteLine($"[StopRealtimeVideoProcessing] Live recording with audio successful: {finalPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StopRealtimeVideoProcessing] Failed to add audio to live recording: {ex.Message}");
                    }
                }
                ClearPreRecordingBuffer();
            }

            // Clean up temp audio files
            if (!string.IsNullOrEmpty(preRecAudioFilePath) && File.Exists(preRecAudioFilePath))
            {
                try { File.Delete(preRecAudioFilePath); } catch { }
            }
            if (!string.IsNullOrEmpty(liveAudioFilePath) && File.Exists(liveAudioFilePath))
            {
                try { File.Delete(liveAudioFilePath); } catch { }
            }

            // Update state and notify success
            stopwatchTotal.Stop();
            Debug.WriteLine($"[StopRealtimeVideoProcessing] TIMING: TOTAL stop time {stopwatchTotal.ElapsedMilliseconds}ms");

            SetIsRecordingVideo(false);
            if (capturedVideo != null)
            {
                OnRecordingSuccess(capturedVideo);
            }

            IsBusy = false; // Release busy state after successful muxing

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }
        }
        catch (Exception ex)
        {
            // Clean up on error
            if (NativeControl is NativeCamera nativeCam)
            {
                nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            }

            // Clean up audio resources on error
            if (_audioCapture != null)
            {
                try
                {
                    _audioCapture.SampleAvailable -= OnAudioSampleAvailable;
                    await _audioCapture.StopAsync();
                    _audioCapture.Dispose();
                }
                catch { }
                _audioCapture = null;
            }

            // Clean up live audio writer on error
            CleanupLiveAudioWriter();
            _audioBuffer = null;
            _preRecordedAudioSamples = null;

            // Note: temp audio files (preRecAudioFilePath, liveAudioFilePath, combinedAudioFilePath)
            // are local to the try block and will be orphaned on error. They're in temp directory
            // so OS will clean them up eventually.

            _captureVideoEncoder = null;

            SetIsRecordingVideo(false);
            IsBusy = false; // Release busy state on error

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }

            RecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task AbortRealtimeVideoProcessingInternal()
    {
        ICaptureVideoEncoder encoder = null;

        try
        {
            // CRITICAL: Stop frame capture FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            if (NativeControl is NativeCamera nativeCam)
            {
                nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            }

            // CRITICAL: Unsubscribe event handlers before disposal to prevent memory leaks
            if (_captureVideoEncoder is AppleVideoToolboxEncoder appleEncToAbort && _encoderPreviewInvalidateHandler != null)
            {
                appleEncToAbort.PreviewAvailable -= _encoderPreviewInvalidateHandler;
            }

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;
            await encoder?.StopAsync();

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);

            // Dispose encoder directly WITHOUT calling StopAsync - this should abandon the recording
            Debug.WriteLine($"[AbortRealtimeVideoProcessing] Disposing encoder without finalizing video");
            encoder?.Dispose();

            // Clean up audio resources
            if (_audioCapture != null)
            {
                try
                {
                    _audioCapture.SampleAvailable -= OnAudioSampleAvailable;
                    await _audioCapture.StopAsync();
                    _audioCapture.Dispose();
                }
                catch { }
                _audioCapture = null;
                Debug.WriteLine($"[AbortRealtimeVideoProcessing] Audio capture stopped and disposed");
            }

            // Clean up live audio writer
            CleanupLiveAudioWriter();
            _audioBuffer = null;
            _preRecordedAudioSamples = null;
            Debug.WriteLine($"[AbortRealtimeVideoProcessing] Live audio writer cleaned up");

            // Clean up any pre-recording files
            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[AbortRealtimeVideoProcessing] Deleted pre-recording file: {_preRecordingFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AbortRealtimeVideoProcessing] Failed to delete pre-recording file: {ex.Message}");
                }
            }

            ClearPreRecordingBuffer();

            // Update state - recording is now aborted
            SetIsRecordingVideo(false);
            SetIsPreRecording(false);

            Debug.WriteLine($"[AbortRealtimeVideoProcessing] Capture video flow aborted successfully");

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AbortRealtimeVideoProcessing] Error during abort: {ex.Message}");

            // Clean up on error
            if (NativeControl is NativeCamera nativeCam)
            {
                nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            }

            _captureVideoEncoder = null;

            SetIsRecordingVideo(false);
            SetIsPreRecording(false);

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }

            // Don't throw - we want abort to always succeed in stopping the recording
        }
        finally
        {
            // Ensure encoder is disposed even if errors occurred
            if (encoder != null)
            {
                try
                {
                    encoder.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            IsBusy = false;
        }
    }

    /// <summary>
    /// Start video recording. Run this in background thread!
    /// Locks the device rotation for the entire recording session.
    /// Uses either native video recording or capture video flow depending on UseRealtimeVideoProcessing setting.
    /// 
    /// State machine logic:
    /// - If EnablePreRecording && !IsPreRecording: Start memory-only recording (pre-recording phase)
    /// - If IsPreRecording && !IsRecording: Prepend buffer and start file recording (normal phase)
    /// - Otherwise: Start normal file recording
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StartVideoRecording()
    {
        if (IsBusy)
        {
            Debug.WriteLine($"[StartVideoRecording] IsBusy cannot start");
            return;
        }

        // Handle audio-only recording (EnableVideoRecording=false)
        if (!EnableVideoRecording)
        {
            if (!EnableAudioRecording)
                throw new InvalidOperationException("EnableAudioRecording must be true when EnableVideoRecording is false");
            await StartAudioOnlyRecording();
            return;
        }

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecording={IsRecording}");

        try
        {
            // State 1 -> State 2: If pre-recording enabled and not yet in pre-recording phase, start memory-only recording
            if (EnablePreRecording && !IsPreRecording && !IsRecording)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning to IsPreRecording (memory-only recording)");
                SetIsPreRecording(true);
                InitializePreRecordingBuffer();

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // Start recording in memory-only mode
                if (UseRealtimeVideoProcessing && FrameProcessor != null)
                {
                    await StartRealtimeVideoProcessing(false);
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
            // State 2 -> State 3: If in pre-recording phase, transition to file recording with muxing
            else if (IsPreRecording && !IsRecording)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning from IsPreRecording to IsRecording (file recording with mux) [OVERLAP MODE]");

                // 1. GLOBAL TIMELINE: Capture current duration but DON'T stop pre-rec
                // Pre-rec continues running until the swap - ZERO frames lost
                // Live encoder will use the same _captureVideoStartTime, so timestamps are seamless
                if (_captureVideoEncoder != null)
                {
                    _preRecordingDurationTracked = _captureVideoEncoder.EncodingDuration;
                    Debug.WriteLine($"[StartVideoRecording] Pre-rec duration estimate: {_preRecordingDurationTracked.TotalSeconds:F3}s (still running until swap)");
                }
                else
                {
                    _preRecordingDurationTracked = TimeSpan.Zero;
                }
                
                // 2. Process Audio (Save buffer, start writer)
                // SAVE PRE-REC AUDIO before transition - DON'T TRIM YET
                // Audio will be trimmed in background task AFTER video is finalized (for correct sync)
                if (_audioBuffer != null && EnableAudioRecording)
                {
                    var allAudioSamples = _audioBuffer.GetAllSamples();
                    Debug.WriteLine($"[StartVideoRecording] Saving ALL {allAudioSamples?.Length ?? 0} audio samples (will trim after video is finalized)");

                    // Save ALL samples - trimming happens in background task after we know actual video duration
                    _preRecordedAudioSamples = allAudioSamples;

                    // OOM-SAFE: Start streaming audio to file instead of linear buffer
                    // Try to activate pre-allocated writer first (avoids lag spike), fall back to creating new one
                    var firstSample = _preRecordedAudioSamples?.FirstOrDefault();
                    int sampleRate = firstSample?.SampleRate ?? AudioSampleRate;
                    int channels = firstSample?.Channels ?? AudioChannels;
                    if (ActivateLiveAudioWriter())
                    {
                        Debug.WriteLine("[StartVideoRecording] Activated PRE-ALLOCATED audio writer for live phase (no lag spike)");
                    }
                    else if (StartLiveAudioWriter(sampleRate, channels))
                    {
                        Debug.WriteLine("[StartVideoRecording] Started NEW audio writer for live phase (fallback)");
                    }
                    else
                    {
                        Debug.WriteLine("[StartVideoRecording] Warning: Failed to start streaming audio writer");
                    }

                    // Clear the pre-rec buffer
                    _audioBuffer = null;
                }

                // 3. Update State Flags
                SetIsPreRecording(false);
                SetIsRecordingVideo(true);

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // 4. Start Live Recording (Overlap)
                ICaptureVideoEncoder oldEncoderToStop = null;

                if (UseRealtimeVideoProcessing && FrameProcessor != null)
                {
                    oldEncoderToStop = await StartRealtimeVideoProcessing(preserveCurrentEncoder: true);
                }
                else
                {
                    // Fallback to native (no overlap support implemented here yet)
                    if (_captureVideoEncoder != null)
                    {
                         // Classic stop behavior for native path to avoid issues
                         var preRecResult = await _captureVideoEncoder.StopAsync();
                         if (preRecResult != null && !string.IsNullOrEmpty(preRecResult.FilePath))
                        {
                            _preRecordingFilePath = preRecResult.FilePath;
                            _preRecordingDurationTracked = _captureVideoEncoder.EncodingDuration;
                        }
                         _captureVideoEncoder.Dispose();
                         _captureVideoEncoder = null;
                    }
                    await StartNativeVideoRecording();
                }
                
                // 5. Background Stop of Old Encoder (if overlap used)
                if (oldEncoderToStop != null)
                {
                    Debug.WriteLine("[StartVideoRecording] Spawning background task to stop/flush old encoder");

                    _preRecFlushTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Pre-rec already stopped accepting frames (StopAcceptingFrames called earlier)
                            // Just flush the buffer to file
                            var preRecResult = await oldEncoderToStop.StopAsync();
                            Debug.WriteLine("[StartVideoRecording] BkTask: Old encoder stopped");

                            // Capture result
                            if (preRecResult != null && !string.IsNullOrEmpty(preRecResult.FilePath))
                            {
                                _preRecordingFilePath = preRecResult.FilePath;
                                // Update with FINAL duration for muxer (from actual frame timestamps, not wall-clock)
                                _preRecordingDurationTracked = oldEncoderToStop.EncodingDuration;
                                Debug.WriteLine($"[StartVideoRecording] BkTask: Captured pre-recording file: {_preRecordingFilePath}");
                                Debug.WriteLine($"[StartVideoRecording] BkTask: Final video duration: {_preRecordingDurationTracked.TotalSeconds:F3}s");

                                // AUDIO SYNC FIX: Now trim audio to match ACTUAL video duration
                                if (_preRecordedAudioSamples != null && _preRecordedAudioSamples.Length > 0)
                                {
                                    var videoDurationMs = _preRecordingDurationTracked.TotalMilliseconds;
                                    var lastSampleTimestamp = _preRecordedAudioSamples[_preRecordedAudioSamples.Length - 1].TimestampNs;
                                    var firstSampleTimestamp = _preRecordedAudioSamples[0].TimestampNs;
                                    var audioDurationMs = (lastSampleTimestamp - firstSampleTimestamp) / 1_000_000.0;

                                    Debug.WriteLine($"[StartVideoRecording] BkTask: Audio sync - Video: {videoDurationMs:F0}ms, Audio: {audioDurationMs:F0}ms");

                                    if (audioDurationMs > videoDurationMs + 50)
                                    {
                                        // Trim audio from START to match video duration
                                        var cutoffTimestampNs = lastSampleTimestamp - (long)(videoDurationMs * 1_000_000);
                                        int startIndex = 0;
                                        for (int i = 0; i < _preRecordedAudioSamples.Length; i++)
                                        {
                                            if (_preRecordedAudioSamples[i].TimestampNs >= cutoffTimestampNs)
                                            {
                                                startIndex = i;
                                                break;
                                            }
                                        }

                                        if (startIndex > 0)
                                        {
                                            var trimmedLength = _preRecordedAudioSamples.Length - startIndex;
                                            var trimmedSamples = new AudioSample[trimmedLength];
                                            Array.Copy(_preRecordedAudioSamples, startIndex, trimmedSamples, 0, trimmedLength);
                                            _preRecordedAudioSamples = trimmedSamples;
                                            Debug.WriteLine($"[StartVideoRecording] BkTask: AUDIO SYNC - Trimmed {startIndex} samples, keeping {trimmedLength} to match video");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[StartVideoRecording] BkTask WARNING: No file path returned!");
                            }

                            // Dispose
                            oldEncoderToStop.Dispose();
                            Debug.WriteLine("[StartVideoRecording] BkTask: Old encoder disposed");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[StartVideoRecording] BkTask Error: {ex}");
                        }
                    });
                }
            }
            // Normal recording (no pre-recording)
            else if (!IsRecording)
            {
                Debug.WriteLine("[StartVideoRecording] Starting normal recording (no pre-recording)");
                SetIsRecordingVideo(true);

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (UseRealtimeVideoProcessing && FrameProcessor != null)
                {
                    await StartRealtimeVideoProcessing();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
        }
        catch (Exception ex)
        {
            SetIsRecordingVideo(false);
            SetIsPreRecording(false);
            IsBusy = false;
            RecordingLockedRotation = -1; // Reset on error
            ClearPreRecordingBuffer();
            RecordingFailed?.Invoke(this, ex);
            throw;
        }
    }

    protected async Task<List<string>> GetAvailableAudioDevicesPlatform()
    {
        return await Task.Run(() =>
        {
            var detected = new List<string>();
            try
            {
                // AVCaptureDeviceDiscoverySession available iOS 10+
                var discoverySession = AVCaptureDeviceDiscoverySession.Create(
                    new AVCaptureDeviceType[] { AVCaptureDeviceType.BuiltInMicrophone },
                    AVMediaTypes.Audio,
                    AVCaptureDevicePosition.Unspecified);

                if (discoverySession != null && discoverySession.Devices != null)
                {
                    foreach (var device in discoverySession.Devices)
                    {
                        detected.Add(device.LocalizedName);
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"[SkiaCamera] Audio device discovery error: {e}");
            }
            return detected;
        });
    }

    protected async Task<List<string>> GetAvailableAudioCodecsPlatform()
    {
        // iOS primarily supports AAC for MP4
        return await Task.FromResult(new List<string> { "AAC" });
    }

    /// <summary>
    /// Combines pre-recorded audio samples with live audio samples into a single array.
    /// Used when muxing pre-recording + live video with continuous audio.
    /// </summary>
    private AudioSample[] CombineAudioSamples(AudioSample[] preRec, AudioSample[] live)
    {
        if (preRec == null || preRec.Length == 0) return live ?? Array.Empty<AudioSample>();
        if (live == null || live.Length == 0) return preRec;

        var combined = new AudioSample[preRec.Length + live.Length];
        Array.Copy(preRec, 0, combined, 0, preRec.Length);
        Array.Copy(live, 0, combined, preRec.Length, live.Length);
        return combined;
    }

    /// <summary>
    /// Writes audio samples to an M4A file using AVAssetWriter.
    /// This is used to create a separate audio track file that can be muxed with video.
    /// </summary>
    private async Task<string> WriteAudioSamplesToM4AAsync(AudioSample[] samples, string outputPath)
    {
        if (samples == null || samples.Length == 0)
        {
            Debug.WriteLine("[WriteAudioSamplesToM4A] No audio samples provided");
            return null;
        }

        // Declare memory tracker outside try block so we can clean up on exceptions
        var memoryToFree = new List<IntPtr>();

        try
        {
            // Delete existing file
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            var url = NSUrl.FromFilename(outputPath);
            // Use UTType for M4A audio format - "com.apple.m4a-audio" is the standard UTI
            var writer = new AVAssetWriter(url, "com.apple.m4a-audio", out var writerError);

            if (writer == null || writerError != null)
            {
                Debug.WriteLine($"[WriteAudioSamplesToM4A] AVAssetWriter creation failed: {writerError?.LocalizedDescription}");
                return null;
            }

            // Configure audio output (AAC)
            var audioSettings = new NSDictionary(
                AVAudioSettings.AVFormatIDKey, NSNumber.FromInt32((int)AudioToolbox.AudioFormatType.MPEG4AAC),
                AVAudioSettings.AVSampleRateKey, NSNumber.FromDouble(samples[0].SampleRate),
                AVAudioSettings.AVNumberOfChannelsKey, NSNumber.FromInt32(samples[0].Channels),
                AVAudioSettings.AVEncoderBitRateKey, NSNumber.FromInt32(128000)
            );

            var audioInput = new AVAssetWriterInput(AVMediaTypes.Audio.GetConstant(), new AVFoundation.AudioSettings(audioSettings));
            audioInput.ExpectsMediaDataInRealTime = false;

            if (!writer.CanAddInput(audioInput))
            {
                Debug.WriteLine("[WriteAudioSamplesToM4A] Cannot add audio input to writer");
                writer.Dispose();
                return null;
            }
            writer.AddInput(audioInput);

            // Start writing
            if (!writer.StartWriting())
            {
                Debug.WriteLine($"[WriteAudioSamplesToM4A] StartWriting failed: {writer.Error?.LocalizedDescription}");
                audioInput.Dispose();
                writer.Dispose();
                return null;
            }
            writer.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);

            // Get the first timestamp as baseline (normalize to 0)
            long firstTimestampNs = samples[0].TimestampNs;
            int writtenCount = 0;
            int failedCount = 0;

            Debug.WriteLine($"[WriteAudioSamplesToM4A] Writing {samples.Length} audio samples, first timestamp: {firstTimestampNs / 1_000_000_000.0:F3}s");

            // Write all samples
            // CRITICAL: CMBlockBuffer does NOT copy data, so we track memory in memoryToFree for deferred cleanup
            foreach (var sample in samples)
            {
                // Normalize timestamp relative to session start
                long normalizedNs = sample.TimestampNs - firstTimestampNs;
                if (normalizedNs < 0) normalizedNs = 0;

                // Create CMSampleBuffer from audio sample - memory is tracked for deferred cleanup
                IntPtr memoryPtr;
                using var cmSampleBuffer = CreateCMSampleBufferFromAudio(sample, normalizedNs, out memoryPtr);
                if (cmSampleBuffer == null)
                {
                    failedCount++;
                    continue;
                }

                // Wait for audio input to be ready
                int waitCount = 0;
                while (!audioInput.ReadyForMoreMediaData && writer.Status == AVAssetWriterStatus.Writing && waitCount < 100)
                {
                    await Task.Yield();
                    waitCount++;
                }

                if (audioInput.ReadyForMoreMediaData && writer.Status == AVAssetWriterStatus.Writing)
                {
                    if (audioInput.AppendSampleBuffer(cmSampleBuffer))
                    {
                        writtenCount++;
                        // Track memory for cleanup after writer finishes
                        if (memoryPtr != IntPtr.Zero)
                        {
                            memoryToFree.Add(memoryPtr);
                        }
                    }
                    else
                    {
                        failedCount++;
                        // Free immediately on append failure
                        if (memoryPtr != IntPtr.Zero)
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                        }
                    }
                }
                else
                {
                    failedCount++;
                    // Free immediately if not appended
                    if (memoryPtr != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                    }
                }
            }

            // Finish writing
            audioInput.MarkAsFinished();

            var tcs = new TaskCompletionSource<bool>();
            writer.FinishWriting(() =>
            {
                tcs.TrySetResult(writer.Status == AVAssetWriterStatus.Completed);
            });
            await tcs.Task;

            audioInput.Dispose();
            var status = writer.Status;
            var error = writer.Error;
            writer.Dispose();

            // NOW it's safe to free all tracked memory - writer has finished consuming the data
            int freedCount = memoryToFree.Count;
            foreach (var ptr in memoryToFree)
            {
                if (ptr != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }
            memoryToFree.Clear();
            Debug.WriteLine($"[WriteAudioSamplesToM4A] Freed {freedCount} memory allocations");

            if (status == AVAssetWriterStatus.Completed && File.Exists(outputPath))
            {
                var fileSize = new FileInfo(outputPath).Length;
                Debug.WriteLine($"[WriteAudioSamplesToM4A] Success: {writtenCount} samples written ({fileSize / 1024.0:F2} KB), {failedCount} failed");
                return outputPath;
            }
            else
            {
                Debug.WriteLine($"[WriteAudioSamplesToM4A] Failed: status={status}, error={error?.LocalizedDescription}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WriteAudioSamplesToM4A] Exception: {ex.Message}");

            // CRITICAL: Free all tracked memory on exception to prevent memory leaks
            foreach (var ptr in memoryToFree)
            {
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
            }
            memoryToFree.Clear();

            return null;
        }
    }

    /// <summary>
    /// Starts streaming audio to file for OOM-safe live recording.
    /// Audio samples are written directly to disk instead of being buffered in memory.
    /// If writer was pre-allocated, this just activates it. Otherwise creates a new one.
    /// </summary>
    private bool StartLiveAudioWriter(int sampleRate, int channels)
    {
        lock (_liveAudioWriterLock)
        {
            // If pre-allocated, just activate it
            if (_liveAudioWriter != null && _liveAudioWriterPreAllocated)
            {
                Debug.WriteLine("[StartLiveAudioWriter] Found pre-allocated writer, activating...");
                // Release lock temporarily to call ActivateLiveAudioWriter which also takes the lock
                // Actually, we're already in the lock, so just inline the activation logic
                try
                {
                    if (!_liveAudioWriter.StartWriting())
                    {
                        Debug.WriteLine($"[StartLiveAudioWriter] StartWriting failed on pre-allocated: {_liveAudioWriter.Error?.LocalizedDescription}");
                        CleanupLiveAudioWriter();
                        return false;
                    }
                    _liveAudioWriter.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);
                    _liveAudioWriterPreAllocated = false;
                    Debug.WriteLine($"[StartLiveAudioWriter] Activated pre-allocated writer: {_liveAudioFilePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StartLiveAudioWriter] Exception activating pre-allocated: {ex.Message}");
                    CleanupLiveAudioWriter();
                    return false;
                }
            }

            if (_liveAudioWriter != null)
            {
                Debug.WriteLine("[StartLiveAudioWriter] Writer already active");
                return true;
            }

            try
            {
                // Create temp file path
                var tempDir = Path.GetTempPath();
                _liveAudioFilePath = Path.Combine(tempDir, $"live_audio_{Guid.NewGuid():N}.m4a");

                // Delete existing file
                if (File.Exists(_liveAudioFilePath))
                {
                    File.Delete(_liveAudioFilePath);
                }

                var url = NSUrl.FromFilename(_liveAudioFilePath);
                _liveAudioWriter = new AVAssetWriter(url, "com.apple.m4a-audio", out var writerError);

                if (_liveAudioWriter == null || writerError != null)
                {
                    Debug.WriteLine($"[StartLiveAudioWriter] AVAssetWriter creation failed: {writerError?.LocalizedDescription}");
                    return false;
                }

                // Configure audio output (AAC)
                var audioSettings = new NSDictionary(
                    AVAudioSettings.AVFormatIDKey, NSNumber.FromInt32((int)AudioToolbox.AudioFormatType.MPEG4AAC),
                    AVAudioSettings.AVSampleRateKey, NSNumber.FromDouble(sampleRate),
                    AVAudioSettings.AVNumberOfChannelsKey, NSNumber.FromInt32(channels),
                    AVAudioSettings.AVEncoderBitRateKey, NSNumber.FromInt32(128000)
                );

                _liveAudioInput = new AVAssetWriterInput(AVMediaTypes.Audio.GetConstant(), new AVFoundation.AudioSettings(audioSettings));
                _liveAudioInput.ExpectsMediaDataInRealTime = true;  // Real-time streaming

                if (!_liveAudioWriter.CanAddInput(_liveAudioInput))
                {
                    Debug.WriteLine("[StartLiveAudioWriter] Cannot add audio input to writer");
                    CleanupLiveAudioWriter();
                    return false;
                }
                _liveAudioWriter.AddInput(_liveAudioInput);

                // Start writing
                if (!_liveAudioWriter.StartWriting())
                {
                    Debug.WriteLine($"[StartLiveAudioWriter] StartWriting failed: {_liveAudioWriter.Error?.LocalizedDescription}");
                    CleanupLiveAudioWriter();
                    return false;
                }
                _liveAudioWriter.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);

                _liveAudioWriterPreAllocated = false;  // Actively writing, not just pre-allocated
                _liveAudioFirstTimestampNs = -1;  // Will be set on first sample
                _liveAudioMemoryToFree = new List<IntPtr>();

                Debug.WriteLine($"[StartLiveAudioWriter] Started streaming to: {_liveAudioFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartLiveAudioWriter] Exception: {ex.Message}");
                CleanupLiveAudioWriter();
                return false;
            }
        }
    }

    /// <summary>
    /// Writes an audio sample to the live streaming audio file.
    /// Thread-safe and non-blocking for audio callback performance.
    /// </summary>
    private void WriteSampleToLiveAudioWriter(AudioSample sample)
    {
        lock (_liveAudioWriterLock)
        {
            if (_liveAudioWriter == null || _liveAudioInput == null ||
                _liveAudioWriter.Status != AVAssetWriterStatus.Writing)
            {
                return;
            }

            try
            {
                // Set first timestamp baseline on first sample
                if (_liveAudioFirstTimestampNs < 0)
                {
                    _liveAudioFirstTimestampNs = sample.TimestampNs;
                }

                // Normalize timestamp relative to start
                long normalizedNs = sample.TimestampNs - _liveAudioFirstTimestampNs;
                if (normalizedNs < 0) normalizedNs = 0;

                // Create CMSampleBuffer from audio sample
                IntPtr memoryPtr;
                using var cmSampleBuffer = CreateCMSampleBufferFromAudio(sample, normalizedNs, out memoryPtr);
                if (cmSampleBuffer == null)
                {
                    if (memoryPtr != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                    }
                    return;
                }

                // Append to writer if ready (drop if not - real-time streaming)
                if (_liveAudioInput.ReadyForMoreMediaData)
                {
                    if (_liveAudioInput.AppendSampleBuffer(cmSampleBuffer))
                    {
                        // Track memory for cleanup after writer finishes
                        if (memoryPtr != IntPtr.Zero)
                        {
                            _liveAudioMemoryToFree?.Add(memoryPtr);
                        }
                    }
                    else
                    {
                        // Free immediately on append failure
                        if (memoryPtr != IntPtr.Zero)
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                        }
                    }
                }
                else
                {
                    // Writer not ready - drop sample and free memory immediately
                    if (memoryPtr != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WriteSampleToLiveAudioWriter] Exception: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Stops the live audio writer and finalizes the file.
    /// Returns the path to the completed audio file, or null on failure.
    /// </summary>
    private async Task<string> StopLiveAudioWriterAsync()
    {
        AVAssetWriter writer;
        AVAssetWriterInput input;
        string filePath;
        List<IntPtr> memoryToFree;

        lock (_liveAudioWriterLock)
        {
            writer = _liveAudioWriter;
            input = _liveAudioInput;
            filePath = _liveAudioFilePath;
            memoryToFree = _liveAudioMemoryToFree;

            // Clear references immediately to prevent new writes
            _liveAudioWriter = null;
            _liveAudioInput = null;
            _liveAudioFilePath = null;
            _liveAudioMemoryToFree = null;
            _liveAudioFirstTimestampNs = -1;
        }

        if (writer == null)
        {
            Debug.WriteLine("[StopLiveAudioWriter] No active writer");
            return null;
        }

        try
        {
            // Mark as finished
            input?.MarkAsFinished();

            // Finish writing asynchronously
            var tcs = new TaskCompletionSource<bool>();
            writer.FinishWriting(() =>
            {
                tcs.TrySetResult(writer.Status == AVAssetWriterStatus.Completed);
            });
            await tcs.Task;

            var status = writer.Status;
            var error = writer.Error;

            // Dispose resources
            input?.Dispose();
            writer.Dispose();

            // NOW it's safe to free all tracked memory
            if (memoryToFree != null)
            {
                int freedCount = memoryToFree.Count;
                foreach (var ptr in memoryToFree)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        try
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
                Debug.WriteLine($"[StopLiveAudioWriter] Freed {freedCount} memory allocations");
            }

            if (status == AVAssetWriterStatus.Completed && File.Exists(filePath))
            {
                var fileSize = new FileInfo(filePath).Length;
                Debug.WriteLine($"[StopLiveAudioWriter] Success: {filePath} ({fileSize / 1024.0:F2} KB)");
                return filePath;
            }
            else
            {
                Debug.WriteLine($"[StopLiveAudioWriter] Failed: status={status}, error={error?.LocalizedDescription}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StopLiveAudioWriter] Exception: {ex.Message}");

            // Clean up memory on exception
            if (memoryToFree != null)
            {
                foreach (var ptr in memoryToFree)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        try { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); } catch { }
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Cleanup helper for live audio writer resources.
    /// </summary>
    private void CleanupLiveAudioWriter()
    {
        _liveAudioInput?.Dispose();
        _liveAudioInput = null;
        _liveAudioWriter?.Dispose();
        _liveAudioWriter = null;
        _liveAudioFilePath = null;
        _liveAudioFirstTimestampNs = -1;
        _liveAudioWriterPreAllocated = false;

        // Free any tracked memory
        if (_liveAudioMemoryToFree != null)
        {
            foreach (var ptr in _liveAudioMemoryToFree)
            {
                if (ptr != IntPtr.Zero)
                {
                    try { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); } catch { }
                }
            }
            _liveAudioMemoryToFree = null;
        }
    }

    /// <summary>
    /// Concatenates two M4A audio files into a single output file.
    /// Uses AVAssetExportSession for efficient file-to-file operation (no memory buffering).
    /// </summary>
    private async Task<string> ConcatenateAudioFilesAsync(string firstAudioPath, string secondAudioPath)
    {
        if (string.IsNullOrEmpty(firstAudioPath) || !File.Exists(firstAudioPath))
        {
            return secondAudioPath;
        }
        if (string.IsNullOrEmpty(secondAudioPath) || !File.Exists(secondAudioPath))
        {
            return firstAudioPath;
        }

        try
        {
            // Create output path
            var outputPath = Path.Combine(
                Path.GetTempPath(),
                $"combined_audio_{Guid.NewGuid():N}.m4a"
            );

            // Load both audio files as assets
            using var firstAsset = AVAsset.FromUrl(NSUrl.FromFilename(firstAudioPath));
            using var secondAsset = AVAsset.FromUrl(NSUrl.FromFilename(secondAudioPath));

            // Create composition
            using var composition = new AVMutableComposition();
            var audioTrack = composition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0);

            // Get audio tracks from both assets
            var firstAudioTracks = firstAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());
            var secondAudioTracks = secondAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());

            if (firstAudioTracks.Length == 0 && secondAudioTracks.Length == 0)
            {
                Debug.WriteLine("[ConcatenateAudioFiles] No audio tracks found in either file");
                return null;
            }

            CoreMedia.CMTime insertTime = CoreMedia.CMTime.Zero;

            // Insert first audio track
            if (firstAudioTracks.Length > 0)
            {
                var timeRange = new CoreMedia.CMTimeRange
                {
                    Start = CoreMedia.CMTime.Zero,
                    Duration = firstAsset.Duration
                };
                NSError insertError;
                audioTrack.InsertTimeRange(timeRange, firstAudioTracks[0], CoreMedia.CMTime.Zero, out insertError);
                if (insertError != null)
                {
                    Debug.WriteLine($"[ConcatenateAudioFiles] Error inserting first track: {insertError.LocalizedDescription}");
                }
                insertTime = firstAsset.Duration;
                Debug.WriteLine($"[ConcatenateAudioFiles] First audio duration: {firstAsset.Duration.Seconds:F2}s");
            }

            // Insert second audio track at the end of first
            if (secondAudioTracks.Length > 0)
            {
                var timeRange = new CoreMedia.CMTimeRange
                {
                    Start = CoreMedia.CMTime.Zero,
                    Duration = secondAsset.Duration
                };
                NSError insertError;
                audioTrack.InsertTimeRange(timeRange, secondAudioTracks[0], insertTime, out insertError);
                if (insertError != null)
                {
                    Debug.WriteLine($"[ConcatenateAudioFiles] Error inserting second track: {insertError.LocalizedDescription}");
                }
                Debug.WriteLine($"[ConcatenateAudioFiles] Second audio duration: {secondAsset.Duration.Seconds:F2}s");
            }

            // Export the composition
            using var exportSession = new AVAssetExportSession(composition, AVAssetExportSessionPreset.AppleM4A);
            exportSession.OutputUrl = NSUrl.FromFilename(outputPath);
            exportSession.OutputFileType = AVFileTypes.AppleM4a.GetConstant();// new NSString("com.apple.m4a-audio");

            var tcs = new TaskCompletionSource<bool>();
            exportSession.ExportAsynchronously(() =>
            {
                tcs.TrySetResult(exportSession.Status == AVAssetExportSessionStatus.Completed);
            });

            await tcs.Task;

            if (exportSession.Status == AVAssetExportSessionStatus.Completed && File.Exists(outputPath))
            {
                var fileSize = new FileInfo(outputPath).Length;
                Debug.WriteLine($"[ConcatenateAudioFiles] Success: {outputPath} ({fileSize / 1024.0:F2} KB)");
                return outputPath;
            }
            else
            {
                Debug.WriteLine($"[ConcatenateAudioFiles] Export failed: {exportSession.Status}, {exportSession.Error?.LocalizedDescription}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConcatenateAudioFiles] Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a CMSampleBuffer from an AudioSample with the specified timestamp.
    /// IMPORTANT: The caller must track and free the returned memoryPtr AFTER AppendSampleBuffer completes,
    /// because CMBlockBuffer does NOT copy the data - it just references it.
    /// </summary>
    /// <param name="sample">The audio sample to convert</param>
    /// <param name="timestampNs">Timestamp in nanoseconds</param>
    /// <param name="memoryPtr">Output: The unmanaged memory pointer that must be freed by caller after use</param>
    private CoreMedia.CMSampleBuffer CreateCMSampleBufferFromAudio(AudioSample sample, long timestampNs, out IntPtr memoryPtr)
    {
        memoryPtr = IntPtr.Zero;
        try
        {
            int bytesPerSample = sample.BitDepth == AudioBitDepth.Pcm8Bit ? 1 :
                                 sample.BitDepth == AudioBitDepth.Float32Bit ? 4 : 2;
            int bytesPerFrame = bytesPerSample * sample.Channels;
            nint numSamples = sample.Data.Length / bytesPerFrame;

            // Create audio format description
            var audioFormat = new AudioToolbox.AudioStreamBasicDescription
            {
                SampleRate = sample.SampleRate,
                Format = AudioToolbox.AudioFormatType.LinearPCM,
                FormatFlags = AudioToolbox.AudioFormatFlags.LinearPCMIsPacked | AudioToolbox.AudioFormatFlags.LinearPCMIsSignedInteger,
                ChannelsPerFrame = sample.Channels,
                BytesPerPacket = bytesPerFrame,
                FramesPerPacket = 1,
                BytesPerFrame = bytesPerFrame,
                BitsPerChannel = bytesPerSample * 8
            };

            if (sample.BitDepth == AudioBitDepth.Float32Bit)
            {
                audioFormat.FormatFlags = AudioToolbox.AudioFormatFlags.LinearPCMIsFloat | AudioToolbox.AudioFormatFlags.LinearPCMIsPacked;
            }

            // Create format description using P/Invoke
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
                return null;
            }

            var formatDesc = CoreMedia.CMFormatDescription.Create(formatDescPtr, true);

            // Allocate unmanaged memory for audio data
            // CRITICAL: Caller must free this AFTER AppendSampleBuffer completes!
            memoryPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(sample.Data.Length);
            System.Runtime.InteropServices.Marshal.Copy(sample.Data, 0, memoryPtr, sample.Data.Length);

            // Create block buffer - NOTE: This does NOT copy the data, just references memoryPtr!
            var blockBuffer = CoreMedia.CMBlockBuffer.FromMemoryBlock(
                memoryPtr,
                (nuint)sample.Data.Length,
                null,
                0,
                (nuint)sample.Data.Length,
                CoreMedia.CMBlockBufferFlags.AssureMemoryNow,
                out var blockErr);

            if (blockBuffer == null || blockErr != CoreMedia.CMBlockBufferError.None)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                memoryPtr = IntPtr.Zero;
                return null;
            }

            // Calculate presentation time
            double timeSec = timestampNs / 1_000_000_000.0;
            var pts = CoreMedia.CMTime.FromSeconds(timeSec, sample.SampleRate);

            // Duration based on sample count
            double durationSec = (double)numSamples / sample.SampleRate;
            var duration = CoreMedia.CMTime.FromSeconds(durationSec, sample.SampleRate);

            var timing = new CoreMedia.CMSampleTimingInfo
            {
                PresentationTimeStamp = pts,
                DecodeTimeStamp = pts,
                Duration = duration
            };

            var cmSampleBuffer = CoreMedia.CMSampleBuffer.CreateReady(
                blockBuffer,
                (CoreMedia.CMAudioFormatDescription)formatDesc,
                (int)numSamples,
                new[] { timing },
                new nuint[] { (nuint)sample.Data.Length },
                out var sbErr);

            // DO NOT free memoryPtr here! Caller must free it after AppendSampleBuffer completes.
            return cmSampleBuffer;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CreateCMSampleBufferFromAudio] Error: {ex.Message}");
            // Free memory on error
            if (memoryPtr != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryPtr);
                memoryPtr = IntPtr.Zero;
            }
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMAudioFormatDescriptionCreate(
        IntPtr allocator,
        ref AudioToolbox.AudioStreamBasicDescription asbd,
        nuint layoutSize,
        IntPtr layout,
        nuint magicCookieSize,
        IntPtr magicCookie,
        IntPtr extensions,
        out IntPtr formatDescriptionOut
    );

    /// <summary>
    /// Combines a video file with an audio file into a new output file using AVMutableComposition.
    /// Used for live-only recording (no pre-recording) when audio needs to be added.
    /// </summary>
    private async Task<string> AddAudioToVideoAsync(string videoPath, string audioPath, string outputPath)
    {
        try
        {
            Debug.WriteLine($"[AddAudioToVideo] Combining video: {videoPath} with audio: {audioPath}");

            // Delete existing output file
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            using var videoAsset = AVAsset.FromUrl(NSUrl.FromFilename(videoPath));
            using var audioAsset = AVAsset.FromUrl(NSUrl.FromFilename(audioPath));

            if (videoAsset == null)
            {
                Debug.WriteLine("[AddAudioToVideo] Failed to load video asset");
                return null;
            }

            using var composition = new AVMutableComposition();

            // Add video track
            var videoTrack = composition.AddMutableTrack(AVMediaTypes.Video.GetConstant(), 0);
            var sourceVideoTracks = videoAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant());

            if (sourceVideoTracks == null || sourceVideoTracks.Length == 0)
            {
                Debug.WriteLine("[AddAudioToVideo] No video tracks in source file");
                return null;
            }

            var sourceVideoTrack = sourceVideoTracks[0];
            var videoRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = videoAsset.Duration };
            videoTrack.InsertTimeRange(videoRange, sourceVideoTrack, CoreMedia.CMTime.Zero, out var videoError);

            if (videoError != null)
            {
                Debug.WriteLine($"[AddAudioToVideo] Failed to insert video track: {videoError.LocalizedDescription}");
                return null;
            }

            // Copy transform from source video
            videoTrack.PreferredTransform = sourceVideoTrack.PreferredTransform;

            // Add audio track if audio asset is available
            if (audioAsset != null)
            {
                var sourceAudioTracks = audioAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());
                if (sourceAudioTracks != null && sourceAudioTracks.Length > 0)
                {
                    var audioTrack = composition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0);
                    var sourceAudioTrack = sourceAudioTracks[0];

                    // Use the shorter of video duration and audio duration
                    var audioDuration = audioAsset.Duration;
                    var insertDuration = audioDuration.Seconds <= videoAsset.Duration.Seconds ? audioDuration : videoAsset.Duration;

                    var audioRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = insertDuration };
                    audioTrack.InsertTimeRange(audioRange, sourceAudioTrack, CoreMedia.CMTime.Zero, out var audioError);

                    if (audioError != null)
                    {
                        Debug.WriteLine($"[AddAudioToVideo] Failed to insert audio track: {audioError.LocalizedDescription}");
                        // Continue without audio
                    }
                    else
                    {
                        Debug.WriteLine($"[AddAudioToVideo] Added audio track ({insertDuration.Seconds:F2}s)");
                    }
                }
            }

            // Export the composition - use Passthrough since we're just muxing, not re-encoding
            using var exportSession = new AVAssetExportSession(composition, AVAssetExportSessionPreset.Passthrough);
            exportSession.OutputUrl = NSUrl.FromFilename(outputPath);
            exportSession.OutputFileType = AVFileTypes.Mpeg4.GetConstant();
            exportSession.ShouldOptimizeForNetworkUse = ShouldOptimizeForNetworkUse;

            var tcs = new TaskCompletionSource<bool>();
            exportSession.ExportAsynchronously(() =>
            {
                tcs.TrySetResult(exportSession.Status == AVAssetExportSessionStatus.Completed);
            });

            var success = await tcs.Task;
            var status = exportSession.Status;
            var error = exportSession.Error;

            if (success && File.Exists(outputPath))
            {
                var outputInfo = new FileInfo(outputPath);
                Debug.WriteLine($"[AddAudioToVideo] Success: {outputPath} ({outputInfo.Length / 1024.0:F2} KB)");
                return outputPath;
            }
            else
            {
                Debug.WriteLine($"[AddAudioToVideo] Export failed: status={status}, error={error?.LocalizedDescription}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AddAudioToVideo] Exception: {ex.Message}");
            return null;
        }
    }

    #region Preview Audio Capture

    private void OnPreviewAudioSampleAvailable(object sender, AudioSample sample)
    {
        // Lightweight - just fire the event, no recording logic
        OnAudioSampleAvailable(sample);
    }

    partial void StartPreviewAudioCapture()
    {
        // Start audio capture if either recording audio or audio monitoring is enabled
        if (_previewAudioCapture != null || (!EnableAudioRecording && !EnableAudioMonitoring))
            return;

        Task.Run(async () =>
        {
            if (!await _audioSemaphore.WaitAsync(1)) // Skip if busy processing
                return;

            try
            {
                StopPreviewAudioCapture();

                _previewAudioCapture = new AudioCaptureApple();
                _previewAudioCapture.SampleAvailable += OnPreviewAudioSampleAvailable;
                var started = await _previewAudioCapture.StartAsync(AudioSampleRate, AudioChannels, AudioBitDepth,
                    AudioDeviceIndex);
                if (started)
                {
                    Debug.WriteLine(
                        $"[SkiaCamera.Apple] Preview audio capture started: {_previewAudioCapture.SampleRate}Hz, {_previewAudioCapture.Channels}ch");
                }
                else
                {
                    RaiseError($"Preview audio capture failed to start: {_previewAudioCapture.LastError}");
                    _previewAudioCapture.SampleAvailable -= OnPreviewAudioSampleAvailable;
                    _previewAudioCapture.Dispose();
                    _previewAudioCapture = null;
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Preview audio capture error: {ex}");
            }
            finally
            {
                _audioSemaphore?.Release();
            }
        });
    }

 

    private SemaphoreSlim _audioSemaphore = new(1, 1);

    public override void OnDisposing()
    {
        _audioSemaphore?.Dispose();
        _audioSemaphore = null;
    }

    partial void StopPreviewAudioCapture()
    {
        if (_previewAudioCapture == null)
            return;

        try
        {
            _previewAudioCapture.SampleAvailable -= OnPreviewAudioSampleAvailable;
            var kill = _previewAudioCapture;
            _ = kill.StopAsync().ContinueWith(_ =>
            {
                kill.Dispose();
            });

            _previewAudioCapture = null;
            Debug.WriteLine("[SkiaCamera.Apple] Preview audio capture stopped");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera.Apple] Error stopping preview audio: {ex.Message}");
        }
    }

    #endregion

    #region AUDIO-ONLY RECORDING

    private IAudioCapture _audioOnlyCapture;

    private partial void CreateAudioOnlyEncoder(out IAudioOnlyEncoder encoder)
    {
        encoder = new AudioOnlyEncoderApple();
    }

    private partial void StartAudioOnlyCapture(int sampleRate, int channels, out Task task)
    {
        var tcs = new TaskCompletionSource<bool>();
        task = tcs.Task;

        Task.Run(async () =>
        {
            try
            {
                // Stop preview audio capture first
                StopPreviewAudioCapture();

                _audioOnlyCapture = new AudioCaptureApple();
                _audioOnlyCapture.SampleAvailable += OnAudioOnlySampleAvailable;
                var started = await _audioOnlyCapture.StartAsync(sampleRate, channels, AudioBitDepth, AudioDeviceIndex);
                if (started)
                {
                    Debug.WriteLine($"[SkiaCamera.Apple] Audio-only capture started: {_audioOnlyCapture.SampleRate}Hz, {_audioOnlyCapture.Channels}ch");
                }
                else
                {
                    Debug.WriteLine("[SkiaCamera.Apple] Audio-only capture failed to start");
                    _audioOnlyCapture.SampleAvailable -= OnAudioOnlySampleAvailable;
                    _audioOnlyCapture.Dispose();
                    _audioOnlyCapture = null;
                }
                tcs.TrySetResult(started);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera.Apple] Audio-only capture error: {ex.Message}");
                tcs.TrySetException(ex);
            }
        });
    }

    private partial void StopAudioOnlyCapture(out Task task)
    {
        var tcs = new TaskCompletionSource<bool>();
        task = tcs.Task;

        if (_audioOnlyCapture == null)
        {
            tcs.TrySetResult(true);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                _audioOnlyCapture.SampleAvailable -= OnAudioOnlySampleAvailable;
                await _audioOnlyCapture.StopAsync();
                _audioOnlyCapture.Dispose();
                _audioOnlyCapture = null;
                Debug.WriteLine("[SkiaCamera.Apple] Audio-only capture stopped");
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera.Apple] Error stopping audio-only capture: {ex.Message}");
                tcs.TrySetException(ex);
            }
        });
    }

    #endregion

    //end of class declaration
}

#endif

