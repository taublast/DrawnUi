namespace DrawnUi.Camera;

public interface INativeCamera : IDisposable
{
    void Stop(bool force = false);
    void Start();

    void TurnOnFlash();
    void TurnOffFlash();

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
    /// Sets the camera to automatic exposure mode
    /// </summary>
    void SetAutoExposure();
}
