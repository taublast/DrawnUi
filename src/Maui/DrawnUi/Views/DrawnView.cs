using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using DrawnUi.Infrastructure.Enums;

namespace DrawnUi.Views
{

    public partial class DrawnView : IDrawnBase, IAnimatorsManager, IVisualTreeElement
    {

        public void DumpLayersTree(VisualLayer node, string prefix = "", bool isLast = true, int level = 0)
        {
            if (node == null)
            {
                Super.Log("[DumpTree] root node is NULL");
                return;
            }

            string indent = new string(' ', level * 4);

            // Box drawing characters for tree structure
            string connector = isLast ? "└─ " : "├─ ";
            string childPrefix = isLast ? "   " : "│  ";

            // Print the current node with appropriate prefix
            var line =
                $"{indent}{prefix}{connector}{node.Control.GetType()} at {node.HitBoxWithTransforms.Pixels.Location} ({node.Children.Count})";

            if (!string.IsNullOrEmpty(node.Control.Tag))
            {
                line += $" \"{node.Control.Tag}\"";
            }

            if (node.Cached != SkiaCacheType.None)
            {
                line += $" [{node.Cached}]";
            }

            if (node.IsFrozen)
            {
                line += $" [Inside cache]";
            }


            Super.Log(line);

            // Process children
            for (int i = 0; i < node.Children.Count; i++)
            {
                bool childIsLast = (i == node.Children.Count - 1);
                DumpLayersTree(node.Children[i], prefix + childPrefix, childIsLast, level);
            }
        }


        public class DiagnosticData
        {
            public int LayersSaved { get; set; }
        }

        public DiagnosticData Diagnostics = new();

        public virtual void Update()
        {
            if (!Super.EnableRendering || IsDisposing || IsDisposed || UpdateLocks > 0)
            {
                return;
            }

#if ONPLATFORM
            UpdatePlatform();
#endif
        }

        public bool IsUsingHardwareAcceleration
        {
            get
            {
                if (!Super.CanUseHardwareAcceleration)
                    return false;

                return RenderingMode != RenderingModeType.Default;
            }
        }

        public bool NeedRedraw { get; set; }

        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (_isDirty != value)
                {
                    if (!value && UpdateMode == UpdateMode.Constant)
                    {
                        value = true;
                    }

                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        bool _isDirty;

        public virtual bool IsVisibleInViewTree()
        {
            return IsVisible; //this is used by animators, do not make this heavier!
        }

        public void TakeScreenShot(Action<SKImage> callback)
        {
            CallbackScreenshot = callback;
            Update();
        }

        public virtual void InvalidateByChild(SkiaControl child)
        {
            Invalidate();
        }

        public virtual void UpdateByChild(SkiaControl child)
        {
            Update();
        }

        /// <summary>
        /// For virtualization. For this method to be conditional we introduced the `pixelsDestination`
        /// parameter so that the Parent could return different visible areas upon context.
        /// Normally pass your current destination you are drawing into as this parameter. 
        /// </summary>
        /// <param name="pixelsDestination"></param>
        /// <param name="inflateByPixels"></param>
        /// <returns></returns>
        public virtual ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            var bounds = new SKRect(0 - inflateByPixels.X, 0 - inflateByPixels.Y,
                (int)(Width * RenderingScale + inflateByPixels.X), (int)(Height * RenderingScale + inflateByPixels.Y));

            return ScaledRect.FromPixels(bounds, (float)RenderingScale);
        }

        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {

            if (args.NewHandler == null || args.OldHandler != null)
            {
                DestroySkiaView();

#if ONPLATFORM
                DisposePlatform();
#endif
            }

            base.OnHandlerChanging(args);
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

#if ANDROID
            OnHandlerChangedInternal();
#endif

            if (Handler != null)
            {
                CreateSkiaView();

#if ONPLATFORM
                SetupRenderingLoop();
#endif
            }

            HandlerWasSet?.Invoke(this, Handler != null);
            //InvalidateChildren(); //need clear gfx cache
        }

        public event EventHandler<bool> HandlerWasSet;

        protected virtual void TakeScreenShotInternal(SKSurface surface)
        {
            if (surface != null)
            {
                //Console.WriteLine($"[DrawnView] TakeScreenShotInternal ------------------------- START");
                surface.Canvas.Flush();
                CallbackScreenshot?.Invoke(surface.Snapshot());
                //Console.WriteLine($"[DrawnView] TakeScreenShotInternal ------------------------- END");
            }

            CallbackScreenshot = null;
        }

        /// <summary>
        /// Postpone the action to be executed before the next frame being drawn. Exception-safe.
        /// </summary>
        /// <param name="action"></param>
        public void PostponeExecutionBeforeDraw(Action action)
        {
            ExecuteBeforeDraw.Enqueue(action);
        }

        /// <summary>
        ///Postpone the action to be executed after the current frame is drawn. Exception-safe.
        /// </summary>
        /// <param name="action"></param>
        public void PostponeExecutionAfterDraw(Action action)
        {
            ExecuteAfterDraw.Enqueue(action);
        }

        public Queue<Action> ExecuteBeforeDraw { get; } = new(1024);
        public Queue<Action> ExecuteAfterDraw { get; } = new(1024);
        protected Action<SKImage> CallbackScreenshot;
        //protected Dictionary<SkiaControl, VisualTreeChain> RenderingTrees = new(128);

        /// <summary>
        /// For native controls over Canvas to be notified after every of their position
        /// </summary>
        public Dictionary<SkiaControl, bool> RenderingSubscribers = new(128);

        /// <summary>
        /// SetVisualTransform will be called after every frame
        /// </summary>
        /// <param name="control"></param>
        public void SubscribeToRenderingFinished(SkiaControl control)
        {
            RenderingSubscribers.TryAdd(control, true);
        }

        public void UsubscribeFromRenderingFinished(SkiaControl control)
        {
            RenderingSubscribers.Remove(control, out _);
        }

        IReadOnlyList<IVisualTreeElement> IVisualTreeElement.GetVisualChildren()
        {
            return Views.Cast<IVisualTreeElement>()
                .ToList();
        }

        public void RegisterGestureListener(ISkiaGestureListener gestureListener)
        {
            if (gestureListener == null)
                return;

            gestureListener.GestureListenerRegistrationTime = DateTime.UtcNow;
            GestureListeners.Add(gestureListener);
        }

        public void UnregisterGestureListener(ISkiaGestureListener gestureListener)
        {
            if (gestureListener == null)
                return;

            gestureListener.GestureListenerRegistrationTime = null;
            GestureListeners.Remove(gestureListener);
        }

        protected object LockIterateListeners = new();

        /// <summary>
        /// Children we should check for touch hits
        /// </summary>
        public SortedGestureListeners GestureListeners { get; } = new();

        //public SortedSet<ISkiaGestureListener> GestureListeners { get; } = new(new DescendingZIndexGestureListenerComparer());

        public SKRect DrawingRect
        {
            get { return new SKRect(0, 0, (float)(Width * RenderingScale), (float)(Height * RenderingScale)); }
        }

        public void AddAnimator(ISkiaAnimator animator)
        {
            //lock (LockAnimatingControls)
            {
                animator.IsDeactivated = false;
                if (animator.Parent != null && !animator.Parent.IsVisible)
                {
                    animator.IsHiddenInViewTree = true;
                }
                else
                {
                    animator.IsHiddenInViewTree = false;
                }

                AnimatingControls.TryAdd(animator.Uid, animator);
            }


            Update();
        }

        public void RemoveAnimator(Guid uid)
        {
            //lock (LockAnimatingControls)
            {
                if (AnimatingControls.TryGetValue(uid, out var animator))
                {
                    animator.IsDeactivated = true;
                }
            }
        }

        /// <summary>
        /// Called by a control that whats to be constantly animated or doesn't anymore,
        /// so we know whether we should refresh canvas non-stop 
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="animating"></param>
        public bool RegisterAnimator(ISkiaAnimator animator)
        {
            AddAnimator(animator);

            return true;
        }

