using System.Collections.Concurrent;
using System.Numerics;
using SkiaControl = DrawnUi.Draw.SkiaControl;

namespace DrawnUi.Controls;

/// <summary>
/// A specialized scroll control designed for creating swipeable carousels with automatic snapping to items.
/// Supports data binding through ItemsSource and ItemTemplate, peek effects with SidesOffset, and smooth transitions.
/// Ideal for image galleries, tab interfaces, and any swipeable content display.
/// </summary>
public class SkiaCarousel : SnappingLayout
{
    public SkiaCarousel()
    {
        //some defaults
        Spacing = 0;

        //init
        ChildrenFactory.UpdateViews();
    }

    public override bool WillClipBounds => true;

    protected virtual void RenderVisibleChild(
        DrawingContext context,
        SkiaControl view, Vector2 position)
    {
        view.OptionalOnBeforeDrawing(); //draw even hidden neighboors to be able to preload stuff
        if (view.CanDraw)
        {
            view.LockUpdate(true);
            AnimateVisibleChild(view, position);
            view.LockUpdate(false);
            view.Render(context);
        }
    }

    protected virtual void AnimateVisibleChild(
        SkiaControl view, Vector2 position)
    {
        view.TranslationX = position.X;
        view.TranslationY = position.Y;
    }

    bool ApplyLoopedLogic
    {
        get
        {
            return (IsLooped && Animated && CanAnimate && SnapPoints.Count > 1);
        }
    }

    public override void ScrollToNearestAnchor(Vector2 location, Vector2 velocity)
    {
        var theshold = 100f;
        if (Math.Abs(velocity.X) < theshold)
        {
            velocity.X = 0;
        }

        if (Math.Abs(velocity.Y) < theshold)
        {
            velocity.Y = 0;
        }

        if (ApplyLoopedLogic)
        {
            // Use the nearest real snap as origin to avoid multi-step jumps
            var originSnap = FindNearestAnchorInternal(location, velocity);

            Vector2 projectionAnchor = SelectNextAnchor(originSnap, velocity);
            if (Vector2.Distance(location, projectionAnchor) >= 0.5) //todo move threshold to options
            {
                ScrollToOffset(projectionAnchor, velocity, CanAnimate);
            }
            else
            {
                UpdateReportedPosition();
            }

            return;
        }

        base.ScrollToNearestAnchor(location, velocity);
    }

    public override Vector2 SelectNextAnchor(Vector2 origin, Vector2 velocity)
    {
        // Non-looped: use base behavior exactly as-is
        if (!ApplyLoopedLogic)
            return base.SelectNextAnchor(origin, velocity);

        // First, ask base to choose. It works well in all non-border cases.
        var baseTarget = base.SelectNextAnchor(origin, velocity);

        // Map origin to a real snap index (handles virtuals via our FindNearestAnchorInternal override)
        var originSnap = FindNearestAnchorInternal(origin, velocity);
        int originIndex = SnapPoints.IndexOf(originSnap);
        if (originIndex < 0)
        {
            originSnap = FindNearestAnchor(origin);
            originIndex = SnapPoints.IndexOf(originSnap);
        }

        // If base already picked a different anchor, keep it
        if (originSnap != baseTarget)
            return baseTarget;

        // Decide outward direction only; minimal logic to handle edges robustly.
        int dirSign = 0;
        if (!IsVertical)
            dirSign = Math.Sign(velocity.X);
        else
            dirSign = Math.Sign(velocity.Y);

        if (dirSign == 0)
        {
            // Fallback when velocity is tiny at finger-up: infer from pan displacement
            var delta = CurrentPosition - _panningStartOffset;
            float axisDelta = !IsVertical ? delta.X : delta.Y;

            // Require a minimal displacement before considering wrap, otherwise keep base
            float axisStep = 0f;
            if (SnapPoints.Count > 1)
            {
                var step = SnapPoints[1] - SnapPoints[0];
                axisStep = Math.Abs(!IsVertical ? step.X : step.Y);
            }
            double minDisplacement = axisStep > 0 ? axisStep * SnapDistanceRatio : 0.0;

            if (axisStep <= 0 || Math.Abs(axisDelta) < minDisplacement)
            {
                dirSign = 0; // too small to decide, stick with base
            }
            else
            {
                dirSign = Math.Sign(axisDelta);
            }
        }

        if (dirSign == 0)
            return baseTarget; // no direction (or below threshold) -> keep base decision

        // If velocity is zero, require stronger intent (>= 50% of step) before wrapping
        if (velocity == Vector2.Zero)
        {
            float stepAxis = 0f;
            if (SnapPoints.Count > 1)
            {
                var step = SnapPoints[1] - SnapPoints[0];
                stepAxis = Math.Abs(!IsVertical ? step.X : step.Y);
            }
            if (stepAxis > 0)
            {
                var disp = !IsVertical ? (CurrentPosition.X - _panningStartOffset.X) : (CurrentPosition.Y - _panningStartOffset.Y);
                if (Math.Abs(disp) < stepAxis * 0.5)
                    return baseTarget; // under half-step → don't wrap
            }
        }

        // Border wrap-only: last -> pseudoEnd when swiping forward, first -> pseudoStart when swiping backward
        if (dirSign < 0 && originIndex == MaxIndex)
        {
            var pseudoEnd = GetVirtualSnapPoints().First(sp => sp.Id == -2).Location;
            return pseudoEnd;
        }
        if (dirSign > 0 && originIndex == 0)
        {
            var pseudoStart = GetVirtualSnapPoints().First(sp => sp.Id == -1).Location;
            return pseudoStart;
        }

        return baseTarget;
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        SelectedIndexChanged = null;
        ItemAppearing = null;
        ItemDisappearing = null;
        Stopped = null;
    }

    #region EVENTS

    public event EventHandler<int> SelectedIndexChanged;

    public event EventHandler<int> ItemAppearing;

    public event EventHandler<int> ItemDisappearing;


    public event EventHandler<Vector2> Stopped;

    protected ConcurrentDictionary<int, bool> ItemsVisibility { get; } = new();

    void SendVisibility(int index, bool state)
    {
        var last = ItemsVisibility[index];
        if (last != state)
        {
            ItemsVisibility[index] = state;
            if (state)
            {
                //Debug.WriteLine($"ItemAppearing: {index}");
                ItemAppearing?.Invoke(this, index);
            }
            else
            {
                ItemDisappearing?.Invoke(this, index);
                //Debug.WriteLine($"ItemDisappearing: {index}");
            }
        }
    }


