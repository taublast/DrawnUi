namespace DrawnUi.Draw
{
    /*
    The concept:

    To create and move/transform the native view to where the skia placeholder is sitting.

    ANDROID + WINDOWS:
    To respond to fast skia updates (ex: while scrolling) we are forced to make a native view snapshot,
    hide the native view and draw the snapshot while we are animating.
    Basically we launch a restarting timer everytime the native view detects it needs to
    change its layout/tansform, we display the snapshot instead of native view until
    restarting timer expires (its relaunched every time we detect a layout/transform change),
    after that we can show the real maui view again.
    This snapshot activation mechanics is an option set by static property bool AnimateSnapshot.
    So inside scroll this property must be set to true to have the maui view scroll smoothly with
    other skia elements.

    OTHER PLATFORMS:
    Do not need a snapshot, maui view is moved/transformed directly.
     */

    [ContentProperty(nameof(Content))]
    public partial class SkiaMauiElement : SkiaControl
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (Element != null)
                Element.BindingContext = this.BindingContext;
        }

        public override bool NeedMeasure
        {
            get
            {
                return base.NeedMeasure;
            }
            set
            {
                //Debug.WriteLine($"[SkiaMauiElement] NeedMeasure {value}");
                base.NeedMeasure = value;
            }
        }

        public override ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
        {
            try
            {
                if (Element is IView mauiNativeView && Element.Handler != null && rectForChildrenPixels.Width > 0
                    && rectForChildrenPixels.Height > 0 && LayoutReady) //without layoutready we will get 0 as size from maui ty
                {
                    var measured = mauiNativeView.Measure(rectForChildrenPixels.Width / scale,
                        rectForChildrenPixels.Height / scale);

                    //Debug.WriteLine($"[SkiaMauiElement] Calling native measure for pixels {rectForChildrenPixels}");

                    //var arranged = view.Arrange(new Rect(0, 0, ptsWidth, ptsHeight));
                    return ScaledSize.FromUnits((float)measured.Width, (float)measured.Height, scale);
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return ScaledSize.Default;
        }

        /// <summary>
        /// Measure and arrange VisualElement using Maui methods
        /// </summary>
        /// <param name="ptsWidth"></param>
        /// <param name="ptsHeight"></param>
        /// <returns></returns>
        public virtual Size MeasureAndArrangeMauiElement(double ptsWidth, double ptsHeight)
        {
            //if you pass 0 to initial measure whatever you pass later will not fix the broken maui control afterwards
            //so never pass 0
            try
            {
                if (Element is IView view && Element.Handler != null && ptsWidth > 0 && ptsHeight > 0 && LayoutReady)
                {
                    //todo System.InvalidOperationException: Unable to convert DrawnUi.Controls.MauiEditor to Android.Views.View

                    //var width = ptsWidth - (this.Padding.Left + this.Padding.Right);
                    //var height = ptsHeight - (this.Padding.Top + this.Padding.Bottom);

                    var measured = view.Measure(ptsWidth, ptsHeight);

                    //Debug.WriteLine($"[SkiaMauiElement] Calling native measure {ptsWidth} x {ptsHeight}");

                    //var arranged = view.Arrange(new Rect(0, 0, ptsWidth, ptsHeight));
                    return measured;
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            try
            {
#if ANDROID
                return new(VisualTransformNative.Rect.Size.Width, VisualTransformNative.Rect.Size.Height);
#else
                return new(VisualTransformNative.Rect.Size.Width,
                    VisualTransformNative.Rect.Size.Height);
#endif
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return Size.Zero;
        }

        private object lockLayout = new();

        //public override ScaledSize Measure(float widthConstraint, float heightConstraint, float scale)
        //{
        //    if (IsDisposed || IsDisposing)
        //        return ScaledSize.Default;

        //    lock (lockLayout)
        //    {
        //        var bounds = base.Measure(widthConstraint, heightConstraint, scale);

        //        Debug.WriteLine($"[SkiaMauiElement] drawn measure to {bounds.Units.Width} x {bounds.Units.Height} pts from constraints {widthConstraint} x {heightConstraint}");

        //        if (Element is IView view && Element.Handler != null)
        //        {
        //            ContentSizeUnits = MeasureAndArrangeMauiElement(bounds.Units.Width, bounds.Units.Height);

        //            //Super.Log($"[Measure] ContentSizeUnits {ContentSizeUnits}");
        //        }

        //        return bounds;
        //    }
        //}
        protected Size ContentSizeUnits;
#if ANDROID || WINDOWS
        public SKImage GetSnapshot()
        {
            if (CachedBitmap != null)
            {
                return CachedBitmap.Snapshot();
            }

            return null;
        }

        public bool ShowSnapshot
        {
            get { return _showSnapshot; }
            protected set
            {
                if (_showSnapshot != value)
                {
                    _showSnapshot = value;
                    OnPropertyChanged();
                    //Super.Log($"ShowSnapshot] {value}");
                    Update();
                }
            }
        }

        bool _showSnapshot;
        public virtual void TakeSnapshot()
        {
            var width = (int)(ElementSize.X);
            var height = (int)(ElementSize.Y);

            //create snapshot container
            if (CachedBitmap == null || height != CacheSurfaceInfo.Height || width != CacheSurfaceInfo.Width)
            {
                CachedBitmap?.Dispose();
                CacheSurfaceInfo = new SKImageInfo(width, height);
                // hardware accelerated surface will not do here as we might use different gfx contexts with ui thread
                //normal one
                CachedBitmap = SKSurface.Create(CacheSurfaceInfo);
            }
            else
            {
                CachedBitmap.Canvas.Clear();
            }

            //flush native view into snapshot container

            TakeNativeSnapshot(CachedBitmap);
        }
#endif
        public SKSurface CachedBitmap { get; protected set; }
        public SKImageInfo CacheSurfaceInfo { get; set; }

        public override void OnWillDisposeWithChildren()
        {
            SubscribeToRenderingChain(false);

#if ONPLATFORM
            RemoveMauiElement(Element);
#endif

            base.OnWillDisposeWithChildren();
        }

        public override void OnDisposing()
        {
            CachedBitmap?.Dispose();

            CachedBitmap = null;

            base.OnDisposing();
        }

        protected RestartingTimer<object> TimerShowMauiView;

        protected void PostponeShowingNativeView(int ms)
        {
            if (!ShowSnapshot)
                return;

            if (Element != null && Element.Handler != null && TimerShowMauiView == null)
            {
                TimerShowMauiView = new(TimeSpan.FromMilliseconds(ms), (arg) =>
                {
                    if (Element != null)
                    {
#if ANDROID || WINDOWS
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Debug.WriteLine("** HIDE ** SNAPSHOT");
                            ShowSnapshot = false;
                            if (ContentSizeUnits == Size.Zero)
                            {
                                Invalidate();
                            }

                            SetNativeVisibility(true);
                        });
#else
                        SetNativeVisibility(true);
#endif
                    }
                });
                TimerShowMauiView.Start(null);
            }
            else
            {
                TimerShowMauiView.Restart(null);
            }
        }

        /// <summary>
        /// For HotReload
        /// </summary>
        /// <returns></returns>
        public override IReadOnlyList<IVisualTreeElement> GetVisualChildren()
        {
            var ret = new List<IVisualTreeElement>();
            if (Content != null)
            {
                ret.Add(Content);
            }

            return ret.AsReadOnly();
            ;
        }

        /// <summary>
        /// Prevent usage of subviews as we are using Content property for this control
        /// </summary>
        /// <param name="views"></param>
        public override void SetChildren(IEnumerable<SkiaControl> views)
        {
            //do not use subviews as we are using Content property for this control
            // so we just override not calling base
        }

        /// <summary>
        /// Prevent usage of subviews as we are using Content property for this control
        /// </summary>
        public override void OnChildAdded(SkiaControl child)
        {
            if (this.Views.Count > 0)
            {
                throw new Exception(
                    "SkiaMauiElement cannot have SkiaControl subviews. Please use Content property of type VisualElement.");
            }

            base.OnChildAdded(child);
        }

        public override void SuperViewChanged()
        {
            base.SuperViewChanged();

            MainThread.BeginInvokeOnMainThread(() => { SetContent(Content); });
        }

        /// <summary>
        /// Use Content property for direct access
        /// </summary>
        protected virtual void SetContent(VisualElement view)
        {
            if (view == null)
                return;

            if (Element == view)
            {
                //better update layout
#if ANDROID || WINDOWS
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ShowSnapshot = false;
                    if (ContentSizeUnits == Size.Zero)
                    {
                        Invalidate();
                    }

                    try
                    {
                    }
                    catch (global::System.Exception)
                    {
                        LayoutNativeView(Element);
                    }
                });
#else
                LayoutNativeView(Element);
#endif
                return;
            }

            if (Superview != null && Element != null)
            {
#if ONPLATFORM
                RemoveMauiElement(Element);
#endif
            }

            Element = null;

            if (Superview != null && view != null)
            {
                CreateMauiElement(view);
            }
        }

        protected override void OnFirstDrawn()
        {
            base.OnFirstDrawn();

            SetContent(this.Content);

        }

        public void CreateMauiElement(VisualElement element)
        {
            Element = element;

            Element.BindingContext = this.BindingContext;

#if ONPLATFORM
            SetupMauiElement(Element);
#endif
        }

        public bool NeedsLayoutNative { get; set; } = true;

        public void ApplyTransform(VisualTransform transform)
        {
            _transform = transform;

            if (Element == null || Element.Handler == null)
                return;

            var outRect = DrawingRect;
            //outRect.Intersect(Superview.DrawingRect);
            var native = transform.ToNative(outRect, RenderingScale);

            //Debug.WriteLine($"[ME] {native.IsVisible}");

            if (native != VisualTransformNative) // && !ShowSnapshot)
            {
                UpdateElementSize();
                VisualTransformNative = native;
                NeedsLayoutNative = true;
            }
        }

        public void Refresh()
        {
#if ONPLATFORM
            LayoutMauiElement();
#endif
            Update();
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            UpdateElementSize();
        }

        void UpdateElementSize()
        {
            ElementSize = new(DrawingRect.Width, DrawingRect.Height);

            Refresh(); // newly introduced
        }

        protected void SubscribeToRenderingChain(bool subscribe)
        {
            if (Superview != null)
            {
                if (subscribe)
                {
                    Superview.SubscribeToRenderingFinished(this);
                }
                else
                {
                    Superview.UsubscribeFromRenderingFinished(this);
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            Superview = context.Context.Superview;

            if (Content != null)
            {
                /*
                    if (Element == null) //was set in xaml before we attached to superview..
                    {
                        if (Superview != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                CreateMauiElement(Content);
                            });
                        }
                    }
                */

                //might become nut after CreateMauiElement
                if (Element != null)
                    SubscribeToRenderingChain(true);
            }

            base.Render(context);
        }

#if ANDROID || WINDOWS
        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);

            if (ShowSnapshot && CachedBitmap != null)
            {
                //Debug.WriteLine($"Drawing snapshot at {destination}");
                //todo apply transforms
                DrawSnapshot(ctx.Context.Canvas, ctx.Destination);
            }
        }

        protected bool SnapshotReady { get; set; }
        protected virtual void DrawSnapshot(SKCanvas canvas, SKRect destination)
        {
            if (SnapshotReady)
            {
                Debug.WriteLine($"Drawing snapshot");

                var point = new SKPoint(destination.Left, destination.Top);
                canvas.DrawSurface(CachedBitmap, point);

                if (IsNativeVisible)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Debug.WriteLine("** SHOW ** SNAPSHOT");

                        SetNativeVisibility(false);
                    });
                }
            }
        }