        public void UnregisterAnimator(Guid uid)
        {
            RemoveAnimator(uid);
        }

        public virtual IEnumerable<ISkiaAnimator> UnregisterAllAnimatorsByType(Type type)
        {
            //lock (LockAnimatingControls)
            {
                var ret = AnimatingControls.Where(x => x.Value.GetType() == type).Select(s => s.Value).ToArray();
                foreach (var animator in ret)
                {
                    try
                    {
                        animator.IsDeactivated = true;
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                return ret;
            }
        }

        public virtual IEnumerable<ISkiaAnimator> UnregisterAllAnimatorsByParent(SkiaControl parent)
        {
            //lock (LockAnimatingControls)
            {
                var ret = AnimatingControls.Values.Where(x => x.Parent == parent).ToArray();
                foreach (var animator in ret)
                {
                    try
                    {
                        animator.IsDeactivated = true;
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                return ret;
            }
        }

        /// <summary>
        /// TODO maybe use renderedNode tree
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public virtual IEnumerable<ISkiaAnimator> SetViewTreeVisibilityByParent(SkiaControl parent, bool state)
        {
            //lock (LockAnimatingControls)
            {
                var ret = AnimatingControls.Values.Where(x => x.Parent == parent).ToArray();
                foreach (var animator in ret)
                {
                    try
                    {
                        animator.IsHiddenInViewTree = !state;
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                return ret;
            }
        }

        public virtual IEnumerable<ISkiaAnimator> SetPauseStateOfAllAnimatorsByParent(SkiaControl parent, bool state)
        {
            //lock (LockAnimatingControls)
            {
                var ret = AnimatingControls.Values.Where(x => x.Parent == parent).ToArray();
                foreach (var animator in ret)
                {
                    try
                    {
                        if (state)
                            animator.Pause();
                        else
                            animator.Resume();
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                return ret;
            }
        }

        public int ExecutePostAnimators(DrawingContext context)
        {
            var executed = 0;

            try
            {
                if (PostAnimators.Count == 0 || IsDisposing || IsDisposed)
                    return executed;

                foreach (var skiaAnimation in PostAnimators)
                {
                    if (skiaAnimation.IsRunning && !skiaAnimation.IsPaused)
                    {
                        executed++;
                        var finished = skiaAnimation.TickFrame(context.Context.FrameTimeNanos);
                        if (skiaAnimation is ICanRenderOnCanvas renderer)
                        {
                            var renderedrawn = renderer.Render(context, this);
                        }

                        if (finished)
                        {
                            skiaAnimation.Stop();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return executed;
        }

        //protected object LockAnimatingControls = new();

        /// <summary>
        /// Executed after the rendering
        /// </summary>
        public List<IOverlayEffect> PostAnimators { get; } = new(128);

        List<Guid> _listRemoveAnimators = new(512);

        /// <summary>
        /// Tracking controls that what to be animated right now so we constantly refresh
        /// canvas until there is none left
        /// </summary>
        public ConcurrentDictionary<Guid, ISkiaAnimator> AnimatingControls { get; } = new();

        protected FrameTimeInterpolator FrameTimeInterpolator = new();
        public long mLastFrameTime { get; set; }

        protected int ExecuteAnimators(long frameTime)
        {
            var executed = 0;


            //lock (LockAnimatingControls)
            {
                try
                {
                    if (AnimatingControls.Count == 0)
                        return executed;

                    var nanos = frameTime;
                    //if (mLastFrameTime == 0)
                    //{
                    //    mLastFrameTime = frameTime;
                    //}
                    //else
                    //{
                    //    float deltaSeconds = (frameTime - mLastFrameTime) / 1_000_000_000.0f;
                    //    deltaSeconds = FrameTimeInterpolator.GetDeltaTime(deltaSeconds);
                    //    var deltaNanos = (long)(deltaSeconds * 1_000_000_000.0f);
                    //    nanos = mLastFrameTime + deltaNanos;
                    //    mLastFrameTime = frameTime;
                    //}

                    _listRemoveAnimators.Clear();

                    var animatorKeys = AnimatingControls.Keys.ToList();

                    //Debug.WriteLine($"Animators: {animatorKeys.Count}");

                    foreach (var key in animatorKeys)
                    {
                        if (AnimatingControls.TryGetValue(key, out var skiaAnimation))
                        {
                            if (skiaAnimation.IsDeactivated
                                || skiaAnimation.Parent != null && skiaAnimation.Parent.IsDisposed)
                            {
                                //Debug.WriteLine($"Animators: removing {key}");
                                _listRemoveAnimators.Add(key);
                                continue;
                            }

                            bool canPlay =
                                !skiaAnimation
                                    .IsHiddenInViewTree; //!(skiaAnimation.Parent != null && !skiaAnimation.Parent.IsVisibleInViewTree());

                            if (canPlay)
                            {
                                if (skiaAnimation.IsPaused)
                                    skiaAnimation.Resume(); //continue anim from current time instead of the old one

                                skiaAnimation.TickFrame(nanos);
                                executed++;
                            }
                            else
                            {
                                if (!skiaAnimation.IsPaused)
                                {
                                    skiaAnimation.Pause();
                                    //Debug.WriteLine($"ANIMATORS - PAUSED {key}");
                                }
                            }
                        }
                    }

                    foreach (var key in _listRemoveAnimators)
                    {
                        //Debug.WriteLine($"ANIMATORS - REMOVED {key}");
                        AnimatingControls.TryRemove(key, out _);
                    }
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }

                return executed;
            }
        }

        public virtual void OnCanvasViewChanged()
        {
            Update();
        }

        public ISkiaDrawable CanvasView
        {
            get => _canvasView;
            protected set
            {
                if (Equals(value, _canvasView)) return;
                _canvasView = value;
                FrameNumber = 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanvasFps));
                OnPropertyChanged(nameof(CanDraw));
                OnCanvasViewChanged();
            }
        }

        private bool _initialized;

        private void Init()
        {
            if (!_initialized)
            {
                _initialized = true;

                HorizontalOptions = LayoutOptions.Start;
                VerticalOptions = LayoutOptions.Start;
                Padding = new Thickness(0);

                SizeChanged += ViewSizeChanged;

                //bug this creates garbage on aandroid on every frame
                // DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
                InitFramework(true);
            }
        }

        private void OnMainDisplayInfoChanged(object sender, DisplayInfoChangedEventArgs e)
        {
            switch (e.DisplayInfo.Rotation)
            {
                case Microsoft.Maui.Devices.DisplayRotation.Rotation90:
                    DeviceRotation = 90;
                    break;
                case Microsoft.Maui.Devices.DisplayRotation.Rotation180:
                    DeviceRotation = 180;
                    break;
                case Microsoft.Maui.Devices.DisplayRotation.Rotation270:
                    DeviceRotation = 270;
                    break;
                case Microsoft.Maui.Devices.DisplayRotation.Rotation0:
                default:
                    DeviceRotation = 0;
                    break;
            }

            if (Parent != null)
                RenderingScale = (float)e.DisplayInfo.Density;
        }

        public void SetDeviceOrientation(int rotation)
        {
            DeviceRotation = rotation;
        }

        public event EventHandler<int> DeviceRotationChanged;

        public static readonly BindableProperty DisplayRotationProperty = BindableProperty.Create(
            nameof(DeviceRotation),
            typeof(int),
            typeof(DrawnView),
            0,
            propertyChanged: UpdateRotation);

        private static void UpdateRotation(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView control)
            {
                control.DeviceRotationChanged?.Invoke(control, (int)newvalue);
                ;
            }
        }

        public int DeviceRotation
        {
            get { return (int)GetValue(DisplayRotationProperty); }
            set { SetValue(DisplayRotationProperty, value); }
        }

        //{
        //    HorizontalOptions = LayoutOptions.Fill,
        //    VerticalOptions = LayoutOptions.Fill
        //};
        public bool HasHandler { get; set; }

        public virtual void DisconnectedHandler()
        {
            HasHandler = false;
            Super.NeedGlobalRefresh -= OnNeedUpdate;
        }

        public long NeedGlobalRefreshCount { get; set; }

        private void OnNeedUpdate(object sender, EventArgs e)
        {
            UpdateGlobal();
        }

        protected virtual void UpdateGlobal()
        {
            NeedCheckParentVisibility = true;
            NeedGlobalRefreshCount++;
            RenderingScale = -1;
            InvalidateChildren();
            Update();
        }

        /// <summary>
        /// Underlying drawn views need measurement
        /// </summary>
        protected bool NeedMeasureDrawn { get; set; } = true;

        /// <summary>
        /// Invoked when IsHiddenInViewTree changes
        /// </summary>
        /// <param name="state"></param>
        public virtual void OnCanRenderChanged(bool state)
        {
            if (state)
            {
                NeedMeasureDrawn = true;
                Update();
            }
        }

        public virtual void ConnectedHandler()
        {
            HasHandler = true;
            Super.NeedGlobalRefresh -= OnNeedUpdate;
            Super.NeedGlobalRefresh += OnNeedUpdate;
        }

        protected void FixDensity()
        {
            if (_renderingScale <= 0.0)
            {
                var scale = (float)GetDensity();
                if (scale <= 0.0)
                {
                    scale = (float)(CanvasView.CanvasSize.Width / this.Width);
                }

                RenderingScale = scale;
            }
        }

        /// <summary>
        /// Set this to true if you do not want the canvas to be redrawn as transparent and showing content below the canvas (splash?..) when UpdateLocks is True
        /// </summary>
        public bool StopDrawingWhenUpdateIsLocked { get; set; }

        public DateTime TimeDrawingStarted { get; protected set; }
        public DateTime TimeDrawingComplete { get; protected set; }
        volatile bool _isWaiting = false;

        public virtual void InvalidateViewport()
        {
            Update();
        }

        public virtual void Repaint()
        {
            Update();
        }

        public bool OrderedDraw { get; protected set; }
        double _lastUpdateTimeNanos;

        public void ResetUpdate()
        {
            NeedCheckParentVisibility = true;
            OrderedDraw = false;
            InvalidatedCanvas = 0;
        }

        /// <summary>
        /// A very important tracking prop to avoid saturating main thread with too many updates
        /// </summary>
        protected long InvalidatedCanvas { get; set; }

        public bool IsRendering { get; protected set; }
        protected Grid Delayed { get; set; }

        public static double GetDensity()
        {
            return Super.Screen.Density;
        }

        /// <summary>
        /// Will safely destroy existing if any
        /// </summary>
        protected void CreateSkiaView()
        {

            DestroySkiaView();

#if ONPLATFORM
            PlatformHardwareAccelerationChanged();
#endif

            if (IsUsingHardwareAcceleration)
            {
                var view = new SkiaViewAccelerated(this);
                view.OnDraw = OnDrawSurface;
                CanvasView = view;
            }
            else
            {
                var view = new SkiaView(this);
                view.OnDraw = OnDrawSurface;
                CanvasView = view;
            }

            Content = CanvasView as View;
        }

        protected virtual void OnDestroyingVew()
        {

        }

        protected void DestroySkiaView()
        {
            lock (LockDraw)
            {
                if (CanvasView == null)
                    return;

                if (_destroyed == CanvasView.Uid)
                    return;

                _destroyed = CanvasView.Uid;

                var kill = CanvasView;
                if (kill != null)
                {
                    OnDestroyingVew();
                    CanvasView = null;
                    kill.OnDraw = null;
                    if (kill is View view)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                view.DisconnectHandlers();
                            }
                            catch (Exception e)
                            {
                                Super.Log(e);
                            }
                        });
                    }
                    else
                    {
                        kill.Dispose();
                    }
                }
            }
        }

        public bool IsDisposing { get; set; }

        ~DrawnView()
        {
            if (IsDisposed)
                return;
            Dispose(false);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Dispose(true);
            }
        }

        void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (!disposing)
            {
                IsDisposed = true;
                return;
            }

            WillDispose();
            OnDispose();
        }

        public event EventHandler ViewDisposing;

        protected virtual void WillDispose()
        {
            IsDisposing = true;
            ViewDisposing?.Invoke(this, EventArgs.Empty);
            Parent = null;
            this.SizeChanged -= OnFormsSizeChanged;
        }

        public virtual void OnDispose()
        {
            lock (LockDraw)
            {
                if (IsDisposed)
                    return;

                try
                {
                    IsDisposing = true;

                    InitFramework(false);

                    OnDisposing();

                    IsDisposed = true;

                    DisposeManager.Dispose();

                    PaintSystem?.Dispose();

                    DestroySkiaView();

                    GestureListeners.Clear();

                    ClearChildren();

                    Content = null;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            this.Handler?.DisconnectHandler();
                        }
                        catch (Exception e)
                        {
                            Super.Log(e);
                        }
                    });
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }
        }

        /// <summary>
        /// Makes the control dirty, in need to be remeasured and rendered but this doesn't call Update, it's up yo you
        /// </summary>
        public virtual void Invalidate()
        {
            NeedMeasure = true;
            NeedMeasureDrawn = true;
        }

        public void InvalidateParents()
        {
            Invalidate();
        }

        public virtual void OnSuperviewShouldRenderChanged(bool state)
        {
            foreach (var view in Views.ToList())
            {
                view.OnSuperviewShouldRenderChanged(state);
            }
        }

        /// <summary>
        /// We need to invalidate children maui changed our storyboard size
        /// </summary>
        public void InvalidateChildren()
        {
            foreach (var view in Views)
            {
                view.InvalidateWithChildren();
            }

            NeedMeasure = true;
            NeedMeasureDrawn = true;

            Update();
        }

        public void PrintDebug(string indent = "")
        {
            Console.WriteLine($"{indent}└─ {GetType().Name} {Width:0.0},{Height:0.0} pts");
            foreach (var view in Views)
            {
                //Console.Write($"{indent}    ├─ ");
                view.PrintDebug(indent);
            }
        }

        public bool NeedAutoSize
        {
            get { return NeedAutoHeight || NeedAutoWidth; }
        }

        public bool NeedAutoHeight
        {
            get { return VerticalOptions.Alignment != LayoutAlignment.Fill && HeightRequest < 0; }
        }

        public bool NeedAutoWidth
        {
            get { return HorizontalOptions.Alignment != LayoutAlignment.Fill && WidthRequest < 0; }
        }

        public virtual bool ShouldInvalidateByChildren
        {
            get { return NeedAutoSize; }
        }

        public static readonly BindableProperty UpdateLocksProperty = BindableProperty.Create(
            nameof(UpdateLocks),
            typeof(int),
            typeof(DrawnView),
            0);

        public int UpdateLocks
        {
            get { return (int)GetValue(UpdateLocksProperty); }
            set { SetValue(UpdateLocksProperty, value); }
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);


            if (propertyName == nameof(HeightRequest)
                || propertyName == nameof(WidthRequest)
                || propertyName == nameof(Padding)
                || propertyName == nameof(Margin)
                || propertyName == nameof(HorizontalOptions)
                || propertyName == nameof(VerticalOptions)
               )
            {
                Invalidate();
            }
            else if (propertyName == nameof(IsVisible))
            {
                if (IsVisible)
                    Update();
            }
        }

