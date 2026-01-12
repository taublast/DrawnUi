#if IOS || MACCATALYST

using System.Diagnostics;
using AVFoundation;
using DrawnUi.Maui.Navigation;
using Foundation;
using Photos;
using UIKit;
using Metal; // Added for Zero-Copy path
using SkiaSharp.Views.Maui.Controls; // For SKGLView
using Foundation;
using Photos;
using AVFoundation;
using AVKit;

namespace DrawnUi.Camera;

public partial class SkiaCamera
{

    private AudioSample[] _preRecordedAudioSamples;  // Saved at pre-rec → live transition

    // Streaming audio writer for OOM-safe live recording (audio goes to file, not memory)
    private AVAssetWriter _liveAudioWriter;
    private AVAssetWriterInput _liveAudioInput;
    private string _liveAudioFilePath;
    private long _liveAudioFirstTimestampNs = -1;
    private readonly object _liveAudioWriterLock = new object();
    private List<IntPtr> _liveAudioMemoryToFree;  // Memory to free after writer finishes

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
        if (!(IsRecordingVideo || IsPreRecording) || _captureVideoEncoder == null)
            return;

        // Make sure we never queue more than one frame — drop if previous is still processing
        if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) != 0)
        {
            System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            return;
        }

        // Only now fire the async work - we've already acquired the frame slot
        _ = CaptureFrameCore();
    }

    private async Task CaptureFrameCore()
    {
        try
        {
            // Double-check encoder still exists (race condition protection)
           if (_captureVideoEncoder == null || (!IsRecordingVideo && !IsPreRecording))
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
                            var width = (int)texture.Width;
                            var height = (int)texture.Height;
                            
                            var textureInfo = new GRMtlTextureInfo(texture.Handle);
                            using var backendTexture = new GRBackendTexture(width, height, false, textureInfo);

                            // Create image (BORROWED texture, will NOT dispose underlying Metal texture)
                            // Use encoder-specific context to ensure compatibility
                            var image = SKImage.FromTexture(encoderContext, backendTexture, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                            
                            if (image != null)
                            {
                                imageToDraw = image;
                                imageRotation = (int)nativeCam.CurrentRotation;
                                imageFlip = (CameraDevice?.Facing ?? Facing) == CameraPosition.Selfie;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CaptureFrame] Zero-copy texture failed: {ex.Message}");
                        }
                    }

                    if (imageToDraw == null)
                    {
                        var raw = nativeCam.GetRawFullImage();
                        if (raw.Image != null)
                        {
                            imageToDraw = raw.Image;
                            imageRotation = raw.Rotation;
                            imageFlip = raw.Flip;
                        }
                    }
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

    private void OnAudioSampleAvailable(object sender, AudioSample e)
    {
        // OOM-SAFE AUDIO HANDLING:
        // - Pre-recording phase: Write to circular buffer (bounded memory, ~5 sec max)
        // - Live recording phase: Stream directly to file (zero memory growth)

        if (IsPreRecording && !IsRecordingVideo)
        {
            // Pre-recording: Circular buffer keeps last N seconds (bounded memory)
            _audioBuffer?.Write(e);
        }
        else if (IsRecordingVideo && _liveAudioWriter != null)
        {
            // Live recording: Stream directly to file (OOM-safe)
            WriteSampleToLiveAudioWriter(e);
        }
    }

    private async Task StartCaptureVideoFlow()
    {
        // Dispose previous encoder if exists to prevent resource leaks and GC finalizer crashes
        // CRITICAL: Unsubscribe event handlers before disposal to prevent memory leaks
        if (_captureVideoEncoder is AppleVideoToolboxEncoder prevAppleEnc && _encoderPreviewInvalidateHandler != null)
        {
            prevAppleEnc.PreviewAvailable -= _encoderPreviewInvalidateHandler;
        }
        _captureVideoEncoder?.Dispose();
        _captureVideoEncoder = null;

        // Create Apple encoder using VideoToolbox for hardware H.264 encoding
        // Note: _captureVideoEncoder is defined in Shared partial
        // Re-use existing or create new; but usually we create new.
        // Cast to local variable for configuration
        var appleEncoder = new AppleVideoToolboxEncoder();
        
        // Inject GRContext for Zero-Copy path (Metal)
        // CRITICAL FIX: Do NOT pass UI Thread's GRContext to background recording thread to avoid crash.
        // The encoder will create its own dedicated Metal GRContext for thread safety.
        /*
        if (Superview?.CanvasView is SkiaSharp.Views.Maui.Controls.SKGLView glView)
        {
            appleEncoder.Context = glView.GRContext;
        }
        */

        _captureVideoEncoder = appleEncoder;

        if (RecordAudio)
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
                if (EnablePreRecording && !IsRecordingVideo)
                {
                    // Pre-recording phase: Circular buffer matching video duration
                    if (_audioBuffer == null)
                    {
                        _audioBuffer = new CircularAudioBuffer(PreRecordDuration);
                        Debug.WriteLine($"[StartCaptureVideoFlow] Created CIRCULAR audio buffer ({PreRecordDuration.TotalSeconds:F1}s)");
                    }
                }
                else if (IsRecordingVideo && _liveAudioWriter == null)
                {
                    // Live-only (no pre-recording): Start streaming writer immediately
                    if (StartLiveAudioWriter(AudioSampleRate, AudioChannels))
                    {
                        Debug.WriteLine("[StartCaptureVideoFlow] Started STREAMING audio writer for live recording (OOM-safe)");
                    }
                    else
                    {
                        Debug.WriteLine("[StartCaptureVideoFlow] Warning: Failed to start streaming audio writer");
                    }
                }

                // ARCHITECTURAL FIX: Encoder handles VIDEO ONLY - never pass audio buffer to encoder
                appleEncoder.SetAudioBuffer(null);

                // Start audio capture if not already running (survives transition)
                if (_audioCapture != null && !_audioCapture.IsCapturing)
                {
                    await _audioCapture.StartAsync(AudioSampleRate, AudioChannels, AudioBitDepth, AudioDeviceIndex);
                    Debug.WriteLine("[StartCaptureVideoFlow] Audio capture started");
                }
                else if (_audioCapture?.IsCapturing == true)
                {
                    Debug.WriteLine("[StartCaptureVideoFlow] Audio capture already running (surviving transition)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] Audio init error: {ex}");
            }
        }

        // Set parent reference and pre-recording mode
        _captureVideoEncoder.ParentCamera = this;
        _captureVideoEncoder.IsPreRecordingMode = IsPreRecording;
        Debug.WriteLine($"[StartCaptureVideoFlow] iOS encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Always use raw camera frames for preview (PreviewProcessor only, not FrameProcessor)
        UseRecordingFramesForPreview = false;
        if (MirrorRecordingToPreview && _captureVideoEncoder is AppleVideoToolboxEncoder _appleEncPrev)
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
            var documentsPath = GetAppVideoFolder(string.Empty);
            outputPath = Path.Combine(documentsPath, $"vid{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
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
        SetSourceFrameDimensions(width, height);

        // Pass locked rotation to encoder for proper video orientation metadata (iOS-specific)
        if (_captureVideoEncoder is DrawnUi.Camera.AppleVideoToolboxEncoder encoder)
        {
            await encoder.InitializeAsync(outputPath, width, height, fps, RecordAudio, RecordingLockedRotation);

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
                    encoder.SetPreRecordingDuration(_preRecordingDurationTracked);
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
        if (IsPreRecording || (!IsPreRecording && _preRecordingDurationTracked == TimeSpan.Zero))
        {
            _diagStartTime = DateTime.Now;
            _diagDroppedFrames = 0;
            _diagSubmittedFrames = 0;
            _diagLastSubmitMs = 0;
        }

        _targetFps = fps;

        // Progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            OnVideoRecordingProgress(duration);
        };

        // Start frame capture for Apple (drive encoder frames)
        if (NativeControl is NativeCamera nativeCam)
        {
            nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            nativeCam.RecordingFrameAvailable += OnRecordingFrameAvailable;
        }

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

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform(bool refresh)
    {
        var cameras = new List<CameraInfo>();

        try
        {
            var deviceTypes = new AVFoundation.AVCaptureDeviceType[]
            {
                AVFoundation.AVCaptureDeviceType.BuiltInWideAngleCamera,
                AVFoundation.AVCaptureDeviceType.BuiltInTelephotoCamera,
                AVFoundation.AVCaptureDeviceType.BuiltInUltraWideCamera
            };

            if (UIKit.UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                deviceTypes = deviceTypes.Concat(new[]
                {
                    AVFoundation.AVCaptureDeviceType.BuiltInDualCamera,
                    AVFoundation.AVCaptureDeviceType.BuiltInTripleCamera
                }).ToArray();
            }

            var discoverySession = AVFoundation.AVCaptureDeviceDiscoverySession.Create(
                deviceTypes,
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

                cameras.Add(new CameraInfo
                {
                    Id = device.UniqueID,
                    Name = device.LocalizedName,
                    Position = position,
                    Index = i,
                    HasFlash = device.HasFlash
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

        if (status == PermissionStatus.Granted && this.CaptureMode == CaptureModeType.Video && this.RecordAudio)
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
                    var liveRange =
                        new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = liveAsset.Duration };
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
                            // Don't exceed live video duration
                            var insertDuration = audioDuration.Seconds <= liveVideoDuration.Seconds ? audioDuration : liveVideoDuration;
                            var audioRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = insertDuration };

                            // Insert at the end of pre-rec video
                            audioTrack.InsertTimeRange(audioRange, liveAudioTracks[0], preRecVideoDuration, out var audioError);

                            if (audioError != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to insert live audio: {audioError.LocalizedDescription}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Added live audio at {preRecVideoDuration.Seconds:F2}s ({insertDuration.Seconds:F2}s)");
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
                                var liveAudioRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = liveAsset.Duration };
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
                        ShouldOptimizeForNetworkUse = false
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



    private async Task StopCaptureVideoFlowInternal()
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

            // OOM-SAFE AUDIO HANDLING:
            // 1. Stop live audio writer (instant - audio already on disk)
            // 2. Write pre-rec audio to file if present (fast - only ~5 sec max)
            // 3. Concatenate pre-rec + live audio files if needed
            string liveAudioFilePath = null;
            string preRecAudioFilePath = null;
            var stopwatchTotal = System.Diagnostics.Stopwatch.StartNew();
            var stopwatchStep = new System.Diagnostics.Stopwatch();

            if (RecordAudio)
            {
                // Stop the streaming audio writer and get its file path (instant - already on disk)
                stopwatchStep.Restart();
                liveAudioFilePath = await StopLiveAudioWriterAsync();
                stopwatchStep.Stop();
                if (!string.IsNullOrEmpty(liveAudioFilePath))
                {
                    var liveAudioSize = File.Exists(liveAudioFilePath) ? new FileInfo(liveAudioFilePath).Length / 1024.0 : 0;
                    Debug.WriteLine($"[StopCaptureVideoFlow] TIMING: StopLiveAudioWriter took {stopwatchStep.ElapsedMilliseconds}ms, file: {liveAudioSize:F1} KB");
                }

                // Write pre-recorded audio samples to file if present (small, ~5 sec max)
                if (_preRecordedAudioSamples != null && _preRecordedAudioSamples.Length > 0)
                {
                    stopwatchStep.Restart();
                    Debug.WriteLine($"[StopCaptureVideoFlow] Writing {_preRecordedAudioSamples.Length} pre-rec audio samples");
                    preRecAudioFilePath = Path.Combine(
                        Path.GetTempPath(),
                        $"prerec_audio_{Guid.NewGuid():N}.m4a"
                    );
                    preRecAudioFilePath = await WriteAudioSamplesToM4AAsync(_preRecordedAudioSamples, preRecAudioFilePath);
                    stopwatchStep.Stop();
                    var preRecAudioSize = File.Exists(preRecAudioFilePath) ? new FileInfo(preRecAudioFilePath).Length / 1024.0 : 0;
                    Debug.WriteLine($"[StopCaptureVideoFlow] TIMING: WritePreRecAudio took {stopwatchStep.ElapsedMilliseconds}ms, file: {preRecAudioSize:F1} KB");
                    _preRecordedAudioSamples = null;  // Clean up
                }

                // Clear any remaining buffer
                _audioBuffer = null;
            }

            // Stop audio capture
            if (_audioCapture != null)
            {
                Debug.WriteLine($"[StopCaptureVideoFlow] Stopping audio capture");
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
            Debug.WriteLine($"[StopCaptureVideoFlow] TIMING: StopEncoder took {stopwatchStep.ElapsedMilliseconds}ms");

            // OPTIMIZED: Skip audio concatenation - pass both files directly to MuxVideosInternal
            // This saves one AVAssetExportSession call (much faster)
            Debug.WriteLine($"[StopCaptureVideoFlow] Audio files: preRec={preRecAudioFilePath ?? "none"}, live={liveAudioFilePath ?? "none"}");

            // If we have both pre-recorded and live recording, mux them together
            if (capturedVideo != null && !string.IsNullOrEmpty(_preRecordingFilePath) &&
                File.Exists(_preRecordingFilePath))
            {
                Debug.WriteLine($"[StopCaptureVideoFlow] Muxing pre-recorded file with live recording");
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
                    Debug.WriteLine($"[StopCaptureVideoFlow] TIMING: MuxVideos took {stopwatchStep.ElapsedMilliseconds}ms");
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
                // LIVE-ONLY recording (no pre-recording) - add audio if present
                if (capturedVideo != null && !string.IsNullOrEmpty(liveAudioFilePath))
                {
                    Debug.WriteLine($"[StopCaptureVideoFlow] Live-only recording with audio - adding audio track");
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
                            Debug.WriteLine($"[StopCaptureVideoFlow] Live recording with audio successful: {finalPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StopCaptureVideoFlow] Failed to add audio to live recording: {ex.Message}");
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
            Debug.WriteLine($"[StopCaptureVideoFlow] TIMING: TOTAL stop time {stopwatchTotal.ElapsedMilliseconds}ms");

            IsRecordingVideo = false;
            if (capturedVideo != null)
            {
                OnVideoRecordingSuccess(capturedVideo);
            }

            IsBusy = false; // Release busy state after successful muxing
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

            IsRecordingVideo = false;
            IsBusy = false; // Release busy state on error
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task AbortCaptureVideoFlowInternal()
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
            Debug.WriteLine($"[AbortCaptureVideoFlow] Disposing encoder without finalizing video");
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
                Debug.WriteLine($"[AbortCaptureVideoFlow] Audio capture stopped and disposed");
            }

            // Clean up live audio writer
            CleanupLiveAudioWriter();
            _audioBuffer = null;
            _preRecordedAudioSamples = null;
            Debug.WriteLine($"[AbortCaptureVideoFlow] Live audio writer cleaned up");

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
            if (NativeControl is NativeCamera nativeCam)
            {
                nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
            }

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

            IsBusy = false;
        }
    }

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
        {
            Debug.WriteLine($"[StartVideoRecording] IsBusy cannot start");
            return;
        }

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecordingVideo={IsRecordingVideo}");

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

                // CRITICAL: Stop and finalize the pre-recording encoder BEFORE starting live recording
                if (_captureVideoEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] Stopping pre-recording encoder to finalize file");

                    // Stop frame capture first
                    if (NativeControl is NativeCamera nativeCam)
                    {
                        nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
                    }

                    // Wait for in-flight frame to complete before stopping encoder
                    // Prevents race condition where CaptureFrameCore still uses encoder being disposed
                    while (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 0, 0) != 0)
                    {
                        await Task.Yield();
                    }

                    try
                    {
                        var preRecResult = await _captureVideoEncoder.StopAsync();
                        Debug.WriteLine("[StartVideoRecording] Pre-recording encoder stopped and file finalized");

                        // ✅ CRITICAL: Capture pre-recording file path AND duration from StopAsync result
                        // The encoder wrote the file, so we must use ITS path, not generate our own!
                        if (preRecResult != null && !string.IsNullOrEmpty(preRecResult.FilePath))
                        {
                            _preRecordingFilePath = preRecResult.FilePath;
                            _preRecordingDurationTracked = _captureVideoEncoder.EncodingDuration;
                            Debug.WriteLine($"[StartVideoRecording] Captured pre-recording file: {_preRecordingFilePath}");
                            Debug.WriteLine($"[StartVideoRecording] Captured pre-recording duration: {_preRecordingDurationTracked.TotalSeconds:F2}s");
                        }
                        else
                        {
                            Debug.WriteLine($"[StartVideoRecording] WARNING: No pre-recording file path returned from encoder!");
                            _preRecordingFilePath = null;
                            _preRecordingDurationTracked = TimeSpan.Zero;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StartVideoRecording] Error stopping pre-recording encoder: {ex.Message}");
                    }

                    // CRITICAL: Unsubscribe event handlers before disposal to prevent memory leaks
                    if (_captureVideoEncoder is AppleVideoToolboxEncoder appleEncPreRec && _encoderPreviewInvalidateHandler != null)
                    {
                        appleEncPreRec.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                    }
                    _captureVideoEncoder?.Dispose();
                    _captureVideoEncoder = null;
                }

                // SAVE PRE-REC AUDIO before transition
                // The circular buffer only keeps last N seconds, so we must save now
                // IMPORTANT: Trim audio to match video duration for proper sync
                if (_audioBuffer != null && RecordAudio)
                {
                    var allAudioSamples = _audioBuffer.GetAllSamples();
                    Debug.WriteLine($"[StartVideoRecording] Raw audio buffer: {allAudioSamples?.Length ?? 0} samples");

                    // AUDIO-VIDEO SYNC FIX: Trim audio to match pre-rec video duration
                    // Audio capture may start before video encoding, causing audio to be longer
                    if (allAudioSamples != null && allAudioSamples.Length > 0 && _preRecordingDurationTracked.TotalSeconds > 0)
                    {
                        var videoDurationMs = _preRecordingDurationTracked.TotalMilliseconds;
                        var lastSampleTimestamp = allAudioSamples[allAudioSamples.Length - 1].TimestampNs;
                        var firstSampleTimestamp = allAudioSamples[0].TimestampNs;
                        var audioDurationMs = (lastSampleTimestamp - firstSampleTimestamp) / 1_000_000.0;

                        Debug.WriteLine($"[StartVideoRecording] Video duration: {videoDurationMs:F0}ms, Audio duration: {audioDurationMs:F0}ms");

                        if (audioDurationMs > videoDurationMs + 50) // Audio is longer than video (+50ms tolerance)
                        {
                            // Calculate the cutoff timestamp - keep only the LAST (videoDuration) of audio
                            var cutoffTimestampNs = lastSampleTimestamp - (long)(videoDurationMs * 1_000_000);

                            // Find first sample at or after cutoff
                            int startIndex = 0;
                            for (int i = 0; i < allAudioSamples.Length; i++)
                            {
                                if (allAudioSamples[i].TimestampNs >= cutoffTimestampNs)
                                {
                                    startIndex = i;
                                    break;
                                }
                            }

                            if (startIndex > 0)
                            {
                                var trimmedLength = allAudioSamples.Length - startIndex;
                                _preRecordedAudioSamples = new AudioSample[trimmedLength];
                                Array.Copy(allAudioSamples, startIndex, _preRecordedAudioSamples, 0, trimmedLength);
                                Debug.WriteLine($"[StartVideoRecording] SYNC FIX: Trimmed {startIndex} samples from start, keeping {trimmedLength} samples");
                            }
                            else
                            {
                                _preRecordedAudioSamples = allAudioSamples;
                            }
                        }
                        else
                        {
                            _preRecordedAudioSamples = allAudioSamples;
                        }
                    }
                    else
                    {
                        _preRecordedAudioSamples = allAudioSamples;
                    }

                    Debug.WriteLine($"[StartVideoRecording] Saved {_preRecordedAudioSamples?.Length ?? 0} pre-rec audio samples (after sync trim)");

                    // OOM-SAFE: Start streaming audio to file instead of linear buffer
                    // This prevents memory growth for long recordings
                    var firstSample = _preRecordedAudioSamples?.FirstOrDefault();
                    int sampleRate = firstSample?.SampleRate ?? AudioSampleRate;
                    int channels = firstSample?.Channels ?? AudioChannels;
                    if (StartLiveAudioWriter(sampleRate, channels))
                    {
                        Debug.WriteLine("[StartVideoRecording] Started STREAMING audio writer for live phase (OOM-safe)");
                    }
                    else
                    {
                        Debug.WriteLine("[StartVideoRecording] Warning: Failed to start streaming audio writer");
                    }

                    // Clear the pre-rec buffer (samples are saved in _preRecordedAudioSamples)
                    _audioBuffer = null;
                }

                IsPreRecording = false;
                IsRecordingVideo = true;

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // Start recording to file (will be muxed with pre-recorded file later)
                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    await StartCaptureVideoFlow();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
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
                    foreach(var device in discoverySession.Devices)
                    {
                        detected.Add(device.LocalizedName);
                    }
                }
            }
            catch(Exception e)
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
    /// </summary>
    private bool StartLiveAudioWriter(int sampleRate, int channels)
    {
        lock (_liveAudioWriterLock)
        {
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
            exportSession.ShouldOptimizeForNetworkUse = true;

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

    //end of class declaration
}

#endif