#endif

        public bool IsNativeVisible
        {
            get => _isNativeVisible;
            protected set
            {
                if (value == _isNativeVisible) return;
                _isNativeVisible = value;
                OnPropertyChanged();
                //Debug.WriteLine($"IsNativeVisible {value}");
            }
        }

        public bool WasRendered { get; protected set; }

        public void LayoutMauiElement(bool manageMainThread = true)
        {
            if (Element == null || Element.Handler == null || !NeedsLayoutNative)
            {
                if (ShowSnapshot)
                {
                    PostponeShowingNativeView(FreezeTimeMs);
                }
                else
                {
 /*
                    if (manageMainThread)
                    {
                        Tasks.StartDelayed(TimeSpan.FromMilliseconds(10),
                            () =>
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    Element.InvalidateMeasureNonVirtual(Microsoft.Maui.Controls.Internals
                                        .InvalidationTrigger.HorizontalOptionsChanged);
                                });
                            });
                    }
                    else
                    {
                        Element.InvalidateMeasureNonVirtual(Microsoft.Maui.Controls.Internals.InvalidationTrigger
                            .HorizontalOptionsChanged);
                    }
 */
                }

                return;
            }

            NeedsLayoutNative = false;

            void Act()
            {
                LayoutNativeView(Element);

#if ANDROID // || WINDOWS
                if (AnimateSnapshot && WasRendered && VisualTransformNative.IsVisible)
                {
                    if (!ShowSnapshot)
                    {
                        if (VisualTransformNative.IsVisible)
                        {
                            SnapshotReady = false;
                            TakeSnapshot();
                            ShowSnapshot = true;
                            LayoutMauiElement();
                        }
                    }
                    else
                        PostponeShowingNativeView(FreezeTimeMs); //to set ShowSnapshot to false;
                }
                else
                {
                    SetNativeVisibility(true);
                }
#else
                SetNativeVisibility(true);
#endif
            }

            if (!manageMainThread)
            {
                Act();
                return;
            }

#if ANDROID || WINDOWS
            MainThread.BeginInvokeOnMainThread(() => { Act(); });
#else
            Act();
#endif
        }

        public override void SetVisualTransform(VisualTransform transform)
        {
            //Super.Log($"SetVisualTransform {transform.Translation.X},{transform.Translation.Y} Tree: " + transform.Logs);

            ApplyTransform(transform);

#if ONPLATFORM
            LayoutMauiElement();
#endif
        }

        /// <summary>
        /// Maui Element to be rendered
        /// </summary>
        public VisualElement Element { get; protected set; }

        /// <summary>
        /// PIXELS, for faster checks
        /// </summary>
        public SKPoint ElementSize { get; set; }

        #region PROPERTIES

        public static readonly BindableProperty FreezeTimeMsProperty = BindableProperty.Create(
            nameof(FreezeTimeMs),
            typeof(int),
            typeof(SkiaMauiElement),
            250);

        public int FreezeTimeMs
        {
            get { return (int)GetValue(FreezeTimeMsProperty); }
            set { SetValue(FreezeTimeMsProperty, value); }
        }

        public static readonly BindableProperty AnimateSnapshotProperty = BindableProperty.Create(
            nameof(AnimateSnapshot),
            typeof(bool),
            typeof(SkiaMauiElement),
            false);

        /// <summary>
        /// Set to true if you are hosting the control inside a scroll or similar case
        /// where the control position/transforms are animated fast.
        /// </summary>
        public bool AnimateSnapshot
        {
            get { return (bool)GetValue(AnimateSnapshotProperty); }
            set { SetValue(AnimateSnapshotProperty, value); }
        }

        public static readonly BindableProperty ContentProperty = BindableProperty.Create(
            nameof(Content),
            typeof(VisualElement), typeof(SkiaMauiElement),
            null,
            propertyChanged: OnReplaceContent);

        private VisualTransform _transform;
        private bool _isNativeVisible = true;

        private static void OnReplaceContent(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaMauiElement control)
            {
                MainThread.BeginInvokeOnMainThread(() => { control.SetContent(newvalue as VisualElement); });
            }
        }

        /// <summary>
        /// Gets or sets the native MAUI VisualElement to embed within the DrawnUI canvas.
        /// This can be any MAUI control like WebView, MediaElement, Entry, Picker, etc.
        /// The control will be rendered using platform-specific optimizations.
        /// </summary>
        public VisualElement Content
        {
            get { return (VisualElement)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public VisualTransformNative VisualTransformNative { get; protected set; }

        #endregion

#if !ONPLATFORM

        public void NativeInvalidate()
        {
            throw new NotImplementedException();
        }

        protected virtual void LayoutNativeView(VisualElement element)
        {
            throw new NotImplementedException();
        }

        public virtual void SetNativeVisibility(bool state)
        {
            throw new NotImplementedException();
        }

        public bool ShowSnapshot => false;
#endif
    }
}