    void InitializeItemsVisibility(int count, bool force)
    {
        if (force || ItemsVisibility.Count != count)
        {
            for (int i = 0; i < count; i++)
            {
                ItemsVisibility[i] = false;
            }
        }
    }

    #endregion

    #region METHODS

    /// <summary>
    /// Will translate child and raise appearing/disappearing events
    /// </summary>
    /// <param name="currentPosition"></param>
    public override void ApplyPosition(Vector2 currentPosition)
    {
        base.ApplyPosition(currentPosition);

        Update();
    }

    public virtual void FixIndex()
    {
        if (IsLooped && SnapPoints.Count > 1)
        {
            if (SelectedIndex >= 0 && SelectedIndex < SnapPoints.Count)
            {
                var snap = GetVirtualAnchor(CurrentPosition, Vector2.Zero);
                var offset = CurrentPosition - snap.Location;

                var snapPoint = SnapPoints[SelectedIndex];

                CurrentSnap = snapPoint;
                CurrentPosition = snapPoint + offset;
            }
        }

    }

    public virtual void ApplyIndex(bool instant = false)
    {
        if (SelectedIndex >= 0 && SelectedIndex < SnapPoints.Count)
        {
            var snapPoint = SnapPoints[SelectedIndex];

            if (instant)
            {
                CurrentPosition = snapPoint;
            }
            else if (ApplyLoopedLogic)
            {
                if (SelectedIndex == 0 && LastIndex == MaxIndex)
                {
                    var snap = GetVirtualSnapPoints().First(x => x.Id == -2);
                    snapPoint = snap.Location;
                }
                else
                if (SelectedIndex == MaxIndex && LastIndex == 0)
                {
                    var snap = GetVirtualSnapPoints().First(x => x.Id == -1);
                    snapPoint = snap.Location;
                }
            }

            ScrollToOffset(snapPoint, Vector2.Zero, !instant && CanAnimate && Animated);
        }
    }

    public override void UpdateReportedPosition()
    {
        if (SnapPoints == null || SnapPoints.Count == 0)
            return;

        if (IsLooped && SnapPoints.Count > 1)
        {
            var current = GetVirtualAnchor(CurrentSnap, Vector2.Zero);
            if (current.Id == -1)
            {
                SelectedIndex = MaxIndex;
            }
            else
            if (current.Id == -2)
            {
                SelectedIndex = 0;
            }
            else
            {
                SelectedIndex = current.Id;
            }
        }
        else
        {
            var snapPoint = SnapPoints.FirstOrDefault(x => AreVectorsEqual(x, CurrentSnap, 1));
            var index = SnapPoints.IndexOf(snapPoint);
            if (index >= 0 && index < ChildrenFactory.GetChildrenCount())
            {
                SelectedIndex = index;
            }
        }
    }

    #endregion

    #region TEMPLATE

    /// <summary>
    /// This might be called from background thread if we set InitializeTemplatesInBackgroundDelay true
    /// </summary>
    public override void OnTemplatesAvailable()
    {
        ApplyOptions(true);

        base.OnTemplatesAvailable();
    }

    protected virtual bool CanRender
    {
        get
        {
            if (!IsTemplated)
                return SnapPoints.Count > 0;

            return ChildrenFactory.TemplatesAvailable && SnapPoints.Count > 0;
        }
    }

    protected virtual bool ChildrenReady
    {
        get
        {
            if (!IsTemplated)
                return Views.Count > 0;

            return ChildrenFactory.TemplatesAvailable;
        }
    }

    protected virtual void OnScrollProgressChanged()
    {
    }

    protected override int RenderViewsList(DrawingContext context, IEnumerable<SkiaControl> skiaControls)
    {
        var drawn = 0;

        if (CanRender)
        {
            double progressMax = 0;
            double progress = 0;
            if (this.IsVertical)
            {
                progressMax = SnapPoints.Last().Y;
                progress = CurrentPosition.Y;
            }
            else
            {
                progressMax = SnapPoints.Last().X;
                progress = CurrentPosition.X;
            }

            ScrollProgress = progress / progressMax;

            if (ScrollProgress != LastScrollProgress)
            {
                LastScrollProgress = ScrollProgress;

                TransitionDirection = ScrollProgress > LastScrollProgress
                    ? LinearDirectionType.Forward
                    : LinearDirectionType.Backward;
                OnScrollProgressChanged();
            }

            var childrenCount = ChildrenFactory.GetChildrenCount();

            //PASS 1
            List<ControlInStack> visibleElements = new();
            List<SkiaControlWithRect> tree = new();

            for (int index = 0; index < childrenCount; index++)
            {
                bool wasUsed = false;
                var position = CalculateChildPosition(CurrentPosition, index, childrenCount);
                if (position.OnScreen || position.NextToScreen)
                {
                    var cell = new ControlInStack()
                    {
                        ControlIndex = index, IsVisible = position.OnScreen, Offset = position.Offset,
                    };
                    visibleElements.Add(cell);
                    wasUsed = true;
                }

                if (!wasUsed)
                    ChildrenFactory.MarkViewAsHidden(index); //recycle template

                SendVisibility(index, position.OnScreen);
            }

            //PASS 2 - draw only visible and thoses at sides that would be selected
            var cellsToRelease = new List<SkiaControl>();

            try
            {
                var track = DrawingRect.Width - SidesOffset;
                foreach (var cell in visibleElements)
                {
                    var view = ChildrenFactory.GetViewForIndex(cell.ControlIndex);
                    if (IsTemplated)
                        cellsToRelease.Add(view);

                    //Debug.Write($"[Carousel] {Tag} obtained cell for index {cell.ControlIndex}, visible {cell.IsVisible}");

                    if (view == null)
                    {
                        break; //looks like itemssource changed?..
                    }

                    if (cell.ControlIndex == SelectedIndex)
                    {
                        //todo calculate ScrollAmount from 0 to 1
                        var pixels = cell.Offset.X * RenderingScale;
                        ScrollAmount = pixels / track;
                    }

                    if (cell.IsVisible || this.PreloadNeighboors)
                    {
                        RenderVisibleChild(context, view, cell.Offset);
                    }

                    if (cell.IsVisible) //but handle gestures only for visible views
                    {
                        drawn++;

                        //used by gestures etc..
                        cell.Drawn.Set(view.DrawingRect.Left, view.DrawingRect.Top, view.DrawingRect.Right,
                            view.DrawingRect.Bottom);

                        var destinationRect = new SKRect(cell.Drawn.Left, cell.Drawn.Top, cell.Drawn.Right,
                            cell.Drawn.Bottom);
                        tree.Add(new SkiaControlWithRect(view,
                            destinationRect,
                            view.DrawingRect,
                            cell.ControlIndex,
                            -1, // Default freeze index
                            view.BindingContext)); // Capture current binding context
                    }
                }
            }
            finally
            {
                if (IsTemplated)
                    foreach (var cell in cellsToRelease)
                    {
                        ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                    }
            }


            //Trace.WriteLine($"[CAROUSEL] {Tag}: {ChildrenFactory.GetDebugInfo()}");

            SetRenderingTree(tree);

            return drawn;
        }

        return drawn;
    }

