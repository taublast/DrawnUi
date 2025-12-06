global using DrawnUi.Draw;
global using SkiaSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Views;
using Color = Microsoft.Maui.Graphics.Color;

#if WINDOWS
using DrawnUi.Camera.Platforms.Windows; 
#elif IOS || MACCATALYST
using AVFoundation;
using CoreMedia;
using Foundation;
#endif

namespace DrawnUi.Camera;

/// <summary>
/// SkiaCamera control with support for manual camera selection.
///
/// Basic usage:
/// - Set Facing to Default/Selfie for automatic camera selection
/// - Set Facing to Manual and CameraIndex for manual camera selection
///
/// Example:
/// var camera = new SkiaCamera { Facing = CameraPosition.Manual, CameraIndex = 2 };
/// var cameras = await camera.GetAvailableCamerasAsync();
/// </summary>
public partial class SkiaCamera : SkiaControl
{

#if !IOS && !MACCATALYST


    #region VIDEO RECORDING METHODS

    /// <summary>
    /// Start video recording. Run this in background thread!
    /// Locks the device rotation for the entire recording session.
    /// Uses either native video recording or capture video flow depending on UseCaptureVideoFlow setting.
    /// 
    /// State machine logic:
    /// - If EnablePreRecording && !IsPreRecording: Start memory-only recording (pre-recording phase)
    /// - If IsPreRecording && !IsRecordingVideo: Prepend buffer and start file recording (normal phase)
    /// - Otherwise: Start normal file recording
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StartVideoRecording()
    {
        if (IsBusy)
            return;

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecordingVideo={IsRecordingVideo}");

        IsBusy = true;

        try
        {
            // State 1 -> State 2: If pre-recording enabled and not yet in pre-recording phase, start memory-only recording
            if (EnablePreRecording && !IsPreRecording && !IsRecordingVideo)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning to IsPreRecording (memory-only recording)");
                IsPreRecording = true;
                InitializePreRecordingBuffer();

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // Start recording in memory-only mode
                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    await StartCaptureVideoFlow();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
            // State 2 -> State 3: If in pre-recording phase, transition to file recording with muxing
            else if (IsPreRecording && !IsRecordingVideo)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning from IsPreRecording to IsRecordingVideo (file recording with mux)");

                // CRITICAL ANDROID FIX: Single-file approach - reuse existing encoder!
                // Encoder was already initialized and warmed up during pre-recording phase
                // Just call StartAsync() to write buffer + continue with live frames in same muxer session
#if ANDROID
                // Change states
                IsPreRecording = false;
                IsRecordingVideo = true;
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // CRITICAL: Reuse existing encoder (single-file pattern)
                // This will write buffer to muxer, then continue with live frames in same session
                if (_captureVideoEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] ========================================");
                    Debug.WriteLine("[StartVideoRecording] SINGLE-FILE PATTERN (professional)");
                    Debug.WriteLine("[StartVideoRecording] Reusing pre-warmed encoder for live recording");
                    Debug.WriteLine("[StartVideoRecording] No encoder recreation = zero frame loss!");
                    Debug.WriteLine("[StartVideoRecording] Buffer already has keyframes from periodic requests");
                    Debug.WriteLine("[StartVideoRecording] ========================================");

                    await _captureVideoEncoder.StartAsync();

                    Debug.WriteLine("[StartVideoRecording] Encoder transitioned to live recording mode");
                    Debug.WriteLine("[StartVideoRecording] Buffer written, live frames continuing in same muxer");
                }
                else
                {
                    Debug.WriteLine("[StartVideoRecording] ERROR: No encoder found for transition!");
                }
#else
                // iOS: Create new encoder FIRST, swap atomically, THEN stop old one (prevents frame loss)
                ICaptureVideoEncoder oldEncoder = null;
                if (_captureVideoEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] Preparing to transition encoders without frame loss");

                    // Keep reference to old encoder
                    oldEncoder = _captureVideoEncoder;
                    Debug.WriteLine("[StartVideoRecording] Old encoder captured for transition");
                }

                // Update state flags BEFORE creating new encoder
                IsPreRecording = false;
                IsRecordingVideo = true;
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    // Create new encoder - this ATOMICALLY swaps _captureVideoEncoder to the new instance
                    // Any frames arriving now will go to the new encoder (no gap!)
                    await StartCaptureVideoFlow();
                    Debug.WriteLine("[StartVideoRecording] New encoder created and active - frames now routing to encoder #2");

