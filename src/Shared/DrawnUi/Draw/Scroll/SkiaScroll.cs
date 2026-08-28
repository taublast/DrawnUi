using DrawnUi.Infrastructure.Helpers;

namespace DrawnUi.Draw
{
    [ContentProperty("Content")]
    public partial class SkiaScroll : SkiaControl, ISkiaGestureListener, IDefinesViewport, IWithContent
    {

        /// <summary>
        /// Min velocity in points/sec to flee/swipe when finger is up
        /// </summary>
        public static float ThesholdSwipeOnUp = 20f;

        /// <summary>
        /// To filter micro-gestures while manually panning
        /// </summary>
        public static float ScrollVelocityThreshold = 5;

        /// <summary>
        /// Time for the snapping animations as well as the scroll to top etc animations..
        /// </summary>
        public static float SystemAnimationTimeSecs = 0.2f;

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            IndexChanged = null;
            ScrollingEnded = null;
            Scrolled = null;

            Content?.Dispose();
            Header?.Dispose();
            Footer?.Dispose();

            if (InternalScrollBar is SkiaControl scrollBar)
            {
                scrollBar.Dispose();
            }

            if (InternalScrollBarHorizontal is SkiaControl scrollBarHorizontal)
            {
                scrollBarHorizontal.Dispose();
            }
        }

        private ScrollInteractionState _intercationState;

        public ScrollInteractionState InteractionState
        {
            get { return _intercationState; }
            set
            {
                if (_intercationState != value)
                {
                    _intercationState = value;
                    OnPropertyChanged();
                }
            }
        }

        public virtual void UpdateVisibleIndex()
        {
            if (LayoutReady && TrackIndexPosition != RelativePositionType.None)
            {
                CalculateVisibleIndexes();
                //CurrentIndexHit = CalculateVisibleIndex(TrackIndexPosition);
                //CurrentIndex = CurrentIndexHit.Index;
            }
        }

        #region Scrollers

        public bool HasContentToScroll
        {
            get { return _hasContentToScroll; }
            set
            {
                if (_hasContentToScroll != value)
                {
                    _hasContentToScroll = value;
                    OnPropertyChanged();
                }
            }
        }

        bool _hasContentToScroll;

        /// <summary>
        /// Content is measured but does not fill the viewport along the scroll orientation.
        /// This is exactly when LoadMore must stay available even though nothing can scroll yet
        /// (e.g. a window rebase landed near the dataset end with a single resident item).
        /// </summary>
        public bool ContentUnderfillsViewport
        {
            get
            {
                if (Orientation == ScrollOrientation.Vertical)
                    return ptsContentHeight > 0 && ptsContentHeight < Viewport.Units.Height;

                if (Orientation == ScrollOrientation.Horizontal)
                    return ptsContentWidth > 0 && ptsContentWidth < Viewport.Units.Width;

                return false;
            }
        }

        public static readonly BindableProperty HeaderStickyProperty = BindableProperty.Create(
            nameof(HeaderSticky),
            typeof(bool),
            typeof(SkiaScroll),
            false, propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Should the header stay in place when content is scrolling
        /// </summary>
        public bool HeaderSticky
        {
            get { return (bool)GetValue(HeaderStickyProperty); }
            set { SetValue(HeaderStickyProperty, value); }
        }

        public static readonly BindableProperty ParallaxOverscrollEnabledProperty = BindableProperty.Create(
            nameof(ParallaxOverscrollEnabled),
            typeof(bool),
            typeof(SkiaScroll),
            true, propertyChanged: NeedInvalidateMeasure);

        public bool ParallaxOverscrollEnabled
        {
            get { return (bool)GetValue(ParallaxOverscrollEnabledProperty); }
            set { SetValue(ParallaxOverscrollEnabledProperty, value); }
        }

        public static readonly BindableProperty HeaderBehindProperty = BindableProperty.Create(
            nameof(HeaderBehind),
            typeof(bool),
            typeof(SkiaScroll),
            false, propertyChanged: NeedInvalidateMeasure);

        public bool HeaderBehind
        {
            get { return (bool)GetValue(HeaderBehindProperty); }
            set { SetValue(HeaderBehindProperty, value); }
        }

        public static readonly BindableProperty ContentOffsetProperty = BindableProperty.Create(
            nameof(ContentOffset),
            typeof(double),
            typeof(SkiaScroll),
            0.0, propertyChanged: NeedDraw);

        public double ContentOffset
        {
            get { return (double)GetValue(ContentOffsetProperty); }
            set { SetValue(ContentOffsetProperty, value); }
        }

        public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
            nameof(Header),
            typeof(SkiaControl),
            typeof(SkiaScroll),
            null, propertyChanged: (b, o, n) =>
            {
                if (b is SkiaScroll control)
                {
                    control.SetHeader((SkiaControl)n);
                }
            });

