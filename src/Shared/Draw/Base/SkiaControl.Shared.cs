using System.Resources;
using HarfBuzzSharp;
using SKBlendMode = SkiaSharp.SKBlendMode;
using SKCanvas = SkiaSharp.SKCanvas;
using SKClipOperation = SkiaSharp.SKClipOperation;
using SKColor = SkiaSharp.SKColor;
using SKFilterQuality = SkiaSharp.SKFilterQuality;
using SKMatrix = SkiaSharp.SKMatrix;
using SKPaint = SkiaSharp.SKPaint;
using SKPaintStyle = SkiaSharp.SKPaintStyle;
using SKPath = SkiaSharp.SKPath;
using SKPathOp = SkiaSharp.SKPathOp;
using SKPoint = SkiaSharp.SKPoint;
using SKRect = SkiaSharp.SKRect;
using SKShader = SkiaSharp.SKShader;
using SKSize = SkiaSharp.SKSize;

namespace DrawnUi.Draw
{
    [DebuggerDisplay("{DebugString}")]
    public partial class SkiaControl :
        ISkiaGestureListener,
        IHasAfterEffects,
        ISkiaControl
    {
        public SkiaControl()
        {
            Init();
        }

        public virtual bool OnFocusChanged(bool focus)
        {
            return false;
        }

        public VisualLayer? VisualLayer { get; set; }

        private void Init()
        {
            NeedMeasure = true;
            IsLayoutDirty = true;

            SizeChanged += ViewSizeChanged;

            CalculateMargins();
            CalculateSizeRequest();

            AttachEffects();
        }

        /// <summary>
        /// Provides a cached  image if any
        /// </summary>
        public virtual SKImage CachedImage
        {
            get { return RenderObject?.Image; }
        }

        /// <summary>
        /// Offset cache (RenderObject) in points.
        /// This works similar to TranslationX but uses no matrix transform, works faster.
        /// For code-behind fast reposition of cached controls, background thread friendly, no bindings involved.
        /// Cached controls only, not a bindable property, doesn't trigger repaint, would need to do this manually if needed.
        /// </summary>
        public double Left { get; set; }

        /// <summary>
        /// Offset cache (RenderObject) in points.
        /// This works similar to TranslationY but uses no matrix transform, works faster.
        /// For code-behind fast reposition of cached controls, background thread friendly, no bindings involved.
        /// Cached controls only, not a bindable property, doesn't trigger repaint, would need to do this manually if needed.
        /// </summary>
        public double Top { get; set; }

        public static readonly BindableProperty ControlStyleProperty = BindableProperty.Create(
            nameof(PrebuiltControlStyle),
            typeof(PrebuiltControlStyle), typeof(SkiaControl),
            PrebuiltControlStyle.Unset,
            propertyChanged: NeedInitialize);

        private static void NeedInitialize(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaControl control)
            {
                control.InitializeDefaultContent(true);
            }
        }

        /// <summary>
        /// Will be used by control CreateDefaultContent to create appropriate look. Then controls use a virtual `UsingControlStyle` property to define their look.
        /// </summary>
        public PrebuiltControlStyle ControlStyle
        {
            get { return (PrebuiltControlStyle)GetValue(ControlStyleProperty); }
            set { SetValue(ControlStyleProperty, value); }
        }

        public static readonly BindableProperty ClippedEffectsWithProperty = BindableProperty.Create(
            nameof(ClippedEffectsWith),
            typeof(SkiaControl),
            typeof(SkiaControl),
            null);

        /// <summary>
        /// Can specify another control that would provide the clipping region to be used by post animattors etc.
        /// </summary>
        public SkiaControl ClippedEffectsWith
        {
            get { return (SkiaControl)GetValue(ClippedEffectsWithProperty); }
            set { SetValue(ClippedEffectsWithProperty, value); }
        }

        public static readonly BindableProperty ClipWithProperty = BindableProperty.Create(
            nameof(ClipWith),
            typeof(SkiaControl),
            typeof(SkiaControl),
            null);

        /// <summary>
        /// Can specify another control that would provide the clipping region for this one. 
        /// </summary>
        public SkiaControl ClipWith
        {
            get { return (SkiaControl)GetValue(ClipWithProperty); }
            set { SetValue(ClipWithProperty, value); }
        }

        public static readonly BindableProperty ChildrenProperty = BindableProperty.Create(
            nameof(Children),
            typeof(IList<SkiaControl>),
            typeof(SkiaControl),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableAttachedItemsCollection<SkiaControl>();
                created.CollectionChanged += ((SkiaControl)instance).OnChildrenCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaControl>,
            propertyChanged: ChildrenPropertyChanged);

        /// <summary>
        /// This is used by MAUI to set content from XAML. 
        /// </summary>
        public IList<SkiaControl> Children
        {
            get => (IList<SkiaControl>)GetValue(ChildrenProperty);
            set => SetValue(ChildrenProperty, value);
        }

        protected static SKBlendMode DefaultBlendMode = SKBlendMode.SrcOver;

        public virtual bool IsVisibleInViewTree()
        {
            var isVisible = IsVisible && !IsDisposed;

            var parent = this.Parent as SkiaControl;

            if (parent == null)
                return isVisible;

            if (isVisible)
                return parent.IsVisibleInViewTree();

            return false;
        }

        /// <summary>
        /// Absolute position in points
        /// </summary>
        /// <returns></returns>
        public virtual SKPoint GetPositionOnCanvasInPoints()
        {
            var position = GetPositionOnCanvas();

            return new(position.X / RenderingScale, position.Y / RenderingScale);
        }

        /// <summary>
        /// Absolute position in points
        /// </summary>
        /// <returns></returns>
        public virtual SKPoint GetFuturePositionOnCanvasInPoints(bool useTranslation = true)
        {
            var position = GetFuturePositionOnCanvas();

            return new(position.X / RenderingScale, position.Y / RenderingScale);
        }

        /// <summary>
        /// Absolute position in pixels after drawn.
        /// </summary>
        /// <returns></returns>
        public virtual SKPoint GetPositionOnCanvas()
        {
            //if (LastVisualNode != null)
            //{
            //    return LastVisualNode.HitBoxWithTransforms.Pixels.Location;
            //}

            var position = BuildDrawnOffsetRecursive(LastDrawnAt.Location, this, true, true);
            return new(position.X, position.Y);
        }

        /// <summary>
        /// Absolute position in pixels before drawn.
        /// </summary>
        /// <returns></returns>
        public virtual SKPoint GetFuturePositionOnCanvas()
        {
            var position = BuildDrawnOffsetRecursive(DrawingRect.Location, this, true, true);
            return new(position.X, position.Y);
        }

        /// <summary>
        /// Find drawing position for control accounting for all caches up the rendering tree.
        /// </summary>
        /// <returns></returns>
        public virtual SKPoint GetSelfDrawingPosition()
        {
            var position = BuildSelfDrawingPosition(LastDrawnAt.Location, this, true);

            return new(position.X, position.Y);
        }

        public SKPoint BuildSelfDrawingPosition(SKPoint offset, SkiaControl control, bool isChild)
        {
            if (control == null)
            {
                return offset;
            }

            var drawingOffset = SKPoint.Empty;
            if (!isChild)
            {
                drawingOffset = control.GetPositionOffsetInPixels(true);
                drawingOffset.Offset(offset);
            }
            else
            {
                drawingOffset = offset;
            }

            var parent = control.Parent as SkiaControl;
            if (parent == null)
            {
                return drawingOffset;
            }

            return BuildSelfDrawingPosition(drawingOffset, parent, false);
        }

        public SKPoint BuildDrawingOffsetRecursive(SKPoint offset, SkiaControl control, bool ignoreCache,
            bool useTranslation = true)
        {
            if (control == null)
            {
                return offset;
            }

            var drawingOffset = control.GetFuturePositionOffsetInPixels(false, ignoreCache);
            drawingOffset.Offset(offset);

            //if (UsingCacheType != SkiaCacheType.None)
            //{
            //    drawingOffset.Offset((float)(Left * RenderingScale), (float)(Top * RenderingScale));
            //}

            var parent = control.Parent as SkiaControl;
            if (parent == null)
            {
                drawingOffset.Offset(control.DrawingRect.Location);
                return drawingOffset;
            }

            return BuildDrawingOffsetRecursive(drawingOffset, parent, false, useTranslation);
        }

        public SKPoint BuildDrawnOffsetRecursive(SKPoint offset, SkiaControl control, bool ignoreCache,
            bool useTranslation = true)
        {
            if (control == null)
            {
                return offset;
            }

            var drawingOffset = control.GetPositionOffsetInPixels(false, ignoreCache);
            drawingOffset.Offset(offset);

            //if (UsingCacheType != SkiaCacheType.None)
            //{
            //    drawingOffset.Offset((float)(Left * RenderingScale), (float)(Top * RenderingScale));
            //}

            var parent = control.Parent as SkiaControl;
            if (parent == null)
            {
                drawingOffset.Offset(control.LastDrawnAt.Location);
                return drawingOffset;
            }

            return BuildDrawnOffsetRecursive(drawingOffset, parent, false, useTranslation);
        }

        public virtual string DebugString
        {
            get
            {
                return
                    $"{GetType().Name} Tag {Tag}, IsVisible {IsVisible}, Children {Views.Count}, {Width:0.0}x{Height:0.0}dp, DrawingRect: {DrawingRect}";
            }
        }

        public virtual bool CanDraw
        {
            get
            {
                if (!IsVisible || IsDisposed || IsDisposing || SkipRendering)
                    return false;

                //if (Superview != null && !Superview.CanDraw)
                //{
                //    return false;
                //}

                return true;
            }
        }

        //public virtual bool ShouldMeasure
        //{
        //    get
        //    {
        //        if (!IsVisible || IsDisposed || IsDisposing || SkipRendering)
        //            return false;

        //        return true;
        //    }
        //}

        /// <summary>
        /// Can be set but custom controls while optimizing rendering etc. Will affect CanDraw.
        /// </summary>
        public bool SkipRendering { get; set; }

        protected bool DefaultContentCreated { get; set; }

        protected virtual void CreateDefaultContent()
        {
        }

        protected virtual void SetDefaultMinimumContentSize(double width, double height)
        {
            if (width > 0)
            {
                if (this.MinimumWidthRequest < 0 && HorizontalOptions.Alignment != LayoutAlignment.Fill &&
                    (LockRatio == 0 || MinimumWidthRequest < 0))
                    this.MinimumWidthRequest = width;
            }

            if (height > 0)
            {
                if (this.MinimumHeightRequest < 0 && VerticalOptions.Alignment != LayoutAlignment.Fill &&
                    (LockRatio == 0 || MinimumHeightRequest < 0))
                    this.MinimumHeightRequest = height;
            }
        }

        protected virtual void SetDefaultContentSize(double width, double height)
        {
            if (width > 0)
            {
                if (this.WidthRequest < 0 && HorizontalOptions.Alignment != LayoutAlignment.Fill &&
                    (LockRatio == 0 || HeightRequest < 0))
                    this.WidthRequest = width;
            }

            if (height > 0)
            {
                if (this.HeightRequest < 0 && VerticalOptions.Alignment != LayoutAlignment.Fill &&
                    (LockRatio == 0 || WidthRequest < 0))
                    this.HeightRequest = height;
            }
        }

        /// <summary>
        /// This actually used by SkiaMauiElement but could be used by other controls. Also might be useful for debugging purposes.
        /// </summary>
        /// <returns></returns>
        public VisualTreeChain GenerateParentChain()
        {
            var currentParent = this.Parent as SkiaControl;

            var chain = new VisualTreeChain(this);

            var parents = new List<SkiaControl>();

            // Traverse up the parent hierarchy
            while (currentParent != null)
            {
                // Add the current parent to the chain
                parents.Add(currentParent);
                // Move to the next parent
                currentParent = currentParent.Parent as SkiaControl;
            }

            parents.Reverse();

            foreach (var parent in parents)
            {
                chain.AddNode(parent);
            }

            return chain;
        }

        /// <summary>
        /// Comes after all drawn, actually used by SkiaMauiElement only
        /// </summary>
        /// <param name="transform"></param>
        public virtual void SetVisualTransform(VisualTransform transform)
        {
        }

        /// <summary>
        /// Apply all postponed invalidation other logic that was postponed until
        /// the first draw for optimization.
        /// Use this for special code-behind cases, like tests etc,
        /// if you cannot wait until the first Draw().
        /// In this version this affects ItemsSource only.
        /// </summary>
        public void CommitInvalidations()
        {
            foreach (var invalidation in PostponedInvalidations)
            {
                invalidation.Value.Invoke();
            }

            PostponedInvalidations.Clear();
        }

        public virtual void SuperViewChanged()
        {
            if (Superview != null)
            {
                CommitInvalidations();
                if (Super.Multithreaded && CanUseCacheDoubleBuffering)
                    Update();
            }
        }

        /// <summary>
        /// Used for optimization process, for example, to avoid changing ItemSource several times before the first draw.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        public void PostponeInvalidation(string key, Action action)
        {
            if (Superview == null)
            {
                PostponedInvalidations[key] = action;
            }
            else
            {
                //action.Invoke();
                Superview.PostponeInvalidation(this, action);
            }
        }

        readonly Dictionary<string, Action> PostponedInvalidations = new();

        /// <summary>
        /// Returns rendering scale adapted for another output size, useful for offline rendering
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public float GetRenderingScaleFor(float width, float height)
        {
            var aspectX = width / DrawingRect.Width;
            var aspectY = height / DrawingRect.Height;
            var scale = Math.Min(aspectX, aspectY) * RenderingScale;
            return scale;
        }

        public float GetRenderingScaleFor(float measure)
        {
            var aspectX = measure / DrawingRect.Width;
            var scale = aspectX * RenderingScale;
            return scale;
        }

        /// <summary>
        /// Creates a new animator, animates from 0 to 1 over a given time, and calls your callback with the current eased value
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task AnimateAsync(Action<double> callback,
            Action callbaclOnCancel = null,
            float ms = 250, Easing easing = null, CancellationTokenSource cancel = default)
        {
            if (easing == null)
            {
                easing = Easing.Linear;
            }

            var animator = new SkiaValueAnimator(this);

            if (cancel == default)
                cancel = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<bool>(cancel.Token);

            // Update animator parameters
            animator.mMinValue = 0;
            animator.mMaxValue = 1;
            animator.Speed = ms;
            animator.Easing = easing;

            animator.OnStop = () =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(true);
                DisposeObject(animator);
            };
            animator.OnUpdated = (value) =>
            {
                if (!cancel.IsCancellationRequested)
                {
                    callback?.Invoke(value);
                }
                else
                {
                    callbaclOnCancel?.Invoke();
                    animator.Stop();
                    DisposeObject(animator);
                }
            };

            animator.Start();

