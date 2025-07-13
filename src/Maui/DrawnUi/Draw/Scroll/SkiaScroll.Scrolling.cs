using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;




public partial class SkiaScroll
{

    public float ViewportOffsetY
    {
        get
        {
            return _viewportOffsetY;
        }

        set
        {
            if (_viewportOffsetY != value)
            {
                _viewportOffsetY = value;
                if (!NeedUpdate)
                    Update();
                //OnPropertyChanged();
            }
        }
    }

    protected float _viewportOffsetY;

    public float ViewportOffsetX
    {
        get
        {
            return _viewportOffsetX;
        }

        set
        {
            if (_viewportOffsetX != value)
            {
                _viewportOffsetX = value;
                if (!NeedUpdate)
                    Update();
                //OnPropertyChanged();
            }
        }
    }

    protected float _viewportOffsetX;

    protected virtual void InitializeViewport(float scale)
    {
        _loadMoreTriggeredAt = 0;

        ContentOffsetBounds = GetContentOffsetBounds();

        HasContentToScroll = ptsContentHeight > Viewport.Units.Height || ptsContentWidth > Viewport.Units.Width;

        _scrollMinX = ContentOffsetBounds.Left;
        if (_scrollMinX >= 0)
        {
            ViewportOffsetX = 0;
        }
        _scrollMaxX = 0;

        _scrollMinY = ContentOffsetBounds.Top;
        if (_scrollMinY >= 0)
        {
            ViewportOffsetY = 0;
        }
        _scrollMaxY = 0;

        IsViewportReady = true;
        onceAfterInitializeViewport = true;
    }

    bool onceAfterInitializeViewport;

    public bool IsViewportReady { get; protected set; }

    public LinearDirectionType ScrollingDirection { get; protected set; }

    protected virtual void CheckAndSetIsStillAnimating()
    {
        if (!_animatorFlingY.IsRunning
            && !_animatorFlingX.IsRunning
            && !_vectorAnimatorBounceY.IsRunning
            && !_vectorAnimatorBounceX.IsRunning)
        {
            IsAnimating = false;
        }
    }