        public Guid Uid { get; } = Guid.NewGuid();

        public static (double X1, double Y1, double X2, double Y2) LinearGradientAngleToPoints(double direction)
        {
            //adapt to css style
            direction -= 90;

            //allow negative angles
            if (direction < 0)
                direction = 360 + direction;

            if (direction > 360)
                direction = 360;

            (double x, double y) pointOfAngle(double a)
            {
                return (x: Math.Cos(a), y: Math.Sin(a));
            }

            ;

            double degreesToRadians(double d)
            {
                return ((d * Math.PI) / 180);
            }

            var eps = Math.Pow(2, -52);
            var angle = (direction % 360);
            var startPoint = pointOfAngle(degreesToRadians(180 - angle));
            var endPoint = pointOfAngle(degreesToRadians(360 - angle));

            if (startPoint.x <= 0 || Math.Abs(startPoint.x) <= eps)
                startPoint.x = 0;

            if (startPoint.y <= 0 || Math.Abs(startPoint.y) <= eps)
                startPoint.y = 0;

            if (endPoint.x <= 0 || Math.Abs(endPoint.x) <= eps)
                endPoint.x = 0;

            if (endPoint.y <= 0 || Math.Abs(endPoint.y) <= eps)
                endPoint.y = 0;

            return (startPoint.x, startPoint.y, endPoint.x, endPoint.y);
        }

