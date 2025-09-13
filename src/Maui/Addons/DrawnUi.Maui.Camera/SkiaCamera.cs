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
    public override bool CanUseCacheDoubleBuffering => false;
    public override bool WillClipBounds => true;


    public SkiaCamera()
    {
        Instances.Add(this);
        Super.OnNativeAppResumed += Super_OnNativeAppResumed;
        Super.OnNativeAppPaused += Super_OnNativeAppPaused;
    }

    public override void LockUpdate(bool value)
    {
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        Super.OnNativeAppResumed -= Super_OnNativeAppResumed;
        Super.OnNativeAppPaused -= Super_OnNativeAppPaused;

        if (Superview != null)
        {
            Superview.DeviceRotationChanged -= DeviceRotationChanged;
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

    public event EventHandler<CapturedImage> CaptureSuccess;

    public event EventHandler<Exception> CaptureFailed;

    public event EventHandler<LoadedImageSource> NewPreviewSet;

    public event EventHandler<string> OnError;

    public event EventHandler<double> Zoomed;

    internal void RaiseError(string error)
    {
        OnError?.Invoke(this, error);
    }

    #endregion

    #region Display

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
    public async Task<string> SaveToGalleryAsync(CapturedImage captured, bool reorient, string album = null)
    {
        var filename = GenerateJpgFileName();

        var rotation = reorient ? captured.Rotation : 0;

        await using var stream = CreateOutputStreamRotated(captured, reorient);
        if (stream != null)
        {
            using var exifStream = await JpegExifInjector.InjectExifMetadata(stream, captured.Meta);

            var filenameOutput = GenerateJpgFileName();

            var path = await NativeControl.SaveJpgStreamToGallery(exifStream, filename, rotation,
                captured.Meta, album);

            if (!string.IsNullOrEmpty(path))
            {
                captured.Path = path;
                Debug.WriteLine($"[SkiaCamera] saved photo: {filenameOutput}");
                return path;
            }
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
    /// Use with CaptureFormatIndex when CapturePhotoQuality is set to Manual.
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
    /// the current CapturePhotoQuality and CaptureFormatIndex settings.
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
    /// </summary>
    /// <returns></returns>
    public async Task StartVideoRecording()
    {
        if (IsBusy || IsRecordingVideo)
            return;

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}");

        IsBusy = true;
        IsRecordingVideo = true;

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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnVideoRecordingProgress(duration);
            });
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
        await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio);

        // Start encoder
        await _captureVideoEncoder.StartAsync();

        _captureVideoStartTime = DateTime.Now;

        // Reset diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        _targetFps = fps;


        // Windows uses real-time preview-driven capture (no timer)
        _useWindowsPreviewDrivenCapture = true;

            // Show exactly what is being recorded on screen
            UseRecordingFramesForPreview = true;

            // Invalidate preview when the encoder publishes a new composed frame
            if (_captureVideoEncoder is WindowsCaptureVideoEncoder _winEncPrev)
            {
                _encoderPreviewInvalidateHandler = (s, e) =>
                {
                    try { MainThread.BeginInvokeOnMainThread(() => UpdatePreview()); }
                    catch { }
                };
                _winEncPrev.PreviewAvailable += _encoderPreviewInvalidateHandler;
            }


        // Set up progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnVideoRecordingProgress(duration);
            });
        };
#else
        throw new NotSupportedException("Capture video flow is currently only supported on Windows");
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
                    // Draw camera frame to encoder surface
                    canvas.DrawImage(previewImage, 0, 0);

                    // Apply overlay
                    FrameProcessor?.Invoke(canvas, info, elapsed);

                    // Diagnostics overlay
                    DrawDiagnostics(canvas, info);
                }

                var __sw = System.Diagnostics.Stopwatch.StartNew();
                await winEnc.SubmitFrameAsync();
                __sw.Stop();
                _diagLastSubmitMs = __sw.Elapsed.TotalMilliseconds;
                System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                return;
            }
