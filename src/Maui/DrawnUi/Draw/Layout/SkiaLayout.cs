#define TMP

using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;

namespace DrawnUi.Draw
{
    public partial class SkiaLayout : SkiaControl, ISkiaGridLayout
    {
        public override bool PreArrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (!CanDraw)
                return false;

            if (WillInvalidateMeasure)
            {
                WillInvalidateMeasure = false;
                InvalidateMeasureInternal();
            }

            if (NeedMeasure)
            {
                MeasureSelf(destination, widthRequest, heightRequest, scale);
            }
            else
            {
                LastArrangedInside = destination;
            }

            return true;
        }

        public override bool IsGestureForChild(SkiaControlWithRect child, SKPoint point)
        {
            if (this.IsStack)
            {
                bool inside = false;
                if (child.Control != null && !child.Control.IsDisposing && !child.Control.IsDisposed &&
                    !child.Control.InputTransparent && child.Control.CanDraw)
                {
                    var transformed = child.Control.ApplyTransforms(child.HitRect); //instead of HitRect
                    inside = transformed.ContainsInclusive(point.X,
                        point.Y); // || child.Control == Superview.FocusedChild;

                    if (inside)
                    {
                        var stop = 1;
                    }
                }

                return inside;
            }

            return base.IsGestureForChild(child, point);
        }

        public override void ApplyBindingContext()
        {
            UpdateRowColumnBindingContexts();

            base.ApplyBindingContext();
        }

        //todo use rendering tree for templated!!
        //protected override void OnParentVisibilityChanged(bool newvalue)
        //{

        //    base.OnParentVisibilityChanged(newvalue);
        //}


        public override bool ShouldInvalidateByChildren
        {
            get
            {
                if (Type == LayoutType.Grid)
                {
                    return true; //we need this to eventually recalculate spans
                }

                if (!IsTemplated && IsStack)
                    return true;

                // do not invalidate if template didnt change from last time?
                // NOPE template could be the same but size could be different!
                //if (IsTemplated && _measuredNewTemplates)
                //{
                //    return false;
                //}

                return base.ShouldInvalidateByChildren;
            }
        }

        bool _measuredNewTemplates;

        /// <summary>
        /// Will be called by views adapter upot succsessfull execution of InitializeTemplates.
        /// When using InitializeTemplatesInBackground this is your callbacl to wait for.  
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// Flag to expand viewport temporarily for initial drawing to pre-create cells with different heights
        /// </summary>
        private bool _isInitialDrawingFromFreshSource = false;
        private int _initialDrawFrameCount = 0;

        public virtual void OnTemplatesAvailable()
        {
            _measuredNewTemplates = false;
            NeedMeasure = true;
            InvalidateParent();
            
            // Enable initial drawing mode to pre-create more cells
            _isInitialDrawingFromFreshSource = true;
            _initialDrawFrameCount = 0;
        }

        protected override ScaledSize SetMeasured(float width, float height, bool widthCut, bool heightCut, float scale)
        {
            _measuredNewTemplates = true;

            return base.SetMeasured(width, height, widthCut, heightCut, scale);
        }


