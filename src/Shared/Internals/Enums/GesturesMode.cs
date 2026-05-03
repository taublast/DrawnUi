namespace DrawnUi.Draw;

/// <summary>
/// Used by the canvas, do not need this for drawn controls
/// </summary>
public enum GesturesMode
{
    /// <summary>
    /// Default
    /// </summary>
    Disabled,

    /// <summary>
    /// Gestures attached
    /// </summary>
    Enabled,

    /// <summary>
    /// Full lock input for self, useful inside scroll view, panning controls like slider etc
    /// </summary>
    Lock,
}
