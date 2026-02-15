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
        // Count ALL incoming frames for raw FPS calculation (before any filtering)
        _rawFrameCount++;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_rawFrameLastReportTime == 0)
        {
            _rawFrameLastReportTime = now;
        }
        else
        {
            var elapsedTicks = now - _rawFrameLastReportTime;
            var elapsedSeconds = (double)elapsedTicks / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedSeconds >= 1.0) // Report every second
            {
                _rawFrameFps = _rawFrameCount / elapsedSeconds;
                System.Diagnostics.Debug.WriteLine($"[NativeCameraAndroid] RAW camera FPS: {_rawFrameFps:F1} (frames: {_rawFrameCount} in {elapsedSeconds:F2}s)");
                _rawFrameCount = 0;
                _rawFrameLastReportTime = now;
            }
        }

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

        try
        {
            ProcessFrameOnBackgroundThread(newImage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NativeCamera] Error processing frame: {ex.Message}");
        }
        finally
        {
            newImage.Close();   // Return to ImageReader pool
        }

        return;

        // Swap into current slot (lock held for ~10ns - just pointer swap)
        Android.Media.Image oldImage = null;
        lock (_imageLock)
        {
            oldImage = _currentImage;
            _currentImage = newImage;
        }

        // Close old frame OUTSIDE lock (if processor was slow, frame is dropped)
        oldImage?.Close();

        // Signal processing thread (non-blocking)
        _frameAvailable?.Set();

        // All heavy work (RenderScript, SKImage, callbacks) happens on background thread
        // FrameProcessingLoop inside NativeCamera.Android.cs
    }
}
