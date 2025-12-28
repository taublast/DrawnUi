#if IOS || MACCATALYST
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
using IOSurface;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Media;
using Photos;
using SkiaSharp;
using SkiaSharp.Views.iOS;
using UIKit;
using static AVFoundation.AVMetadataIdentifiers;

namespace DrawnUi.Camera;


public partial class NativeCamera : NSObject, IDisposable, INativeCamera, INotifyPropertyChanged, IAVCaptureVideoDataOutputSampleBufferDelegate, IAVCaptureFileOutputRecordingDelegate
{
    protected readonly SkiaCamera FormsControl;
    private AVCaptureSession _session;
    private AVCaptureVideoDataOutput _videoDataOutput;
    private AVCaptureStillImageOutput _stillImageOutput;
    private AVCaptureDeviceInput _deviceInput;
    private AVCaptureDeviceInput _audioInput;
    private DispatchQueue _videoDataOutputQueue;
    private CameraProcessorState _state = CameraProcessorState.None;
    private bool _flashSupported;
    private bool _isCapturingStill;
    private bool _isRecordingVideo;
    private AVCaptureMovieFileOutput _movieFileOutput;
    private NSUrl _currentVideoUrl;
    private DateTime _recordingStartTime;
    private NSTimer _progressTimer;
    private bool _recordAudio = false; // Default to silent video
    private double _zoomScale = 1.0;
    private readonly object _lockPreview = new();
    private CapturedImage _preview;
    bool _cameraUnitInitialized;
    FlashMode _flashMode = FlashMode.Off;
    CaptureFlashMode _captureFlashMode = CaptureFlashMode.Auto;

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




    object lockSetup = new();

