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

[StructLayout(LayoutKind.Sequential)]
struct D3D11_MAPPED_SUBRESOURCE
{
    public IntPtr pData;
    public uint RowPitch;
    public uint DepthPitch;
}

[ComImport]
[Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ID3D11Texture2D : ID3D11Resource
{
    // ID3D11Resource methods
    new void GetDevice(out ID3D11Device ppDevice);
    new void GetPrivateData(ref Guid guid, ref uint pDataSize, IntPtr pData);
    new void SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    new void SetPrivateDataInterface(ref Guid guid, [MarshalAs(UnmanagedType.IUnknown)] object pData);
    new void GetType(out int pResourceDimension);
    new void SetEvictionPriority(uint EvictionPriority);
    new uint GetEvictionPriority();

    // ID3D11Texture2D methods
    void GetDesc(out D3D11_TEXTURE2D_DESC pDesc);
}

[ComImport]
[Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ID3D11Resource : ID3D11DeviceChild
{
    new void GetDevice(out ID3D11Device ppDevice);
    new void GetPrivateData(ref Guid guid, ref uint pDataSize, IntPtr pData);
    new void SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    new void SetPrivateDataInterface(ref Guid guid, [MarshalAs(UnmanagedType.IUnknown)] object pData);
    void GetType(out int pResourceDimension);
    void SetEvictionPriority(uint EvictionPriority);
    uint GetEvictionPriority();
}

[ComImport]
[Guid("1841e5c8-16b0-489b-bcc8-44cfb0d5deae")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ID3D11DeviceChild
{
    void GetDevice(out ID3D11Device ppDevice);
    void GetPrivateData(ref Guid guid, ref uint pDataSize, IntPtr pData);
    void SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid guid, [MarshalAs(UnmanagedType.IUnknown)] object pData);
}

[ComImport]
[Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ID3D11Device
{
    void CreateBuffer(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppBuffer);
    void CreateTexture1D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture1D);
    void CreateTexture2D(ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out ID3D11Texture2D ppTexture2D);
    void CreateTexture3D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture3D);
    void CreateShaderResourceView(ID3D11Resource pResource, IntPtr pDesc, out IntPtr ppSRView);
    void CreateUnorderedAccessView(ID3D11Resource pResource, IntPtr pDesc, out IntPtr ppUAView);
    void CreateRenderTargetView(ID3D11Resource pResource, IntPtr pDesc, out IntPtr ppRTView);
    void CreateDepthStencilView(ID3D11Resource pResource, IntPtr pDesc, out IntPtr ppDSView);
    void CreateInputLayout(IntPtr pInputElementDescs, uint NumElements, IntPtr pShaderBytecodeWithInputSignature, IntPtr BytecodeLength, out IntPtr ppInputLayout);
    void CreateVertexShader(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppVertexShader);
    void CreateGeometryShader(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppGeometryShader);
    void CreateGeometryShaderWithStreamOutput(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pSODeclaration, uint NumEntries, IntPtr pBufferStrides, uint NumStrides, uint RasterizedStream, IntPtr pClassLinkage, out IntPtr ppGeometryShader);
    void CreatePixelShader(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppPixelShader);
    void CreateHullShader(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppHullShader);
    void CreateDomainShader(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppDomainShader);
    void CreateComputeShader(IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppComputeShader);
    void CreateClassLinkage(out IntPtr ppLinkage);
    void CreateBlendState(IntPtr pBlendStateDesc, out IntPtr ppBlendState);
    void CreateDepthStencilState(IntPtr pDepthStencilStateDesc, out IntPtr ppDepthStencilState);
    void CreateRasterizerState(IntPtr pRasterizerDesc, out IntPtr ppRasterizerState);
    void CreateSamplerState(IntPtr pSamplerDesc, out IntPtr ppSamplerState);
    void CreateQuery(IntPtr pQueryDesc, out IntPtr ppQuery);
    void CreatePredicate(IntPtr pPredicateDesc, out IntPtr ppPredicate);
    void CreateCounter(IntPtr pCounterDesc, out IntPtr ppCounter);
    void CreateDeferredContext(uint ContextFlags, out ID3D11DeviceContext ppDeferredContext);
    void OpenSharedResource(IntPtr hResource, ref Guid ReturnedInterface, out IntPtr ppResource);
    void CheckFormatSupport(uint Format, out uint pFormatSupport);
    void CheckMultisampleQualityLevels(uint Format, uint SampleCount, out uint pNumQualityLevels);
    void CheckCounterInfo(out IntPtr pCounterInfo);
    void CheckCounter(IntPtr pDesc, out int pType, out int pActiveCounters, out IntPtr szName, out uint pNameLength, out IntPtr szUnits, out uint pUnitsLength, out IntPtr szDescription, out uint pDescriptionLength);
    void CheckFeatureSupport(int Feature, IntPtr pFeatureSupportData, uint FeatureSupportDataSize);
    void GetPrivateData(ref Guid guid, ref uint pDataSize, IntPtr pData);
    void SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid guid, [MarshalAs(UnmanagedType.IUnknown)] object pData);
    void GetFeatureLevel(out int pFeatureLevel);
    void GetCreationFlags(out uint pFlags);
    void GetDeviceRemovedReason(out int pReason);
    void GetImmediateContext(out ID3D11DeviceContext ppImmediateContext);
    void SetExceptionMode(uint RaiseFlags);
    void GetExceptionMode(out uint pRaiseFlags);
}

