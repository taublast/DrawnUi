using System.Numerics;

namespace DrawnUi.Draw;

public partial class SkiaScroll
{
    public bool ReverseGestures { get; set; }


    public override bool IsGestureForChild(SkiaControl child, SkiaGesturesParameters args)
    {
        if (lockHeader && child != Header)
        {
            return false;
        }

        var forChild = base.
            IsGestureForChild(child, args);
        if (!HeaderBehind && Header != null)
        {
            //block gestures for other children if from header got them
            if (child == this.Header && forChild)
            {
                lockHeader = true;
            }
        }

        return forChild;
    }

    public override bool IsGestureForChild(SkiaControlWithRect child, SKPoint point)
    {
        if (lockHeader && child.Control != Header)
        {
            return false;
        }

        var forChild = base.IsGestureForChild(child, point);
        if (!HeaderBehind && Header != null)
        {
            //block gestures for other children if from header got them
            if (child.Control == this.Header && forChild)
            {
                lockHeader = true;
            }
        }

        return forChild;
    }

    public override bool IsGestureForChild(ISkiaGestureListener listener, SKPoint point)
    {
        if (lockHeader && listener != Header)
        {
            return false;
        }

        var forChild = base.IsGestureForChild(listener, point);
        if (!HeaderBehind && Header != null)
        {
            //block gestures for other children if from header got them
            if (listener == this.Header && forChild)
            {
                lockHeader = true;
            }
        }

        return forChild;
    }

    protected bool InContact;

    protected bool LockGesturesUntilDown;

    bool _isPanning = false;
    bool _isFlinging = false;

    protected Vector2 _pannedOffset = Vector2.Zero;
    protected Vector2 _pannedVelocity = Vector2.Zero;
    protected Vector2 _pannedVelocityRemaining = Vector2.Zero;

    /// <summary>
    /// Had no panning just down+up with velocity more than threshold
    /// </summary>
    protected bool WasSwiping { get; set; }

    public bool IsUserFocused
    {
        get => isUserFocused;
        protected set
        {
            if (value == isUserFocused)
            {
                return;
            }

            isUserFocused = value;
            OnPropertyChanged();
        }
    }

    public bool IsUserPanning
    {
        get => isUserPanning;
        protected set
        {
            if (value == isUserPanning)
            {
                return;
            }

            isUserPanning = value;
            OnPropertyChanged();
        }
    }

    public bool HadDown { get; protected set; }

    protected void ResetOverscroll()
    {
        if (OverscrollDistance.X != 0)
        {
            ViewportOffsetX = 0;
        }
        if (OverscrollDistance.Y != 0)
        {
            ViewportOffsetY = 0;
        }
    }

    protected virtual void ResetPan()
    {
        //Trace.WriteLine("[SCROLL] Pan reset!");
        _pannedOffset = Vector2.Zero;
        _pannedVelocity = Vector2.Zero;
        _pannedVelocityRemaining = Vector2.Zero;

        ChildWasTapped = false;
        WasSwiping = false;
        IsUserFocused = true;
        IsUserPanning = false;
        ChildWasPanning = false;
        ChildWasTapped = false;

        StopScrolling();

        SwipeVelocityAccumulator.Clear();

        _panningLastDelta = Vector2.Zero;
        _panningStartOffsetPts = new(ViewportOffsetX, ViewportOffsetY);
        _panningCurrentOffsetPts =
        _panningStartOffsetPts; //new(InternalViewportOffset.Units.X, InternalViewportOffset.Units.Y);
    }

    protected void StopVelocityPanning()
    {
        IsUserPanning = false;
        _pannedOffset = Vector2.Zero;
        _pannedVelocityRemaining = Vector2.Zero;
    }

    #region MOUSE WHEEL

    /// <summary>
    /// How many points (not pixels) we want to scroll for 1 wheel line
    /// </summary>
    public static float WheelLineSize = 150;