#endif

            // Fallback (non-Windows or encoder without GPU path): CPU composition
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
                var imageInfo = new SKImageInfo(previewBitmap.Width, previewBitmap.Height);
                FrameProcessor(cpuCanvas, imageInfo, elapsed);
                DrawDiagnostics(cpuCanvas, imageInfo);
            }
            else
            {
                var imageInfo = new SKImageInfo(previewBitmap.Width, previewBitmap.Height);
                DrawDiagnostics(cpuCanvas, imageInfo);
            }

            var __sw2 = System.Diagnostics.Stopwatch.StartNew();
            await _captureVideoEncoder.AddFrameAsync(finalBitmap, elapsed);
            __sw2.Stop();
            _diagLastSubmitMs = __sw2.Elapsed.TotalMilliseconds;
            System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
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
    /// Stop video recording
    /// </summary>
    /// <returns></returns>
    public async Task StopVideoRecording()
    {
        if (!IsRecordingVideo)
            return;

        Debug.WriteLine($"[StopVideoRecording] IsMainThread {MainThread.IsMainThread}");

        IsRecordingVideo = false;

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

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;

#if WINDOWS
            // Stop mirroring recording frames to preview and detach event
            UseRecordingFramesForPreview = false;
            if (encoder is WindowsCaptureVideoEncoder _winEncPrev)
            {
                try { _winEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler; } catch { }
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
        if (capturedVideo == null || string.IsNullOrEmpty(capturedVideo.FilePath) || !File.Exists(capturedVideo.FilePath))
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
    public async Task<string> MoveVideoToGalleryAsync(CapturedVideo capturedVideo, string album = null, bool deleteOriginal = true)
    {
        if (capturedVideo == null || string.IsNullOrEmpty(capturedVideo.FilePath) || !File.Exists(capturedVideo.FilePath))
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
    protected virtual async Task<List<CameraInfo>> GetAvailableCamerasInternal(bool refresh=false)
    {
#if ONPLATFORM
        return await GetAvailableCamerasPlatform(refresh);
#endif

        return new List<CameraInfo>();
    }

    #endregion

    #region METHODS

    public virtual void Stop()
    {
        IsOn = false;
    }

    /// <summary>
    /// Stops the camera
    /// </summary>
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


    public Stream CreateOutputStreamRotated(CapturedImage captured,
        bool reorient,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg,
        int quality = 90)
    {
        try
        {
            var rotated = Reorient();
            var data = rotated.Encode(format, quality);
            return data.AsStream();

            SKBitmap Reorient()
            {
                var bitmap = SKBitmap.FromImage(captured.Image);

                if (!reorient)
                    return bitmap;

                SKBitmap rotated;

                switch (captured.Rotation)
                {
                    case 180:
                        using (var surface = new SKCanvas(bitmap))
                        {
                            surface.RotateDegrees(180, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                            surface.DrawBitmap(bitmap.Copy(), 0, 0);
                        }

                        return bitmap;
                    case 270:
                        rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                        using (var surface = new SKCanvas(rotated))
                        {
                            surface.Translate(rotated.Width, 0);
                            surface.RotateDegrees(90);
                            surface.DrawBitmap(bitmap, 0, 0);
                        }

                        return rotated;
                    case 90:
                        rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                        using (var surface = new SKCanvas(rotated))
                        {
                            surface.Translate(0, rotated.Height);
                            surface.RotateDegrees(270);
                            surface.DrawBitmap(bitmap, 0, 0);
                        }

                        return rotated;
                    default:
                        return bitmap;
                }
            }
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

    private void DrawDiagnostics(SKCanvas canvas, SKImageInfo info)
    {
        if (!EnableCaptureDiagnostics || canvas == null)
            return;

        var elapsed = (DateTime.Now - _diagStartTime).TotalSeconds;
        var effFps = elapsed > 0 ? _diagSubmittedFrames / elapsed : 0;

        // Compose text
        string line1 = $"FPS: {effFps:F1} / {_targetFps}  dropped: {_diagDroppedFrames}";
        string line2 = $"submit: {_diagLastSubmitMs:F1} ms";

        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = Math.Max(14, info.Width / 60f) };


        var pad = 8f;
        var y1 = pad + textPaint.TextSize;
        var y2 = y1 + textPaint.TextSize + 4f;
        var maxTextWidth = Math.Max(textPaint.MeasureText(line1), textPaint.MeasureText(line2));
        var rect = new SKRect(pad, pad, pad + maxTextWidth + pad, y2 + pad);

        canvas.Save();
        canvas.DrawRoundRect(rect, 6, 6, bgPaint);
        canvas.DrawText(line1, pad * 1.5f, y1, textPaint);
        canvas.DrawText(line2, pad * 1.5f, y2, textPaint);
        canvas.Restore();
    }




    public INativeCamera NativeControl;

    private ICaptureVideoEncoder _captureVideoEncoder;
    private System.Threading.Timer _frameCaptureTimer;
    private DateTime _captureVideoStartTime;
#if WINDOWS
    private bool _useWindowsPreviewDrivenCapture;
#endif
#if WINDOWS
        private EventHandler _encoderPreviewInvalidateHandler;
#endif



    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        if (State == CameraState.Error)
            StartInternal();
    }

    bool subscribed;

    public override void SuperViewChanged()
    {
        if (Superview != null && !subscribed)
        {
            subscribed = true;
            Superview.DeviceRotationChanged += DeviceRotationChanged;
        }

        base.SuperViewChanged();
    }


    private void DeviceRotationChanged(object sender, int orientation)
    {
        var rotation = ((orientation + 45) / 90) * 90 % 360;

        DeviceRotation = rotation;
    }

    private int _DeviceRotation;

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

    object lockFrame = new();

    public bool FrameAquired { get; set; }

    public virtual void UpdatePreview()
    {
        FrameAquired = false;
        Update();

#if WINDOWS
        // If using capture video flow and preview-driven capture, submit frames in real-time with the preview
        if (_useWindowsPreviewDrivenCapture && IsRecordingVideo && _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
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
                            canvas.DrawImage(previewImage, 0, 0);
                            FrameProcessor?.Invoke(canvas, info, elapsed);
                            DrawDiagnostics(canvas, info);
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

    public SKSurface FrameSurface { get; protected set; }
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
#if WINDOWS
        if (IsRecordingVideo && UseRecordingFramesForPreview && _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // Only show frames that were actually composed for recording.
            // If none is available yet, return null so the previous displayed frame stays,
            // avoiding a fallback blink from the raw preview without overlay.
            if (winEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose

            return null; // do NOT fallback to raw preview during recording
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
    public SKBitmap GetPreviewBitmap()
    {
        throw new NotImplementedException();
    }


#endif


    bool lockStartup;

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
        }
    }

    #region SkiaCamera xam control

    private bool _PermissionsWarning;

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


    public class CameraQueuedPictured
    {
        public string Filename { get; set; }

        public double SensorRotation { get; set; }

        /// <summary>
        /// Set by renderer after work
        /// </summary>
        public bool Processed { get; set; }
    }

    public class CameraPicturesQueue : Queue<CameraQueuedPictured>
    {
    }




    private bool _IsTakingPhoto;

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


    public CameraPicturesQueue PicturesQueue { get; } = new CameraPicturesQueue();


    #region PERMISSIONS

    protected static bool ChecksBusy = false;

    private static DateTime lastTimeChecked = DateTime.MinValue;

    public static bool PermissionsGranted { get; protected set; }


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
    /// <param name="notGranted"></param>
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

    #endregion


    /// <summary>
    /// This is filled by renderer
    /// </summary>
    public string SavedFilename
    {
        get { return _SavedFilename; }
        set
        {
            if (_SavedFilename != value)
            {
                _SavedFilename = value;
                OnPropertyChanged("SavedFilename");
            }
        }
    }

    private string _SavedFilename;

    public static readonly BindableProperty CaptureLocationProperty = BindableProperty.Create(
        nameof(CaptureLocation),
        typeof(CaptureLocationType),
        typeof(SkiaCamera),
        CaptureLocationType.Gallery);

    public CaptureLocationType CaptureLocation
    {
        get { return (CaptureLocationType)GetValue(CaptureLocationProperty); }
        set { SetValue(CaptureLocationProperty, value); }
    }

    public static readonly BindableProperty CaptureCustomFolderProperty = BindableProperty.Create(
        nameof(CaptureCustomFolder),
        typeof(string),
        typeof(SkiaCamera),
        string.Empty);

    public string CaptureCustomFolder
    {
        get { return (string)GetValue(CaptureCustomFolderProperty); }
        set { SetValue(CaptureCustomFolderProperty, value); }
    }


    public static readonly BindableProperty FacingProperty = BindableProperty.Create(
        nameof(Facing),
        typeof(CameraPosition),
        typeof(SkiaCamera),
        CameraPosition.Default, propertyChanged: NeedRestart);

    private static void NeedRestart(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            if (control.State == CameraState.On)
            {
                control.StopInternal();

                // Force recreation of NativeControl when camera properties change
                // This ensures the new camera selection settings are applied
                if (control.NativeControl != null)
                {
                    control.NativeControl.Dispose();
                    control.NativeControl = null;
                }
            }

            if (control.IsOn)
            {
                control.StartInternal();
            }
            else
            {
                control.Start();
            }
        }
    }

    private static void OnCaptureFormatChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            // When capture format changes, update preview to match aspect ratio
            Debug.WriteLine($"[SkiaCamera] Capture format changed: {oldvalue} -> {newvalue}");

            if (newvalue is int)
            {
                if (control.CapturePhotoQuality != CaptureQuality.Manual)
                {
                    return;
                }
            }

            if (control.IsOn)
            {
#if ONPLATFORM
                control.UpdatePreviewFormatForAspectRatio();
#endif

            }
            //else
            //{
            //    if (control.IsOn)
            //    {
            //        control.StartInternal();
            //    }
            //    else
            //    {
            //        control.Start();
            //    }
            //}
        }
    }

    public CameraPosition Facing
    {
        get { return (CameraPosition)GetValue(FacingProperty); }
        set { SetValue(FacingProperty, value); }
    }

    public static readonly BindableProperty CameraIndexProperty = BindableProperty.Create(
        nameof(CameraIndex),
        typeof(int),
        typeof(SkiaCamera),
        -1, propertyChanged: NeedRestart);

    /// <summary>
    /// Camera index for manual camera selection.
    /// When set to -1 (default), uses automatic selection based on Facing property.
    /// When Facing is set to Manual, this property determines which camera to use.
    /// </summary>
    public int CameraIndex
    {
        get { return (int)GetValue(CameraIndexProperty); }
        set { SetValue(CameraIndexProperty, value); }
    }

    public static readonly BindableProperty CapturePhotoQualityProperty = BindableProperty.Create(
        nameof(CapturePhotoQuality),
        typeof(CaptureQuality),
        typeof(SkiaCamera),
        CaptureQuality.Max, propertyChanged: OnCaptureFormatChanged);

    /// <summary>
    /// Photo capture quality
    /// </summary>
    public CaptureQuality CapturePhotoQuality
    {
        get { return (CaptureQuality)GetValue(CapturePhotoQualityProperty); }
        set { SetValue(CapturePhotoQualityProperty, value); }
    }

    public static readonly BindableProperty CaptureFormatIndexProperty = BindableProperty.Create(
        nameof(CaptureFormatIndex),
        typeof(int),
        typeof(SkiaCamera),
        0, propertyChanged: OnCaptureFormatChanged);

    /// <summary>
    /// Index of capture format when CapturePhotoQuality is set to Manual.
    /// Selects from the array of available capture formats/resolutions.
    /// Use GetAvailableCaptureFormats() to see available options.
    /// </summary>
    public int CaptureFormatIndex
    {
        get { return (int)GetValue(CaptureFormatIndexProperty); }
        set { SetValue(CaptureFormatIndexProperty, value); }
    }

    public static readonly BindableProperty CaptureFlashModeProperty = BindableProperty.Create(
        nameof(CaptureFlashMode),
        typeof(CaptureFlashMode),
        typeof(SkiaCamera),
        CaptureFlashMode.Auto,
        propertyChanged: OnCaptureFlashModeChanged);

    /// <summary>
    /// Flash mode for still image capture. Controls whether flash fires during photo capture.
    /// </summary>
    public CaptureFlashMode CaptureFlashMode
    {
        get { return (CaptureFlashMode)GetValue(CaptureFlashModeProperty); }
        set { SetValue(CaptureFlashModeProperty, value); }
    }

    private static void OnCaptureFlashModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera && camera.NativeControl != null)
        {
            camera.NativeControl.SetCaptureFlashMode((CaptureFlashMode)newValue);
        }
    }

    /// <summary>
    /// Gets whether flash is supported on the current camera
    /// </summary>
    public bool IsFlashSupported
    {
        get { return NativeControl?.IsFlashSupported() ?? false; }
    }

    /// <summary>
    /// Gets whether auto flash mode is supported on the current camera
    /// </summary>
    public bool IsAutoFlashSupported
    {
        get { return NativeControl?.IsAutoFlashSupported() ?? false; }
    }

    public static readonly BindableProperty FlashModeProperty = BindableProperty.Create(
        nameof(FlashMode),
        typeof(FlashMode),
        typeof(SkiaCamera),
        FlashMode.Off,
        propertyChanged: OnFlashModeChanged);

    /// <summary>
    /// Flash mode for preview torch. Controls LED torch for live camera preview.
    /// </summary>
    public FlashMode FlashMode
    {
        get { return (FlashMode)GetValue(FlashModeProperty); }
        set { SetValue(FlashModeProperty, value); }
    }

    private static void OnFlashModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera && camera.NativeControl != null)
        {
            camera.NativeControl.SetFlashMode((FlashMode)newValue);
        }
    }

    #region VIDEO RECORDING PROPERTIES

    public static readonly BindableProperty IsRecordingVideoProperty = BindableProperty.Create(
        nameof(IsRecordingVideo),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        BindingMode.OneWayToSource);

    /// <summary>
    /// Whether video recording is currently active (read-only)
    /// </summary>
    public bool IsRecordingVideo
    {
        get { return (bool)GetValue(IsRecordingVideoProperty); }
        private set { SetValue(IsRecordingVideoProperty, value); }
    }

    public static readonly BindableProperty VideoQualityProperty = BindableProperty.Create(
        nameof(VideoQuality),
        typeof(VideoQuality),
        typeof(SkiaCamera),
        VideoQuality.High,
        propertyChanged: OnVideoFormatChanged);

    /// <summary>
    /// Video recording quality preset
    /// </summary>
    public VideoQuality VideoQuality
    {
        get { return (VideoQuality)GetValue(VideoQualityProperty); }
        set { SetValue(VideoQualityProperty, value); }
    }

    public static readonly BindableProperty VideoFormatIndexProperty = BindableProperty.Create(
        nameof(VideoFormatIndex),
        typeof(int),
        typeof(SkiaCamera),
        0,
        propertyChanged: OnVideoFormatChanged);

    /// <summary>
    /// Index of video format when VideoQuality is set to Manual.
    /// Selects from the array of available video formats.
    /// Use GetAvailableVideoFormatsAsync() to see available options.
    /// </summary>
    public int VideoFormatIndex
    {
        get { return (int)GetValue(VideoFormatIndexProperty); }
        set { SetValue(VideoFormatIndexProperty, value); }
    }

    private static void OnVideoFormatChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera && camera.NativeControl != null)
        {
            // Platform will handle format changes
            // This callback allows for future format switching during recording if supported
        }
    }

    /// <summary>
    /// Gets whether video recording is supported on the current device/camera
    /// </summary>
    public bool CanRecordVideo
    {
        get { return NativeControl?.CanRecordVideo() ?? false; }
    }

    /// <summary>
    /// Gets the current recording duration (if recording)
    /// </summary>
    public TimeSpan CurrentRecordingDuration { get; private set; }

    public static readonly BindableProperty RecordAudioProperty = BindableProperty.Create(
        nameof(RecordAudio),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// Whether to record audio with video. Default is false (silent video).
    /// Must be set before starting video recording.
    /// </summary>
    public bool RecordAudio
    {
        get { return (bool)GetValue(RecordAudioProperty); }
        set { SetValue(RecordAudioProperty, value); }
    }

    public static readonly BindableProperty UseCaptureVideoFlowProperty = BindableProperty.Create(
        nameof(UseCaptureVideoFlow),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// Whether to use capture video flow (frame-by-frame processing) instead of native video recording.
    /// When true, individual camera frames are captured and processed through FrameProcessor callback before encoding.
    /// Default is false (use native video recording).
    /// </summary>
    public bool UseCaptureVideoFlow
    {
        get { return (bool)GetValue(UseCaptureVideoFlowProperty); }
        set { SetValue(UseCaptureVideoFlowProperty, value); }
    }

    /// <summary>
    /// Callback for processing individual frames during capture video flow.
    /// Only used when UseCaptureVideoFlow is true.
    /// Parameters: SKCanvas (for drawing), SKImageInfo (frame info), TimeSpan (recording timestamp)
    /// </summary>
    public Action<SKCanvas, SKImageInfo, TimeSpan> FrameProcessor { get; set; }

    #endregion

        /// <summary>
        /// While recording, show exactly the frames being composed for the encoder as the on-screen preview.
        /// This avoids stutter by not relying on a separate preview feed. Enabled automatically during capture.
        /// </summary>
        public bool UseRecordingFramesForPreview { get; set; } = false;


    public static readonly BindableProperty TypeProperty = BindableProperty.Create(
        nameof(Type),
        typeof(CameraType),
        typeof(SkiaCamera),
        CameraType.Default, propertyChanged: NeedRestart);

    /// <summary>
    /// To be implemented
    /// </summary>
    public CameraType Type
    {
        get { return (CameraType)GetValue(TypeProperty); }
        set { SetValue(TypeProperty, value); }
    }


    /// <summary>
    /// Will be applied to viewport for focal length etc
    /// </summary>
    public CameraUnit CameraDevice
    {
        get { return _virtualCameraUnit; }
        set
        {
            if (_virtualCameraUnit != value)
            {
                if (_virtualCameraUnit != value)
                {
                    _virtualCameraUnit = value;
                    AssignFocalLengthInternal(value);
                }
            }
        }
    }

    private CameraUnit _virtualCameraUnit;

    public void AssignFocalLengthInternal(CameraUnit value)
    {
        if (value != null)
        {
            FocalLength = (float)(value.FocalLength * value.SensorCropFactor);
        }

        OnPropertyChanged(nameof(CameraDevice));
    }

    private int _PreviewWidth;

    public int PreviewWidth
    {
        get { return _PreviewWidth; }
        set
        {
            if (_PreviewWidth != value)
            {
                _PreviewWidth = value;
                OnPropertyChanged("PreviewWidth");
            }
        }
    }

    private int _PreviewHeight;

    public int PreviewHeight
    {
        get { return _PreviewHeight; }
        set
        {
            if (_PreviewHeight != value)
            {
                _PreviewHeight = value;
                OnPropertyChanged("PreviewHeight");
            }
        }
    }

    private int _CaptureWidth;

    public int CaptureWidth
    {
        get { return _CaptureWidth; }
        set
        {
            if (_CaptureWidth != value)
            {
                _CaptureWidth = value;
                OnPropertyChanged("CaptureWidth");
            }
        }
    }

    private int _CaptureHeight;

    public int CaptureHeight
    {
        get { return _CaptureHeight; }
        set
        {
            if (_CaptureHeight != value)
            {
                _CaptureHeight = value;
                OnPropertyChanged("CaptureHeight");
            }
        }
    }


    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(FocalLength) || propertyName == nameof(FocalLengthAdjustment))
        {
            FocalLengthAdjusted = FocalLength + FocalLengthAdjustment;
        }
    }

    public static double GetSensorRotation(DeviceOrientation orientation)
    {
        if (orientation == DeviceOrientation.PortraitUpsideDown)
            return 180.0;

        if (orientation == DeviceOrientation.LandscapeLeft)
            return 90.0;

        if (orientation == DeviceOrientation.LandscapeRight)
            return 270.0;

        return 0.0;
    }


    public static readonly BindableProperty CapturedStillImageProperty = BindableProperty.Create(
        nameof(CapturedStillImage),
        typeof(CapturedImage),
        typeof(SkiaCamera),
        null);

    public CapturedImage CapturedStillImage
    {
        get { return (CapturedImage)GetValue(CapturedStillImageProperty); }
        set { SetValue(CapturedStillImageProperty, value); }
    }


    public static readonly BindableProperty CustomAlbumProperty = BindableProperty.Create(nameof(CustomAlbum),
        typeof(string),
        typeof(SkiaCamera),
        string.Empty);

    /// <summary>
    /// If not null will use this instead of Camera Roll folder for photos output
    /// </summary>
    public string CustomAlbum
    {
        get { return (string)GetValue(CustomAlbumProperty); }
        set { SetValue(CustomAlbumProperty, value); }
    }


    public static readonly BindableProperty GeotagProperty = BindableProperty.Create(nameof(Geotag),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// try to inject location metadata if to photos if GPS succeeds
    /// </summary>
    public bool Geotag
    {
        get { return (bool)GetValue(GeotagProperty); }
        set { SetValue(GeotagProperty, value); }
    }


    public static readonly BindableProperty FocalLengthProperty = BindableProperty.Create(
        nameof(FocalLength),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLength
    {
        get { return (double)GetValue(FocalLengthProperty); }
        set { SetValue(FocalLengthProperty, value); }
    }

    public static readonly BindableProperty FocalLengthAdjustedProperty = BindableProperty.Create(
        nameof(FocalLengthAdjusted),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLengthAdjusted
    {
        get { return (double)GetValue(FocalLengthAdjustedProperty); }
        set { SetValue(FocalLengthAdjustedProperty, value); }
    }

    public static readonly BindableProperty FocalLengthAdjustmentProperty = BindableProperty.Create(
        nameof(FocalLengthAdjustment),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLengthAdjustment
    {
        get { return (double)GetValue(FocalLengthAdjustmentProperty); }
        set { SetValue(FocalLengthAdjustmentProperty, value); }
    }

    public static readonly BindableProperty ManualZoomProperty = BindableProperty.Create(
        nameof(ManualZoom),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    public bool ManualZoom
    {
        get { return (bool)GetValue(ManualZoomProperty); }
        set { SetValue(ManualZoomProperty, value); }
    }

    public static readonly BindableProperty ZoomProperty = BindableProperty.Create(
        nameof(Zoom),
        typeof(double),
        typeof(SkiaCamera),
        1.0,
        propertyChanged: NeedSetZoom);

    /// <summary>
    /// Zoom camera
    /// </summary>
    public double Zoom
    {
        get { return (double)GetValue(ZoomProperty); }
        set { SetValue(ZoomProperty, value); }
    }

    public static readonly BindableProperty ConstantUpdateProperty = BindableProperty.Create(
        nameof(ConstantUpdate),
        typeof(bool),
        typeof(SkiaCamera),
        true);

    /// <summary>
    /// Default is true.
    /// Whether it should update non-stop or only when a new frame is acquired.
    /// For example if camera gives frames at 30 fps, screen might update around 40fps without this set to true.
    /// If enabled will force max redraws at 60 fps.
    /// </summary>
    public bool ConstantUpdate
    {
        get { return (bool)GetValue(ConstantUpdateProperty); }
        set { SetValue(ConstantUpdateProperty, value); }
    }

    public static readonly BindableProperty ViewportScaleProperty = BindableProperty.Create(
        nameof(ViewportScale),
        typeof(double),
        typeof(SkiaCamera),
        1.0);

    /// <summary>
    /// Zoom viewport value, NOT a camera zoom,
    /// </summary>
    public double ViewportScale
    {
        get { return (double)GetValue(ViewportScaleProperty); }
        set { SetValue(ViewportScaleProperty, value); }
    }

    public static readonly BindableProperty TextureScaleProperty = BindableProperty.Create(
        nameof(TextureScale),
        typeof(double),
        typeof(SkiaCamera),
        1.0, defaultBindingMode: BindingMode.OneWayToSource);

    public double TextureScale
    {
        get { return (double)GetValue(TextureScaleProperty); }
        set { SetValue(TextureScaleProperty, value); }
    }

    public static readonly BindableProperty ZoomLimitMinProperty = BindableProperty.Create(
        nameof(ZoomLimitMin),
        typeof(double),
        typeof(SkiaCamera),
        1.0);

    public double ZoomLimitMin
    {
        get { return (double)GetValue(ZoomLimitMinProperty); }
        set { SetValue(ZoomLimitMinProperty, value); }
    }

    public static readonly BindableProperty ZoomLimitMaxProperty = BindableProperty.Create(
        nameof(ZoomLimitMax),
        typeof(double),
        typeof(SkiaCamera),
        10.0);

    public double ZoomLimitMax
    {
        get { return (double)GetValue(ZoomLimitMaxProperty); }
        set { SetValue(ZoomLimitMaxProperty, value); }
    }


    private static void NeedSetZoom(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            var zoom = (double)newvalue;
            if (zoom < control.ZoomLimitMin)
            {
                zoom = control.ZoomLimitMin;
            }
            else if (zoom > control.ZoomLimitMax)
            {
                zoom = control.ZoomLimitMax;
            }

            control.SetZoom(zoom);
        }
    }

    protected virtual void ApplyDisplayProperties()
    {
        if (Display != null)
        {
            Display.Aspect = this.Aspect;

            Display.ScaleX = this.IsMirrored ?  -1 : 1;
        }
    }

    protected override void OnMeasured()
    {
        base.OnMeasured();

        ApplyDisplayProperties();
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ApplyDisplayProperties();
    }

    //public static readonly BindableProperty DisplayModeProperty = BindableProperty.Create(
    //    nameof(DisplayMode),
    //    typeof(StretchModes),
    //    typeof(SkiaCamera),
    //    StretchModes.Fill);

    //public StretchModes DisplayMode
    //{
    //    get { return (StretchModes)GetValue(DisplayModeProperty); }
    //    set { SetValue(DisplayModeProperty, value); }
    //}

    public static readonly BindableProperty IsMirroredProperty = BindableProperty.Create(
        nameof(IsMirrored),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: NeedInvalidateMeasure);

    public bool IsMirrored
    {
        get { return (bool)GetValue(IsMirroredProperty); }
        set { SetValue(IsMirroredProperty, value); }
    }

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(TransformAspect),
        typeof(SkiaImage),
        TransformAspect.AspectCover,
        propertyChanged: NeedInvalidateMeasure);

    /// <summary>
    /// Apspect to render image with, default is AspectCover.
    /// </summary>
    public TransformAspect Aspect
    {
        get { return (TransformAspect)GetValue(AspectProperty); }
        set { SetValue(AspectProperty, value); }
    }


    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State),
        typeof(CameraState),
        typeof(SkiaCamera),
        CameraState.Off,
        BindingMode.OneWayToSource, propertyChanged: ControlStateChanged);

    private static void ControlStateChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StateChanged?.Invoke(control, control.State);
            control.UpdateInfo();
        }
    }

    public CameraState State
    {
        get { return (CameraState)GetValue(StateProperty); }
        set { SetValue(StateProperty, value); }
    }

    public event EventHandler<CameraState> StateChanged;

    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(
        nameof(IsOn),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: PowerChanged);

    public bool IsOn
    {
        get { return (bool)GetValue(IsOnProperty); }
        set { SetValue(IsOnProperty, value); }
    }

    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    public bool IsBusy
    {
        get { return (bool)GetValue(IsBusyProperty); }
        set { SetValue(IsBusyProperty, value); }
    }

    public static readonly BindableProperty PickerModeProperty = BindableProperty.Create(
        nameof(PickerMode),
        typeof(CameraPickerMode),
        typeof(SkiaCamera),
        CameraPickerMode.None);

    public CameraPickerMode PickerMode
    {
        get { return (CameraPickerMode)GetValue(PickerModeProperty); }
        set { SetValue(PickerModeProperty, value); }
    }

    public static readonly BindableProperty FilterProperty = BindableProperty.Create(
        nameof(Filter),
        typeof(CameraEffect),
        typeof(SkiaCamera),
        CameraEffect.None);

    public CameraEffect Filter
    {
        get { return (CameraEffect)GetValue(FilterProperty); }
        set { SetValue(FilterProperty, value); }
    }


    public double SavedRotation { get; set; }


    //public bool
    //ShowSettings
    //{
    //    get { return (bool)GetValue(PageCamera.ShowSettingsProperty); }
    //    set { SetValue(PageCamera.ShowSettingsProperty, value); }
    //}

    #endregion

    /// <summary>
    /// The size of the camera preview in pixels
    /// </summary>

    public SKSize PreviewSize
    {
        get { return _previewSize; }
        set
        {
            if (_previewSize != value)
            {
                _previewSize = value;
                OnPropertyChanged();
            }
        }
    }

    SKSize _previewSize;


    public SKSize CapturePhotoSize
    {
        get { return _capturePhotoSize; }

        set
        {
            if (_capturePhotoSize != value)
            {
                _capturePhotoSize = value;
                OnPropertyChanged();
                //UpdateInfo();
            }
        }
    }

    SKSize _capturePhotoSize;

    public void SetRotatedContentSize(SKSize size, int cameraRotation)
    {
        if (size.Width < 0 || size.Height < 0)
        {
            throw new Exception("Camera preview size cannot be negative.");
        }

        PreviewSize = size;

        Invalidate();
    }

    private string _DisplayInfo;
    private bool _hasPermissions;

    public string DisplayInfo
    {
        get { return _DisplayInfo; }
        set
        {
            if (_DisplayInfo != value)
            {
                _DisplayInfo = value;
                OnPropertyChanged();
            }
        }
    }

    #region PROPERTIES

    public static readonly BindableProperty EffectProperty = BindableProperty.Create(
        nameof(Effect),
        typeof(SkiaImageEffect),
        typeof(SkiaCamera),
        SkiaImageEffect.None,
        propertyChanged: NeedSetupPreview);

    private static void NeedSetupPreview(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.ApplyPreviewProperties();
        }
    }

    public SkiaImageEffect Effect
    {
        get { return (SkiaImageEffect)GetValue(EffectProperty); }
        set { SetValue(EffectProperty, value); }
    }

    #endregion

    private void Super_OnNativeAppPaused(object sender, EventArgs e)
    {
        StopAll();
    }

    private void Super_OnNativeAppResumed(object sender, EventArgs e)
    {
        ResumeIfNeeded();
    }

    public void ResumeIfNeeded()
    {
        if (IsOn)
            StartInternal();
    }

    public static List<SkiaCamera> Instances = new();

    /// <summary>
    /// Stops all instances
    /// </summary>
    public static void StopAll()
    {
        foreach (var renderer in Instances)
        {
            renderer.StopInternal(true);
        }
    }
}