        #region DISPOSE BY XAMARIN FORMS

        //private bool _isRendererSet;
        //protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        //{
        //    base.OnPropertyChanged(propertyName);
        //    if (propertyName == "Renderer")
        //    {
        //        _isRendererSet = !_isRendererSet;
        //        if (!_isRendererSet)
        //        {
        //            Dispose();
        //            Debug.WriteLine("SkiaBaseView disposed!");
        //        }
        //        else
        //        {
        //            IsDisposed = false;
        //        }
        //    }
        //}

        #endregion

        private bool _IsGhost;

        public bool IsGhost
        {
            get { return _IsGhost; }
            set
            {
                if (_IsGhost != value)
                {
                    _IsGhost = value;
                    OnPropertyChanged();
                }
            }
        }

        private void ViewSizeChanged(object sender, EventArgs e)
        {
            //xamarin forms changed our size if used inside xamarin layout
            OnSizeChanged();
        }

        public DrawnView()
        {
            Init();
        }

        public Action<SKPath, SKRect> Clipping { get; set; }

        public virtual SKPath CreateClip(object arguments, bool usePosition, SKPath path = null)
        {
            path ??= new SKPath();

            if (usePosition)
            {
                path.AddRect(DrawingRect);
            }
            else
            {
                path.AddRect(new(0, 0, DrawingRect.Width, DrawingRect.Height));
            }

            return path;
        }

        public string Tag { get; set; }

        public bool IsRootView(float width, float height, SKRect destination)
        {
            if (this.Parent != null)
            {
                return true; //Xamarin/MAUI
            }

            return this.Parent == null && destination.Width == width && destination.Height == height;
        }

        //-------------------------------------------------------------
        /// <summary>
        ///  destination in PIXELS, requests in UNITS. resulting Destination prop will be filed in PIXELS.
        /// </summary>
        /// <param name="destination">PIXELS</param>
        /// <param name="widthRequest">UNITS</param>
        /// <param name="heightRequest">UNITS</param>
        /// <param name="scale"></param>
        public SKRect CalculateLayout(SKRect destination, double widthRequest,
                double heightRequest, double scale = 1.0)
            //-------------------------------------------------------------
        {
            var scaledOffsetMargin = 0;

            var rectWidth = destination.Width - scaledOffsetMargin * 2;
            var wants = Math.Round(widthRequest * scale);
            if (wants > 0 && wants < rectWidth)
                rectWidth = (int)wants;

            var rectHeight = destination.Height - scaledOffsetMargin * 2;
            wants = Math.Round(heightRequest * scale);
            if (wants > 0 && wants < rectHeight)
                rectHeight = (int)wants;

            var availableWidth = destination.Width - scaledOffsetMargin * 2;
            var availableHeight = destination.Height - scaledOffsetMargin * 2;

            var layoutHorizontal = new LayoutOptions(HorizontalOptions.Alignment, HorizontalOptions.Expands);
            var layoutVertical = new LayoutOptions(VerticalOptions.Alignment, VerticalOptions.Expands);


            //todo sensor rotation

            //initial fill
            var left = destination.Left + scaledOffsetMargin;
            var top = destination.Top + scaledOffsetMargin;
            var right = left + rectWidth;
            var bottom = top + rectHeight;


            //layoutHorizontal
            if (layoutHorizontal.Alignment == LayoutAlignment.Center)
            {
                //center
                left += (availableWidth - rectWidth) / 2.0f;
                right = left + rectWidth;

                if (left < destination.Left)
                {
                    left = (float)(destination.Left);
                    right = left + rectWidth;
                }

                if (right > destination.Right)
                {
                    right = (float)(destination.Right);
                }
            }
            else if (layoutHorizontal.Alignment == LayoutAlignment.End)
            {
                //end
                right = destination.Right;
                left = right - rectWidth;
                if (left < destination.Left)
                {
                    left = (float)(destination.Left);
                }
            }
            else
            {
                //start or fill
                right = left + rectWidth;
                if (right > destination.Right)
                {
                    right = (float)(destination.Right);
                }
            }

            //VerticalOptions
            if (layoutVertical.Alignment == LayoutAlignment.Center)
            {
                //center
                top += availableHeight / 2.0f - rectHeight / 2.0f;
                bottom = top + rectHeight;
                if (top < destination.Top)
                {
                    top = (float)(destination.Top);
                    bottom = top + rectHeight;
                }
                else if (bottom > destination.Bottom)
                {
                    bottom = (float)(destination.Bottom);
                    top = bottom - rectHeight;
                }
            }
            else if (layoutVertical.Alignment == LayoutAlignment.End && double.IsFinite(destination.Bottom))
            {
                //end
                bottom = destination.Bottom;
                top = bottom - rectHeight;
                if (top < destination.Top)
                {
                    top = (float)(destination.Top);
                }
            }
            else
            {
                //start or fill
                bottom = top + rectHeight;
                if (bottom > destination.Bottom)
                {
                    bottom = (float)(destination.Bottom);
                }
            }

            return new SKRect((float)left, (float)top, (float)right, (float)bottom);
        }

        /// <summary>
        ///  destination in PIXELS, requests in UNITS. resulting Destination prop will be filed in PIXELS.
        /// </summary>
        /// <param name="destination">PIXELS</param>
        /// <param name="widthRequest">UNITS</param>
        /// <param name="heightRequest">UNITS</param>
        /// <param name="scale"></param>
        public virtual void Arrange(SKRect destination, double widthRequest, double heightRequest, double scale = 1.0)
        {
            Destination = CalculateLayout(destination, widthRequest, heightRequest, scale);
        }

        public ScaledSize MeasuredSize { get; set; } = new();

        public virtual ScaledSize Measure(float widthConstraintPts, float heightConstraintPts)
        {
            if (!IsVisible)
            {
                return SetMeasured(0, 0, (float)RenderingScale);
            }

            if (widthConstraintPts < 0 || heightConstraintPts < 0)
            {
                //not setting NeedMeasure=false;
                return ScaledSize.Default;
            }

            var widthPixels = (float)((WidthRequest));
            var heightPixels = (float)((HeightRequest));

            if (WidthRequest > 0 && widthPixels < widthConstraintPts)
                widthConstraintPts = widthPixels;

            if (HeightRequest > 0 && heightPixels < heightConstraintPts)
                heightConstraintPts = heightPixels;

            return SetMeasured(widthConstraintPts, heightConstraintPts, (float)RenderingScale);
        }

        /// <summary>
        /// The virtual view needs native measurement
        /// </summary>
        protected virtual bool NeedMeasure
        {
            get => needMeasure;
            set => needMeasure = value;
        }

        protected ScaledSize SetMeasured(float width, float height, float scale)
        {
            NeedMeasure = false;
            NeedMeasureDrawn = true;

            if (!double.IsNaN(height))
            {
                //Height = height / scale;
            }
            else
            {
                height = -1;
                //Height = height;
            }

            if (!double.IsNaN(width))
            {
                //Width = width / scale;
            }
            else
            {
                width = -1;
                //Width = width;
            }

            MeasuredSize = ScaledSize.FromUnits(width, height, scale);

            //SetValueCore(RenderingScaleProperty, scale, SetValueFlags.None);

            OnMeasured();

            return MeasuredSize;
        }

        protected virtual void OnMeasured()
        {
            Measured?.Invoke(this, MeasuredSize);
        }