[ComImport]
[Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ID3D11DeviceContext : ID3D11DeviceChild
{
    // ID3D11DeviceChild methods
    new void GetDevice(out ID3D11Device ppDevice);
    new void GetPrivateData(ref Guid guid, ref uint pDataSize, IntPtr pData);
    new void SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    new void SetPrivateDataInterface(ref Guid guid, [MarshalAs(UnmanagedType.IUnknown)] object pData);

    // ID3D11DeviceContext methods
    void VSSetConstantBuffers(uint StartSlot, uint NumBuffers, IntPtr ppConstantBuffers);
    void PSSetShaderResources(uint StartSlot, uint NumViews, IntPtr ppShaderResourceViews);
    void PSSetShader(IntPtr pPixelShader, IntPtr ppClassInstances, uint NumClassInstances);
    void PSSetSamplers(uint StartSlot, uint NumSamplers, IntPtr ppSamplers);
    void VSSetShader(IntPtr pVertexShader, IntPtr ppClassInstances, uint NumClassInstances);
    void DrawIndexed(uint IndexCount, uint StartIndexLocation, int BaseVertexLocation);
    void Draw(uint VertexCount, uint StartVertexLocation);
    void Map(ID3D11Resource pResource, uint Subresource, uint MapType, uint MapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);
    void Unmap(ID3D11Resource pResource, uint Subresource);
    void PSSetConstantBuffers(uint StartSlot, uint NumBuffers, IntPtr ppConstantBuffers);
    void IASetInputLayout(IntPtr pInputLayout);
    void IASetVertexBuffers(uint StartSlot, uint NumBuffers, IntPtr ppVertexBuffers, IntPtr pStrides, IntPtr pOffsets);
    void IASetIndexBuffer(IntPtr pIndexBuffer, uint Format, uint Offset);
    void DrawIndexedInstanced(uint IndexCountPerInstance, uint InstanceCount, uint StartIndexLocation, int BaseVertexLocation, uint StartInstanceLocation);
    void DrawInstanced(uint VertexCountPerInstance, uint InstanceCount, uint StartVertexLocation, uint StartInstanceLocation);
    void GSSetConstantBuffers(uint StartSlot, uint NumBuffers, IntPtr ppConstantBuffers);
    void GSSetShader(IntPtr pShader, IntPtr ppClassInstances, uint NumClassInstances);
    void IASetPrimitiveTopology(uint Topology);
    void VSSetShaderResources(uint StartSlot, uint NumViews, IntPtr ppShaderResourceViews);
    void VSSetSamplers(uint StartSlot, uint NumSamplers, IntPtr ppSamplers);
    void Begin(IntPtr pAsync);
    void End(IntPtr pAsync);
    void GetData(IntPtr pAsync, IntPtr pData, uint DataSize, uint GetDataFlags);
    void SetPredication(IntPtr pPredicate, int PredicateValue);
    void GSSetShaderResources(uint StartSlot, uint NumViews, IntPtr ppShaderResourceViews);
    void GSSetSamplers(uint StartSlot, uint NumSamplers, IntPtr ppSamplers);
    void OMSetRenderTargets(uint NumViews, IntPtr ppRenderTargetViews, IntPtr pDepthStencilView);
    void OMSetRenderTargetsAndUnorderedAccessViews(uint NumRTVs, IntPtr ppRenderTargetViews, IntPtr pDepthStencilView, uint UAVStartSlot, uint NumUAVs, IntPtr ppUnorderedAccessViews, IntPtr pUAVInitialCounts);
    void OMSetBlendState(IntPtr pBlendState, float[] BlendFactor, uint SampleMask);
    void OMSetDepthStencilState(IntPtr pDepthStencilState, uint StencilRef);
    void SOSetTargets(uint NumBuffers, IntPtr ppSOTargets, IntPtr pOffsets);
    void DrawAuto();
    void DrawIndexedInstancedIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);
    void DrawInstancedIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);
    void Dispatch(uint ThreadGroupCountX, uint ThreadGroupCountY, uint ThreadGroupCountZ);
    void DispatchIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);
    void RSSetState(IntPtr pRasterizerState);
    void RSSetViewports(uint NumViewports, IntPtr pViewports);
    void RSSetScissorRects(uint NumRects, IntPtr pRects);
    void CopySubresourceRegion(ID3D11Resource pDstResource, uint DstSubresource, uint DstX, uint DstY, uint DstZ, ID3D11Resource pSrcResource, uint SrcSubresource, IntPtr pSrcBox);
    void CopyResource(ID3D11Resource pDstResource, ID3D11Resource pSrcResource);
    // Truncated for brevity, but needed
    void UpdateSubresource(ID3D11Resource pDstResource, uint DstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint SrcRowPitch, uint SrcDepthPitch);
}

[StructLayout(LayoutKind.Sequential)]
struct D3D11_TEXTURE2D_DESC
{
    public uint Width;
    public uint Height;
    public uint MipLevels;
    public uint ArraySize;
    public uint Format;
    public DXGI_SAMPLE_DESC SampleDesc;
    public uint Usage;
    public uint BindFlags;
    public uint CPUAccessFlags;
    public uint MiscFlags;
}

