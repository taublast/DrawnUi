using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Renderscripts;
using Android.Util;
using Android.Views;
using AppoMobi.Maui.Gestures;
using AppoMobi.Maui.Native.Droid.Graphics;
using Java.Lang;
using Java.Util.Concurrent;
using SkiaSharp.Views.Android;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AppoMobi.Specials;
using Boolean = System.Boolean;
using Debug = System.Diagnostics.Debug;
using Exception = System.Exception;
using Image = Android.Media.Image;
using Math = System.Math;
using Point = Android.Graphics.Point;
using Semaphore = Java.Util.Concurrent.Semaphore;
using Size = Android.Util.Size;
using StringBuilder = System.Text.StringBuilder;
using Trace = System.Diagnostics.Trace;

namespace DrawnUi.Camera;

public partial class NativeCamera : Java.Lang.Object, ImageReader.IOnImageAvailableListener, INativeCamera
{
    public static T Cast<T>(Java.Lang.Object obj) where T : class
    {
        var propertyInfo = obj.GetType().GetProperty("Instance");
        return propertyInfo == null ? null : propertyInfo.GetValue(obj, null) as T;
    }

    // Camera configuration constants

    // Max preview dimensions
    public int MaxPreviewWidth = 800;
    public int MaxPreviewHeight = 800;

    // Still capture formats - same pattern as Apple implementation
    public List<CaptureFormat> StillFormats { get; protected set; } = new List<CaptureFormat>();

    /// <summary>
    /// Setup still capture formats - same pattern as Apple implementation
    /// </summary>
    private void SetupStillFormats(Android.Hardware.Camera2.Params.StreamConfigurationMap map, string cameraId)
    {
        var formats = new List<CaptureFormat>();

        try
        {
            if (map != null)
            {
                // Get all available still sizes - same logic as before but centralized
                var stillSizes = map.GetOutputSizes((int)ImageFormatType.Yuv420888)
                    .Where(size => size.Width > 0 && size.Height > 0)
                    .GroupBy(size => new { size.Width, size.Height }) // Remove any potential duplicates
                    .Select(group => group.First())
                    .OrderByDescending(size => size.Width * size.Height)
                    .ToList();

                Debug.WriteLine($"[NativeCameraAndroid] Found {stillSizes.Count} unique YUV420 still capture formats for camera {cameraId}:");

                for (int i = 0; i < stillSizes.Count; i++)
                {
                    var size = stillSizes[i];
                    Debug.WriteLine($"  [{i}] {size.Width}x{size.Height}");

                    formats.Add(new CaptureFormat
                    {
                        Width = size.Width,
                        Height = size.Height,
                        FormatId = $"android_yuv_{cameraId}_{i}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraAndroid] Error setting up still formats: {ex.Message}");
        }

        StillFormats = formats;
        Debug.WriteLine($"[NativeCameraAndroid] Setup {StillFormats.Count} still formats");
    }

    public static void FillMetadata(Metadata meta, CaptureResult result)
    {
        // Get the camera's chosen exposure settings for "proper" exposure
        var measuredExposureTime = (long)result.Get(CaptureResult.SensorExposureTime);
        var measuredSensitivity = (int)result.Get(CaptureResult.SensorSensitivity);
        var measuredAperture = (float)result.Get(CaptureResult.LensAperture);
        var usedLens = (float)result.Get(CaptureResult.LensFocalLength);

        // Convert to standard units
        double shutterSpeed = measuredExposureTime / 1_000_000_000.0; // nanoseconds to seconds
        double iso = measuredSensitivity;
        double aperture = measuredAperture;

        meta.FocalLength = usedLens;
        meta.ISO = (int)iso;
        meta.Aperture = aperture;
        meta.Shutter = shutterSpeed;

        meta.Orientation = (int)result.Get(CaptureResult.JpegOrientation);
    }

    public void SetZoom(float zoom)
    {
        ZoomScale = zoom;
    }

    /// <summary>
    /// Sets manual exposure settings for the camera
    /// </summary>
    /// <param name="iso">ISO sensitivity value</param>
    /// <param name="shutterSpeed">Shutter speed in seconds</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool SetManualExposure(float iso, float shutterSpeed)
    {
        if (mCameraDevice == null || CaptureSession == null || mPreviewRequestBuilder == null)
        {
            System.Diagnostics.Debug.WriteLine("[Android MANUAL] Camera not initialized");
            return false;
        }

        try
        {
            // Set manual exposure mode
            mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.Off);

            // Set ISO (sensitivity)
            var isoValue = (int)Math.Max(100, Math.Min(3200, iso)); // Clamp to reasonable range
            mPreviewRequestBuilder.Set(CaptureRequest.SensorSensitivity, isoValue);

            // Set shutter speed (exposure time in nanoseconds)
            var exposureTimeNs = (long)(shutterSpeed * 1_000_000_000);
            mPreviewRequestBuilder.Set(CaptureRequest.SensorExposureTime, exposureTimeNs);

            mPreviewRequest = mPreviewRequestBuilder.Build();
            CaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback, mBackgroundHandler);

            System.Diagnostics.Debug.WriteLine($"[Android MANUAL] Set ISO: {isoValue}, Shutter: {shutterSpeed}s");

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Android MANUAL] Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets the camera to automatic exposure mode
    /// </summary>
    public void SetAutoExposure()
    {
        if (mCameraDevice == null || CaptureSession == null || mPreviewRequestBuilder == null)
        {
            System.Diagnostics.Debug.WriteLine("[Android AUTO] Camera not initialized");
            return;
        }

        try
        {
            // Set auto exposure mode
            mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);

            mPreviewRequest = mPreviewRequestBuilder.Build();
            CaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback, mBackgroundHandler);

            System.Diagnostics.Debug.WriteLine("[Android AUTO] Set to auto exposure mode");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Android AUTO] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the currently selected capture format
    /// </summary>
    /// <returns>Current capture format or null if not available</returns>
    public CaptureFormat GetCurrentCaptureFormat()
    {
        try
        {
            if (CaptureWidth > 0 && CaptureHeight > 0)
            {
                return new CaptureFormat
                {
                    Width = CaptureWidth,
                    Height = CaptureHeight,
                    FormatId = $"android_{CameraId}_{CaptureWidth}x{CaptureHeight}"
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NativeCameraAndroid] GetCurrentCaptureFormat error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the manual exposure capabilities and recommended settings for the camera
    /// </summary>
    /// <returns>Camera manual exposure range information</returns>
    public CameraManualExposureRange GetExposureRange()
    {
        if (CameraId == null)
        {
            return new CameraManualExposureRange(0, 0, 0, 0, false, null);
        }

        try
        {
            var activity = Platform.CurrentActivity;
            var manager = (CameraManager)activity.GetSystemService(Context.CameraService);
            var characteristics = manager.GetCameraCharacteristics(CameraId);

            // Check if manual exposure is supported
            bool isSupported = false;

            try
            {
                // Use ToArray<T>() extension method to convert Java array to C# array
                var availableModes = characteristics.Get(CameraCharacteristics.ControlAeAvailableModes).ToArray<int>();
                isSupported = availableModes?.Contains((int)ControlAEMode.Off) == true;
            }
            catch (Exception)
            {
                // Fallback: assume manual exposure is not supported
                isSupported = false;
            }

            if (!isSupported)
            {
                return new CameraManualExposureRange(0, 0, 0, 0, false, null);
            }

            // Get ISO range
            var isoRangeObj = characteristics.Get(CameraCharacteristics.SensorInfoSensitivityRange);
            var isoRange = isoRangeObj as Android.Util.Range;
            float minISO = isoRange?.Lower != null ? (float)(int)isoRange.Lower : 100f;
            float maxISO = isoRange?.Upper != null ? (float)(int)isoRange.Upper : 3200f;

            // Get exposure time range (in nanoseconds, convert to seconds)
            var exposureRangeObj = characteristics.Get(CameraCharacteristics.SensorInfoExposureTimeRange);
            var exposureRange = exposureRangeObj as Android.Util.Range;
            long minExposureNs = exposureRange?.Lower != null ? (long)exposureRange.Lower : 1000000L;
            long maxExposureNs = exposureRange?.Upper != null ? (long)exposureRange.Upper : 1_000_000_000L;
            float minShutter = minExposureNs / 1_000_000_000.0f; // Convert ns to seconds
            float maxShutter = maxExposureNs / 1_000_000_000.0f; // Convert ns to seconds

            var baselines = new CameraExposureBaseline[]
            {
                new CameraExposureBaseline(100, 1.0f / 60.0f, "Indoor", "Office/bright indoor lighting"),
                new CameraExposureBaseline(400, 1.0f / 30.0f, "Mixed", "Dim indoor/overcast outdoor"),
                new CameraExposureBaseline(800, 1.0f / 15.0f, "Low Light", "Evening/dark indoor")
            };

            System.Diagnostics.Debug.WriteLine(
                $"[Android RANGE] ISO: {minISO}-{maxISO}, Shutter: {minShutter}-{maxShutter}s");

            return new CameraManualExposureRange(minISO, maxISO, minShutter, maxShutter, true, baselines);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Android RANGE] Error: {ex.Message}");
            return new CameraManualExposureRange(0, 0, 0, 0, false, null);
        }
    }

    public void PublishFile(string filename, Metadata meta)
    {
        if (meta != null)
        {
            var newexif = new ExifInterface(filename);

            Metadata.FillExif(newexif, meta);

            newexif.SaveAttributes();
        }

        Java.IO.File file = new Java.IO.File(filename);
        Android.Net.Uri uri = Android.Net.Uri.FromFile(file);
        Platform.AppContext.SendBroadcast(new Intent(Intent.ActionMediaScannerScanFile, uri));
    }

    /// <summary>
    /// Will auto-select method upon android version: either save to camera folder, if lower that android 10, or use MediaStore. Will return path or uri like "content://..."
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="filename"></param>
    /// <param name="rotation"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    public async Task<string> SaveJpgStreamToGallery(System.IO.Stream stream, string filename,
        Metadata meta, string album)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            return await SaveJpgStreamToGalleryLegacy(stream, filename, meta, album);
        }

        var sub = "Camera";
        if (!string.IsNullOrEmpty(album))
            sub = album;

        var resolver = Platform.AppContext.ContentResolver;
        var contentValues = new ContentValues();
        contentValues.Put(MediaStore.MediaColumns.DisplayName, filename);
        contentValues.Put(MediaStore.MediaColumns.MimeType, "image/jpeg");
        contentValues.Put(MediaStore.MediaColumns.RelativePath, Android.OS.Environment.DirectoryDcim + "/" + sub);

        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri, contentValues);
        using (var outputStream = resolver.OpenOutputStream(uri))
        {
            await stream.CopyToAsync(outputStream);
        }