                    // NOTE: Pre-recording offset will be set AFTER stopping old encoder and getting correct duration
                }

                // NOW stop the old encoder (frames already going to new encoder, zero frame loss)
                if (oldEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] Stopping old pre-recording encoder to finalize file");

                    try
                    {
                        var preRecResult = await oldEncoder.StopAsync();
                        Debug.WriteLine("[StartVideoRecording] Pre-recording encoder stopped and file finalized");

                        if (preRecResult != null && !string.IsNullOrEmpty(preRecResult.FilePath))
                        {
                            _preRecordingFilePath = preRecResult.FilePath;

                            // CRITICAL: Use corrected duration from StopAsync result, NOT the wall-clock duration captured earlier
                            _preRecordingDurationTracked = preRecResult.Duration;
                            Debug.WriteLine($"[StartVideoRecording] Captured pre-recording file: {_preRecordingFilePath}");
                            Debug.WriteLine($"[StartVideoRecording] Captured pre-recording duration (corrected): {_preRecordingDurationTracked.TotalSeconds:F2}s");

                            // CRITICAL: Set pre-recording duration offset on NEW encoder with CORRECTED duration
                            if (_preRecordingDurationTracked > TimeSpan.Zero && _captureVideoEncoder != null)
                            {
                                // Platform-specific: set offset on encoder
#if WINDOWS
                                if (_captureVideoEncoder is WindowsCaptureVideoEncoder winEncoder)
                                {
                                    winEncoder.SetPreRecordingDuration(_preRecordingDurationTracked);
                                    Debug.WriteLine($"[StartVideoRecording] Set pre-recording offset on new Windows encoder: {_preRecordingDurationTracked.TotalSeconds:F2}s");
                                }
#elif IOS || MACCATALYST
                                if (_captureVideoEncoder is AppleVideoToolboxEncoder appleEncoder)
                                {
                                    appleEncoder.SetPreRecordingDuration(_preRecordingDurationTracked);
                                    Debug.WriteLine($"[StartVideoRecording] Set pre-recording offset on new Apple encoder: {_preRecordingDurationTracked.TotalSeconds:F2}s");
                                }
#endif
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StartVideoRecording] Error stopping pre-recording encoder: {ex.Message}");
                    }

                    oldEncoder?.Dispose();
                    oldEncoder = null;
                    Debug.WriteLine("[StartVideoRecording] Old encoder disposed");
                }
                else
                {
                    await StartNativeVideoRecording();
                    Debug.WriteLine("[StartVideoRecording] Native recording started for live recording");
                }
#endif
            }
            // Normal recording (no pre-recording)
            else if (!IsRecordingVideo)
            {
                Debug.WriteLine("[StartVideoRecording] Starting normal recording (no pre-recording)");
                IsRecordingVideo = true;

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    await StartCaptureVideoFlow();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
        }
        catch (Exception ex)
        {
            IsRecordingVideo = false;
            IsPreRecording = false;
            IsBusy = false;
            RecordingLockedRotation = -1; // Reset on error
            ClearPreRecordingBuffer();
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }

        IsBusy = false;
    }

    private async Task StartCaptureVideoFlow()
    {
#if WINDOWS
        // Create platform-specific encoder with existing GRContext (GPU path)
        var grContext = (Superview?.CanvasView as SkiaViewAccelerated)?.GRContext;
        _captureVideoEncoder = new WindowsCaptureVideoEncoder(grContext);
        
        // Set parent reference and pre-recording mode
        _captureVideoEncoder.ParentCamera = this;
        _captureVideoEncoder.IsPreRecordingMode = IsPreRecording;
        Debug.WriteLine($"[StartCaptureVideoFlow] Encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Generate output path
        // If pre-recording, use temp file for streaming encoded frames
        string outputPath;
        if (IsPreRecording)
        {
            // Use temp file path from InitializePreRecordingBuffer
            outputPath = _preRecordingFilePath;
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.WriteLine("[StartCaptureVideoFlow] ERROR: Pre-recording file path not initialized");
                return;
            }
            Debug.WriteLine($"[StartCaptureVideoFlow] Pre-recording to file: {outputPath}");
        }
        else
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            outputPath = Path.Combine(documentsPath, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            Debug.WriteLine($"[StartCaptureVideoFlow] Recording to file: {outputPath}");
        }

        // CRITICAL: Set start time BEFORE initializing encoder to avoid losing initial frames!
        // Encoder initialization can take 1-2 seconds on Android, and we calculate PTS from this time
        _captureVideoStartTime = DateTime.Now;
        _capturePtsBaseTime = null;

        // Get current video format dimensions from native camera
        var currentFormat = NativeControl?.GetCurrentVideoFormat();
        var width = currentFormat?.Width ?? 1280;
        var height = currentFormat?.Height ?? 720;
        var fps = currentFormat?.FrameRate > 0 ? currentFormat.FrameRate : 30;

        // Initialize encoder with current settings (follow camera defaults)
        _diagEncWidth = width;
        _diagEncHeight = height;
        _diagBitrate = (long)Math.Max(1, width * height) * Math.Max(1, fps) * 4 / 10;
        await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio);

        // CRITICAL: In pre-recording mode, do NOT call StartAsync during initialization
        // Pre-recording mode should just buffer frames in memory without starting file writing
        // StartAsync will be called later when transitioning to live recording
        if (!IsPreRecording)
        {
            await _captureVideoEncoder.StartAsync();
            Debug.WriteLine($"[StartCaptureVideoFlow] StartAsync called for live/normal recording");
        }
        else
        {
            Debug.WriteLine($"[StartCaptureVideoFlow] Skipping StartAsync - pre-recording mode will buffer frames in memory");
        }

        // Reset diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        _targetFps = fps;

        // Windows uses real-time preview-driven capture (no timer)
        _useWindowsPreviewDrivenCapture = true;

        // Control preview source: processed frames from encoder (PreviewVideoFlow=true) or raw camera (PreviewVideoFlow=false)
        // Only applies when UseCaptureVideoFlow is TRUE (enforced by caller)
        UseRecordingFramesForPreview = PreviewVideoFlow;

        // Invalidate preview when the encoder publishes a new composed frame (Windows mirror)
        if (PreviewVideoFlow && _captureVideoEncoder is WindowsCaptureVideoEncoder _winEncPrev)
        {
            _encoderPreviewInvalidateHandler = (s, e) =>
            {
                try
                {
                    MainThread.BeginInvokeOnMainThread(() => UpdatePreview());
                }
                catch
                {
                }
            };
            _winEncPrev.PreviewAvailable += _encoderPreviewInvalidateHandler;
        }

        // Set up progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnVideoRecordingProgress(duration));
        };