            return tcs.Task;
        }

        CancellationTokenSource _fadeCancelTokenSource;

        /// <summary>
        /// Fades the drawn view from the current Opacity to end, animator is reused if already running
        /// </summary>
        /// <param name="end"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task FadeToAsync(double end, float ms = 250, Easing easing = null,
            CancellationTokenSource cancel = default)
        {
            if (_fadeCancelTokenSource != null)
            {
                try
                {
                    _fadeCancelTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try
                    {
                        _fadeCancelTokenSource.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _fadeCancelTokenSource = null; // Clear reference
                }
            }

            _fadeCancelTokenSource = cancel ?? new CancellationTokenSource();

            var startOpacity = this.Opacity;
            return AnimateAsync(
                (value) =>
                {
                    this.Opacity = startOpacity + (end - startOpacity) * value;
                    //Debug.WriteLine($"[ANIM] Opacity: {this.Opacity}");
                },
                () => { this.Opacity = end; },
                ms,
                easing,
                _fadeCancelTokenSource);
        }

        CancellationTokenSource _scaleCancelTokenSource;

        /// <summary>
        /// Scales the drawn view from the current Scale to x,y, animator is reused if already running
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task ScaleToAsync(double x, double y, float length = 250, Easing easing = null,
            CancellationTokenSource cancel = default)
        {
            if (_scaleCancelTokenSource != null)
            {
                try
                {
                    _scaleCancelTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try
                    {
                        _scaleCancelTokenSource.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _scaleCancelTokenSource = null;
                }
            }

            _scaleCancelTokenSource = cancel ?? new CancellationTokenSource();

            var startScaleX = this.ScaleX;
            var startScaleY = this.ScaleY;

            return AnimateAsync(value =>
            {
                this.ScaleX = startScaleX + (x - startScaleX) * value;
                this.ScaleY = startScaleY + (y - startScaleY) * value;
            },
                () =>
                {
                    this.ScaleX = x;
                    this.ScaleY = y;
                }, length, easing, _scaleCancelTokenSource);
        }

        CancellationTokenSource _translateCancelTokenSource;

        /// <summary>
        /// Translates the drawn view from the current position to x,y, animator is reused if already running
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task TranslateToAsync(double x, double y, float length = 250, Easing easing = null,
            CancellationTokenSource cancel = default)
        {
            if (_translateCancelTokenSource != null)
            {
                try
                {
                    _translateCancelTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try
                    {
                        _translateCancelTokenSource.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _translateCancelTokenSource = null;
                }
            }

            _translateCancelTokenSource = cancel ?? new CancellationTokenSource();

            var startTranslationX = this.TranslationX;
            var startTranslationY = this.TranslationY;

            return AnimateAsync(value =>
            {
                this.TranslationX = (float)(startTranslationX + (x - startTranslationX) * value);
                this.TranslationY = (float)(startTranslationY + (y - startTranslationY) * value);
            },
                () =>
                {
                    this.TranslationX = x;
                    this.TranslationY = y;
                },
                length, easing, _translateCancelTokenSource);
        }

        CancellationTokenSource _rotateCancelTokenSource;

        /// <summary>
        /// Rotates the drawn view from the current rotation to end, animator is reused if already running
        /// </summary>
        /// <param name="end"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task RotateToAsync(double end, uint length = 250, Easing easing = null,
            CancellationTokenSource cancel = default)
        {
            if (_rotateCancelTokenSource != null)
            {
                try
                {
                    _rotateCancelTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try
                    {
                        _rotateCancelTokenSource.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _rotateCancelTokenSource = null;
                }
            }

            _rotateCancelTokenSource = cancel ?? new CancellationTokenSource();

            var startRotation = this.Rotation;

            return AnimateAsync(value => { this.Rotation = (float)(startRotation + (end - startRotation) * value); },
                () => { this.Rotation = end; },
                length, easing, _rotateCancelTokenSource);
        }

        public virtual void OnPrintDebug()
        {
        }

        public void PrintDebug(string indent = "     ")
        {
            Super.Log(
                $"{indent}└─ {GetType().Name} {Width:0.0}x{Height:0.0} pts ({MeasuredSize.Pixels.Width:0.0}x{MeasuredSize.Pixels.Height:0.0} px)");
            OnPrintDebug();
            foreach (var view in Views)
            {
                Trace.Write($"{indent}    ├─ ");
                view.PrintDebug(indent + "    │  ");
            }
        }

        public static readonly BindableProperty DebugRenderingProperty = BindableProperty.Create(nameof(DebugRendering),
            typeof(bool),
            typeof(SkiaControl),
            false, propertyChanged: NeedDraw);

        public bool DebugRendering
        {
            get { return (bool)GetValue(DebugRenderingProperty); }
            set { SetValue(DebugRenderingProperty, value); }
        }

        float _debugRenderingCurrentOpacity;

        /// <summary>
        /// When the animator is cancelled if applyEndValueOnStop is true then the end value will be sent to your callback
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <param name="applyEndValueOnStop"></param>
        /// <returns></returns>
        public Task AnimateRangeAsync(Action<double> callback, double start, double end, double length = 250,
            Easing easing = null,
            CancellationToken cancel = default,
            bool applyEndValueOnStop = false)
        {
            RangeAnimator animator = null;

            var tcs = new TaskCompletionSource<bool>(cancel);

            tcs.Task.ContinueWith(task => { DisposeObject(animator); });

            animator = new RangeAnimator(this)
            {
                OnStop = () =>
                {
                    //if (animator.WasStarted && !cancel.IsCancellationRequested)
                    {
                        if (applyEndValueOnStop)
                            callback?.Invoke(end);
                        tcs.SetResult(true);
                    }
                }
            };
            animator.Start(
                (value) =>
                {
                    if (!cancel.IsCancellationRequested)
                    {
                        callback?.Invoke(value);
                    }
                    else
                    {
                        animator.Stop();
                    }
                },
                start, end, (uint)length, easing);

            return tcs.Task;
        }

        public SKPoint NotValidPoint()
        {
            return new SKPoint(float.NaN, float.NaN);
        }

        public bool PointIsValid(SKPoint point)
        {
            return !float.IsNaN(point.X) && !float.IsNaN(point.Y);
        }

        /// <summary>
        /// Is set by InvalidateMeasure();
        /// </summary>
        public SKSize SizeRequest { get; protected set; }

        /// <summary>
        /// Points
        /// </summary>
        public double WidthRequestWithMargins
        {
            get
            {
                if (WidthRequest >= 0)
                {
                    return WidthRequest + Margins.HorizontalThickness;
                }
                return WidthRequest;
            }
        }

        /// <summary>
        /// Apply margins to SizeRequest
        /// </summary>
        /// <param name="sizeConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public float AdaptWidthConstraintToRequest(float sizeConstraint, Thickness constraints, double scale)
        {
            var ret = sizeConstraint;

            if (SizeRequest.Width >= 0)
            {
                ret = (float)Math.Round(SizeRequest.Width * scale + constraints.HorizontalThickness);
                if (sizeConstraint >= 0 && ret > sizeConstraint)
                {
                    return sizeConstraint;
                }
            }

            return ret;
        }

        /// <summary>
        /// Apply margins to SizeRequest
        /// </summary>
        /// <param name="sizeConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public float AdaptHeightContraintToRequest(float sizeConstraint, Thickness constraints, double scale)
        {
            var ret = sizeConstraint;

            if (SizeRequest.Height >= 0)
            {
                ret = (float)Math.Round(SizeRequest.Height * scale + constraints.VerticalThickness);
                if (sizeConstraint >= 0 && ret > sizeConstraint)
                {
                    return sizeConstraint;
                }
            }

            return ret;
        }

        public virtual MeasuringConstraints GetMeasuringConstraints(MeasureRequest request)
        {
            var withLock = GetSizeRequest(request.WidthRequest, request.HeightRequest, true);
            var margins = GetMarginsInPixels(request.Scale);

            var adaptedWidthConstraint = AdaptWidthConstraintToRequest(withLock.Width, margins, request.Scale);
            var adaptedHeightConstraint = AdaptHeightContraintToRequest(withLock.Height, margins, request.Scale);

            var rectForChildrenPixels =
                GetMeasuringRectForChildren(adaptedWidthConstraint, adaptedHeightConstraint, request.Scale);

            return new MeasuringConstraints
            {
                Margins = margins,
                TotalMargins = GetAllMarginsInPixels(request.Scale),
                Request = new(adaptedWidthConstraint, adaptedHeightConstraint),
                Content = rectForChildrenPixels
            };
        }

        public bool NeedFillHorizontally
        {
            get
            {
                return WidthRequest < 0 && HorizontalOptions.Alignment == LayoutAlignment.Fill;
            }
        }

        public bool NeedFillVertically
        {
            get
            {
                return HeightRequest < 0 && VerticalOptions.Alignment == LayoutAlignment.Fill;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AdaptConstraintToContentRequest(
            float constraintPixels,
            double measuredDimension,
            double sideConstraintsPixels,
            bool autoSize,
            double minRequest, double maxRequest, float scale, bool canExpand)
        {
            var contentDimension = sideConstraintsPixels + measuredDimension;

            if (autoSize && measuredDimension >= 0 && (canExpand || measuredDimension < constraintPixels)
                || float.IsInfinity(constraintPixels))
            {
                constraintPixels = (float)contentDimension;
            }

            if (minRequest >= 0)
            {
                var min = double.MinValue;
                if (double.IsFinite(minRequest))
                {
                    min = (minRequest * scale);
                }

                constraintPixels = (float)Math.Max(constraintPixels, min);
            }

            if (maxRequest >= 0)
            {
                var max = double.MaxValue;
                if (double.IsFinite(maxRequest))
                {
                    max = (maxRequest * scale);
                }

                constraintPixels = (float)Math.Min(constraintPixels, max);
            }

            return (float)(constraintPixels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AdaptWidthConstraintToContentRequest(MeasuringConstraints constraints, float contentWidthPixels,
            bool canExpand)
        {
            var sideConstraintsPixels = NeedAutoWidth
                ? constraints.TotalMargins.HorizontalThickness
                : constraints.Margins.HorizontalThickness;

            return AdaptConstraintToContentRequest(
                constraints.Request.Width,
                contentWidthPixels,
                sideConstraintsPixels,
                NeedAutoWidth,
                MinimumWidthRequest,
                MaximumWidthRequest,
                RenderingScale, canExpand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AdaptHeightConstraintToContentRequest(MeasuringConstraints constraints,
            float contentHeightPixels, bool canExpand)
        {
            var sideConstraintsPixels = NeedAutoHeight
                ? constraints.TotalMargins.VerticalThickness
                : constraints.Margins.VerticalThickness;

            return AdaptConstraintToContentRequest(
                constraints.Request.Height,
                contentHeightPixels,
                sideConstraintsPixels,
                NeedAutoHeight,
                MinimumHeightRequest,
                MaximumHeightRequest,
                RenderingScale, canExpand);
        }

        public float AdaptWidthConstraintToContentRequest(float widthConstraintPixels,
            ScaledSize measuredContent, double sideConstraintsPixels)
        {
            return AdaptConstraintToContentRequest(
                widthConstraintPixels,
                measuredContent.Pixels.Width,
                sideConstraintsPixels,
                NeedAutoWidth,
                MinimumWidthRequest,
                MaximumWidthRequest,
                RenderingScale, this.HorizontalOptions.Expands);
        }

        public float AdaptHeightConstraintToContentRequest(float heightConstraintPixels,
            ScaledSize measuredContent,
            double sideConstraintsPixels)
        {
            return AdaptConstraintToContentRequest(
                heightConstraintPixels,
                measuredContent.Pixels.Height,
                sideConstraintsPixels,
                NeedAutoHeight,
                MinimumHeightRequest,
                MaximumHeightRequest,
                RenderingScale, this.VerticalOptions.Expands);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SKRect AdaptToContraints(SKRect measuredPixels,
            double constraintLeft,
            double constraintRight,
            double constraintTop,
            double constraintBottom)
        {
            double widthConstraintsPixels = constraintLeft + constraintRight;
            double heightConstraintsPixels = constraintTop + constraintBottom;
            var outPixels = measuredPixels.Clone();

            if (NeedAutoWidth)
            {
                if (outPixels.Width > 0
                    && outPixels.Width < widthConstraintsPixels)
                {
                    outPixels.Left -= (float)constraintLeft;
                    outPixels.Right += (float)constraintRight;
                }
            }

            if (NeedAutoHeight)
            {
                if (outPixels.Height > 0
                    && outPixels.Height < heightConstraintsPixels)
                {
                    outPixels.Top -= (float)constraintTop;
                    outPixels.Bottom += (float)constraintBottom;
                }
            }

            return outPixels;
        }

        /// <summary>
        /// In UNITS
        /// </summary>
        /// <param name="widthRequestPts"></param>
        /// <param name="heightRequestPts"></param>
        /// <returns></returns>
        protected Size AdaptSizeRequestToContent(double widthRequestPts, double heightRequestPts)
        {
            if (NeedAutoWidth && ContentSize.Units.Width > 0)
            {
                widthRequestPts = ContentSize.Units.Width + Padding.Left + Padding.Right;
            }

            if (NeedAutoHeight && ContentSize.Units.Height > 0)
            {
                heightRequestPts = ContentSize.Units.Height + Padding.Top + Padding.Bottom;
            }

            return new Size(widthRequestPts, heightRequestPts);
            ;
        }

        /// <summary>
        /// Use Superview from public area
        /// </summary>
        /// <returns></returns>
        public virtual DrawnView GetTopParentView()
        {
            return GetParentElement(this) as DrawnView;
        }

        public static IDrawnBase GetParentElement(IDrawnBase control)
        {
            if (control != null)
            {
                if (control is DrawnView)
                {
                    return control;
                }

                if (control is SkiaControl skia)
                {
                    return GetParentElement(skia.Parent);
                }
            }

            return null;
        }

        /// <summary>
        /// To detect if current location is inside Destination
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool GestureIsInside(TouchActionEventArgs args, float offsetX = 0, float offsetY = 0)
        {
            return IsPixelInside((float)args.Location.X + offsetX, (float)args.Location.Y + offsetY);

            //            return IsPointInside((float)args.Event.Location.X + offsetX, (float)args.Event.Location.Y + offsetY, (float)RenderingScale);
        }

        /// <summary>
        /// To detect if a gesture Start point was inside Destination
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool GestureStartedInside(TouchActionEventArgs args, float offsetX = 0, float offsetY = 0)
        {
            return IsPixelInside((float)args.StartingLocation.X + offsetX, (float)args.StartingLocation.Y + offsetY);

            //            return IsPointInside((float)args.Event.Distance.Start.X + offsetX, (float)args.Event.Distance.Start.Y + offsetY, (float)RenderingScale);
        }

        /// <summary>
        /// Whether the point is inside Destination
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public bool IsPointInside(float x, float y, float scale)
        {
            return IsPointInside(DrawingRect, x, y, scale);
        }

        public bool IsPointInside(SKRect rect, float x, float y, float scale)
        {
            var xx = x * scale;
            var yy = y * scale;
            bool isInside = rect.ContainsInclusive(xx, yy);

            return isInside;
        }

        public bool IsPixelInside(SKRect rect, float x, float y)
        {
            bool isInside = rect.ContainsInclusive(x, y);
            return isInside;
        }

        /// <summary>
        /// Whether the pixel is inside Destination
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool IsPixelInside(float x, float y)
        {
            bool isInside = DrawingRect.Contains(x, y);
            return isInside;
        }

        protected virtual bool CheckGestureIsInsideChild(SkiaControl child, TouchActionEventArgs args,
            float offsetX = 0, float offsetY = 0)
        {
            return child.CanDraw && child.GestureIsInside(args, offsetX, offsetY);
        }

        protected virtual bool CheckGestureIsForChild(SkiaControl child, TouchActionEventArgs args, float offsetX = 0,
            float offsetY = 0)
        {
            return child.CanDraw && child.GestureStartedInside(args, offsetX, offsetY);
        }

        protected object LockIterateListeners = new();

        public static readonly BindableProperty LockChildrenGesturesProperty = BindableProperty.Create(
            nameof(LockChildrenGestures),
            typeof(LockTouch),
            typeof(SkiaControl),
            LockTouch.Disabled);

        /// <summary>
        /// What gestures are allowed to be passed to children below.
        /// If set to Enabled wit, otherwise can be more specific.
        /// </summary>
        public LockTouch LockChildrenGestures
        {
            get { return (LockTouch)GetValue(LockChildrenGesturesProperty); }
            set { SetValue(LockChildrenGesturesProperty, value); }
        }

        protected bool CheckChildrenGesturesLocked(TouchActionResult action)
        {
            switch (LockChildrenGestures)
            {
                case LockTouch.Enabled:
                    return true;

                case LockTouch.Disabled:
                    break;

                case LockTouch.PassTap:
                    if (action != TouchActionResult.Tapped)
                        return true;
                    break;

                case LockTouch.PassTapAndLongPress:
                    if (action != TouchActionResult.Tapped && action != TouchActionResult.LongPressing)
                        return true;
                    break;
            }

            return false;
        }

        /// <summary>
        /// Will be called by Canvas. Please override ProcessGestures for handling gestures, this method is for internal use.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        public ISkiaGestureListener OnSkiaGestureEvent(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            if (CanDraw && this is ISkiaGestureListener listener)
            {
                var canvas = _superview as Canvas;
                if (canvas != null)
                {
                    if (args.Type == TouchActionResult.Up && !canvas.SignalInput(listener, args.Type))
                    {
                        return null; //avoid being called twice for same gesture
                    }
                }

                var result = ProcessGestures(args, apply);

                return result;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool IsGestureForChild(SkiaControlWithRect child, SKPoint point)
        {
            bool inside = false;
            if (child.Control != null && !child.Control.IsDisposing && !child.Control.IsDisposed &&
                !child.Control.InputTransparent && child.Control.CanDraw)
            {
                var transformed = child.Control.ApplyTransforms(child.HitRect);
                inside = transformed.ContainsInclusive(point.X, point.Y); 
            }

            return inside;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool IsGestureForChild(SkiaControl child, SkiaGesturesParameters args)
        {
            bool inside = false;
            if (child != null && !child.IsDisposing && !child.IsDisposed &&
                !child.InputTransparent && child.VisualLayer != null && child.CanDraw)
            {
                var hitbox = child.VisualLayer.HitBoxWithTransforms.Pixels;
                inside = hitbox.ContainsInclusive(args.Event.Location.X, args.Event.Location.Y);
            }

            return inside;
        }

        //          
        // SKPoint childOffset, SKPoint childOffsetDirect, ISkiaGestureListener alreadyConsumed
        public static readonly BindableProperty CommandChildTappedProperty = BindableProperty.Create(
            nameof(CommandChildTapped), typeof(ICommand),
            typeof(SkiaControl),
            null);

        /// <summary>
        /// Child was tapped. Will pass the tapped child as parameter.
        /// You might want then read child's BindingContext etc..
        /// This works only if your control implements ISkiaGestureListener.
        /// If this is set then the Tapped gesture will be consumed by this control after passing it to child.
        /// </summary>
        public ICommand CommandChildTapped
        {
            get { return (ICommand)GetValue(CommandChildTappedProperty); }
            set { SetValue(CommandChildTappedProperty, value); }
        }

        public static readonly BindableProperty TouchEffectColorProperty = BindableProperty.Create(
            nameof(TouchEffectColor), typeof(Color),
            typeof(SkiaControl),
            Colors.White);

        public Color TouchEffectColor
        {
            get { return (Color)GetValue(TouchEffectColorProperty); }
            set { SetValue(TouchEffectColorProperty, value); }
        }

        public static readonly BindableProperty AnimationTappedProperty = BindableProperty.Create(
            nameof(AnimationTapped),
            typeof(SkiaTouchAnimation),
            typeof(SkiaControl), SkiaTouchAnimation.None);

        public SkiaTouchAnimation AnimationTapped
        {
            get { return (SkiaTouchAnimation)GetValue(AnimationTappedProperty); }
            set { SetValue(AnimationTappedProperty, value); }
        }

        public static readonly BindableProperty TransformViewProperty = BindableProperty.Create(nameof(TransformView),
            typeof(object),
            typeof(SkiaControl), null);

        public object TransformView
        {
            get { return (object)GetValue(TransformViewProperty); }
            set { SetValue(TransformViewProperty, value); }
        }

        /// <summary>
        /// If Tapped handler was defined, activates it and return true of false it was defined.
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="args"></param>
        /// <param name="apply"></param>
        /// <param name="useMainThread"></param>
        /// <returns></returns>
        protected bool SendTapped(object listener, SkiaGesturesParameters args, GestureEventProcessingInfo apply,
            bool useMainThread)
        {
            if (Tapped != null)
            {
                if (this.AnimationTapped != SkiaTouchAnimation.None)
                {
                    var control = this as SkiaControl;
                    if (this.TransformView is SkiaControl other)
                    {
                        control = other;
                    }

                    if (AnimationTapped == SkiaTouchAnimation.Ripple)
                    {
                        var ptsInsideControl = GetOffsetInsideControlInPoints(args.Event.Location, apply.ChildOffset);
                        control.PlayRippleAnimation(TouchEffectColor, ptsInsideControl.X, ptsInsideControl.Y);
                    }
                    else if (AnimationTapped == SkiaTouchAnimation.Shimmer)
                    {
                        var color = TouchEffectColor;
                        control.PlayShimmerAnimation(color, 150, 33, 300);
                    }
                }

                if (useMainThread)
                {
                    MainThread.BeginInvokeOnMainThread(() => { Tapped?.Invoke(this, new(listener, args, apply)); });
                }
                else
                {
                    Tapped?.Invoke(this, new(listener, args, apply));
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// WIll be called if a child implements ISkiaGestureListener and was Tapped. If this is set then the Tapped gesture will be consumed by this control after passing it to child.
        /// </summary>
        public event EventHandler<ControlTappedEventArgs> ChildTapped;

        public class ControlTappedEventArgs : EventArgs
        {
            public object Control { get; set; }
            public SkiaGesturesParameters Parameters { get; set; }
            public GestureEventProcessingInfo ProcessingInfo { get; set; }

            public ControlTappedEventArgs(object control, SkiaGesturesParameters args, GestureEventProcessingInfo info)
            {
                Control = control;
                Parameters = args;
                ProcessingInfo = info;
            }
        }

        public SKPoint TransformPointToLocalSpace(SKPoint pointInParentSpace)
        {
            //if (RenderTransformMatrix != SKMatrix.Identity)
            //{
            //    return RenderTransformMatrix.MapPoint(pointInParentSpace);
            //}

            // Apply the inverse transformation matrix to get point in local space
            if (RenderTransformMatrix != SKMatrix.Identity &&
                RenderTransformMatrix.TryInvert(out SKMatrix inverse))
            {
                return inverse.MapPoint(pointInParentSpace);
            }

            // No transformations applied, return original point
            return pointInParentSpace;
        }

        public bool IsGestureInside(GestureEventProcessingInfo apply)
        {
            var thisOffset = TranslateInputCoords(apply.ChildOffset);
            var touchLocationWIthOffset = new SKPoint(apply.MappedLocation.X + thisOffset.X,
                apply.MappedLocation.Y + thisOffset.Y);
            var transformed = ApplyTransforms(DrawingRect);
            var inside = transformed.ContainsInclusive(touchLocationWIthOffset.X, touchLocationWIthOffset.Y);
            return inside;
        }


        /// <summary>
        /// Delegate for use instead of calling base.OnSkiaGestureEvent
        /// </summary>
        public Func<SkiaGesturesParameters, GestureEventProcessingInfo, ISkiaGestureListener> OnGestures;

        /// <summary>
        /// Fast usage event handler to handle taps.
        /// For more control over gestures use AddGestures or code-behind ProcessGestures override.
        /// If this is set then the Tapped gesture will be consumed by this control without alternatives.
        /// </summary>
        public event EventHandler<ControlTappedEventArgs> Tapped;

        /// <summary>
        /// Gestures event handler for fast access. To mark a gesture as consumed set `e.Consumed` to `true` inside a synchronous (!) event handler.
        /// Can also use code-behind ProcessGestures override for more control.
        /// </summary>
        public event EventHandler<SkiaGesturesInfo> ConsumeGestures;

        /// <summary>
        /// Layout detected that child was tapped, base used to call callback ChildTapped 
        /// </summary>
        /// <param name="child"></param>
        /// <param name="args"></param>
        /// <param name="apply"></param>
        public virtual void OnChildTapped(SkiaControl child, SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            if (ChildTapped != null)
            {
                ChildTapped.Invoke(this, new(child, args, apply));
            }

            if (CommandChildTapped != null)
            {
                CommandChildTapped.Execute(child);
            }
        }

 
 
        public virtual ISkiaGestureListener ProcessGestures(
            SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            if (IsDisposed || IsDisposing)
                return null;

            if (Superview == null)
            {
                //shit happened. we are capturing input but we are not supposed to be on the screen!
                Super.Log($"[OnGestureEvent] base captured by unassigned view {this.GetType()} {this.Tag}");
                return null;
            }

            if (TouchEffect.LogEnabled)
            {
                Super.Log($"[BASE] {this.Tag} Got {args.Type}.. {Uid}");
            }

            var consumedDefault = BlockGesturesBelow ? this as ISkiaGestureListener : null;

            if (HasTransform)
            {
                // Transform the mapped location using the inverse transformation matrix
                if (RenderTransformMatrix.TryInvert(out SKMatrix inverse))
                {
                    // Create a new struct with the updated MappedLocation
                    apply = new GestureEventProcessingInfo(
                        inverse.MapPoint(apply.MappedLocation),
                        apply.ChildOffset,
                        apply.ChildOffsetDirect,
                        apply.AlreadyConsumed
                    );
                }
            }

            if (EffectsGestureProcessors.Count > 0)
            {
                foreach (var effect in EffectsGestureProcessors)
                {
                    effect.ProcessGestures(args, apply);
                }
            }

            ISkiaGestureListener consumed = null;
            ISkiaGestureListener wasConsumed = apply.AlreadyConsumed;
            bool manageChildFocus = false;

            if (OnGestures != null)
            {
                consumed = OnGestures(args, apply);
                if (consumed != null)
                {
                    return consumed;
                }
            }

            if (ConsumeGestures != null)
            {
                var sent = SkiaGesturesInfo.Create(args, apply);
                ConsumeGestures.Invoke(this, sent);
                if (args.Type != TouchActionResult.Up && sent.Consumed)
                {
                    return this;
                }
            }

            if (UsesRenderingTree && RenderTree != null)
            {
                var hadInputConsumed = consumed;
                var thisOffset = TranslateInputCoords(apply.ChildOffset);

                //if previously having input didn't keep it
                if (consumed == null || args.Type == TouchActionResult.Up)
                {
                    var asSpan = CollectionsMarshal.AsSpan(RenderTree);

                    for (int i = asSpan.Length - 1; i >= 0; i--)
                    {
                        var child = asSpan[i];

                        if (child == Superview.FocusedChild)
                            manageChildFocus = true;

                        ISkiaGestureListener listener = child.Control.GesturesEffect;
                        if (listener == null && child.Control is ISkiaGestureListener listen)
                        {
                            listener = listen;
                        }

                        ISkiaGestureListener breakForChild = null;
                        if (listener != null)
                        {
                            bool forChild;
                            if (Super.UseFrozenVisualLayers && Super.UseFrozenVisualLayers)
                            {
                                forChild = IsGestureForChild(child.Control, args);
                            }
                            else
                            {
                                var touchLocationWIthOffset = new SKPoint(apply.MappedLocation.X + thisOffset.X,
                                    apply.MappedLocation.Y + thisOffset.Y);

                                forChild = IsGestureForChild(child, touchLocationWIthOffset);
                            }

                            if (forChild)
                            {
                                if (args.Type == TouchActionResult.Tapped)
                                {
                                    OnChildTapped(child.Control, args, apply);
                                }

                                if (manageChildFocus && listener == Superview.FocusedChild)
                                {
                                    manageChildFocus = false;
                                }

                                var childOffset = TranslateInputCoords(apply.ChildOffsetDirect, false);

                                //standart gesture processing
                                var c = listener.OnSkiaGestureEvent(args,
                                    new GestureEventProcessingInfo(
                                        apply.MappedLocation,
                                        thisOffset,
                                        childOffset,
                                        apply.AlreadyConsumed));
                                if (c != null)
                                {
                                    consumed = c;
                                }
                                else
                                {
                                    //check attached listeners then
                                    if (AddGestures.AttachedListeners.TryGetValue(child.Control, out var effect))
                                    {
                                        c = effect.OnSkiaGestureEvent(args,
                                            new GestureEventProcessingInfo(
                                                apply.MappedLocation,
                                                thisOffset,
                                                childOffset,
                                                apply.AlreadyConsumed));
                                        if (c != null)
                                        {
                                            consumed = effect;
                                        }
                                    }
                                }

                                if (consumed != null)
                                {
                                    break;
                                }

                                if (breakForChild != null)
                                {
                                    consumed = breakForChild;
                                    break;
                                }
                            }
                        }
                    }
                } //end

                if (manageChildFocus)
                {
                    Superview.FocusedChild = null;
                }

                if (hadInputConsumed != null)
                    consumed = hadInputConsumed;
            }
            else
            {
                //lock (LockIterateListeners)
                {
                    try
                    {
                        if (CheckChildrenGesturesLocked(args.Type))
                            return consumedDefault;

                        var point = TranslateInputOffsetToPixels(args.Event.Location, apply.ChildOffset);

                        ISkiaGestureListener breakForChild = null;

                        if (consumed == null ||
                            args.Type == TouchActionResult.Up) // !GestureListeners.Contains(consumed))
                            foreach (var listener in GestureListeners.GetListeners())
                            {
                                if (listener == null || !listener.CanDraw || listener.InputTransparent ||
                                    listener.GestureListenerRegistrationTime == null)
                                    continue;

                                if (listener == Superview.FocusedChild)
                                    manageChildFocus = true;

                                var forChild = IsGestureForChild(listener, point);

                                if (TouchEffect.LogEnabled)
                                {
                                    if (listener is SkiaControl c)
                                    {
                                        Debug.WriteLine(
                                            $"[BASE] for child {forChild} {c.Tag} at {point.X:0},{point.Y:0} -> {c.HitBoxAuto} ");
                                    }
                                }

                                if (forChild)
                                {
                                    if (args.Type == TouchActionResult.Tapped)
                                    {
                                        if (ChildTapped != null)
                                        {
                                            breakForChild = listener;
                                        }
                                        if (CommandChildTapped != null)
                                        {
                                            breakForChild = listener;
                                        }

                                        OnChildTapped(listener as SkiaControl, args, apply);
                                    }

                                    if (manageChildFocus && listener == Superview.FocusedChild)
                                    {
                                        manageChildFocus = false;
                                    }

                                    //Log($"[OnGestureEvent] sent {args.Action} to {listener.Tag}");
                                    consumed = listener.OnSkiaGestureEvent(args,
                                        new GestureEventProcessingInfo(
                                            apply.MappedLocation,
                                            TranslateInputCoords(apply.ChildOffset, true),
                                            TranslateInputCoords(apply.ChildOffsetDirect, false),
                                            wasConsumed));

                                    if (consumed != null)
                                    {
                                        if (wasConsumed == null)
                                            wasConsumed = consumed;

                                        break;
                                    }

                                    if (breakForChild != null)
                                    {
                                        consumed = breakForChild;
                                        break;
                                    }
                                }
                            }

                        if (manageChildFocus)
                        {
                            Superview.FocusedChild = null;
                        }

                        if (wasConsumed != null)
                            consumed = wasConsumed;
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }
            }

            if (args.Type == TouchActionResult.Tapped && this is ISkiaGestureListener meAsListener && consumed == null)
            {
                if (SendTapped(meAsListener, args, apply, Super.SendTapsOnMainThread))
                {
                    return meAsListener;
                }
            }

            if (BlockGesturesBelow && consumed == null && args.Type != TouchActionResult.Up)
            {
                return this as ISkiaGestureListener;
            }

            return consumed;
        }
 

        public static readonly BindableProperty BlockGesturesBelowProperty = BindableProperty.Create(
            nameof(BlockGesturesBelow),
            typeof(bool), typeof(SkiaControl), false);

        /// <summary>
        ///  If set to true will not allow gestures to go below
        /// </summary>
        public bool BlockGesturesBelow
        {
            get { return (bool)GetValue(BlockGesturesBelowProperty); }
            set { SetValue(BlockGesturesBelowProperty, value); }
        }

        public int UpdateLocks { get; private set; }

        public void UnlockUpdate()
        {
            LockUpdate(false);
        }

        public virtual void LockUpdate(bool value)
        {
            bool fire = UpdateLocks > 0 && !value;
            if (value)
            {
                UpdateLocks++;
            }
            else
            {
                if (UpdateLocks > 0)
                {
                    UpdateLocks--;
                }
            }

            if (fire && _neededUpdate)
            {
                UpdateInternal();
            }
        }

        public Element NativeParent
        {
            get { return base.Parent; }
        }

        public static readonly BindableProperty ParentProperty = BindableProperty.Create(
            nameof(Parent),
            typeof(IDrawnBase),
            typeof(SkiaControl),
            default(IDrawnBase),
            propertyChanged: OnControlParentChanged);

        /// <summary>
        /// Do not set this directly if you don't know what you are doing, use SetParent()
        /// </summary>
        public new IDrawnBase Parent
        {
            get { return (IDrawnBase)GetValue(ParentProperty); }
            set { SetValue(ParentProperty, value); }
        }

        public static readonly BindableProperty AlignContentVerticalProperty = BindableProperty.Create(
            nameof(AlignContentVertical),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutOptions AlignContentVertical
        {
            get { return (LayoutOptions)GetValue(AlignContentVerticalProperty); }
            set { SetValue(AlignContentVerticalProperty, value); }
        }

        public static readonly BindableProperty AlignContentHorizontalProperty = BindableProperty.Create(
            nameof(AlignContentHorizontal),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutOptions AlignContentHorizontal
        {
            get { return (LayoutOptions)GetValue(AlignContentHorizontalProperty); }
            set { SetValue(AlignContentHorizontalProperty, value); }
        }

        #region View

        public static readonly BindableProperty VerticalOptionsProperty = BindableProperty.Create(
            nameof(VerticalOptions),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutOptions VerticalOptions
        {
            get { return (LayoutOptions)GetValue(VerticalOptionsProperty); }
            set { SetValue(VerticalOptionsProperty, value); }
        }

        public static readonly BindableProperty HorizontalOptionsProperty = BindableProperty.Create(
            nameof(HorizontalOptions),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutOptions HorizontalOptions
        {
            get { return (LayoutOptions)GetValue(HorizontalOptionsProperty); }
            set { SetValue(HorizontalOptionsProperty, value); }
        }

        /// <summary>
        /// todo override for templated skialayout to use ViewsProvider
        /// </summary>
        /// <param name="newvalue"></param>
        public virtual void OnParentVisibilityChanged(bool newvalue)
        {
            if (!newvalue)
            {
                StopPostAnimators();
            }

            Superview?.SetViewTreeVisibilityByParent(this, newvalue);

            if (!newvalue)
            {
                if (this.UsingCacheType == SkiaCacheType.GPU)
                {
                    RenderObject = null;
                }
            }

            if (!IsVisible)
            {
                //though shell not pass
                return;
            }

            PropagateVisibilityChanged(newvalue);

            Update(); //always redraw coming out of this (postanimators need update etc)
        }

        protected virtual void PropagateVisibilityChanged(bool newvalue)
        {
            try
            {
                foreach (var child in Views.ToList())
                {
                    child.OnParentVisibilityChanged(newvalue);
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        public event EventHandler<bool> VisibilityChanged;

        public void SendVisibilityChanged()
        {
            VisibilityChanged?.Invoke(this, IsVisible);
        }

        /// <summary>
        /// todo override for templated skialayout to use ViewsProvider
        /// </summary>
        /// <param name="newvalue"></param>
        public virtual void OnVisibilityChanged(bool newvalue)
        {
            if (!newvalue)
            {
                if (this.UsingCacheType == SkiaCacheType.GPU)
                {
                    RenderObject = null;
                }
            }

            // need to this to:
            // disable child gesture listeners
            // pause hidden animations
            try
            {
                var pass = IsVisible && newvalue;
                if (Views.Count > 0) //todo use childrenfactory for layout?
                {
                    foreach (var child in Views)
                    {
                        child.OnParentVisibilityChanged(pass);
                    }
                }

                //todo think about switching to visible/hidden/collapsed?
                if (!WasMeasured || newvalue)
                {
                    InvalidateParent();
                }

                Superview?.SetViewTreeVisibilityByParent(this, newvalue);

                if (!newvalue)
                {
                    StopPostAnimators();
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            SendVisibilityChanged();
        }

        void StopPostAnimators()
        {
            var process = PostAnimators.ToList();
            if (process.Count > 0)
            {
                foreach (IOverlayEffect effect in process)
                {
                    effect.Stop();
                }

                Repaint();
            }
        }

        /// <summary>
        /// Base performs some cleanup actions with Superview
        /// </summary>
        public virtual void OnDisposing()
        {
            ExecuteOnPaint.Clear();
            ExecuteAfterCreated.Clear();
            Rendered = null;
            ClipWith = null;
            Disposing?.Invoke(this, null);
            Superview?.UnregisterGestureListener(this as ISkiaGestureListener);
            Superview?.UnregisterAllAnimatorsByParent(this);
        }

        protected object lockPausingAnimators = new();

        public virtual void PauseAllAnimators()
        {
            var paused = Superview?.SetPauseStateOfAllAnimatorsByParent(this, true);
        }

        public virtual void ResumePausedAnimators()
        {
            var resumed = Superview?.SetPauseStateOfAllAnimatorsByParent(this, false);
        }

        public static readonly BindableProperty IsGhostProperty = BindableProperty.Create(nameof(IsGhost),
            typeof(bool),
            typeof(SkiaControl),
            false,
            propertyChanged: NeedDraw);

        public bool IsGhost
        {
            get { return (bool)GetValue(IsGhostProperty); }
            set { SetValue(IsGhostProperty, value); }
        }

        public static readonly BindableProperty IgnoreChildrenInvalidationsProperty
            = BindableProperty.Create(nameof(IgnoreChildrenInvalidations),
                typeof(bool), typeof(SkiaControl),
                false);

        public bool IgnoreChildrenInvalidations
        {
            get { return (bool)GetValue(IgnoreChildrenInvalidationsProperty); }
            set { SetValue(IgnoreChildrenInvalidationsProperty, value); }
        }

        #region FillGradient

        public static readonly BindableProperty FillGradientProperty = BindableProperty.Create(nameof(FillGradient),
            typeof(SkiaGradient), typeof(SkiaControl),
            null,
            propertyChanged: SetupFillGradient);

        private static void SetupFillGradient(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {

                if (oldvalue is SkiaGradient old)
                {
                    old.BindingContext = null;
                    old.Parent = null;
                }

                if (newvalue is SkiaGradient gradient)
                {
                    gradient.BindingContext = control.BindingContext;
                    gradient.Parent = control;
                }

                control.Update();
            }
        }

        public SkiaGradient FillGradient
        {
            get { return (SkiaGradient)GetValue(FillGradientProperty); }
            set { SetValue(FillGradientProperty, value); }
        }

        public bool HasFillGradient
        {
            get { return this.FillGradient != null && this.FillGradient.Type != GradientType.None; }
        }

        #endregion

        public virtual SKSize GetSizeRequest(float widthConstraint, float heightConstraint, bool insideLayout)
        {
            widthConstraint *= (float)this.WidthRequestRatio;
            heightConstraint *= (float)this.HeightRequestRatio;

            if (LockRatio > 0)
            {
                var lockValue = (float)SmartMax(widthConstraint, heightConstraint);
                if (lockValue > 0) //otherwise would need to use this while layouting
                    return new SKSize(lockValue, lockValue);
            }

            if (LockRatio < 0)
            {
                var lockValue = (float)SmartMin(widthConstraint, heightConstraint);
                if (lockValue > 0)
                    return new SKSize(lockValue, lockValue);
            }

            return new SKSize(widthConstraint, heightConstraint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static float SmartMax(float a, float b)
        {
            if (!float.IsFinite(a) || float.IsFinite(b) && b > a)
                return b;
            return a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static float SmartMin(float a, float b)
        {
            if (!float.IsFinite(a) || float.IsFinite(a) && b < a)
                return b;
            return a;
        }

        public static readonly BindableProperty ViewportHeightLimitProperty = BindableProperty.Create(
            nameof(ViewportHeightLimit),
            typeof(double),
            typeof(SkiaControl),
            -1.0, propertyChanged: NeedInvalidateViewport);

        /// <summary>
        /// Will be used inside GetDrawingRectWithMargins to limit the height of the DrawingRect
        /// </summary>
        public double ViewportHeightLimit
        {
            get { return (double)GetValue(ViewportHeightLimitProperty); }
            set { SetValue(ViewportHeightLimitProperty, value); }
        }

        public static readonly BindableProperty ViewportWidthLimitProperty = BindableProperty.Create(
            nameof(ViewportWidthLimit),
            typeof(double),
            typeof(SkiaControl),
            -1.0, propertyChanged: NeedInvalidateViewport);

        /// <summary>
        /// Will be used inside GetDrawingRectWithMargins to limit the width of the DrawingRect
        /// </summary>
        public double ViewportWidthLimit
        {
            get { return (double)GetValue(ViewportWidthLimitProperty); }
            set { SetValue(ViewportWidthLimitProperty, value); }
        }

        private double _height = -1;

        public new double Height
        {
            get { return _height; }
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _width = -1;

        public new double Width
        {
            get { return _width; }
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Please use ScaleX, ScaleY instead of this maui property
        /// </summary>
        public new double Scale
        {
            get { return Math.Min(ScaleX, ScaleY); }
            set
            {
                ScaleX = value;
                ScaleY = value;
            }
        }

        public static readonly BindableProperty TagProperty = BindableProperty.Create(nameof(Tag),
            typeof(string),
            typeof(SkiaControl),
            string.Empty,
            propertyChanged: NeedDraw);

        public string Tag
        {
            get { return (string)GetValue(TagProperty); }
            set { SetValue(TagProperty, value); }
        }

        public static readonly BindableProperty LockRatioProperty = BindableProperty.Create(nameof(LockRatio),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Locks the final size to the min (-1.0 -> 0.0) or max (0.0 -> 1.0) of the provided size.
        /// </summary>
        public double LockRatio
        {
            get { return (double)GetValue(LockRatioProperty); }
            set { SetValue(LockRatioProperty, value); }
        }

        public static readonly BindableProperty HeightRequestRatioProperty = BindableProperty.Create(
            nameof(HeightRequestRatio),
            typeof(double),
            typeof(SkiaControl),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// HeightRequest Multiplier, default is 1.0
        /// </summary>
        public double HeightRequestRatio
        {
            get { return (double)GetValue(HeightRequestRatioProperty); }
            set { SetValue(HeightRequestRatioProperty, value); }
        }

        public static readonly BindableProperty WidthRequestRatioProperty = BindableProperty.Create(
            nameof(WidthRequestRatio),
            typeof(double),
            typeof(SkiaControl),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// WidthRequest Multiplier, default is 1.0
        /// </summary>
        public double WidthRequestRatio
        {
            get { return (double)GetValue(WidthRequestRatioProperty); }
            set { SetValue(WidthRequestRatioProperty, value); }
        }

        public static readonly BindableProperty HorizontalFillRatioProperty = BindableProperty.Create(
            nameof(HorizontalFillRatio),
            typeof(double),
            typeof(SkiaControl),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// This would be applied to the reported available dimension for the control to fill. The parent layout is responsible for aliignement.
        /// </summary>
        public double HorizontalFillRatio
        {
            get { return (double)GetValue(HorizontalFillRatioProperty); }
            set { SetValue(HorizontalFillRatioProperty, value); }
        }

        public static readonly BindableProperty VerticalFillRatioProperty = BindableProperty.Create(
            nameof(VerticalFillRatio),
            typeof(double),
            typeof(SkiaControl),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// This would be applied to the reported available dimension for the control to fill. The parent layout is responsible for aliignement.
        /// </summary>
        public double VerticalFillRatio
        {
            get { return (double)GetValue(VerticalFillRatioProperty); }
            set { SetValue(VerticalFillRatioProperty, value); }
        }

        public static readonly BindableProperty HorizontalPositionOffsetRatioProperty = BindableProperty.Create(
            nameof(HorizontalPositionOffsetRatio),
            typeof(double),
            typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);

        public double HorizontalPositionOffsetRatio
        {
            get { return (double)GetValue(HorizontalPositionOffsetRatioProperty); }
            set { SetValue(HorizontalPositionOffsetRatioProperty, value); }
        }

        public static readonly BindableProperty VerticalPositionOffsetRatioProperty = BindableProperty.Create(
            nameof(VerticalPositionOffsetRatio),
            typeof(double),
            typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);

        public double VerticalPositionOffsetRatio
        {
            get { return (double)GetValue(VerticalPositionOffsetRatioProperty); }
            set { SetValue(VerticalPositionOffsetRatioProperty, value); }
        }

        public static readonly BindableProperty FillBlendModeProperty = BindableProperty.Create(nameof(FillBlendMode),
            typeof(SKBlendMode), typeof(SkiaControl),
            SKBlendMode.SrcOver,
            propertyChanged: NeedDraw);

        public SKBlendMode FillBlendMode
        {
            get { return (SKBlendMode)GetValue(FillBlendModeProperty); }
            set { SetValue(FillBlendModeProperty, value); }
        }

        /*

        disabled this for fps... we can use drawingrect.x and y /renderingscale but OnPropertyChanged calls might slow us down?..

        private double _X;
        public double X
        {
            get
            {
                return _X;
            }
            set
            {
                if (_X != value)
                {
                    _X = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Y;
        public double Y
        {
            get
            {
                return _Y;
            }
            set
            {
                if (_Y != value)
                {
                    _Y = value;
                    OnPropertyChanged();
                }
            }
        }

        */

        //public static readonly BindableProperty HeightProperty = BindableProperty.Create(nameof(Height),
        //	typeof(double), typeof(SkiaControl),
        //	-1.0);
        //public double Height
        //{
        //	get { return (double)GetValue(HeightProperty); }
        //	set { SetValue(HeightProperty, value); }
        //}

        //public static readonly BindableProperty WidthProperty = BindableProperty.Create(nameof(Width),
        //	typeof(double), typeof(SkiaControl),
        //	-1.0);
        //public double Width
        //{
        //	get { return (double)GetValue(WidthProperty); }
        //	set { SetValue(WidthProperty, value); }
        //}
        /*

        public static readonly BindableProperty OpacityProperty = BindableProperty.Create(nameof(Opacity),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: RedrawCanvas);
        public double Opacity
        {
            get { return (double)GetValue(OpacityProperty); }
            set { SetValue(OpacityProperty, value); }
        }
        */

        public static readonly BindableProperty SkewXProperty
            = BindableProperty.Create(nameof(SkewX),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float SkewX
        {
            get { return (float)GetValue(SkewXProperty); }
            set { SetValue(SkewXProperty, value); }
        }

        public static readonly BindableProperty SkewYProperty
            = BindableProperty.Create(nameof(SkewY),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float SkewY
        {
            get { return (float)GetValue(SkewYProperty); }
            set { SetValue(SkewYProperty, value); }
        }

        public static readonly BindableProperty TranslationZProperty
            = BindableProperty.Create(nameof(TranslationZ),
                typeof(double), typeof(SkiaControl),
                0.0, propertyChanged: NeedRepaint);

        /// <summary>
        /// Gets or sets Z-perspective translation. This is a bindable property.
        /// </summary>
        /// <remarks>Rotation is applied relative to <see cref="Microsoft.Maui.Controls.VisualElement.AnchorX"/> and <see cref="Microsoft.Maui.Controls.VisualElement.AnchorY" />.</remarks>
        public double TranslationZ
        {
            get { return (double)GetValue(TranslationZProperty); }
            set { SetValue(TranslationZProperty, value); }
        }

        public static readonly BindableProperty RotationZProperty
            = BindableProperty.Create(nameof(RotationZ),
                typeof(double), typeof(SkiaControl),
                0.0, propertyChanged: NeedRepaint);

        /// <summary>
        /// Gets or sets the rotation (in degrees) about the Z-axis (perspective rotation) when the element is rendered. This is a bindable property.
        /// </summary>
        /// <remarks>Rotation is applied relative to <see cref="Microsoft.Maui.Controls.VisualElement.AnchorX"/> and <see cref="Microsoft.Maui.Controls.VisualElement.AnchorY" />.</remarks>
        public double RotationZ
        {
            get { return (double)GetValue(RotationZProperty); }
            set { SetValue(RotationZProperty, value); }
        }

        //public new static readonly BindableProperty VerticalOptionsProperty = BindableProperty.Create(nameof(VerticalOptions),
        //    typeof(LayoutOptions),
        //    typeof(SkiaControl),
        //    LayoutOptions.Start,
        //    propertyChanged: NeedInvalidateMeasure);

        //public new LayoutOptions VerticalOptions
        //{
        //    get { return (LayoutOptions)GetValue(VerticalOptionsProperty); }
        //    set { SetValue(VerticalOptionsProperty, value); }
        //}

        //public new  static readonly BindableProperty HorizontalOptionsProperty = BindableProperty.Create(nameof(HorizontalOptions),
        //    typeof(LayoutOptions),
        //    typeof(SkiaControl),
        //    LayoutOptions.Start,
        //    propertyChanged: NeedInvalidateMeasure);

        //public new LayoutOptions HorizontalOptions
        //{
        //    get { return (LayoutOptions)GetValue(HorizontalOptionsProperty); }
        //    set { SetValue(HorizontalOptionsProperty, value); }
        //}

        public static readonly BindableProperty Perspective1Property
            = BindableProperty.Create(nameof(Perspective1),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float Perspective1
        {
            get { return (float)GetValue(Perspective1Property); }
            set { SetValue(Perspective1Property, value); }
        }

        public static readonly BindableProperty Perspective2Property
            = BindableProperty.Create(nameof(Perspective2),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float Perspective2
        {
            get { return (float)GetValue(Perspective2Property); }
            set { SetValue(Perspective2Property, value); }
        }

        //public static readonly BindableProperty TransformPivotPointXProperty
        //= BindableProperty.Create(nameof(TransformPivotPointX),
        //typeof(double), typeof(SkiaControl),
        //0.5, propertyChanged: NeedRepaint);
        //public double TransformPivotPointX
        //{
        //    get
        //    {
        //        return (double)GetValue(TransformPivotPointXProperty);
        //    }
        //    set
        //    {
        //        SetValue(TransformPivotPointXProperty, value);
        //    }
        //}

        //public static readonly BindableProperty TransformPivotPointYProperty
        //= BindableProperty.Create(nameof(TransformPivotPointY),
        //typeof(double), typeof(SkiaControl),
        //0.5, propertyChanged: NeedRepaint);

        //public double TransformPivotPointY
        //{
        //    get
        //    {
        //        return (double)GetValue(TransformPivotPointYProperty);
        //    }
        //    set
        //    {
        //        SetValue(TransformPivotPointYProperty, value);
        //    }
        //}

        private static void OnControlParentChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.OnParentChanged(newvalue as IDrawnBase, oldvalue as IDrawnBase);
            }
        }

        public new event EventHandler<IDrawnBase> ParentChanged;


        public static readonly BindableProperty FastMeasurementProperty = BindableProperty.Create(
            nameof(FastMeasurement),
            typeof(bool),
            typeof(SkiaControl),
            false,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// When true, uses single-pass fast measurement. When false, uses 3-pass measurement with Fill handling.
        /// </summary>
        public bool FastMeasurement
        {
            get => (bool)GetValue(FastMeasurementProperty);
            set => SetValue(FastMeasurementProperty, value);
        }

        public static readonly BindableProperty AdjustClippingProperty = BindableProperty.Create(
            nameof(AdjustClipping),
            typeof(Thickness),
            typeof(SkiaControl),
            Thickness.Zero,
            propertyChanged: NeedInvalidateMeasure);

        public Thickness AdjustClipping
        {
            get { return (Thickness)GetValue(AdjustClippingProperty); }
            set { SetValue(AdjustClippingProperty, value); }
        }

        public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding),
            typeof(Thickness),
            typeof(SkiaControl), Thickness.Zero,
            propertyChanged: NeedInvalidateMeasure);

        public Thickness Padding
        {
            get { return (Thickness)GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }

        public static readonly BindableProperty MarginProperty = BindableProperty.Create(nameof(Margin),
            typeof(Thickness),
            typeof(SkiaControl), Thickness.Zero,
            propertyChanged: NeedInvalidateMeasure);

        public Thickness Margin
        {
            get { return (Thickness)GetValue(MarginProperty); }
            set { SetValue(MarginProperty, value); }
        }

        public static readonly BindableProperty AddMarginTopProperty = BindableProperty.Create(
            nameof(AddMarginTop),
            typeof(double),
            typeof(SkiaControl),
            0.0, propertyChanged: NeedInvalidateMeasure);

        public double AddMarginTop
        {
            get { return (double)GetValue(AddMarginTopProperty); }
            set { SetValue(AddMarginTopProperty, value); }
        }

        public static readonly BindableProperty AddMarginBottomProperty = BindableProperty.Create(
            nameof(AddMarginBottom),
            typeof(double),
            typeof(SkiaControl),
            0.0, propertyChanged: NeedInvalidateMeasure);

        public double AddMarginBottom
        {
            get { return (double)GetValue(AddMarginBottomProperty); }
            set { SetValue(AddMarginBottomProperty, value); }
        }

        public static readonly BindableProperty AddMarginLeftProperty = BindableProperty.Create(
            nameof(AddMarginLeft),
            typeof(double),
            typeof(SkiaControl),
            0.0, propertyChanged: NeedInvalidateMeasure);

        public double AddMarginLeft
        {
            get { return (double)GetValue(AddMarginLeftProperty); }
            set { SetValue(AddMarginLeftProperty, value); }
        }

        public static readonly BindableProperty AddMarginRightProperty = BindableProperty.Create(
            nameof(AddMarginRight),
            typeof(double),
            typeof(SkiaControl),
            0.0, propertyChanged: NeedInvalidateMeasure);

        public double AddMarginRight
        {
            get { return (double)GetValue(AddMarginRightProperty); }
            set { SetValue(AddMarginRightProperty, value); }
        }

        /// <summary>
        /// Total calculated margins in points
        /// </summary>
        public Thickness Margins
        {
            get => _margins;
            protected set
            {
                if (value.Equals(_margins)) return;
                _margins = value;
                OnPropertyChanged();
            }
        }

        public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing),
            typeof(double),
            typeof(SkiaControl),
            8.0,
            propertyChanged: NeedInvalidateMeasure);

        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        public static readonly BindableProperty AddTranslationYProperty = BindableProperty.Create(
            nameof(AddTranslationY),
            typeof(double),
            typeof(SkiaControl),
            0.0, propertyChanged: NeedRepaint);

        public double AddTranslationY
        {
            get { return (double)GetValue(AddTranslationYProperty); }
            set { SetValue(AddTranslationYProperty, value); }
        }

        public static readonly BindableProperty AddTranslationXProperty = BindableProperty.Create(
            nameof(AddTranslationX),
            typeof(double),
            typeof(SkiaControl),
            0.0, propertyChanged: NeedRepaint);

        public double AddTranslationX
        {
            get { return (double)GetValue(AddTranslationXProperty); }
            set { SetValue(AddTranslationXProperty, value); }
        }

        public static readonly BindableProperty ExpandDirtyRegionProperty = BindableProperty.Create(
            nameof(ExpandDirtyRegion),
            typeof(Thickness),
            typeof(SkiaControl),
            defaultValueCreator: (instance) => { return new Thickness(0); },
            propertyChanged: NeedDraw);

        /// <summary>
        /// Thickness In points.
        /// You might want to expand this when your control drops shadows or does something else depassing normal bounds.
        /// This will be used for ImageComposite dirty regions management and when caching rectangles
        /// Normally cache is recorded inside DrawingRect, but you might want to expand this to include shadows around, for example.
        /// Specify number of points by which you want to expand the recording area.
        /// Also you might maybe want to include a bigger area if your control is not inside the DrawingRect due to transforms/translations.
        /// Override GetCacheRecordingArea method for a similar action.
        /// </summary>
        public Thickness ExpandDirtyRegion
        {
            get { return (Thickness)GetValue(ExpandDirtyRegionProperty); }
            set { SetValue(ExpandDirtyRegionProperty, value); }
        }

        public static readonly BindableProperty LockFocusProperty = BindableProperty.Create(
            nameof(LockFocus),
            typeof(bool), typeof(SkiaControl), false);

        public bool LockFocus
        {
            get { return (bool)GetValue(LockFocusProperty); }
            set { SetValue(LockFocusProperty, value); }
        }

        public static readonly BindableProperty IsClippedToBoundsProperty = BindableProperty.Create(
            nameof(IsClippedToBounds),
            typeof(bool), typeof(SkiaControl), false,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// This cuts shadows etc. You might want to enable it for some cases as it speeds up the rendering, it is False by default
        /// </summary>
        public bool IsClippedToBounds
        {
            get { return (bool)GetValue(IsClippedToBoundsProperty); }
            set { SetValue(IsClippedToBoundsProperty, value); }
        }

        public static readonly BindableProperty ClipEffectsProperty = BindableProperty.Create(nameof(ClipEffects),
            typeof(bool), typeof(SkiaControl), true,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// This cuts shadows etc
        /// </summary>
        public bool ClipEffects
        {
            get { return (bool)GetValue(ClipEffectsProperty); }
            set { SetValue(ClipEffectsProperty, value); }
        }

        public static readonly BindableProperty BindableTriggerProperty = BindableProperty.Create(
            nameof(BindableTrigger),
            typeof(object), typeof(SkiaControl),
            null,
            propertyChanged: TriggerPropertyChanged);

        private static void TriggerPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.OnTriggerChanged();
            }
        }

        public virtual void OnTriggerChanged()
        {
        }

        public object BindableTrigger
        {
            get { return (object)GetValue(BindableTriggerProperty); }
            set { SetValue(BindableTriggerProperty, value); }
        }

        public static readonly BindableProperty Value1Property = BindableProperty.Create(nameof(Value1),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);

        public double Value1
        {
            get { return (double)GetValue(Value1Property); }
            set { SetValue(Value1Property, value); }
        }

        public static readonly BindableProperty Value2Property = BindableProperty.Create(nameof(Value2),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);

        public double Value2
        {
            get { return (double)GetValue(Value2Property); }
            set { SetValue(Value2Property, value); }
        }

        public static readonly BindableProperty Value3Property = BindableProperty.Create(nameof(Value3),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);

        public double Value3
        {
            get { return (double)GetValue(Value3Property); }
            set { SetValue(Value3Property, value); }
        }

        //-------------------------------------------------------------
        // Value4
        //-------------------------------------------------------------
        public static readonly BindableProperty Value4Property = BindableProperty.Create(nameof(Value4),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);

        public double Value4
        {
            get { return (double)GetValue(Value4Property); }
            set { SetValue(Value4Property, value); }
        }

        public static readonly BindableProperty RenderingScaleProperty = BindableProperty.Create(nameof(RenderingScale),
            typeof(float), typeof(SkiaControl),
            -1.0f, propertyChanged: NeedUpdateScale);

        private static void NeedUpdateScale(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaControl control)
            {
                control.OnScaleChanged();
            }
        }

        public float RenderingScale
        {
            get
            {
                var value = -1f;
                try
                {
                    value = (float)GetValue(RenderingScaleProperty);
                }
                catch (Exception e)
                {
                    Super.Log(e); //catching Nullable object must have a value, is this because of NET8?
                }

                if (value <= 0)
                {
                    return GetDensity();
                }

                return value;
            }
            set { SetValue(RenderingScaleProperty, value); }
        }

        //public double RenderingScaleSafe
        //{
        //    get
        //    {
        //        var value = Density;
        //        if (value == 0)
        //            value = 1;

        //        if (RenderingScale < 0 || RenderingScale == 0)
        //        {
        //            return value;
        //        }

        //        return RenderingScale;
        //    }
        //}

        #endregion

        public SKRect RenderedAtDestination { get; set; }

        public virtual void OnScaleChanged()
        {
            InvalidateMeasure();
        }

        private Style _currentStyle;

        private void SubscribeToStyleProperties()
        {
            UnsubscribeFromOldStyle();

            _currentStyle = Style;

            if (_currentStyle != null)
            {
            }
        }

        private void UnsubscribeFromOldStyle()
        {
            if (_currentStyle != null)
            {
            }
        }

        public virtual void SetPropertyValue(BindableProperty property, object value)
        {
            this.SetValue(property, value);
        }

        public Guid Uid { get; set; } = Guid.NewGuid();

        //todo check adapt for MAUI

        public static float DegreesToRadians(float value)
        {
            return (float)((value * Math.PI) / 180);
        }

        public static float RadiansToDegrees(float value)
        {
            return (float)(value * 180 / Math.PI);
        }

        public static double DegreesToRadians(double value)
        {
            return ((value * Math.PI) / 180);
        }

        public static double RadiansToDegrees(double value)
        {
            return value * 180 / Math.PI;
        }

        public static (double X1, double Y1, double X2, double Y2) LinearGradientAngleToPoints(double direction)
        {
            //adapt to css style
            direction -= 90;

            //allow negative angles
            if (direction < 0)
                direction = 360 + direction;

            if (direction > 360)
                direction = 360;

            (double x, double y) PointOfAngle(double a)
            {
                return (x: Math.Cos(a), y: Math.Sin(a));
            }

            ;


            var eps = Math.Pow(2, -52);
            var angle = (direction % 360);
            var startPoint = PointOfAngle(DegreesToRadians(180 - angle));
            var endPoint = PointOfAngle(DegreesToRadians(360 - angle));

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

        public static TimeSpan DisposalDelay = TimeSpan.FromSeconds(3.5);
        public ObjectAliveType IsAlive { get; set; }

        public void DisposeObject()
        {
            DisposeObject(this);
        }

        /// <summary>
        /// Dispose with needed delay. 
        /// </summary>
        /// <param name="disposable"></param>
        public virtual void DisposeObject(IDisposable disposable)
        {
            if (disposable != null)
            {
                if (Superview is DrawnView view)
                {
                    try
                    {
                        view.DisposeObject(disposable);
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }
                else
                {
                    if (disposable is SkiaControl skia)
                    {
                        skia.IsAlive = ObjectAliveType.BeingDisposed;
                    }

                    Tasks.StartDelayed(DisposalDelay, () =>
                    {
                        try
                        {
                            disposable?.Dispose();
                            if (disposable is SkiaControl skia)
                            {
                                skia.IsAlive = ObjectAliveType.Disposed;
                            }
                        }
                        catch (Exception e)
                        {
                            Super.Log(e);
                        }
                    });
                }
            }
        }

        private void ViewSizeChanged(object sender, EventArgs e)
        {
            OnSizeChanged();
        }

        protected virtual void OnSizeChanged()
        {
        }

        public Action<SKPath, SKRect> Clipping { get; set; }

        /// <summary>
        /// Optional scene hero control identifier
        /// </summary>
        public string Hero { get; set; }

        /// <summary>
        /// For internal use, will be set for recycled/templated views by the ViewFactory to the corresponding index in the data source.
        /// </summary>
        public int ContextIndex { get; set; } = -1;

        public bool IsRootView()
        {
            return this.Parent == null;
        }

        /// <summary>
        ///  Destination in PIXELS, requests in UNITS. This is affected by HorizontalFillRatio and VerticalFillRatio.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="widthRequest"></param>
        /// <param name="heightRequest"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public ScaledSize DefineAvailableSize(SKRect destination,
            float widthRequest, float heightRequest, float scale, bool useModifiers = true)
        {
            var rectWidth = destination.Width;
            var wants = widthRequest * scale;
            if (wants >= 0 && wants < rectWidth)
                rectWidth = (int)wants;

            var rectHeight = destination.Height;
            wants = heightRequest * scale;
            if (wants >= 0 && wants < rectHeight)
                rectHeight = (int)wants;

            if (useModifiers)
            {
                if (HorizontalFillRatio != 1)
                {
                    rectWidth *= (float)HorizontalFillRatio;
                }

                if (VerticalFillRatio != 1)
                {
                    rectHeight *= (float)VerticalFillRatio;
                }
            }

            return ScaledSize.FromPixels(rectWidth, rectHeight, scale);
        }

        /// <summary>
        /// Set this by parent if needed, normally child can detect this itsself. If true will call Arrange when drawing.
        /// </summary>
        public bool IsLayoutDirty
        {
            get => _isLayoutDirty;
            set
            {
                if (value == _isLayoutDirty) return;
                _isLayoutDirty = value;
                OnPropertyChanged();
            }
        }

        public static float SnapToPixel(double point, double scale)
        {
            return (float)(Math.Round(point * scale) / scale);
        }

        public static SKPoint SnapSizeToPixel(SKSize point, double scale)
        {
            return new((float)(Math.Round(point.Width * scale) / scale),
                (float)(Math.Round(point.Height * scale) / scale));
        }

        public static SKPoint SnapPointToPixel(SKPoint point, double scale)
        {
            return new((float)(Math.Round(point.X * scale) / scale),
                (float)(Math.Round(point.Y * scale) / scale));
        }

        public static SKPoint SnapRectCenter(SKRect rect, float scale)
        {
            var center = new SKPoint(rect.MidX, rect.MidY);
            var snappedCenter = new SKPoint(
                (float)Math.Round(center.X * scale) / scale,
                (float)Math.Round(center.Y * scale) / scale
            );

            // Offset the rect so that its center becomes the snapped center
            return new SKPoint(
                snappedCenter.X - rect.Width / 2,
                snappedCenter.Y - rect.Height / 2
            );
        }

        public static SKRect SnapRectToPixels(SKRect rect, float scale)
        {
            var left = Math.Round(rect.Left * scale) / scale;
            var top = Math.Round(rect.Top * scale) / scale;
            var right = Math.Round(rect.Right * scale) / scale;
            var bottom = Math.Round(rect.Bottom * scale) / scale;

            return new SKRect((float)left, (float)top, (float)right, (float)bottom);
        }

        public bool RoundCenterAlignment = true;

        /// <summary>
        ///  destination in PIXELS, requests in UNITS. resulting Destination prop will be filed in PIXELS.
        /// Not using Margins nor Padding
        /// Children are responsible to apply Padding to their content and to apply Margin to destination when measuring and drawing
        /// </summary>
        /// <param name="destination">PIXELS</param>
        /// <param name="widthRequest">UNITS</param>
        /// <param name="heightRequest">UNITS</param>
        /// <param name="scale"></param>
        public SKRect CalculateLayout(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            var rectAvailable = DefineAvailableSize(destination, widthRequest, heightRequest, scale);

            var useMaxWidth = rectAvailable.Pixels.Width;
            var useMaxHeight = rectAvailable.Pixels.Height;

            var availableWidth = destination.Width;
            var availableHeight = destination.Height;

            var layoutHorizontal = new LayoutOptions(HorizontalOptions.Alignment, HorizontalOptions.Expands);
            var layoutVertical = new LayoutOptions(VerticalOptions.Alignment, VerticalOptions.Expands);

            // initial fill
            var left = destination.Left;
            var top = destination.Top;
            var right = 0f;
            var bottom = 0f;

            bool snapX = layoutHorizontal.Alignment != LayoutAlignment.Center;
            bool snapY = layoutVertical.Alignment != LayoutAlignment.Center;

            bool useHorizontalThickness = widthRequest > 0;
            bool useVerticalThickness = heightRequest > 0;
            float marginHorizontalDelta = (float)((Margins.Left - Margins.Right) * scale);
            float marginVerticalDelta = (float)((Margins.Top - Margins.Bottom) * scale);

            var realWidth = useHorizontalThickness ? useMaxWidth + marginHorizontalDelta : useMaxWidth;
            var realHeight = useVerticalThickness ? useMaxHeight + marginVerticalDelta : useMaxWidth;

            // layoutHorizontal
            switch (layoutHorizontal.Alignment)
            {
                case LayoutAlignment.Center when float.IsFinite(availableWidth) && availableWidth > useMaxWidth:
                {
                    var half = availableWidth / 2.0f - realWidth / 2.0f;
                    if (RoundCenterAlignment)
                    {
                        left += (float)Math.Ceiling(half);
                    }
                    else
                    {
                        left += (float)half;
                    }

                    right = left + useMaxWidth;

                    if (left < destination.Left)
                    {
                        left = destination.Left;
                        right = left + useMaxWidth;
                    }

                    if (right > destination.Right)
                    {
                        right = destination.Right;
                    }

                    if (useHorizontalThickness)
                    {
                        left += marginHorizontalDelta;
                        right += marginHorizontalDelta;
                    }

                    break;
                }
                case LayoutAlignment.End when float.IsFinite(destination.Right) && availableWidth > useMaxWidth:
                {
                    right = destination.Right;
                    left = right - useMaxWidth;
                    if (left < destination.Left)
                    {
                        left = destination.Left;
                    }

                    break;
                }
                case LayoutAlignment.Fill:
                case LayoutAlignment.Start:
                default:
                {
                    right = left + useMaxWidth;
                    if (right > destination.Right)
                    {
                        right = destination.Right;
                    }

                    break;
                }
            }

            // VerticalOptions
            switch (layoutVertical.Alignment)
            {
                case LayoutAlignment.Center when float.IsFinite(availableHeight) && availableHeight > useMaxHeight:
                {
                    var half = availableHeight / 2.0f - realHeight / 2.0f;
                    if (RoundCenterAlignment)
                    {
                        top += (float)Math.Ceiling(half);
                    }
                    else
                    {
                        top += (float)half;
                    }

                    bottom = top + useMaxHeight;

                    if (top < destination.Top)
                    {
                        top = destination.Top;
                        bottom = top + useMaxHeight;
                    }

                    else if (bottom > destination.Bottom)
                    {
                        bottom = destination.Bottom;
                        top = bottom - useMaxHeight;
                    }

                    if (useVerticalThickness)
                    {
                        top += marginVerticalDelta;
                        bottom += marginVerticalDelta;
                    }

                    break;
                }
                case LayoutAlignment.End when float.IsFinite(destination.Bottom) && availableHeight > useMaxHeight:
                {
                    bottom = destination.Bottom;
                    top = bottom - useMaxHeight;
                    if (top < destination.Top)
                    {
                        top = destination.Top;
                    }

                    break;
                }
                case LayoutAlignment.Start:
                case LayoutAlignment.Fill:
                default:

                    bottom = top + useMaxHeight;
                    if (bottom > destination.Bottom)
                    {
                        bottom = destination.Bottom;
                    }

                    break;
            }

            var layout = new SKRect(left, top, right, bottom);

            var offsetX = 0f;
            var offsetY = 0f;
            if (float.IsFinite(availableHeight))
            {
                offsetY = (float)VerticalPositionOffsetRatio * layout.Height;
            }

            if (float.IsFinite(availableWidth))
            {
                offsetX = (float)HorizontalPositionOffsetRatio * layout.Width;
            }

            if (snapX)
            {
                offsetX = (float)Math.Round(offsetX);
            }

            if (snapY)
            {
                offsetY = (float)Math.Round(offsetY);
            }

            layout.Offset(offsetX, offsetY);

            return layout;
        }

        private ScaledSize _contentSize = new();

        public ScaledSize ContentSize
        {
            get { return _contentSize; }
            protected set
            {
                if (_contentSize != value)
                {
                    _contentSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool WasMeasured { get; protected set; }

        protected virtual void OnDrawingSizeChanged()
        {
        }

        protected virtual void AdaptCachedLayout(SKRect destination, float scale)
        {
            //adapt cache to current request
            var newDestination = ArrangedDestination;

            newDestination.Offset(destination.Left, destination.Top);

            Destination = newDestination;
            DrawingRect = GetDrawingRectWithMargins(newDestination, scale);

            IsLayoutDirty = false;
        }

        private SKRect _lastArrangedFor = new();
        private float _lastArrangedWidth;
        private float _lastArrangedHeight;
        public float _lastMeasuredForWidth { get; protected set; }
        public float _lastMeasuredForHeight { get; protected set; }

        /// <summary>
        /// This is the destination in PIXELS with margins applied, using this to paint background. Since we enabled subpixel drawing (for smooth scroll etc) expect this to have non-rounded values, use CompareRects and similar for comparison.
        /// </summary>
        public SKRect DrawingRect
        {
            get => _drawingRect;
            set
            {
                _drawingRect = value;
                var dirty = value;
                if (ExpandDirtyRegion != Thickness.Zero)
                {
                    dirty = new(
                        value.Left - (float)Math.Round(ExpandDirtyRegion.Left * RenderingScale),
                        value.Top - (float)Math.Round(ExpandDirtyRegion.Top * RenderingScale),
                        value.Right + (float)Math.Round(ExpandDirtyRegion.Right * RenderingScale),
                        value.Bottom + (float)Math.Round(ExpandDirtyRegion.Bottom * RenderingScale)
                    );
                }

                DirtyRegion = dirty;
            }
        }

        public SKRect DirtyRegion { get; set; }

        /// <summary>
        /// Overriding VisualElement property, use DrawingRect instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Rect Bounds
        {
            get { return DrawingRect.ToMauiRectangle(); }
            private set { throw new NotImplementedException("Use DrawingRect instead."); }
        }

        /// <summary>
        /// ISkiaGestureListener impl
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool HitIsInside(float x, float y)
        {
            var hitbox = HitBoxAuto;

            //if (UsingCacheType != SkiaCacheType.None && RenderObject != null)
            //{
            //    var offsetCacheX = Math.Abs(DrawingRect.Left - RenderObject.Bounds.Left);
            //    var offsetCacheY = Math.Abs(DrawingRect.Top - RenderObject.Bounds.Top);

            //    hitbox.Offset(offsetCacheX,offsetCacheY);
            //}

            return hitbox.ContainsInclusive(x, y);
            ;
        }

        /// <summary>
        /// This can be absolutely false if we are inside a cached 
        /// rendering object parent that already moved somewhere.
        /// So coords will be of the moment we were first drawn,
        /// while if cached parent moved, our coords might differ.
        /// todo detect if parent is cached somewhere and offset hotbox by cached parent movement offset...
        /// todo think about it baby =) meanwhile just do not set gestures below cached level
        /// </summary>
        public virtual SKRect HitBoxAuto
        {
            get
            {
                var moved = ApplyTransforms(DrawingRect);

                return moved;
            }
        }

        public virtual bool IsGestureForChild(ISkiaGestureListener listener, SKPoint point)
        {
            return IsGestureForChild(listener, point.X, point.Y);
        }

        public virtual bool IsGestureForChild(ISkiaGestureListener listener, float x, float y)
        {
            var hit = listener.HitIsInside(x, y);
            return hit;
        }

        /// <summary>
        /// Will apply additional offsets: Left and Top. This is for GESTURES ONLY! work in progress
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public SKRect ApplyTransforms(SKRect rect)
        {
            return new SKRect(rect.Left + (float)((Left + UseTranslationX) * RenderingScale),
                rect.Top + (float)((Top + UseTranslationY) * RenderingScale),
                rect.Right + (float)((Left + UseTranslationX) * RenderingScale),
                rect.Bottom + (float)((Top + UseTranslationY) * RenderingScale));
        }

        /// <summary>
        /// Stores the transformation matrix used during rendering for hit testing
        /// </summary>
        public SKMatrix RenderTransformMatrix { get; protected set; } = SKMatrix.Identity;

        /// <summary>
        /// Gets the inverse of the rendering transformation matrix for hit testing
        /// </summary>
        public SKMatrix InverseRenderTransformMatrix
        {
            get
            {
                if (!RenderTransformMatrix.TryInvert(out SKMatrix inverse))
                    return SKMatrix.Identity; // if matrix can't be inverted
                return inverse;
            }
        }

        protected virtual void ApplyTransformationMatrix(SkiaDrawingContext ctx)
        {
            var matrix = RenderTransformMatrix.PostConcat(ctx.Canvas.TotalMatrix);
            ctx.Canvas.SetMatrix(matrix);
        }

        protected virtual void CreateTransformationMatrix(SkiaDrawingContext ctx, SKRect destination)
        {
            if (!HasTransform)
            {
                RenderTransformMatrix = SKMatrix.Identity;
                return;
            }

            var provider = LinkTransforms != null ? LinkTransforms : this;

            var moveX = provider.UseTranslationX * RenderingScale;
            var moveY = provider.UseTranslationY * RenderingScale;

            // Fast path for simple translation
            if (provider.Rotation == 0 &&
                provider.ScaleX == 1 && provider.ScaleY == 1 &&
                provider.SkewX == 0 && provider.SkewY == 0 &&
                provider.Perspective1 == 0 && provider.Perspective2 == 0 &&
                provider.RotationX == 0 && provider.RotationY == 0 && provider.RotationZ == 0 &&
                provider.TranslationZ == 0)
            {
                RenderTransformMatrix = SKMatrix.CreateTranslation((float)moveX, (float)moveY);
                return;
            }

            float pivotX = (float)(destination.Left + destination.Width * AnchorX);
            float pivotY = (float)(destination.Top + destination.Height * AnchorY);
            var centerX = moveX + destination.Left + destination.Width * AnchorX;
            var centerY = moveY + destination.Top + destination.Height * AnchorY;
            var skewX = SkewX > 0 ? (float)Math.Tan(Math.PI * SkewX / 180f) : 0f;
            var skewY = SkewY > 0 ? (float)Math.Tan(Math.PI * SkewY / 180f) : 0f;

            // Build transformation matrix step by step

            // Start with translation to pivot point
            SKMatrix matrix = SKMatrix.CreateTranslation(-pivotX, -pivotY);

            // Apply transforms
            var transformMatrix = new SKMatrix
            {
                TransX = (float)moveX,
                TransY = (float)moveY,
                Persp0 = Perspective1,
                Persp1 = Perspective2,
                SkewX = skewX,
                SkewY = skewY,
                Persp2 = 1,
                ScaleX = (float)provider.ScaleX,
                ScaleY = (float)provider.ScaleY
            };
            matrix = matrix.PostConcat(transformMatrix);

            // Apply 3D transformations if needed
            if (draw3d || provider.RotationX != 0 || provider.RotationY != 0 || provider.RotationZ != 0 ||
                provider.TranslationZ != 0)
            {
                draw3d = true;
                Helper3d ??= new();
                Helper3d.Reset();
                Helper3d.RotateXDegrees((float)provider.RotationX);
                Helper3d.RotateYDegrees((float)provider.RotationY);
                Helper3d.RotateZDegrees(-(float)provider.RotationZ);
                Helper3d.Translate(0, 0, (float)provider.TranslationZ);
                matrix = matrix.PostConcat(Helper3d.Matrix);
            }

            // Translate back from pivot point
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(pivotX, pivotY));

            // Apply rotation around center if needed
            if (provider.Rotation != 0)
            {
                SKMatrix rotationMatrix =
                    SKMatrix.CreateRotationDegrees((float)Rotation, (float)centerX, (float)centerY);
                matrix = matrix.PostConcat(rotationMatrix);
            }

            // Save the complete transformation matrix for hit testing
            RenderTransformMatrix = matrix;
        }

        protected virtual void ApplyTransforms(SkiaDrawingContext ctx, SKRect destination)
        {
            var moveX = UseTranslationX * RenderingScale;
            var moveY = UseTranslationY * RenderingScale;

            // Fast path for simple translation
            if (Rotation == 0 &&
                ScaleX == 1 && ScaleY == 1 &&
                SkewX == 0 && SkewY == 0 &&
                Perspective1 == 0 && Perspective2 == 0 &&
                RotationX == 0 && RotationY == 0 && RotationZ == 0 && TranslationZ == 0)
            {
                // Apply and save simple translation
                ctx.Canvas.Translate((float)moveX, (float)moveY);
                if (!NodeAttached)
                    RenderTransformMatrix = SKMatrix.CreateTranslation((float)moveX, (float)moveY);
                return;
            }

            float pivotX = (float)(destination.Left + destination.Width * AnchorX);
            float pivotY = (float)(destination.Top + destination.Height * AnchorY);
            var centerX = moveX + destination.Left + destination.Width * AnchorX;
            var centerY = moveY + destination.Top + destination.Height * AnchorY;
            var skewX = SkewX > 0 ? (float)Math.Tan(Math.PI * SkewX / 180f) : 0f;
            var skewY = SkewY > 0 ? (float)Math.Tan(Math.PI * SkewY / 180f) : 0f;

            // Build transformation matrix step by step

            // Start with translation to pivot point
            SKMatrix matrix = SKMatrix.CreateTranslation(-pivotX, -pivotY);

            // Apply transforms
            var transformMatrix = new SKMatrix
            {
                TransX = (float)moveX,
                TransY = (float)moveY,
                Persp0 = Perspective1,
                Persp1 = Perspective2,
                SkewX = skewX,
                SkewY = skewY,
                Persp2 = 1,
                ScaleX = (float)ScaleX,
                ScaleY = (float)ScaleY
            };
            matrix = matrix.PostConcat(transformMatrix);

            // Apply 3D transformations if needed
            if (draw3d || RotationX != 0 || RotationY != 0 || RotationZ != 0 || TranslationZ != 0)
            {
                draw3d = true;
                Helper3d ??= new();
                Helper3d.Reset();
                Helper3d.RotateXDegrees((float)RotationX);
                Helper3d.RotateYDegrees((float)RotationY);
                Helper3d.RotateZDegrees(-(float)RotationZ);
                Helper3d.Translate(0, 0, (float)TranslationZ);
                matrix = matrix.PostConcat(Helper3d.Matrix);
            }

            // Translate back from pivot point
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(pivotX, pivotY));

            // Apply rotation around center if needed
            if (Rotation != 0)
            {
                SKMatrix rotationMatrix =
                    SKMatrix.CreateRotationDegrees((float)Rotation, (float)centerX, (float)centerY);
                matrix = matrix.PostConcat(rotationMatrix);
            }

            // Save the complete transformation matrix for hit testing
            if (!NodeAttached)
                RenderTransformMatrix = matrix;

            // Apply to canvas (concat with canvas total matrix)
            matrix = matrix.PostConcat(ctx.Canvas.TotalMatrix);
            ctx.Canvas.SetMatrix(matrix);
        }

        public virtual SKPoint TranslateInputDirectOffsetToPoints(PointF location, SKPoint childOffsetDirect)
        {
            var thisOffset1 = TranslateInputCoords(childOffsetDirect, false);
            //apply touch coords
            var x1 = location.X + thisOffset1.X;
            var y1 = location.Y + thisOffset1.Y;
            //convert to points
            return new SKPoint(x1 / RenderingScale, y1 / RenderingScale);
        }

        public virtual SKPoint TranslateInputOffsetToPixels(PointF location, SKPoint childOffset)
        {
            var thisOffset = TranslateInputCoords(childOffset);
            return new SKPoint(location.X + thisOffset.X, location.Y + thisOffset.Y);
        }

        public virtual SKRect GetHitBoxOnCanvas()
        {
            SKRect hitbox;
            if (Super.UseFrozenVisualLayers)
            {
                hitbox = this.VisualLayer.HitBoxWithTransforms.Pixels;
            }
            else
            {
                var legacy = this.GetPositionOnCanvas();
                hitbox = new SKRect(legacy.X, legacy.Y, legacy.X + DrawingRect.Width, legacy.Y + DrawingRect.Height);
            }
            return hitbox;
        }

        /// <summary>
        /// Use this to consume gestures in your control only,
        /// do not use result for passing gestures below
        /// </summary>
        /// <param name="childOffset"></param>
        /// <returns></returns>
        public virtual SKPoint TranslateInputCoords(SKPoint childOffset, bool accountForCache = true)
        {
            //removed translation because we use matrices now todo cleanup more
            var thisOffset = SKPoint.Empty;

            //inside a cached object coordinates are frozen at the moment the snapshot was taken
            //so we must offset the coordinates to match the current drawing rect
            if (accountForCache)
            {
                if (RenderObject != null)
                {
                    thisOffset.Offset(RenderObject.TranslateInputCoords(DrawingRect));
                }
                else if (RenderObjectPrevious != null)
                {
                    thisOffset.Offset(RenderObjectPrevious.TranslateInputCoords(DrawingRect));
                }
            }

            thisOffset.Offset(childOffset);

            //layout is different from real drawing area
            var displaced = LastDrawnAt.Location - DrawingRect.Location;
            //thisOffset.Offset(displaced);

            return thisOffset;
        }

        public virtual SKPoint CalculatePositionOffset(bool cacheOnly = false,
            bool ignoreCache = false,
            bool useTranlsation = true)
        {
            var thisOffset = SKPoint.Empty;
            if (!cacheOnly && useTranlsation)
            {
                thisOffset = new SKPoint((float)((Left + UseTranslationX) * RenderingScale),
                    (float)((Top + UseTranslationY) * RenderingScale));
            }

            //inside a cached object coordinates are frozen at the moment the snapshot was taken
            //so we must offset the coordinates to match the current drawing rect
            if (!ignoreCache && UsingCacheType != SkiaCacheType.None)
            {
                if (RenderObject != null)
                {
                    thisOffset.Offset(RenderObject.CalculatePositionOffset(LastDrawnAt.Location));
                }
                else if (UsesCacheDoubleBuffering && RenderObjectPrevious != null)
                {
                    thisOffset.Offset(RenderObjectPrevious.CalculatePositionOffset(LastDrawnAt.Location));
                }
            }

            return thisOffset;
        }

        public virtual SKPoint CalculateFuturePositionOffset(bool cacheOnly = false,
            bool ignoreCache = false,
            bool useTranlsation = true)
        {
            var thisOffset = SKPoint.Empty;
            if (!cacheOnly && useTranlsation)
            {
                thisOffset = new SKPoint((float)((Left + UseTranslationX) * RenderingScale),
                    (float)((Top + UseTranslationY) * RenderingScale));
            }

            //inside a cached object coordinates are frozen at the moment the snapshot was taken
            //so we must offset the coordinates to match the current drawing rect
            if (!ignoreCache && UsingCacheType != SkiaCacheType.None)
            {
                if (RenderObject != null)
                {
                    thisOffset.Offset(RenderObject.CalculatePositionOffset(DrawingRect.Location));
                }
                else if (UsesCacheDoubleBuffering && RenderObjectPrevious != null)
                {
                    thisOffset.Offset(RenderObjectPrevious.CalculatePositionOffset(DrawingRect.Location));
                }
            }

            return thisOffset;
        }

        long _layoutChanged = 0;
        public SKRect ArrangedDestination { get; protected set; }
        private SKSize _lastSize;
        public event EventHandler LayoutIsReady;
        public event EventHandler Disposing;

        /// <summary>
        /// Layout was changed with dimensions above zero. Rather a helper method, can you more generic OnLayoutChanged().
        /// </summary>
        protected virtual void OnLayoutReady()
        {
            IsLayoutReady = true;
            LayoutIsReady?.Invoke(this, null);
        }

        /// <summary>
        /// Will be set by OnLayoutReady
        /// </summary>
        public bool IsLayoutReady { get; protected set; }

        public bool LayoutReady
        {
            get { return _layoutReady; }
            protected set
            {
                if (_layoutReady != value)
                {
                    _layoutReady = value;
                    OnPropertyChanged();
                    if (value)
                    {
                        OnLayoutReady();
                    }
                }
            }
        }

        bool _layoutReady;

        public bool CheckIsGhost()
        {
            return IsGhost || Destination == SKRect.Empty;
        }

        /// <summary>
        /// Helper on top of `Measure` +`Arrange` for an easy-use
        /// Input in PIXELS.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destinationPixels"></param>
        public void Layout(DrawingContext context, SKRect destinationPixels)
        {
            Measure(SizeRequest.Width, SizeRequest.Height, context.Scale);
            Arrange(context.Destination, SizeRequest.Width, SizeRequest.Height, context.Scale);
        }

        /// <summary>
        /// Helper on top of `Measure` +`Arrange` for an easy-use
        /// Input in PIXELS.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destinationPixels"></param>
        public void Layout(DrawingContext context, float left, float top, float right, float bottom)
        {
            Layout(context, new(left, top, right, bottom));
        }

        /// <summary>
        /// Helper on top of `Measure` +`Arrange` for an easy-use
        /// Input in PIXELS.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destinationPixels"></param>
        public void Layout(DrawingContext context, int left, int top, int right, int bottom)
        {
            Layout(context, new(left, top, right, bottom));
        }

        /// <summary>
        ///  destination in PIXELS, requests in UNITS. resulting Destination prop will be filed in PIXELS.
        /// DrawUsingRenderObject wil call this among others..
        /// </summary>
        /// <param name="destination">PIXELS</param>
        /// <param name="widthRequest">UNITS</param>
        /// <param name="heightRequest">UNITS</param>
        /// <param name="scale"></param>
        public virtual void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (!PreArrange(destination, widthRequest, heightRequest, scale))
            {
                DrawingRect = SKRect.Empty;
                return;
            }

            var width = (HorizontalOptions.Alignment == LayoutAlignment.Fill && WidthRequest < 0)
                ? -1
                : MeasuredSize.Units.Width;
            var height = (VerticalOptions.Alignment == LayoutAlignment.Fill && HeightRequest < 0)
                ? -1
                : MeasuredSize.Units.Height;

            PostArrange(destination, width, height, scale);
        }

        protected virtual void PostArrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            //create area to arrange inside
            SKRect arrangingFor = new(0, 0, destination.Width, destination.Height);

            if (!IsLayoutDirty &&
                (ViewportHeightLimit != _arrangedViewportHeightLimit ||
                 ViewportWidthLimit != _arrangedViewportWidthLimit ||
                 scale != _lastArrangedForScale ||
                 !CompareRects(arrangingFor, _lastArrangedFor, 0.5f) ||
                 !AreEqual(_lastArrangedHeight, heightRequest, 0.5f) ||
                 !AreEqual(_lastArrangedWidth, widthRequest, 0.5f)))
            {
                IsLayoutDirty = true;
            }

            if (!IsLayoutDirty)
            {
                AdaptCachedLayout(destination, scale);
                return;
            }

            //var oldDestination = Destination;
            var layout = CalculateLayout(arrangingFor, widthRequest, heightRequest, scale);
            bool layoutChanged = false;
            if (!CompareRects(layout, ArrangedDestination, 0.5f))
            {
                layoutChanged = true;
            }

            var oldDrawingRect = this.DrawingRect;

            //save to cache
            ArrangedDestination = layout;

            AdaptCachedLayout(destination, scale);

            _arrangedViewportHeightLimit = ViewportHeightLimit;
            _arrangedViewportWidthLimit = ViewportWidthLimit;
            _lastArrangedFor = arrangingFor;
            _lastArrangedForScale = scale;
            _lastArrangedHeight = heightRequest;
            _lastArrangedWidth = widthRequest;

            if (!AreEqual(oldDrawingRect.Height, DrawingRect.Height, 0.5)
                || !AreEqual(oldDrawingRect.Width, DrawingRect.Width, 0.5))
            {
                OnDrawingSizeChanged();
                layoutChanged = true;
            }

            if (layoutChanged)
                OnLayoutChanged();

            IsLayoutDirty = false;
        }

        /// <summary>
        /// PIXELS
        /// </summary>
        /// <param name="child"></param>
        /// <param name="availableWidth"></param>
        /// <param name="availableHeight"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public ScaledSize MeasureChild(SkiaControl child, double availableWidth, double availableHeight, float scale)
        {
            if (child == null)
            {
                return ScaledSize.Default;
            }

            child.OnBeforeMeasure(); //could set IsVisible or whatever inside

            if (!child.CanDraw)
                return ScaledSize.Default; //child set himself invisible

            return child.Measure((float)availableWidth, (float)availableHeight, scale);
        }

        /// <summary>
        /// Measuring as absolute layout for passed children
        /// </summary>
        /// <param name="children"></param>
        /// <param name="rectForChildrenPixels"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual ScaledSize MeasureContent(
            IEnumerable<SkiaControl> children,
            SKRect rectForChildrenPixels,
            float scale)
        {
            // Use FastMeasurement property to conditionally skip multi-pass FILL calculations
            if (FastMeasurement)
            {
                return MeasureContentFast(children, rectForChildrenPixels, scale);
            }
            else
            {
                return MeasureContentCore(children, rectForChildrenPixels, scale);
            }
        }

        /// <summary>
        /// Fast single-pass measurement for absolute layouts when FastMeasurement=true
        /// Optimized version that skips FILL handling for maximum performance
        /// </summary>
        private ScaledSize MeasureContentFast(
            IEnumerable<SkiaControl> children,
            SKRect rectForChildrenPixels,
            float scale)
        {
            var maxHeight = -1.0f;
            var maxWidth = -1.0f;
            bool heightCut = false, widthCut = false;

            // Single pass - measure all children directly
            foreach (var child in children)
            {
                if (child == null)
                    continue;

                child.OnBeforeMeasure();
                if (!child.CanDraw)
                    continue;

                var measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);

                if (!measured.IsEmpty)
                {
                    var measuredHeight = measured.Pixels.Height;
                    var measuredWidth = measured.Pixels.Width;

                    // Apply viewport limits if set
                    if (child.ViewportHeightLimit >= 0)
                    {
                        float mHeight = (float)(child.ViewportHeightLimit * scale);
                        if (measuredHeight > mHeight)
                            measuredHeight = mHeight;
                    }

                    if (child.ViewportWidthLimit >= 0)
                    {
                        float mWidth = (float)(child.ViewportWidthLimit * scale);
                        if (measuredWidth > mWidth)
                            measuredWidth = mWidth;
                    }

                    if (measuredWidth > maxWidth)
                        maxWidth = measuredWidth;

                    if (measuredHeight > maxHeight)
                        maxHeight = measuredHeight;
                }

                widthCut |= measured.WidthCut;
                heightCut |= measured.HeightCut;
            }

            // Apply Fill constraints for parent
            if (NeedFillHorizontally && float.IsFinite(rectForChildrenPixels.Width))
                maxWidth = rectForChildrenPixels.Width;

            if (NeedFillVertically && float.IsFinite(rectForChildrenPixels.Height))
                maxHeight = rectForChildrenPixels.Height;

            if (maxWidth < 0) maxWidth = 0;
            if (maxHeight < 0) maxHeight = 0;

            return ScaledSize.FromPixels(maxWidth, maxHeight, widthCut, heightCut, scale);
        }

        /// <summary>
        /// Core 3-pass measurement logic for absolute layouts with full FILL support
        /// </summary>
        private ScaledSize MeasureContentCore(
            IEnumerable<SkiaControl> children,
            SKRect rectForChildrenPixels,
            float scale)
        {
            var maxHeight = -1.0f;
            var maxWidth = -1.0f;

            var maxChildHeight = -1.0f;
            var maxChildWidth = -1.0f;

            // Optimize: only allocate collections if we'll actually use them
            List<SkiaControl> fullFill = null;
            List<(SkiaControl child, ScaledSize measured)> partialFill = null;
            var autosize = this.NeedAutoSize;
            var hadFixedSize = false;

            void PostProcessMeasuredChild(ScaledSize measured, SkiaControl child)
            {
                if (!measured.IsEmpty)
                {
                    var measuredHeight = measured.Pixels.Height;
                    var measuredWidth = measured.Pixels.Width;

                    if (child.ViewportHeightLimit >= 0)
                    {
                        float mHeight = (float)(child.ViewportHeightLimit * scale);
                        if (measuredHeight > mHeight)
                        {
                            measuredHeight = mHeight; // Optimized: direct assignment
                        }
                    }

                    if (child.ViewportWidthLimit >= 0)
                    {
                        float mWidth = (float)(child.ViewportWidthLimit * scale);
                        if (measuredWidth > mWidth)
                        {
                            measuredWidth = mWidth; // Optimized: direct assignment
                        }
                    }

                    if (measuredWidth > maxWidth && !child.NeedFillHorizontally)
                        maxWidth = measuredWidth;

                    if (measuredHeight > maxHeight && !child.NeedFillVertically)
                        maxHeight = measuredHeight;
                }
            }

            bool heightCut = false, widthCut = false;

            //PASS 1
            foreach (var child in children)
            {
                if (child == null)
                    continue;

                child.OnBeforeMeasure();
                if (!child.CanDraw)
                    continue;

                var hasHorizontalFill = child.NeedFillHorizontally;
                var hasVerticalFill = child.NeedFillVertically;

                // Only defer children with Fill in BOTH dimensions
                bool shouldDefer = hasHorizontalFill && hasVerticalFill;

                if (shouldDefer)
                {
                    // Lazy allocation: only create collection when needed
                    if (fullFill == null)
                        fullFill = new List<SkiaControl>();
                    fullFill.Add(child);
                    continue;
                }

                hadFixedSize = true;
                var measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);

                // Only record dimensions that are NOT Fill
                if (!hasHorizontalFill && measured.Pixels.Width > maxChildWidth)
                {
                    maxChildWidth = measured.Pixels.Width;
                }

                if (!hasVerticalFill && measured.Pixels.Height > maxChildHeight)
                {
                    maxChildHeight = measured.Pixels.Height;
                }

                // Add to partialFill if has Fill in only one dimension
                if (hasHorizontalFill != hasVerticalFill) // XOR - only one is true
                {
                    // Lazy allocation: only create collection when needed
                    if (partialFill == null)
                        partialFill = new List<(SkiaControl child, ScaledSize measured)>();
                    partialFill.Add((child, measured));
                }

                PostProcessMeasuredChild(measured, child);

                widthCut |= measured.WidthCut;
                heightCut |= measured.HeightCut;
            }

            //PASS 2 for those with full Fill (both dimensions) - only if needed
            if (fullFill != null)
            {
                foreach (var child in fullFill)
                {
                    ScaledSize measured;
                    if (!hadFixedSize)
                    {
                        measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
                        // Don't process fill children sizes - they adapt to parent, not define it
                    }
                    else
                    {
                        var provideWidth = rectForChildrenPixels.Width;
                        if (NeedAutoWidth && maxWidth >= 0)
                        {
                            provideWidth = maxWidth;
                        }

                        var provideHeight = rectForChildrenPixels.Height;
                        if (NeedAutoHeight && maxHeight >= 0)
                        {
                            provideHeight = maxHeight;
                        }

                        measured = MeasureChild(child, provideWidth, provideHeight, scale);
                        // Don't call PostProcessMeasuredChild for fill children
                    }

                    widthCut |= measured.WidthCut;
                    heightCut |= measured.HeightCut;
                }
            }

            //PASS 3 for those with partial Fill (only one dimension) - only if needed
            if (partialFill != null)
            {
                foreach (var (child, originalMeasured) in partialFill)
                {
                    var hasHorizontalFill = child.NeedFillHorizontally;
                    var hasVerticalFill = child.NeedFillVertically;

                    var provideWidth = hasHorizontalFill
                        ? (NeedAutoWidth && maxWidth >= 0 ? maxWidth : rectForChildrenPixels.Width)
                        : rectForChildrenPixels.Width;

                    var provideHeight = hasVerticalFill
                        ? (NeedAutoHeight && maxHeight >= 0 ? maxHeight : rectForChildrenPixels.Height)
                        : rectForChildrenPixels.Height;

                    var measured = MeasureChild(child, provideWidth, provideHeight, scale);

                    // Only record dimensions that are NOT Fill
                    if (!hasHorizontalFill && measured.Pixels.Width > maxChildWidth)
                    {
                        maxChildWidth = measured.Pixels.Width;
                    }

                    if (!hasVerticalFill && measured.Pixels.Height > maxChildHeight)
                    {
                        maxChildHeight = measured.Pixels.Height;
                    }

                    widthCut |= measured.WidthCut;
                    heightCut |= measured.HeightCut;
                }
            }

            if (NeedFillHorizontally && float.IsFinite(rectForChildrenPixels.Width))
            {
                maxWidth = rectForChildrenPixels.Width;
            }

            if (NeedFillVertically && float.IsFinite(rectForChildrenPixels.Height))
            {
                maxHeight = rectForChildrenPixels.Height;
            }

            if (maxWidth < 0)
            {
                maxWidth = maxChildWidth;
            }

            if (maxHeight < 0)
            {
                maxHeight = maxChildHeight;
            }

            return ScaledSize.FromPixels(maxWidth, maxHeight, widthCut, heightCut, scale);
        }

        /// <summary>
        /// This is to be called by layouts to propagate their binding context to children.
        /// By overriding this method any child could deny a new context or use any other custom logic.
        /// To force new context for child parent would set child's BindingContext directly skipping the use of this method.
        /// </summary>
        /// <param name="context"></param>
        public virtual void SetInheritedBindingContext(object context)
        {
            if (IsDisposing)
                return;

            BindingContext = context;
        }

        /// <summary>
        /// https://github.com/taublast/DrawnUi/issues/92#issuecomment-2408805077
        /// </summary>
        public virtual void ApplyBindingContext()
        {
            if (IsDisposing)
                return;

            foreach (var content in this.Views.ToList())
            {
                content.SetInheritedBindingContext(BindingContext);
            }

            AttachEffects();

            if (FillGradient != null)
                FillGradient.BindingContext = BindingContext;

            ApplyingBindingContext?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ApplyingBindingContext;
        protected bool BindingContextWasSet { get; set; }

        /// <summary>
        /// First Maui will apply bindings to your controls, then it would call OnBindingContextChanged, so beware on not to break bindings.
        /// </summary>
        protected override void OnBindingContextChanged()
        {
            if (IsDisposing)
                return;

            BindingContextWasSet = true;

            try
            {
                //InvalidateCacheWithPrevious();

                //InvalidateViewsList(); //we might get different ZIndex which is bindable..

                ApplyBindingContext();

                //will apply to maui prps like styles, triggers etc
                base.OnBindingContextChanged();
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        public struct ParentMeasureRequest
        {
            public IDrawnBase Parent { get; set; }
            public float WidthRequest { get; set; }
            public float HeightRequest { get; set; }
        }

        protected virtual ScaledSize MeasureInternal(MeasureRequest request)
        {
            if (!this.CanDraw || request.WidthRequest == 0 || request.HeightRequest == 0)
            {
                InvalidateCacheWithPrevious();

                return SetMeasuredAsEmpty(request.Scale);
            }

            var constraints = GetMeasuringConstraints(request);

            ContentSize = MeasureAbsolute(constraints.Content, request.Scale);

            return SetMeasuredAdaptToContentSize(constraints, request.Scale);
        }

        /// <summary>
        /// Main method for measuring, override OnMeasuring to plug-in.
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="heightConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public ScaledSize Measure(float widthConstraint, float heightConstraint, float scale)
        {
            if (IsDisposed || IsDisposing)
                return ScaledSize.Default;

            if (!WasMeasured)
            {
                InitializeMeasuring();
            }

            return OnMeasuring(widthConstraint, heightConstraint, scale);
        }

        protected virtual void InitializeMeasuring()
        {
            CalculateMargins();
            CalculateSizeRequest();
            NeedMeasure = true;
        }

        /// <summary>
        /// Input in POINTS
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="heightConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public virtual ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            if (IsDisposed || IsDisposing)
                return ScaledSize.Default;

            if (IsMeasuring) //basically we need this for cache double buffering to avoid conflicts with background thread
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
                IsMeasuring = true;

                RenderingScale = scale;

                var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);

                if (AvoidRemeasuring(request))
                {
                    return MeasuredSize;
                }

                InitializeDefaultContent();

                return MeasureInternal(request);
            }
            finally
            {
                IsMeasuring = false;
            }
        }

        public virtual bool AvoidRemeasuring(MeasureRequest request)
        {
            if (!NeedMeasure && request.IsSame)
            {
                return true;
            }
            return false;
        }

        public virtual void InitializeDefaultContent(bool force = false)
        {
            if (!DefaultContentCreated || force)
            {
                DefaultContentCreated = true;

                if (ExecuteAfterCreated.Count > 0)
                {
                    foreach (var action in ExecuteAfterCreated.Values)
                    {
                        action?.Invoke(this);
                    }
                }

                CreateDefaultContent();
            }
        }

        protected virtual ScaledSize SetMeasuredAsEmpty(float scale)
        {
            return SetMeasured(0, 0, false, false, scale);
        }

        /// <summary>
        /// Override this if you need to adjust the measured ContentSize for the parent to adapt to it. For example SkiaShape overrides this to expand by adding the needed size for stroke.
        /// </summary>
        /// <returns></returns>
        protected virtual SKSize GetContentSizeForAutosizeInPixels()
        {
            return new SKSize(
                (float)(ContentSize.Pixels.Width),
                (float)(ContentSize.Pixels.Height));
        }

        public virtual ScaledSize SetMeasuredAdaptToContentSize(MeasuringConstraints constraints,
            float scale)
        {
            var needSizePixels = GetContentSizeForAutosizeInPixels();

            var contentWidth = NeedAutoWidth
                ? needSizePixels.Width
                : SmartMax(needSizePixels.Width, constraints.Request.Width);
            var contentHeight = NeedAutoHeight
                ? needSizePixels.Height
                : SmartMax(needSizePixels.Height, constraints.Request.Height);

            var width = AdaptWidthConstraintToContentRequest(constraints, contentWidth, HorizontalOptions.Expands);
            var height = AdaptHeightConstraintToContentRequest(constraints, contentHeight, VerticalOptions.Expands);

            var widthCut = ContentSize.Pixels.Width > width + 1 || ContentSize.WidthCut;
            var heighCut = ContentSize.Pixels.Height > height + 1 || ContentSize.HeightCut;

            SKSize size = new(width, height);

            var invalid = !CompareSize(size, MeasuredSize.Pixels, 0);
            if (invalid)
            {
                InvalidateCacheWithPrevious();
            }

            return SetMeasured(size.Width, size.Height, widthCut, heighCut, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SKSize GetSizeInPoints(SKSize size, float scale)
        {
            var width = float.PositiveInfinity;
            var height = float.PositiveInfinity;
            if (double.IsFinite(size.Width) && size.Width >= 0)
            {
                width = size.Width / scale;
            }

            if (double.IsFinite(size.Height) && size.Height >= 0)
            {
                height = size.Height / scale;
            }

            return new SKSize(width, height);
        }

        /// <summary>
        /// todo use this for layout, actually this is used for measurement only .
        /// RenderingScale is set insde.
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="heightConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual MeasureRequest CreateMeasureRequest(float widthConstraint, float heightConstraint,
            float scale)
        {
            RenderingScale = scale;

            if (float.IsFinite(widthConstraint) && widthConstraint > 0)
            {
                if (HorizontalFillRatio != 1)
                {
                    widthConstraint *= (float)HorizontalFillRatio;
                }

                if (double.IsFinite(MaximumWidthRequest) && MaximumWidthRequest >= 0)
                {
                    var maxWidth = (float)(MaximumWidthRequest * scale);
                    if (widthConstraint > maxWidth)
                    {
                        widthConstraint = maxWidth;
                    }
                }
            }

            if (float.IsFinite(heightConstraint) && heightConstraint > 0)
            {
                if (VerticalFillRatio != 1)
                {
                    heightConstraint *= (float)VerticalFillRatio;
                }

                if (double.IsFinite(MaximumHeightRequest) && MaximumHeightRequest >= 0)
                {
                    var maxHeight = (float)(MaximumHeightRequest * scale);
                    if (widthConstraint > maxHeight)
                    {
                        widthConstraint = maxHeight;
                    }
                }
            }

            if (LockRatio < 0)
            {
                var size = Math.Min(heightConstraint, widthConstraint);
                size *= (float)-LockRatio;
                heightConstraint = size;
                widthConstraint = size;
            }
            else if (LockRatio > 0)
            {
                var size = Math.Max(heightConstraint, widthConstraint);
                size *= (float)LockRatio;
                heightConstraint = size;
                widthConstraint = size;
            }

            var isSame =
                !NeedMeasure
                && _lastMeasuredForScale == scale
                && AreEqual(_lastMeasuredForHeight, heightConstraint, 1)
                && AreEqual(_lastMeasuredForWidth, widthConstraint, 1);

            if (!isSame)
            {
                _lastMeasuredForWidth = widthConstraint;
                _lastMeasuredForHeight = heightConstraint;
                _lastMeasuredForScale = scale;
            }

            return new MeasureRequest(widthConstraint, heightConstraint, scale) { IsSame = isSame };
        }

        public virtual SKRect GetMeasuringRectForChildren(float widthConstraint, float heightConstraint, double scale)
        {
            var constraintLeft = (Padding.Left + Margins.Left) * scale;
            var constraintRight = (Padding.Right + Margins.Right) * scale;
            var constraintTop = (Padding.Top + Margins.Top) * scale;
            var constraintBottom = (Padding.Bottom + Margins.Bottom) * scale;

            //SKRect rectForChild = new SKRect(0 + (float)constraintLeft,
            //    0 + (float)constraintTop,
            //    widthConstraint - (float)constraintRight,
            //    heightConstraint - (float)constraintBottom);

            SKRect rectForChild = new SKRect(0, 0,
                (float)Math.Round(widthConstraint - (float)(constraintRight + constraintLeft)),
                (float)Math.Round(heightConstraint - (float)(constraintBottom + constraintTop)));


            return rectForChild;
        }

        /// <summary>
        /// Measure children inside absolute layout
        /// </summary>
        /// <param name="rectForChildrenPixels"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public virtual ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
        {
            return MeasureAbsoluteBase(rectForChildrenPixels, scale);
        }

        /// <summary>
        /// Measure children inside absolute layout
        /// Base method, not aware of any views provider, not virtual, silly measuring Children.
        /// </summary>
        /// <param name="rectForChildrenPixels"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected ScaledSize MeasureAbsoluteBase(SKRect rectForChildrenPixels, float scale)
        {
            if (Views.Count > 0)
            {
                var children = Views; //GetOrderedSubviews();
                return MeasureContent(children, rectForChildrenPixels, scale);
            }

            //empty container
            var width = 0f;
            var height = 0f;
            if (HorizontalOptions.Alignment == LayoutAlignment.Fill)
            {
                width = rectForChildrenPixels.Width;
            }

            if (VerticalOptions.Alignment == LayoutAlignment.Fill)
            {
                height = rectForChildrenPixels.Height;
            }

            return ScaledSize.FromPixels(width, height, scale);
        }

        public static SKRect ContractPixelsRect(SKRect rect, float pixels)
        {
            return new SKRect(
                rect.Left + pixels,
                rect.Top + pixels,
                rect.Right - pixels,
                rect.Bottom - pixels
            );
        }

        public static SKRect ContractPixelsRect(SKRect rect, float scale, Thickness amount)
        {
            return new SKRect(
                rect.Left + (float)((float)amount.Left * scale),
                rect.Top + (float)((float)amount.Top * scale),
                rect.Right - (float)((float)amount.Right * scale),
                rect.Bottom - (float)((float)amount.Bottom * scale)
            );
        }

        public static SKRect ExpandPixelsRect(SKRect rect, float scale, Thickness amount)
        {
            return new SKRect(
                rect.Left - (float)((float)amount.Left * scale),
                rect.Top - (float)((float)amount.Top * scale),
                rect.Right + (float)((float)amount.Right * scale),
                rect.Bottom + (float)((float)amount.Bottom * scale)
            );
        }

        public SKRect GetDrawingRectForChildren(SKRect destination, double scale)
        {
            var constraintLeft = (Padding.Left + Margins.Left) * scale;
            var constraintRight = (Padding.Right + Margins.Right) * scale;
            var constraintTop = (Padding.Top + Margins.Top) * scale;
            var constraintBottom = (Padding.Bottom + Margins.Bottom) * scale;


            SKRect rectForChild = new SKRect(
                (float)Math.Round(destination.Left + (float)constraintLeft),
                (float)Math.Round(destination.Top + (float)constraintTop),
                (float)Math.Round(destination.Right - (float)constraintRight),
                (float)Math.Round(destination.Bottom - (float)constraintBottom)
            );

            return rectForChild;
        }

        public virtual SKRect GetDrawingRectWithMargins(SKRect destination, double scale)
        {
            var constraintLeft = (float)Math.Round(Margins.Left * scale);
            var constraintRight = (float)Math.Round(Margins.Right * scale);
            var constraintTop = (float)Math.Round(Margins.Top * scale);
            var constraintBottom = (float)Math.Round(Margins.Bottom * scale);

            SKRect rectForChild;

            rectForChild = new SKRect(
                (destination.Left + constraintLeft),
                (destination.Top + constraintTop),
                (destination.Right - constraintRight),
                (destination.Bottom - constraintBottom)
            );

            // Apply ViewportHeightLimit if it's set
            if (ViewportHeightLimit >= 0)
            {
                float maxHeight = (float)Math.Round(ViewportHeightLimit * scale);
                if (rectForChild.Height > maxHeight)
                {
                    float excessHeight = rectForChild.Height - maxHeight;
                    rectForChild.Bottom -= excessHeight;
                }
            }

            // Apply ViewportWidthLimit if it's set
            if (ViewportWidthLimit >= 0)
            {
                float maxWidth = (float)Math.Round(ViewportWidthLimit * scale);
                if (rectForChild.Width > maxWidth)
                {
                    float excessWidth = rectForChild.Width - maxWidth;
                    rectForChild.Right -= excessWidth;
                }
            }

            return rectForChild;
        }

        protected object lockMeasured = new();
        bool debugMe;

        /// <summary>
        /// Flag for internal use, maynly used to avoid conflicts between measuring on ui-thread and in background. If true, measure will return last measured value.
        /// </summary>
        public bool IsMeasuring { get; protected internal set; }

        public object LockMeasure = new();

        /// <summary>
        /// Parameters in PIXELS. sets IsLayoutDirty = true;
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual ScaledSize SetMeasured(float width, float height, bool widthCut, bool heightCut, float scale)
        {
            lock (lockMeasured)
            {
                WasMeasured = true;

                NeedMeasure = false;

                IsLayoutDirty = true;

                if (double.IsFinite(height) && !double.IsNaN(height))
                {
                    Height = (height / scale) - (Margins.Top + Margins.Bottom);
                }
                else
                {
                    height = -1;
                    Height = height;
                }

                if (double.IsFinite(width) && !double.IsNaN(width))
                {
                    Width = (width / scale) - (Margins.Left + Margins.Right);
                }
                else
                {
                    width = -1;
                    Width = width;
                }

                MeasuredSize = ScaledSize.FromPixels(width, height, widthCut, heightCut, scale);

                //SetValueCore(RenderingScaleProperty, scale, SetValueFlags.None);

                OnMeasured();

                InvalidatedParent = false;

                return MeasuredSize;
            }
        }

        public virtual void SendOnMeasured()
        {
            //Debug.WriteLine($"[MEASURED] {this.GetType().Name} {this.Tag} ");
            Measured?.Invoke(this, MeasuredSize);
        }

        protected virtual void OnMeasured()
        {
            SendOnMeasured();
        }

        /// <summary>
        /// UNITS
        /// </summary>
        public event EventHandler<ScaledSize> Measured;

        public ScaledSize MeasuredSize { get; set; } = new();

        public virtual bool NeedAutoSize
        {
            get { return NeedAutoHeight || NeedAutoWidth; }
        }

        public virtual bool NeedAutoHeight
        {
            get
            {
                return LockRatio == 0 && VerticalOptions.Alignment != LayoutAlignment.Fill && SizeRequest.Height < 0;
            }
        }

        public virtual bool NeedAutoWidth
        {
            get
            {
                return LockRatio == 0 && HorizontalOptions.Alignment != LayoutAlignment.Fill && SizeRequest.Width < 0;
            }
        }

        public virtual bool NeedFillX
        {
            get
            {
                return HorizontalOptions.Alignment == LayoutAlignment.Fill && SizeRequest.Width < 0;
            }
        }

        public virtual bool NeedFillY
        {
            get
            {
                return VerticalOptions.Alignment == LayoutAlignment.Fill && SizeRequest.Height < 0;
            }
        }

        private bool _isDisposed;
        public bool IsDisposed
        {
            get { return _isDisposed; }
            protected set
            {
                if (value != _isDisposed)
                {
                    _isDisposed = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isDisposing;

        public bool IsDisposing
        {
            get { return _isDisposing; }
            protected set
            {
                if (value != _isDisposing)
                {
                    _isDisposing = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Developer can use this to mark control as to be disposed by parent custom controls
        /// </summary>
        public bool NeedDispose { get; set; }

        public TChild FindView<TChild>(string tag) where TChild : SkiaControl
        {
            if (this.Tag == tag && this is TChild)
                return this as TChild;

            var found = Views.FirstOrDefault(x => x.Tag == tag) as TChild;
            if (found == null)
            {
                //go sub level
                foreach (var view in Views)
                {
                    found = view.FindView<TChild>(tag);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return found;
        }

        public TChild FindView<TChild>() where TChild : SkiaControl
        {
            var found = Views.FirstOrDefault(x => x is TChild) as TChild;
            if (found == null)
            {
                //go sub level
                foreach (var view in Views)
                {
                    found = view.FindView<TChild>();
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return found;
        }

        public SkiaControl FindViewByTag(string tag)
        {
            if (this.Tag == tag)
                return this;

            var found = Views.FirstOrDefault(x => x.Tag == tag);
            if (found == null)
            {
                //go sub level
                foreach (var view in Views)
                {
                    found = view.FindViewByTag(tag);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// This will be executed when the control is being disposed, use to avoid memory leaks.
        /// Example of usage, an extension that would subscribe to something and add unsubscribtion here.
        /// </summary>
        public Dictionary<string, Action> ExecuteUponDisposal { get; } = new();

        /// <summary>
        /// This will be executed ones along and just before the CreateDefaultContent. This lets you execute initialization code after the control is already in the view tree and all variables you might want to use are already filled.
        /// </summary>
        public Dictionary<string, Action<SkiaControl>> ExecuteAfterCreated { get; } = new();

        public Dictionary<string, Action<SkiaControl, DrawingContext>> ExecuteOnPaint { get; } = new();

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this object has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ResourceManager"/> and
        /// optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// true to release both managed and unmanaged resources; false to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (!disposing)
            {
                _isDisposed = true;
                return;
            }

            OnWillDisposeWithChildren();

            IsDisposed = true;

            SizeChanged -= ViewSizeChanged;

            StopPostAnimators();

            //for the double buffering case it's safer to delay
            Tasks.StartDelayed(DisposalDelay, () =>
            {
                // Execute all cleanup actions
                foreach (var action in ExecuteUponDisposal.Values)
                {
                    action?.Invoke();
                }

                ExecuteUponDisposal.Clear();

                RenderObject = null;

                PaintSystem?.Dispose();

                _lastAnimatorManager = null;

                DisposeChildren();

                RenderTree?.Clear();

                GestureListeners?.Clear();

                VisualEffects?.Clear();

                OnDisposing();

                Parent = null;

                Superview = null;

                LastGradient?.Dispose();
                LastGradient = null;

                LastShadow?.Dispose();
                LastShadow = null;

                CustomizeLayerPaint = null;

                var kill2 = RenderObjectPreparing;
                RenderObjectPreparing = null;
                kill2?.Dispose();

                clipPreviousCachePath?.Dispose();
                PaintErase?.Dispose();

                var kill3 = RenderObjectPrevious;
                RenderObjectPrevious = null;
                kill3?.Dispose();

                _paintWithOpacity?.Dispose();
                _paintWithEffects?.Dispose();
                _preparedClipBounds?.Dispose();

                EffectColorFilter = null;
                EffectImageFilter = null;
                EffectRenderers = null;
                EffectsState = null;
                EffectsGestureProcessors = null;
                EffectPostRenderer = null;
            });
        }

        /// <summary>
        /// Releases unmanaged resources before the object is reclaimed by garbage collection.
        /// </summary>
        ~SkiaControl()
        {
            if (_isDisposed)
                return;
            Dispose(false);
        }

        /// <summary>
        /// Avoid setting parent to null before calling this, or set SuperView prop manually for proper cleanup of animations and gestures if any used
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected SKPaint PaintErase = new() { Color = SKColors.Transparent, BlendMode = SKBlendMode.Src };

        public static long GetNanoseconds()
        {
            double timestamp = Stopwatch.GetTimestamp();
            double nanoseconds = 1_000_000_000.0 * timestamp / Stopwatch.Frequency;
            return (long)nanoseconds;

            //double nano = 10000L * Stopwatch.GetTimestamp();
            //nano /= TimeSpan.TicksPerMillisecond;
            //nano *= 100L;
            //return (long)nano;
        }

        public virtual void OnBeforeMeasure()
        {
        }

        public virtual void OptionalOnBeforeDrawing()
        {
            if (NeedRemeasuring)
            {
                NeedRemeasuring = false;
                Invalidate();
            }
        }

        /// <summary>
        /// do not ever erase background
        /// </summary>
        public bool IsOverlay { get; set; }

        /// <summary>
        /// Executed after the rendering
        /// </summary>
        public List<IOverlayEffect> PostAnimators { get; } = new(); //to be renamed to post-effects

        public static readonly BindableProperty UpdateWhenReturnedFromBackgroundProperty = BindableProperty.Create(
            nameof(UpdateWhenReturnedFromBackground),
            typeof(bool),
            typeof(SkiaControl),
            false);

        public bool UpdateWhenReturnedFromBackground
        {
            get { return (bool)GetValue(UpdateWhenReturnedFromBackgroundProperty); }
            set { SetValue(UpdateWhenReturnedFromBackgroundProperty, value); }
        }

        public virtual void OnSuperviewShouldRenderChanged(bool state)
        {
            if (UpdateWhenReturnedFromBackground)
            {
                Update();
            }

            try
            {
                foreach (var view in Views.ToList())
                {
                    view.OnSuperviewShouldRenderChanged(state);
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        /// <summary>
        /// Normally get a a Measure by parent then parent calls Draw and we can apply the measure result.
        /// But in a case we have measured us ourselves inside PreArrange etc we must call ApplyMeasureResult because this would happen after the Draw and not before.
        /// </summary>
        public virtual void ApplyMeasureResult()
        {
        }

        /// <summary>
        /// Returns false if should not render
        /// </summary>
        /// <returns></returns>
        public virtual bool PreArrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (!CanDraw)
                return false;

            if (WillInvalidateMeasure)
            {
                WillInvalidateMeasure = false;
                InvalidateMeasureInternal();
            }

            if (WillInvalidateMeasure || NeedMeasure)
            {
                MeasureSelf(destination, widthRequest, heightRequest, scale);
            }
            else
            {
                LastArrangedInside = destination;
            }

            return true;
        }

        /// <summary>
        /// Self measuring, for top controls and those invalidated-redrawn when parents didn't re-measure them
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="widthRequest"></param>
        /// <param name="heightRequest"></param>
        /// <param name="scale"></param>
        protected virtual void MeasureSelf(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (!WasMeasured)
            {
                InitializeMeasuring();
            }

            var rectAvailable = DefineAvailableSize(destination, widthRequest, heightRequest, scale, false);
            var width = rectAvailable.Pixels.Width + (float)Margins.HorizontalThickness * scale;
            var height = rectAvailable.Pixels.Height + (float)Margins.VerticalThickness * scale;
            if (width > destination.Width)
            {
                width = destination.Width;
            }
            if (height > destination.Height)
            {
                height = destination.Height;
            }

            Measure(width, height, scale);

            ApplyMeasureResult();
        }

        protected bool IsRendering { get; set; }
        protected bool NodeAttached { get; set; }

        public VisualLayer? FindRenderedNode(SkiaControl control)
        {
            if (VisualLayer == null)
                return null;

            foreach (var node in this.VisualLayer.Children)
            {
                if (node.Control == control)
                    return node;

                var found = node.Control.FindRenderedNode(control);
                if (found != null)
                    return found;
            }

            return null;
        }

        public virtual VisualLayer CreateRenderedNode(SKRect destination, float scale)
        {
            VisualLayer parentVisualNode = null;
            if (Parent is SkiaControl skia)
            {
                parentVisualNode = skia.VisualLayer;
            }

            VisualLayer = new VisualLayer(this, parentVisualNode, destination, scale);

            if (parentVisualNode != null)
                ((SkiaControl)Parent).VisualLayer?.Children.Add(VisualLayer);

            NodeAttached = true;

            return VisualLayer;
        }

        public virtual void Render(DrawingContext context)
        {
            if (IsDisposing || IsDisposed)
                return;

            IsRendering = true;
            NodeAttached = false;

            Superview = context.Context.Superview;
            RenderingScale = context.Scale;
            NeedUpdate = false;

            VisualLayer = null;

            OnBeforeDrawing(context);

            if (WillInvalidateMeasure)
            {
                WillInvalidateMeasure = false;
                InvalidateMeasureInternal();
            }

            if (RenderObjectNeedsUpdate && !UsesCacheDoubleBuffering)
            {
                RenderObject = null; //disposal inside setter
            }

            if (!CompareRectsSize(RenderedAtDestination, context.Destination, 0.5f)
                || RenderingScale != context.Scale)
            {
                RenderedAtDestination = context.Destination;
            }

            if (RenderedAtDestination != SKRect.Empty)
            {
                Draw(context);
            }

            OnAfterDrawing(context);


            Rendered?.Invoke(this, EventArgs.Empty);

            IsRendering = false;
        }

        public event EventHandler Rendered;

        /// <summary>
        /// Lock between replacing and using RenderObject
        /// </summary>
        protected object LockDraw = new();

        /// <summary>
        /// Creating new cache lock
        /// </summary>
        protected object LockRenderObject = new();

        public virtual DrawingContext AddPaintArguments(DrawingContext ctx)
        {
            return ctx;
        }

        protected virtual void OnBeforeDrawing(DrawingContext context)
        {
            InvalidatedParent = false;
            _invalidatedParentPostponed = false;

            if (EffectsState != null)
            {
                foreach (var stateEffect in EffectsState)
                {
                    stateEffect?.UpdateState();
                }
            }
        }

        protected virtual void OnAfterDrawing(DrawingContext context)
        {
            if (_invalidatedParentPostponed)
            {
                InvalidatedParent = false;
                InvalidateParent();
            }

            if (UsingCacheType == SkiaCacheType.None)
                NeedUpdate = false; //otherwise CreateRenderingObject will set this to false

            //trying to find exact location on the canvas

            LastDrawnAt = DrawingRect;

            X = LastDrawnAt.Location.X / context.Scale;
            Y = LastDrawnAt.Location.Y / context.Scale;

            ExecutePostAnimators(context);

            if (NeedRemeasuring || NeedMeasure)
            {
                NeedRemeasuring = false;
                InvalidateMeasure();
            }
            else if (UsesCacheDoubleBuffering
                     && RenderObject != null)
            {
                if (!CompareRectsSize(DrawingRect, RenderObject.Bounds, 0.5f))
                {
                    InvalidateMeasure();
                }
            }
            else if (UsingCacheType == SkiaCacheType.ImageComposite
                     && RenderObjectPrevious != null)
            {
                if (!CompareRectsSize(DrawingRect, RenderObjectPrevious.Bounds, 0.5f))
                {
                    InvalidateMeasure();
                }
            }
        }

        protected virtual void Draw(DrawingContext context)
        {
            if (IsDisposing || IsDisposed)
                return;

            DrawUsingRenderObject(context,
                SizeRequest.Width, SizeRequest.Height);
        }

        //public new static readonly BindableProperty XProperty
        //    = BindableProperty.Create(nameof(X),
        //        typeof(double), typeof(SkiaControl),
        //        0.0f);
        private double _X;

        /// <summary>
        /// Absolute position obtained after this control was drawn on the Canvas, this is not relative to parent control.
        /// </summary>
        public new double X
        {
            get { return _X; }
            protected set
            {
                if (_X != value)
                {
                    _X = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Y;

        /// <summary>
        /// Absolute position obtained after this control was drawn on the Canvas, this is not relative to parent control.
        /// </summary>
        public new double Y
        {
            get { return _Y; }
            protected set
            {
                if (_Y != value)
                {
                    _Y = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Execute post drawing operations, like post-animators etc
        /// </summary>
        /// <param name="context"></param>
        /// <param name="scale"></param>
        protected void FinalizeDrawingWithRenderObject(DrawingContext context)
        {
        }

        public SKPoint GetPositionOffsetInPoints()
        {
            var thisOffset = CalculatePositionOffset();
            var x = (thisOffset.X) / RenderingScale;
            var y = (thisOffset.Y) / RenderingScale;
            return new((float)x, (float)y);
        }

        public SKPoint GetPositionOffsetInPixels(bool cacheOnly = false, bool ignoreCache = false,
            bool useTranslation = true)
        {
            var thisOffset = CalculatePositionOffset(cacheOnly, ignoreCache, useTranslation);
            var x = (thisOffset.X);
            var y = (thisOffset.Y);
            return new((float)x, (float)y);
        }

        public SKPoint GetFuturePositionOffsetInPixels(bool cacheOnly = false, bool ignoreCache = false,
            bool useTranslation = true)
        {
            var thisOffset = CalculateFuturePositionOffset(cacheOnly, ignoreCache, useTranslation);
            var x = (thisOffset.X);
            var y = (thisOffset.Y);
            return new((float)x, (float)y);
        }

        public SKPoint GetOffsetInsideControlInPoints(PointF location, SKPoint childOffset)
        {
            var thisOffset = TranslateInputCoords(childOffset, false);
            var x = (location.X + thisOffset.X) / RenderingScale;
            var y = (location.Y + thisOffset.Y) / RenderingScale;
            var insideX = x - X;
            var insideY = y - Y;
            return new((float)insideX, (float)insideY);
        }

        public SKPoint GetOffsetInsideControlInPixels(PointF location, SKPoint childOffset)
        {
            var thisOffset = TranslateInputCoords(childOffset, false);
            var x = location.X + thisOffset.X;
            var y = location.Y + thisOffset.Y;
            var insideX = x - X * RenderingScale;
            var insideY = y - Y * RenderingScale;
            return new((float)insideX, (float)insideY);
        }

        /// <summary>
        /// Location on the canvas after last drawing completed
        /// </summary>
        public SKRect LastDrawnAt { get; protected set; }

        public void ExecutePostAnimators(DrawingContext context)
        {
            try
            {
                if (PostAnimators.Count == 0 || IsDisposing || IsDisposed)
                {
                    return;
                }

                //Debug.WriteLine($"[ExecutePostAnimators] {Tag} {PostAnimators.Count} effects");

                foreach (var effect in PostAnimators.ToList())
                {
                    //if (effect.IsRunning)
                    {
                        if (effect.Render(context, this))
                        {
                            Repaint();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        #region CACHE

        /// <summary>
        /// Base method will call RenderViewsList.
        /// Return number of drawn views.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        protected virtual int DrawViews(DrawingContext context)
        {
            var children = GetOrderedSubviews();

            return RenderViewsList(context, (IList<SkiaControl>)children);
        }

        /// <summary>
        /// Just make us repaint to apply new transforms etc
        /// </summary>
        public virtual void Repaint()
        {
            if (IsDisposing ||
                Superview == null
                || IsDisposed || Parent == null)
                return;

            Parent?.UpdateByChild(this);
        }

        protected SKPaint _paintWithEffects = null;
        protected SKPaint _paintWithOpacity = null;
        SKPath _preparedClipBounds = null;
        private IAnimatorsManager _lastAnimatorManager;
        private Func<List<SkiaControl>> _createChildren;

        /// <summary>
        /// Can customize the SKPaint used for painting the object
        /// </summary>
        public Action<SKPaint, SKRect> CustomizeLayerPaint { get; set; }

        public Sk3dView Helper3d;

        public void DrawWithClipAndTransforms(
            DrawingContext ctx,
            SKRect transformsArea,
            bool useOpacity,
            bool useClipping,
            Action<DrawingContext> draw)
        {
            bool isClipping = (WillClipBounds || Clipping != null
                                              || ClipWith != null || HasPlatformClip()) && useClipping;

            if (isClipping)
            {
                _preparedClipBounds ??= new SKPath();
                _preparedClipBounds.Reset();

                if (HasPlatformClip())
                {
                    GetPlatformClip(_preparedClipBounds, ctx.Destination, RenderingScale);
                }
                else if (ClipWith != null)
                {
                    ClipWith.CreateClip(null, true, _preparedClipBounds);
                }
                else
                {
                    _preparedClipBounds.AddRect(ctx.Destination);
                    Clipping?.Invoke(_preparedClipBounds, ctx.Destination);
                }
            }

            bool applyOpacity = useOpacity && Opacity < 1;
            bool needTransform = HasTransform;

            if (applyOpacity || isClipping || needTransform || CustomizeLayerPaint != null)
            {
                _paintWithOpacity ??= new SKPaint
                {
                    IsAntialias = IsDistorted,
                    IsDither = IsDistorted,
                    FilterQuality = IsDistorted ? SKFilterQuality.Medium : SKFilterQuality.None
                };

                _paintWithOpacity.Color = SKColors.White.WithAlpha((byte)(0xFF * Opacity));

                var restore = 0;

                if (applyOpacity || CustomizeLayerPaint != null)
                {
                    CustomizeLayerPaint?.Invoke(_paintWithOpacity, ctx.Destination);
                    restore = ctx.Context.Canvas.SaveLayer(_paintWithOpacity);
                }
                else
                {
                    restore = ctx.Context.Canvas.Save();
                }

                if (needTransform)
                {
                    ApplyTransforms(ctx.Context, transformsArea);
                }
                else
                {
                    RenderTransformMatrix = SKMatrix.Identity;
                }

                //creating node BEFORE drawing children so they can attach
                //but AFTER we have matrix and destination ready
                CreateRenderedNode(transformsArea, ctx.Scale);

                if (isClipping)
                {
                    //ctx.Canvas.ClipPath(_preparedClipBounds, SKClipOperation.Intersect, false);
                    ClipSmart(ctx.Context.Canvas, _preparedClipBounds);
                }

                draw(ctx);

                ctx.Context.Canvas.RestoreToCount(restore);
            }
            else
            {
                //draw without transforms
                RenderTransformMatrix = SKMatrix.Identity;

                CreateRenderedNode(transformsArea, ctx.Scale);

                draw(ctx);
            }
        }

        private bool usePixelSnapping = false;

        private bool draw3d;

        public static bool IsSimpleRectangle(SKPath path)
        {
            if (path == null)
                return false;

            if (path.VerbCount != 5)
                return false;

            var iterator = path.CreateRawIterator();
            var points = new SKPoint[4];
            int lineToCount = 0;
            bool moveToFound = false;

            SKPathVerb verb;
            while ((verb = iterator.Next(points)) != SKPathVerb.Done)
            {
                switch (verb)
                {
                    case SKPathVerb.Move:
                        if (moveToFound)
                            return false; // Multiple MoveTo commands
                        moveToFound = true;
                        break;

                    case SKPathVerb.Line:
                        if (lineToCount < 4)
                        {
                            lineToCount++;
                        }
                        else
                        {
                            return false; // More than 4 LineTo commands
                        }

                        break;

                    case SKPathVerb.Close:
                        return lineToCount == 4; // Ensure we have exactly 4 LineTo commands before Close

                    default:
                        return false; // Any other command invalidates the rectangle check
                }
            }

            return false;
        }

        /// <summary>
        /// Use antialiasing from ShouldClipAntialiased
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="path"></param>
        /// <param name="operation"></param>
        public virtual void ClipSmart(SKCanvas canvas, SKPath path,
            SKClipOperation operation = SKClipOperation.Intersect)
        {
            canvas.ClipPath(path, operation, ShouldClipAntialiased);
        }

        /// <summary>
        /// This is not a static bindable property.
        /// Can be set manually or by control, for example SkiaShape sets this to true for non-rectangular shapes, or rounded corners..
        /// </summary>
        public bool ShouldClipAntialiased { get; set; }

        public virtual bool NeedMeasure
        {
            get => _needMeasure;
            set
            {
                if (value == _needMeasure) return;
                _needMeasure = value;

                if (value)
                {
                    IsLayoutDirty = true;
                    InvalidateCacheWithPrevious();
                }
                //OnPropertyChanged(); disabled atm
            }
        }

        private bool _needMeasure = true;

        /// <summary>
        /// If attached to a SuperView and rendering is in progress will run before drawing it. Run now otherwise.
        /// </summary>
        /// <param name="action"></param>
        protected void SafeAction(Action action)
        {
            var super = this.Superview;
            if (super != null)
            {
                Superview.PostponeExecutionBeforeDraw(() => { action(); });
                Repaint();
            }
            else
            {
                action();
            }
        }

        protected bool NeedRemeasuring;

        protected virtual void PaintWithShadows(DrawingContext ctx, Action render)
        {
            if (PlatformShadow != null)
            {
                using var paint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
                SetupShadow(paint, PlatformShadow, RenderingScale);
                var saved = ctx.Context.Canvas.SaveLayer(paint);
                render();
                ctx.Context.Canvas.RestoreToCount(saved);
            }
            else
            {
                render();
            }
        }

        public virtual void PaintWithEffects(DrawingContext ctx)
        {
            if (IsDisposed || IsDisposing)
                return;

            void PaintWithEffectsInternal(DrawingContext context)
            {
                PaintWithShadows(context, () => { Paint(ctx); });
            }

            if (!DisableEffects && VisualEffects.Count > 0)
            {
                if (_paintWithEffects == null)
                {
                    _paintWithEffects = new() { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
                }

                var effectColor = EffectColorFilter;
                var effectImage = EffectImageFilter;

                if (effectImage != null)
                    _paintWithEffects.ImageFilter = effectImage.CreateFilter(ctx.Destination);
                else
                    _paintWithEffects.ImageFilter = null; //will be disposed internally by effect

                if (effectColor != null)
                    _paintWithEffects.ColorFilter = effectColor.CreateFilter(ctx.Destination);
                else
                    _paintWithEffects.ColorFilter = null;

                var restore = ctx.Context.Canvas.SaveLayer(_paintWithEffects);

                bool hasDrawnControl = false;

                var renderers = EffectRenderers;

                if (renderers.Count > 0)
                {
                    foreach (var effect in renderers)
                    {
                        var chainedEffectResult = effect.Draw(ctx, PaintWithEffectsInternal);
                        if (chainedEffectResult.DrawnControl)
                            hasDrawnControl = true;
                    }
                }

                if (!hasDrawnControl)
                {
                    PaintWithEffectsInternal(ctx);
                }

                ctx.Context.Canvas.RestoreToCount(restore);
            }
            else
            {
                PaintWithEffectsInternal(ctx);
            }
        }

        /// <summary>
        /// This is the main drawing routine you should override to draw something.
        /// Base one paints background color inside DrawingRect that was defined by Arrange inside base.Draw.
        /// Pass arguments if you want to use some time-frozen data for painting at any time from any thread..
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        protected virtual void Paint(DrawingContext ctx)
        {
            if (ctx.Destination.Width == 0 || ctx.Destination.Height == 0 || IsDisposing || IsDisposed)
                return;

            PaintTintBackground(ctx.Context.Canvas, ctx.Destination);

            if (ExecuteOnPaint.Count > 0)
            {
                foreach (var action in ExecuteOnPaint.Values)
                {
                    action?.Invoke(this, ctx);
                }
            }

            WasDrawn = true;
        }

        private bool _wasDrawn;

        /// <summary>
        /// Signals if this control was drawn on canvas one time at least, it will be set by Paint method. 
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool WasDrawn
        {
            get { return _wasDrawn; }
            set
            {
                if (_wasDrawn != value)
                {
                    _wasDrawn = value;
                    OnPropertyChanged();
                    if (value)
                    {
                        OnFirstDrawn();
                    }
                }
            }
        }

        protected virtual void OnFirstDrawn()
        {
            WasFirstTimeDrawn?.Invoke(this, null);
        }

        public event EventHandler WasFirstTimeDrawn;

        /// <summary>
        /// Create this control clip for painting content.
        /// Pass arguments if you want to use some time-frozen data for painting at any time from any thread..
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
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

        public static SKColor DebugRenderingColor = SKColor.Parse("#66FFFF00");

        public double UseTranslationY
        {
            get { return TranslationY + AddTranslationY; }
        }

        public double UseTranslationX
        {
            get { return TranslationX + AddTranslationX; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool HasTransform
        {
            get
            {
                return
                    LinkTransforms != null ||
                    UseTranslationY != 0 || UseTranslationX != 0
                    || ScaleY != 1f || ScaleX != 1f
                    || Perspective1 != 0f || Perspective2 != 0f
                    || SkewX != 0 || SkewY != 0
                    || Rotation != 0 || TranslationZ != 0
                    || RotationX != 0 || RotationY != 0 || RotationZ != 0;
            }
        }

        public SkiaControl LinkTransforms { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsDistorted
        {
            get
            {
                return
                    LinkTransforms != null ||
                    Rotation != 0 || ScaleY != 1f || ScaleX != 1f
                    || Perspective1 != 0f || Perspective2 != 0f
                    || SkewX != 0 || SkewY != 0 || TranslationZ != 0
                    || RotationX != 0 || RotationY != 0 || RotationZ != 0;
            }
        }

        /// <summary>
        /// Drawing cache, applying clip and transforms as well
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="context"></param>
        /// <param name="destination"></param>
        public virtual void DrawRenderObject(DrawingContext context, CachedObject cache)
        {
            DrawWithClipAndTransforms(context, context.Destination, true, true, (ctx) =>
            {
                if (EffectPostRenderer != null)
                {
                    EffectPostRenderer
                        .Render(context); //post renderer will use this render object for rendering itsself
                }
                else
                {
                    if (_paintWithOpacity == null)
                    {
                        _paintWithOpacity = new SKPaint();
                    }

                    _paintWithOpacity.Color = SKColors.White;
                    _paintWithOpacity.IsAntialias = true;
                    _paintWithOpacity.IsDither = IsDistorted;
                    _paintWithOpacity.FilterQuality = SKFilterQuality.Medium;

                    cache.Draw(ctx.Context.Canvas, context.Destination, _paintWithOpacity);
                }
            });
        }

        /// <summary>
        /// Use to render Absolute layout. Base method is not supporting templates, override it to implemen your logic.
        /// Returns number of drawn children.
        /// </summary>
        /// <param name="skiaControls"></param>
        /// <param name="context"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <param name="debug"></param>
        protected virtual int RenderViewsList(DrawingContext context, IEnumerable<SkiaControl> skiaControls)
        {
            if (skiaControls == null)
                return 0;
            var count = 0;

            List<SkiaControlWithRect> tree = new();

            //actually base control has NO virtualization on purpose. implemented only for layouts.
            foreach (var child in skiaControls)
            {
                if (child != null)
                {
                    child.OptionalOnBeforeDrawing(); //could set IsVisible or whatever inside
                    if (child.CanDraw) //still visible 
                    {
                        child.Render(context);

                        tree.Add(new SkiaControlWithRect(child,
                            context.Destination,
                            child.DrawingRect,
                            count,
                            -1, // Default freeze index
                            child.BindingContext)); // Capture current binding context

                        count++;
                    }
                }
            }

            SetRenderingTree(tree);

            return count;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool UsesRenderingTree
        {
            get { return true; }
        }

        protected long _measuredStamp;
        protected long _builtRenderTreeStamp;

        /// <summary>
        /// Last rendered controls tree. Used by gestures etc..Please use SetRenderingTree method for setting it correctly.
        /// </summary>
        public List<SkiaControlWithRect> RenderTree { get; protected set; }

        public virtual void SetRenderingTree(List<SkiaControlWithRect> tree)
        {
            RenderTree = tree;
            _builtRenderTreeStamp = _measuredStamp;
        }

        #endregion

        public bool Invalidated { get; set; } = true;

        /// <summary>
        /// For internal use, set by Update method
        /// </summary>
        public bool NeedUpdate
        {
            get { return _needUpdate; }
            set
            {
                if (_needUpdate != value)
                {
                    _needUpdate = value;

                    if (value)
                        InvalidateCache();
                }
            }
        }

        bool _needUpdate;
        DrawnView _superview;

        /// <summary>
        /// Our canvas
        /// </summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        public DrawnView Superview
        {
            get
            {
                if (_superview == null)
                {
                    var value = GetTopParentView();
                    if (value != _superview)
                    {
                        _superview = value;
                        OnPropertyChanged();
                        SuperViewChanged();
                    }
                }

                return _superview;
            }
            protected set
            {
                if (value != _superview)
                {
                    _superview = value;
                    OnPropertyChanged();
                    SuperViewChanged();
                }
            }
        }

        public Func<Vector2, ScaledRect> DelegateGetOnScreenVisibleArea;

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
            var pixelsDestination = context.Destination;

            if (DelegateGetOnScreenVisibleArea != null)
            {
                return DelegateGetOnScreenVisibleArea(inflateByPixels);
            }

            if (this.UsingCacheType != SkiaCacheType.None)
            {
                //we are going to cache our children so they all must draw
                //regardless of the fact they might still be offscreen
                var inflated = pixelsDestination; //DrawingRect;
                inflated.Inflate(inflateByPixels.X, inflateByPixels.Y);

                return ScaledRect.FromPixels(inflated, RenderingScale);
            }

            //go up the tree to find the screen area or some parent will override this
            if (Parent != null)
            {
                return Parent.GetOnScreenVisibleArea(context, inflateByPixels);
            }

            if (Superview != null)
            {
                return Superview.GetOnScreenVisibleArea(context, inflateByPixels);
            }

            //var inflated2 = Destination;
            var inflated2 = context.Destination;
            inflated2.Inflate(inflateByPixels.X, inflateByPixels.Y);
            return ScaledRect.FromPixels(inflated2, RenderingScale);
        }

        bool _lastUpdatedVisibility;

        /// <summary>
        /// Used to check whether to apply IsClippedToBounds property
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool WillClipBounds
        {
            get { return IsClippedToBounds || (UsingCacheType != SkiaCacheType.None && !IsCacheOperations); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool WillClipEffects
        {
            get { return ClipEffects; }
        }

        /// <summary>
        /// Will not invalidate the measurement of parent if True
        /// </summary>
        public bool IsParentIndependent { get; set; }

        /// <summary>
        /// Will not call Update on Parent if True
        /// </summary>
        public bool WillNotUpdateParent { get; set; }

        protected virtual void UpdateInternal()
        {
            if (IsDisposing || IsDisposed)
                return;

            if (UpdateLocks > 0)
            {
                _neededUpdate = true;
                return;
            }

            _neededUpdate = false;

            NeedUpdateFrontCache = true;
            NeedUpdate = true;

            if (!WillNotUpdateParent)
            {
                Parent?.UpdateByChild(this);
            }
        }

        private int _updatedFromThread;
        private volatile bool _neededUpdate;

        /// <summary>
        /// Main method to invalidate cache and invoke rendering
        /// </summary>
        public virtual void Update()
        {
            if (NeedUpdate && Thread.CurrentThread.ManagedThreadId == _updatedFromThread)
            {
                //Repaint(); //todo!!!
                return;
            }

            if (OutputDebug)
            {
                Super.Log($"[SkiaControl] will Update {this}");
            }

            _updatedFromThread = Thread.CurrentThread.ManagedThreadId;

            InvalidateCache();

            UpdateInternal();

            Updated?.Invoke(this, null);
        }

        /// <summary>
        /// Triggered by Update method
        /// </summary>
        public event EventHandler Updated;

        public static MemoryStream StreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public static int DeviceUnitsToPixels(double units)
        {
            return (int)(units * GetDensity());
        }

        public static double PixelsToDeviceUnits(double units)
        {
            return units / GetDensity();
        }

        /// <summary>
        /// For internal use
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SKRect Destination { get; protected set; }

        protected SKPaint PaintSystem { get; set; }
        private bool _IsRenderingWithComposition;

        /// <summary>
        /// Internal flag indicating that the current frame will use cache composition, old cache will be reused, only dirty children will be redrawn over it
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsRenderingWithComposition
        {
            get { return _IsRenderingWithComposition; }
            protected set
            {
                if (_IsRenderingWithComposition != value)
                {
                    _IsRenderingWithComposition = value;
                    OnPropertyChanged();
                }
            }
        }

        private SKPath clipPreviousCachePath = new();

        /// <summary>
        /// Applies Background and BackgroundColor properties to paint inside destination. Returns false if there is nothing to paint painted.
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        protected virtual bool SetupBackgroundPaint(SKPaint paint, SKRect destination)
        {
            if (paint == null)
                return false;

            var color = this.BackgroundColor;
            var gradient = FillGradient;

            if (Background != null)
            {
                if (Background is SolidColorBrush solid)
                {
                    if (solid.Color != null)
                        color = solid.Color;
                }
                else if (Background is GradientBrush gradientBrush)
                {
                    gradient = SkiaGradient.FromBrush(gradientBrush);
                    if (color == null)
                        color = Colors.Black;
                }
            }
            else
            {
                if (BackgroundColor != null)
                {
                    color = BackgroundColor;
                }
            }

            if (gradient != null && color == null)
            {
                color = Colors.Black;
            }

            if (color == null || color.Alpha <= 0) return false;

            paint.Color = color.ToSKColor();
            paint.Style = SKPaintStyle.StrokeAndFill;
            paint.BlendMode = this.FillBlendMode;

            SetupGradient(paint, gradient, destination);

            return true;
        }

        /// <summary>
        /// destination in pixels, if you see no Scale parameter
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="destination"></param>
        public virtual void PaintTintBackground(SKCanvas canvas, SKRect destination)
        {
            if (PaintSystem == null)
            {
                PaintSystem = new();
            }

            var needPaint = SetupBackgroundPaint(PaintSystem, destination);

            if (needPaint)
            {
                //clip upon ImageComposite
                if (IsRenderingWithComposition)
                {
                    var previousCache = RenderObjectPrevious;
                    var offset = new SKPoint(this.DrawingRect.Left - previousCache.Bounds.Left,
                        DrawingRect.Top - previousCache.Bounds.Top);
                    clipPreviousCachePath.Reset();

                    foreach (var dirtyChild in DirtyChildrenInternal)
                    {
                        var clip = dirtyChild.DirtyRegion;
                        clip.Offset(offset);
                        clipPreviousCachePath.AddRect(clip);
                    }

                    var saved = canvas.Save();
                    canvas.ClipPath(clipPreviousCachePath, SKClipOperation.Intersect,
                        false); //we have rectangles, no need to antialiase
                    canvas.DrawRect(destination, PaintSystem);
                    canvas.RestoreToCount(saved);
                }
                else
                {
                    canvas.DrawRect(destination, PaintSystem);
                }
            }
        }

        protected SKPath CombineClipping(SKPath add, SKPath path)
        {
            if (path == null)
                return add;
            if (add != null)
                return add.Op(path, SKPathOp.Intersect);
            return null;
        }

        protected void ActionWithClipping(SKRect viewport, SKCanvas canvas, Action draw)
        {
            var clip = new SKPath();
            {
                clip.MoveTo(viewport.Left, viewport.Top);
                clip.LineTo(viewport.Right, viewport.Top);
                clip.LineTo(viewport.Right, viewport.Bottom);
                clip.LineTo(viewport.Left, viewport.Bottom);
                clip.MoveTo(viewport.Left, viewport.Top);
                clip.Close();
            }

            var saved = canvas.Save();

            ClipSmart(canvas, clip);

            draw();

            canvas.RestoreToCount(saved);
        }

        /// <summary>
        /// Summing up Margins and AddMargin.. properties
        /// </summary>
        public virtual void CalculateMargins()
        {
            //use Margin property as starting point
            //if specific margin is set (>=0) apply 
            //to final Thickness
            var margin = Margin;

            margin.Left += AddMarginLeft;
            margin.Right += AddMarginRight;
            margin.Top += AddMarginTop;
            margin.Bottom += AddMarginBottom;

            Margins = margin;
        }

        public virtual Thickness GetAllMarginsInPixels(float scale)
        {
            var constraintLeft = Math.Round((Margins.Left + Padding.Left) * scale);
            var constraintRight = Math.Round((Margins.Right + Padding.Right) * scale);
            var constraintTop = Math.Round((Margins.Top + Padding.Top) * scale);
            var constraintBottom = Math.Round((Margins.Bottom + Padding.Bottom) * scale);
            return new(constraintLeft, constraintTop, constraintRight, constraintBottom);
        }

        public virtual Thickness GetMarginsInPixels(float scale)
        {
            var constraintLeft = Math.Round((Margins.Left) * scale);
            var constraintRight = Math.Round((Margins.Right) * scale);
            var constraintTop = Math.Round((Margins.Top) * scale);
            var constraintBottom = Math.Round((Margins.Bottom) * scale);
            return new(constraintLeft, constraintTop, constraintRight, constraintBottom);
        }

        ///// <summary>
        ///// Main method to call when dimensions changed
        ///// </summary>
        //public virtual void InvalidateMeasure()
        //{
        //    if (!IsDisposed)
        //    {
        //        CalculateMargins();
        //        CalculateSizeRequest();

        //        InvalidateWithChildren();
        //        InvalidateParent();

        //        Update();
        //    }
        //}

        public virtual void InvalidateMeasureInternal()
        {
            CalculateMargins();
            CalculateSizeRequest();
            InvalidateWithChildren();
            InvalidateParent();
        }

        protected virtual void CalculateSizeRequest()
        {
            SizeRequest = GetSizeRequest((float)WidthRequest, (float)HeightRequest, false);
        }

        /// <summary>
        /// Will invoke InvalidateInternal on controls and subviews
        /// </summary>
        /// <param name="control"></param>
        public virtual void InvalidateChildrenTree(SkiaControl control)
        {
            if (control != null)
            {
                control.NeedMeasure = true;

                foreach (var view in control.Views.ToList())
                {
                    InvalidateChildrenTree(view as SkiaControl);
                }
            }
        }

        public virtual void InvalidateChildrenTree()
        {
            foreach (var view in Views.ToList())
            {
                InvalidateChildrenTree(view as SkiaControl);
            }
        }

        /// <summary>
        /// Enable this to get console logs specifically from this instance
        /// </summary>
        public bool OutputDebug { get; set; }

        public virtual void InvalidateWithChildren()
        {
            if (OutputDebug)
            {
                Super.Log($"[SkiaControl] InvalidateWithChildren");
            }

            LockUpdate(true);

            foreach (var view in Views.ToList())
            {
                InvalidateChildren(view as SkiaControl);
            }

            foreach (var effect in this.VisualEffects.ToList())
            {
                effect.Update();
            }

            LockUpdate(false);

            InvalidateInternal();
        }

        /// <summary>
        /// Will invoke InvalidateInternal on controls and subviews
        /// </summary>
        /// <param name="control"></param>
        void InvalidateChildren(SkiaControl control)
        {
            if (control != null)
            {
                control.InvalidateInternal();

                foreach (var view in control.Views.ToList())
                {
                    InvalidateChildren(view as SkiaControl);
                }
            }
        }

        protected bool WillInvalidateMeasure { get; set; }

        /// <summary>
        /// Need to re-measure and then redraw from scratch
        /// </summary>
        /// <param name="bindable"></param>
        /// <param name="oldvalue"></param>
        /// <param name="newvalue"></param>
        protected static void NeedInvalidateMeasure(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.InvalidateMeasure();
                //control.PostponeInvalidation(nameof(InvalidateMeasure), control.InvalidateMeasure);
            }
        }

        /// <summary>
        /// Need to erase cache and draw from scratch
        /// </summary>
        /// <param name="bindable"></param>
        /// <param name="oldvalue"></param>
        /// <param name="newvalue"></param>
        protected static void NeedDraw(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                //control.PostponeInvalidation(nameof(Update), control.Update);
                control.Update();
            }
        }

        /// <summary>
        /// Just make us repaint to apply new transforms etc keeping existing cache if any
        /// </summary>
        protected static void NeedRepaint(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.Repaint();
                //control.PostponeInvalidation(nameof(Repaint), control.Repaint);
            }
        }

        protected override void InvalidateMeasure()
        {
            if (WasMeasured)
            {
                InvalidateMeasureInternal();
                Update();
            }
            else
            {
                NeedMeasure = true;
            }
        }

        protected static void NeedInvalidateViewport(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as SkiaControl;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.InvalidateViewport();
                }
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        /// <summary>
        /// Is using ItemTemplate or no
        /// </summary>
        public virtual bool IsTemplated
        {
            get { return (this.ItemTemplate != null || ItemTemplateType != null); }
        }

        public virtual void OnItemTemplateChanged()
        {
        }

        public virtual object CreateContentFromTemplate()
        {
            if (ItemTemplateType != null)
            {
                return Activator.CreateInstance(ItemTemplateType);
            }

            if (ItemTemplate is DataTemplateSelector selector)
            {
                //obsolete case for limited compatibility with MAUI
                var tpl = selector.SelectTemplate(null, this);
                if (tpl == null)
                {
                    throw new ApplicationException("DrawnUI has limited compatibility wih DataTemplateSelector " +
                                                   "as it is not needed here as you can modify your cell view on the fly, at the same time template selector prohibits enhanced optimizations. Your legacy selector will be called upon first cell creation only with a null context. Kindly consider adapting your code to DrawnUI style.");
                }

                return tpl.CreateContent();
            }

            return ItemTemplate.CreateContent();
        }

        protected static void ItemTemplateChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as SkiaControl;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.OnItemTemplateChanged();
                }
            }
        }

        static object lockViews = new();

        #region Animation

        //public async Task PlayRippleAnimationAsync(Color color, double x, double y, bool removePrevious = true)
        //{
        //	var animation = new AfterEffectRipple()
        //	{
        //		X = x,
        //		Y = y,
        //		Color = color.ToSKColor(),
        //	};

        //	await PlayAnimation(animation, 350, null, removePrevious);
        //}

        public IAnimatorsManager GetAnimatorsManager()
        {
            return GetTopParentView() as IAnimatorsManager;

            //var parent = this.Parent;

            //if (parent is IAnimatorsManager manager)
            //{
            //    return manager;
            //}

            //if (parent is SkiaControl control)
            //    return control.GetAnimatorsManager();

            //return null;
        }

        public bool RegisterAnimator(ISkiaAnimator animator)
        {
            var top = GetAnimatorsManager();
            if (top != null)
            {
                _lastAnimatorManager = top;
                top.AddAnimator(animator);
                Repaint();
                return true;
            }

            return false;
        }

        public void UnregisterAnimator(Guid uid)
        {
            var top = GetAnimatorsManager();
            if (top == null)
            {
                top = _lastAnimatorManager;
            }

            top?.RemoveAnimator(uid);
        }

        public IEnumerable<ISkiaAnimator> UnregisterAllAnimatorsByType(Type type)
        {
            if (Superview != null)
                return Superview.UnregisterAllAnimatorsByType(type);

            return Array.Empty<ISkiaAnimator>();
        }

        /// <summary>
        /// Expecting input coordinates in POINTs and relative to control coordinates. Use GetOffsetInsideControlInPoints to help.
        /// </summary>
        /// <param name="color"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="removePrevious"></param>
        public async void PlayRippleAnimation(Color color, double x, double y, bool removePrevious = true)
        {
            if (removePrevious)
            {
                //UnregisterAllAnimatorsByType(typeof(RippleAnimator));
            }

            //Debug.WriteLine($"[RIPPLE] start play for '{Tag}'");
            var animation = new RippleAnimator(this) { Color = color.ToSKColor(), X = x, Y = y };
            animation.Start();
        }

        public async void PlayShimmerAnimation(Color color, float shimmerWidth = 50, float shimmerAngle = 45,
            int speedMs = 1000, bool removePrevious = true)
        {
            //Debug.WriteLine($"[SHIMMER] start play for '{Tag}'");
            if (removePrevious)
            {
                //UnregisterAllAnimatorsByType(typeof(ShimmerAnimator));
            }

            var animation = new ShimmerAnimator(this)
            {
                Color = color.ToSKColor(),
                ShimmerWidth = shimmerWidth,
                ShimmerAngle = shimmerAngle,
                Speed = speedMs
            };
            animation.Start();
        }

        #endregion

        #region PAINT HELPERS

        public SKShader CreateGradient(SKRect destination, SkiaGradient gradient)
        {
            if (gradient != null && gradient.Type != GradientType.None)
            {
                var colors = new List<SKColor>();
                foreach (var color in gradient.Colors.ToList())
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

                    var newAlpha = usingColor.Alpha * gradient.Opacity;
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

                        //float sweep = (float)Value3;//((float)this.Variable1 % (float)this.Variable2 / 100F) * 360.0F;

                        return SKShader.CreateSweepGradient(
                            new SKPoint(destination.Left + destination.Width / 2.0f,
                                destination.Top + destination.Height / 2.0f),
                            colors.ToArray(),
                            colorPositions,
                            gradient.TileMode, (float)Value1, (float)(Value1 + Value2));

                    case GradientType.Circular:
                    case GradientType.Oval:
                        var halfX = gradient.StartXRatio * destination.Width;
                        var halfY = gradient.StartYRatio * destination.Height;
                        if (gradient.Type == GradientType.Circular)
                            return SKShader.CreateRadialGradient(
                                new SKPoint(destination.Left + halfX, destination.Top + halfY),
                                Math.Min(destination.Width / 2f, destination.Height / 2f),
                                colors.ToArray(),
                                colorPositions,
                                gradient.TileMode
                            );
                        var shader = SKShader.CreateRadialGradient(
                            new SKPoint(destination.Left + halfX, destination.Top + halfY),
                            Math.Max(destination.Width / 2f, destination.Height / 2f),
                            colors.ToArray(),
                            colorPositions,
                            gradient.TileMode
                        );
                        // Create a scaling matrix centered around the gradient's origin point
                        float scaleX = destination.Width >= destination.Height
                            ? 1f
                            : destination.Width / destination.Height;
                        float scaleY = destination.Height >= destination.Width
                            ? 1f
                            : destination.Height / destination.Width;
                        var transform = SKMatrix.CreateScale(scaleX, scaleY, destination.Left + halfX,
                            destination.Top + halfY);
                        return shader.WithLocalMatrix(transform);

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

        protected CachedGradient LastGradient;
        protected CachedShadow LastShadow;

        /// <summary>
        /// Creates and sets an ImageFilter for SKPaint
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="shadow"></param>
        public bool SetupShadow(SKPaint paint, SkiaShadow shadow, float scale)
        {
            if (shadow != null && paint != null)
            {
                var kill = LastShadow;
                LastShadow = new() { Filter = CreateShadow(shadow, scale), Scale = scale, Shadow = shadow };
                DisposeObject(kill);

                var old = paint.ImageFilter;
                paint.ImageFilter = LastShadow.Filter;
                if (old != paint.ImageFilter)
                {
                    DisposeObject(old);
                }

                return true;
            }

            return false;
        }

        #endregion

        #region CHILDREN VIEWS

        private List<SkiaControl> _orderedChildren;

        public List<SkiaControl> GetOrderedSubviews(bool recalculate = false)
        {
            if (_orderedChildren == null || recalculate)
            {
                _orderedChildren = Views.OrderBy(x => x.ZIndex).ToList();
            }

            return _orderedChildren;
        }

        public IReadOnlyList<SkiaControl> GetUnorderedSubviews(bool recalculate = false)
        {
            return Views;
        }

        /// <summary>
        /// For internal use
        /// </summary>
        public List<SkiaControl> Views { get; } = new();

        public virtual void DisposeChildren()
        {
            foreach (var child in Views.ToList())
            {
                RemoveSubView(child);
                child.Dispose();
            }

            Views.Clear();
            Invalidate();
        }

        /// <summary>
        /// The OnDisposing might come with a delay to avoid disposing resources at use.
        /// This method will be called without delay when Dispose() is invoked. Disposed will set to True and for Views their OnWillDisposeWithChildren will be called.
        /// </summary>
        public virtual void OnWillDisposeWithChildren()
        {
            IsDisposing = true;

            DelegateDrawCache = null;
            Clipping = null;
            CustomizeLayerPaint = null;

            foreach (var child in Views.ToList())
            {
                if (child == null)
                    continue;

                child.OnWillDisposeWithChildren();
            }
        }

        public virtual void ClearChildren()
        {
            foreach (var child in Views.ToList())
            {
                if (child == null)
                    continue;

                RemoveSubView(child);
            }

            Views.Clear();

            Invalidate();
        }

        public virtual void InvalidateViewsList()
        {
            _orderedChildren = null;
            //NeedMeasure = true;
        }

        /// <summary>
        /// Avoid slowing us down with MAUI internals
        /// </summary>
        /// <param name="child"></param>
        /// <param name="oldLogicalIndex"></param>
        protected override void OnChildRemoved(Element child, int oldLogicalIndex)
        {
            //base.OnChildRemoved(child, oldLogicalIndex);
        }

        /// <summary>
        /// Avoid slowing us down with MAUI internals
        /// </summary>
        /// <param name="child"></param>
        protected override void OnChildAdded(Element child)
        {
            //base.OnChildAdded(child);
        }

        public virtual void OnChildAdded(SkiaControl child)
        {
            OnChildrenChanged();
        }

        public virtual void OnChildRemoved(SkiaControl child)
        {
            OnChildrenChanged();
        }

        /// <summary>
        /// Happens when child was added or removed, will call Invalidate() in base
        /// </summary>
        public virtual void OnChildrenChanged()
        {
            if (NeedAutoSize)
            {
                Invalidate();
            }
            else
            {
                Update();
            }
        }

        public virtual void OnViewAttached()
        {
        }

        public virtual void OnViewDetached()
        {
        }

        public DateTime? GestureListenerRegistrationTime { get; set; }

        public void RegisterGestureListener(ISkiaGestureListener gestureListener)
        {
            lock (LockIterateListeners)
            {
                gestureListener.GestureListenerRegistrationTime = DateTime.UtcNow;
                GestureListeners.Add(gestureListener);
                //Debug.WriteLine($"Added {gestureListener} to gestures of {this.Tag} {this}");
            }
        }

        public void UnregisterGestureListener(ISkiaGestureListener gestureListener)
        {
            lock (LockIterateListeners)
            {
                gestureListener.GestureListenerRegistrationTime = null;
                GestureListeners.Remove(gestureListener);
                //Debug.WriteLine($"Removed {gestureListener} from gestures of {this.Tag} {this}");
            }
        }

        /// <summary>
        /// Children we should check for touch hits
        /// </summary>
        //public SortedSet<ISkiaGestureListener> GestureListeners { get; } = new(new DescendingZIndexGestureListenerComparer());
        public SortedGestureListeners GestureListeners { get; } = new();

        public virtual void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
        {
            if (newvalue != null && newvalue is SkiaControl control)
            {
                Superview = control.Superview;
            }

            if (newvalue != null)
                Update();

            ParentChanged?.Invoke(this, Parent);
        }

        static object lockParent = new();

        /// <summary>
        /// This is called by SetParent  when parent should be assigned to null.
        /// Internally this sets BindingContext to null after that. You might want to override this to keep BindngContext even if unattached from Parent, for example in case of recycled cells.
        /// </summary>
        public virtual void ClearParent()
        {
            Parent = null;
            SetInheritedBindingContext(null);
        }

        public virtual void StopAnimations()
        {
            StopPostAnimators();
            Superview?.UnregisterAllAnimatorsByParent(this);
        }

        public virtual void SetParent(IDrawnBase parent)
        {
            //lock (lockParent)
            {
                if (Parent == parent)
                    return;

                var iAmGestureListener = this as ISkiaGestureListener;

                //clear previous
                if (Parent is IDrawnBase oldParent)
                {
                    //kill gestures
                    if (iAmGestureListener != null)
                    {
                        oldParent.UnregisterGestureListener(iAmGestureListener);
                    }

                    //fill animations
                    Superview?.UnregisterAllAnimatorsByParent(this);

                    oldParent.Views.Remove(this);

                    if (oldParent is SkiaControl skiaParent)
                    {
                        skiaParent.InvalidateViewsList();
                    }
                }

                if (parent == null)
                {
                    ClearParent();
                    return;
                }

                parent.Views.Add(this);
                if (parent is SkiaControl skiaParent2)
                {
                    skiaParent2.InvalidateViewsList();
                }

                Parent = parent;

                if (iAmGestureListener != null)
                {
                    parent.RegisterGestureListener(iAmGestureListener);
                }

                if (parent is IDrawnBase control)
                {
                    if (this.BindingContext == null)
                        SetInheritedBindingContext(control.BindingContext);
                }

                InvalidateInternal();
            }
        }

        #endregion

        public virtual void RegisterGestureListenersTree(SkiaControl control)
        {
            if (control.Parent == null)
                return;

            if (control is ISkiaGestureListener listener)
            {
                control.Parent.RegisterGestureListener(listener);
            }

            foreach (var view in Views)
            {
                view.RegisterGestureListenersTree(view);
            }
        }

        public virtual void UnregisterGestureListenersTree(SkiaControl control)
        {
            if (control.Parent == null)
                return;

            if (control is ISkiaGestureListener listener)
            {
                control.Parent.UnregisterGestureListener(listener);
            }

            foreach (var view in Views)
            {
                view.UnregisterGestureListenersTree(view);
            }
        }

        #region Children

        public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(DataTemplate), null,
            propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// Kind of BindableLayout.DrawnTemplate
        /// </summary>
        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public static readonly BindableProperty ItemTemplateTypeProperty = BindableProperty.Create(
            nameof(ItemTemplateType),
            typeof(Type),
            typeof(SkiaControl),
            null
            , propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// ItemTemplate alternative for faster creation
        /// </summary>
        public Type ItemTemplateType
        {
            get { return (Type)GetValue(ItemTemplateTypeProperty); }
            set { SetValue(ItemTemplateTypeProperty, value); }
        }

        protected void AddOrRemoveView(SkiaControl subView, bool add)
        {
            if (subView != null)
            {
                if (add)
                {
                    AddSubView(subView);
                }
                else
                {
                    RemoveSubView(subView);
                }
            }
        }

        /// <summary>
        /// Just a helper to pass children one by one comma separated instead of using a list
        /// </summary>
        /// <param name="children"></param>
        public void SetChildrenAsParameters(params SkiaControl[] children)
        {
            SetChildren(children);
        }

        public virtual void SetChildren(IEnumerable<SkiaControl> views)
        {
            ClearChildren();

            if (views == null)
                return;

            foreach (var child in views)
            {
                AddOrRemoveView(child, true);
            }
        }

        private static void ChildrenPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
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

                if (skiaControl.ItemTemplate == null)
                {
                    skiaControl.SetChildren(enumerableChildren);
                }

                //foreach (var subView in enumerableChildren)
                //{
                //	subView.SetParent(skiaControl);
                //}

                if (newvalue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= skiaControl.OnChildrenCollectionChanged;
                    newCollection.CollectionChanged += skiaControl.OnChildrenCollectionChanged;
                }

                skiaControl.Update();
            }
        }

        public bool HasItemTemplate
        {
            get { return ItemTemplate != null; }
        }

        private void OnChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (HasItemTemplate)
                return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (SkiaControl newChildren in e.NewItems)
                    {
                        AddOrRemoveView(newChildren, true);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                    foreach (SkiaControl oldChildren in e.OldItems ?? Array.Empty<SkiaControl>())
                    {
                        AddOrRemoveView(oldChildren, false);
                    }

                    break;
            }

            Update();
        }

        #endregion

        public AddGestures.GestureListener GesturesEffect { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (float X, float Y) RescaleAspect(float width, float height, SKRect dest, TransformAspect stretch)
        {
            float aspectX = 1;
            float aspectY = 1;

            var s1 = dest.Width / width;
            var s2 = dest.Height / height;

            switch (stretch)
            {
                case TransformAspect.None:
                    break;

                case TransformAspect.Fit:
                    aspectX = dest.Width < width ? dest.Width / width : 1;
                    aspectY = dest.Height < height ? dest.Height / height : 1;
                    break;

                case TransformAspect.Fill:
                    aspectX = width < dest.Width ? s1 : 1;
                    aspectY = height < dest.Height ? s2 : 1;
                    break;

                case TransformAspect.FitFill:
                    //fill
                    aspectX = width < dest.Width ? s1 : 1;
                    aspectY = height < dest.Height ? s2 : 1;
                    if (width * aspectX > dest.Width || height * aspectY > dest.Height)
                    {
                        //fit
                        aspectX = dest.Width < width ? dest.Width / width : 1;
                        aspectY = dest.Height < height ? dest.Height / height : 1;
                    }

                    break;

                case TransformAspect.Cover:
                    aspectX = s1;
                    aspectY = s2;
                    break;

                case TransformAspect.AspectCover:
                    aspectX = Math.Max(s1, s2);
                    aspectY = aspectX;
                    break;

                case TransformAspect.AspectFill:
                    aspectX = width < dest.Width ? Math.Max(s1, s2) : 1;
                    aspectY = aspectX;
                    break;

                case TransformAspect.AspectFit:
                    aspectX = Math.Min(s1, s2);
                    aspectY = aspectX;
                    break;

                case TransformAspect.AspectFitFill:
                    //fill
                    aspectX = width < dest.Width ? Math.Max(s1, s2) : 1;
                    aspectY = aspectX;
                    if (width * aspectX > dest.Width || height * aspectY > dest.Height)
                    {
                        //fit
                        aspectX = Math.Min(s1, s2);
                        aspectY = aspectX;
                    }

                    break;
            }

            return (aspectX, aspectY);
        }

        #region HELPERS

        public static Random Random = new Random();
        protected SKRect LastArrangedInside;
        protected double _arrangedViewportHeightLimit;
        protected double _arrangedViewportWidthLimit;
        protected float _lastMeasuredForScale;
        private bool _isLayoutDirty;
        private Thickness _margins;
        private double _lastArrangedForScale;
        private bool _needUpdateFrontCache;
        private SKRect _drawingRect;

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static bool CompareRects(SKRect a, SKRect b, float precision)
        //{
        //    return
        //        Math.Abs(a.Left - b.Left) <= precision
        //                 && Math.Abs(a.Top - b.Top) <= precision
        //                             && Math.Abs(a.Right - b.Right) <= precision
        //                             && Math.Abs(a.Bottom - b.Bottom) <= precision;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static bool CompareRectsSize(SKRect a, SKRect b, float precision)
        //{
        //    return
        //        Math.Abs(a.Width - b.Width) <= precision
        //        && Math.Abs(a.Height - b.Height) <= precision;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareFloats(float a, float b, float precision = float.Epsilon)
        {
            return Math.Abs(a - b) < precision;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareDoubles(double a, double b, double precision = double.Epsilon)
        {
            return Math.Abs(a - b) < precision;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareRects(SKRect a, SKRect b, float precision)
        {
            if ((float.IsInfinity(a.Left) && float.IsInfinity(b.Left)) || Math.Abs(a.Left - b.Left) <= precision)
            {
                if ((float.IsInfinity(a.Top) && float.IsInfinity(b.Top)) || Math.Abs(a.Top - b.Top) <= precision)
                {
                    if ((float.IsInfinity(a.Right) && float.IsInfinity(b.Right)) ||
                        Math.Abs(a.Right - b.Right) <= precision)
                    {
                        if ((float.IsInfinity(a.Bottom) && float.IsInfinity(b.Bottom)) ||
                            Math.Abs(a.Bottom - b.Bottom) <= precision)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareRectsSize(SKRect a, SKRect b, float precision)
        {
            if ((float.IsInfinity(a.Width) && float.IsInfinity(b.Width)) || (Math.Abs(a.Width - b.Width) <= precision))
            {
                if ((float.IsInfinity(a.Height) && float.IsInfinity(b.Height)) ||
                    (Math.Abs(a.Height - b.Height) <= precision))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareSize(SKSize a, SKSize b, float precision)
        {
            if ((float.IsInfinity(a.Width) && float.IsInfinity(b.Width)) || Math.Abs(a.Width - b.Width) <= precision)
            {
                if ((float.IsInfinity(a.Height) && float.IsInfinity(b.Height)) ||
                    Math.Abs(a.Height - b.Height) <= precision)
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareVectors(Vector2 a, Vector2 b, float precision)
        {
            if ((float.IsInfinity(a.X) && float.IsInfinity(b.X)) || Math.Abs(a.X - b.X) <= precision)
            {
                if ((float.IsInfinity(a.Y) && float.IsInfinity(b.Y)) || Math.Abs(a.Y - b.Y) <= precision)
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(double v1, double v2, double precision)
        {
            if (double.IsInfinity(v1) && double.IsInfinity(v2) || Math.Abs(v1 - v2) <= precision)
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(float v1, float v2, float precision)
        {
            if (float.IsInfinity(v1) && float.IsInfinity(v2) || Math.Abs(v1 - v2) <= precision)
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreVectorsEqual(Vector2 v1, Vector2 v2, float precision)
        {
            if ((float.IsInfinity(v1.X) && float.IsInfinity(v2.X)) || Math.Abs(v1.X - v2.X) <= precision)
            {
                if ((float.IsInfinity(v1.Y) && float.IsInfinity(v2.Y)) || Math.Abs(v1.Y - v2.Y) <= precision)
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectionType GetDirectionType(Vector2 velocity, DirectionType defaultDirection, float ratio)
        {
            float x = Math.Abs(velocity.X);
            float y = Math.Abs(velocity.Y);

            if (x > y && x / y > ratio)
            {
                //Debug.WriteLine($"[DirectionType] H {x:0.0},{y:0.0} = {x / y:0.00}");
                return DirectionType.Horizontal;
            }
            else if (y > x && y / x >= ratio)
            {
                return DirectionType.Vertical;
            }
            else
            {
                return defaultDirection;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectionType GetDirectionType(Vector2 start, Vector2 end, float ratio)
        {
            // Calculate the absolute differences between the X and Y coordinates
            float x = Math.Abs(end.X - start.X);
            float y = Math.Abs(end.Y - start.Y);

            // Compare the differences to determine the dominant direction
            if (x > y && x / y > ratio)
            {
                return DirectionType.Horizontal;
            }
            else if (y > x && y / x >= ratio)
            {
                return DirectionType.Vertical;
            }
            else
            {
                return
                    DirectionType
                        .None; // The direction is neither horizontal nor vertical (the vectors are equal or diagonally aligned)
            }
        }

        /// <summary>
        /// Ported from Avalonia: AreClose - Returns whether or not two floats are "close".  That is, whether or 
        /// not they are within epsilon of each other.
        /// </summary> 
        /// <param name="value1"> The first float to compare. </param>
        /// <param name="value2"> The second float to compare. </param>
        public static bool AreClose(float value1, float value2)
        {
            //in case they are Infinities (then epsilon check does not work)
            if (value1 == value2) return true;
            float eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0f) * float.Epsilon;
            float delta = value1 - value2;
            return (-eps < delta) && (eps > delta);
        }

        /// <summary>
        /// Ported from Avalonia: AreClose - Returns whether or not two doubles are "close".  That is, whether or 
        /// not they are within epsilon of each other.
        /// </summary> 
        /// <param name="value1"> The first double to compare. </param>
        /// <param name="value2"> The second double to compare. </param>
        public static bool AreClose(double value1, double value2)
        {
            //in case they are Infinities (then epsilon check does not work)
            if (value1 == value2) return true;
            double eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * double.Epsilon;
            double delta = value1 - value2;
            return (-eps < delta) && (eps > delta);
        }

        /// <summary>
        /// Avalonia: IsOne - Returns whether or not the double is "close" to 1.  Same as AreClose(double, 1),
        /// but this is faster.
        /// </summary>
        /// <param name="value"> The double to compare to 1. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOne(double value)
        {
            return Math.Abs(value - 1.0) < 10.0 * double.Epsilon;
        }

        #endregion
    }

    public static class Snapping
    {
        /// <summary>
        /// Used by the layout system to round a position translation value applying scale and initial anchor. Pass POINTS only, it wont do its job when receiving pixels!
        /// </summary>
        /// <param name="initialPosition"></param>
        /// <param name="translation"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SnapPointsToPixel(float initialPosition, float translation, double scale)
        {
            // Scale the initial and the translation
            float scaledInitial = initialPosition * (float)scale;
            float scaledTranslation = translation * (float)scale;

            // Add the scaled initial to the scaled translation
            float scaledTotal = scaledInitial + scaledTranslation;

            // Round to the nearest integer (you could also use Floor or Ceiling or Round, play with it 
            float snappedTotal = (float)Math.Round(scaledTotal);

            // Subtract the scaled initial position to get the snapped, scaled translation
            float snappedScaledTranslation = snappedTotal - scaledInitial;

            // Divide by scale to get back to your original units
            float snappedTranslation = snappedScaledTranslation / (float)scale;

            return snappedTranslation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SnapPixelsToPixel(float initialPosition, float translation)
        {
            // Sum the initial position and the translation
            float total = initialPosition + translation;

            // Round to the nearest integer to snap to the nearest pixel
            float snappedTotal = (float)Math.Round(total);

            // Find out how much we've adjusted the translation by
            float snappedTranslation = snappedTotal - initialPosition;

            return snappedTranslation;
        }
    }
}
