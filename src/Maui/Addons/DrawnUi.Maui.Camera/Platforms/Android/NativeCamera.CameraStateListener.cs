using Android.Hardware.Camera2;

namespace DrawnUi.Camera
{
    public partial class NativeCamera
    {
        public class CameraStateListener : CameraDevice.StateCallback
        {
            private readonly NativeCamera owner;

            public CameraStateListener(NativeCamera owner)
            {
                if (owner == null)
                    throw new System.ArgumentNullException("owner");
                this.owner = owner;
            }

            public override void OnClosed(CameraDevice camera)
            {
                //base.OnClosed(camera);

                // owner.StopBackgroundThread();
            }

            public override void OnOpened(CameraDevice cameraDevice)
            {
                try
                {
                    // This method is called when the camera is opened.  We start camera preview here.
                    owner.mCameraOpenCloseLock.Release();
                    owner.mCameraDevice = cameraDevice;
                    owner.CreateCameraPreviewSession();
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }

            public override void OnDisconnected(CameraDevice cameraDevice)
            {
                try
                {
                    cameraDevice.Close();

                    owner.mCameraDevice = null;

                    owner.mCameraOpenCloseLock.Release();
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }

            }

            public override void OnError(CameraDevice cameraDevice, CameraError error)
            {

                if (owner == null)
                    return;

                try
                {
                    owner.mCameraOpenCloseLock.Release();
                    cameraDevice.Close();
                    owner.mCameraDevice = null;
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }

            }

        }
    }
}
