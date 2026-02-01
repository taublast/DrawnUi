using SkiaSharp;

namespace DrawnUi.Camera;

/// <summary>
/// Cross-platform interface for video decoding.
/// Provides hardware-accelerated video frame decoding.
/// </summary>
public interface IVideoDecoder : IDisposable
{
    /// <summary>
    /// Video duration
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Video frame rate
    /// </summary>
    double FrameRate { get; }

    /// <summary>
    /// Video dimensions
    /// </summary>
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Whether video has audio track
    /// </summary>
    bool HasAudio { get; }

    /// <summary>
    /// Get next video frame at current position
    /// </summary>
    Task<SKBitmap> GetNextFrameAsync();

    /// <summary>
    /// Seek to specific position
    /// </summary>
    Task SeekAsync(TimeSpan position);

    /// <summary>
    /// Get current playback position
    /// </summary>
    TimeSpan Position { get; }
}