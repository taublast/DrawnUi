using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Color = Microsoft.Maui.Graphics.Color;
using Grid = DrawnUi.Blazor.Views.Grid;

namespace DrawnUi.Views
{

    [ContentProperty("Children")]
    public partial class DrawnView : Canvas,
        IDrawnBase, IAnimatorsManager
    {
        public bool IsDirty { get; set; } = true;

        protected virtual void Draw(SkiaDrawingContext context, SKRect destination, float scale)
        {

            ++renderedFrames;

            //Debug.WriteLine($"[DRAW] {Tag}");

            if (IsDisposed || UpdateLocked)
            {
                return;
            }

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

                //while (ExecuteBeforeDraw.TryDequeue(out Action action))
                //{
                //	try
                //	{
                //		action?.Invoke();
                //	}
                //	catch (Exception e)
                //	{
                //		Super.Log(e);
                //	}
                //}

                while (ExecuteBeforeDraw.Count > 0)
                {
                    try
                    {
                        var action = ExecuteBeforeDraw.Dequeue();
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                var executed = ExecuteAnimators(context.FrameTimeNanos);

                if (this.Width > 0 && this.Height > 0)
                {
                    // ABSOLUTE like inside grid
                    var children = GetOrderedSubviews();

                    foreach (var child in children)
                    {
                        child.OptionalOnBeforeDrawing(); //could set IsVisible or whatever inside
                        if (child.CanDraw) //still visible
                        {
                            var rectForChild = new SKRect(
                                destination.Left + (float)Math.Round((Padding.Left) * scale),
                                destination.Top + (float)Math.Round((Padding.Top) * scale),
                                destination.Right - (float)Math.Round((Padding.Right) * scale),
                                destination.Bottom - (float)Math.Round((Padding.Bottom) * scale));
                            child.Render(new DrawingContext(context, rectForChild, (float)scale), rectForChild, (float)scale);
                        }
                    }

                    dirtyChilrenProcessing = true;
                    foreach (var child in DirtyChildren.Values)
                    {
                        if (child != null && !child.IsDisposing)
                        {
                            child.InvalidatedParent = false;
                            child?.InvalidateParent();
                        }
                    }
                    DirtyChildren.Clear();
                    dirtyChilrenProcessing = false;

                    //notify registered tree final nodes of rendering tree state
                    foreach (var tree in RenderingTrees)
                    {
                        //DebugV(tree.Value.Transform.IsVisible);
                        //todo what was this case? disabled as bugging
                        //if (tree.Value.Nodes.Count != tree.Value.Transform.RenderedNodes)
                        //{
                        //tree.Value.Transform.IsVisible = false;
                        //}
                        tree.Key.SetVisualTransform(tree.Value.Transform);
                    }

                    var postExecuted = ExecutePostAnimators(context, scale);

                    //Kick to redraw if need animate
                    if (executed + postExecuted > 0)
                    {
                        IsDirty = true;
                    }

                    if (CallbackScreenshot != null)
                    {
                        TakeScreenShotInternal(context.Superview.CanvasView.Surface);
                    }
                }
                else
                {
                    var stop = 1;
                }
                //while (ExecuteAfterDraw.TryDequeue(out Action action))
                //{
                //	try
                //	{
                //		action?.Invoke();
                //	}
                //	catch (Exception e)
                //	{
                //		Super.Log($"[DrawnView] Handled ExecuteAfterDraw: {e}");
                //	}
                //}
                while (ExecuteAfterDraw.Count > 0)
                {
                    Action a;
                    try
                    {
                        a = ExecuteAfterDraw.Dequeue();
                        a?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Super.Log($"[DrawnView] Handled ExecuteAfterDraw: {e}");
                    }
                }

                if (renderedFrames is >= 3 and < 5 && HardwareAcceleration == HardwareAccelerationMode.Prerender)
                {
                    //looks like we have finally loaded
                    SwapToDelayed();
                }

            }
            catch (Exception e)
            {
                Super.Log(e); //most probably data was modified while drawing

                NeedMeasure = true;
                IsDirty = true;
            }
            finally
            {
                DrawingThreads--;
            }

        }

        public void PostponeInvalidation(SkiaControl key, Action action)
        {
            lock (_lockInvalidations)
            {
                GetBackInvalidations()[action] = key;
                Repaint();
            }
        }

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


        protected Dictionary<Guid, SkiaControl> DirtyChildren = new();

        public void SetChildAsDirty(SkiaControl child)
        {
            if (dirtyChilrenProcessing)
                return;

            DirtyChildren[child.Uid] = child;
        }

        private volatile bool dirtyChilrenProcessing;

        protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            var measured = this.Measure((float)widthConstraint, (float)heightConstraint);

            //var system = base.OnMeasure(measured.Units.Width, measured.Units.Height);

            var custom = new SizeRequest(new(measured.Units.Width, measured.Units.Height), new(measured.Units.Width, measured.Units.Height));

            return custom;
        }

        public object Handler
        {
            get => _handler;
            set
            {
                if (Equals(value, _handler)) return;
                _handler = value;
                OnPropertyChanged();
                OnHandlerChanged();
            }
        }

        protected virtual void OnHandlerChanged()
        {
            NeedCheckParentVisibility = true;
        }

        protected override void OnParentSet()
        {
            base.OnParentSet();

            NeedCheckParentVisibility = true;
        }

        public bool GetIsVisibleWithParent(Microsoft.Maui.Controls.View element)
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

                if (element.Parent is Microsoft.Maui.Controls.View visualParent)
                {
                    return GetIsVisibleWithParent(visualParent);
                }
            }

