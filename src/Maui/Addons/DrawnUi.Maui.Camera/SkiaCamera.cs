global using DrawnUi.Draw;
global using SkiaSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Views;
using static DrawnUi.Camera.SkiaCamera;
using Color = Microsoft.Maui.Graphics.Color;

#if WINDOWS
using DrawnUi.Camera.Platforms.Windows;
#elif IOS || MACCATALYST
using AVFoundation;
using CoreMedia;
using Foundation;
#endif

namespace DrawnUi.Camera;

/// <summary>
/// SkiaCamera control with support for manual camera selection.
///
/// Basic usage:
/// - Set Facing to Default/Selfie for automatic camera selection
/// - Set Facing to Manual and CameraIndex for manual camera selection
///
/// Example:
/// var camera = new SkiaCamera { Facing = CameraPosition.Manual, CameraIndex = 2 };
/// var cameras = await camera.GetAvailableCamerasAsync();
/// </summary>
public partial class SkiaCamera : SkiaControl
{
    #region COMMON

    #region PROPERTIES


    public event EventHandler<bool> IsPreRecordingVideoChanged;
    public event EventHandler<bool> IsRecordingVideoChanged;
    public event EventHandler<bool> IsRecordingAudioOnlyChanged;

    /// <summary>
    /// Fired when audio sample is captured - both during preview and recording.
    /// Active when EnableAudioRecording=true and camera is running.
    /// Parameters: (byte[] data, int sampleRate, int bitsPerSample, int channels)
    /// </summary>
    public event Action<byte[], int, int, int> AudioSampleAvailable;

    /// <summary>
    /// Audio is available, use by writer etc, you can use to change the audio, apply gain etc.. Raises the AudioSampleAvailable event!
    /// </summary>
    protected virtual AudioSample OnAudioSampleAvailable(AudioSample sample)
    {
        AudioSampleAvailable?.Invoke(
            sample.Data,
            sample.SampleRate,
            sample.BytesPerSample * 8,
            sample.Channels);

        return sample;
    }

    protected virtual void SetIsRecordingVideo(bool isRecording)
    {
        if (IsRecording != isRecording)
        {
            IsRecording = isRecording;
            IsRecordingVideoChanged?.Invoke(this, isRecording);
        }
    }

    protected virtual void SetIsPreRecording(bool isPreRecording)
    {
        if (IsPreRecording != isPreRecording)
        {
            IsPreRecording = isPreRecording;
            IsPreRecordingVideoChanged?.Invoke(this, isPreRecording);
        }
    }

    protected virtual void SetIsRecordingAudioOnly(bool isRecording)
    {
        if (IsRecordingAudioOnly != isRecording)
        {
            IsRecordingAudioOnly = isRecording;
            IsRecordingAudioOnlyChanged?.Invoke(this, isRecording);
        }
    }

    #region VIDEO/AUDIO RECORDING PROPERTIES

    public static readonly BindableProperty IsRecordingProperty = BindableProperty.Create(
        nameof(IsRecording),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        BindingMode.OneWayToSource);

    /// <summary>
    /// Whether video/audio recording is currently active (read-only)
    /// </summary>
    public bool IsRecording
    {
        get { return (bool)GetValue(IsRecordingProperty); }
        private set { SetValue(IsRecordingProperty, value); }
    }

    public static readonly BindableProperty IsPreRecordingProperty = BindableProperty.Create(
        nameof(IsPreRecording),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        BindingMode.OneWayToSource);

    /// <summary>
    /// Whether currently recording to memory buffer only (pre-recording mode) (read-only).
    /// When true, frames are being recorded to memory buffer instead of file.
    /// These buffered frames will be prepended when file recording starts.
    /// </summary>
    public bool IsPreRecording
    {
        get { return (bool)GetValue(IsPreRecordingProperty); }
        private set { SetValue(IsPreRecordingProperty, value); }
    }

    public static readonly BindableProperty IsRecordingAudioOnlyProperty = BindableProperty.Create(
        nameof(IsRecordingAudioOnly),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        BindingMode.OneWayToSource);

    /// <summary>
    /// Whether audio-only recording is currently active (read-only).
    /// True when EnableVideoRecording=false and audio recording is in progress.
    /// </summary>
    public bool IsRecordingAudioOnly
    {
        get { return (bool)GetValue(IsRecordingAudioOnlyProperty); }
        private set { SetValue(IsRecordingAudioOnlyProperty, value); }
    }

    public static readonly BindableProperty VideoQualityProperty = BindableProperty.Create(
        nameof(VideoQuality),
        typeof(VideoQuality),
        typeof(SkiaCamera),
        VideoQuality.Standard,
        propertyChanged: OnCaptureVideoFormatChanged);

    /// <summary>
    /// Video recording quality preset
    /// </summary>
    public VideoQuality VideoQuality
    {
        get { return (VideoQuality)GetValue(VideoQualityProperty); }
        set { SetValue(VideoQualityProperty, value); }
    }

    public static readonly BindableProperty VideoFormatIndexProperty = BindableProperty.Create(
        nameof(VideoFormatIndex),
        typeof(int),
        typeof(SkiaCamera),
        0,
        propertyChanged: OnCaptureVideoFormatChanged);

    /// <summary>
    /// Index of video format when VideoQuality is set to Manual.
    /// Selects from the array of available video formats.
    /// Use GetAvailableVideoFormatsAsync() to see available options.
    /// </summary>
    public int VideoFormatIndex
    {
        get { return (int)GetValue(VideoFormatIndexProperty); }
        set { SetValue(VideoFormatIndexProperty, value); }
    }

    public static readonly BindableProperty AudioSampleRateProperty = BindableProperty.Create(
        nameof(AudioSampleRate),
        typeof(int),
        typeof(SkiaCamera),
        44100,
        BindingMode.OneWay);

    public int AudioSampleRate
    {
        get => (int)GetValue(AudioSampleRateProperty);
        set => SetValue(AudioSampleRateProperty, value);
    }

    public static readonly BindableProperty AudioChannelsProperty = BindableProperty.Create(
        nameof(AudioChannels),
        typeof(int),
        typeof(SkiaCamera),
        1,
        BindingMode.OneWay);

    public int AudioChannels
    {
        get => (int)GetValue(AudioChannelsProperty);
        set => SetValue(AudioChannelsProperty, value);
    }

    public static readonly BindableProperty AudioBitDepthProperty = BindableProperty.Create(
        nameof(AudioBitDepth),
        typeof(AudioBitDepth),
        typeof(SkiaCamera),
        AudioBitDepth.Pcm16Bit,
        BindingMode.OneWay);

    public AudioBitDepth AudioBitDepth
    {
        get => (AudioBitDepth)GetValue(AudioBitDepthProperty);
        set => SetValue(AudioBitDepthProperty, value);
    }

    /// <summary>
    /// Controls which stream/aspect the live preview should match.
    /// Still: preview matches still-capture aspect. Video: preview matches intended video recording aspect.
    /// Changing this will restart the camera to apply the new preview configuration.
    /// </summary>
    public static readonly BindableProperty CaptureModeProperty = BindableProperty.Create(
        nameof(CaptureMode),
        typeof(CaptureModeType),
        typeof(SkiaCamera),
        CaptureModeType.Still,
        propertyChanged: NeedRestart);

    /// <summary>
    /// Preview mode: Still or Video. Determines which aspect/format the preview sizing should follow.
    /// </summary>
    public CaptureModeType CaptureMode
    {
        get => (CaptureModeType)GetValue(CaptureModeProperty);
        set => SetValue(CaptureModeProperty, value);
    }

    /// <summary>
    /// Readonly, gets whether video recording is supported on the current device/camera
    /// </summary>
    public bool CanRecordVideo
    {
        get { return NativeControl?.CanRecordVideo() ?? false; }
    }

    /// <summary>
    /// Gets the current recording duration (if recording)
    /// </summary>
    public TimeSpan LiveRecordingDuration
    {
        get
        {
            if (_captureVideoEncoder != null)
            {
                return _captureVideoEncoder.LiveRecordingDuration;
            }
            return TimeSpan.Zero;
        }
    }

    public static readonly BindableProperty AudioDeviceIndexProperty = BindableProperty.Create(
        nameof(AudioDeviceIndex),
        typeof(int),
        typeof(SkiaCamera),
        -1, // -1 means "Default" or "Auto"
        propertyChanged: NeedRestart);

    /// <summary>
    /// Index of the audio device to use. -1 for default.
    /// Use GetAvailableAudioDevicesAsync() to get the list of devices.
    /// </summary>
    public int AudioDeviceIndex
    {
        get { return (int)GetValue(AudioDeviceIndexProperty); }
        set { SetValue(AudioDeviceIndexProperty, value); }
    }

    public static readonly BindableProperty AudioCodecIndexProperty = BindableProperty.Create(
        nameof(AudioCodecIndex),
        typeof(int),
        typeof(SkiaCamera),
        -1, // -1 means "Default" or "Auto"
        propertyChanged: NeedRestart);

    /// <summary>
    /// Index of the audio codec to use. -1 for default (usually AAC).
    /// Use GetAvailableAudioCodecsAsync() to get the list of available codecs.
    /// </summary>
    public int AudioCodecIndex
    {
        get { return (int)GetValue(AudioCodecIndexProperty); }
        set { SetValue(AudioCodecIndexProperty, value); }
    }

    public static readonly BindableProperty EnableAudioRecordingProperty = BindableProperty.Create(
        nameof(EnableAudioRecording),
        typeof(bool),
        typeof(SkiaCamera),
        true);

    /// <summary>
    /// Whether to record audio with video. Default is true.
    /// Must be set before starting video recording.
    /// </summary>
    public bool EnableAudioRecording
    {
        get { return (bool)GetValue(EnableAudioRecordingProperty); }
        set { SetValue(EnableAudioRecordingProperty, value); }
    }

    public static readonly BindableProperty EnableVideoRecordingProperty = BindableProperty.Create(
        nameof(EnableVideoRecording),
        typeof(bool),
        typeof(SkiaCamera),
        true); // Default: record video

    /// <summary>
    /// Whether to record video frames. Default is true.
    /// When false, only audio will be recorded (output: M4A file).
    /// EnableAudioRecording must be true when EnableVideoRecording is false.
    /// </summary>
    public bool EnableVideoRecording
    {
        get { return (bool)GetValue(EnableVideoRecordingProperty); }
        set { SetValue(EnableVideoRecordingProperty, value); }
    }

    public static readonly BindableProperty EnableVideoPreviewProperty = BindableProperty.Create(
        nameof(EnableVideoPreview),
        typeof(bool),
        typeof(SkiaCamera),
        true,
        propertyChanged: NeedRestart);

    /// <summary>
    /// Whether to display video preview UI. Default is true.
    /// When false, hides the preview display but camera still initializes if EnableVideoRecording=true.
    /// Set both EnableVideoPreview=false AND EnableVideoRecording=false for pure audio-only mode (no camera hardware).
    /// </summary>
    public bool EnableVideoPreview
    {
        get { return (bool)GetValue(EnableVideoPreviewProperty); }
        set { SetValue(EnableVideoPreviewProperty, value); }
    }

    public static readonly BindableProperty EnableAudioMonitoringProperty = BindableProperty.Create(
        nameof(EnableAudioMonitoring),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: NeedRestart);

    /// <summary>
    /// Whether to enable live audio preview/monitoring. Default is false.
    /// When true, provides live audio feedback (useful for audio level meters, live monitoring).
    /// Independent from EnableAudioRecording - you can monitor audio without recording it.
    /// </summary>
    public bool EnableAudioMonitoring
    {
        get { return (bool)GetValue(EnableAudioMonitoringProperty); }
        set { SetValue(EnableAudioMonitoringProperty, value); }
    }

