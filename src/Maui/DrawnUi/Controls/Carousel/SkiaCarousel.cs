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

        base.ScrollToNearestAnchor(location, velocity);
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


    public virtual void ApplyIndex(bool instant = false)
    {
        if (SelectedIndex >= 0 && SelectedIndex < SnapPoints.Count)
        {
            var snapPoint = SnapPoints[SelectedIndex];

            ScrollToOffset(snapPoint, Vector2.Zero, !instant && CanAnimate && Animated);
        }
    }

    public override void UpdateReportedPosition()
    {
        if (SnapPoints == null || SnapPoints.Count == 0)
            return;

        var snapPoint = SnapPoints.FirstOrDefault(x => AreVectorsEqual(x, CurrentSnap, 1));

        var index = SnapPoints.IndexOf(snapPoint);
        if (index >= 0 && index < ChildrenFactory.GetChildrenCount())
        {
            SelectedIndex = index;
        }
    }

    #endregion

    #region TEMPLATE

    /// <summary>
    /// This might be called from background thread if we set InitializeTemplatesInBackgroundDelay true
    /// </summary>
    public override void OnTemplatesAvailable()
    {
        ApplyOptions();

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
        _itemsSourceChangedNeedResetIndex = _loaded;

        _loaded = true;

        base.OnItemSourceChanged();

        AdaptChildren();

        if (ChildrenFactory.TemplatesAvailable)
        {
            ApplyOptions();
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

        return (new Vector2(newX, newY), isVisible, nextToScreen);
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

            if (displacement != Vector2.Zero)
            {
                //if (atSnapPoint && !ThresholdOk(displacement))
                //{
                //    //Debug.WriteLine("[CAROUSEL] threshold low");
                //    return false;
                //}

                if (Bounces)
                {
                    var spring = new Spring((float)(1 * (1 + RubberDamping)), 200, (float)(0.5f * (1 + RubberDamping)));
                    VectorAnimatorSpring.Initialize(end, displacement, velocity, spring, 0.5f);
                    VectorAnimatorSpring.Start();
                    _isSnapping = end;
                }
                else
                {
                    var maxSpeed = 0.25; //secs

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
                    AnimatorRange.Initialize(start, end, (float)speed, Easing.Linear);
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

    bool ThresholdOk(Vector2 displacement)
    {
        var threshold = 10;

        if (IsVertical)
            return threshold < Math.Abs(displacement.Y);

        return threshold < Math.Abs(displacement.X);
    }

    public override void ApplyOptions()
    {
        if (Parent == null)
            return;

        SetupViewport();

        //Viewport = Parent.DrawingRect;

        //InitializeChildren();

        base.ApplyOptions();
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
                    OnStop = () => { Stopped?.Invoke(this, CurrentPosition); },
                    OnVectorUpdated = (value) => { ApplyPosition(value); },
                    Finished = () => { _isSnapping = null; },
                };
                AnimatorRange = new(this)
                {
                    OnVectorUpdated = (value) => { ApplyPosition(value); },
                    OnStop = () => { Stopped?.Invoke(this, CurrentPosition); },
                    Finished = () => { _isSnapping = null; },
                };
            }

            ApplyOptions();
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

            if (!IsLooped)
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
            return;
        }

        ChildrenInitialized = true;

        var childrenCount = ChildrenFactory.GetChildrenCount();

        MaxIndex = childrenCount - 1;

        ChildrenTotal = childrenCount;

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

    protected override Vector2 FindNearestAnchorInternal(Vector2 current, Vector2 velocity)
    {
        if (velocity == Vector2.Zero)
        {
            return base.FindNearestAnchorInternal(current, velocity);
        }

        //hack to slide only 1 item at a time
        return base.FindNearestAnchorInternal(_panningStartOffset, velocity);
    }

    #endregion

    #region PROPERTIES

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
        //todo use IsLooped
        var index = SelectedIndex;
        if (index < MaxIndex)
        {
            SelectedIndex++;
        }
    }

    public virtual void GoPrev()
    {
        //todo use IsLooped
        var index = SelectedIndex;
        if (index > 0)
        {
            SelectedIndex--;
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
        _itemsSourceChangedNeedResetIndex = false;

        SelectedIndexChanged?.Invoke(this, index);

        Debug.WriteLine($"[CAROUSEL] Index set to {index}");

        //forced to use ui-tread for maui not to randomly crash 
        MainThread.BeginInvokeOnMainThread(() =>
        {
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

    protected Vector2 _panningOffset;
    protected Vector2 _panningStartOffset;
    private SKRect _lastViewport;

    protected VelocityAccumulator VelocityAccumulator { get; } = new();

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
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
            if (consumed != null)
            {
                return consumed;
            }
        }

        if (!RespondsToGestures)
            return consumedDefault;

        void ResetPan()
        {
            IsUserFocused = true;
            IsUserPanning = false;

            AnimatorRange.Stop();

            VectorAnimatorSpring?.Stop();
            VelocityAccumulator.Clear();

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

            case TouchActionResult.Panning when args.Event.NumberOfTouches == 1:

                if (!IsUserPanning)
                {
                    //first pan
                    if (args.Event.Distance.Total.X == 0 ||
                        Math.Abs(args.Event.Distance.Total.Y) > Math.Abs(args.Event.Distance.Total.X) ||
                        Math.Abs(args.Event.Distance.Total.X) < 2)
                    {
                        return consumedDefault;
                    }
                }

                if (!IsUserFocused)
                {
                    ResetPan();
                }

                //todo add direction
                //this.IgnoreWrongDirection

                IsUserPanning = true;

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

                if (IsUserFocused)
                {
                    if (IsUserPanning || InTransition)
                    {
                        consumed = this;

                        var final = VelocityAccumulator.CalculateFinalVelocity(500);

                        //animate
                        CurrentSnap = CurrentPosition;

                        ScrollToNearestAnchor(CurrentSnap, final);
                    }

                    IsUserPanning = false;
                    IsUserFocused = false;
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
