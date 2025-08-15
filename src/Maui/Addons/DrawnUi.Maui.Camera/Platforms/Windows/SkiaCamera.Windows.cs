using System.Diagnostics;

namespace DrawnUi.Camera;

public partial class SkiaCamera : SkiaControl
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

    public static void OpenFileInGallery(string imageFilePath)
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

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform(bool refresh)
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

    protected async Task<List<CaptureFormat>> GetAvailableCaptureFormatsPlatform()
    {
        var formats = new List<CaptureFormat>();

        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            // Find current camera device or use default
            var currentDevice = devices.FirstOrDefault(d =>
                (Facing == CameraPosition.Selfie && d.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Front) ||
                (Facing == CameraPosition.Default && d.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Back))
                ?? devices.FirstOrDefault();

            if (currentDevice != null)
            {
                var frameSourceGroups = await Windows.Media.Capture.Frames.MediaFrameSourceGroup.FindAllAsync();
                var selectedGroup = frameSourceGroups.FirstOrDefault(g =>
                    g.SourceInfos.Any(si => si.DeviceInformation?.Id == currentDevice.Id));

                if (selectedGroup != null)
                {
                    using var mediaCapture = new Windows.Media.Capture.MediaCapture();
                    var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings
                    {
                        SourceGroup = selectedGroup,
                        SharingMode = Windows.Media.Capture.MediaCaptureSharingMode.SharedReadOnly,
                        StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Video,
                        MemoryPreference = Windows.Media.Capture.MediaCaptureMemoryPreference.Cpu
                    };

                    await mediaCapture.InitializeAsync(settings);

                    var frameSource = mediaCapture.FrameSources.Values.FirstOrDefault(s =>
                        s.Info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord);

                    if (frameSource?.SupportedFormats != null)
                    {
                        // Get unique resolutions (remove duplicates from different pixel formats)
                        var uniqueResolutions = frameSource.SupportedFormats
                            .Where(f => f.VideoFormat.Width > 0 && f.VideoFormat.Height > 0)
                            .GroupBy(f => new { f.VideoFormat.Width, f.VideoFormat.Height })
                            .Select(group => group.First())
                            .OrderByDescending(f => f.VideoFormat.Width * f.VideoFormat.Height)
                            .ToList();

                        Debug.WriteLine($"[SkiaCameraWindows] Found {uniqueResolutions.Count} unique video formats:");

                        for (int i = 0; i < uniqueResolutions.Count; i++)
                        {
                            var format = uniqueResolutions[i];
                            Debug.WriteLine($"  [{i}] {format.VideoFormat.Width}x{format.VideoFormat.Height}");

                            formats.Add(new CaptureFormat
                            {
                                Width = (int)format.VideoFormat.Width,
                                Height = (int)format.VideoFormat.Height,
                                FormatId = $"windows_{currentDevice.Id}_{i}"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraWindows] Error getting capture formats: {ex.Message}");
        }

        return formats;
    }

    /// <summary>
    /// Updates preview format to match current capture format aspect ratio
    /// </summary>
    protected virtual void UpdatePreviewFormatForAspectRatio()
    {
        if (NativeControl is NativeCamera windowsCamera)
        {
            Debug.WriteLine("[SkiaCameraWindows] Updating preview format for aspect ratio match");

            // Trigger preview format update in native camera
            Task.Run(async () =>
            {
                try
                {
                    await windowsCamera.UpdatePreviewFormatAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SkiaCameraWindows] Error updating preview format: {ex.Message}");
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
}