    public static readonly BindableProperty UseRealtimeVideoProcessingProperty = BindableProperty.Create(
        nameof(UseRealtimeVideoProcessing),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// Whether to use capture video flow (frame-by-frame processing) instead of native video recording.
    /// When true, individual camera frames are captured and processed through FrameProcessor callback before encoding.
    /// Default is false (use native video recording).
    /// </summary>
    public bool UseRealtimeVideoProcessing
    {
        get { return (bool)GetValue(UseRealtimeVideoProcessingProperty); }
        set { SetValue(UseRealtimeVideoProcessingProperty, value); }
    }

    public static readonly BindableProperty EnablePreRecordingProperty = BindableProperty.Create(
        nameof(EnablePreRecording),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: OnPreRecordingEnabledChanged);

    /// <summary>
    /// Whether to buffer pre-recorded frames before video recording starts.
    /// When enabled, frames are buffered in memory before recording begins.
    /// </summary>
    public bool EnablePreRecording
    {
        get { return (bool)GetValue(EnablePreRecordingProperty); }
        set { SetValue(EnablePreRecordingProperty, value); }
    }

    public static readonly BindableProperty PreRecordDurationProperty = BindableProperty.Create(
        nameof(PreRecordDuration),
        typeof(TimeSpan),
        typeof(SkiaCamera),
        TimeSpan.FromSeconds(5),
        propertyChanged: OnPreRecordDurationChanged);

    /// <summary>
    /// Duration of pre-recording buffer to maintain (default: 3 seconds).
    /// Assumes 30 fps average frame rate for buffer size calculation.
    /// </summary>
    public TimeSpan PreRecordDuration
    {
        get { return (TimeSpan)GetValue(PreRecordDurationProperty); }
        set { SetValue(PreRecordDurationProperty, value); }
    }

    private static void OnPreRecordDurationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera && (camera.IsRecording || camera.IsPreRecording))
        {
            Task.Run(async () =>
            {
                await camera.StopVideoRecording(true);
            });
        }
    }

    private static void OnPreRecordingEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera)
        {
            Task.Run(async () =>
            {
                await camera.StopVideoRecording(true);

                bool enabled = (bool)newValue;
                if (enabled && !camera.IsRecording)
                {
                    camera.InitializePreRecordingBuffer();
                }
                else if (!enabled)
                {
                    camera.ClearPreRecordingBuffer();
                }
            });
        }
    }

    /// <summary>
    /// Callback for processing individual frames during capture video flow.
    /// Only used when UseRealtimeVideoProcessing is true.
    /// Parameters: SKCanvas (for drawing), SKImageInfo (frame info), TimeSpan (recording timestamp)
    /// </summary>

    #endregion

    /// <summary>
    /// Whether to mirror composed recording frames to the on-screen preview during capture video flow.
    /// Set this to true to see overlays exactly as recorded; set to false to avoid any mirroring overhead.
    /// </summary>

    /// <summary>
    /// While recording, show exactly the frames being composed for the encoder as the on-screen preview.
    /// This avoids stutter by not relying on a separate preview feed. Controlled by MirrorRecordingToPreview.
    /// </summary>

    public static readonly BindableProperty PreviewVideoFlowProperty = BindableProperty.Create(
        nameof(PreviewVideoFlow),
        typeof(bool),
        typeof(SkiaCamera),
        true);

    /// <summary>
    /// Controls whether preview shows processed frames from the video flow encoder (TRUE) or raw camera frames (FALSE).
    /// Only applies when UseRealtimeVideoProcessing is TRUE. Default is TRUE to show processed preview with overlays.
    /// Set to FALSE to show raw camera preview while still recording processed video with overlays.
    /// </summary>
    public bool PreviewVideoFlow
    {
        get { return (bool)GetValue(PreviewVideoFlowProperty); }
        set { SetValue(PreviewVideoFlowProperty, value); }
    }

    public static readonly BindableProperty TypeProperty = BindableProperty.Create(
        nameof(Type),
        typeof(CameraType),
        typeof(SkiaCamera),
        CameraType.Default, propertyChanged: NeedRestart);

    /// <summary>
    /// To be implemented
    /// </summary>
    public CameraType Type
    {
        get { return (CameraType)GetValue(TypeProperty); }
        set { SetValue(TypeProperty, value); }
    }

    /// <summary>
    /// Will be applied to viewport for focal length etc
    /// </summary>
    public CameraUnit CameraDevice
    {
        get { return _virtualCameraUnit; }
        set
        {
            if (_virtualCameraUnit != value)
            {
                if (_virtualCameraUnit != value)
                {
                    _virtualCameraUnit = value;
                    AssignFocalLengthInternal(value);
                }
            }
        }
    }

    private CameraUnit _virtualCameraUnit;

    public void AssignFocalLengthInternal(CameraUnit value)
    {
        if (value != null)
        {
            FocalLength = (float)(value.FocalLength * value.SensorCropFactor);
        }

        OnPropertyChanged(nameof(CameraDevice));
    }

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

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(FocalLength) || propertyName == nameof(FocalLengthAdjustment))
        {
            FocalLengthAdjusted = FocalLength + FocalLengthAdjustment;
        }
    }

    public static double GetSensorRotation(DeviceOrientation orientation)
    {
        if (orientation == DeviceOrientation.PortraitUpsideDown)
            return 180.0;

        if (orientation == DeviceOrientation.LandscapeLeft)
            return 90.0;

        if (orientation == DeviceOrientation.LandscapeRight)
            return 270.0;

        return 0.0;
    }

    public static readonly BindableProperty CapturedStillImageProperty = BindableProperty.Create(
        nameof(CapturedStillImage),
        typeof(CapturedImage),
        typeof(SkiaCamera),
        null);

    public CapturedImage CapturedStillImage
    {
        get { return (CapturedImage)GetValue(CapturedStillImageProperty); }
        set { SetValue(CapturedStillImageProperty, value); }
    }

    // Camera selection properties used by platform implementations
    public static readonly BindableProperty FacingProperty = BindableProperty.Create(
        nameof(Facing),
        typeof(CameraPosition),
        typeof(SkiaCamera),
        CameraPosition.Default,
        propertyChanged: NeedRestart);

    public CameraPosition Facing
    {
        get => (CameraPosition)GetValue(FacingProperty);
        set => SetValue(FacingProperty, value);
    }

    public static readonly BindableProperty CameraIndexProperty = BindableProperty.Create(
        nameof(CameraIndex),
        typeof(int),
        typeof(SkiaCamera),
        -1,
        propertyChanged: NeedRestart);

    /// <summary>
    /// Index of camera to use when Facing is Manual
    /// </summary>
    public int CameraIndex
    {
        get => (int)GetValue(CameraIndexProperty);
        set => SetValue(CameraIndexProperty, value);
    }

    public static readonly BindableProperty CustomAlbumProperty = BindableProperty.Create(nameof(CustomAlbum),
        typeof(string),
        typeof(SkiaCamera),
        string.Empty);

    /// <summary>
    /// If not null will use this instead of Camera Roll folder for photos output
    /// </summary>
    public string CustomAlbum
    {
        get { return (string)GetValue(CustomAlbumProperty); }
        set { SetValue(CustomAlbumProperty, value); }
    }

    // Photo capture quality selection used by platform implementations
    public static readonly BindableProperty PhotoQualityProperty = BindableProperty.Create(
        nameof(PhotoQuality),
        typeof(CaptureQuality),
        typeof(SkiaCamera),
        CaptureQuality.Max,
        propertyChanged: NeedRestart);

    public CaptureQuality PhotoQuality
    {
        get => (CaptureQuality)GetValue(PhotoQualityProperty);
        set => SetValue(PhotoQualityProperty, value);
    }

    public static readonly BindableProperty PhotoFormatIndexProperty = BindableProperty.Create(
        nameof(PhotoFormatIndex),
        typeof(int),
        typeof(SkiaCamera),
        0,
        propertyChanged: NeedRestart);

    /// <summary>
    /// Index into available still capture formats when PhotoQuality is set to Manual
    /// </summary>
    public int PhotoFormatIndex
    {
        get => (int)GetValue(PhotoFormatIndexProperty);
        set => SetValue(PhotoFormatIndexProperty, value);
    }

    public static readonly BindableProperty GeotagProperty = BindableProperty.Create(nameof(Geotag),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// try to inject location metadata if to photos if GPS succeeds
    /// </summary>
    public bool Geotag
    {
        get { return (bool)GetValue(GeotagProperty); }
        set { SetValue(GeotagProperty, value); }
    }

    // Capture flash mode property used by platform code
    public static readonly BindableProperty CaptureFlashModeProperty = BindableProperty.Create(
        nameof(CaptureFlashMode),
        typeof(CaptureFlashMode),
        typeof(SkiaCamera),
        CaptureFlashMode.Off,
        propertyChanged: OnCaptureFlashModeChanged);

    /// <summary>
    /// Flash mode for still image capture
    /// </summary>
    public CaptureFlashMode CaptureFlashMode
    {
        get => (CaptureFlashMode)GetValue(CaptureFlashModeProperty);
        set => SetValue(CaptureFlashModeProperty, value);
    }

    private static void OnCaptureFlashModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera && camera.NativeControl != null)
        {
            camera.NativeControl.SetCaptureFlashMode((CaptureFlashMode)newValue);
        }
    }

    public static readonly BindableProperty FocalLengthProperty = BindableProperty.Create(
        nameof(FocalLength),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLength
    {
        get { return (double)GetValue(FocalLengthProperty); }
        set { SetValue(FocalLengthProperty, value); }
    }

    public static readonly BindableProperty FocalLengthAdjustedProperty = BindableProperty.Create(
        nameof(FocalLengthAdjusted),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLengthAdjusted
    {
        get { return (double)GetValue(FocalLengthAdjustedProperty); }
        set { SetValue(FocalLengthAdjustedProperty, value); }
    }

    public static readonly BindableProperty FocalLengthAdjustmentProperty = BindableProperty.Create(
        nameof(FocalLengthAdjustment),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLengthAdjustment
    {
        get { return (double)GetValue(FocalLengthAdjustmentProperty); }
        set { SetValue(FocalLengthAdjustmentProperty, value); }
    }

    public static readonly BindableProperty ManualZoomProperty = BindableProperty.Create(
        nameof(ManualZoom),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    public bool ManualZoom
    {
        get { return (bool)GetValue(ManualZoomProperty); }
        set { SetValue(ManualZoomProperty, value); }
    }

    public static readonly BindableProperty ZoomProperty = BindableProperty.Create(
        nameof(Zoom),
        typeof(double),
        typeof(SkiaCamera),
        1.0,
        propertyChanged: NeedSetZoom);

    /// <summary>
    /// Zoom camera
    /// </summary>
    public double Zoom
    {
        get { return (double)GetValue(ZoomProperty); }
        set { SetValue(ZoomProperty, value); }
    }

    public static readonly BindableProperty ConstantUpdateProperty = BindableProperty.Create(
        nameof(ConstantUpdate),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// Default is true.
    /// Whether it should update non-stop or only when a new frame is acquired.
    /// For example if camera gives frames at 30 fps, screen might update around 40fps without this set to true.
    /// If enabled will force max redraws at 60 fps.
    /// </summary>
    public bool ConstantUpdate
    {
        get { return (bool)GetValue(ConstantUpdateProperty); }
        set { SetValue(ConstantUpdateProperty, value); }
    }

    public static readonly BindableProperty ViewportScaleProperty = BindableProperty.Create(
        nameof(ViewportScale),
        typeof(double),
        typeof(SkiaCamera),
        1.0);

    /// <summary>
    /// Zoom viewport value, NOT a camera zoom,
    /// </summary>
    public double ViewportScale
    {
        get { return (double)GetValue(ViewportScaleProperty); }
        set { SetValue(ViewportScaleProperty, value); }
    }

    public static readonly BindableProperty TextureScaleProperty = BindableProperty.Create(
        nameof(TextureScale),
        typeof(double),
        typeof(SkiaCamera),
        1.0, defaultBindingMode: BindingMode.OneWayToSource);

    public double TextureScale
    {
        get { return (double)GetValue(TextureScaleProperty); }
        set { SetValue(TextureScaleProperty, value); }
    }

    public static readonly BindableProperty ZoomLimitMinProperty = BindableProperty.Create(
        nameof(ZoomLimitMin),
        typeof(double),
        typeof(SkiaCamera),
        1.0);

    public double ZoomLimitMin
    {
        get { return (double)GetValue(ZoomLimitMinProperty); }
        set { SetValue(ZoomLimitMinProperty, value); }
    }

    public static readonly BindableProperty ZoomLimitMaxProperty = BindableProperty.Create(
        nameof(ZoomLimitMax),
        typeof(double),
        typeof(SkiaCamera),
        10.0);

    public double ZoomLimitMax
    {
        get { return (double)GetValue(ZoomLimitMaxProperty); }
        set { SetValue(ZoomLimitMaxProperty, value); }
    }

    private static void NeedSetZoom(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            var zoom = (double)newvalue;
            if (zoom < control.ZoomLimitMin)
            {
                zoom = control.ZoomLimitMin;
            }
            else if (zoom > control.ZoomLimitMax)
            {
                zoom = control.ZoomLimitMax;
            }

            control.SetZoom(zoom);
        }
    }

    protected virtual void ApplyDisplayProperties()
    {
        if (Display != null)
        {
            Display.Aspect = this.Aspect;

            Display.ScaleX = this.IsMirrored ? -1 : 1;
        }
    }

    protected override void OnMeasured()
    {
        base.OnMeasured();

        ApplyDisplayProperties();
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ApplyDisplayProperties();

        // Update preview scale when layout changes
        if (_sourceFrameWidth > 0)
        {
            UpdatePreviewScale();
        }
    }

    public static readonly BindableProperty IsMirroredProperty = BindableProperty.Create(
        nameof(IsMirrored),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: NeedInvalidateMeasure);

    public bool IsMirrored
    {
        get { return (bool)GetValue(IsMirroredProperty); }
        set { SetValue(IsMirroredProperty, value); }
    }

    private static readonly BindablePropertyKey PreviewScalePropertyKey = BindableProperty.CreateReadOnly(
        nameof(PreviewScale),
        typeof(float),
        typeof(SkiaCamera),
        1f);

    public static readonly BindableProperty PreviewScaleProperty = PreviewScalePropertyKey.BindableProperty;

    /// <summary>
    /// Scale factor of preview relative to recording/capture frame size.
    /// 1.0 = same size, less than 1.0 = preview is smaller than recording frame.
    /// Use this to scale overlay drawings consistently between preview and recording.
    /// </summary>
    public float PreviewScale
    {
        get => (float)GetValue(PreviewScaleProperty);
        private set => SetValue(PreviewScalePropertyKey, value);
    }

    /// <summary>
    /// Tracks the source frame dimensions (from camera/encoder) for scale calculation
    /// </summary>
    private int _sourceFrameWidth;
    private int _sourceFrameHeight;

    /// <summary>
    /// Tracks the actual preview image dimensions (after rotation) for scale calculation
    /// </summary>
    private int _actualPreviewWidth;
    private int _actualPreviewHeight;

    /// <summary>
    /// Updates PreviewScale based on actual preview image width and encoder/recording width.
    /// Scale = actualPreviewImageWidth / encoderWidth
    /// </summary>
    protected void UpdatePreviewScale()
    {
        // _actualPreviewWidth = actual preview image width (after rotation, from SKImage)
        // _sourceFrameWidth = encoder/recording frame width
        if (_sourceFrameWidth > 0 && _actualPreviewWidth > 0)
        {
            PreviewScale = (float)Math.Max(_actualPreviewWidth, _actualPreviewHeight) / Math.Max(_sourceFrameWidth, _sourceFrameHeight);
            System.Diagnostics.Debug.WriteLine($"[SkiaCamera] PreviewScale updated: {_actualPreviewWidth} / {_sourceFrameWidth} = {PreviewScale:F3}");
        }
        else
        {
            PreviewScale = 1f;
        }
    }

    /// <summary>
    /// Sets the source frame dimensions (encoder/recording size) and updates PreviewScale
    /// </summary>
    public void SetSourceFrameDimensions(int width, int height)
    {
        _sourceFrameWidth = width;
        _sourceFrameHeight = height;

        // Update scale using preview image width vs encoder width
        UpdatePreviewScale();
    }

    /// <summary>
    /// Updates source frame dimensions from the current video format.
    /// Called when camera starts or format changes.
    /// PreviewScale will be updated once actual preview frames arrive.
    /// </summary>
    protected void UpdatePreviewScaleFromFormat()
    {
#if ONPLATFORM
        try
        {
            var format = NativeControl?.GetCurrentVideoFormat();
            if (format != null && format.Width > 0)
            {
                var (width, height) = GetRotationCorrectedDimensions(format.Width, format.Height);

                _sourceFrameWidth = width;
                _sourceFrameHeight = height;
                System.Diagnostics.Debug.WriteLine($"[SkiaCamera] Source frame dimensions set from format: {_sourceFrameWidth}x{_sourceFrameHeight} (raw: {format.Width}x{format.Height})");

                // PreviewScale will be updated when actual preview frames arrive in SetFrameFromNative()
                // because we need the actual rotated preview image dimensions
                if (_actualPreviewWidth > 0)
                {
                    UpdatePreviewScale();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCamera] UpdatePreviewScaleFromFormat error: {ex.Message}");
        }
#endif
    }

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(TransformAspect),
        typeof(SkiaImage),
        TransformAspect.AspectFit,
        propertyChanged: NeedInvalidateMeasure);

    /// <summary>
    /// Apspect to render image with, default is AspectFit.
    /// </summary>
    public TransformAspect Aspect
    {
        get { return (TransformAspect)GetValue(AspectProperty); }
        set { SetValue(AspectProperty, value); }
    }

    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State),
        typeof(CameraState),
        typeof(SkiaCamera),
        CameraState.Off,
        BindingMode.OneWayToSource, propertyChanged: ControlStateChanged);

    private static void ControlStateChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StateChanged?.Invoke(control, control.State);
            control.UpdateInfo();

            // Update preview scale when camera becomes ready
            if (control.State == CameraState.On)
            {
                control.UpdatePreviewScaleFromFormat();

                // Start preview audio capture if EnableAudioRecording or EnableAudioMonitoring is enabled and not recording
                if ((control.EnableAudioRecording || control.EnableAudioMonitoring) && !control.IsRecording && !control.IsPreRecording)
                {
                    control.StartPreviewAudioCapture();
                }
            }
            else
            {
                // Camera is no longer On - stop preview audio
                control.StopPreviewAudioCapture();
            }
        }
    }

    public CameraState State
    {
        get { return (CameraState)GetValue(StateProperty); }
        set { SetValue(StateProperty, value); }
    }

    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(
        nameof(IsOn),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: PowerChanged);

    public bool IsOn
    {
        get { return (bool)GetValue(IsOnProperty); }
        set { SetValue(IsOnProperty, value); }
    }

    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    public bool IsBusy
    {
        get { return (bool)GetValue(IsBusyProperty); }
        set { SetValue(IsBusyProperty, value); }
    }

    public static readonly BindableProperty PickerModeProperty = BindableProperty.Create(
        nameof(PickerMode),
        typeof(CameraPickerMode),
        typeof(SkiaCamera),
        CameraPickerMode.None);

    public CameraPickerMode PickerMode
    {
        get { return (CameraPickerMode)GetValue(PickerModeProperty); }
        set { SetValue(PickerModeProperty, value); }
    }

    public static readonly BindableProperty FilterProperty = BindableProperty.Create(
        nameof(Filter),
        typeof(CameraEffect),
        typeof(SkiaCamera),
        CameraEffect.None);

    public CameraEffect Filter
    {
        get { return (CameraEffect)GetValue(FilterProperty); }
        set { SetValue(FilterProperty, value); }
    }


    /// <summary>
    /// The size of the camera preview in pixels
    /// </summary>
    public SKSize PreviewSize
    {
        get { return _previewSize; }
        set
        {
            if (_previewSize != value)
            {
                _previewSize = value;
                OnPropertyChanged();
            }
        }
    }

    SKSize _previewSize;

    public SKSize CapturePhotoSize
    {
        get { return _capturePhotoSize; }

        set
        {
            if (_capturePhotoSize != value)
            {
                _capturePhotoSize = value;
                OnPropertyChanged();
            }
        }
    }

    SKSize _capturePhotoSize;

    public void SetRotatedContentSize(SKSize size, int cameraRotation)
    {
        if (size.Width < 0 || size.Height < 0)
        {
            throw new Exception("Camera preview size cannot be negative.");
        }

        PreviewSize = size;

        Invalidate();
    }

    private string _DisplayInfo;
    private bool _hasPermissions;

    public string DisplayInfo
    {
        get { return _DisplayInfo; }
        set
        {
            if (_DisplayInfo != value)
            {
                _DisplayInfo = value;
                OnPropertyChanged();
            }
        }
    }

    // Fix Effect property block
    public static readonly BindableProperty EffectProperty = BindableProperty.Create(
        nameof(Effect),
        typeof(SkiaImageEffect),
        typeof(SkiaCamera),
        SkiaImageEffect.None,
        propertyChanged: OnPreviewEffectChanged);

    private static void OnPreviewEffectChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.ApplyPreviewProperties();
        }
    }

    public SkiaImageEffect Effect
    {
        get { return (SkiaImageEffect)GetValue(EffectProperty); }
        set { SetValue(EffectProperty, value); }
    }

    // Restart helpers used by propertyChanged callbacks
    private static void OnCaptureVideoFormatChanged(BindableObject bindable, object oldValue, object newValue)
    {
        NeedRestart(bindable, oldValue, newValue);
    }

    private static void NeedRestart(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera)
        {
            // Only restart if camera is currently on
            if (camera.IsOn)
            {
                camera.ScheduleRestartDebounced();
            }
        }
    }

    void ClearInternalCache()
    {
        _currentVideoFormat = null;
    }

    /// <summary>
    /// Debounces rapid restart requests using a settling period.
    /// Waits for property changes to settle before restarting the camera.
    /// This prevents camera restart spam when multiple properties change in quick succession.
    ///
    /// How it works:
    /// - First property change: starts 500ms settling timer
    /// - More property changes during settling: timer resets to 500ms (waits for "peace")
    /// - No changes for 500ms: camera restarts once with all accumulated changes
    /// </summary>
    private void ScheduleRestartDebounced()
    {
        // Cancel any pending restart (resets the 500ms settling period)
        ClearInternalCache();
        _restartDebounceTimer?.Dispose();


        // Schedule restart after 500ms of no property changes
        // If another property change happens before this fires, it will cancel and reschedule again
        _restartDebounceTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                Debug.WriteLine($"[SkiaCamera] Settings settled, restarting camera");
                StopInternal(true);
                StartWithPermissionsInternal();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] Debounced restart failed: {ex.Message}");
            }
            finally
            {
                _restartDebounceTimer?.Dispose();
                _restartDebounceTimer = null;
            }
        }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    #endregion

    /// <summary>
    /// Gets rotation-corrected dimensions for encoder/recording based on platform and sensor orientation.
    /// Use this to align encoder dimensions with preview orientation.
    /// </summary>
    /// <param name="rawWidth">Raw format width</param>
    /// <param name="rawHeight">Raw format height</param>
    /// <returns>Tuple of (correctedWidth, correctedHeight)</returns>
    public (int width, int height) GetRotationCorrectedDimensions(int rawWidth, int rawHeight)
    {
#if IOS || MACCATALYST
        // iOS/Mac: formats are always landscape, swap for portrait video
        return (rawHeight, rawWidth);
#elif ANDROID
        // Android: swap based on sensor orientation to align encoder with preview
        if (NativeControl is NativeCamera cam)
        {
            int sensor = cam.SensorOrientation;
            bool previewRotated = (sensor == 90 || sensor == 270);

            // If preview is rotated (portrait logical orientation), make encoder portrait too
            if (previewRotated && rawWidth >= rawHeight)
            {
                return (rawHeight, rawWidth);
            }
            // If preview is not rotated but format is portrait, make encoder landscape to match
            else if (!previewRotated && rawHeight > rawWidth)
            {
                return (rawHeight, rawWidth);
            }
        }
        return (rawWidth, rawHeight);
#else
        // Windows: no rotation correction needed - uses raw format dimensions
        return (rawWidth, rawHeight);
#endif
    }

    /// <summary>
    /// Alias for StopVideoRecording(true)
    /// </summary>
    /// <returns></returns>
    public async Task Abort()
    {
        await StopVideoRecording(true);
    }

    /// <summary>
    /// Stop video recording and finalizes the video file or Aborts if passed parameter is `true`.
    /// Resets the locked rotation and restores normal preview behavior.
    /// The video file path will be provided through the RecordingSuccess event.
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StopVideoRecording(bool abort = false)
    {
        if (IsBusy)
        {
            Debug.WriteLine($"[StopVideoRecording] IsBusy cannot stop");
            return;
        }

        // Handle audio-only recording stop
        if (IsRecordingAudioOnly)
        {
            IsBusy = true;
            try
            {
                await StopAudioOnlyRecording(abort);
            }
            finally
            {
                IsBusy = false;
            }
            return;
        }

        if (!IsRecording && !IsPreRecording)
            return;

        IsBusy = true;

        try
        {
            Debug.WriteLine($"[StopVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecording={IsRecording}");

            SetIsRecordingVideo(false);

            // Reset locked rotation
            RecordingLockedRotation = -1;
            Debug.WriteLine($"[StopVideoRecording] Reset locked rotation");

#if ANDROID
            // Stop Android event-driven capture and restore normal preview behavior
            try
            {
                if (NativeControl is NativeCamera androidCam)
                {
                    androidCam.PreviewCaptureSuccess = null;
                }
            }
            catch
            {
            }

            UseRecordingFramesForPreview = false;
#endif

#if ONPLATFORM
            try
            {
                // Check if using capture video flow
                if (_captureVideoEncoder != null)
                {
                    if (abort)
                    {
                        await AbortRealtimeVideoProcessingInternal();
                    }
                    else
                    {
                        // Internal method will handle busy state too but we lock externally as well
                        await StopRealtimeVideoProcessingInternal();
                    }

                    ClearPreRecordingBuffer();
                }
                else
                {

                    await NativeControl.StopVideoRecording();

                    IsBusy = false;
                }

                SetIsPreRecording(false);
            }
            catch (Exception ex)
            {
                SetIsPreRecording(false);
                SetIsRecordingVideo(false);
                //ClearPreRecordingBuffer();
                RecordingFailed?.Invoke(this, ex);
                IsBusy = false;
                throw;
            }
#endif
        }
        finally
        {
            if (abort)
            {
                IsBusy = false;
            }
        }
    }

    #region AUDIO-ONLY RECORDING

    /// <summary>
    /// Starts audio-only recording (no video). Called internally when EnableVideoRecording=false.
    /// Output format: M4A (AAC audio in MP4 container).
    /// </summary>
    protected async Task StartAudioOnlyRecording()
    {
        if (IsRecordingAudioOnly)
            return;

        Debug.WriteLine("[StartAudioOnlyRecording] Starting audio-only recording...");

        try
        {
            // Generate output path
            var filename = $"audio_{DateTime.Now:yyyyMMdd_HHmmss}.m4a";
            var outputPath = Path.Combine(FileSystem.CacheDirectory, filename);

            // Create platform-specific encoder
            CreateAudioOnlyEncoder(out var encoder);
            if (encoder == null)
                throw new InvalidOperationException("Failed to create audio encoder for this platform");

            _audioOnlyEncoder = encoder;

            // Initialize encoder with audio settings
            int sampleRate = AudioSampleRate > 0 ? AudioSampleRate : 44100;
            int channels = AudioChannels > 0 ? AudioChannels : 1;
            var bitDepth = AudioBitDepth.Pcm16Bit;

            await _audioOnlyEncoder.InitializeAsync(outputPath, sampleRate, channels, bitDepth);
            await _audioOnlyEncoder.StartAsync();

            // Start audio capture using the platform's audio capture infrastructure
            // Reuse existing _audioCapture creation pattern from each platform
            await StartAudioOnlyCapture(sampleRate, channels);

            SetIsRecordingAudioOnly(true);
            SetIsRecordingVideo(true); // UI buttons typically bind to IsRecording

            // Start progress reporting timer
            _audioOnlyProgressTimer = new System.Threading.Timer(_ =>
            {
                if (_audioOnlyEncoder?.IsRecording == true)
                {
                    OnRecordingProgress(_audioOnlyEncoder.RecordingDuration);
                }
            }, null, 500, 500);

            Debug.WriteLine($"[StartAudioOnlyRecording] Recording to: {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartAudioOnlyRecording] Error: {ex.Message}");
            _audioOnlyProgressTimer?.Dispose();
            _audioOnlyProgressTimer = null;
            _audioOnlyEncoder?.Dispose();
            _audioOnlyEncoder = null;
            AudioRecordingFailed?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Stops audio-only recording and returns the captured audio.
    /// </summary>
    protected async Task<CapturedAudio> StopAudioOnlyRecording(bool abort = false)
    {
        if (!IsRecordingAudioOnly || _audioOnlyEncoder == null)
            return null;

        Debug.WriteLine("[StopAudioOnlyRecording] Stopping audio-only recording...");

        // Stop progress timer
        _audioOnlyProgressTimer?.Dispose();
        _audioOnlyProgressTimer = null;

        try
        {
            // Stop audio capture first
            await StopAudioOnlyCapture();

            CapturedAudio result = null;
            if (abort)
            {
                await _audioOnlyEncoder.AbortAsync();
            }
            else
            {
                result = await _audioOnlyEncoder.StopAsync();
            }

            _audioOnlyEncoder.Dispose();
            _audioOnlyEncoder = null;

            SetIsRecordingAudioOnly(false);
            SetIsRecordingVideo(false);

            if (result != null && !abort)
            {
                OnAudioRecordingSuccess(result);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StopAudioOnlyRecording] Error: {ex.Message}");
            _audioOnlyEncoder?.Dispose();
            _audioOnlyEncoder = null;
            SetIsRecordingAudioOnly(false);
            SetIsRecordingVideo(false);
            AudioRecordingFailed?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Starts audio capture for audio-only recording.
    /// Implemented per platform.
    /// </summary>
    private partial void StartAudioOnlyCapture(int sampleRate, int channels, out Task task);

    private async Task StartAudioOnlyCapture(int sampleRate, int channels)
    {
        Task task = Task.CompletedTask;
        StartAudioOnlyCapture(sampleRate, channels, out task);
        await task;
    }

    /// <summary>
    /// Stops audio capture for audio-only recording.
    /// Implemented per platform.
    /// </summary>
    private partial void StopAudioOnlyCapture(out Task task);

    private async Task StopAudioOnlyCapture()
    {
        Task task = Task.CompletedTask;
        StopAudioOnlyCapture(out task);
        await task;
    }

    #endregion

    private void Super_OnNativeAppPaused(object sender, EventArgs e)
    {
        StopAll();
    }

    private void Super_OnNativeAppResumed(object sender, EventArgs e)
    {
        ResumeIfNeeded();
    }

    public void ResumeIfNeeded()
    {
        if (IsOn)
            StartInternal();
    }

    public static List<SkiaCamera> Instances = new();

    /// <summary>
    /// Stops all instances
    /// </summary>
    public static void StopAll()
    {
        foreach (var renderer in Instances)
        {
            renderer.StopInternal(true);
        }
    }

    /// <summary>
    /// Gets whether flash is supported on the current camera
    /// </summary>
    public bool IsFlashSupported
    {
        get { return NativeControl?.IsFlashSupported() ?? false; }
    }

    /// <summary>
    /// Gets whether auto flash mode is supported on the current camera
    /// </summary>
    public bool IsAutoFlashSupported
    {
        get { return NativeControl?.IsAutoFlashSupported() ?? false; }
    }

    public static readonly BindableProperty FlashModeProperty = BindableProperty.Create(
        nameof(FlashMode),
        typeof(FlashMode),
        typeof(SkiaCamera),
        FlashMode.Off,
        propertyChanged: OnFlashModeChanged);

    /// <summary>
    /// Flash mode for preview torch. Controls LED torch for live camera preview.
    /// </summary>
    public FlashMode FlashMode
    {
        get { return (FlashMode)GetValue(FlashModeProperty); }
        set { SetValue(FlashModeProperty, value); }
    }

    private static void OnFlashModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaCamera camera && camera.NativeControl != null)
        {
            camera.NativeControl.SetFlashMode((FlashMode)newValue);
        }
    }

    private static void PowerChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StopInternal(true);
            if (control.IsOn)
            {
                control.StartWithPermissionsInternal();
            }
            else
            {
                Debug.WriteLine("CAMERA TURNED OFF");
            }
        }
    }


    /// <summary>
    /// Gets or sets whether a permissions error occurred
    /// </summary>
    public bool PermissionsError { get; set; }

    /// <summary>
    /// Request permissions and start camera without setting IsOn true. Will set IsOn to false if permissions denied.
    /// </summary>
    public virtual void StartWithPermissionsInternal()
    {
        lock (this)
        {

            Super.OnNativeAppResumed -= Super_OnNativeAppResumed;
            Super.OnNativeAppPaused -= Super_OnNativeAppPaused;
            Super.OnNativeAppResumed += Super_OnNativeAppResumed;
            Super.OnNativeAppPaused += Super_OnNativeAppPaused;

            ClearInternalCache();

            try
            {
                Debug.WriteLine("[SkiaCamera] Requesting permissions...");
                CheckPermissions((presented) =>
                    {
                        Debug.WriteLine("[SkiaCamera] Starting..");
                        PermissionsWarning = false;
                        PermissionsError = false;
                        StartInternal();
                    },
                    (presented) =>
                    {
                        Super.Log("[SkiaCamera] Permissions denied");
                        IsOn = false;
                        PermissionsWarning = true;
                        PermissionsError = true;
                    });
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
        }

    }


    /// <summary>
    /// Starts the camera after permissions where acquired
    /// </summary>
    protected virtual void StartInternal()
    {
        if (IsDisposing || IsDisposed)
            return;

#if ONPLATFORM
        DisableOtherCameras();

        // Initialize camera hardware if needed for video recording OR preview
        // Pure audio-only mode: EnableVideoPreview=false AND EnableVideoRecording=false
        bool needsCamera = EnableVideoPreview || EnableVideoRecording;
        
        if (needsCamera)
        {
            if (NativeControl == null)
            {
                CreateNative();
                OnNativeControlCreated();
            }

            // Control preview visibility separately from camera initialization
            if (Display != null)
            {
                Display.IsVisible = EnableVideoPreview;
            }

            NativeControl?.Start();
        }
        else
        {
            // Pure audio-only mode: no video recording, no preview
            Debug.WriteLine("[SkiaCamera] Starting in pure audio-only mode (no camera hardware)");
            StartPreviewAudioCapture();
            State = CameraState.On;
        }
#endif
    }

    public void DisableOtherCameras(bool all = false)
    {
        foreach (var renderer in Instances)
        {
            System.Diagnostics.Debug.WriteLine($"[CAMERA] DisableOtherCameras..");
            bool disable = false;
            if (all || renderer != this)
            {
                disable = true;
            }

            if (disable)
            {
                renderer.StopInternal(true);
                System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {renderer.Uid} {renderer.Tag}");
            }
        }
    }

    /// <summary>
    /// Called after native control is created to notify property changes
    /// </summary>
    protected virtual void OnNativeControlCreated()
    {
        // Notify that flash capability properties may have changed
        OnPropertyChanged(nameof(IsFlashSupported));
        OnPropertyChanged(nameof(IsAutoFlashSupported));

        // Apply current flash modes to native control
        if (NativeControl != null)
        {
            NativeControl.SetFlashMode(FlashMode);
            NativeControl.SetCaptureFlashMode(CaptureFlashMode);
        }
    }

#if !ONPLATFORM

    public virtual void SetZoom(double value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets a bitmap of the current preview frame (not implemented on this platform)
    /// </summary>
    /// <returns>Preview bitmap</returns>
    public SKBitmap GetPreviewBitmap()
    {
        throw new NotImplementedException();
    }

#endif

    private System.Threading.Timer _restartDebounceTimer;

    /// <summary>
    /// Raised when the display rectangle changes
    /// </summary>
    public event EventHandler<SKRect> DisplayRectChanged;

    private void DisplayWasChanged(object sender, SKRect e)
    {
        DisplayRectChanged?.Invoke(this, e);

        // Update preview scale when display dimensions change
        if (_sourceFrameWidth > 0)
        {
            UpdatePreviewScale();
        }
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        Super.OnNativeAppResumed -= Super_OnNativeAppResumed;
        Super.OnNativeAppPaused -= Super_OnNativeAppPaused;

        if (Display != null)
        {
            Display.DisplayRectChanged -= DisplayWasChanged;
        }

        if (Superview != null)
        {
            Superview.OrientationChanged -= DeviceOrientationChanged;
        }

        if (NativeControl != null)
        {
            StopInternal(true);
        }

        // Clean up restart debounce timer
        _restartDebounceTimer?.Dispose();
        _restartDebounceTimer = null;

        // Clean up capture video resources (stop recording first if active)
#if IOS || MACCATALYST
        if (NativeControl is NativeCamera nativeCam)
        {
            nativeCam.RecordingFrameAvailable -= OnRecordingFrameAvailable;
        }
#endif

        _frameCaptureTimer?.Dispose();
        _frameCaptureTimer = null;

        _frameCaptureTimer?.Dispose();
        _frameCaptureTimer = null;
        _captureVideoEncoder?.Dispose();
        _captureVideoEncoder = null;

        NativeControl?.Dispose();
        NativeControl = null;

        Instances.Remove(this);

        //will force crash if our implementation is not safe
#if DEBUG
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
#endif
    }

    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        if (Display == null)
        {
            Display = CreatePreview();
            Display.IsParentIndependent = true;
            Display.AddEffect = Effect;
            Display.SetParent(this);
            OnDisplayReady();
        }

        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }



    /// <summary>
    /// Move captured video from temporary location to public gallery (faster than SaveVideoToGalleryAsync)
    /// </summary>
    /// <param name="capturedVideo">The captured video to move</param>
    /// <param name="album">Optional album name</param>
    /// <param name="deleteOriginal">Whether to delete the original file after successful move (default true)</param>
    /// <param name="filename">Optional custom filename (without path, just the name). If provided, the file will be renamed before moving.</param>
    /// <returns>Gallery path if successful, null if failed</returns>
    public async Task<string> MoveVideoToGalleryAsync(CapturedVideo capturedVideo, string album = null,
        bool deleteOriginal = true, string filename = null)
    {
        if (capturedVideo == null || string.IsNullOrEmpty(capturedVideo.FilePath) ||
            !File.Exists(capturedVideo.FilePath))
            return null;

        try
        {
            var currentPath = capturedVideo.FilePath;

            // Rename file if custom filename provided
            if (!string.IsNullOrWhiteSpace(filename))
            {
                // Validate filename (no path separators)
                if (filename.Contains(Path.DirectorySeparatorChar) || filename.Contains(Path.AltDirectorySeparatorChar))
                {
                    Debug.WriteLine($"[SkiaCamera] Invalid filename (contains path separators): {filename}");
                    return null;
                }

                // Ensure proper extension based on original file type
                var originalExt = Path.GetExtension(currentPath)?.ToLowerInvariant();
                var expectedExt = originalExt == ".m4a" || originalExt == ".aac" || originalExt == ".mp3" || originalExt == ".wav"
                    ? originalExt
                    : ".mp4";
                if (!filename.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
                {
                    filename = Path.GetFileNameWithoutExtension(filename) + expectedExt;
                }

                // Rename file in place
                var directory = Path.GetDirectoryName(currentPath);
                var newPath = Path.Combine(directory, filename);

                if (File.Exists(newPath) && newPath != currentPath)
                {
                    File.Delete(newPath); // Overwrite if exists
                }

                if (newPath != currentPath)
                {
                    File.Move(currentPath, newPath);
                    currentPath = newPath;
                    Debug.WriteLine($"[SkiaCamera] Renamed video file to: {filename}");
                }
            }

            // Fill GPS coordinates first (needed for iOS CLLocation)
            FillVideoGpsCoordinates(capturedVideo);

            // Auto-fill and inject video metadata (GPS, device info, date)
            try
            {
                AutoFillVideoMetadata(capturedVideo);
                var atoms = Mp4MetadataInjector.MetadataToAtoms(capturedVideo.Meta);
                Debug.WriteLine($"[SkiaCamera] Injecting {atoms.Count} metadata atoms: {string.Join(", ", atoms.Keys)}");
                var injected = await Mp4MetadataInjector.InjectMetadataAsync(currentPath, capturedVideo.Meta);
                Debug.WriteLine($"[SkiaCamera] Metadata injected: {injected}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] Metadata injection failed (non-fatal): {ex.Message}");
            }

            Debug.WriteLine($"[SkiaCamera] Moving video to gallery: {currentPath}");

#if ANDROID
            return await MoveVideoToGalleryAndroid(currentPath, album, deleteOriginal);
#elif IOS || MACCATALYST
            return await MoveVideoToGalleryApple(currentPath, album, deleteOriginal,
                capturedVideo.Latitude, capturedVideo.Longitude,
                capturedVideo.Meta?.DateTimeOriginal ?? capturedVideo.Time,
                capturedVideo.Meta);
#elif WINDOWS
            return await MoveVideoToGalleryWindows(currentPath, album, deleteOriginal);
#else
            Debug.WriteLine("[SkiaCamera] MoveVideoToGalleryAsync not implemented for this platform");
            return null;
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera] Failed to move video to gallery: {ex.Message}");
            return null;
        }
    }

    public static string DefaultAlbum = string.Empty;

    /// <summary>
    /// Returns the projected public video directory path for the given album.
    /// </summary>
    /// <param name="album"></param>
    /// <returns></returns>
    public static string GetAppVideoFolder(string albumName)
    {
#if ANDROID
        var dcimDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDcim);
        var appDir = new Java.IO.File(dcimDir, albumName);
        return appDir.AbsolutePath;
#elif WINDOWS
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), albumName);
#elif IOS || MACCATALYST
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#else
        return null;
