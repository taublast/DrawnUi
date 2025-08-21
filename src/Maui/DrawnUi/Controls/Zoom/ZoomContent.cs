using DrawnUi.Draw;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

/// <summary>
/// Wrapper to zoom and pan content by changing the rendering scale so not affecting quality, this is not a transform.
/// 
/// DEBUG INSTRUCTIONS:
/// - Key debug outputs are enabled for pan movements and clamp operations
/// - Call DumpDebugState() manually to get a full state dump
/// - Uncomment debug sections in DrawViews, MeasureContent, ComputeContentScale for frame-by-frame debugging
/// - Watch for [ClampOffsetImage] CLAMPED! messages to see when pan limits are hit
/// - The DebugSummary provides comprehensive state when hitting pan limits
/// </summary>
public class ZoomContent : ContentLayout, ISkiaGestureListener
{
    public ZoomContent()
    {
        IsClippedToBounds = true;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Reset();
    }

    #region PROPERTIES

    public static readonly BindableProperty PanningModeProperty = BindableProperty.Create(
        nameof(PanningMode),
        typeof(PanningModeType),
        typeof(ZoomContent),
        PanningModeType.OneFinger);

    public PanningModeType PanningMode
    {
        get { return (PanningModeType)GetValue(PanningModeProperty); }
        set { SetValue(PanningModeProperty, value); }
    }

    public static readonly BindableProperty ZoomMinProperty = BindableProperty.Create(nameof(ZoomMin),
        typeof(double),
        typeof(ZoomContent),
        0.1);

    public double ZoomMin
    {
        get { return (double)GetValue(ZoomMinProperty); }
        set { SetValue(ZoomMinProperty, value); }
    }

    public static readonly BindableProperty ZoomMaxProperty = BindableProperty.Create(nameof(ZoomMax),
        typeof(double),
        typeof(ZoomContent),
        10.0);

    public double ZoomMax
    {
        get { return (double)GetValue(ZoomMaxProperty); }
        set { SetValue(ZoomMaxProperty, value); }
    }

    public static readonly BindableProperty ViewportZoomProperty = BindableProperty.Create(nameof(ViewportZoom),
        typeof(double), typeof(ZoomContent),
        1.0,
        propertyChanged: ApplyZoom);

    public double ViewportZoom
    {
        get { return (double)GetValue(ViewportZoomProperty); }
        set { SetValue(ViewportZoomProperty, value); }
    }

    public static readonly BindableProperty ZoomSpeedProperty = BindableProperty.Create(nameof(ZoomSpeed),
        typeof(double), typeof(ZoomContent),
        0.9);

    /// <summary>
    /// How much of finger movement will affect zoom change
    /// </summary>
    public double ZoomSpeed
    {
        get { return (double)GetValue(ZoomSpeedProperty); }
        set { SetValue(ZoomSpeedProperty, value); }
    }

    public static readonly BindableProperty ZoomLockedProperty = BindableProperty.Create(nameof(ZoomLocked),
        typeof(bool),
        typeof(ZoomContent),
        false);

    public bool ZoomLocked
    {
        get { return (bool)GetValue(ZoomLockedProperty); }
        set { SetValue(ZoomLockedProperty, value); }
    }

    public static readonly BindableProperty PanSpeedProperty = BindableProperty.Create(
        nameof(PanSpeed),
        typeof(double),
        typeof(ZoomContent),
        1.0);

    public double PanSpeed
    {
        get { return (double)GetValue(PanSpeedProperty); }
        set { SetValue(PanSpeedProperty, value); }
    }

    #endregion