    protected virtual void InitializeScroller(float scale)
    {
        if (_vectorAnimatorBounceY == null)
        {
            _vectorAnimatorBounceY = new(this)
            {
                OnStart = () =>
                {
                    IsAnimating = true;
                },
                OnStop = () =>
                {
                    UpdateLoadingLock(false);
                    IsSnapping = false;
                    if (_vectorAnimatorBounceY.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    ViewportOffsetY = (float)value; //not clamped
                }
            };

            _vectorAnimatorBounceX = new(this)
            {
                OnStart = () =>
                {
                    IsAnimating = true;
                },
                OnStop = () =>
                {
                    UpdateLoadingLock(false);
                    IsSnapping = false;
                    if (_vectorAnimatorBounceX.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    ViewportOffsetX = (float)value; //not clamped
                }
            };

            _animatorFlingX = new(this)
            {
                OnStart = () =>
                {
                    //_isSnapping = false;
                    IsAnimating = true;
                    OnScrollerStarted();
                },
                OnStop = () =>
                {
                    if (_animatorFlingX.WasStarted)
                    {
                        OnScrollerStopped();
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    var clamped = ClampOffset((float)value, 0, ContentOffsetBounds);
                    ViewportOffsetX = clamped.X;

                    OnScrollerUpdated();
                }
            };

            _animatorFlingY = new(this)
            {
                OnStart = () =>
                {
                    IsAnimating = true;
                    //_isSnapping = false;
                    OnScrollerStarted();
                },
                OnStop = () =>
                {
                    if (_animatorFlingY.WasStarted)
                    {
                        OnScrollerStopped();
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    var clamped = ClampOffset(0, (float)value, ContentOffsetBounds);
                    ViewportOffsetY = clamped.Y;

                    OnScrollerUpdated();
                }
            };

            _scrollerX = new(this)
            {
                OnStart = () =>
                {
                    IsAnimating = true;
                },
                OnStop = () =>
                {
                    IsSnapping = false;
                    if (_scrollerX.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                    //SkiaImageLoadingManager.Instance.IsLoadingLocked = false;
                }
            };

            _scrollerY = new(this)
            {
                OnStart = () =>
                {
                    IsAnimating = true;
                },
                OnStop = () =>
                {
                    IsSnapping = false;
                    if (_scrollerY.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }

                }
            };
        }

        if (_vectorAnimatorBounceY.IsRunning)
        {
            _vectorAnimatorBounceY.Stop();
        }
        if (_vectorAnimatorBounceX.IsRunning)
        {
            _vectorAnimatorBounceX.Stop();
        }

        SetDetectIndexChildPoint(TrackIndexPosition);

        this.UpdateVisibleIndex();

        ExecuteDelayedScrollOrders();

        if (CheckNeedToSnap())
            Snap(0);
    }

    /// <summary>
    /// Use Range scroller, offset in Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    public void ScrollToX(float offset, bool animate)
    {

        if (animate)
        {
            _scrollerX.Start(
                (value) =>
                {
                    ViewportOffsetX = (float)value;
                },
                InternalViewportOffset.Units.X, offset, (uint)ScrollingSpeedMs, ScrollingEasing);
        }
        else
        {
            ViewportOffsetX = offset;
            IsSnapping = false;
        }
    }

    /// <summary>
    /// Use Range scroller, offset in Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    protected void ScrollToY(float offset, bool animate)
    {

        if (animate)
        {
            _scrollerY.Start(
                (value) =>
                {
                    ViewportOffsetY = (float)value;
                },
                InternalViewportOffset.Units.Y, offset, (uint)ScrollingSpeedMs, ScrollingEasing);
        }
        else
        {
            ViewportOffsetY = offset;
            IsSnapping = false;
        }
    }

    protected virtual void OnScrollerStarted()
    {
        UpdateLoadingLock(new Vector2(
            _animatorFlingX.Parameters.InitialVelocity,
            _animatorFlingY.Parameters.InitialVelocity)
        );
    }

    protected virtual void OnScrollerUpdated()
    {
        UpdateLoadingLock(new Vector2(
            _animatorFlingX.CurrentVelocity,
            _animatorFlingY.CurrentVelocity));
    }

    protected virtual void BounceIfNeeded(ScrollFlingAnimator animator)
    {
        if (animator.SelfFinished)
        {
            var remainingVelocity = animator.Parameters.VelocityAt(animator.Speed);

            var velocity = remainingVelocity;

            if (Math.Abs(remainingVelocity) > MaxBounceVelocity)
            {
                velocity = Math.Sign(remainingVelocity) * MaxBounceVelocity;
            }

            var swipeThreshold = ThesholdSwipeOnUp * RenderingScale;
            if (Math.Abs(velocity) > swipeThreshold)
            {
                if (animator == _animatorFlingY)
                {
                    BounceY((float)ViewportOffsetY, _axis.Y, velocity);
                }
                else
                if (animator == _animatorFlingX)
                {
                    BounceX((float)ViewportOffsetX, _axis.X, velocity);
                }
            }

        }
    }


    protected virtual void OnScrollerStopped()
    {
        UpdateLoadingLock(false);

        //if (CheckNeedToSnap())
        //{
        //    Snap(SystemAnimationTimeSecs);
        //}
        //else
        {
            //scroll ended prematurely by our intent because it would end past the bounds
            if (Bounces)
            {

                if (_changeSpeed != null)
                {
                    BounceIfNeeded(_animatorFlingY);
                    BounceIfNeeded(_animatorFlingX);
                }

            }
        }
    }

    public virtual void ExecuteDelayedScrollOrders()
    {
        if (OrderedScrollToIndex.IsSet)
        {
            ExecuteScrollToIndexOrder();
        }
        else
        {
            ExecuteScrollToOrder();
        }
    }

    /*
    
    basic concept:

    when finger goes up we check where the scrolling would end with current velocity.
    if it is outside of the bounds we adjust the scroling duration so it ends near the bounds,
    otherwise we start scrolling animator as usual.

    when scrolling animator stops natually
    we check if we are outside of the bounds then start bouncing animator if needed

    when animator passes offsets to props they get clamped, see below

    if the finger goes down we stop animators unnaturally

    when the finger is down we can pan: we apply rubber clamp to offsets if bounce prop is true,
    otherwise we apply simple clamp

     */

    //deceleration slow 0.999
    // deceleration normal 0.998
    // deceleration fast 0.99

    void BounceX(float offsetFrom, float offsetTo, float velocity)
    {
        //Super.Log($"[SCROLL] {this.Tag} *BOUNCE* to {offsetTo.Y} v {velocity.Y}..");

        var displacement = offsetFrom - offsetTo;

        //Debug.WriteLine($"[BOUNCE] {offsetFrom} - {offsetTo} with {velocity}");

        if (displacement != 0)
        {
            var spring = new Spring((float)(1 * (1 + RubberDamping)), 200, (float)(0.5f * (1 + RubberDamping)));
            _animatorFlingX.Stop();
            _vectorAnimatorBounceX.Initialize(offsetTo, displacement, velocity, spring);
            _vectorAnimatorBounceX.Start();
        }
        else
        {
            IsSnapping = false;
        }
    }

    void BounceY(float offsetFrom, float offsetTo, float velocity)
    {
        //Super.Log($"[SCROLL] {this.Tag} *BOUNCE* to {offsetTo.Y} v {velocity.Y}..");

        var displacement = offsetFrom - offsetTo;

        //Debug.WriteLine($"[BOUNCE] {offsetFrom} - {offsetTo} with {velocity}");

        if (displacement != 0)
        {
            _animatorFlingY.Stop();
            var spring = new Spring((float)(1 * (1 + RubberDamping)), 200, (float)(0.5f * (1 + RubberDamping)));
            _vectorAnimatorBounceY.Initialize(offsetTo, displacement, velocity, spring);
            _vectorAnimatorBounceY.Start();
        }
        else
        {
            IsSnapping = false;
        }
    }

    /// <summary>
    /// This uses whole viewport size, do not use this for snapping
    /// </summary>
    /// <param name="overscrollPoint"></param>
    /// <param name="contentRect"></param>
    /// <param name="viewportSize"></param>
    /// <returns></returns>
    public static SKPoint GetClosestSidePoint(SKPoint overscrollPoint, SKRect contentRect, SKSize viewportSize)
    {
        SKPoint closestPoint = new SKPoint();

        // The overscrollPoint represents the negative of the content offset, so we need to reverse it for calculation
        SKPoint contentOffset = new SKPoint(-overscrollPoint.X, -overscrollPoint.Y);

        var width = contentRect.Width - viewportSize.Width;
        if (width < 0)
            width = 0;

        if (contentOffset.X < 0) //scrolling to  right
            closestPoint.X = contentRect.Left;
        else
        if (contentOffset.X > 0) //scrolling to left
            closestPoint.X = width;
        else
            closestPoint.X = contentOffset.X;

        var height = contentRect.Height - viewportSize.Height;
        if (height < 0)
            height = 0;

        if (contentOffset.Y < 0) //scrolling to bottom
            closestPoint.Y = contentRect.Top;
        else
        if (contentOffset.Y > 0) //scrolling to top
            closestPoint.Y = height;
        else
            closestPoint.Y = contentOffset.Y;

        // Reverse the offset back to the overscroll representation for the result
        closestPoint.X = -closestPoint.X;
        closestPoint.Y = -closestPoint.Y;

        return closestPoint;
    }

    public static SKPoint ClosestPoint(SKRect rect, SKPoint point)
    {
        SKPoint result = point;

        if (!rect.ContainsInclusive(point))
        {
            if (point.X < rect.Left)
                result.X = rect.Left;
            else if (point.X > rect.Right)
                result.X = rect.Right;

            if (point.Y < rect.Top)
                result.Y = rect.Top;
            else if (point.Y > rect.Bottom)
                result.Y = rect.Bottom;
        }

        return result;
    }

    /// <summary>
    /// Whether the scrolling offset in inside scrollable bounds or not
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    protected virtual bool OffsetOk(Vector2 offset)
    {
        if (offset.Y >= ContentOffsetBounds.Top && offset.Y <= ContentOffsetBounds.Bottom
        && offset.X >= ContentOffsetBounds.Left && offset.X <= ContentOffsetBounds.Right)
            return true;

        return false;
    }

    public bool OverScrolled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return OverscrollDistance != Vector2.Zero;
        }
    }

    protected float ptsContentWidth;

    protected float ptsContentHeight;

    /// <summary>
    /// There are the bounds the scroll offset can go to.. This is NOT the bounds for the whole content.
    /// </summary>
    public virtual SKRect GetContentOffsetBounds()
    {
        ptsContentWidth = ContentSize.Units.Width;
        ptsContentHeight = ContentSize.Units.Height;

        if (Orientation == ScrollOrientation.Vertical)
        {
            ptsContentHeight += HeaderSize.Units.Height + FooterSize.Units.Height + (float)ContentOffset;
        }

        if (Orientation == ScrollOrientation.Horizontal)
        {
            ptsContentWidth += HeaderSize.Units.Width + FooterSize.Units.Width + (float)ContentOffset;
        }

        var width = ptsContentWidth - Viewport.Units.Width;
        var height = ptsContentHeight - Viewport.Units.Height;

        if (height < 0)
            height = 0;

        if (width < 0)
            width = 0;

        var rect = new SKRect(-width, -height, 0, 0);

        return rect;
    }

    public Vector2 CalculateOverscrollDistance(float x, float y)
    {
        float overscrollX = 0f;
        float overscrollY = 0f;

        if (x > _scrollMaxX)
        {
            overscrollX = x - _scrollMaxX;
        }
        else if (x < _scrollMinX)
        {
            overscrollX = -(_scrollMinX - x);
        }

        if (y > _scrollMaxY)
        {
            overscrollY = y - _scrollMaxY;
        }
        else if (y < _scrollMinY)
        {
            overscrollY = -(_scrollMinY - y);
        }

        //Debug.WriteLine($"[OVERSCROLL] {overscrollY}");

        return new Vector2(overscrollX, overscrollY);
    }

    protected double _minVelocity = 1.5;

    private float _DecelerationRatio = 0.002f;
    public float DecelerationRatio
    {
        get
        {
            return _DecelerationRatio;
        }
        set
        {
            if (_DecelerationRatio != value)
            {
                _DecelerationRatio = value;
                OnPropertyChanged();
            }
        }
    }

    public void UpdateFriction()
    {
        var friction = FrictionScrolled;
        if (friction < 0.1)
        {
            //silent clamp
            friction = 0.1f;
        }

        DecelerationRatio = (float)friction / 100f; // 0.2 => 0.002
    }

    public virtual bool StartToFlingFrom(ScrollFlingAnimator animator, float from, float velocity)
    {
        var contentOffset = from;

        animator.InitializeWithVelocity(contentOffset, velocity, 1f - DecelerationRatio);

        if (PrepareToFlingAfterInitialized(animator))
        {
            animator.RunAsync(null).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    protected virtual async Task<bool> FlingFrom(ScrollFlingAnimator animator, float from, float velocity)
    {
        //todo - add cancellation support

        //	Trace.WriteLine($"[FLING] velocity {velocity}");

        var contentOffset = from;// new float((float)ViewportOffsetX, (float)ViewportOffsetY);

        animator.InitializeWithVelocity(contentOffset, velocity, 1f - DecelerationRatio);

        return await FlingAfterInitialized(animator);
    }

    protected virtual async Task<bool> FlingToAuto(ScrollFlingAnimator animator, float from, float to, float changeSpeedSecs = 0)
    {
        var velocity = animator.Parameters.VelocityToZero(from, to, changeSpeedSecs);

        animator.InitializeWithVelocity(from, velocity, 1f - DecelerationRatio);

        if (changeSpeedSecs > 0)
            animator.Speed = changeSpeedSecs;

        return await FlingAfterInitialized(animator);
    }

    protected virtual async Task<bool> FlingTo(ScrollFlingAnimator animator, float from, float to, float timeSeconds)
    {
        var velocity = animator.Parameters.VelocityTo(from, to, timeSeconds);

        animator.InitializeWithVelocity(from, velocity, 1f - DecelerationRatio);

        animator.Speed = timeSeconds;

        return await FlingAfterInitialized(animator);
    }

    protected virtual bool PrepareToFlingAfterInitialized(ScrollFlingAnimator animator)
    {
        var destination = animator.Parameters.Destination;
        bool offsetOk = true;

        var destinationPoint = SKPoint.Empty;
        if (animator == _animatorFlingX)
        {
            destinationPoint = new SKPoint(destination, 0);
            offsetOk = OffsetOk(new(destination, 0));
        }
        else
        if (animator == _animatorFlingY)
        {
            destinationPoint = new SKPoint(0, destination);
            offsetOk = OffsetOk(new(0, destination));
        }

        _changeSpeed = null;

        if (!offsetOk) //detected that scroll will end past the bounds
        {
            var contentRect = new SKRect(0, 0, ptsContentWidth, ptsContentHeight);
            var closestPoint = GetClosestSidePoint(destinationPoint, contentRect, Viewport.Units.Size);

            if (animator == _animatorFlingX)
            {
                _axis = _axis with { X = closestPoint.X };
                _changeSpeed = animator.Parameters.DurationToValue(closestPoint.X);
                animator.Speed = _changeSpeed.Value;
            }
            else
            if (animator == _animatorFlingY)
            {
                _axis = _axis with { Y = closestPoint.Y };
                _changeSpeed = animator.Parameters.DurationToValue(closestPoint.Y);
                animator.Speed = _changeSpeed.Value;
            }
        }

        return animator.Speed > 0;
    }

    protected async Task<bool> FlingAfterInitialized(ScrollFlingAnimator animator)
    {

        if (PrepareToFlingAfterInitialized(animator))
        {
            await animator.RunAsync(null);

            IsSnapping = false;

            return true;
        }

        return false;
    }

    /// <summary>
    /// We might order a scroll before the control was drawn, so it's a kind of startup position
    /// saved every time one calls ScrollTo
    /// </summary>
    protected ScrollToPointOrder OrderedScrollTo = ScrollToPointOrder.NotValid;

    /// <summary>
    /// We might order a scroll before the control was drawn, so it's a kind of startup position
    /// saved every time one calls ScrollToIndex
    /// </summary>
    protected ScrollToIndexOrder OrderedScrollToIndex;

    /// <summary>
    /// In Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    protected void ScrollToOffset(Vector2 targetOffset, float maxTimeSecs)
    {
        if (maxTimeSecs > 0 && Height > 0)
        {
            //_animatorFling.Stop();
            //var from = new Vector2((float)ViewportOffsetX, (float)ViewportOffsetY);
            //FlingToAuto(from, targetOffset, maxTimeSecs).ConfigureAwait(false);

            StopScrolling();
            ScrollToX(targetOffset.X, true);
            ScrollToY(targetOffset.Y, true);
        }
        else
        {
            ViewportOffsetX = targetOffset.X;
            ViewportOffsetY = targetOffset.Y;
            IsSnapping = false;

            this.UpdateVisibleIndex();
        }
    }

    public virtual void MoveToY(float value)
    {
        if (!ScrollLocked)
        {
            ViewportOffsetY = value;
        }
    }

    public virtual void MoveToX(float value)
    {
        if (!ScrollLocked)
        {
            ViewportOffsetX = value;

        }
    }

    public void ScrollToIndex(int index, bool animate, RelativePositionType option = RelativePositionType.Start)
    {
        //saving to use upon creating control if this was called before its internal structure was really created
        OrderedScrollToIndex = new()
        {
            Animated = animate,
            RelativePosition = option,
            Index = index
        };

        ExecuteScrollToIndexOrder();
    }

    public bool ExecuteScrollToOrder()
    {
        if (OrderedScrollTo.IsValid)
        {
            ScrollToOffset(new Vector2(OrderedScrollTo.Location.X,
                    OrderedScrollTo.Location.Y),
                OrderedScrollTo.MaxTimeSecs);
            OrderedScrollTo = ScrollToPointOrder.NotValid;
            return true;
        }

        return false;
    }

    public bool ExecuteScrollToIndexOrder()
    {
        if (OrderedScrollToIndex.IsSet)
        {
            //saving to use upon creating control if this was called before its internal structure was really created
            var offset = CalculateScrollOffsetForIndex(OrderedScrollToIndex.Index,
                OrderedScrollToIndex.RelativePosition);

            if (PointIsValid(offset))
            {
                var time = 0f;
                if (OrderedScrollToIndex.Animated)
                    time = SystemAnimationTimeSecs;

                ScrollTo(offset.X, offset.Y, time);
                OrderedScrollToIndex = ScrollToIndexOrder.Default;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Easy-to-use helper around using a lower level ScrollTo function
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animated"></param>
    public void SetContentOffset(Vector2 offset, bool animated)
    {
        var speed = animated ? AutoScrollingSpeedMs : 0;

        ScrollTo(offset.X, offset.Y, speed);
    }

    public void ScrollTo(float x, float y, float maxSpeedSecs)
    {
        StopScrolling();

        OrderedScrollTo = ScrollToPointOrder.ToCoords(x, y, maxSpeedSecs);

        if (!ExecuteScrollToOrder())
        {
            this.UpdateVisibleIndex();
        }
    }

    public void ScrollToTop(float maxTimeSecs)
    {
        if (Orientation == ScrollOrientation.Vertical)
        {
            ScrollTo(InternalViewportOffset.Units.X, 0, maxTimeSecs);
        }
        else
        if (Orientation == ScrollOrientation.Horizontal)
        {
            ScrollTo(0, InternalViewportOffset.Units.Y, maxTimeSecs);
        }
        else
        {
            ScrollTo(0, 0, maxTimeSecs);
        }
    }

    public void ScrollToBottom(float maxTimeSecs)
    {
        // For virtualized lists with unmeasured items, use estimated bottom position
        if (UseVirtual && Content is SkiaLayout layout && layout.IsTemplated && 
            layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            ScrollToEstimatedBottom(maxTimeSecs);
            return;
        }

        // Standard scroll to bottom using measured content
        if (Orientation == ScrollOrientation.Vertical)
        {
            ScrollTo(InternalViewportOffset.Units.X, _scrollMinY, maxTimeSecs);
        }
        else
        if (Orientation == ScrollOrientation.Horizontal)
        {
            ScrollTo(_scrollMinX, InternalViewportOffset.Units.Y, maxTimeSecs);
        }
        else
        {
            ScrollTo(_scrollMinX, _scrollMinY, maxTimeSecs);
        }
    }

    /// <summary>
    /// Scrolls to estimated bottom position for virtualized lists with unmeasured items
    /// </summary>
    private void ScrollToEstimatedBottom(float maxTimeSecs)
    {
        if (!(Content is SkiaLayout layout) || !layout.IsTemplated)
            return;

        var estimatedSize = layout.GetEstimatedContentSize(RenderingScale);
        
        Debug.WriteLine($"[ScrollToEstimatedBottom] Current content size: {ContentSize.Pixels.Width}x{ContentSize.Pixels.Height}, estimated: {estimatedSize.Pixels.Width}x{estimatedSize.Pixels.Height}");

        if (Orientation == ScrollOrientation.Vertical)
        {
            // Calculate estimated bottom position
            var estimatedContentHeight = estimatedSize.Pixels.Height;
            var viewportHeight = Viewport.Pixels.Height;
            var estimatedScrollY = -(estimatedContentHeight - viewportHeight);
            
            // Clamp to reasonable bounds
            var minScrollY = Math.Min(0, estimatedScrollY);
            
            Debug.WriteLine($"[ScrollToEstimatedBottom] Scrolling to estimated Y: {minScrollY} (content: {estimatedContentHeight}, viewport: {viewportHeight})");
            
            ScrollTo(InternalViewportOffset.Units.X, minScrollY, maxTimeSecs);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            // Calculate estimated right position
            var estimatedContentWidth = estimatedSize.Pixels.Width;
            var viewportWidth = Viewport.Pixels.Width;
            var estimatedScrollX = -(estimatedContentWidth - viewportWidth);
            
            // Clamp to reasonable bounds
            var minScrollX = Math.Min(0, estimatedScrollX);
            
            Debug.WriteLine($"[ScrollToEstimatedBottom] Scrolling to estimated X: {minScrollX} (content: {estimatedContentWidth}, viewport: {viewportWidth})");
            
            ScrollTo(minScrollX, InternalViewportOffset.Units.Y, maxTimeSecs);
        }
    }

    private bool _Snapped;
    public bool Snapped
    {
        get
        {
            return _Snapped;
        }
        set
        {
            if (_Snapped != value)
            {
                _Snapped = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _IsSnapping;
    public bool IsSnapping
    {
        get
        {
            return _IsSnapping;
        }
        set
        {
            if (_IsSnapping != value)
            {
                _IsSnapping = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAnimating { get; set; }
    public bool IsBouncing { get; set; }

    Vector2 _axis;
    double? _changeSpeed = null;

}