        public event EventHandler<ScaledSize> Measured;

        private void OnFormsSizeChanged(object sender, EventArgs e)
        {
            Update();
        }

        public bool IsDisposed { get; protected set; }

        SkiaDrawingContext CreateContext(SKSurface surface)
        {
            return new SkiaDrawingContext()
            {
                Superview = this,
                FrameTimeNanos = CanvasView.FrameTime,
                Surface = surface,
                Canvas = surface.Canvas,
                Width = surface.Canvas.DeviceClipBounds.Width,
                Height = surface.Canvas.DeviceClipBounds.Height
            };
        }

        /// <summary>
        /// Can use this to manage double buffering to detect if we are in the drawing thread or in background.
        /// </summary>
        public int DrawingThreadId { get; protected set; }

        public bool WasRendered { get; set; }

        /// <summary>
        /// OnDrawSurface will call that
        /// </summary>
        public event EventHandler<SkiaDrawingContext?> WasDrawn;

        /// <summary>
        /// OnDrawSurface will call that
        /// </summary>
        public event EventHandler<SkiaDrawingContext?> WillDraw;

        /// <summary>
        /// OnDrawSurface will call that if never been drawn yet
        /// </summary>
        public event EventHandler<SkiaDrawingContext?> WillFirstTimeDraw;

        protected SKRect LastDrawnRect;

        private bool OnDrawSurface(SKSurface surface, SKRect rect)
        {
            lock (LockDraw)
            {
                if (CanvasView == null)
                    return false;

                if (!SkiaControl.CompareSize(LastDrawnRect.Size, rect.Size, 1))
                {
                    LastDrawnRect = rect;
                    NeedMeasure = true;
                    NeedMeasureDrawn = true;
                }

                if (!OnStartRendering(surface.Canvas))
                    return UpdateMode == UpdateMode.Constant;

                try
                {
                    if (NeedMeasure)
                    {
                        FixDensity();
                    }

                    var args = CreateContext(surface);

                    this.DrawingThreadId = Thread.CurrentThread.ManagedThreadId;

                    if (!WasRendered)
                    {
                        WillFirstTimeDraw?.Invoke(this, args);
                    }

                    WillDraw?.Invoke(this, null);

                    var ctx = new DrawingContext(args, rect, RenderingScale, null);
                    Draw(ctx);
                }
                finally
                {
                    OnFinalizeRendering();

                    WasRendered = true;
                }

                return IsDirty;
            }
        }

        protected virtual bool OnStartRendering(SKCanvas canvas)
        {
            var monitor = InvalidatedCanvas;
            monitor--;
            if (monitor < 0)
            {
                monitor = 0;
            }

            InvalidatedCanvas = monitor;
            OrderedDraw = false;

            if (!CanDraw || canvas == null)
                return false;

            TimeDrawingStarted = DateTime.Now;
            IsRendering = true;

            IsDirty = false; //any control can set to true after that

            return true;
        }

        protected virtual void OnFinalizeRendering()
        {
            TimeDrawingComplete = DateTime.Now;

            IsRendering = false;

            if (CanvasView != null)
            {
                if (UpdateMode == UpdateMode.Constant || !CanvasView.HasDrawn)
                    IsDirty = true;

                if (IsDirty)
                {
                    Update();
                }
            }

            //track and cap queued updates
            var monitor = InvalidatedCanvas;
            monitor--;
            if (monitor < 0)
            {
                monitor = 0;
            }

            InvalidatedCanvas = monitor;

            WasDrawn?.Invoke(this, null);
        }

        public virtual void OnDisposing()
        {
            if (_visibilityParent != null)
            {
                _visibilityParent.PropertyChanged -= OnParentVisibilityCheck;
            }

#if ONPLATFORM
            DisposePlatform();
#endif

            DeviceDisplay.Current.MainDisplayInfoChanged -= OnMainDisplayInfoChanged;
        }

        //public virtual void Render(SkiaDrawingContext context, SKRect destination, float scale,
        //    )
        //{
        //    AvailableDestination = destination;
        //    Draw(context, destination, scale);
        //}
        public SKRect AvailableDestination { get; set; }

        /// <summary>
        /// will be reset to null by InvalidateViewsList()
        /// </summary>
        private IReadOnlyList<SkiaControl> _orderedChildren;

        /// <summary>
        /// For non templated simple subviews
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<SkiaControl> GetOrderedSubviews()
        {
            if (_orderedChildren == null)
            {
                _orderedChildren = Views.OrderBy(x => x.ZIndex).ToList();
            }

            return _orderedChildren;
        }

        /// <summary>
        /// To make GetOrderedSubviews() regenerate next result instead of using cached
        /// </summary>
        public void InvalidateViewsList()
        {
            _orderedChildren = null;
        }

        #region FPS

        private double _fpsAverage;
        private int _fpsCount;
        protected double _fps;

        /// <summary>
        /// Frame started rendering nanoseconds
        /// </summary>
        public long FrameTime { get; protected set; }

        /// <summary>
        /// Actual FPS
        /// </summary>
        public double CanvasFps
        {
            get
            {
                if (CanvasView == null)
                    return 0.0;
                return CanvasView.FPS;
            }
        }

        /// <summary>
        /// Average FPS
        /// </summary>
        public double FPS { get; protected set; }

        #endregion

        protected object LockDraw = new();

        public long FrameNumber { get; private set; }

        #region DISPOSE STUFF

        public void DisposeObject(IDisposable resource)
        {
            if (this.IsDisposed)
                return;

            //resource?.Dispose();

            DisposeManager.EnqueueDisposable(resource, FrameNumber);
        }

        protected DisposableManager DisposeManager { get; } = new(2);

        public readonly struct TimedDisposable : IDisposable
        {
            public IDisposable Disposable { get; }
            public DateTime EnqueuedTime { get; }

            public TimedDisposable(IDisposable disposable)
            {
                Disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
                EnqueuedTime = DateTime.UtcNow;
            }

            public void Dispose()
            {
                Disposable.Dispose();
            }
        }

        #endregion

        public void PostponeInvalidation(SkiaControl key, Action action)
        {
            lock (_lockInvalidations)
            {
                GetBackInvalidations()[action] = key;
                Repaint();
            }
        }

        private object _lockInvalidations = new();
        private bool _invalidationsA;
        protected readonly Dictionary<Action, SkiaControl> InvalidationActionsA = new();
        protected readonly Dictionary<Action, SkiaControl> InvalidationActionsB = new();

        protected Dictionary<Action, SkiaControl> GetFrontInvalidations()
        {
            lock (_lockInvalidations)
            {
                return _invalidationsA ? InvalidationActionsA : InvalidationActionsB;
            }
        }

        protected Dictionary<Action, SkiaControl> GetBackInvalidations()
        {
            lock (_lockInvalidations)
            {
                return !_invalidationsA ? InvalidationActionsA : InvalidationActionsB;
            }
        }

        protected void SwapInvalidations()
        {
            lock (_lockInvalidations)
            {
                _invalidationsA = !_invalidationsA;
            }
        }

        /// <summary>
        /// For debugging purposes check if dont have concurrent threads
        /// </summary>
        public int DrawingThreads { get; protected set; }

        protected ConcurrentDictionary<Guid, SkiaControl> DirtyChildrenTracker = new();

        public void SetChildAsDirty(SkiaControl child)
        {
            if (dirtyChilrenProcessing)
                return;

            DirtyChildrenTracker[child.Uid] = child;
        }

        private volatile bool dirtyChilrenProcessing;

        protected void CommitInvalidations()
        {
            SwapInvalidations();
            var invalidations = GetFrontInvalidations();
            foreach (var invalidation in invalidations)
            {
                invalidation.Key.Invoke();
            }

            invalidations.Clear();
        }

        #region BACKGROUND RENDERING

        protected object LockStartOffscreenQueue = new();
        private bool _processingOffscrenRendering = false;