#elif ANDROID
        // Create Android encoder (GPU path via MediaCodec Surface + EGL + Skia GL)
        _captureVideoEncoder = new AndroidCaptureVideoEncoder();
        
        // Set parent reference and pre-recording mode
        _captureVideoEncoder.ParentCamera = this;
        _captureVideoEncoder.IsPreRecordingMode = IsPreRecording;
        Debug.WriteLine($"[StartCaptureVideoFlow] Android encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Control preview source: processed frames from encoder (PreviewVideoFlow=true) or raw camera (PreviewVideoFlow=false)
        // Only applies when UseCaptureVideoFlow is TRUE (enforced by caller)
        UseRecordingFramesForPreview = PreviewVideoFlow;

        // Invalidate preview when the encoder publishes a new composed frame (Android mirror)
        if (PreviewVideoFlow && _captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
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
            _droidEncPrev.PreviewAvailable += _encoderPreviewInvalidateHandler;
        }

        // CRITICAL: Always use final Movies directory path (single-file approach)
        // Buffer stays in memory, so output path doesn't matter during pre-recording phase
        var ctx = Android.App.Application.Context;
        var moviesDir =
            ctx.GetExternalFilesDir(Android.OS.Environment.DirectoryMovies)?.AbsolutePath ??
            ctx.FilesDir?.AbsolutePath ?? ".";
        string outputPath = Path.Combine(moviesDir, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        if (IsPreRecording)
        {
            Debug.WriteLine($"[StartCaptureVideoFlow] Android pre-recording (buffer to memory, final output: {outputPath})");
        }
        else
        {
            Debug.WriteLine($"[StartCaptureVideoFlow] Android recording to file: {outputPath}");
        }

        // Use camera-reported format if available; else fall back to preview size or 1280x720
        var currentFormat = NativeControl?.GetCurrentVideoFormat();
        var width =
            currentFormat?.Width > 0 ? currentFormat.Width : (int)(PreviewSize.Width > 0 ? PreviewSize.Width : 1280);
        var height =
            currentFormat?.Height > 0 ? currentFormat.Height : (int)(PreviewSize.Height > 0 ? PreviewSize.Height : 720);
        var fps = currentFormat?.FrameRate > 0 ? currentFormat.FrameRate : 30;

        // IMPORTANT: Align encoder orientation with the live preview to avoid instant crop/"zoom" when recording starts.
        // If preview is portrait (h > w) but the selected video format is landscape (w >= h),
        // swap encoder dimensions so we record a vertical video instead of cropping it to fit a horizontal surface.
        int prevW = 0, prevH = 0;
        int encWBefore = width, encHBefore = height;
        int sensor = -1;
        bool previewRotated = false;
        try
        {
            if (NativeControl is NativeCamera cam)
            {
                prevW = cam.PreviewWidth;
                prevH = cam.PreviewHeight;
                sensor = cam.SensorOrientation;
                previewRotated = (sensor == 90 || sensor == 270);

                // If preview is rotated (portrait logical orientation), make encoder portrait too
                if (previewRotated && width >= height)
                {
                    var tmp = width;
                    width = height;
                    height = tmp;
                }
                // If preview is not rotated but encoder is portrait, make encoder landscape to match
                else if (!previewRotated && height > width)
                {
                    var tmp = width;
                    width = height;
                    height = tmp;
                }
            }
        }
        catch
        {
        }

        System.Diagnostics.Debug.WriteLine(
            $"[CAPTURE-ENCODER] preview={prevW}x{prevH} rotated={previewRotated} sensor={sensor} currentFormat={(currentFormat?.Width ?? 0)}x{(currentFormat?.Height ?? 0)}@{fps} encoderBefore={encWBefore}x{encHBefore} encoderFinal={width}x{height} UseRecordingFramesForPreview={UseRecordingFramesForPreview}");
        _diagEncWidth = width;
        _diagEncHeight = height;
        _diagBitrate = Math.Max((long)width * height * 4, 2_000_000L);

        // Pass locked rotation to encoder for proper video orientation metadata (Android-specific)
        if (_captureVideoEncoder is DrawnUi.Camera.AndroidCaptureVideoEncoder androidEncoder)
        {
            await androidEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio, RecordingLockedRotation);
        }
        else
        {
            await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio);
        }

        // CRITICAL: In pre-recording mode, do NOT call StartAsync during initialization
        // Pre-recording mode should just buffer frames in memory without starting file writing
        // StartAsync will be called later when transitioning to live recording
        if (!IsPreRecording)
        {
            await _captureVideoEncoder.StartAsync();
            Debug.WriteLine($"[StartCaptureVideoFlow] StartAsync called for live/normal recording");
        }
        else
        {
            Debug.WriteLine($"[StartCaptureVideoFlow] Skipping StartAsync - pre-recording mode will buffer frames in memory");
        }

        // Drop the first camera frame to avoid occasional corrupted first frame from the camera/RenderScript pipeline
        _androidWarmupDropRemaining = 1;

        _captureVideoStartTime = DateTime.Now;
        _capturePtsBaseTime = null;

        // Diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        _targetFps = fps;

        // Event-driven capture on Android: drive encoder from camera preview callback
        int diagCounter = 0;

        if (NativeControl is NativeCamera androidCam)
        {
            _androidPreviewHandler = async (captured) =>
            {
                try
                {
                    // CRITICAL: Process frames during BOTH pre-recording AND live recording
                    // If encoder is null/disposed during transition, this check will catch it and return gracefully
                    if ((!IsPreRecording && !IsRecordingVideo) || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
                        return;

                    // Warmup: drop the first frame to avoid occasional corrupted first frame artifacts
                    if (System.Threading.Volatile.Read(ref _androidWarmupDropRemaining) > 0)
                    {
                        System.Threading.Interlocked.Decrement(ref _androidWarmupDropRemaining);
                        System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
                        return;
                    }

                    // Ensure single-frame processing
                    if (System.Threading.Interlocked.CompareExchange(ref _androidFrameGate, 1, 0) != 0)
                    {
                        System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
                        return;
                    }

                    // CRITICAL FIX: Use wall clock time from recording start, not camera monotonic timestamps
                    // captured.Time is a monotonic timestamp that continues from camera session start
                    // If camera ran in preview mode before recording, captured.Time is already at high value
                    // We need PTS relative to when recording STARTED (wall clock), not first frame arrival
                    var elapsedLocal = DateTime.Now - _captureVideoStartTime;
                    if (elapsedLocal.Ticks < 0)
                        elapsedLocal = TimeSpan.Zero;

                    try
                    {
                        using (droidEnc.BeginFrame(elapsedLocal, out var canvas, out var info))
                        {
                            var img = captured?.Image;
                            if (img == null)
                                return;

                            var rects = GetAspectFillRects(img.Width, img.Height, info.Width, info.Height);

                            // Apply 180° flip for selfie camera in landscape (sensor is opposite orientation)
                            var isSelfieInLandscape = Facing == CameraPosition.Selfie && (RecordingLockedRotation == 90 || RecordingLockedRotation == 270);
                            if (isSelfieInLandscape)
                            {
                                canvas.Save();
                                canvas.Scale(-1, -1, info.Width / 2f, info.Height / 2f);
                            }

                            canvas.DrawImage(img, rects.src, rects.dst);

                            if (isSelfieInLandscape)
                            {
                                canvas.Restore();
                            }

                            if (FrameProcessor != null || VideoDiagnosticsOn)
                            {
                                // Apply rotation based on device orientation
                                var rotation = GetActiveRecordingRotation();
                                canvas.Save();
                                ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                                var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                                var frame = new DrawableFrame
                                {
                                    Width = frameWidth, Height = frameHeight, Canvas = canvas, Time = elapsedLocal
                                };
                                FrameProcessor?.Invoke(frame);

                                if (VideoDiagnosticsOn)
                                    DrawDiagnostics(canvas, info.Width, info.Height);

                                canvas.Restore();
                            }
                        }

                        var __sw = System.Diagnostics.Stopwatch.StartNew();
                        await droidEnc.SubmitFrameAsync();
                        __sw.Stop();
                        _diagLastSubmitMs = __sw.Elapsed.TotalMilliseconds;
                        System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CaptureFrame/Event] Error: {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Volatile.Write(ref _androidFrameGate, 0);
                    }
                }
                catch
                {
                }
            };

            androidCam.PreviewCaptureSuccess = _androidPreviewHandler;
        }

        // Progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnVideoRecordingProgress(duration));
        };
