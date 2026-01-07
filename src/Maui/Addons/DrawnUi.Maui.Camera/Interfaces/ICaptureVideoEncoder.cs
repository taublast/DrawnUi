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
    /// Stops encoding and finalizes the video file.
    /// In pre-recording mode, this will concatenate pre-recorded and live recordings.
    /// </summary>
    Task<CapturedVideo> StopAsync();

    /// <summary>
    /// Gets whether the encoder is currently recording.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Gets the duration of the current live recording session (excluding pre-recording buffer).
    /// </summary>
    TimeSpan LiveRecordingDuration { get; }

    /// <summary>
    /// Sets whether the encoder is in pre-recording mode (buffering to memory only).
    /// When true, encoded frames should be buffered instead of written to file.
    /// </summary>
    bool IsPreRecordingMode { get; set; }

    /// <summary>
    /// Reference to parent SkiaCamera for accessing BufferPreRecordingFrame method.
    /// Used by encoder to buffer encoded frames during pre-recording phase.
    /// </summary>
    SkiaCamera ParentCamera { get; set; }

    /// <summary>
    /// Number of video frames successfully encoded.
    /// </summary>
    int EncodedFrameCount { get; }

    /// <summary>
    /// Total bytes of encoded data written to output.
    /// </summary>
    long EncodedDataSize { get; }

    /// <summary>
    /// Elapsed time since encoding started.
    /// </summary>
    TimeSpan EncodingDuration { get; }

    /// <summary>
    /// Current status of the encoding operation ("Idle", "Started", "Encoding", "Stopping", "Completed").
    /// </summary>
    string EncodingStatus { get; }

    /// <summary>
    /// Progress reporting event (optional).
    /// </summary>
    event EventHandler<TimeSpan> ProgressReported;

    /// <summary>
    /// Sets the circular audio buffer to use for pre-recording.
    /// </summary>
    void SetAudioBuffer(CircularAudioBuffer buffer);

    /// <summary>
    /// Writes an audio sample to the encoder (or buffer if pre-recording).
    /// </summary>
    void WriteAudioSample(AudioSample sample);

    /// <summary>
    /// Whether this encoder supports audio recording.
    /// </summary>
    bool SupportsAudio { get; }

    Task AbortAsync();
}