        // Holds the incoming commands without blocking
        private readonly ConcurrentQueue<OffscreenCommand> _incomingCommands = new ConcurrentQueue<OffscreenCommand>();

        // Holds the latest commands for each control (only processed by background thread)
        private readonly Dictionary<SkiaControl, OffscreenCommand> _offscreenCommands = new();
        protected SemaphoreSlim semaphoreOffscreenProcess = new(1);

        public record OffscreenCommand(SkiaControl Control, CancellationToken Cancel);

        /// <summary>
        /// Ensures offscreen rendering queue is running
        /// </summary>
        public void KickOffscreenCacheRendering()
        {
            lock (LockStartOffscreenQueue)
            {
                if (!_processingOffscrenRendering)
                {
                    _processingOffscrenRendering = true;
                    Task.Run(async () => { await ProcessOffscreenCacheRenderingAsync(); }).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Push an offscreen rendering command without blocking the UI thread.
        /// </summary>
        public void PushToOffscreenRendering(SkiaControl control, CancellationToken cancel = default)
        {
            _incomingCommands.Enqueue(new OffscreenCommand(control, cancel));
            KickOffscreenCacheRendering();
        }

        public async Task ProcessOffscreenCacheRenderingAsync()
        {
            await semaphoreOffscreenProcess.WaitAsync();
            try
            {
                // Drain the ConcurrentQueue into a local list
                var drainedCommands = new List<OffscreenCommand>();
                while (_incomingCommands.TryDequeue(out var cmd))
                {
                    drainedCommands.Add(cmd);
                }

                // If nothing was drained, we can safely return
                if (drainedCommands.Count == 0)
                    return;

                lock (_offscreenCommands)
                {
                    foreach (var command in drainedCommands)
                    {
                        _offscreenCommands[command.Control] = command;
                    }
                }

                // Process the latest commands now
                // Reading dictionary under lock might not be strictly necessary
                // if we trust only this background thread modifies it
                // but we can snapshot under lock for safety.
                OffscreenCommand[] toProcess;
                lock (_offscreenCommands)
                {
                    toProcess = _offscreenCommands.Values.ToArray();
                    _offscreenCommands.Clear(); // We clear after processing to avoid memory growth
                }

                _processingOffscrenRendering = true;

                foreach (var command in toProcess)
                {
                    try
                    {
                        if (command.Control.IsDisposed || command.Control.IsDisposing ||
                            command.Cancel.IsCancellationRequested)
                        {
                            continue;
                        }

                        var action = command.Control.GetOffscreenRenderingAction();
                        action?.Invoke();

                        //command.Control.Repaint();
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }
            }
            finally
            {
                _processingOffscrenRendering = false;
                semaphoreOffscreenProcess.Release();
            }
        }

        #endregion

        private VisualTreeHandler VisualTree;// = new();

        protected virtual void Draw(DrawingContext context)
        {
            ++FrameNumber;

            DisposeManager.DisposeDisposables(FrameNumber);

            //Debug.WriteLine($"[DRAW] {Tag}");

            if (IsDisposing || IsDisposed || UpdateLocks > 0)
            {
                return;
            }

            var destination = context.Destination;
            var scale = context.Scale;

            if (NeedGlobalRefreshCount != _globalRefresh)
            {
                _globalRefresh = NeedGlobalRefreshCount;
                foreach (var item in Children)
                {
                    item.InvalidateMeasureInternal();
                }
            }

            try
            {
                DrawingThreads++;

                FrameTime = CanvasView.FrameTime;

                FPS = CanvasFps;

                CommitInvalidations();

                while (ExecuteBeforeDraw.TryDequeue(out Action action))
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                var executed = ExecuteAnimators(context.Context.FrameTimeNanos);

                if (this.Width > 0 && this.Height > 0)
                {
                    // ABSOLUTE like inside maui grid
                    var children = GetOrderedSubviews();
                    var rectForChild = new SKRect(
                        destination.Left + (float)Math.Round((Padding.Left) * scale),
                        destination.Top + (float)Math.Round((Padding.Top) * scale),
                        destination.Right - (float)Math.Round((Padding.Right) * scale),
                        destination.Bottom - (float)Math.Round((Padding.Bottom) * scale));

                    /*
                    if (VisualTree != null) //not used at all just playing around
                    {
                        var child = children.FirstOrDefault();
                        if (child != null)
                        {
                            // Pass 1: Prepare logical tree
                            child.OptionalOnBeforeDrawing();
                            if (child.CanDraw)
                            {
                                if (NeedMeasureDrawn)
                                {
                                    child.NeedMeasure = true;
                                }

                                // Build the logical VisualNode tree (similar to existing pattern)
                                VisualTree.PrepareLogicalTree(context.WithDestination(rectForChild),
                                    (float)(Width - Padding.HorizontalThickness),
                                    (float)(Height - Padding.VerticalThickness),
                                    child);
                            }

                            // Pass 2: Render using logical tree
                            VisualTree.RenderLogical(context.WithDestination(rectForChild));
                        }
                    }
                    else //usual one, still working fine
                    */
                    {
                        foreach (var child in children)
                        {
                            child.OptionalOnBeforeDrawing(); //could set IsVisible or whatever inside
                            if (child.CanDraw) //still visible
                            {
                                if (NeedMeasureDrawn)
                                {
                                    child.NeedMeasure = true;
                                }

                                child.Render(context.WithDestination(rectForChild));
                            }
                        }

                        dirtyChilrenProcessing = true;

                        //todo for retained mode!!!
                        foreach (var child in DirtyChildrenTracker.Values)
                        {
                            if (child != null && !child.IsDisposed && !child.IsDisposing)
                            {
                                child.InvalidatedParent = false;
                                child?.InvalidateParent();
                            }
                        }

                        DirtyChildrenTracker.Clear();
                        dirtyChilrenProcessing = false;

                        //notify registered tree final nodes of rendering tree state
                        foreach (var skiaControl in this.Views)
                        {
                            foreach (SkiaControl subscriber in RenderingSubscribers.Keys)
                            {
                                var transform = new VisualTransform();

                                //var position = subscriber.LastVisualNode.HitBoxWithTransforms.Units.Location;

                                var node = skiaControl.FindRenderedNode(subscriber);

                                transform.IsVisible = node != null;

                                if (transform.IsVisible)
                                {
                                    transform.Translation = node.TranslationTotal;
                                    transform.Opacity = (float)node.OpacityTotal;
                                    transform.Rotation = (float)node.RotationTotal;
                                    transform.Scale = node.ScaleTotal;
                                    transform.Frame = subscriber.VisualLayer.HitBoxWithTransforms;
                                }
                                else
                                {
                                    transform.Frame = ScaledRect.FromPixels(SKRect.Empty, 1f);

                                    //DumpTree(skiaControl.VisualNode);
                                }

                                subscriber.SetVisualTransform(transform);
                            }
                        }

                        var postExecuted = ExecutePostAnimators(context);

                        //Kick to redraw if need animate
                        if (executed + postExecuted > 0)
                        {
                            IsDirty = true;
                        }

                        if (CallbackScreenshot != null)
                        {
                            TakeScreenShotInternal(context.Context.Superview.CanvasView.Surface);
                        }
                    }
                }

                while (ExecuteAfterDraw.TryDequeue(out Action action))
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Super.Log($"[DrawnView] Handled ExecuteAfterDraw: {e}");
                    }
                }

                NeedMeasureDrawn = false;
            }
            catch (Exception e)
            {
                Super.Log(e); //most probably data was modified while drawing

                NeedMeasure = true;
                NeedMeasureDrawn = true;
                IsDirty = true;
            }
            finally
            {
                NeedMeasureDrawn = false;
                DrawingThreads--;
            }
        }

        public static readonly BindableProperty RenderingScaleProperty = BindableProperty.Create(nameof(RenderingScale),
            typeof(float), typeof(DrawnView),
            -1.0f,
            propertyChanged: (b, o, n) =>
            {
                var control = b as DrawnView;
                {
                    if (control != null && !control.IsDisposed)
                    {
                        control.OnDensityChanged();
                    }
                }
            });

        public virtual void OnDensityChanged()
        {
            InvalidateChildren();
            Update();
        }

        public float RenderingScale
        {
            get
            {
                var value = (float)GetValue(RenderingScaleProperty);
                if (value < 0.1)
                {
                    return (float)GetDensity();
                }

                return value;
            }
            set
            {
                _renderingScale = value;
                try
                {
                    SetValue(RenderingScaleProperty, value);
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }
        }

        private float _renderingScale = -1;

        private static void OnHardwareModeChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView control && control.Handler != null)
            {
                control.CreateSkiaView();
            }
        }

        public static readonly BindableProperty RenderingModeProperty = BindableProperty.Create(
            nameof(RenderingMode),
            typeof(RenderingModeType),
            typeof(DrawnView),
            RenderingModeType.Default,
            propertyChanged: OnHardwareModeChanged);

        public RenderingModeType RenderingMode
        {
            get { return (RenderingModeType)GetValue(RenderingModeProperty); }
            set { SetValue(RenderingModeProperty, value); }
        }

        public static readonly BindableProperty CanRenderOffScreenProperty = BindableProperty.Create(
            nameof(CanRenderOffScreen),
            typeof(bool),
            typeof(DrawnView),
            false);

        /// <summary>
        /// If this is check you view will be refreshed even offScreen or hidden
        /// </summary>
        public bool CanRenderOffScreen
        {
            get { return (bool)GetValue(CanRenderOffScreenProperty); }
            set { SetValue(CanRenderOffScreenProperty, value); }
        }

        /// <summary>
        /// Indicates that it is allowed to be rendered by engine, internal use
        /// </summary>
        /// <returns></returns>
        public bool CanDraw
        {
            get
            {
                var canRenderOffScreen = !IsHiddenInViewTree || CanRenderOffScreen;
                return CanvasView != null && !IsDisposed && IsVisible && Handler != null && canRenderOffScreen;
            }
        }

        /// <summary>
        /// Indicates that view is either hidden or offscreen.
        /// This disables rendering if you don't set CanRenderOffScreen to true
        /// </summary>
        public bool IsHiddenInViewTree
        {
            get { return _stopRendering; }
            protected set
            {
                if (value != _stopRendering)
                {
                    _stopRendering = value;
                    OnCanRenderChanged(!value);
                }
            }
        }

        bool _stopRendering;

        public bool NeedCheckParentVisibility
        {
            get { return _needCheckParentVisibility; }
            set
            {
                if (_needCheckParentVisibility != value)
                {
                    _needCheckParentVisibility = value;
                    OnPropertyChanged();
                    OnSuperviewShouldRenderChanged(value); //maybe use just event handler???
                }
            }
        }

        bool _needCheckParentVisibility;
        private long _globalRefresh;

        public static MemoryStream StreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }

