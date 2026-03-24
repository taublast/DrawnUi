using SkiaSharp;

namespace DrawnUi.Camera;

/// <summary>
/// A <see cref="SkiaLayout"/> that rotates and repositions its content so child controls stay
/// visually upright when the device is rotated, matching the orientation of the camera feed.
///
/// Place this over your <see cref="SkiaCamera"/> view and call
/// <see cref="Layout(SKRect, DeviceOrientation, int, int)"/> whenever the camera preview rect
/// or device orientation changes.
/// </summary>
public class CameraOverlayLayout : SkiaLayout
{
    /// <summary>
    /// The effective drawing area used for measuring and rendering children,
    /// already adjusted for the current orientation.
    /// </summary>
    public SKRect RectLimits { get; protected set; }

    private DeviceOrientation _orientation;
    private int _videoWidth;
    private int _videoHeight;

    /// <summary>
    /// Updates the overlay layout for a new drawing rect and device orientation.
    /// Call this from <c>OnSizeAllocated</c> and your <c>OrientationChanged</c> handler.
    /// </summary>
    /// <param name="drawingRect">The current drawing bounds of the canvas content (pixels).</param>
    /// <param name="orientation">Current device orientation.</param>
    /// <param name="videoWidth">Raw video width, if known (informational, reserved).</param>
    /// <param name="videoHeight">Raw video height, if known (informational, reserved).</param>
    public void Layout(SKRect drawingRect, DeviceOrientation orientation, int videoWidth = 0, int videoHeight = 0)
    {
        _orientation = orientation;
        _videoWidth = videoWidth;
        _videoHeight = videoHeight;

        if (_orientation == DeviceOrientation.LandscapeLeft || _orientation == DeviceOrientation.LandscapeRight)
        {
            RectLimits = new SKRect(
                drawingRect.Top,
                drawingRect.Left,
                drawingRect.Top + drawingRect.Height,
                drawingRect.Left + drawingRect.Width
            );
        }
        else
        {
            RectLimits = drawingRect;
        }

        InvalidateMeasure();
    }

    protected override ScaledSize MeasureContent(IEnumerable<SkiaControl> children, SKRect rectForChildrenPixels, float scale)
    {
        if (RectLimits != SKRect.Empty)
        {
            return base.MeasureContent(children, RectLimits, scale);
        }

        return base.MeasureContent(children, rectForChildrenPixels, scale);
    }

    public override void Render(DrawingContext context)
    {
        if (RectLimits == SKRect.Empty)
        {
            return;
        }

        if (_orientation == DeviceOrientation.LandscapeLeft || _orientation == DeviceOrientation.LandscapeRight)
        {
            var rect = context.Destination;

            // AnchorX/Y = 0 so translation math is simpler (pivot at top-left).
            // Note: gesture hit-testing may not map correctly in landscape — known limitation.
            AnchorX = 0.0;
            AnchorY = 0.0;

            if (_orientation == DeviceOrientation.LandscapeLeft)
            {
                TranslationX = rect.Width / context.Scale - RectLimits.Left / context.Scale;
                TranslationY = RectLimits.Left / context.Scale;
                Rotation = 90;
            }
            else // LandscapeRight
            {
                TranslationX = -RectLimits.Left / context.Scale;
                TranslationY = rect.Height / context.Scale - RectLimits.Left / context.Scale;
                Rotation = -90;
            }
        }
        else
        {
            TranslationX = 0;
            TranslationY = 0;
            Rotation = 0;
        }

        base.Render(context.WithDestination(RectLimits));
    }
}
