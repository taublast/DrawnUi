using Android.Media;
using AppoMobi.Specials;
using SkiaSharp.Views.Android;
using Exception = System.Exception;
using Trace = System.Diagnostics.Trace;

namespace DrawnUi.Camera;

public partial class NativeCamera : Java.Lang.Object, ImageReader.IOnImageAvailableListener, INativeCamera
{
    /// <summary>
    /// IOnImageAvailableListener - iOS-style minimal callback
    /// Heavy processing moved to background thread via ProcessFrameOnBackgroundThread
    /// </summary>
    /// <param name="reader"></param>
    public void OnImageAvailable(ImageReader reader)
    {
        // Skip if not ready
        if (FormsControl.Height <= 0 || FormsControl.Width <= 0 || CapturingStill)
            return;

        FramesReader = reader;

        // FAST: Get latest frame (drops older frames automatically)
        Android.Media.Image? newImage = reader.AcquireLatestImage();
        if (newImage == null)
        {
            return;
        }

        // Swap into current slot — fast pointer swap, drops previous unprocessed frame
        Android.Media.Image oldImage = null;
        lock (_imageLock)
        {
            oldImage = _currentImage;
            _currentImage = newImage;
        }

        // Close old frame OUTSIDE lock (if processor was slow, frame is dropped)
        oldImage?.Close();

        // Signal processing thread (non-blocking)
        // All heavy work (RenderScript, SKImage, callbacks) happens on FrameProcessingLoop background thread
        _frameAvailable?.Set();
    }
}
