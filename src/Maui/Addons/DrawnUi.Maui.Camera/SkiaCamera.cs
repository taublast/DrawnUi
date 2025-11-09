global using DrawnUi.Draw;
global using SkiaSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Views;
using Microsoft.Maui.Controls;
using static Microsoft.Maui.ApplicationModel.Permissions;
using Color = Microsoft.Maui.Graphics.Color;

#if WINDOWS
using DrawnUi.Camera.Platforms.Windows;
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
    /// <summary>
    /// Camera controls cannot use double buffering cache for performance reasons
    /// </summary>
    public override bool CanUseCacheDoubleBuffering => false;

    /// <summary>
    /// Camera preview will be clipped to the control bounds
    /// </summary>
    public override bool WillClipBounds => true;

    public SkiaCamera()
    {
        Instances.Add(this);
        Super.OnNativeAppResumed += Super_OnNativeAppResumed;
        Super.OnNativeAppPaused += Super_OnNativeAppPaused;
    }

    /// <summary>
    /// Camera control does not support update locking
    /// </summary>
    /// <param name="value">Lock state (ignored)</param>
    public override void LockUpdate(bool value)
    {
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        Super.OnNativeAppResumed -= Super_OnNativeAppResumed;
        Super.OnNativeAppPaused -= Super_OnNativeAppResumed;

        if (Superview != null)
        {
            Superview.OrientationChanged -= DeviceOrientationChanged;
        }

        if (NativeControl != null)
        {
            StopInternal(true);

            NativeControl?.Dispose();
        }

        // Clean up capture video resources (stop recording first if active)
        if (IsRecordingVideo && _captureVideoEncoder != null)
        {
            // Force stop capture video flow to prevent disposal race
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Don't await - just force cleanup in disposal
            try
            {
                var encoder = _captureVideoEncoder;
                _captureVideoEncoder = null;
                encoder?.Dispose();
            }
            catch
            {
                // Ignore errors during disposal cleanup
            }
        }
        else
        {
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder?.Dispose();
            _captureVideoEncoder = null;
        }

        NativeControl = null;

        Instances.Remove(this);
    }

    /// <summary>
    /// Command to start the camera
    /// </summary>
    public ICommand CommandStart
    {
        get { return new Command((object context) => { Start(); }); }
    }

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN)
    public virtual void SetZoom(double value)
    {
        throw new NotImplementedException();
    }

#endif


    #region EVENTS

    /// <summary>
    /// Raised when a still image is successfully captured
    /// </summary>
    public event EventHandler<CapturedImage> CaptureSuccess;

    /// <summary>
    /// Raised when still image capture fails
    /// </summary>
    public event EventHandler<Exception> CaptureFailed;

    /// <summary>
    /// Raised when a new preview image is set to the display
    /// </summary>
    public event EventHandler<LoadedImageSource> NewPreviewSet;

    /// <summary>
    /// Raised when a camera error occurs
    /// </summary>
    public event EventHandler<string> OnError;

    /// <summary>
    /// Raised when camera zoom level changes
    /// </summary>
    public event EventHandler<double> Zoomed;

    internal void RaiseError(string error)
    {
        OnError?.Invoke(this, error);
    }

    #endregion

    #region Display

    /// <summary>
    /// The SkiaImage control that displays the camera preview
    /// </summary>
    public SkiaImage Display { get; protected set; }

    protected override void InvalidateMeasure()
    {
        if (Display != null)
        {
            LayoutDisplay();
        }

        base.InvalidateMeasure();
    }

    protected virtual void LayoutDisplay()
    {
        Display.HorizontalOptions = this.NeedAutoWidth ? LayoutOptions.Start : LayoutOptions.Fill;
        Display.VerticalOptions = this.NeedAutoHeight ? LayoutOptions.Start : LayoutOptions.Fill;
    }

    /// <summary>
    /// This creates a SkiaImage that will be used to display camera preview frames. Please do NOT use cache here.
    /// </summary>
    /// <returns></returns>
    protected virtual SkiaImage CreatePreview()
    {
        return new SkiaImage()
        {
            LoadSourceOnFirstDraw = true,
            RescalingQuality = SKFilterQuality.None,
            HorizontalOptions = this.NeedAutoWidth ? LayoutOptions.Start : LayoutOptions.Fill,
            VerticalOptions = this.NeedAutoHeight ? LayoutOptions.Start : LayoutOptions.Fill,
            Aspect = this.Aspect,
        };
    }

    protected override void CreateDefaultContent()
    {
        UpdateOrientationFromDevice();

        base.CreateDefaultContent();
    }

    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        if (Display == null)
        {
            Display = CreatePreview();
            Display.IsParentIndependent = true;
            Display.AddEffect = Effect;
            Display.SetParent(this);
            OnDisplayReady();
        }

        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }

    protected virtual void OnDisplayReady()
    {
        DisplayReady?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raised when the display control is ready for use
    /// </summary>
    public event EventHandler DisplayReady;

    /// <summary>
    /// Apply effects on preview
    /// </summary>
    public virtual void ApplyPreviewProperties()
    {
        if (Display != null)
        {
            Display.AddEffect = Effect;
        }
    }

    #endregion

    #region Capture Photo / Take Picture

    /// <summary>
    /// Take camera picture. Run this in background thread!
    /// </summary>
    /// <returns></returns>
    public async Task TakePicture()
    {
        if (IsBusy)
            return;

        Debug.WriteLine($"[TakePicture] IsMainThread {MainThread.IsMainThread}");

        IsBusy = true;

        IsTakingPhoto = true;

        NativeControl.StillImageCaptureFailed = ex =>
        {
            OnCaptureFailed(ex);

            IsTakingPhoto = false;
        };

        NativeControl.StillImageCaptureSuccess = captured =>
        {
            CapturedStillImage = captured;

            OnCaptureSuccess(captured);

            IsTakingPhoto = false;
        };

        NativeControl.TakePicture();

        while (IsTakingPhoto)
        {
            await Task.Delay(60);
        }

        IsBusy = false;
    }

    /// <summary>
    /// Flash screen with color
    /// </summary>
    /// <param name="color"></param>
    public virtual void FlashScreen(Color color)
    {
        var layer = new SkiaControl()
        {
            Tag = "Flash",
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = color,
            ZIndex = int.MaxValue,
        };

        layer.SetParent(this);

        layer.FadeToAsync(0).ContinueWith(_ => { layer.Parent = null; });
    }

    /// <summary>
    /// Play shutter sound
    /// </summary>
    public virtual void PlaySound()
    {
        //todo
    }


    /// <summary>
    /// Sets the flash mode for still image capture
    /// </summary>
    /// <param name="mode">Flash mode to use for capture</param>
    public virtual void SetCaptureFlashMode(CaptureFlashMode mode)
    {
        CaptureFlashMode = mode;
    }

    /// <summary>
    /// Gets the current capture flash mode
    /// </summary>
    /// <returns>Current capture flash mode</returns>
    public virtual CaptureFlashMode GetCaptureFlashMode()
    {
        return CaptureFlashMode;
    }

    private static int filenamesCounter = 0;

    /// <summary>
    /// Generate Jpg filename
    /// </summary>
    /// <returns></returns>
    public virtual string GenerateJpgFileName()
    {
        var add = $"{DateTime.Now:MM/dd/yyyy HH:mm:ss}{++filenamesCounter}";
        var filename =
            $"skiacamera-{add.Replace("/", "").Replace(":", "").Replace(" ", "").Replace(",", "").Replace(".", "").Replace("-", "")}.jpg";

        return filename;
    }

    /// <summary>
    /// Save captured bitmap to native gallery
    /// </summary>
    /// <param name="captured"></param>
    /// <param name="reorient"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    public async Task<string> SaveToGalleryAsync(CapturedImage captured, string album = null)
    {
        var filename = GenerateJpgFileName();

        await using var stream = CreateOutputStreamRotated(captured, false);

        using var exifStream = await JpegExifInjector.InjectExifMetadata(stream, captured.Meta);

        var filenameOutput = GenerateJpgFileName();

        var path = await NativeControl.SaveJpgStreamToGallery(exifStream, filename,
            captured.Meta, album);

        if (!string.IsNullOrEmpty(path))
        {
            captured.Path = path;
            Debug.WriteLine(
                $"[SkiaCamera] saved photo: {filenameOutput} exif orientation: {captured.Meta.Orientation}");
            return path;
        }


        Debug.WriteLine($"[SkiaCamera] failed to save photo");
        return null;
    }

    /// <summary>
    /// Gets the list of available cameras on the device
    /// </summary>
    /// <returns>List of available cameras</returns>
    public virtual async Task<List<CameraInfo>> GetAvailableCamerasAsync()
    {
        return await GetAvailableCamerasInternal();
    }

    /// <summary>
    /// Get available capture formats/resolutions for the current camera.
    /// Use with PhotoFormatIndex when PhotoQuality is set to Manual.
    /// Formats are cached when camera is initialized.
    /// </summary>
    /// <returns>List of available capture formats</returns>
    public virtual async Task<List<CaptureFormat>> GetAvailableCaptureFormatsAsync()
    {
#if ONPLATFORM
        // If not cached, detect and cache them
        return await GetAvailableCaptureFormatsPlatform();
#else
        return new List<CaptureFormat>();
#endif
    }

    /// <summary>
    /// Get available video recording formats/resolutions for the current camera.
    /// Use with VideoFormatIndex when VideoQuality is set to Manual.
    /// Formats are cached when camera is initialized.
    /// </summary>
    /// <returns>List of available video formats</returns>
    public virtual async Task<List<VideoFormat>> GetAvailableVideoFormatsAsync()
    {
#if ONPLATFORM
        // If not cached, detect and cache them
        return await GetAvailableVideoFormatsPlatform();
#else
        return new List<VideoFormat>();
#endif
    }

    /// <summary>
    /// Gets the currently selected capture format.
    /// This reflects the format that will be used for still image capture based on
    /// the current PhotoQuality and PhotoFormatIndex settings.
    /// </summary>
    public CaptureFormat CurrentStillCaptureFormat
    {
        get
        {
#if ONPLATFORM
            try
            {
                return NativeControl?.GetCurrentCaptureFormat();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SkiaCamera] Error getting current capture format: {ex.Message}");
            }
#endif
            return null;
        }
    }

    /// <summary>
    /// Gets the currently selected video format.
    /// This reflects the format that will be used for video recording based on
    /// the current VideoQuality and VideoFormatIndex settings.
    /// </summary>
    public VideoFormat CurrentVideoFormat
    {
        get
        {
#if ONPLATFORM
            try
            {
                return NativeControl?.GetCurrentVideoFormat();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SkiaCamera] Error getting current video format: {ex.Message}");
            }
#endif
            return null;
        }
    }

    #region VIDEO RECORDING METHODS

    /// <summary>
    /// Start video recording. Run this in background thread!
    /// Locks the device rotation for the entire recording session.
    /// Uses either native video recording or capture video flow depending on UseCaptureVideoFlow setting.
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StartVideoRecording()
    {
        if (IsBusy || IsRecordingVideo)
            return;

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}");

        IsBusy = true;
        IsRecordingVideo = true;

        // Lock the current device rotation for the entire recording session
        RecordingLockedRotation = DeviceRotation;
        Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

        try
        {
            // Check if using capture video flow
            if (UseCaptureVideoFlow && FrameProcessor != null)
            {
                await StartCaptureVideoFlow();
            }
            else
            {
                // Use existing native video recording
                await StartNativeVideoRecording();
            }
        }
        catch (Exception ex)
        {
            IsRecordingVideo = false;
            IsBusy = false;
            RecordingLockedRotation = -1; // Reset on error
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }

        IsBusy = false;
    }

    private async Task StartNativeVideoRecording()
    {
        // Set up video recording callbacks to handle state synchronization
        NativeControl.VideoRecordingFailed = ex =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsRecordingVideo = false;
                VideoRecordingFailed?.Invoke(this, ex);
            });
        };

        NativeControl.VideoRecordingSuccess = capturedVideo =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsRecordingVideo = false;
                OnVideoRecordingSuccess(capturedVideo);
            });
        };

        NativeControl.VideoRecordingProgress = duration =>
        {
            MainThread.BeginInvokeOnMainThread(() => { OnVideoRecordingProgress(duration); });
        };

