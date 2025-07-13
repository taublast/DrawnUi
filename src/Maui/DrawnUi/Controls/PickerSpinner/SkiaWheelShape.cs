using System.Numerics;

namespace DrawnUi.Controls;

/// <summary>
/// Custom SkiaShape that positions children in a circular arrangement around the wheel circumference.
/// Handles rotation and positioning calculations for the spinner wheel.
/// </summary>
public class SkiaWheelShape : SkiaShape
{
    public SkiaWheelShape()
    {
        Type = ShapeType.Circle;
        UseCache = SkiaCacheType.Image;
    }

    #region BINDABLE PROPERTIES

    public static readonly BindableProperty WheelRotationProperty = BindableProperty.Create(
        nameof(WheelRotation),
        typeof(double),
        typeof(SkiaWheelShape),
        0.0,
        propertyChanged: NeedRepaint);

    /// <summary>
    /// Gets or sets the current rotation of the wheel in degrees
    /// </summary>
    public double WheelRotation
    {
        get => (double)GetValue(WheelRotationProperty);
        set => SetValue(WheelRotationProperty, value);
    }

    public static readonly BindableProperty InverseVisualRotationProperty = BindableProperty.Create(
        nameof(InverseVisualRotation),
        typeof(bool),
        typeof(SkiaWheelShape),
        false,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Controls the visual orientation direction. False = normal, True = inverted
    /// </summary>
    public bool InverseVisualRotation
    {
        get => (bool)GetValue(InverseVisualRotationProperty);
        set => SetValue(InverseVisualRotationProperty, value);
    }

    #endregion

    #region LAYOUT AND DRAWING

    /// <summary>
    /// Renders children positioned around the wheel circumference with proper rotation
    /// </summary>
    /// <param name="context">Drawing context</param>
    /// <param name="skiaControls">Collection of controls to render</param>
    /// <returns>Number of rendered items</returns>
    protected int RenderViewsListBak(DrawingContext context, IEnumerable<SkiaControl> skiaControls)
    {
        if (skiaControls == null)
            return 0;

        var total = ItemsSource.Count;
        List<SkiaControlWithRect> tree = new();

        // Calculate wheel properties once
        var wheelRadius = (float)Math.Min(DrawingRect.Width, DrawingRect.Height) / 2;
        var wheelCenterX = DrawingRect.MidX;
        var wheelCenterY = DrawingRect.MidY;
        var anglePerItem = 360.0 / total;

        for (int index = 0; index < total; index++)
        {
            var child = ChildrenFactory.GetViewForIndex(index);

            child.OptionalOnBeforeDrawing();
            if (child.CanDraw)
            {
                // Calculate positioning
                var radius = (float)Math.Min(DrawingRect.Width, DrawingRect.Height);
                var textRadius = radius / 2;
                var itemAngle = index * anglePerItem - 90;

                // Apply inverse visual rotation if enabled
                if (InverseVisualRotation)
                {
                    itemAngle = -itemAngle;
                }

                var angleInRadians = itemAngle * Math.PI / 180.0;

                // Calculate offset from center
                var offsetXPixels = Math.Cos(angleInRadians) * textRadius + radius / 2;
                var offsetYPixels = Math.Sin(angleInRadians) * textRadius;

                // Convert pixels to units using the scale factor
                var scale = child.MeasuredSize.Scale;
                var offsetXUnits = offsetXPixels / scale;
                var offsetYUnits = offsetYPixels / scale;

                child.AnchorX = 0;
                child.TranslationX = offsetXUnits;
                child.TranslationY = offsetYUnits;

                // Calculate text rotation
                var textRotation = itemAngle;
                if (InverseVisualRotation)
                {
                    // When direction is reversed, flip the text orientation for readability
                    textRotation += 180;
                }
                child.Rotation = textRotation;

                // Set up clipping for pie slice shape
                child.Clipping = (path, childRect) =>
                {
                    child.ShouldClipAntialiased = true;
                    path.Reset();

                    var halfAngle = anglePerItem / 2.0;
                    var tanHalfAngle = Math.Tan(halfAngle * Math.PI / 180.0);
                    var centerY = childRect.MidY;

                    // Create a triangle/trapezoid from left (center direction) to right (outer edge)
                    var leftX = childRect.Left;
                    var rightX = childRect.Right;
                    var width = childRect.Width;

                    // Calculate how much the triangle should "spread" based on the angle
                    var spread = (float)(width * tanHalfAngle);

                    // Create triangle pointing toward wheel center (left side)
                    path.MoveTo(leftX, centerY);                    // Point toward center
                    path.LineTo(rightX, centerY - spread);          // Top right
                    path.LineTo(rightX, centerY + spread);          // Bottom right
                    path.Close();                                   // Back to center point
                };

                child.Render(context);

                tree.Add(new SkiaControlWithRect(child,
                    context.Destination,
                    child.DrawingRect,
                    index,
                    -1, // Default freeze index
                    child.BindingContext)); // Capture current binding context
            }
        }

        SetRenderingTree(tree);

        return total;
    }

    /// <summary>
    /// Renders children positioned around the wheel circumference with proper rotation
    /// </summary>
    /// <param name="context">Drawing context</param>
    /// <param name="skiaControls">Collection of controls to render</param>
    /// <returns>Number of rendered items</returns>
    protected override int RenderViewsList(DrawingContext context, IEnumerable<SkiaControl> skiaControls)
    {
        if (skiaControls == null)
            return 0;

        var total = ItemsSource.Count;
        List<SkiaControlWithRect> tree = new();

        // Calculate wheel properties once
        var wheelRadius = (float)Math.Min(DrawingRect.Width, DrawingRect.Height) / 2;
        var wheelCenterX = DrawingRect.MidX;
        var wheelCenterY = DrawingRect.MidY;
        var anglePerItem = 360.0 / total;

        for (int index = 0; index < total; index++)
        {
            var child = ChildrenFactory.GetViewForIndex(index);

            child.OptionalOnBeforeDrawing();
            if (child.CanDraw)
            {
                // Calculate positioning
                var radius = (float)Math.Min(DrawingRect.Width, DrawingRect.Height);
                var textRadius = radius / 2;
                var itemAngle = index * anglePerItem - 90;

                // Apply inverse visual rotation if enabled
                if (InverseVisualRotation)
                {
                    itemAngle = -itemAngle;
                }

                var angleInRadians = itemAngle * Math.PI / 180.0;

                // Calculate offset from center
                var offsetXPixels = Math.Cos(angleInRadians) * textRadius + radius / 2;
                var offsetYPixels = Math.Sin(angleInRadians) * textRadius;

                // Convert pixels to units using the scale factor
                var scale = child.MeasuredSize.Scale;
                var offsetXUnits = offsetXPixels / scale;
                var offsetYUnits = offsetYPixels / scale;

                child.AnchorX = 0;
                child.TranslationX = offsetXUnits;
                child.TranslationY = offsetYUnits;

                // Calculate text rotation
                var textRotation = itemAngle;
                if (InverseVisualRotation)
                {
                    // When direction is reversed, flip the text orientation for readability
                    textRotation += 180;
                }
                child.Rotation = textRotation;

                // Set up clipping for pie slice shape
                child.Clipping = (path, childRect) =>
                {
                    child.ShouldClipAntialiased = true;

                    path.Reset();

                    var halfAngle = anglePerItem / 2.0;
                    var tanHalfAngle = Math.Tan(halfAngle * Math.PI / 180.0);
                    var centerY = childRect.MidY;

                    // Create a triangle/trapezoid from right (center direction) to left (outer edge)
                    var leftX = childRect.Left;
                    var rightX = childRect.Right;
                    var width = childRect.Width;

                    // Calculate how much the triangle should "spread" based on the angle
                    var spread = (float)(width * tanHalfAngle);

                    // Create triangle pointing toward wheel center (right side is the point)
                    path.MoveTo(rightX, centerY);                   // Point toward center
                    path.LineTo(leftX, centerY - spread);           // Top left  
                    path.LineTo(leftX, centerY + spread);           // Bottom left
                    path.Close();                                   // Back to center point
                };

                child.Render(context);

                tree.Add(new SkiaControlWithRect(child,
                    context.Destination,
                    child.DrawingRect,
                    index,
                    -1, // Default freeze index
                    child.BindingContext)); // Capture current binding context
            }
        }

        SetRenderingTree(tree);

        return total;
    }

    #endregion
}