        protected SKPaint PaintSystem { get; set; }
        public SKRect Destination { get; protected set; }

        public void PaintTintBackground(SKCanvas canvas)
        {
            if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
            {
                if (PaintSystem == null)
                {
                    PaintSystem = new SKPaint();
                }

                PaintSystem.Style = SKPaintStyle.StrokeAndFill;
                PaintSystem.Color = BackgroundColor.ToSKColor();
                canvas.DrawRect(Destination, PaintSystem);
            }
        }

        protected static void RedrawCanvas(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as DrawnView;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.Update();
                }
            }
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            foreach (var view in this.Views)
            {
                view.SetInheritedBindingContext(BindingContext);
            }
        }

        #region SUBVIEWS

        public List<SkiaControl> Views { get; } = new();

        public virtual void ClearChildren()
        {
            foreach (var child in Views.ToList())
            {
                RemoveSubView(child);
                child.Dispose();
            }

            Views.Clear();
            InvalidateViewsList();
        }

        public virtual void SetChildren(IEnumerable<SkiaControl> views)
        {
            ClearChildren();
            foreach (var child in views)
            {
                AddOrRemoveView(child, true);
            }
        }

        public void AddSubView(SkiaControl control)
        {
            if (control == null)
                return;
            control.SetParent(this);
            OnChildAdded(control);
            //if (Debugger.IsAttached)
            ReportHotreloadChildAdded(control);
        }

        public virtual void ReportHotreloadChildAdded(SkiaControl child)
        {
            if (child == null)
                return;

            var index = Views.FindIndex(child);
            VisualDiagnostics.OnChildAdded(this, child, index);
        }

        public void RemoveSubView(SkiaControl control)
        {
            if (control == null)
                return;

            //if (Debugger.IsAttached)
            ReportHotreloadChildRemoved(control);

            control.SetParent(null);
            OnChildRemoved(control);
        }

        public virtual void ReportHotreloadChildRemoved(SkiaControl control)
        {
            if (control == null)
                return;

            var index = Views.FindIndex(control);
            VisualDiagnostics.OnChildRemoved(this, control, index);
        }

        protected virtual void OnChildAdded(SkiaControl child)
        {
            InvalidateViewsList();
        }

        protected virtual void OnChildRemoved(SkiaControl child)
        {
            InvalidateViewsList();
        }

        #endregion

        #region Children

#pragma warning disable NU1605, CS0108

        public static readonly BindableProperty ChildrenProperty = BindableProperty.Create(
            nameof(Children),
            typeof(IList<SkiaControl>),
            typeof(DrawnView),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableCollection<SkiaControl>();
                created.CollectionChanged += ((DrawnView)instance).OnChildrenCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaControl>,
            propertyChanged: ChildrenPropertyChanged);

        public IList<SkiaControl> Children
        {
            get => (IList<SkiaControl>)GetValue(ChildrenProperty);
            set => SetValue(ChildrenProperty, value);
        }