        return uri.ToString();
    }

    /// <summary>
    /// Use below android 10
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="filename"></param>
    /// <param name="rotation"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    public async Task<string> SaveJpgStreamToGalleryLegacy(System.IO.Stream stream, string filename,
        Metadata meta, string album)
    {
        string fullFilename = System.IO.Path.Combine(GetOutputGalleryFolder(album).AbsolutePath, filename);

        SaveStreamAsFile(stream, fullFilename);

        PublishFile(fullFilename, meta);

        return fullFilename;
    }

    public void SaveStreamAsFile(System.IO.Stream inputStream, string fullFilename)
    {
        using (FileStream outputFileStream = new FileStream(fullFilename, FileMode.Create))
        {
            inputStream.CopyTo(outputFileStream);
        }
    }

    public Java.IO.File GetOutputGalleryFolder(string album)
    {
        if (string.IsNullOrEmpty(album))
            album = "Camera";

        var jFolder =
            new Java.IO.File(
                Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDcim), album);

        if (!jFolder.Exists())
            jFolder.Mkdirs();

        return jFolder;
    }

    public RenderScript rs;

    protected AllocatedBitmap Output { get; set; }

    //protected DoubleBuffer Output { get; set; }


    protected void AllocateOutSurface(bool reset = false)
    {
#if DEBUG_RELEASE
		Trace.WriteLine($"[CAMERA] reallocating surface {mPreviewSize.Width}x{mPreviewSize.Height}");
#endif

        var kill = Output;

        var width = PreviewWidth;
        var height = PreviewHeight;
        if (SensorOrientation != 0 || SensorOrientation != 270)
        {
            width = PreviewHeight;
            height = PreviewWidth;
        }

        PreviewSize = new(width, height);


        //new
        //var ok = FormsControl.AllocatedFrameSurface(width, height);

        //var output = Allocation.CreateTyped(rs,
        //	new Android.Renderscripts.Type.Builder(rs,
        //			Android.Renderscripts.Element.RGBA_8888(rs))
        //		.SetX(width)
        //		.SetY(height).Create(),
        //	AllocationUsage.IoOutput | AllocationUsage.Script);

        //output.Surface = FormsControl.FrameSurface;


        //old

        Output = new(rs, width, height);

#if DEBUG_RELEASE
		Trace.WriteLine($"[CAMERA] ceated output");
#endif

        FormsControl.SetRotatedContentSize(
            PreviewSize,
            SensorOrientation);

        if (kill != null)
        {
            kill.Dispose();
        }

        //_stack.Clear();
    }

    //var output = Allocation.CreateTyped(rs,
    //	new Android.Renderscripts.Type.Builder(rs,
    //			Android.Renderscripts.Element.RGBA_8888(rs))
    //		.SetX(mRotatedPreviewSize.Width)
    //		.SetY(mRotatedPreviewSize.Height).Create(),
    //	AllocationUsage.IoOutput | AllocationUsage.Script);

    public SplinesHelper Splines { get; set; } = new();


    /// <summary>
    /// Process image using RenderScript
    /// </summary>
    /// <param name="image"></param>
    /// <param name="output"></param>
    public void ProcessImage(Image image, Allocation output)
    {
        var rotation = SensorOrientation;

        if (Effect == CameraEffect.ColorNegativeAuto)
        {
            if (Splines.Current != null)
                Rendering.BlitWithLUT(rs, Splines.Renderer, Splines.Current.RendererLUT, image, output, rotation,
                    Gamma);
            else
                Rendering.TestOutput(rs, output);
        }
        else
        {
            if (Effect == CameraEffect.ColorNegativeManual)
            {
                Rendering.BlitAdjust(rs, Splines.Renderer, image, output, rotation, Gamma, false, true);
            }
            else if (Effect == CameraEffect.GrayscaleNegative)
            {
                Rendering.BlitAdjust(rs, Splines.Renderer, image, output, rotation, Gamma, true, true);
            }
            else if (Effect == CameraEffect.Grayscale)
            {
                Rendering.BlitAdjust(rs, Splines.Renderer, image, output, rotation, Gamma, true, false);
            }
            else
            {
                //default, no effects
                Rendering.BlitAdjust(rs, Splines.Renderer, image, output, rotation, Gamma, false, false);
                //Rendering.TestOutput(rs, output);
            }
        }
    }

    //public SKImage GetPreviewImage(Allocation androidAllocation, int width, int height)
    //{
    //    // Create an SKImageInfo object to describe the allocation's properties
    //    var info = new SKImageInfo(width, height, SKColorType.Rgba8888);

    //    // Get the address of the ByteBuffer
    //    IntPtr ptr = androidAllocation.ByteBuffer.GetDirectBufferAddress();
    //    var data = SKData.Create(ptr, androidAllocation.BytesSize);

    //    //var buffer = androidAllocation.ByteBuffer;
    //    //buffer.Position(0);
    //    //buffer.Limit(androidAllocation.BytesSize);

    //    //byte[] bytes = new byte[androidAllocation.BytesSize];
    //    //buffer.Get(bytes);
    //    //var data = SKData.CreateCopy(bytes);

    //    // Wrap the existing pixel data from the Allocation
    //    SKImage skImage = SKImage.FromPixels(info, data);
    //    return skImage;
    //}

    object _lockPreview = new();


    public CapturedImage Preview
    {
        get => _preview;
        protected set
        {
            lock (_lockPreview)
            {
                var kill = _preview;
                _preview = value;
                kill?.Dispose();
            }
        }
    }

    /// <summary>
    /// WIll be correct from correct thread hopefully
    /// </summary>
    /// <returns></returns>
    public SKImage GetPreviewImage()
    {
        lock (_lockPreview)
        {
            SKImage preview = null;
            if (_preview != null && _preview.Image != null)
            {
                preview = _preview.Image;
                this._preview.Image = null; //protected from GC
                _preview = null; // Transfer ownership - renderer will dispose the SKImage
            }

            return preview;
        }
    }

    public void Start()
    {
        try
        {
            if (State == CameraProcessorState.Enabled)
            {
                Debug.WriteLine("[CAMERA] cannot start already running");
                return;
            }

            var width = (int)(FormsControl.Width * FormsControl.RenderingScale);
            var height = (int)(FormsControl.Height * FormsControl.RenderingScale);

            if (width <= 0 || height <= 0)
            {
                Debug.WriteLine("[CAMERA] cannot start for invalid preview size");
                State = CameraProcessorState.Error;
                return;
            }

            StartBackgroundThread();

            OpenCamera(width, height);

            MainThread.BeginInvokeOnMainThread(() => { DeviceDisplay.Current.KeepScreenOn = true; });
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            State = CameraProcessorState.Error;
        }
    }

    /// <summary>
    /// Call when inactive to free resources
    /// </summary>
    public void Stop(bool force = false)
    {
        try
        {
            CloseCamera(force);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            State = CameraProcessorState.Error;
        }

        MainThread.BeginInvokeOnMainThread(() => { DeviceDisplay.Current.KeepScreenOn = false; });
    }


    public NativeCamera(SkiaCamera parent)
    {
        FormsControl = parent;

        // Initialize flash modes from SkiaCamera properties to ensure consistency
        // This prevents flash settings from being reset when camera is recreated
        _flashMode = parent.FlashMode;
        _captureFlashMode = parent.CaptureFlashMode;

        rs = RenderScript.Create(Platform.AppContext);
        Splines.Initialize(rs);
    }

    //private readonly FramesQueue _stack = new();

    //BitmapPool _bitmapPool = new();


    SemaphoreSlim semaphireSlim = new SemaphoreSlim(1, 1);


    private object lockProcessingPreviewFrame = new();
    bool lockProcessing;

    //volatile bool lockAllocation;


    private List<Image> processing = new List<Image>();


    protected SkiaCamera FormsControl { get; set; }


    #region FRAGMENT

    public static readonly int REQUEST_CAMERA_PERMISSION = 1;
    private static readonly string FRAGMENT_DIALOG = "dialog";

    // Tag for the {@link Log}.
    private static readonly string TAG = "Camera2BasicFragment";

    // Camera state: Showing camera preview.
    public const int STATE_PREVIEW = 0;

    // Camera state: Waiting for the focus to be locked.
    public const int STATE_WAITING_LOCK = 1;

    // Camera state: Waiting for the exposure to be precapture state.
    public const int STATE_WAITING_PRECAPTURE = 2;

    // Camera state: Waiting for the exposure state to be something other than precapture.
    public const int STATE_WAITING_NON_PRECAPTURE = 3;

    // Camera state: Picture was taken.
    public const int STATE_PICTURE_TAKEN = 4;

    // ID of the current {@link CameraDevice}.
    private string CameraId;

    // A {@link CameraCaptureSession } for camera preview.
    public CameraCaptureSession CaptureSession;

    // A reference to the opened CameraDevice
    public CameraDevice mCameraDevice;


    /// <summary>
    /// The size of the camera preview in pixels
    /// </summary>
    public SKSize PreviewSize { get; set; }

    // CameraDevice.StateListener is called when a CameraDevice changes its state
    private CameraStateListener mStateCallback;

    // An additional thread for running tasks that shouldn't block the UI.
    private HandlerThread mBackgroundThread;

    // A {@link Handler} for running tasks in the background.
    public Handler mBackgroundHandler;

    // An {@link ImageReader} that handles still image capture.
    public ImageReader mImageReaderPreview;

    private ImageReader mImageReaderPhoto;

    //{@link CaptureRequest.Builder} for the camera preview
    public CaptureRequest.Builder mPreviewRequestBuilder;

    // {@link CaptureRequest} generated by {@link #mPreviewRequestBuilder}
    public CaptureRequest mPreviewRequest;

    // The current state of camera state for taking pictures.
    public int mState = STATE_PREVIEW;

    // A {@link Semaphore} to prevent the app from exiting before closing the camera.
    public Semaphore mCameraOpenCloseLock = new Semaphore(1);

    // Whether the current camera device supports Flash or not.
    private bool mFlashSupported;

    // Video recording fields
    private bool _isRecordingVideo;
    private MediaRecorder _mediaRecorder;
    private string _currentVideoFile;
    private DateTime _recordingStartTime;
    private System.Threading.Timer _progressTimer;

    /// <summary>
    /// Camera sensor orientation in degrees
    /// </summary>
    public int SensorOrientation { get; set; }

    // A {@link CameraCaptureSession.CaptureCallback} that handles events related to JPEG capture.
    public StillPhotoCaptureCallback mCaptureCallback;

    // Shows a {@link Toast} on the UI thread.
    public void ShowToast(string text)
    {
        Trace.WriteLine(text);
        //if (Activity != null)
        //{
        //	Activity.RunOnUiThread(new ShowToastRunnable(Activity.ApplicationContext, text));
        //}
    }

    /// <summary>
    /// Given choices of sizes supported by a camera, choose the one with closest aspect ratio match
    /// that fits within the specified maximum dimensions. Optionally applies ratio tolerance.
    /// </summary>
    /// <param name="choices">The list of sizes that the camera supports for the intended output class</param>
    /// <param name="maxWidth">The maximum width that can be chosen</param>
    /// <param name="maxHeight">The maximum height that can be chosen</param>
    /// <param name="aspectRatio">The desired aspect ratio</param>
    /// <param name="ratioTolerance">Tolerance for aspect ratio matching (default 0.1 = 10%)</param>
    /// <returns>The optimal Size, or first choice if none were suitable</returns>
    private static Size ChooseOptimalSize(Size[] choices, int maxWidth, int maxHeight, Size aspectRatio,
        double ratioTolerance = 0.1)
    {
        double targetRatio = (double)aspectRatio.Width / aspectRatio.Height;
        Size optimalSize = null;
        double minDiffRatio = double.MaxValue;

        // First pass: find best aspect match within max dimensions
        foreach (Size size in choices)
        {
            int width = size.Width;
            int height = size.Height;

            if (width > maxWidth || height > maxHeight) continue;

            double ratio = (double)width / height;
            double diffRatio = Math.Abs(targetRatio - ratio);
            double normalizedDiff = diffRatio / targetRatio;

            if (normalizedDiff <= ratioTolerance && diffRatio < minDiffRatio)
            {
                optimalSize = size;
                minDiffRatio = diffRatio;
            }
        }

        // Second pass: if no good aspect match, find any size with closest aspect within max dimensions
        if (optimalSize == null)
        {
            foreach (Size size in choices)
            {
                if (size.Width <= maxWidth && size.Height <= maxHeight)
                {
                    double ratio = (double)size.Width / size.Height;
                    double diffRatio = Math.Abs(targetRatio - ratio);

                    if (diffRatio < minDiffRatio)
                    {
                        optimalSize = size;
                        minDiffRatio = diffRatio;
                    }
                }
            }
        }

        if (optimalSize == null)
        {
            Debug.WriteLine($"[ChooseOptimalSize] Couldn't find any suitable preview size within {maxWidth}x{maxHeight}");
            return choices[0];
        }

        Debug.WriteLine($"[ChooseOptimalSize] Selected {optimalSize.Width}x{optimalSize.Height}");
        return optimalSize;
    }


    public bool ManualZoomEnabled = true;

    private void OnScaleChanged(object sender, TouchEffect.WheelEventArgs e)
    {
        if (ManualZoomEnabled)
        {
            SetZoom(e.Scale);
        }
    }

    /// <summary>
    /// Select format by quality percentile
    /// </summary>
    /// <param name="formatDetails"></param>
    /// <param name="percentile"></param>
    /// <returns></returns>
    private Size SelectFormatByQuality(List<Size> formatDetails, double percentile)
    {
        var index = (int)(formatDetails.Count * percentile);
        var result = formatDetails.Skip(index).FirstOrDefault();
        return result != null ? result : formatDetails.First();
    }

    /// <summary>
    /// Pass preview size as params
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    protected virtual void SetupHardware(int width, int height)
    {
        lock (lockSetup)
        {
            int allowPreviewOverflow = 200; //by px

            var activity = Platform.CurrentActivity;
            var manager = (CameraManager)activity.GetSystemService(Context.CameraService);
            try
            {
                //get avalable cameras info
                var cameras = new List<CameraUnit>();

                var cams = manager.GetCameraIdList();
                for (var i = 0; i < cams.Length; i++)
                {
                    var cameraId = cams[i];
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraId);

                    #region compatible camera

                    // Skip wrong facing cameras (only if not in manual mode)
                    var facing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing);
                    if (FormsControl.Facing != CameraPosition.Manual && facing != null)
                    {
                        if (FormsControl.Facing == CameraPosition.Default &&
                            facing == (Integer.ValueOf((int)LensFacing.Front)))
                            continue;
                        else if (FormsControl.Facing == CameraPosition.Selfie &&
                                 facing == (Integer.ValueOf((int)LensFacing.Back)))
                            continue;
                    }

                    var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics
                        .ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        continue;
                    }

                    #endregion

                    var focalList = (float[])characteristics.Get(CameraCharacteristics.LensInfoAvailableFocalLengths);
                    var sensorSize =
                        (Android.Util.SizeF)characteristics.Get(CameraCharacteristics.SensorInfoPhysicalSize);

                    var unit = new CameraUnit
                    {
                        Id = cameraId,
                        Facing = FormsControl.Facing,
                        MinFocalDistance =
                            (float)characteristics.Get(CameraCharacteristics.LensInfoMinimumFocusDistance),
                        //LensDistortion = (???)characteristics.Get(CameraCharacteristics.LensDistortion),
                        SensorHeight = sensorSize.Height,
                        SensorWidth = sensorSize.Width,
                        FocalLengths = new List<float>(),
                        Meta = FormsControl.CreateMetadata()
                    };

                    foreach (var focalLength in focalList)
                    {
                        unit.FocalLengths.Add(focalLength);
                    }

                    unit.FocalLength = unit.FocalLengths[0];

                    cameras.Add(unit);
                }

                if (!cameras.Any())
                    return;

                // Select camera based on manual index or default to first
                CameraUnit selectedCamera;
                if (FormsControl.Facing == CameraPosition.Manual && FormsControl.CameraIndex >= 0)
                {
                    if (FormsControl.CameraIndex < cameras.Count)
                    {
                        selectedCamera = cameras[FormsControl.CameraIndex];
                        Debug.WriteLine(
                            $"[NativeCameraAndroid] Selected camera by index {FormsControl.CameraIndex}: {selectedCamera.Id}");
                    }
                    else
                    {
                        Debug.WriteLine(
                            $"[NativeCameraAndroid] Invalid camera index {FormsControl.CameraIndex}, falling back to first camera");
                        selectedCamera = cameras[0];
                    }
                }
                else
                {
                    selectedCamera = cameras[0];
                }


                bool SetupCamera(CameraUnit cameraUnit)
                {
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraUnit.Id);

                    var map = (StreamConfigurationMap)characteristics.Get(
                        CameraCharacteristics.ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        return false;
                    }

                    // Check if the flash is supported.
                    var available = (Boolean)characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
                    if (available == null)
                    {
                        mFlashSupported = false;
                    }
                    else
                    {
                        mFlashSupported = (bool)available;
                    }

                    SensorOrientation = (int)(Integer)characteristics.Get(CameraCharacteristics.SensorOrientation);


                    Point displaySize = new(width, height);
                    //activity.WindowManager.DefaultDisplay.GetSize(displaySize);

                    //camera width

                    // Camera preview sizes are always in sensor orientation (typically landscape)
                    bool rotated = (SensorOrientation != 0 && SensorOrientation != 180);

                    int maxPreviewWidth, maxPreviewHeight;

                    if (rotated)
                    {
                        // Landscape sensor: allow wider preview sizes to match camera native format
                        // Use 1.78 ratio (16:9) with max height of 1024 -> width can be up to 1820
                        maxPreviewWidth = MaxPreviewHeight * 2; // Allow up to 2:1 aspect ratio
                        maxPreviewHeight = MaxPreviewWidth;     // Height limited to 1024
                    }
                    else
                    {
                        // Portrait sensor: use standard limits
                        maxPreviewWidth = MaxPreviewWidth;
                        maxPreviewHeight = MaxPreviewHeight;
                    }

                    #region STILL PHOTO

                    // Setup still formats once - same pattern as Apple implementation
                    SetupStillFormats(map, CameraId);

                    // For Manual quality, use the index directly from StillFormats
                    // For other qualities, filter by orientation as before
                    List<Size> validSizes;
                    if (FormsControl.PhotoQuality == CaptureQuality.Manual)
                    {
                        // Use StillFormats directly for Manual mode - same as Apple implementation
                        validSizes = StillFormats.Select(f => new Size(f.Width, f.Height)).ToList();
                        Debug.WriteLine($"[NativeCameraAndroid] Using StillFormats for Manual quality: {validSizes.Count} formats");
                    }
                    else
                    {
                        // For other qualities, filter by orientation as before
                        var allStillSizes = StillFormats.Select(f => new Size(f.Width, f.Height)).ToList();

                        if (rotated)
                        {
                            validSizes = allStillSizes.Where(x => x.Width > x.Height)
                                .OrderByDescending(x => x.Width * x.Height)
                                .ToList();
                        }
                        else
                        {
                            validSizes = allStillSizes.Where(x => x.Width < x.Height)
                                .OrderByDescending(x => x.Width * x.Height)
                                .ToList();
                        }

                        if (!validSizes.Any())
                        {
                            validSizes = allStillSizes.Where(x => x.Width == x.Height)
                                .OrderByDescending(x => x.Width * x.Height)
                                .ToList();
                        }

                        Debug.WriteLine($"[NativeCameraAndroid] Using orientation-filtered format list for {FormsControl.PhotoQuality} quality: {validSizes.Count} formats (rotated: {rotated})");
                    }

                    Size selectedSize;

                    switch (FormsControl.PhotoQuality)
                    {
                        case CaptureQuality.Max:
                            selectedSize = validSizes.First();
                            break;

                        case CaptureQuality.High:
                            selectedSize = SelectFormatByQuality(validSizes, 0.2);
                            break;

                        case CaptureQuality.Medium:
                            selectedSize = SelectFormatByQuality(validSizes, 0.5);
                            break;

                        case CaptureQuality.Low:
                            selectedSize = SelectFormatByQuality(validSizes, 0.8);
                            break;

                        case CaptureQuality.Manual:
                            // Use specific format index from the complete format list (validSizes = allStillSizes for Manual)
                            var formatIndex = FormsControl.PhotoFormatIndex;
                            if (formatIndex >= 0 && formatIndex < validSizes.Count)
                            {
                                selectedSize = validSizes[formatIndex];
                                Debug.WriteLine($"[NativeCameraAndroid] Manual format selection: index {formatIndex} = {selectedSize.Width}x{selectedSize.Height}");
                            }
                            else
                            {
                                Debug.WriteLine(
                                    $"[NativeCameraAndroid] Invalid PhotoFormatIndex {formatIndex} (max: {validSizes.Count - 1}), using Max quality");
                                selectedSize = validSizes.First();
                            }

                            break;

                        default:
                            selectedSize = new(1, 1);
                            break;
                    }

                    CaptureWidth = selectedSize.Width;
                    CaptureHeight = selectedSize.Height;

                    if (selectedSize.Width > 1 && selectedSize.Height > 1)
                    {
                        mImageReaderPhoto =
                            ImageReader.NewInstance(CaptureWidth, CaptureHeight, ImageFormatType.Yuv420888, 2);
                        mImageReaderPhoto.SetOnImageAvailableListener(mCaptureCallback, mBackgroundHandler);

                        FormsControl.CapturePhotoSize = new(CaptureWidth, CaptureHeight);
                    }
                    else
                    {
                        mImageReaderPhoto = null;
                    }

                    #endregion


                    // Decide aspect target based on CaptureMode: use video format aspect in Video mode
                    Size aspectTarget = selectedSize;
                    if (FormsControl.CaptureMode == CaptureModeType.Video)
                    {
                        try
                        {
                            var videoFrame = GetCurrentVideoFormat();
                            aspectTarget = new Size(videoFrame.Width, videoFrame.Height);

                            Debug.WriteLine($"[VIDEO] Using {aspectTarget}");
                        }
                        catch { }
                    }

                    var previewSize = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                        maxPreviewWidth, maxPreviewHeight, aspectTarget);

                    PreviewWidth = previewSize.Width;
                    PreviewHeight = previewSize.Height;

                    System.Diagnostics.Debug.WriteLine($"[PREVIEW] Selected {PreviewWidth}x{PreviewHeight} rotated={rotated} max={maxPreviewWidth}x{maxPreviewHeight} aspectTarget={aspectTarget.Width}x{aspectTarget.Height}");

                    mImageReaderPreview =
                        ImageReader.NewInstance(PreviewWidth, PreviewHeight, ImageFormatType.Yuv420888, 3);
                    mImageReaderPreview.SetOnImageAvailableListener(this, mBackgroundHandler);

                    AllocateOutSurface();

                    CameraId = cameraUnit.Id;

                    FormsControl.CameraDevice = cameraUnit;

                    FocalLength = (float)(cameraUnit.FocalLength * cameraUnit.SensorCropFactor);

                    //                    ConsoleColor.Green.WriteLineForDebug(ViewFinderData.PrettyJson(PresetViewport));

                    //System.Diagnostics.Debug.WriteLine($"[CameraFragment] Cameras:\n {ViewFinderData.PrettyJson(BackCameras)}");

                    return true;
                }

                if (SetupCamera(selectedCamera))
                    return;

                System.Diagnostics.Debug.WriteLine($"[CameraFragment] No outputs!");
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (NullPointerException e)
            {
                //ErrorDialog.NewInstance(GetString(Resource.String.camera_error)).Show(ChildFragmentManager, FRAGMENT_DIALOG);
            }
        }
    }

    object lockSetup = new();

    //private CameraUnit _camera;
    //public CameraUnit Camera
    //{
    //	get { return _camera; }
    //	set
    //	{
    //		if (_camera != value)
    //		{
    //			_camera = value;
    //			OnPropertyChanged();
    //		}
    //	}
    //}

    private int _PreviewWidth;

    public int PreviewWidth
    {
        get { return _PreviewWidth; }
        set
        {
            if (_PreviewWidth != value)
            {
                _PreviewWidth = value;
                OnPropertyChanged("PreviewWidth");
            }
        }
    }

    private int _PreviewHeight;

    public int PreviewHeight
    {
        get { return _PreviewHeight; }
        set
        {
            if (_PreviewHeight != value)
            {
                _PreviewHeight = value;
                OnPropertyChanged("PreviewHeight");
            }
        }
    }

    private int _CaptureWidth;

    public int CaptureWidth
    {
        get { return _CaptureWidth; }
        set
        {
            if (_CaptureWidth != value)
            {
                _CaptureWidth = value;
                OnPropertyChanged("CaptureWidth");
            }
        }
    }

    private int _CaptureHeight;

    public int CaptureHeight
    {
        get { return _CaptureHeight; }
        set
        {
            if (_CaptureHeight != value)
            {
                _CaptureHeight = value;
                OnPropertyChanged("CaptureHeight");
            }
        }
    }


    private CameraProcessorState _state;

    public CameraProcessorState State
    {
        get { return _state; }
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
                if (FormsControl != null)
                {
                    switch (value)
                    {
                        case CameraProcessorState.Enabled:
                            FormsControl.State = CameraState.On;
                            break;
                        case CameraProcessorState.Error:
                            FormsControl.State = CameraState.Error;
                            break;
                        default:
                            FormsControl.State = CameraState.Off;
                            break;
                    }
                }
            }
        }
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;
    //        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        var changed = PropertyChanged;
        if (changed == null)
            return;

        changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    /// <summary>
    /// Calls SetupHardware(width, height); inside
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    /// <exception cref="RuntimeException"></exception>
    public bool OpenCamera(int width, int height)
    {
        if (width > 0 && height > 0)
        {
            if (State == CameraProcessorState.Enabled)
                return true; //avoid spam

            var activity = Platform.AppContext;

            try
            {
                if (!mCameraOpenCloseLock.TryAcquire(2500, TimeUnit.Milliseconds))
                {
                    throw new RuntimeException("Time out waiting to lock camera opening.");
                }

                if (mCaptureCallback == null)
                    mCaptureCallback = new StillPhotoCaptureCallback(this);

                SetupHardware(width, height);

                if (mStateCallback == null)
                    mStateCallback = new CameraStateListener(this);

                var manager = (CameraManager)activity.GetSystemService(Context.CameraService);
                manager.OpenCamera(CameraId, mStateCallback, mBackgroundHandler);

                State = CameraProcessorState.Enabled;
                Debug.WriteLine($"[CAMERA] {CameraId} Started");

                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                return false;
            }
            finally
            {
                mCameraOpenCloseLock.Release();
            }
        }

        return false;
    }

    public event EventHandler OnImageTaken;
    public event EventHandler<Exception> OnImageTakingFailed;

    public event EventHandler OnUpdateFPS;

    public event EventHandler OnUpdateOrientation;


    // Closes the current {@link CameraDevice}.
    public void CloseCamera(bool force = false)
    {
        if (State == CameraProcessorState.None && !force)
            return;

        if (State != CameraProcessorState.Enabled && !force)
            return; //avoid spam

        try
        {
            mCameraOpenCloseLock.Acquire();

            if (null != CaptureSession)
            {
                CaptureSession.Close();
                CaptureSession = null;
            }

            if (null != mCameraDevice)
            {
                mCameraDevice.Close();
                mCameraDevice = null;
            }

            mStateCallback = null;
            mCaptureCallback = null;

            if (null != mImageReaderPreview)
            {
                mImageReaderPreview.Close();
                mImageReaderPreview = null;
            }

            if (null != mImageReaderPhoto)
            {
                mImageReaderPhoto.Close();
                mImageReaderPhoto = null;
            }


            State = CameraProcessorState.None;

            Debug.WriteLine($"[CAMERA] {CameraId} Stopped");

            StopBackgroundThread();
        }
        catch (Exception e)
        {
            //throw new RuntimeException("Interrupted while trying to lock camera closing.", e);
            Trace.WriteLine(e);
        }
        finally
        {
            mCameraOpenCloseLock.Release();
            GC.Collect();
        }
    }

    // Starts a background thread and its {@link Handler}.
    private void StartBackgroundThread()
    {
        mBackgroundThread = new HandlerThread("CameraBackground");
        mBackgroundThread.Start();
        mBackgroundHandler = new Handler(mBackgroundThread.Looper);
    }

    // Stops the background thread and its {@link Handler}.
    private void StopBackgroundThread()
    {
        try
        {
            mBackgroundThread?.QuitSafely();
            mBackgroundThread?.Join();
            mBackgroundThread = null;
            mBackgroundHandler = null;
        }
        catch (Exception e)
        {
            //e.PrintStackTrace();
            mBackgroundThread = null;
            mBackgroundHandler = null;
        }
    }

    bool _isTorchOn;
    FlashMode _flashMode = FlashMode.Off;
    CaptureFlashMode _captureFlashMode = CaptureFlashMode.Auto;

    public void SetFlashMode(FlashMode mode)
    {
        _flashMode = mode;
        ApplyFlashMode();
    }

    public FlashMode GetFlashMode()
    {
        return _flashMode;
    }

    private void ApplyFlashMode()
    {
        if (mCameraDevice == null || CaptureSession == null || !mFlashSupported)
            return;

        try
        {
            switch (_flashMode)
            {
                case FlashMode.Off:
                    mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Off);
                    _isTorchOn = false;
                    break;
                case FlashMode.On:
                    mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Torch);
                    _isTorchOn = true;
                    break;
                case FlashMode.Strobe:
                    mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Torch);
                    _isTorchOn = true;
                    break;
            }

            // Apply the updated request to the session
            mPreviewRequest = mPreviewRequestBuilder.Build();
            CaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback, mBackgroundHandler);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
    }

    public void SetCaptureFlashMode(CaptureFlashMode mode)
    {
        _captureFlashMode = mode;
        // Reset AE state to clear any cached flash settings
        ResetAutoExposureState();
    }

    private void ResetAutoExposureState()
    {
        if (mCameraDevice == null || CaptureSession == null)
            return;

        try
        {
            // Reset AE state by triggering AE precapture
            var resetRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Preview);

            // Copy current preview settings
            switch (_flashMode)
            {
                case FlashMode.Off:
                    resetRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    resetRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Off);
                    break;
                case FlashMode.On:
                    resetRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    resetRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Torch);
                    break;
                case FlashMode.Strobe:
                    resetRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    resetRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Torch);
                    break;
            }

            // Reset AE state
            resetRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, (int)ControlAEPrecaptureTrigger.Cancel);

            var resetRequest = resetRequestBuilder.Build();
            CaptureSession.Capture(resetRequest, mCaptureCallback, mBackgroundHandler);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[CAMERA] ResetAutoExposureState error: {e}");
        }
    }

    public CaptureFlashMode GetCaptureFlashMode()
    {
        return _captureFlashMode;
    }

    public bool IsFlashSupported()
    {
        return mFlashSupported;
    }

    public bool IsAutoFlashSupported()
    {
        return mFlashSupported; // Android supports auto flash when flash is available
    }

    public void SetCapturingStillOptions(CaptureRequest.Builder requestBuilder)
    {
        requestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);

        if (mFlashSupported)
        {
            switch (_captureFlashMode)
            {
                case CaptureFlashMode.Off:
                    requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    requestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Off);
                    break;
                case CaptureFlashMode.Auto:
                    requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
                    requestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Off);
                    break;
                case CaptureFlashMode.On:
                    requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                    requestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Single);
                    break;
            }
        }
    }

    /// <summary>
    /// Sets preview-specific options without interfering with flash settings already applied for preview
    /// </summary>
    public void SetPreviewOptions(CaptureRequest.Builder requestBuilder)
    {
        requestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
        // Note: Flash settings are NOT applied here as they're already set in CreateCameraPreviewSession()
        // This prevents conflicts between preview flash (torch) and capture flash (single) modes
    }

    public void StartCapturingStill()
    {
        if (CapturingStill)
            return;

        try
        {
            CapturingStill = true;

            PlaySound();

            var activity = Platform.AppContext;
            if (null == activity || null == mCameraDevice)
            {
                OnImageTakingFailed?.Invoke(this, null);
                CapturingStill = false;
                return;
            }

            var stillCaptureBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);
            stillCaptureBuilder.AddTarget(mImageReaderPhoto.Surface);

            // Use the same AE and AF modes as the preview.
            SetCapturingStillOptions(stillCaptureBuilder);

            // Orientation
            int rotation = 0; //int)activity.WindowManager.DefaultDisplay.Rotation;
            stillCaptureBuilder.Set(CaptureRequest.JpegOrientation, SensorOrientation);

            CaptureSession.StopRepeating();

            CaptureSession
                .Capture(stillCaptureBuilder.Build(), new StillPhotoCaptureFinishedCallback(this),
                    mBackgroundHandler);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            OnCaptureError(e);
        }
        finally
        {
        }
    }

    public void CreateCameraPreviewSession()
    {
        try
        {
            mPreviewRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
            // Ensure consistent FOV: disable video stabilization for preview
            try { mPreviewRequestBuilder.Set(CaptureRequest.ControlVideoStabilizationMode, (int)ControlVideoStabilizationMode.Off); } catch { }

            // Apply current flash mode to preview request builder
            if (mFlashSupported)
            {
                switch (_flashMode)
                {
                    case FlashMode.Off:
                        mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                        mPreviewRequestBuilder.Set(CaptureRequest.FlashMode,
                            (int)Android.Hardware.Camera2.FlashMode.Off);
                        _isTorchOn = false;
                        break;
                    case FlashMode.On:
                        mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                        mPreviewRequestBuilder.Set(CaptureRequest.FlashMode,
                            (int)Android.Hardware.Camera2.FlashMode.Torch);
                        _isTorchOn = true;
                        break;
                    case FlashMode.Strobe:
                        // Future implementation for strobe mode
                        // For now, treat as On
                        mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                        mPreviewRequestBuilder.Set(CaptureRequest.FlashMode,
                            (int)Android.Hardware.Camera2.FlashMode.Torch);
                        _isTorchOn = true;
                        break;
                }
            }

            var surfaces = new List<Surface> { mImageReaderPreview.Surface };
            if (mImageReaderPhoto != null)
                surfaces.Add(mImageReaderPhoto.Surface);

            mCameraDevice.CreateCaptureSession(
                surfaces,
                new CameraCaptureSessionCallback(this),
                mBackgroundHandler);
        }
        catch (CameraAccessException e)
        {
            Trace.WriteLine(e);
            Trace.WriteLine($"[CAMERA] {CameraId} Failed to start camera session");

            State = CameraProcessorState.Error;
        }
    }





    public void TakePicture()
    {
        CaptureStillImage();
    }

    private void CaptureStillImage()
    {
        try
        {
            mPreviewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);
            mState = STATE_WAITING_LOCK;
            CaptureSession.Capture(mPreviewRequestBuilder.Build(), mCaptureCallback,
                mBackgroundHandler);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
    }

    public void RunPrecaptureSequence()
    {
        try
        {
            mPreviewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger,
                (int)ControlAEPrecaptureTrigger.Start);
            mState = STATE_WAITING_PRECAPTURE;
            CaptureSession.Capture(mPreviewRequestBuilder.Build(), mCaptureCallback, null);
        }
        catch (CameraAccessException e)
        {
            e.PrintStackTrace();
        }
    }

    //private CaptureRequest.Builder stillCaptureBuilder;

    public MediaPlayer MediaPlayer;

    public void PlaySound()
    {
        if (Silent)
            return;

        try
        {
            if (MediaPlayer != null && MediaPlayer.IsPlaying)
            {
                MediaPlayer.Stop();
            }

            if (MediaPlayer != null)
            {
                MediaPlayer.Release();
                MediaPlayer = null;
            }

            if (MediaPlayer == null)
            {
                MediaPlayer = new MediaPlayer();
            }

            AssetFileDescriptor descriptor = Platform.AppContext.Assets.OpenFd("canond30.mp3");
            MediaPlayer.SetDataSource(descriptor.FileDescriptor, descriptor.StartOffset, descriptor.Length);
            descriptor.Close();
            MediaPlayer.Prepare();
            MediaPlayer.SetVolume(1f, 1f);
            MediaPlayer.Looping = false;
            MediaPlayer.Start();
        }
        catch (Exception e)
        {
            //e.printStackTrace();
        }
    }

    void OnCaptureError(Exception e)
    {
        StillImageCaptureFailed(e);

        CapturingStill = false;
        StopCapturingStillImage();
    }

    void OnCaptureSuccess(CapturedImage result)
    {
        StillImageCaptureSuccess?.Invoke(result);
        CapturingStill = false;
        StopCapturingStillImage();
    }

    void OnPreviewCaptureSuccess(CapturedImage result)
    {
        PreviewCaptureSuccess?.Invoke(result);
    }


    public void StopCapturingStillImage()
    {
        try
        {
            mPreviewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
            SetPreviewOptions(mPreviewRequestBuilder);
            CaptureSession.Capture(mPreviewRequestBuilder.Build(), mCaptureCallback,
                mBackgroundHandler);
            mState = STATE_PREVIEW;
            CaptureSession.SetRepeatingRequest(
                mPreviewRequest,
                mCaptureCallback,
                mBackgroundHandler);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
    }


    /*
    public RenderScript rs;

    protected Allocation Output { get; set; }


    protected void AllocateOutSurface(bool reset = false)
    {
        if (Output != null && !reset)
            return;

        Debug.WriteLine($"[CAMERA] reallocating surface {mRotatedPreviewSize.Width}x{mRotatedPreviewSize.Height}");

        var oldOutput = Output;

        var output = Allocation.CreateTyped(rs,
                     new Android.Renderscripts.Type.Builder(rs,
                             Android.Renderscripts.Element.RGBA_8888(rs))
                         .SetX(mRotatedPreviewSize.Width)
                         .SetY(mRotatedPreviewSize.Height).Create(),
                     AllocationUsage.IoOutput | AllocationUsage.Script);

        output.Surface = new Surface(mTextureView.SurfaceTexture);

        Output = output;

        if (oldOutput != null)
        {
            oldOutput.Destroy();
            oldOutput.Dispose();
        }

    }

    */

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Stop video recording if active
            if (_isRecordingVideo)
            {
                try
                {
                    _mediaRecorder?.Stop();
                }
                catch { }
                _isRecordingVideo = false;
            }

            _progressTimer?.Dispose();
            CleanupMediaRecorder();

            Stop(true);

            //mTextureView.Dispose();
        }

        base.Dispose(disposing);
    }

    protected int countFrames = 0;


    public bool CapturingStill
    {
        get { return _capturingStill; }

        set
        {
            if (_capturingStill != value)
            {
                _capturingStill = value;
                OnPropertyChanged();
            }
        }
    }

    bool _capturingStill;


    public bool Silent { get; set; }


    public string CaptureCustomFolder { get; set; }

    //public CaptureLocationType CaptureLocation { get; set; }

    public void InsertImageIntoMediaStore(Context context, string imagePath, string imageName)
    {
        ContentValues values = new ContentValues();
        values.Put(MediaStore.Images.Media.InterfaceConsts.Title, imageName);
        values.Put(MediaStore.Images.Media.InterfaceConsts.DisplayName, imageName);
        values.Put(MediaStore.Images.Media.InterfaceConsts.MimeType, "image/jpeg");
        values.Put(MediaStore.Images.Media.InterfaceConsts.Data, imagePath);

        context.ContentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);
    }

    public Android.Net.Uri GetMediaStore(Context context, string imagePath, string imageName)
    {
        ContentValues values = new ContentValues();
        values.Put(MediaStore.Images.Media.InterfaceConsts.Title, imageName);
        values.Put(MediaStore.Images.Media.InterfaceConsts.DisplayName, imageName);
        values.Put(MediaStore.Images.Media.InterfaceConsts.MimeType, "image/jpeg");
        values.Put(MediaStore.Images.Media.InterfaceConsts.RelativePath, "ArtOfFoto");
        //values.Put(MediaStore.Images.Media.InterfaceConsts.Data, imagePath);
        return context.ContentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);
    }


    private double _SavedRotation;

    public double SavedRotation
    {
        get { return _SavedRotation; }
        set
        {
            if (_SavedRotation != value)
            {
                _SavedRotation = value;
                OnPropertyChanged("SavedRotation");
            }
        }
    }


    public string SavedFilename
    {
        get { return _SavedFilename; }
        set
        {
            if (_SavedFilename != value)
            {
                _SavedFilename = value;
                OnPropertyChanged("SavedFilename");
            }
        }
    }

    private string _SavedFilename;

    public Action<CapturedImage> PreviewCaptureSuccess { get; set; }

    public Action<CapturedImage> StillImageCaptureSuccess { get; set; }

    public Action<Exception> StillImageCaptureFailed { get; set; }


    public void ApplyDeviceOrientation(int orientation)
    {
        Debug.WriteLine($"[SkiaCamera] New orientation {orientation}");
    }


    /// <summary>
    /// Ex-SaveImageFromYUV
    /// </summary>
    /// <param name="image"></param>
    public void OnCapturedStillImage(Image image)
    {
        try
        {
            var width = image.Width;
            var height = image.Height;

            if (SensorOrientation == 90 || SensorOrientation == 270)
            {
                height = image.Width;
                width = image.Height;
            }

            using var allocated = new AllocatedBitmap(rs, width, height);

            ProcessImage(image, allocated.Allocation);

            allocated.Update();

            switch (FormsControl.DeviceRotation)
            {
                case 90:
                    FormsControl.CameraDevice.Meta.Orientation = 8;
                    break;
                case 270:
                    FormsControl.CameraDevice.Meta.Orientation = 6;
                    break;
                case 180:
                    FormsControl.CameraDevice.Meta.Orientation = 3;
                    break;
                default:
                    FormsControl.CameraDevice.Meta.Orientation = 1;
                    break;
            }

            var meta = Reflection.Clone(FormsControl.CameraDevice.Meta);
            var rotation = FormsControl.DeviceRotation;
            Metadata.ApplyRotation(meta, rotation);

            var outImage = new CapturedImage()
            {
                Facing = FormsControl.Facing,
                Time = DateTime.UtcNow,
                Image = allocated.Bitmap.ToSKImage(),
                Meta = meta,
                Rotation = rotation
            };

            OnCaptureSuccess(outImage);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            OnCaptureError(e);
        }
    }

    public static string ConvertCoords(double coord)
    {
        coord = Math.Abs(coord);
        int degree = (int)coord;
        coord *= 60;
        coord -= (degree * 60.0d);
        int minute = (int)coord;
        coord *= 60;
        coord -= (minute * 60.0d);
        int second = (int)(coord * 1000.0d);

        StringBuilder sb = new StringBuilder();
        sb.Append(degree);
        sb.Append("/1,");
        sb.Append(minute);
        sb.Append("/1,");
        sb.Append(second);
        sb.Append("/1000");
        return sb.ToString();
    }


    private static int filenamesCounter = 0;


    public float Gamma { get; set; } = 1.0f;

    //private StretchModes _displayMode;
    //public StretchModes DisplayMode
    //{
    //	get
    //	{
    //		return _displayMode;
    //	}
    //	set
    //	{
    //		_displayMode = value;
    //		//todo update!
    //		mTextureView?.SetDisplayMode(value);
    //	}
    //}

    public float _manualZoom = 1.0f;
    public float _manualZoomCamera = 1.0f;
    public float _minZoom = 0.1f;

    private float _ZoomScale = 1.0f;

    public float ZoomScale
    {
        get { return _ZoomScale; }
        set
        {
            _ZoomScale = value;
            //mTextureView?.SetZoomScale(value);

            ZoomScaleTexture = value;

            OnPropertyChanged();
        }
    }


    private float _ZoomScaleTexture = 1.0f;

    public float ZoomScaleTexture
    {
        get { return _ZoomScaleTexture; }
        set
        {
            _ZoomScaleTexture = value;
            OnPropertyChanged();
        }
    }

    private float _ViewportScale = 1.0f;

    public float ViewportScale
    {
        get { return _ViewportScale; }
        set
        {
            _ViewportScale = value;
            OnPropertyChanged();
        }
    }

    private float _focalLength = 0.0f;

    public float FocalLength
    {
        get { return _focalLength; }
        set
        {
            _focalLength = value;
            OnPropertyChanged();
        }
    }


    private CameraEffect _effect;
    private CapturedImage _preview;

    private readonly List<CamcorderQuality> _videoQualities = new();

    private static List<Size> _stillSizes;

    protected ImageReader FramesReader { get; set; }


    public CameraEffect Effect
    {
        get { return _effect; }
        set { _effect = value; }
    }

    #endregion

    #region VIDEO RECORDING

    /// <summary>
    /// Gets the currently selected video format
    /// </summary>
    /// <returns>Current video format or null if not available</returns>
    public VideoFormat GetCurrentVideoFormat()
    {
        try
        {
            // If Manual, return the exact selected entry from our supported list
            if (FormsControl.VideoQuality == VideoQuality.Manual)
            {
                var list = GetPredefinedVideoFormats();
                var idx = FormsControl.VideoFormatIndex;
                if (idx >= 0 && idx < list.Count)
                    return list[idx];
            }

            // Otherwise, reflect the resolved CamcorderProfile
            var profile = GetVideoProfile();
            return new VideoFormat
            {
                Width = profile.VideoFrameWidth,
                Height = profile.VideoFrameHeight,
                FrameRate = profile.VideoFrameRate,
                Codec = "H.264",
                BitRate = profile.VideoBitRate,
                FormatId = $"android_{profile.VideoFrameWidth}x{profile.VideoFrameHeight}_{profile.VideoFrameRate}fps"
            };
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Error getting current video format: {ex.Message}");
            return new VideoFormat { Width = 1920, Height = 1080, FrameRate = 30, Codec = "H.264", BitRate = 8000000, FormatId = "1080p30" };
        }
    }

    /// <summary>
    /// Setup MediaRecorder for video recording
    /// </summary>
    private async Task SetupMediaRecorder()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
            throw new InvalidOperationException("Current activity is null");

        _mediaRecorder?.Release();
        _mediaRecorder = new MediaRecorder();

        // Create output file
        var fileName = $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        var moviesDir = activity.GetExternalFilesDir(Android.OS.Environment.DirectoryMovies);
        _currentVideoFile = System.IO.Path.Combine(moviesDir.AbsolutePath, fileName);

        // Configure MediaRecorder
        if (FormsControl.RecordAudio)
        {
            _mediaRecorder.SetAudioSource(AudioSource.Mic);
        }
        _mediaRecorder.SetVideoSource(VideoSource.Surface);

        // Set output format and encoding
        var profile = GetVideoProfile();
        _mediaRecorder.SetOutputFormat(profile.FileFormat);
        if (FormsControl.RecordAudio)
        {
            _mediaRecorder.SetAudioEncoder(profile.AudioCodec);
            _mediaRecorder.SetAudioEncodingBitRate(profile.AudioBitRate);
        }
        _mediaRecorder.SetVideoEncoder(profile.VideoCodec);
        _mediaRecorder.SetVideoSize(profile.VideoFrameWidth, profile.VideoFrameHeight);
        _mediaRecorder.SetVideoFrameRate(profile.VideoFrameRate);
        _mediaRecorder.SetVideoEncodingBitRate(profile.VideoBitRate);

        _mediaRecorder.SetOutputFile(_currentVideoFile);

        // Set orientation hint - native recording uses camera's native format (already landscape 1280x720)
        // So we need to compensate: if device is landscape but video is already landscape, don't rotate
        // The custom capture flow swaps encoder dims, but native recording doesn't
        var deviceRotation = FormsControl?.RecordingLockedRotation ?? 0;
        bool deviceIsLandscape = (deviceRotation == 90 || deviceRotation == 270);
        bool videoIsLandscape = (profile.VideoFrameWidth > profile.VideoFrameHeight);

        int orientationHint;
        if (deviceIsLandscape && videoIsLandscape)
        {
            // Selfie camera sensor is opposite to back camera, needs 180° rotation in landscape
            orientationHint = (FormsControl.Facing == CameraPosition.Selfie) ? 180 : 0;
        }
        else if (!deviceIsLandscape && !videoIsLandscape)
        {
            // Device is portrait, video is portrait - already correct, no rotation
            orientationHint = 0;
        }
        else
        {
            // Orientations don't match, apply device rotation
            orientationHint = deviceRotation;
        }

        _mediaRecorder.SetOrientationHint(orientationHint);

        // Prepare MediaRecorder
        _mediaRecorder.Prepare();

    }

    /// <summary>
    /// Get video recording profile based on current video quality
    /// </summary>
    private CamcorderProfile GetVideoProfile()
    {
        var cameraId = int.Parse(CameraId);
        var quality = FormsControl.VideoQuality;

        // Build an ordered list of preferred qualities for each enum value.
        // This enforces: Standard ≈ 720p, High ≈ 1080p, Ultra ≈ 2160p (4K), with sane fallbacks.
        var tryOrder = new List<CamcorderQuality>();
        switch (quality)
        {
            case VideoQuality.Low:
                tryOrder.AddRange(new[]
                {
                    CamcorderQuality.Low,
                    CamcorderQuality.High,
                    CamcorderQuality.Q720p,
                    CamcorderQuality.Q1080p,
                    CamcorderQuality.Q2160p
                });
                break;

            case VideoQuality.Standard: // 720p target
                tryOrder.AddRange(new[]
                {
                    CamcorderQuality.Q720p,
                    CamcorderQuality.Q1080p,
                    CamcorderQuality.High,
                    CamcorderQuality.Low
                });
                break;

            case VideoQuality.High: // 1080p target
                tryOrder.AddRange(new[]
                {
                    CamcorderQuality.Q1080p,
                    CamcorderQuality.High,
                    CamcorderQuality.Q720p,
                    CamcorderQuality.Low
                });
                break;

            case VideoQuality.Ultra: // 4K target
                tryOrder.AddRange(new[]
                {
                    CamcorderQuality.Q2160p,
                    CamcorderQuality.Q1080p,
                    CamcorderQuality.High,
                    CamcorderQuality.Q720p,
                    CamcorderQuality.Low
                });
                break;

            case VideoQuality.Manual:
                var manualQ = GetManualVideoProfile();
                tryOrder.AddRange(new[]
                {
                    manualQ,
                    CamcorderQuality.High,
                    CamcorderQuality.Q1080p,
                    CamcorderQuality.Q720p,
                    CamcorderQuality.Low
                });
                break;

            default:
                tryOrder.AddRange(new[]
                {
                    CamcorderQuality.High,
                    CamcorderQuality.Q1080p,
                    CamcorderQuality.Q720p,
                    CamcorderQuality.Low
                });
                break;
        }

        foreach (var q in tryOrder)
        {
            if (CamcorderProfile.HasProfile(cameraId, q))
                return CamcorderProfile.Get(cameraId, q);
        }

        // Last resort
        return CamcorderProfile.Get(cameraId, CamcorderQuality.Low);
    }

    /// <summary>
    /// Get manual video recording profile based on exact selected entry
    /// </summary>
    private CamcorderQuality GetManualVideoProfile()
    {
        // Ensure mapping is available
        if (_videoQualities.Count == 0)
            GetPredefinedVideoFormats();

        var idx = FormsControl.VideoFormatIndex;
        if (idx >= 0 && idx < _videoQualities.Count)
            return _videoQualities[idx];

        return CamcorderQuality.High; // Fallback
    }

    /// <summary>
    /// Get predefined video formats we can really record (CamcorderProfile-backed)
    /// Ordered from highest to lowest, including low-res when supported.
    /// Also populates _videoQualities to map indices to exact qualities.
    /// </summary>
    public List<VideoFormat> GetPredefinedVideoFormats()
    {
        var cameraId = int.Parse(CameraId);
        var formats = new List<VideoFormat>();

        _videoQualities.Clear();

        var supportedQualities = new List<(CamcorderQuality quality, string name)>
        {
            (CamcorderQuality.Q2160p, "2160p"),
            (CamcorderQuality.Q1080p, "1080p"),
            (CamcorderQuality.Q720p,  "720p"),
            (CamcorderQuality.Q480p,  "480p"),
            (CamcorderQuality.Cif,    "CIF"),    // 352x288
            (CamcorderQuality.Qvga,   "QVGA"),   // 320x240
            (CamcorderQuality.Qcif,   "QCIF"),   // 176x144
        };

        foreach (var (quality, name) in supportedQualities)
        {
            if (CamcorderProfile.HasProfile(cameraId, quality))
            {
                var profile = CamcorderProfile.Get(cameraId, quality);
                formats.Add(new VideoFormat
                {
                    Width = profile.VideoFrameWidth,
                    Height = profile.VideoFrameHeight,
                    FrameRate = profile.VideoFrameRate,
                    Codec = "H.264",
                    BitRate = profile.VideoBitRate,
                    FormatId = $"{name}_{profile.VideoFrameRate}fps"
                });
                _videoQualities.Add(quality);
            }
        }

        // As a last resort include 'Low' if nothing else matched
        if (formats.Count == 0 && CamcorderProfile.HasProfile(cameraId, CamcorderQuality.Low))
        {
            var profile = CamcorderProfile.Get(cameraId, CamcorderQuality.Low);
            formats.Add(new VideoFormat
            {
                Width = profile.VideoFrameWidth,
                Height = profile.VideoFrameHeight,
                FrameRate = profile.VideoFrameRate,
                Codec = "H.264",
                BitRate = profile.VideoBitRate,
                FormatId = $"Low_{profile.VideoFrameRate}fps"
            });
            _videoQualities.Add(CamcorderQuality.Low);
        }

        return formats;
    }

    /// <summary>
    /// Configure video flash settings
    /// </summary>
    private void ConfigureVideoFlash()
    {
        var flashMode = FormsControl.FlashMode;
        switch (flashMode)
        {
            case FlashMode.On:
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Torch);
                break;
            case FlashMode.Off:
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Off);
                break;
            case FlashMode.Strobe:
                // Strobe mode not typically supported during video recording
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)Android.Hardware.Camera2.FlashMode.Off);
                break;
        }
    }

    /// <summary>
    /// Clean up MediaRecorder resources
    /// </summary>
    private void CleanupMediaRecorder()
    {
        try
        {
            _mediaRecorder?.Reset();
            _mediaRecorder?.Release();
            _mediaRecorder = null;
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Error cleaning up MediaRecorder: {ex.Message}");
        }
    }

    /// <summary>
    /// Timer callback for video recording progress
    /// </summary>
    private void OnProgressTimer(object state)
    {
        if (!_isRecordingVideo)
            return;

        var elapsed = DateTime.Now - _recordingStartTime;
        VideoRecordingProgress?.Invoke(elapsed);
    }

    /// <summary>
    /// Starts video recording
    /// </summary>
    public async Task StartVideoRecording()
    {
        if (_isRecordingVideo || mCameraDevice == null || CaptureSession == null)
            return;

        try
        {
            // Stop current preview
            CaptureSession.StopRepeating();

            // Setup MediaRecorder
            await SetupMediaRecorder();

            // Get surfaces for preview and recording
            var surfaces = new List<Surface>();

            // Preview surface
            surfaces.Add(mImageReaderPreview.Surface);

            // MediaRecorder surface
            var recorderSurface = _mediaRecorder.Surface;
            surfaces.Add(recorderSurface);

            // Create capture request for video recording
            mPreviewRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Record);
            // Ensure consistent FOV during recording: explicitly disable video stabilization
            try
            {
                mPreviewRequestBuilder.Set(CaptureRequest.ControlVideoStabilizationMode,
                    (int)ControlVideoStabilizationMode.Off);
            }
            catch
            {

            }

            mPreviewRequestBuilder.AddTarget(mImageReaderPreview.Surface);
            mPreviewRequestBuilder.AddTarget(recorderSurface);

            // Configure flash for video recording
            if (mFlashSupported)
            {
                ConfigureVideoFlash();
            }

            // Start recording session
            CaptureSession.Close();
            var stateCallback = new VideoCameraCaptureStateCallback(this);

            mCameraDevice.CreateCaptureSession(surfaces, stateCallback, mBackgroundHandler);

            _isRecordingVideo = true;
            _recordingStartTime = DateTime.Now;

            // Start progress timer
            _progressTimer = new System.Threading.Timer(OnProgressTimer, null,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Failed to start video recording: {ex.Message}");
            _isRecordingVideo = false;
            CleanupMediaRecorder();
            VideoRecordingFailed?.Invoke(ex);
        }
    }

    /// <summary>
    /// Stops video recording
    /// </summary>
    public async Task StopVideoRecording()
    {
        if (!_isRecordingVideo || _mediaRecorder == null)
            return;

        try
        {
            // Stop progress timer
            _progressTimer?.Dispose();
            _progressTimer = null;

            // Stop MediaRecorder
            _mediaRecorder.Stop();
            _mediaRecorder.Reset();

            var recordingEndTime = DateTime.Now;
            var duration = recordingEndTime - _recordingStartTime;

            // Get file info
            var fileInfo = new Java.IO.File(_currentVideoFile);
            var fileSizeBytes = fileInfo.Length();

            // Create captured video object
            var capturedVideo = new CapturedVideo
            {
                FilePath = _currentVideoFile,
                Duration = duration,
                Format = GetCurrentVideoFormat(),
                Facing = FormsControl.Facing,
                Time = _recordingStartTime,
                FileSizeBytes = fileSizeBytes,
                Metadata = new Dictionary<string, object>
                {
                    { "Platform", "Android" },
                    { "CameraId", CameraId },
                    { "RecordingStartTime", _recordingStartTime },
                    { "RecordingEndTime", recordingEndTime },
                    { "SensorOrientation", SensorOrientation }
                }
            };

            _isRecordingVideo = false;
            CleanupMediaRecorder();

            // Restart preview
            CreateCameraPreviewSession();

            // Fire success event
            VideoRecordingSuccess?.Invoke(capturedVideo);

            Log.Debug(TAG, "Video recording completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Failed to stop video recording: {ex.Message}");
            _isRecordingVideo = false;
            CleanupMediaRecorder();
            VideoRecordingFailed?.Invoke(ex);
        }
    }

    /// <summary>
    /// Gets whether video recording is supported on this camera
    /// </summary>
    /// <returns>True if video recording is supported</returns>
    public bool CanRecordVideo()
    {
        try
        {
            // Check if we have camera device and it supports video recording
            return mCameraDevice != null && Platform.CurrentActivity != null;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Save video to gallery
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="album">Optional album name</param>
    /// <returns>Gallery path if successful, null if failed</returns>
    public async Task<string> SaveVideoToGallery(string videoFilePath, string album)
    {
        try
        {
            if (string.IsNullOrEmpty(videoFilePath) || !System.IO.File.Exists(videoFilePath))
            {
                Log.Error(TAG, $"Video file not found: {videoFilePath}");
                return null;
            }

            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                Log.Error(TAG, "Current activity is null");
                return null;
            }

            var contentResolver = activity.ContentResolver;
            var values = new ContentValues();

            // Set basic video metadata
            var fileName = System.IO.Path.GetFileNameWithoutExtension(videoFilePath);
            var fileExtension = System.IO.Path.GetExtension(videoFilePath);

            values.Put(MediaStore.Video.Media.InterfaceConsts.DisplayName, fileName);
            values.Put(MediaStore.Video.Media.InterfaceConsts.MimeType, "video/mp4");
            values.Put(MediaStore.Video.Media.InterfaceConsts.DateAdded, Java.Lang.JavaSystem.CurrentTimeMillis() / 1000);
            values.Put(MediaStore.Video.Media.InterfaceConsts.DateTaken, Java.Lang.JavaSystem.CurrentTimeMillis());

            // Set album/folder if specified
            if (!string.IsNullOrEmpty(album))
            {
                values.Put(MediaStore.Video.Media.InterfaceConsts.BucketDisplayName, album);
            }

            // For Android 10+ (API 29+), use scoped storage
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                values.Put(MediaStore.Video.Media.InterfaceConsts.RelativePath,
                    !string.IsNullOrEmpty(album) ? $"Movies/{album}" : "Movies/Camera");
                values.Put(MediaStore.Video.Media.InterfaceConsts.IsPending, 1);
            }

            // Insert into MediaStore
            var uri = contentResolver.Insert(MediaStore.Video.Media.ExternalContentUri, values);
            if (uri == null)
            {
                Log.Error(TAG, "Failed to create MediaStore entry");
                return null;
            }

            // Copy file to gallery
            using var inputStream = new Java.IO.FileInputStream(videoFilePath);
            using var outputStream = contentResolver.OpenOutputStream(uri);

            if (outputStream != null)
            {
                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = inputStream.Read(buffer)) > 0)
                {
                    outputStream.Write(buffer, 0, bytesRead);
                }
                outputStream.Flush();
            }

            // For Android 10+, clear the pending flag
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                values.Clear();
                values.Put(MediaStore.Video.Media.InterfaceConsts.IsPending, 0);
                contentResolver.Update(uri, values, null, null);
            }

            // Get the actual file path
            var projection = new[] { MediaStore.Video.Media.InterfaceConsts.Data };
            using var cursor = contentResolver.Query(uri, projection, null, null, null);

            if (cursor?.MoveToFirst() == true)
            {
                var columnIndex = cursor.GetColumnIndexOrThrow(MediaStore.Video.Media.InterfaceConsts.Data);
                var galleryPath = cursor.GetString(columnIndex);
                Debug.WriteLine(TAG, $"Video saved to gallery: {galleryPath}");
                return galleryPath;
            }

            Debug.WriteLine(TAG, $"Video saved to gallery with URI: {uri}");
            return uri.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Failed to save video to gallery: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Event fired when video recording completes successfully
    /// </summary>
    public Action<CapturedVideo> VideoRecordingSuccess { get; set; }

    /// <summary>
    /// Event fired when video recording fails
    /// </summary>
    public Action<Exception> VideoRecordingFailed { get; set; }

    /// <summary>
    /// Event fired when video recording progress updates
    /// </summary>
    public Action<TimeSpan> VideoRecordingProgress { get; set; }

    #endregion

    /// <summary>
    /// Camera capture session state callback for video recording
    /// </summary>
    public class VideoCameraCaptureStateCallback : CameraCaptureSession.StateCallback
    {
        private readonly NativeCamera _owner;

        public VideoCameraCaptureStateCallback(NativeCamera owner)
        {
            _owner = owner;
        }

        public override void OnConfigured(CameraCaptureSession session)
        {
            try
            {
                _owner.CaptureSession = session;

                // Start MediaRecorder
                _owner._mediaRecorder?.Start();

                // Start repeating request for video recording
                var captureRequest = _owner.mPreviewRequestBuilder.Build();
                session.SetRepeatingRequest(captureRequest, null, _owner.mBackgroundHandler);
            }
            catch (Exception ex)
            {
                Log.Error(NativeCamera.TAG, $"Failed to configure video capture session: {ex.Message}");
                _owner.VideoRecordingFailed?.Invoke(ex);
            }
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            Log.Error(NativeCamera.TAG, "Video capture session configuration failed");
            _owner._isRecordingVideo = false;
            _owner.CleanupMediaRecorder();
            _owner.VideoRecordingFailed?.Invoke(new Exception("Video capture session configuration failed"));
        }
    }
}
