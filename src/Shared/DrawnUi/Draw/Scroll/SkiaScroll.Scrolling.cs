using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public partial class SkiaScroll
{
    /// <summary>
    /// Keeps visible content pinned when content above the viewport changes size (e.g. a virtualizing
    /// layout removes/inserts items above the viewport and re-flows the rest). Shifts the vertical
    /// viewport offset by <paramref name="deltaPoints"/>. Called by the content layout after it
    /// resolves the new position of the anchored (first-visible) item. No-op for tiny deltas.
    /// </summary>
    public void OffsetVisibleAnchorY(float deltaPoints)
    {
        if (Math.Abs(deltaPoints) < 0.01f)
            return;

        ViewportOffsetY += deltaPoints;

        // Panning is incremental over _panningCurrentOffsetPts; without shifting that baseline the
        // next pan move would compute from the stale base and revert the anchor correction.
        _panningCurrentOffsetPts.Y += deltaPoints;

        // A running fling/scroll animator writes ViewportOffsetY every frame from its own trajectory and
        // would instantly revert the anchor correction. Translate the active trajectory by the same delta
        // so it keeps targeting the same content after the window shifted.
        if (_animatorFlingY != null && _animatorFlingY.IsRunning)
        {
            _animatorFlingY.Shift(deltaPoints);

            // A fast fling gets its duration CUT to stop exactly at the content edge
            // (PrepareToFlingAfterInitialized). The content just grew past that edge, so the cut
            // trajectory would race at full speed and slam-stop at the OLD edge's content position.
            // Re-plan it with the remaining velocity once this frame's bounds are refreshed.
            if (_changeSpeed != null)
            {
                _replanFlingY = true;
                _replanVelocityY = _animatorFlingY.CurrentVelocity;
            }
        }

        // The direct range scroller (wheel steps, ScrollToY, snaps) also rewrites ViewportOffsetY per
        // frame from its own start/end range. Untranslated it overwrites this compensation with stale
        // pre-shift values: for one+ frames the viewport points outside the translated content — a
        // BLANK frame that makes the adapter release every in-use view, then mass-rebind + re-bake
        // next frame (the "cut lag" / "cells 11/24 -> 0/24" collapse).
        if (_scrollerY != null && _scrollerY.IsRunning)
            _scrollerY.Shift(deltaPoints);

        if (_vectorAnimatorBounceY != null && _vectorAnimatorBounceY.IsRunning)
            _vectorAnimatorBounceY.Stop(); // bounce target is stale after a content shift; let it re-evaluate
    }

    /// <summary>
    /// Set when a duration-cut fling must be re-planned against refreshed content bounds
    /// (content grew during the fling, e.g. backward LoadMore prepend). Consumed in Draw.
    /// </summary>
    protected bool _replanFlingY;

    /// <summary>
    /// Velocity captured when <see cref="_replanFlingY"/> was raised, used if the cut fling already
    /// self-finished by the time the replan is consumed.
    /// </summary>
    protected float _replanVelocityY;


    public float ViewportOffsetY
    {
        get { return _viewportOffsetY; }

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
        get { return _viewportOffsetX; }

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

    /// <summary>
    /// 0.0 - 1.0
    /// </summary>
    public double ScrollProgressY
    {
        get
        {
            if (ContentOffsetBounds.Height == 0)
            {
                return 0;
            }

            // ContentOffsetBounds is in POINTS (GetContentOffsetBounds builds it from Units) — the offset
            // must be POINTS too. Using Pixels here inflated progress by RenderingScale, pinning the
            // scrollbar thumb at the end for the bottom (1 - 1/scale) of the range on any scale > 1
            // ("thumb stuck at end after LoadMore" — content grew but progress stayed clamped at 1).
            return 1 - (ContentOffsetBounds.Height + InternalViewportOffset.Units.Y) / ContentOffsetBounds.Height;
        }
    }

    /// <summary>
    /// 0.0 - 1.0
    /// </summary>
    public double ScrollProgressX
    {
        get
        {
            if (ContentOffsetBounds.Width == 0)
            {
                return 0;
            }

            // POINTS, not Pixels — see ScrollProgressY.
            return 1 - (ContentOffsetBounds.Width + InternalViewportOffset.Units.X) / ContentOffsetBounds.Width;
        }
    }

    private SKRect _lastContentBounds = new SKRect();

    protected virtual void InitializeViewport(float scale)
    {
        _loadMoreBottomTriggeredAt = 0;
        _loadMoreTopTriggeredAt = 0;

        _lastContentBounds = ContentOffsetBounds;

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
            Repaint(); //we need this for after scrolling events
        }
    }

    protected virtual void InitializeScroller(float scale)
    {
        if (_vectorAnimatorBounceY == null)
        {
            _vectorAnimatorBounceY = new(this)
            {
                OnStart = () => { IsAnimating = true; },
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
                OnStart = () => { IsAnimating = true; },
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
                UseInterpolator = true,
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
                UseInterpolator = true,
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
                OnStart = () => { IsAnimating = true; },
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
                OnStart = () => { IsAnimating = true; },
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
                (value) => { ViewportOffsetX = (float)value; },
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
                (value) => { ViewportOffsetY = (float)value; },
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
                else if (animator == _animatorFlingX)
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
        else if (contentOffset.X > 0) //scrolling to left
            closestPoint.X = width;
        else
            closestPoint.X = contentOffset.X;

        var height = contentRect.Height - viewportSize.Height;
        if (height < 0)
            height = 0;

        if (contentOffset.Y < 0) //scrolling to bottom
            closestPoint.Y = contentRect.Top;
        else if (contentOffset.Y > 0) //scrolling to top
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
                                                && offset.X >= ContentOffsetBounds.Left &&
                                                offset.X <= ContentOffsetBounds.Right)
            return true;

        return false;
    }

    public bool OverScrolled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return OverscrollDistance != Vector2.Zero; }
    }

    protected float ptsContentWidth;

    protected float ptsContentHeight;

    /// <summary>
    /// There are the bounds the scroll offset can go to..
    /// This is NOT the bounds for the whole content.
    /// In POINTS not pixels!!!
    /// </summary>
    public virtual SKRect GetContentOffsetBounds()
    {
        ptsContentWidth = ContentSize.Units.Width;
        ptsContentHeight = ContentSize.Units.Height;

        // Managed (planes) virtualization renders per-plane sliding windows, so the shared measured
        // ContentSize is only a small seed window and can even collapse once everything is measured.
        // Derive the scroll extent from a STABLE average-item estimate (avg * item count) that matches
        // the per-plane window grid, so the scroll range always spans the whole virtual list.
        if (UseVirtual && Content is SkiaLayout vlayout && vlayout.IsTemplated
            && vlayout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
            && vlayout.EffectiveItemsSource != null && vlayout.EffectiveItemsSource.Count > 0)
        {
            float scale = (float)RenderingScale;
            if (scale <= 0) scale = 1;
            float avgPx = vlayout.GetAverageItemHeightPixels(scale);
            float spacingPx = (float)(vlayout.Spacing * scale);
            double estTotalPts = ((avgPx + spacingPx) * vlayout.EffectiveItemsSource.Count) / scale;

            if (Orientation == ScrollOrientation.Vertical && estTotalPts > ptsContentHeight)
                ptsContentHeight = (float)estTotalPts;
            else if (Orientation == ScrollOrientation.Horizontal && estTotalPts > ptsContentWidth)
                ptsContentWidth = (float)estTotalPts;
        }

        if (Orientation == ScrollOrientation.Vertical)
        {
            ptsContentHeight += HeaderSize.Units.Height + FooterSize.Units.Height + (float)ContentOffset;
        }

        if (Orientation == ScrollOrientation.Horizontal)
        {
            ptsContentWidth += HeaderSize.Units.Width + FooterSize.Units.Width + (float)ContentOffset;
        }

        var width = ptsContentWidth - MeasuredSize.Units.Width;
        var height = ptsContentHeight - MeasuredSize.Units.Height;

        if (height < 0)
            height = 0;

        if (width < 0)
            width = 0;

        // CLAMP TRAVEL TO MEASURED (not extent) while the bg pass is still measuring. ptsContentHeight above
        // is the full ESTIMATED extent — kept as-is so the scrollbar/anchor (TrackIndexPosition) stay stable.
        // But the scroll must not TRAVEL past the cells that actually exist in the structure, or it lands on
        // un-materialized space => blank. So narrow ONLY the offset bounds to the measured content; the unready
        // edge becomes a temporary content edge (normal bounce applies) and grows as measurement progresses.
        // Pure scroll-side, no structure writes => Android-safe and reproducible single-thread.
        // CLAMP TRAVEL TO MEASURED (not extent) while the structure is incomplete. ptsContentHeight above is
        // the full ESTIMATED extent — kept as-is so the scrollbar/anchor (TrackIndexPosition) stay stable. But
        // the scroll must not TRAVEL past the cells that actually exist in the structure, or it lands on
        // un-materialized space => blank. Narrow ONLY the offset bounds to the measured content; the unready
        // edge becomes a temporary content edge (normal bounce + LoadMore apply) and grows as measurement
        // progresses. Gate on LastMeasuredIndexLocal < Count-1 (reliable "incomplete"), NOT IsBackgroundMeasuring
        // (that flag is False during the blank window). Pure scroll-side, no structure writes => thread-safe.
        // (windowed sources are exempt: their extent is virtual — see the window extension below —
        // and clamping to the measured slice would re-create the hard wall the extension removes)
        if (Content is SkiaLayout mvLayout && mvLayout.IsTemplated
            && mvLayout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
            && mvLayout.ItemsWindow == null
            && mvLayout.EffectiveItemsSource != null && mvLayout.EffectiveItemsSource.Count > 0
            && mvLayout.LastMeasuredIndexLocal < mvLayout.EffectiveItemsSource.Count - 1)
        {
            double measuredEndPts = mvLayout.GetMeasuredContentEnd(); // points, top of last measured cell
            if (measuredEndPts > 0)
            {
                float scaleC = (float)RenderingScale; if (scaleC <= 0) scaleC = 1;
                double oneCellPts = mvLayout.GetAverageItemHeightPixels(scaleC) / scaleC + mvLayout.Spacing;
                // same extras the extent got, so the limit is in the same coordinate space as 'height'/'width'
                double extras = (Orientation == ScrollOrientation.Vertical
                                    ? HeaderSize.Units.Height + FooterSize.Units.Height
                                    : HeaderSize.Units.Width + FooterSize.Units.Width) + (float)ContentOffset;
                double measuredTravel = (measuredEndPts + oneCellPts + extras)
                                        - (Orientation == ScrollOrientation.Vertical
                                            ? MeasuredSize.Units.Height : MeasuredSize.Units.Width);
                if (Orientation == ScrollOrientation.Vertical)
                {
                    if (measuredTravel >= 0 && measuredTravel < height)
                        height = (float)measuredTravel; // narrows bounds.Top only; ptsContentHeight (extent) untouched
                }
                else
                {
                    if (measuredTravel >= 0 && measuredTravel < width)
                        width = (float)measuredTravel;
                }
            }
        }

        // WINDOWED SOURCE = VIRTUAL EXTENT. The layout materializes only a physical slice (e.g. 128)
        // of a bigger ItemsSource (e.g. 1000). Reporting the SLICE as the scroll extent makes the
        // scroll STOP DEAD at the slice end whenever slides can't outrun the user (guaranteed on
        // single-threaded targets: measurement/slides only progress between frames). The slice end is
        // NOT the end of content — extend forward travel past the slice so slides can chase the offset,
        // each head trim compensating it back: the traveler never meets a wall.
        // The extension is CAPPED at a chase headroom (2 viewports, always > one slide batch): an
        // unbounded remainder estimate let the viewport strand deep in unmaterialized space (blank
        // frames, adapter in-use collapse, edge LoadMore starved on vis=-1 — harness regressions).
        // Headroom advances as slides land, so sustained scrolling still traverses the whole source.
        // Backward needs no extension: head inserts land at local 0 with offset compensation, and
        // backward slides fire at the local-top margin before the clamp is ever reached.
        _windowTravelExtension = 0;
        if (Content is SkiaLayout wLayout && wLayout.ItemsWindow != null && wLayout.ItemsSource != null)
        {
            int below = wLayout.ItemsSource.Count - wLayout.ItemsWindow.WindowEnd;
            if (below > 0)
            {
                float scaleW = (float)RenderingScale;
                if (scaleW <= 0) scaleW = 1;
                double cellPts = wLayout.GetAverageItemHeightPixels(scaleW) / scaleW + wLayout.Spacing;
                double headroomPts = 2 * (Orientation == ScrollOrientation.Vertical
                    ? MeasuredSize.Units.Height : MeasuredSize.Units.Width);
                double extension = Math.Min(below * cellPts, headroomPts);
                _windowTravelExtension = (float)extension;
                if (Orientation == ScrollOrientation.Vertical)
                    height += (float)extension;
                else
                    width += (float)extension;
            }
        }

        var rect = new SKRect(-width, -height, 0, 0);

        return rect;
    }


    /// <summary>
    ///
    /// In POINTS not pixels!!!
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
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

        //if (overscrollY != 0)
        //{
        //    Debug.WriteLine($"[SCROLL] overscroll Y {overscrollY}");
        //}

        return new Vector2(overscrollX, overscrollY);
    }

    protected double _minVelocity = 1.5;

    private float _DecelerationRatio = 0.002f;

    public float DecelerationRatio
    {
        get { return _DecelerationRatio; }
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

    /// <summary>
    /// Rendering scales below this use the pixel-aware fling finish (short ease-out instead of the
    /// asymptotic sub-pixel tail, which shows as jagged 1px hops on low-density displays).
    /// Scales at or above it keep the classic smooth exponential die-out. Default 1.5.
    /// </summary>
    public static float PixelAwareFlingFinishBelowScale = 1.5f;

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

        var contentOffset = from; // new float((float)ViewportOffsetX, (float)ViewportOffsetY);

        animator.InitializeWithVelocity(contentOffset, velocity, 1f - DecelerationRatio);

        return await FlingAfterInitialized(animator);
    }

    protected virtual async Task<bool> FlingToAuto(ScrollFlingAnimator animator, float from, float to,
        float changeSpeedSecs = 0)
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
        // Pixel-aware finish replaces the sub-pixel exponential tail with a short ease-out landing on
        // the px grid — ONLY on low-density displays (desktop 100-125%), where the tail renders as
        // visible 1px hops with growing pauses ("jagged fling ending"). On high-DPI screens the same
        // tail is sub-visual and reads as the natural smooth iOS-like die-out — keep it (device-tested:
        // the finish there trades smoothness for nothing). 0 disables.
        animator.PixelsScale = RenderingScale < PixelAwareFlingFinishBelowScale ? RenderingScale : 0;

        var destination = animator.Parameters.Destination;
        bool offsetOk = true;

        var destinationPoint = SKPoint.Empty;
        if (animator == _animatorFlingX)
        {
            destinationPoint = new SKPoint(destination, 0);
            offsetOk = OffsetOk(new(destination, 0));
        }
        else if (animator == _animatorFlingY)
        {
            destinationPoint = new SKPoint(0, destination);
            offsetOk = OffsetOk(new(0, destination));
        }

        _changeSpeed = null;

        // A windowed source publishes only a SLICE: ContentOffsetBounds ends at the slice plus a
        // capped chase headroom (2 viewports, see GetContentOffsetBounds), NOT at the end of the real
        // content. Cutting the fling against that provisional wall killed it in ~20ms — the scroll
        // froze dead at every window slide/engage until the user touched again. There IS more content
        // below, so let the fling decelerate naturally and let the per-frame clamp hold it while the
        // slides chase; the wall moves forward as they land.
        if (!offsetOk && animator == _animatorFlingY && _windowTravelExtension > 0 &&
            destination < ContentOffsetBounds.Top)
        {
            offsetOk = true;
        }

        if (!offsetOk) //detected that scroll will end past the bounds
        {
            var clamped = ClampOffset((float)destinationPoint.X, (float)destinationPoint.Y, ContentOffsetBounds, true);
            var closestPoint = new SKPoint(clamped.X, clamped.Y);

            if (animator == _animatorFlingX)
            {
                _axis = _axis with { X = closestPoint.X };
                _changeSpeed = animator.Parameters.DurationToValue(closestPoint.X);
                animator.Speed = _changeSpeed.Value;
            }
            else if (animator == _animatorFlingY)
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
    public ScrollToPointOrder OrderedScrollTo = ScrollToPointOrder.NotValid;

    /// <summary>
    /// We might order a scroll before the control was drawn, so it's a kind of startup position
    /// saved every time one calls ScrollToIndex
    /// </summary>
    protected ScrollToIndexOrder OrderedScrollToIndex;

    // Homing state for a pending OrderedScrollToIndex: the order is held until ARRIVAL (so the
    // LoadMore gate stays closed for the whole animated travel and the retry can re-aim if content
    // resizes mid-flight). Tracks the offset actually issued plus a stall watchdog for scrolls that
    // clamp short of the computed target.
    private SKPoint _orderedIssuedTarget;
    private bool _orderedIssuedTargetValid;
    private SKPoint _orderedWatchdogOffset;
    private int _orderedStalledFrames;
    private int _orderedReissues;

    /// <summary>
    /// Clears a pending ScrollToIndex order and its homing/watchdog state.
    /// </summary>
    protected void ClearOrderedScrollToIndex()
    {
        OrderedScrollToIndex = ScrollToIndexOrder.Default;
        _orderedIssuedTargetValid = false;
        _orderedStalledFrames = 0;
        _orderedReissues = 0;
    }

    public bool OrderedScrollToIndexIsSet
    {
        get
        {
            return OrderedScrollToIndex.IsSet;
        }
    }

    /// <summary>
    /// True while an explicit ScrollToIndex order is pending. The head-insert viewport pin
    /// (CommitPendingHeadInsert) is suppressed when this is set: an explicit scroll target and the
    /// position-preserving pin are mutually exclusive intents — honoring both in the same frame causes
    /// a 1-frame blink (e.g. a just-sent message that orders ScrollToIndex(0)).
    /// </summary>
    public bool HasPendingScrollOrder => OrderedScrollToIndex.IsSet;

    /// <summary>
    /// In Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    /// <summary>
    /// Forward chase-headroom (points) currently included in ContentOffsetBounds for a windowed
    /// source (see the virtual-extent block in GetContentOffsetBounds). Zero when no window or the
    /// window reached the source end. INCREMENTAL user travel (wheel/pan/fling) may use it — slides
    /// chase the offset; ABSOLUTE programmatic targets (ScrollToIndex) must NOT aim into it: a jump
    /// clamped against the extended bounds landed the viewport in the VOID past the last item
    /// (empty screen, vis=-1) until user interaction pulled it back.
    /// </summary>
    protected float _windowTravelExtension;

    /// <summary>ContentOffsetBounds with the window chase-headroom removed: the REAL content travel.</summary>
    protected SKRect GetHardContentOffsetBounds()
    {
        var b = ContentOffsetBounds;
        if (_windowTravelExtension > 0)
        {
            if (Orientation == ScrollOrientation.Vertical)
                return new SKRect(b.Left, b.Top + _windowTravelExtension, b.Right, b.Bottom);
            return new SKRect(b.Left + _windowTravelExtension, b.Top, b.Right, b.Bottom);
        }
        return b;
    }

    protected void ScrollToOffset(Vector2 targetOffset, float maxTimeSecs)
    {
        StopScrolling();

        if (maxTimeSecs > 0 && Height > 0)
        {
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

    public void ScrollToIndex(int index, bool animate, RelativePositionType option = RelativePositionType.Start,
        bool clamp = false)
    {
        // Built-in source window engaged: the public API speaks GLOBAL (ItemsSource-space) indices.
        // Resident target -> plain local mapping; non-resident -> the window rebases centered on it
        // (staged full replace) and the ordered scroll below waits that out before resolving geometry.
        if (Content is SkiaLayout { ItemsWindow: not null } windowed)
        {
            index = windowed.ItemsWindow.MapToLocalForScroll(index);
        }

        //saving to use upon creating control if this was called before its internal structure was really created
        OrderedScrollToIndex = new()
        {
            Animated = animate,
            RelativePosition = option,
            Index = index,
            Clamp = clamp
        };
        _orderedIssuedTargetValid = false; // fresh order: previous homing state is void
        _orderedStalledFrames = 0;
        _orderedReissues = 0;

        ExecuteScrollToIndexOrder();
    }

    public bool ExecuteScrollToOrder()
    {
        if (OrderedScrollTo.IsValid)
        {
            ScrollToOffset(new Vector2(OrderedScrollTo.Location.X, OrderedScrollTo.Location.Y), OrderedScrollTo.MaxTimeSecs);
            OrderedScrollTo = ScrollToPointOrder.NotValid;
            return true;
        }

        return false;
    }

    public bool ExecuteScrollToIndexOrder()
    {
        if (OrderedScrollToIndex.IsSet)
        {
            if (Content is SkiaLayout layout)
            {
                if (layout.HasPendingStructureChanges)
                    return false;

                // MeasureVisible: the target's geometry is REAL only once measurement has passed it
                // (LastMeasuredIndexLocal); before that its Destination and the content bounds are estimates.
                // The old gate ("IsBackgroundMeasuring && progress < index") had a hole: background idle +
                // target unmeasured passed through, the offset was computed from estimates, fired and
                // self-cleared — a window jump (ScrollToOldest) landed short and never corrected. Hold the
                // order until the target is really measured; if measurement is idle, KICK it toward the
                // target (the draw-side restart alone starves behind pending changes). Repaint keeps retry
                // frames coming while we hold. Bounded: measured progress is monotonic per kick; if a kick
                // is impossible (no constraints yet), fall through and resolve with estimates once instead
                // of deadlocking.
                if (layout.IsTemplated && layout.EffectiveItemsSource != null
                    && OrderedScrollToIndex.Index < layout.EffectiveItemsSource.Count
                    && layout.LastMeasuredIndexLocal < OrderedScrollToIndex.Index)
                {
                    if (layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                    {
                        if (layout.IsBackgroundMeasuring || layout.KickBackgroundMeasurement())
                        {
                            Repaint();
                            return false;
                        }
                    }
                    else
                    {
                        // Sync strategies (MeasureFirst): after a windowed jump's full-replace reset the
                        // content is unmeasured for a frame or two — resolving now clamps the target into
                        // "already there" and self-clears without moving (jump lands short). The next
                        // measure pass restores the frontier; just hold the order until then.
                        Repaint();
                        return false;
                    }
                }
            }

            //saving to use upon creating control if this was called before its internal structure was really created
            var offset = CalculateScrollOffsetForIndex(OrderedScrollToIndex.Index,
                OrderedScrollToIndex.RelativePosition);

            if (PointIsValid(offset))
            {
                // A jump target must land INSIDE real content: clamp against the HARD bounds
                // (chase-headroom removed) — a target clamped by the downstream ScrollToY against
                // the EXTENDED bounds landed the viewport in the void past the last item.
                var hard = GetHardContentOffsetBounds();
                offset = new SKPoint(
                    Math.Clamp(offset.X, hard.Left, hard.Right),
                    Math.Clamp(offset.Y, hard.Top, hard.Bottom));

                if (AreEqual((float)InternalViewportOffset.Units.X, offset.X, 0.5)
                    && AreEqual((float)InternalViewportOffset.Units.Y, offset.Y, 0.5))
                {
                    ClearOrderedScrollToIndex();
                    return true;
                }

                // The order stays PENDING until we actually arrive (checked above), not just until the
                // scroll is issued. Clearing on issue opened the LoadMore gate for the whole animated
                // travel: an inverted-chat jump to the oldest starts its animation at the newest edge —
                // exactly the LoadNewer trigger zone — so LoadMore fired one frame into the flight, grew
                // the content mid-travel and the jump landed mid-list. Held, the gate stays closed and
                // the per-frame retry re-aims automatically if content resizes during the flight.
                var alreadyHoming = _orderedIssuedTargetValid
                                    && AreEqual(_orderedIssuedTarget.X, offset.X, 1f)
                                    && AreEqual(_orderedIssuedTarget.Y, offset.Y, 1f);

                if (alreadyHoming)
                {
                    // Watchdog: the issued animation can be cut short (a competing animator/stop) or
                    // clamp short of the computed target — then "arrived" never trips. If the viewport
                    // stops moving while the order is still pending: RE-ISSUE the scroll (bounded), and
                    // only after retries are exhausted accept the landing, so a genuinely clamped target
                    // can't gate LoadMore forever.
                    var currentX = (float)InternalViewportOffset.Units.X;
                    var currentY = (float)InternalViewportOffset.Units.Y;
                    if (AreEqual(currentX, _orderedWatchdogOffset.X, 0.05f)
                        && AreEqual(currentY, _orderedWatchdogOffset.Y, 0.05f))
                    {
                        if (++_orderedStalledFrames > 10)
                        {
                            if (_orderedReissues < 2)
                            {
                                _orderedReissues++;
                                _orderedIssuedTargetValid = false; // force a fresh ScrollTo on the next retry
                                _orderedStalledFrames = 0;
                            }
                            else
                            {
                                ClearOrderedScrollToIndex();
                                return true;
                            }
                        }
                    }
                    else
                    {
                        _orderedStalledFrames = 0;
                        _orderedWatchdogOffset = new SKPoint(currentX, currentY);
                    }

                    Repaint(); // keep retry frames coming until arrival
                    return false;
                }

                var time = 0f;
                if (OrderedScrollToIndex.Animated)
                    time = SystemAnimationTimeSecs;

                ScrollTo(offset.X, offset.Y, time, OrderedScrollToIndex.Clamp);
                _orderedIssuedTarget = offset;
                _orderedIssuedTargetValid = true;
                _orderedStalledFrames = 0;
                _orderedWatchdogOffset = new SKPoint((float)InternalViewportOffset.Units.X,
                    (float)InternalViewportOffset.Units.Y);
                Repaint();
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Instant scroll to top
    /// </summary>
    public virtual void ResetPosition()
    {
        SetContentOffset(Vector2.Zero, false, false);
    }

    /// <summary>
    /// Easy-to-use helper around using a lower level ScrollTo function
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animated"></param>
    public void SetContentOffset(Vector2 offset, bool animated, bool clamp)
    {
        var speed = animated ? AutoScrollingSpeedMs : 0;

        ScrollTo(offset.X, offset.Y, speed, clamp);
    }

    public virtual void ScrollTo(float x, float y, float maxSpeedSecs, bool clamp)
    {
        StopScrolling();

        var clamped = ClampOffsetHard(x, y);

        OrderedScrollTo = ScrollToPointOrder.ToCoords(clamped.X, clamped.Y, maxSpeedSecs);

        if (!ExecuteScrollToOrder())
        {
            this.UpdateVisibleIndex();
        }
    }

    public void ScrollToTop(float maxTimeSecs)
    {
        if (Orientation == ScrollOrientation.Vertical)
        {
            ScrollTo(InternalViewportOffset.Units.X, 0, maxTimeSecs, false);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            ScrollTo(0, InternalViewportOffset.Units.Y, maxTimeSecs, false);
        }
        else
        {
            ScrollTo(0, 0, maxTimeSecs, false);
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
            ScrollTo(InternalViewportOffset.Units.X, -ContentSize.Pixels.Height, maxTimeSecs, true);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            ScrollTo(_scrollMinX, InternalViewportOffset.Units.Y, maxTimeSecs, true);
        }
        else
        {
            ScrollTo(_scrollMinX, _scrollMinY, maxTimeSecs, true);
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

        Debug.WriteLine(
            $"[ScrollToEstimatedBottom] Current content size: {ContentSize.Pixels.Width}x{ContentSize.Pixels.Height}, estimated: {estimatedSize.Pixels.Width}x{estimatedSize.Pixels.Height}");

        if (Orientation == ScrollOrientation.Vertical)
        {
            // Calculate estimated bottom position
            var estimatedContentHeight = estimatedSize.Pixels.Height;
            var viewportHeight = Viewport.Pixels.Height;
            var estimatedScrollY = -(estimatedContentHeight - viewportHeight);

            // Clamp to reasonable bounds
            var minScrollY = Math.Min(0, estimatedScrollY);

            Debug.WriteLine(
                $"[ScrollToEstimatedBottom] Scrolling to estimated Y: {minScrollY} (content: {estimatedContentHeight}, viewport: {viewportHeight})");

            ScrollTo(InternalViewportOffset.Units.X, minScrollY, maxTimeSecs, true);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            // Calculate estimated right position
            var estimatedContentWidth = estimatedSize.Pixels.Width;
            var viewportWidth = Viewport.Pixels.Width;
            var estimatedScrollX = -(estimatedContentWidth - viewportWidth);

            // Clamp to reasonable bounds
            var minScrollX = Math.Min(0, estimatedScrollX);

            Debug.WriteLine(
                $"[ScrollToEstimatedBottom] Scrolling to estimated X: {minScrollX} (content: {estimatedContentWidth}, viewport: {viewportWidth})");

            ScrollTo(minScrollX, InternalViewportOffset.Units.Y, maxTimeSecs, true);
        }
    }

    private bool _Snapped;

    public bool Snapped
    {
        get { return _Snapped; }
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
        get { return _IsSnapping; }
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