#endif
    }

    /// <summary>
    /// Camera controls cannot use double buffering cache for performance reasons
    /// </summary>
    public override bool CanUseCacheDoubleBuffering => false;

    /// <summary>
    /// Camera preview will be clipped to the control bounds
    /// </summary>
    public override bool WillClipBounds => true;

    public SkiaCamera()
    {
        Instances.Add(this);
    }

    /// <summary>
    /// Camera control does not support update locking
    /// </summary>
    /// <param name="value">Lock state (ignored)</param>
    public override void LockUpdate(bool value)
    {
    }

    /// <summary>
    /// Command to start the camera
    /// </summary>
    public ICommand CommandStart
    {
        get { return new Command((object context) => { Start(); }); }
    }

    #region EVENTS

    /// <summary>
    /// Raised when a still image is successfully captured
    /// </summary>
    public event EventHandler<CapturedImage> CaptureSuccess;

    /// <summary>
    /// Raised when still image capture fails
    /// </summary>
    public event EventHandler<Exception> CaptureFailed;

    /// <summary>
    /// Raised when a new preview image is set to the display
    /// </summary>
    public event EventHandler<LoadedImageSource> NewPreviewSet;

    /// <summary>
    /// Raised when a camera error occurs
    /// </summary>
    public event EventHandler<string> OnError;

    /// <summary>
    /// Raised when camera zoom level changes
    /// </summary>
    public event EventHandler<double> Zoomed;

    /// <summary>
    /// Raised when the display control is ready for use
    /// </summary>
    public event EventHandler DisplayReady;

    /// <summary>
    /// Fired when video recording completes successfully
    /// </summary>
    public event EventHandler<CapturedVideo> RecordingSuccess;

    /// <summary>
    /// Fired when video recording fails
    /// </summary>
    public event EventHandler<Exception> RecordingFailed;

    /// <summary>
    /// Fired when video recording progress updates. This will NOT be invoked on UI thread!
    /// </summary>
    public event EventHandler<TimeSpan> RecordingProgress;

    /// <summary>
    /// Fired when audio-only recording completes successfully (when EnableVideoRecording=false)
    /// </summary>
    public event EventHandler<CapturedAudio> AudioRecordingSuccess;

    /// <summary>
    /// Fired when audio-only recording fails (when EnableVideoRecording=false)
    /// </summary>
    public event EventHandler<Exception> AudioRecordingFailed;

    public event EventHandler<CameraState> StateChanged;

    internal void RaiseError(string error)
    {
        OnError?.Invoke(this, error);
    }

    #endregion

    #region Display

    protected virtual void OnDisplayReady()
    {
        DisplayReady?.Invoke(this, EventArgs.Empty);

        Display.DisplayRectChanged += DisplayWasChanged;
    }


    protected virtual SkiaImage CreatePreview()
    {
        return new SkiaImage()
        {
            LoadSourceOnFirstDraw = true,
#if IOS || ANDROID
            RescalingQuality = SKFilterQuality.None, //reduce power consumption
#endif
            CacheRescaledSource = false,
            HorizontalOptions = this.NeedAutoWidth ? LayoutOptions.Start : LayoutOptions.Fill,
            VerticalOptions = this.NeedAutoHeight ? LayoutOptions.Start : LayoutOptions.Fill,
            Aspect = this.Aspect,
        };
    }

    /// <summary>
    /// The SkiaImage control that displays the camera preview
    /// </summary>
    public SkiaImage Display { get; protected set; }

    protected override void InvalidateMeasure()
    {
        if (Display != null)
        {
            LayoutDisplay();
        }

        base.InvalidateMeasure();
    }

    protected virtual void LayoutDisplay()
    {
        Display.HorizontalOptions = this.NeedAutoWidth ? LayoutOptions.Start : LayoutOptions.Fill;
        Display.VerticalOptions = this.NeedAutoHeight ? LayoutOptions.Start : LayoutOptions.Fill;
    }

    protected override void CreateDefaultContent()
    {
        UpdateOrientationFromDevice();

        base.CreateDefaultContent();
    }

    /// <summary>
    /// Apply effects on preview
    /// </summary>
    public virtual void ApplyPreviewProperties()
    {
        if (Display != null)
        {
            Display.AddEffect = Effect;
        }
    }

    #endregion

    #region Capture Photo / Take Picture

    /// <summary>
    /// Take camera picture. Run this in background thread!
    /// </summary>
    /// <returns></returns>
    public async Task TakePicture()
    {
        if (IsBusy)
            return;

        Debug.WriteLine($"[TakePicture] IsMainThread {MainThread.IsMainThread}");

        IsBusy = true;

        IsTakingPhoto = true;

        NativeControl.StillImageCaptureFailed = ex =>
        {
            OnCaptureFailed(ex);

            IsTakingPhoto = false;
        };

        NativeControl.StillImageCaptureSuccess = captured =>
        {
            CapturedStillImage = captured;

            OnCaptureSuccess(captured);

            IsTakingPhoto = false;
        };

        NativeControl.TakePicture();

        while (IsTakingPhoto)
        {
            await Task.Delay(60);
        }

        IsBusy = false;
    }

    /// <summary>
    /// Flash screen with color
    /// </summary>
    /// <param name="color"></param>
    public virtual void FlashScreen(Color color)
    {
        var layer = new SkiaControl()
        {
            Tag = "Flash",
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = color,
            ZIndex = int.MaxValue,
        };

        layer.SetParent(this);

        layer.FadeToAsync(0).ContinueWith(_ => { layer.Parent = null; });
    }

    /// <summary>
    /// Play shutter sound
    /// </summary>
    public virtual void PlaySound()
    {
        //todo
    }

    /// <summary>
    /// Sets the flash mode for still image capture
    /// </summary>
    /// <param name="mode">Flash mode to use for capture</param>
    public virtual void SetCaptureFlashMode(CaptureFlashMode mode)
    {
        CaptureFlashMode = mode;
    }

    /// <summary>
    /// Gets the current capture flash mode
    /// </summary>
    /// <returns>Current capture flash mode</returns>
    public virtual CaptureFlashMode GetCaptureFlashMode()
    {
        return CaptureFlashMode;
    }

    private static int filenamesCounter = 0;

    /// <summary>
    /// Generate Jpg filename
    /// </summary>
    /// <returns></returns>
    public virtual string GenerateJpgFileName()
    {
        var add = $"{DateTime.Now:MM/dd/yyyy HH:mm:ss}{++filenamesCounter}";
        var filename =
            $"skiacamera-{add.Replace("/", "").Replace(":", "").Replace(" ", "").Replace(",", "").Replace(".", "").Replace("-", "")}.jpg";

        return filename;
    }

    /// <summary>
    /// Save captured bitmap to native gallery
    /// </summary>
    /// <param name="captured"></param>
    /// <param name="reorient"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    public async Task<string> SaveToGalleryAsync(CapturedImage captured, string album = null)
    {
        var filename = GenerateJpgFileName();

        // Apply GPS coordinates if available and injection is enabled
        if (InjectGpsLocation && LocationLat != 0 && LocationLon != 0 && !captured.Meta.GpsLatitude.HasValue)
        {
            Metadata.ApplyGpsCoordinates(captured.Meta, LocationLat, LocationLon);
        }

        await using var stream = CreateOutputStreamRotated(captured, false);

        using var exifStream = await JpegExifInjector.InjectExifMetadata(stream, captured.Meta);

        var filenameOutput = GenerateJpgFileName();

        var path = await NativeControl.SaveJpgStreamToGallery(exifStream, filename,
            captured.Meta, album);

        if (!string.IsNullOrEmpty(path))
        {
            captured.Path = path;
            Debug.WriteLine(
                $"[SkiaCamera] saved photo: {filenameOutput} exif orientation: {captured.Meta.Orientation}");
            return path;
        }

        Debug.WriteLine($"[SkiaCamera] failed to save photo");
        return null;
    }

    /// <summary>
    /// Gets the list of available cameras on the device
    /// </summary>
    /// <returns>List of available cameras</returns>
    public virtual async Task<List<CameraInfo>> GetAvailableCamerasAsync()
    {
        return await GetAvailableCamerasInternal();
    }

    /// <summary>
    /// Get available capture formats/resolutions for the current camera.
    /// Use with PhotoFormatIndex when PhotoQuality is set to Manual.
    /// Formats are cached when camera is initialized.
    /// </summary>
    /// <returns>List of available capture formats</returns>
    public virtual async Task<List<CaptureFormat>> GetAvailableCaptureFormatsAsync()
    {
#if ONPLATFORM
        // If not cached, detect and cache them
        return await GetAvailableCaptureFormatsPlatform();
#else
        return new List<CaptureFormat>();
#endif
    }

    /// <summary>
    /// Get available video recording formats/resolutions for the current camera.
    /// Use with VideoFormatIndex when VideoQuality is set to Manual.
    /// Formats are cached when camera is initialized.
    /// </summary>
    /// <returns>List of available video formats</returns>
    public virtual async Task<List<VideoFormat>> GetAvailableVideoFormatsAsync()
    {
#if ONPLATFORM
        // If not cached, detect and cache them
        return await GetAvailableVideoFormatsPlatform();
#else
        return new List<VideoFormat>();
#endif
    }

    /// <summary>
    /// Gets the currently selected capture format.
    /// This reflects the format that will be used for still image capture based on
    /// the current PhotoQuality and PhotoFormatIndex settings.
    /// </summary>
    public CaptureFormat CurrentStillCaptureFormat
    {
        get
        {
#if ONPLATFORM
            try
            {
                return NativeControl?.GetCurrentCaptureFormat();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SkiaCamera] Error getting current capture format: {ex.Message}");
            }
#endif
            return null;
        }
    }

    /// <summary>
    /// Gets the currently selected video format.
    /// This reflects the format that will be used for video recording based on
    /// the current VideoQuality and VideoFormatIndex settings.
    /// </summary>
    public VideoFormat CurrentVideoFormat
    {
        get
        {
#if ONPLATFORM
            try
            {
                if (_currentVideoFormat == null)
                {
                    _currentVideoFormat = NativeControl?.GetCurrentVideoFormat();
                }
                return _currentVideoFormat;
            }
            catch (Exception ex)
            {
                Super.Log($"[SkiaCamera] Error getting current video format: {ex.Message}");
            }
#endif
            return null;
        }
    }



    private VideoFormat? _currentVideoFormat;

    /// <summary>
    /// Get available microphones/audio capture devices.
    /// </summary>
    /// <returns>List of device names</returns>
    public virtual async Task<List<string>> GetAvailableAudioDevicesAsync()
    {
#if WINDOWS || ANDROID || IOS || MACCATALYST //todo ONPLATFORM
        return await GetAvailableAudioDevicesPlatform();
#endif
        return new List<string>();
    }

    /// <summary>
    /// Get available audio codecs for video recording.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<List<string>> GetAvailableAudioCodecsAsync()
    {
#if WINDOWS || ANDROID || IOS || MACCATALYST //todo ONPLATFORM
        return await GetAvailableAudioCodecsPlatform();
#endif
        return new List<string>();
    }

    /// <summary>
    /// Internal method to get available cameras with caching
    /// </summary>
    protected virtual async Task<List<CameraInfo>> GetAvailableCamerasInternal(bool refresh = false)
    {
#if ONPLATFORM
        return await GetAvailableCamerasPlatform(refresh);
#endif

        return new List<CameraInfo>();
    }

    #endregion

    /// <summary>
    /// Starts the camera by setting IsOn to true.
    /// The actual camera initialization and permission handling happens automatically.
    /// </summary>
    public virtual void Start()
    {
        IsOn = true; //haha
    }

    #region VIDEO RECORDING SHARED METHODS

    /// <summary>
    /// Clear the pre-recording buffer/file
    /// </summary>
    private void ClearPreRecordingBuffer()
    {
        lock (_preRecordingLock)
        {
            // Stop any active pre-recording encoder first
            if (_captureVideoEncoder != null && IsPreRecording)
            {
                try
                {
                    _captureVideoEncoder.Dispose();
                }
                catch { }
                _captureVideoEncoder = null;
            }

            // Delete temp file if it exists
            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[ClearPreRecordingBuffer] Deleted: {_preRecordingFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ClearPreRecordingBuffer] Failed to delete: {ex.Message}");
                }
            }

            _preRecordingFilePath = null;
            _maxPreRecordingFrames = 0;
            _audioBuffer = null;
        }
    }

    /// <summary>
    /// Save captured video to gallery (copies the video file)
    /// </summary>
    /// <param name="capturedVideo">The captured video to save</param>
    /// <param name="album">Optional album name</param>
    /// <returns>Gallery path if successful, null if failed</returns>
    public async Task<string> SaveVideoToGalleryAsync(CapturedVideo capturedVideo, string album = null)
    {
        if (capturedVideo == null || string.IsNullOrEmpty(capturedVideo.FilePath) ||
            !File.Exists(capturedVideo.FilePath))
            return null;

        try
        {
            // Fill GPS coordinates first (needed for iOS CLLocation)
            FillVideoGpsCoordinates(capturedVideo);

            // Auto-fill and inject video metadata (GPS, device info, date)
            try
            {
                AutoFillVideoMetadata(capturedVideo); //GPS set inside

                var atoms = Mp4MetadataInjector.MetadataToAtoms(capturedVideo.Meta);
                Debug.WriteLine($"[SkiaCamera] Injecting {atoms.Count} metadata atoms: {string.Join(", ", atoms.Keys)}");
                var injected = await Mp4MetadataInjector.InjectMetadataAsync(capturedVideo.FilePath, capturedVideo.Meta);
                Debug.WriteLine($"[SkiaCamera] Metadata injected: {injected}");

                capturedVideo.Meta.GpsLongitude = capturedVideo.Longitude;
                capturedVideo.Meta.GpsLatitude = capturedVideo.Latitude;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] Metadata injection failed (non-fatal): {ex.Message}");
            }

#if ONPLATFORM
            var path = await NativeControl.SaveVideoToGallery(capturedVideo.FilePath, album);
            return path;
#else
            return null;
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera] Failed to save video to gallery: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fills CapturedVideo.Latitude/Longitude from camera GPS if available.
    /// Always called first so iOS CLLocation is set regardless of metadata injection.
    /// </summary>
    private void FillVideoGpsCoordinates(CapturedVideo capturedVideo)
    {
        if (!capturedVideo.Latitude.HasValue && InjectGpsLocation && LocationLat != 0 && LocationLon != 0)
        {
            capturedVideo.Latitude = LocationLat;
            capturedVideo.Longitude = LocationLon;
        }
    }

    /// <summary>
    /// Auto-fills CapturedVideo.Meta with device info, GPS, and recording time.
    /// If Meta is already set by the user, only fills missing fields.
    /// Never throws — all errors are logged and swallowed.
    /// </summary>
    private void AutoFillVideoMetadata(CapturedVideo capturedVideo)
    {
        try
        {
            capturedVideo.Meta ??= new Metadata();
            var meta = capturedVideo.Meta;

            // Fill device info
            try
            {
                if (string.IsNullOrEmpty(meta.Software))
                    meta.Software = $"{AppInfo.Name} {AppInfo.VersionString}";
                if (string.IsNullOrEmpty(meta.Vendor))
                    meta.Vendor = DeviceInfo.Manufacturer;
                if (string.IsNullOrEmpty(meta.Model))
                    meta.Model = DeviceInfo.Model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] AutoFillVideoMetadata device info failed: {ex.Message}");
            }

            // Fill lens info from camera device
            try
            {
                if (string.IsNullOrEmpty(meta.LensModel) && CameraDevice != null)
                {
                    var facing = CameraDevice.Facing == CameraPosition.Selfie ? "Front Camera" : "Back Camera";
                    var focalStr = "";
                    if (CameraDevice.FocalLengths?.Count > 0)
                    {
                        var fl = CameraDevice.FocalLengths[0];
                        focalStr = $" {fl:G4}mm";
                    }
                    var apertureStr = "";
                    if (meta.Aperture.HasValue)
                    {
                        apertureStr = $" f/{meta.Aperture.Value:G3}";
                    }
                    meta.LensModel = $"{meta.Vendor ?? DeviceInfo.Manufacturer} {meta.Model ?? DeviceInfo.Model} {facing}{focalStr}{apertureStr}".Trim();
                    meta.LensMake = meta.Vendor ?? DeviceInfo.Manufacturer;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera] AutoFillVideoMetadata lens info failed: {ex.Message}");
            }

            // Fill recording time
            if (!meta.DateTimeOriginal.HasValue)
                meta.DateTimeOriginal = capturedVideo.Time != default ? capturedVideo.Time : DateTime.Now;

            // Fill GPS from camera location or from CapturedVideo coordinates
            if (!meta.GpsLatitude.HasValue)
            {
                if (capturedVideo.Latitude.HasValue && capturedVideo.Longitude.HasValue
                    && (capturedVideo.Latitude.Value != 0 || capturedVideo.Longitude.Value != 0))
                {
                    Metadata.ApplyGpsCoordinates(meta, capturedVideo.Latitude.Value, capturedVideo.Longitude.Value);
                }
            }

            Debug.WriteLine($"[SkiaCamera] AutoFillVideoMetadata: Software={meta.Software}, " +
                            $"Make={meta.Vendor}, Model={meta.Model}, " +
                            $"LensModel={meta.LensModel}, " +
                            $"GPS={meta.GpsLatitude}/{meta.GpsLongitude}, " +
                            $"Date={meta.DateTimeOriginal}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera] AutoFillVideoMetadata failed: {ex.Message}");
        }
    }

    private async Task StartNativeVideoRecording()
    {
#if ONPLATFORM

        // Tell native camera whether to record audio
        NativeControl.SetRecordAudio(EnableAudioRecording);

        // Set up video recording callbacks to handle state synchronization
        NativeControl.RecordingFailed = ex =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetIsRecordingVideo(false);
                RecordingFailed?.Invoke(this, ex);
            });
        };

        NativeControl.RecordingSuccess = capturedVideo =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetIsRecordingVideo(false);
                OnRecordingSuccess(capturedVideo);
            });
        };

        NativeControl.RecordingProgress = duration =>
        {
            OnRecordingProgress(duration);
        };


        await NativeControl.StartVideoRecording();

