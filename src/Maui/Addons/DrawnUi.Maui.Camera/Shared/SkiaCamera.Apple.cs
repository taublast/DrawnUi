#if IOS || MACCATALYST
using Foundation;
using UIKit;

namespace DrawnUi.Camera;

public partial class SkiaCamera
{
    public virtual void TurnOnFlash()
    {
        NativeControl?.TurnOnFlash();
    }

    public virtual void TurnOffFlash()
    {
        NativeControl?.TurnOffFlash();
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

    public void OpenFileInGallery(string imageFilePath)
    {
        try
        {
            if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] File not found: {imageFilePath}");
                return;
            }

            var fileUrl = NSUrl.FromFilename(imageFilePath);
            var documentController = UIDocumentInteractionController.FromUrl(fileUrl);

            // Get the current view controller
            var viewController = Platform.GetCurrentUIViewController();
            if (viewController != null)
            {
                // Present the document interaction controller
                documentController.PresentPreview(true);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SkiaCamera Apple] Could not get current view controller");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCamera Apple] Error opening file in gallery: {ex.Message}");
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
