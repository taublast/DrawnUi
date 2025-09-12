namespace DrawnUi.Camera;

/// <summary>
/// Video recording quality presets
/// </summary>
public enum VideoQuality
{
    /// <summary>
    /// Low quality (480p @ 30fps)
    /// </summary>
    Low,
    
    /// <summary>
    /// Standard quality (720p @ 30fps)  
    /// </summary>
    Standard,
    
    /// <summary>
    /// High quality (1080p @ 30fps)
    /// </summary>
    High,
    
    /// <summary>
    /// Ultra quality (1080p @ 60fps or 4K @ 30fps)
    /// </summary>
    Ultra,
    
    /// <summary>
    /// Manual format selection using VideoFormatIndex
    /// </summary>
    Manual
}