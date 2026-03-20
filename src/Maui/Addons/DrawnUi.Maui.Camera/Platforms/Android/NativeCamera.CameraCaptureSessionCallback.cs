using Android.Hardware.Camera2;

namespace DrawnUi.Camera
{
    public partial class NativeCamera
    {
        /// <summary>
        /// Session callback for the temporary still-capture session.
        /// Once configured, immediately fires the single still capture,
        /// then StopCapturingStillImage() restores the lean preview session.
        /// </summary>
        public class StillCaptureCameraSessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly NativeCamera owner;

            public StillCaptureCameraSessionCallback(NativeCamera owner)
            {
                this.owner = owner ?? throw new System.ArgumentNullException("owner");
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                if (owner.mCameraDevice == null)
                {
                    owner.CapturingStill = false;
                    return;
                }
                owner.CaptureSession = session;
                owner.DoActualStillCapture();
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                System.Diagnostics.Debug.WriteLine("[StillCapture] Session configuration FAILED");
                owner.CapturingStill = false;
                owner.OnCaptureError(new System.Exception("Still capture session configuration failed"));
            }
        }

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
