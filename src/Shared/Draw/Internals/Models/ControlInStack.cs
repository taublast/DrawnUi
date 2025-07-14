using System.Numerics;

namespace DrawnUi.Draw;

public class ControlInStack
{
    public ControlInStack()
    {
        Drawn = new();
        Destination = new();
        Area = new();
    }

    /// <summary>
    /// Index inside enumerator that was passed for measurement OR index inside ItemsSource
    /// </summary>
    public int ControlIndex { get; set; }

    /// <summary>
    /// Measure result
    /// </summary>
    public ScaledSize Measured { get; set; }

    public SKRect Layout { get; set; }

    /// <summary>
    /// Available area for Arrange
    /// </summary>
    public SKRect Area { get; set; }

    /// <summary>
    /// PIXELS, this is to hold our arranged layout
    /// </summary>
    public SKRect Destination { get; set; }

    public Vector2 OffsetOthers { get; set; }

    /// <summary>
    /// Was drawn during the last frame
    /// </summary>
    public bool WasLastDrawn { get; set; }

    /// <summary>
    /// This will be null for recycled views
    /// </summary>
    public SkiaControl View { get; set; }

    /// <summary>
    /// Was used for actual drawing
    /// </summary>
    public DrawingRect Drawn { get; set; }

    /// <summary>
    /// For internal use by your custom controls
    /// </summary>
    public Vector2 Offset { get; set; }

    public bool WasMeasured { get; set; }

    public bool IsVisible { get; set; }

    public int ZIndex { get; set; }

    public int Column { get; set; }

    public int Row { get; set; }

    /// <summary>
    /// Cell's own visibility state (independent of viewport visibility)
    /// </summary>
    public bool IsContentVisible { get; set; } = true;
}
