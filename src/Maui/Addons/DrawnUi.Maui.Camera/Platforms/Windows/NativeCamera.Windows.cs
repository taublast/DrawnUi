using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices.WindowsRuntime;
using AppoMobi.Specials;
using DrawnUi.Views;
using Microsoft.Maui.ApplicationModel;
using SkiaSharp;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DrawnUi.Camera;

#region Direct3D Interop Interfaces

[ComImport]
[Guid("035f3ab4-482e-4e50-b960-13b05d3696c9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
}

[ComImport]
[Guid("4AE63092-6327-4c1b-80AE-BFE12EA32B86")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDXGISurface : IDXGIDeviceSubObject
{
    // IDXGIObject methods
    //void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
    //void SetPrivateDataInterface([In] ref Guid Name, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    //void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
    //void GetParent([In] ref Guid riid, out IntPtr ppParent);

    // IDXGIDeviceSubObject methods
    //void GetDevice([In] ref Guid riid, out IntPtr ppDevice);

    // IDXGISurface methods
    void GetDesc(out DXGI_SURFACE_DESC pDesc);
    void Map(out DXGI_MAPPED_RECT pLockedRect, uint MapFlags);
    void Unmap();
}

[ComImport]
[Guid("3D3E0379-F9DE-4D58-BB6C-18D62992F1A6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDXGIDeviceSubObject : IDXGIObject
{
    void GetDevice([In] ref Guid riid, out IntPtr ppDevice);
}

[ComImport]
[Guid("aec22fb8-76f3-4639-9be0-28eb43a67a2e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDXGIObject
{
    void SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface([In] ref Guid Name, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    void GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
    void GetParent([In] ref Guid riid, out IntPtr ppParent);
}

[StructLayout(LayoutKind.Sequential)]
struct DXGI_SURFACE_DESC
{
    public uint Width;
    public uint Height;
    public uint Format;
    public DXGI_SAMPLE_DESC SampleDesc;
}

[StructLayout(LayoutKind.Sequential)]
struct DXGI_SAMPLE_DESC
{
    public uint Count;
    public uint Quality;
}

[StructLayout(LayoutKind.Sequential)]
struct DXGI_MAPPED_RECT
{
    public int Pitch;
    public IntPtr pBits;
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}

#endregion



public partial class NativeCamera : IDisposable, INativeCamera, INotifyPropertyChanged
{
    protected readonly SkiaCamera FormsControl;
    private MediaCapture _mediaCapture;
    private MediaFrameReader _frameReader;
    private CameraProcessorState _state = CameraProcessorState.None;
    private bool _flashSupported;
    private bool _isCapturingStill;
    private double _zoomScale = 1.0;
    private readonly object _lockPreview = new();
    private volatile CapturedImage _preview;
    private DeviceInformation _cameraDevice;
    private MediaFrameSource _frameSource;

    private readonly SemaphoreSlim _frameSemaphore = new(1, 1);
    private volatile bool _isProcessingFrame = false;

    public NativeCamera(SkiaCamera formsControl)
    {
        FormsControl = formsControl;
        Setup();
    }

    #region Properties

    public CameraProcessorState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();

                MainThread.BeginInvokeOnMainThread(() =>
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
                });
            }
        }
    }

    public Action<CapturedImage> PreviewCaptureSuccess { get; set; }
    public Action<CapturedImage> StillImageCaptureSuccess { get; set; }
    public Action<Exception> StillImageCaptureFailed { get; set; }

    #endregion

    #region Setup

    private async void Setup()
    {
        try
        {
            //Debug.WriteLine("[NativeCameraWindows] Starting setup...");
            await SetupHardware();
            //Debug.WriteLine("[NativeCameraWindows] Hardware setup completed successfully");
            State = CameraProcessorState.Enabled;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] Setup error: {e}");
            State = CameraProcessorState.Error;
        }
    }

    private async Task SetupHardware()
    {
        //Debug.WriteLine("[NativeCameraWindows] Finding camera devices...");

        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        Debug.WriteLine($"[NativeCameraWindows] Found {devices.Count} camera devices");

        foreach (var device in devices)
        {
            Debug.WriteLine($"[NativeCameraWindows] Device: {device.Name}, Id: {device.Id}, Panel: {device.EnclosureLocation?.Panel}");
        }

        // Manual camera selection
        if (FormsControl.Facing == CameraPosition.Manual && FormsControl.CameraIndex >= 0)
        {
            if (FormsControl.CameraIndex < devices.Count)
            {
                _cameraDevice = devices[FormsControl.CameraIndex];
                Debug.WriteLine($"[NativeCameraWindows] Selected camera by index {FormsControl.CameraIndex}: {_cameraDevice.Name}");
            }
            else
            {
                Debug.WriteLine($"[NativeCameraWindows] Invalid camera index {FormsControl.CameraIndex}, falling back to first camera");
                _cameraDevice = devices.FirstOrDefault();
            }
        }
        else
        {
            // Automatic selection based on facing
            var cameraPosition = FormsControl.Facing == CameraPosition.Selfie
                ? Panel.Front
                : Panel.Back;

            _cameraDevice = devices.FirstOrDefault(d =>
            {
                var location = d.EnclosureLocation;
                return location?.Panel == cameraPosition;
            }) ?? devices.FirstOrDefault();
        }

        if (_cameraDevice == null)
        {
            throw new InvalidOperationException("No camera device found");
        }

        Debug.WriteLine($"[NativeCameraWindows] Selected camera: {_cameraDevice.Name}");

        //Debug.WriteLine("[NativeCameraWindows] Initializing MediaCapture...");
        _mediaCapture = new MediaCapture();
        var settings = new MediaCaptureInitializationSettings
        {
            VideoDeviceId = _cameraDevice.Id,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            PhotoCaptureSource = PhotoCaptureSource.VideoPreview
        };



        await _mediaCapture.InitializeAsync(settings);
        Debug.WriteLine("[NativeCameraWindows] MediaCapture initialized successfully");

        _flashSupported = _mediaCapture.VideoDeviceController.FlashControl.Supported;
        Debug.WriteLine($"[NativeCameraWindows] Flash supported: {_flashSupported}");

        //Debug.WriteLine("[NativeCameraWindows] Setting up frame reader...");
        await SetupFrameReader();
        Debug.WriteLine("[NativeCameraWindows] Frame reader setup completed");

        // Create and assign CameraUnit to parent control using real frame source data
        CreateCameraUnit();

        Debug.WriteLine("[NativeCameraWindows] Auto-starting frame reader...");
        await StartFrameReaderAsync();
        Debug.WriteLine("[NativeCameraWindows] Frame reader auto-start completed");
    }

    private void CreateCameraUnit()
    {
        try
        {
            Debug.WriteLine("[NativeCameraWindows] Creating CameraUnit from real MediaFrameSource data...");

            // Extract real camera data from the MediaFrameSource and selected format
            var cameraSpecs = ExtractCameraSpecsFromFrameSource();

            // Create CameraUnit with real Windows camera information
            var cameraUnit = new CameraUnit
            {
                Id = _cameraDevice.Id,
                Facing = FormsControl.Facing,
                FocalLengths = cameraSpecs.FocalLengths,
                FocalLength = cameraSpecs.FocalLength,
                FieldOfView = cameraSpecs.FieldOfView,
                SensorWidth = cameraSpecs.SensorWidth,
                SensorHeight = cameraSpecs.SensorHeight,
                MinFocalDistance = cameraSpecs.MinFocalDistance,
                Meta = FormsControl.CreateMetadata()
            };

            // Assign to parent control
            FormsControl.CameraDevice = cameraUnit;

            Debug.WriteLine($"[NativeCameraWindows] CameraUnit created from real data:");
            Debug.WriteLine($"  - Id: {cameraUnit.Id}");
            Debug.WriteLine($"  - Facing: {cameraUnit.Facing}");
            Debug.WriteLine($"  - Focal Length: {cameraUnit.FocalLength}mm");
            Debug.WriteLine($"  - FOV: {cameraUnit.FieldOfView}°");
            Debug.WriteLine($"  - Sensor: {cameraUnit.SensorWidth}x{cameraUnit.SensorHeight}mm");
            Debug.WriteLine($"  - FocalLengths count: {cameraUnit.FocalLengths.Count}");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] CreateCameraUnitFromRealData error: {e}");
        }
    }

    private WindowsCameraSpecs ExtractCameraSpecsFromFrameSource()
    {
        var specs = new WindowsCameraSpecs();

        try
        {
            Debug.WriteLine("[NativeCameraWindows] Extracting camera specs from MediaFrameSource...");

            // Get the current format that was set
            var currentFormat = _frameSource?.CurrentFormat;
            if (currentFormat != null)
            {
                // Extract real resolution data
                var width = currentFormat.VideoFormat.Width;
                var height = currentFormat.VideoFormat.Height;
                var fps = currentFormat.FrameRate.Numerator / (double)currentFormat.FrameRate.Denominator;

                Debug.WriteLine($"[NativeCameraWindows] Current format: {width}x{height} @ {fps:F1} FPS");

                // Calculate sensor size from resolution (using typical pixel pitch for cameras)
                // Most modern cameras have pixel pitch between 1.0-3.0 micrometers
                var pixelPitchMicrometers = 1.4f; // Typical for modern cameras
                specs.SensorWidth = (width * pixelPitchMicrometers) / 1000.0f; // Convert to mm
                specs.SensorHeight = (height * pixelPitchMicrometers) / 1000.0f; // Convert to mm

                Debug.WriteLine($"[NativeCameraWindows] Calculated sensor size: {specs.SensorWidth:F2}x{specs.SensorHeight:F2}mm");
            }

            // Extract zoom capabilities for focal length estimation
            if (_mediaCapture?.VideoDeviceController?.ZoomControl?.Supported == true)
            {
                var zoomControl = _mediaCapture.VideoDeviceController.ZoomControl;
                var minZoom = zoomControl.Min;
                var maxZoom = zoomControl.Max;
                var currentZoom = zoomControl.Value;

                Debug.WriteLine($"[NativeCameraWindows] Zoom capabilities: {minZoom}x - {maxZoom}x (current: {currentZoom}x)");

                // Estimate base focal length from sensor size and typical field of view
                if (specs.SensorWidth > 0)
                {
                    // Assume typical webcam FOV of 60-70 degrees
                    var estimatedFOV = 65.0f;
                    var fovRadians = estimatedFOV * Math.PI / 180.0;
                    specs.FocalLength = (float)(specs.SensorWidth / (2.0 * Math.Tan(fovRadians / 2.0)));
                    specs.FieldOfView = estimatedFOV;

                    // Add focal lengths for zoom range
                    specs.FocalLengths.Add(specs.FocalLength * (float)minZoom);
                    if (maxZoom > minZoom)
                    {
                        specs.FocalLengths.Add(specs.FocalLength * (float)maxZoom);
                    }

                    Debug.WriteLine($"[NativeCameraWindows] Calculated focal length: {specs.FocalLength:F2}mm, FOV: {specs.FieldOfView}°");
                }
            }

            // Extract focus capabilities
            if (_mediaCapture?.VideoDeviceController?.FocusControl?.Supported == true)
            {
                var focusControl = _mediaCapture.VideoDeviceController.FocusControl;
                if (focusControl.SupportedFocusRanges?.Contains(AutoFocusRange.Macro) == true)
                {
                    specs.MinFocalDistance = 0.1f; // 10cm for macro
                }
                else if (focusControl.SupportedFocusRanges?.Contains(AutoFocusRange.Normal) == true)
                {
                    specs.MinFocalDistance = 0.3f; // 30cm for normal
                }
                else
                {
                    specs.MinFocalDistance = 0.5f; // 50cm default
                }
                Debug.WriteLine($"[NativeCameraWindows] Min focus distance: {specs.MinFocalDistance}m");
            }

            // Ensure we have at least basic values
            if (specs.FocalLength <= 0)
            {
                specs.FocalLength = 4.0f; // Reasonable default based on sensor size
                specs.FocalLengths.Add(specs.FocalLength);
            }
            if (specs.FieldOfView <= 0)
            {
                specs.FieldOfView = 65.0f; // Typical webcam FOV
            }
            if (specs.MinFocalDistance <= 0)
            {
                specs.MinFocalDistance = 0.3f;
            }

            Debug.WriteLine($"[NativeCameraWindows] Final specs: Focal={specs.FocalLength:F2}mm, FOV={specs.FieldOfView}°, FocalLengths={specs.FocalLengths.Count}");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ExtractCameraSpecsFromFrameSource error: {e}");
            // Set minimal defaults
            specs.FocalLength = 4.0f;
            specs.FocalLengths.Add(specs.FocalLength);
            specs.FieldOfView = 65.0f;
            specs.SensorWidth = 5.6f;
            specs.SensorHeight = 4.2f;
            specs.MinFocalDistance = 0.3f;
        }

        return specs;
    }

    public class WindowsCameraSpecs
    {
        public float FocalLength { get; set; }
        public List<float> FocalLengths { get; set; } = new List<float>();
        public float FieldOfView { get; set; }
        public float SensorWidth { get; set; }
        public float SensorHeight { get; set; }
        public float MinFocalDistance { get; set; }
    }

    private async Task SetupFrameReader()
    {
        //Debug.WriteLine("[NativeCameraWindows] Getting frame source groups...");

        var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
        Debug.WriteLine($"[NativeCameraWindows] Found {frameSourceGroups.Count} frame source groups");

        var selectedGroup = frameSourceGroups.FirstOrDefault(g =>
            g.SourceInfos.Any(si => si.DeviceInformation?.Id == _cameraDevice.Id));

        if (selectedGroup == null)
        {
            //Debug.WriteLine("[NativeCameraWindows] Could not find frame source group for camera, trying alternative approach...");

            if (_mediaCapture.FrameSources.Count > 0)
            {
                Debug.WriteLine($"[NativeCameraWindows] Found {_mediaCapture.FrameSources.Count} frame sources in MediaCapture");
                _frameSource = _mediaCapture.FrameSources.Values.FirstOrDefault(fs => fs.Info.SourceKind == MediaFrameSourceKind.Color);

                if (_frameSource == null)
                {
                    throw new InvalidOperationException("Could not find color frame source in MediaCapture");
                }
            }
            else
            {
                throw new InvalidOperationException("Could not find frame source group for camera");
            }
        }
        else
        {
            Debug.WriteLine($"[NativeCameraWindows] Selected frame source group: {selectedGroup.DisplayName}");

            var colorSourceInfo = selectedGroup.SourceInfos.FirstOrDefault(si =>
                si.SourceKind == MediaFrameSourceKind.Color);

            if (colorSourceInfo == null)
            {
                throw new InvalidOperationException("Could not find color frame source");
            }

            Debug.WriteLine($"[NativeCameraWindows] Selected color source: {colorSourceInfo.Id}");
            _frameSource = _mediaCapture.FrameSources[colorSourceInfo.Id];
        }

        Debug.WriteLine($"[NativeCameraWindows] Frame source info: {_frameSource.Info.Id}, Kind: {_frameSource.Info.SourceKind}");

        // Get supported formats and their frame rates
        foreach (var format in _frameSource.SupportedFormats)
        {
            var fps = format.FrameRate.Numerator / (double)format.FrameRate.Denominator;
            Debug.WriteLine($"[NativeCameraWindows] Available format: {format.VideoFormat.Width}x{format.VideoFormat.Height} @ {fps:F1} FPS");
        }

        var preferredFormat = _frameSource.SupportedFormats.FirstOrDefault(format =>
            format.VideoFormat.Width >= 640 &&
            format.VideoFormat.Height >= 480);

        if (preferredFormat != null)
        {
            var fps = preferredFormat.FrameRate.Numerator / (double)preferredFormat.FrameRate.Denominator;
            Debug.WriteLine($"[NativeCameraWindows] Setting frame source format: {preferredFormat.VideoFormat.Width}x{preferredFormat.VideoFormat.Height} @ {fps:F1} FPS");
            await _frameSource.SetFormatAsync(preferredFormat);
        }
        else
        {
            Debug.WriteLine("[NativeCameraWindows] No suitable format found, using default");
        }

        _frameReader = await _mediaCapture.CreateFrameReaderAsync(_frameSource, MediaEncodingSubtypes.Bgra8);
        _frameReader.FrameArrived += OnFrameArrived;
        Debug.WriteLine("[NativeCameraWindows] Frame reader created and event handler attached");
    }

    #endregion

    #region Optimized Direct3D Processing

    /// <summary>
    /// Get the GRContext from the accelerated SkiaSharp canvas
    /// </summary>
    private GRContext GetExistingGRContext()
    {
        try
        {
            if (FormsControl.Superview?.CanvasView is SkiaViewAccelerated accelerated)
            {
                return accelerated.GRContext;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] GetExistingGRContext error: {e}");
        }
        return null;
    }

    /// <summary>
    /// Extract DXGI surface from Direct3D surface
    /// </summary>
    private IDXGISurface GetDXGISurfaceFromD3DSurface(Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface d3dSurface)
    {
        try
        {
            // Try to get the DXGI interface access
            if (d3dSurface is IDirect3DDxgiInterfaceAccess access)
            {
                var dxgiSurfaceGuid = typeof(IDXGISurface).GUID;
                var surfacePtr = access.GetInterface(ref dxgiSurfaceGuid);
                if (surfacePtr != IntPtr.Zero)
                {
                    return Marshal.GetObjectForIUnknown(surfacePtr) as IDXGISurface;
                }
            }

            Debug.WriteLine("[NativeCameraWindows] Direct3D surface does not support DXGI interface access");
            return null;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] GetDXGISurfaceFromD3DSurface error: {e}");
            return null;
        }
    }

    /// <summary>
    /// Create optimized SKImage directly from Direct3D surface
    /// </summary>
    private SKImage ConvertDirect3DToOptimizedSKImage(Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface d3dSurface)
    {
        try
        {
            var grContext = GetExistingGRContext();
            if (grContext == null)
            {
                Debug.WriteLine("[NativeCameraWindows] No GRContext available, falling back to software processing");
                return null;
            }

            var dxgiSurface = GetDXGISurfaceFromD3DSurface(d3dSurface);
            if (dxgiSurface == null)
            {
                Debug.WriteLine("[NativeCameraWindows] Failed to extract DXGI surface");
                return null;
            }

            dxgiSurface.GetDesc(out DXGI_SURFACE_DESC desc);
            Debug.WriteLine($"[NativeCameraWindows] Creating GPU SKImage: {desc.Width}x{desc.Height}, Format: {desc.Format}");

            var imageInfo = new SKImageInfo((int)desc.Width, (int)desc.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            dxgiSurface.Map(out DXGI_MAPPED_RECT mappedRect, 0);

            try
            {
                var skImage = SKImage.FromPixels(imageInfo, mappedRect.pBits, mappedRect.Pitch);
                Debug.WriteLine($"[NativeCameraWindows] Successfully created SKImage from D3D surface: {skImage?.Width}x{skImage?.Height}");
                return skImage;
            }
            finally
            {
                dxgiSurface.Unmap();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ConvertDirect3DToGPUSKImage error: {e}");
            return null;
        }
    }



    #endregion

    #region Improved Frame Processing

    /// <summary>
    /// Process Direct3D frame using GPU-assisted conversion to SoftwareBitmap
    /// This leverages GPU-resident data for better performance than pure software processing
    /// Will set _preview.
    /// </summary>
    private async void ProcessDirect3DFrameAsync(Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface d3dSurface)
    {
        if (!await _frameSemaphore.WaitAsync(1)) // Skip if busy processing
            return;

        _isProcessingFrame = true;
        CapturedImage capturedImage = null;
        try
        {
            // Use GPU-assisted conversion from Direct3D surface to SoftwareBitmap
            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(d3dSurface);
            if (softwareBitmap != null)
            {
                var skImage = await ConvertToSKImageDirectAsync(softwareBitmap);
                if (skImage != null)
                {
                    var meta = FormsControl.CameraDevice.Meta;
                    var rotation = FormsControl.DeviceRotation;
                    Metadata.ApplyRotation(meta, rotation);

                    capturedImage = new CapturedImage()
                    {
                        Facing = FormsControl.Facing,
                        Time = DateTime.UtcNow,
                        Image = skImage, // Transfer ownership to CapturedImage - renderer will dispose
                        Meta = meta,
                        Rotation = rotation
                    };
                }
                softwareBitmap.Dispose();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ProcessDirect3DFrameAsync error: {e}");
        }
        finally
        {
            _isProcessingFrame = false;
            // Update preview safely
            lock (_lockPreview)
            {
                _preview?.Dispose(); // Only dispose old preview, not the new SKImage
                _preview = capturedImage;
            }
            if (capturedImage != null)
            {
                // Update camera metadata with current exposure settings
                UpdateCameraMetadata();

                //PREVIEW FRAME READY
                FormsControl.UpdatePreview();
            }
            _frameSemaphore.Release();
        }
    }

    /// <summary>
    /// Improved frame arrival handler with GPU acceleration priority
    /// </summary>
    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        // Skip if already processing a frame to prevent backlog
        if (_isProcessingFrame)
            return;

        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            if (frame?.VideoMediaFrame != null)
            {
                var videoFrame = frame.VideoMediaFrame;

                // PRIORITY 1: Use GPU-assisted Direct3D processing
                if (videoFrame.Direct3DSurface != null)
                {
                    //Debug.WriteLine("[NativeCameraWindows] Frame arrived with Direct3D surface, using GPU-assisted processing...");
                    ProcessDirect3DFrameAsync(videoFrame.Direct3DSurface);
                    return;
                }

                // PRIORITY 2: Fallback to software bitmap processing
                if (videoFrame.SoftwareBitmap != null)
                {
                    //Debug.WriteLine("[NativeCameraWindows] Frame arrived with software bitmap, processing...");
                    ProcessFrameAsync(videoFrame.SoftwareBitmap);
                }
                else
                {
                    //Debug.WriteLine("[NativeCameraWindows] Frame arrived but no usable bitmap format available");
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] Frame processing error: {e}");
        }
    }

    /// <summary>
    /// Process frame with direct pixel access
    /// </summary>
    private async void ProcessFrameAsync(SoftwareBitmap softwareBitmap)
    {
        if (!await _frameSemaphore.WaitAsync(1)) // Skip if busy processing
            return;

        _isProcessingFrame = true;

        try
        {
            var skImage = await ConvertToSKImageDirectAsync(softwareBitmap);
            if (skImage != null)
            {
                var meta = FormsControl.CameraDevice.Meta;
                var rotation = FormsControl.DeviceRotation;
                Metadata.ApplyRotation(meta, rotation);

                var capturedImage = new CapturedImage()
                {
                    Facing = FormsControl.Facing,
                    Time = DateTime.UtcNow,
                    Image = skImage, // Transfer ownership to CapturedImage - renderer will dispose
                    Meta = meta,
                    Rotation = rotation
                };

                // Update preview safely
                lock (_lockPreview)
                {
                    _preview?.Dispose(); // Only dispose old preview, not the new SKImage
                    _preview = capturedImage;
                }

                // Update camera metadata with current exposure settings
                UpdateCameraMetadata();

                //PREVIEW FRAME READY
                FormsControl.UpdatePreview();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ProcessFrameAsync error: {e}");
        }
        finally
        {
            _isProcessingFrame = false;
            _frameSemaphore.Release();
        }
    }





    /// <summary>
    /// Convert SoftwareBitmap to SKImage using direct memory access
    /// </summary>
    private async Task<SKImage> ConvertToSKImageDirectAsync(SoftwareBitmap softwareBitmap)
    {
        try
        {
            //Debug.WriteLine($"[NativeCameraWindows] Converting SoftwareBitmap to SKImage directly...");

            // Ensure correct format
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap,
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var width = softwareBitmap.PixelWidth;
            var height = softwareBitmap.PixelHeight;

            try
            {
                using var buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read);
                using var reference = buffer.CreateReference();

                if (reference is IMemoryBufferByteAccess memoryAccess)
                {
                    unsafe
                    {
                        memoryAccess.GetBuffer(out byte* dataInBytes, out uint capacity);
                        var planeDescription = buffer.GetPlaneDescription(0);
                        var stride = planeDescription.Stride;

                        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var skImage = SKImage.FromPixels(info, new IntPtr(dataInBytes), stride);
                        return skImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NativeCameraWindows] Direct access failed, falling back: {ex.Message}");
            }

            return await ConvertToSKImageManagedCopy(softwareBitmap);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ConvertToSKImageDirectAsync error: {e}");
            return null;
        }
    }

    /// <summary>
    /// Fallback conversion using BMP encoding
    /// </summary>
    private async Task<SKImage> ConvertToSKImageManagedCopy(SoftwareBitmap softwareBitmap)
    {
        try
        {
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap,
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var width = softwareBitmap.PixelWidth;
            var height = softwareBitmap.PixelHeight;

            try
            {
                using var bitmapBuffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read);
                using var reference = bitmapBuffer.CreateReference();

                if (reference is IMemoryBufferByteAccess memoryAccess)
                {
                    unsafe
                    {
                        memoryAccess.GetBuffer(out byte* dataInBytes, out uint capacity);
                        var planeDescription = bitmapBuffer.GetPlaneDescription(0);
                        var stride = planeDescription.Stride;

                        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var skImage = SKImage.FromPixels(info, new IntPtr(dataInBytes), stride);
                        return skImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NativeCameraWindows] Direct access failed, using stream approach: {ex.Message}");
            }

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);

            encoder.BitmapTransform.ScaledWidth = (uint)softwareBitmap.PixelWidth;
            encoder.BitmapTransform.ScaledHeight = (uint)softwareBitmap.PixelHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.NearestNeighbor;

            await encoder.FlushAsync();

            var size = (int)stream.Size;
            var bytes = new byte[size];
            stream.Seek(0);
            var streamBuffer = await stream.ReadAsync(bytes.AsBuffer(), (uint)size, InputStreamOptions.None);

            var skImageFromStream = SKImage.FromEncodedData(bytes);
            return skImageFromStream;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ConvertToSKImageManagedCopy error: {e}");
            return null;
        }
    }

    private async void ConvertDirect3DToSoftwareBitmapAsync(VideoMediaFrame videoFrame)
    {
        try
        {
            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(videoFrame.Direct3DSurface);
            if (softwareBitmap != null)
            {
                //Debug.WriteLine("[NativeCameraWindows] Successfully converted Direct3D surface to software bitmap");
                ProcessFrameAsync(softwareBitmap);
            }
            else
            {
                //Debug.WriteLine("[NativeCameraWindows] Failed to convert Direct3D surface to software bitmap");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ConvertDirect3DToSoftwareBitmapAsync error: {e}");
        }
    }

    private async Task StartFrameReaderAsync()
    {
        if (_frameReader == null)
        {
            //Debug.WriteLine("[NativeCameraWindows] Frame reader is null, cannot start");
            State = CameraProcessorState.Error;
            return;
        }

        try
        {
            //Debug.WriteLine("[NativeCameraWindows] Starting frame reader...");
            var result = await _frameReader.StartAsync();
            Debug.WriteLine($"[NativeCameraWindows] Frame reader start result: {result}");

            if (result == MediaFrameReaderStartStatus.Success)
            {
                State = CameraProcessorState.Enabled;
                //Debug.WriteLine("[NativeCameraWindows] Camera started successfully");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DeviceDisplay.Current.KeepScreenOn = true;
                });
            }
            else
            {
                Debug.WriteLine($"[NativeCameraWindows] Failed to start frame reader: {result}");
                State = CameraProcessorState.Error;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] StartFrameReaderAsync error: {e}");
            State = CameraProcessorState.Error;
        }
    }

    #endregion

    #region INativeCamera Implementation

    public async void Start()
    {
        if (State == CameraProcessorState.Enabled && _frameReader != null)
        {
            //Debug.WriteLine("[NativeCameraWindows] Camera already started");
            return;
        }

        await StartFrameReaderAsync();
    }

    public async void Stop(bool force = false)
    {
        if (State == CameraProcessorState.None && !force)
            return;

        if (State != CameraProcessorState.Enabled && !force)
            return; //avoid spam

        try
        {
            //Debug.WriteLine("[NativeCameraWindows] Stopping frame reader...");
            if (_frameReader != null)
            {
                await _frameReader.StopAsync();
                //Debug.WriteLine("[NativeCameraWindows] Frame reader stopped");
            }





            State = CameraProcessorState.None;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.Current.KeepScreenOn = false;
            });
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] Stop error: {e}");
            State = CameraProcessorState.Error;
        }
    }

    public void TurnOnFlash()
    {
        if (_flashSupported && _mediaCapture != null)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FlashControl.Enabled = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[NativeCameraWindows] TurnOnFlash error: {e}");
            }
        }
    }

    public void TurnOffFlash()
    {
        if (_flashSupported && _mediaCapture != null)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FlashControl.Enabled = false;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[NativeCameraWindows] TurnOffFlash error: {e}");
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

    public void ApplyDeviceOrientation(int orientation)
    {
        // Windows handles orientation automatically in most cases
    }

    public void SetZoom(float value)
    {
        _zoomScale = value;

        if (_mediaCapture?.VideoDeviceController?.ZoomControl?.Supported == true)
        {
            try
            {
                var zoomControl = _mediaCapture.VideoDeviceController.ZoomControl;
                var clampedValue = Math.Max(zoomControl.Min, Math.Min(zoomControl.Max, value));
                zoomControl.Value = clampedValue;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[NativeCameraWindows] SetZoom error: {e}");
            }
        }
    }

    /// <summary>
    /// Updates camera metadata with current exposure settings from the camera
    /// </summary>
    private void UpdateCameraMetadata()
    {
        try
        {
            if (_mediaCapture?.VideoDeviceController == null || FormsControl?.CameraDevice?.Meta == null)
                return;

            var controller = _mediaCapture.VideoDeviceController;
            var meta = FormsControl.CameraDevice.Meta;

            // Update ISO if supported
            if (controller.IsoSpeedControl.Supported)
            {
                try
                {
                    var currentISO = controller.IsoSpeedControl.Value;
                    if (currentISO > 0)
                    {
                        meta.ISO = (int)currentISO;
                    }
                }
                catch (Exception ex)
                {
                    // ISO might not be available in auto mode
                    System.Diagnostics.Debug.WriteLine($"[Windows Camera] Could not read ISO: {ex.Message}");
                }
            }

            // Update exposure time if supported
            if (controller.ExposureControl.Supported)
            {
                try
                {
                    var currentExposure = controller.ExposureControl.Value;
                    if (currentExposure.TotalSeconds > 0)
                    {
                        meta.Shutter = currentExposure.TotalSeconds;
                    }
                }
                catch (Exception ex)
                {
                    // Exposure might not be available in auto mode
                    System.Diagnostics.Debug.WriteLine($"[Windows Camera] Could not read exposure: {ex.Message}");
                }
            }

            // Set a default aperture value since Windows doesn't expose this
            // Most webcams have a fixed aperture around f/2.0 to f/2.8
            if (meta.Aperture.GetValueOrDefault() <= 0)
            {
                meta.Aperture = 2.4; // Common webcam aperture
            }

            // Set default values if we couldn't read from camera
            if (meta.ISO.GetValueOrDefault() <= 0)
            {
                meta.ISO = 100; // Default ISO
            }
            if (meta.Shutter.GetValueOrDefault() <= 0)
            {
                meta.Shutter = 1.0 / 30; // Default 30fps = 1/30s exposure
            }

            //if (DateTime.Now.Millisecond % 1000 < 50) // Log roughly once per second
            //{
            //    System.Diagnostics.Debug.WriteLine($"[Windows Camera Meta] ISO: {meta.ISO}, Shutter: {meta.Shutter:F4}s, Aperture: f/{meta.Aperture:F1}");
            //}
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Windows Camera] UpdateCameraMetadata error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets manual exposure settings for the camera (not supported on Windows)
    /// </summary>
    /// <param name="iso">ISO sensitivity value</param>
    /// <param name="shutterSpeed">Shutter speed in seconds</param>
    public bool SetManualExposure(float iso, float shutterSpeed)
    {
        System.Diagnostics.Debug.WriteLine("[Windows MANUAL] Manual exposure not fully supported - Windows camera controls are limited");
        // Windows UWP camera API doesn't support full manual exposure control like iOS/Android
        // ExposureControl.Value and IsoSpeedControl.Value are read-only properties
        // Manual exposure would require using MediaFrameReader with custom processing
        return false;
    }

    /// <summary>
    /// Sets the camera to automatic exposure mode (Windows is already in auto mode by default)
    /// </summary>
    public void SetAutoExposure()
    {
        System.Diagnostics.Debug.WriteLine("[Windows AUTO] Camera is already in auto exposure mode by default");
        // Windows camera is in auto mode by default and doesn't need explicit setting
    }

    /// <summary>
    /// Gets the manual exposure capabilities and recommended settings for the camera (not supported on Windows)
    /// </summary>
    /// <returns>Camera manual exposure range information indicating no support</returns>
    public CameraManualExposureRange GetExposureRange()
    {
        // Windows UWP camera API doesn't support full manual exposure control
        // ExposureControl.Value and IsoSpeedControl.Value are read-only properties
        System.Diagnostics.Debug.WriteLine("[Windows RANGE] Manual exposure not supported");

        return new CameraManualExposureRange(0, 0, 0, 0, false, null);
    }

    public async void TakePicture()
    {
        if (_isCapturingStill || _mediaCapture == null)
            return;

        _isCapturingStill = true;

        try
        {
            Debug.WriteLine("[NativeCameraWindows] Taking picture...");

            // Create image encoding properties for high quality JPEG
            var imageProperties = ImageEncodingProperties.CreateJpeg();
            imageProperties.Width = 1920; // Set higher resolution for still capture
            imageProperties.Height = 1080;

            // Capture photo to stream
            using var stream = new InMemoryRandomAccessStream();
            await _mediaCapture.CapturePhotoToStreamAsync(imageProperties, stream);
            Debug.WriteLine($"[NativeCameraWindows] Photo captured to stream, size: {stream.Size} bytes");

            // Read stream data
            stream.Seek(0);
            var bytes = new byte[stream.Size];
            await stream.AsStream().ReadAsync(bytes, 0, bytes.Length);

            // Create SKImage from captured data
            var skImage = SKImage.FromEncodedData(bytes);
            Debug.WriteLine($"[NativeCameraWindows] SKImage created: {skImage?.Width}x{skImage?.Height}");

            var meta = Reflection.Clone(FormsControl.CameraDevice.Meta);
            var rotation = FormsControl.DeviceRotation;
            Metadata.ApplyRotation(meta, rotation);

            var capturedImage = new CapturedImage()
            {
                Facing = FormsControl.Facing,
                Time = DateTime.UtcNow,
                Image = skImage,
                Rotation = rotation,
                Meta = meta
            };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                StillImageCaptureSuccess?.Invoke(capturedImage);
            });

            Debug.WriteLine("[NativeCameraWindows] Restarting frame reader to resume preview...");
            await RestartFrameReaderAsync();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] TakePicture error: {e}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StillImageCaptureFailed?.Invoke(e);
            });

            // Restart frame reader even if capture failed
            try
            {
                await RestartFrameReaderAsync();
            }
            catch (Exception restartEx)
            {
                Debug.WriteLine($"[NativeCameraWindows] Failed to restart frame reader: {restartEx}");
            }
        }
        finally
        {
            _isCapturingStill = false;
        }
    }

    private async Task RestartFrameReaderAsync()
    {
        try
        {
            if (_frameReader != null)
            {
                Debug.WriteLine("[NativeCameraWindows] Stopping frame reader...");
                await _frameReader.StopAsync();

                Debug.WriteLine("[NativeCameraWindows] Starting frame reader...");
                var result = await _frameReader.StartAsync();
                Debug.WriteLine($"[NativeCameraWindows] Frame reader restart result: {result}");

                if (result == MediaFrameReaderStartStatus.Success)
                {
                    Debug.WriteLine("[NativeCameraWindows] Frame reader restarted successfully - preview should resume");
                }
                else
                {
                    Debug.WriteLine($"[NativeCameraWindows] Failed to restart frame reader: {result}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] RestartFrameReaderAsync error: {e}");
        }
    }

    public async Task<string> SaveJpgStreamToGallery(Stream stream, string filename,
        double rotation, Metadata meta, string album)
    {
        try
        {
            var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            var saveFolder = picturesLibrary.SaveFolder;

            // Create album subfolder if specified, similar to Android/iOS behavior
            if (!string.IsNullOrEmpty(album))
            {
                Debug.WriteLine($"[NativeCameraWindows] Creating album folder: {album}");
                saveFolder = await saveFolder.CreateFolderAsync(album, CreationCollisionOption.OpenIfExists);
            }
            else
            {
                // Default to "Camera" folder like Android does when no album specified
                Debug.WriteLine("[NativeCameraWindows] Using default Camera folder");
                saveFolder = await saveFolder.CreateFolderAsync("Camera", CreationCollisionOption.OpenIfExists);
            }

            var file = await saveFolder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);
            Debug.WriteLine($"[NativeCameraWindows] Saving to: {file.Path}");

            using var fileStream = await file.OpenStreamForWriteAsync();
            await stream.CopyToAsync(fileStream);

            Debug.WriteLine($"[NativeCameraWindows] Successfully saved image to: {file.Path}");
            return file.Path;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] SaveJpgStreamToGallery error: {e}");
            return null;
        }
    }


    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            Stop();

            _frameReader?.Dispose();
            _mediaCapture?.Dispose();
            _frameSemaphore?.Dispose();

            lock (_lockPreview)
            {
                _preview?.Dispose();
                _preview = null;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] Dispose error: {e}");
        }
    }

    #endregion
}
