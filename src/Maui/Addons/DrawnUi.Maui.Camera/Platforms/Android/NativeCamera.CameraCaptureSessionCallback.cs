using Android.Hardware.Camera2;

namespace DrawnUi.Camera
{
    public partial class NativeCamera
    {
        public class CameraCaptureSessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly NativeCamera owner;

            public CameraCaptureSessionCallback(IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
                : base(handle, transfer) { }

            public CameraCaptureSessionCallback(NativeCamera owner)
            {
                if (owner == null)
                    throw new System.ArgumentNullException("owner");
                this.owner = owner;
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                System.Diagnostics.Debug.WriteLine("[CameraCallback] OnConfigureFailed - session configuration FAILED!");
                //owner.ShowToast(ResStrings.Error);
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                // The camera is already closed
                if (null == owner.mCameraDevice)
                    return;

                // When the session is ready, we start displaying the preview.
                owner.CaptureSession = session;
                try
                {
                    // For GPU camera path (recording), targets are already set in CreateGpuCameraSession.
                    // For normal path, add the appropriate preview surface.
                    if (!owner._useGpuCameraPath)
                    {
                        if (owner._useGlPreview && owner._glPreviewRenderer != null)
                        {
                            owner.mPreviewRequestBuilder.AddTarget(owner._glPreviewRenderer.GetCameraOutputSurface());
                        }
                        else
                        {
                            owner.mPreviewRequestBuilder.AddTarget(owner.mImageReaderPreview.Surface);
                        }
                    }

                    // Apply preview-specific settings (focus mode only, flash already set in CreateCameraPreviewSession)
                    owner.SetPreviewOptions(owner.mPreviewRequestBuilder);

                    // Finally, we start displaying the camera preview.
                    owner.mPreviewRequest = owner.mPreviewRequestBuilder.Build();

                    owner.CaptureSession.SetRepeatingRequest(
                        owner.mPreviewRequest,
                        owner.mCaptureCallback,
                        owner.mBackgroundHandler);
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }
        }
    }
}
