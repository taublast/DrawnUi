using Android.Hardware.Camera2;

namespace DrawnUi.Camera
{
    public partial class NativeCamera
    {
        public class StillPhotoCaptureFinishedCallback : CameraCaptureSession.CaptureCallback
        {
            private readonly NativeCamera _camera;

            public StillPhotoCaptureFinishedCallback(NativeCamera camera)
            {
                if (camera == null)
                    throw new System.ArgumentNullException("camera");

                this._camera = camera;
            }

            public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
            {
                //var ControlAfMode = result.Get(CaptureResult.ControlAfMode);

                var meta = _camera.FormsControl.CreateMetadata();
                meta.Vendor = $"{Android.OS.Build.Manufacturer}";
                meta.Model = $"{Android.OS.Build.Model}";

                NativeCamera.FillMetadata(meta, result);

                _camera.FormsControl.CameraDevice.Meta = meta;
            }
        }
    }
}