    private static void ApplyZoom(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ZoomContent control)
        {
            Debug.WriteLine($"[ApplyZoom] ViewportZoom changed: {oldvalue} -> {newvalue}");
            control.Content?.InvalidateWithChildren();
            control.Update();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected float ScalePixels(float value)
    {
        var useScale = RenderingScale / (float)ViewportZoom;
        var scaleDifference = RenderingScale - useScale;
        return value * (1 + scaleDifference);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected float ScalePoints(double value)
    {
        var useScale = RenderingScale / (float)ViewportZoom;
        return (float)(value * useScale);
    }

    protected SKRect ContentScaledRect = SKRect.Empty;

    /// <summary>
    /// Computes the scaled destination rectangle and scale for content rendering.
    /// OffsetImage is expected to be in screen/pixel coordinates relative to the viewport.
    /// </summary>
    public (SKRect ScaledDestination, float Scale) ComputeContentScale(SKRect destination, float scale,
        SKPoint offsetPixels)
    {
        // Calculate the actual scale to use for rendering the content
        var useScale = scale / (float)ViewportZoom;

        // When scale=1 and ViewportZoom=2, we want the destination to be 2x bigger
        // The scaleDifference approach accounts for parent scaling
        var scaleDifference = scale - useScale;
        var destScale = 1 + scaleDifference;

        // Scale the destination rectangle
        ContentScaledRect = new SKRect(
            destination.Left * destScale,
            destination.Top * destScale,
            destination.Right * destScale,
            destination.Bottom * destScale);

        var scaledDestination = ContentScaledRect;

        var diffWidth = scaledDestination.Width - destination.Width;
        var diffHeight = scaledDestination.Height - destination.Height;

        // Calculate the center offset to center the zoomed content
        var centerOffsetX = -diffWidth / 2f;
        var centerOffsetY = -diffHeight / 2f;

        // Apply the pan offset directly (already in pixel coordinates)

        //var moveX = Math.Clamp(offsetPixels.X, -ContentSize.Pixels.Width / 2, ContentSize.Pixels.Width / 2);

        var offsetX = offsetPixels.X * destScale;
        var offsetY = offsetPixels.Y * destScale;

        scaledDestination.Offset(centerOffsetX, centerOffsetY); //zoomed to center

        // Apply the clamped offsets
        scaledDestination.Offset(offsetX, offsetY);

        //Trace.WriteLine($"[X] {moveX}");

        return (scaledDestination, useScale);
    }

    protected override int DrawViews(DrawingContext context)
    {
        //ClampOffsetImage(context.Destination, context.Scale);

        var use = ComputeContentScale(context.Destination, context.Scale, OffsetImage);

        var useScale = use.Scale;
        if (use.Scale < 1)
        {
            Content.Scale = 1 + ViewportZoom - context.Scale;
            useScale = 1;
        }
        else
        {
            Content.Scale = 1;
        }

        // DEBUG OUTPUT - Reduced frequency, uncomment when needed
        // Debug.WriteLine($"[DrawViews]");
        // Debug.WriteLine($"  context.Destination={context.Destination}");
        // Debug.WriteLine($"  context.Scale={context.Scale:F3}");
        // Debug.WriteLine($"  OffsetImage={OffsetImage}");
        // Debug.WriteLine($"  use.ScaledDestination={use.ScaledDestination}");
        // Debug.WriteLine($"  use.Scale={use.Scale:F3}");
        // Debug.WriteLine($"  Content.Scale={Content.Scale:F3}");

        // Additional debug info about content bounds - uncomment when needed
        // DebugContentBounds(context.Destination, context.Scale);

        return base.DrawViews(context.WithDestination(use.ScaledDestination).WithScale(useScale));
    }


    double _lastPinch = 0;
    double _zoom = 1;
    PointF _pinchCenter;
    bool _wasPinching;
    bool _wasPanning;

    /// <summary>
    /// Pan offset in screen/pixel coordinates
    /// </summary>
    protected SKPoint OffsetImage;

    protected PointF _panStarted;


    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type == TouchActionResult.Wheel)
        {
            _wasPinching = true;

            if (!ZoomLocked)
            {
                if (_lastPinch != 0 || args.Event.Wheel.Delta != 0)
                {
                    double delta = 0;
                    if (args.Event.Wheel.Delta != 0)
                    {
                        delta = args.Event.Wheel.Delta * ZoomSpeed;
                    }
                    else
                        delta = (args.Event.Wheel.Scale - _lastPinch) * ZoomSpeed;

                    if (PanningMode == PanningModeType.TwoFingers || PanningMode == PanningModeType.Enabled)
                    {
                        var moved = args.Event.Wheel.Center - _pinchCenter;
                        // Direct 1:1 movement in screen space
                        OffsetImage = new(
                            (float)(OffsetImage.X - moved.Width * PanSpeed),
                            (float)(OffsetImage.Y - moved.Height * PanSpeed));
                    }

                    _lastPinch = args.Event.Wheel.Scale;
                    _zoom += delta;
                    _pinchCenter = args.Event.Wheel.Center;

                    SetZoom(_zoom, false);
                    _zoom = ViewportZoom;
                }
                else
                {
                    _lastPinch = args.Event.Wheel.Scale;

                    if (!_wasPanning)
                        _pinchCenter = args.Event.Wheel.Center;
                    else
                    {
                        _pinchCenter = new(
                            args.Event.Wheel.Center.X - OffsetImage.X,
                            args.Event.Wheel.Center.Y - OffsetImage.Y);
                    }

                    LastValue = -1;
                }

                return this;
            }
        }
        else if (args.Type == TouchActionResult.Panning)
        {
            if (args.Event.Manipulation != null)
            {
                if (ZoomLocked)
                    return null;

                _zoom += args.Event.Manipulation.Scale * ZoomSpeed;
                _pinchCenter = args.Event.Manipulation.Center;
                SetZoom(_zoom, false);
                _zoom = ViewportZoom;
                _wasPinching = true;

                var deltaX = args.Event.Manipulation.Center.X - args.Event.Manipulation.PreviousCenter.X;
                var deltaY = args.Event.Manipulation.Center.Y - args.Event.Manipulation.PreviousCenter.Y;

                if (_zoom == 1)
                {
                    OffsetImage = SKPoint.Empty;
                }
                else
                {
                    if (PanningMode == PanningModeType.Enabled || PanningMode == PanningModeType.TwoFingers)
                    {
                        // Direct 1:1 movement in screen space
                        OffsetImage = new(
                            (float)(OffsetImage.X - deltaX * PanSpeed),
                            (float)(OffsetImage.Y - deltaY * PanSpeed));
                    }
                }
            }
            else
            {
                if (_wasPinching && args.Event.NumberOfTouches < 2)
                {
                    _wasPinching = false;
                    _wasPanning = false;
                }

                if (CompareDoubles(ViewportZoom, 1, 0.001))
                {
                    OffsetImage = SKPoint.Empty;
                    return null;
                }

                if ((PanningMode == PanningModeType.OneFinger && args.Event.NumberOfTouches < 2) ||
                    PanningMode == PanningModeType.Enabled)
                {
                    if (!_wasPanning)
                    {
                        _panStarted = args.Event.Location;
                        _wasPanning = true;
                        //Debug.WriteLine($"[Pan] Started at {_panStarted}");
                    }

                    var deltaX = args.Event.Location.X - _panStarted.X;
                    var deltaY = args.Event.Location.Y - _panStarted.Y;

                    _panStarted = args.Event.Location;

                    // Direct 1:1 movement in screen space for natural panning
                    // Finger "sticks" to the content
                    OffsetImage = new(
                        (float)(OffsetImage.X - deltaX * PanSpeed),
                        (float)(OffsetImage.Y - deltaY * PanSpeed));
                }

                Update();
                return this;
            }
        }
        else if (args.Type == TouchActionResult.Up)
        {
            if (args.Event.NumberOfTouches < 2)
            {
                if (ViewportZoom == 1)
                {
                    OffsetImage = SKPoint.Empty;
                }

                _wasPanning = false;
            }
        }

        if (!_wasPinching)
        {
            return base.ProcessGestures(args, apply);
        }

        if (_wasPinching && args.Type == TouchActionResult.Up && args.Event.NumberOfTouches < 2)
        {
            _lastPinch = 0;
            _wasPinching = false;

            if (ViewportZoom == 1.0)
            {
                OffsetImage = SKPoint.Empty;
                _wasPanning = false;
            }
        }

        return this;
    }

