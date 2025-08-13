#if IOS || MACCATALYST

using Foundation;
using UIKit;
using Photos;

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
    }

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform()
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
}
#endif