#elif IOS || MACCATALYST
        // Create Apple encoder using VideoToolbox for hardware H.264 encoding
        _captureVideoEncoder = new AppleVideoToolboxEncoder();

        // Set parent reference and pre-recording mode
        _captureVideoEncoder.ParentCamera = this;
        _captureVideoEncoder.IsPreRecordingMode = IsPreRecording;
        Debug.WriteLine($"[StartCaptureVideoFlow] iOS encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Control preview source: processed frames from encoder (PreviewVideoFlow=true) or raw camera (PreviewVideoFlow=false)
        // Only applies when UseCaptureVideoFlow is TRUE (enforced by caller)
        UseRecordingFramesForPreview = PreviewVideoFlow;
        if (PreviewVideoFlow && _captureVideoEncoder is AppleVideoToolboxEncoder _appleEncPrev)
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
            _appleEncPrev.PreviewAvailable += _encoderPreviewInvalidateHandler;
        }

        // Output path (Documents) or pre-recording file path
        string outputPath;
        if (IsPreRecording)
        {
            outputPath = _preRecordingFilePath;
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.WriteLine("[StartCaptureVideoFlow] ERROR: Pre-recording file path not initialized");
                return;
            }
            Debug.WriteLine($"[StartCaptureVideoFlow] iOS pre-recording to file: {outputPath}");
        }
        else
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            outputPath = Path.Combine(documentsPath, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            Debug.WriteLine($"[StartCaptureVideoFlow] iOS recording to file: {outputPath}");
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

        // Pass locked rotation to encoder for proper video orientation metadata (iOS-specific)
        if (_captureVideoEncoder is DrawnUi.Camera.AppleVideoToolboxEncoder appleEncoder)
        {
            await appleEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio, RecordingLockedRotation);

            // ✅ CRITICAL: If transitioning from pre-recording to live, set the duration offset BEFORE StartAsync
            // BUT ONLY if pre-recording file actually exists and has content (otherwise standalone live recording will be corrupted!)
            if (!IsPreRecording && _preRecordingDurationTracked > TimeSpan.Zero)
            {
                // Verify pre-recording file exists and has content before setting offset
                bool hasValidPreRecording = !string.IsNullOrEmpty(_preRecordingFilePath) &&
                                           File.Exists(_preRecordingFilePath) &&
                                           new FileInfo(_preRecordingFilePath).Length > 0;

                if (hasValidPreRecording)
                {
                    appleEncoder.SetPreRecordingDuration(_preRecordingDurationTracked);
                    Debug.WriteLine($"[StartCaptureVideoFlow] Set pre-recording duration offset: {_preRecordingDurationTracked.TotalSeconds:F2}s (pre-recording file valid)");
                }
                else
                {
                    Debug.WriteLine($"[StartCaptureVideoFlow] WARNING: Pre-recording duration tracked ({_preRecordingDurationTracked.TotalSeconds:F2}s) but file is invalid/empty. NOT setting offset to avoid corrupting standalone live recording!");
                    _preRecordingDurationTracked = TimeSpan.Zero; // Reset to avoid muxing attempt later
                }
            }
        }
        else
        {
            await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio);
        }

        // CRITICAL: In pre-recording mode, do NOT call StartAsync during initialization
        // Pre-recording mode should just buffer frames in memory without starting file writing
        // StartAsync will be called later when transitioning to live recording
        if (!IsPreRecording)
        {
            await _captureVideoEncoder.StartAsync();
            Debug.WriteLine($"[StartCaptureVideoFlow] StartAsync called for live/normal recording");
        }
        else
        {
            Debug.WriteLine($"[StartCaptureVideoFlow] Skipping StartAsync - pre-recording mode will buffer frames in memory");
        }

        _capturePtsBaseTime = null;
        _captureVideoStartTime = DateTime.Now;

        // Diagnostics
        if (IsPreRecording  || (!IsPreRecording &&  _preRecordingDurationTracked == TimeSpan.Zero))
        {
            _diagStartTime = DateTime.Now;
            _diagDroppedFrames = 0;
            _diagSubmittedFrames = 0;
            _diagLastSubmitMs = 0;
            _captureVideoTotalStartTime = DateTime.Now;
        }

        _targetFps = fps;

        // Progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnVideoRecordingProgress(duration));
        };

        // Start frame capture timer for Apple (drive encoder frames)
        _frameCaptureTimer?.Dispose();
        var periodMs = Math.Max(1, (int)Math.Round(1000.0 / Math.Max(1, fps)));
        _frameCaptureTimer =
            new System.Threading.Timer(CaptureFrame, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(periodMs));