    public void Reset()
    {
        _lastPinch = 0;
        _zoom = 1;
        _wasPinching = false;
        _wasPanning = false;
        OffsetImage = SKPoint.Empty;
        ViewportZoom = 1;
    }

    public void SetZoom(double zoom, bool animate)
    {
        if (zoom < ZoomMin)
            zoom = ZoomMin;
        else if (zoom > ZoomMax)
            zoom = ZoomMax;

        Value = zoom;

        Debug.WriteLine($"[SetZoom] zoom={zoom:F3}, animate={animate}, LastValue={LastValue:F3}");

        if (LastValue != Value)
        {
            if (animate)
            {
                InitializeAnimator();

                var start = LastValue;
                var end = Value;

                if (_animatorValue.IsRunning)
                {
                    _animatorValue
                        .SetSpeed(50)
                        .SetValue(end);
                }
                else
                {
                    _animatorValue.Start(
                        (value) =>
                        {
                            ViewportZoom = value;
                            if (ViewportZoom <= 1.0)
                            {
                                OffsetImage = SKPoint.Empty;
                            }
                        },
                        start, end, 150, Easing.Linear);
                }
            }
            else
            {
                ViewportZoom = zoom;
                if (ViewportZoom <= 1.0)
                {
                    OffsetImage = SKPoint.Empty;
                }
            }
        }

        LastValue = Value;

        if (Value == 1)
        {
            _pinchCenter = PointF.Zero;
            OffsetImage = SKPoint.Empty;
        }

        Debug.WriteLine($"[SetZoom] Final ViewportZoom={ViewportZoom:F3}, OffsetImage={OffsetImage}");
    }

    protected RangeAnimator _animatorValue;

    /// <summary>
    /// Last ViewportZoom value we are animating from
    /// </summary>
    protected double LastValue = 0;

    protected double Value = 0;

    protected void InitializeAnimator()
    {
        if (_animatorValue == null)
        {
            _animatorValue = new(this);
        }
    }
}