#endif
    }

    /// <summary>
    /// Call this method manually on the main thread to request location permissions and fetch GPS coordinates.
    /// Results are stored in LocationLat/LocationLon. When InjectGpsLocation is true, save methods will
    /// automatically inject these coordinates into photo EXIF and video MP4 metadata.
    /// Must be called from the main thread so permission dialogs can be displayed.
    /// </summary>
    public async Task RefreshGpsLocation(int msTimeout = 2000)
    {
        if (GpsBusy)
            return;

        try
        {
            GpsBusy = true;

            // Check and request location permission (must be on main thread)
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    Debug.WriteLine("[SkiaCamera] Location permission denied");
                    return;
                }
            }

            // Try cached location first (instant, no GPS fix needed)
            try
            {
                var location = await Geolocation.GetLastKnownLocationAsync();
                if (location != null)
                {
                    LocationLat = location.Latitude;
                    LocationLon = location.Longitude;
                    Debug.WriteLine(
                        $"[SkiaCamera] Cached location: {location.Latitude}, {location.Longitude}");
                    return;
                }
            }
            catch (FeatureNotSupportedException)
            {
                Debug.WriteLine("[SkiaCamera] Geolocation not supported on this device");
                return;
            }
            catch (FeatureNotEnabledException)
            {
                Debug.WriteLine("[SkiaCamera] Geolocation not enabled on device");
                return;
            }

            // Fall back to fresh GPS fix if no cached location
            await RefreshLocationInternal(msTimeout);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera] RefreshGpsLocation failed: {ex.Message}");
        }
        finally
        {
            GpsBusy = false;
        }
    }

    protected virtual async Task RefreshLocationInternal(int msTimeout)
    {
        try
        {
            GpsBusy = true;

            var request = new GeolocationRequest(GeolocationAccuracy.Medium);
            var cancel = new CancellationTokenSource();
            cancel.CancelAfter(msTimeout);
            var location = await Geolocation.GetLocationAsync(request, cancel.Token);

            if (location != null)
            {
                Debug.WriteLine(
                    $"ACTUAL Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

                this.LocationLat = location.Latitude;
                this.LocationLon = location.Longitude;
            }
        }
        catch (FeatureNotSupportedException fnsEx)
        {
            // Handle not supported on device exception
        }
        catch (FeatureNotEnabledException fneEx)
        {
            // Handle not enabled on device exception
        }
        catch (PermissionException pEx)
        {
            // Handle permission exception
        }
        catch (Exception ex)
        {
            // Unable to get location
        }
        finally
        {
            GpsBusy = false;
        }
    }

    #endregion // GPS

    #region SHARED PROPERTIES AND FIELDS

    /// <summary>
    /// Enable on-screen diagnostics overlay (effective FPS, dropped frames, last submit ms)
    /// during capture video flow to validate performance.
    /// </summary>
    public bool EnableCaptureDiagnostics { get; set; } = false;

    private int _targetFps = 0;

    #region COMMON DIAGNOSTIC FIELDS

    private int _frameInFlight = 0;

    /// <summary>
    /// Mirror diagnostics toggle used in drawing overlays
    /// </summary>
    public bool VideoDiagnosticsOn
    {
        get => EnableCaptureDiagnostics;
        set => EnableCaptureDiagnostics = value;
    }

    private long _diagDroppedFrames = 0;
    private long _diagSubmittedFrames = 0;
    private double _diagLastSubmitMs = 0;
    private DateTime _diagStartTime;
    private int _diagEncWidth = 0, _diagEncHeight = 0;
    private long _diagBitrate = 0;

    #endregion

    private TimeSpan _preRecordingDurationTracked = TimeSpan.Zero;  // Track pre-rec duration for timestamp offset in live recording

    /// <summary>
    /// Locked rotation value during video recording
    /// </summary>
    public int RecordingLockedRotation { get; private set; } = -1;

    /// <summary>
    /// Gets the active rotation to use during recording (respects RecordingLockedRotation)
    /// </summary>
    /// <returns>Rotation in degrees</returns>
    protected int GetActiveRecordingRotation()
    {
        // If rotation is locked during recording, use the locked value
        if (RecordingLockedRotation >= 0)
        {
            return RecordingLockedRotation;
        }

        // Otherwise use current device rotation
        return DeviceRotation;
    }

    /// <summary>
    /// Gets or sets whether a new camera frame has been acquired and is ready for display
    /// </summary>
    public bool FrameAquired { get; set; }

    /// <summary>
    /// The SKSurface used for frame rendering
    /// </summary>
    public SKSurface FrameSurface { get; protected set; }

    /// <summary>
    /// The image info for the frame surface
    /// </summary>
    public SKImageInfo FrameSurfaceInfo { get; protected set; }

    /// <summary>
    /// Class representing a queued picture waiting to be processed
    /// </summary>
    public class CameraQueuedPictured
    {
        /// <summary>
        /// Gets or sets the filename for this queued picture
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Gets or sets the sensor rotation angle in degrees
        /// </summary>
        public double SensorRotation { get; set; }

        /// <summary>
        /// Set by renderer after work
        /// </summary>
        public bool Processed { get; set; }
    }

    /// <summary>
    /// Queue of pictures waiting to be processed
    /// </summary>
    public CameraPicturesQueue PicturesQueue { get; } = new CameraPicturesQueue();

    /// <summary>
    /// Current duration of the active recording
    /// </summary>
    public TimeSpan CurrentRecordingDuration { get; private set; }

    /// <summary>
    /// Custom frame processor for video capture (recording frames).
    /// Called for each frame being encoded to video. Scale is always 1.0.
    /// </summary>
    public Action<DrawableFrame> FrameProcessor { get; set; }

    /// <summary>
    /// Custom frame processor for preview display.
    /// Called for each preview frame before display. Use PreviewScale to match recording overlay sizing.
    /// </summary>
    public Action<DrawableFrame> PreviewProcessor { get; set; }

    /// <summary>
    /// Whether to mirror recording frames to preview
    /// </summary>
    public bool MirrorRecordingToPreview { get; set; } = true;

    /// <summary>
    /// Whether to use recording frames for preview during recording
    /// </summary>
    public bool UseRecordingFramesForPreview { get; set; } = true;

    /// <summary>
    /// Saved rotation value
    /// </summary>
    public double SavedRotation { get; set; }

    #endregion

    public INativeCamera NativeControl;

    private IAudioCapture _audioCapture;
    private IAudioCapture _previewAudioCapture; // Separate instance for preview-only audio (not recording)
    private CircularAudioBuffer _audioBuffer;
    private IAudioOnlyEncoder _audioOnlyEncoder; // For audio-only recording when EnableVideoRecording=false
    private System.Threading.Timer _audioOnlyProgressTimer;

    /// <summary>
    /// Starts preview audio capture. Called when EnableAudioRecording=true and camera starts (not recording).
    /// Implemented per platform.
    /// </summary>
    partial void StartPreviewAudioCapture();

    /// <summary>
    /// Stops preview audio capture. Called when recording starts or camera stops.
    /// Implemented per platform.
    /// </summary>
    partial void StopPreviewAudioCapture();

    /// <summary>
    /// Creates a platform-specific audio-only encoder.
    /// Implemented per platform.
    /// </summary>
    private partial void CreateAudioOnlyEncoder(out IAudioOnlyEncoder encoder);

    /// <summary>
    /// Called when audio sample is available during audio-only recording.
    /// </summary>
    private void OnAudioOnlySampleAvailable(object sender, AudioSample sample)
    {
        var useSample = OnAudioSampleAvailable(sample);

        _audioOnlyEncoder?.WriteAudio(useSample);
    }

    /// <summary>
    /// Helper to raise AudioRecordingSuccess event.
    /// </summary>
    internal void OnAudioRecordingSuccess(CapturedAudio capturedAudio)
    {
        Debug.WriteLine($"[OnAudioRecordingSuccess] Audio file: {capturedAudio?.FilePath}, Duration: {capturedAudio?.Duration}");
        AudioRecordingSuccess?.Invoke(this, capturedAudio);

        // Also fire RecordingSuccess so existing UI code (gallery saving etc.) works
        if (capturedAudio != null)
        {
            OnRecordingSuccess(new CapturedVideo
            {
                FilePath = capturedAudio.FilePath,
                Duration = capturedAudio.Duration,
                FileSizeBytes = capturedAudio.FileSizeBytes,
                Time = capturedAudio.Time
            });
        }
    }

    private long _captureEpochNs;

    private ICaptureVideoEncoder _captureVideoEncoder;
    private System.Threading.Timer _frameCaptureTimer;
    private DateTime _captureVideoStartTime;

    /// <summary>
    /// Start time of current capture video recording (for overlay time sync)
    /// </summary>
    public DateTime CaptureVideoStartTime => _captureVideoStartTime;

    // Pre-recording file fields (streaming to disk, not memory)
    private object _preRecordingLock = new object();
    private string _preRecordingFilePath; // Path to temp file for pre-recorded video
    private int _maxPreRecordingFrames;
#if WINDOWS
    private bool _useWindowsPreviewDrivenCapture;
#endif
#if WINDOWS || ANDROID || IOS || MACCATALYST
    private EventHandler _encoderPreviewInvalidateHandler;
#endif
    private DateTime? _capturePtsBaseTime; // base timestamp for PTS (from first captured frame)

#if ANDROID
    private int _androidFrameGate; // 0 = free, 1 = in-flight
    private int _androidWarmupDropRemaining; // drop first N frames to avoid initial garbage frame
    private System.Action<CapturedImage> _androidPreviewHandler;
#endif

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        if (State == CameraState.Error)
            StartInternal();
    }

    bool subscribed;

    /// <summary>
    /// Called when the superview (parent container) changes.
    /// Subscribes to orientation change events from the superview.
    /// </summary>
    public override void SuperViewChanged()
    {
        if (Superview != null && !subscribed)
        {
            subscribed = true;
            Superview.OrientationChanged += DeviceOrientationChanged;
        }

        base.SuperViewChanged();
    }

    /// <summary>
    /// Updates the camera orientation from the current device rotation
    /// </summary>
    public virtual void UpdateOrientationFromDevice()
    {
        DeviceRotation = Super.DeviceRotationSnap;

        Debug.WriteLine($"[CAMERA] DeviceRotation: {DeviceRotation}");
    }

    private void DeviceOrientationChanged(object sender, DeviceOrientation deviceOrientation)
    {
        UpdateOrientationFromDevice();
    }

    private int _DeviceRotation = -1;

    /// <summary>
    /// Gets or sets the current device rotation in degrees (0, 90, 180, 270).
    /// Automatically applies the orientation to the native camera when changed.
    /// </summary>
    public int DeviceRotation
    {
        get { return _DeviceRotation; }
        set
        {
            if (_DeviceRotation != value)
            {
                _DeviceRotation = value;
                OnPropertyChanged();
                NativeControl?.ApplyDeviceOrientation(value);
                UpdateInfo();
            }
        }
    }

    /// <summary>
    /// Rotation locked when video recording started. Used throughout recording to ensure consistent orientation.
    /// </summary>

    object lockFrame = new();

    /// <summary>
    /// Gets or sets whether a new camera frame has been acquired and is ready for display
    /// </summary>

    /// <summary>
    /// Updates the camera preview display. Called when a new frame is available from the native camera.
    /// Handles frame submission for video capture flow if recording is active.
    /// </summary>
    public virtual void UpdatePreview()
    {
        FrameAquired = false;
        NeedUpdate = false;
        Update();

#if WINDOWS
        // If using capture video flow and preview-driven capture, submit frames in real-time with the preview
        // Use fire-and-forget Task instead of SafeAction to avoid cascading repaints that cause preview lag
        if (_useWindowsPreviewDrivenCapture && (IsRecording || IsPreRecording) &&
            _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // Ensure single-frame processing - drop if previous is still in progress
            if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) != 0)
            {
                System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
            }
            else
            {
                // Track camera input fps
                CalculateCameraInputFps();

                // Fire-and-forget frame processing (no SafeAction to avoid repaint cascade)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var elapsed = DateTime.Now - _captureVideoStartTime;
                        using var previewImage = NativeControl?.GetPreviewImage();
                        if (previewImage == null)
                            return;

                        using (winEnc.BeginFrame(elapsed, out var canvas, out var info))
                        {
                            if (canvas != null)
                            {
                                var __rects3 =
                                    GetAspectFillRects(previewImage.Width, previewImage.Height, info.Width, info.Height);
                                canvas.DrawImage(previewImage, __rects3.src, __rects3.dst);

                                if (FrameProcessor != null || VideoDiagnosticsOn)
                                {
                                    // Apply rotation based on device orientation
                                    var rotation = GetActiveRecordingRotation();
                                    canvas.Save();
                                    ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                                    var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                                    var frame = new DrawableFrame
                                    {
                                        Width = frameWidth,
                                        Height = frameHeight,
                                        Canvas = canvas,
                                        Time = elapsed,
                                        Scale = 1f
                                    };
                                    FrameProcessor?.Invoke(frame);

                                    if (VideoDiagnosticsOn)
                                        DrawDiagnostics(canvas, info.Width, info.Height);

                                    canvas.Restore();
                                }

                                var sw = System.Diagnostics.Stopwatch.StartNew();
                                await winEnc.SubmitFrameAsync();
                                sw.Stop();
                                _diagLastSubmitMs = sw.Elapsed.TotalMilliseconds;
                                System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);

                                // Track encoder output fps
                                CalculateRecordingFps();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdatePreview Capture] {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
                    }
                });
            }
        }