// Enums
enum D3D11_USAGE : uint { DEFAULT = 0, IMMUTABLE = 1, DYNAMIC = 2, STAGING = 3 }
enum D3D11_BIND_FLAG : uint { VERTEX_BUFFER = 1, INDEX_BUFFER = 2, CONSTANT_BUFFER = 4, SHADER_RESOURCE = 8, STREAM_OUTPUT = 16, RENDER_TARGET = 32, DEPTH_STENCIL = 64, UNORDERED_ACCESS = 128, DECODER = 512, VIDEO_ENCODER = 1024 }
enum D3D11_CPU_ACCESS_FLAG : uint { WRITE = 65536, READ = 131072 }
enum D3D11_MAP : uint { READ = 1, WRITE = 2, READ_WRITE = 3, WRITE_DISCARD = 4, WRITE_NO_OVERWRITE = 5 }

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
    // Global lock to prevent overlapping camera sessions on Windows
    private static readonly SemaphoreSlim _cameraLock = new(1, 1);
    private bool _hasLock = false;

    protected readonly SkiaCamera FormsControl;
    private MediaCapture _mediaCapture;
    private MediaFrameReader _frameReader;
    private CameraProcessorState _state = CameraProcessorState.None;
    private bool _flashSupported;
    private bool _isCapturingStill;
    private bool _isRecordingVideo;
    private StorageFile _currentVideoFile;
    private DateTime _recordingStartTime;
    private System.Threading.Timer _progressTimer;
    private double _zoomScale = 1.0;
    private readonly object _lockPreview = new();
    private volatile CapturedImage _preview;
    private DeviceInformation _cameraDevice;
    private MediaFrameSource _frameSource;
    FlashMode _flashMode = FlashMode.Off;
    CaptureFlashMode _captureFlashMode = CaptureFlashMode.Auto;

    public int PreviewWidth { get; private set; }
    public int PreviewHeight { get; private set; }

    private readonly SemaphoreSlim _frameSemaphore = new(1, 1);
    private volatile bool _isProcessingFrame = false;

    // Raw frame arrival diagnostics (counts ALL frames before filtering)
    private long _rawFrameCount = 0;
    private long _rawFrameLastReportTime = 0;
    private double _rawFrameFps = 0;

    // Pre-recording buffer fields
    private bool _enablePreRecording;
    private TimeSpan _preRecordDuration = TimeSpan.FromSeconds(5);
    private readonly object _preRecordingLock = new();
    private Queue<Direct3DFrameData> _preRecordingBuffer;
    private int _maxPreRecordingFrames = 0;

    /// <summary>
    /// Represents a Direct3D frame with timestamp for pre-recording buffer
    /// </summary>
    private class Direct3DFrameData : IDisposable
    {
        public byte[] Data { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime Timestamp { get; set; }

        public void Dispose()
        {
            // Frame data will be managed by GC
        }
    }

    // Reused buffers for managed fallback conversion (apply capture-path trick: single reusable buffers)
    private InMemoryRandomAccessStream _scratchPreviewStream;
    private byte[] _scratchPreviewBytes;

    public NativeCamera(SkiaCamera formsControl)
    {
        FormsControl = formsControl;
        //Setup();
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

    /// <summary>
    /// Raw camera frame delivery rate (all frames before any filtering/processing)
    /// </summary>
    public double RawCameraFps => _rawFrameFps;

    /// <summary>
    /// Gets or sets whether pre-recording is enabled.
    /// </summary>
    public bool EnablePreRecording
    {
        get => _enablePreRecording;
        set
        {
            if (_enablePreRecording != value)
            {
                _enablePreRecording = value;
                if (value)
                {
                    InitializePreRecordingBuffer();
                }
                else
                {
                    ClearPreRecordingBuffer();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the duration of the pre-recording buffer.
    /// </summary>
    public TimeSpan PreRecordDuration
    {
        get => _preRecordDuration;
        set
        {
            if (_preRecordDuration != value)
            {
                _preRecordDuration = value;
                CalculateMaxPreRecordingFrames();
            }
        }
    }

    #endregion

    #region Setup

    private async void Setup()
    {
        try
        {
            //Debug.WriteLine("[NativeCameraWindows] Starting setup...");
            await SetupHardware();
            //Debug.WriteLine("[NativeCameraWindows] Hardware setup completed successfully");
            //State = CameraProcessorState.Enabled;
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

        Debug.WriteLine($"[NativeCameraWindows] CURRENT SELECTION CONFIG: Facing={FormsControl.Facing}, CameraIndex={FormsControl.CameraIndex}");

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

        Debug.WriteLine($"[NativeCameraWindows] *** FINAL SELECTED CAMERA: {_cameraDevice.Name} (ID: {_cameraDevice.Id}) ***");

        Debug.WriteLine("[NativeCameraWindows] Initializing MediaCapture...");
        _mediaCapture = new MediaCapture();
        var settings = new MediaCaptureInitializationSettings
        {
            VideoDeviceId = _cameraDevice.Id,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            PhotoCaptureSource = PhotoCaptureSource.VideoPreview
        };

        Debug.WriteLine($"[NativeCameraWindows] *** INITIALIZING MEDIACAPTURE WITH VideoDeviceId: {_cameraDevice.Id} ({_cameraDevice.Name}) ***");



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

        // Get target aspect ratio based on capture mode
        int captureWidth, captureHeight;
        if (FormsControl.CaptureMode == CaptureModeType.Video)
        {
            var vf = GetCurrentVideoFormat();
            captureWidth = vf?.Width ?? 1280;
            captureHeight = vf?.Height ?? 720;
        }
        else
        {
            var (w, h) = GetBestCaptureResolution();
            captureWidth = (int)w;
            captureHeight = (int)h;
        }
        double targetAspectRatio = (double)captureWidth / captureHeight;

        Debug.WriteLine($"[NativeCameraWindows] Target capture resolution: {captureWidth}x{captureHeight} (AR: {targetAspectRatio:F2})");

        var preferredFormat = ChooseOptimalPreviewFormat(_frameSource.SupportedFormats, targetAspectRatio);

        if (preferredFormat != null)
        {
            var fps = preferredFormat.FrameRate.Numerator / (double)preferredFormat.FrameRate.Denominator;
            Debug.WriteLine($"[NativeCameraWindows] Setting preview format: {preferredFormat.VideoFormat.Width}x{preferredFormat.VideoFormat.Height} @ {fps:F1} FPS (AR: {(double)preferredFormat.VideoFormat.Width / preferredFormat.VideoFormat.Height:F2})");
            await _frameSource.SetFormatAsync(preferredFormat);

            PreviewWidth = (int)preferredFormat.VideoFormat.Width;
            PreviewHeight = (int)preferredFormat.VideoFormat.Height;
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
    /// Create optimized SKImage directly from Direct3D surface using Staging texture if needed.
    /// This avoids the overhead of SoftwareBitmap wrapper.
    /// </summary>
    private SKImage ConvertDirect3DToOptimizedSKImage(Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface d3dSurface)
    {
        ID3D11Texture2D texture = null;
        ID3D11Device device = null;
        ID3D11DeviceContext context = null;
        ID3D11Texture2D stagingTexture = null;

        try
        {
            // Get DXGI Interface Access
            var access = d3dSurface as IDirect3DDxgiInterfaceAccess;
            if (access == null) return null;

            var textureGuid = typeof(ID3D11Texture2D).GUID;
            var texturePtr = access.GetInterface(ref textureGuid);
            if (texturePtr == IntPtr.Zero) return null;

            texture = Marshal.GetObjectForIUnknown(texturePtr) as ID3D11Texture2D;
            if (texture == null) return null;

            texture.GetDesc(out D3D11_TEXTURE2D_DESC desc);
            texture.GetDevice(out device);
            device.GetImmediateContext(out context);

            D3D11_MAPPED_SUBRESOURCE mapped;
            bool useStaging = true;

            // Checks if we can map directly (optimization)
            if (desc.Usage == (uint)D3D11_USAGE.STAGING && (desc.CPUAccessFlags & (uint)D3D11_CPU_ACCESS_FLAG.READ) != 0)
            {
                useStaging = false;
            }

            ID3D11Resource resourceToMap = null;

            if (useStaging)
            {
                // Create Staging
                var stagingDesc = desc;
                stagingDesc.Usage = (uint)D3D11_USAGE.STAGING;
                stagingDesc.BindFlags = 0;
                stagingDesc.CPUAccessFlags = (uint)D3D11_CPU_ACCESS_FLAG.READ;
                stagingDesc.MiscFlags = 0; // Clear any shared flags

                device.CreateTexture2D(ref stagingDesc, IntPtr.Zero, out stagingTexture);

                context.CopyResource((ID3D11Resource)stagingTexture, (ID3D11Resource)texture);
                resourceToMap = (ID3D11Resource)stagingTexture;
            }
            else
            {
                resourceToMap = (ID3D11Resource)texture;
            }

            context.Map(resourceToMap, 0, (uint)D3D11_MAP.READ, 0, out mapped);

            try
            {
                // Create SKImage from mapped memory
                // We use SKImage.FromPixels which copies the data unless we use a ReleaseProc, but we need to Unmap immediately so copy is safer/easier.
                // This is still faster than SoftwareBitmap intermediate.
                var info = new SKImageInfo((int)desc.Width, (int)desc.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var skImage = SKImage.FromPixels(info, mapped.pData, (int)mapped.RowPitch);
                return skImage;
            }
            finally
            {
                context.Unmap(resourceToMap, 0);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ConvertDirect3DToOptimizedSKImage error: {e}");
            return null;
        }
        finally
        {
            if (stagingTexture != null) Marshal.ReleaseComObject(stagingTexture);
            if (context != null) Marshal.ReleaseComObject(context);
            if (device != null) Marshal.ReleaseComObject(device);
            if (texture != null) Marshal.ReleaseComObject(texture);
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
            // PRIORITY 1: Try highly optimized Staging Texture Map (1 copy)
            // This bypasses the SoftwareBitmap wrapper overhead and double buffering
            var skImage = ConvertDirect3DToOptimizedSKImage(d3dSurface);

            if (skImage == null)
            {
                // PRIORITY 2: Fallback to SoftwareBitmap (2 copies)
                // GPU Copy (Surface->SoftBitmap) -> CPU Copy (SoftBitmap->Skia)
                var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(d3dSurface);
                if (softwareBitmap != null)
                {
                    skImage = await ConvertToSKImageDirectAsync(softwareBitmap);
                    softwareBitmap.Dispose();
                }
            }

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

                // Invoke capture callback for encoder (same pattern as Android)
                PreviewCaptureSuccess?.Invoke(capturedImage);

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
                //Debug.WriteLine($"[NativeCameraWindows] RAW camera FPS: {_rawFrameFps:F1} (frames: {_rawFrameCount} in {elapsedSeconds:F2}s)");
                _rawFrameCount = 0;
                _rawFrameLastReportTime = now;
            }
        }

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
                // This is the fastest path for Preview, bypassing SoftwareBitmap overhead
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
            // Handle pre-recording buffer when enabled and not currently recording
            if (_enablePreRecording && !_isRecordingVideo)
            {
                BufferPreRecordingFrameFromBitmap(softwareBitmap);
            }

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

                // Invoke capture callback for encoder (same pattern as Android)
                PreviewCaptureSuccess?.Invoke(capturedImage);

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
    /// Fallback conversion using BMP encoding with reusable buffers (minimize per-frame allocations)
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

            // Reuse a single in-memory stream across frames
            _scratchPreviewStream ??= new InMemoryRandomAccessStream();
            _scratchPreviewStream.Seek(0);
            _scratchPreviewStream.Size = 0;

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, _scratchPreviewStream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            encoder.BitmapTransform.ScaledWidth = (uint)softwareBitmap.PixelWidth;
            encoder.BitmapTransform.ScaledHeight = (uint)softwareBitmap.PixelHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.NearestNeighbor;
            await encoder.FlushAsync();

            var size = (int)_scratchPreviewStream.Size;
            if (_scratchPreviewBytes == null || _scratchPreviewBytes.Length < size)
            {
                _scratchPreviewBytes = new byte[size];
            }

            // Read into the reusable byte[] buffer
            _scratchPreviewStream.Seek(0);
            await _scratchPreviewStream.ReadAsync(_scratchPreviewBytes.AsBuffer(0, size), (uint)size, InputStreamOptions.None);

            return SKImage.FromEncodedData(_scratchPreviewBytes);
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
        // Acquire global lock to ensure previous camera is fully stopped
        if (!_hasLock)
        {
            if (await _cameraLock.WaitAsync(5000))
            {
                _hasLock = true;
            }
            else
            {
                Debug.WriteLine("[NativeCameraWindows] FAILED to acquire camera lock - potential resource conflict");
                return; // Abort start if lock cannot be acquired
            }
        }

        try
        {
            Setup();

            if (State == CameraProcessorState.Enabled && _frameReader != null)
            {
                //Debug.WriteLine("[NativeCameraWindows] Camera already started");
                return;
            }

            await StartFrameReaderAsync();

            // Apply current flash modes after camera starts
            if (State == CameraProcessorState.Enabled)
            {
                ApplyFlashMode();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] Start error: {e}");
            // Release lock if start failed
            if (_hasLock)
            {
                _cameraLock.Release();
                _hasLock = false;
            }
        }
    }

    public async void Stop(bool force = false)
    {
        // Only return early if we definitely don't need to do anything AND we don't hold the lock
        if (!_hasLock && State == CameraProcessorState.None && !force)
            return;

        if (!_hasLock && State != CameraProcessorState.Enabled && !force)
            return; //avoid spam

        try
        {
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
        finally
        {
            if (_hasLock)
            {
                _cameraLock.Release();
                _hasLock = false;
            }
        }
    }

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
        if (!_flashSupported || _mediaCapture == null)
            return;

        try
        {
            var flashControl = _mediaCapture.VideoDeviceController.FlashControl;

            switch (_flashMode)
            {
                case FlashMode.Off:
                    flashControl.Enabled = false;
                    break;
                case FlashMode.On:
                    flashControl.Enabled = true;
                    flashControl.Auto = false;
                    break;
                case FlashMode.Strobe:
                    // Future implementation for strobe mode
                    // For now, treat as On
                    flashControl.Enabled = true;
                    flashControl.Auto = false;
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] ApplyFlashMode error: {e}");
        }
    }

    public void SetCaptureFlashMode(CaptureFlashMode mode)
    {
        _captureFlashMode = mode;
    }

    public CaptureFlashMode GetCaptureFlashMode()
    {
        return _captureFlashMode;
    }

    public bool IsFlashSupported()
    {
        return _flashSupported;
    }

    public bool IsAutoFlashSupported()
    {
        return _flashSupported; // Windows supports auto flash when flash is available
    }

    private void SetFlashModeForCapture()
    {
        if (!_flashSupported || _mediaCapture == null)
            return;

        try
        {
            var flashControl = _mediaCapture.VideoDeviceController.FlashControl;

            switch (_captureFlashMode)
            {
                case CaptureFlashMode.Off:
                    flashControl.Enabled = false;
                    flashControl.Auto = false;
                    break;
                case CaptureFlashMode.Auto:
                    flashControl.Enabled = true;
                    flashControl.Auto = true;
                    break;
                case CaptureFlashMode.On:
                    flashControl.Enabled = true;
                    flashControl.Auto = false;
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] SetFlashModeForCapture error: {e}");
        }
    }

    private (uint width, uint height) GetBestCaptureResolution()
    {
        try
        {
            if (_frameSource?.SupportedFormats == null || !_frameSource.SupportedFormats.Any())
            {
                Debug.WriteLine("[NativeCameraWindows] No supported formats available, using fallback resolution");
                return (1920, 1080); // Fallback to original hardcoded values
            }

            // Get all available resolutions sorted by total pixels (descending)
            var availableResolutions = _frameSource.SupportedFormats
                .Select(format => new
                {
                    Width = format.VideoFormat.Width,
                    Height = format.VideoFormat.Height,
                    TotalPixels = format.VideoFormat.Width * format.VideoFormat.Height,
                    AspectRatio = (double)format.VideoFormat.Width / format.VideoFormat.Height
                })
                .Distinct()
                .OrderByDescending(r => r.TotalPixels)
                .ToList();

            Debug.WriteLine($"[NativeCameraWindows] Available resolutions:");
            foreach (var res in availableResolutions)
            {
                Debug.WriteLine($"  {res.Width}x{res.Height} ({res.TotalPixels:N0} pixels, AR: {res.AspectRatio:F2})");
            }

            // Select resolution based on PhotoQuality setting
            var selectedResolution = FormsControl.PhotoQuality switch
            {
                CaptureQuality.Max => availableResolutions.First(), // Highest resolution
                CaptureQuality.High => availableResolutions.Skip(availableResolutions.Count / 5).First(), // ~20% down the list
                CaptureQuality.Medium => availableResolutions.Skip(availableResolutions.Count / 3).First(), // ~66% down the list
                CaptureQuality.Low => availableResolutions.Skip(2 * availableResolutions.Count / 3).First(), // ~33% down the list
                CaptureQuality.Preview => availableResolutions.LastOrDefault(r => r.Width >= 640 && r.Height >= 480)
                                         ?? availableResolutions.Last(), // Smallest usable resolution
                CaptureQuality.Manual => GetManualResolution(availableResolutions, FormsControl.PhotoFormatIndex),
                _ => availableResolutions.First()
            };

            Debug.WriteLine($"[NativeCameraWindows] Selected resolution for {FormsControl.PhotoQuality}: {selectedResolution.Width}x{selectedResolution.Height}");
            return (selectedResolution.Width, selectedResolution.Height);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[NativeCameraWindows] GetBestCaptureResolution error: {e}");
            return (1920, 1080); // Fallback to original hardcoded values
        }
    }

    private static dynamic GetManualResolution(IEnumerable<dynamic> availableResolutions, int formatIndex)
    {
        var resolutionsList = availableResolutions.ToList();

        if (formatIndex >= 0 && formatIndex < resolutionsList.Count)
        {
            Debug.WriteLine($"[NativeCameraWindows] Using manual format index {formatIndex}");
            return resolutionsList[formatIndex];
        }
        else
        {
            Debug.WriteLine($"[NativeCameraWindows] Invalid PhotoFormatIndex {formatIndex}, using Max quality");
            return resolutionsList.First();
        }
    }

    /// <summary>
    /// Choose optimal preview format that matches the target aspect ratio.
    /// IMPORTANT: Prioritizes HIGH FPS (≥24fps) and LOW RESOLUTION for smooth performance.
    /// </summary>
    private static Windows.Media.Capture.Frames.MediaFrameFormat ChooseOptimalPreviewFormat(
        IReadOnlyList<Windows.Media.Capture.Frames.MediaFrameFormat> availableFormats,
        double targetAspectRatio)
    {
        const double aspectRatioTolerance = 0.1; // 10% tolerance
        const int minWidth = 640;
        const int minHeight = 480;
        const int maxPreviewWidth = 1920;  // Cap preview resolution for performance
        const int maxPreviewHeight = 1080;
        const double minFps = 24.0;        // Minimum acceptable FPS for smooth preview
        const double preferredFps = 30.0;  // Preferred FPS

        // First, filter formats that are suitable for preview with good FPS
        var suitableFormats = new List<(Windows.Media.Capture.Frames.MediaFrameFormat format, double aspectRatioDiff, int totalPixels, double fps)>();

        foreach (var format in availableFormats)
        {
            var width = format.VideoFormat.Width;
            var height = format.VideoFormat.Height;
            var fps = format.FrameRate.Numerator / (double)format.FrameRate.Denominator;

            // Skip formats that are too small, too large, or have terrible FPS
            if (width < minWidth || height < minHeight ||
                width > maxPreviewWidth || height > maxPreviewHeight ||
                fps < minFps)
                continue;

            double aspectRatio = (double)width / height;
            double aspectRatioDiff = Math.Abs(targetAspectRatio - aspectRatio);
            double normalizedDiff = aspectRatioDiff / targetAspectRatio;

            // Only consider formats within aspect ratio tolerance
            if (normalizedDiff <= aspectRatioTolerance)
            {
                suitableFormats.Add((format, aspectRatioDiff, (int)(width * height), fps));
            }
        }

        Windows.Media.Capture.Frames.MediaFrameFormat bestMatch = null;

        if (suitableFormats.Any())
        {
            // Priority: 1) Good aspect ratio, 2) High FPS (≥30), 3) Small resolution
            bestMatch = suitableFormats
                .OrderBy(f => f.aspectRatioDiff)                           // Best aspect ratio match first
                .ThenByDescending(f => f.fps >= preferredFps ? 1 : 0)      // Prefer ≥30 FPS
                .ThenByDescending(f => f.fps)                              // Then highest FPS
                .ThenBy(f => f.totalPixels)                                // Then smallest resolution
                .First().format;

            var selectedInfo = suitableFormats.First(f => f.format == bestMatch);
            Debug.WriteLine($"[NativeCameraWindows] Selected SMOOTH preview: {bestMatch.VideoFormat.Width}x{bestMatch.VideoFormat.Height} @ {selectedInfo.fps:F1} FPS (AR diff: {selectedInfo.aspectRatioDiff:F4})");
        }
        else
        {
            // Fallback: Find format with good FPS, ignore aspect ratio if needed
            bestMatch = availableFormats
                .Where(f => f.VideoFormat.Width >= minWidth && f.VideoFormat.Height >= minHeight &&
                           f.VideoFormat.Width <= maxPreviewWidth && f.VideoFormat.Height <= maxPreviewHeight)
                .Where(f => f.FrameRate.Numerator / (double)f.FrameRate.Denominator >= minFps)
                .OrderByDescending(f => f.FrameRate.Numerator / (double)f.FrameRate.Denominator)  // Highest FPS first
                .ThenBy(f => f.VideoFormat.Width * f.VideoFormat.Height)                          // Then smallest resolution
                .FirstOrDefault();

            if (bestMatch != null)
            {
                var fps = bestMatch.FrameRate.Numerator / (double)bestMatch.FrameRate.Denominator;
                Debug.WriteLine($"[NativeCameraWindows] Fallback to SMOOTH format (ignoring AR): {bestMatch.VideoFormat.Width}x{bestMatch.VideoFormat.Height} @ {fps:F1} FPS");
            }
        }

        return bestMatch;
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

    /// <summary>
    /// Updates preview format to match current capture format aspect ratio
    /// </summary>
    public async Task UpdatePreviewFormatAsync()
    {
        try
        {
            if (_frameSource?.SupportedFormats == null)
            {
                Debug.WriteLine("[NativeCameraWindows] No frame source available for preview format update");
                return;
            }

            // Get target aspect ratio based on capture mode (photo vs. video)
            int captureWidth, captureHeight;
            if (FormsControl.CaptureMode == CaptureModeType.Video)
            {
                var vf = GetCurrentVideoFormat();
                captureWidth = vf?.Width ?? 1280;
                captureHeight = vf?.Height ?? 720;
            }
            else
            {
                var (w, h) = GetBestCaptureResolution();
                captureWidth = (int)w;
                captureHeight = (int)h;
            }
            double targetAspectRatio = (double)captureWidth / captureHeight;

            Debug.WriteLine($"[NativeCameraWindows] Updating preview format to match capture AR: {targetAspectRatio:F2} ({captureWidth}x{captureHeight})");

            // Find optimal preview format
            var newPreviewFormat = ChooseOptimalPreviewFormat(_frameSource.SupportedFormats, targetAspectRatio);

            if (newPreviewFormat != null)
            {
                // Stop frame reader
                if (_frameReader != null)
                {
                    await _frameReader.StopAsync();
                }

                // Set new format
                await _frameSource.SetFormatAsync(newPreviewFormat);

                PreviewWidth = (int)newPreviewFormat.VideoFormat.Width;
                PreviewHeight = (int)newPreviewFormat.VideoFormat.Height;

                var fps = newPreviewFormat.FrameRate.Numerator / (double)newPreviewFormat.FrameRate.Denominator;
                Debug.WriteLine($"[NativeCameraWindows] Updated preview format: {newPreviewFormat.VideoFormat.Width}x{newPreviewFormat.VideoFormat.Height} @ {fps:F1} FPS");

                // Restart frame reader
                if (_frameReader != null)
                {
                    var result = await _frameReader.StartAsync();
                    Debug.WriteLine($"[NativeCameraWindows] Frame reader restart result: {result}");
                }
            }
            else
            {
                Debug.WriteLine("[NativeCameraWindows] No suitable preview format found for aspect ratio update");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraWindows] Error updating preview format: {ex}");
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
    /// Gets the currently selected capture format
    /// </summary>
    /// <returns>Current capture format or null if not available</returns>
    public CaptureFormat GetCurrentCaptureFormat()
    {
        try
        {
            // For Windows, we need to get the current capture resolution that would be used
            // This is determined by the PhotoQuality and PhotoFormatIndex settings
            var (width, height) = GetBestCaptureResolution();

            if (width > 0 && height > 0)
            {
                return new CaptureFormat
                {
                    Width = (int)width,
                    Height = (int)height,
                    FormatId = $"windows_{_cameraDevice?.Id}_{width}x{height}"
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraWindows] GetCurrentCaptureFormat error: {ex.Message}");
        }

        return null;
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

            // Set flash mode for capture
            SetFlashModeForCapture();

            // Create image encoding properties using camera's actual capabilities
            var imageProperties = ImageEncodingProperties.CreateJpeg();

            // Get the best available resolution from camera capabilities
            var (width, height) = GetBestCaptureResolution();
            imageProperties.Width = width;
            imageProperties.Height = height;

            Debug.WriteLine($"[NativeCameraWindows] Using capture resolution: {width}x{height}");

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

    public async Task<string> SaveJpgStreamToGallery(Stream stream, string filename, Metadata meta, string album)
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

    #region VIDEO RECORDING

    /// <summary>
    /// Gets the currently selected video format based on user settings.
    /// CRITICAL: Returns format extracted from GetVideoEncodingProfile() to ensure
    /// consistency - this returns EXACTLY what the encoder will use.
    /// This matches iOS/Android pattern (they extract from their encoding profiles).
    /// </summary>
    /// <returns>Current video format or null if not available</returns>
    public VideoFormat GetCurrentVideoFormat()
    {
        try
        {
            // Get the MediaEncodingProfile that the encoder will actually use
            // This handles all quality modes (Low, Standard, High, Ultra, Manual)
            var profile = GetVideoEncodingProfile();

            if (profile?.Video != null)
            {
                var w = (int)profile.Video.Width;
                var h = (int)profile.Video.Height;
                var fps = (int)(profile.Video.FrameRate.Numerator / Math.Max(1, profile.Video.FrameRate.Denominator));
                var bitrate = (int)profile.Video.Bitrate;

                //Debug.WriteLine($"[NativeCameraWindows] GetCurrentVideoFormat: Extracted from encoding profile -> {w}x{h}@{fps}fps, {bitrate / 1_000_000}Mbps");

                return new VideoFormat
                {
                    Width = w,
                    Height = h,
                    FrameRate = Math.Max(1, fps),
                    Codec = "H.264",
                    BitRate = bitrate,
                    FormatId = $"{w}x{h}@{fps}"
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraWindows] GetCurrentVideoFormat error: {ex.Message}");
        }

        // Final fallback: use a safe default
        Debug.WriteLine($"[NativeCameraWindows] GetCurrentVideoFormat: Using fallback default 720p30");
        return new VideoFormat { Width = 1280, Height = 720, FrameRate = 30, Codec = "H.264", BitRate = 5_000_000, FormatId = "720p30" };
    }

    public void SetRecordAudio(bool recordAudio)
    {
   
    }

    /// <summary>
    /// Gets video formats from ACTUAL camera capabilities, not hardcoded values.
    /// Queries _frameSource.SupportedFormats to return what the hardware actually supports.
    /// </summary>
    public List<VideoFormat> GetPredefinedVideoFormats()
    {
        var formats = new List<VideoFormat>();

        try
        {
            if (_frameSource?.SupportedFormats != null)
            {
                // Extract unique resolutions from actual camera formats
                var uniqueFormats = _frameSource.SupportedFormats
                    .Select(f => new
                    {
                        Width = (int)f.VideoFormat.Width,
                        Height = (int)f.VideoFormat.Height,
                        FPS = (int)Math.Round(f.FrameRate.Numerator / (double)Math.Max(1, f.FrameRate.Denominator)),
                        Pixels = f.VideoFormat.Width * f.VideoFormat.Height
                    })
                    .GroupBy(f => new { f.Width, f.Height, f.FPS })
                    .Select(g => g.First())
                    .OrderByDescending(f => f.Pixels)
                    .ThenByDescending(f => f.FPS)
                    .ToList();

                foreach (var fmt in uniqueFormats)
                {
                    // Estimate bitrate based on resolution and framerate
                    int EstimateBitrate(int width, int height, int fps)
                    {
                        var pixelsPerSec = (long)width * height * fps;
                        var bps = (long)(pixelsPerSec * 0.07); // ~0.07 bits per pixel
                        if (bps < 2_000_000) bps = 2_000_000;   // Min 2 Mbps
                        if (bps > 35_000_000) bps = 35_000_000; // Max 35 Mbps
                        return (int)bps;
                    }

                    formats.Add(new VideoFormat
                    {
                        Width = fmt.Width,
                        Height = fmt.Height,
                        FrameRate = fmt.FPS,
                        Codec = "H.264",
                        BitRate = EstimateBitrate(fmt.Width, fmt.Height, fmt.FPS),
                        FormatId = $"{fmt.Width}x{fmt.Height}@{fmt.FPS}"
                    });
                }

                Debug.WriteLine($"[NativeCameraWindows] GetPredefinedVideoFormats: Found {formats.Count} actual formats from camera");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraWindows] GetPredefinedVideoFormats error: {ex.Message}");
        }

        // Fallback if no formats found: return safe defaults
        if (formats.Count == 0)
        {
            Debug.WriteLine("[NativeCameraWindows] GetPredefinedVideoFormats: No camera formats available, using fallback defaults");
            formats.AddRange(new[]
            {
                new VideoFormat { Width = 1920, Height = 1080, FrameRate = 30, Codec = "H.264", BitRate = 8_000_000, FormatId = "1080p30" },
                new VideoFormat { Width = 1280, Height = 720, FrameRate = 30, Codec = "H.264", BitRate = 5_000_000, FormatId = "720p30" },
                new VideoFormat { Width = 640, Height = 480, FrameRate = 30, Codec = "H.264", BitRate = 2_000_000, FormatId = "480p30" }
            });
        }

        return formats;
    }

    /// <summary>
    /// Gets the appropriate video encoding profile based on current video quality setting.
    /// For presets: picks best available format from camera instead of hardcoded resolutions.
    /// </summary>
    private MediaEncodingProfile GetVideoEncodingProfile()
    {
        var quality = FormsControl.VideoQuality;
        MediaEncodingProfile profile;

        if (quality == VideoQuality.Manual)
        {
            profile = GetManualVideoEncodingProfile();
        }
        else
        {
            // For preset modes: find best match from actual available formats
            var availableFormats = GetPredefinedVideoFormats();

            // Target resolutions for each quality level
            var targetResolution = quality switch
            {
                VideoQuality.Low => (width: 640, height: 480),        // 480p
                VideoQuality.Standard => (width: 1280, height: 720),  // 720p
                VideoQuality.High => (width: 1920, height: 1080),     // 1080p
                VideoQuality.Ultra => (width: 3840, height: 2160),    // 4K
                _ => (width: 1920, height: 1080)
            };

            // Target FPS
            int targetFps = 30;
            if (quality == VideoQuality.High || quality == VideoQuality.Ultra)
            {
                targetFps = 60;
            }

            // Find closest match from available formats
            var bestFormat = availableFormats
                .OrderBy(f => Math.Abs((f.Width * f.Height) - (targetResolution.width * targetResolution.height)))
                .ThenBy(f => Math.Abs(f.FrameRate - targetFps))
                .ThenByDescending(f => f.FrameRate)
                .FirstOrDefault();

            if (bestFormat != null)
            {
                // Start with a base profile and customize it
                profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
                profile.Video.Width = (uint)bestFormat.Width;
                profile.Video.Height = (uint)bestFormat.Height;
                profile.Video.FrameRate.Numerator = (uint)bestFormat.FrameRate;
                profile.Video.FrameRate.Denominator = 1;
                profile.Video.Bitrate = (uint)bestFormat.BitRate;

                Debug.WriteLine($"[NativeCamera.Windows] {quality} quality -> matched to {bestFormat.Width}x{bestFormat.Height}@{bestFormat.FrameRate}fps");
            }
            else
            {
                // Fallback to hardcoded preset if no formats available
                profile = quality switch
                {
                    VideoQuality.Low => MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga),
                    VideoQuality.Standard => MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p),
                    VideoQuality.Ultra => MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Uhd2160p),
                    _ => MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p)
                };

                Debug.WriteLine($"[NativeCamera.Windows] {quality} quality -> using fallback preset");
            }
        }

        // Remove audio if not recording audio
        if (!FormsControl.RecordAudio)
        {
            profile.Audio = null;
            //Debug.WriteLine("[NativeCamera.Windows] Audio disabled for video recording");
        }

        return profile;
    }

    /// <summary>
    /// Gets manual video encoding profile based on VideoFormatIndex
    /// </summary>
    private MediaEncodingProfile GetManualVideoEncodingProfile()
    {
        try
        {
            // For manual mode, create custom profile
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);

            // Get selected format if available (for now use predefined formats)
            var formats = GetPredefinedVideoFormats();
            if (formats.Count > FormsControl.VideoFormatIndex)
            {
                var selectedFormat = formats[FormsControl.VideoFormatIndex];

                // Customize video encoding properties
                profile.Video.Width = (uint)selectedFormat.Width;
                profile.Video.Height = (uint)selectedFormat.Height;
                profile.Video.FrameRate.Numerator = (uint)selectedFormat.FrameRate;
                profile.Video.FrameRate.Denominator = 1;
                profile.Video.Bitrate = (uint)selectedFormat.BitRate;
            }

            // Handle audio setting for manual profile too
            if (!FormsControl.RecordAudio)
            {
                profile.Audio = null;
            }

            return profile;
        }
        catch
        {
            // Fallback to standard quality
            var fallbackProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
            if (!FormsControl.RecordAudio)
            {
                fallbackProfile.Audio = null;
            }
            return fallbackProfile;
        }
    }

    /// <summary>
    /// Timer callback for video recording progress updates
    /// </summary>
    private void OnProgressTimer(object state)
    {
        if (!_isRecordingVideo)
            return;

        var elapsed = DateTime.Now - _recordingStartTime;
        VideoRecordingProgress?.Invoke(elapsed);
    }


    public async Task StartVideoRecording()
    {
        if (_isRecordingVideo || _mediaCapture == null)
            return;

        try
        {
            // Note: Windows MediaCapture does not support injecting pre-recorded frames
            // like AVAssetWriter on iOS. The pre-recording buffer is maintained for future
            // enhancement with custom encoding. For now, clear the buffer when recording starts
            // to prepare for fresh recording session.
            if (_enablePreRecording)
            {
                ClearPreRecordingBuffer();
                InitializePreRecordingBuffer();
            }

            // Create video encoding profile based on current video quality
            var profile = GetVideoEncodingProfile();

            // Create temp file for video recording in cache directory
            var fileName = $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            var cacheDir = FileSystem.Current.CacheDirectory;
            var filePath = Path.Combine(cacheDir, fileName);

            // Create or replace the file in the cache directory
            var cacheFolder = await StorageFolder.GetFolderFromPathAsync(cacheDir);
            _currentVideoFile = await cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            Debug.WriteLine($"[NativeCameraWindows] Starting video recording to: {_currentVideoFile.Path}");

            // Start recording to storage file
            await _mediaCapture.StartRecordToStorageFileAsync(profile, _currentVideoFile);

            _isRecordingVideo = true;
            _recordingStartTime = DateTime.Now;

            // Start progress timer (fire every second)
            _progressTimer = new System.Threading.Timer(OnProgressTimer, null,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            Debug.WriteLine("[NativeCameraWindows] Video recording started successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraWindows] Failed to start video recording: {ex.Message}\nStackTrace: {ex.StackTrace}");
            _isRecordingVideo = false;
            _currentVideoFile = null;
            VideoRecordingFailed?.Invoke(ex);
        }
    }

    /// <summary>
    /// Stops video recording
    /// </summary>
    public async Task StopVideoRecording()
    {
        if (!_isRecordingVideo || _mediaCapture == null || _currentVideoFile == null)
            return;

        try
        {
            Debug.WriteLine("[NativeCameraWindows] Stopping video recording...");

            // Stop progress timer
            _progressTimer?.Dispose();
            _progressTimer = null;

            // Stop recording
            await _mediaCapture.StopRecordAsync();

            DateTime recordingEndTime = DateTime.Now;
            TimeSpan duration = recordingEndTime - _recordingStartTime;

            // Get file size
            var properties = await _currentVideoFile.GetBasicPropertiesAsync();
            long fileSizeBytes = (long)properties.Size;

            Debug.WriteLine($"[NativeCameraWindows] Video recording stopped. Duration: {duration:mm\\:ss}, Size: {fileSizeBytes / (1024 * 1024):F1} MB");

            // Create captured video object
            CapturedVideo capturedVideo = new CapturedVideo
            {
                FilePath = _currentVideoFile.Path,
                Duration = duration,
                Format = GetCurrentVideoFormat(),
                Facing = FormsControl.Facing,
                Time = _recordingStartTime,
                FileSizeBytes = fileSizeBytes,
                Metadata = new Dictionary<string, object>
                {
                    { "Platform", "Windows" },
                    { "CameraDevice", _cameraDevice?.Name ?? "Unknown" },
                    { "RecordingStartTime", _recordingStartTime },
                    { "RecordingEndTime", recordingEndTime }
                }
            };

            _isRecordingVideo = false;

            // Resume pre-recording buffer after recording stops
            if (_enablePreRecording)
            {
                InitializePreRecordingBuffer();
            }

            // Fire success event
            VideoRecordingSuccess?.Invoke(capturedVideo);

            Debug.WriteLine("[NativeCameraWindows] Video recording completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCameraWindows] Failed to stop video recording: {ex.Message}");
            _isRecordingVideo = false;
            VideoRecordingFailed?.Invoke(ex);
        }
        finally
        {
            _currentVideoFile = null;
        }
    }    /// <summary>
         /// Initialize the pre-recording buffer
         /// </summary>
    private void InitializePreRecordingBuffer()
    {
        lock (_preRecordingLock)
        {
            if (_preRecordingBuffer == null)
            {
                _preRecordingBuffer = new Queue<Direct3DFrameData>();
                CalculateMaxPreRecordingFrames();
            }
        }
    }

    /// <summary>
    /// Calculate the maximum number of frames to keep in the pre-recording buffer
    /// Assumes ~30 fps average frame rate for estimation
    /// </summary>
    private void CalculateMaxPreRecordingFrames()
    {
        // Assuming average frame rate of 30 fps
        int averageFps = 30;
        _maxPreRecordingFrames = Math.Max(1, (int)(_preRecordDuration.TotalSeconds * averageFps));
    }

    /// <summary>
    /// Clear the pre-recording buffer
    /// </summary>
    private void ClearPreRecordingBuffer()
    {
        lock (_preRecordingLock)
        {
            if (_preRecordingBuffer != null)
            {
                while (_preRecordingBuffer.Count > 0)
                {
                    _preRecordingBuffer.Dequeue()?.Dispose();
                }
                _preRecordingBuffer = null;
            }
        }
    }

    /// <summary>
    /// Add a frame to the pre-recording buffer with automatic size management
    /// </summary>
    private void BufferPreRecordingFrame(byte[] frameData, int width, int height)
    {
        if (!_enablePreRecording || _preRecordingBuffer == null || frameData == null)
            return;

        try
        {
            lock (_preRecordingLock)
            {
                if (_preRecordingBuffer == null)
                    return;

                byte[] bufferedData = new byte[frameData.Length];
                Array.Copy(frameData, bufferedData, frameData.Length);

                var frame = new Direct3DFrameData
                {
                    Data = bufferedData,
                    Width = width,
                    Height = height,
                    Timestamp = DateTime.UtcNow
                };

                _preRecordingBuffer.Enqueue(frame);

                // Trim buffer to maintain PreRecordDuration
                while (_preRecordingBuffer.Count > _maxPreRecordingFrames)
                {
                    _preRecordingBuffer.Dequeue()?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Windows PreRecording] Error buffering frame: {ex.Message}");
        }
    }

    /// <summary>
    /// Add a frame to the pre-recording buffer from a SoftwareBitmap
    /// </summary>
    private void BufferPreRecordingFrameFromBitmap(SoftwareBitmap softwareBitmap)
    {
        if (!_enablePreRecording || _preRecordingBuffer == null || softwareBitmap == null)
            return;

        try
        {
            byte[] frameData = ExtractBitmapData(softwareBitmap);
            if (frameData != null)
            {
                BufferPreRecordingFrame(frameData, softwareBitmap.PixelWidth, softwareBitmap.PixelHeight);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Windows PreRecording] Error buffering frame from bitmap: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract raw pixel data from a SoftwareBitmap
    /// </summary>
    private byte[] ExtractBitmapData(SoftwareBitmap softwareBitmap)
    {
        try
        {
            using var buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read);
            using var reference = buffer.CreateReference();

            unsafe
            {
                byte* dataInBytes;
                uint capacity;
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity);

                byte[] managedArray = new byte[capacity];
                System.Runtime.InteropServices.Marshal.Copy((IntPtr)dataInBytes, managedArray, 0, (int)capacity);
                return managedArray;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Windows PreRecording] Error extracting bitmap data: {ex.Message}");
            return null;
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
            // Check if MediaCapture is available and has video recording capabilities
            return _mediaCapture != null &&
                   _mediaCapture.MediaCaptureSettings != null &&
                   _mediaCapture.MediaCaptureSettings.StreamingCaptureMode != StreamingCaptureMode.Audio;
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
        // TODO: Implement Windows video save to gallery
        await Task.Delay(100); // Placeholder
        return null;
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

    #region IDisposable

    public void Dispose()
    {
        try
        {
            Stop();

            // Stop video recording if active
            if (_isRecordingVideo)
            {
                try
                {
                    _mediaCapture?.StopRecordAsync()?.AsTask()?.Wait(1000);
                }
                catch { }
                _isRecordingVideo = false;
            }

            _progressTimer?.Dispose();
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