    /// <summary>
    /// Scroll "like in Windows" by lines. No bouncing here.
    /// Mouse wheel UP gives positive value, mouse wheel DOWN give negative.
    /// </summary>
    /// <param name="value">In point how much to scroll</param>
    /// <param name="position">Reserved</param>
    void ApplyWheelScroll(float value, SKPoint position)
    {
        var offsetY = ViewportOffsetY;
        var offsetX = ViewportOffsetX;

        if (this.Orientation == ScrollOrientation.Vertical)
        {
            offsetY += WheelLineSize * Math.Sign(value);
        }
        else if (this.Orientation == ScrollOrientation.Horizontal)
        {
            offsetX += WheelLineSize * Math.Sign(value);
        }

        var clamped = ClampOffsetHard(offsetX, offsetY);

        ScrollTo(clamped.X, clamped.Y, AutoScrollingSpeedMs);
    }

    /// <summary>
    /// "Just clamp". Currently used for wheel scroll.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    protected virtual Vector2 ClampOffsetHard(float x, float y)
    {
        var clampedX = Math.Max(ContentOffsetBounds.Left, Math.Min(ContentOffsetBounds.Right, x));
        var clampedY = Math.Max(ContentOffsetBounds.Top, Math.Min(ContentOffsetBounds.Bottom, y));

        return new Vector2(clampedX, clampedY);
    }

    #endregion

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        var consumedDefault = BlockGesturesBelow ? this : null;

        if (LockGesturesUntilDown)
        {
            if (args.Type != TouchActionResult.Down)
                return consumedDefault;

            LockGesturesUntilDown = false;
        }

        //todo use number of gestures !!!
        if (args.Type == TouchActionResult.Down)
        {
            lockHeader = false;
            InContact = true;
            HadDown = true;
        }
        else if (args.Type == TouchActionResult.Up)
        {
            lockHeader = false;
            HadDown = false;
            InContact = false;
        }

        var preciseStop = false;
        ContentGesturesHit = false;
        var thisOffset = TranslateInputCoords(apply.ChildOffset);
        if (Content != null && Header != null)
        {
            var x = args.Event.Location.X + thisOffset.X;
            var y = args.Event.Location.Y + thisOffset.Y;

            ContentGesturesHit = Content.HitIsInside(x, y);
        }

        if (TouchEffect.LogEnabled)
        {
            Super.Log($"[SCROLL] {this.Tag} Got {args.Type} touches {args.Event.NumberOfTouches} {VelocityY}..");
        }

        if (args.Type == TouchActionResult.Down && RespondsToGestures)
        {
            ResetPan();
        }

        bool passedToChildren = false;

        ISkiaGestureListener PassToChildren()
        {
            passedToChildren = true;
            
            // Use plane-aware gesture processing for 3-plane virtualization
            if (UseVirtual && PlaneCurrent != null)
            {
                return ProcessGesturesForPlanes(args, apply);
            }
            
            return base.ProcessGestures(args, apply);
        }


        bool wrongDirection = false;
        var velocityDirection = ReverseGestures ? -1 : 1;

        if (args.Type == TouchActionResult.Panning)
        {
            if (args.Event.Manipulation != null && !ZoomLocked) //todo not only panning?
            {
                IsUserFocused = true;
                var scale = args.Event.Manipulation.Scale + this.ViewportZoom;
                Debug.WriteLine($"Scale: {scale}");
                var zoomed = SetZoom(scale);
            }

            if (IgnoreWrongDirection)
            {
                var panDirection = DirectionType.Vertical;
                if (Math.Abs(args.Event.Distance.Delta.X) > Math.Abs(args.Event.Distance.Delta.Y))
                {
                    panDirection = DirectionType.Horizontal;
                }

                if (Orientation == ScrollOrientation.Vertical && panDirection != DirectionType.Vertical)
                {
                    wrongDirection = true;
                }

                if (Orientation == ScrollOrientation.Horizontal && panDirection != DirectionType.Horizontal)
                {
                    wrongDirection = true;
                }
            }
        }

