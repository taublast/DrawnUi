using Android.Content;
using Android.Hardware.Camera2;
using Android.Telecom;


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


    public void OpenFileInGallery(string imageFilePath)
    {
        // Create a new Intent
        Intent intent = new Intent();

        // Set the Intent action to ACTION_VIEW (view the data)
        intent.SetAction(Intent.ActionView);

        // Create a Uri from the file path
        var photoUri = FileProvider.GetUriForFile(Platform.AppContext, Platform.AppContext.PackageName + ".provider", new Java.IO.File(imageFilePath));

        // Set the Intent data to the Uri
        intent.SetDataAndType(photoUri, "image/*");

        // Grant temporary permission to external app to use FileProvider
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

        // Start the external activity
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

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform()
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
