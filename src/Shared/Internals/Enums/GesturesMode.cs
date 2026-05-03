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

#if !DRAWNUI_NET 
    /// <summary>
    /// Smart lock for working inside native controls like scroll view to share gestures with it if panning not consumed by your controls.
    /// </summary>
    SoftLock,
#endif

    /// <summary>
    /// Full lock input for self, useful inside scroll view, panning controls like slider etc
    /// </summary>
    Lock,
}
