﻿using Android.Media;
using AppoMobi.Specials;
using SkiaSharp.Views.Android;
using Exception = System.Exception;
using Trace = System.Diagnostics.Trace;

namespace DrawnUi.Camera;

public partial class NativeCamera : Java.Lang.Object, ImageReader.IOnImageAvailableListener, INativeCamera
{

    /// <summary>
    /// IOnImageAvailableListener
    /// </summary>
    /// <param name="reader"></param>
    public void OnImageAvailable(ImageReader reader)
    {
        lock (lockProcessingPreviewFrame)
        {
            if (lockProcessing || FormsControl.Height <= 0 || FormsControl.Width <= 0 || CapturingStill)
                return;

            FramesReader = reader;

            if (Output != null)
            {
                var allocated = Output;

                lockProcessing = true;

                Android.Media.Image image = null;
                try
                {
                    // ImageReader
                    image = reader.AcquireLatestImage();
                    if (image != null)
                    {
                        if (allocated.Allocation != null && allocated.Bitmap is { Width: > 0, Height: > 0 })
                        {
                            ProcessImage(image, allocated.Allocation);
                            allocated.Update();

                            // During capture video flow recording, avoid any UI preview work.
                            bool inCaptureRecording = FormsControl.UseCaptureVideoFlow && FormsControl.IsRecordingVideo;

                            // Convert to SKImage once per frame (needed for encoder when using event-driven capture)
                            var sk = allocated.Bitmap.ToSKImage();
                            if (sk != null)
                            {
                                var meta = FormsControl.CameraDevice.Meta;
                                var rotation = FormsControl.DeviceRotation;
                                Metadata.ApplyRotation(meta, rotation);

                                // Use Android sensor timestamp (ns since boot) to populate Time as a monotonic clock
                                // Convert ns -> microseconds -> ticks (1 tick = 100 ns)
                                var tsNs = image.Timestamp;
                                var micros = tsNs / 1000L;
                                var monotonicTime = new DateTime(micros * 10, DateTimeKind.Utc);

                                var outImage = new CapturedImage()
                                {
                                    Facing = FormsControl.Facing,
                                    Time = monotonicTime,
                                    Image = sk,
                                    Meta = meta,
                                    Rotation = rotation
                                };

                                // Always notify encoder path
                                OnPreviewCaptureSuccess(outImage);

                                // Only push to UI preview when NOT recording in capture flow
                                if (!inCaptureRecording)
                                {
                                    Preview = outImage;
                                    FormsControl.UpdatePreview();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                }
                finally
                {
                    if (image != null)
                    {
                        //lockAllocation = false;
                        image.Close();
                    }

                    lockProcessing = false;
                }
            }
        }
    }
}
