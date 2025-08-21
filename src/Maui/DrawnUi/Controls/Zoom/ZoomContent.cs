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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clampf(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>
        /// Clamp OffsetImage so that the zoomed content stays within the viewport bounds.
        /// Uses the same scale math as ComputeContentScale to derive legal pan ranges.
        /// </summary>
        private void ClampOffsetImage(SKRect viewport, float scale)
        {
            // If not zoomed in, keep centered and no panning
            if (ViewportZoom <= 1.0)
            {
                if (!OffsetImage.Equals(SKPoint.Empty))
                {
                    Debug.WriteLine("[ClampOffsetImage] Not zoomed, resetting OffsetImage to (0,0)");
                }
                OffsetImage = SKPoint.Empty;
                return;
            }

            // Mirror ComputeContentScale's math
            var useScale = scale / (float)ViewportZoom; // child render scale
            var destScale = 1 + (scale - useScale);     // expansion of destination rect around center

            // With MAUI Content.Scale removed, the final visual size is governed by destScale only.
            // Allowed pan ranges in screen-space are ±(diff / (2 * destScale)).
            var diffW = viewport.Width * destScale - viewport.Width;
            var diffH = viewport.Height * destScale - viewport.Height;

            var canPanX = diffW > 0f;
            var canPanY = diffH > 0f;

            float minX = 0, maxX = 0, minY = 0, maxY = 0;

            if (canPanX)
            {
                var halfRangeX = (float)(diffW / (2f * destScale));
                minX = -halfRangeX;
                maxX = halfRangeX;
            }

            if (canPanY)
            {
                var halfRangeY = (float)(diffH / (2f * destScale));
                minY = -halfRangeY;
                maxY = halfRangeY;
            }

            var before = OffsetImage;

            // Force to zero on axes that can't pan (content not larger than viewport)
            var newX = canPanX ? Clampf(OffsetImage.X, minX, maxX) : 0f;
            var newY = canPanY ? Clampf(OffsetImage.Y, minY, maxY) : 0f;

            //if (!before.X.Equals(newX) || !before.Y.Equals(newY))
            //{
            //    Debug.WriteLine($"[ClampOffsetImage] CLAMPED! Before={before} After=({newX:F2},{newY:F2}) XRange=[{minX:F2},{maxX:F2}] YRange=[{minY:F2},{maxY:F2}] destScale={destScale:F3} zoom={ViewportZoom:F3} scale={scale:F3}");
            //}

            OffsetImage = new SKPoint(newX, newY);
        }

        private void TryClampWithLastViewport()
        {
            if (_lastViewportScale > 0 && _lastViewportRect.Width > 0 && _lastViewportRect.Height > 0)
            {
                ClampOffsetImage(_lastViewportRect, _lastViewportScale);
            }
        }


    protected override int DrawViews(DrawingContext context)
    {
        // First clamp with current viewport to avoid overscroll
        ClampOffsetImage(context.Destination, context.Scale);

        // Remember the last viewport/scale so gesture handlers can clamp immediately
        _lastViewportRect = context.Destination;
        _lastViewportScale = context.Scale;

        var use = ComputeContentScale(context.Destination, context.Scale, OffsetImage);

        var useScale = use.Scale;

        // Ensure we never pass a scale below 1 to avoid low-res rendering paths.
        //if (useScale < 1f) useScale = 1f;

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


        // Cache of the last viewport destination and scale used for drawing
        private SKRect _lastViewportRect;
        private float _lastViewportScale;


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

                        // Immediate clamp using last viewport (if available)
                        TryClampWithLastViewport();

                        //Debug.WriteLine($"[Pan/Wheel] Offset after move+clamp: {OffsetImage}");
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

                        // Immediate clamp using last viewport (if available)
                        TryClampWithLastViewport();
                        //Debug.WriteLine($"[Pan/Manipulation] Offset after move+clamp: {OffsetImage}");
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