        if (args.Type == TouchActionResult.Up ||
            !IsUserPanning && (wrongDirection
                               || args.Type == TouchActionResult.Tapped || !RespondsToGestures))
        {
            var childConsumed = PassToChildren();
            if (childConsumed == this)
            {
                //BlockGesturesBelow fired
                childConsumed = null;
            }
            if (childConsumed != null)
            {
                if (args.Type == TouchActionResult.Panning)
                {
                    ChildWasPanning = true;
                }
                else if (args.Type == TouchActionResult.Tapped && HadDown)
                {
                    ChildWasTapped = true;
                }

                if (args.Type != TouchActionResult.Up)
                    return childConsumed;
            }
            else
            {
                ChildWasPanning = false;
            }
        }

        ISkiaGestureListener consumed = null;

        if (Orientation == ScrollOrientation.Vertical || Orientation == ScrollOrientation.Both)
        {
            VelocityY = (float)(args.Event.Distance.Velocity.Y / RenderingScale * velocityDirection);
        }

        if (Orientation == ScrollOrientation.Horizontal || Orientation == ScrollOrientation.Both)
        {
            VelocityX = (float)(args.Event.Distance.Velocity.X / RenderingScale * velocityDirection);
        }

        var hadNumberOfTouches = lastNumberOfTouches;
        lastNumberOfTouches = args.Event.NumberOfTouches;

