using Android.Hardware.Camera2;

namespace DrawnUi.Camera
{
    public partial class NativeCamera
    {
        public class CameraCaptureSessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly NativeCamera owner;

            public CameraCaptureSessionCallback(NativeCamera owner)
            {
                if (owner == null)
                    throw new System.ArgumentNullException("owner");
                this.owner = owner;
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                //owner.ShowToast(ResStrings.Error);
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                // The camera is already closed
                if (null == owner.mCameraDevice)
                {
                    return;
                }

                // When the session is ready, we start displaying the preview.
                owner.CaptureSession = session;
                try
                {
                    // Auto focus should be continuous for camera preview.
                    owner.mPreviewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);

                    owner.mPreviewRequestBuilder.AddTarget(owner.mImageReaderPreview.Surface);

                    // Flash is automatically enabled when necessary.
                    owner.SetCapturingStillOptions(owner.mPreviewRequestBuilder);

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
                    //owner.ShowToast(ResStrings.Error);
                }
            }
        }
    }
}
