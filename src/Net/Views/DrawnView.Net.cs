using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Color = DrawnUi.Color;

namespace DrawnUi.Views
{
    [ContentProperty("Children")]
    public partial class DrawnView : ContentView,
        IDrawnBase, IAnimatorsManager
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdatePlatform()
        {
            IsDirty = true;
        }

        protected virtual void PlatformHardwareAccelerationChanged()
        {
        }

        protected virtual void InitFramework(bool subscribe)
        {
        }

        protected void FixDensity()
        {
            Width = CanvasView.CanvasSize.Width;
            Height = CanvasView.CanvasSize.Height;

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

        protected Dictionary<Guid, SkiaControl> DirtyChildren = new();

        protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            var measured = this.Measure((float)widthConstraint, (float)heightConstraint);

            return new SizeRequest(new(measured.Units.Width, measured.Units.Height),
                new(measured.Units.Width, measured.Units.Height));
        }

        public void DestroyThis()
        {
        }

        public new object Handler = new();

        private View _visibilityParent;

        public bool GetIsVisibleWithParent(View element)
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

                if (element.Parent is View visualParent)
                {
                    return GetIsVisibleWithParent(visualParent);
                }
            }

            return true;
        }

        public virtual ScaledRect GetOnScreenVisibleArea(float inflateByPixels = 0)
        {
            var bounds = new SKRect(0 - inflateByPixels, 0 - inflateByPixels,
                (int)(Width * RenderingScale + inflateByPixels), (int)(Height * RenderingScale + inflateByPixels));

            return ScaledRect.FromPixels(bounds, (float)RenderingScale);
        }

        protected Dictionary<SkiaControl, VisualTreeChain> RenderingTrees = new(128);

        public void RegisterRenderingChain(VisualTreeChain chain)
        {
            RenderingTrees.TryAdd(chain.Child, chain);

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
            foreach (VisualTreeChain chain in RenderingTrees.Values)
            {
                if (chain.NodeIndices.TryGetValue(node, out int index))
                {
                    if (index == 0)
                    {
                        chain.Transform = new VisualTransform();
                    }

                    chain.Transform.IsVisible = chain.Nodes.All(x => x.IsVisible) && node.CanDraw;

                    var translation = new SKPoint((float)node.UseTranslationX, (float)node.UseTranslationY);
                    chain.Transform.Translation += translation;
                    chain.Transform.Opacity *= (float)node.Opacity;
                    chain.Transform.Rotation += (float)node.Rotation;
                    chain.Transform.Scale = new SKPoint((float)(chain.Transform.Scale.X * node.ScaleX),
                        (float)(chain.Transform.Scale.Y * node.ScaleY));

                    chain.Transform.RenderedNodes++;
                }
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
                            renderer.Render(new DrawingContext(context, DrawingRect, (float)scale), this);
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
            SyncExternalSize(rect.Width, rect.Height);
            return OnDrawSurface(surface, rect);
        }

        protected bool DrawFrame(SKSurface surface, SKRect rect)
        {
            SyncExternalSize(rect.Width, rect.Height);
            return OnDrawSurface(surface, rect);
        }

        partial void OnCanvasViewChangedPlatform()
        {
            CanvasViewVersion++;
        }

        private void Init()
        {
            if (!_initialized)
            {
                _initialized = true;
                HorizontalOptions = LayoutOptions.Start;
                VerticalOptions = LayoutOptions.Start;
                Padding = new Thickness(0);

                SizeChanged += ViewSizeChanged;

                InitFramework(true);

                SurfaceCacheManager = new(this);
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
            Super.OnFrame -= OnNetFrame;
        }

        protected virtual void SetupRenderingLoop()
        {
            Super.EnsureFrameLoopStarted();
            Super.OnFrame -= OnNetFrame;
            Super.OnFrame += OnNetFrame;
        }

        private void OnNetFrame(object sender, EventArgs e)
        {
            if (NeedCheckParentVisibility)
            {
                CheckElementVisibility(this);
            }

            if (CheckCanDraw() && CanDraw)
            {
                CanvasView.Update();
            }
        }

        public bool CheckCanDraw()
        {
            return
                IsDirty &&
                CanvasView != null
                && !CanvasView.IsDrawing
                && !(UpdateLocks > 0 && StopDrawingWhenUpdateIsLocked)
                && IsVisible && Super.EnableRendering;
        }

        public int DeviceRotation
        {
            get { return (int)GetValue(DisplayRotationProperty); }
            set { SetValue(DisplayRotationProperty, value); }
        }

        public bool OrderedDraw { get; protected set; }

        protected int CanvasViewVersion { get; private set; }

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

        public static long GetNanoseconds()
        {
            double timestamp = Stopwatch.GetTimestamp();
            double nanoseconds = 1_000_000_000.0 * timestamp / Stopwatch.Frequency;
            return (long)nanoseconds;
        }

        long renderedFrames;
        private object _handler;

        public static readonly BindableProperty TintColorProperty = BindableProperty.Create(nameof(TintColor),
            typeof(Color), typeof(DrawnView),
            Colors.Transparent,
            propertyChanged: RedrawCanvas);

        public Color TintColor
        {
            get { return (Color)GetValue(TintColorProperty); }
            set { SetValue(TintColorProperty, value); }
        }

        public static readonly BindableProperty ClearColorProperty = BindableProperty.Create(nameof(ClearColor),
            typeof(Color), typeof(DrawnView),
            Colors.Transparent,
            propertyChanged: RedrawCanvas);

        public Color ClearColor
        {
            get { return (Color)GetValue(ClearColorProperty); }
            set { SetValue(ClearColorProperty, value); }
        }

        public static readonly BindableProperty HardwareAccelerationProperty = BindableProperty.Create(
            nameof(HardwareAcceleration),
            typeof(HardwareAccelerationMode),
            typeof(DrawnView),
            HardwareAccelerationMode.Disabled, propertyChanged: OnHardwareModeChanged);

        public HardwareAccelerationMode HardwareAcceleration
        {
            get { return (HardwareAccelerationMode)GetValue(HardwareAccelerationProperty); }
            set { SetValue(HardwareAccelerationProperty, value); }
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
                }
            }

            return null;
        }

        #endregion

        #region SUBVIEWS

        protected virtual void OnDrawnChildAdded(SkiaControl child)
        {
            InvalidateViewsList();
        }

        protected virtual void OnDrawnChildRemoved(SkiaControl child)
        {
            InvalidateViewsList();
        }

        #endregion

        private static void NeedInvalidate(BindableObject bindable, object oldvalue, object newvalue)
        {
            ((DrawnView)bindable).Invalidate();
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

        public virtual void ReportHotreloadChildRemoved(SkiaControl control)
        {
        }
    }
}