        if (args.Type == TouchActionResult.Wheel && !ZoomLocked)
        {
            IsUserFocused = true;
            Debug.WriteLine($"Wheel: {args.Event.Wheel.Scale}");
            var zoomed = SetZoom(args.Event.Wheel.Scale);
            consumed = this;
        }
        else if (args.Event.NumberOfTouches < 2 && hadNumberOfTouches < 2)
        {
            switch (args.Type)
            {
                case TouchActionResult.Tapped:
                    //case TouchActionResult.LongPressing:
                    if (!passedToChildren && !IsUserPanning)
                    {
                        ResetPan();
                        //_panningStartOffsetPts = new(InternalViewportOffset.Units.X, InternalViewportOffset.Units.Y);
                        consumed = PassToChildren();
                        if (consumed == this)
                        {
                            //BlockGesturesBelow fired
                            consumed = null;
                        }
                    }

                    break;

                case TouchActionResult.Panning when RespondsToGestures:

                    if (ChildWasTapped)
                        break;

                    bool canPan = !ScrollLocked;

                    if (!IsUserPanning)
                    {
                        if (Orientation == ScrollOrientation.Vertical)
                        {
                            canPan &= Math.Abs(VelocityY) > ScrollVelocityThreshold;
                        }
                        else if (Orientation == ScrollOrientation.Horizontal)
                        {
                            canPan &= Math.Abs(VelocityX) > ScrollVelocityThreshold;
                        }
                        else if (Orientation == ScrollOrientation.Both)
                        {
                            canPan &= Math.Abs(VelocityX) > ScrollVelocityThreshold ||
                                      Math.Abs(VelocityY) > ScrollVelocityThreshold;
                        }
                    }

                    if (lockHeader && !CanScrollUsingHeader)
                    {
                        canPan = false;
                    }

                    if (canPan)
                    {
                        bool checkOverscroll = true;

                        if (!IsUserFocused)
                        {
                            ResetPan();
                            checkOverscroll = false;
                        }

                        if (!IsUserPanning)
                        {
                            if (IgnoreWrongDirection && wrongDirection)
                            {
                                IsUserFocused = false;
                                return consumedDefault;
                            }
                        }

                        IsUserPanning = true;

                        if (IgnoreWrongDirection && wrongDirection)
                        {
                            return consumedDefault;
                        }

                        SwipeVelocityAccumulator.CaptureVelocity(new(VelocityX, VelocityY));

                        var movedPtsX = (args.Event.Distance.Delta.X / RenderingScale) * ChangeDistancePanned *
                                        velocityDirection;
                        var movedPtsY = (args.Event.Distance.Delta.Y / RenderingScale) * ChangeDistancePanned *
                                        velocityDirection;

                        var interpolatedMoveToX = _panningLastDelta.X + (movedPtsX - _panningLastDelta.X) * 0.85f;
                        var interpolatedMoveToY = _panningLastDelta.Y + (movedPtsY - _panningLastDelta.Y) * 0.85f;

                        _panningLastDelta = new Vector2(interpolatedMoveToX, interpolatedMoveToY);

                        var moveTo = new Vector2(
                            (float)(_panningCurrentOffsetPts.X + interpolatedMoveToX),
                            (float)(_panningCurrentOffsetPts.Y + interpolatedMoveToY));

                        _panningCurrentOffsetPts = moveTo;

                        var clamped = ClampOffset(moveTo.X, moveTo.Y, ContentOffsetBounds);

                        if (!Bounces && checkOverscroll)
                        {
                            if (!AreEqual(clamped.X, moveTo.X, 0.5) && !AreEqual(clamped.Y, moveTo.Y, 0.5))
                            {
                                return consumedDefault;
                            }
                        }

                        //accumulate velocity for different gestures before drawing
                        _pannedVelocity = _pannedVelocity + new Vector2(VelocityX, VelocityY);

                        _pannedOffset = clamped; //will be applied once when drawing by ApplyPannedOffsetWithVelocity

                        consumed = this;

                        Repaint();
                    }

                    break;

                case TouchActionResult.Up when RespondsToGestures:
                    if ((!ChildWasTapped || OverScrolled) && (!ChildWasPanning || IsUserPanning))
                    {
                        StopVelocityPanning();

                        if (apply.AlreadyConsumed != null)
                        {
                            //if (CheckNeedToSnap())
                            //    Snap(SystemAnimationTimeSecs);
                            return consumedDefault;
                        }

                        bool canSwipe = true;
                        if (lockHeader && !CanScrollUsingHeader)
                        {
                            canSwipe = false;
                        }

                        if (!ScrollLocked && canSwipe)
                        {
                            var finalVelocity = SwipeVelocityAccumulator.CalculateFinalVelocity(this.MaxVelocity);

                            bool fling = false;
                            bool swipe = false;

                            if (!OverScrolled || Orientation == ScrollOrientation.Both)
                            {
                                var mainDirection = GetDirectionType(new Vector2(finalVelocity.X, finalVelocity.Y),
                                    DirectionType.None, 0.9f);

                                if (Orientation != ScrollOrientation.Both && !IsUserPanning)
                                {
                                    if (IgnoreWrongDirection)
                                    {
                                        if (Orientation == ScrollOrientation.Vertical &&
                                            mainDirection != DirectionType.Vertical)
                                        {
                                            Repaint();
                                            return consumedDefault;
                                        }

                                        if (Orientation == ScrollOrientation.Horizontal &&
                                            mainDirection != DirectionType.Horizontal)
                                        {
                                            Repaint();
                                            return consumedDefault;
                                        }
                                    }
                                }

                                var swipeThreshold = ThesholdSwipeOnUp * RenderingScale;
                                if ((Orientation == ScrollOrientation.Both
                                     || (Orientation == ScrollOrientation.Vertical &&
                                         mainDirection == DirectionType.Vertical)
                                     || (Orientation == ScrollOrientation.Horizontal &&
                                         mainDirection == DirectionType.Horizontal))
                                    && (Math.Abs(finalVelocity.X) > swipeThreshold ||
                                        Math.Abs(finalVelocity.Y) > swipeThreshold))
                                {
                                    swipe = true;
                                }
                            }

                            if (OverScrolled || swipe)
                            {
                                IsUserPanning = false;

                                bool bounceX = false, bounceY = false;
                                if (OverScrolled)
                                {
                                    var contentRect = new SKRect(0, 0, ptsContentWidth, ptsContentHeight);
                                    var closestPoint = GetClosestSidePoint(
                                        new SKPoint((float)InternalViewportOffset.Units.X,
                                            (float)InternalViewportOffset.Units.Y), contentRect, Viewport.Units.Size);

                                    var axis = new Vector2(closestPoint.X, closestPoint.Y);

                                    var velocityY = finalVelocity.Y * ChangeVelocityScrolled;
                                    var velocityX = finalVelocity.X * ChangeVelocityScrolled;

                                    if (Math.Abs(velocityX) > MaxBounceVelocity)
                                    {
                                        velocityX = Math.Sign(velocityX) * MaxBounceVelocity;
                                    }

                                    if (Math.Abs(velocityY) > MaxBounceVelocity)
                                    {
                                        velocityY = Math.Sign(velocityY) * MaxBounceVelocity;
                                    }

                                    if (OverscrollDistance.Y != 0)
                                    {
                                        bounceY = true;
                                        BounceY(InternalViewportOffset.Units.Y,
                                            axis.Y, velocityY);
                                    }

                                    if (OverscrollDistance.X != 0)
                                    {
                                        bounceX = true;
                                        BounceX(InternalViewportOffset.Units.X,
                                            axis.X, velocityX);
                                    }

                                    fling = true;
                                }

                                if (Orientation != ScrollOrientation.Neither)
                                {
                                    if (!Bounces)
                                    {
                                        if (Orientation == ScrollOrientation.Vertical && !bounceY)
                                        {
                                            if ((AreEqual(InternalViewportOffset.Pixels.Y, ContentOffsetBounds.Bottom,
                                                    1) && finalVelocity.Y > 0) ||
                                                (AreEqual(InternalViewportOffset.Pixels.Y, ContentOffsetBounds.Top,
                                                    1) && finalVelocity.Y < 0))
                                                return consumedDefault;
                                        }

                                        if (Orientation == ScrollOrientation.Horizontal && !bounceX)
                                        {
                                            if ((AreEqual(InternalViewportOffset.Pixels.X, ContentOffsetBounds.Right,
                                                    1) && finalVelocity.X > 0) ||
                                                (AreEqual(InternalViewportOffset.Pixels.X, ContentOffsetBounds.Left,
                                                    1) && finalVelocity.X < 0))
                                                return consumedDefault;
                                        }
                                    }

                                    var velocityY = finalVelocity.Y * ChangeVelocityScrolled;
                                    var velocityX = finalVelocity.X * ChangeVelocityScrolled;

                                    if (Math.Abs(velocityX) > _minVelocity && !bounceX)
                                    {
                                        IsUserFocused = false;
                                        _vectorAnimatorBounceX.Stop();
                                        fling = StartToFlingFrom(_animatorFlingX, ViewportOffsetX, velocityX);
                                    }

                                    if (Math.Abs(velocityY) > _minVelocity && !bounceY)
                                    {
                                        IsUserFocused = false;
                                        _vectorAnimatorBounceY.Stop();
                                        fling = StartToFlingFrom(_animatorFlingY, ViewportOffsetY, velocityY);
                                    }
                                }

                                if (fling)
                                {
                                    WasSwiping = true;
                                    consumed = this;
                                    passedToChildren = true;
                                }
                            }

                            IsUserFocused = false;
                            IsUserPanning = false;
                            Repaint(); //need to repaint again for snapping to work on next frame

                            if (!fling)
                            {
                                ResetOverscroll();
                            }
                        }

                        break;
                    }

                    break;

                case TouchActionResult.Wheel:

                    Debug.WriteLine($"Wheel {args.Event.Wheel.Delta}");

                    //just in case you might want to know where is the mouse cursor
                    var point = TranslateInputOffsetToPixels(args.Event.Wheel.Center, apply.ChildOffset);
                    var position = new SKPoint((point.X - DrawingRect.Left) / RenderingScale,
                        (point.Y - DrawingRect.Top) / RenderingScale);

                    ApplyWheelScroll(args.Event.Wheel.Delta, position);
                    consumed = this;
                    break;
            }
        }

