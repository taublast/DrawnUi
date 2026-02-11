namespace DrawnUi.Camera;

/// <summary>
/// Backward-compatible wrapper around Mp4MetadataInjector for GPS-only injection.
/// New code should use Mp4MetadataInjector directly.
/// </summary>
public static class Mp4LocationInjector
{
    public static bool Debug
    {
        get => Mp4MetadataInjector.Debug;
        set => Mp4MetadataInjector.Debug = value;
    }

    /// <summary>
    /// Injects GPS location into an MP4/MOV file. Modifies the file in-place.
    /// </summary>
    public static bool InjectLocation(string filePath, double latitude, double longitude)
    {
        return Mp4MetadataInjector.InjectLocation(filePath, latitude, longitude);
    }

    /// <summary>
    /// Injects GPS location into an MP4/MOV file. Modifies the file in-place.
    /// </summary>
    public static Task<bool> InjectLocationAsync(string filePath, double latitude, double longitude)
    {
        return Mp4MetadataInjector.InjectLocationAsync(filePath, latitude, longitude);
    }

    /// <summary>
    /// Reads GPS location from an MP4/MOV file if present.
    /// </summary>
    public static bool ReadLocation(string filePath, out double latitude, out double longitude)
    {
        return Mp4MetadataInjector.ReadLocation(filePath, out latitude, out longitude);
    }
}
