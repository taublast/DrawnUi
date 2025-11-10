#if IOS || MACCATALYST

using AVFoundation;
using DrawnUi.Maui.Navigation;
using Foundation;
using Photos;
using UIKit;

namespace DrawnUi.Camera;

public partial class SkiaCamera
{
 
 
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
                System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] File not found and not a valid assets-library URL: {imageFilePathOrUrl}");
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
    private static void ShowPhotoFromAssetsLibrary(string assetsLibraryUrl)
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
                System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] Asset not found for identifier: {localIdentifier}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] Error showing photo from assets-library: {ex.Message}");
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
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        photoViewController.SetImage(image);
                    });
                }
            });

        viewController.PresentViewController(photoViewController, true, null);
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
            var tapGesture = new UITapGestureRecognizer(() =>
            {
                DismissViewController(true, null);
            });
            View.AddGestureRecognizer(tapGesture);

            // Add double tap to zoom
            var doubleTapGesture = new UITapGestureRecognizer(HandleDoubleTap)
            {
                NumberOfTapsRequired = 2
            };
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

            var horizontalPadding = imageViewSize.Width < scrollViewSize.Width ?
                (scrollViewSize.Width - imageViewSize.Width) / 2 : 0;
            var verticalPadding = imageViewSize.Height < scrollViewSize.Height ?
                (scrollViewSize.Height - imageViewSize.Height) / 2 : 0;

            scrollView.ContentInset = new UIEdgeInsets(verticalPadding, horizontalPadding, verticalPadding, horizontalPadding);
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
        return status == PermissionStatus.Granted;
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
    /// Mux pre-recorded and live video files using AVAssetComposition
    /// </summary>
    private async Task<string> MuxVideosInternal(string preRecordedPath, string liveRecordingPath, string outputPath)
    {
        try
        {
            // Log input/output file paths for debugging
            System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Input files:");
            System.Diagnostics.Debug.WriteLine($"  Pre-recorded: {preRecordedPath} (exists: {File.Exists(preRecordedPath)})");
            System.Diagnostics.Debug.WriteLine($"  Live recording: {liveRecordingPath} (exists: {File.Exists(liveRecordingPath)})");
            System.Diagnostics.Debug.WriteLine($"  Output: {outputPath}");

            using (var preAsset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(preRecordedPath)))
            using (var liveAsset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(liveRecordingPath)))
            {
                if (preAsset == null || liveAsset == null)
                    throw new InvalidOperationException("Failed to load video assets");

                var composition = new AVFoundation.AVMutableComposition();
                var videoTrack = composition.AddMutableTrack(AVMediaTypes.Video.GetConstant(), 0);

                if (videoTrack == null)
                    throw new InvalidOperationException("Failed to create video track");

                var currentTime = CoreMedia.CMTime.Zero;

                // Add pre-recorded video
                var preTracks = preAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant());
                if (preTracks != null && preTracks.Length > 0)
                {
                    var preTrack = preTracks[0];
                    var preRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = preAsset.Duration };
                    videoTrack.InsertTimeRange(preRange, preTrack, currentTime, out var error);
                    if (error != null)
                        throw new InvalidOperationException($"Failed to insert pre-recorded track: {error.LocalizedDescription}");
                    
                    currentTime = CoreMedia.CMTime.Add(currentTime, preAsset.Duration);
                }

                // Add live recording video
                var liveTracks = liveAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant());
                if (liveTracks != null && liveTracks.Length > 0)
                {
                    var liveTrack = liveTracks[0];
                    var liveRange = new CoreMedia.CMTimeRange { Start = CoreMedia.CMTime.Zero, Duration = liveAsset.Duration };
                    videoTrack.InsertTimeRange(liveRange, liveTrack, currentTime, out var error);
                    if (error != null)
                        throw new InvalidOperationException($"Failed to insert live track: {error.LocalizedDescription}");
                }

                // Export composition to file
                // CRITICAL: AVAssetExportSession fails if output file already exists
                if (File.Exists(outputPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Deleting existing output file: {outputPath}");
                    try { File.Delete(outputPath); } catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MuxVideosApple] Warning: Failed to delete existing output file: {ex.Message}");
                    }
                }

                var outputUrl = Foundation.NSUrl.FromFilename(outputPath);
                var exporter = new AVFoundation.AVAssetExportSession(composition, AVFoundation.AVAssetExportSessionPreset.MediumQuality)
                {
                    OutputUrl = outputUrl,
                    OutputFileType = AVFoundation.AVFileTypes.Mpeg4.GetConstant(),
                    ShouldOptimizeForNetworkUse = false
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
}
#endif