#pragma warning restore NU1605, CS0108

        protected void AddOrRemoveView(SkiaControl subView, bool add)
        {
            if (subView != null)
            {
                if (add)
                {
                    AddSubView(subView);
                    subView.BindingContext = this.BindingContext;
                }
                else
                {
                    RemoveSubView(subView);
                    subView.BindingContext = null;
                    //subView.Dispose(); ?????
                }
            }
        }

        private static void ChildrenPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView skiaControl)
            {
                var enumerableChildren = (IEnumerable<SkiaControl>)newvalue;

                if (oldvalue != null)
                {
                    var oldViews = (IEnumerable<SkiaControl>)oldvalue;

                    if (oldvalue is INotifyCollectionChanged oldCollection)
                    {
                        oldCollection.CollectionChanged -= skiaControl.OnChildrenCollectionChanged;
                    }

                    foreach (var subView in oldViews)
                    {
                        skiaControl.AddOrRemoveView(subView, false);
                    }
                }

                //foreach (var subView in enumerableChildren)
                //{
                //	skiaControl.SetChildren(enumerableChildren);
                //}
                skiaControl.SetChildren(enumerableChildren);

                if (newvalue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += skiaControl.OnChildrenCollectionChanged;
                }

                skiaControl.Update();
            }
        }

        private void OnChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (SkiaControl newChildren in e.NewItems)
                    {
                        newChildren.SetParent(this);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                    foreach (SkiaControl oldChildren in e.OldItems ?? new SkiaControl[0])
                    {
                        oldChildren.SetParent(null);
                    }

                    break;
            }

            Update();
        }

        #endregion

        public static readonly BindableProperty UpdateModeProperty = BindableProperty.Create(
            nameof(UpdateMode),
            typeof(UpdateMode),
            typeof(DrawnView),
            UpdateMode.Dynamic,
            propertyChanged: ChangeUpdateMode);

        private static void ChangeUpdateMode(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView control)
            {
                control.Update();
            }
        }

        public UpdateMode UpdateMode
        {
            get { return (UpdateMode)GetValue(UpdateModeProperty); }
            set { SetValue(UpdateModeProperty, value); }
        }

        public static readonly BindableProperty ClipEffectsProperty = BindableProperty.Create(
            nameof(ClipEffects),
            typeof(bool),
            typeof(DrawnView),
            true);

        public bool ClipEffects
        {
            get { return (bool)GetValue(ClipEffectsProperty); }
            set { SetValue(ClipEffectsProperty, value); }
        }

        public static readonly BindableProperty Value1Property = BindableProperty.Create(
            nameof(Value1),
            typeof(double),
            typeof(DrawnView),
            0.0,
            propertyChanged: RedrawCanvas);

        public double Value1
        {
            get { return (double)GetValue(Value1Property); }
            set { SetValue(Value1Property, value); }
        }

        public static readonly BindableProperty Value2Property = BindableProperty.Create(
            nameof(Value2),
            typeof(double),
            typeof(DrawnView),
            0.0,
            propertyChanged: RedrawCanvas);

        public double Value2
        {
            get { return (double)GetValue(Value2Property); }
            set { SetValue(Value2Property, value); }
        }

        public static readonly BindableProperty Value3Property = BindableProperty.Create(
            nameof(Value3),
            typeof(double),
            typeof(DrawnView),
            0.0,
            propertyChanged: RedrawCanvas);

        public double Value3
        {
            get { return (double)GetValue(Value3Property); }
            set { SetValue(Value3Property, value); }
        }

        public static readonly BindableProperty Value4Property = BindableProperty.Create(
            nameof(Value4),
            typeof(double),
            typeof(DrawnView),
            0.0,
            propertyChanged: RedrawCanvas);

        public double Value4
        {
            get { return (double)GetValue(Value4Property); }
            set { SetValue(Value4Property, value); }
        }

        private bool _FocusLocked;

        public bool FocusLocked
        {
            get { return _FocusLocked; }
            set
            {
                if (_FocusLocked != value)
                {
                    _FocusLocked = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler<FocusedItemChangedArgs> FocusedItemChanged;

        public class FocusedItemChangedArgs : EventArgs
        {
            public FocusedItemChangedArgs(SkiaControl item, bool isFocused)
            {
                Item = item;
                IsFocused = isFocused;
            }

            public bool IsFocused { get; set; }
            public SkiaControl Item { get; set; }
        }

        /// <summary>
        /// Internal call by control, after reporting will affect FocusedChild but will not get FocusedItemChanged as it was its own call
        /// </summary>
        /// <param name="listener"></param>
        public void ReportFocus(ISkiaGestureListener value, ISkiaGestureListener setter = null)
        {
            if (value == _focusedChild)
                return;

            System.Diagnostics.Debug.WriteLine($"[Canvas] ReportFocus {value} from {setter}");

            if (_focusedChild != value && !FocusLocked)
            {
                if (_focusedChild != null)
                {
                    Debug.WriteLine($"[UNFOCUSED] DrawnView ReportFocus to {_focusedChild} will go to {value}");
                    if (_focusedChild != value || setter == null)
                        _focusedChild.OnFocusChanged(false);

                    FocusedItemChanged?.Invoke(this, new(_focusedChild as SkiaControl, false));
                }


                if (value != null)
                {
                    if (value != setter || setter == null)
                    {
                        var accept = value.OnFocusChanged(true);
                        if (!accept)
                        {
                            value = null;
                        }
                    }

                    FocusedItemChanged?.Invoke(this, new(value as SkiaControl, true));
                }

                _focusedChild = value;
                Debug.WriteLine($"[FOCUSED] 1 DrawnView ReportFocus to {_focusedChild}");

                if (_focusedChild == null)
                {
                    Debug.WriteLine($"[FOCUSED] 2 DrawnView ReportFocus to {_focusedChild}");

                    //with delay maybe some other control will focus itsself in that time
                    ResetFocusWithDelay(150);
                }


                OnPropertyChanged(nameof(FocusedChild));
            }
        }

        private static RestartingTimer<object> _timerResetFocus;

        public void ResetFocusWithDelay(int ms)
        {
            if (_timerResetFocus == null)
            {
                _timerResetFocus = new(TimeSpan.FromMilliseconds(ms), (arg) =>
                {
                    if (FocusedChild == null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
#if WINDOWS
                                Super.SetFocus(IntPtr.Zero); // Removes focus from all
#elif ANDROID
                                ResetFocus();
#else
                                this.Focus();
                                TouchEffect.CloseKeyboard();
#endif
                            }
                            catch (Exception e)
                            {
                                Super.Log(e);
                            }
                        });
                    }
                });
                _timerResetFocus.Start(null);
            }
            else
            {
                _timerResetFocus.Restart(null);
            }
        }

        /// <summary>
        /// Is set upon the consumer of the DOWN gesture. Calls ReportFocus methos when set.
        /// </summary>
        public ISkiaGestureListener FocusedChild
        {
            get { return _focusedChild; }
            set { ReportFocus(value); }
        }

        ISkiaGestureListener _focusedChild;
        private ISkiaDrawable _canvasView;
        private bool _wasBusy;

        /// <summary>
        /// 
        /// </summary>
        [Obsolete("Used by Update() when Super.UseLegacyLoop is True")]
        protected void InvalidateCanvas()
        {
            IsDirty = true;

            if (CanvasView == null)
            {
                OrderedDraw = false;
                return;
            }

            //sanity check
            var widthPixels = (int)CanvasView.CanvasSize.Width;
            var heightPixels = (int)CanvasView.CanvasSize.Height;
            if (widthPixels > 0 && heightPixels > 0)
            {
#if ANDROID || WINDOWS
                if (NeedCheckParentVisibility)
                    CheckElementVisibility(this);
                Continue();

#else
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (NeedCheckParentVisibility)
                            CheckElementVisibility(this);
                        Continue();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });
#endif

                void Continue()
                {
                    if (CanvasView != null)
                    {
                        if (CanDraw && !CanvasView.IsDrawing && !_isWaiting) //passed checks //
                        {
                            _wasBusy = false;
                            _isWaiting = true;
                            InvalidatedCanvas++;
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                try
                                {
                                    //avoid blocking ui thread
                                    //await Task.Delay(1);

                                    CanvasView
                                        ?.Update(); //very rarely could throw on windows here if maui destroys view when navigating, so we secured with try-catch
                                }
                                catch (Exception e)
                                {
                                    Super.Log(e);
                                }
                                finally
                                {
                                    _isWaiting = false;
                                    if (_wasBusy)
                                    {
                                        Update();
                                    }
                                }
                            });
                            return;
                        }
                        else
                        {
                            _wasBusy = true;
                        }
                    }

                    OrderedDraw = false;
                }
            }
            else
            {
                OrderedDraw = false;
            }
        }

        protected override void OnParentSet()
        {
            base.OnParentSet();

            NeedCheckParentVisibility = true;
        }

        private VisualElement _visibilityParent;
        private bool needMeasure = true;
        private Guid _destroyed;

        private void OnParentVisibilityCheck(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsVisible))
            {
                _visibilityParent.PropertyChanged -= OnParentVisibilityCheck;
                NeedCheckParentVisibility = true;
            }
        }

        public bool GetIsVisibleWithParent(VisualElement element)
        {
            if (element != null)
            {
                if (!element.IsVisible)
                {
                    if (element is not DrawnView)
                    {
                        if (_visibilityParent != null)
                            _visibilityParent.PropertyChanged -= OnParentVisibilityCheck;

                        _visibilityParent = element;
                        _visibilityParent.PropertyChanged += OnParentVisibilityCheck;
                        element.PropertyChanged += OnParentVisibilityCheck;
                    }

                    return false;
                }

                if (element.Parent is VisualElement visualParent)
                {
                    return GetIsVisibleWithParent(visualParent);
                }
            }

            return true;
        }

#if !ONPLATFORM
        public void CheckElementVisibility(VisualElement element)
        {
            NeedCheckParentVisibility = false;
        }

        protected virtual void OnSizeChanged()
        {
        }
#endif

        public virtual void ClipSmart(SKCanvas canvas, SKPath path,
            SKClipOperation operation = SKClipOperation.Intersect)
        {
            canvas.ClipPath(path, operation, false);
        }
    }
}
