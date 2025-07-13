using Android.Hardware.Camera2;
using Android.Media;
using Java.Lang;
using Exception = System.Exception;
using Image = Android.Media.Image;

namespace DrawnUi.Camera
{
    public partial class NativeCamera
    {
        public class StillPhotoCaptureCallback : CameraCaptureSession.CaptureCallback,
            Android.Media.ImageReader.IOnImageAvailableListener
        {
            private readonly NativeCamera _camera;

            public StillPhotoCaptureCallback(NativeCamera camera)
            {
                if (camera == null)
                    throw new System.ArgumentNullException("camera");

                this._camera = camera;
            }


            public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
            {
                Process(result);
            }

            public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
            {
                Process(partialResult);
            }

            public void OnImageAvailable(ImageReader reader)
            {

                Image image = null;
                try
                {
                    System.Diagnostics.Debug.WriteLine("[CAMERA] AcquireLatestImage..");

                    image = reader.AcquireNextImage();
                    if (image != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[CAMERA] Processing still image..");
                        _camera.OnCapturedStillImage(image);
                    }
                }
                catch (Exception e)
                {
                    Super.Log(e);
                    _camera.OnCaptureError(e);
                }
                finally
                {
                    if (image != null)
                    {
                        image.Close();
                    }
                    _camera.CapturingStill = false;
                    System.Diagnostics.Debug.WriteLine("[CAMERA] Still capture finished");
                }
            }

            private void Process(CaptureResult result)
            {
                NativeCamera.FillMetadata(_camera.FormsControl.CameraDevice.Meta, result);

                switch (_camera.mState)
                {
                    case NativeCamera.STATE_WAITING_LOCK:
                    {
                        Integer afState = (Integer)result.Get(CaptureResult.ControlAfState);
                        if (afState == null)
                        {
                            _camera.mState = NativeCamera.STATE_PICTURE_TAKEN; // avoids multiple picture callbacks
                            _camera.StartCapturingStill();
                        }
                        else
                        if (
                            afState == null ||
                            ((int)ControlAFState.FocusedLocked) == afState.IntValue() ||
                            ((int)ControlAFState.NotFocusedLocked) == afState.IntValue() ||
                            ((int)ControlAFState.Inactive) == afState.IntValue())
                        {
                            // ControlAeState can be null on some devices
                            // also they can not be supporting auto-focus

                            Integer aeState = (Integer)result.Get(CaptureResult.ControlAeState);
                            if (aeState == null ||
                                aeState.IntValue() == ((int)ControlAEState.Converged))
                            {
                                _camera.mState = NativeCamera.STATE_PICTURE_TAKEN;
                                _camera.StartCapturingStill();
                            }
                            else
                            {
                                _camera.RunPrecaptureSequence();
                            }
                        }

                        break;
                    }

                    case NativeCamera.STATE_WAITING_PRECAPTURE:
                    {
                        // ControlAeState can be null on some devices
                        Integer aeState = (Integer)result.Get(CaptureResult.ControlAeState);
                        if (aeState == null ||
                            aeState.IntValue() == ((int)ControlAEState.Precapture) ||
                            aeState.IntValue() == ((int)ControlAEState.FlashRequired))
                        {
                            _camera.mState = NativeCamera.STATE_WAITING_NON_PRECAPTURE;
                        }
                        break;
                    }

                    case NativeCamera.STATE_WAITING_NON_PRECAPTURE:
                    {
                        // ControlAeState can be null on some devices
                        Integer aeState = (Integer)result.Get(CaptureResult.ControlAeState);
                        if (aeState == null || aeState.IntValue() != ((int)ControlAEState.Precapture))
                        {
                            _camera.mState = NativeCamera.STATE_PICTURE_TAKEN;

                            _camera.StartCapturingStill();
                        }
                        break;
                    }
                }
            }
        }
    }
}
