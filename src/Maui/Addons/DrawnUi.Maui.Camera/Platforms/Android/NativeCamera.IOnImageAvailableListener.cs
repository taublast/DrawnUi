using Android.Media;
using AppoMobi.Specials;
using SkiaSharp.Views.Android;
using Exception = System.Exception;
using Trace = System.Diagnostics.Trace;

namespace DrawnUi.Camera;

public partial class NativeCamera : Java.Lang.Object, ImageReader.IOnImageAvailableListener, INativeCamera
{
    /// <summary>
    /// IOnImageAvailableListener - acquire immediately and hand off only the latest image
    /// to the processing thread so ImageReader is drained as soon as frames arrive.
    /// </summary>
    public void OnImageAvailable(ImageReader reader)
    {
        if (reader == null)
            return;

        if (_suspendFrameProcessing)
        {
            reader.AcquireLatestImage()?.Close();
            return;
        }

        // When skipping frames (not ready / still capture in progress) we must
        // still drain the ImageReader queue so the HAL ring buffer never stalls.
        if (FormsControl.Height <= 0 || FormsControl.Width <= 0 || CapturingStill)
        {
            reader.AcquireLatestImage()?.Close();
            return;
        }

        FramesReader = reader;

        var newImage = reader.AcquireLatestImage();
        if (newImage == null)
            return;

        Android.Media.Image oldImage = null;
        lock (_imageLock)
        {
            oldImage = _currentImage;
            _currentImage = newImage;
        }

        oldImage?.Close();

        // Signal the processing thread after publishing the latest frame.
        _frameAvailable?.Set();
    }
}