    private double _ScrollAmount;

    /// <summary>
    /// Scroll amount from 0 to 1 of the current (SelectedIndex) slide. Another similar but different property would be ScrollProgress. This is not linear as SelectedIndex changes earlier than 0 or 1 are attained.
    /// </summary>
    public double ScrollAmount
    {
        get { return _ScrollAmount; }
        set
        {
            if (_ScrollAmount != value)
            {
                _ScrollAmount = value;
                OnPropertyChanged();
                //Debug.WriteLine($"ScrollAmount {value:0.000}");
            }
        }
    }

    private double _ScrollProgress;

    /// <summary>
    /// Scroll progress from 0 to (numberOfSlides-1).
    /// This is not dependent of the SelectedIndex, just reflects visible progress. Useful to create custom controls attached to carousel.
    /// Calculated as (for horizontal): CurrentPosition.X / SnapPoints.Last().X
    /// </summary>
    public double ScrollProgress
    {
        get { return _ScrollProgress; }
        set
        {
            if (_ScrollProgress != value)
            {
                _ScrollProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TransitionProgress));
                //Debug.WriteLine($"ScrollAmount {value / 3.0:0.000}");
            }
        }
    }

    protected double LastScrollProgress { get; set; } = double.NegativeInfinity;

    public double TransitionProgress
    {
        get
        {
            if (MaxIndex < 1)
                return 0.0;

            int numberOfTransitions = MaxIndex;
            double scaledProgress = ScrollProgress * numberOfTransitions;
            double value = scaledProgress - Math.Floor(scaledProgress);

            //if (value == 0 && TransitionDirection == LinearDirectionType.Forward) value = 1.0;

            //if (TransitionDirection == LinearDirectionType.Backward)
            //    value = 1.0 - value;

            //Debug.WriteLine($"TransitionAmount {value:0.00} scroll {ScrollProgress:0.00}");

            return value;
        }
    }


    protected void AdaptTemplate(SkiaControl skiaControl)
    {
        var margin = SidesOffset;

        if (this.HeightRequest < 0 && VerticalOptions.Alignment == LayoutAlignment.Start)
        {
            skiaControl.VerticalOptions = LayoutOptions.Start;
        }
        else
        {
            skiaControl.VerticalOptions = LayoutOptions.Fill;
        }

        if (this.WidthRequest < 0 && HorizontalOptions.Alignment == LayoutAlignment.Start)
        {
            skiaControl.HorizontalOptions = LayoutOptions.Start;
        }
        else
        {
            skiaControl.HorizontalOptions = LayoutOptions.Fill;
        }

        if (IsVertical)
        {
            skiaControl.Margin = new Thickness(0, margin, 0, margin);
        }
        else
        {
            skiaControl.Margin = new Thickness(margin, 0, margin, 0);
        }
    }

    public override object CreateContentFromTemplate()
    {
        try
        {
            var control = base.CreateContentFromTemplate() as SkiaControl;
            AdaptTemplate(control);
            return control;
        }
        catch (Exception e)
        {
            Super.Log(e);
            return null;
        }
    }

    SemaphoreSlim semaphoreItemSouce = new(1);

    //protected async Task ProcessItemsSource()
    //{
    //    await semaphoreItemSouce.WaitAsync();
    //    try
    //    {
    //        AdaptItemsSource();
    //    }
    //    catch (Exception e)
    //    {
    //        Trace.WriteLine(e);
    //    }
    //    finally
    //    {
    //        semaphoreItemSouce.Release();
    //    }

    //}

    //protected virtual void AdaptItemsSource()
    //{

    //}

    private bool _itemsSourceChangedNeedResetIndex;
    private bool _loaded;


    public override void OnItemSourceChanged()
    {
        ChildrenInitialized = false;
        LastIndex = -1;

        _itemsSourceChangedNeedResetIndex = true;

        _loaded = true;

        base.OnItemSourceChanged();

        AdaptChildren();

        if (ChildrenFactory.TemplatesAvailable)
        {
            ApplyOptions(true);
        }
    }

    #endregion

    #region ENGINE

    protected virtual (Vector2 Offset, bool OnScreen, bool NextToScreen) CalculateChildPosition(Vector2 currentPosition,
        int index, int childrenCount)
    {
        var childPos = SnapPoints[index];

        float newX = 0;
        float newY = 0;
        bool isVisible = true;
        bool nextToScreen = false;

        float nextToScreenOffset = 10; // you can adjust this value

        if (IsVertical)
        {
            newY = (currentPosition.Y + Math.Abs(childPos.Y));
            isVisible = newY + SidesOffset * 2 <= Height && newY + (Height - SidesOffset * 2) + SidesOffset * 2 >= 0;
            nextToScreen = Math.Abs(newY) - Width - SidesOffset - Spacing <= nextToScreenOffset;
        }
        else
        {
            newX = (currentPosition.X + Math.Abs(childPos.X));
            isVisible = newX + SidesOffset * 2 <= Width && newX + (Width - SidesOffset * 2) + SidesOffset * 2 >= 0;
            nextToScreen = Math.Abs(newX) - Width - SidesOffset - Spacing <= nextToScreenOffset;
        }

        // Looped rendering: draw edge neighbors beyond bounds when needed
        if (IsLooped && childrenCount > 1)
        {
            if (!IsVertical)
            {
                float step = SnapPoints.Count >= 2
                    ? Math.Abs(SnapPoints[1].X - SnapPoints[0].X)
                    : (float)(Width + Spacing - SidesOffset * 2);

                if (index == 0)
                {
                    var alt = newX + step * childrenCount; // place first item after last
                    bool altVisible = alt + SidesOffset * 2 <= Width &&
                                      alt + (Width - SidesOffset * 2) + SidesOffset * 2 >= 0;
                    bool altNextTo = Math.Abs(alt) - Width - SidesOffset - Spacing <= nextToScreenOffset;
                    if ((altVisible || altNextTo) && !isVisible)
                    {
                        newX = alt;
                        isVisible = altVisible;
                        nextToScreen = altNextTo;
                    }
                }
                else if (index == childrenCount - 1)
                {
                    var alt = newX - step * childrenCount; // place last item before first
                    bool altVisible = alt + SidesOffset * 2 <= Width &&
                                      alt + (Width - SidesOffset * 2) + SidesOffset * 2 >= 0;
                    bool altNextTo = Math.Abs(alt) - Width - SidesOffset - Spacing <= nextToScreenOffset;
                    if ((altVisible || altNextTo) && !isVisible)
                    {
                        newX = alt;
                        isVisible = altVisible;
                        nextToScreen = altNextTo;
                    }
                }
            }
            else
            {
                float step = SnapPoints.Count >= 2
                    ? Math.Abs(SnapPoints[1].Y - SnapPoints[0].Y)
                    : (float)(Height + Spacing - SidesOffset * 2);

                if (index == 0)
                {
                    var alt = newY + step * childrenCount; // place first item after last
                    bool altVisible = alt + SidesOffset * 2 <= Height &&
                                      alt + (Height - SidesOffset * 2) + SidesOffset * 2 >= 0;
                    bool altNextTo = Math.Abs(alt) - Height - SidesOffset - Spacing <= nextToScreenOffset;
                    if ((altVisible || altNextTo) && !isVisible)
                    {
                        newY = alt;
                        isVisible = altVisible;
                        nextToScreen = altNextTo;
                    }
                }
                else if (index == childrenCount - 1)
                {
                    var alt = newY - step * childrenCount; // place last item before first
                    bool altVisible = alt + SidesOffset * 2 <= Height &&
                                      alt + (Height - SidesOffset * 2) + SidesOffset * 2 >= 0;
                    bool altNextTo = Math.Abs(alt) - Height - SidesOffset - Spacing <= nextToScreenOffset;
                    if ((altVisible || altNextTo) && !isVisible)
                    {
                        newY = alt;
                        isVisible = altVisible;
                        nextToScreen = altNextTo;
                    }
                }
            }
        }

        return (new Vector2(newX, newY), isVisible, nextToScreen);
    }

    /// <summary>
    /// Set true position if inside virtual for IsLooped
    /// </summary>
    protected virtual void FixPosition()
    {
        if (IsLooped && SnapPoints.Count>1)
        {
            //todo set real snap if inside virtualf
            var virtualSnap = GetVirtualAnchor(CurrentSnap, Vector2.Zero);
            //Debug.WriteLine($"[STOPPED] At {virtualSnap.Id}");
            if (virtualSnap.Id == -1 || virtualSnap.Id == -2)
            {
                FixIndex();
            }
        }
    }

    public override void OnTransitionChanged()
    {
        if (!InTransition)
        {
            FixPosition();

            Stopped?.Invoke(this, CurrentPosition);

            //Debug.WriteLine($"[STOPPED] CurrentPosition {CurrentPosition}");
        }

        base.OnTransitionChanged();
    }

    protected override bool ScrollToOffset(Vector2 targetOffset, Vector2 velocity, bool animate)
    {
        if (ScrollLocked || targetOffset == CurrentSnap)
        {
            //Debug.WriteLine("[CAROUSEL] ScrollToOffset blocked");
            return false;
        }

        if (animate && Height > 0)
        {
            //_animatorSpring?.Stop();

            var start = CurrentSnap;
            var end = new Vector2((float)targetOffset.X, (float)targetOffset.Y);

            //var atSnapPoint = SnapPoints.Any(x => x.X == CurrentSnap.X && x.Y == CurrentSnap.Y);

            var displacement = start - end;

            if (velocity == Vector2.Zero)
                velocity = GetAutoVelocity(displacement);

            var speedK = (float)SwipeSpeed / 2f;

            velocity *= speedK;

            if (displacement != Vector2.Zero)
            {
                if (Bounces)
                {
                    var spring = new Spring((float)(1 * (1 + RubberDamping)), (float)(200*SwipeSpeed), (float)(0.5f * (1 + RubberDamping)));
                    VectorAnimatorSpring.Initialize(end, displacement, velocity * (float)SwipeSpeed, spring, 0.5f);
                    VectorAnimatorSpring.Start();
                    _isSnapping = end;
                }
                else
                {
                    var maxSpeed = 0.25 / speedK; //secs

                    var direction = GetDirectionType(start, end, 0.8f);
                    var seconds = displacement / velocity;
                    var speed = maxSpeed;
                    if (direction == DirectionType.Vertical)
                    {
                        speed = Math.Abs(seconds.Y);
                        speed *= (Math.Abs(end.Y - start.Y) / Height);
                    }
                    else if (direction == DirectionType.Horizontal)
                    {
                        speed = Math.Abs(seconds.X);
                        speed *= (Math.Abs(end.X - start.X) / Height);
                    }

                    if (speed > maxSpeed)
                        speed = maxSpeed;

                    if (LinearSpeedMs > 0)
                    {
                        var ratio = Math.Abs(end.X - start.X) / CellSize.Pixels.Width;

                        speed = ratio * LinearSpeedMs / 1000.0;
                    }

                    //Debug.WriteLine($"Will snap:{start} -> {end}");

                    _isSnapping = end;
                    AnimatorRange.Initialize(start, end, (float)speed, Easing.SinOut);
                    AnimatorRange.Start();
                }
            }
            else
            {
                //Debug.WriteLine($"[CAROUSEL] displacement zero!");
                return false;
            }

            //if (displacement != Vector2.Zero)
            //{
            //    if (atSnapPoint && !ThresholdOk(displacement))
            //    {
            //        //Debug.WriteLine("[CAROUSEL] threshold low");
            //        return false;
            //    }

            //    var spring = new Spring((float)(1 * (1 + RubberDamping)), 200, (float)(0.5f * (1 + RubberDamping)));
            //    _animatorSpring.Initialize(end, displacement, velocity, spring);

            //    _animatorSpring.Start();
            //}
        }
        else if (CanDraw)
        {
            CurrentSnap = targetOffset;
            ApplyPosition(targetOffset);
        }

        CurrentSnap = targetOffset;
        UpdateReportedPosition();
        return true;
    }


    private Vector2? _isSnapping;

    private void AnimateLoopStep(bool forward)
    {
        if (!CanAnimate)
            return;

        var start = CurrentSnap;

        // step distance between consecutive snap points (units)
        float step;
        if (SnapPoints.Count >= 2)
        {
            step = IsVertical
                ? Math.Abs(SnapPoints[1].Y - SnapPoints[0].Y)
                : Math.Abs(SnapPoints[1].X - SnapPoints[0].X);
        }
        else
        {
            step = (float)((IsVertical ? Height : Width) + Spacing - SidesOffset * 2);
        }

        Vector2 end;
        if (IsVertical)
        {
            // forward (next) moves negative along axis (towards larger index)
            end = start + new Vector2(0, forward ? -step : step);
        }
        else
        {
            end = start + new Vector2(forward ? -step : step, 0);
        }

        // derive speed similar to ScrollToOffset
        double maxSpeed = 0.25; // secs
        double speed = maxSpeed;
        var delta = IsVertical ? Math.Abs(end.Y - start.Y) : Math.Abs(end.X - start.X);
        if (LinearSpeedMs > 0)
        {
            var denom = IsVertical ? CellSize.Pixels.Height : CellSize.Pixels.Width;
            if (denom > 0)
            {
                var ratio = delta / denom;
                speed = ratio * LinearSpeedMs / 1000.0;
            }
        }

        if (speed > maxSpeed)
            speed = maxSpeed;

        var prevFinished = AnimatorRange.Finished;
        AnimatorRange.Finished = () =>
        {
            try
            {
                var teleportIndex = forward ? 0 : MaxIndex;
                if (teleportIndex >= 0 && teleportIndex < SnapPoints.Count)
                {
                    var target = SnapPoints[teleportIndex];
                    CurrentSnap = target;
                    ApplyPosition(target);
                    UpdateReportedPosition();
                }
            }
            finally
            {
                // restore previous handler and ensure default cleanup runs (_isSnapping=null)
                AnimatorRange.Finished = prevFinished;
                prevFinished?.Invoke();
            }
        };

        _isSnapping = end;
        AnimatorRange.Initialize(start, end, (float)speed, Easing.Linear);
        AnimatorRange.Start();
    }


    bool ThresholdOk(Vector2 displacement)
    {
        var threshold = 10;

        if (IsVertical)
            return threshold < Math.Abs(displacement.Y);

        return threshold < Math.Abs(displacement.X);
    }

    public override void ApplyOptions(bool initialize)
    {
        if (Parent == null)
            return;

        SetupViewport();

        //Viewport = Parent.DrawingRect;

        if (initialize)
        {
            InitializeChildren();
        }

        base.ApplyOptions(initialize);
    }

    public override bool CheckTransitionEnded()
    {
        if (_isSnapping != null)
        {
            return false;
        }

        return base.CheckTransitionEnded();
    }

    void Init()
    {
        //CheckConstraints();

        if (Parent != null)
        {
            if (VectorAnimatorSpring == null)
            {
                VectorAnimatorSpring = new(this)
                {
                    OnStart = () => { },
                    OnStop = () =>
                    {
                        
                    },
                    OnVectorUpdated = (value) =>
                    {
                        ApplyPosition(value);
                    },
                    Finished = () =>
                    {
                        _isSnapping = null;
                    },
                };
                AnimatorRange = new(this)
                {
                    OnVectorUpdated = (value) =>
                    {
                        ApplyPosition(value);
                    },
                    OnStop = () =>
                    {
                        if (!Bounces)
                        {
             
                        }
                    },
                    Finished = () =>
                    {
                        _isSnapping = null;
                    },
                };
            }

            ApplyOptions(true);
        }
    }

    /// <summary>
    /// There are the bounds the scroll offset can go to.. This are NOT the bounds of the whole content.
    /// </summary>
    public override SKRect GetContentOffsetBounds()
    {
        if (SnapPoints.Count > 0)
        {
            var last = SnapPoints.Count - 1;

            if (!IsLooped || SnapPoints.Count < 2)
            {
                //DEFAULT
                if (IsVertical)
                {
                    return new SKRect(
                        SnapPoints[0].X,
                        SnapPoints[last].Y,
                        SnapPoints[last].X,
                        SnapPoints[0].Y
                    );
                }
                else
                {
                    return new SKRect(
                        SnapPoints[last].X,
                        SnapPoints[0].Y,
                        SnapPoints[0].X,
                        SnapPoints[last].Y
                    );
                }
            }
            else
            {
                //LOOPED
                if (IsVertical)
                {
                    return new SKRect(
                        SnapPoints[0].X,
                        float.MinValue,
                        SnapPoints[last].X,
                        float.MaxValue
                    );
                }
                else
                {
                    return new SKRect(
                        float.MinValue,
                        SnapPoints[0].Y,
                        float.MaxValue,
                        SnapPoints[last].Y
                    );
                }
            }
        }

        return SKRect.Empty;
    }


    protected virtual void CheckConstraints()
    {
        if (this.HeightRequest < 0 && VerticalOptions.Alignment == LayoutAlignment.Start
            || this.WidthRequest < 0 && HorizontalOptions.Alignment == LayoutAlignment.Start)
        {
            throw new Exception("Carousel must have a fixed size or use Fill");
        }
    }


    public override void OnChildAdded(SkiaControl child)
    {
        base.OnChildAdded(child);

        AdaptChildren();
    }


    bool viewportSet;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        SetupViewport();
    }

    protected void SetupViewport()
    {
        if (Parent != null)
        {
            Viewport = DrawingRect; // Parent.DrawingRect;

            if (!viewportSet) // !CompareRects(Viewport, _lastViewport, 0.5f))
            {
                viewportSet = true;
                _lastViewport = Viewport;
                Init();
            }

            if (DynamicSize && SelectedIndex >= 0)
            {
                if (!ChildrenInitialized)
                {
                    InitializeChildren();
                }

                ApplyDynamicSize(SelectedIndex);
            }
            else
            {
                InitializeChildren();
            }
        }
    }


    protected bool ChildrenInitialized;

    private int _MaxIndex;

    public int MaxIndex
    {
        get { return _MaxIndex; }
        set
        {
            if (_MaxIndex != value)
            {
                _MaxIndex = value;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAtStart));
                    OnPropertyChanged(nameof(IsAtEnd));
                });
            }
        }
    }


    /// <summary>
    /// We expect this to be called after this alyout is invalidated
    /// </summary>
    public virtual void InitializeChildren()
    {
        if (Viewport == SKRect.Empty || !ChildrenReady)
        {
            Debug.WriteLine(
                $"[SkiaCarousel] {Tag} InitializeChildren SKIPPED! Viewpoert empty = {Viewport == SKRect.Empty}, ChildrenReady={ChildrenReady}");
            return;
        }

        ChildrenInitialized = true;

        var childrenCount = ChildrenFactory.GetChildrenCount();

        MaxIndex = childrenCount - 1;

        ChildrenTotal = childrenCount;

        Debug.WriteLine(
            $"[SkiaCarousel] {Tag} InitializeChildren called: ChildrenCount={childrenCount}, MaxIndex={MaxIndex}, IsLooped={IsLooped}");

        InitializeItemsVisibility(childrenCount, true);

        var snapPoints = new List<Vector2>();
        float currentPosition = 0;

        var cellSize = new SKSize((float)Width, (float)Height);
        var cellsToRelease = new List<SkiaControl>();
        try
        {
            for (int index = 0; index < childrenCount; index++)
            {
                if (!IsTemplated || RecyclingTemplate == RecyclingTemplate.Disabled)
                {
                    var view = ChildrenFactory.GetViewForIndex(index);
                    if (IsTemplated) cellsToRelease.Add(view);
                    view.InvalidateWithChildren();
                }

                var offset = (float)(index * (-SidesOffset * 2 + Spacing));

                var position = IsVertical
                    ? new SKPoint(0, currentPosition + offset)
                    : new SKPoint(currentPosition + offset, 0);

                snapPoints.Add(new Vector2(-position.X, -position.Y));

                currentPosition += (IsVertical ? cellSize.Height : cellSize.Width);
            }
        }
        finally
        {
            if (IsTemplated)
                foreach (var cell in cellsToRelease)
                {
                    ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                }
        }


        CellSize = ScaledSize.FromUnits(cellSize.Width, cellSize.Height, RenderingScale);

        SnapPoints = snapPoints;
        SnapPointsVirtual = null;

        ContentOffsetBounds = GetContentOffsetBounds();

        CurrentSnap = new(-1, -1);

        if (SnapPoints.Any() && (_itemsSourceChangedNeedResetIndex || SelectedIndex < 0 ||
                                 SelectedIndex > snapPoints.Count - 1))
        {
            SelectedIndex = 0;
        }
        else
        {
            ApplyIndex(true);
        }

        _itemsSourceChangedNeedResetIndex = false;
        OnChildrenInitialized();
    }

    protected virtual void OnChildrenInitialized()
    {
        LastScrollProgress = double.NegativeInfinity;
        OnScrollProgressChanged();
    }

    public ScaledSize CellSize { get; set; }

    /// <summary>
    /// Set children layout options according to our settings. Not used for template case.
    /// </summary>
    protected virtual void AdaptChildren()
    {
        if (!IsTemplated)
        {
            var index = 0;

            ChildrenFactory.UpdateViews();

            using var cells = ChildrenFactory.GetViewsIterator();

            foreach (var skiaControl in cells)
            {
                ItemsVisibility[index] = false;

                AdaptTemplate(skiaControl);

                index++;
            }

            ChildrenCount = ChildrenFactory.GetChildrenCount();
        }
    }

    private int _ChildrenCount;

    public int ChildrenCount
    {
        get { return _ChildrenCount; }
        set
        {
            if (_ChildrenCount != value)
            {
                _ChildrenCount = value;
                OnPropertyChanged();
            }
        }
    }


    protected override int GetTemplatesPoolLimit()
    {
        var poolSize = 3;
        if (this.RecyclingTemplate == RecyclingTemplate.Disabled)
            poolSize = ItemsSource.Count;

        return poolSize;
    }

    protected override int GetTemplatesPoolPrefill()
    {
        return GetTemplatesPoolLimit();
    }


    //would need if layout was column or row inside scroll
    void ResizeChildrenViewport()
    {
        var childrenCount = ChildrenFactory.GetChildrenCount();
        SKRect rect = DrawingRect;
        if (IsVertical)
        {
            var totalheight = this.Height * childrenCount + Spacing * childrenCount - 1;
            rect = new SKRect(DrawingRect.Left, DrawingRect.Top, DrawingRect.Right,
                (float)(DrawingRect.Top + totalheight * RenderingScale));
        }
        else
        {
            var totalWidth = this.Width * childrenCount + Spacing * childrenCount - 1;
            rect = new SKRect(DrawingRect.Left, DrawingRect.Top,
                (float)(DrawingRect.Left + totalWidth * RenderingScale), DrawingRect.Bottom);
        }

        Viewport = rect;
    }

    public class SnapPoint
    {
        public int Id { get; set; }
        public Vector2 Location { get; set; }
    }


    protected virtual List<SnapPoint> GetVirtualSnapPoints()
    {
        if (SnapPointsVirtual == null)
        {
            var distance = SnapPoints[1] - SnapPoints[0];
            var pseudoStart = new Vector2(SnapPoints[0].X - distance.X, SnapPoints[0].Y - distance.Y);
            var pseudoEnd = new Vector2(SnapPoints[SnapPoints.Count - 1].X + distance.X,
                SnapPoints[SnapPoints.Count - 1].Y + distance.Y);

            var virtualList = new List<SnapPoint>();
            virtualList.Add(new() { Id = -1, Location = pseudoStart });
            virtualList.Add(new() { Id = -2, Location = pseudoEnd });
            var index = 0;
            foreach (var snap in SnapPoints)
            {
                virtualList.Add(new() { Id = index++, Location = snap });
            }

            SnapPointsVirtual = virtualList.ToList();
        }
        return SnapPointsVirtual;
    }

    protected virtual SnapPoint GetVirtualAnchor(Vector2 current, Vector2 velocity)
    {
        var orderedSnapPoints = GetVirtualSnapPoints().OrderBy(item => Vector2.Distance(item.Location, current)).ToList();
        return orderedSnapPoints.First();
    }

    protected override Vector2 FindNearestAnchorInternal(Vector2 current, Vector2 velocity)
    {
        if (IsLooped && SnapPoints.Count > 1)
        {
            var closest = GetVirtualAnchor(current, velocity);
            if (closest.Id == -1)
            {
                return SnapPoints.Last();
            }
            else if (closest.Id == -2)
            {
                return SnapPoints.First();
            }

            return closest.Location;
        }
        else
        {
            // Non-looped: delegate fully to base with the original inputs
            return base.FindNearestAnchorInternal(current, velocity);
        }
    }

    protected List<SnapPoint> SnapPointsVirtual { get; set; }

    #endregion

    #region PROPERTIES

    public static readonly BindableProperty SwipeSpeedProperty = BindableProperty.Create(
        nameof(SwipeSpeed),
        typeof(double),
        typeof(SkiaCarousel),
        1.0);

    /// <summary>
    /// Used inside ScrollToOffset to control the speed
    /// </summary>
    public double SwipeSpeed
    {
        get { return (double)GetValue(SwipeSpeedProperty); }
        set { SetValue(SwipeSpeedProperty, value); }
    }

    public static readonly BindableProperty PreloadNeighboorsProperty = BindableProperty.Create(
        nameof(PreloadNeighboors),
        typeof(bool),
        typeof(SkiaCarousel),
        true);

    /// <summary>
    /// Whether should preload neighboors sides cells even when they are hidden, to preload images etc.. Default is true. Beware set this to False if you have complex layouts otherwise rendering might be slow.
    /// </summary>
    public bool PreloadNeighboors
    {
        get { return (bool)GetValue(PreloadNeighboorsProperty); }
        set { SetValue(PreloadNeighboorsProperty, value); }
    }

    public static readonly BindableProperty DynamicSizeProperty = BindableProperty.Create(
        nameof(DynamicSize),
        typeof(bool),
        typeof(SkiaCarousel),
        false, propertyChanged: NeedInvalidateMeasure);

    /// <summary>
    /// When specific dimension is adapting to children size, will use max child dimension if False,
    /// otherwise will change size when children with different dimension size are selected. Default is false.
    /// If true, requires MeasureAllItems to be set to all items.
    /// </summary>
    public bool DynamicSize
    {
        get { return (bool)GetValue(DynamicSizeProperty); }
        set { SetValue(DynamicSizeProperty, value); }
    }


    public static readonly BindableProperty IsLoopedProperty = BindableProperty.Create(
        nameof(IsLooped),
        typeof(bool),
        typeof(SkiaCarousel),
        false, propertyChanged: NeedApplyOptions);

    /// <summary>
    /// UNIMPLEMENTED YET
    /// </summary>
    public bool IsLooped
    {
        get { return (bool)GetValue(IsLoopedProperty); }
        set { SetValue(IsLoopedProperty, value); }
    }


    public static readonly BindableProperty IsVerticalProperty = BindableProperty.Create(
        nameof(IsVertical),
        typeof(bool),
        typeof(SkiaCarousel),
        false, propertyChanged: NeedApplyOptions);

    /// <summary>
    /// Orientation of the carousel
    /// </summary>
    public bool IsVertical
    {
        get { return (bool)GetValue(IsVerticalProperty); }
        set { SetValue(IsVerticalProperty, value); }
    }


    protected virtual void ApplyDynamicSize(int index)
    {
        if (ChildrenFactory.TemplatesAvailable)
        {
            var cell = ChildrenFactory.GetViewForIndex(index);
            if (cell != null && !cell.NeedMeasure)
            {
                if (this.NeedAutoHeight)
                {
                    {
                        var height = cell.MeasuredSize.Units.Height;
                        if (height >= 0)
                        {
                            //Trace.WriteLine($"[DH] {height:0.0}");
                            this.ViewportHeightLimit = height;
                        }
                    }
                }

                if (this.NeedAutoWidth)
                {
                    {
                        var width = cell.MeasuredSize.Units.Width;
                        if (width >= 0)
                        {
                            //this.ViewportWidthLimit = width;
                        }
                    }
                }
            }

            if (IsTemplated)
                ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
        }
    }

    /// <summary>
    /// Will be updated on MainThread to safely work with bindings,
    /// this a read-only property returning SelectingIndex in a thread-safe way;
    /// </summary>
    public int SafeIndex
    {
        get { return SelectedIndex; }
    }

    public void GoNext()
    {
        _isSnapping = null;

        var index = SelectedIndex;
        if (index < MaxIndex)
        {
            SelectedIndex++;
            return;
        }

        // Edge -> loop behavior
        if (IsLooped)
        {
            SelectedIndex = 0;
        }
    }

    public virtual void GoPrev()
    {
        _isSnapping = null;

        var index = SelectedIndex;
        if (index > 0)
        {
            SelectedIndex--;
            return;
        }

        // Edge -> loop behavior
        if (IsLooped)
        {
            SelectedIndex = MaxIndex;
        }
    }

    public virtual bool IsAtStart
    {
        get { return SelectedIndex == 0; }
    }

    public virtual bool IsAtEnd
    {
        get { return SelectedIndex == MaxIndex; }
    }

    protected virtual void OnSelectedIndexChanged(int index)
    {
        //Debug.WriteLine($"[CAROUSEL] Index set to {index}");

        //forced to use ui-tread for maui not to randomly crash
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SelectedIndexChanged?.Invoke(this, index);

            OnPropertyChanged(nameof(SafeIndex));
            OnPropertyChanged(nameof(IsAtStart));
            OnPropertyChanged(nameof(IsAtEnd));
        });

        if (!LayoutReady || _isSnapping != null)
            return;


        if (!ChildrenInitialized)
        {
            InitializeChildren();
        }
        else
        {
            ApplyIndex();
        }

        if (DynamicSize && SelectedIndex >= 0)
        {
            ApplyDynamicSize(SelectedIndex);
        }
    }


    public static readonly BindableProperty LinearSpeedMsProperty = BindableProperty.Create(
        nameof(LinearSpeedMs),
        typeof(double),
        typeof(SkiaCarousel),
        0.0);

    /// <summary>
    /// How long would a whole auto-sliding take, if `Bounces` is `False`.
    /// If set (>0) will be used for automatic scrolls instead of using manual velocity.
    /// For bouncing carousel
    /// </summary>
    public double LinearSpeedMs
    {
        get { return (double)GetValue(LinearSpeedMsProperty); }
        set { SetValue(LinearSpeedMsProperty, value); }
    }

    private int _LastIndex;

    public int LastIndex
    {
        get { return _LastIndex; }
        set
        {
            if (_LastIndex != value)
            {
                _LastIndex = value;
                OnPropertyChanged();
            }
        }
    }


    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SkiaCarousel),
        -1, BindingMode.TwoWay,
        propertyChanged: (b, o, n) =>
        {
            if (b is SkiaCarousel control)
            {
                control.LastIndex = (int)o;
                control.OnSelectedIndexChanged((int)n);
            }
        });

    /// <summary>
    /// Zero-based index of the currently selected slide
    /// </summary>
    public int SelectedIndex
    {
        get { return (int)GetValue(SelectedIndexProperty); }
        set { SetValue(SelectedIndexProperty, value); }
    }


    /// <summary>
    /// Can be used for indicators
    /// </summary>
    public int ChildrenTotal
    {
        get { return (int)GetValue(ChildrenTotalProperty); }
        set { SetValue(ChildrenTotalProperty, value); }
    }

    public static readonly BindableProperty ChildrenTotalProperty = BindableProperty.Create(
        nameof(ChildrenTotal),
        typeof(int),
        typeof(SkiaCarousel),
        0, BindingMode.OneWayToSource);


    public static readonly BindableProperty IsRightToLeftProperty = BindableProperty.Create(
        nameof(IsRightToLeft),
        typeof(bool),
        typeof(SkiaCarousel),
        false, propertyChanged: NeedApplyOptions);

    /// <summary>
    /// TODO
    /// </summary>
    public bool IsRightToLeft
    {
        get { return (bool)GetValue(IsRightToLeftProperty); }
        set { SetValue(IsRightToLeftProperty, value); }
    }

    public static readonly BindableProperty SidesOffsetProperty = BindableProperty.Create(
        nameof(SidesOffset),
        typeof(double),
        typeof(SkiaCarousel),
        0.0, propertyChanged: NeedAdaptChildren);

    /// <summary>
    /// Basically size margins of every slide, offset from the side of the carousel. Another similar but different property to use would be Spacing between slides.
    /// </summary>
    public double SidesOffset
    {
        get { return (double)GetValue(SidesOffsetProperty); }
        set { SetValue(SidesOffsetProperty, value); }
    }


    private static void NeedAdaptChildren(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCarousel control)
        {
            control.AdaptChildren();
        }
    }

    #endregion

    #region GESTURES

    public bool IsUserFocused { get; protected set; }
    public bool IsUserPanning { get; protected set; }

    // When finger-down interrupts an in-flight transition but the user doesn't actually start panning the carousel
    // (e.g., they swipe vertically inside the current slide), we still need to snap back on Up.
    private bool _snapIfNoPanOnUp;

    protected Vector2 _panningOffset;
    protected Vector2 _panningStartOffset;
    private SKRect _lastViewport;

    protected VelocityAccumulator VelocityAccumulator { get; } = new();

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        CheckHovered(args);

        bool wrongDirection = false;
        bool passedToChildren = false;
        var consumedDefault = BlockGesturesBelow ? this : null;

        ISkiaGestureListener PassToChildren()
        {
            passedToChildren = true;

            if (!IsTemplated || RecyclingTemplate == RecyclingTemplate.Disabled)
            {
                return base.ProcessGestures(args, apply); //can pass to them all
            }

            //todo templated visible only...

            return base.ProcessGestures(args, apply);
        }

        ISkiaGestureListener consumed = null;

        if (!IsUserPanning || !RespondsToGestures || args.Type == TouchActionResult.Tapped)
        {
            consumed = PassToChildren();
            if (consumed == this)
            {
                //BlockGesturesBelow fired
                consumed = null;
            }

            // For Up: even if a child consumed the gesture, we may still need to perform a snap side-effect
            // if Down interrupted an in-flight transition and we never started a horizontal pan.
            if (consumed != null && !(args.Type == TouchActionResult.Up && _snapIfNoPanOnUp))
            {
                return consumed;
            }
        }

        if (!RespondsToGestures || ChildrenTotal < 2)
            return consumedDefault;

        void ResetPan()
        {
            wrongDirection = false;

            IsUserFocused = true;
            IsUserPanning = false;

            // Remember if we interrupted a snap/transition; if user doesn't pan horizontally after this,
            // we will re-snap on Up.
            _snapIfNoPanOnUp = _isSnapping != null || InTransition;

            AnimatorRange.Stop();
            VectorAnimatorSpring?.Stop();
            VelocityAccumulator.Clear();

            // If Stop() doesn't trigger Finished handlers, make sure we don't stay stuck in "transition".
            _isSnapping = null;

            FixPosition();

            _panningOffset = CurrentPosition;
            _panningStartOffset = CurrentPosition;
        }

        switch (args.Type)
        {
            case TouchActionResult.Down:

                //        if (!IsUserFocused) //first finger down
                if (args.Event.NumberOfTouches == 1) //first finger down
                {
                    ResetPan();
                }

                consumed = this;

                break;

            case TouchActionResult.Panning when args.Event.NumberOfTouches == 1 && !wrongDirection:

                if (!IsUserPanning)
                {
                    //first pan
                    var movex = Math.Abs(args.Event.Distance.Delta.X);
                    var movey = Math.Abs(args.Event.Distance.Delta.Y);
                    if (movex < RenderingScale * 2 || movey > movex)
                    {
                        //IgnoreWrongDirection
                        wrongDirection = true;
                        return consumedDefault;
                    }
                }

                if (!IsUserFocused)
                {
                    ResetPan();
                }

                IsUserPanning = true;
                _snapIfNoPanOnUp = false;

                var x = _panningOffset.X + args.Event.Distance.Delta.X / RenderingScale;
                var y = _panningOffset.Y + args.Event.Distance.Delta.Y / RenderingScale;

                Vector2 velocity;
                float useVelocity = 0;
                if (!IsVertical)
                {
                    useVelocity = (float)(args.Event.Distance.Velocity.X / RenderingScale);
                    velocity = new(useVelocity, 0);
                }
                else
                {
                    useVelocity = (float)(args.Event.Distance.Velocity.Y / RenderingScale);
                    velocity = new(0, useVelocity);
                }

                //record velocity
                VelocityAccumulator.CaptureVelocity(velocity);

                //saving non clamped
                _panningOffset.X = x;
                _panningOffset.Y = y;


                var clamped = ClampOffset((float)x, (float)y, Bounces);

                //Debug.WriteLine($"[CAROUSEL] Panning: {_panningOffset:0} / {clamped:0}");
                ApplyPosition(clamped);

                consumed = this;
                break;

            case TouchActionResult.Up:
                //Debug.WriteLine($"[Carousel] {args.Type} {IsUserFocused} {IsUserPanning} {InTransition}");

                if (IsUserPanning)
                {
                    consumed = this;

                    var final = VelocityAccumulator.CalculateFinalVelocity(500);

                    //animate
                    CurrentSnap = CurrentPosition;

                    Debug.WriteLine($"[SkiaCarousel] UP velocity {final:0}");

                    ScrollToNearestAnchor(CurrentSnap, final);

                    IsUserPanning = false;
                    IsUserFocused = false;
                    _snapIfNoPanOnUp = false;
                }
                else if (_snapIfNoPanOnUp)
                {
                    // We interrupted an in-flight snap (on Down) but never actually started a carousel pan.
                    // Ensure we still snap to the nearest anchor. Do not consume Up so inner controls keep it.
                    CurrentSnap = CurrentPosition;
                    ScrollToNearestAnchor(CurrentSnap, Vector2.Zero);

                    IsUserFocused = false;
                    IsUserPanning = false;
                    _snapIfNoPanOnUp = false;
                }

                break;
        }

        if (consumed != null || IsUserPanning)
        {
            if (consumed == null && args.Type != TouchActionResult.Up)
            {
                return this;
            }

            return consumed;
        }

        if (!passedToChildren)
        {
            return PassToChildren();
        }

        return consumedDefault;
    }

    #endregion
}
