#if IOS || MACCATALYST
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AppoMobi.Specials;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreImage;
using CoreMedia;
using CoreVideo;
using DrawnUi.Controls;
using Foundation;
using ImageIO;
using Microsoft.Maui.Media;
using Photos;
using SkiaSharp;
using SkiaSharp.Views.iOS;
using UIKit;
using static AVFoundation.AVMetadataIdentifiers;

namespace DrawnUi.Camera;

// Lightweight container for raw frame data - no SKImage creation
internal class RawFrameData : IDisposable
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BytesPerRow { get; set; }
    public DateTime Time { get; set; }
    public Rotation CurrentRotation { get; set; }
    public CameraPosition Facing { get; set; }
    public int Orientation { get; set; }
    public byte[] PixelData { get; set; } // Copy pixel data to avoid CVPixelBuffer lifetime issues

    public void Dispose()
    {
        PixelData = null; // Let GC handle byte array
    }
}

public partial class NativeCamera : NSObject, IDisposable, INativeCamera, INotifyPropertyChanged, IAVCaptureVideoDataOutputSampleBufferDelegate
{
    protected readonly SkiaCamera FormsControl;
    private AVCaptureSession _session;
    private AVCaptureVideoDataOutput _videoDataOutput;
    private AVCaptureStillImageOutput _stillImageOutput;
    private AVCaptureDeviceInput _deviceInput;
    private DispatchQueue _videoDataOutputQueue;
    private CameraProcessorState _state = CameraProcessorState.None;
    private bool _flashSupported;
    private bool _isCapturingStill;
    private double _zoomScale = 1.0;
    private readonly object _lockPreview = new();
    private CapturedImage _preview;
    bool _cameraUnitInitialized;

    // Frame processing throttling - only prevent concurrent processing
    private volatile bool _isProcessingFrame = false;
    private int _skippedFrameCount = 0;
    private int _processedFrameCount = 0;

    // Raw frame data for lazy SKImage creation - fixed memory leak version
    private readonly object _lockRawFrame = new();
    private RawFrameData _latestRawFrame;
    
    // Orientation tracking properties
    private UIInterfaceOrientation _uiOrientation;
    private UIDeviceOrientation _deviceOrientation;
    private AVCaptureVideoOrientation _videoOrientation;
    private UIImageOrientation _imageOrientation;
    private NSObject _orientationObserver;
    
    public Rotation CurrentRotation { get; private set; } = Rotation.rotate0Degrees;

    public AVCaptureDevice CaptureDevice
    {
        get
        {
            if (_deviceInput == null)
                return null;

            return _deviceInput.Device;
        }
    }

    public NativeCamera(SkiaCamera formsControl)
    {
        FormsControl = formsControl;
        _session = new AVCaptureSession();
        _videoDataOutput = new AVCaptureVideoDataOutput();
        _videoDataOutputQueue = new DispatchQueue("VideoDataOutput", false);

        SetupOrientationObserver();


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

    private void Setup()
    {
        try
        {
            SetupHardware();
            State = CameraProcessorState.Enabled;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NativeCameraiOS] Setup error: {e}");
            State = CameraProcessorState.Error;
        }
    }

    private void SetupOrientationObserver()
    {
        // Initialize orientation values
        _uiOrientation = UIApplication.SharedApplication.StatusBarOrientation;
        _deviceOrientation = UIDevice.CurrentDevice.Orientation;
        _videoOrientation = AVCaptureVideoOrientation.Portrait;
        
        System.Diagnostics.Debug.WriteLine($"[CAMERA SETUP] Initial orientations - UI: {_uiOrientation}, Device: {_deviceOrientation}, Video: {_videoOrientation}");
        
        // Set up orientation change observer
        _orientationObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIDevice.OrientationDidChangeNotification, 
            (notification) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CAMERA ORIENTATION] Device orientation changed notification received");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateOrientationFromMainThread();
                    // Also update the SkiaCamera's DeviceRotation to ensure both systems are in sync
                    var deviceOrientation = UIDevice.CurrentDevice.Orientation;
                    var rotation = 0;
                    switch (deviceOrientation)
                    {
                        case UIDeviceOrientation.Portrait:
                            rotation = 0;
                            break;
                        case UIDeviceOrientation.LandscapeLeft:
                            rotation = 90;
                            break;
                        case UIDeviceOrientation.PortraitUpsideDown:
                            rotation = 180;
                            break;
                        case UIDeviceOrientation.LandscapeRight:
                            rotation = 270;
                            break;
                        default:
                            rotation = 0;
                            break;
                    }
                    System.Diagnostics.Debug.WriteLine($"[CAMERA ORIENTATION] Setting SkiaCamera DeviceRotation to {rotation} degrees");
                    FormsControl.DeviceRotation = rotation;
                });
            });
    }

    private void SetupHardware()
    {
        _session.BeginConfiguration();
        _cameraUnitInitialized = false;

#if MACCATALYST
            _session.SessionPreset = AVCaptureSession.PresetHigh;
#else
        // Set session preset
        if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad)
        {
            _session.SessionPreset = AVCaptureSession.PresetHigh;
        }
        else
        {
            _session.SessionPreset = AVCaptureSession.PresetInputPriority;
        }