            return true;
        }

        private Microsoft.Maui.Controls.View _visibilityParent;

        private void OnParentVisibilityCheck(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsVisible))
            {
                _visibilityParent.PropertyChanged -= OnParentVisibilityCheck;
                NeedCheckParentVisibility = true;
            }
        }


        public bool IsUsingHardwareAcceleration
        {
            get
            {
                if (!Super.CanUseHardwareAcceleration)
                    return false;

                return HardwareAcceleration != HardwareAccelerationMode.Disabled
                       && DeviceInfo.Platform != DevicePlatform.UWP
                       && DeviceInfo.Platform != DevicePlatform.iOS;
            }
        }


        public virtual bool IsVisibleInViewTree()
        {
            return IsVisible; //todo
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
        public virtual ScaledRect GetOnScreenVisibleArea(float inflateByPixels = 0)
        {
            var bounds = new SKRect(0 - inflateByPixels, 0 - inflateByPixels, (int)(Width * RenderingScale + inflateByPixels), (int)(Height * RenderingScale + inflateByPixels));

            return ScaledRect.FromPixels(bounds, (float)RenderingScale);
        }

        public virtual ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            var inflate = Math.Max(Math.Abs(inflateByPixels.X), Math.Abs(inflateByPixels.Y));
            return GetOnScreenVisibleArea(inflate);
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

        public void PostponeExecutionBeforeDraw(Action action, long key)
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

        public Queue<Action> ExecuteBeforeDraw { get; } = new(256);
        public Queue<Action> ExecuteAfterDraw { get; } = new(256);

        protected Action<SKImage> CallbackScreenshot;

        protected Dictionary<SkiaControl, VisualTreeChain> RenderingTrees = new(128);

        public void RegisterRenderingChain(VisualTreeChain chain)
        {
            // Generate NodeIndices dictionary
            RenderingTrees.TryAdd(chain.Child, chain);

            //initial state
            for (int i = 0; i < chain.Nodes.Count; i++)
            {
                UpdateRenderingChains(chain.Nodes[i]);
            }
        }

        public void UnregisterRenderingChain(SkiaControl child)
        {
            RenderingTrees.Remove(child);
        }

        public VisualTreeChain GetRenderingChain(SkiaControl child)
        {
            RenderingTrees.TryGetValue(child, out VisualTreeChain chain);
            return chain;
        }

        public void UpdateRenderingChains(SkiaControl node)
        {
            // Check each chain
            foreach (VisualTreeChain chain in RenderingTrees.Values)
            {
                // If the chain includes the node, update the chain's transform
                if (chain.NodeIndices.TryGetValue(node, out int index))
                {
                    // If the node is the root of the chain, reset the chain's transform
                    if (index == 0)
                    {
                        chain.Transform = new VisualTransform();
                    }

                    chain.Transform.IsVisible = chain.Nodes.All(x => x.IsVisible) && node.CanDraw;

                    //Debug.WriteLine($"U {node.GetType().Name} {node.IsVisible} => {chain.Transform.IsVisible}");

                    var translation = new SKPoint((float)node.UseTranslationX, (float)node.UseTranslationY);
                    chain.Transform.Translation += translation;
                    chain.Transform.Opacity *= (float)node.Opacity;
                    chain.Transform.Rotation += (float)node.Rotation;
                    chain.Transform.Scale = new SKPoint((float)(chain.Transform.Scale.X * node.ScaleX), (float)(chain.Transform.Scale.Y * node.ScaleY));

                    //var log = $"{node.GetType().Name} {translation} | ";
                    //Debug.WriteLine(log);
                    //chain.Transform.Logs += log;
                    chain.Transform.RenderedNodes++;
                }
            }
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

            GestureListeners.Remove(gestureListener);
        }

        protected object LockIterateListeners = new();



        /// <summary>
        /// Children we should check for touch hits
        /// </summary>
        public SortedGestureListeners GestureListeners { get; } = new();



        public SKRect DrawingRect
        {
            get
            {
                return new SKRect(0, 0, (float)(Width * RenderingScale), (float)(Height * RenderingScale));
            }
        }

        public void AddAnimator(ISkiaAnimator animator)
        {
            PostponeExecutionBeforeDraw(() =>
            {
                lock (LockAnimatingControls)
                {
                    animator.IsDeactivated = false;
                    AnimatingControls.TryAdd(animator.Uid, animator);
                }
            });

            Update();
        }

        public void RemoveAnimator(Guid uid)
        {
            lock (LockAnimatingControls)
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
            lock (LockAnimatingControls)
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
            lock (LockAnimatingControls)
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

        public virtual IEnumerable<ISkiaAnimator> SetViewTreeVisibilityByParent(SkiaControl parent, bool state)
        {
            lock (LockAnimatingControls)
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
            lock (LockAnimatingControls)
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

        public int ExecutePostAnimators(SkiaDrawingContext context, double scale)
        {
            var executed = 0;

            try
            {
                if (PostAnimators.Count == 0)
                    return executed;

                foreach (var skiaAnimation in PostAnimators)
                {
                    if (skiaAnimation.IsRunning && !skiaAnimation.IsPaused)
                    {
                        executed++;
                        var finished = skiaAnimation.TickFrame(context.FrameTimeNanos);
                        if (skiaAnimation is ICanRenderOnCanvas renderer)
                        {
                            var renderedrawn = renderer.Render(new DrawingContext(context, DrawingRect, (float)scale), this);
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

        protected object LockAnimatingControls = new();

        /// <summary>
        /// Executed after the rendering
        /// </summary>
        public List<IOverlayEffect> PostAnimators { get; } = new(128);

        List<Guid> _listRemoveAnimators = new(512);

        /// <summary>
        /// Tracking controls that what to be animated right now so we constantly refresh
        /// canvas until there is none left
        /// </summary>
        public Dictionary<Guid, ISkiaAnimator> AnimatingControls { get; } = new(512);

        protected int ExecuteAnimators(long nanos)
        {

            var executed = 0;

            //lock (LockAnimatingControls)
            {
                try
                {

                    if (AnimatingControls.Count == 0)
                        return executed;

                    _listRemoveAnimators.Clear();

                    foreach (var key in AnimatingControls.Keys)
                    {
                        var skiaAnimation = AnimatingControls[key];

                        if (skiaAnimation.IsDeactivated
                            || skiaAnimation.Parent != null && skiaAnimation.Parent.IsDisposed)
                        {
                            _listRemoveAnimators.Add(key);
                            continue;
                        }

                        bool canPlay = !skiaAnimation.IsHiddenInViewTree; //!(skiaAnimation.Parent != null && !skiaAnimation.Parent.IsVisibleInViewTree());

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
                                skiaAnimation.Pause();
                        }
                    }

                    foreach (var key in _listRemoveAnimators)
                    {
                        AnimatingControls.Remove(key);
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

        public void AttachCanvasView(ISkiaDrawable drawable)
        {
            if (drawable == null)
                return;

            if (ReferenceEquals(CanvasView, drawable) && rendererSet)
                return;

            CanvasView = drawable;

            if (!rendererSet)
            {
                rendererSet = true;
                ConnectedHandler();
                HandlerWasSet?.Invoke(this, rendererSet);
            }
        }

        public void SyncExternalSize(double width, double height)
        {
            if (Math.Abs(Width - width) < 0.5 && Math.Abs(Height - height) < 0.5)
                return;

            Width = width;
            Height = height;
            OnSizeChanged();
        }

        public bool RenderExternalSurface(SKSurface surface, SKRect rect, long frameTimeNanos)
        {
            if (CanvasView is SkiaView skiaView)
            {
                skiaView.AttachSurface(surface, rect, frameTimeNanos);
            }

            SyncExternalSize(rect.Width, rect.Height);

            return OnDrawSurface(surface, rect);
        }

        public ISkiaDrawable CanvasView
        {
            get => _canvasView;
            protected set
            {
                if (Equals(value, _canvasView)) return;
                _canvasView = value;
                renderedFrames = 0;
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
            }
        }

        private void OnMainDisplayInfoChanged(object sender, DisplayInfoChangedEventArgs e)
        {
            switch (e.DisplayInfo.Rotation)
            {
                case DisplayRotation.Rotation90:
                DeviceRotation = 90;
                break;
                case DisplayRotation.Rotation180:
                DeviceRotation = 180;
                break;
                case DisplayRotation.Rotation270:
                DeviceRotation = 270;
                break;
                case DisplayRotation.Rotation0:
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

            protected virtual void DisposePlatform()
            {
            }
        
            protected virtual void SetupRenderingLoop()
            {
            }
        private static void UpdateRotation(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView control)
            {
                control.DeviceRotationChanged?.Invoke(control, (int)newvalue); ;
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

            DisposePlatform();

            NeedCheckParentVisibility = true;
        }

        public long NeedGlobalRefreshCount { get; set; }

        private void OnNeedUpdate(object sender, EventArgs e)
        {
            NeedGlobalRefreshCount++;
            NeedCheckParentVisibility = true;
            Update();
        }

        public virtual void ConnectedHandler()
        {
            HasHandler = true;

            Super.NeedGlobalRefresh -= OnNeedUpdate;
            Super.NeedGlobalRefresh += OnNeedUpdate;

            SetupRenderingLoop();

            NeedCheckParentVisibility = true;
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
        /// Set this to true if you do not want the canvas to be redrawn as transparent and showing content below the canvas (splash?..) when UpdateLocked is True
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
            if (CanvasView != null)
            {
                CanvasView.Update();
                return;
            }

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
            return Screen.DisplayInfo.Density;
        }

        protected void SwapToDelayed()
        {
            Delayed = null;
        }

        /// <summary>
        /// Will safely destroy existing if any
        /// </summary>
        protected void CreateSkiaView()
        {
            DestroySkiaView();

            if (IsUsingHardwareAcceleration && HardwareAcceleration != HardwareAccelerationMode.Disabled)
            {
                var acceleratedView = new SkiaViewAccelerated(this);
                acceleratedView.OnDraw = (surface, rect) => OnDrawSurface(surface, rect);
                CanvasView = acceleratedView;
            }
            else
            {
                var softwareView = new SkiaView(this);
                softwareView.OnDraw = (surface, rect) => OnDrawSurface(surface, rect);
                CanvasView = softwareView;
            }

            Content = CanvasView as View;
        }

        protected void DestroySkiaView()
        {
            var kill = CanvasView;
            if (kill != null)
                CanvasView = null;
            if (kill != null)
            {
                kill.OnDraw = null;
                kill.Dispose();
            }
        }

        public bool IsDisposing { get; set; }

        public void Dispose()
        {
            IsDisposing = true;

            if (IsDisposed)
                return;

            this.SizeChanged -= OnFormsSizeChanged;

            OnDisposing();

            IsDisposed = true;

            DisposeManager.Dispose();

            Parent = null;

            PaintSystem?.Dispose();

            DestroySkiaView();

            Content = null;

            //this.Handler?.DisconnectHandler();

            ClearChildren();

            DisposePlatform();

        }
        /// <summary>
        /// Makes the control dirty, in need to be remeasured and rendered but this doesn't call Update, it's up yo you
        /// </summary>
        public virtual void Invalidate()
        {
            NeedMeasure = true;

            //InvalidateChildren();
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
            get
            {
                return NeedAutoHeight || NeedAutoWidth;
            }
        }

        public bool NeedAutoHeight
        {
            get
            {
                return VerticalOptions.Alignment != LayoutAlignment.Fill && HeightRequest < 0;
            }
        }
        public bool NeedAutoWidth
        {
            get
            {
                return HorizontalOptions.Alignment != LayoutAlignment.Fill && WidthRequest < 0;
            }
        }

        public virtual bool ShouldInvalidateByChildren
        {
            get
            {
                return NeedAutoSize;
            }
        }

        public static readonly BindableProperty UpdateLockedProperty = BindableProperty.Create(
            nameof(UpdateLocked),
            typeof(bool),
            typeof(DrawnView),
            false);

        public bool UpdateLocked
        {
            get { return (bool)GetValue(UpdateLockedProperty); }
            set { SetValue(UpdateLockedProperty, value); }
        }



        bool rendererSet;

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);


            if (propertyName == "Renderer")
            {

                if (rendererSet)
                {
                    //disconnect
                    DestroySkiaView();

                    HandlerWasSet?.Invoke(this, rendererSet);

                    Dispose();
                }
                else
                {
                    rendererSet = true;
                    //connect
                    CreateSkiaView();

                    HandlerWasSet?.Invoke(this, rendererSet);
                }

            }
            else
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
            };

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


        public virtual void OnDensityChanged()
        {
            Update(); //todo for children!!!!!
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
            else
            if (layoutHorizontal.Alignment == LayoutAlignment.End)
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
                else
                if (bottom > destination.Bottom)
                {
                    bottom = (float)(destination.Bottom);
                    top = bottom - rectHeight;
                }
            }
            else
            if (layoutVertical.Alignment == LayoutAlignment.End && DrawnExtensions.IsFinite(destination.Bottom))
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


        //-------------------------------------------------------------
        /// <summary>
        ///  destination in PIXELS, requests in UNITS. resulting Destination prop will be filed in PIXELS.
        /// </summary>
        /// <param name="destination">PIXELS</param>
        /// <param name="widthRequest">UNITS</param>
        /// <param name="heightRequest">UNITS</param>
        /// <param name="scale"></param>
        public virtual void Arrange(SKRect destination, double widthRequest, double heightRequest, double scale = 1.0)
        //-------------------------------------------------------------
        {


            Destination = CalculateLayout(destination, widthRequest, heightRequest, scale);

            if (Destination.Width < 0 || Destination.Height < 0)
            {
                var stop = true;
            }
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

        protected virtual bool NeedMeasure { get; set; } = true;

        protected ScaledSize SetMeasured(float width, float height, float scale)
        {
            NeedMeasure = false;

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

        public static long GetNanoseconds()
        {
            double timestamp = Stopwatch.GetTimestamp();
            double nanoseconds = 1_000_000_000.0 * timestamp / Stopwatch.Frequency;

            return (long)nanoseconds;
        }

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

        protected bool WasRendered { get; set; }

        public event EventHandler<SkiaDrawingContext?> WasDrawn;
        public event EventHandler<SkiaDrawingContext?> WillDraw;
        public event EventHandler<SkiaDrawingContext?> WillFirstTimeDraw;

        private bool OnDrawSurface(SKSurface surface, SKRect rect)
        {
            //lock (LockDraw)
            {
                var canvas = surface?.Canvas;

                if (!OnStartRendering(canvas))
                    return UpdateMode == DrawnUi.Infrastructure.Enums.UpdateMode.Constant;

                try
                {
                    canvas.Clear(SKColors.Transparent);

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

                    Draw(args, rect, (float)RenderingScale);
                }
                finally
                {
                    OnFinalizeRendering();

                    WasRendered = true;
                }

                return IsDirty || UpdateMode == DrawnUi.Infrastructure.Enums.UpdateMode.Constant;
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

            if (UpdateMode != DrawnUi.Infrastructure.Enums.UpdateMode.Constant)
            {
                IsDirty = false; //any control can set to true after that
            }

            return true;
        }

        protected virtual void OnFinalizeRendering()
        {

            TimeDrawingComplete = DateTime.Now;

            IsRendering = false;

            if (UpdateMode == DrawnUi.Infrastructure.Enums.UpdateMode.Constant)
                IsDirty = true;

            if (IsDirty)
            {
                Update();
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

            DeviceDisplay.MainDisplayInfoChanged -= OnMainDisplayInfoChanged;

            Debug.WriteLine($"[DrawnUI] Disposing {this}");
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
        /// Frame finished rendering nanoseconds
        /// </summary>
        public long FrameFinishedTime { get; protected set; }

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

        public int UpdateLocks => 0;

        long renderedFrames;


        #region DISPOSE STUFF

        public void DisposeObject(IDisposable resource, [CallerMemberName] string caller = null)
        {
            if (this.IsDisposed)
                return;

            DisposeManager.EnqueueDisposable(resource);
        }

        protected DisposableManager DisposeManager { get; } = new(3.5);

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

        public class DisposableManager : IDisposable
        {
            private readonly ConcurrentQueue<TimedDisposable> _toBeDisposed = new ConcurrentQueue<TimedDisposable>();
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
            private readonly Task _disposalTask;
            private readonly double _disposeDelaySeconds;
            private bool _disposed = false;

            /// <summary>
            /// Initializes a new instance of the DisposableManager class.
            /// </summary>
            /// <param name="disposeDelaySeconds">The delay in seconds before an IDisposable is disposed after being enqueued. Default is 3.5 seconds.</param>
            public DisposableManager(double disposeDelaySeconds = 3.5)
            {
                if (disposeDelaySeconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(disposeDelaySeconds), "Dispose delay must be positive.");

                _disposeDelaySeconds = disposeDelaySeconds;
                _disposalTask = Task.Run(() => PeriodicDisposeAsync(_cancellationTokenSource.Token));
            }

            /// <summary>
            /// Enqueues an IDisposable object with the current timestamp.
            /// </summary>
            /// <param name="disposable">The IDisposable object to enqueue.</param>
            public void EnqueueDisposable(IDisposable disposable)
            {
                if (disposable == null)
                    return;

                var timedDisposable = new TimedDisposable(disposable);
                _toBeDisposed.Enqueue(timedDisposable);
            }

            /// <summary>
            /// Periodically disposes of eligible disposables.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token to stop the disposal loop.</param>
            private async Task PeriodicDisposeAsync(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        DisposeDisposables();
                        await Task.Delay(500, cancellationToken); //.ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellationToken is canceled
                }
                catch (Exception ex)
                {
                    // Log unexpected exceptions
                    LogError(ex);
                }
            }

            /// <summary>
            /// Disposes of all IDisposable objects that have been in the queue for more than the specified delay.
            /// </summary>
            private void DisposeDisposables()
            {
                DateTime now = DateTime.UtcNow;

                while (_toBeDisposed.TryPeek(out var timedDisposable))
                {
                    var elapsed = now - timedDisposable.EnqueuedTime;
                    if (elapsed.TotalSeconds >= _disposeDelaySeconds)
                    {
                        if (_toBeDisposed.TryDequeue(out var disposableToDispose))
                        {
                            try
                            {
                                disposableToDispose.Dispose();
                            }
                            catch (Exception ex)
                            {
                                // Log the exception and continue disposing other items
                                LogError(ex);
                            }
                        }
                    }
                    else
                    {
                        // Since the queue is FIFO, no need to check further
                        break;
                    }
                }
            }


            private void LogError(Exception ex)
            {
                Super.Log(ex);
            }

            /// <summary>
            /// Disposes all remaining disposables immediately and stops the periodic disposal task.
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                // Cancel the disposal loop
                _cancellationTokenSource.Cancel();

                try
                {
                    // Wait for the disposal task to complete
                    _disposalTask.Wait();
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex =>
                    {
                        LogError(ex);
                        return true;
                    });
                }

                // Dispose remaining disposables
                DisposeAllRemaining();

                // Dispose the cancellation token source
                _cancellationTokenSource.Dispose();
            }

            /// <summary>
            /// Disposes all remaining IDisposable objects in the queue.
            /// </summary>
            private void DisposeAllRemaining()
            {
                List<TimedDisposable> remainingDisposables = new List<TimedDisposable>();

                while (_toBeDisposed.TryDequeue(out var timedDisposable))
                {
                    remainingDisposables.Add(timedDisposable);
                }

                foreach (var disposable in remainingDisposables)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Log the exception and continue disposing other items
                        LogError(ex);
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// For debugging purposes check if dont have concurrent threads
        /// </summary>
        public int DrawingThreads { get; protected set; }



        public static readonly BindableProperty TintColorProperty = BindableProperty.Create(nameof(TintColor), typeof(Color), typeof(DrawnView),
            Colors.Transparent,
            propertyChanged: RedrawCanvas);
        public Color TintColor
        {
            get { return (Color)GetValue(TintColorProperty); }
            set { SetValue(TintColorProperty, value); }
        }

        public static readonly BindableProperty ClearColorProperty = BindableProperty.Create(nameof(ClearColor), typeof(Color), typeof(DrawnView),
            Colors.Transparent,
            propertyChanged: RedrawCanvas);
        public Color ClearColor
        {
            get { return (Color)GetValue(ClearColorProperty); }
            set { SetValue(ClearColorProperty, value); }
        }

        public static readonly BindableProperty RenderingScaleProperty = BindableProperty.Create(nameof(RenderingScale), typeof(float), typeof(DrawnView),
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
                SetValue(RenderingScaleProperty, value);
            }
        }

        private float _renderingScale = -1;


        private static void OnHardwareModeChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView control && control.rendererSet)
            {
                control.CreateSkiaView();
            }
        }

        public static readonly BindableProperty HardwareAccelerationProperty = BindableProperty.Create(nameof(HardwareAcceleration),
        typeof(HardwareAccelerationMode),
        typeof(DrawnView),
        HardwareAccelerationMode.Disabled, propertyChanged: OnHardwareModeChanged);

        public HardwareAccelerationMode HardwareAcceleration
        {
            get { return (HardwareAccelerationMode)GetValue(HardwareAccelerationProperty); }
            set { SetValue(HardwareAccelerationProperty, value); }
        }

        public static readonly BindableProperty CanRenderOffScreenProperty = BindableProperty.Create(nameof(CanRenderOffScreen),
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
                return CanvasView != null && !IsDisposed && IsVisible && rendererSet && canRenderOffScreen;
            }
        }

        /// <summary>
        /// Indicates that view is either hidden or offscreen.
        /// This disables rendering if you don't set CanRenderOffScreen to true
        /// </summary>
        public bool IsHiddenInViewTree { get; protected set; }


        public bool NeedCheckParentVisibility
        {
            get
            {
                return _needCheckParentVisibility;
            }

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

        bool _needCheckParentVisibility = true;

        private long _globalRefresh;



        public static MemoryStream StreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }


        protected SKPaint PaintSystem { get; set; }

        public SKRect Destination { get; protected set; }

        public void PaintTintBackground(SKCanvas canvas)
        {
            if (TintColor != null && TintColor != Colors.Transparent)
            {
                if (PaintSystem == null)
                {
                    PaintSystem = new SKPaint();
                }
                PaintSystem.Style = SKPaintStyle.StrokeAndFill;
                PaintSystem.Color = TintColor.ToSKColor();
                canvas.DrawRect(Destination, PaintSystem);
            }
        }

        public void PaintClearBackground(SKCanvas canvas)
        {
            if (ClearColor != Colors.Transparent)
            {
                if (PaintSystem == null)
                {
                    PaintSystem = new SKPaint();
                }
                PaintSystem.Style = SKPaintStyle.StrokeAndFill;
                PaintSystem.Color = ClearColor.ToSKColor();
                canvas.DrawRect(Destination, PaintSystem);
            }
        }

        //static int countRedraws = 0;
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
                view.BindingContext = BindingContext;
            }
        }

        #region GRADIENTS

        public SKShader CreateGradientAsShader(SKRect destination, SkiaGradient gradient)
        {
            if (gradient != null && gradient.Type != GradientType.None)
            {
                var colors = new List<SKColor>();
                foreach (var color in gradient.Colors)
                {
                    var usingColor = color;
                    if (gradient.Light < 1.0)
                    {
                        usingColor = usingColor.MakeDarker(100 - gradient.Light * 100);
                    }
                    else if (gradient.Light > 1.0)
                    {
                        usingColor = usingColor.MakeLighter(gradient.Light * 100 - 100);
                    }

                    var newAlpha = usingColor.A * gradient.Opacity;
                    usingColor = usingColor.WithAlpha(newAlpha);
                    colors.Add(usingColor.ToSKColor());
                }

                float[] colorPositions = null;
                if (gradient.ColorPositions?.Count == colors.Count)
                {
                    colorPositions = gradient.ColorPositions.Select(x => (float)x).ToArray();
                }

                switch (gradient.Type)
                {
                    case GradientType.Sweep:

                    return SKShader.CreateSweepGradient(
                         new SKPoint(destination.Left + destination.Width / 2.0f,
                            destination.Top + destination.Height / 2.0f),
                        colors.ToArray(),
                        colorPositions,
                        gradient.TileMode, (float)Value1, (float)(Value1 + Value2));

                    case GradientType.Circular:
                    return SKShader.CreateRadialGradient(
                        new SKPoint(destination.Left + destination.Width / 2.0f,
                            destination.Top + destination.Height / 2.0f),
                        Math.Max(destination.Width, destination.Height) / 2.0f,
                        colors.ToArray(),
                        colorPositions,
                        gradient.TileMode);

                    case GradientType.Linear:
                    default:
                    return SKShader.CreateLinearGradient(
                        new SKPoint(destination.Left + destination.Width * gradient.StartXRatio,
                            destination.Top + destination.Height * gradient.StartYRatio),
                        new SKPoint(destination.Left + destination.Width * gradient.EndXRatio,
                            destination.Top + destination.Height * gradient.EndYRatio),
                        colors.ToArray(),
                        colorPositions,
                        gradient.TileMode);
                    break;
                }

            }

            return null;
        }



        #endregion

        #region SUBVIEWS

        public List<SkiaControl> Views { get; } = new();

        public virtual void ClearChildren()
        {
            foreach (var child in Views.ToList())
            {
                RemoveSubView(child);
                //child.Dispose();
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
            OnDrawnChildAdded(control);

            if (Debugger.IsAttached)
                ReportHotreloadChildAdded(control);
        }

        public virtual void ReportHotreloadChildAdded(SkiaControl child)
        {
            if (child == null)
                return;

            OnChildAdded(child);
        }

        public void RemoveSubView(SkiaControl control)
        {
            if (control == null)
                return;

            if (Debugger.IsAttached)
                ReportHotreloadChildRemoved(control);

            control.SetParent(null);
            OnDrawnChildRemoved(control);
        }

        public virtual void ReportHotreloadChildRemoved(SkiaControl control)
        {
            if (control == null)
                return;

            var index = Views.IndexOf(control);
            OnChildRemoved(control, index);
        }

        protected virtual void OnDrawnChildAdded(SkiaControl child)
        {
            InvalidateViewsList();
        }

        protected virtual void OnDrawnChildRemoved(SkiaControl child)
        {
            InvalidateViewsList();
        }



        #endregion

        #region Children

#pragma warning disable NU1605, CS0108

        private IList<SkiaControl> _children;

        public IList<SkiaControl> Children
        {
            get
            {
                if (_children == null)
                {
                    var created = new ObservableCollection<SkiaControl>();
                    created.CollectionChanged += OnChildrenCollectionChanged;
                    _children = created;
                }
                return _children;
            }
            set
            {
                if (_children == value)
                    return;

                var oldValue = _children;

                if (oldValue is INotifyCollectionChanged oldCollection)
                    oldCollection.CollectionChanged -= OnChildrenCollectionChanged;

                if (oldValue != null)
                {
                    foreach (var subView in oldValue)
                        AddOrRemoveView(subView, false);
                }

                _children = value;

                SetChildren(value);

                if (value is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= OnChildrenCollectionChanged;
                    newCollection.CollectionChanged += OnChildrenCollectionChanged;
                }

                Update();
            }
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

        private static void NeedInvalidate(BindableObject bindable, object oldvalue, object newvalue)
        {
            ((DrawnView)bindable).Invalidate();
        }

        protected virtual void OnSizeChanged()
        {
            Invalidate();
        }

        public static readonly BindableProperty MaximumWidthRequestProperty = BindableProperty.Create(
            nameof(MaximumWidthRequest),
            typeof(double),
            typeof(DrawnView),
            -1.0,
            propertyChanged: NeedInvalidate);

        public double MaximumWidthRequest
        {
            get { return (double)GetValue(MaximumWidthRequestProperty); }
            set { SetValue(MaximumWidthRequestProperty, value); }
        }

        public static readonly BindableProperty MaximumHeightRequestProperty = BindableProperty.Create(
            nameof(MaximumHeightRequest),
            typeof(double),
            typeof(DrawnView),
            -1.0,
            propertyChanged: NeedInvalidate);

        public double MaximumHeightRequest
        {
            get { return (double)GetValue(MaximumHeightRequestProperty); }
            set { SetValue(MaximumHeightRequestProperty, value); }
        }

        public static readonly BindableProperty UpdateModeProperty = BindableProperty.Create(
            nameof(UpdateMode),
            typeof(DrawnUi.Infrastructure.Enums.UpdateMode),
            typeof(DrawnView),
            DrawnUi.Infrastructure.Enums.UpdateMode.Dynamic, propertyChanged: ChangeUpdateMode);

        private static void ChangeUpdateMode(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is DrawnView control)
            {
                control.Update();
            }
        }

        public DrawnUi.Infrastructure.Enums.UpdateMode UpdateMode
        {
            get { return (DrawnUi.Infrastructure.Enums.UpdateMode)GetValue(UpdateModeProperty); }
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
            get
            {
                return _FocusLocked;
            }
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

        private ISkiaDrawable _canvasView;
        private object _handler;

        public void ReportFocus(ISkiaGestureListener value, ISkiaGestureListener setter = null)
        {
            if (_focusedChild != value && !FocusLocked)
            {
                if (_focusedChild != null)
                {
                    Debug.WriteLine($"[UNFOCUSED] {_focusedChild}");
                    if (_focusedChild != setter || setter == null)
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

                    if (value != null)
                    {
                        FocusedItemChanged?.Invoke(this, new(value as SkiaControl, true));
                    }
                }

                _focusedChild = value;
                Debug.WriteLine($"[FOCUSED] {_focusedChild}");

                if (_focusedChild == null)
                {
                    Debug.WriteLine($"[FOCUSED] {_focusedChild}");

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
                                this.Focus();
                            }
                            catch (Exception e)
                            {
                                Super.Log(e);
                            }

                            TouchEffect.CloseKeyboard();
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

        public ISkiaGestureListener FocusedChild
        {
            get
            {
                return _focusedChild;
            }
            set
            {
                ReportFocus(value);
            }
        }
        ISkiaGestureListener _focusedChild;

        public virtual void ClipSmart(SKCanvas canvas, SKPath path, SKClipOperation operation = SKClipOperation.Intersect)
        {
            canvas.ClipPath(path, operation, false);
        }
    }
}