        if (consumed != null || IsUserPanning)
        {
            if (consumed == null && args.Type != TouchActionResult.Up)
            {
                return this;
            }

            return consumed;
        }

        if (!passedToChildren) //will not pass when panning
        {
            return PassToChildren();
        }

        return consumedDefault;
    }

    /// <summary>
    /// Process gestures across all visible planes in correct Z-order
    /// </summary>
    protected virtual ISkiaGestureListener ProcessGesturesForPlanes(
        SkiaGesturesParameters args,
        GestureEventProcessingInfo apply)
    {
        var gestureLocation = args.Event.Location;
        
        // Process planes in Z-order (top to bottom): Forward -> Current -> Backward
        // Check Forward plane first (top layer)
        if (PlaneForward?.IsReady == true && PlaneForward.RenderTree != null)
        {
            if (IsGestureInPlane(PlaneForward, gestureLocation))
            {
                var consumed = ProcessGesturesForPlane(PlaneForward, args, apply);
                if (consumed != null)
                {
                    Debug.WriteLine($"Gesture consumed by Forward plane ({PlaneForward.Id})");
                    return consumed;
                }
            }
        }

        // Check Current plane (middle layer)
        if (PlaneCurrent?.IsReady == true && PlaneCurrent.RenderTree != null)
        {
            if (IsGestureInPlane(PlaneCurrent, gestureLocation))
            {
                var consumed = ProcessGesturesForPlane(PlaneCurrent, args, apply);
                if (consumed != null)
                {
                    Debug.WriteLine($"Gesture consumed by Current plane ({PlaneCurrent.Id})");
                    return consumed;
                }
            }
        }

        // Check Backward plane (bottom layer)
        if (PlaneBackward?.IsReady == true && PlaneBackward.RenderTree != null)
        {
            if (IsGestureInPlane(PlaneBackward, gestureLocation))
            {
                var consumed = ProcessGesturesForPlane(PlaneBackward, args, apply);
                if (consumed != null)
                {
                    Debug.WriteLine($"Gesture consumed by Backward plane ({PlaneBackward.Id})");
                    return consumed;
                }
            }
        }
        
        return null;
    }

    private long _lastVelocitySetTime = 0;
    private static long logLine;

    float _dragFriction = 0.9f;
    private bool isUserFocused;
    private bool isUserPanning;


    /// <summary>
    /// Applies panning on draw every frame, to be able to smoothly animate frames between panning changes.
    /// </summary>
    /// <param name="ctx"></param>
    protected virtual void ApplyPannedOffsetWithVelocity(SkiaDrawingContext ctx)
    {
        if (!IsAnimating
            && (_pannedOffset != Vector2.Zero
                || !CompareVectors(_pannedVelocityRemaining, Vector2.Zero, 0.00001f)))
        {
            if (_pannedOffset != Vector2.Zero)
            {
                ViewportOffsetX = _pannedOffset.X;
                ViewportOffsetY = _pannedOffset.Y;


                _pannedVelocityRemaining = _pannedVelocity;
                _pannedOffset = Vector2.Zero;
                _pannedVelocity = Vector2.Zero;
            }
            //else
            //{
            //    var deltaSeconds = (ctx.FrameTimeNanos - _lastVelocitySetTime) / 1000000000.0f;
            //    _pannedVelocityRemaining *= _dragFriction;
            //    var movedPtsX = (_pannedVelocityRemaining.X * deltaSeconds) * ChangeDistancePanned;
            //    var movedPtsY = (_pannedVelocityRemaining.Y * deltaSeconds) * ChangeDistancePanned;
            //    if (movedPtsY != 0 || movedPtsX != 0)
            //    {
            //        _panningCurrentOffsetPts = new(
            //            _panningCurrentOffsetPts.X + movedPtsX,
            //            _panningCurrentOffsetPts.Y + movedPtsY);
            //        var clamped = ClampOffset(_panningCurrentOffsetPts.X, _panningCurrentOffsetPts.Y, ContentOffsetBounds);
            //        ViewportOffsetX = clamped.X;
            //        ViewportOffsetY = clamped.Y;
            //    }
            //}

            _lastVelocitySetTime = ctx.FrameTimeNanos;
        }
    }


}