#else
        throw new NotSupportedException("Capture video flow is currently only supported on Windows, Android and Apple");
#endif
    }

    private async void CaptureFrame(object state)
    {
        if (!(IsRecordingVideo || IsPreRecording) || _captureVideoEncoder == null)
        {
            Debug.WriteLine($"[CaptureFrame] Early exit: IsRecordingVideo={IsRecordingVideo}, IsPreRecording={IsPreRecording}, encoder={(_captureVideoEncoder == null ? "NULL" : "EXISTS")}");
            return;
        }

        // Make sure we never queue more than one frame — drop if previous is still processing
        if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) != 0)
        {
            System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            return;
        }

        try
        {
            // Double-check encoder still exists (race condition protection)
            if (_captureVideoEncoder == null || (!IsRecordingVideo && !IsPreRecording))
            {
                Debug.WriteLine($"[CaptureFrame] Double-check failed: encoder={(_captureVideoEncoder == null ? "NULL" : "EXISTS")}, IsRecordingVideo={IsRecordingVideo}, IsPreRecording={IsPreRecording}");
                return;
            }

            var elapsed = DateTime.Now - _captureVideoStartTime;
            var elapsedTotal = DateTime.Now - _captureVideoTotalStartTime;

#if WINDOWS
            // GPU-first path on Windows: draw directly into encoder-owned GPU surface
            if (_captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
            {
                using var previewImage = NativeControl?.GetPreviewImage();
                if (previewImage == null)
                {
                    Debug.WriteLine("[CaptureFrame] No preview image available from camera");
                    return;
                }

                using (winEnc.BeginFrame(elapsed, out var canvas, out var info))
                {
                    // Draw camera frame to encoder surface (aspect-fill)
                    var __rects1 = GetAspectFillRects(previewImage.Width, previewImage.Height, info.Width, info.Height);
                    canvas.DrawImage(previewImage, __rects1.src, __rects1.dst);

                    // Apply overlay
                    if (FrameProcessor != null || VideoDiagnosticsOn)
                    {
                        // Apply rotation based on device orientation
                        var rotation = GetActiveRecordingRotation();
                        canvas.Save();
                        ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                        var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                        var frame = new DrawableFrame
                        {
                            Width = frameWidth, Height = frameHeight, Canvas = canvas, Time = elapsed
                        };
                        FrameProcessor?.Invoke(frame);

                        if (VideoDiagnosticsOn)
                            DrawDiagnostics(canvas, info.Width, info.Height);

                        canvas.Restore();
                    }
                }

                var __sw = System.Diagnostics.Stopwatch.StartNew();
                await winEnc.SubmitFrameAsync();
                __sw.Stop();
                _diagLastSubmitMs = __sw.Elapsed.TotalMilliseconds;
                System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                return;
            }
#elif ANDROID
            // GPU path on Android: draw via Skia GL into MediaCodec's input surface
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder droidEnc)
            {
                // Ensure single-threaded EGL usage; drop if previous frame still in progress
                if (System.Threading.Interlocked.CompareExchange(ref _androidFrameGate, 1, 0) != 0)
                {
                    System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
                    return;
                }

                try
                {
                    using var previewImage = NativeControl?.GetPreviewImage();
                    if (previewImage == null)
                    {
                        Debug.WriteLine("[CaptureFrame] No preview image available from camera");
                        return;
                    }

                    using (droidEnc.BeginFrame(elapsed, out var canvas, out var info))
                    {
                        var __rects2 =
                            GetAspectFillRects(previewImage.Width, previewImage.Height, info.Width, info.Height);

                        // Apply 180° flip for selfie camera in landscape (sensor is opposite orientation)
                        var isSelfieInLandscape = Facing == CameraPosition.Selfie && (RecordingLockedRotation == 90 || RecordingLockedRotation == 270);
                        if (isSelfieInLandscape)
                        {
                            canvas.Save();
                            canvas.Scale(-1, -1, info.Width / 2f, info.Height / 2f);
                        }

                        canvas.DrawImage(previewImage, __rects2.src, __rects2.dst);

                        if (isSelfieInLandscape)
                        {
                            canvas.Restore();
                        }

                        if (FrameProcessor != null || VideoDiagnosticsOn)
                        {
                            // Apply rotation based on device orientation
                            var rotation = GetActiveRecordingRotation();
                            canvas.Save();
                            ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                            var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                            var frame = new DrawableFrame
                            {
                                Width = frameWidth, Height = frameHeight, Canvas = canvas, Time = elapsed
                            };
                            FrameProcessor?.Invoke(frame);

                            if (VideoDiagnosticsOn)
                                DrawDiagnostics(canvas, info.Width, info.Height);

                            canvas.Restore();
                        }
                    }

                    var __sw = System.Diagnostics.Stopwatch.StartNew();
                    await droidEnc.SubmitFrameAsync();
                    __sw.Stop();
                    _diagLastSubmitMs = __sw.Elapsed.TotalMilliseconds;
                    System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CaptureFrame] Error: {ex.Message}");
                }
                finally
                {
                    System.Threading.Volatile.Write(ref _androidFrameGate, 0);
                }
            }
