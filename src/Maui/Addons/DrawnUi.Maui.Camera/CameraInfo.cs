namespace DrawnUi.Camera;

/// <summary>
/// Represents information about an available camera device
/// </summary>
public class CameraInfo
{
    /// <summary>
    /// Camera device identifier
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Human-readable camera name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Camera position (Front/Back/Unknown)
    /// </summary>
    public CameraPosition Position { get; set; }

    /// <summary>
    /// Index of this camera in the available cameras list
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Whether this camera supports flash
    /// </summary>
    public bool HasFlash { get; set; }

    public override string ToString()
    {
        return $"{Name} ({Position})";
    }
}
