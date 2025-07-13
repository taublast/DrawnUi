using System.Diagnostics;

namespace DrawnUi.Camera;

public partial class SkiaCamera : SkiaControl
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
        Task.Run(async () =>
        {
            try
            {
                if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
                {
                    Debug.WriteLine($"[SkiaCamera Windows] File not found: {imageFilePath}");
                    return;
                }

                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imageFilePath);
                var success = await Windows.System.Launcher.LaunchFileAsync(file);

                if (!success)
                {
                    Debug.WriteLine($"[SkiaCamera Windows] Failed to launch file: {imageFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera Windows] Error opening file in gallery: {ex.Message}");
            }
        });
    }

    public virtual Metadata CreateMetadata()
    {
        return new Metadata()
        {
            Software = "SkiaCamera Windows",
            Vendor = Environment.MachineName,
            Model = Environment.OSVersion.ToString(),
        };
    }

    protected virtual void CreateNative()
    {
        if (!IsOn || NativeControl != null)
        {
            Debug.WriteLine($"[SkiaCameraWindows] CreateNative skipped - IsOn: {IsOn}, NativeControl exists: {NativeControl != null}");
            return;
        }

        Debug.WriteLine("[SkiaCameraWindows] Creating native camera...");
        NativeControl = new NativeCamera(this);
        Debug.WriteLine("[SkiaCameraWindows] Native camera created");
    }

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform()
    {
        var cameras = new List<CameraInfo>();

        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var position = CameraPosition.Default;

                if (device.EnclosureLocation?.Panel != null)
                {
                    position = device.EnclosureLocation.Panel switch
                    {
                        Windows.Devices.Enumeration.Panel.Front => CameraPosition.Selfie,
                        Windows.Devices.Enumeration.Panel.Back => CameraPosition.Default,
                        _ => CameraPosition.Default
                    };
                }

                cameras.Add(new CameraInfo
                {
                    Id = device.Id,
                    Name = device.Name,
                    Position = position,
                    Index = i,
                    HasFlash = false // TODO: Detect flash support
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraWindows] Error enumerating cameras: {ex.Message}");
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