    private void SetupHardware()
    {
        lock (lockSetup)
        {
            _session.BeginConfiguration();

            try
            {
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
                                return;
                            }
                        }
                    }
                }

                var allFormats = videoDevice.Formats.ToList();
                AVCaptureDeviceFormat format = null;

                SetupStillFormats(allFormats);

                // Select format based on CaptureMode
                if (FormsControl.CaptureMode == CaptureModeType.Video)
                {
                    format = SelectFormatForVideoAspect(allFormats);
                }
                else
                {
                    format = SelectOptimalFormat(allFormats, FormsControl.PhotoQuality);
                }

                NSError error;
                if (videoDevice.LockForConfiguration(out error))
                {
                    if (videoDevice.SmoothAutoFocusSupported)
                        videoDevice.SmoothAutoFocusEnabled = true;

                    videoDevice.ActiveFormat = format;

                    // Set FPS if video mode
                    if (FormsControl.CaptureMode == CaptureModeType.Video && FormsControl.VideoQuality != VideoQuality.Manual)
                    {
                        double targetFps = 30.0;
                        if (FormsControl.VideoQuality == VideoQuality.High || FormsControl.VideoQuality == VideoQuality.Ultra)
                        {
                            targetFps = 60.0;
                        }
                        
                        // Check if format supports this FPS
                        bool supported = false;
                        foreach(var range in format.VideoSupportedFrameRateRanges)
                        {
                            if (Math.Abs(range.MaxFrameRate - targetFps) < 0.1)
                            {
                                supported = true;
                                break;
                            }
                        }
                        
                        if (supported)
                        {
                            try
                            {
                                videoDevice.ActiveVideoMinFrameDuration = new CMTime(1, (int)targetFps);
                                videoDevice.ActiveVideoMaxFrameDuration = new CMTime(1, (int)targetFps);
                                Console.WriteLine($"[NativeCameraiOS] Set FPS to {targetFps}");
                            }
                            catch(Exception e)
                            {
                                Console.WriteLine($"[NativeCameraiOS] Failed to set FPS: {e.Message}");
                            }
                        }
                    }

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
                    PreviewWidth = (int)dimensions.Width;  
                    PreviewHeight = (int)dimensions.Height; 
                    FormsControl.SetRotatedContentSize(new SKSize(dimensions.Width, dimensions.Height), 0);
                }
            }
            finally
            {
                _session.CommitConfiguration();
            }

            UpdateDetectOrientation();
        }
    }

    public int PreviewWidth { get; private set; }
    public int PreviewHeight { get; private set; }

    public List<CaptureFormat> StillFormats { get; protected set; }

    private void SetupStillFormats(List<AVCaptureDeviceFormat> allFormats)
    {
        var formats = new List<CaptureFormat>();

        try
        {
            if (allFormats != null)
            {
                var filtered = GetFilteredFormats(allFormats);

                var uniqueResolutions = filtered
                .Select(f => f.HighResolutionStillImageDimensions)
                .GroupBy(dims => new { dims.Width, dims.Height })
                .Select(group => group.First())
                .OrderByDescending(dims => dims.Width * dims.Height)
                .ToList();

                Console.WriteLine($"[SkiaCameraApple] Found {uniqueResolutions.Count} unique still image formats:");

                for (int i = 0; i < uniqueResolutions.Count; i++)
                {
                    var dims = uniqueResolutions[i];
                    Console.WriteLine($"  [{i}] {dims.Width}x{dims.Height}");

                    formats.Add(new CaptureFormat
                    {
                        Width = (int)dims.Width,
                        Height = (int)dims.Height,
                        FormatId = $"{dims.Width}x{dims.Height}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraApple] Error getting capture formats: {ex.Message}");
        }

        StillFormats = formats;
    }

    /// <summary>
    /// Filter formats and sort by resolution
    /// </summary>
    /// <param name="allFormats"></param>
    /// <returns></returns>
    public List<AVCaptureDeviceFormat> GetFilteredFormats(IEnumerable<AVCaptureDeviceFormat> allFormats)
    {
        var availableFormats = allFormats
            .Where(f => f.HighResolutionStillImageDimensions.Width > 0 && f.HighResolutionStillImageDimensions.Height > 0)
            .Select(f => new
            {
                Format = f,
                VideoDims = (f.FormatDescription as CMVideoFormatDescription)?.Dimensions ?? new CMVideoDimensions()
            })
            .Where(f => IsPreviewSizeSuitable(f.VideoDims))
            .OrderByDescending(f => f.Format.HighResolutionStillImageDimensions.Width * f.Format.HighResolutionStillImageDimensions.Height)
            .Select(f => f.Format)
            .ToList();

        return availableFormats;
    }

    /// <summary>
    /// Determines if video dimensions are suitable for preview
    /// </summary>
    /// <param name="dimensions"></param>
    /// <returns></returns>
    private bool IsPreviewSizeSuitable(CMVideoDimensions dimensions)
    {
        var width = dimensions.Width;
        var height = dimensions.Height;

        // Minimum resolution threshold
        if (width < 640 || height < 480)
            return false;

        // limit preview size for performance
        var videoPixels = width * height;

        // With optimized pipeline we can allow 1080p on phones too
        var maxVideoPixels = 1920 * 1080; 

        if (videoPixels > maxVideoPixels)
            return false;

        // Reject unusual aspect ratios that might indicate non-standard formats
        var aspectRatio = (double)width / height;
        if (aspectRatio < 0.5 || aspectRatio > 2.5)
            return false;

        return true;
    }

    /// <summary>
    /// Format details structure for AOT compatibility
    /// </summary>
    private struct FormatDetail
    {
        public AVCaptureDeviceFormat Format { get; init; }
        public CMVideoDimensions StillDims { get; init; }
        public CMVideoDimensions VideoDims { get; init; }
        public int StillPixels { get; init; }
    }

    /// <summary>
    /// Select optimal format based on quality requirements
    /// </summary>
    /// <param name="allFormats"></param>
    /// <param name="quality"></param>
    /// <returns></returns>
    private AVCaptureDeviceFormat SelectOptimalFormat(List<AVCaptureDeviceFormat> allFormats, CaptureQuality quality)
    {
        var availableFormats = GetFilteredFormats(allFormats);

        if (!availableFormats.Any())
        {
            Console.WriteLine("[NativeCameraiOS] No valid formats found, using first available");
            return allFormats.FirstOrDefault();
        }

        var formatDetails = availableFormats.Select(f => new FormatDetail
        {
            Format = f,
            StillDims = f.HighResolutionStillImageDimensions,
            VideoDims = (f.FormatDescription as CMVideoFormatDescription)?.Dimensions ?? new CMVideoDimensions(),
            StillPixels = f.HighResolutionStillImageDimensions.Width * f.HighResolutionStillImageDimensions.Height
        }).ToList();

        //Console.WriteLine($"[NativeCameraiOS] Available formats for quality {quality}:");
        //foreach (var detail in formatDetails)
        //{
        //    Console.WriteLine($"  Still: {detail.StillDims.Width}x{detail.StillDims.Height} ({detail.StillPixels:N0} pixels), Video: {detail.VideoDims.Width}x{detail.VideoDims.Height}");
        //}

        var selectedDetail = quality switch
        {
            CaptureQuality.Max => formatDetails.First(),
            CaptureQuality.High => SelectFormatByQuality(formatDetails, 0.2),
            CaptureQuality.Medium => SelectFormatByQuality(formatDetails, 0.5),
            CaptureQuality.Low => SelectFormatByQuality(formatDetails, 0.8),
            CaptureQuality.Preview => SelectPreviewFormat(formatDetails),
            CaptureQuality.Manual => GetManualFormatDetail(formatDetails, FormsControl.PhotoFormatIndex),
            _ => formatDetails.First()
        };

        Console.WriteLine($"[NativeCameraiOS] Selected format: Still {selectedDetail.StillDims.Width}x{selectedDetail.StillDims.Height}, Video {selectedDetail.VideoDims.Width}x{selectedDetail.VideoDims.Height} for quality {quality}");

        return selectedDetail.Format;
    }

    /// <summary>
    /// Select format by quality percentile
    /// </summary>
    /// <param name="formatDetails"></param>
    /// <param name="percentile"></param>
    /// <returns></returns>
    private FormatDetail SelectFormatByQuality(List<FormatDetail> formatDetails, double percentile)
    {
        var index = (int)(formatDetails.Count * percentile);
        var result = formatDetails.Skip(index).FirstOrDefault();
        return result.Format != null ? result : formatDetails.First();
    }

    /// <summary>
    /// Select optimal format for preview quality
    /// </summary>
    /// <param name="formatDetails"></param>
    /// <returns></returns>
    private FormatDetail SelectPreviewFormat(List<FormatDetail> formatDetails)
    {
        // For preview, prioritize reasonable video dimensions for smooth performance
        var videoPixelLimit = 1920 * 1080; // Max 1080p video for good performance

        var result = formatDetails
            .Where(d => d.VideoDims.Width * d.VideoDims.Height <= videoPixelLimit)
            .Where(d => d.StillDims.Width >= 1920 && d.StillDims.Height >= 1080) // Still want decent still quality
            .OrderByDescending(d => d.VideoDims.Width * d.VideoDims.Height) // Pick the best video quality within limit
            .FirstOrDefault();

        // Fallback: if no format meets both criteria, pick smallest video dimensions
        if (result.Format == null)
        {
            result = formatDetails
                .OrderBy(d => d.VideoDims.Width * d.VideoDims.Height)
                .First();
        }

        return result;
    }

    /// <summary>
    /// Select format whose video dimensions aspect ratio best matches the intended video format
    /// based on VideoQuality/VideoFormatIndex.
    /// </summary>
    private AVCaptureDeviceFormat SelectFormatForVideoAspect(List<AVCaptureDeviceFormat> allFormats)
    {
        var availableFormats = GetFilteredFormats(allFormats);
        if (!availableFormats.Any())
        {
            Console.WriteLine("[NativeCameraiOS] No valid formats found for video aspect selection, using first available");
            return allFormats.FirstOrDefault();
        }

        var formatDetails = availableFormats.Select(f => new FormatDetail
        {
            Format = f,
            StillDims = f.HighResolutionStillImageDimensions,
            VideoDims = (f.FormatDescription as CMVideoFormatDescription)?.Dimensions ?? new CMVideoDimensions(),
            StillPixels = f.HighResolutionStillImageDimensions.Width * f.HighResolutionStillImageDimensions.Height
        }).ToList();

        // Determine target video dimensions from current VideoQuality settings
        int targetW = 1920, targetH = 1080;
        try
        {
            var (_, settings) = GetVideoPresetAndSettings(FormsControl.VideoQuality);
            if (settings != null)
            {
                targetW = ((NSNumber)settings[AVVideo.WidthKey])?.Int32Value ?? targetW;
                targetH = ((NSNumber)settings[AVVideo.HeightKey])?.Int32Value ?? targetH;
            }
            else if (FormsControl.VideoQuality == VideoQuality.Manual)
            {
                var predefined = GetPredefinedVideoFormats();
                if (FormsControl.VideoFormatIndex >= 0 && FormsControl.VideoFormatIndex < predefined.Count)
                {
                    var sel = predefined[FormsControl.VideoFormatIndex];
                    targetW = sel.Width;
                    targetH = sel.Height;
                }
            }
        }
        catch { }

        double targetAR = targetH > 0 ? (double)targetW / targetH : 16.0 / 9.0;

        // Determine target FPS
        double targetFps = 30.0;
        if (FormsControl.VideoQuality == VideoQuality.High || FormsControl.VideoQuality == VideoQuality.Ultra)
        {
            targetFps = 60.0;
        }

        var best = formatDetails
            .Select(d => new { 
                d, 
                ar = d.VideoDims.Height > 0 ? (double)d.VideoDims.Width / d.VideoDims.Height : 0.0,
                maxFps = GetMaxFps(d.Format),
                supportsTargetFps = SupportsFps(d.Format, targetFps)
            })
            .OrderBy(x => Math.Abs(x.ar - targetAR)) // 1. Aspect Ratio
            .ThenByDescending(x => x.supportsTargetFps) // 2. Must support target FPS
            .ThenBy(x => Math.Abs((x.d.VideoDims.Width * x.d.VideoDims.Height) - (targetW * targetH))) // 3. Closest Resolution to Target
            .ThenBy(x => Math.Abs(x.maxFps - targetFps)) // 4. Closest Max FPS (prefer 60 over 240 for 60 target)
            .FirstOrDefault();

        var chosen = best?.d.Format ?? availableFormats.First();
        Debug.WriteLine($"[NativeCameraiOS] Selected format for video aspect {targetW}x{targetH}: Video {best?.d.VideoDims.Width}x{best?.d.VideoDims.Height} @ {best?.maxFps}fps (Supports {targetFps}: {best?.supportsTargetFps})");
        return chosen;
    }

    private bool SupportsFps(AVCaptureDeviceFormat format, double targetFps)
    {
        foreach(var range in format.VideoSupportedFrameRateRanges)
        {
            // Check if targetFps is within range (with small epsilon for float comparison)
            if (range.MinFrameRate <= targetFps + 0.1 && range.MaxFrameRate >= targetFps - 0.1)
                return true;
        }
        return false;
    }

    private double GetMaxFps(AVCaptureDeviceFormat format)
    {
        double max = 0;
        foreach(var range in format.VideoSupportedFrameRateRanges)
        {
            if (range.MaxFrameRate > max) max = range.MaxFrameRate;
        }
        return max;
    }

    /// <summary>
    /// Get manual format by index from StillFormats array
    /// </summary>
    /// <param name="formatDetails"></param>
    /// <param name="formatIndex"></param>
    /// <returns></returns>
    private FormatDetail GetManualFormatDetail(List<FormatDetail> formatDetails, int formatIndex)
    {
        if (formatIndex < 0 || formatIndex >= StillFormats.Count)
        {
            Console.WriteLine($"[NativeCameraiOS] Invalid PhotoFormatIndex {formatIndex}, using Max quality");
            return formatDetails.First();
        }

        // Get the desired still resolution from StillFormats
        var desiredFormat = StillFormats[formatIndex];
        Debug.WriteLine($"[NativeCameraiOS] Manual selection: looking for still format {desiredFormat.Width}x{desiredFormat.Height}");

        // Find all formats that match the desired still resolution
        // Note: formatDetails already contains only formats with acceptable video dimensions from GetFilteredFormats
        var matchingFormats = formatDetails
            .Where(d => d.StillDims.Width == desiredFormat.Width && d.StillDims.Height == desiredFormat.Height)
            .ToList();

        if (!matchingFormats.Any())
        {
            Console.WriteLine($"[NativeCameraiOS] No formats found with still resolution {desiredFormat.Width}x{desiredFormat.Height} and acceptable video dimensions");
            Console.WriteLine($"[NativeCameraiOS] Available formats all have acceptable video dimensions, selecting best still quality");
            return formatDetails.First(); // Best still quality with acceptable video
        }

        // All matching formats already have acceptable video dimensions, so pick the best one
        // Prefer video resolution closest to 720p for optimal performance
        var preferredVideoPixels = 1280 * 720;
        var selectedFormat = matchingFormats
            .OrderBy(f => Math.Abs((f.VideoDims.Width * f.VideoDims.Height) - preferredVideoPixels))
            .First();

        Console.WriteLine($"[NativeCameraiOS] Manual selection: selected still {selectedFormat.StillDims.Width}x{selectedFormat.StillDims.Height}, video {selectedFormat.VideoDims.Width}x{selectedFormat.VideoDims.Height}");

        return selectedFormat;
    }

    #endregion

    #region INativeCamera Implementation

    public void Start()
    {
        if (State == CameraProcessorState.Enabled && _session.Running)
            return;

        lock (lockSetup)
        {
            try
            {
                Setup();

                _session.StartRunning();
                State = CameraProcessorState.Enabled;

                // Apply current flash modes after session starts
                ApplyFlashMode();

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
        {
            State = CameraProcessorState.None;
            return; //avoid spam
        }

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
        if (!_flashSupported || _deviceInput?.Device == null)
            return;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                if (_deviceInput.Device.HasTorch)
                {
                    switch (_flashMode)
                    {
                        case FlashMode.Off:
                            _deviceInput.Device.TorchMode = AVCaptureTorchMode.Off;
                            break;
                        case FlashMode.On:
                            _deviceInput.Device.TorchMode = AVCaptureTorchMode.On;
                            break;
                        case FlashMode.Strobe:
                            // Future implementation for strobe mode
                            // For now, treat as On
                            _deviceInput.Device.TorchMode = AVCaptureTorchMode.On;
                            break;
                    }
                }
            }
            finally
            {
                _deviceInput.Device.UnlockForConfiguration();
            }
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
        return _flashSupported; // iOS supports auto flash when flash is available
    }

    private void SetFlashModeForCapture()
    {
        if (!_flashSupported || _deviceInput?.Device == null)
            return;

        NSError error;
        if (_deviceInput.Device.LockForConfiguration(out error))
        {
            try
            {
                if (_deviceInput.Device.HasFlash)
                {
                    switch (_captureFlashMode)
                    {
                        case CaptureFlashMode.Off:
                            _deviceInput.Device.FlashMode = AVCaptureFlashMode.Off;
                            break;
                        case CaptureFlashMode.Auto:
                            _deviceInput.Device.FlashMode = AVCaptureFlashMode.Auto;
                            break;
                        case CaptureFlashMode.On:
                            _deviceInput.Device.FlashMode = AVCaptureFlashMode.On;
                            break;
                    }
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
    /// Gets the currently selected capture format
    /// </summary>
    /// <returns>Current capture format or null if not available</returns>
    public CaptureFormat GetCurrentCaptureFormat()
    {
        try
        {
            if (_deviceInput?.Device?.ActiveFormat != null)
            {
                var activeFormat = _deviceInput.Device.ActiveFormat;
                var stillDimensions = activeFormat.HighResolutionStillImageDimensions;

                return new CaptureFormat
                {
                    Width = (int)stillDimensions.Width,
                    Height = (int)stillDimensions.Height,
                    FormatId = $"ios_{_deviceInput.Device.UniqueID}_{stillDimensions.Width}x{stillDimensions.Height}"
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NativeCameraiOS] GetCurrentCaptureFormat error: {ex.Message}");
        }

        return null;
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

    /// <summary>
    /// Handles bitmap orientation for still captures with proper dimension swapping for rotations.
    /// Combines sensor orientation with device rotation to produce correctly oriented output.
    /// </summary>
    /// <param name="bitmap">Source bitmap from camera sensor (in sensor's natural orientation)</param>
    /// <param name="sensor">Sensor rotation relative to device (CurrentRotation)</param>
    /// <param name="deviceRotation">Physical device rotation in degrees (0, 90, 180, 270)</param>
    /// <param name="flip">Whether to flip horizontally (for selfie camera)</param>
    /// <returns>Rotated bitmap with correct dimensions for user processing</returns>
    public SKBitmap HandleOrientationForStillCapture(SKBitmap bitmap, double sensor, int deviceRotation, bool flip)
    {
        // Calculate final rotation: device rotation minus sensor offset
        // Sensor tells us how raw image is rotated from device portrait
        // deviceRotation tells us current device orientation
        // We subtract to align image with current device orientation
        var finalRotation = (deviceRotation - (int)sensor + 360) % 360;

        // Portrait orientations need an additional 180° flip for correct orientation
        if (deviceRotation == 0 || deviceRotation == 180)
        {
            finalRotation = (finalRotation + 180) % 360;
        }

        Debug.WriteLine($"[STILL CAPTURE] sensor: {sensor}°, deviceRotation: {deviceRotation}°, finalRotation: {finalRotation}°, isSelfie: {flip}");

        SKBitmap rotated;
        switch (finalRotation)
        {
            case 180: //iphone landscape on left side
                if (flip)
                {
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(0, bitmap.Height);
                        surface.Scale(1, -1);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                }
                return bitmap;

            case 270: //iphone portrait upside down
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(0, rotated.Height);
                    surface.RotateDegrees(270);
                    if (flip)
                    {
                        surface.Scale(1, -1);
                        surface.Translate(0, -bitmap.Height);
                    }
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            case 90: //iphone portrait
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(rotated.Width, 0);
                    surface.RotateDegrees(90);
                    if (flip)
                    {
                        surface.Scale(1, -1);
                        surface.Translate(0, -bitmap.Height);
                    }
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            case 0: //iphone landscape on right side
            case 360: //cant happen?
                rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                if (!flip)
                {
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(0, bitmap.Height);
                        surface.Scale(1, -1);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                }
                else
                {
                    using (var surface = new SKCanvas(rotated)) //mirror X
                    {
                        surface.Translate(bitmap.Width, 0);
                        surface.Scale(-1, 1);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                }

            default:
                Debug.WriteLine($"[STILL CAPTURE] Unexpected rotation {finalRotation}°, returning original");
                return bitmap;
        }
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

                // Set flash mode for capture
                SetFlashModeForCapture();

                var cameraPosition = FormsControl.Facing == CameraPosition.Selfie
                    ? AVCaptureDevicePosition.Front
                    : AVCaptureDevicePosition.Back;
                var deviceRotation = FormsControl.DeviceRotation;

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

                using var uiImage = UIImage.LoadFromData(jpegData);
                using var rawImage = uiImage.ToSKImage();

                //using var rawImage = SKImage.FromPixels(info, pinnedPtr, _latestRawFrame.BytesPerRow);

                // Apply rotation if needed
                using var bitmap = SKBitmap.FromImage(rawImage);
                using var skBitmap = HandleOrientationForStillCapture(bitmap, (double)CurrentRotation, deviceRotation, FormsControl.Facing == CameraPosition.Selfie);

                var skImage = SKImage.FromBitmap(skBitmap);

                var newExif = 1;

                Debug.WriteLine($"[CAPTURED] {cameraPosition}  exif {orientation} => {newExif} for {FormsControl.DeviceRotation}, {CurrentRotation}");

                var capturedImage = new CapturedImage()
                {
                    Facing = FormsControl.Facing,
                    Time = DateTime.UtcNow,
                    Image = skImage,
                    Rotation = 0, //we already applied rotation
                    Meta = Metadata.CreateMetadataFromProperties(props, metaData)
                };

                capturedImage.Meta.Orientation = newExif;
                FormsControl.CameraDevice.Meta.Orientation = newExif;

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


    public (SKImage Image, int Rotation, bool Flip) GetRawPreviewImage()
    {
        lock (_lockRawFrame)
        {
            if (_latestRawFrame == null)
                return (null, 0, false);

            try
            {
                var info = new SKImageInfo(_latestRawFrame.Width, _latestRawFrame.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                
                // Create a copy of the pixel data to be safe and independent
                // This is faster than GetPreviewImage which does rotation/drawing
                using var data = SKData.CreateCopy(_latestRawFrame.PixelData);
                var image = SKImage.FromPixels(info, data, _latestRawFrame.BytesPerRow);
                
                return (image, (int)_latestRawFrame.CurrentRotation, _latestRawFrame.Facing == CameraPosition.Selfie);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[NativeCameraiOS] GetRawPreviewImage error: {e}");
                return (null, 0, false);
            }
        }
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
                    using var rotatedBitmap = HandleOrientationForPreview(bitmap, (double)_latestRawFrame.CurrentRotation, _latestRawFrame.Facing == CameraPosition.Selfie);
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

    /// <summary>
    /// Saves JPEG stream to iOS Photos gallery and returns the assets-library URL
    /// </summary>
    /// <param name="stream">Image stream</param>
    /// <param name="filename">Original filename</param>
    /// <param name="cameraSavedRotation">Camera rotation</param>
    /// <param name="meta">Image metadata</param>
    /// <param name="album">Album name (optional)</param>
    /// <returns>assets-library:// URL to reference the saved photo</returns>
    public async Task<string> SaveJpgStreamToGallery(Stream stream, string filename, Metadata meta, string album)
    {
        try
        {
            var data = NSData.FromStream(stream);

            bool complete = false;
            string resultUrl = null;
            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var options = new PHAssetResourceCreationOptions
                {
                    OriginalFilename = filename
                };
                var creationRequest = PHAssetCreationRequest.CreationRequestForAsset();
                creationRequest.AddResource(PHAssetResourceType.Photo, data, options);

                // Add to specific album if provided
                if (!string.IsNullOrEmpty(album))
                {
                    // Find or create album and add asset to it
                    var albumCollection = FindOrCreateAlbum(album);
                    if (albumCollection != null)
                    {
                        var albumChangeRequest = PHAssetCollectionChangeRequest.ChangeRequest(albumCollection);
                        albumChangeRequest?.AddAssets(new PHObject[] { creationRequest.PlaceholderForCreatedAsset });
                    }
                }

                // Get the placeholder for the asset being created
                var placeholder = creationRequest.PlaceholderForCreatedAsset;
                if (placeholder != null)
                {
                    // Generate assets-library URL using the local identifier
                    var assetIdentifier = placeholder.LocalIdentifier;
                    resultUrl = $"assets-library://asset/asset.JPG?id={assetIdentifier}";
                }
            }, (success, error) =>
            {
                if (!success)
                {
                    Console.WriteLine($"SaveJpgStreamToGallery error: {error}");
                    resultUrl = null;
                }
                complete = true;
            });

            while (!complete)
            {
                await Task.Delay(10);
            }

            return resultUrl;
        }
        catch (Exception e)
        {
            Console.WriteLine($"SaveJpgStreamToGallery error: {e}");
            return null;
        }
    }

    /// <summary>
    /// Finds existing album or creates new one
    /// </summary>
    /// <param name="albumName">Album name</param>
    /// <returns>PHAssetCollection for the album</returns>
    private PHAssetCollection FindOrCreateAlbum(string albumName)
    {
        try
        {
            // First try to find existing album
            var fetchOptions = new PHFetchOptions();
            fetchOptions.Predicate = NSPredicate.FromFormat($"title = '{albumName}'");
            var existingAlbums = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album, PHAssetCollectionSubtype.Any, fetchOptions);

            if (existingAlbums.Count > 0)
            {
                return existingAlbums.FirstObject as PHAssetCollection;
            }

            // Create new album if not found
            string albumIdentifier = null;
            bool createComplete = false;

            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var createRequest = PHAssetCollectionChangeRequest.CreateAssetCollection(albumName);
                albumIdentifier = createRequest.PlaceholderForCreatedAssetCollection.LocalIdentifier;
            }, (success, error) =>
            {
                if (!success)
                {
                    Console.WriteLine($"Failed to create album '{albumName}': {error}");
                }
                createComplete = true;
            });

            // Wait for creation to complete
            while (!createComplete)
            {
                Task.Delay(10).Wait();
            }

            if (!string.IsNullOrEmpty(albumIdentifier))
            {
                // Fetch the created album by identifier - need correct API here
                // For now, search by name again as workaround
                var newFetchOptions = new PHFetchOptions();
                newFetchOptions.Predicate = NSPredicate.FromFormat($"title = '{albumName}'");
                var newAlbums = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album, PHAssetCollectionSubtype.Any, newFetchOptions);
                return newAlbums.FirstObject as PHAssetCollection;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding/creating album '{albumName}': {ex}");
        }

        return null;
    }
    /// <summary>
    /// Trims identifier to remove unwanted suffixes
    /// </summary>
    /// <param name="identifier">Full identifier</param>
    /// <returns>Trimmed identifier</returns>
    private static string TrimIdentifier(string identifier)
    {
        var index = identifier.IndexOf('/');
        return index >= 0 ? identifier.Substring(0, index) : identifier;
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

    // Buffer pool to reduce GC pressure
    private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _pixelBufferPool = new();

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
        //if (_processedFrameCount % 300 == 0)
        //{
        //    System.Diagnostics.Debug.WriteLine($"[NativeCameraiOS] Frame stats - Processed: {_processedFrameCount}, Skipped: {_skippedFrameCount}");
        //}

        bool hasFrame = false;
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

                    //unit.Id = name;
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
                
                // Reuse buffer from pool if available
                byte[] pixelData = null;
                if (_pixelBufferPool.TryDequeue(out var pooledBuffer) && pooledBuffer.Length == dataSize)
                {
                    pixelData = pooledBuffer;
                }
                else
                {
                    pixelData = new byte[dataSize];
                }
                
                System.Runtime.InteropServices.Marshal.Copy(baseAddress, pixelData, 0, dataSize);

                var time = DateTime.UtcNow;

                // Store raw frame data for SKImage creation on main thread
                var rawFrame = new RawFrameData
                {
                    Width = width,
                    Height = height,
                    BytesPerRow = bytesPerRow,
                    Time = time,
                    CurrentRotation = CurrentRotation,
                    Facing = FormsControl.Facing,
                    Orientation = (int)CurrentRotation,
                    PixelData = pixelData
                };

                SetRawFrame(rawFrame);
                hasFrame = true;
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
            // Recycle old buffer if it exists
            if (_latestRawFrame != null && _latestRawFrame.PixelData != null)
            {
                _pixelBufferPool.Enqueue(_latestRawFrame.PixelData);
            }
            
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



    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Orientation Handling

    /// <summary>
    /// This rotates the preview frame for providing a correctly-rotated preview.
    /// Handles bitmap orientation based on sensor rotation and optional horizontal flip.
    /// </summary>
    /// <param name="bitmap"></param>
    /// <param name="sensor"></param>
    /// <returns></returns>
    public SKBitmap HandleOrientationForPreview(SKBitmap bitmap, double sensor, bool flip)
    {
        SKBitmap rotated;
        switch (sensor)
        {
            case 180:
                rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.RotateDegrees(180);
                    if (flip)
                    {
                        surface.Scale(1, -1);
                    }
                    surface.Translate(-bitmap.Width / 2.0f, -bitmap.Height / 2.0f);
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            case 270:
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(0, rotated.Height);
                    surface.RotateDegrees(270);
                    if (flip)
                    {
                        surface.Scale(1, -1);
                        surface.Translate(0, -bitmap.Height);
                    }
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            case 90:
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var surface = new SKCanvas(rotated))
                {
                    surface.Translate(rotated.Width, 0);
                    surface.RotateDegrees(90);
                    if (flip)
                    {
                        surface.Scale(1, -1);
                        surface.Translate(0, -bitmap.Height);
                    }
                    surface.DrawBitmap(bitmap, 0, 0);
                }
                return rotated;

            default:
                if (flip)
                {
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(0, bitmap.Height);
                        surface.Scale(1, -1);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                }
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
            // Stop video recording if active
            if (_isRecordingVideo)
            {
                try
                {
                    _movieFileOutput?.StopRecording();
                }
                catch { }
                _isRecordingVideo = false;
            }

            _progressTimer?.Invalidate();
            _progressTimer = null;
            CleanupMovieFileOutput();

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
        }

        base.Dispose(disposing);
    }

    #region VIDEO RECORDING

    /// <summary>
    /// Gets the currently selected video format based on user settings.
    /// Returns format from actual camera capabilities, matching Windows/Android pattern.
    /// </summary>
    /// <returns>Current video format or null if not available</returns>
    public VideoFormat GetCurrentVideoFormat()
    {
        try
        {
            // 1. Try to return actual active format if camera is running
            if (CaptureDevice?.ActiveFormat != null)
            {
                int activeFps = 30;
                if (CaptureDevice.ActiveVideoMinFrameDuration.Value > 0)
                {
                    activeFps = (int)(CaptureDevice.ActiveVideoMinFrameDuration.TimeScale / CaptureDevice.ActiveVideoMinFrameDuration.Value);
                }

                var desc = CaptureDevice.ActiveFormat.FormatDescription as CMVideoFormatDescription;
                if (desc != null)
                {
                    var dims = desc.Dimensions;
                    Debug.WriteLine($"[NativeCamera.Apple] GetCurrentVideoFormat: Returning ACTIVE format {dims.Width}x{dims.Height}@{activeFps}");
                    return new VideoFormat
                    {
                        Width = dims.Width,
                        Height = dims.Height,
                        FrameRate = activeFps,
                        Codec = "H.264",
                        BitRate = 8_000_000, // Estimate or placeholder
                        FormatId = $"Active_{dims.Width}x{dims.Height}@{activeFps}"
                    };
                }
            }

            var quality = FormsControl.VideoQuality;

            // Manual mode: use VideoFormatIndex to select from predefined formats
            if (quality == VideoQuality.Manual)
            {
                var formats = GetPredefinedVideoFormats();
                var idx = FormsControl.VideoFormatIndex;
                if (idx >= 0 && idx < formats.Count)
                {
                    Debug.WriteLine($"[NativeCamera.Apple] GetCurrentVideoFormat: Manual mode, index {idx} -> {formats[idx].Width}x{formats[idx].Height}@{formats[idx].FrameRate}");
                    return formats[idx];
                }

                Debug.WriteLine($"[NativeCamera.Apple] GetCurrentVideoFormat: Manual mode but invalid index {idx}, using first format");
                return formats.FirstOrDefault() ?? new VideoFormat { Width = 1920, Height = 1080, FrameRate = 30, Codec = "H.264", BitRate = 8_000_000, FormatId = "1080p30" };
            }

            // Preset modes: find best match from actual available formats
            var availableFormats = GetPredefinedVideoFormats();

            var targetResolution = quality switch
            {
                VideoQuality.Low => (width: 640, height: 480),
                VideoQuality.Standard => (width: 1280, height: 720),
                VideoQuality.High => (width: 1920, height: 1080),
                VideoQuality.Ultra => (width: 3840, height: 2160),
                _ => (width: 1920, height: 1080)
            };

            // Determine target FPS
            int targetFps = 30;
            if (quality == VideoQuality.High || quality == VideoQuality.Ultra)
            {
                targetFps = 60;
            }

            var bestFormat = availableFormats
                .OrderBy(f => Math.Abs((f.Width * f.Height) - (targetResolution.width * targetResolution.height)))
                .ThenBy(f => Math.Abs(f.FrameRate - targetFps)) // Prefer closest FPS (60 vs 240 -> 60 wins)
                .ThenByDescending(f => f.FrameRate)
                .FirstOrDefault();

            if (bestFormat != null)
            {
                // Clamp reported FPS to target if the format supports higher
                // This reflects that we will limit the FPS in ConfigureSession
                var reportedFps = bestFormat.FrameRate;
                if (reportedFps > targetFps)
                {
                    reportedFps = targetFps;
                }

                Debug.WriteLine($"[NativeCamera.Apple] GetCurrentVideoFormat: {quality} quality -> {bestFormat.Width}x{bestFormat.Height}@{reportedFps} (Base: {bestFormat.FrameRate})");
                
                // Return a copy with adjusted FPS
                return new VideoFormat 
                { 
                    Width = bestFormat.Width, 
                    Height = bestFormat.Height, 
                    FrameRate = reportedFps, 
                    Codec = bestFormat.Codec, 
                    BitRate = bestFormat.BitRate, 
                    FormatId = bestFormat.FormatId 
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Error getting current video format: {ex.Message}");
        }

        // Final fallback
        Debug.WriteLine($"[NativeCamera.Apple] GetCurrentVideoFormat: Using fallback default 1080p30");
        return new VideoFormat { Width = 1920, Height = 1080, FrameRate = 30, Codec = "H.264", BitRate = 8_000_000, FormatId = "1080p30" };
    }

    /// <summary>
    /// Setup movie file output for video recording
    /// </summary>
    private async Task SetupMovieFileOutput()
    {
        if (_movieFileOutput != null)
            return; // Already set up

        _movieFileOutput = new AVCaptureMovieFileOutput();

        // Configure video settings based on current video quality
        ConfigureVideoSettings();

        _session.BeginConfiguration();

        // Add audio input if audio recording is enabled
        if (_recordAudio)
        {
            await SetupAudioInput();
        }

        // Add movie file output
        if (_session.CanAddOutput(_movieFileOutput))
        {
            _session.AddOutput(_movieFileOutput);
            Debug.WriteLine("[NativeCamera.Apple] Movie file output added to session");
        }
        else
        {
            _session.CommitConfiguration();
            throw new InvalidOperationException("Cannot add movie file output to capture session");
        }

        _session.CommitConfiguration();
    }

    /// <summary>
    /// Setup audio input for video recording
    /// </summary>
    private async Task SetupAudioInput()
    {
        try
        {
            // Check if audio input already exists
            if (_audioInput != null)
            {
                Debug.WriteLine("[NativeCamera.Apple] Audio input already set up");
                return;
            }

            // Get default audio capture device
            var audioDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Audio);
            if (audioDevice == null)
            {
                Debug.WriteLine("[NativeCamera.Apple] No audio capture device available");
                return;
            }

            // Create audio input
            _audioInput = AVCaptureDeviceInput.FromDevice(audioDevice, out var error);
            if (_audioInput == null || error != null)
            {
                Debug.WriteLine($"[NativeCamera.Apple] Failed to create audio input: {error?.LocalizedDescription}");
                return;
            }

            // Add audio input to session
            if (_session.CanAddInput(_audioInput))
            {
                _session.AddInput(_audioInput);
                Debug.WriteLine("[NativeCamera.Apple] Audio input added to session");
            }
            else
            {
                Debug.WriteLine("[NativeCamera.Apple] Cannot add audio input to session");
                _audioInput?.Dispose();
                _audioInput = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Error setting up audio input: {ex.Message}");
            _audioInput?.Dispose();
            _audioInput = null;
        }
    }

    /// <summary>
    /// Configure video recording settings based on current video quality
    /// </summary>
    private void ConfigureVideoSettings()
    {
        if (_movieFileOutput == null)
            return;

        var quality = FormsControl.VideoQuality;

        // Get video formats and settings
        var (preset, settings) = GetVideoPresetAndSettings(quality);

        // Apply video settings if available - let AVCaptureMovieFileOutput use defaults
        // The movie file output will automatically configure appropriate settings

        Debug.WriteLine($"[NativeCamera.Apple] Configured video settings for quality: {quality}");
    }

    /// <summary>
    /// Get video preset and settings based on video quality.
    /// For presets: picks best available format from camera instead of hardcoded resolutions.
    /// </summary>
    private (NSString preset, NSDictionary settings) GetVideoPresetAndSettings(VideoQuality quality)
    {
        // Helper to pick the first supported preset
        NSString ChoosePreset(params NSString[] presets)
        {
            foreach (var p in presets)
            {
                try { if (_session != null && _session.CanSetSessionPreset(p)) return p; } catch { }
            }
            return AVCaptureSession.PresetHigh;
        }

        if (quality == VideoQuality.Manual)
        {
            return GetManualVideoSettings();
        }

        // For preset modes: find best match from actual available formats
        var availableFormats = GetPredefinedVideoFormats();

        // Target resolutions for each quality level
        var targetResolution = quality switch
        {
            VideoQuality.Low => (width: 640, height: 480),       // 480p
            VideoQuality.Standard => (width: 1280, height: 720),  // 720p
            VideoQuality.High => (width: 1920, height: 1080),     // 1080p
            VideoQuality.Ultra => (width: 3840, height: 2160),    // 4K
            _ => (width: 1920, height: 1080)
        };

        // Find closest match from available formats
        var bestFormat = availableFormats
            .OrderBy(f => Math.Abs((f.Width * f.Height) - (targetResolution.width * targetResolution.height)))
            .ThenByDescending(f => f.FrameRate)
            .FirstOrDefault();

        if (bestFormat != null)
        {
            var settings = CreateVideoSettings(bestFormat.Width, bestFormat.Height, (int)bestFormat.FrameRate);

            // Pick appropriate preset based on actual resolution
            NSString preset = (bestFormat.Width, bestFormat.Height) switch
            {
                (3840, 2160) => ChoosePreset(AVCaptureSession.Preset3840x2160, AVCaptureSession.PresetHigh),
                (1920, 1080) => ChoosePreset(AVCaptureSession.Preset1920x1080, AVCaptureSession.PresetHigh),
                (1280, 720) => ChoosePreset(AVCaptureSession.Preset1280x720, AVCaptureSession.PresetHigh),
                (640, 480) => ChoosePreset(AVCaptureSession.Preset640x480, AVCaptureSession.PresetHigh),
                _ => AVCaptureSession.PresetHigh
            };

            Debug.WriteLine($"[NativeCamera.Apple] {quality} quality -> matched to {bestFormat.Width}x{bestFormat.Height}@{bestFormat.FrameRate}fps");
            return (preset, settings);
        }

        // Fallback to hardcoded presets if no formats available
        Debug.WriteLine($"[NativeCamera.Apple] {quality} quality -> using fallback preset");
        return quality switch
        {
            VideoQuality.Low => (ChoosePreset(AVCaptureSession.Preset640x480, AVCaptureSession.PresetHigh), CreateVideoSettings(640, 480, 30)),
            VideoQuality.Standard => (ChoosePreset(AVCaptureSession.Preset1280x720, AVCaptureSession.PresetHigh), CreateVideoSettings(1280, 720, 30)),
            VideoQuality.High => (ChoosePreset(AVCaptureSession.Preset1920x1080, AVCaptureSession.PresetHigh), CreateVideoSettings(1920, 1080, 30)),
            VideoQuality.Ultra => (ChoosePreset(AVCaptureSession.Preset3840x2160, AVCaptureSession.PresetHigh), CreateVideoSettings(3840, 2160, 30)),
            _ => (ChoosePreset(AVCaptureSession.Preset1920x1080, AVCaptureSession.PresetHigh), CreateVideoSettings(1920, 1080, 30))
        };
    }

    /// <summary>
    /// Create video settings dictionary
    /// </summary>
    private NSDictionary CreateVideoSettings(int width, int height, int frameRate)
    {
        var compressionProperties = NSDictionary.FromObjectsAndKeys(
            new NSObject[] { NSNumber.FromInt32(frameRate * 1000000) }, // Bitrate estimation
            new NSObject[] { AVVideo.AverageBitRateKey }
        );

        return NSDictionary.FromObjectsAndKeys(
            new NSObject[]
            {
                NSNumber.FromInt32(width),
                NSNumber.FromInt32(height),
                new NSString("avc1"),
                compressionProperties
            },
            new NSObject[]
            {
                AVVideo.WidthKey,
                AVVideo.HeightKey,
                AVVideo.CodecKey,
                AVVideo.CompressionPropertiesKey
            }
        );
    }

    /// <summary>
    /// Get manual video settings based on VideoFormatIndex
    /// </summary>
    private (NSString preset, NSDictionary settings) GetManualVideoSettings()
    {
        var formats = GetPredefinedVideoFormats();

        if (formats.Count > FormsControl.VideoFormatIndex)
        {
            var selectedFormat = formats[FormsControl.VideoFormatIndex];
            var settings = CreateVideoSettings(
                selectedFormat.Width,
                selectedFormat.Height,
                (int)selectedFormat.FrameRate);
            return (AVCaptureSession.PresetHigh, settings);
        }

        // Fallback to standard quality
        return (AVCaptureSession.PresetHigh, CreateVideoSettings(1920, 1080, 30));
    }

    /// <summary>
    /// Get video formats from ACTUAL camera device capabilities, not hardcoded values.
    /// Queries AVCaptureDevice.Formats to return what the hardware actually supports.
    /// </summary>
    public List<VideoFormat> GetPredefinedVideoFormats()
    {
        var formats = new List<VideoFormat>();

        try
        {
            var device = CaptureDevice;
            if (device?.Formats != null)
            {
                // Extract unique video resolutions from actual device formats
                var uniqueFormats = device.Formats
                    .Where(f => f.FormatDescription is CMVideoFormatDescription)
                    .Select(f =>
                    {
                        var desc = f.FormatDescription as CMVideoFormatDescription;
                        var dims = desc.Dimensions;

                        // Get max frame rate for this format
                        var maxFrameRate = 30; // Default
                        if (f.VideoSupportedFrameRateRanges?.Length > 0)
                        {
                            maxFrameRate = (int)Math.Round(f.VideoSupportedFrameRateRanges
                                .Max(r => r.MaxFrameRate));
                        }

                        return new
                        {
                            Width = (int)dims.Width,
                            Height = (int)dims.Height,
                            FPS = maxFrameRate,
                            Pixels = dims.Width * dims.Height
                        };
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

                Debug.WriteLine($"[NativeCamera.Apple] GetPredefinedVideoFormats: Found {formats.Count} actual formats from camera");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] GetPredefinedVideoFormats error: {ex.Message}");
        }

        // Fallback if no formats found: return safe defaults
        if (formats.Count == 0)
        {
            Debug.WriteLine("[NativeCamera.Apple] GetPredefinedVideoFormats: No camera formats available, using fallback defaults");
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
    /// Clean up movie file output resources
    /// </summary>
    private void CleanupMovieFileOutput()
    {
        try
        {
            if (_session != null)
            {
                _session.BeginConfiguration();

                if (_movieFileOutput != null)
                {
                    _session.RemoveOutput(_movieFileOutput);
                }

                // Clean up audio input if it exists
                if (_audioInput != null)
                {
                    _session.RemoveInput(_audioInput);
                    _audioInput?.Dispose();
                    _audioInput = null;
                    Debug.WriteLine("[NativeCamera.Apple] Audio input removed and disposed");
                }

                _session.CommitConfiguration();
            }

            _movieFileOutput?.Dispose();
            _movieFileOutput = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Error cleaning up movie file output: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts video recording
    /// </summary>
    public async Task StartVideoRecording()
    {
        if (_isRecordingVideo || _session == null)
            return;

        try
        {
            Debug.WriteLine("[NativeCamera.Apple] Starting video recording...");

            // Setup movie file output if not already created
            await SetupMovieFileOutput();

            // Apply target session preset based on VideoQuality (with fallbacks)
            try
            {
                var (preset, _) = GetVideoPresetAndSettings(FormsControl.VideoQuality);
                if (preset != null && _session.CanSetSessionPreset(preset))
                {
                    _session.BeginConfiguration();
                    _session.SessionPreset = preset;
                    _session.CommitConfiguration();
                    Debug.WriteLine($"[NativeCamera.Apple] Using session preset: {preset}");
                }
            }
            catch { }

            // Create temporary file URL for video recording
            var fileName = $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mov";
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var videoPath = Path.Combine(documentsPath, fileName);
            _currentVideoUrl = NSUrl.FromFilename(videoPath);

            // Set video orientation on the capture connection to ensure correct playback orientation
            var videoConnection = _movieFileOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant());
            if (videoConnection != null && videoConnection.SupportsVideoOrientation)
            {
                // Map device rotation to AVCaptureVideoOrientation
                var orientation = DeviceRotationToVideoOrientation(FormsControl.DeviceRotation);
                videoConnection.VideoOrientation = orientation;
                Debug.WriteLine($"[NativeCamera.Apple] Set video orientation to: {orientation} (DeviceRotation: {FormsControl.DeviceRotation})");
            }

            // Start recording
            _movieFileOutput.StartRecordingToOutputFile(_currentVideoUrl, this);

            _isRecordingVideo = true;
            _recordingStartTime = DateTime.Now;

            // Start progress timer (fire every second)
            _progressTimer = NSTimer.CreateRepeatingScheduledTimer(1.0, timer =>
            {
                if (_isRecordingVideo)
                {
                    var elapsed = DateTime.Now - _recordingStartTime;
                    VideoRecordingProgress?.Invoke(elapsed);
                }
            });

            Debug.WriteLine("[NativeCamera.Apple] Video recording started successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Failed to start video recording: {ex.Message}");
            _isRecordingVideo = false;
            CleanupMovieFileOutput();
            VideoRecordingFailed?.Invoke(ex);
        }
    }

    /// <summary>
    /// Converts device rotation (degrees) to AVCaptureVideoOrientation
    /// Selfie camera sensor is opposite to back camera, so landscape orientations are flipped
    /// </summary>
    private AVCaptureVideoOrientation DeviceRotationToVideoOrientation(int deviceRotation)
    {
        var normalizedRotation = deviceRotation % 360;
        if (normalizedRotation < 0)
            normalizedRotation += 360;

        var isSelfie = FormsControl?.Facing == CameraPosition.Selfie;

        return normalizedRotation switch
        {
            0 => AVCaptureVideoOrientation.Portrait,
            90 => isSelfie ? AVCaptureVideoOrientation.LandscapeLeft : AVCaptureVideoOrientation.LandscapeRight,
            180 => AVCaptureVideoOrientation.PortraitUpsideDown,
            270 => isSelfie ? AVCaptureVideoOrientation.LandscapeRight : AVCaptureVideoOrientation.LandscapeLeft,
            _ => AVCaptureVideoOrientation.Portrait
        };
    }

    /// <summary>
    /// Stops video recording
    /// </summary>
    public async Task StopVideoRecording()
    {
        if (!_isRecordingVideo || _movieFileOutput == null)
            return;

        try
        {
            Debug.WriteLine("[NativeCamera.Apple] Stopping video recording...");

            // Stop progress timer
            _progressTimer?.Invalidate();
            _progressTimer = null;

            // Stop recording (this will trigger FinishedRecording delegate method)
            _movieFileOutput.StopRecording();

            Debug.WriteLine("[NativeCamera.Apple] Video recording stop initiated");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Failed to stop video recording: {ex.Message}");
            _isRecordingVideo = false;
            CleanupMovieFileOutput();
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
            // Check if we have a session and device that supports video recording
            return _session != null &&
                   _deviceInput?.Device != null &&
                   _deviceInput.Device.HasMediaType(AVMediaTypes.Video);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Set whether to record audio with video
    /// </summary>
    /// <param name="recordAudio">True to record audio, false for silent video</param>
    public void SetRecordAudio(bool recordAudio)
    {
        _recordAudio = recordAudio;
        System.Diagnostics.Debug.WriteLine($"[NativeCamera.Apple] SetRecordAudio: {recordAudio}");
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
                Debug.WriteLine($"[NativeCamera.Apple] Video file not found: {videoFilePath}");
                return null;
            }

            var videoUrl = NSUrl.FromFilename(videoFilePath);
            var tcs = new TaskCompletionSource<string>();

            // Request photo library access
            var authorizationStatus = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly);
            if (authorizationStatus != PHAuthorizationStatus.Authorized)
            {
                Debug.WriteLine("[NativeCamera.Apple] Photo library access denied");
                return null;
            }

            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var request = PHAssetChangeRequest.FromVideo(videoUrl);

                // Set album if specified
                if (!string.IsNullOrEmpty(album))
                {
                    // Find or create album and add asset to it
                    var albumCollection = FindOrCreateAlbum(album);
                    if (albumCollection != null)
                    {
                        var albumChangeRequest = PHAssetCollectionChangeRequest.ChangeRequest(albumCollection);
                        albumChangeRequest?.AddAssets(new PHObject[] { request.PlaceholderForCreatedAsset });
                    }
                }
            },
            (success, error) =>
            {
                if (success)
                {
                    Debug.WriteLine($"[NativeCamera.Apple] Video saved to gallery successfully");
                    tcs.SetResult(videoFilePath); // iOS doesn't provide new path
                }
                else
                {
                    Debug.WriteLine($"[NativeCamera.Apple] Failed to save video: {error?.LocalizedDescription}");
                    tcs.SetResult(null);
                }
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Error saving video to gallery: {ex.Message}");
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

    /// <summary>
    /// Gets or sets whether pre-recording is enabled.
    /// </summary>
    public bool EnablePreRecording { get; set; }

    /// <summary>
    /// Gets or sets the duration of the pre-recording buffer.
    /// </summary>
    public TimeSpan PreRecordDuration { get; set; }

    #endregion

    #region IAVCaptureFileOutputRecordingDelegate Implementation

    /// <summary>
    /// Called when video recording finishes successfully or with an error
    /// </summary>
    [Export("captureOutput:didFinishRecordingToOutputFileAtURL:fromConnections:error:")]
    public void FinishedRecording(AVCaptureFileOutput captureOutput, NSUrl outputFileUrl, AVCaptureConnection[] connections, NSError error)
    {
        var recordingEndTime = DateTime.Now;
        var duration = recordingEndTime - _recordingStartTime;

        _isRecordingVideo = false;

        if (error != null)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Video recording failed: {error.LocalizedDescription}");
            CleanupMovieFileOutput();
            VideoRecordingFailed?.Invoke(new Exception(error.LocalizedDescription));
            return;
        }

        try
        {
            // Get file info
            var filePath = outputFileUrl.Path;
            var fileAttributes = NSFileManager.DefaultManager.GetAttributes(filePath, out var fileError);
            var fileSizeBytes = fileError == null ? (long)fileAttributes.Size : 0;

            Debug.WriteLine($"[NativeCamera.Apple] Video recording completed. Duration: {duration:mm\\:ss}, Size: {fileSizeBytes / (1024 * 1024):F1} MB");

            // Create captured video object
            var capturedVideo = new CapturedVideo
            {
                FilePath = filePath,
                Duration = duration,
                Format = GetCurrentVideoFormat(),
                Facing = FormsControl.Facing,
                Time = _recordingStartTime,
                FileSizeBytes = fileSizeBytes,
                Metadata = new Dictionary<string, object>
                {
                    { "Platform", "iOS/Mac" },
                    { "DeviceId", _deviceInput?.Device?.UniqueID ?? "Unknown" },
                    { "RecordingStartTime", _recordingStartTime },
                    { "RecordingEndTime", recordingEndTime }
                }
            };

            // Fire success event on main thread
            NSOperationQueue.MainQueue.AddOperation(() =>
            {
                VideoRecordingSuccess?.Invoke(capturedVideo);
            });

            Debug.WriteLine("[NativeCamera.Apple] Video recording completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NativeCamera.Apple] Error processing recorded video: {ex.Message}");
            VideoRecordingFailed?.Invoke(ex);
        }
        finally
        {
            CleanupMovieFileOutput();
        }
    }

    /// <summary>
    /// Called when video recording starts
    /// </summary>
    [Export("captureOutput:didStartRecordingToOutputFileAtURL:fromConnections:")]
    public void StartedRecording(AVCaptureFileOutput captureOutput, NSUrl outputFileUrl, AVCaptureConnection[] connections)
    {
        Debug.WriteLine("[NativeCamera.Apple] Video recording started successfully");
    }

    #endregion

    #endregion
}
#endif
