namespace DrawnUi.Camera;

public interface INativeCamera : IDisposable
{
    void Stop(bool force = false);
    void Start();

    /// <summary>
    /// Sets the flash mode for preview torch
    /// </summary>
    /// <param name="mode">Flash mode for preview torch</param>
    void SetFlashMode(FlashMode mode);

    /// <summary>
    /// Gets the current flash mode for preview torch
    /// </summary>
    /// <returns>Current flash mode</returns>
    FlashMode GetFlashMode();

    /// <summary>
    /// Sets the flash mode for still image capture
    /// </summary>
    /// <param name="mode">Flash mode to use for capture</param>
    void SetCaptureFlashMode(CaptureFlashMode mode);

    /// <summary>
    /// Gets the current capture flash mode
    /// </summary>
    /// <returns>Current capture flash mode</returns>
    CaptureFlashMode GetCaptureFlashMode();

    /// <summary>
    /// Gets whether flash is supported on this camera
    /// </summary>
    /// <returns>True if flash is supported</returns>
    bool IsFlashSupported();

    /// <summary>
    /// Gets whether auto flash mode is supported on this camera
    /// </summary>
    /// <returns>True if auto flash is supported</returns>
    bool IsAutoFlashSupported();

    /// <summary>
    /// If you get the preview via this method you are now responsible to dispose it yourself to avoid memory leaks.
    /// </summary>
    /// <returns></returns>
    SKImage GetPreviewImage();

    void ApplyDeviceOrientation(int orientation);

    void TakePicture();

    Action<CapturedImage> PreviewCaptureSuccess { get; set; }

    Action<CapturedImage> StillImageCaptureSuccess { get; set; }

    Action<Exception> StillImageCaptureFailed { get; set; }

    //Action<Bitmap> CapturedImage;

    //Task<SKBitmap> TakePictureForSkia();

    /// <summary>
    /// Return pull path of saved file or null if error
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="filename"></param>
    /// <param name="cameraSavedRotation"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    Task<string> SaveJpgStreamToGallery(Stream stream, string filename, double cameraSavedRotation, Metadata meta, string album);

    void SetZoom(float value);

    /// <summary>
    /// Gets the manual exposure capabilities and recommended settings for the camera
    /// </summary>
    /// <returns>Camera manual exposure range information</returns>
    CameraManualExposureRange GetExposureRange();

    /// <summary>
    /// Sets manual exposure settings for the camera
    /// </summary>
    /// <param name="iso">ISO sensitivity value</param>
    /// <param name="shutterSpeed">Shutter speed in seconds</param>
    bool SetManualExposure(float iso, float shutterSpeed);

    /// <summary>
    /// Gets the currently selected capture format
    /// </summary>
    /// <returns>Current capture format or null if not available</returns>
    CaptureFormat GetCurrentCaptureFormat();

    /// <summary>
    /// Sets the camera to automatic exposure mode
    /// </summary>
    void SetAutoExposure();

    #region VIDEO RECORDING

    /// <summary>
    /// Gets the currently selected video format
    /// </summary>
    /// <returns>Current video format or null if not available</returns>
    VideoFormat GetCurrentVideoFormat();

    /// <summary>
    /// Starts video recording
    /// </summary>
    Task StartVideoRecording();

    /// <summary>
    /// Stops video recording
    /// </summary>
    Task StopVideoRecording();

    /// <summary>
    /// Gets whether video recording is supported on this camera
    /// </summary>
    /// <returns>True if video recording is supported</returns>
    bool CanRecordVideo();

    /// <summary>
    /// Save video to gallery
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="album">Optional album name</param>
    /// <returns>Gallery path if successful, null if failed</returns>
    Task<string> SaveVideoToGallery(string videoFilePath, string album);

    /// <summary>
    /// Event fired when video recording completes successfully
    /// </summary>
    Action<CapturedVideo> VideoRecordingSuccess { get; set; }

    /// <summary>
    /// Event fired when video recording fails
    /// </summary>
    Action<Exception> VideoRecordingFailed { get; set; }

    /// <summary>
    /// Event fired when video recording progress updates
    /// </summary>
    Action<TimeSpan> VideoRecordingProgress { get; set; }

    #endregion
}