#elif IOS || MACCATALYST
            // GPU path on Apple: draw via Skia into VideoToolbox encoder's pixel buffer
            if (_captureVideoEncoder is DrawnUi.Camera.AppleVideoToolboxEncoder appleEnc)
            {
                using var previewImage = NativeControl?.GetPreviewImage();
                if (previewImage == null)
                {
                    Debug.WriteLine("[CaptureFrame] No preview image available from camera (NativeControl exists: {0})", NativeControl != null);
                    return;
                }

                // Remove debug logging once issue is identified
                // Debug.WriteLine($"[CaptureFrame] Got preview image: {previewImage.Width}x{previewImage.Height}, encoder expects: {_diagEncWidth}x{_diagEncHeight}");

                using (appleEnc.BeginFrame(elapsed, out var canvas, out var info, DeviceRotation))
                {
                    var __rectsA = GetAspectFillRects(previewImage.Width, previewImage.Height, info.Width, info.Height);

                    canvas.DrawImage(previewImage, __rectsA.src, __rectsA.dst);

                    if (FrameProcessor != null || VideoDiagnosticsOn)
                    {
                        // Apply rotation based on device orientation
                        var rotation = GetActiveRecordingRotation();
                        var checkpoint = canvas.Save();
                        ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                        var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                        var frame = new DrawableFrame
                        {
                            Width = frameWidth,
                            Height = frameHeight,
                            Canvas = canvas,
                            Time = elapsedTotal
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
                return;
            }
#endif
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
    /// Stop video recording and finalize the video file.
    /// Resets the locked rotation and restores normal preview behavior.
    /// The video file path will be provided through the VideoRecordingSuccess event.
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StopVideoRecording()
    {
        if (!IsRecordingVideo && !IsPreRecording)
        {
            return;
        }

        Debug.WriteLine($"[StopVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecordingVideo={IsRecordingVideo}");

        IsRecordingVideo = false; //CRITICAL for logic

        // Reset locked rotation
        RecordingLockedRotation = -1;
        Debug.WriteLine($"[StopVideoRecording] Reset locked rotation");

#if ANDROID
        // Stop Android event-driven capture and restore normal preview behavior
        try
        {
            if (NativeControl is NativeCamera androidCam)
            {
                androidCam.PreviewCaptureSuccess = null;
            }
        }
        catch
        {
        }

        UseRecordingFramesForPreview = false;
#endif
        try
        {
            // Check if using capture video flow
            if (_captureVideoEncoder != null)
            {
                await StopCaptureVideoFlow();
            }
            else
            {
#if ONPLATFORM
                await NativeControl.StopVideoRecording();
#endif
                // Note: IsRecordingVideo will be set to false by the VideoRecordingSuccess/Failed callbacks
            }

            IsPreRecording = false;
        }
        catch (Exception ex)
        {
            // On immediate exception, set the state and invoke the event
            IsRecordingVideo = false;
            ClearPreRecordingBuffer();
            VideoRecordingFailed?.Invoke(this, ex);
            IsRecordingVideo = false;
            IsPreRecording = false;

            throw;
        }
    }

    private async Task StopCaptureVideoFlow()
    {
        ICaptureVideoEncoder encoder = null;

        try
        {
            // CRITICAL: Stop frame capture timer FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);

#if WINDOWS
            _useWindowsPreviewDrivenCapture = false;
#endif

#if ANDROID
            // Detach Android mirror event
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
            {
                try
                {
                    _droidEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }
            }
#endif

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;

#if WINDOWS
            // Stop mirroring recording frames to preview and detach event
            UseRecordingFramesForPreview = false;
            if (encoder is WindowsCaptureVideoEncoder _winEncPrev)
            {
                try
                {
                    _winEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }
            }
#endif

            // Stop encoder and get result
            CapturedVideo capturedVideo = await encoder?.StopAsync();

#if ANDROID
            // ANDROID: Single-file approach - no muxing needed!
            // Encoder already wrote buffer + live frames to ONE file
            Debug.WriteLine($"[StopCaptureVideoFlow] Android single-file approach - no muxing needed");
            Debug.WriteLine($"[StopCaptureVideoFlow] Video file: {capturedVideo?.FilePath}");

            // Clean up pre-recording file if it exists (shouldn't exist with new approach)
            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[StopCaptureVideoFlow] Deleted old pre-recording temp file");
                }
                catch { }
            }
            ClearPreRecordingBuffer();
#else
            // iOS/Windows: Mux two files together (legacy approach)
            if (capturedVideo != null && !string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                Debug.WriteLine($"[StopCaptureVideoFlow] Muxing pre-recorded file with live recording");
                try
                {
                    // Save original live recording path before overwriting capturedVideo
                    string originalLiveRecordingPath = capturedVideo.FilePath;

                    // Mux pre-recorded file + live file into final output
                    string finalOutputPath = await MuxVideosAsync(_preRecordingFilePath, originalLiveRecordingPath);
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
                        try { File.Delete(originalLiveRecordingPath); } catch { }

                        Debug.WriteLine($"[StopCaptureVideoFlow] Muxing successful: {finalOutputPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StopCaptureVideoFlow] Muxing failed: {ex.Message}. Using live recording only.");
                }
                finally
                {
                    ClearPreRecordingBuffer();
                }
            }
            else
            {
                ClearPreRecordingBuffer();
            }
#endif

            // Update state and notify success
            IsRecordingVideo = false;
            if (capturedVideo != null)
            {
                OnVideoRecordingSuccess(capturedVideo);
            }
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            IsRecordingVideo = false;
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task AbortCaptureVideoFlow()
    {
        ICaptureVideoEncoder encoder = null;

        try
        {
            // CRITICAL: Stop frame capture timer FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);

#if WINDOWS
            _useWindowsPreviewDrivenCapture = false;
#endif

#if ANDROID
            // Detach Android mirror event
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
            {
                try
                {
                    _droidEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }
            }
#endif

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;

#if WINDOWS
            // Stop mirroring recording frames to preview and detach event
            UseRecordingFramesForPreview = false;
            if (encoder is WindowsCaptureVideoEncoder _winEncPrev)
            {
                try
                {
                    _winEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }
            }
#endif

            // Dispose encoder directly WITHOUT calling StopAsync - this should abandon the recording
            Debug.WriteLine($"[AbortCaptureVideoFlow] Disposing encoder without finalizing video");
            encoder?.Dispose();

            // Clean up any pre-recording files
            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[AbortCaptureVideoFlow] Deleted pre-recording file: {_preRecordingFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AbortCaptureVideoFlow] Failed to delete pre-recording file: {ex.Message}");
                }
            }

            ClearPreRecordingBuffer();

            // Update state - recording is now aborted
            IsRecordingVideo = false;
            IsPreRecording = false;

            Debug.WriteLine($"[AbortCaptureVideoFlow] Capture video flow aborted successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AbortCaptureVideoFlow] Error during abort: {ex.Message}");

            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            IsRecordingVideo = false;
            IsPreRecording = false;

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
        }
    }

    /// <summary>
    /// Save captured video to gallery (copies the video file)
    /// </summary>
    /// <param name="capturedVideo">The captured video to save</param>
    /// <param name="album">Optional album name</param>
    /// <returns>Gallery path if successful, null if failed</returns>
    public async Task<string> SaveVideoToGalleryAsync(CapturedVideo capturedVideo, string album = null)
    {
        if (capturedVideo == null || string.IsNullOrEmpty(capturedVideo.FilePath) ||
            !File.Exists(capturedVideo.FilePath))
            return null;

        try
        {
#if ONPLATFORM
            var path = await NativeControl.SaveVideoToGallery(capturedVideo.FilePath, album);
            return path;
#else
            return null;
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera] Failed to save video to gallery: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clear the pre-recording buffer/file
    /// </summary>
    private void ClearPreRecordingBuffer()
    {
        lock (_preRecordingLock)
        {
            // Stop any active pre-recording encoder first
            if (_captureVideoEncoder != null && IsPreRecording)
            {
                try
                {
                    _captureVideoEncoder.Dispose();
                }
                catch { }
                _captureVideoEncoder = null;
            }

            // Delete temp file if it exists
            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[ClearPreRecordingBuffer] Deleted: {_preRecordingFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ClearPreRecordingBuffer] Failed to delete: {ex.Message}");
                }
            }

            _preRecordingFilePath = null;
            _maxPreRecordingFrames = 0;
        }
    }

    private int _frameInFlight = 0;

    /// <summary>
    /// Enable on-screen diagnostics overlay (effective FPS, dropped frames, last submit ms)
    /// during capture video flow to validate performance.
    /// </summary>

    /// <summary>
    /// Mirror diagnostics toggle used in drawing overlays
    /// </summary>
    public bool VideoDiagnosticsOn
    {
        get => EnableCaptureDiagnostics;
        set => EnableCaptureDiagnostics = value;
    }

    private long _diagDroppedFrames = 0;
    private long _diagSubmittedFrames = 0;
    private double _diagLastSubmitMs = 0;
    private DateTime _diagStartTime;
    private int _diagEncWidth = 0, _diagEncHeight = 0;
    private long _diagBitrate = 0;

    private void DrawDiagnostics(SKCanvas canvas, int width, int height)
    {
        if (!EnableCaptureDiagnostics || canvas == null)
            return;

        var elapsed = (DateTime.Now - _diagStartTime).TotalSeconds;
        var effFps = elapsed > 0 ? _diagSubmittedFrames / elapsed : 0;

        // Compose text
        string line1 = $"FPS: {effFps:F1} / {_targetFps}  dropped: {_diagDroppedFrames}";
        string line2 = $"submit: {_diagLastSubmitMs:F1} ms";
        double mbps = _diagBitrate > 0 ? _diagBitrate / 1_000_000.0 : 0.0;
        string line3 = _diagEncWidth > 0 && _diagEncHeight > 0
            ? $"rec: {_diagEncWidth}x{_diagEncHeight}@{_targetFps}  bitrate: {mbps:F1} Mbps"
            : $"bitrate: {mbps:F1} Mbps";

        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true };
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = Math.Max(14, width / 60f)
        };

        var pad = 8f;
        var y1 = pad + textPaint.TextSize;
        var y2 = y1 + textPaint.TextSize + 4f;
        var y3 = y2 + textPaint.TextSize + 4f;
        var maxTextWidth = Math.Max(textPaint.MeasureText(line1),
            Math.Max(textPaint.MeasureText(line2), textPaint.MeasureText(line3)));
        var rect = new SKRect(pad, pad, pad + maxTextWidth + pad, y3 + pad);

        canvas.Save();
        canvas.DrawRoundRect(rect, 6, 6, bgPaint);
        canvas.DrawText(line1, pad * 1.5f, y1, textPaint);
        canvas.DrawText(line2, pad * 1.5f, y2, textPaint);
        canvas.DrawText(line3, pad * 1.5f, y3, textPaint);
        canvas.Restore();
    }

    private static (SKRect src, SKRect dst) GetAspectFillRects(int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new SKRect(0, 0, dstW, dstH);
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
            return (new SKRect(0, 0, srcW, srcH), dst);

        float srcAR = (float)srcW / srcH;
        float dstAR = (float)dstW / dstH;
        SKRect src;
        if (srcAR > dstAR)
        {
            // Crop width
            float newW = srcH * dstAR;
            float left = (srcW - newW) * 0.5f;
            src = new SKRect(left, 0, left + newW, srcH);
        }
        else
        {
            // Crop height
            float newH = srcW / dstAR;
            float top = (srcH - newH) * 0.5f;
            src = new SKRect(0, top, srcW, top + newH);
        }

        return (src, dst);
    }

    /// <summary>
    /// Applies canvas rotation based on device orientation (0, 90, 180, 270 degrees)
    /// </summary>
    private static void ApplyCanvasRotation(SKCanvas canvas, int width, int height, int rotation)
    {
        var normalizedRotation = rotation % 360;
        if (normalizedRotation < 0)
            normalizedRotation += 360;

        switch (normalizedRotation)
        {
            case 90:
                // Rotate 90° clockwise: translate to bottom-left, then rotate
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
            case 180:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case 270:
                // Rotate 270° clockwise (or 90° counter-clockwise): translate to top-right, then rotate
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
                // case 0: no rotation needed
        }
    }

    /// <summary>
    /// Returns frame dimensions after rotation (swaps width/height for 90/270 degrees)
    /// </summary>
    private static (int width, int height) GetRotatedDimensions(int width, int height, int rotation)
    {
        var normalizedRotation = rotation % 360;
        if (normalizedRotation < 0)
            normalizedRotation += 360;

        // Swap dimensions for 90 and 270 degree rotations
        if (normalizedRotation == 90 || normalizedRotation == 270)
            return (height, width);

        return (width, height);
    }

    private DateTime _captureVideoTotalStartTime;

    #endregion

#endif

}
