using Android.Media;
using AppoMobi.Specials;
using SkiaSharp.Views.Android;
using Exception = System.Exception;
using Trace = System.Diagnostics.Trace;

namespace DrawnUi.Camera;

public partial class NativeCamera : Java.Lang.Object, ImageReader.IOnImageAvailableListener, INativeCamera
{
    /// <summary>
    /// IOnImageAvailableListener - signals the processing thread only.
    /// Acquisition happens inside FrameProcessingLoop so at most 1 image
    /// is ever outstanding, making the maxImages overflow structurally impossible.
    /// </summary>
    public void OnImageAvailable(ImageReader reader)
    {
        if (reader == null)
            return;

        // When skipping frames (not ready / still capture in progress) we must
        // still drain the ImageReader queue so the HAL ring buffer never stalls.
        if (FormsControl.Height <= 0 || FormsControl.Width <= 0 || CapturingStill)
        {
            reader.AcquireLatestImage()?.Close();
            return;
        }

        FramesReader = reader;

        // Signal the processing thread — acquisition happens there, not here.
        _frameAvailable?.Set();
    }
}