#endif
    }


    /// <summary>
    /// Returns frame dimensions after rotation (swaps width/height for 90/270 degrees)
    /// </summary>
    private static (int width, int height) GetRotatedDimensions(int width, int height, int rotation)
    {
        var normalizedRotation = rotation % 360;
        if (normalizedRotation < 0)
            normalizedRotation += 360;

        // Swap dimensions for 90 and 270 degree rotations
        if (normalizedRotation == 90 || normalizedRotation == 270)
            return (height, width);

        return (width, height);
    }

    /// <summary>
    /// Applies canvas rotation based on device orientation (0, 90, 180, 270 degrees)
    /// </summary>
    private static void ApplyCanvasRotation(SKCanvas canvas, int width, int height, int rotation)
    {
        var normalizedRotation = rotation % 360;
        if (normalizedRotation < 0)
            normalizedRotation += 360;

        switch (normalizedRotation)
        {
            case 90:
                // Rotate 90° clockwise: translate to bottom-left, then rotate
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
            case 180:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case 270:
                // Rotate 270° clockwise (or 90° counter-clockwise): translate to top-right, then rotate
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
                // case 0: no rotation needed
        }
    }

    /// <summary>
    /// Gets the SKSurface used for frame rendering operations
    /// </summary>

    /// <summary>
    /// Gets the image info for the frame surface
    /// </summary>

    protected virtual void OnNewFrameSet(LoadedImageSource source)
    {
        NewPreviewSet?.Invoke(this, source);
    }

    protected virtual SKImage AquireFrameFromNative()
    {
        // When UseRecordingFramesForPreview=false, we want raw ImageReader preview (don't suppress)

#if WINDOWS
        if ((IsRecording || IsPreRecording) && UseRecordingFramesForPreview &&
            _captureVideoEncoder is WindowsCaptureVideoEncoder winEnc)
        {
            // Only show frames that were actually composed for recording.
            // If none is available yet, return null so the previous displayed frame stays,
            // avoiding a fallback blink from the raw preview without overlay.
            if (winEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose

            return null; // do NOT fallback to raw preview during recording
        }
#elif ANDROID
        // While recording on Android, mirror the composed encoder frames into the preview (no second camera feed)
        if ((IsRecording || IsPreRecording) && UseRecordingFramesForPreview &&
            _captureVideoEncoder is AndroidCaptureVideoEncoder droidEnc)
        {
            if (droidEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose
            return null; // no fallback to raw preview during recording
        }
#elif IOS || MACCATALYST
        // While recording on Apple, mirror the composed encoder frames into the preview
        if ((IsRecording || IsPreRecording) && UseRecordingFramesForPreview &&
            _captureVideoEncoder is DrawnUi.Camera.AppleVideoToolboxEncoder appleEnc)
        {
            if (appleEnc.TryAcquirePreviewImage(out var img) && img != null)
                return img; // renderer takes ownership and must dispose
            return null; // no fallback to raw preview during recording
        }
#endif
        return NativeControl.GetPreviewImage();
    }

    protected virtual void SetFrameFromNative()
    {
        if (NativeControl != null && !FrameAquired)
        {
            //acquire latest image from camera
            var image = AquireFrameFromNative();
            if (image != null)
            {
                FrameAquired = true;

                // Capture actual preview image dimensions for PreviewScale calculation
                if (_actualPreviewWidth != image.Width)
                {
                    _actualPreviewWidth = image.Width;
                    _actualPreviewHeight = image.Height;
                    if (_sourceFrameWidth > 0)
                    {
                        UpdatePreviewScale();
                    }
                }

                // Apply PreviewProcessor if set — but skip when UseRecordingFramesForPreview is active
                // because the encoder preview already has FrameProcessor overlay baked in.
                SKImage finalImage = image;
                if (PreviewProcessor != null && !(UseRecordingFramesForPreview && (IsRecording || IsPreRecording)))
                {
                    var processed = ApplyPreviewProcessor(image);
                    if (processed != null)
                    {
                        finalImage = processed;
                    }
                }

                // Note: Pre-recording frame buffering happens at the encoder level (platform-specific)
                // The encoder intercepts encoded frames during the pre-recording phase and buffers them

                OnNewFrameSet(Display.SetImageInternal(finalImage, false));
            }
        }
    }

    /// <summary>
    /// Applies PreviewProcessor to the preview image and returns the composited result.
    /// </summary>
    private SKImage ApplyPreviewProcessor(SKImage source)
    {
        if (source == null || PreviewProcessor == null)
            return null;

        try
        {
            var width = source.Width;
            var height = source.Height;

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null)
                return null;

            var canvas = surface.Canvas;

            // Draw raw preview image
            canvas.DrawImage(source, 0, 0);

            // Calculate elapsed time (for animation sync with recording)
            var elapsed = (IsRecording || IsPreRecording)
                ? DateTime.Now - _captureVideoStartTime
                : TimeSpan.Zero;

            // Call PreviewProcessor with preview frame info
            var frame = new DrawableFrame
            {
                Width = width,
                Height = height,
                Canvas = canvas,
                Time = elapsed,
                IsPreview = true,
                Scale = PreviewScale  // Use PreviewScale so user can match recording overlay
            };
            PreviewProcessor.Invoke(frame);

            // Return composited image
            return surface.Snapshot();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCamera] ApplyPreviewProcessor error: {ex.Message}");
            return null;
        }
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        if (State == CameraState.On)
        {
            SetFrameFromNative();
        }

        DrawViews(ctx);

        if (ConstantUpdate && State == CameraState.On)
        {
            Update();
        }
    }

    #region SkiaCamera xam control

    private bool _PermissionsWarning;

    /// <summary>
    /// Gets or sets whether a permissions warning is active (permissions need to be granted)
    /// </summary>
    public bool PermissionsWarning
    {
        get { return _PermissionsWarning; }
        set
        {
            if (_PermissionsWarning != value)
            {
                _PermissionsWarning = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Queue for managing pictures waiting to be processed
    /// </summary>
    public class CameraPicturesQueue : Queue<CameraQueuedPictured>
    {
    }


    private bool _IsTakingPhoto;

    /// <summary>
    /// Gets whether the camera is currently taking a still photo
    /// </summary>
    public bool IsTakingPhoto
    {
        get { return _IsTakingPhoto; }
        set
        {
            if (_IsTakingPhoto != value)
            {
                _IsTakingPhoto = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the queue of pictures waiting to be processed
    /// </summary>

    #region PERMISSIONS

    protected static bool ChecksBusy = false;

    private static DateTime lastTimeChecked = DateTime.MinValue;

    [Flags]
    public enum NeedPermissions
    {
        Camera     = 1,
        Gallery    = 2,
        Microphone = 4,
        Location   = 8
    }

    /// <summary>
    /// Gets whether camera permissions have been granted
    /// </summary>
    public static bool PermissionsGranted { get; protected set; }


    private bool _GpsBusy;

    public bool GpsBusy
    {
        get { return _GpsBusy; }
        set
        {
            if (_GpsBusy != value)
            {
                _GpsBusy = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLat;

    public double LocationLat
    {
        get { return _LocationLat; }
        set
        {
            if (_LocationLat != value)
            {
                _LocationLat = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLon;

    public double LocationLon
    {
        get { return _LocationLon; }
        set
        {
            if (_LocationLon != value)
            {
                _LocationLon = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _InjectGpsLocation;

    public bool InjectGpsLocation
    {
        get { return _InjectGpsLocation; }
        set
        {
            if (_InjectGpsLocation != value)
            {
                _InjectGpsLocation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Checks and requests only the permissions specified by <paramref name="request"/> flags,
    /// then invokes the appropriate callback. Can be called from any thread (main not needed).
    /// </summary>
    /// <param name="granted">Invoked when all requested permissions are granted.</param>
    /// <param name="notGranted">Invoked when at least one requested permission is denied.</param>
    /// <param name="request">Flags indicating which permissions to check and request.</param>
    public static void CheckPermissions(Action granted, Action notGranted, NeedPermissions request)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (ChecksBusy)
                return;

            bool allGranted = true;

            ChecksBusy = true;
            try
            {
                if (request.HasFlag(NeedPermissions.Camera))
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted)
                        status = await Permissions.RequestAsync<Permissions.Camera>();
                    allGranted = allGranted && status == PermissionStatus.Granted;
                }

                if (allGranted && request.HasFlag(NeedPermissions.Gallery))
                {
#if IOS || MACCATALYST
                    allGranted = await RequestGalleryPermissions();
#elif ANDROID
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                    {
                        var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                        if (readStatus != PermissionStatus.Granted)
                            readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
                        allGranted = readStatus == PermissionStatus.Granted;
                    }
                    else
                    {
                        var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                        if (writeStatus != PermissionStatus.Granted)
                            writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                        allGranted = writeStatus == PermissionStatus.Granted;
                    }
#else
                    var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    if (storageStatus != PermissionStatus.Granted)
                        storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                    allGranted = storageStatus == PermissionStatus.Granted;
#endif
                }

                if (allGranted && request.HasFlag(NeedPermissions.Microphone))
                {
#if IOS || MACCATALYST
                    var s = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
                    if (s == AVAuthorizationStatus.NotDetermined)
                        allGranted = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Audio);
                    else
                        allGranted = s == AVAuthorizationStatus.Authorized;
#else
                    var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                    if (micStatus != PermissionStatus.Granted)
                        micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
                    allGranted = micStatus == PermissionStatus.Granted;
#endif
                }

                if (allGranted && request.HasFlag(NeedPermissions.Location))
                {
                    var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (locStatus != PermissionStatus.Granted)
                        locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    allGranted = locStatus == PermissionStatus.Granted;
                }
            }
            catch (Exception ex)
            {
                Super.Log(ex);
                allGranted = false;
            }
            finally
            {
                PermissionsGranted = allGranted;

                if (allGranted)
                    granted?.Invoke();
                else
                    notGranted?.Invoke();

                ChecksBusy = false;
            }
        });
    }


    /// <summary>
    /// Checks and requests only the permissions specified by <paramref name="request"/> flags,
    /// then invokes the appropriate callback. Can be called from any thread (main not needed).
    /// </summary>    /// <param name="request"></param>
    /// <returns></returns>
    public static Task<bool> RequestPermissionsAsync(NeedPermissions request)
    {
        var tcs = new TaskCompletionSource<bool>();

        SkiaCamera.CheckPermissions(() =>
            {
                tcs.TrySetResult(true);
            },
            () =>
            {
                tcs.TrySetResult(false);
            }, request);

        return tcs.Task;
    }

    /// <summary>
    /// Silently checks whether the specified permissions are already granted, without showing
    /// any system permission dialog to the user. Can be called from any thread (main not needed).
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static Task<bool> RequestPermissionsGrantedAsync(NeedPermissions request)
    {
        var tcs = new TaskCompletionSource<bool>();

        SkiaCamera.CheckPermissionsGranted(() =>
            {
                tcs.TrySetResult(true);
            },
            () =>
            {
                tcs.TrySetResult(false);
            }, request);

        return tcs.Task;
    }

    /// <summary>
    /// Silently checks whether the specified permissions are already granted, without showing
    /// any system permission dialog to the user. Callbacks are invoked on the main thread.
    /// Can be called from any thread (main not needed).
    /// </summary>
    /// <param name="granted">Invoked when all requested permissions are already granted.</param>
    /// <param name="notGranted">Invoked when at least one requested permission is not granted.</param>
    /// <param name="request">Flags indicating which permissions to check.</param>
    public static void CheckPermissionsGranted(Action granted, Action notGranted, NeedPermissions request)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            bool allGranted = true;

            try
            {
                if (request.HasFlag(NeedPermissions.Camera))
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    allGranted = allGranted && status == PermissionStatus.Granted;
                }

                if (allGranted && request.HasFlag(NeedPermissions.Gallery))
                {
#if IOS || MACCATALYST
                    var authStatus = Photos.PHPhotoLibrary.GetAuthorizationStatus(Photos.PHAccessLevel.ReadWrite);
                    allGranted = authStatus == Photos.PHAuthorizationStatus.Authorized ||
                                 authStatus == Photos.PHAuthorizationStatus.Limited;
#elif ANDROID
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                    {
                        var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                        allGranted = readStatus == PermissionStatus.Granted;
                    }
                    else
                    {
                        var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                        allGranted = writeStatus == PermissionStatus.Granted;
                    }
#else
                    var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    allGranted = storageStatus == PermissionStatus.Granted;
#endif
                }

                if (allGranted && request.HasFlag(NeedPermissions.Microphone))
                {
#if IOS || MACCATALYST
                    var s = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
                    allGranted = s == AVAuthorizationStatus.Authorized;
#else
                    var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                    allGranted = micStatus == PermissionStatus.Granted;
#endif
                }

                if (allGranted && request.HasFlag(NeedPermissions.Location))
                {
                    var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    allGranted = locStatus == PermissionStatus.Granted;
                }
            }
            catch (Exception ex)
            {
                Super.Log(ex);
                allGranted = false;
            }

            if (allGranted)
                granted?.Invoke();
            else
                notGranted?.Invoke();
        });
    }

    /// <summary>
    /// Wrapper used by existing code to request  permissions defined by NeedPermissionsSet then proceed. This is used when camera is turned On.
    /// </summary>
    public void CheckPermissions(Action<object> granted, Action<object> notGranted)
    {
        CheckPermissions(() => granted?.Invoke(null), () => notGranted?.Invoke(null), NeedPermissionsSet);
    }

    NeedPermissions _needPermissionsSet = NeedPermissions.Camera | NeedPermissions.Gallery;
    public NeedPermissions NeedPermissionsSet
    {
    	get => _needPermissionsSet;
    	set
    	{
    		if (_needPermissionsSet != value)
    		{
           		_needPermissionsSet = value;
    			OnPropertyChanged();	
    		}
    	}
    }

    #endregion

    /// <summary>
    /// Internal method to raise RecordingSuccess event
    /// </summary>
    internal void OnRecordingSuccess(CapturedVideo capturedVideo)
    {
        CurrentRecordingDuration = TimeSpan.Zero;
        RecordingSuccess?.Invoke(this, capturedVideo);
    }

    /// <summary>
    /// Internal method to raise RecordingProgress event
    /// </summary>
    internal void OnRecordingProgress(TimeSpan duration)
    {
        CurrentRecordingDuration = duration;
        if (RecordingProgress != null)
        {
            RecordingProgress?.Invoke(this, duration);
        }
    }

    /// <summary>
    /// Mux two video files (pre-recorded + live) into a single output file.
    /// Platform-specific implementation for iOS, Android, and Windows.
    /// Note: Audio handling is platform-specific and not part of this shared interface.
    /// </summary>
    /// <param name="preRecordedPath">Path to pre-recorded video file</param>
    /// <param name="liveRecordingPath">Path to live recording video file</param>
    private async Task<string> MuxVideosAsync(string preRecordedPath, string liveRecordingPath)
    {
#if ONPLATFORM
        if (string.IsNullOrEmpty(preRecordedPath) || string.IsNullOrEmpty(liveRecordingPath))
            return null;

        if (!File.Exists(preRecordedPath) || !File.Exists(liveRecordingPath))
            return null;

        string outputPath = Path.Combine(
            Path.GetDirectoryName(liveRecordingPath),
            $"muxed_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4"
        );

        try
        {
            return await MuxVideosInternal(preRecordedPath, liveRecordingPath, outputPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MuxVideosAsync] Error: {ex.Message}");
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            throw;
        }
#else
        return null;
#endif
    }

    #endregion

    #region METHODS

    /// <summary>
    /// Stops the camera by setting IsOn to false
    /// </summary>
    public virtual void Stop()
    {
        IsOn = false;
    }

    /// <summary>
    /// Stops the camera immediately and releases native camera resources
    /// </summary>
    /// <param name="force">If true, forces immediate stop regardless of state</param>
    public virtual void StopInternal(bool force = false)
    {
        if (IsDisposing || IsDisposed)
            return;

        System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {Uid} {Tag}");

        //_ = StopVideoRecording(true);

        NativeControl?.Stop(force);
        
        // Stop audio capture if running in pure audio-only mode (no camera hardware)
        if (!EnableVideoPreview && !EnableVideoRecording)
        {
            StopPreviewAudioCapture();
        }
        
        State = CameraState.Off;
    }

    /// <summary>
    /// Override this method to customize DisplayInfo content
    /// </summary>
    public virtual void UpdateInfo()
    {
        var info = $"Position: {Facing}" +
                   $"\nState: {State}" +
                   $"\nPreview: {PreviewSize} px" +
                   $"\nPhoto: {CapturePhotoSize} px" +
                   $"\nRotation: {this.DeviceRotation}";

        if (Display != null)
        {
            info += $"\nAspect: {Display.Aspect}";
        }

        DisplayInfo = info;
    }

    /// <summary>
    /// Creates an output stream from a captured image with optional rotation correction
    /// </summary>
    /// <param name="captured">The captured image to encode</param>
    /// <param name="reorient">If true, applies rotation correction before encoding</param>
    /// <param name="format">Output image format (default: JPEG)</param>
    /// <param name="quality">Encoding quality 0-100 (default: 90)</param>
    /// <returns>Stream containing the encoded image</returns>
    public Stream CreateOutputStreamRotated(CapturedImage captured,
        bool reorient,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg,
        int quality = 90)
    {
        try
        {
            SKBitmap skBitmap = SKBitmap.FromImage(captured.Image);
            if (reorient)
            {
                skBitmap = Reorient(skBitmap, captured.Rotation);
            }

            Debug.WriteLine($"[SkiaCamera] Saving bitmap {skBitmap.Width}x{skBitmap.Height}");

            var data = skBitmap.Encode(format, quality);
            return data.AsStream();
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            return null;
        }
    }

    #endregion

    #region ENGINE

    protected virtual void OnCaptureSuccess(CapturedImage captured)
    {
        CaptureSuccess?.Invoke(this, captured);
    }

    protected virtual void OnCaptureFailed(Exception ex)
    {
        CaptureFailed?.Invoke(this, ex);
    }

    /// <summary>
    /// Initialize pre-recording buffer for buffering encoded frames before recording starts
    /// </summary>
    private void InitializePreRecordingBuffer()
    {
        lock (_preRecordingLock)
        {
            // Generate base path for encoder initialization
            // The encoder will create pre_rec_*.mp4 from this base path
            var tempDir = FileSystem.CacheDirectory;
            _preRecordingFilePath = Path.Combine(tempDir, $"pre_recording_base_{Guid.NewGuid()}.mp4");
            _maxPreRecordingFrames = Math.Max(1, (int)(PreRecordDuration.TotalSeconds * 30)); // Assume 30 fps for diagnostics
            Debug.WriteLine($"[InitializePreRecordingBuffer] Base path for encoder: {_preRecordingFilePath}");
        }

        // Platform-specific buffer pre-allocation (to avoid lag spike on record button press)
        EnsurePreRecordingBufferPreAllocated();
    }

    /// <summary>
    /// Platform-specific: Pre-allocate memory for pre-recording buffer to avoid lag spike when recording starts.
    /// Implemented in SkiaCamera.Apple.cs for iOS/MacCatalyst.
    /// </summary>
    partial void EnsurePreRecordingBufferPreAllocated();

    #endregion

    #endregion

    private static (SKRect src, SKRect dst) GetAspectFillRects(int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new SKRect(0, 0, dstW, dstH);
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
            return (new SKRect(0, 0, srcW, srcH), dst);

        float srcAR = (float)srcW / srcH;
        float dstAR = (float)dstW / dstH;
        SKRect src;
        if (srcAR > dstAR)
        {
            // Crop width
            float newW = srcH * dstAR;
            float left = (srcW - newW) * 0.5f;
            src = new SKRect(left, 0, left + newW, srcH);
        }
        else
        {
            // Crop height
            float newH = srcW / dstAR;
            float top = (srcH - newH) * 0.5f;
            src = new SKRect(0, top, srcW, top + newH);
        }

        return (src, dst);
    }

    #region UNIFIED DIAGNOSTICS

    private double _diagReportFps;
    private double _diagInputReportFps;
    private long _diagLastFrameTimestamp;
    private long _diagLastInputFrameTimestamp;

    private const double DiagFpsAlpha = 0.1; // EMA smoothing factor

    private void CalculateRecordingFps()
    {
        long now = Super.GetCurrentTimeNanos();
        if (_diagLastFrameTimestamp == 0) { _diagLastFrameTimestamp = now; return; }
        double elapsed = (now - _diagLastFrameTimestamp) / 1_000_000_000.0;
        _diagLastFrameTimestamp = now;
        if (elapsed <= 0 || elapsed > 1.0) return;
        double fps = 1.0 / elapsed;
        _diagReportFps = _diagReportFps <= 0 ? fps : DiagFpsAlpha * fps + (1.0 - DiagFpsAlpha) * _diagReportFps;
    }

    private void CalculateCameraInputFps()
    {
        long now = Super.GetCurrentTimeNanos();
        if (_diagLastInputFrameTimestamp == 0) { _diagLastInputFrameTimestamp = now; return; }
        double elapsed = (now - _diagLastInputFrameTimestamp) / 1_000_000_000.0;
        _diagLastInputFrameTimestamp = now;
        if (elapsed <= 0 || elapsed > 1.0) return;
        double fps = 1.0 / elapsed;
        _diagInputReportFps = _diagInputReportFps <= 0 ? fps : DiagFpsAlpha * fps + (1.0 - DiagFpsAlpha) * _diagInputReportFps;
    }

    private void ResetRecordingFps()
    {
        _diagLastFrameTimestamp = 0;
        _diagReportFps = 0;
        _diagLastInputFrameTimestamp = 0;
        _diagInputReportFps = 0;
    }

    private DateTime _captureVideoTotalStartTime;

    /// <summary>
    /// Draws diagnostic overlay on video frames showing FPS, dropped frames, encoder settings.
    /// Unified implementation with platform-specific data sources.
    /// </summary>
    private void DrawDiagnostics(SKCanvas canvas, int width, int height)
    {
        if (!EnableCaptureDiagnostics || canvas == null)
            return;

        double rawCamFps = 0;
        double inputFps = 0;
        double outputFps = 0;
        int backpressureDrops = 0;

        // All platforms: use rolling average FPS
        outputFps = _diagReportFps;
        inputFps = _diagInputReportFps;

#if IOS || MACCATALYST
        // Get raw camera FPS from native control
        if (NativeControl is NativeCamera nativeCam)
        {
            rawCamFps = nativeCam.RawCameraFps;
        }

        // Get encoder backpressure drops
        if (_captureVideoEncoder is DrawnUi.Camera.AppleVideoToolboxEncoder appleEnc)
        {
            backpressureDrops = appleEnc.BackpressureDroppedFrames;
        }
#elif WINDOWS
        if (NativeControl is NativeCamera winCam)
        {
            rawCamFps = winCam.RawCameraFps;
        }
#endif

        // Format diagnostic text based on platform
        string line1;
#if IOS || MACCATALYST
        // Apple format: show raw, encoder FPS, and both drop counters
        line1 = rawCamFps > 0
            ? $"raw: {rawCamFps:F1}  enc: {outputFps:F1} / {_targetFps}  drop: {_diagDroppedFrames} bp: {backpressureDrops}"
            : $"FPS: {outputFps:F1} / {_targetFps}  drop: {_diagDroppedFrames} bp: {backpressureDrops}";
#else
        // Windows/Android format: show raw, camera input, and encoder output FPS
        line1 = rawCamFps > 0
            ? $"raw: {rawCamFps:F1}  cam: {inputFps:F1}  enc: {outputFps:F1} / {_targetFps}"
            : $"cam: {inputFps:F1}  enc: {outputFps:F1} / {_targetFps}";
#endif

        string line2;
#if IOS || MACCATALYST
        line2 = $"submit: {_diagLastSubmitMs:F1} ms";
#else
        line2 = $"dropped: {_diagDroppedFrames}  submit: {_diagLastSubmitMs:F1} ms";
#endif

        double mbps = _diagBitrate > 0 ? _diagBitrate / 1_000_000.0 : 0.0;
        string line3 = _diagEncWidth > 0 && _diagEncHeight > 0
            ? $"rec: {_diagEncWidth}x{_diagEncHeight}@{_targetFps}  bitrate: {mbps:F1} Mbps"
            : $"bitrate: {mbps:F1} Mbps";

        // Common drawing code for all platforms
        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true };
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = Math.Max(14, width / 60f)
        };

        var pad = 8f;
        var y1 = pad + textPaint.TextSize;
        var y2 = y1 + textPaint.TextSize + 4f;
        var y3 = y2 + textPaint.TextSize + 4f;
        var maxTextWidth = Math.Max(textPaint.MeasureText(line1),
            Math.Max(textPaint.MeasureText(line2), textPaint.MeasureText(line3)));
        var rect = new SKRect(pad, pad, pad + maxTextWidth + pad, y3 + pad);

        canvas.Save();
        canvas.DrawRoundRect(rect, 6, 6, bgPaint);
        canvas.DrawText(line1, pad * 1.5f, y1, textPaint);
        canvas.DrawText(line2, pad * 1.5f, y2, textPaint);
        canvas.DrawText(line3, pad * 1.5f, y3, textPaint);
        canvas.Restore();
    }

    #endregion

}