#if ONPLATFORM
        await NativeControl.StartVideoRecording();
#endif
    }

    private async Task StartCaptureVideoFlow()
    {
#if WINDOWS
        // Create platform-specific encoder with existing GRContext (GPU path)
        var grContext = (Superview?.CanvasView as SkiaViewAccelerated)?.GRContext;
        _captureVideoEncoder = new WindowsCaptureVideoEncoder(grContext);

        // Generate output path
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var outputPath = Path.Combine(documentsPath, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

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

        // Start encoder
        await _captureVideoEncoder.StartAsync();

        _captureVideoStartTime = DateTime.Now;
        _capturePtsBaseTime = null;

        // Reset diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        _targetFps = fps;

        // Windows uses real-time preview-driven capture (no timer)
        _useWindowsPreviewDrivenCapture = true;

        // Mirroring of composed frames to preview is optional
        UseRecordingFramesForPreview = MirrorRecordingToPreview;

        // Invalidate preview when the encoder publishes a new composed frame (Windows mirror)
        if (MirrorRecordingToPreview && _captureVideoEncoder is WindowsCaptureVideoEncoder _winEncPrev)
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

        // Mirroring of composed frames to preview is optional
        UseRecordingFramesForPreview = MirrorRecordingToPreview;

        // Invalidate preview when the encoder publishes a new composed frame (Android mirror)
        if (MirrorRecordingToPreview && _captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
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

        // Output path in app's Movies dir
        var ctx = Android.App.Application.Context;
        var moviesDir =
            ctx.GetExternalFilesDir(Android.OS.Environment.DirectoryMovies)?.AbsolutePath ??
            ctx.FilesDir?.AbsolutePath ?? ".";
        var outputPath = Path.Combine(moviesDir, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

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

        await _captureVideoEncoder.StartAsync();

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
                    if (!IsRecordingVideo || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
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

                    // PTS from image Time using first frame as monotonic base
                    if (_capturePtsBaseTime == null)
                        _capturePtsBaseTime = captured.Time;
                    var elapsedLocal = captured.Time - _capturePtsBaseTime.Value;
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
                            //if ((diagCounter++ % 30) == 0)

                            //    System.Diagnostics.Debug.WriteLine($"[CAPTURE-DRAW] src={img.Width}x{img.Height} dst={info.Width}x{info.Height} srcRect=({rects.src.Left},{rects.src.Top},{rects.src.Right},{rects.src.Bottom}) dstRect=({rects.dst.Left},{rects.dst.Top},{rects.dst.Right},{rects.dst.Bottom})");

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
        // Create Apple encoder (AVAssetWriter + CVPixelBuffer pool). GPU composition via Skia; reuse buffers.
        _captureVideoEncoder = new AppleCaptureVideoEncoder();

        // Mirror composed frames to preview when enabled
        UseRecordingFramesForPreview = MirrorRecordingToPreview;
        if (MirrorRecordingToPreview && _captureVideoEncoder is AppleCaptureVideoEncoder _appleEncPrev)
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

        // Output path (Documents)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var outputPath = Path.Combine(documentsPath, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

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
        if (_captureVideoEncoder is DrawnUi.Camera.AppleCaptureVideoEncoder appleEncoder)
        {
            await appleEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio, RecordingLockedRotation);
        }
        else
        {
            await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio);
        }

        await _captureVideoEncoder.StartAsync();

        _captureVideoStartTime = DateTime.Now;
        _capturePtsBaseTime = null;

        // Diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
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
        if (!IsRecordingVideo || _captureVideoEncoder == null)
            return;

        // Make sure we never queue more than one frame — drop if previous is still processing
        if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) != 0)
        {
            System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            return;
        }

        try
        {
            var elapsed = DateTime.Now - _captureVideoStartTime;

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
                    return;
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
            // GPU path on Apple: draw via Skia into AVAssetWriter's pixel buffer
            if (_captureVideoEncoder is DrawnUi.Camera.AppleCaptureVideoEncoder appleEnc)
            {
                using var previewImage = NativeControl?.GetPreviewImage();
                if (previewImage == null)
                {
                    Debug.WriteLine("[CaptureFrame] No preview image available from camera");
                    return;
                }

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
                            Width = frameWidth, Height = frameHeight, Canvas = canvas, Time = elapsed
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

            // Fallback (non-Windows/Android or encoder without GPU path): CPU composition
            /*
            using var previewImage2 = NativeControl?.GetPreviewImage();
            if (previewImage2 == null)
            {
                Debug.WriteLine("[CaptureFrame] No preview image available from camera");
                return;
            }

            using var previewBitmap = SKBitmap.FromImage(previewImage2);
            if (previewBitmap == null)
                return;

            using var finalBitmap = new SKBitmap(previewBitmap.Width, previewBitmap.Height);
            using var cpuCanvas = new SKCanvas(finalBitmap);

            cpuCanvas.DrawBitmap(previewBitmap, 0, 0);

            if (FrameProcessor != null)
            {
                // Apply rotation based on device orientation
                var rotation = GetActiveRecordingRotation();
                cpuCanvas.Save();
                ApplyCanvasRotation(cpuCanvas, previewBitmap.Width, previewBitmap.Height, rotation);

                var (frameWidth, frameHeight) = GetRotatedDimensions(previewBitmap.Width, previewBitmap.Height, rotation);
                var frame = new DrawableFrame
                {
                    Width = frameWidth, Height = frameHeight, Canvas = cpuCanvas, Time = elapsed
                };
                FrameProcessor?.Invoke(frame);

                cpuCanvas.Restore();
            }

            if (VideoDiagnosticsOn)
            {
                DrawDiagnostics(cpuCanvas, previewBitmap.Width, previewBitmap.Height);
            }

            var __sw2 = System.Diagnostics.Stopwatch.StartNew();
            await _captureVideoEncoder.AddFrameAsync(finalBitmap, elapsed);
            __sw2.Stop();
            _diagLastSubmitMs = __sw2.Elapsed.TotalMilliseconds;
            System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
            */
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CaptureFrame] Error: {ex.Message}");
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
        if (!IsRecordingVideo)
            return;

        Debug.WriteLine($"[StopVideoRecording] IsMainThread {MainThread.IsMainThread}");

        IsRecordingVideo = false;

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
        }
        catch (Exception ex)
        {
            // On immediate exception, set the state and invoke the event
            IsRecordingVideo = false;
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }
    }

    private async Task StopCaptureVideoFlow()
    {
        ICaptureVideoEncoder encoder = null;

        try
        {
            // Stop frame capture timer (if any)
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
#if WINDOWS
            _useWindowsPreviewDrivenCapture = false;
#endif

#if ANDROID
            // Detach Android mirror event
            if (encoder is AndroidCaptureVideoEncoder _droidEncPrev)
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
            var capturedVideo = await encoder?.StopAsync();

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
    /// Move captured video from temporary location to public gallery (faster than SaveVideoToGalleryAsync)
    /// </summary>
    /// <param name="capturedVideo">The captured video to move</param>
    /// <param name="album">Optional album name</param>
    /// <param name="deleteOriginal">Whether to delete the original file after successful move (default true)</param>
    /// <returns>Gallery path if successful, null if failed</returns>
    public async Task<string> MoveVideoToGalleryAsync(CapturedVideo capturedVideo, string album = null,
        bool deleteOriginal = true)
    {
        if (capturedVideo == null || string.IsNullOrEmpty(capturedVideo.FilePath) ||
            !File.Exists(capturedVideo.FilePath))
            return null;

        try
        {
            Debug.WriteLine($"[SkiaCamera] Moving video to gallery: {capturedVideo.FilePath}");

#if ANDROID
            return await MoveVideoToGalleryAndroid(capturedVideo.FilePath, album, deleteOriginal);
#elif IOS || MACCATALYST
            return await MoveVideoToGalleryApple(capturedVideo.FilePath, album, deleteOriginal);
#elif WINDOWS
            return await MoveVideoToGalleryWindows(capturedVideo.FilePath, album, deleteOriginal);
#else
            Debug.WriteLine("[SkiaCamera] MoveVideoToGalleryAsync not implemented for this platform");
            return null;
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera] Failed to move video to gallery: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region VIDEO RECORDING EVENTS

    /// <summary>
    /// Fired when video recording completes successfully
    /// </summary>
    public event EventHandler<CapturedVideo> VideoRecordingSuccess;

    /// <summary>
    /// Fired when video recording fails
    /// </summary>
    public event EventHandler<Exception> VideoRecordingFailed;

    /// <summary>
    /// Fired when video recording progress updates
    /// </summary>
    public event EventHandler<TimeSpan> VideoRecordingProgress;

    /// <summary>
    /// Internal method to raise VideoRecordingSuccess event
    /// </summary>
    internal void OnVideoRecordingSuccess(CapturedVideo capturedVideo)
    {
        CurrentRecordingDuration = TimeSpan.Zero;
        VideoRecordingSuccess?.Invoke(this, capturedVideo);
    }

    /// <summary>
    /// Internal method to raise VideoRecordingProgress event
    /// </summary>
    internal void OnVideoRecordingProgress(TimeSpan duration)
    {
        CurrentRecordingDuration = duration;
        VideoRecordingProgress?.Invoke(this, duration);
    }

    #endregion

    /// <summary>
    /// Internal method to get available cameras with caching
    /// </summary>
    protected virtual async Task<List<CameraInfo>> GetAvailableCamerasInternal(bool refresh = false)
    {
#if ONPLATFORM
        return await GetAvailableCamerasPlatform(refresh);
#endif

        return new List<CameraInfo>();
    }

    #endregion

    #region METHODS

    /// <summary>
    /// Stops the camera by setting IsOn to false
    /// </summary>
    public virtual void Stop()
    {
        IsOn = false;
    }

    /// <summary>
    /// Stops the camera immediately and releases native camera resources
    /// </summary>
    /// <param name="force">If true, forces immediate stop regardless of state</param>
    public virtual void StopInternal(bool force = false)
    {
        if (IsDisposing || IsDisposed)
            return;

        System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {Uid} {Tag}");

        NativeControl?.Stop(force);
        State = CameraState.Off;
        //DisplayImage.IsVisible = false;
    }


    /// <summary>
    /// Override this method to customize DisplayInfo content
    /// </summary>
    public virtual void UpdateInfo()
    {
        var info = $"Position: {Facing}" +
                   $"\nState: {State}" +
                   //$"\nSize: {Width}x{Height} pts" +
                   $"\nPreview: {PreviewSize} px" +
                   $"\nPhoto: {CapturePhotoSize} px" +
                   $"\nRotation: {this.DeviceRotation}";

        if (Display != null)
        {
            info += $"\nAspect: {Display.Aspect}";
        }

        DisplayInfo = info;
    }

    /// <summary>
    /// Creates an output stream from a captured image with optional rotation correction
    /// </summary>
    /// <param name="captured">The captured image to encode</param>
    /// <param name="reorient">If true, applies rotation correction before encoding</param>
    /// <param name="format">Output image format (default: JPEG)</param>
    /// <param name="quality">Encoding quality 0-100 (default: 90)</param>
    /// <returns>Stream containing the encoded image</returns>
    public Stream CreateOutputStreamRotated(CapturedImage captured,
        bool reorient,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg,
        int quality = 90)
    {
        try
        {
            SKBitmap skBitmap = SKBitmap.FromImage(captured.Image);
            if (reorient)
            {
                skBitmap = Reorient(skBitmap, captured.Rotation);
            }

            Debug.WriteLine($"[SkiaCamera] Saving bitmap {skBitmap.Width}x{skBitmap.Height}");

            var data = skBitmap.Encode(format, quality);
            return data.AsStream();
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            return null;
        }
    }

    #endregion

    #region ENGINE

    protected virtual void OnCaptureSuccess(CapturedImage captured)
    {
        CaptureSuccess?.Invoke(this, captured);
    }

    protected virtual void OnCaptureFailed(Exception ex)
    {
        CaptureFailed?.Invoke(this, ex);
    }

    private int _frameInFlight = 0;

    /// <summary>
    /// Enable on-screen diagnostics overlay (effective FPS, dropped frames, last submit ms)
    /// during capture video flow to validate performance.
    /// </summary>
    public bool EnableCaptureDiagnostics { get; set; } = true;

    private long _diagDroppedFrames = 0;
    private long _diagSubmittedFrames = 0;
    private double _diagLastSubmitMs = 0;
    private DateTime _diagStartTime;
    private int _targetFps = 0;
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
            Color = SKColors.White, IsAntialias = true, TextSize = Math.Max(14, width / 60f)
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


    public INativeCamera NativeControl;

    private ICaptureVideoEncoder _captureVideoEncoder;
    private System.Threading.Timer _frameCaptureTimer;
    private DateTime _captureVideoStartTime;
#if WINDOWS
    private bool _useWindowsPreviewDrivenCapture;
#endif
#if WINDOWS || ANDROID || IOS || MACCATALYST
    private EventHandler _encoderPreviewInvalidateHandler;
#endif
    private DateTime? _capturePtsBaseTime; // base timestamp for PTS (from first captured frame)


#if ANDROID
    private int _androidFrameGate; // 0 = free, 1 = in-flight
    private int _androidWarmupDropRemaining; // drop first N frames to avoid initial garbage frame
    private System.Action<CapturedImage> _androidPreviewHandler;
#endif


    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        if (State == CameraState.Error)
            StartInternal();
    }

    bool subscribed;

    /// <summary>
    /// Called when the superview (parent container) changes.
    /// Subscribes to orientation change events from the superview.
    /// </summary>
    public override void SuperViewChanged()
    {
        if (Superview != null && !subscribed)
        {
            subscribed = true;
            Superview.OrientationChanged += DeviceOrientationChanged;
        }

        base.SuperViewChanged();
    }

    /// <summary>
    /// Updates the camera orientation from the current device rotation
    /// </summary>
    public virtual void UpdateOrientationFromDevice()
    {
        //var rotation = ((orientation + 45) / 90) * 90 % 360;

        DeviceRotation = Super.DeviceRotationSnap;

        Debug.WriteLine($"[CAMERA] DeviceRotation: {DeviceRotation}");
    }

    private void DeviceOrientationChanged(object sender, DeviceOrientation deviceOrientation)
    {
        UpdateOrientationFromDevice();
    }

    private int _DeviceRotation = -1;

    /// <summary>
    /// Gets or sets the current device rotation in degrees (0, 90, 180, 270).
    /// Automatically applies the orientation to the native camera when changed.
    /// </summary>
    public int DeviceRotation
    {
        get { return _DeviceRotation; }
        set
        {
            if (_DeviceRotation != value)
            {
                _DeviceRotation = value;
                OnPropertyChanged();
                NativeControl?.ApplyDeviceOrientation(value);
                UpdateInfo();
            }
        }
    }

    /// <summary>
    /// Rotation locked when video recording started. Used throughout recording to ensure consistent orientation.
    /// </summary>
    public int RecordingLockedRotation { get; private set; } = -1;

    /// <summary>
    /// Gets the rotation to use for video recording - returns locked rotation during recording, current rotation otherwise.
    /// </summary>
    protected int GetActiveRecordingRotation()
    {
        return IsRecordingVideo && RecordingLockedRotation >= 0 ? RecordingLockedRotation : DeviceRotation;
    }

    object lockFrame = new();

    /// <summary>
    /// Gets or sets whether a new camera frame has been acquired and is ready for display
    /// </summary>
    public bool FrameAquired { get; set; }

    /// <summary>
    /// Updates the camera preview display. Called when a new frame is available from the native camera.
    /// Handles frame submission for video capture flow if recording is active.
    /// </summary>
    public virtual void UpdatePreview()
    {
        FrameAquired = false;
        NeedUpdate = false;
        Update();

#if WINDOWS
        // If using capture video flow and preview-driven capture, submit frames in real-time with the preview
        if (_useWindowsPreviewDrivenCapture && IsRecordingVideo &&
            _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // One frame in flight policy: drop if busy
            //if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) == 0)
            {
                SafeAction(async () =>
                {
                    try
                    {
                        var elapsed = DateTime.Now - _captureVideoStartTime;
                        using var previewImage = NativeControl?.GetPreviewImage();
                        if (previewImage == null)
                            return;

                        using (winEnc.BeginFrame(elapsed, out var canvas, out var info))
                        {
                            var __rects3 =
                                GetAspectFillRects(previewImage.Width, previewImage.Height, info.Width, info.Height);
                            canvas.DrawImage(previewImage, __rects3.src, __rects3.dst);

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

                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        await winEnc.SubmitFrameAsync();
                        sw.Stop();
                        _diagLastSubmitMs = sw.Elapsed.TotalMilliseconds;
                        System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdatePreview Capture] {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
                    }
                });
            }
            //else
            //{
            //    System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            //}
        }
#endif
    }

    /// <summary>
    /// Gets the SKSurface used for frame rendering operations
    /// </summary>
    public SKSurface FrameSurface { get; protected set; }

    /// <summary>
    /// Gets the image info for the frame surface
    /// </summary>
    public SKImageInfo FrameSurfaceInfo { get; protected set; }

    //public bool AllocatedFrameSurface(int width, int height)
    //{
    //    if (Superview == null || width == 0 || height == 0)
    //    {
    //        return false;
    //    }

    //    var kill = FrameSurface;
    //    FrameSurfaceInfo = new SKImageInfo(width, height);

    //    if (Superview.CanvasView is SkiaViewAccelerated accelerated)
    //    {
    //        FrameSurface = SKSurface.Create(accelerated.GRContext, true, FrameSurfaceInfo);
    //    }
    //    else
    //    {
    //        //normal one
    //        FrameSurface = SKSurface.Create(FrameSurfaceInfo);
    //    }

    //    kill?.Dispose();

    //    return true;
    //}

    protected virtual void OnNewFrameSet(LoadedImageSource source)
    {
        NewPreviewSet?.Invoke(this, source);
    }

    protected virtual SKImage AquireFrameFromNative()
    {
        // If we are recording and not mirroring encoder frames to preview, suppress raw preview updates
        if (IsRecordingVideo && !UseRecordingFramesForPreview)
            return null;

#if WINDOWS
        if (IsRecordingVideo && UseRecordingFramesToPreview &&
            _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // Only show frames that were actually composed for recording.
            // If none is available yet, return null so the previous displayed frame stays,
            // avoiding a fallback blink from the raw preview without overlay.
            if (winEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose

            return null; // do NOT fallback to raw preview during recording
        }
#elif ANDROID
        // While recording on Android, mirror the composed encoder frames into the preview (no second camera feed)
        if (IsRecordingVideo && UseRecordingFramesToPreview &&
            _captureVideoEncoder is AndroidCaptureVideoEncoder droidEnc)
        {
            if (droidEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose
            return null; // no fallback to raw preview during recording
        }
#elif IOS || MACCATALYST
        // While recording on Apple, mirror the composed encoder frames into the preview
        if (IsRecordingVideo && UseRecordingFramesToPreview &&
            _captureVideoEncoder is DrawnUi.Camera.AppleCaptureVideoEncoder appleEnc)
        {
            if (appleEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose
            return null; // no fallback to raw preview during recording
        }
#endif
        return NativeControl.GetPreviewImage();
    }

    protected virtual void SetFrameFromNative()
    {
        if (NativeControl != null && !FrameAquired)
        {
            //acquire latest image from camera
            var image = AquireFrameFromNative();
            if (image != null)
            {
                FrameAquired = true;
                OnNewFrameSet(Display.SetImageInternal(image, false));
            }
        }
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        if (State == CameraState.On)
        {
            SetFrameFromNative();
        }

        DrawViews(ctx);

        if (ConstantUpdate && State == CameraState.On)
        {
            Update();
        }
    }

    #endregion

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN)
    /// <summary>
    /// Gets a bitmap of the current preview frame (not implemented on this platform)
    /// </summary>
    /// <returns>Preview bitmap</returns>
    public SKBitmap GetPreviewBitmap()
    {
        throw new NotImplementedException();
    }


#endif


    bool lockStartup;

    /// <summary>
    /// Starts the camera by setting IsOn to true.
    /// The actual camera initialization and permission handling happens automatically.
    /// </summary>
    public virtual void Start()
    {
        IsOn = true; //haha
    }

    private static void PowerChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StopInternal(true);
            if (control.IsOn)
            {
                control.StartWithPermissionsInternal();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether a permissions error occurred
    /// </summary>
    public bool PermissionsError { get; set; }

    /// <summary>
    /// Request permissions and start camera without setting IsOn true. Will set IsOn to false if permissions denied.
    /// </summary>
    public virtual void StartWithPermissionsInternal()
    {
        if (lockStartup)
        {
            Debug.WriteLine("[SkiaCamera] Startup locked.");
            return;
        }

        lockStartup = true;

        try
        {
            Debug.WriteLine("[SkiaCamera] Requesting permissions...");

            SkiaCamera.CheckPermissions((presented) =>
                {
                    Debug.WriteLine("[SkiaCamera] Starting..");
                    PermissionsWarning = false;
                    PermissionsError = false;
                    StartInternal();

                    //if (Geotag)
                    //	CommandGetLocation.Execute(null);
                    //else
                    //{
                    //	CanDetectLocation = false;
                    //}
                },
                (presented) =>
                {
                    Super.Log("[SkiaCamera] Permissions denied");
                    IsOn = false;
                    PermissionsWarning = true;
                    PermissionsError = true;
                });
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
        finally
        {
            Tasks.StartDelayed(TimeSpan.FromSeconds(1), () =>
            {
                Debug.WriteLine("[SkiaCamera] Startup UNlocked.");
                lockStartup = false;
            });
        }
    }

    /// <summary>
    /// Starts the camera after permissions where acquired
    /// </summary>
    protected virtual void StartInternal()
    {
        if (IsDisposing || IsDisposed)
            return;

        if (NativeControl == null)
        {
#if ONPLATFORM
            CreateNative();
            OnNativeControlCreated();
#endif
        }

        //var rotation = ((Superview.DeviceRotation + 45) / 90) % 4;
        //NativeControl?.ApplyDeviceOrientation(rotation);

        if (Display != null)
        {
            //DestroyRenderingObject();
            Display.IsVisible = true;
        }

        //IsOn = true;

        NativeControl?.Start();
    }

    /// <summary>
    /// Called after native control is created to notify property changes
    /// </summary>
    protected virtual void OnNativeControlCreated()
    {
        // Notify that flash capability properties may have changed
        OnPropertyChanged(nameof(IsFlashSupported));
        OnPropertyChanged(nameof(IsAutoFlashSupported));

        // Apply current flash modes to native control
        if (NativeControl != null)
        {
            NativeControl.SetFlashMode(FlashMode);
            NativeControl.SetCaptureFlashMode(CaptureFlashMode);
            NativeControl.EnablePreRecording = EnablePreRecording;
            NativeControl.PreRecordDuration = PreRecordDuration;
        }
    }

    #region SkiaCamera xam control

    private bool _PermissionsWarning;

    /// <summary>
    /// Gets or sets whether a permissions warning is active (permissions need to be granted)
    /// </summary>
    public bool PermissionsWarning
    {
        get { return _PermissionsWarning; }
        set
        {
            if (_PermissionsWarning != value)
            {
                _PermissionsWarning = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Represents a queued picture waiting to be processed
    /// </summary>
    public class CameraQueuedPictured
    {
        /// <summary>
        /// Gets or sets the filename for this queued picture
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Gets or sets the sensor rotation angle in degrees
        /// </summary>
        public double SensorRotation { get; set; }

        /// <summary>
        /// Set by renderer after work
        /// </summary>
        public bool Processed { get; set; }
    }

    /// <summary>
    /// Queue for managing pictures waiting to be processed
    /// </summary>
    public class CameraPicturesQueue : Queue<CameraQueuedPictured>
    {
    }


    private bool _IsTakingPhoto;

    /// <summary>
    /// Gets whether the camera is currently taking a still photo
    /// </summary>
    public bool IsTakingPhoto
    {
        get { return _IsTakingPhoto; }
        set
        {
            if (_IsTakingPhoto != value)
            {
                _IsTakingPhoto = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the queue of pictures waiting to be processed
    /// </summary>
    public CameraPicturesQueue PicturesQueue { get; } = new CameraPicturesQueue();


    #region PERMISSIONS

    protected static bool ChecksBusy = false;

    private static DateTime lastTimeChecked = DateTime.MinValue;

    /// <summary>
    /// Gets whether camera permissions have been granted
    /// </summary>
    public static bool PermissionsGranted { get; protected set; }

    /// <summary>
    /// Checks gallery/camera permissions and invokes the appropriate callback
    /// </summary>
    /// <param name="granted">Action to invoke if permissions are granted</param>
    /// <param name="notGranted">Action to invoke if permissions are denied</param>
    public static void CheckGalleryPermissions(Action granted, Action notGranted)
    {
        if (lastTimeChecked + TimeSpan.FromSeconds(5) < DateTime.Now) //avoid spam
        {
            lastTimeChecked = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (ChecksBusy)
                    return;

                bool okay1 = false;


                ChecksBusy = true;
                // Update the UI
                try
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<Permissions.Camera>();


                        if (status == PermissionStatus.Granted)
                        {
                            okay1 = true;
                        }
                    }
                    else
                    {
                        okay1 = true;
                    }


                    // Could prompt to enable in settings if needed
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
                finally
                {
                    if (okay1)
                    {
                        PermissionsGranted = true;
                        granted?.Invoke();
                    }
                    else
                    {
                        PermissionsGranted = false;
                        notGranted?.Invoke();
                    }

                    ChecksBusy = false;
                }
            });
        }
    }

    private bool _GpsBusy;

    public bool GpsBusy
    {
        get { return _GpsBusy; }
        set
        {
            if (_GpsBusy != value)
            {
                _GpsBusy = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLat;

    public double LocationLat
    {
        get { return _LocationLat; }
        set
        {
            if (_LocationLat != value)
            {
                _LocationLat = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLon;

    public double LocationLon
    {
        get { return _LocationLon; }
        set
        {
            if (_LocationLon != value)
            {
                _LocationLon = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _CanDetectLocation;

    public bool CanDetectLocation
    {
        get { return _CanDetectLocation; }
        set
        {
            if (_CanDetectLocation != value)
            {
                _CanDetectLocation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Safe and if CanDetectLocation
    /// </summary>
    /// <returns></returns>
    public async Task RefreshLocation(int msTimeout)
    {
        if (CanDetectLocation)
        {
            //my ACTUAL location
            try
            {
                GpsBusy = true;

                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var cancel = new CancellationTokenSource();
                cancel.CancelAfter(msTimeout);
                var location = await Geolocation.GetLocationAsync(request, cancel.Token);

                if (location != null)
                {
                    Debug.WriteLine(
                        $"ACTUAL Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

                    this.LocationLat = location.Latitude;
                    this.LocationLon = location.Longitude;
                }
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // Handle not supported on device exception
                //Toast.ShortMessage("GPS не поддерживается на устройстве");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Handle not enabled on device exception
                //Toast.ShortMessage("GPS отключен на устройстве");
            }
            catch (PermissionException pEx)
            {
                // Handle permission exception
                //Toast.ShortMessage("Вы не дали разрешение на использование GPS");
            }
            catch (Exception ex)
            {
                // Unable to get location
            }
            finally
            {
                GpsBusy = false;
            }
        }
    }

    //public ICommand CommandGetLocation
    //{
    //	get
    //	{
    //		return new Command((object context) =>
    //		{
    //			if (GpsBusy || !App.Native.CheckGpsEnabled())
    //				return;

    //			MainThread.BeginInvokeOnMainThread(async () =>
    //			{
    //				// Update the UI
    //				try
    //				{
    //					GpsBusy = true;

    //					var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    //					if (status != PermissionStatus.Granted)
    //					{
    //						CanDetectLocation = false;

    //						await App.Current.MainPage.DisplayAlert(Core.Current.MyCompany.Name, ResStrings.X_NeedMoreForGeo, ResStrings.ButtonOk);

    //						status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    //						if (status != PermissionStatus.Granted)
    //						{
    //							// Additionally could prompt the user to turn on in settings
    //							return;
    //						}
    //						else
    //						{
    //							CanDetectLocation = true;
    //						}
    //					}
    //					else
    //					{
    //						CanDetectLocation = true;
    //					}

    //					if (CanDetectLocation)
    //					{
    //						//my LAST location:
    //						try
    //						{
    //							if (App.Native.CheckGpsEnabled())
    //							{
    //								var location = await Geolocation.GetLastKnownLocationAsync();

    //								if (location != null)
    //								{
    //									Debug.WriteLine(
    //										$"LAST Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

    //									LocationLat = location.Latitude;
    //									LocationLon = location.Longitude;
    //								}
    //							}
    //						}
    //						catch (FeatureNotSupportedException fnsEx)
    //						{
    //							// Handle not supported on device exception
    //							//Toast.ShortMessage("GPS не поддерживается на устройстве");
    //						}
    //						catch (FeatureNotEnabledException fneEx)
    //						{
    //							// Handle not enabled on device exception
    //							//Toast.ShortMessage("GPS отключен на устройстве");
    //						}
    //						catch (PermissionException pEx)
    //						{
    //							// Handle permission exception
    //							//Toast.ShortMessage("Вы не дали разрешение на использование GPS");
    //						}
    //						catch (Exception ex)
    //						{
    //							// Unable to get location
    //						}

    //						await Task.Run(async () =>
    //						{
    //							await RefreshLocation(1200);

    //						}).ConfigureAwait(false);

    //					}
    //					else
    //					{
    //						GpsBusy = false;
    //					}


    //				}
    //				catch (Exception ex)
    //				{
    //					//Something went wrong
    //					Trace.WriteLine(ex);
    //					CanDetectLocation = false;
    //					GpsBusy = false;
    //				}
    //				finally
    //				{

    //				}

    //			});


    //		});
    //	}
    //}

    /// <summary>
    /// Will pass the fact if permissions dialog was diplayed as bool
    /// </summary>
    /// <param name="granted"></param>
    public static void CheckPermissions(Action<bool> granted, Action<bool> notGranted)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (ChecksBusy)
                return;

            bool grantedCam = false;
            bool grantedStorage = false;
            bool presented = false;

            ChecksBusy = true;
            // Update the UI
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    presented = true;

                    status = await Permissions.RequestAsync<Permissions.Camera>();
 

                    if (status == PermissionStatus.Granted)
                    {
                        grantedCam = true;
                    }
                }
                else
                {
                    grantedCam = true;
                }

                var needStorage = true;
                if (Device.RuntimePlatform == Device.Android && DeviceInfo.Version.Major > 9)
                {
                    needStorage = false;
                }

                if (needStorage)
                {
                    status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    if (status != PermissionStatus.Granted)
                    {
                        presented = true;

                        status = await Permissions.RequestAsync<Permissions.StorageWrite>();

                        if (status == PermissionStatus.Granted)
                        {
                            grantedStorage = true;
                        }
                    }
                    else
                    {
                        grantedStorage = true;
                    }
                }
                else
                {
                    grantedStorage = true;
                }


                // Could prompt to enable in settings if needed
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            finally
            {
                if (grantedCam && grantedStorage)
                {
                    PermissionsGranted = true;
                    granted?.Invoke(presented);
                }
                else
                {
                    PermissionsGranted = false;
                    notGranted?.Invoke(presented);
                }

                ChecksBusy = false;
            }
        });
    }

    private bool _GpsBusy;

    public bool GpsBusy
    {
        get { return _GpsBusy; }
        set
        {
            if (_GpsBusy != value)
            {
                _GpsBusy = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLat;

    public double LocationLat
    {
        get { return _LocationLat; }
        set
        {
            if (_LocationLat != value)
            {
                _LocationLat = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLon;

    public double LocationLon
    {
        get { return _LocationLon; }
        set
        {
            if (_LocationLon != value)
            {
                _LocationLon = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _CanDetectLocation;

    public bool CanDetectLocation
    {
        get { return _CanDetectLocation; }
        set
        {
            if (_CanDetectLocation != value)
            {
                _CanDetectLocation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Safe and if CanDetectLocation
    /// </summary>
    /// <returns></returns>
    public async Task RefreshLocation(int msTimeout)
    {
        if (CanDetectLocation)
        {
            //my ACTUAL location
            try
            {
                GpsBusy = true;

                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var cancel = new CancellationTokenSource();
                cancel.CancelAfter(msTimeout);
                var location = await Geolocation.GetLocationAsync(request, cancel.Token);

                if (location != null)
                {
                    Debug.WriteLine(
                        $"ACTUAL Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

                    this.LocationLat = location.Latitude;
                    this.LocationLon = location.Longitude;
                }
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // Handle not supported on device exception
                //Toast.ShortMessage("GPS не поддерживается на устройстве");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Handle not enabled on device exception
                //Toast.ShortMessage("GPS отключен на устройстве");
            }
            catch (PermissionException pEx)
            {
                // Handle permission exception
                //Toast.ShortMessage("Вы не дали разрешение на использование GPS");
            }
            catch (Exception ex)
            {
                // Unable to get location
            }
            finally
            {
                GpsBusy = false;
            }
        }
    }

    //public ICommand CommandGetLocation
    //{
    //	get
    //	{
    //		return new Command((object context) =>
    //		{
    //			if (GpsBusy || !App.Native.CheckGpsEnabled())
    //				return;

    //			MainThread.BeginInvokeOnMainThread(async () =>
    //			{
    //				// Update the UI
    //				try
    //				{
    //					GpsBusy = true;

    //					var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    //					if (status != PermissionStatus.Granted)
    //					{
    //						CanDetectLocation = false;

    //						await App.Current.MainPage.DisplayAlert(Core.Current.MyCompany.Name, ResStrings.X_NeedMoreForGeo, ResStrings.ButtonOk);

    //						status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    //						if (status != PermissionStatus.Granted)
    //						{
    //							// Additionally could prompt the user to turn on in settings
    //							return;
    //						}
    //						else
    //						{
    //							CanDetectLocation = true;
    //						}
    //					}
    //					else
    //					{
    //						CanDetectLocation = true;
    //					}

    //					if (CanDetectLocation)
    //					{
    //						//my LAST location:
    //						try
    //						{
    //							if (App.Native.CheckGpsEnabled())
    //							{
    //								var location = await Geolocation.GetLastKnownLocationAsync();

    //								if (location != null)
    //								{
    //									Debug.WriteLine(
    //										$"LAST Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

    //									LocationLat = location.Latitude;
    //									LocationLon = location.Longitude;
    //								}
    //							}
    //						}
    //						catch (FeatureNotSupportedException fnsEx)
    //						{
    //							// Handle not supported on device exception
    //							//Toast.ShortMessage("GPS не поддерживается на устройстве");
    //						}
    //						catch (FeatureNotEnabledException fneEx)
    //						{
    //							// Handle not enabled on device exception
    //							//Toast.ShortMessage("GPS отключен на устройстве");
    //						}
    //						catch (PermissionException pEx)
    //						{
    //							// Handle permission exception
    //							//Toast.ShortMessage("Вы не дали разрешение на использование GPS");
    //						}
    //						catch (Exception ex)
    //						{
    //							// Unable to get location
    //						}

    //						await Task.Run(async () =>
    //						{
    //							await RefreshLocation(1200);

    //						}).ConfigureAwait(false);

    //					}
    //					else
    //					{
    //						GpsBusy = false;
    //					}


    //				}
    //				catch (Exception ex)
    //				{
    //					//Something went wrong
    //					Trace.WriteLine(ex);
    //					CanDetectLocation = false;
    //					GpsBusy = false;
    //				}
    //				finally
    //				{

    //				}

    //			});


    //		});
    //	}
    //}

    /// <summary>
    /// Enable pre-recording, which buffers frames before recording starts.
    /// </summary>
    public bool EnablePreRecording
    {
        get => (bool)GetValue(EnablePreRecordingProperty);
        set => SetValue(EnablePreRecordingProperty, value);
    }

    public static readonly BindableProperty EnablePreRecordingProperty = BindableProperty.Create(
        nameof(EnablePreRecording), typeof(bool), typeof(SkiaCamera), false,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is SkiaCamera control && control.NativeControl != null)
            {
                control.NativeControl.EnablePreRecording = (bool)newValue;
            }
        });

    /// <summary>
    /// Gets or sets the duration of the pre-recording buffer. Default is 5 seconds.
    /// </summary>
    public TimeSpan PreRecordDuration
    {
        get => (TimeSpan)GetValue(PreRecordDurationProperty);
        set => SetValue(PreRecordDurationProperty, value);
    }

    public static readonly BindableProperty PreRecordDurationProperty = BindableProperty.Create(
        nameof(PreRecordDuration), typeof(TimeSpan), typeof(SkiaCamera), TimeSpan.FromSeconds(5),
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is SkiaCamera control && control.NativeControl != null)
            {
                control.NativeControl.PreRecordDuration = (TimeSpan)newValue;
            }
        });

    /// <summary>
    /// Gets or sets whether the camera is currently recording video
    /// </summary>
    public bool IsRecordingVideo
    {
        get => _isRecordingVideo;
        set
        {
            if (_isRecordingVideo != value)
            {
                _isRecordingVideo = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isRecordingVideo;

    /// <summary>
    /// Notify property changed for IsRecordingVideo
    /// </summary>
    protected void OnIsRecordingVideoChanged()
    {
        OnPropertyChanged(nameof(IsRecordingVideo));
    }

    /// <summary>
    /// Notify property changed for EnablePreRecording
    /// </summary>
    protected void OnEnablePreRecordingChanged()
    {
        OnPropertyChanged(nameof(EnablePreRecording));
    }

    /// <summary>
    /// Notify property changed for PreRecordDuration
    /// </summary>
    protected void OnPreRecordDurationChanged()
    {
        OnPropertyChanged(nameof(PreRecordDuration));
    }

    /// <summary>
    /// Internal method to raise VideoRecordingSuccess event
    /// </summary>
    internal void OnVideoRecordingSuccess(CapturedVideo capturedVideo)
    {
        CurrentRecordingDuration = TimeSpan.Zero;
        VideoRecordingSuccess?.Invoke(this, capturedVideo);
    }

    /// <summary>
    /// Internal method to raise VideoRecordingProgress event
    /// </summary>
    internal void OnVideoRecordingProgress(TimeSpan duration)
    {
        CurrentRecordingDuration = duration;
        VideoRecordingProgress?.Invoke(this, duration);
    }

    #endregion

    /// <summary>
    /// Internal method to get available cameras with caching
    /// </summary>
    protected virtual async Task<List<CameraInfo>> GetAvailableCamerasInternal(bool refresh = false)
    {
#if ONPLATFORM
        return await GetAvailableCamerasPlatform(refresh);
#endif

        return new List<CameraInfo>();
    }

    #endregion

    #region METHODS

    /// <summary>
    /// Stops the camera by setting IsOn to false
    /// </summary>
    public virtual void Stop()
    {
        IsOn = false;
    }

    /// <summary>
    /// Stops the camera immediately and releases native camera resources
    /// </summary>
    /// <param name="force">If true, forces immediate stop regardless of state</param>
    public virtual void StopInternal(bool force = false)
    {
        if (IsDisposing || IsDisposed)
            return;

        System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {Uid} {Tag}");

        NativeControl?.Stop(force);
        State = CameraState.Off;
        //DisplayImage.IsVisible = false;
    }


    /// <summary>
    /// Override this method to customize DisplayInfo content
    /// </summary>
    public virtual void UpdateInfo()
    {
        var info = $"Position: {Facing}" +
                   $"\nState: {State}" +
                   //$"\nSize: {Width}x{Height} pts" +
                   $"\nPreview: {PreviewSize} px" +
                   $"\nPhoto: {CapturePhotoSize} px" +
                   $"\nRotation: {this.DeviceRotation}";

        if (Display != null)
        {
            info += $"\nAspect: {Display.Aspect}";
        }

        DisplayInfo = info;
    }

    /// <summary>
    /// Creates an output stream from a captured image with optional rotation correction
    /// </summary>
    /// <param name="captured">The captured image to encode</param>
    /// <param name="reorient">If true, applies rotation correction before encoding</param>
    /// <param name="format">Output image format (default: JPEG)</param>
    /// <param name="quality">Encoding quality 0-100 (default: 90)</param>
    /// <returns>Stream containing the encoded image</returns>
    public Stream CreateOutputStreamRotated(CapturedImage captured,
        bool reorient,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg,
        int quality = 90)
    {
        try
        {
            SKBitmap skBitmap = SKBitmap.FromImage(captured.Image);
            if (reorient)
            {
                skBitmap = Reorient(skBitmap, captured.Rotation);
            }

            Debug.WriteLine($"[SkiaCamera] Saving bitmap {skBitmap.Width}x{skBitmap.Height}");

            var data = skBitmap.Encode(format, quality);
            return data.AsStream();
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            return null;
        }
    }

    #endregion

    #region ENGINE

    protected virtual void OnCaptureSuccess(CapturedImage captured)
    {
        CaptureSuccess?.Invoke(this, captured);
    }

    protected virtual void OnCaptureFailed(Exception ex)
    {
        CaptureFailed?.Invoke(this, ex);
    }

    private int _frameInFlight = 0;

    /// <summary>
    /// Enable on-screen diagnostics overlay (effective FPS, dropped frames, last submit ms)
    /// during capture video flow to validate performance.
    /// </summary>
    public bool EnableCaptureDiagnostics { get; set; } = true;

    private long _diagDroppedFrames = 0;
    private long _diagSubmittedFrames = 0;
    private double _diagLastSubmitMs = 0;
    private DateTime _diagStartTime;
    private int _targetFps = 0;
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
            Color = SKColors.White, IsAntialias = true, TextSize = Math.Max(14, width / 60f)
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


    public INativeCamera NativeControl;

    private ICaptureVideoEncoder _captureVideoEncoder;
    private System.Threading.Timer _frameCaptureTimer;
    private DateTime _captureVideoStartTime;
#if WINDOWS
    private bool _useWindowsPreviewDrivenCapture;
#endif
#if WINDOWS || ANDROID || IOS || MACCATALYST
    private EventHandler _encoderPreviewInvalidateHandler;
#endif
    private DateTime? _capturePtsBaseTime; // base timestamp for PTS (from first captured frame)


#if ANDROID
    private int _androidFrameGate; // 0 = free, 1 = in-flight
    private int _androidWarmupDropRemaining; // drop first N frames to avoid initial garbage frame
    private System.Action<CapturedImage> _androidPreviewHandler;
#endif


    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        if (State == CameraState.Error)
            StartInternal();
    }

    bool subscribed;

    /// <summary>
    /// Called when the superview (parent container) changes.
    /// Subscribes to orientation change events from the superview.
    /// </summary>
    public override void SuperViewChanged()
    {
        if (Superview != null && !subscribed)
        {
            subscribed = true;
            Superview.OrientationChanged += DeviceOrientationChanged;
        }

        base.SuperViewChanged();
    }

    /// <summary>
    /// Updates the camera orientation from the current device rotation
    /// </summary>
    public virtual void UpdateOrientationFromDevice()
    {
        //var rotation = ((orientation + 45) / 90) * 90 % 360;

        DeviceRotation = Super.DeviceRotationSnap;

        Debug.WriteLine($"[CAMERA] DeviceRotation: {DeviceRotation}");
    }

    private void DeviceOrientationChanged(object sender, DeviceOrientation deviceOrientation)
    {
        UpdateOrientationFromDevice();
    }

    private int _DeviceRotation = -1;

    /// <summary>
    /// Gets or sets the current device rotation in degrees (0, 90, 180, 270).
    /// Automatically applies the orientation to the native camera when changed.
    /// </summary>
    public int DeviceRotation
    {
        get { return _DeviceRotation; }
        set
        {
            if (_DeviceRotation != value)
            {
                _DeviceRotation = value;
                OnPropertyChanged();
                NativeControl?.ApplyDeviceOrientation(value);
                UpdateInfo();
            }
        }
    }

    /// <summary>
    /// Rotation locked when video recording started. Used throughout recording to ensure consistent orientation.
    /// </summary>
    public int RecordingLockedRotation { get; private set; } = -1;

    /// <summary>
    /// Gets the rotation to use for video recording - returns locked rotation during recording, current rotation otherwise.
    /// </summary>
    protected int GetActiveRecordingRotation()
    {
        return IsRecordingVideo && RecordingLockedRotation >= 0 ? RecordingLockedRotation : DeviceRotation;
    }

    object lockFrame = new();

    /// <summary>
    /// Gets or sets whether a new camera frame has been acquired and is ready for display
    /// </summary>
    public bool FrameAquired { get; set; }

    /// <summary>
    /// Updates the camera preview display. Called when a new frame is available from the native camera.
    /// Handles frame submission for video capture flow if recording is active.
    /// </summary>
    public virtual void UpdatePreview()
    {
        FrameAquired = false;
        NeedUpdate = false;
        Update();

#if WINDOWS
        // If using capture video flow and preview-driven capture, submit frames in real-time with the preview
        if (_useWindowsPreviewDrivenCapture && IsRecordingVideo &&
            _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // One frame in flight policy: drop if busy
            //if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) == 0)
            {
                SafeAction(async () =>
                {
                    try
                    {
                        var elapsed = DateTime.Now - _captureVideoStartTime;
                        using var previewImage = NativeControl?.GetPreviewImage();
                        if (previewImage == null)
                            return;

                        using (winEnc.BeginFrame(elapsed, out var canvas, out var info))
                        {
                            var __rects3 =
                                GetAspectFillRects(previewImage.Width, previewImage.Height, info.Width, info.Height);
                            canvas.DrawImage(previewImage, __rects3.src, __rects3.dst);

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

                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        await winEnc.SubmitFrameAsync();
                        sw.Stop();
                        _diagLastSubmitMs = sw.Elapsed.TotalMilliseconds;
                        System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdatePreview Capture] {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
                    }
                });
            }
            //else
            //{
            //    System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            //}
        }
#endif
    }

    /// <summary>
    /// Gets the SKSurface used for frame rendering operations
    /// </summary>
    public SKSurface FrameSurface { get; protected set; }

    /// <summary>
    /// Gets the image info for the frame surface
    /// </summary>
    public SKImageInfo FrameSurfaceInfo { get; protected set; }

    //public bool AllocatedFrameSurface(int width, int height)
    //{
    //    if (Superview == null || width == 0 || height == 0)
    //    {
    //        return false;
    //    }

    //    var kill = FrameSurface;
    //    FrameSurfaceInfo = new SKImageInfo(width, height);

    //    if (Superview.CanvasView is SkiaViewAccelerated accelerated)
    //    {
    //        FrameSurface = SKSurface.Create(accelerated.GRContext, true, FrameSurfaceInfo);
    //    }
    //    else
    //    {
    //        //normal one
    //        FrameSurface = SKSurface.Create(FrameSurfaceInfo);
    //    }

    //    kill?.Dispose();

    //    return true;
    //}

    protected virtual void OnNewFrameSet(LoadedImageSource source)
    {
        NewPreviewSet?.Invoke(this, source);
    }

    protected virtual SKImage AquireFrameFromNative()
    {
        // If we are recording and not mirroring encoder frames to preview, suppress raw preview updates
        if (IsRecordingVideo && !UseRecordingFramesToPreview)
            return null;

#if WINDOWS
        if (IsRecordingVideo && UseRecordingFramesToPreview &&
            _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // Only show frames that were actually composed for recording.
            // If none is available yet, return null so the previous displayed frame stays,
            // avoiding a fallback blink from the raw preview without overlay.
            if (winEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose

            return null; // do NOT fallback to raw preview during recording
        }
#elif ANDROID
        // While recording on Android, mirror the composed encoder frames into the preview (no second camera feed)
        if (IsRecordingVideo && UseRecordingFramesToPreview &&
            _captureVideoEncoder is AndroidCaptureVideoEncoder droidEnc)
        {
            if (droidEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose
            return null; // no fallback to raw preview during recording
        }
#elif IOS || MACCATALYST
        // While recording on Apple, mirror the composed encoder frames into the preview
        if (IsRecordingVideo && UseRecordingFramesToPreview &&
            _captureVideoEncoder is DrawnUi.Camera.AppleCaptureVideoEncoder appleEnc)
        {
            if (appleEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose
            return null; // no fallback to raw preview during recording
        }
#endif
        return NativeControl.GetPreviewImage();
    }

    protected virtual void SetFrameFromNative()
    {
        if (NativeControl != null && !FrameAquired)
        {
            //acquire latest image from camera
            var image = AquireFrameFromNative();
            if (image != null)
            {
                FrameAquired = true;
                OnNewFrameSet(Display.SetImageInternal(image, false));
            }
        }
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        if (State == CameraState.On)
        {
            SetFrameFromNative();
        }

        DrawViews(ctx);

        if (ConstantUpdate && State == CameraState.On)
        {
            Update();
        }
    }

    #endregion

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN)
    /// <summary>
    /// Gets a bitmap of the current preview frame (not implemented on this platform)
    /// </summary>
    /// <returns>Preview bitmap</returns>
    public SKBitmap GetPreviewBitmap()
    {
        throw new NotImplementedException();
    }


#endif


    bool lockStartup;

    /// <summary>
    /// Starts the camera by setting IsOn to true.
    /// The actual camera initialization and permission handling happens automatically.
    /// </summary>
    public virtual void Start()
    {
        IsOn = true; //haha
    }

    private static void PowerChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StopInternal(true);
            if (control.IsOn)
            {
                control.StartWithPermissionsInternal();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether a permissions error occurred
    /// </summary>
    public bool PermissionsError { get; set; }

    /// <summary>
    /// Request permissions and start camera without setting IsOn true. Will set IsOn to false if permissions denied.
    /// </summary>
    public virtual void StartWithPermissionsInternal()
    {
        if (lockStartup)
        {
            Debug.WriteLine("[SkiaCamera] Startup locked.");
            return;
        }

        lockStartup = true;

        try
        {
            Debug.WriteLine("[SkiaCamera] Requesting permissions...");

            SkiaCamera.CheckPermissions((presented) =>
                {
                    Debug.WriteLine("[SkiaCamera] Starting..");
                    PermissionsWarning = false;
                    PermissionsError = false;
                    StartInternal();

                    //if (Geotag)
                    //	CommandGetLocation.Execute(null);
                    //else
                    //{
                    //	CanDetectLocation = false;
                    //}
                },
                (presented) =>
                {
                    Super.Log("[SkiaCamera] Permissions denied");
                    IsOn = false;
                    PermissionsWarning = true;
                    PermissionsError = true;
                });
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
        finally
        {
            Tasks.StartDelayed(TimeSpan.FromSeconds(1), () =>
            {
                Debug.WriteLine("[SkiaCamera] Startup UNlocked.");
                lockStartup = false;
            });
        }
    }

    /// <summary>
    /// Starts the camera after permissions where acquired
    /// </summary>
    protected virtual void StartInternal()
    {
        if (IsDisposing || IsDisposed)
            return;

        if (NativeControl == null)
        {
#if ONPLATFORM
            CreateNative();
            OnNativeControlCreated();
#endif
        }

        //var rotation = ((Superview.DeviceRotation + 45) / 90) % 4;
        //NativeControl?.ApplyDeviceOrientation(rotation);

        if (Display != null)
        {
            //DestroyRenderingObject();
            Display.IsVisible = true;
        }

        //IsOn = true;

        NativeControl?.Start();
    }

    /// <summary>
    /// Called after native control is created to notify property changes
    /// </summary>
    protected virtual void OnNativeControlCreated()
    {
        // Notify that flash capability properties may have changed
        OnPropertyChanged(nameof(IsFlashSupported));
        OnPropertyChanged(nameof(IsAutoFlashSupported));

        // Apply current flash modes to native control
        if (NativeControl != null)
        {
            NativeControl.SetFlashMode(FlashMode);
            NativeControl.SetCaptureFlashMode(CaptureFlashMode);
            NativeControl.EnablePreRecording = EnablePreRecording;
            NativeControl.PreRecordDuration = PreRecordDuration;
        }
    }

    #region SkiaCamera xam control

    private bool _PermissionsWarning;

    /// <summary>
    /// Gets or sets whether a permissions warning is active (permissions need to be granted)
    /// </summary>
    public bool PermissionsWarning
    {
        get { return _PermissionsWarning; }
        set
        {
            if (_PermissionsWarning != value)
            {
                _PermissionsWarning = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Represents a queued picture waiting to be processed
    /// </summary>
    public class CameraQueuedPictured
    {
        /// <summary>
        /// Gets or sets the filename for this queued picture
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Gets or sets the sensor rotation angle in degrees
        /// </summary>
        public double SensorRotation { get; set; }

        /// <summary>
        /// Set by renderer after work
        /// </summary>
        public bool Processed { get; set; }
    }

    /// <summary>
    /// Queue for managing pictures waiting to be processed
    /// </summary>
    public class CameraPicturesQueue : Queue<CameraQueuedPictured>
    {
    }


    private bool _IsTakingPhoto;

    /// <summary>
    /// Gets whether the camera is currently taking a still photo
    /// </summary>
    public bool IsTakingPhoto
    {
        get { return _IsTakingPhoto; }
        set
        {
            if (_IsTakingPhoto != value)
            {
                _IsTakingPhoto = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the queue of pictures waiting to be processed
    /// </summary>
    public CameraPicturesQueue PicturesQueue { get; } = new CameraPicturesQueue();


    #region PERMISSIONS

    protected static bool ChecksBusy = false;

    private static DateTime lastTimeChecked = DateTime.MinValue;

    /// <summary>
    /// Gets whether camera permissions have been granted
    /// </summary>
    public static bool PermissionsGranted { get; protected set; }

    /// <summary>
    /// Checks gallery/camera permissions and invokes the appropriate callback
    /// </summary>
    /// <param name="granted">Action to invoke if permissions are granted</param>
    /// <param name="notGranted">Action to invoke if permissions are denied</param>
    public static void CheckGalleryPermissions(Action granted, Action notGranted)
    {
        if (lastTimeChecked + TimeSpan.FromSeconds(5) < DateTime.Now) //avoid spam
        {
            lastTimeChecked = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (ChecksBusy)
                    return;

                bool okay1 = false;


                ChecksBusy = true;
                // Update the UI
                try
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<Permissions.Camera>();


                        if (status == PermissionStatus.Granted)
                        {
                            okay1 = true;
                        }
                    }
                    else
                    {
                        okay1 = true;
                    }


                    // Could prompt to enable in settings if needed
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
                finally
                {
                    if (okay1)
                    {
                        PermissionsGranted = true;
                        granted?.Invoke();
                    }
                    else
                    {
                        PermissionsGranted = false;
                        notGranted?.Invoke();
                    }

                    ChecksBusy = false;
                }
            });
        }
    }

    private bool _GpsBusy;

    public bool GpsBusy
    {
        get { return _GpsBusy; }
        set
        {
            if (_GpsBusy != value)
            {
                _GpsBusy = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLat;

    public double LocationLat
    {
        get { return _LocationLat; }
        set
        {
            if (_LocationLat != value)
            {
                _LocationLat = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLon;

    public double LocationLon
    {
        get { return _LocationLon; }
        set
        {
            if (_LocationLon != value)
            {
                _LocationLon = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _CanDetectLocation;

    public bool CanDetectLocation
    {
        get { return _CanDetectLocation; }
        set
        {
            if (_CanDetectLocation != value)
            {
                _CanDetectLocation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Safe and if CanDetectLocation
    /// </summary>
    /// <returns></returns>
    public async Task RefreshLocation(int msTimeout)
    {
        if (CanDetectLocation)
        {
            //my ACTUAL location
            try
            {
                GpsBusy = true;

                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var cancel = new CancellationTokenSource();
                cancel.CancelAfter(msTimeout);
                var location = await Geolocation.GetLocationAsync(request, cancel.Token);

                if (location != null)
                {
                    Debug.WriteLine(
                        $"ACTUAL Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

                    this.LocationLat = location.Latitude;
                    this.LocationLon = location.Longitude;
                }
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // Handle not supported on device exception
                //Toast.ShortMessage("GPS не поддерживается на устройстве");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Handle not enabled on device exception
                //Toast.ShortMessage("GPS отключен на устройстве");
            }
            catch (PermissionException pEx)
            {
                // Handle permission exception
                //Toast.ShortMessage("Вы не дали разрешение на использование GPS");
            }
            catch (Exception ex)
            {
                // Unable to get location
            }
            finally
            {
                GpsBusy = false;
            }
        }
    }

    //public ICommand CommandGetLocation
    //{
    //	get
    //	{
    //		return new Command((object context) =>
    //		{
    //			if (GpsBusy || !App.Native.CheckGpsEnabled())
    //				return;

    //			MainThread.BeginInvokeOnMainThread(async () =>
    //			{
    //				// Update the UI
    //				try
    //				{
    //					GpsBusy = true;

    //					var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    //					if (status != PermissionStatus.Granted)
    //					{
    //						CanDetectLocation = false;

    //						await App.Current.MainPage.DisplayAlert(Core.Current.MyCompany.Name, ResStrings.X_NeedMoreForGeo, ResStrings.ButtonOk);

    //						status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    //						if (status != PermissionStatus.Granted)
    //						{
    //							// Additionally could prompt the user to turn on in settings
    //							return;
    //						}
    //						else
    //						{
    //							CanDetectLocation = true;
    //						}
    //					}
    //					else
    //					{
    //						CanDetectLocation = true;
    //					}

    //					if (CanDetectLocation)
    //					{
    //						//my LAST location:
    //						try
    //						{
    //							if (App.Native.CheckGpsEnabled())
    //							{
    //								var location = await Geolocation.GetLastKnownLocationAsync();

    //								if (location != null)
    //								{
    //									Debug.WriteLine(
    //										$"LAST Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

    //									LocationLat = location.Latitude;
    //									LocationLon = location.Longitude;
    //								}
    //							}
    //						}
    //						catch (FeatureNotSupportedException fnsEx)
    //						{
    //							// Handle not supported on device exception
    //							//Toast.ShortMessage("GPS не поддерживается на устройстве");
    //						}
    //						catch (FeatureNotEnabledException fneEx)
    //						{
    //							// Handle not enabled on device exception
    //							//Toast.ShortMessage("GPS отключен на устройстве");
    //						}
    //						catch (PermissionException pEx)
    //						{
    //							// Handle permission exception
    //							//Toast.ShortMessage("Вы не дали разрешение на использование GPS");
    //						}
    //						catch (Exception ex)
    //						{
    //							// Unable to get location
    //						}

    //						await Task.Run(async () =>
    //						{
    //							await RefreshLocation(1200);

    //						}).ConfigureAwait(false);

    //					}
    //					else
    //					{
    //						GpsBusy = false;
    //					}


    //				}
    //				catch (Exception ex)
    //				{
    //					//Something went wrong
    //					Trace.WriteLine(ex);
    //					CanDetectLocation = false;
    //					GpsBusy = false;
    //				}
    //				finally
    //				{

    //				}

    //			});


    //		});
    //	}
    //}