        //bindable property RecyclingTemplate
        public static readonly BindableProperty RecyclingTemplateProperty = BindableProperty.Create(
            nameof(RecyclingTemplate),
            typeof(RecyclingTemplate),
            typeof(SkiaLayout),
            RecyclingTemplate.Enabled,
            propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// In case of ItemsSource+ItemTemplate set will define should we reuse already created views: hidden items views will be reused for currently visible items on screen.
        /// If set to true inside a SkiaScrollLooped will cause it to redraw constantly even when idle because of the looped scroll mechanics.
        /// </summary>
        public RecyclingTemplate RecyclingTemplate
        {
            get { return (RecyclingTemplate)GetValue(RecyclingTemplateProperty); }
            set { SetValue(RecyclingTemplateProperty, value); }
        }

        //protected override void AdaptCachedLayout(SKRect destination, float scale)
        //{
        //    base.AdaptCachedLayout(destination, scale);

        //    if (Parent == null || Parent is not IDefinesViewport)
        //    {
        //        RenderingViewport = new(DrawingRect);
        //    }
        //}

        //protected override void OnLayoutChanged()
        //{
        //    base.OnLayoutChanged();

        //    if (Parent == null || Parent is not IDefinesViewport)
        //    {
        //        RenderingViewport = new(DrawingRect);
        //    }

        //}

        public static readonly BindableProperty TemplatedHeaderProperty = BindableProperty.Create(
            nameof(TemplatedHeader), typeof(SkiaControl),
            typeof(SkiaControl), null, propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// Kind of BindableLayout.DrawnTemplate
        /// </summary>
        public SkiaControl TemplatedHeader
        {
            get { return (SkiaControl)GetValue(TemplatedHeaderProperty); }
            set { SetValue(TemplatedHeaderProperty, value); }
        }

        public static readonly BindableProperty TemplatedFooterProperty = BindableProperty.Create(
            nameof(TemplatedFooter), typeof(SkiaControl),
            typeof(SkiaControl), null, propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// Kind of BindableLayout.DrawnTemplate
        /// </summary>
        public SkiaControl TemplatedFooter
        {
            get { return (SkiaControl)GetValue(TemplatedFooterProperty); }
            set { SetValue(TemplatedFooterProperty, value); }
        }


        public override bool IsTemplated =>
            ((this.ItemTemplate != null || ItemTemplateType != null) && this.ItemsSource != null);

        public SKRect GetChildRect(int index)
        {
            ISkiaControl child = null;
            if (IsTemplated)
            {
                throw new Exception("Cannot get child rect for a templated view");

                return SKRect.Empty;
            }

            return GetChildRect(child);
        }

        public SKRect GetChildRect(ISkiaControl child)
        {
            if (IsTemplated)
            {
                throw new Exception("Cannot get child rect for a templated view");
            }
            else
            {
                if (child != null)
                    return child.Destination;
            }

            return SKRect.Empty;
        }

        /*
        /// <summary>
        /// TODO for templated stacks this is not optimized to handle cell size
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SkiaControl GetChildAt(int index)
        {
            if (IsTemplated)
            {
                //todo
                bool useOneTemplate = IsTemplated && //ItemSizingStrategy == ItemSizingStrategy.MeasureFirstItem &&
                                      RecyclingTemplate != RecyclingTemplate.Disabled;

                SkiaControl template = null;
                if (useOneTemplate)
                {
                    template = ChildrenFactory.GetTemplateInstance();
                }

                var child = ChildrenFactory.GetViewForIndex(index, template, 0, true);

                return child;
            }

            return GetOrderedSubviews()[index];
        }
        */

        public SkiaControl GetChildAt(float x, float y)
        {
            if (IsTemplated)
            {
                //todo 
                throw new Exception("Cannot get child at for a templated view");
            }
            else
            {
                foreach (var child in GetUnorderedSubviews())
                {
                    var rect = GetChildRect(child);
                    if (rect.ContainsInclusive(x, y))
                    {
                        return child as SkiaControl;
                    }
                }
            }

            return null;
        }



        public SkiaLayout()
        {
            ChildrenFactory = new(this);

            PostponeInvalidation(nameof(ApplyItemsSource), ApplyItemsSource);
            //OnItemSourceChanged();
        }

        #region PROPERTIES

        public static readonly BindableProperty VirtualizationProperty = BindableProperty.Create(
            nameof(Virtualisation),
            typeof(VirtualisationType),
            typeof(SkiaLayout),
            VirtualisationType.Enabled,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Default is Enabled, children get the visible viewport area for rendering and can virtualize.
        /// </summary>
        public VirtualisationType Virtualisation
        {
            get { return (VirtualisationType)GetValue(VirtualizationProperty); }
            set { SetValue(VirtualizationProperty, value); }
        }

        public static readonly BindableProperty VirtualisationInflatedProperty = BindableProperty.Create(
            nameof(VirtualisationInflated),
            typeof(double),
            typeof(SkiaLayout),
            0.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// How much of the hidden content out of visible bounds should be considered visible for rendering,
        /// default is 0 pts.
        /// Basically how much should be expand in every direction of the visible area prior to checking if content falls
        /// into its bounds for rendering controlled with Virtualisation.
        /// </summary>
        public double VirtualisationInflated
        {
            get { return (double)GetValue(VirtualisationInflatedProperty); }
            set { SetValue(VirtualisationInflatedProperty, value); }
        }


        public static readonly BindableProperty RecyclingBufferProperty = BindableProperty.Create(
            nameof(RecyclingBuffer),
            typeof(double),
            typeof(SkiaLayout),
            500.0);

        /// <summary>
        /// Extra buffer zone for avoiding recycling  
        /// Default is 500pts - increase for less jaggy scroll, decrease for more memory efficiency
        /// </summary>
        public double RecyclingBuffer
        {
            get { return (double)GetValue(RecyclingBufferProperty); }
            set { SetValue(RecyclingBufferProperty, value); }
        }

        #endregion

        #region STACK ROW/GRID

        protected List<ISkiaControl> ChildrenGrid { get; set; }


        public void BreakLine()
        {
            LineBreaks.Add(Views.Count);
        }

        protected List<int> LineBreaks = new List<int>();


        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            if (DelegateGetOnScreenVisibleArea != null)
            {
                return DelegateGetOnScreenVisibleArea(inflateByPixels);
            }

            var onscreen = base.GetOnScreenVisibleArea(context, inflateByPixels);

            if (Virtualisation == VirtualisationType.Managed)
            {
                // Check if we have a plane-specific viewport for managed virtualization
                var planeSpecificViewport = context.GetArgument(nameof(ContextArguments.PlaneViewport)) as SKRect?;
                if (planeSpecificViewport.HasValue)
                {
                    return ScaledRect.FromPixels(planeSpecificViewport.Value, RenderingScale);
                }
                
                return onscreen;
            }

            var visible = SKRect.Intersect(onscreen.Pixels, context.Destination);

            return ScaledRect.FromPixels(visible, RenderingScale);
        }

        public override void DrawRenderObject(DrawingContext context, CachedObject cache)
        {
            var visibleArea = GetOnScreenVisibleArea(context);

            base.DrawRenderObject(context.WithDestination(visibleArea.Pixels), cache);
        }

        protected SkiaControl _emptyView;

        protected virtual void SetupViews()
        {
            if (EmptyView != _emptyView)
            {
                RemoveSubView(_emptyView);
                _emptyView?.Dispose();

                if (EmptyView != null)
                {
                    _emptyView = EmptyView;
                    CheckAndSetupIfEmpty();
                    AddSubView(_emptyView);
                }

                Update();
            }
        }

        private bool _IsEmpty;

        public bool IsEmpty
        {
            get { return _IsEmpty; }
            set
            {
                if (_IsEmpty != value)
                {
                    _IsEmpty = value;
                    OnPropertyChanged();
                    IsEmptyChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool> IsEmptyChanged;


        protected virtual void ApplyIsEmpty(bool value)
        {
            IsEmpty = value;

            if (_emptyView != null)
            {
                _emptyView.IsVisible = value;
            }
        }

        protected virtual bool CheckAndSetupIfEmpty()
        {
            var value = false;

            if (ItemTemplate != null)
            {
                value = ItemsSource == null || ItemsSource.Count == 0;
            }
            else
            {
                value = this.ChildrenFactory.GetChildrenCount() == 0;
            }

            ApplyIsEmpty(value);

            return value;
        }

        public override string DebugString
        {
            get
            {
                var output =
                    $"{this.GetType().Name} {Type} Tag: `{Tag}`, {MeasuredSize.Pixels.Width:0}x{MeasuredSize.Pixels.Height:0}, ";

                if (IsTemplated && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                {
                    output += $"measured {LastMeasuredIndex + 1}, ";
                }

                if (IsTemplated || RenderTree == null)
                    return output + ChildrenFactory.GetDebugInfo();

                return output +
                       $"visible {RenderTree.Count}, skipped {ChildrenFactory.GetChildrenCount() - RenderTree.Count}, total {ChildrenFactory.GetChildrenCount()}";
            }
        }

        public ViewsAdapter ChildrenFactory { get; protected set; }


        public static readonly BindableProperty SplitProperty = BindableProperty.Create(
            nameof(Split),
            typeof(int),
            typeof(SkiaLayout),
            0,
            propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// For Wrap number of columns/rows to split into, If 0 will use auto, if 1+ will have 1+ columns.
        /// </summary>
        public int Split
        {
            get { return (int)GetValue(SplitProperty); }
            set { SetValue(SplitProperty, value); }
        }

        public static readonly BindableProperty SplitAlignProperty = BindableProperty.Create(
            nameof(SplitAlign),
            typeof(bool),
            typeof(SkiaLayout),
            true, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// Whether should keep same column width among rows
        /// </summary>
        public bool SplitAlign
        {
            get { return (bool)GetValue(SplitAlignProperty); }
            set { SetValue(SplitAlignProperty, value); }
        }

        public static readonly BindableProperty SplitSpaceProperty = BindableProperty.Create(
            nameof(SplitSpace),
            typeof(SpaceDistribution),
            typeof(SkiaLayout),
            SpaceDistribution.Auto,
            propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// How to distribute free space between children TODO
        /// </summary>
        public SpaceDistribution SplitSpace
        {
            get { return (SpaceDistribution)GetValue(SplitSpaceProperty); }
            set { SetValue(SplitSpaceProperty, value); }
        }

        public static readonly BindableProperty DynamicColumnsProperty = BindableProperty.Create(
            nameof(DynamicColumns),
            typeof(bool),
            typeof(SkiaLayout),
            false, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// If true, will not create additional columns to match SplitMax if there are less real columns, and take additional space for drawing
        /// </summary>
        public bool DynamicColumns
        {
            get { return (bool)GetValue(DynamicColumnsProperty); }
            set { SetValue(DynamicColumnsProperty, value); }
        }

        #endregion

        #region RENDERiNG

        public virtual void OnViewportWasChanged(ScaledRect viewport)
        {
            //RenderingViewport = new(viewport.Pixels);

            //cells will get OnScrolled
            ViewportWasChanged = true;
        }

        protected bool ViewportWasChanged { get; set; }

        protected virtual bool DrawChild(DrawingContext ctx, ISkiaControl child)
        {
            if (child == null || child.IsDisposed || child.IsDisposing)
            {
                return false;
            }

            child.OptionalOnBeforeDrawing(); //could set IsVisible or whatever inside

            if (!child.CanDraw)
                return false; //child set himself invisible

            if (ViewportWasChanged)
            {
                //if (child is IInsideViewport viewport)
                //{
                //    var intersection = SKRect.Intersect(_viewport.Pixels, dest);
                //    viewport.OnViewportWasChanged(ScaledRect.FromPixels(intersection, RenderingScale));
                //}

                if (child is ISkiaCell cell)
                {
                    cell.OnScrolled();

                    //Task.Run(() =>
                    //{
                    //    cell.OnScrolled();
                    //}).ConfigureAwait(false);
                }
            }

            child.Render(ctx);

            return true;
        }


        //protected void Build()
        //{
        //	if (AvailableDestination != SKRect.Empty)
        //	{
        //		Measure(AvailableDestination.Width, AvailableDestination.Height);
        //	}
        //	Update();
        //}

        public override void SetChildren(IEnumerable<SkiaControl> views)
        {
            base.SetChildren(views);

            Invalidate();
        }


        protected override void OnMeasured()
        {
            base.OnMeasured();

            _measuredStamp++;
        }

        public override void InvalidateInternal()
        {
            templatesInvalidated = true;

            base.InvalidateInternal();
        }

        protected bool templatesInvalidated;

        public override void InvalidateWithChildren()
        {
            if (ChildrenFactory == null)
                return;

            if (IsTemplated)
            {
                //todo cannot really spam this if we find out we need this need to find some
                //more optimizations to minimize the lag from recreating templates
                //ChildrenFactory.TemplatesInvalidated = true;

                OnItemSourceChanged();
            }

            base.InvalidateWithChildren();
        }

        //protected override void OnChildAdded(SkiaControl child)
        //{
        //    base.OnChildAdded(child);

        //    needUpdateViews = true;
        //}

        //protected override void OnChildRemoved(SkiaControl child)
        //{
        //    base.OnChildRemoved(child);

        //    needUpdateViews = true;
        //}

        public override void InvalidateViewsList()
        {
            base.InvalidateViewsList(); //_orderedChildren = null;

            ActualizeSubviews();
        }

        public virtual void ActualizeSubviews()
        {
            needUpdateViews = false;

            ChildrenFactory?.UpdateViews();
        }

        bool needUpdateViews;

        protected virtual int GetTemplatesPoolPrefill()
        {
            if (RecyclingTemplate == RecyclingTemplate.Disabled)
            {
                return ItemsSource.Count;
            }

            var mult = 1;
            if (Split > 0)
                mult = Split;

            return mult * 3;
        }

        protected virtual int GetTemplatesPoolLimit()
        {
            if (ItemTemplatePoolSize > 0)
                return ItemTemplatePoolSize;

            if (ItemsSource == null)
                return 0;

            var mult = 1;
            if (Split > 0)
                mult = Split;

            return ItemsSource.Count + mult * 2;
        }

        public override void OnChildrenChanged()
        {
            base.OnChildrenChanged();

            if (!NeedMeasure && Type != LayoutType.Absolute)
            {
                Invalidate();
            }
            else
            {
                Update();
            }
        }

        public override void Invalidate()
        {
            base.Invalidate();

            Update();
        }


        SemaphoreSlim semaphoreItemTemplates = new(1);

        protected async Task CreateTemplatesInBackground()
        {
            await semaphoreItemTemplates.WaitAsync();
            try
            {
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            finally
            {
                semaphoreItemTemplates.Release();
            }
        }


        public override ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
        {
            var childrenCount = ChildrenFactory.GetChildrenCount(); // Cache count
            if (childrenCount > 0)
            {
                if (!IsTemplated)
                {
                    var children = GetUnorderedSubviews();
                    return MeasureContent(children, rectForChildrenPixels, scale);
                }

                var maxHeight = 0.0f;
                var maxWidth = 0.0f;

                bool standalone = false;
                if (!ChildrenFactory.TemplatesAvailable)
                {
                    return ScaledSize.CreateEmpty(scale);
                }
                else
                {
                    if (this.MeasureItemsStrategy == MeasuringStrategy.MeasureFirst)
                    {
                        standalone = true;
                        var template = ChildrenFactory.GetTemplateInstance();
                        var child = ChildrenFactory.GetViewForIndex(0, template, 0, true);

                        var measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height,
                            scale);
                        if (!measured.IsEmpty)
                        {
                            // FastMeasurement: skip FILL checks for performance
                            if (FastMeasurement)
                            {
                                if (measured.Pixels.Width > maxWidth)
                                    maxWidth = measured.Pixels.Width;
                                if (measured.Pixels.Height > maxHeight)
                                    maxHeight = measured.Pixels.Height;
                            }
                            else
                            {
                                if (measured.Pixels.Width > maxWidth
                                    && child.HorizontalOptions.Alignment != LayoutAlignment.Fill)
                                    maxWidth = measured.Pixels.Width;

                                if (measured.Pixels.Height > maxHeight
                                    && child.VerticalOptions.Alignment != LayoutAlignment.Fill)
                                    maxHeight = measured.Pixels.Height;
                            }
                        }

                        ChildrenFactory.ReleaseTemplateInstance(template);
                    }
                    else if (this.MeasureItemsStrategy == MeasuringStrategy.MeasureAll
                             || RecyclingTemplate == RecyclingTemplate.Disabled)
                    {
                        // Optimize: only allocate collection if templated
                        List<SkiaControl> cellsToRelease = null;
                        if (IsTemplated)
                            cellsToRelease = new List<SkiaControl>();

                        try
                        {
                            for (int index = 0; index < childrenCount; index++)
                            {
                                var child = ChildrenFactory.GetViewForIndex(index, null, 0, true);
                                if (IsTemplated) cellsToRelease?.Add(child);

                                if (child == null)
                                {
                                    break; //unexpected but..
                                }

                                var measured = MeasureChild(child, rectForChildrenPixels.Width,
                                    rectForChildrenPixels.Height, scale);
                                if (!measured.IsEmpty)
                                {
                                    // FastMeasurement: skip FILL checks for performance
                                    if (true)//FastMeasurement)
                                    {
                                        if (measured.Pixels.Width > maxWidth)
                                            maxWidth = measured.Pixels.Width;
                                        if (measured.Pixels.Height > maxHeight)
                                            maxHeight = measured.Pixels.Height;
                                    }
                                    else
                                    {
                                        if (measured.Pixels.Width > maxWidth &&
                                            child.HorizontalOptions.Alignment != LayoutAlignment.Fill)
                                            maxWidth = measured.Pixels.Width;
                                        if (measured.Pixels.Height > maxHeight &&
                                            child.VerticalOptions.Alignment != LayoutAlignment.Fill)
                                            maxHeight = measured.Pixels.Height;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (cellsToRelease?.Count > 0)
                            {
                                foreach (var cell in cellsToRelease)
                                {
                                    ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                                }
                            }
                        }
                    }

                    return ScaledSize.FromPixels(maxWidth, maxHeight, scale);
                }
            }
            //empty container
            else if (NeedAutoHeight || NeedAutoWidth)
            {
                return ScaledSize.CreateEmpty(scale);
                //return SetMeasured(0, 0, scale);
            }


            return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
        }

        protected object lockMeasureLayout = new();

        public virtual ScaledSize MeasureLayout(MeasureRequest request, bool force)
        {
            //until we implement 2-threads rendering this is needed for ImageDoubleBuffered cache rendering
            if (IsDisposing || IsDisposed)
                return ScaledSize.Default;

            lock (lockMeasureLayout)
            {
                _measuredNewTemplates = false;

                var constraints = GetMeasuringConstraints(request);

                GridStructureMeasured = null;

                if (!CheckAndSetupIfEmpty())
                {
                    switch (Type)
                    {
                        case LayoutType.Absolute:
                            ContentSize = MeasureAbsolute(constraints.Content, request.Scale);
                            break;

                        case LayoutType.Grid:

                            ContentSize = MeasureGrid(constraints.Content, request.Scale);
                            break;

                        case LayoutType.Column:
                        case LayoutType.Row:
                            if (IsTemplated) //fix threads conflict when templates are initialized in background thread
                            {
                                var canMeasureTemplates = ChildrenFactory.TemplatesAvailable || force;

                                if (!canMeasureTemplates)
                                    return ScaledSize.CreateEmpty(request.Scale);

                                if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                                {
                                    ContentSize = MeasureList(constraints.Content, request.Scale);
                                    break;
                                }

                                ContentSize = MeasureStackTemplated(constraints.Content, request.Scale);
                            }
                            else
                            {
                                ContentSize = MeasureStackNonTemplated(constraints.Content, request.Scale);
                            }
                                
                            break;

                        case LayoutType.Wrap:
                            if (IsTemplated) //fix threads conflict when templates are initialized in background thread
                            {
                                var canMeasureTemplates = ChildrenFactory.TemplatesAvailable || force;

                                if (!canMeasureTemplates)
                                    return ScaledSize.CreateEmpty(request.Scale);
                            }

                            ContentSize = MeasureWrap(constraints.Content, request.Scale);
                            break;

                        default:
                            ContentSize = ScaledSize.FromPixels(constraints.Content.Width, constraints.Content.Height,
                                request.Scale);
                            break;
                    }
                }
                else
                {
                    ContentSize = MeasureAbsoluteBase(constraints.Content, request.Scale);
                }

                return SetMeasuredAdaptToContentSize(constraints, request.Scale);
            }
        }

        protected readonly object _lockTemplates = new object();

        /// <summary>
        /// If you call this while measurement is in process (IsMeasuring==True) will return last measured value.
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="heightConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            //background measuring or invisible
            if (IsMeasuring //|| !CanDraw
                || (IsTemplated && ChildrenFactory.TemplatesBusy))
            {
                NeedRemeasuring = true;
                return MeasuredSize;
            }

            if (!IsVisible)
            {
                return SetMeasuredAsEmpty(scale);
            }

            try
            {
                lock (LockMeasure)
                {
                    IsMeasuring = true;

                    InitializeDefaultContent();

                    var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);
                    //this optimization WAS nice (byebye) but not working for Grid inside a recycled cell where request is same but height is different 
                    //if (request.IsSame)
                    //{
                    //    return MeasuredSize;
                    //}

                    if (request.WidthRequest == 0 || request.HeightRequest == 0)
                    {
                        InvalidateCacheWithPrevious();
                        return SetMeasuredAsEmpty(request.Scale);
                    }

                    if (IsTemplated)
                    {
                        //this is used for initial views creation or a rare case when we manually invalidated templates
                        lock (_lockTemplates)
                        {
                            if (ChildrenFactory.TemplatesInvalidated
                                && !ChildrenFactory.TemplesInvalidating)
                            {
                                ChildrenFactory.TemplesInvalidating = true;
                                ApplyNewItemsSource = false;
                                ChildrenFactory.InitializeTemplates(
                                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
                                    CreateContentFromTemplate, ItemsSource,
                                    GetTemplatesPoolLimit(),
                                    GetTemplatesPoolPrefill());
                            }
                        }
                    }
                    else
                    {
                        if (needUpdateViews)
                        {
                            ActualizeSubviews();
                        }
                    }

                    return MeasureLayout(request, false);
                } //end lock
            }
            catch (Exception e)
            {
                Super.Log(e);
                return MeasuredSize;
            }
            finally
            {
                IsMeasuring = false;

                //LockUpdate(false); never enable this here, breaks stuff
            }
        }

        public override void ApplyMeasureResult()
        {
            if (StackStructureMeasured != null)
            {
                var kill = StackStructure;
                StackStructure = StackStructureMeasured;
                StackStructureMeasured = null;
                if (kill != StackStructure)
                    kill?.Clear();
                CheckAndSetupIfEmpty();
            }

            if (GridStructureMeasured != null)
            {
                GridStructure = GridStructureMeasured;
                GridStructureMeasured = null;
                CheckAndSetupIfEmpty();
            }

            base.ApplyMeasureResult();
        }


        /// <summary>
        /// Will use this when drawing
        /// </summary>
        protected HashSet<SkiaControl> InvalidatedChildrenInternal { get; set; }

        protected override void Draw(DrawingContext context)
        {
            if (IsDisposed || IsDisposing)
                return;

            InvalidatedChildrenInternal = InvalidatedChildren;
            InvalidatedChildren = new();

            ApplyMeasureResult();

            base.Draw(context); //will call DrawStack

            ViewportWasChanged = false;
        }

        /// <summary>
        /// Will be modified by InvalidateByChild
        /// </summary>
        protected HashSet<SkiaControl> InvalidatedChildren { get; set; } = new();

        public override void InvalidateByChild(SkiaControl child)
        {
            InvalidatedChildren.Add(child);

            if (!NeedAutoSize && (child.NeedAutoSize || IsTemplated))
            {
                UpdateByChild(child); //simple update
                return;
            }

            base.InvalidateByChild(child);  //calling Invalidate
        }

        bool _trackWasDrawn;


        protected override void Paint(DrawingContext ctx)
        {
            if (ctx.Destination.Width == 0 || ctx.Destination.Height == 0)
                return;

            LockUpdate(true);

            if (Type == LayoutType.Grid || IsStack)
            {
                SetupRenderingWithComposition(ctx);
            }
            else
            {
                DirtyChildrenTracker.Clear();
            }

            base.Paint(ctx);

            var rectForChildren = ContractPixelsRect(ctx.Destination, ctx.Scale, Padding);

            var drawnChildrenCount = 0;

            //placeholder for empty
            if (_emptyView != null && _emptyView.IsVisible)
            {
                drawnChildrenCount = DrawViews(ctx.WithDestination(rectForChildren));
            }
            else if (Type == LayoutType.Grid) //todo add optimization for OptimizeRenderingViewport
            {
                drawnChildrenCount = DrawChildrenGrid(ctx.WithDestination(rectForChildren));
            }
            else
                //stacklayout
            if (IsStack)
            {
                var structure = LatestStackStructure;
                if (structure != null && structure.GetCount() > 0)
                {
                    if (IsTemplated && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                    {
                        drawnChildrenCount = DrawList(ctx.WithDestination(rectForChildren), structure);
                    }
                    else
                    {
                        drawnChildrenCount = DrawStack(ctx.WithDestination(rectForChildren), structure);
                    }
                }
            }
            else
                //absolute layout
            {
                drawnChildrenCount = DrawViews(ctx.WithDestination(rectForChildren));
            }

            ApplyIsEmpty(drawnChildrenCount == 0);

            if (!_trackWasDrawn && LayoutReady)
            {
                _trackWasDrawn = true;
                OnAppeared();
            }

            LockUpdate(false);
        }

        public override void OnDisposing()
        {
            IsEmptyChanged = null;

            ChildrenFactory?.Dispose();

            ClearChildren();

            DirtyChildrenTracker.Clear();

            DirtyChildrenInternal.Clear();

            StackStructure?.Clear();
            StackStructureMeasured?.Clear();

            OnGestures = null;

            base.OnDisposing();
        }


        /// <summary>
        /// Find intersections between changed children and DrawingRect,
        /// add intersecting ones to DirtyChildrenInternal and set IsRenderingWithComposition = true if any.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="destination"></param>
        void SetupRenderingWithComposition(DrawingContext ctx)
        {
            if (UsingCacheType == SkiaCacheType.ImageComposite)
            {
                DirtyChildrenInternal.Clear();

                var previousCache = RenderObjectPrevious;

                if (previousCache != null && ctx.Context.IsRecycled) //not the first draw
                {
                    IsRenderingWithComposition = true;

                    var offset = new SKPoint(this.DrawingRect.Left - previousCache.Bounds.Left,
                        DrawingRect.Top - previousCache.Bounds.Top);

                    //Super.Log($"[ImageComposite] {Tag} drawing cached at {offset}  {DrawingRect}");


                    // Add more children that are not already added but intersect with the dirty regions
                    var asSpans = CollectionsMarshal.AsSpan(DirtyChildrenTracker.GetList());
                    foreach (var item in asSpans)
                    {
                        DirtyChildrenInternal.Add(item);
                    }

                    //make intersecting children dirty too
                    var asSpan = CollectionsMarshal.AsSpan(RenderTree);
                    foreach (var cell in asSpan)
                    {
                        if (!DirtyChildrenInternal.Contains(cell.Control) &&
                            DirtyChildrenInternal.Any(dirtyChild =>
                                dirtyChild.DirtyRegion.IntersectsWith(cell.Control.DirtyRegion)))
                        {
                            DirtyChildrenInternal.Add(cell.Control);
                        }
                    }

                    DirtyChildrenTracker.Clear();

                    var count = 0;
                    foreach (var dirtyChild in DirtyChildrenInternal)
                    {
                        var clip = dirtyChild.DirtyRegion;
                        clip.Offset(offset);

                        previousCache.Surface.Canvas.DrawRect(clip, PaintErase);

                        count++;
                    }
                }
                else
                {
                    //Super.Log($"[ImageComposite] {Tag} drawing new");

                    IsRenderingWithComposition = false;
                    DirtyChildrenTracker.Clear();
                }
            }
            else
            {
                DirtyChildrenTracker.Clear();
                IsRenderingWithComposition = false;
            }
        }


        protected override int DrawViews(DrawingContext context)
        {
            var drawn = 0;

            if (IsTemplated)
            {
                if (ChildrenFactory.TemplatesAvailable)
                {
                    using var children = ChildrenFactory.GetViewsIterator();
                    drawn = RenderViewsList(context, children);
                }

                if (drawn == 0 && _emptyView != null && _emptyView.IsVisible)
                {
                    var drawViews = new List<SkiaControl> { _emptyView };
                    RenderViewsList(context, drawViews);
                    return 0;
                }
            }
            else
            {
                drawn = base.DrawViews(context);

                if (drawn == 0 && _emptyView != null && _emptyView.IsVisible)
                {
                    var drawViews = new List<SkiaControl> { _emptyView };
                    RenderViewsList(context, drawViews);
                    return 0;
                }
            }

            return drawn;
        }

        /// <summary>
        /// Column/Row/Stack
        /// </summary>
        public bool IsStack
        {
            get { return this.Type == LayoutType.Column || Type == LayoutType.Row || Type == LayoutType.Wrap; }
        }

        public static readonly BindableProperty TypeProperty = BindableProperty.Create(nameof(Type), typeof(LayoutType),
            typeof(SkiaLayout),
            LayoutType.Absolute,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutType Type
        {
            get { return (LayoutType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        #endregion

        #region ItemsSource

        //public static readonly BindableProperty InitializeTemplatesInBackgroundDelayProperty = BindableProperty.Create(
        //    nameof(InitializeTemplatesInBackgroundDelay),
        //    typeof(int),
        //    typeof(SkiaLayout),
        //    0, propertyChanged: NeedUpdateItemsSource);

        ///// <summary>
        ///// Whether should initialize templates in background instead of blocking UI thread, default is 0.
        ///// Set your delay in Milliseconds to enable.
        ///// When this is enabled and RecyclingTemplate is Disabled will also measure the layout in background
        ///// when templates are available without blocking UI-tread.
        ///// After that OnTemplatesAvailable will be called on parent layout.
        ///// </summary>
        //public int InitializeTemplatesInBackgroundDelay
        //{
        //    get { return (int)GetValue(InitializeTemplatesInBackgroundDelayProperty); }
        //    set { SetValue(InitializeTemplatesInBackgroundDelayProperty, value); }
        //}

        public static readonly BindableProperty MeasureItemsStrategyProperty = BindableProperty.Create(
            nameof(MeasureItemsStrategy),
            typeof(MeasuringStrategy),
            typeof(SkiaLayout),
            MeasuringStrategy.MeasureFirst,
            propertyChanged: NeedUpdateItemsSource);

        public MeasuringStrategy MeasureItemsStrategy
        {
            get { return (MeasuringStrategy)GetValue(MeasureItemsStrategyProperty); }
            set { SetValue(MeasureItemsStrategyProperty, value); }
        }

        public static readonly BindableProperty ItemTemplatePoolSizeProperty = BindableProperty.Create(
            nameof(ItemTemplatePoolSize),
            typeof(int),
            typeof(SkiaLayout),
            -1, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// Default is -1, the number od template instances will not be less than data collection count. You can manually set to to a specific number to fill your viewport etc. Beware that if you set this to a number that will not be enough to fill the viewport binding contexts will contasntly be changing triggering screen update.
        /// </summary>
        public int ItemTemplatePoolSize
        {
            get { return (int)GetValue(ItemTemplatePoolSizeProperty); }
            set { SetValue(ItemTemplatePoolSizeProperty, value); }
        }

        public static readonly BindableProperty EmptyViewProperty = BindableProperty.Create(
            nameof(EmptyView),
            typeof(SkiaControl),
            typeof(SkiaLayout),
            null, propertyChanged: (b, o, n) =>
            {
                if (b is SkiaLayout control)
                {
                    control.SetupViews();
                }
            });

        public SkiaControl EmptyView
        {
            get { return (SkiaControl)GetValue(EmptyViewProperty); }
            set { SetValue(EmptyViewProperty, value); }
        }

        public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IList),
            typeof(SkiaLayout),
            null,
            //validateValue: (bo, v) => v is IList,
            propertyChanged: ItemsSourcePropertyChanged);

        public IList ItemsSource
        {
            get => (IList)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }


        private static void ItemsSourcePropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            var skiaControl = (SkiaLayout)bindable;

            if (oldvalue != null)
            {
                //if (oldvalue is IList oldList)
                //{
                //	foreach (var context in oldList)
                //	{
                //		//todo
                //	}
                //}

                if (oldvalue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= skiaControl.ItemsSourceCollectionChanged;
                }
            }


            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= skiaControl.ItemsSourceCollectionChanged;
                newCollection.CollectionChanged += skiaControl.ItemsSourceCollectionChanged;
            }

            skiaControl.OnItemSourceChanged();
        }

        private static void NeedUpdateItemsSource(BindableObject bindable, object oldvalue, object newvalue)
        {
            var skiaControl = (SkiaLayout)bindable;

#if TMP
            if (skiaControl.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                Super.Log("MeasureVisible is not supported for this property yet, soon.");
                skiaControl.MeasureItemsStrategy = MeasuringStrategy.MeasureFirst;
            }
#endif

            //skiaControl.PostponeInvalidation(nameof(UpdateItemsSource), skiaControl.UpdateItemsSource);
            //skiaControl.Update();

            skiaControl.ApplyItemsSource();
        }

        public override void OnItemTemplateChanged()
        {
            //PostponeInvalidation(nameof(OnItemSourceChanged), OnItemSourceChanged);
            ApplyItemsSource();
        }

        public bool ApplyNewItemsSource { get; set; }

        public virtual void OnItemSourceChanged()
        {
            ApplyItemsSource();
        }

        /// <summary>
        /// Invalidate and re-apply ItemsSource
        /// </summary>
        public virtual void ApplyItemsSource()
        {
            //if (!string.IsNullOrEmpty(Tag))
            //    Debug.WriteLine($"OnItemSourceChanged {Tag} {IsTemplated} {IsMeasuring}");
            if (!IsTemplated ||
                !BindingContextWasSet &&
                ItemsSource ==
                null) //do not create items from templates until the context was changed properly to avoid bugs
            {
                return;
            }

            this.ChildrenFactory.TemplatesInvalidated = true;
            ApplyNewItemsSource = true;
            Invalidate();
        }

        public virtual void ResetScroll()
        {
            if (Parent is IDefinesViewport viewport)
            {
                viewport.ScrollTo(0, 0, 0);
            }
        }

        /// <summary>
        /// Enhanced collection change handler with smart handling and fallback
        /// </summary>
        protected virtual void ItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (!IsTemplated)
                return;

            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine($"[SkiaLayout] {Tag} Collection changed: {args.Action}, " +
                                $"OldIndex: {args.OldStartingIndex}, NewIndex: {args.NewStartingIndex}, " +
                                $"OldCount: {args.OldItems?.Count ?? 0}, NewCount: {args.NewItems?.Count ?? 0}");
            }

            lock (LockMeasure)
            {
                // For very rapid changes, fall back to full reset
                if (ChildrenFactory.TemplatesBusy || ChildrenFactory.TemplesInvalidating)
                {
                    if (ViewsAdapter.LogEnabled)
                    {
                        Trace.WriteLine($"[SkiaLayout] {Tag} Templates busy, deferring change handling");
                    }

                    // Defer the change until templates are ready
                    PostponeInvalidation($"collection_change_{args.Action}", () =>
                    {
                        ItemsSourceCollectionChanged(sender, args); //re-enter
                    });
                    return;
                }

                ApplyNewItemsSource = false;

                //we could enter here from a different thread:
                SafeAction(() =>
                {
                    ChildrenFactory.InitializeTemplates(args, CreateContentFromTemplate, ItemsSource,
                        GetTemplatesPoolLimit(),
                        GetTemplatesPoolPrefill());

                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        ResetScroll();
                        Invalidate();
                    }
                });
                
            }
        }

        /// <summary>
        /// Force a full refresh of all cached views (useful for debugging)
        /// </summary>
        public void RefreshAllViews()
        {
            if (!IsTemplated)
                return;

            lock (LockMeasure)
            {
                ChildrenFactory.MarkAllViewsAsHidden();
                Update();
            }
        }

        /// <summary>
        /// Get debug information about cached views
        /// </summary>
        public string GetCacheDebugInfo()
        {
            if (!IsTemplated || ChildrenFactory == null)
                return "Not templated";

            var info = ChildrenFactory.GetDebugInfo();

            // Add validation info
            var isValid = true;
            var issues = new List<string>();

            try
            {
                // This is a simplified validation - the full version is in ViewsAdapter
                if (ItemsSource != null && ItemsSource.Count > 0)
                {
                    // Add any specific validation checks here
                }
            }
            catch (Exception e)
            {
                isValid = false;
                issues.Add($"Validation error: {e.Message}");
            }

            return $"{info}, Valid: {isValid}" +
                   (issues.Count > 0 ? $", Issues: {string.Join(", ", issues)}" : "");
        }

        /// <summary>
        /// Enhanced debug printing
        /// </summary>
        public override void OnPrintDebug()
        {
            base.OnPrintDebug();

            if (IsTemplated)
            {
                Trace.WriteLine($"[SkiaLayout] {Tag} Cache Debug: {GetCacheDebugInfo()}");
                ChildrenFactory.PrintDebugVisible();
            }
        }

        //public override void OnPrintDebug()
        //{
        //    Trace.WriteLine($"ViewsAdapter tpls: {ChildrenFactory.PoolSize}/{ChildrenFactory.PoolMaxSize}");
        //    if (IsTemplated)
        //    {
        //        ChildrenFactory.PrintDebugVisible();
        //    }
        //}

        #endregion

        protected override void OnLayoutReady()
        {
            base.OnLayoutReady();
        }

        public virtual void OnAppearing()
        {
        }

        public virtual void OnDisappearing()
        {
        }

        public virtual void OnAppeared()
        {
        }

        public virtual void OnDisappeared()
        {
        }

        public virtual void OnLoaded()
        {
        }

        public virtual ContainsPointResult GetVisibleChildIndexAt(SKPoint point)
        {
            //relative inside parent:
            var asSpan = CollectionsMarshal.AsSpan(RenderTree);
            for (int i = 0; i < asSpan.Length; i++)
            {
                var child = asSpan[i];

                if (child.Rect.ContainsInclusive(point))
                {
                    return new ContainsPointResult() { Index = child.Index, Area = child.Rect, Point = point };
                }
            }

            return ContainsPointResult.NotFound();
        }

        public ContainsPointResult GetChildIndexAt(SKPoint point)
        {
            //todo

            return ContainsPointResult.NotFound();
        }
    }
}
