using SkiaSharp;

namespace DrawnUi.Camera;

/// <summary>
/// Cross-platform interface for capture video encoding.
/// Handles encoding of individual processed frames into video files.
/// </summary>
public interface ICaptureVideoEncoder : IDisposable
{
    /// <summary>
    /// Initializes the encoder with video parameters and file output.
    /// </summary>
    /// <param name="outputPath">Output video file path</param>
    /// <param name="width">Video width in pixels</param>
    /// <param name="height">Video height in pixels</param>
    /// <param name="frameRate">Target frame rate (fps)</param>
    /// <param name="recordAudio">Whether to record audio alongside video</param>
    Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio);

    /// <summary>
    /// Starts the encoding session.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Adds a processed frame to the video.
    /// </summary>
    /// <param name="bitmap">The processed frame bitmap</param>
    /// <param name="timestamp">Frame timestamp for proper timing</param>
    Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp);

    /// <summary>
    /// Prepends pre-recorded buffered encoded data to the output.
    /// This writes the pre-encoded video data directly to the file stream before live frames.
    /// </summary>
    /// <param name="prerecordingBuffer">Buffer containing pre-encoded video data</param>
    Task PrependBufferedEncodedDataAsync(PrerecordingEncodedBuffer prerecordingBuffer);

    /// <summary>
    /// Stops encoding and finalizes the video file.
    /// </summary>
    Task<CapturedVideo> StopAsync();

    /// <summary>
    /// Gets whether the encoder is currently recording.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Progress reporting event (optional).
    /// </summary>
    event EventHandler<TimeSpan> ProgressReported;
}