#endif

        AVCaptureDevice videoDevice = null;

        // Manual camera selection
        if (FormsControl.Facing == CameraPosition.Manual && FormsControl.CameraIndex >= 0)
        {
            var allDevices = AVCaptureDevice.DevicesWithMediaType(AVMediaTypes.Video.GetConstant());
            if (FormsControl.CameraIndex < allDevices.Length)
            {
                videoDevice = allDevices[FormsControl.CameraIndex];
                Console.WriteLine($"[NativeCameraApple] Selected camera by index {FormsControl.CameraIndex}: {videoDevice.LocalizedName}");
            }
            else
            {
                Console.WriteLine($"[NativeCameraApple] Invalid camera index {FormsControl.CameraIndex}, falling back to default");
                videoDevice = allDevices.FirstOrDefault();
            }
        }
        else
        {
            // Automatic selection based on facing
            var cameraPosition = FormsControl.Facing == CameraPosition.Selfie
                ? AVCaptureDevicePosition.Front
                : AVCaptureDevicePosition.Back;

            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0) && FormsControl.Type == CameraType.Max)
            {
                videoDevice = AVCaptureDevice.GetDefaultDevice(AVCaptureDeviceType.BuiltInTripleCamera, AVMediaTypes.Video, cameraPosition);
            }

            if (videoDevice == null)
            {
                if (UIDevice.CurrentDevice.CheckSystemVersion(10, 2) && FormsControl.Type == CameraType.Max)
                {
                    videoDevice = AVCaptureDevice.GetDefaultDevice(AVCaptureDeviceType.BuiltInDualCamera, AVMediaTypes.Video, cameraPosition);
                }

                if (videoDevice == null)
                {
                    var videoDevices = AVCaptureDevice.DevicesWithMediaType(AVMediaTypes.Video.GetConstant());

#if MACCATALYST
                    videoDevice = videoDevices.FirstOrDefault();
#else
                    videoDevice = videoDevices.FirstOrDefault(d => d.Position == cameraPosition);
#endif
                    if (videoDevice == null)
                    {
                        State = CameraProcessorState.Error;
                        _session.CommitConfiguration();
                        return;
                    }
                }
            }
        }

        var allFormats = videoDevice.Formats.ToList();
        AVCaptureDeviceFormat format = null;
        
        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            format = allFormats.Where(x => x.MultiCamSupported)
                .OrderByDescending(x => x.HighResolutionStillImageDimensions.Width)
                .FirstOrDefault();
        }

        if (format == null)
        {
            format = allFormats.OrderByDescending(x => x.HighResolutionStillImageDimensions.Width)
                .FirstOrDefault();
        }

        NSError error;
        if (videoDevice.LockForConfiguration(out error))
        {
            if (videoDevice.SmoothAutoFocusSupported)
                videoDevice.SmoothAutoFocusEnabled = true;
                
            videoDevice.ActiveFormat = format;
            
            // Ensure exposure is set to continuous auto exposure during setup
            if (videoDevice.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                videoDevice.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                System.Diagnostics.Debug.WriteLine($"[CAMERA SETUP] Set initial exposure mode to ContinuousAutoExposure");
            }
            
            // Reset exposure bias to neutral
            if (videoDevice.MinExposureTargetBias != videoDevice.MaxExposureTargetBias)
            {
                videoDevice.SetExposureTargetBias(0, null);
                System.Diagnostics.Debug.WriteLine($"[CAMERA SETUP] Reset exposure bias to 0");
            }
            
            videoDevice.UnlockForConfiguration();
        }

        while (_session.Inputs.Any())
        {
            _session.RemoveInput(_session.Inputs[0]);
        }

        // Remove all existing outputs before adding new ones
        while (_session.Outputs.Any())
        {
            _session.RemoveOutput(_session.Outputs[0]);
        }

        _deviceInput = new AVCaptureDeviceInput(videoDevice, out error);
        if (error != null)
        {
            Console.WriteLine($"Could not create video device input: {error.LocalizedDescription}");
            _session.CommitConfiguration();
            State = CameraProcessorState.Error;
            return;
        }

        _session.AddInput(_deviceInput);

        var dictionary = new NSMutableDictionary();
        dictionary[AVVideo.CodecKey] = new NSNumber((int)AVVideoCodec.JPEG);
        _stillImageOutput = new AVCaptureStillImageOutput()
        {
            OutputSettings = new NSDictionary()
        };
        _stillImageOutput.HighResolutionStillImageOutputEnabled = true;

        if (_session.CanAddOutput(_stillImageOutput))
        {
            _session.AddOutput(_stillImageOutput);
        }
        else
        {
            Console.WriteLine("Could not add still image output to the session");
            _session.CommitConfiguration();
            State = CameraProcessorState.Error;
            return;
        }

        if (_session.CanAddOutput(_videoDataOutput))
        {
            // Configure video data output BEFORE adding to session
            _session.AddOutput(_videoDataOutput);
            _videoDataOutput.AlwaysDiscardsLateVideoFrames = true;
            _videoDataOutput.WeakVideoSettings = new NSDictionary(CVPixelBuffer.PixelFormatTypeKey, 
                CVPixelFormatType.CV32BGRA);
            _videoDataOutput.SetSampleBufferDelegate(this, _videoDataOutputQueue);
            
            // Set initial video orientation from the connection
            var videoConnection = _videoDataOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant());
            if (videoConnection != null && videoConnection.SupportsVideoOrientation)
            {
                _videoOrientation = videoConnection.VideoOrientation;
                System.Diagnostics.Debug.WriteLine($"[CAMERA SETUP] Initial video orientation: {_videoOrientation}");
            }
        }
        else
        {
            Console.WriteLine("Could not add video data output to the session");
            _session.CommitConfiguration();
            State = CameraProcessorState.Error;
            return;
        }

        _flashSupported = videoDevice.FlashAvailable;

        var focalLengths = new List<float>();
        //var physicalFocalLength = 4.15f;
        //focalLengths.Add(physicalFocalLength);

        var cameraUnit = new CameraUnit
        {
            Id = videoDevice.UniqueID,
            Facing = FormsControl.Facing,
            FocalLengths = focalLengths,
            FieldOfView = videoDevice.ActiveFormat.VideoFieldOfView,
            Meta = FormsControl.CreateMetadata()
        };

        //other data will be filled when camera starts working..

        FormsControl.CameraDevice = cameraUnit;

        var formatDescription = videoDevice.ActiveFormat.FormatDescription as CMVideoFormatDescription;
        if (formatDescription != null)
        {
            var dimensions = formatDescription.Dimensions;
            FormsControl.SetRotatedContentSize(new SKSize(dimensions.Width, dimensions.Height), 0);
        }

        _session.CommitConfiguration();

        UpdateDetectOrientation();
    }

 

    #endregion

    #region INativeCamera Implementation

    public void Start()
    {
        if (State == CameraProcessorState.Enabled && _session.Running)
            return;

        try
        {
            Setup();

            _session.StartRunning();
            State = CameraProcessorState.Enabled;
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.Current.KeepScreenOn = true;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NativeCameraiOS] Start error: {e}");
            State = CameraProcessorState.Error;
        }
    }

    public void Stop(bool force = false)
    {
        SetCapture(null);

        // Clear raw frame data
        lock (_lockRawFrame)
        {
            _latestRawFrame?.Dispose();
            _latestRawFrame = null;
        }

        if (State == CameraProcessorState.None && !force)
            return;

        if (State != CameraProcessorState.Enabled && !force)
            return; //avoid spam

        try
        {
            _session.StopRunning();
            State = CameraProcessorState.None;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.Current.KeepScreenOn = false;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NativeCameraiOS] Stop error: {e}");
            State = CameraProcessorState.Error;
        }
    }

    public void TurnOnFlash()
    {
        if (!_flashSupported || _deviceInput?.Device == null)
            return;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                if (_deviceInput.Device.HasTorch)
                {
                    _deviceInput.Device.TorchMode = AVCaptureTorchMode.On;
                }
                if (_deviceInput.Device.HasFlash)
                {
                    _deviceInput.Device.FlashMode = AVCaptureFlashMode.On;
                }
            }
            finally
            {
                _deviceInput.Device.UnlockForConfiguration();
            }
        }
    }

    public void TurnOffFlash()
    {
        if (!_flashSupported || _deviceInput?.Device == null)
            return;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                if (_deviceInput.Device.HasTorch)
                {
                    _deviceInput.Device.TorchMode = AVCaptureTorchMode.Off;
                }
                if (_deviceInput.Device.HasFlash)
                {
                    _deviceInput.Device.FlashMode = AVCaptureFlashMode.Off;
                }
            }
            finally
            {
                _deviceInput.Device.UnlockForConfiguration();
            }
        }
    }

    public void SetZoom(float zoom)
    {
        if (_deviceInput?.Device == null)
            return;

        _zoomScale = zoom;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                var clampedZoom = (nfloat)Math.Max(_deviceInput.Device.MinAvailableVideoZoomFactor,
                    Math.Min(zoom, _deviceInput.Device.MaxAvailableVideoZoomFactor));

                _deviceInput.Device.VideoZoomFactor = clampedZoom;
            }
            finally
            {
                _deviceInput.Device.UnlockForConfiguration();
            }
        }
    }

    /// <summary>
    /// Sets manual exposure settings for the camera
    /// </summary>
    /// <param name="iso">ISO sensitivity value</param>
    /// <param name="shutterSpeed">Shutter speed in seconds</param>
    public bool SetManualExposure(float iso, float shutterSpeed)
    {
        if (_deviceInput?.Device == null)
            return false;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                if (_deviceInput.Device.IsExposureModeSupported(AVCaptureExposureMode.Custom))
                {
                    var duration = CMTime.FromSeconds(shutterSpeed, 1000000000);
                    _deviceInput.Device.LockExposure(duration, iso, null);

                    System.Diagnostics.Debug.WriteLine($"[iOS MANUAL] Set ISO: {iso}, Shutter: {shutterSpeed}s");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[iOS MANUAL] Custom exposure mode not supported");
                }
            }
            finally
            {
                _deviceInput.Device.UnlockForConfiguration();
            }
        }

        return false;
    }

    /// <summary>
    /// Sets the camera to automatic exposure mode
    /// </summary>
    public void SetAutoExposure()
    {
        if (_deviceInput?.Device == null)
            return;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                if (_deviceInput.Device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
                {
                    _deviceInput.Device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                    System.Diagnostics.Debug.WriteLine("[iOS AUTO] Set to ContinuousAutoExposure mode");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[iOS AUTO] ContinuousAutoExposure mode not supported");
                }
            }
            finally
            {
                _deviceInput.Device.UnlockForConfiguration();
            }
        }
    }

    /// <summary>
    /// Gets the manual exposure capabilities and recommended settings for the camera
    /// </summary>
    /// <returns>Camera manual exposure range information</returns>
    public CameraManualExposureRange GetExposureRange()
    {
        if (_deviceInput?.Device == null)
        {
            return new CameraManualExposureRange(0, 0, 0, 0, false, null);
        }

        try
        {
            bool isSupported = _deviceInput.Device.IsExposureModeSupported(AVCaptureExposureMode.Custom);

            if (!isSupported)
            {
                return new CameraManualExposureRange(0, 0, 0, 0, false, null);
            }

            float minISO = _deviceInput.Device.ActiveFormat.MinISO;
            float maxISO = _deviceInput.Device.ActiveFormat.MaxISO;
            float minShutter = (float)_deviceInput.Device.ActiveFormat.MinExposureDuration.Seconds;
            float maxShutter = (float)_deviceInput.Device.ActiveFormat.MaxExposureDuration.Seconds;

            var baselines = new CameraExposureBaseline[]
            {
                new CameraExposureBaseline(100, 1.0f/60.0f, "Indoor", "Office/bright indoor lighting"),
                new CameraExposureBaseline(400, 1.0f/30.0f, "Mixed", "Dim indoor/overcast outdoor"),
                new CameraExposureBaseline(800, 1.0f/15.0f, "Low Light", "Evening/dark indoor")
            };

            System.Diagnostics.Debug.WriteLine($"[iOS RANGE] ISO: {minISO}-{maxISO}, Shutter: {minShutter}-{maxShutter}s");

            return new CameraManualExposureRange(minISO, maxISO, minShutter, maxShutter, true, baselines);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[iOS RANGE] Error: {ex.Message}");
            return new CameraManualExposureRange(0, 0, 0, 0, false, null);
        }
    }

    public void ApplyDeviceOrientation(int orientation)
    {
        UpdateOrientationFromMainThread();
    }

    public void TakePicture()
    {
        if (_isCapturingStill || _stillImageOutput == null)
            return;

        Task.Run(async () =>
        {
            try
            {
                _isCapturingStill = true;

                var status = PHPhotoLibrary.AuthorizationStatus;
                if (status != PHAuthorizationStatus.Authorized)
                {
                    status = await PHPhotoLibrary.RequestAuthorizationAsync();
                    if (status != PHAuthorizationStatus.Authorized)
                    {
                        StillImageCaptureFailed?.Invoke(new UnauthorizedAccessException("Photo library access denied"));
                        return;
                    }
                }

                var videoConnection = _stillImageOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant());
                var sampleBuffer = await _stillImageOutput.CaptureStillImageTaskAsync(videoConnection);
                var jpegData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);

                using var image = CIImage.FromData(jpegData);

                //get metadata
                var metaData = image.Properties.Dictionary.MutableCopy() as NSMutableDictionary;
                var orientation = metaData["Orientation"].ToString().ToInteger();
                var props = image.Properties;

#if DEBUG
                var exif = image.Properties.Exif;
                foreach (var key in exif.Dictionary.Keys)
                {
                    Debug.WriteLine($"{key}: {exif.Dictionary[key]}");
                }
#endif

                var rotation = 0;
                bool flipHorizontal, flipVertical; //unused
                switch (orientation)
                {
                    case 1:
                        break;
                    case 2:
                        flipHorizontal = true; 
                        break;
                    case 3:
                        rotation = 180;
                        break;
                    case 4:
                        flipVertical = true; 
                        break;
                    case 5:
                        rotation = 270;
                        flipHorizontal = true;
                        break;
                    case 6:
                        rotation = 270;
                        break;
                    case 7:
                        rotation = 90;
                        flipHorizontal = true;
                        break;
                    case 8:
                        rotation = 90;
                        break;
                }

                using var uiImage = UIImage.LoadFromData(jpegData);
                var skImage = uiImage.ToSKImage();

                var capturedImage = new CapturedImage()
                {
                    Facing = FormsControl.Facing,
                    Time = DateTime.UtcNow,
                    Image = skImage,
                    Rotation = rotation,
                    Meta = Metadata.CreateMetadataFromProperties(props, metaData)
                };

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StillImageCaptureSuccess?.Invoke(capturedImage);
                });
            }
            catch (Exception e)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StillImageCaptureFailed?.Invoke(e);
                });
            }
            finally
            {
                _isCapturingStill = false;
            }
        });
    }

 
    public SKImage GetPreviewImage()
    {
        // First check if we have a ready preview
        lock (_lockPreview)
        {
            if (_preview != null)
            {
                var get = _preview;
                _preview = null;

                if (_kill == get)
                {
                    _kill = null;
                }
                return get.Image;
            }
        }

        // No ready preview, create one from raw frame data if available
        lock (_lockRawFrame)
        {
            if (_latestRawFrame == null)
                return null;

            try
            {
                // Create SKImage on main thread from copied pixel data
                var info = new SKImageInfo(_latestRawFrame.Width, _latestRawFrame.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                // Pin the byte array and create SKImage
                var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(_latestRawFrame.PixelData, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    var pinnedPtr = gcHandle.AddrOfPinnedObject();
                    using var rawImage = SKImage.FromPixels(info, pinnedPtr, _latestRawFrame.BytesPerRow);

                    // Apply rotation if needed
                    using var bitmap = SKBitmap.FromImage(rawImage);
                    using var rotatedBitmap = HandleOrientation(bitmap, (double)_latestRawFrame.CurrentRotation);
                    return SKImage.FromBitmap(rotatedBitmap);
                }
                finally
                {
                    gcHandle.Free();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[NativeCameraiOS] GetPreviewImage error: {e}");
                return null;
            }
        }
    }

    public async Task<string> SaveJpgStreamToGallery(Stream stream, string filename, double cameraSavedRotation, Metadata meta, string album)
    {
        try
        {
            var data = NSData.FromStream(stream);
            
            bool complete = false;
            string resultPath = null;

            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var options = new PHAssetResourceCreationOptions
                {
                    OriginalFilename = filename
                };

                var creationRequest = PHAssetCreationRequest.CreationRequestForAsset();
                creationRequest.AddResource(PHAssetResourceType.Photo, data, options);

            }, (success, error) =>
            {
                if (success)
                {
                    resultPath = filename;
                }
                else
                {
                    Console.WriteLine($"SaveJpgStreamToGallery error: {error}");
                }
                complete = true;
            });

            while (!complete)
            {
                await Task.Delay(10);
            }

            return resultPath;
        }
        catch (Exception e)
        {
            Console.WriteLine($"SaveJpgStreamToGallery error: {e}");
            return null;
        }
    }

    #endregion

    /// <summary>
    /// Gets current live exposure settings from AVCaptureDevice in auto exposure mode
    /// These properties update dynamically as the camera adjusts exposure automatically
    /// </summary>
    private (float iso, float aperture, float shutterSpeed) GetLiveExposureSettings()
    {
        if (CaptureDevice == null)
            return (100f, 1.8f, 1f / 60f);

        try
        {
            // These properties are observable and change dynamically in auto exposure mode
            var currentISO = CaptureDevice.ISO;                          // Real-time ISO
            var currentAperture = CaptureDevice.LensAperture;            // Fixed on iPhone (f/1.8, f/2.8, etc)
            var exposureDuration = CaptureDevice.ExposureDuration;       // Real-time shutter speed
            var currentShutter = (float)exposureDuration.Seconds;

            return (currentISO, currentAperture, currentShutter);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[iOS Exposure Error] {ex.Message}");
            return (100f, 1.8f, 1f / 60f);
        }
    }

    #region AVCaptureVideoDataOutputSampleBufferDelegate

    [Export("captureOutput:didOutputSampleBuffer:fromConnection:")]
    public void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
    {
        if (FormsControl == null || _isCapturingStill || State != CameraProcessorState.Enabled)
            return;

        // THROTTLING: Only skip if previous frame is still being processed (prevents thread overwhelm)
        if (_isProcessingFrame)
        {
            _skippedFrameCount++;
            return;
        }

        _isProcessingFrame = true;
        _processedFrameCount++;

        // Log stats every 300 frames
        if (_processedFrameCount % 300 == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[NativeCameraiOS] Frame stats - Processed: {_processedFrameCount}, Skipped: {_skippedFrameCount}");
        }

        bool hasFrame=false;
        try
        {
            using var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
            if (pixelBuffer == null)
                return;

            pixelBuffer.Lock(CVPixelBufferLock.ReadOnly);

            try
            {

                var (iso, aperture, shutterSpeed) = GetLiveExposureSettings();

                var attachments = sampleBuffer.GetAttachments(CMAttachmentMode.ShouldPropagate);
                var exif = attachments["{Exif}"] as NSDictionary;
                var focal = exif["FocalLength"].ToString().ToFloat();


                if (!_cameraUnitInitialized)
                {
                    _cameraUnitInitialized = true;

                    var focals = new List<float>();
                    var focal35mm = exif["FocalLenIn35mmFilm"].ToString().ToFloat();
                    var name = exif["LensModel"].ToString();
                    var lenses = exif["LensSpecification "] as NSDictionary;
                    if (lenses != null)
                    {
                        foreach (var lens in lenses)
                        {
                            var add = lens.ToString().ToDouble();
                            focals.Add((float)add);
                        }
                    }
                    else
                    {
                        focals.Add((float)focal);
                    }

                    // FOV = 2 arctan (x / (2 f)), where x is the diagonal of the film.
                    var unit = FormsControl.CameraDevice;

                    unit.Id = name;
                    unit.SensorCropFactor = focal35mm / focal;
                    unit.FocalLengths = focals;
                    unit.PixelXDimension = exif["PixelXDimension"].ToString().ToFloat();
                    unit.PixelYDimension = exif["PixelYDimension"].ToString().ToFloat();
                    unit.FocalLength = focal;

                    var formatInfo = _deviceInput.Device.ActiveFormat;
                    var pixelsZoom = formatInfo.VideoZoomFactorUpscaleThreshold;
                    float aspectH = unit.PixelXDimension / unit.PixelYDimension;
                    float fovH = formatInfo.VideoFieldOfView;
                    float fovV = fovH / aspectH;

                    var sensorWidth = (float)(2 * unit.FocalLength * Math.Tan(fovH * Math.PI / 2.0f * 180));
                    var sensorHeight = (float)(2 * unit.FocalLength * Math.Tan(fovV * Math.PI / 2.0f * 180));

                    unit.SensorHeight = sensorHeight;
                    unit.SensorWidth = sensorWidth;
                    unit.FieldOfView = fovH;

                }

                FormsControl.CameraDevice.Meta.FocalLength = focal;
                FormsControl.CameraDevice.Meta.ISO = (int)iso;
                FormsControl.CameraDevice.Meta.Aperture = aperture;
                FormsControl.CameraDevice.Meta.Shutter = shutterSpeed;

                switch ((int)CurrentRotation)
                {
                    case 90:
                        FormsControl.CameraDevice.Meta.Orientation = 6;
                        break;
                    case 270:
                        FormsControl.CameraDevice.Meta.Orientation = 8;
                        break;
                    case 180:
                        FormsControl.CameraDevice.Meta.Orientation = 3;
                        break;
                    default:
                        FormsControl.CameraDevice.Meta.Orientation = 1;
                        break;
                }

               var width = (int)pixelBuffer.Width;
                var height = (int)pixelBuffer.Height;
                var bytesPerRow = (int)pixelBuffer.BytesPerRow;
                var baseAddress = pixelBuffer.BaseAddress;

                // Copy pixel data to avoid CVPixelBuffer lifetime issues - minimal memory copy
                var dataSize = height * bytesPerRow;
                var pixelData = new byte[dataSize];
                System.Runtime.InteropServices.Marshal.Copy(baseAddress, pixelData, 0, dataSize);

                // Store raw frame data for SKImage creation on main thread
                var rawFrame = new RawFrameData
                {
                    Width = width,
                    Height = height,
                    BytesPerRow = bytesPerRow,
                    Time = DateTime.UtcNow,
                    CurrentRotation = CurrentRotation,
                    Facing = FormsControl.Facing,
                    Orientation = (int)CurrentRotation,
                    PixelData = pixelData
                };

                SetRawFrame(rawFrame);
                hasFrame=true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[NativeCameraiOS] pixelBuffer processing error: {e}");
            }
            finally
            {
                pixelBuffer.Unlock(CVPixelBufferLock.ReadOnly);
                if (hasFrame)
                {
                    FormsControl.UpdatePreview();
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NativeCameraiOS] Frame processing error: {e}");
        }
        finally
        {
            // IMPORTANT: Always reset processing flag
            _isProcessingFrame = false;
        }
    }

    CapturedImage _kill;

    void SetRawFrame(RawFrameData rawFrame)
    {
        lock (_lockRawFrame)
        {
            // Dispose old raw frame data immediately to prevent memory accumulation
            _latestRawFrame?.Dispose();
            _latestRawFrame = rawFrame;
        }
    }

    void SetCapture(CapturedImage capturedImage)
    {
        lock (_lockPreview)
        {
            // Apple's recommended pattern: Keep only the latest frame
            // Dispose the old preview immediately if we have a new one
            if (_preview != null && capturedImage != null)
            {
                _preview.Dispose();
                _preview = null;
            }

            // Dispose any queued frame
            _kill?.Dispose();
            _kill = _preview;
            _preview = capturedImage;
        }
    }

    #endregion



    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Orientation Handling

    public SKBitmap HandleOrientation(SKBitmap bitmap, double sensor)
    {
        SKBitmap rotated;
        switch (sensor)
        {
            case 180:
                using (var surface = new SKCanvas(bitmap))
                {
                    surface.RotateDegrees(180, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.DrawBitmap(bitmap.Copy(), 0, 0);
                }
                return bitmap;

            case 270: //iphone on the right side
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(0, rotated.Height);
                    surface.RotateDegrees(270);
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            case 90: // iphone on the left side
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(rotated.Width, 0);
                    surface.RotateDegrees(90);
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            default:
                return bitmap;
        }
    }

    public void UpdateOrientationFromMainThread()
    {
        _uiOrientation = UIApplication.SharedApplication.StatusBarOrientation;
        _deviceOrientation = UIDevice.CurrentDevice.Orientation;
        UpdateDetectOrientation();
    }

    public void UpdateDetectOrientation()
    {
        if (_videoDataOutput?.Connections?.Any() == true)
        {
            // Get current video orientation from connection
            var videoConnection = _videoDataOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant());
            if (videoConnection != null && videoConnection.SupportsVideoOrientation)
            {
                _videoOrientation = videoConnection.VideoOrientation;
            }
            
            CurrentRotation = GetRotation(
                _uiOrientation,
                _videoOrientation,
                _deviceInput?.Device?.Position ?? AVCaptureDevicePosition.Back);

            switch (_uiOrientation)
            {
                case UIInterfaceOrientation.Portrait:
                    _imageOrientation = UIImageOrientation.Right;
                    break;
                case UIInterfaceOrientation.PortraitUpsideDown:
                    _imageOrientation = UIImageOrientation.Left;
                    break;
                case UIInterfaceOrientation.LandscapeLeft:
                    _imageOrientation = UIImageOrientation.Up;
                    break;
                case UIInterfaceOrientation.LandscapeRight:
                    _imageOrientation = UIImageOrientation.Down;
                    break;
                default:
                    _imageOrientation = UIImageOrientation.Up;
                    break;
            }

            System.Diagnostics.Debug.WriteLine($"[UpdateDetectOrientation]: rotation: {CurrentRotation}, orientation: {_imageOrientation}, device: {_deviceInput?.Device?.Position}, video: {_videoOrientation}, ui:{_uiOrientation}");
        }
    }

    public Rotation GetRotation(
        UIInterfaceOrientation interfaceOrientation,
        AVCaptureVideoOrientation videoOrientation,
        AVCaptureDevicePosition cameraPosition)
    {
        /*
         Calculate the rotation between the videoOrientation and the interfaceOrientation.
         The direction of the rotation depends upon the camera position.
         */

        switch (videoOrientation)
        {
            case AVCaptureVideoOrientation.Portrait:
                switch (interfaceOrientation)
                {
                    case UIInterfaceOrientation.LandscapeRight:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate90Degrees;
                        }
                        else
                        {
                            return Rotation.rotate270Degrees;
                        }

                    case UIInterfaceOrientation.LandscapeLeft:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate270Degrees;
                        }
                        else
                        {
                            return Rotation.rotate90Degrees;
                        }

                    case UIInterfaceOrientation.Portrait:
                        return Rotation.rotate0Degrees;

                    case UIInterfaceOrientation.PortraitUpsideDown:
                        return Rotation.rotate180Degrees;

                    default:
                        return Rotation.rotate0Degrees;
                }

            case AVCaptureVideoOrientation.PortraitUpsideDown:
                switch (interfaceOrientation)
                {
                    case UIInterfaceOrientation.LandscapeRight:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate270Degrees;
                        }
                        else
                        {
                            return Rotation.rotate90Degrees;
                        }

                    case UIInterfaceOrientation.LandscapeLeft:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate90Degrees;
                        }
                        else
                        {
                            return Rotation.rotate270Degrees;
                        }

                    case UIInterfaceOrientation.Portrait:
                        return Rotation.rotate180Degrees;

                    case UIInterfaceOrientation.PortraitUpsideDown:
                        return Rotation.rotate0Degrees;

                    default:
                        return Rotation.rotate180Degrees;
                }

            case AVCaptureVideoOrientation.LandscapeRight:
                switch (interfaceOrientation)
                {
                    case UIInterfaceOrientation.LandscapeRight:
                        return Rotation.rotate0Degrees;

                    case UIInterfaceOrientation.LandscapeLeft:
                        return Rotation.rotate180Degrees;

                    case UIInterfaceOrientation.Portrait:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate270Degrees;
                        }
                        else
                        {
                            return Rotation.rotate90Degrees;
                        }

                    case UIInterfaceOrientation.PortraitUpsideDown:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate90Degrees;
                        }
                        else
                        {
                            return Rotation.rotate270Degrees;
                        }

                    default:
                        return Rotation.rotate0Degrees;
                }

            case AVCaptureVideoOrientation.LandscapeLeft:
                switch (interfaceOrientation)
                {
                    case UIInterfaceOrientation.LandscapeLeft:
                        return Rotation.rotate0Degrees;

                    case UIInterfaceOrientation.LandscapeRight:
                        return Rotation.rotate180Degrees;

                    case UIInterfaceOrientation.Portrait:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate90Degrees;
                        }
                        else
                        {
                            return Rotation.rotate270Degrees;
                        }

                    case UIInterfaceOrientation.PortraitUpsideDown:
                        if (cameraPosition == AVCaptureDevicePosition.Front)
                        {
                            return Rotation.rotate270Degrees;
                        }
                        else
                        {
                            return Rotation.rotate90Degrees;
                        }

                    default:
                        return Rotation.rotate0Degrees;
                }

            default:
                return Rotation.rotate0Degrees;
        }
    }

    #endregion

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            
            _session?.Dispose();
            _videoDataOutput?.Dispose();
            _stillImageOutput?.Dispose();
            _deviceInput?.Dispose();
            _videoDataOutputQueue?.Dispose();

            SetCapture(null);
            _kill?.Dispose();

            // Clean up raw frame data
            lock (_lockRawFrame)
            {
                _latestRawFrame?.Dispose();
                _latestRawFrame = null;
            }

            // Clean up orientation observer
            if (_orientationObserver != null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(_orientationObserver);
                _orientationObserver?.Dispose();
                _orientationObserver = null;
            }
        }

        base.Dispose(disposing);
    }

    #endregion
}
#endif
