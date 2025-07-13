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
    // Camera configuration constants

    // Max preview width that is guaranteed by Camera2 API
    public int MaxPreviewWidth = 800;

    // Max preview height that is guaranteed by Camera2 API
    public int MaxPreviewHeight = 800;

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
        double rotation, Metadata meta, string album)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            return await SaveJpgStreamToGalleryLegacy(stream, filename, rotation, meta, album);
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
        double rotation, Metadata meta, string album)
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
    private static Size ChooseOptimalSize(Size[] choices, int maxWidth, int maxHeight, Size aspectRatio, double ratioTolerance = 0.1)
    {
        double targetRatio = (double)aspectRatio.Width / aspectRatio.Height;
        Size optimalSize = null;
        double minDiffRatio = double.MaxValue;

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
            Super.Log("Couldn't find any suitable preview size");
            return choices[0];
        }

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
    /// Pass preview size as params
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    protected virtual void SetupHardware(int width, int height)
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
                var sensorSize = (Android.Util.SizeF)characteristics.Get(CameraCharacteristics.SensorInfoPhysicalSize);

                var unit = new CameraUnit
                {
                    Id = cameraId,
                    Facing = FormsControl.Facing,
                    MinFocalDistance = (float)characteristics.Get(CameraCharacteristics.LensInfoMinimumFocusDistance),
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
                    Debug.WriteLine($"[NativeCameraAndroid] Selected camera by index {FormsControl.CameraIndex}: {selectedCamera.Id}");
                }
                else
                {
                    Debug.WriteLine($"[NativeCameraAndroid] Invalid camera index {FormsControl.CameraIndex}, falling back to first camera");
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


                var maxPreviewWidth = displaySize.X + allowPreviewOverflow;
                var maxPreviewHeight = displaySize.Y + allowPreviewOverflow;

                bool rotated = false;

                if (SensorOrientation != 0 && SensorOrientation != 180)
                {
                    rotated = true;
                    maxPreviewWidth = displaySize.Y + allowPreviewOverflow;
                    maxPreviewHeight = displaySize.X + allowPreviewOverflow;
                }

                if (maxPreviewWidth > MaxPreviewWidth)
                {
                    maxPreviewWidth = MaxPreviewWidth;
                }

                if (maxPreviewHeight > MaxPreviewHeight)
                {
                    maxPreviewHeight = MaxPreviewHeight;
                }

                #region STILL PHOTO

                var stillSizes = map.GetOutputSizes((int)ImageFormatType.Yuv420888)
                    .ToList();

                List<Size> validSizes;
                if (rotated)
                {
                    validSizes = stillSizes.Where(x => x.Width > x.Height).OrderByDescending(x => x.Width * x.Height)
                        .ToList();
                }
                else
                {
                    validSizes = stillSizes.Where(x => x.Width < x.Height).OrderByDescending(x => x.Width * x.Height)
                        .ToList();
                }

                if (!validSizes.Any())
                {
                    validSizes = stillSizes.Where(x => x.Width == x.Height).OrderByDescending(x => x.Width * x.Height)
                        .ToList();
                }

                Size selectedSize;

                switch (FormsControl.CapturePhotoQuality)
                {
                    case CaptureQuality.Max:
                        selectedSize = validSizes.First();
                        break;

                    case CaptureQuality.Medium:
                        selectedSize = validSizes[validSizes.Count / 3];
                        break;

                    case CaptureQuality.Low:
                        selectedSize = validSizes.Last();
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


                var previewSize = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                    maxPreviewWidth, maxPreviewHeight, selectedSize);

                PreviewWidth = previewSize.Width;
                PreviewHeight = previewSize.Height;

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
            mBackgroundThread.QuitSafely();
            mBackgroundThread.Join();
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

    public void TurnOnFlash()
    {
        if (mCameraDevice == null || CaptureSession == null)
        {
            throw new InvalidOperationException("Camera not initialized or capture session not started.");
        }

        try
        {
            // Update the capture request builder to turn on the flash
            if (mFlashSupported)
            {
                mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Torch);

                // Apply this updated request to the session
                mPreviewRequest = mPreviewRequestBuilder.Build();
                CaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback, mBackgroundHandler);

                _isTorchOn = true;
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
    }

    public void TurnOffFlash()
    {
        if (mCameraDevice == null || CaptureSession == null)
        {
            throw new InvalidOperationException("Camera not initialized or capture session not started.");
        }

        try
        {
            // Update the capture request builder to turn on the flash
            if (mFlashSupported)
            {
                mPreviewRequestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Off);

                // Apply this updated request to the session
                mPreviewRequest = mPreviewRequestBuilder.Build();
                CaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback, mBackgroundHandler);

                _isTorchOn = false;
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
    }

    public void SetCapturingStillOptions(CaptureRequest.Builder requestBuilder)
    {
        requestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);

        if (mFlashSupported)
        {
            if (_isTorchOn)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
                requestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Torch);
            }
            else
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
                requestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Off);
            }
        }
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

            if (mFlashSupported)
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Torch);

            mCameraDevice.CreateCaptureSession(
                new List<Surface> { mImageReaderPreview.Surface, mImageReaderPhoto.Surface },
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

    public static T Cast<T>(Java.Lang.Object obj) where T : class
    {
        var propertyInfo = obj.GetType().GetProperty("Instance");
        return propertyInfo == null ? null : propertyInfo.GetValue(obj, null) as T;
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


    /*
    private int GetJpegOrientation()
    {
        int sensorOrientation = mRotateTexture;

        var deviceOrientation = 0;

        // Round device orientation to a multiple of 90
        deviceOrientation = (deviceOrientation + 45) / 90 * 90;

        // Reverse device orientation for front-facing cameras
        boolean facingFront = c.get(CameraCharacteristics.LENS_FACING) == CameraCharacteristics.LENS_FACING_FRONT;
        if (facingFront) deviceOrientation = -deviceOrientation;

        // Calculate desired JPEG orientation relative to camera orientation to make
        // the image upright relative to the device orientation
        int jpegOrientation = (sensorOrientation + deviceOrientation + 360) % 360;

        return jpegOrientation;
    }
    */


    public void StopCapturingStillImage()
    {
        try
        {
            mPreviewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
            SetCapturingStillOptions(mPreviewRequestBuilder);
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

    private static List<Size> _stillSizes;

    protected ImageReader FramesReader { get; set; }


    public CameraEffect Effect
    {
        get { return _effect; }
        set
        {
            _effect = value;
    
        }
    }

    #endregion
}
