using Android.Content;
using Android.Hardware.Camera2;
using Android.Telecom;


namespace DrawnUi.Camera;

public partial class SkiaCamera
{
    
    public virtual void SetZoom(double value)
    {
        // Hardware zoom not supported on Android currently, using manual scaling
        TextureScale = value;

        //in theory nativecontrol should set TextureScale regarding on the amount it was able to set using hardware
        //so the remaining zoom comes from scaling the output texture (preview)
        NativeControl.SetZoom((float)value);

        //temporary hack - preview is our texture
        Display.ZoomX = TextureScale;
        Display.ZoomY = TextureScale;

        Zoomed?.Invoke(this, value);
    }

    /// <summary>
    /// Updates preview format to match current capture format aspect ratio.
    /// Android implementation: Restarts camera session to apply new format selection.
    /// </summary>
    protected virtual void UpdatePreviewFormatForAspectRatio()
    {
        if (NativeControl is NativeCamera androidCamera)
        {
            System.Diagnostics.Debug.WriteLine("[SkiaCameraAndroid] Updating preview format for aspect ratio match");

            // Android's ChooseOptimalSize() automatically matches aspect ratios during setup
            // We need to restart the camera session to apply the new capture format
            Task.Run(async () =>
            {
                try
                {
                    // Stop current session
                    androidCamera.Stop();

                    // Small delay to ensure cleanup
                    await Task.Delay(100);

                    // Restart with new format - this will trigger ChooseOptimalSize()
                    // with the new capture format as aspect ratio target
                    androidCamera.Start();

                    System.Diagnostics.Debug.WriteLine("[SkiaCameraAndroid] Camera session restarted for format change");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error updating preview format: {ex.Message}");
                }
            });
        }
    }

    /// <summary>
    /// Opens a file in the gallery app
    /// </summary>
    /// <param name="imageFilePath">File path or content URI</param>
    public static void OpenFileInGallery(string imageFilePath)
    {
        Intent intent = new Intent();
        intent.SetAction(Intent.ActionView);
        Android.Net.Uri photoUri;

        if (imageFilePath.StartsWith("content://"))
        {
            photoUri = Android.Net.Uri.Parse(imageFilePath);
        }
        else
        {
            var file = new Java.IO.File(imageFilePath);
            if (!file.Exists())
            {
                throw new FileNotFoundException($"File not found: {imageFilePath}");
            }
            photoUri = FileProvider.GetUriForFile(
                Platform.AppContext,
                Platform.AppContext.PackageName + ".provider",
                file);
        }

        intent.SetDataAndType(photoUri, "image/*");
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.GrantReadUriPermission);
        Platform.AppContext.StartActivity(intent);
    }


    public virtual Metadata CreateMetadata()
    {
        return new Metadata()
        {
            Software = "SkiaCamera Android",
            Vendor = $"{Android.OS.Build.Manufacturer}",
            Model = $"{Android.OS.Build.Model}",

            //this will be created inside session
            //Orientation = (int)result.Get(CaptureResult.JpegOrientation),
            //ISO = (int)result.Get(CaptureResult.SensorSensitivity),
            //FocalLength = (float)result.Get(CaptureResult.LensFocalLength)
        };
    }



    protected virtual void CreateNative()
    {
        if (!IsOn || NativeControl != null)
            return;

        DisableOtherCameras();

        NativeControl = new NativeCamera(this);

        //OnUpdateOrientation(null, null);

        //SubscribeToNativeControl();
    }

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform(bool refresh)
    {
        var cameras = new List<CameraInfo>();

        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            var manager = (Android.Hardware.Camera2.CameraManager)context.GetSystemService(Android.Content.Context.CameraService);
            var cameraIds = manager.GetCameraIdList();

            for (int i = 0; i < cameraIds.Length; i++)
            {
                var cameraId = cameraIds[i];
                var characteristics = manager.GetCameraCharacteristics(cameraId);

                var facing = (Java.Lang.Integer)characteristics.Get(Android.Hardware.Camera2.CameraCharacteristics.LensFacing);
                var position = CameraPosition.Default;

                if (facing != null)
                {
                    position = facing.IntValue() switch
                    {
                        (int)Android.Hardware.Camera2.LensFacing.Front => CameraPosition.Selfie,
                        (int)Android.Hardware.Camera2.LensFacing.Back => CameraPosition.Default,
                        _ => CameraPosition.Default
                    };
                }

                var flashAvailable = (Java.Lang.Boolean)characteristics.Get(Android.Hardware.Camera2.CameraCharacteristics.FlashInfoAvailable);

                cameras.Add(new CameraInfo
                {
                    Id = cameraId,
                    Name = $"Camera {i} ({position})",
                    Position = position,
                    Index = i,
                    HasFlash = flashAvailable?.BooleanValue() ?? false
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error enumerating cameras: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error getting capture formats: {ex.Message}");
        }

        return formats;
    }

    public void DisableOtherCameras(bool all = false)
    {
        foreach (var renderer in Instances)
        {
            System.Diagnostics.Debug.WriteLine($"[CAMERA] DisableOtherCameras..");
            bool disable = false;
            if (all || renderer != this)
            {
                disable = true;
            }

            if (disable)
            {
                renderer.StopInternal(true);
                System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {renderer.Uid} {renderer.Tag}");
            }
        }
    }


    /// <summary>
    /// Call on UI thread only. Called by CheckPermissions.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> RequestPermissions()
    {
        var status = await Permissions
            .CheckStatusAsync<Permissions.Camera>();

        return status == PermissionStatus.Granted;
    }



}
