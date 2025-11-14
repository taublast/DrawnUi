namespace DrawnUi.Camera;

/// <summary>
/// Represents a captured video file with metadata
/// </summary>
public class CapturedVideo : IDisposable
{
    /// <summary>
    /// Path to the recorded video file
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// Duration of the recorded video
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Video format used for recording
    /// </summary>
    public VideoFormat Format { get; set; }

    /// <summary>
    /// Camera position used for recording
    /// </summary>
    public CameraPosition Facing { get; set; }

    /// <summary>
    /// Device local time when recording started
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Additional metadata (GPS, device info, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            
            // Optionally delete the video file if it's in temp/cache directory
            // Users can control this by setting DeleteOnDispose = true
            if (DeleteOnDispose && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch
                {
                    // Ignore file deletion errors
                }
            }
        }
    }

    /// <summary>
    /// Whether to delete the video file when disposing (default: false)
    /// </summary>
    public bool DeleteOnDispose { get; set; } = false;

    public bool IsDisposed { get; protected set; }

    /// <summary>
    /// Gets human-readable file size
    /// </summary>
    public string FileSizeString
    {
        get
        {
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
            if (FileSizeBytes < 1024 * 1024 * 1024) return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }

    /// <summary>
    /// Gets human-readable duration string
    /// </summary>
    public string DurationString => Duration.ToString(@"mm\:ss");

    public override string ToString()
    {
        return $"Video: {Format?.Description ?? "Unknown"}, Duration: {DurationString}, Size: {FileSizeString}";
    }
}