        public SkiaControl Header
        {
            get { return (SkiaControl)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly BindableProperty HeaderParallaxRatioProperty = BindableProperty.Create(
            nameof(HeaderParallaxRatio),
            typeof(double),
            typeof(SkiaScroll),
            1.0, propertyChanged: NeedDraw);

        public double HeaderParallaxRatio
        {
            get { return (double)GetValue(HeaderParallaxRatioProperty); }
            set { SetValue(HeaderParallaxRatioProperty, value); }
        }

        public static readonly BindableProperty FooterProperty = BindableProperty.Create(
            nameof(Footer),
            typeof(SkiaControl),
            typeof(SkiaScroll),
            null, propertyChanged: (b, o, n) =>
            {
                if (b is SkiaScroll control)
                {
                    control.SetFooter((SkiaControl)n);
                }
            });

        public SkiaControl Footer
        {
            get { return (SkiaControl)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly BindableProperty RefreshIndicatorProperty = BindableProperty.Create(
            nameof(RefreshIndicator),
            typeof(IRefreshIndicator),
            typeof(SkiaScroll),
            null,
            propertyChanged: OnNeedSetRefreshIndicator);

        public IRefreshIndicator RefreshIndicator
        {
            get { return (IRefreshIndicator)GetValue(RefreshIndicatorProperty); }
            set { SetValue(RefreshIndicatorProperty, value); }
        }

        private static void OnNeedSetRefreshIndicator(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control.SetRefreshIndicator(newvalue as IRefreshIndicator);
            }
        }

        protected IRefreshIndicator InternalRefreshIndicator { get; set; }

        private void SetRefreshIndicator(IRefreshIndicator indicator)
        {
            //delete existing from Views
            //and dispose
            if (InternalRefreshIndicator is SkiaControl control)
            {
                control.SetParent(null);
                control.Dispose();
            }

            //set props for the new one and and it to views
            if (indicator is SkiaControl newControl)
            {
                InternalRefreshIndicator = indicator;

                //if (Orientation == ScrollOrientation.Vertical)
                //{
                //    newControl.HeightRequest = RefreshDistanceLimit;
                //}
                //else if (Orientation == ScrollOrientation.Horizontal)
                //{
                //    newControl.WidthRequest = RefreshDistanceLimit;
                //}

                if (!newControl.IsSet(ZIndexProperty)) newControl.ZIndex = 1000;
                AddSubView(newControl);
            }
        }

        public static readonly BindableProperty ScrollBarProperty = BindableProperty.Create(
            nameof(ScrollBar),
            typeof(IScrollBar),
            typeof(SkiaScroll),
            null,
            propertyChanged: OnNeedSetScrollBar);

        /// <summary>
        /// Optional scroll bar indicator drawn as overlay over the viewport.
        /// Assign a SkiaScrollBar or any SkiaControl implementing IScrollBar.
        /// The scroll pushes progress/thumb-size/overscroll state to it when scroll position changes.
        /// Null by default: costs nothing when unused.
        /// </summary>
        public IScrollBar ScrollBar
        {
            get { return (IScrollBar)GetValue(ScrollBarProperty); }
            set { SetValue(ScrollBarProperty, value); }
        }

        private static void OnNeedSetScrollBar(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control._scrollBarAutoCreated = false; // explicit assignment, ours to keep as-is
                control.SetScrollBar(newvalue as IScrollBar);
            }
        }

        public static readonly BindableProperty ScrollBarHorizontalProperty = BindableProperty.Create(
            nameof(ScrollBarHorizontal),
            typeof(IScrollBar),
            typeof(SkiaScroll),
            null,
            propertyChanged: OnNeedSetScrollBarHorizontal);

        /// <summary>
        /// Same as ScrollBar but for the horizontal axis, used together with ScrollBar for a scroll
        /// with Orientation="Both" that needs both bars shown at once.
        /// </summary>
        public IScrollBar ScrollBarHorizontal
        {
            get { return (IScrollBar)GetValue(ScrollBarHorizontalProperty); }
            set { SetValue(ScrollBarHorizontalProperty, value); }
        }

        private static void OnNeedSetScrollBarHorizontal(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control._scrollBarHorizontalAutoCreated = false;
                control.SetScrollBarHorizontal(newvalue as IScrollBar);
            }
        }

        public static readonly BindableProperty ScrollBarsVisibilityProperty = BindableProperty.Create(
            nameof(ScrollBarsVisibility),
            typeof(ScrollBarVisibility),
            typeof(SkiaScroll),
            ScrollBarVisibility.None,
            propertyChanged: (b, o, n) => (b as SkiaScroll)?.ApplyScrollBarsVisibility());

        /// <summary>
        /// Enables the scroll bar overlay(s) without writing code: set Vertical and/or Horizontal
        /// (it's [Flags], both can be set at once e.g. for Orientation="Both") and a default
        /// SkiaScrollBar is created for each axis flagged. Assign ScrollBar / ScrollBarHorizontal
        /// yourself for a custom bar; visibility flags then just show/hide it. Default None (no bars).
        /// </summary>
        public ScrollBarVisibility ScrollBarsVisibility
        {
            get { return (ScrollBarVisibility)GetValue(ScrollBarsVisibilityProperty); }
            set { SetValue(ScrollBarsVisibilityProperty, value); }
        }

        private bool _scrollBarAutoCreated;
        private bool _scrollBarHorizontalAutoCreated;

        protected virtual void ApplyScrollBarsVisibility()
        {
            var wantVertical = ScrollBarsVisibility.HasFlag(ScrollBarVisibility.Vertical);
            if (wantVertical && ScrollBar == null)
            {
                ScrollBar = new SkiaScrollBar();
                _scrollBarAutoCreated = true; // ScrollBarProperty's changed handler runs first and would reset this, so set after
            }
            else if (!wantVertical && _scrollBarAutoCreated)
            {
                ScrollBar = null;
                _scrollBarAutoCreated = false;
            }

            var wantHorizontal = ScrollBarsVisibility.HasFlag(ScrollBarVisibility.Horizontal);
            if (wantHorizontal && ScrollBarHorizontal == null)
            {
                ScrollBarHorizontal = new SkiaScrollBar();
                _scrollBarHorizontalAutoCreated = true;
            }
            else if (!wantHorizontal && _scrollBarHorizontalAutoCreated)
            {
                ScrollBarHorizontal = null;
                _scrollBarHorizontalAutoCreated = false;
            }
        }

        public static readonly BindableProperty ScrollBarThumbColorProperty = BindableProperty.Create(
            nameof(ScrollBarThumbColor),
            typeof(Color),
            typeof(SkiaScroll),
            Color.FromArgb("#66888888"),
            propertyChanged: (b, o, n) => (b as SkiaScroll)?.ApplyScrollBarColors());

        /// <summary>
        /// Thumb color pushed to ScrollBar / ScrollBarHorizontal when they are SkiaScrollBar instances
        /// (auto-created or user-assigned). Ignored by custom IScrollBar types.
        /// </summary>
        public Color ScrollBarThumbColor
        {
            get { return (Color)GetValue(ScrollBarThumbColorProperty); }
            set { SetValue(ScrollBarThumbColorProperty, value); }
        }

        public static readonly BindableProperty ScrollBarTrackColorProperty = BindableProperty.Create(
            nameof(ScrollBarTrackColor),
            typeof(Color),
            typeof(SkiaScroll),
            Colors.Transparent,
            propertyChanged: (b, o, n) => (b as SkiaScroll)?.ApplyScrollBarColors());

        /// <summary>
        /// Track color pushed to ScrollBar / ScrollBarHorizontal when they are SkiaScrollBar instances
        /// (auto-created or user-assigned).
        /// </summary>
        public Color ScrollBarTrackColor
        {
            get { return (Color)GetValue(ScrollBarTrackColorProperty); }
            set { SetValue(ScrollBarTrackColorProperty, value); }
        }

        protected virtual void ApplyScrollBarColors()
        {
            if (InternalScrollBar is SkiaScrollBar bar)
            {
                bar.ThumbColor = ScrollBarThumbColor;
                bar.TrackColor = ScrollBarTrackColor;
            }

            if (InternalScrollBarHorizontal is SkiaScrollBar barHorizontal)
            {
                barHorizontal.ThumbColor = ScrollBarThumbColor;
                barHorizontal.TrackColor = ScrollBarTrackColor;
            }
        }

        protected IScrollBar InternalScrollBar { get; set; }
        protected IScrollBar InternalScrollBarHorizontal { get; set; }

        private void SetScrollBar(IScrollBar indicator)
        {
            //delete existing from Views and dispose
            if (InternalScrollBar is SkiaControl control)
            {
                control.SetParent(null);
                control.Dispose();
            }

            InternalScrollBar = indicator;
            _scrollBarLastProgress = float.MinValue; //force push to the new indicator

            if (indicator is SkiaControl newControl)
            {
                if (!newControl.IsSet(ZIndexProperty)) newControl.ZIndex = 1001;
                AddSubView(newControl);
            }

            ApplyScrollBarColors();
        }

        private void SetScrollBarHorizontal(IScrollBar indicator)
        {
            if (InternalScrollBarHorizontal is SkiaControl control)
            {
                control.SetParent(null);
                control.Dispose();
            }

            InternalScrollBarHorizontal = indicator;
            _scrollBarHLastProgress = float.MinValue;

            if (indicator is SkiaControl newControl)
            {
                if (!newControl.IsSet(ZIndexProperty)) newControl.ZIndex = 1001;
                AddSubView(newControl);
            }

            ApplyScrollBarColors();
        }

        private float _scrollBarLastProgress = float.MinValue;
        private float _scrollBarLastRatio;
        private float _scrollBarLastOverscroll;
        private bool _scrollBarLastScrolling;

        private float _scrollBarHLastProgress = float.MinValue;
        private float _scrollBarHLastRatio;
        private float _scrollBarHLastOverscroll;
        private bool _scrollBarHLastScrolling;

        /// <summary>
        /// Pushes current scroll state to the attached ScrollBar/ScrollBarHorizontal indicators.
        /// Called on every frame draw, no-op per bar when nothing changed since its last push.
        /// </summary>
        protected virtual void UpdateScrollBarIndicator()
        {
            var isScrolling = IsScrolling || IsUserPanning;

            if (InternalScrollBar != null)
            {
                var progress = (float)ScrollProgressY;
                var ratio = ptsContentHeight > 0 ? Viewport.Units.Height / ptsContentHeight : 1f;
                var overscroll = OverscrollDistance.Y;

                if (progress != _scrollBarLastProgress
                    || ratio != _scrollBarLastRatio
                    || overscroll != _scrollBarLastOverscroll
                    || isScrolling != _scrollBarLastScrolling)
                {
                    _scrollBarLastProgress = progress;
                    _scrollBarLastRatio = ratio;
                    _scrollBarLastOverscroll = overscroll;
                    _scrollBarLastScrolling = isScrolling;
                    InternalScrollBar.SetScrollProgress(ScrollOrientation.Vertical, progress, ratio, overscroll,
                        isScrolling);
                }
            }

            if (InternalScrollBarHorizontal != null)
            {
                var progress = (float)ScrollProgressX;
                var ratio = ptsContentWidth > 0 ? Viewport.Units.Width / ptsContentWidth : 1f;
                var overscroll = OverscrollDistance.X;

                if (progress != _scrollBarHLastProgress
                    || ratio != _scrollBarHLastRatio
                    || overscroll != _scrollBarHLastOverscroll
                    || isScrolling != _scrollBarHLastScrolling)
                {
                    _scrollBarHLastProgress = progress;
                    _scrollBarHLastRatio = ratio;
                    _scrollBarHLastOverscroll = overscroll;
                    _scrollBarHLastScrolling = isScrolling;
                    InternalScrollBarHorizontal.SetScrollProgress(ScrollOrientation.Horizontal, progress, ratio,
                        overscroll, isScrolling);
                }
            }
        }

        private static void NeedToScroll(BindableObject bindable, object oldvalue, object newvalue)
        {
            if ((int)newvalue >= 0 && bindable is SkiaScroll scroll)
            {
                scroll.ScrollToIndex(index: (int)newvalue,
                    scroll.OrderedScrollIsAnimated,
                    scroll.TrackIndexPosition);
                scroll.OrderedScroll = -1;
            }
        }

        public static readonly BindableProperty OrderedScrollProperty = BindableProperty.Create(nameof(OrderedScroll),
            typeof(int),
            typeof(SkiaScroll), -1, BindingMode.TwoWay, propertyChanged: NeedToScroll);

        public int OrderedScroll
        {
            get { return (int)GetValue(OrderedScrollProperty); }
            set { SetValue(OrderedScrollProperty, value); }
        }

        public static readonly BindableProperty OrderedScrollIsAnimatedProperty = BindableProperty.Create(
            nameof(OrderedScrollIsAnimated),
            typeof(bool),
            typeof(SkiaScroll), false);

        public bool OrderedScrollIsAnimated
        {
            get { return (bool)GetValue(OrderedScrollIsAnimatedProperty); }
            set { SetValue(OrderedScrollIsAnimatedProperty, value); }
        }

        public static readonly BindableProperty RefreshEnabledProperty = BindableProperty.Create(nameof(RefreshEnabled),
            typeof(bool),
            typeof(SkiaScroll),
            false);

        public bool RefreshEnabled
        {
            get { return (bool)GetValue(RefreshEnabledProperty); }
            set { SetValue(RefreshEnabledProperty, value); }
        }

        public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(nameof(IsRefreshing),
            typeof(bool),
            typeof(SkiaScroll),
            false,
            BindingMode.TwoWay,
            propertyChanged: (bindable, old, changed) =>
            {
                if (bindable is SkiaScroll scroll)
                {
                    try
                    {
                        scroll.SetIsRefreshing((bool)changed, false);
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }
            });

        public bool IsRefreshing
        {
            get { return (bool)GetValue(IsRefreshingProperty); }
            set { SetValue(IsRefreshingProperty, value); }
        }

        public static readonly BindableProperty RefreshCommandProperty = BindableProperty.Create(nameof(RefreshCommand),
            typeof(ICommand),
            typeof(SkiaScroll),
            null);

        public ICommand RefreshCommand
        {
            get { return (ICommand)GetValue(RefreshCommandProperty); }
            set { SetValue(RefreshCommandProperty, value); }
        }

        public static readonly BindableProperty RefreshDistanceLimitProperty = BindableProperty.Create(
            nameof(RefreshDistanceLimit),
            typeof(float),
            typeof(SkiaScroll),
            150f);

        /// <summary>
        /// Applyed to RefreshView
        /// </summary>
        public float RefreshDistanceLimit
        {
            get { return (float)GetValue(RefreshDistanceLimitProperty); }
            set { SetValue(RefreshDistanceLimitProperty, value); }
        }

        public static readonly BindableProperty RefreshShowDistanceProperty = BindableProperty.Create(
            nameof(RefreshShowDistance),
            typeof(float),
            typeof(SkiaScroll),
            50f);

        /// <summary>
        /// Applyed to RefreshView, distance in points where the refresh view will stop moving and stay here animating
        /// </summary>
        public float RefreshShowDistance
        {
            get { return (float)GetValue(RefreshShowDistanceProperty); }
            set { SetValue(RefreshShowDistanceProperty, value); }
        }

        public Easing ScrollingEasing = Easing.SpringOut;
        private readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(10);
        private float filterFactor = 0.99f; //   (0 to 1)
        protected float _velocitySwipe = 200; //pts
        protected float _velocitySwipeRatio = 1.0f;
        protected Vector2 _panningLastDelta;
        protected Vector2 _panningCurrentOffsetPts;
        private Vector2 _panningStartOffsetPts;

        public async void PlayEdgeGlowAnimation(Color color, double x, double y, bool removePrevious = true)
        {
            if (removePrevious)
            {
                UnregisterAllAnimatorsByType(typeof(EdgeGlowAnimator));
            }

            var animation = new EdgeGlowAnimator(this)
            {
                GlowPosition = GlowPosition.Top,
                Color = color.ToSKColor(),
                X = x,
                Y = y,
            };
            animation.Start();
        }

        /// <summary>
        /// Units
        /// </summary>
        public Vector2 OverscrollDistance
        {
            get { return _overscrollDistance; }
            set
            {
                if (_overscrollDistance != value)
                {
                    //if (_rubberBandDistanceY == 0 && value != 0)
                    //{
                    //	//show effect
                    //	PlayEdgeGlowAnimation(Colors.White, 100, 100);
                    //}
                    _overscrollDistance = value;
                    OnPropertyChanged();
                }
            }
        }

        Vector2 _overscrollDistance;

        public bool ScrollLocked
        {
            get { return _scrollLocked; }
            set
            {
                if (_scrollLocked != value)
                {
                    _scrollLocked = value;
                    OnPropertyChanged();
                    //Debug.WriteLine($"[SCROLL] ScrollLocked = {value}");
                }
            }
        }

        bool _scrollLocked;

        //private const float PanTimeThreshold = 10;
        //private DateTimeOffset lastPanTime = DateTimeOffset.Now;
        protected VelocityTracker VelocityTrackerPan = new();
        protected VelocityTracker VelocityTrackerScale = new();
        DateTime lastPanTime;

        /// <summary>
        /// There are the bounds the scroll offset can go to.. This is NOT the bounds for the whole content.
        /// </summary>
        protected SKRect ContentOffsetBounds { get; set; }

        /// <summary>
        /// Used to clamp while panning while finger is down
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        //protected virtual Vector2 ClampOffsetWithRubberBand(float x, float y)
        //{
        //    var clampedElastic = RubberBandUtils.ClampOnTrack(new Vector2(x, y), ContentOffsetBounds, (float)RubberEffect);

        //    if (Orientation == ScrollOrientation.Vertical)
        //    {
        //        var clampedX = Math.Max(ContentOffsetBounds.Left, Math.Min(ContentOffsetBounds.Right, x));
        //        return clampedElastic with { X = clampedX };
        //    }
        //    else
        //    if (Orientation == ScrollOrientation.Horizontal)
        //    {
        //        var clampedY = Math.Max(ContentOffsetBounds.Top, Math.Min(ContentOffsetBounds.Bottom, y));
        //        return clampedElastic with { Y = clampedY };
        //    }

        //    return clampedElastic;
        //}
        protected virtual Vector2 ClampOffsetWithRubberBand(float x, float y, SKRect contentOffsetBounds)
        {
            Vector2 clampedElastic = Vector2.Zero;
            var add = Elastic * RenderingScale;
            var limit = RefreshDistanceLimit * RenderingScale;

            bool clamped = false;
            if (RefreshEnabled)
            {
                if (Orientation == ScrollOrientation.Vertical && y > 0) //pulling down
                {
                    clamped = true;

                    float adjusted = contentOffsetBounds.Height + limit;
                    var min = MeasuredSize.Pixels.Height + limit;
                    if (adjusted < min)
                        adjusted = min;

                    var customDims = new Vector2(contentOffsetBounds.Width, adjusted);
                    clampedElastic = RubberBandUtils.ClampOnTrack(
                        new Vector2(x, y),
                        contentOffsetBounds,
                        (float)RubberEffect,
                        customDims
                    );
                }
                else if (Orientation == ScrollOrientation.Horizontal && x > 0) //pulling right
                {
                    clamped = true;

                    float adjusted = contentOffsetBounds.Width + limit;
                    var min = MeasuredSize.Pixels.Width + limit;
                    if (adjusted < min)
                        adjusted = min;

                    var customDims = new Vector2(adjusted, contentOffsetBounds.Height);

                    clampedElastic = RubberBandUtils.ClampOnTrack(
                        new Vector2(x, y),
                        contentOffsetBounds,
                        (float)RubberEffect,
                        customDims
                    );
                }
            }

            if (!clamped)
            {
                clampedElastic = RubberBandUtils.ClampOnTrack(
                    new Vector2(x, y),
                    contentOffsetBounds,
                    (float)RubberEffect,
                    new Vector2(add, add)
                );
            }

            // Preserve the clamping in the non-scrolling direction
            if (Orientation == ScrollOrientation.Vertical)
            {
                var clampedX = Math.Max(contentOffsetBounds.Left, Math.Min(contentOffsetBounds.Right, x));
                return clampedElastic with { X = clampedX };
            }

            if (Orientation == ScrollOrientation.Horizontal)
            {
                var clampedY = Math.Max(contentOffsetBounds.Top, Math.Min(contentOffsetBounds.Bottom, y));
                return clampedElastic with { Y = clampedY };
            }

            return clampedElastic;
        }

        public static int Elastic = 100;

        // Virtualization cull viewport for the Content layout: the content's GetOnScreenVisibleArea walks up
        // to this and uses the returned rect to decide which cells are "visible" (others get recycled). When
        // planes were removed this override went with SkiaScroll.Planes.cs -> the walk-up never reached the
        // real viewport -> every cell was considered visible (cells == Data, recycling effectively off).
        // Restores the non-planes branch: return the actual ContentViewport (inflated by the requested band).
        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            if (Virtualisation != VirtualisationType.Disabled) //true by default
            {
                var inflated = ContentViewport.Pixels;
                if (inflated.IsEmpty)
                {
                    var initialViewport = Viewport.Pixels;
                    if (initialViewport.IsEmpty)
                        initialViewport = DrawingRect;

                    if (!initialViewport.IsEmpty)
                    {
                        initialViewport.Inflate(inflateByPixels.X, inflateByPixels.Y);
                        return ScaledRect.FromPixels(initialViewport, RenderingScale);
                    }

                    return ContentRectWithOffset; // last-resort before viewport is initialized
                }

                inflated.Inflate(inflateByPixels.X, inflateByPixels.Y);
                return ScaledRect.FromPixels(inflated, RenderingScale);
            }

            // Virtualisation disabled: whole content drawn, just translated while scrolling.
            return ContentRectWithOffset;
        }

        public virtual Vector2 ClampOffset(float x, float y, SKRect contentOffsetBounds, bool strict = false)
        {
            // The content may cap travel toward a region its backing (e.g. background plane cache) hasn't
            // baked yet -> the unready edge becomes a temporary content edge and the existing bounce applies.
            if (Content is IInsideViewport insideViewport)
                contentOffsetBounds = insideViewport.LimitScrollBounds(x, y, contentOffsetBounds, Orientation);

            if (!Bounces || strict)
            {
                var clampedX = Math.Max(contentOffsetBounds.Left, Math.Min(contentOffsetBounds.Right, x));
                var clampedY = Math.Max(contentOffsetBounds.Top, Math.Min(contentOffsetBounds.Bottom, y));

                //Debug.WriteLine($"Clamped {y} => {clampedY}");

                return new Vector2(clampedX, clampedY);
            }

            return ClampOffsetWithRubberBand(x, y, contentOffsetBounds);
        }

        public static readonly BindableProperty RespondsToGesturesProperty = BindableProperty.Create(
            nameof(RespondsToGestures),
            typeof(bool),
            typeof(SkiaScroll),
            true);

        /// <summary>
        /// If disabled will not scroll using gestures. Scrolling will still be possible by code.
        /// </summary>
        public bool RespondsToGestures
        {
            get { return (bool)GetValue(RespondsToGesturesProperty); }
            set { SetValue(RespondsToGesturesProperty, value); }
        }

        public static readonly BindableProperty CanScrollUsingHeaderProperty = BindableProperty.Create(
            nameof(CanScrollUsingHeader),
            typeof(bool),
            typeof(SkiaScroll),
            true);

        /// <summary>
        /// If disabled will not scroll using gestures. Scrolling will still be possible by code.
        /// </summary>
        public bool CanScrollUsingHeader
        {
            get { return (bool)GetValue(CanScrollUsingHeaderProperty); }
            set { SetValue(CanScrollUsingHeaderProperty, value); }
        }

        protected bool ContentGesturesHit;

        public override bool IsGestureForChild(ISkiaGestureListener listener, float x, float y)
        {
            if (ContentGesturesHit
                && HeaderBehind && listener == Header)
            {
                return false; //do not pass gestures to header
            }

            return base.IsGestureForChild(listener, x, y);
        }

        protected bool ChildWasPanning { get; set; }
        protected bool ChildWasTapped { get; set; }

        protected virtual bool IsContentActive
        {
            get { return Content != null && Content.IsVisible; }
        }

        protected VelocityAccumulator SwipeVelocityAccumulator { get; } = new();
        int lastNumberOfTouches;
        private bool lockHeader;
        public override bool UsesRenderingTree => false;
        protected SpringWithVelocityAnimator _vectorAnimatorBounceX;
        protected SpringWithVelocityAnimator _vectorAnimatorBounceY;

        /// <summary>
        /// Fling with deceleration
        /// </summary>
        protected ScrollFlingAnimator _animatorFlingX;

        /// <summary>
        /// Fling with deceleration
        /// </summary>
        protected ScrollFlingAnimator _animatorFlingY;

        /// <summary>
        /// Direct scroller for ordered scroll, snap etc
        /// </summary>
        protected RangeAnimator _scrollerX;

        /// <summary>
        /// Direct scroller for ordered scroll, snap etc
        /// </summary>
        protected RangeAnimator _scrollerY;

        /// <summary>
        /// Units
        /// </summary>
        protected float _scrollMinX;

        /// <summary>
        /// Units
        /// </summary>
        protected float _scrollMinY;

        protected float _scrollMaxX;
        protected float _scrollMaxY;

        public void StopScrolling()
        {
            if (_scrollerX != null && _scrollerX.IsRunning)
            {
                _scrollerX.Stop();
            }

            if (_scrollerY != null && _scrollerY.IsRunning)
            {
                _scrollerY.Stop();
            }

            if (_animatorFlingX != null && _animatorFlingX.IsRunning)
            {
                _animatorFlingX.Stop();
            }

            if (_animatorFlingY != null && _animatorFlingY.IsRunning)
            {
                _animatorFlingY.Stop();
            }

            if (_vectorAnimatorBounceX != null && _vectorAnimatorBounceX.IsRunning)
            {
                _vectorAnimatorBounceX.Stop();
            }

            if (_vectorAnimatorBounceY != null && _vectorAnimatorBounceY.IsRunning)
            {
                _vectorAnimatorBounceY.Stop();
            }

            IsSnapping = false;

            VelocityTrackerPan.Clear();
            VelocityTrackerScale.Clear();

            //ViewportOffsetY = InternalViewportOffset.Units.Y;
            //ViewportOffsetX = InternalViewportOffset.Units.X;
        }

        void UpdateLoadingLock(bool state)
        {
            SkiaImageManager.Instance.IsLoadingLocked = state;
        }

        void UpdateLoadingLock(Vector2 velocity)
        {
            bool shouldLock;

            switch (Orientation)
            {
                case ScrollOrientation.Vertical:
                    shouldLock = Math.Abs(velocity.Y) >= VelocityImageLoaderLock;
                    break;
                case ScrollOrientation.Horizontal:
                    shouldLock = Math.Abs(velocity.X) >= VelocityImageLoaderLock;
                    break;
                default:
                    shouldLock = Math.Abs(velocity.Y) >= VelocityImageLoaderLock ||
                                 Math.Abs(velocity.X) >= VelocityImageLoaderLock;
                    break;
            }

            UpdateLoadingLock(shouldLock);
        }

        protected float _minVelocitySnap = 15f;

        /// <summary>
        /// POINTS per sec
        /// </summary>
        protected float snapMinimumVelocity = 3f;

        //public virtual bool ScrollStoppedForSnap()
        //{
        //	//if (_velocityScrollerY.IsRunning)
        //	//{
        //	//    return _velocityScrollerY.mVelocity <= snapMinimumVelocity;
        //	//}

        //	if (_animatorFling.IsRunning)
        //	{
        //		return _animatorFling.CurrentVelocity.Y <= snapMinimumVelocity && _animatorFling.CurrentVelocity.X <= snapMinimumVelocity;
        //	}

        //	return !_animatorBounce.IsRunning && !_scrollerX.IsRunning && !_scrollerY.IsRunning;
        //}

        //protected bool CanSnap()
        //{
        //	return (!IsUserFocused
        //		&& SnapToChildren != SnapToChildrenType.Disabled
        //		&& Content is SkiaLayout layout
        //		&& ScrollStoppedForSnap());
        //}

        /// <summary>
        /// ToDo adapt this to same logic as ScrollLooped has !
        /// </summary>
        /// <param name="force"></param>
        //protected virtual void SnapIfNeeded(bool force = false)
        //{
        //	return; //todo

        //	if (force ||
        //		!IsUserFocused
        //		&& SnapToChildren != SnapToChildrenType.Disabled
        //		&& ScrollStoppedForSnap())
        //	{
        //		if (Content is SkiaLayout layout)
        //		{
        //			var hit = CurrentIndexHit;
        //			if (hit?.Index > -1 && layout.Views.Count > hit?.Index)
        //			{
        //				float needOffsetX = (float)Math.Truncate(InternalViewportOffset.Pixels.X);
        //				var initialOffset = needOffsetX;

        //				var calcOffset = NotValidPoint();
        //				if (SnapToChildren == SnapToChildrenType.Center)
        //				{
        //					calcOffset = CalculateScrollOffsetForIndex(hit.Index, RelativePositionType.Center);
        //				}

        //				if (SnapToChildren == SnapToChildrenType.Side)
        //				{
        //					if (TrackIndexPosition == RelativePositionType.Start)
        //					{
        //						calcOffset = CalculateScrollOffsetForIndex(hit.Index, RelativePositionType.Start);
        //					}
        //					else if (TrackIndexPosition == RelativePositionType.End)
        //					{
        //						calcOffset = CalculateScrollOffsetForIndex(hit.Index, RelativePositionType.End);
        //					}
        //				}

        //				if (PointIsValid(calcOffset))
        //				{
        //					if (initialOffset != calcOffset.X)
        //					{
        //						System.Diagnostics.Debug.WriteLine($"[SNAP] ------------ {CurrentIndex}");
        //						ScrollToX(calcOffset.X, true);
        //					}
        //				}

        //			}
        //		}
        //	}
        //}
        public static readonly BindableProperty BouncesProperty = BindableProperty.Create(nameof(Bounces),
            typeof(bool),
            typeof(SkiaScroll),
            true);

        /// <summary>
        /// Should the scroll bounce at edges. Set to false if you want this scroll to let the parent SkiaDrawer respond to scroll when the child scroll reached bounds.
        /// </summary>
        public bool Bounces
        {
            get { return (bool)GetValue(BouncesProperty); }
            set { SetValue(BouncesProperty, value); }
        }

        public static readonly BindableProperty RubberDampingProperty = BindableProperty.Create(
            nameof(RubberDamping),
            typeof(double),
            typeof(SkiaScroll),
            0.55);

        /// <summary>
        /// If Bounce is enabled this basically controls how less the scroll will bounce when displaced from limit by finger or inertia. Default is 0.55.
        /// </summary>
        public double RubberDamping
        {
            get { return (double)GetValue(RubberDampingProperty); }
            set { SetValue(RubberDampingProperty, value); }
        }

        public static readonly BindableProperty RubberEffectProperty = BindableProperty.Create(
            nameof(RubberEffect),
            typeof(double),
            typeof(SkiaScroll),
            0.55);

        /// <summary>
        /// If Bounce is enabled this basically controls how far from the limit can the scroll be elastically offset by finger or inertia. Default is 0.55.
        /// </summary>
        public double RubberEffect
        {
            get { return (double)GetValue(RubberEffectProperty); }
            set { SetValue(RubberEffectProperty, value); }
        }

        public float SnapBouncingIfVelocityLessThan
        {
            get { return (float)GetValue(SnapBouncingIfVelocityLessThanProperty); }
            set { SetValue(SnapBouncingIfVelocityLessThanProperty, value); }
        }

        public static readonly BindableProperty SnapBouncingIfVelocityLessThanProperty = BindableProperty.Create(
            nameof(SnapBouncingIfVelocityLessThan),
            typeof(float),
            typeof(SkiaScroll),
            750.0f);

        public static readonly BindableProperty AutoScrollingSpeedMsProperty = BindableProperty.Create(
            nameof(AutoScrollingSpeedMs),
            typeof(int),
            typeof(SkiaScroll),
            600);

        /// <summary>
        /// For snap and ordered scrolling
        /// </summary>
        public int AutoScrollingSpeedMs
        {
            get { return (int)GetValue(AutoScrollingSpeedMsProperty); }
            set { SetValue(AutoScrollingSpeedMsProperty, value); }
        }

        /// <summary>
        /// Use this to control how fast the scroll will decelerate.
        /// Values 0.1 - 0.9 are the best, default is 0.3.
        /// Usually you would set higher friction for ScrollView-like scrolls and much lower for CollectionView-like scrolls (0.1 or 0.2).
        /// For a picker: 05 - 0.9.
        /// </summary>
        public float FrictionScrolled
        {
            get { return (float)GetValue(FrictionScrolledProperty); }
            set { SetValue(FrictionScrolledProperty, value); }
        }

        public static readonly BindableProperty FrictionScrolledProperty = BindableProperty.Create(
            nameof(FrictionScrolled),
            typeof(float),
            typeof(SkiaScroll),
            .3f,
            propertyChanged: FrictionValueChanged);

        public static readonly BindableProperty IgnoreWrongDirectionProperty = BindableProperty.Create(
            nameof(IgnoreWrongDirection),
            typeof(bool),
            typeof(SkiaScroll),
            false);

        /// <summary>
        /// Will ignore gestures of the wrong direction, like if this Orientation is Horizontal will ignore gestures with vertical direction velocity. Default is False.
        /// </summary>
        public bool IgnoreWrongDirection
        {
            get { return (bool)GetValue(IgnoreWrongDirectionProperty); }
            set { SetValue(IgnoreWrongDirectionProperty, value); }
        }

        /*
        public static readonly BindableProperty IgnoreWrongDirectionLockProperty = BindableProperty.Create(
            nameof(IgnoreWrongDirectionLock),
            typeof(bool),
            typeof(SkiaScroll),
            false);

        /// <summary>
        /// In case if will ignore gestures of the wrong direction, should we lock this direction or multi-directional scrolling (True) is still allowed (False). Default is False.
        /// </summary>
        public bool IgnoreWrongDirectionLock
        {
            get { return (bool)GetValue(IgnoreWrongDirectionLockProperty); }
            set { SetValue(IgnoreWrongDirectionLockProperty, value); }
        }
        */

        public static readonly BindableProperty ResetScrollPositionOnContentSizeChangedProperty =
            BindableProperty.Create(
                nameof(ResetScrollPositionOnContentSizeChanged),
                typeof(bool),
                typeof(SkiaScroll),
                false);

        public bool ResetScrollPositionOnContentSizeChanged
        {
            get { return (bool)GetValue(ResetScrollPositionOnContentSizeChangedProperty); }
            set { SetValue(ResetScrollPositionOnContentSizeChangedProperty, value); }
        }

        /// <summary>
        /// For when the finger is up and swipe is detected
        /// </summary>
        public float ChangeVelocityScrolled
        {
            get { return (float)GetValue(ChangeVelocityScrolledProperty); }
            set { SetValue(ChangeVelocityScrolledProperty, value); }
        }

        public static readonly BindableProperty ChangeVelocityScrolledProperty = BindableProperty.Create(
            nameof(ChangeVelocityScrolled),
            typeof(float),
            typeof(SkiaScroll),
            1.33f);

        public static readonly BindableProperty MaxVelocityProperty = BindableProperty.Create(
            nameof(MaxVelocity),
            typeof(float),
            typeof(SkiaScroll),
            3000f);

        /// <summary>
        /// Limit user input velocity
        /// </summary>
        public float MaxVelocity
        {
            get { return (float)GetValue(MaxVelocityProperty); }
            set { SetValue(MaxVelocityProperty, value); }
        }

        public static readonly BindableProperty MaxBounceVelocityProperty = BindableProperty.Create(
            nameof(MaxBounceVelocity),
            typeof(float),
            typeof(SkiaScroll),
            500f);

        /// <summary>
        /// Limit bounce velocity
        /// </summary>
        public float MaxBounceVelocity
        {
            get { return (float)GetValue(MaxBounceVelocityProperty); }
            set { SetValue(MaxBounceVelocityProperty, value); }
        }

        /// <summary>
        /// For when the finger is down and panning
        /// </summary>
        public float ChangeDistancePanned
        {
            get { return (float)GetValue(ChangeDistancePannedProperty); }
            set { SetValue(ChangeDistancePannedProperty, value); }
        }

        public static readonly BindableProperty ChangeDistancePannedProperty = BindableProperty.Create(
            nameof(ChangeDistancePanned),
            typeof(float),
            typeof(SkiaScroll),
            1.0f);

        private static void FrictionValueChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control.UpdateFriction();
            }
        }

        int _currentIndex = -1;

        public int CurrentIndex
        {
            get { return _currentIndex; }
            protected set
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;
                    OnPropertyChanged();
                    IndexChanged?.Invoke(this, value);
                    //Debug.WriteLine($"Scroll {Tag} CurrentIndex {value}");
                }
            }
        }

        public int FirstVisibleIndex
        {
            get => firstVisibleIndex;
            set
            {
                if (value == firstVisibleIndex)
                {
                    return;
                }

                firstVisibleIndex = value;
                OnPropertyChanged();
            }
        }

        public int LastVisibleIndex
        {
            get => lastVisibleIndex;
            set
            {
                if (value == lastVisibleIndex)
                {
                    return;
                }

                lastVisibleIndex = value;
                OnPropertyChanged();
            }
        }

        public event EventHandler<int> IndexChanged;

        public ContainsPointResult CurrentIndexHit
        {
            get { return _CurrentIndexHit; }
            set
            {
                if (value != _CurrentIndexHit)
                {
                    _CurrentIndexHit = value;
                    OnPropertyChanged();
                }
            }
        }

        private ContainsPointResult _CurrentIndexHit;

        void WatchState()
        {
        }

        protected SKPoint DetectIndexChildStartAt;
        protected SKPoint DetectIndexChildAt;
        protected SKPoint DetectIndexChildEndAt;

        protected virtual void SetDetectIndexChildPoint(RelativePositionType option = RelativePositionType.Start)
        {
            //todo this will need to change for multiple columns?

            if (!IsContentActive || Content.MeasuredSize == null || TrackIndexPosition == RelativePositionType.None)
                return;

            var point = new SKPoint();
            var pointStart = new SKPoint();
            var pointEnd = new SKPoint();

            if (this.Orientation == ScrollOrientation.Vertical)
            {
                var endY = this.Viewport.Pixels.Height;
                //if (this.Content.MeasuredSize.Pixels.Height < endY)
                //    endY = this.Content.MeasuredSize.Pixels.Height;

                pointEnd.Y = endY;

                if (option == RelativePositionType.End)
                {
                    point.Y += (endY - TrackIndexPositionOffset);
                }
                else if (option == RelativePositionType.Center)
                {
                    point.Y += endY / 2f;
                }

                point.X = this.Viewport.Pixels.MidX;
                pointStart.X = point.X;
                pointEnd.X = point.X;
            }
            else if (this.Orientation == ScrollOrientation.Horizontal)
            {
                var endX = this.Viewport.Pixels.Width;
                //if (this.Content.MeasuredSize.Pixels.Width < endX)
                //    endX = this.Content.MeasuredSize.Pixels.Width;

                pointEnd.X = endX;

                if (option == RelativePositionType.End)
                {
                    point.X += endX - TrackIndexPositionOffset;
                }
                else if (option == RelativePositionType.Center)
                {
                    point.X += endX / 2f;
                }

                point.Y = this.Viewport.Pixels.MidY;
                pointStart.Y = point.Y;
                pointEnd.Y = point.Y;
            }

            //Debug.WriteLine($"[POINT] V {Viewport.Pixels.Bottom} P {point.Y}");

            DetectIndexChildStartAt = pointStart;
            DetectIndexChildEndAt = pointEnd;
            DetectIndexChildAt = point;
        }

        /// <summary>
        /// Calculates CurrentIndex
        /// </summary>
        public virtual ContainsPointResult CalculateVisibleIndex(RelativePositionType option)
        {
            if (Content is SkiaLayout layout)
            {
                var pixelsOffsetX =
                    InternalViewportOffset.Pixels.X; // (float)(ViewportOffsetX * layout.RenderingScale);
                var pixelsOffsetY =
                    InternalViewportOffset.Pixels.Y; // (float)(ViewportOffsetY * layout.RenderingScale);

                return GetItemIndex(layout, pixelsOffsetX, pixelsOffsetY, option);
            }
            else if (Content is ILayoutInsideViewport inside)
            {
                var point = new SKPoint(
                    DetectIndexChildAt.X + InternalViewportOffset.Pixels.X + DrawingRect.Left,
                    DetectIndexChildAt.Y + InternalViewportOffset.Pixels.Y + DrawingRect.Top);

                var found = inside.GetVisibleChildIndexAt(point);

                if (found.Index != -1)
                {
                    //todo translate found
                    var area = found.Area;
                    area.Offset(-DrawingRect.Left, -DrawingRect.Top);
                    point.Offset(-DrawingRect.Left, -DrawingRect.Top);
                    return new ContainsPointResult()
                    {
                        Index = found.Index,
                        Area = area,
                        Point = point,
                        Unmodified = new(InternalViewportOffset.Pixels.X, InternalViewportOffset.Pixels.Y)
                    };
                }

                return found;
            }

            return ContainsPointResult.NotFound();
        }

        /// <summary>
        /// Will calculate first/last visible and current index, if tracked with TrackIndexPosition.
        /// </summary>
        /// <param name="option"></param>
        public virtual void CalculateVisibleIndexes()
        {
            if (Content is SkiaLayout layout)
            {
                var pixelsOffsetX =
                    InternalViewportOffset.Pixels.X;
                var pixelsOffsetY =
                    InternalViewportOffset.Pixels.Y;

                var points = GetVisibleIndexes(layout, pixelsOffsetX, pixelsOffsetY);

                CurrentIndexHit = points.Current;
                CurrentIndex = points.Current.Index;
                FirstVisibleIndex = points.Start.Index;
                LastVisibleIndex = points.End.Index;
            }
            else if (Content is ILayoutInsideViewport inside)
            {
                var point = new SKPoint(
                    DetectIndexChildAt.X + InternalViewportOffset.Pixels.X + DrawingRect.Left,
                    DetectIndexChildAt.Y + InternalViewportOffset.Pixels.Y + DrawingRect.Top);

                var current = inside.GetVisibleChildIndexAt(point);

                if (current.Index != -1)
                {
                    //todo translate found
                    var area = current.Area;
                    area.Offset(-DrawingRect.Left, -DrawingRect.Top);
                    point.Offset(-DrawingRect.Left, -DrawingRect.Top);
                    var currentPoint = new ContainsPointResult()
                    {
                        Index = current.Index,
                        Area = area,
                        Point = point,
                        Unmodified = new(InternalViewportOffset.Pixels.X, InternalViewportOffset.Pixels.Y)
                    };
                    CurrentIndexHit = currentPoint;
                    CurrentIndex = currentPoint.Index;
                }
            }
        }

        public virtual (ContainsPointResult Current, ContainsPointResult Start, ContainsPointResult End)
            GetVisibleIndexes(SkiaLayout layout, float pixelsOffsetX, float pixelsOffsetY)
        {
            ContainsPointResult currentResult = ContainsPointResult.NotFound();
            ContainsPointResult startResult = ContainsPointResult.NotFound();
            ContainsPointResult endResult = ContainsPointResult.NotFound();

            if (layout.GetStackStructure() != null)
            {
                bool trace = false;


                var current = this.DetectIndexChildAt;
                var end = this.DetectIndexChildEndAt;
                var start = this.DetectIndexChildStartAt;

                var offset = new SKPoint(Math.Abs(pixelsOffsetX), Math.Abs(pixelsOffsetY));
                current.Offset(offset);
                start.Offset(offset);
                end.Offset(offset);

                if (this.Orientation == ScrollOrientation.Vertical || this.Orientation == ScrollOrientation.Horizontal)
                {
                    if (layout.Type == LayoutType.Column || layout.Type == LayoutType.Row ||
                        layout.Type == LayoutType.Wrap && layout.Split > 0) //todo grid
                    {
                        var stackStructure = layout.GetStackStructure();
                        int index = -1;

                        foreach (var childInfo in stackStructure.GetChildren())
                        {
                            index++;
                            if (childInfo.Destination.ContainsInclusive(current))
                            {
                                currentResult = new ContainsPointResult()
                                {
                                    Index = index,
                                    Area = childInfo.Destination,
                                    Point = current,
                                    Unmodified = new SKPoint(0, pixelsOffsetY)
                                };
                            }

                            if (childInfo.Destination.ContainsInclusive(start))
                            {
                                startResult = new ContainsPointResult()
                                {
                                    Index = index,
                                    Area = childInfo.Destination,
                                    Point = start,
                                    Unmodified = new SKPoint(0, pixelsOffsetY)
                                };
                            }

                            if (childInfo.Destination.ContainsInclusive(end))
                            {
                                endResult = new ContainsPointResult()
                                {
                                    Index = index,
                                    Area = childInfo.Destination,
                                    Point = end,
                                    Unmodified = new SKPoint(0, pixelsOffsetY)
                                };
                            }
                        }
                    }
                }
            }

            return (currentResult, startResult, endResult);
        }

        public virtual ContainsPointResult GetItemIndex(SkiaLayout layout, float pixelsOffsetX, float pixelsOffsetY,
            RelativePositionType option)
        {
            if (layout.GetStackStructure() == null)
                return ContainsPointResult.NotFound();

            bool trace = false;

            if (this.Orientation == ScrollOrientation.Vertical)
            {
                var initialValue = pixelsOffsetY;

                // ----------- proper to infinite start 

                if (option == RelativePositionType.Center)
                {
                    pixelsOffsetY -= Viewport.Pixels.Height / 2f;
                }
                else if (option == RelativePositionType.End)
                {
                    pixelsOffsetY -= Viewport.Pixels.Height;
                }

                if (pixelsOffsetY > 0)
                {
                    //inverted scroll
                    pixelsOffsetY -= Content.MeasuredSize.Pixels.Height;
                }
                else
                {
                    //normal scroll
                    if (-pixelsOffsetY > Content.MeasuredSize.Pixels.Height)
                    {
                        pixelsOffsetY += Content.MeasuredSize.Pixels.Height;
                    }
                }

                // ----------- proper to infinite end

                var point = new SKPoint(
                    (float)Math.Abs(pixelsOffsetX),
                    (float)Math.Abs(pixelsOffsetY)
                );

                if (layout.Type == LayoutType.Column || layout.Type == LayoutType.Wrap && layout.Split > 0) //todo grid
                {
                    var stackStructure = layout.GetStackStructure();
                    int index = -1;
                    int row;
                    int col;

                    if (trace)
                        Trace.WriteLine($"offset: {point.Y}");

                    foreach (var childInfo in stackStructure.GetChildren())
                    {
                        index++;
                        if (childInfo.Destination.ContainsInclusive(point))
                        {
                            return new ContainsPointResult()
                            {
                                Index = index,
                                Area = childInfo.Destination,
                                Point = point,
                                Unmodified = new SKPoint(0, initialValue)
                            };
                        }
                    }
                }
            }
            else if (this.Orientation == ScrollOrientation.Horizontal)
            {
                var initialValue = pixelsOffsetX;

                // ----------- proper to infinite start 

                if (option == RelativePositionType.Center)
                {
                    pixelsOffsetX -= Viewport.Pixels.Width / 2f;
                }
                else if (option == RelativePositionType.End)
                {
                    pixelsOffsetX -= Viewport.Pixels.Width;
                }

                if (pixelsOffsetX > 0)
                {
                    //inverted scroll
                    //var bak = pixelsOffsetX;
                    pixelsOffsetX -= Content.MeasuredSize.Pixels.Width;
                    //Trace.WriteLine($"[INVERT ] {bak:0.0} --> {pixelsOffsetX:0.0}");
                }
                else
                {
                    //normal scroll
                    if (-pixelsOffsetX > Content.MeasuredSize.Pixels.Width)
                    {
                        pixelsOffsetX += Content.MeasuredSize.Pixels.Width;
                    }
                }

                //Trace.WriteLine($"[CALC] for {pixelsOffsetX:0.0}");
                // ----------- proper to infinite end


                var point = new SKPoint(
                    (float)Math.Abs(pixelsOffsetX),
                    (float)Math.Abs(pixelsOffsetY)
                );


                if (layout.Type == LayoutType.Row || layout.Type == LayoutType.Wrap && layout.Split == 0) //todo grid
                {
                    var stackStructure = layout.GetStackStructure();
                    int index = -1;
                    int row;
                    int col;

                    foreach (var childInfo in stackStructure.GetChildren())
                    {
                        index++;
                        var childRect = childInfo.Destination.Clone();
                        //childRect.Offset(point.X, point.Y);

                        if (childRect.ContainsInclusive(point))
                        {
                            return new ContainsPointResult()
                            {
                                Index = index,
                                Area = childRect,
                                Point = point,
                                Unmodified = new SKPoint(initialValue, 0)
                            };
                        }
                    }
                }
            }

            return ContainsPointResult.NotFound();
        }


        protected virtual SKPoint ClampedOrderedScrollOffset(SKPoint scrollTo)
        {
            if (Orientation == ScrollOrientation.Vertical)
            {
                var scrollSpaceY = ptsContentHeight - Viewport.Units.Height;
                var offsetViewportY = Math.Abs(scrollTo.Y) - Viewport.Units.Height;
                if (scrollSpaceY < 0 || offsetViewportY < 0)
                {
                    return NotValidPoint();
                }
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                var scrollSpaceX = ptsContentWidth - Viewport.Units.Width;
                var offsetViewportX = Math.Abs(scrollTo.X) - Viewport.Units.Width;
                if (scrollSpaceX < 0 || offsetViewportX < 0)
                {
                    return NotValidPoint();
                }
            }
            else if (Orientation == ScrollOrientation.Both)
            {
                var scrollSpaceY = ptsContentHeight - Viewport.Units.Height;
                var offsetViewportY = Math.Abs(scrollTo.Y) - Viewport.Units.Height;
                var scrollSpaceX = ptsContentWidth - Viewport.Units.Width;
                var offsetViewportX = Math.Abs(scrollTo.X) - Viewport.Units.Width;
                if (scrollSpaceY < 0 || offsetViewportY < 0 || scrollSpaceX < 0 || offsetViewportX < 0)
                {
                    return NotValidPoint();
                }
            }

            return scrollTo;
        }

        /// <summary>
        /// ToDo this actually work only for Stack and Row
        /// </summary>
        /// <param name="index"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public virtual SKPoint CalculateScrollOffsetForIndex(int index, RelativePositionType option)
        {
            //Debug.WriteLine($"CalculateScrollOffsetForIndex ? {index}");

            if (Content is SkiaLayout layout)
            {
                var childrenCount = layout.ChildrenFactory.GetChildrenCount();
                if (
                    ptsContentHeight <= 0 || ptsContentWidth <= 0 ||
                    childrenCount == 0 || index < 0 || index >= childrenCount)
                {
                    return NotValidPoint(); //can throw too
                }

                var structure = layout.GetStackStructure();
                if (structure != null && structure.GetCount() > 0) // && layout.StackStructure.Count == childrenCount)
                {
                    float offset = 0;

                    //in case index falls out of array bounds due to multiple threads..
                    try
                    {
                        ControlInStack childInfo = null;

                        bool isValid = false;
                        if (Orientation == ScrollOrientation.Horizontal)
                        {
                            if (index < structure.MaxColumns)
                            {
                                isValid = true;
                                childInfo = structure.Get(index, 0);
                            }
                        }
                        else
                        {
                            if (index < structure.MaxRows)
                            {
                                isValid = true;
                                childInfo = structure.Get(0, index);
                            }
                        }

                        if (isValid && childInfo.Measured != null)
                        {
                            if (Orientation == ScrollOrientation.Horizontal)
                            {
                                //todo rework
                                var childOffset = childInfo.Destination.Left / (float)layout.RenderingScale;

                                if (option == RelativePositionType.End)
                                {
                                    offset = childOffset - (this.Viewport.Units.Width - childInfo.Measured.Units.Width);
                                }
                                else if (option == RelativePositionType.Center)
                                {
                                    offset = childOffset -
                                             (this.Viewport.Units.Width - childInfo.Measured.Units.Width) / 2f;
                                }
                                else
                                {
                                    offset = childOffset;
                                }

                                return ClampedOrderedScrollOffset(new SKPoint(-offset, 0));
                            }
                            else if (Orientation == ScrollOrientation.Vertical)
                            {
                                var scrollSpaceY = ptsContentHeight - Viewport.Units.Height;

                                if (scrollSpaceY > 0)
                                {
                                    //todo rework
                                    var childOffset = childInfo.Destination.Top / (float)layout.RenderingScale;

                                    if (option == RelativePositionType.End)
                                    {
                                        offset = childOffset -
                                                 (this.Viewport.Units.Height - childInfo.Measured.Units.Height);
                                    }
                                    else if (option == RelativePositionType.Center)
                                    {
                                        offset = childOffset -
                                                 (this.Viewport.Units.Height - childInfo.Measured.Units.Height) / 2f;
                                    }
                                    else
                                    {
                                        offset = childOffset;
                                    }

                                    //Debug.WriteLine($"CalculateScrollOffsetForIndex OK {index} {offset:0.0}");

                                    return new SKPoint(0, -offset);
                                }

                                //return ClampedOrderedScrollOffset(new SKPoint(0, -offset));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(e);
                    }
                }
            }

            return NotValidPoint();
        }

        protected virtual bool CheckNeedToSnap()
        {
            bool ret = !(IsSnapping || Snapped
                                    || IsUserFocused
                                    || OrderedScrollTo.IsValid //already scrolling somewhere
                                    || this.SnapToChildren == SnapToChildrenType.Disabled
                                    || _vectorAnimatorBounceY.IsRunning || _vectorAnimatorBounceX.IsRunning
                                    || _animatorFlingX.IsRunning &&
                                    (Math.Abs(_animatorFlingX.CurrentVelocity) > _minVelocitySnap
                                     || _animatorFlingY.IsRunning &&
                                     (Math.Abs(_animatorFlingY.CurrentVelocity) > _minVelocitySnap
                                      || Math.Abs(_animatorFlingY.CurrentVelocity) > _minVelocitySnap)
                                     || Math.Abs(_animatorFlingX.CurrentVelocity) > _minVelocitySnap)
                );

            //Trace.WriteLine($"CheckNeedToSnap {ret}");

            return ret;
        }

        public virtual void Snap(float maxTimeSecs)
        {
            if (OrderedScrollTo.IsValid || IsSnapping)
            {
                return;
            }

            IsSnapping = true;

            if (Content is SkiaLayout layout)
            {
                var hit = CurrentIndexHit;
                if (hit?.Index > -1 && layout.ChildrenFactory.GetChildrenCount() > hit?.Index)
                {
                    //if (hit.Unmodified == SKPoint.Empty)
                    //{
                    //	_isSnapping = false;
                    //	return;
                    //}

                    var needMove = 0f;
                    if (Orientation == ScrollOrientation.Vertical)
                    {
                        //float needOffsetY = (float)Math.Truncate(ViewportOffsetY);
                        float needOffsetY = (float)Math.Truncate(InternalViewportOffset.Pixels.Y);
                        var initialOffset = needOffsetY;
                        if (SnapToChildren == SnapToChildrenType.Center)
                        {
                            var center = hit.Area.Height / 2f;
                            var pointY = hit.Area.Bottom - hit.Point.Y;
                            needMove = -(pointY - center);
                        }
                        else if (SnapToChildren == SnapToChildrenType.Side)
                        {
                            if (TrackIndexPosition == RelativePositionType.Start)
                            {
                                needMove = hit.Point.Y - hit.Area.Bottom;
                            }
                            else if (TrackIndexPosition == RelativePositionType.End)
                            {
                                needMove = -(hit.Area.Bottom - hit.Point.Y);
                            }
                        }

                        var threshold = RenderingScale * 2;

                        needOffsetY = hit.Unmodified.Y + needMove;
                        if (needMove != 0f && Math.Abs(initialOffset - needOffsetY) > threshold)
                        {
                            //Snapped = true;
                            //ScrollTo(InternalViewportOffset.Units.X, needOffsetY / layout.RenderingScale, maxTimeSecs);

                            Snapped = true;

                            _animatorFlingX.Stop();
                            _animatorFlingY.Stop();

                            ScrollTo(ViewportOffsetX, ViewportOffsetY + needOffsetY / layout.RenderingScale,
                                AutoScrollingSpeedMs, true);

                            return;
                        }

                        //Trace.WriteLine($"Snap low threshold");
                    }
                    else if (Orientation == ScrollOrientation.Horizontal)
                    {
                        float needOffsetX = (float)Math.Truncate(InternalViewportOffset.Units.X);
                        var initialOffset = needOffsetX;
                        if (SnapToChildren == SnapToChildrenType.Center)
                        {
                            var center = hit.Area.Width / 2f;
                            var pointX = hit.Area.Right - hit.Point.X;
                            needMove = -(pointX - center);
                        }
                        else if (SnapToChildren == SnapToChildrenType.Side)
                        {
                            if (TrackIndexPosition == RelativePositionType.Start)
                            {
                                needMove = hit.Area.Width - (hit.Area.Right - hit.Point.X);
                                //needOffsetX += needMove;
                            }
                            else if (TrackIndexPosition == RelativePositionType.End)
                            {
                                needMove = -(hit.Area.Right - hit.Point.X);
                                //needOffsetX += needMove;
                            }
                        }

                        needOffsetX = hit.Unmodified.X + needMove;
                        if (needMove != 0f && initialOffset != needOffsetX)
                        {
                            Snapped = true;

                            _animatorFlingX.Stop();
                            _animatorFlingY.Stop();

                            ScrollTo(ViewportOffsetX + needOffsetX / layout.RenderingScale, ViewportOffsetY,
                                AutoScrollingSpeedMs, true);

                            return;
                        }
                    }
                }
            }

            IsSnapping = false;
        }

        public static readonly BindableProperty SnapToChildrenProperty
            = BindableProperty.Create(nameof(SnapToChildren),
                typeof(SnapToChildrenType), typeof(SkiaScroll),
                SnapToChildrenType.Disabled, propertyChanged: NeedDraw);

        /// <summary>
        /// Whether should snap to children after scrolling stopped
        /// </summary>
        public SnapToChildrenType SnapToChildren
        {
            get { return (SnapToChildrenType)GetValue(SnapToChildrenProperty); }
            set { SetValue(SnapToChildrenProperty, value); }
        }

        public static readonly BindableProperty TrackIndexPositionProperty
            = BindableProperty.Create(nameof(TrackIndexPosition),
                typeof(RelativePositionType), typeof(SkiaScroll),
                RelativePositionType.None, propertyChanged: OnTrackingChanged);

        private static void OnTrackingChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control.SetDetectIndexChildPoint(control.TrackIndexPosition);
                NeedDraw(bindable, oldvalue, newvalue);
            }
        }

        /// <summary>
        /// The position in viewport you want to track for content layout child index
        /// </summary>
        public RelativePositionType TrackIndexPosition
        {
            get { return (RelativePositionType)GetValue(TrackIndexPositionProperty); }
            set { SetValue(TrackIndexPositionProperty, value); }
        }

        public static readonly BindableProperty TrackIndexPositionOffsetProperty = BindableProperty.Create(
            nameof(TrackIndexPositionOffset),
            typeof(float),
            typeof(SkiaScroll),
            8.0f, propertyChanged: OnTrackingChanged);

        public float TrackIndexPositionOffset
        {
            get { return (float)GetValue(TrackIndexPositionOffsetProperty); }
            set { SetValue(TrackIndexPositionOffsetProperty, value); }
        }

        public static readonly BindableProperty LoadMoreCommandProperty = BindableProperty.Create(
            nameof(LoadMoreCommand),
            typeof(ICommand),
            typeof(SkiaScroll),
            null);

        /// <summary>
        /// Command executed when scrolling near the bottom edge (within LoadMoreOffset distance).
        /// Used for pagination or infinite scroll loading.
        /// </summary>
        public ICommand LoadMoreCommand
        {
            get { return (ICommand)GetValue(LoadMoreCommandProperty); }
            set { SetValue(LoadMoreCommandProperty, value); }
        }

        public static readonly BindableProperty LoadMoreTopCommandProperty = BindableProperty.Create(
            nameof(LoadMoreTopCommand),
            typeof(ICommand),
            typeof(SkiaScroll),
            null);

        /// <summary>
        /// Command executed when scrolling near the top edge (within LoadMoreTopOffset distance).
        /// Used for bidirectional pagination in inverted or infinite scroll lists.
        /// </summary>
        public ICommand LoadMoreTopCommand
        {
            get { return (ICommand)GetValue(LoadMoreTopCommandProperty); }
            set { SetValue(LoadMoreTopCommandProperty, value); }
        }

        public static readonly BindableProperty LoadMoreOffsetProperty = BindableProperty.Create(nameof(LoadMoreOffset),
            typeof(float),
            typeof(SkiaScroll),
            0.0f, propertyChanged: OnTrackingChanged);

        /// <summary>
        /// Distance in points from the bottom edge where LoadMoreCommand is triggered. Default is 0.0f.
        /// Set to positive value (e.g., 100) to trigger loading before reaching absolute bottom.
        /// </summary>
        public float LoadMoreOffset
        {
            get { return (float)GetValue(LoadMoreOffsetProperty); }
            set { SetValue(LoadMoreOffsetProperty, value); }
        }

        public static readonly BindableProperty LoadMoreTopOffsetProperty = BindableProperty.Create(
            nameof(LoadMoreTopOffset),
            typeof(float),
            typeof(SkiaScroll),
            0.0f,
            propertyChanged: OnTrackingChanged);

        /// <summary>
        /// Distance in points from the top edge where LoadMoreTopCommand is triggered. Default is 0.0f.
        /// Set to positive value (e.g., 100) for bidirectional loading in inverted or chat-like lists.
        /// </summary>
        public float LoadMoreTopOffset
        {
            get { return (float)GetValue(LoadMoreTopOffsetProperty); }
            set { SetValue(LoadMoreTopOffsetProperty, value); }
        }

        private const double LoadMoreOppositeDirectionCooldownSeconds = 0.35;

        private const double LoadMoreOppositeDirectionMaxBlockSeconds = 1.5;

        private bool IsOppositeLoadMoreBlocked(LoadMoreDirection direction, float scale)
        {
            // Filling phase: content doesn't fill the viewport yet, both directions may be needed
            // to fill it and there is no scrolling to rebound — ping-pong protection doesn't apply.
            // Each fire either grows content (progress) or no-ops into the trigger latch.
            if (ContentUnderfillsViewport)
                return false;

            if (_lastLoadMoreDirection == null)
                return false;

            if (_lastLoadMoreDirection == direction)
                return false;

            var sinceLastTrigger = (DateTime.Now - _lastLoadMoreDirectionTime).TotalSeconds;

            if (sinceLastTrigger < LoadMoreOppositeDirectionCooldownSeconds)
            {
                return true;
            }

            // The travel/zone rules below only protect against the immediate rebound after a
            // trigger (anchor correction ping-pong). Applied forever they would freeze the
            // opposite direction — e.g. a no-op top trigger at startup permanently blocking
            // bottom loads. Time-bound them.
            if (sinceLastTrigger > LoadMoreOppositeDirectionMaxBlockSeconds)
            {
                return false;
            }

            // Require real movement away from the previous trigger before allowing
            // the opposite command. This blocks one-frame rebound loops after rebase.
            double currentOffset = Orientation == ScrollOrientation.Vertical
                ? InternalViewportOffset.Units.Y
                : InternalViewportOffset.Units.X;
            var minTravel = (LoadMoreOffset + LoadMoreTopOffset + 20f) * scale;
            if (Math.Abs(currentOffset - _lastLoadMoreDirectionOffset) < minTravel)
            {
                return true;
            }

            // Keep opposite direction blocked while still sitting inside the previously
            // triggered edge zone (anchor correction can otherwise cause ping-pong loops).
            if (Orientation == ScrollOrientation.Vertical)
            {
                if (_lastLoadMoreDirection == LoadMoreDirection.Bottom && direction == LoadMoreDirection.Top)
                {
                    return InternalViewportOffset.Units.Y <= _scrollMinY + (LoadMoreOffset * scale);
                }

                if (_lastLoadMoreDirection == LoadMoreDirection.Top && direction == LoadMoreDirection.Bottom)
                {
                    return InternalViewportOffset.Units.Y >= _scrollMaxY - (LoadMoreTopOffset * scale);
                }
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                if (_lastLoadMoreDirection == LoadMoreDirection.Bottom && direction == LoadMoreDirection.Top)
                {
                    return InternalViewportOffset.Units.X <= _scrollMinX + (LoadMoreOffset * scale);
                }

                if (_lastLoadMoreDirection == LoadMoreDirection.Top && direction == LoadMoreDirection.Bottom)
                {
                    return InternalViewportOffset.Units.X >= _scrollMaxX - (LoadMoreTopOffset * scale);
                }
            }

            return false;
        }

        private void MarkLoadMoreDirection(LoadMoreDirection direction)
        {
            _lastLoadMoreDirection = direction;
            _lastLoadMoreDirectionTime = DateTime.Now;
            _lastLoadMoreDirectionOffset = Orientation == ScrollOrientation.Vertical
                ? InternalViewportOffset.Units.Y
                : InternalViewportOffset.Units.X;
        }

        #endregion

        protected SKSize LastContentSizePixels = new SKSize(-1, -1);
        protected SKSize LastMeasuredSizePixels = new SKSize(-1, -1);

        protected virtual void ApplyContentSize(bool force = false)
        {
            if (force ||
                !CompareSize(ContentSize.Pixels, LastContentSizePixels, 1f) ||
                !CompareSize(MeasuredSize.Pixels, LastMeasuredSizePixels, 1f))
            {
                LastContentSizePixels = ContentSize.Pixels;
                LastMeasuredSizePixels = MeasuredSize.Pixels;

                InitializeViewport((float)RenderingScale);

                InitializeScroller((float)RenderingScale);
            }
        }

        protected override void OnMeasured()
        {
            base.OnMeasured();

            ApplyContentSize();
        }

        private PointF lastVelocity;
        private double prevV;
        private long c1;

        protected virtual ISkiaGestureListener PassGestureToChildren(SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            if (IsContentActive)
            {
                return Content.OnSkiaGestureEvent(args, apply);
            }

            return null;
        }

        public float VelocityY
        {
            get { return _velocityY; }
            set
            {
                if (Math.Abs(value) > MaxVelocity)
                {
                    value = MaxVelocity * Math.Sign(value);
                }

                if (_velocityY != value)
                {
                    _velocityY = value;
                    OnPropertyChanged();
                }
            }
        }

        float _velocityY;

        public float VelocityX
        {
            get { return _velocityX; }
            set
            {
                if (Math.Abs(value) > MaxVelocity)
                {
                    value = MaxVelocity * Math.Sign(value);
                }

                if (_velocityX != value)
                {
                    _velocityX = value;
                    OnPropertyChanged();
                }
            }
        }

        float _velocityX;
        private DateTime lastInputTime;

        bool SameSign(double a, double b)
        {
            return Math.Sign(a) == Math.Sign(b);
        }

        public bool SetZoom(double zoom)
        {
            if (ZoomLocked)
                return false;

            //Debug.WriteLine($"[ZOOM] {zoom:0.000}");

            if (zoom < ZoomMin)
                zoom = ZoomMin;
            else if (zoom > ZoomMax)
                zoom = ZoomMax;

            ZoomScaleInternal = zoom;

            ViewportZoom = zoom;
            return true;
        }

        /*
        public bool SetZoom(double zoom)
        {
            if (ZoomLocked)
                return false;

            Debug.WriteLine($"[ZOOM] {zoom:0.000}");

            if (zoom < ZoomMin)
                zoom = ZoomMin;
            else if (zoom > ZoomMax)
                zoom = ZoomMax;

            // Calculate viewport center in screen coordinates
            var viewportCenterScreen = new SKPoint((float)(Width / 2), (float)(Height / 2));

            // Current content scale
            var scale = RenderingScale; // Assuming RenderingScale is your base scale factor
            var currentContentScale = (float)(scale * ViewportZoom);

            // Current content offset in pixels
            var contentOffsetPixels = new SKPoint(
                ViewportOffsetX * currentContentScale,
                ViewportOffsetY * currentContentScale);

            // Content coordinates of the center before zooming
            var contentCenterBeforeZoom = new SKPoint(
                (viewportCenterScreen.X - contentOffsetPixels.X) / currentContentScale,
                (viewportCenterScreen.Y - contentOffsetPixels.Y) / currentContentScale);

            // Update the zoom level
            ZoomScaleInternal = zoom;
            ViewportZoom = zoom;

            // New content scale
            var newContentScale = (float)(scale * ViewportZoom);

            // Adjust offsets to keep the content centered
            ViewportOffsetX = ((viewportCenterScreen.X - (contentCenterBeforeZoom.X * newContentScale)) / newContentScale);
            ViewportOffsetY = ((viewportCenterScreen.Y - (contentCenterBeforeZoom.Y * newContentScale)) / newContentScale);

            return true;
        }
        */

        /// <summary>
        /// We might have difference between pinch scale and manually set zoom. 
        /// </summary>
        protected double ZoomScaleInternal { get; set; }

        protected ScaledSize HeaderSize = new();
        protected ScaledSize FooterSize = new();

        /// <summary>
        /// Calculate the value that will be set to ContentSize after that
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual ScaledSize MeasureContent(float width, float height, float scale)
        {
            return Content.Measure(width, height, scale);
        }

        protected override ScaledSize MeasureInternal(MeasureRequest request)
        {
            //if (UsePlanes)
            //{
            //    SetContentVisibleDelegate();
            //}

            var constraints = GetMeasuringConstraints(request);
            var viewport = GetContentAvailableRect(constraints.Content);

            Viewport = ScaledRect.FromPixels(constraints.Content, request.Scale);

            if (Content != null && Content.IsVisible)
            {
                var zoomedScale = (float)(request.Scale * ViewportZoom);

                var measuredContent = MeasureContent(viewport.Width, viewport.Height, zoomedScale);

                ContentSize = ScaledSize.FromPixels(measuredContent.Pixels.Width, measuredContent.Pixels.Height,
                    request.Scale);
            }
            else
            {
                ContentSize = ScaledSize.Default;
            }

            if (Header != null)
                HeaderSize = Header.Measure(request.WidthRequest, request.HeightRequest, request.Scale);
            else
                HeaderSize = ScaledSize.Default;

            if (Footer != null)
                FooterSize = Footer.Measure(request.WidthRequest, request.HeightRequest, request.Scale);
            else
                FooterSize = ScaledSize.Default;

            return SetMeasuredAdaptToContentSize(constraints, request.Scale);
        }

        /*
        public override ScaledSize Measure(float widthConstraint, float heightConstraint, float scale)
        {

            if (IsMeasuring || !CanDraw || (widthConstraint < 0 || heightConstraint < 0))
            {
                return MeasuredSize;
            }

            try
            {

                //measureWatch.Restart();

                IsMeasuring = true;

                var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);
                if (request.IsSame)
                {
                    return MeasuredSize;
                }

                if (!DefaultChildrenCreated)
                {
                    DefaultChildrenCreated = true;
                    CreateDefaultContent();
                }

                return MeasureInternal(request);


            }
            finally
            {
                IsMeasuring = false;
                //measureWatch.Stop();

            }

        }

        */
        public ScaledRect Viewport
        {
            get;
            protected set;
        } = new();

        protected override ScaledSize SetMeasured(float width, float height, bool widthCut, bool heightCut, float scale)
        {
            if (Content != null)
            {
                _lastContentSize = this.Content.MeasuredSize;
            }
            else
                _lastContentSize = ScaledSize.Default;

            return base.SetMeasured(width, height, widthCut, heightCut, scale);
        }

        /// <summary>
        /// In PIXELS
        /// </summary>
        /// <param name="destination"></param>
        /// <returns></returns>
        protected virtual SKRect GetContentAvailableRect(SKRect destination)
        {
            var childRect = new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom);

            if (Orientation == ScrollOrientation.Both)
            {
                childRect.Right = float.PositiveInfinity;
                childRect.Bottom = float.PositiveInfinity;
            }
            else if (Orientation == ScrollOrientation.Vertical)
            {
                childRect.Right = destination.Right;
                childRect.Bottom = float.PositiveInfinity;
            }

            if (Orientation == ScrollOrientation.Horizontal)
            {
                childRect.Right = float.PositiveInfinity;
                childRect.Bottom = destination.Bottom;
            }

            return childRect;
        }

        /// <summary>
        /// This is where the view port is actually is after being scrolled. We used this value to offset viewport on drawing the last frame
        /// </summary>
        public ScaledPoint InternalViewportOffset { get; protected set; } = ScaledPoint.FromPixels(0, 0, 1);

        /// <summary>
        /// 
        /// </summary>
        public ScaledRect ContentViewport { get; protected set; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void AdjustHeaderParallax()
        {
            if (HeaderParallaxRatio == 1)
            {
                ParallaxComputedValue = 0;
            }
            else
            {
                if (this.Orientation == ScrollOrientation.Vertical)
                {
                    var m = InternalViewportOffset.Units.Y * (1 - this.HeaderParallaxRatio);
                    ParallaxComputedValue = -m;
                }
                else if (this.Orientation == ScrollOrientation.Horizontal)
                {
                    var m = InternalViewportOffset.Units.X * (1 - this.HeaderParallaxRatio);
                    ParallaxComputedValue = -m;
                }
            }
        }

        private float lastPos;

        /// <summary>
        /// Input offset parameters in PIXELS.
        /// This is called inside Draw, only if need reposition viewport.
        /// Here we can construct anything according current offset before painting.
        /// Creates a valid ViewportRect inside.
        /// </summary>
        /// /// <param name="destination"></param>
        /// <param name="offsetPixels"></param>
        /// <param name="viewportScale"></param>
        /// <param name="scale"></param>
        /// <returns>Whether we changed viewport and cache changed</returns>
        protected virtual bool PositionViewport(SKRect destination, SKPoint offsetPixels, float viewportScale,
            float scale, bool forceSyncOffsets)
        {
            if (!IsContentActive || Content == null)
                return false;

            lastPos = offsetPixels.Y;

            if (!IsSnapping)
                Snapped = false;

            ContentAvailableSpace = GetContentAvailableRect(destination);

            //we scroll at subpixels but stop only at pixel-snapped
            if (!IsUserPanning && !IsScrolling || onceAfterInitializeViewport)
            {
                var roundY = (float)Math.Round(offsetPixels.Y) - offsetPixels.Y;
                var roundX = (float)Math.Round(offsetPixels.X) - offsetPixels.X;
                offsetPixels.Offset(roundX, roundY);
            }

            InternalViewportOffset =
                ScaledPoint.FromPixels(offsetPixels.X, offsetPixels.Y, scale);

            var childRect = ContentAvailableSpace;
            childRect.Offset(InternalViewportOffset.Pixels.X, InternalViewportOffset.Pixels.Y);

            ContentRectWithOffset = ScaledRect.FromPixels(childRect, scale);

            AdjustHeaderParallax();

            //content size changed?.. maybe need to set offsets to a valid position then
            if (onceAfterInitializeViewport)
            {
                onceAfterInitializeViewport = false;
                var clamped = ClampOffset(InternalViewportOffset.Units.X, InternalViewportOffset.Units.Y,
                    ContentOffsetBounds, true);

                if (clamped.X == 0 && clamped.Y == 0 && OverScrolled)
                {
                    HideRefreshIndicator();
                    ScrollTo(0, 0, 0, false);
                }

                forceSyncOffsets = true;
            }

            if (forceSyncOffsets)
            {
                _viewportOffsetX = InternalViewportOffset.Units.X;
                _viewportOffsetY = InternalViewportOffset.Units.Y;
            }

            OverscrollDistance =
                CalculateOverscrollDistance(InternalViewportOffset.Units.X, InternalViewportOffset.Units.Y);

            if (Content is IInsideViewport viewport)
            {
                SKRect absoluteViewPort = DrawingRect;

                if (Header != null)
                {
                    if (this.Orientation == ScrollOrientation.Vertical)
                    {
                        absoluteViewPort = new SKRect(
                            absoluteViewPort.Left,
                            absoluteViewPort.Top - Header.MeasuredSize.Pixels.Height,
                            absoluteViewPort.Right,
                            absoluteViewPort.Bottom - Header.MeasuredSize.Pixels.Height
                        );
                        absoluteViewPort.Offset(0, (float)Math.Round(-ContentOffset * scale));
                    }
                    else if (this.Orientation == ScrollOrientation.Horizontal)
                    {
                        absoluteViewPort = new SKRect(absoluteViewPort.Left - Header.MeasuredSize.Pixels.Width,
                            absoluteViewPort.Top, absoluteViewPort.Right - Header.MeasuredSize.Pixels.Width,
                            absoluteViewPort.Bottom);
                        absoluteViewPort.Offset((float)Math.Round(-ContentOffset * scale), 0);
                    }
                }

                ContentViewport = ScaledRect.FromPixels(absoluteViewPort, scale);

                viewport.OnViewportWasChanged(ContentViewport, InternalViewportOffset);
            }

            CheckNeedRefresh();

            // While a programmatic jump (ScrollToIndex) is pending the viewport is parked at a
            // meaningless position — evaluating edge triggers there causes load cascades.
            if (OrderedScrollToIndex.IsSet)
                return true;

            if (LoadMoreCommand != null)
            {
                if (_loadMoreBottomTriggeredAt != 0
                    && Math.Abs(InternalViewportOffset.Units.Y - _loadMoreBottomTriggeredAt) > (LoadMoreOffset + 100) * scale
                    && (DateTime.Now - _loadMoreBottomTriggeredTime).TotalSeconds > 2
                    )
                //we have scrolled out of the triggered loadMore by 100pts
                {
                    _loadMoreBottomTriggeredAt = 0; //so can track loadMore again
                }

                // Underfilled viewport must NOT block LoadMore: that's precisely when more content
                // is needed. Storm protection comes from the trigger latch (re-armed only by
                // InitializeViewport on content-size change) and the layout measurement veto.
                if ((HasContentToScroll || ContentUnderfillsViewport) && _loadMoreBottomTriggeredAt == 0)
                {
                    if (IsOppositeLoadMoreBlocked(LoadMoreDirection.Bottom, scale))
                        return true;

                    bool shouldTriggerLoadMore = false;
                    var threshold = LoadMoreOffset * scale;
                    shouldTriggerLoadMore = (Orientation == ScrollOrientation.Vertical &&
                                             InternalViewportOffset.Units.Y <= _scrollMinY + threshold)
                                            || (Orientation == ScrollOrientation.Horizontal &&
                                                InternalViewportOffset.Units.X <= _scrollMinX + threshold);


                    if (shouldTriggerLoadMore)
                    {
                        // Let the content decide if LoadMore should be triggered
                        if (Content is IInsideViewport contentViewport)
                        {
                            // Ask the layout if it's ready for LoadMore based on its measurement state
                            shouldTriggerLoadMore = contentViewport.ShouldTriggerLoadMore(ContentViewport, LoadMoreDirection.Bottom);
                        }

                        if (shouldTriggerLoadMore)
                        {
                            _loadMoreBottomTriggeredTime = DateTime.Now;
                            // 0 is the "unlatched" sentinel; an edge can sit exactly at offset 0,
                            // which would re-execute the command every frame
                            var triggeredAtY = InternalViewportOffset.Units.Y;
                            _loadMoreBottomTriggeredAt = triggeredAtY == 0 ? float.Epsilon : triggeredAtY;
                            MarkLoadMoreDirection(LoadMoreDirection.Bottom);
                            Debug.WriteLine("[SkiaScroll] LoadMoreCommand triggered via ShouldTriggerLoadMore");
                            LoadMoreCommand?.Execute(this);
                            // Filling phase: the command may have been a no-op (nothing below) producing
                            // no invalidation, and triggers only run during a draw — so don't wait for a
                            // next frame that may never come, evaluate the top edge in this same pass.
                            if (!ContentUnderfillsViewport)
                                return true;
                        }
                        else
                        {
                            _loadMoreBottomTriggeredAt = 0;
                        }
                    }
                    else
                    {
                        _loadMoreBottomTriggeredAt = 0;
                    }
                }
            }

            if (LoadMoreTopCommand != null)
            {
                if (_loadMoreTopTriggeredAt != 0
                    && Math.Abs(InternalViewportOffset.Units.Y - _loadMoreTopTriggeredAt) > (LoadMoreTopOffset + 100) * scale
                    && (DateTime.Now - _loadMoreTopTriggeredTime).TotalSeconds > 2
                    )
                {
                    _loadMoreTopTriggeredAt = 0;
                }

                // Same underfilled rule as for the bottom edge above.
                if ((HasContentToScroll || ContentUnderfillsViewport) && _loadMoreTopTriggeredAt == 0)
                {
                    if (IsOppositeLoadMoreBlocked(LoadMoreDirection.Top, scale))
                        return true;

                    bool shouldTriggerTopLoadMore = false;
                    var threshold = LoadMoreTopOffset * scale;
                    shouldTriggerTopLoadMore = (Orientation == ScrollOrientation.Vertical &&
                                                InternalViewportOffset.Units.Y >= _scrollMaxY - threshold)
                                               || (Orientation == ScrollOrientation.Horizontal &&
                                                   InternalViewportOffset.Units.X >= _scrollMaxX - threshold);

                    if (shouldTriggerTopLoadMore)
                    {
                        if (Content is IInsideViewport contentViewport)
                        {
                            shouldTriggerTopLoadMore = contentViewport.ShouldTriggerLoadMore(ContentViewport, LoadMoreDirection.Top);
                        }

                        if (shouldTriggerTopLoadMore)
                        {
                            _loadMoreTopTriggeredTime = DateTime.Now;
                            // 0 is the "unlatched" sentinel; the top edge IS offset 0,
                            // which would re-execute the command every frame
                            var triggeredAt = Orientation == ScrollOrientation.Vertical
                                ? InternalViewportOffset.Units.Y
                                : InternalViewportOffset.Units.X;
                            _loadMoreTopTriggeredAt = triggeredAt == 0 ? float.Epsilon : triggeredAt;
                            MarkLoadMoreDirection(LoadMoreDirection.Top);
                            Debug.WriteLine("[SkiaScroll] LoadMoreTopCommand triggered via ShouldTriggerLoadMore");
                            LoadMoreTopCommand?.Execute(this);
                            return true;
                        }
                        else
                        {
                            _loadMoreTopTriggeredAt = 0;
                        }
                    }
                    else
                    {
                        _loadMoreTopTriggeredAt = 0;
                    }
                }
            }

            return true;
        }

        protected void SendScrolled()
        {
            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine($"[SkiaScroll] Scrolled offset {InternalViewportOffset.Pixels}");
            }

            Scrolled?.Invoke(this, InternalViewportOffset);
            OnScrolled();
        }

        protected void SendScrollingEnded()
        {
            ScrollingEnded?.Invoke(this, InternalViewportOffset);
            OnScrollCompleted();
        }

        /// <summary>
        /// This triggers smapping checks and actions
        /// </summary>
        protected virtual void OnScrollCompleted()
        {
            if (CheckNeedToSnap())
                Snap(SystemAnimationTimeSecs);
            else
            {
                _destination = SKRect.Empty; //force reposition viewport on next draw todo check this 
            }
        }

        public virtual void OnScrollingStateChanged(bool value)
        {
            _autoCacheContent = !value;

            ApplyAutoCache();
        }


        private bool _autoCacheContent;
        private SkiaCacheType _cacheType;

        protected virtual void ApplyAutoCache()
        {
            if (Content != null && AutoCache)
            {
                //Debug.WriteLine($"[c] cacheContent: {cacheContent}");
                if (_autoCacheContent)
                {
                    UseCache = SkiaCacheType.Operations;
                }
                else
                {
                    UseCache = _cacheType;
                }
            }
        }

        private bool _IsScrolling;
        public bool IsScrolling
        {
            get { return _IsScrolling; }
            set
            {
                if (_IsScrolling != value)
                {
                    OnScrollingStateChanged(value);

                    if (value)
                    {
                        InteractionState = ScrollInteractionState.Scrolling;
                    }

                    bool fireStop = _IsScrolling && !value;
                    _IsScrolling = value;
                    OnPropertyChanged();
                    if (fireStop)
                    {
                        InteractionState = ScrollInteractionState.None;
                        SendScrollingEnded();
                    }
                }
            }
        }

        float _loadMoreBottomTriggeredAt = 0;
        float _loadMoreTopTriggeredAt = 0;

        protected virtual void HideRefreshIndicator()
        {
            RefreshIndicator?.SetDragRatio(0, 0, RefreshShowDistance, RefreshDistanceLimit);
            ScrollLocked = false;
            wasRefreshing = false;
        }

        /// <summary>
        /// Notify current scroll offset to some dependent views.
        /// </summary>
        public virtual void OnScrolled()
        {


        }

        public event EventHandler<ScaledPoint> ScrollingEnded;
        public event EventHandler<ScaledPoint> Scrolled;

        protected double UsingRefreshDistanceLimit
        {
            get
            {
                var refreshAt = RefreshDistanceLimit;
                if (refreshAt < RefreshShowDistance)
                {
                    refreshAt = RefreshShowDistance;
                }

                return refreshAt;
            }
        }

        protected virtual void ShowRefreshIndicatorForced()
        {
            if (RefreshIndicator != null)
            {
                var ratio = 1.0f;
                var overscroll = RefreshShowDistance * RenderingScale;
                if (Orientation == ScrollOrientation.Vertical)
                {
                    if (OverscrollDistance.Y <= RefreshShowDistance)
                    {
                        SetScrollOffset(DrawingRect, _updatedViewportForPixY, overscroll, _zoomedScale, RenderingScale,
                            true);
                    }
                    RefreshIndicator.SetDragRatio(ratio, InternalViewportOffset.Units.Y, RefreshShowDistance, RefreshDistanceLimit);
                }
                else if (Orientation == ScrollOrientation.Horizontal)
                {
                    if (OverscrollDistance.X <= RefreshShowDistance)
                    {
                        SetScrollOffset(DrawingRect, overscroll, _updatedViewportForPixX, _zoomedScale, RenderingScale,
                            true);
                    }
                    RefreshIndicator.SetDragRatio(ratio, InternalViewportOffset.Units.X, RefreshShowDistance, RefreshDistanceLimit);
                }

                Update();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void ApplyScrollPositionToRefreshViewUnsafe()
        {
            var ratio = 0.0f;
            bool canRefresh = false;
            var refreshAt = UsingRefreshDistanceLimit;

            if (Orientation == ScrollOrientation.Vertical)
            {
                ratio = OverscrollDistance.Y / RefreshShowDistance;
                if (ratio >= 0)
                    RefreshIndicator.SetDragRatio(ratio, InternalViewportOffset.Units.Y, RefreshShowDistance, RefreshDistanceLimit);
                canRefresh = InternalViewportOffset.Units.Y > refreshAt;
            }

            else if (Orientation == ScrollOrientation.Horizontal)
            {
                ratio = OverscrollDistance.X / RefreshShowDistance;
                if (ratio >= 0)
                    RefreshIndicator.SetDragRatio(ratio, InternalViewportOffset.Units.X, RefreshShowDistance, RefreshDistanceLimit);
                canRefresh = InternalViewportOffset.Units.X > refreshAt;
            }


            if (IsUserPanning)
            {
                if (canRefresh && !IsRefreshing && RefreshCommand != null
                    && !wasRefreshing && !ScrollLocked)
                {
                    StopVelocityPanning();
                    IsRefreshing = true;
                }
            }
        }

        public virtual void CheckNeedRefresh()
        {
            if (IsRefreshing)
            {
                if (RefreshIndicator != null && !RefreshIndicator.IsVisible)
                {
                    RefreshIndicator.IsVisible = true;
                    ShowRefreshIndicatorForced();
                }

                return;
            }

            if (RefreshEnabled && RefreshIndicator != null)
            {
                if (OverScrolled)
                {
                    ApplyScrollPositionToRefreshViewUnsafe();
                }
                //stop and hide when when back from overscroll
                else if (RefreshIndicator.IsVisible)
                {
                    StopVelocityPanning();
                    HideRefreshIndicator();
                }
            }
        }

        bool wasRefreshing;

        public void SetIsRefreshing(bool state, bool initial)
        {
            Debug.WriteLine($"[SCROLL] IsRefreshing {state}");
            //lock scrolling at top
            if (state)
            {
                LockGesturesUntilDown = true;
                wasRefreshing = true;
                IsRefreshing = true;
                ScrollLocked = true;
                ShowRefreshIndicatorForced(); //insure for code-behind triggered refresh
                RefreshCommand?.Execute(this);
            }
            else
            {
                if ( initial || (ViewportOffsetX == 0 && ViewportOffsetY == 0))
                {
                    HideRefreshIndicator();
                }
                else
                {
                    ScrollToTop(SystemAnimationTimeSecs);
                }
                LockGesturesUntilDown = false;
                ScrollLocked = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        protected virtual void PaintViews(DrawingContext ctx)
        {
            if (ctx.GetArgument(ContextArguments.Scale.ToString()) is float zoomedScale)
            {
                ctx = ctx.WithScale(zoomedScale);
            }

            if (ctx.GetArgument(ContextArguments.Rect.ToString()) is SKRect childRectWithOffset)
            {
                ctx = ctx.WithDestination(childRectWithOffset);
            }

            DrawViews(ctx);
        }

        protected override void Paint(DrawingContext ctx)
        {
            if (ctx.Destination.Width == 0 || ctx.Destination.Height == 0)
                return;

            base.Paint(ctx);

            var c = ctx.WithArguments(
                new(ContextArguments.Scale.ToString(), _zoomedScale),
                new(ContextArguments.Rect.ToString(), ContentRectWithOffset.Pixels));

            //Children containment, same pattern SkiaShape uses for its outline: the outer clip in
            //DrawWithClipAndTransforms is an EFFECTS concern (expanded by the subtree's aggregated
            //effects margin so shadows/glow survive cache boundaries) — it must not be relied on to
            //contain scrolled content. A shadowed descendant in content would widen it and leak
            //scrolled content past the viewport (3*Blur*scale). So the viewport clips its children
            //HERE, inside Paint: the own-effects filter (PaintWithEffects wraps Paint) captures the
            //already-clipped content, so a shadow ON the scroll still paints beyond bounds while
            //content never does.
            //Gated: when nothing in the subtree paints beyond bounds the outer clip is already the
            //exact rect and this inner clip is skipped — zero per-frame cost for plain scrolls.
            var needContentClip = GetRenderingExpandPixels() != Thickness.Zero;

            int saved = 0;
            if (needContentClip)
            {
                saved = ctx.Context.Canvas.Save();
                ClipSmart(ctx.Context.Canvas, GetContentClip());
            }

            try
            {
                if (UseVirtual)
                {
                    DrawVirtual(c);
                }
                else
                {
                    PaintViews(c);
                }
            }
            finally
            {
                if (needContentClip)
                {
                    ctx.Context.Canvas.RestoreToCount(saved);
                }
            }
        }


        private bool devUseVelocityPanning = false;

        protected override void Draw(DrawingContext context)
        {
            if (_animatorFlingY == null)
            {
                InitializeScroller((float)context.Scale);
            }

            isDrawing = true;

            // A head-insert (backward LoadMore prepend) must be committed BEFORE this frame's offset
            // is computed so the structure translation and the viewport compensation land in the same
            // frame — otherwise content would flash shifted for one frame.
            if (this.Content is SkiaLayout layoutContent && layoutContent.HasPendingStructureRebase)
            {
                layoutContent.CommitPendingStructureRebase();
            }

            var needAdjustPos = false;

            if (IsContentActive)
            {
                bool contentSizeChanged = _lastContentSize != this.Content.MeasuredSize;

                // For tiled-planes virtualization the scroll extent is derived from ItemsSource.Count (an
                // estimate), not the measured window — which may not change on a LoadMore append. So also
                // recompute bounds when the virtual item count changes, otherwise the scroll stays clamped
                // to the previously loaded count and you can't scroll past it.
                bool virtualCountChanged = false;
                if (UseVirtual && this.Content is SkiaLayout vlayout
                    && vlayout.ItemsSource != null && vlayout.ItemsSource.Count != _lastVirtualItemsCount)
                {
                    _lastVirtualItemsCount = vlayout.ItemsSource.Count;
                    contentSizeChanged = true;
                    virtualCountChanged = true;
                }

                // GetContentOffsetBounds clamps travel to the MEASURED end while background measurement
                // is incomplete — but bounds are only recomputed on a ContentSize change. When the
                // progressive estimate reaches the full extent EARLY (uniform cells / SkiaCachedStack),
                // ContentSize stops changing while the frontier still advances: the travel clamp freezes
                // at a stale frontier and the scroll WALLS there forever (window slides starve too, since
                // the margin is never reached). A windowed slide (append batch + head trim) is even
                // NET-ZERO in both ContentSize and LastMeasuredIndexLocal, so track the measured END
                // POSITION — the actual input of the travel clamp — and refresh bounds when it moves.
                bool measuredTravelChanged = false;
                // MeasureVisible: frontier moves without ContentSize change. Windowed (ANY strategy,
                // incl. MeasureFirst): slides are net-zero in size AND count — only the measured end
                // shifts; without this refresh the stale bounds strand the viewport at the edge
                // (harness EDGE LOADMORE vis=-1).
                if (this.Content is SkiaLayout mvContent && mvContent.IsTemplated
                    && ContentSize != null && ContentSize.Pixels.Height > 0
                    && (mvContent.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                        || mvContent.ItemsWindow != null))
                {
                    var measuredEnd = mvContent.GetMeasuredContentEnd();
                    // ALSO key on the window position: a rebase/slide over UNIFORM cells keeps the
                    // measured end value IDENTICAL (same local extent) while WindowEnd moved — the
                    // travel extension then stays stale (still extended at the true source end:
                    // a fling after jump-to-last landed the viewport in the void past the content).
                    var windowEnd = mvContent.ItemsWindow?.WindowEnd ?? -1;
                    // AND on the SOURCE count: a user LoadMore append at the true end changes NEITHER
                    // WindowEnd NOR the measured end NOR ContentSize — without a refresh the travel
                    // stays clamped at the pre-append end and the user is LOCKED OUT of the appended
                    // page (scroll nudges bounce back, window never slides into the new items).
                    var sourceCount = mvContent.ItemsWindow != null ? (mvContent.ItemsSource?.Count ?? -1) : -1;
                    if (Math.Abs(measuredEnd - _lastMeasuredTravelEnd) > 0.5
                        || windowEnd != _lastWindowTravelEnd
                        || sourceCount != _lastWindowSourceCount)
                    {
                        _lastMeasuredTravelEnd = measuredEnd;
                        _lastWindowTravelEnd = windowEnd;
                        _lastWindowSourceCount = sourceCount;
                        // must FORCE ApplyContentSize: its size-compare guard sees the unchanged
                        // ContentSize and would skip InitializeViewport, leaving the stale clamp
                        contentSizeChanged = true;
                        measuredTravelChanged = true;
                    }
                }

                //content size changed, we need to initialize scroller again at least
                if (contentSizeChanged)
                {
                    needAdjustPos = true;

                    if (NeedAutoSize)
                    {
                        NeedMeasure = true;
                    }
                    else
                    {
                        ContentSize = this.Content.MeasuredSize;
                        _lastContentSize = this.Content.MeasuredSize;
                        // On a virtual (LoadMore) count change the windowed ContentSize/MeasuredSize often
                        // DON'T change, so ApplyContentSize's size-compare guard would skip re-initializing
                        // the viewport -> scroll stays clamped to the old count (can't scroll past it). The
                        // real extent comes from ItemsSource.Count (GetContentOffsetBounds), so force it.
                        // Same for a measured-frontier move with unchanged ContentSize (windowed slide).
                        ApplyContentSize(virtualCountChanged || measuredTravelChanged);
                    }
                }
            }

            // A ScrollToIndex to an index not yet measured/created stays pending; retry it every
            // frame until the structure can resolve it. MUST run AFTER the content-size reconciliation
            // above so the clamp uses THIS frame's fresh ptsContentHeight: a jump issued the same frame
            // background measurement grows the content (scroll-to-oldest) would otherwise clamp the
            // target offset against the stale previous-frame content height and land mid-list.
            if (OrderedScrollToIndex.IsSet)
            {
                // While held (MeasureVisible target not really measured yet) it kicks background
                // measurement itself and Repaints, so retry frames keep coming until it resolves.
                ExecuteScrollToIndexOrder();
            }

            // Content grew during an edge-cut fling (backward LoadMore prepend): bounds are fresh
            // now, restart the fling from the current position with its remaining velocity so it
            // decelerates naturally into the new content instead of slam-stopping at the old edge.
            if (_replanFlingY)
            {
                _replanFlingY = false;
                if (_animatorFlingY != null)
                {
                    // The cut duration usually expires before this frame runs, so the animator is
                    // ALREADY stopped here: use the velocity captured when the shift happened,
                    // otherwise the scroll dies dead at every window cut.
                    var remainingVelocity = _animatorFlingY.IsRunning
                        ? (float)_animatorFlingY.CurrentVelocity
                        : _replanVelocityY;
                    _animatorFlingY.Stop();
                    _changeSpeed = null; // consumed: don't let a later anchor shift resurrect this fling
                    if (Math.Abs(remainingVelocity) > _minVelocity)
                    {
                        StartToFlingFrom(_animatorFlingY, ViewportOffsetY, remainingVelocity);
                    }
                }
            }

            Arrange(context.Destination, SizeRequest.Width, SizeRequest.Height, context.Scale);
            //we exit with DrawingRect assigned to new destination

            var zoomedScale = (float)(context.Scale * ViewportZoom);
            var scale = context.Scale;

            if (!CheckIsGhost())
            {
                if (devUseVelocityPanning)
                {
                    ApplyPannedOffsetWithVelocity(context.Context);
                }

                var posX = (float)(ViewportOffsetX * zoomedScale);
                var posY = (float)(ViewportOffsetY * zoomedScale);

                IsScrolling = _animatorFlingY.IsRunning || _animatorFlingX.IsRunning ||
                              _vectorAnimatorBounceY.IsRunning || _vectorAnimatorBounceX.IsRunning
                              || _scrollerX.IsRunning || _scrollerY.IsRunning || IsUserPanning;

                var needReposition =
                    zoomedScale != _zoomedScale ||
                    _updatedViewportForPixY != posY
                    || _updatedViewportForPixX != posX
                    || _destination != DrawingRect;

                if (needAdjustPos)
                {
                    //Debug.WriteLine($"[SCROLL] needAdjustPos Y {posY/scale}, {_lastContentBounds.Height} => {ContentOffsetBounds.Height})");
                    if (ResetScrollPositionOnContentSizeChanged)
                    {
                        ViewportOffsetX = 0;
                        ViewportOffsetY = 0;
                        posX = 0;
                        posY = 0;
                    }
                    else
                    {
                        //do not allow empty space when content became smaller than viewport
                        var overscrollPoints = CalculateOverscrollDistance(posX/scale, posY/scale);
                        posX -= overscrollPoints.X * scale;
                        posY -= overscrollPoints.Y * scale;

                        // Write the clamp BACK through the anchor-shift plumbing: adjusting only the
                        // local pos (or only ViewportOffsetY) left the PAN BASELINE holding the
                        // out-of-bounds value — an active pan restored the ghost on its next move and
                        // the viewport stayed stranded past content forever (windowed source reaching
                        // its true end after virtual-extent travel: vis=-1). OffsetVisibleAnchorY
                        // shifts the offset, the pan baseline and any running animators together.
                        // ONLY when measurement has SETTLED: mid-flight (engage-on-grow reset, window
                        // rebase) the progressive ContentSize is transiently tiny — a sticky write-back
                        // then DESTROYS a just-compensated offset (engage anchor teleported to newest).
                        // Unsettled frames keep the per-frame visual clamp (posX/posY) only.
                        bool measureSettled = !(Content is SkiaLayout mvl && mvl.IsTemplated
                            && mvl.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                            && mvl.LastMeasuredIndexLocal < (mvl.EffectiveItemsSource?.Count ?? 0) - 1);

                        if (overscrollPoints.Y != 0 && measureSettled)
                        {
                            OffsetVisibleAnchorY(-overscrollPoints.Y);
                            posY = (float)(ViewportOffsetY * zoomedScale);
                        }

                        if (overscrollPoints.X != 0 && measureSettled)
                        {
                            ViewportOffsetX -= overscrollPoints.X;
                            _panningCurrentOffsetPts.X -= overscrollPoints.X;
                            posX = (float)(ViewportOffsetX * zoomedScale);
                        }
                    }
                }

                //reposition viewport (scroll)
                if (needReposition || needAdjustPos)
                {
                    SetScrollOffset(DrawingRect, posX, posY, zoomedScale, context.Scale, false);
                }

                if (UsingCacheType != SkiaCacheType.None)
                {
                    var destination = DrawingRect;
                    var recordArea = destination;
                    if (UsingCacheType == SkiaCacheType.OperationsFull)
                    {
                        recordArea = context.Context.Canvas.LocalClipBounds;
                        destination = recordArea;
                    }

                    //paint from cache
                    var clone = AddPaintArguments(context).WithDestination(destination);
                    if (TryUseExistingRenderingObjectOrCreateNewAndPaint(clone, recordArea))
                    {
                        ExistingCacheWasRendered = true;
                    }
                }
                else
                {
                    var clone = AddPaintArguments(context).WithDestination(DrawingRect);
                    DrawWithClipAndTransforms(clone, DrawingRect, true, true, (ctx) => { PaintWithEffects(ctx); });
                }
             
            }

            FinalizeDrawingWithRenderObject(context);

            OnDrawn(context.WithScale(_zoomedScale));

            isDrawing = false;
        }


        /// <summary>
        /// Pass position in PIXELS
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        /// <param name="zoomedScale"></param>
        /// <param name="scale"></param>
        /// <param name="forceSyncOffsets"></param>
        protected virtual void SetScrollOffset(SKRect destination, float posX, float posY, float zoomedScale,
            float scale, bool forceSyncOffsets)
        {
            if (Orientation == ScrollOrientation.Vertical)
            {
                if (posY < _updatedViewportForPixY)
                {
                    ScrollingDirection = LinearDirectionType.Forward;
                }
                else if (posY > _updatedViewportForPixY)
                {
                    ScrollingDirection = LinearDirectionType.Backward;
                }
                else
                {
                    ScrollingDirection = LinearDirectionType.None;
                }
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                if (posX < _updatedViewportForPixX)
                {
                    ScrollingDirection = LinearDirectionType.Forward;
                }
                else if (posX > _updatedViewportForPixX)
                {
                    ScrollingDirection = LinearDirectionType.Backward;
                }
                else
                {
                    ScrollingDirection = LinearDirectionType.None;
                }
            }
            else
            {
                ScrollingDirection = LinearDirectionType.None;
            }

            _destination = destination;
            _updatedViewportForPixX = posX;
            _updatedViewportForPixY = posY;
            _zoomedScale = zoomedScale;

            if (PositionViewport(destination, new(posX, posY), zoomedScale, scale, forceSyncOffsets))
            {
                InvalidateCache();

                //POST EVENTS
                if (IsScrolling)
                    SendScrolled();
            }
        }

        public double ParallaxComputedValue
        {
            get => _parallaxComputedValue;
            set
            {
                if (value.Equals(_parallaxComputedValue)) return;
                _parallaxComputedValue = value;
                OnPropertyChanged();
            }
        }

        protected override int DrawViews(DrawingContext context)
        {
            if (context.Destination.Width <= 0 || context.Destination.Height <= 0)
            {
                return 0;
            }

            int Render(DrawingContext ctx)
            {
                var drawViews = new List<SkiaControl>(5) { Content };
                var offsetFooter = 0f;
                var translateContent = 0.0;

                if (Header != null)
                {
                    bool drawHeaderBefore = false;

                    if (this.Orientation == ScrollOrientation.Vertical)
                    {
                        translateContent = Header.MeasuredSize.Units.Height;

                        if (!ParallaxOverscrollEnabled)
                        {
                            if (OverscrollDistance.Y <= 0)
                            {
                                Header.AddTranslationY = ParallaxComputedValue;
                            }
                            else
                            {
                                Header.AddTranslationY = 0;
                            }
                        }
                        else
                        {
                            Header.AddTranslationY = ParallaxComputedValue;
                        }

                        // Adjust the header hitbox for parallax
                        var headerTop = context.Destination.Top;
                        var headerBottom = headerTop + Header.MeasuredSize.Pixels.Height;

                        var hitboxHeader = new SKRect(
                            0,
                            (float)headerTop,
                            context.Destination.Width,
                            (float)headerBottom);

                        if (!HeaderBehind && !HeaderSticky)
                        {
                            //draw only if onscreen
                            if (hitboxHeader.IntersectsWith(this.Viewport.Pixels))
                                drawHeaderBefore = true;
                        }
                        else
                        {
                            //will not draw header as one of the views, but as overlay, like refreshview below
                            translateContent += ContentOffset;
                        }

                        if (Content != null)
                        {
                            Content.AddTranslationY = translateContent;
                        }

                        offsetFooter += Header.MeasuredSize.Units.Height + (float)ContentOffset;

                        if (drawHeaderBefore)
                        {
                            drawViews.Add(Header);
                        }
                        else if (HeaderBehind)
                        {
                            if (hitboxHeader.IntersectsWith(this.Viewport.Pixels))
                            {
                                if (HeaderSticky)
                                {
                                    Header.Render(context.WithDestination(DrawingRect));
                                }
                                else
                                {
                                    Header.Render(context);
                                }
                            }
                        }
                    }
                    else if (this.Orientation == ScrollOrientation.Horizontal)
                    {
                        translateContent = Header.MeasuredSize.Units.Width;

                        if (!ParallaxOverscrollEnabled)
                        {
                            if (OverscrollDistance.X <= 0)
                            {
                                Header.AddTranslationX = ParallaxComputedValue;
                            }
                            else
                            {
                                Header.AddTranslationX = 0;
                            }
                        }
                        else
                        {
                            Header.AddTranslationX = ParallaxComputedValue;
                        }

                        // Adjust the header hitbox for parallax in horizontal orientation
                        var headerLeft = ctx.Destination.Left;
                        var headerRight = headerLeft + Header.MeasuredSize.Pixels.Width;
                        var hitboxHeader = new SKRect((float)headerLeft, 0, (float)headerRight,
                            ctx.Destination.Height);

                        if (!HeaderBehind && !HeaderSticky)
                        {
                            // Draw only if onscreen
                            if (hitboxHeader.IntersectsWith(this.Viewport.Pixels))
                                drawHeaderBefore = true;
                        }
                        else
                        {
                            // Will not draw header as one of the views, but as overlay
                            translateContent += ContentOffset;
                        }

                        if (Content != null)
                        {
                            Content.AddTranslationX = translateContent;
                        }

                        offsetFooter += Header.MeasuredSize.Units.Width + (float)ContentOffset;

                        if (drawHeaderBefore)
                        {
                            drawViews.Add(Header);
                        }
                        else if (HeaderBehind)
                        {
                            if (hitboxHeader.IntersectsWith(this.Viewport.Pixels))
                            {
                                if (HeaderSticky)
                                {
                                    Header.Render(ctx.WithDestination(DrawingRect));
                                }
                                else
                                {
                                    Header.Render(ctx);
                                }
                            }
                        }
                    }
                }

                if (Footer != null)
                {
                    if (this.Orientation == ScrollOrientation.Vertical)
                    {
                        if (IsContentActive)
                        {
                            // Use the CURRENT measured height, not just the last-rendered DrawingRect: the footer
                            // offset is computed BEFORE Content renders this frame, so DrawingRect is one frame
                            // stale. On a sudden empty->loaded growth that stale (near-zero) height positions the
                            // footer at the top for one frame — the "footer flashes before cells" bug. Max() keeps
                            // it below the fuller of the two, so the footer never lands ON content on any frame.
                            offsetFooter += Math.Max(Content.DrawingRect.Height, Content.MeasuredSize.Pixels.Height);
                        }

                        Footer.AddTranslationY = offsetFooter / ctx.Scale;

                        //draw only if onscreen — MUST include the current scroll offset. offsetFooter is the
                        //footer's position at scroll=0 (below full content); without adding InternalViewportOffset
                        //the hitbox stays far below the viewport for content taller than it, so the footer is
                        //culled forever (space reserved in the extent, but never painted — the empty-gap bug).
                        var footerTop = Viewport.Pixels.Top + offsetFooter + InternalViewportOffset.Pixels.Y;
                        var hitbox = new SKRect(Viewport.Pixels.Left, footerTop,
                            Viewport.Pixels.Right,
                            footerTop + Footer.MeasuredSize.Pixels.Height);
                        if (hitbox.IntersectsWith(this.Viewport.Pixels))
                            drawViews.Add(Footer);
                    }
                    else if (this.Orientation == ScrollOrientation.Horizontal)
                    {
                        if (IsContentActive)
                        {
                            offsetFooter += Math.Max(Content.DrawingRect.Width, Content.MeasuredSize.Pixels.Width);
                        }

                        Footer.AddTranslationX = offsetFooter / ctx.Scale;

                        //draw only if onscreen — include the current horizontal scroll offset (see vertical note)
                        var footerLeft = Viewport.Pixels.Left + offsetFooter + InternalViewportOffset.Pixels.X;
                        var hitbox = new SKRect(
                            footerLeft,
                            Viewport.Pixels.Top,
                            footerLeft + Footer.MeasuredSize.Pixels.Width,
                            Viewport.Pixels.Bottom);
                        if (hitbox.IntersectsWith(this.Viewport.Pixels))
                            drawViews.Add(Footer);
                    }
                }

                return RenderViewsList(ctx, drawViews);
            }

            var drawn = Render(context);

            if (Header != null && HeaderSticky && !HeaderBehind)
            {
                Header.Render(context.WithDestination(DrawingRect));
                drawn++;
            }

            if (RefreshEnabled && RefreshIndicator != null && OverScrolled)
            {
                if (InternalRefreshIndicator is SkiaControl refreshIndicator)
                {
                    if (refreshIndicator.CanDraw)
                    {
                        refreshIndicator.Render(context);
                        drawn++;
                    }
                }
            }

            UpdateScrollBarIndicator();

            if (InternalScrollBar is SkiaControl scrollBar && scrollBar.CanDraw)
            {
                //overlay pinned to the viewport, like sticky header above
                scrollBar.Render(context.WithDestination(DrawingRect));
                drawn++;
            }

            if (InternalScrollBarHorizontal is SkiaControl scrollBarHorizontal && scrollBarHorizontal.CanDraw)
            {
                scrollBarHorizontal.Render(context.WithDestination(DrawingRect));
                drawn++;
            }

            return drawn;
        }

        float _updatedViewportForPixX;
        float _updatedViewportForPixY;
        //float _lastPosViewportScale;

        public SKRect ContentAvailableSpace { get; protected set; }

        /// <summary>
        /// The viewport for content
        /// </summary>
        public ScaledRect ContentRectWithOffset { get; protected set; }

        public SkiaScroll() : base()
        {
            Init();
        }

        protected void Init()
        {
            AutoCache = true;
            UpdateFriction();
            SetRefreshIndicator(RefreshIndicator);
            SetScrollBar(ScrollBar);
            SetScrollBarHorizontal(ScrollBarHorizontal);
        }

        public override void SetChildren(IEnumerable<SkiaControl> views)
        {
            //do not use subviews as we are using Content property for this control

            return;
        }

        public override void ApplyBindingContext()
        {
            base.ApplyBindingContext();

            if (Content != null && Content?.BindingContext == null)
                Content?.SetInheritedBindingContext(BindingContext);
        }

        /// <summary>
        /// Use Content property for direct access
        /// </summary>
        /// <param name="view"></param>
        protected virtual void SetContent(SkiaControl view)
        {
            var oldContent = Views.Except(new[] { Footer, Header }).FirstOrDefault(x => x is not IRefreshIndicator);
            if (view != oldContent)
            {
                if (oldContent != null)
                {
                    RemoveSubView(oldContent);
                }

                if (view != null)
                {
                    AddSubView(view);
                }

                _cacheType = UseCache;
                _autoCacheContent = AutoCache;
                ApplyAutoCache();
            }
        }

        protected override void OnLayoutReady()
        {
            base.OnLayoutReady();

            SetIsRefreshing(IsRefreshing, true);
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            _autoCacheContent = AutoCache;
        }

        public void SetHeader(SkiaControl view)
        {
            var oldContent = Views.Except(new[] { Footer, Content }).FirstOrDefault(x => x is not IRefreshIndicator);
            if (view != oldContent)
            {
                if (oldContent != null)
                {
                    RemoveSubView(oldContent);
                }

                if (view != null)
                {
                    view.ZIndex = 1;
                    AddSubView(view);
                }
            }
        }

        public void SetFooter(SkiaControl view)
        {
            var oldContent = Views.Except(new[] { Header, Content }).FirstOrDefault(x => x is not IRefreshIndicator);
            if (view != oldContent)
            {
                if (oldContent != null)
                {
                    RemoveSubView(oldContent);
                }

                if (view != null)
                {
                    AddSubView(view);
                }
            }
        }

        #region PROPERTIES

        public static readonly BindableProperty ScrollingSpeedMsProperty = BindableProperty.Create(
            nameof(ScrollingSpeedMs),
            typeof(int),
            typeof(SkiaScroll),
            400);

        /// <summary>
        /// Used by range scroller (ScrollToX, ScrollToY)
        /// </summary>
        public int ScrollingSpeedMs
        {
            get { return (int)GetValue(ScrollingSpeedMsProperty); }
            set { SetValue(ScrollingSpeedMsProperty, value); }
        }

        public static readonly BindableProperty ZoomLockedProperty = BindableProperty.Create(nameof(ZoomLocked),
            typeof(bool),
            typeof(SkiaScroll),
            true);

        public bool ZoomLocked
        {
            get { return (bool)GetValue(ZoomLockedProperty); }
            set { SetValue(ZoomLockedProperty, value); }
        }

        public static readonly BindableProperty ZoomMinProperty = BindableProperty.Create(nameof(ZoomMin),
            typeof(double),
            typeof(SkiaScroll),
            0.1);

        public double ZoomMin
        {
            get { return (double)GetValue(ZoomMinProperty); }
            set { SetValue(ZoomMinProperty, value); }
        }

        public static readonly BindableProperty ZoomMaxProperty = BindableProperty.Create(nameof(ZoomMax),
            typeof(double),
            typeof(SkiaScroll),
            10.0);

        public double ZoomMax
        {
            get { return (double)GetValue(ZoomMaxProperty); }
            set { SetValue(ZoomMaxProperty, value); }
        }

        public static readonly BindableProperty ViewportZoomProperty = BindableProperty.Create(nameof(ViewportZoom),
            typeof(double), typeof(SkiaScroll),
            1.0,
            propertyChanged: NeedDraw);

        public double ViewportZoom
        {
            get { return (double)GetValue(ViewportZoomProperty); }
            set { SetValue(ViewportZoomProperty, value); }
        }

        public static readonly BindableProperty VelocityImageLoaderLockProperty = BindableProperty.Create(
            nameof(VelocityImageLoaderLock),
            typeof(double),
            typeof(SkiaScroll),
            2500.0);

        /// <summary>
        /// Range at which the image loader will stop or resume loading images while scrolling
        /// </summary>
        public double VelocityImageLoaderLock
        {
            get { return (double)GetValue(VelocityImageLoaderLockProperty); }
            set { SetValue(VelocityImageLoaderLockProperty, value); }
        }

        /*
        public static readonly BindableProperty ViewportOffsetYProperty = BindableProperty.Create(nameof(ViewportOffsetY),
            typeof(double), typeof(SkiaScroll),
            0.0,
            propertyChanged: NeedDraw);
        public double ViewportOffsetY
        {
            get
            {
                return (double)GetValue(ViewportOffsetYProperty);
            }
            set
            {
                SetValue(ViewportOffsetYProperty, value);

            }
        }



        public static readonly BindableProperty ViewportOffsetXProperty
            = BindableProperty.Create(nameof(ViewportOffsetX),
            typeof(double), typeof(SkiaScroll),
            0.0,
            propertyChanged: NeedDraw);
        public double ViewportOffsetX
        {
            get
            {
                return (double)GetValue(ViewportOffsetXProperty);
            }
            set
            {
                SetValue(ViewportOffsetXProperty, value);

            }
        }
        */
        public static readonly BindableProperty ContentProperty = BindableProperty.Create(
            nameof(Content),
            typeof(SkiaControl), typeof(SkiaScroll),
            null,
            propertyChanged: OnReplaceContent);

        private static void OnReplaceContent(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control.SetContent(newvalue as SkiaControl);
            }
        }

        public SkiaControl Content
        {
            get { return (SkiaControl)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation),
            typeof(ScrollOrientation), typeof(SkiaScroll),
            ScrollOrientation.Vertical,
            propertyChanged: NeedDraw);

        /// <summary>
        /// <summary>Gets or sets the scrolling direction of the ScrollView. This is a bindable property.</summary>
        /// </summary>
        public ScrollOrientation Orientation
        {
            get { return (ScrollOrientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public static readonly BindableProperty ScrollTypeProperty = BindableProperty.Create(nameof(ViewportScrollType),
            typeof(ViewportScrollType), typeof(SkiaScroll),
            ViewportScrollType.Scrollable,
            propertyChanged: NeedDraw);

        /// <summary>
        /// <summary>Gets or sets the scrolling direction of the ScrollView. This is a bindable property.</summary>
        /// </summary>
        public ViewportScrollType ScrollType
        {
            get { return (ViewportScrollType)GetValue(ScrollTypeProperty); }
            set { SetValue(ScrollTypeProperty, value); }
        }

        #endregion

        #region ScrollViewKeyboardAwareBehavior

        public static readonly BindableProperty AdaptToKeyboardForProperty = BindableProperty.Create(
            nameof(AdaptToKeyboardFor),
            typeof(SkiaControl),
            typeof(SkiaScroll),
            null, propertyChanged: OnNeedAdaptToKeyboard);

        public SkiaControl AdaptToKeyboardFor
        {
            get { return (SkiaControl)GetValue(AdaptToKeyboardForProperty); }
            set { SetValue(AdaptToKeyboardForProperty, value); }
        }

        public static readonly BindableProperty AdaptToKeyboardSizeProperty = BindableProperty.Create(
            nameof(AdaptToKeyboardSize),
            typeof(double),
            typeof(SkiaScroll),
            0.0, propertyChanged: OnNeedAdaptToKeyboard);

        public double AdaptToKeyboardSize
        {
            get { return (double)GetValue(AdaptToKeyboardSizeProperty); }
            set { SetValue(AdaptToKeyboardSizeProperty, value); }
        }

        private static void OnNeedAdaptToKeyboard(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaScroll control)
            {
                control.AdaptToKeyboard();
            }
        }

        double AddPadding = 0;
        private double _scrollTo;

        public void CalculateNeededScrollForKeyboard()
        {
            _scrollTo = -1;

            try
            {
                if (AdaptToKeyboardFor == null || AdaptToKeyboardSize == 0 || !this.LayoutReady)
                {
                    return;
                }

                StopScrolling();

                if (AdaptToKeyboardFor.VisualLayer == null || VisualLayer == null)
                {
                    return;
                }

                var myPos = AdaptToKeyboardFor.VisualLayer.HitBoxWithTransforms.Units.Location;
                var scrollPos =
                    VisualLayer.HitBoxWithTransforms.Units.Location; //this.GetPositionOnCanvasInPoints();

                var scrollRect = new SKRect(0, scrollPos.Y, 10, (float)this.Height + scrollPos.Y);
                var parentHeight = Superview.Height;
                var screenRect = new SKRect(0, 0, 10, (float)(parentHeight - AdaptToKeyboardSize));
                var viewportRect = scrollRect.IntersectWith(screenRect);
                var elementRect = new SKRect(0, myPos.Y, 10, (float)AdaptToKeyboardFor.Height + myPos.Y);

                var needScrollMore = elementRect.Bottom - viewportRect.Bottom + AddPadding;

                if (needScrollMore > 0)
                    _scrollTo = this.ViewportOffsetY - needScrollMore;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public virtual void AdaptToKeyboard()
        {
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(150), () =>
            {
                CalculateNeededScrollForKeyboard();

                //scroll to show on screen
                if (LayoutReady && _scrollTo < 0)
                {
                    //Debug.WriteLine($"[SCROLLING] to {_scrollTo} actual offset {this.OffsetY}, last {lastOffsetY}");
                    ViewportOffsetY = (float)_scrollTo;
                }
            });
        }

        #endregion

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == nameof(ViewportZoom)
                || propertyName == nameof(Orientation))
            {
                Invalidate();
            }
        }

        public override void InvalidateViewport()
        {
            //owns viewport
            Repaint();
        }

        #region RENDERiNG

        public override bool WillClipBounds => true;
        bool isDrawing;
        private SKRect _destination;
        private ScaledSize _lastContentSize;
        private int _lastVirtualItemsCount = -1;
        private double _lastMeasuredTravelEnd = -1;
        private int _lastWindowTravelEnd = -1;
        private int _lastWindowSourceCount = -1;
        private float _velocityKY;
        private float _velocityKX;
        private float _zoomedScale = 1;
        private double _LastPanDistanceY;
        private double _LastPanDistanceX;
        private DateTime _loadMoreBottomTriggeredTime;
        private DateTime _loadMoreTopTriggeredTime;
        private LoadMoreDirection? _lastLoadMoreDirection;
        private DateTime _lastLoadMoreDirectionTime;
        private double _lastLoadMoreDirectionOffset;
        private double _parallaxComputedValue;
        private float _offsetMoved;
        private long _offsetMovedTime;
        private int firstVisibleIndex;
        private int lastVisibleIndex;

        protected virtual void OnDrawn(DrawingContext context)
        {
        }

        //public Action<ISkiaControl> Measured { get; set; }

        #endregion
    }
}
