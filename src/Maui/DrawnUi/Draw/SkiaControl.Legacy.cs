using AppoMobi.Maui.DrawnUi.Drawn.Animations;
using AppoMobi.Maui.DrawnUi.Drawn.Infrastructure;
using AppoMobi.Maui.DrawnUi.Drawn.Infrastructure.Enums;
using AppoMobi.Maui.DrawnUi.Drawn.Infrastructure.Interfaces;
using AppoMobi.Maui.DrawnUi.Infrastructure.Extensions;
using AppoMobi.Specials.Helpers;
using Microsoft.Maui.HotReload;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Color = Microsoft.Maui.Graphics.Color;

// this is legacy code from when SkiaControl was a subclassed BindableObject

namespace AppoMobi.Maui.DrawnUi.Draw
{
    [DebuggerDisplay("{DebugString}")]
    [ContentProperty("Children")]
    public partial class SkiaControl :
        Element, //was BindableObject for speed
                 //when this goes public from provate styles could work like in standart maui VisualElement
                 // IStyleElement,
                 // IStylable,
                 // IResourcesProvider,
                 // because VisualElement pushes a lot of GC on redraw and makes everything laggy, verified
        IHasAfterEffects, ISkiaControl, ISkiaAttachable,
        IVisualTreeElement, IReloadHandler
    {

        public virtual string DebugString
        {
            get
            {
                return $"{GetType().Name} Tag {Tag}, IsVisible {IsVisible}, Children {Views.Count}, {Width:0.0}x{Height:0.0}dp, DrawingRect: {DrawingRect}";
            }
        }

        #region Delegates

        public Action<CachedObject, SkiaDrawingContext, SKRect> DelegateDrawCache { get; set; }

        #endregion

        #region HOTRELOAD

        /// <summary>
        /// HOTRELOAD IReloadHandler
        /// </summary>
        public virtual void Reload()
        {
            InvalidateMeasure();
        }

        public virtual void ReportHotreloadChildAdded(SkiaControl child)
        {
            if (child == null)
                return;

            //this.OnChildAdded(child);

            var children = GetVisualChildren();
            var index = children.FindIndex(child);

            if (index >= 0)
                VisualDiagnostics.OnChildAdded(this, child, index);
        }

        public virtual void ReportHotreloadChildRemoved(SkiaControl control)
        {
            if (control == null)
                return;


            var children = GetVisualChildren();
            var index = children.FindIndex(control);

            if (index >= 0)
                VisualDiagnostics.OnChildRemoved(this, control, index);
            //            this.OnChildRemoved(control, index);
        }

        #region IVisualTreeElement

        public virtual IReadOnlyList<IVisualTreeElement> GetVisualChildren() //working fine
        {
            return Views.Cast<IVisualTreeElement>().ToList().AsReadOnly();

            //return Views.Select(x => x as IVisualTreeElement).ToList().AsReadOnly();;
        }

        public virtual IVisualTreeElement GetVisualParent()  //working fine
        {
            return Parent as IVisualTreeElement;
        }

        #endregion

        #endregion

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (!isApplyingStyle && !string.IsNullOrEmpty(propertyName))
            {
                ExplicitPropertiesSet[propertyName] = true;
            }

            if (propertyName == nameof(ZIndex))
            {
                Parent?.InvalidateViewsList();
                Repaint();
            }

        }

        public static readonly BindableProperty ZIndexProperty = BindableProperty.Create(nameof(ZIndex),
            typeof(int), typeof(SkiaControl),
            0,
            propertyChanged: (a, b, c) =>
            {
                if (a is SkiaControl control)
                {
                    if (control.Parent != null)
                    {
                        control.Parent?.InvalidateViewsList();
                        control.Repaint();
                    }
                }
            });

        public int ZIndex
        {
            get { return (int)GetValue(ZIndexProperty); }
            set { SetValue(ZIndexProperty, value); }
        }


        public virtual bool CanDraw
        {
            get
            {
                if (!IsVisible || IsDisposed || SkipRendering)
                    return false;

                if (Superview != null && !Superview.CanDraw)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Can be set but custom controls while optimizing rendering etc. Will affect CanDraw.
        /// </summary>
        public bool SkipRendering { get; set; }


        protected bool DefaultChildrenCreated { get; set; }

        protected virtual void CreateDefaultContent()
        {

        }
        /// <summary>
        /// To create custom content in code-behind. Will be called from OnLayoutChanged if Views.Count == 0.
        /// </summary>
        public Func<List<SkiaControl>> CreateChildren
        {
            get => _createChildren;
            set
            {
                _createChildren = value;
                if (value != null)
                {
                    DefaultChildrenCreated = false;
                    CreateChildrenFromCode();
                }
            }
        }

        /// <summary>
        /// Executed when CreateChildren is set
        /// </summary>
        /// <returns></returns>
        protected virtual void CreateChildrenFromCode()
        {
            if (this.Views.Count == 0 && !DefaultChildrenCreated)
            {
                DefaultChildrenCreated = true;
                if (CreateChildren != null)
                {
                    try
                    {
                        var children = CreateChildren();
                        foreach (var skiaControl in children)
                        {
                            AddSubView(skiaControl);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

            }
        }



        public virtual void AddSubView(SkiaControl control)
        {
            if (control == null)
                return;
            control.SetParent(this);
            OnChildAdded(control);

            //if (Debugger.IsAttached)
            Superview?.PostponeExecutionAfterDraw(() =>
            {
                ReportHotreloadChildAdded(control);
            });
        }

        public virtual void RemoveSubView(SkiaControl control)
        {
            if (control == null)
                return;

            //if (Debugger.IsAttached)
            ReportHotreloadChildRemoved(control);

            control.SetParent(null);
            OnChildRemoved(control);
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
        /// //todo base. this is actually used by SkiaMauiElement only
        /// </summary>
        /// <param name="transform"></param>
        public virtual void SetVisualTransform(VisualTransform transform)
        {

        }

        public virtual void SuperViewChanged()
        {

        }

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
        public Task AnimateAsync(Action<double> callback, float ms = 250, Easing easing = null, CancellationTokenSource cancel = default)
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
                Task.Run(() =>
                {
                    animator.Dispose();
                });
            };
            animator.OnUpdated = (value) =>
            {
                if (!cancel.IsCancellationRequested)
                {
                    callback?.Invoke(value);
                }
                else
                {
                    animator.Stop();
                    Task.Run(() =>
                    {
                        animator.Dispose();
                    });
                }
            };

            animator.Start();

            return tcs.Task;
        }


        CancellationTokenSource _fadeCancelTokenSource;
        /// <summary>
        /// Fades the view from the current Opacity to end, animator is reused if already running
        /// </summary>
        /// <param name="end"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task FadeToAsync(double end, float ms = 250, Easing easing = null, CancellationTokenSource cancel = default)
        {
            // Cancel previous animation if it exists and is still running.
            _fadeCancelTokenSource?.Cancel();
            _fadeCancelTokenSource = new CancellationTokenSource();

            var startOpacity = this.Opacity;
            return AnimateAsync(
                (value) =>
                {
                    this.Opacity = startOpacity + (end - startOpacity) * value;
                    //Debug.WriteLine($"[ANIM] Opacity: {this.Opacity}");
                },
                ms,
                easing,
                _fadeCancelTokenSource);
        }

        CancellationTokenSource _scaleCancelTokenSource;
        /// <summary>
        /// Scales the view from the current Scale to x,y, animator is reused if already running
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task ScaleToAsync(double x, double y, float length = 250, Easing easing = null, CancellationTokenSource cancel = default)
        {
            // Cancel previous animation if it exists and is still running.
            _scaleCancelTokenSource?.Cancel();
            _scaleCancelTokenSource = new CancellationTokenSource();

            var startScaleX = this.ScaleX;
            var startScaleY = this.ScaleY;

            return AnimateAsync(value =>
            {
                this.ScaleX = startScaleX + (x - startScaleX) * value;
                this.ScaleY = startScaleY + (y - startScaleY) * value;
            }, length, easing, _scaleCancelTokenSource);
        }


        CancellationTokenSource _translateCancelTokenSource;
        /// <summary>
        /// Translates the view from the current position to x,y, animator is reused if already running
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task TranslateToAsync(double x, double y, uint length = 250, Easing easing = null, CancellationTokenSource cancel = default)
        {
            // Cancel previous animation if it exists and is still running.
            _translateCancelTokenSource?.Cancel();
            _translateCancelTokenSource = new CancellationTokenSource();

            var startTranslationX = this.TranslationX;
            var startTranslationY = this.TranslationY;

            return AnimateAsync(value =>
            {
                this.TranslationX = (float)(startTranslationX + (x - startTranslationX) * value);
                this.TranslationY = (float)(startTranslationY + (y - startTranslationY) * value);
            }, length, easing, _translateCancelTokenSource);
        }

        CancellationTokenSource _rotateCancelTokenSource;
        /// <summary>
        /// Rotates the view from the current rotation to end, animator is reused if already running
        /// </summary>
        /// <param name="end"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public Task RotateToAsync(double end, uint length = 250, Easing easing = null, CancellationTokenSource cancel = default)
        {
            // Cancel previous animation if it exists and is still running.
            _rotateCancelTokenSource?.Cancel();
            _rotateCancelTokenSource = new CancellationTokenSource();

            var startRotation = this.Rotation;

            return AnimateAsync(value =>
            {
                this.Rotation = (float)(startRotation + (end - startRotation) * value);
            }, length, easing, _rotateCancelTokenSource);
        }


        public virtual void OnPrintDebug()
        {

        }

        public void PrintDebug(string indent = "     ")
        {
            Console.WriteLine($"{indent}└─ {GetType().Name} {Width:0.0}x{Height:0.0} pts ({MeasuredSize.Pixels.Width:0.0}x{MeasuredSize.Pixels.Height:0.0} px)");
            OnPrintDebug();
            foreach (var view in Views)
            {
                Console.Write($"{indent}    ├─ ");
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
        /// <param name="callback"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="length"></param>
        /// <param name="easing"></param>
        /// <param name="cancel"></param>
        /// <param name="onStopped"></param>
        /// <returns></returns>
        public Task AnimateRangeAsync(Action<double> callback, double start, double end, double length = 250, Easing easing = null, CancellationToken cancel = default, bool applyEndValueOnStop = false)
        {
            RangeAnimator animator = null;

            var tcs = new TaskCompletionSource<bool>(cancel);

            tcs.Task.ContinueWith(task =>
            {
                animator?.Dispose();
            });

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
        /// Apply margins to SizeRequest
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public float AdaptWidthConstraintToRequest(float widthConstraint, Thickness constraints, double scale)
        {
            var widthPixels = (float)Math.Round(SizeRequest.Width * scale + constraints.Left + constraints.Right);

            if (SizeRequest.Width >= 0)
                widthConstraint = widthPixels;

            return widthConstraint;
        }

        /// <summary>
        /// Apply margins to SizeRequest
        /// </summary>
        /// <param name="heightConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public float AdaptHeightContraintToRequest(float heightConstraint, Thickness constraints, double scale)
        {
            var widthPixels = (float)Math.Round(SizeRequest.Height * scale + constraints.Top + constraints.Bottom);

            if (SizeRequest.Height >= 0)
                heightConstraint = widthPixels;

            return heightConstraint;
        }

        public virtual MeasuringConstraints GetMeasuringConstraints(MeasureRequest request)
        {
            var withLock = GetSizeRequest(request.WidthRequest, request.HeightRequest, true);
            var constraints = CalculateContentConstraintsInPixels(request.Scale);
            var adaptedWidthConstraint = AdaptWidthConstraintToRequest(withLock.Width, constraints, request.Scale);
            var adaptedHeightConstraint = AdaptHeightContraintToRequest(withLock.Height, constraints, request.Scale);
            var rectForChildrenPixels = GetMeasuringRectForChildren(adaptedWidthConstraint, adaptedHeightConstraint, request.Scale);
            return new MeasuringConstraints
            {
                Margins = constraints,
                Request = new(adaptedWidthConstraint, adaptedHeightConstraint),
                Content = rectForChildrenPixels
            };
        }


        public static float AdaptConstraintToContentRequest(
            float constraintPixels,
            double measuredDimension,
            double sideConstraintsPixels,
            bool autoSize,
            double minRequest, double maxRequest, float scale)
        {
            var contentDimension = sideConstraintsPixels + measuredDimension;

            if (autoSize && (measuredDimension > 0 && measuredDimension < constraintPixels)
                || float.IsInfinity(constraintPixels))
            {
                constraintPixels = (float)contentDimension;
            }

            if (minRequest >= 0)
            {
                var min = Math.Round(minRequest * scale);
                constraintPixels = (float)Math.Max(constraintPixels, min);
            }

            if (maxRequest >= 0)
            {
                var max = Math.Round(maxRequest * scale);
                constraintPixels = (float)Math.Min(constraintPixels, max);
            }

            return (float)Math.Round(constraintPixels);
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
                RenderingScale);
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
                RenderingScale);
        }

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

            return new Size(widthRequestPts, heightRequestPts); ;
        }



        /// <summary>
        /// Use Superview from public area
        /// </summary>
        /// <returns></returns>
        protected DrawnView GetTopParentView()
        {
            return GetParentElement(this) as DrawnView;
        }
        public static IDrawnBase GetParentElement(IDrawnBase control)
        {
            if (control is DrawnView)
            {
                return control;
            }

            if (control is SkiaControl skia)
            {
                if (skia.Parent != null)
                    return GetParentElement(skia.Parent);
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

            //            return IsPointInside((float)args.Location.X + offsetX, (float)args.Location.Y + offsetY, (float)RenderingScale);
        }


        /// <summary>
        /// To detect if a gesture Start point was inside Destination
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool GestureStartedInside(TouchActionEventArgs args, float offsetX = 0, float offsetY = 0)
        {
            return IsPixelInside((float)args.StartingLocation.X + offsetX, (float)args.StartingLocation.Y + offsetY);

            //            return IsPointInside((float)args.Distance.Start.X + offsetX, (float)args.Distance.Start.Y + offsetY, (float)RenderingScale);
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
            bool isInside = rect.Contains(xx, yy);

            return isInside;
        }

        public bool IsPixelInside(SKRect rect, float x, float y)
        {
            bool isInside = rect.Contains(x, y);
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

        protected virtual bool CheckGestureIsInsideChild(SkiaControl child, TouchActionEventArgs args, float offsetX = 0, float offsetY = 0)
        {
            return child.CanDraw && child.GestureIsInside(args, offsetX, offsetY);
        }

        protected virtual bool CheckGestureIsForChild(SkiaControl child, TouchActionEventArgs args, float offsetX = 0, float offsetY = 0)
        {
            return child.CanDraw && child.GestureStartedInside(args, offsetX, offsetY);
        }

        protected object LockIterateListeners = new();

        public static readonly BindableProperty LockChildrenGesturesProperty = BindableProperty.Create(nameof(LockChildrenGestures),
            typeof(LockTouch),
            typeof(SkiaControl),
            LockTouch.Disabled);
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

        public ConcurrentDictionary<string, ISkiaGestureListener> HadInput { get; } = new();

        public virtual ISkiaGestureListener OnGestureEvent(TouchActionType type, TouchActionEventArgs args,
    TouchActionResult touchAction,
    SKPoint childOffset, SKPoint childOffsetDirect)
        {
            //Debug.WriteLine($"[IN] {type} {touchAction}");

            if (Superview == null)
            {
                //shit happened. we are capturing input but we are not supposed to be on the screen!
                Console.WriteLine($"[OnGestureEvent] base captured by unassigned view {this.GetType()} {this.Tag}");
                return null;
            }

            if (TouchEffect.LogEnabled)
            {
                Trace.WriteLine($"[BASE] {this.Tag} Got {touchAction}.. {Uid}");
            }

            lock (LockIterateListeners)
            {
                ISkiaGestureListener consumed = null;

                try
                {
                    if (CheckChildrenGesturesLocked(touchAction))
                        return null;

                    bool manageChildFocus = false;

                    var thisOffset = TranslateInputCoords(childOffset);
                    var x = args.Location.X + thisOffset.X;
                    var y = args.Location.Y + thisOffset.Y;

                    //if (Tag == "ArticleWrapper" && touchAction == TouchActionResult.Tapped && RenderObject != null)
                    //{
                    //    Trace.WriteLine($"[ArticleWrapper] to children {DrawingRect}:{RenderObject.Bounds} -> {thisOffset}");
                    //}

                    if (HadInput.Count > 0)
                    {
                        if (
                            (touchAction == TouchActionResult.Panned ||
                             touchAction == TouchActionResult.Panning ||
                             touchAction == TouchActionResult.Pinched ||
                             touchAction == TouchActionResult.Up))
                        {
                            foreach (var hadInput in HadInput.Values)
                            {
                                if (!hadInput.CanDraw || hadInput.InputTransparent)
                                {
                                    continue;
                                }
                                consumed = hadInput.OnGestureEvent(type, args, touchAction, TranslateInputCoords(childOffset, true), TranslateInputCoords(childOffsetDirect, false));
                                if (consumed != null)
                                {
                                    //consumed = hadInput;
                                    if (touchAction != TouchActionResult.Up)
                                        break;
                                }
                            }
                        }
                        else
                        {
                            HadInput.Clear();
                        }
                    }

                    if (consumed == null)
                        foreach (var listener in GestureListeners)
                        {
                            if (!listener.CanDraw || listener.InputTransparent)
                                continue;

                            if (HadInput.Values.Contains(listener) &&
                        (touchAction == TouchActionResult.Panned ||
                        touchAction == TouchActionResult.Panning ||
                        touchAction == TouchActionResult.Pinched ||
                        touchAction == TouchActionResult.Up))
                            {
                                continue;
                            }

                            //Debug.WriteLine($"Checking {listener} gestures in {this.Tag} {this}");

                            if (listener == Superview.FocusedChild)
                                manageChildFocus = true;

                            var forChild = IsGestureForChild(listener, x, y);

                            //if (Tag == "ArticleWrapper" && touchAction == TouchActionResult.Tapped)
                            //{
                            //    if (listener is HandleGestures.GestureListener cx)
                            //        Trace.WriteLine($"[ArticleWrapper] for child {cx.Tag} {forChild} at {x:0},{y:0} -> {cx.HitBoxAuto} diff {cx.HitBoxAuto.Top}-{y}");
                            //    else
                            //    if (listener is SkiaControl c)
                            //        Trace.WriteLine($"[ArticleWrapper] for child {c.Tag} {forChild} at {x:0},{y:0} -> {c.HitBoxAuto} diff {c.HitBoxAuto.Top}-{y}");

                            //    if (RenderObject != null)
                            //    {
                            //        var check = this.RenderObject.Test(this.DrawingRect);
                            //    }

                            //    forChild = IsGestureForChild(listener, x, y);
                            //}

                            if (TouchEffect.LogEnabled)
                            {
                                if (listener is SkiaControl c)
                                {
                                    Debug.WriteLine($"[BASE] for child {forChild} {c.Tag} at {x:0},{y:0} -> {c.HitBoxAuto} ");
                                }
                            }

                            if (forChild)
                            {
                                if (manageChildFocus && listener == Superview.FocusedChild)
                                {
                                    manageChildFocus = false;
                                }
                                //Console.WriteLine($"[OnGestureEvent] sent {touchAction} to {listener.Tag}");
                                consumed = listener.OnGestureEvent(type, args, touchAction, TranslateInputCoords(childOffset, true), TranslateInputCoords(childOffsetDirect, false));
                                if (consumed != null)
                                {
                                    if (touchAction != TouchActionResult.Up)
                                    {
                                        HadInput.TryAdd(listener.Uid, consumed);
                                    }
                                    break;
                                }
                            }
                        }

                    if (HadInput.Count > 0 && touchAction == TouchActionResult.Up)
                    {
                        HadInput.Clear();
                    }

                    if (manageChildFocus)
                    {
                        Superview.FocusedChild = null;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                return consumed;
            }
        }



        public bool UpdateLocked { get; set; }

        public void LockUpdate(bool value)
        {
            UpdateLocked = value;
        }

        private void Init()
        {
            NeedMeasure = true;
            IsLayoutDirty = true;
            HorizontalOptions = LayoutOptions.Start;
            VerticalOptions = LayoutOptions.Start;
            Padding = new Thickness(0);

            SizeChanged += ViewSizeChanged;

            CalculateMargins();
            CalculateSizeRequest();
        }

        public SkiaControl()
        {
            Init();
        }


        public new static readonly BindableProperty ParentProperty = BindableProperty.Create(nameof(Parent),
        typeof(IDrawnBase),
        typeof(SkiaControl),
        null, propertyChanged: OnControlParentChanged);
        /// <summary>
        /// Do not set this directly if you don't know what you are doing, use SetParent()
        /// </summary>
        public new IDrawnBase Parent
        {
            get { return (IDrawnBase)GetValue(ParentProperty); }
            set { SetValue(ParentProperty, value); }
        }


        #region View


        public static readonly BindableProperty StyleProperty = BindableProperty.Create(nameof(Style),
        typeof(Style),
        typeof(SkiaControl),
        null,
        propertyChanged: StylePropertyChanged);

        protected Style _lastStyle;

        private static void StylePropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl view)
            {
                view.ApplyStyles();
            };
        }

        public Style Style
        {
            get { return (Style)GetValue(StyleProperty); }
            set { SetValue(StyleProperty, value); }
        }


        public static readonly BindableProperty VerticalOptionsProperty = BindableProperty.Create(nameof(VerticalOptions),
        typeof(LayoutOptions),
        typeof(SkiaControl),
        LayoutOptions.Start,
        propertyChanged: NeedInvalidateMeasure);
        public LayoutOptions VerticalOptions
        {
            get { return (LayoutOptions)GetValue(VerticalOptionsProperty); }
            set { SetValue(VerticalOptionsProperty, value); }
        }

        public static readonly BindableProperty HorizontalOptionsProperty = BindableProperty.Create(nameof(HorizontalOptions),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);
        public LayoutOptions HorizontalOptions
        {
            get { return (LayoutOptions)GetValue(HorizontalOptionsProperty); }
            set { SetValue(HorizontalOptionsProperty, value); }
        }


        public static readonly BindableProperty IsVisibleProperty = BindableProperty.Create(nameof(IsVisible),
        typeof(bool),
        typeof(SkiaControl),
        true,
        propertyChanged: VisibilityChanged);

        private static void VisibilityChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.OnVisibilityChanged((bool)newvalue);
            }
            NeedInvalidateMeasure(bindable, oldvalue, newvalue);
        }

        /// <summary>
        /// todo override for templated skialayout
        /// </summary>
        /// <param name="newvalue"></param>
        public virtual void OnVisibilityChanged(bool newvalue)
        {
            //need to this mainly to disable child gesture listeners..
            try
            {
                foreach (var child in Views)
                {
                    child.OnParentVisibilityChanged(newvalue);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        protected virtual void OnParentVisibilityChanged(bool newvalue)
        {

        }

        /// <summary>
        /// Use WillDraw when deciding whether to draw control
        /// </summary>
        public bool IsVisible
        {
            get
            {
                return (bool)GetValue(IsVisibleProperty);
            }
            set
            {
                SetValue(IsVisibleProperty, value);
                IsVisibleChanged();
            }
        }

        /// <summary>
        /// Since this is a critical property for a control to go inactive/active we can easily react to this change to stop/enable animators etc.
        /// </summary>
        protected virtual void IsVisibleChanged()
        {

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
            get
            {
                return (bool)GetValue(IgnoreChildrenInvalidationsProperty);
            }
            set
            {
                SetValue(IgnoreChildrenInvalidationsProperty, value);
            }
        }

        public static readonly BindableProperty ClearColorProperty = BindableProperty.Create(nameof(ClearColor), typeof(Color), typeof(SkiaControl),
            Colors.Transparent,
            propertyChanged: NeedDraw);
        public Color ClearColor
        {
            get { return (Color)GetValue(ClearColorProperty); }
            set { SetValue(ClearColorProperty, value); }
        }

        public static readonly BindableProperty BackgroundColorProperty = BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(SkiaControl),
            Colors.Transparent,
            propertyChanged: NeedDraw);
        public Color BackgroundColor
        {
            get { return (Color)GetValue(BackgroundColorProperty); }
            set { SetValue(BackgroundColorProperty, value); }
        }

        public static readonly BindableProperty UsePixelSnappingProperty = BindableProperty.Create(
            nameof(UsePixelSnapping),
            typeof(bool),
            typeof(SkiaControl),
            false, propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Whether we should round up transformed position, default is False. Will be used for TranslationX TranslationY, for other cases controls must implement their logic, for example SkiaScroll uses it to recalculate the ordered content offset.
        /// </summary>
        public bool UsePixelSnapping
        {
            get { return (bool)GetValue(UsePixelSnappingProperty); }
            set { SetValue(UsePixelSnappingProperty, value); }
        }


        #region FillGradient

        public static readonly BindableProperty FillGradientProperty = BindableProperty.Create(nameof(FillGradient),
            typeof(SkiaGradient), typeof(SkiaControl),
            null,
            propertyChanged: FillGradientPropertyChanged);
        public SkiaGradient FillGradient
        {
            get { return (SkiaGradient)GetValue(FillGradientProperty); }
            set { SetValue(FillGradientProperty, value); }
        }

        private static void FillGradientPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
            {
                if (oldvalue is SkiaGradient skiaGradientOld)
                {
                    skiaGradientOld.Parent = null;
                    skiaGradientOld.BindingContext = null;
                }

                if (newvalue is SkiaGradient skiaGradient)
                {
                    skiaGradient.Parent = skiaControl;
                    skiaGradient.BindingContext = skiaControl.BindingContext;
                }

                skiaControl.Update();
            }

        }

        public bool HasFillGradient
        {
            get
            {
                return this.FillGradient != null && this.FillGradient.Type != GradientType.None;
            }
        }


        #endregion

        public SKSize GetSizeRequest(float widthConstraint, float heightConstraint, bool insideLayout)
        {
            if (insideLayout)
            {

            }

            if (LockRatio > 0)
            {
                var lockValue = (float)Math.Max(widthConstraint, heightConstraint);
                return new SKSize(lockValue, lockValue);
            }

            if (LockRatio < 0)
            {
                var lockValue = (float)Math.Min(widthConstraint, heightConstraint);
                return new SKSize(lockValue, lockValue);
            }

            return new SKSize(widthConstraint, heightConstraint);
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

        public static readonly BindableProperty MinimumHeightRequestProperty = BindableProperty.Create(nameof(MinimumHeightRequest),
            typeof(double), typeof(SkiaControl),
            -1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double MinimumHeightRequest
        {
            get { return (double)GetValue(MinimumHeightRequestProperty); }
            set { SetValue(MinimumHeightRequestProperty, value); }
        }

        public static readonly BindableProperty MaximumHeightRequestProperty = BindableProperty.Create(nameof(MaximumHeightRequest),
            typeof(double), typeof(SkiaControl),
            -1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double MaximumHeightRequest
        {
            get { return (double)GetValue(MaximumHeightRequestProperty); }
            set { SetValue(MaximumHeightRequestProperty, value); }
        }

        public static readonly BindableProperty MinimumWidthRequestProperty = BindableProperty.Create(nameof(MinimumWidthRequest),
            typeof(double), typeof(SkiaControl),
            -1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double MinimumWidthRequest
        {
            get { return (double)GetValue(MinimumWidthRequestProperty); }
            set { SetValue(MinimumWidthRequestProperty, value); }
        }

        public static readonly BindableProperty MaximumWidthRequestProperty = BindableProperty.Create(nameof(MaximumWidthRequest),
            typeof(double), typeof(SkiaControl),
            -1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double MaximumWidthRequest
        {
            get { return (double)GetValue(MaximumWidthRequestProperty); }
            set { SetValue(MaximumWidthRequestProperty, value); }
        }

        public static readonly BindableProperty WidthRequestProperty = BindableProperty.Create(nameof(WidthRequest),
            typeof(double), typeof(SkiaControl),
            -1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double WidthRequest
        {
            get { return (double)GetValue(WidthRequestProperty); }
            set { SetValue(WidthRequestProperty, value); }
        }

        public static readonly BindableProperty HeightRequestProperty = BindableProperty.Create(nameof(HeightRequest),
            typeof(double), typeof(SkiaControl),
            -1.0,
            propertyChanged: NeedInvalidateMeasure);
        public double HeightRequest
        {
            get { return (double)GetValue(HeightRequestProperty); }
            set { SetValue(HeightRequestProperty, value); }
        }

        private double _Height = -1;
        public double Height
        {
            get
            {
                return _Height;
            }
            set
            {
                if (_Height != value)
                {
                    _Height = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Width = -1;
        public double Width
        {
            get
            {
                return _Width;
            }
            set
            {
                if (_Width != value)
                {
                    _Width = value;
                    OnPropertyChanged();
                }
            }
        }

        public static readonly BindableProperty LockRatioProperty = BindableProperty.Create(nameof(LockRatio),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedInvalidateMeasure);
        /// <summary>
        /// Locks the final size to the min (-1.0 <-> 0.0) or max (0.0 <-> 1.0) of the provided size.
        /// </summary>
        public double LockRatio
        {
            get { return (double)GetValue(LockRatioProperty); }
            set { SetValue(LockRatioProperty, value); }
        }

        public static readonly BindableProperty HorizontalFillRatioProperty = BindableProperty.Create(
            nameof(HorizontalFillRatio),
            typeof(double),
            typeof(SkiaControl),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

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

        public double VerticalFillRatio
        {
            get { return (double)GetValue(VerticalFillRatioProperty); }
            set { SetValue(VerticalFillRatioProperty, value); }
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

        public static readonly BindableProperty TriggersProperty = BindableProperty.Create(nameof(Triggers),
            typeof(IList<TriggerBase>), typeof(SkiaControl),
            null);
        public IList<TriggerBase> Triggers
        {
            get { return (IList<TriggerBase>)GetValue(TriggersProperty); }
            set { SetValue(TriggersProperty, value); }
        }


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

        public static readonly BindableProperty TranslationXProperty = BindableProperty.Create(nameof(TranslationX),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedRepaint);
        public double TranslationX
        {
            get { return (double)GetValue(TranslationXProperty); }
            set
            {
                SetValue(TranslationXProperty, value);
            }
        }

        public static readonly BindableProperty TranslationYProperty = BindableProperty.Create(nameof(TranslationY),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedRepaint);
        public double TranslationY
        {
            get { return (double)GetValue(TranslationYProperty); }
            set { SetValue(TranslationYProperty, value); }
        }

        public static readonly BindableProperty SkewXProperty
        = BindableProperty.Create(nameof(SkewX),
        typeof(float), typeof(SkiaControl),
        0.0f, propertyChanged: NeedRepaint);

        public float SkewX
        {
            get
            {
                return (float)GetValue(SkewXProperty);
            }
            set
            {
                SetValue(SkewXProperty, value);
            }
        }

        public static readonly BindableProperty SkewYProperty
        = BindableProperty.Create(nameof(SkewY),
        typeof(float), typeof(SkiaControl),
        0.0f, propertyChanged: NeedRepaint);

        public float SkewY
        {
            get
            {
                return (float)GetValue(SkewYProperty);
            }
            set
            {
                SetValue(SkewYProperty, value);
            }
        }

        public static readonly BindableProperty ScaleXProperty
        = BindableProperty.Create(nameof(ScaleX),
        typeof(double), typeof(SkiaControl),
        1.0, propertyChanged: NeedRepaint);

        public double ScaleX
        {
            get
            {
                return (double)GetValue(ScaleXProperty);
            }
            set
            {
                SetValue(ScaleXProperty, value);
            }
        }

        public static readonly BindableProperty ScaleYProperty
        = BindableProperty.Create(nameof(ScaleY),
        typeof(double), typeof(SkiaControl),
        1.0, propertyChanged: NeedRepaint);

        public double ScaleY
        {
            get
            {
                return (double)GetValue(ScaleYProperty);
            }
            set
            {
                SetValue(ScaleYProperty, value);
            }
        }

        public static readonly BindableProperty CameraAngleXProperty
            = BindableProperty.Create(nameof(CameraAngleX),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float CameraAngleX
        {
            get
            {
                return (float)GetValue(CameraAngleXProperty);
            }
            set
            {
                SetValue(CameraAngleXProperty, value);
            }
        }

        public static readonly BindableProperty CameraAngleYProperty
            = BindableProperty.Create(nameof(CameraAngleY),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float CameraAngleY
        {
            get
            {
                return (float)GetValue(CameraAngleYProperty);
            }
            set
            {
                SetValue(CameraAngleYProperty, value);
            }
        }

        public static readonly BindableProperty CameraAngleZProperty
            = BindableProperty.Create(nameof(CameraAngleZ),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float CameraAngleZ
        {
            get
            {
                return (float)GetValue(CameraAngleZProperty);
            }
            set
            {
                SetValue(CameraAngleZProperty, value);
            }
        }

        public static readonly BindableProperty CameraTranslationZProperty
            = BindableProperty.Create(nameof(CameraTranslationZ),
                typeof(float), typeof(SkiaControl),
                0.0f, propertyChanged: NeedRepaint);

        public float CameraTranslationZ
        {
            get
            {
                return (float)GetValue(CameraTranslationZProperty);
            }
            set
            {
                SetValue(CameraTranslationZProperty, value);
            }
        }


        public static readonly BindableProperty RotationProperty
        = BindableProperty.Create(nameof(Rotation),
        typeof(float), typeof(SkiaControl),
        0.0f, propertyChanged: NeedRepaint);

        public float Rotation
        {
            get
            {
                return (float)GetValue(RotationProperty);
            }
            set
            {
                SetValue(RotationProperty, value);
            }
        }

        public static readonly BindableProperty Perspective1Property
        = BindableProperty.Create(nameof(Perspective1),
        typeof(float), typeof(SkiaControl),
       0.0f, propertyChanged: NeedRepaint);

        public float Perspective1
        {
            get
            {
                return (float)GetValue(Perspective1Property);
            }
            set
            {
                SetValue(Perspective1Property, value);
            }
        }

        public static readonly BindableProperty Perspective2Property
        = BindableProperty.Create(nameof(Perspective2),
        typeof(float), typeof(SkiaControl),
          0.0f, propertyChanged: NeedRepaint);

        public float Perspective2
        {
            get
            {
                return (float)GetValue(Perspective2Property);
            }
            set
            {
                SetValue(Perspective2Property, value);
            }
        }

        public static readonly BindableProperty TransformPivotPointXProperty
        = BindableProperty.Create(nameof(TransformPivotPointX),
        typeof(double), typeof(SkiaControl),
        0.5, propertyChanged: NeedRepaint);
        public double TransformPivotPointX
        {
            get
            {
                return (double)GetValue(TransformPivotPointXProperty);
            }
            set
            {
                SetValue(TransformPivotPointXProperty, value);
            }
        }

        public static readonly BindableProperty TransformPivotPointYProperty
        = BindableProperty.Create(nameof(TransformPivotPointY),
        typeof(double), typeof(SkiaControl),
        0.5, propertyChanged: NeedRepaint);

        public double TransformPivotPointY
        {
            get
            {
                return (double)GetValue(TransformPivotPointYProperty);
            }
            set
            {
                SetValue(TransformPivotPointYProperty, value);
            }
        }



        private static void OnControlParentChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.OnParentChanged(newvalue as IDrawnBase, oldvalue as IDrawnBase);
            }
        }

        public virtual void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
        {
            if (newvalue != null)
                Superview = GetTopParentView();

            ParentChanged?.Invoke(this, Parent);
        }

        public new event EventHandler<IDrawnBase> ParentChanged;

        public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness),
            typeof(SkiaControl), Thickness.Zero,
            propertyChanged: NeedInvalidateMeasure);
        public Thickness Padding
        {
            get { return (Thickness)GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }

        public static readonly BindableProperty MarginProperty = BindableProperty.Create(nameof(Margin), typeof(Thickness),
            typeof(SkiaControl), Thickness.Zero,
            propertyChanged: NeedInvalidateMeasure);
        public Thickness Margin
        {
            get { return (Thickness)GetValue(MarginProperty); }
            set { SetValue(MarginProperty, value); }
        }

        public static readonly BindableProperty MarginTopProperty = BindableProperty.Create(
            nameof(MarginTop),
            typeof(double),
            typeof(SkiaControl),
            -1.0, propertyChanged: NeedInvalidateMeasure);

        public double MarginTop
        {
            get { return (double)GetValue(MarginTopProperty); }
            set { SetValue(MarginTopProperty, value); }
        }

        public static readonly BindableProperty MarginBottomProperty = BindableProperty.Create(
            nameof(MarginBottom),
            typeof(double),
            typeof(SkiaControl),
            -1.0, propertyChanged: NeedInvalidateMeasure);

        public double MarginBottom
        {
            get { return (double)GetValue(MarginBottomProperty); }
            set { SetValue(MarginBottomProperty, value); }
        }

        public static readonly BindableProperty MarginLeftProperty = BindableProperty.Create(
            nameof(MarginLeft),
            typeof(double),
            typeof(SkiaControl),
            -1.0, propertyChanged: NeedInvalidateMeasure);

        public double MarginLeft
        {
            get { return (double)GetValue(MarginLeftProperty); }
            set { SetValue(MarginLeftProperty, value); }
        }

        public static readonly BindableProperty MarginRightProperty = BindableProperty.Create(
            nameof(MarginRight),
            typeof(double),
            typeof(SkiaControl),
            -1.0, propertyChanged: NeedInvalidateMeasure);

        public double MarginRight
        {
            get { return (double)GetValue(MarginRightProperty); }
            set { SetValue(MarginRightProperty, value); }
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

        public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double),
            typeof(SkiaControl),
            8.0,
            propertyChanged: NeedInvalidateMeasure);
        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        /*
        public static readonly BindableProperty AlignContentVerticalProperty = BindableProperty.Create(nameof(AlignContentVertical),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);
        public LayoutOptions AlignContentVertical
        {
            get { return (LayoutOptions)GetValue(AlignContentVerticalProperty); }
            set { SetValue(AlignContentVerticalProperty, value); }
        }

        public static readonly BindableProperty AlignContentHorizontalProperty = BindableProperty.Create(nameof(AlignContentHorizontal),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start,
            propertyChanged: NeedInvalidateMeasure);
        public LayoutOptions AlignContentHorizontal
        {
            get { return (LayoutOptions)GetValue(AlignContentHorizontalProperty); }
            set { SetValue(AlignContentHorizontalProperty, value); }
        }
        */

        //-------------------------------------------------------------
        // IsClippedToBounds
        //-------------------------------------------------------------
        private const string nameIsClippedToBounds = "IsClippedToBounds";
        public static readonly BindableProperty IsClippedToBoundsProperty = BindableProperty.Create(nameIsClippedToBounds,
            typeof(bool), typeof(SkiaControl), false,
            propertyChanged: NeedInvalidateMeasure);
        /// <summary>
        /// This cuts shadows etc. You might want to enable it for some cases as it speeds up the rendering, it is False by default
        /// </summary>
        public virtual bool IsClippedToBounds
        {
            get { return (bool)GetValue(IsClippedToBoundsProperty); }
            set { SetValue(IsClippedToBoundsProperty, value); }
        }

        //-------------------------------------------------------------
        // ClipEffects
        //-------------------------------------------------------------
        private const string nameClipEffects = "ClipEffects";
        public static readonly BindableProperty ClipEffectsProperty = BindableProperty.Create(nameClipEffects,
            typeof(bool), typeof(SkiaControl), true,
            propertyChanged: NeedInvalidateMeasure);
        /// <summary>
        /// This cuts shadows etc
        /// </summary>
        public virtual bool ClipEffects
        {
            get { return (bool)GetValue(ClipEffectsProperty); }
            set { SetValue(ClipEffectsProperty, value); }
        }

        //-------------------------------------------------------------
        // BindableTrigger
        //-------------------------------------------------------------
        public static readonly BindableProperty BindableTriggerProperty = BindableProperty.Create(nameof(BindableTrigger),
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

        //-------------------------------------------------------------
        // Value1
        //-------------------------------------------------------------
        public static readonly BindableProperty Value1Property = BindableProperty.Create(nameof(Value1),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);
        public double Value1
        {
            get { return (double)GetValue(Value1Property); }
            set { SetValue(Value1Property, value); }
        }

        //-------------------------------------------------------------
        // Value2
        //-------------------------------------------------------------
        public static readonly BindableProperty Value2Property = BindableProperty.Create(nameof(Value2),
            typeof(double), typeof(SkiaControl),
            0.0,
            propertyChanged: NeedDraw);
        public double Value2
        {
            get { return (double)GetValue(Value2Property); }
            set { SetValue(Value2Property, value); }
        }

        //-------------------------------------------------------------
        // Value3
        //-------------------------------------------------------------
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


        //-------------------------------------------------------------
        // RenderingScale
        //-------------------------------------------------------------
        private const string nameRenderingScale = "RenderingScale";

        public static readonly BindableProperty RenderingScaleProperty = BindableProperty.Create(nameRenderingScale,
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
                var value = (float)GetValue(RenderingScaleProperty);
                if (value <= 0)
                {
                    return GetDensity();
                }
                return value;
            }
            set
            {
                SetValue(RenderingScaleProperty, value);
            }
        }

        public static float GetDensity()
        {
            return (float)Super.Screen.Density;
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


        public event EventHandler SizeChanged;

        #endregion

        public void BatchBegin()
        {

        }

        public void BatchCommit()
        {

        }

        //we store props that was set manually so we exclude them when applying Style
        private ConcurrentDictionary<string, bool> ExplicitPropertiesSet { get; } = new();

        public void ApplyStyles()
        {
            var state = UpdateLocked;
            UpdateLocked = true;
            ApplyStyle(Style);
            ApplyStates(States);
            UpdateLocked = state;
            Invalidate();
            Update();
        }

        public void ApplyStates(IEnumerable<string> states)
        {
            try
            {
                if (Styles?.Count > 0)
                {
                    var state = UpdateLocked;
                    UpdateLocked = true;

                    bool wasSet = false;
                    Style normal = null;

                    var hasBoolean = Styles.FirstOrDefault(x => x.Condition);
                    if (hasBoolean != null)
                    {
                        wasSet = true;
                        ApplyStyle(hasBoolean.Style);
                    }
                    else
                        foreach (var style in Styles.ToList())
                        {
                            style.SetParent(this);

                            if (style.State == "Normal")
                            {
                                normal = style.Style;
                            }

                            if (states != null && !wasSet && states.Any() && states.Contains(style.State))
                            {
                                wasSet = true;
                                ApplyStyle(style.Style);
                            }

                        }

                    if (!wasSet && normal != null)
                    {
                        ApplyStyle(normal);
                    }

                    UpdateLocked = state;
                    Update();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }



        public SKRect RenderedAtDestination { get; set; }

        public virtual void OnScaleChanged()
        {
            InvalidateMeasure();
        }

        public virtual bool ShouldInvalidateByChildren
        {
            get
            {
                return NeedAutoSize;
            }
        }


        public void InvalidateParents()
        {
            if (Parent != null)
            {
                if (Parent.ShouldInvalidateByChildren)
                    Parent.Invalidate();
                Parent.InvalidateParents();
            }
        }

        public virtual void InvalidateParent()
        {
            if (Parent != null)
            {
                if (Parent is SkiaControl skia)
                {
                    if (skia.IgnoreChildrenInvalidations)
                    {
                        return;
                    }
                }

                if (Parent.ShouldInvalidateByChildren)
                {
                    Parent.InvalidateByChild(this);
                }

            }
        }



        public virtual void InvalidateByChild(SkiaControl child)
        {
            Invalidate();
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

        #region STYLES
        public static void InvalidateStylesCache()
        {
            lock (lockOptimizeStyle)
            {
                OptimizedStyles.Clear();

            }
        }

        public static ConcurrentDictionary<string, List<Setter>> OptimizedStyles = new();

        public IEnumerable<Setter> GetOptimizedSetters(Style style)
        {
            return OptimizeStyle(style);
        }

        static object lockOptimizeStyle = new();

        /// <summary>
        /// Remove overriden properties from base styles,
        /// so they do not get set more than once
        /// </summary>
        IEnumerable<Setter> OptimizeStyle(Style style, List<Setter> optimized = null)
        {
            lock (lockOptimizeStyle)
            {
                bool canCache = false;
                if (optimized == null)
                {
                    if (string.IsNullOrEmpty(style.Class))
                    {
                        var message = "Class property not set for Drawn control style. It is required for speed optimizations";
                        Console.WriteLine($"[SkiaControl] Error: {message}");
                        throw new Exception(message);
                    }

                    if (OptimizedStyles.ContainsKey(style.Class))
                    {
                        return OptimizedStyles[style.Class];
                    }

                    canCache = !string.IsNullOrEmpty(style.Class);
                    optimized = new(style.Setters);
                }

                if (style.BasedOn != null)
                {
                    OptimizeStyle(style.BasedOn, optimized);
                }

                var keys = optimized.Select(x => x.Property.PropertyName).ToArray();
                optimized.AddRange(style.Setters.Where(x => !keys.Contains(x.Property.PropertyName)));

                if (canCache)
                {
                    OptimizedStyles[style.Class] = optimized;
                }

                return optimized;
            }
        }

        public virtual void ApplyStyle(Style style)
        {
            if (style == null)
            {
                return;
            }

            //if (style.TargetType != this.GetType())
            //{
            //    throw new ApplicationException($"Style {style.Class} for {this.GetType()} [{this.Tag}] has incorrect target type!");
            //}

            IEnumerable<Setter> setters = GetOptimizedSetters(style);

            foreach (Setter setter in setters)
            {
                if (!ExplicitPropertiesSet.ContainsKey(setter.Property.PropertyName))
                {
                    isApplyingStyle = true;
                    SetPropertyValue(setter.Property, setter.Value);
                    isApplyingStyle = false;
                }
            }
        }

        // A flag to indicate whether a style is being applied
        protected volatile bool isApplyingStyle;

        public T GetStyleValue<T>(Style style, BindableProperty property, IEnumerable<Setter> styleSetters = null)
        {
            if (styleSetters == null)
                styleSetters = GetOptimizedSetters(style);
            var setter = styleSetters.FirstOrDefault(p => p.Property == property);
            if (setter != null)
            {
                return (T)setter.Value;
            }
            return default;
        }

        public IEnumerable<Setter> ApplyStyleProperty(Style style, BindableProperty property, IEnumerable<Setter> styleSetters = null)
        {
            if (styleSetters == null)
                styleSetters = GetOptimizedSetters(style);
            var setter = styleSetters.FirstOrDefault(p => p.Property == property);
            if (setter != null)
                SetPropertyValue(setter.Property, setter.Value);
            return styleSetters;
        }

        #endregion

        public string Uid { get; set; } = Guid.NewGuid().ToString();

        //public Action<SKPath, SKRect> ClipAsCircle => (path, dest) =>
        //{
        //    path.AddRoundRect(dest, (float)(2 * RenderingScale), (float)(2 * RenderingScale),
        //        SKPathDirection.Clockwise);
        //};

        //todo check adapt for MAUI

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
            };



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


        //public virtual void OnDensityChanged()
        //{
        //	InvalidateMeasure();
        //}


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
        //            Debug.WriteLine("SkiaControl disposed!");
        //        }
        //        else
        //        {
        //            IsDisposed = false;
        //        }
        //    }
        //}

        #endregion


        private void ViewSizeChanged(object sender, EventArgs e)
        {
            OnSizeChanged();
        }

        protected virtual void OnSizeChanged()
        {
            Update();
        }



        public Action<SKPath, SKRect> Clipping { get; set; }

        //public Action<SKPath, SKRect> ClipChildren { get; set; }


        public string Tag { get; set; }

        public bool IsRootView()
        {
            return this.Parent == null;
        }

        /// <summary>
        ///  destination in PIXELS, requests in UNITS
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="widthRequest"></param>
        /// <param name="heightRequest"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public ScaledSize DefineAvailableSize(SKRect destination,
            float widthRequest, float heightRequest, float scale)
        {
            if (Tag == "Test")
            {
                var stop = 1;
            }

            var rectWidth = destination.Width;
            var wants = widthRequest * scale;
            if (wants > 0 && wants < rectWidth)
                rectWidth = (int)wants;

            var rectHeight = destination.Height;
            wants = heightRequest * scale;
            if (wants > 0 && wants < rectHeight)
                rectHeight = (int)wants;

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

        //-------------------------------------------------------------
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
            if (widthRequest == 0 || heightRequest == 0)
            {
                return new SKRect(0, 0, 0, 0);
            }

            var rectAvailable = DefineAvailableSize(destination, widthRequest, heightRequest, scale);

            var useMaxWidth = rectAvailable.Pixels.Width;
            var useMaxHeight = rectAvailable.Pixels.Height;
            var availableWidth = destination.Width;
            var availableHeight = destination.Height;

            var layoutHorizontal = new LayoutOptions(HorizontalOptions.Alignment, HorizontalOptions.Expands);
            var layoutVertical = new LayoutOptions(VerticalOptions.Alignment, HorizontalOptions.Expands);

            // initial fill
            var left = destination.Left;
            var top = destination.Top;
            var right = 0f;// left + rectAvailable.Pixels.Width;
            var bottom = 0f;// top + rectAvailable.Pixels.Height;

            // layoutHorizontal
            switch (layoutHorizontal.Alignment)
            {

            case LayoutAlignment.Center when float.IsFinite(availableWidth):
            {
                left += (float)Math.Round(availableWidth / 2.0f) - (float)Math.Round(useMaxWidth / 2.0f);
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

                break;
            }
            case LayoutAlignment.End when float.IsFinite(destination.Right):
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

            case LayoutAlignment.Center when float.IsFinite(availableHeight):
            {
                top += (float)Math.Round(availableHeight / 2.0f) - (float)Math.Round(useMaxHeight / 2.0f);
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

                break;
            }
            case LayoutAlignment.End when float.IsFinite(destination.Bottom):
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

            return new SKRect(left, top, right, bottom);
        }

        /*
        public SKRect CalculateLayout(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (widthRequest == 0 || heightRequest == 0)
            {
                return new SKRect(0, 0, 0, 0);
            }

            var rectAvailable = DefineAvailableSize(destination, widthRequest, heightRequest, scale);

            var availableWidth = destination.Width;
            var availableHeight = destination.Height;

            var layoutHorizontal = new LayoutOptions(HorizontalOptions.Alignment, HorizontalOptions.Expands);
            var layoutVertical = new LayoutOptions(VerticalOptions.Alignment, HorizontalOptions.Expands);


            //initial fill
            var left = destination.Left;
            var top = destination.Top;
            var right = left + rectAvailable.Pixels.Width;
            var bottom = top + rectAvailable.Pixels.Height;

            //layoutHorizontal
            if (layoutHorizontal.Alignment == LayoutAlignment.Center && float.IsFinite(availableWidth))
            {
                //center
                left += (availableWidth - rectAvailable.Pixels.Width) / 2.0f;
                right = left + rectAvailable.Pixels.Width;

                if (left < destination.Left)
                {
                    left = (float)(destination.Left);
                    right = left + rectAvailable.Pixels.Width;
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
                left = right - rectAvailable.Pixels.Width;
                if (left < destination.Left)
                {
                    left = (float)(destination.Left);
                }
            }
            else
            {
                //start or fill
                right = left + rectAvailable.Pixels.Width;
                if (right > destination.Right)
                {
                    right = (float)(destination.Right);
                }


            }

            //VerticalOptions
            if (layoutVertical.Alignment == LayoutAlignment.Center)
            {
                //center
                top += availableHeight / 2.0f - rectAvailable.Pixels.Height / 2.0f;
                bottom = top + rectAvailable.Pixels.Height;
                if (top < destination.Top)
                {
                    top = (float)(destination.Top);
                    bottom = top + rectAvailable.Pixels.Height;
                }
                else
                if (bottom > destination.Bottom)
                {
                    bottom = (float)(destination.Bottom);
                    top = bottom - rectAvailable.Pixels.Height;
                }
            }
            else
            if (layoutVertical.Alignment == LayoutAlignment.End && double.IsFinite(destination.Bottom))
            {
                //end
                bottom = destination.Bottom;
                top = bottom - rectAvailable.Pixels.Height;
                if (top < destination.Top)
                {
                    top = (float)(destination.Top);
                }

            }
            else
            {
                //start or fill
                bottom = top + rectAvailable.Pixels.Height;
                if (bottom > destination.Bottom)
                {
                    bottom = (float)(destination.Bottom);
                }

            }

            var ret = new SKRect((float)left, (float)top, (float)right, (float)bottom);

            //Debug.WriteLine($"[Layout] '{Tag}' {ret.Left - destination.Left:0.0}-{destination.Right - ret.Right:0.0} ");

            return ret;
        }
        */


        private ScaledSize _contentSize = new();
        public ScaledSize ContentSize
        {
            get
            {
                return _contentSize;
            }
            protected set
            {
                if (_contentSize != value)
                {
                    _contentSize = value;
                    OnPropertyChanged();
                }
            }
        }


        protected bool WasMeasured;



        protected virtual void OnDrawingSizeChanged()
        {

        }



        protected virtual void AdaptCachedLayout(SKRect destination, float scale)
        {
            //adapt cache to current request
            var newDestination = ArrangedDestination;//.Clone();
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
        /// This is the destination in PIXELS with margins applied, using this to paint background
        /// </summary>
        public SKRect DrawingRect { get; set; }

        /// <summary>
        /// ISkiaGestureListener impl
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool HitIsInside(float x, float y)
        {
            var hitbox = HitBoxAuto;

            //if (UseCache != SkiaCacheType.None && RenderObject != null)
            //{
            //    var offsetCacheX = Math.Abs(DrawingRect.Left - RenderObject.Bounds.Left);
            //    var offsetCacheY = Math.Abs(DrawingRect.Top - RenderObject.Bounds.Top);

            //    hitbox.Offset(offsetCacheX,offsetCacheY);
            //}

            return hitbox.Contains(x, y); ;
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

        public virtual bool IsGestureForChild(ISkiaGestureListener listener, float x, float y)
        {
            var hit = listener.HitIsInside(x, y);
            return hit;
        }

        public SKRect ApplyTransforms(SKRect rect)
        {
            //Debug.WriteLine($"[Transforming] {rect}");

            return new SKRect(rect.Left + (float)(TranslationX * RenderingScale),
                rect.Top + (float)(TranslationY * RenderingScale),
                    rect.Right + (float)(TranslationX * RenderingScale),
                rect.Bottom + (float)(TranslationY * RenderingScale));
        }

        /// <summary>
        /// Use this to consume gestures in your control only,
        /// do not use result for passing gestures below
        /// </summary>
        /// <param name="childOffset"></param>
        /// <returns></returns>
        public virtual SKPoint TranslateInputCoords(SKPoint childOffset, bool accountForCache = true)
        {
            var thisOffset = new SKPoint(-(float)(TranslationX * RenderingScale), -(float)(TranslationY * RenderingScale));

            bool frozen = false;

            //inside a cached object coordinates are frozen at the moment the snapshot was taken
            //so we must offset the coordinates to match the current drawing rect
            if (accountForCache && UseCache != SkiaCacheType.None)
            {
                if (RenderObject != null)
                {
                    thisOffset.Offset(RenderObject.TranslateInputCoords(DrawingRect));
                }
                else
                if (UsingCacheDoubleBuffering && OffscreenRenderObject != null)
                {
                    thisOffset.Offset(OffscreenRenderObject.TranslateInputCoords(DrawingRect));
                }
            }

            thisOffset.Offset(childOffset);

            return thisOffset;
        }


        protected bool UsingCacheDoubleBuffering
        {
            get
            {
                return UseCacheDoubleBuffering && UseCache != SkiaCacheType.None;
            }
        }

        long _layoutChanged = 0;

        public SKRect ArrangedDestination { get; protected set; }
        //protected SKRect ArrangedDrawingRect { get; set; }

        protected virtual void OnLayoutChanged()
        {
            LayoutReady = this.Height > 0 && this.Width > 0;
        }

        public Action<SkiaControl> WhenLayoutIsReady;
        public Action<SkiaControl> WhenDisposing;

        protected virtual void OnLayoutReady()
        {

        }

        public bool LayoutReady
        {
            get
            {
                return _layoutReady;
            }

            protected set
            {
                if (_layoutReady != value)
                {
                    _layoutReady = value;
                    OnPropertyChanged();
                    if (value)
                    {
                        WhenLayoutIsReady?.Invoke(this);
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
        ///  destination in PIXELS, requests in UNITS. resulting Destination prop will be filed in PIXELS.
        /// </summary>
        /// <param name="destination">PIXELS</param>
        /// <param name="widthRequest">UNITS</param>
        /// <param name="heightRequest">UNITS</param>
        /// <param name="scale"></param>
        public virtual void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (!CanDraw)
            {
                DrawingRect = SKRect.Empty;
                return;
            }

            SKRect arrangingFor = new(0, 0, destination.Width, destination.Height);

            if (!IsLayoutDirty &&
                (ViewportHeightLimit != _arrangedViewportHeightLimit ||
                 ViewportWidthLimit != _arrangedViewportWidthLimit ||
                 !CompareRects(arrangingFor, _lastArrangedFor, 1) ||
                 !AreEqual(_lastArrangedHeight, heightRequest, 1) ||
                 !AreEqual(_lastArrangedWidth, widthRequest, 1)))
            {
                IsLayoutDirty = true;
            }

            if (!IsLayoutDirty)
            {
                AdaptCachedLayout(destination, scale);
                return;
            }

            if (Tag == "Test")
            {
                var stop = 1;
            }

            //var oldDestination = Destination;
            var layout = CalculateLayout(arrangingFor, widthRequest, heightRequest, scale);

            var oldDrawingRect = this.DrawingRect;

            //save to cache
            ArrangedDestination = layout;
            //ArrangedDrawingRect = GetDrawingRectWithMargins(layout, scale);

            AdaptCachedLayout(destination, scale);

            _arrangedViewportHeightLimit = ViewportHeightLimit;
            _arrangedViewportWidthLimit = ViewportWidthLimit;
            _lastArrangedFor = arrangingFor;
            _lastArrangedHeight = heightRequest;
            _lastArrangedWidth = widthRequest;

            OnLayoutChanged();

            if (!AreEqual(oldDrawingRect.Height, DrawingRect.Height, 1)
                || !AreEqual(oldDrawingRect.Width, DrawingRect.Width, 1))
            {
                OnDrawingSizeChanged();
            }

            //if (!CompareRects(oldDestination, Destination)) //layout can be same but offset might have been changed
            //{
            //    OnLayoutChanged();
            //}

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
                return ScaledSize.Empty;
            }

            child.OnBeforeMeasure(); //could set IsVisible or whatever inside

            if (!child.CanDraw)
                return ScaledSize.Empty; //child set himself invisible

            return child.Measure((float)availableWidth, (float)availableHeight, scale);
        }


        protected virtual ScaledSize MeasureContent(
            IEnumerable<SkiaControl> children,
            SKRect rectForChildrenPixels,
            float scale)
        {
            var maxHeight = 0.0f;
            var maxWidth = 0.0f;

            //PASS 1
            foreach (var child in children)
            {
                child.OnBeforeMeasure(); //could set IsVisible or whatever inside

                //if (autosize &&
                //    (child.HorizontalOptions.Alignment == LayoutAlignment.Fill
                //     || child.VerticalOptions.Alignment == LayoutAlignment.Fill))
                //{
                //    fill.Add(child); //todo not very correct for the case just 1 dimension is Fill and other one may by bigger that other children!
                //    continue;
                //}

                var measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
                if (measured != ScaledSize.Empty)
                {
                    var measuredHeight = measured.Pixels.Height;
                    var measuredWidth = measured.Pixels.Width;

                    if (child.ViewportHeightLimit >= 0)
                    {
                        float mHeight = (float)(child.ViewportHeightLimit * scale);
                        if (measuredHeight > mHeight)
                        {
                            float excessHeight = measuredHeight - mHeight;
                            measuredHeight -= excessHeight;
                        }
                    }

                    if (child.ViewportWidthLimit >= 0)
                    {
                        float mWidth = (float)(child.ViewportWidthLimit * scale);
                        if (measuredWidth > mWidth)
                        {
                            float excessWidth = measuredWidth - mWidth;
                            measuredWidth -= excessWidth;
                        }
                    }

                    if (measuredWidth > maxWidth && (child.HorizontalOptions.Alignment != LayoutAlignment.Fill || child.WidthRequest >= 0))
                        maxWidth = measuredWidth;

                    if (measuredHeight > maxHeight && (child.VerticalOptions.Alignment != LayoutAlignment.Fill || child.HeightRequest >= 0))
                        maxHeight = measuredHeight;
                }
            }

            //PASS 2 for thoses with Fill todo temprorarily disable for rendering speed, investigate 
            //if (fill.Any())
            //{
            //    foreach (var child in fill)
            //    {
            //        //child.InvalidateInternal();
            //        var willDraw = MeasureChild(child, maxWidth, maxHeight, scale);
            //    }
            //}

            return ScaledSize.FromPixels(maxWidth, maxHeight, scale);
        }

        protected virtual void ApplyBindingContext()
        {
            foreach (var shade in Shadows)
            {
                shade.BindingContext = BindingContext;
            }

            if (Styles != null)
            {
                foreach (var style in this.Styles)
                {
                    style.BindingContext = BindingContext;
                }
            }

            foreach (var view in this.Views.ToList())
            {
                view.BindingContext = BindingContext;
            }
        }

        protected bool BindingContextWasSet { get; set; }
        /// <summary>
        /// First Maui will apply bindings to your controls, then it would call OnBindingContextChanged, so beware on not to break bindings.
        /// </summary>
        protected override void OnBindingContextChanged()
        {

            base.OnBindingContextChanged();

            BindingContextWasSet = true;

            try
            {
                InvalidateViewsList(); //we might get different ZIndex which is bindable..

                ApplyBindingContext();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        protected virtual MeasureRequest CreateMeasureRequest(float widthConstraint, float heightConstraint, float scale)
        {
            RenderingScale = scale;

            if (HorizontalFillRatio != 1 && double.IsFinite(widthConstraint) && widthConstraint > 0)
            {
                widthConstraint *= (float)HorizontalFillRatio;
            }
            if (VerticalFillRatio != 1 && double.IsFinite(heightConstraint) && heightConstraint > 0)
            {
                heightConstraint *= (float)VerticalFillRatio;
            }

            //var block = (!NeedMeasure && !NeedAutoSize
            //                          && _lastMeasuredForScale == scale
            //        //&& AreEqual(_lastMeasuredForHeight, heightConstraint, 1)
            //        //&& AreEqual(_lastMeasuredForWidth, widthConstraint, 1)
            //    );

            var block =
                        !NeedMeasure
                         //&& !NeedAutoSize
                         && _lastMeasuredForScale == scale
                //&& AreEqual(_lastMeasuredForHeight, heightConstraint, 1)
                //&& AreEqual(_lastMeasuredForWidth, widthConstraint, 1)
                ;

            if (!block)
            {
                _lastMeasuredForWidth = widthConstraint;
                _lastMeasuredForHeight = heightConstraint;
                _lastMeasuredForScale = scale;
            }

            return new MeasureRequest(widthConstraint, heightConstraint, scale)
            {
                IsSame = block
            };
        }

        /// <summary>
        /// DO NOT call base if you set ContentSize manually!
        /// Expecting PIXELS as input
        /// sets NeedMeasure to false
        /// sets IsLayoutDirty to true
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="heightConstraint"></param>
        /// <returns></returns>
        public virtual ScaledSize Measure(float widthConstraint, float heightConstraint, float scale)
        {
            //background measuring or invisible or self measure from draw because layout will never pass -1
            if (IsMeasuring || !CanDraw || (widthConstraint < 0 || heightConstraint < 0))
            {
                return MeasuredSize;
            }

            var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);
            if (request.IsSame)
            {
                return MeasuredSize;
            }

            //IsLayoutDirty = true;

            if (request.WidthRequest == 0 || request.HeightRequest == 0)
            {
                return SetMeasured(0, 0, request.Scale);
            }

            //if (request.WidthRequest < 0 || request.HeightRequest < 0)
            //{
            //    return ScaledSize.Empty;
            //}

            var constraints = GetMeasuringConstraints(request);

            //we measured no children, simulated///

            ContentSize = ScaledSize.FromPixels(constraints.Content.Width, constraints.Content.Height, request.Scale);

            //if (constraints.Content.Width == 0 || constraints.Content.Height == 0)
            //{
            //    return SetMeasured(0, 0, scale);
            //}

            var width = AdaptWidthConstraintToContentRequest(constraints.Request.Width, ContentSize, constraints.Margins.Left + constraints.Margins.Right);
            var height = AdaptHeightConstraintToContentRequest(constraints.Request.Height, ContentSize, constraints.Margins.Top + constraints.Margins.Bottom);

            return SetMeasured(width, height, request.Scale);
        }

        public SKRect GetMeasuringRectForChildren(float widthConstraint, float heightConstraint, double scale)
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

        public virtual ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
        {
            if (Children.Count > 0)
            {
                var maxHeight = 0.0f;
                var maxWidth = 0.0f;

                var children = GetOrderedSubviews();
                return MeasureContent(children, rectForChildrenPixels, scale);
            }
            //empty container
            else
            if (NeedAutoHeight || NeedAutoWidth)
            {
                return ScaledSize.CreateEmpty(scale);
                //return SetMeasured(0, 0, scale);
            }

            return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
        }


        public static SKRect ContractPixelsRect(SKRect rect, float scale, Thickness amount)
        {
            return new SKRect(
                (float)Math.Round(rect.Left + (float)amount.Left * scale),
                (float)Math.Round(rect.Top + (float)amount.Top * scale),
                    (float)Math.Round(rect.Right - (float)amount.Right * scale),
                        (float)Math.Round(rect.Bottom - (float)amount.Bottom * scale)
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

        /// <summary>
        /// Parameters in PIXELS. sets IsLayoutDirty = true;
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual ScaledSize SetMeasured(float width, float height, float scale)
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

                MeasuredSize = ScaledSize.FromPixels(width, height, scale);

                //SetValueCore(RenderingScaleProperty, scale, SetValueFlags.None);

                OnMeasured();

                return MeasuredSize;
            }
        }

        protected virtual void OnMeasured()
        {
            //Debug.WriteLine($"[MEASURED] {this.GetType().Name} {this.Tag} ");
            Measured?.Invoke(this, MeasuredSize);
        }

        /// <summary>
        /// UNITS
        /// </summary>
        public event EventHandler<ScaledSize> Measured;


        public ScaledSize MeasuredSize { get; set; } = new();


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
                return VerticalOptions.Alignment != LayoutAlignment.Fill && SizeRequest.Height < 0;
            }
        }
        public bool NeedAutoWidth
        {
            get
            {
                return HorizontalOptions.Alignment != LayoutAlignment.Fill && SizeRequest.Width < 0;
            }
        }



        private void OnFormsSizeChanged(object sender, EventArgs e)
        {
            //todo layout not updating!!!!

            Update();
        }


        public bool IsDisposed { get; protected set; }

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
        /// Avoid setting parent to null before calling this, or set SuperView prop manually for proper cleanup of animations and gestures if any used
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            this.SizeChanged -= OnFormsSizeChanged;

            OnDisposing();

            CustomizeLayerPaint = null;

            Tasks.StartDelayed(TimeSpan.FromSeconds(4), () =>
            {
                if (RenderObject != null)
                {
                    UseCacheDoubleBuffering = false;
                    RenderObject = null;
                }

                OffscreenRenderObject?.Dispose();
                OffscreenRenderObject = null;

                TmpRenderObject = null;

                CacheSurface?.Dispose();
                CacheSurface = null;

                //_animatorFade?.Dispose();
                //_animatorTranslate?.Dispose();

                _paintWithOpacity?.Dispose();
                _clipBounds?.Dispose();
            });

            IsDisposed = true;

            DisposeChildren();

            Parent = null;

            _lastAnimatorManager = null;

            Superview = null;
        }




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




        public SkiaDrawingContext CreateContext(SKCanvas canvas)
        {
            return new SkiaDrawingContext()
            {
                FrameTimeNanos = GetNanoseconds(),
                Canvas = canvas,
                Width = canvas.DeviceClipBounds.Width,
                Height = canvas.DeviceClipBounds.Height,
            };
        }

        public virtual void OnBeforeMeasure()
        {

        }

        public virtual void OnBeforeDraw()
        {
            Superview?.UpdateRenderingChains(this);
        }

        /// <summary>
        /// Base performs some cleanup actions with Superview
        /// </summary>
        public virtual void OnDisposing()
        {
            WhenDisposing?.Invoke(this);
            Superview?.UnregisterGestureListener(this as ISkiaGestureListener);
            Superview?.StopAndRemoveAnimatorsByParent(this);
        }


        /// <summary>
        /// do not ever erase background
        /// </summary>
        public bool IsOverlay { get; set; }


        /// <summary>
        /// Executed after the rendering
        /// </summary>
        public List<IOverlayEffect> PostAnimators { get; } = new(); //to be renamed to post-effects


        public static readonly BindableProperty UpdateWhenReturnedFromBackgroundProperty = BindableProperty.Create(nameof(UpdateWhenReturnedFromBackground),
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
            foreach (var view in Views) //will crash? why adapter nor used??
            {
                view.OnSuperviewShouldRenderChanged(state);
            }
        }



        public virtual void Render(SkiaDrawingContext context,
            SKRect destination,
            float scale)
        {

            if (IsDisposed)
                return;

            Superview = context.Superview;

            RenderingScale = scale;

            if (PostponeNeedInvalidateMeasure)
            {
                PostponeNeedInvalidateMeasure = false;
            }

            if (RenderObjectNeedsUpdate)
            {
                //disposal etc inside setter
                RenderObject = null;
            }

            if (
                //RenderedAtDestination.Width != destination.Width ||
                //RenderedAtDestination.Height != destination.Height ||
                !CompareRectsSize(RenderedAtDestination, destination, 1) ||
                RenderingScale != scale)
            {
                //NeedMeasure = true;
                RenderedAtDestination = destination;
            }

            if (RenderedAtDestination != SKRect.Empty)
            {
                //moved to superview
                //ExecuteAnimators(context.FrameTimeNanos);// Super.GetNanoseconds()

                Draw(context, destination, scale);
            }

            //UpdateLocked = false;
        }


        /// <summary>
        /// Lock between replacing and using RenderObject
        /// </summary>
        protected object LockDraw = new();

        /// <summary>
        /// Creating new cache lock
        /// </summary>
        protected object LockRenderObject = new();

        public virtual object CreatePaintArguments()
        {
            return null;
        }

        /// <summary>
        /// Returns true if had drawn.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="widthRequest"></param>
        /// <param name="heightRequest"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual bool DrawUsingRenderObject(SkiaDrawingContext context,
            float widthRequest, float heightRequest,
            SKRect destination, float scale)
        {

            //lock (lockDraw)
            {
                Arrange(destination, widthRequest, heightRequest, scale);

                bool willDraw = !CheckIsGhost();
                if (willDraw)
                {
                    if (UseCache != SkiaCacheType.None)
                    {
                        var recordArea = DrawingRect;

                        //paint from cache
                        if (!UseRenderingObject(context, recordArea, scale))
                        {
                            //record to cache and paint 
                            CreateRenderingObjectAndPaint(context, recordArea, (ctx) =>
                            {
                                Paint(ctx, DrawingRect, scale, CreatePaintArguments());
                            });
                        }
                    }
                    else
                    {
                        DrawWithClipAndTransforms(context, DrawingRect, true, true, (ctx) =>
                        {
                            Paint(ctx, DrawingRect, scale, CreatePaintArguments());
                        });
                    }
                }

                FinalizeDraw(context, scale); //NeedUpdate will go false

                return willDraw;
            }
        }

        protected virtual void Draw(SkiaDrawingContext context, SKRect destination, float scale)
        {
            if (IsDisposed)
                return;

            DrawUsingRenderObject(context,
                SizeRequest.Width, SizeRequest.Height,
                destination, scale);
        }

        /// <summary>
        /// Execute post drawing operations, like post-animators etc
        /// </summary>
        /// <param name="context"></param>
        /// <param name="scale"></param>
        protected void FinalizeDraw(SkiaDrawingContext context, double scale)
        {

            NeedUpdate = false;

            ExecutePostAnimators(context, scale);
        }

        public void ExecutePostAnimators(SkiaDrawingContext context, double scale)
        {
            try
            {
                if (PostAnimators.Count == 0)
                {
                    //Debug.WriteLine($"[ExecutePostAnimators] {Tag} NO effects!");
                    return;
                }

                //Debug.WriteLine($"[ExecutePostAnimators] {Tag} {PostAnimators.Count} effects");

                foreach (var effect in PostAnimators.ToList())
                {
                    //if (effect.IsRunning)
                    {
                        if (effect.Render(this, context, scale))
                        {
                            Repaint();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

        }

        #region ToDo OPTIMIZE

        protected virtual void DrawViews(SkiaDrawingContext context, SKRect destination, float scale, bool debug = false)
        {
            var children = GetOrderedSubviews();

            RenderViewsList((IList<SkiaControl>)children, context, destination, scale, debug);
        }


        public static readonly BindableProperty OpacityProperty = BindableProperty.Create(nameof(Opacity),
            typeof(double), typeof(SkiaControl),
            1.0,
            propertyChanged: NeedRepaint);
        /// <summary>
        /// This is used after RenderingObject is created along with other transforms like translation etc, so it's fast to be applied over a cached drawing result.
        /// </summary>
        public double Opacity
        {
            get { return (double)GetValue(OpacityProperty); }
            set { SetValue(OpacityProperty, value); }
        }


        public static readonly BindableProperty UseCacheProperty = BindableProperty.Create(nameof(UseCache),
        typeof(SkiaCacheType),
        typeof(SkiaControl),
        SkiaCacheType.None,
        propertyChanged: NeedDraw);
        /// <summary>
        /// Never reuse the rendering result. Actually true for ScrollLooped SkiaLayout viewport container to redraw its content several times for creating a looped aspect.
        /// </summary>
        public SkiaCacheType UseCache
        {
            get { return (SkiaCacheType)GetValue(UseCacheProperty); }
            set { SetValue(UseCacheProperty, value); }
        }

        public static readonly BindableProperty UseCacheDoubleBufferingProperty = BindableProperty.Create(
            nameof(UseCacheDoubleBuffering),
            typeof(bool),
            typeof(SkiaControl),
            false);

        /// <summary>
        /// Default is False.
        /// Use for controls where you imperatively has to use caching but redrawing cache might create a ui lag spike. If cache already exists will first create new one in a background thread and then seamlessly apply to ui.
        /// For conditional checks use UsingCacheDoubleBuffering instead!
        /// </summary>
        public bool UseCacheDoubleBuffering
        {
            get { return (bool)GetValue(UseCacheDoubleBufferingProperty); }
            set { SetValue(UseCacheDoubleBufferingProperty, value); }
        }

        /// <summary>
        /// Used by the UseCacheDoubleBuffering process
        /// </summary>
        public CachedObject OffscreenRenderObject
        {
            get
            {
                return _offscreenRenderObject;
            }
            set
            {
                RenderObjectNeedsUpdate = false;
                if (_offscreenRenderObject != value)
                {
                    _offscreenRenderObject = value;
                }
            }
        }
        CachedObject _offscreenRenderObject;



        public CachedObject TmpRenderObject
        {
            get
            {
                return _tmpRenderObject;
            }
            set
            {
                RenderObjectNeedsUpdate = false;
                if (_tmpRenderObject != value)
                {
                    var existing = _tmpRenderObject;
                    _tmpRenderObject = value;
                    existing?.Dispose();
                }
            }
        }
        CachedObject _tmpRenderObject;

        /// <summary>
        /// RenderObject
        /// </summary>
        public CachedObject RenderObject
        {
            get
            {
                return _renderObject;
            }
            set
            {
                RenderObjectNeedsUpdate = false;
                if (_renderObject != value)
                {
                    lock (LockDraw)
                    {
                        if (this.UsingCacheDoubleBuffering && _renderObject != null)
                        {
                            OffscreenRenderObject = _renderObject;
                            _renderObject = value;
                        }
                        else
                        {
                            var existing = _renderObject;
                            _renderObject = value;
                            existing?.Dispose();
                        }
                        OnPropertyChanged();
                        if (value != null)
                            CreatedCache?.Invoke(this, value);
                    }
                }
            }
        }
        CachedObject _renderObject;

        public event EventHandler<CachedObject> CreatedCache;

        /// <summary>
        /// Just make us repaint to apply new transforms etc
        /// </summary>
        public virtual void Repaint()
        {
            if (Parent is { UpdateLocked: false })
            {
                Parent.Update();
            }
        }

        protected SKPaint _paintWithOpacity = null;
        SKPath _clipBounds = null;

        public Action<SKPaint, SKRect> CustomizeLayerPaint { get; set; }

        protected void DrawWithClipAndTransforms(
            SkiaDrawingContext ctx,
            SKRect destination,
            bool useOpacity,
            bool useClipping,
            Action<SkiaDrawingContext> draw)
        {
            bool isClipping = false;

            //clipped inside this view bounds
            if ((IsClippedToBounds || Clipping != null) && useClipping)
            {
                isClipping = true;

                if (_clipBounds == null)
                {
                    _clipBounds = new();
                }
                else
                {
                    _clipBounds.Reset();
                }

                _clipBounds.AddRect(destination);

                if (Clipping != null)
                {
                    Clipping.Invoke(_clipBounds, destination);
                }
            }

            bool applyOpacity = useOpacity && Opacity < 1;
            bool needTransform = HasTransform;

            if (applyOpacity || isClipping || needTransform || CustomizeLayerPaint != null)
            {
                ctx.Canvas.Save();
                var restore = 0;

                _paintWithOpacity ??= new SKPaint();

                _paintWithOpacity.GuardIsAntialias(ref _paintWithOpacityIsAntialias, IsDistorted);
                _paintWithOpacity.GuardFilterQuality(ref _paintWithOpacityFilterQuality,
                    IsDistorted ? SKFilterQuality.Medium : SKFilterQuality.None);

                if (applyOpacity || CustomizeLayerPaint != null)
                {
                    var alpha = (byte)(0xFF / 1.0 * Opacity);
                    _paintWithOpacity.GuardColor(ref _paintWithOpacityColor, SKColors.White.WithAlpha(alpha));

                    if (CustomizeLayerPaint != null)
                    {
                        CustomizeLayerPaint?.Invoke(_paintWithOpacity, destination);
                    }

                    restore = ctx.Canvas.SaveLayer(_paintWithOpacity);
                }

                if (needTransform)
                {
                    var moveX = (float)(TranslationX * RenderingScale);
                    moveX = Snapping.SnapPixelsToPixel(0, moveX);

                    var moveY = (float)(TranslationY * RenderingScale);
                    moveY = Snapping.SnapPixelsToPixel(0, moveY);

                    var centerX = (float)Math.Round(destination.Left + destination.Width * (float)TransformPivotPointX + moveX);
                    var centerY = (float)Math.Round(destination.Top + destination.Height * (float)TransformPivotPointY + moveY);

                    var skewX = 0f;
                    if (SkewX > 0)
                        skewX = (float)Math.Tan(Math.PI * SkewX / 180f);

                    var skewY = 0f;
                    if (SkewY > 0)
                        skewY = (float)Math.Tan(Math.PI * SkewY / 180f);

                    if (Rotation != 0)
                    {
                        ctx.Canvas.RotateDegrees(this.Rotation, centerX, centerY);
                    }

                    var matrixTransforms = new SKMatrix
                    {
                        TransX = moveX,
                        TransY = moveY,
                        Persp0 = Perspective1,
                        Persp1 = Perspective2,
                        SkewX = skewX,
                        SkewY = skewY,
                        Persp2 = 1,
                        ScaleX = (float)this.ScaleX,
                        ScaleY = (float)this.ScaleY,
                    };

                    //set pivot point
                    var DrawingMatrix = SKMatrix.CreateTranslation(-centerX, -centerY);
                    //apply stuff
                    DrawingMatrix = DrawingMatrix.PostConcat(matrixTransforms);

                    if (CameraAngleX != 0 || CameraAngleY != 0 || CameraAngleZ != 0)
                    {
                        Helper3d.Save();
                        Helper3d.RotateXDegrees(CameraAngleX);
                        Helper3d.RotateYDegrees(CameraAngleY);
                        Helper3d.RotateZDegrees(CameraAngleZ);
                        if (CameraTranslationZ != 0)
                            Helper3d.TranslateZ(CameraTranslationZ);
                        DrawingMatrix = DrawingMatrix.PostConcat(Helper3d.Matrix);
                        Helper3d.Restore();
                    }

                    //restore coordinates back
                    DrawingMatrix = DrawingMatrix.PostConcat(SKMatrix.CreateTranslation(centerX, centerY));

                    //apply parent's transforms
                    DrawingMatrix = DrawingMatrix.PostConcat(ctx.Canvas.TotalMatrix);

                    ctx.Canvas.SetMatrix(DrawingMatrix);

                }

                if (isClipping)
                {
                    ctx.Canvas.ClipPath(_clipBounds, SKClipOperation.Intersect, true);
                }

                draw(ctx);

                if (restore != 0)
                    ctx.Canvas.RestoreToCount(restore);

                ctx.Canvas.Restore();
            }
            else
            {
                draw(ctx);
            }

        }


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
                    RenderObjectNeedsUpdate = true;
                    if (Tag == "Content")
                    {
                        var stop = 1;
                    }
                }
                //OnPropertyChanged(); disabled atm
            }
        }
        private bool _needMeasure = true;

        protected virtual void DrawRenderObjectInternal(CachedObject cache, SkiaDrawingContext ctx, SKRect destination)
        {
            if (DelegateDrawCache != null)
            {
                DelegateDrawCache(cache, ctx, destination);
            }
            else
            {
                DrawRenderObject(cache, ctx, destination);
            }
        }

        protected virtual bool UseRenderingObject(SkiaDrawingContext context, SKRect recordArea, float scale)
        {
            //lock (LockDraw)
            {
                if (RenderObject != null)
                {
                    if (UseCache == SkiaCacheType.GPU && context.Superview?.CanvasView is SkiaViewAccelerated hardware)
                    {
                        //hardware context might change if we returned from background..
                        if ((int)hardware.GRContext.Handle != (int)CacheSurface.Context.Handle)
                        {
                            //maybe we returned to app from background and GPU memory was erased
                            Update();
                            return false;
                        }
                    }

                    DrawRenderObjectInternal(RenderObject, context, recordArea);

                    if (!UsingCacheDoubleBuffering || !NeedUpdateCache)
                        return true;
                }
                else
                if (OffscreenRenderObject != null)
                {
                    DrawRenderObjectInternal(OffscreenRenderObject, context, recordArea);
                }
            }

            if (UsingCacheDoubleBuffering)
            {
                //push task to create new cache, will always try to take last from stack:
                var args = CreatePaintArguments();
                _offscreenCacheRenderingStack.Push(() =>
                {
                    //will be executed on background thread in parallel
                    TmpRenderObject = CreateRenderingObject(context, recordArea, (ctx) =>
                        {
                            Paint(ctx, recordArea, scale, args);
                        });
                });

                NeedUpdateCache = false;

                if (!_processingOffscrenRendering)
                {
                    _processingOffscrenRendering = true;
                    Task.Run(async () => //100% background thread
                    {
                        await ProcessOffscreenCacheRenderingAsync();

                    }).ConfigureAwait(false);
                }

                return true;
            }

            return false;
        }

        private readonly LimitedConcurrentStack<Action> _offscreenCacheRenderingStack = new(1);
        private bool _processingOffscrenRendering = false;

        protected SemaphoreSlim semaphoreOffsecreenProcess = new(1);

        protected bool NeedUpdateCache { get; set; }

        public async Task ProcessOffscreenCacheRenderingAsync()
        {

            await semaphoreOffsecreenProcess.WaitAsync();
            _processingOffscrenRendering = true;

            try
            {
                Action action = _offscreenCacheRenderingStack.Pop();
                while (action != null)
                {
                    try
                    {
                        action.Invoke();

                        var tmp = TmpRenderObject;
                        var kill = OffscreenRenderObject;
                        RenderObject = tmp; //OffscreenRenderObject will get maybe replaced by old RenderObject

                        _tmpRenderObject = null;

                        Repaint();

                        Tasks.StartDelayed(TimeSpan.FromSeconds(3.5), () => //kinda hacky..
                        {
                            kill?.Dispose();
                            kill = null;
                        });

                        action = _offscreenCacheRenderingStack.Pop();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                //if (NeedUpdate) //someone changed us while rendering inner content
                //{
                //    Update();
                //}

                Repaint();

            }
            finally
            {
                _processingOffscrenRendering = false;
                semaphoreOffsecreenProcess.Release();
            }

        }

        object lockOffscreenCache = new();

        public SKSurface CacheSurface { get; protected set; }

        public SKImageInfo CacheSurfaceInfo { get; protected set; }

        protected virtual SKRect GetCacheRecordingArea(SKRect drawingRect)
        {
            return drawingRect;
        }

        public void DestroyRenderingObject()
        {
            RenderObject = null;
            _needCreateBitmap = true;
        }
        bool _needCreateBitmap;

        protected virtual CachedObject CreateRenderingObject(SkiaDrawingContext context,
            SKRect recordingArea,
            Action<SkiaDrawingContext> action)
        {
            CachedObject renderObject = null;

            //            Debug.WriteLine($"[CreateRenderingObject] {this.Tag} {this.Uid}");

            //pixelsnapping
            var recordArea = new SKRect(
                (int)Math.Round(recordingArea.Left),
                (int)Math.Round(recordingArea.Top),
                (int)Math.Round(recordingArea.Right),
                (int)Math.Round(recordingArea.Bottom));

            //just draw subviews
            //need a fake context for that..
            var recordingContext = context.Clone();
            recordingContext.Height = recordArea.Height;
            recordingContext.Width = recordArea.Width;

            NeedUpdate = false; //if some child changes this while rendering to cache we will erase resulting RenderObject

            if (UseCache == SkiaCacheType.GPU || UseCache == SkiaCacheType.Image)
            {
                var width = (int)recordArea.Width;
                var height = (int)recordArea.Height;

                bool needCreateBitmap = _needCreateBitmap;

                if (CacheSurface != null &&
                    UseCache == SkiaCacheType.GPU && context.Superview?.CanvasView is SkiaViewAccelerated hardware)
                {
                    //hardware context might change if we returned from background..
                    if ((int)hardware.GRContext.Handle != (int)CacheSurface.Context.Handle)
                    {
                        needCreateBitmap = true;
                    }
                }

                if (needCreateBitmap || CacheSurface == null || height != CacheSurfaceInfo.Height || width != CacheSurfaceInfo.Width)
                {
                    _needCreateBitmap = false;

                    var kill = CacheSurface;
                    CacheSurfaceInfo = new SKImageInfo(width, height);

                    if (UseCache == SkiaCacheType.GPU && context.Superview?.CanvasView is SkiaViewAccelerated accelerated)
                    {
                        //hardware accelerated - might crash Fatal signal 11 (SIGSEGV), code 1 (SEGV_MAPERR)
                        CacheSurface = SKSurface.Create(accelerated.GRContext, true, CacheSurfaceInfo)
                                                    ?? SKSurface.Create(CacheSurfaceInfo);
                    }
                    else
                    {
                        //normal one
                        CacheSurface = SKSurface.Create(CacheSurfaceInfo);
                    }
                    kill?.Dispose();
                }
                else
                {
                    CacheSurface.Canvas.Clear();
                }

                recordingContext.Canvas = CacheSurface.Canvas;

                // Translate the canvas to start drawing at (0,0)
                recordingContext.Canvas.Translate(-recordArea.Left, -recordArea.Top);

                // Perform the drawing action
                action(recordingContext);

                // Restore the original matrix
                recordingContext.Canvas.Translate(recordArea.Left, recordArea.Top);

                CacheSurface.Canvas.Flush(); //gamechanger lol

                renderObject = new(CacheSurface, recordArea);
            }
            else
            {
                var recordOperations = GetCacheRecordingArea(recordArea);
                using (var recorder = new SKPictureRecorder())
                {
                    var canvas = recorder.BeginRecording(recordOperations); //layerCanvas.DeviceClipBounds
                    recordingContext.Canvas = canvas;

                    action(recordingContext);

                    // End the recording and obtain the SKPicture
                    var skPicture = recorder.EndRecording();

                    renderObject = new(skPicture, recordArea); //RenderObjectNeedsUpdate will go false
                }
            }

            return renderObject;
        }

        /// <summary>
        /// This is NOT calling FinalizeDraw()!
        /// parameter 'area' Usually is equal to DrawingRect
        /// </summary>
        /// <param name="context"></param>
        /// <param name="recordArea"></param>
        /// <param name="action"></param>
        protected void CreateRenderingObjectAndPaint(
            SkiaDrawingContext context,
            SKRect recordingArea,
            Action<SkiaDrawingContext> action)
        {



            if (context.Superview == null || recordingArea.Width <= 0 || recordingArea.Height <= 0 || float.IsInfinity(recordingArea.Height) || float.IsInfinity(recordingArea.Width))
            {
                return;
            }

            if (RenderObject != null && !UsingCacheDoubleBuffering)
            {
                throw new Exception("RenderObject already exists for CreateRenderingObjectAndPaint! Need to dispose and assign null to it before.");
            }

            if ((UseCache == SkiaCacheType.Image || UseCache == SkiaCacheType.GPU)
                && !IsClippedToBounds)
            {
                throw new Exception("IsClippedToBounds is required to be TRUE for caching as image.");
            }

            RenderObject = CreateRenderingObject(context, recordingArea, action);

            DrawRenderObjectInternal(RenderObject, context, RenderObject.Bounds);

            if (NeedUpdate) //someone changed us while rendering inner content
            {
                RenderObjectNeedsUpdate = true;
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
        protected virtual void Paint(SkiaDrawingContext ctx, SKRect destination, float scale, object arguments)
        {
            PaintTintBackground(ctx.Canvas, destination);
        }

        /// <summary>
        /// Create this control clip for painting content.
        /// Pass arguments if you want to use some time-frozen data for painting at any time from any thread..
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public virtual SKPath CreateClip(object arguments)
        {
            var path = new SKPath();
            path.AddRect(DrawingRect);
            return path;
        }

        /// <summary>
        /// Should delete RenderObject when starting new frame rendering
        /// </summary>
        public bool RenderObjectNeedsUpdate
        {
            get
            {
                return _renderObjectNeedsUpdate;
            }

            set
            {
                if (_renderObjectNeedsUpdate != value)
                {
                    _renderObjectNeedsUpdate = value;
                }
            }
        }
        bool _renderObjectNeedsUpdate = true;

        public static SKColor DebugRenderingColor = SKColor.Parse("#66FFFF00");

        public bool HasTransform
        {
            get
            {
                return
                    TranslationY != 0 || TranslationX != 0
                                      || ScaleY != 1f || ScaleX != 1f
                                      || Perspective1 != 0f || Perspective2 != 0f
                                      || SkewX != 0 || SkewY != 0
                                      || Rotation != 0
                                      || CameraAngleX != 0 || CameraAngleY != 0 || CameraAngleZ != 0;
            }
        }

        public bool IsDistorted
        {
            get
            {
                return
                    Rotation != 0 || ScaleY != 1f || ScaleX != 1f
                    || Perspective1 != 0f || Perspective2 != 0f
                    || SkewX != 0 || SkewY != 0
                    || CameraAngleX != 0 || CameraAngleY != 0 || CameraAngleZ != 0;
            }
        }


        /// <summary>
        /// Transforms will be applied here:
        /// Current location x,y
        /// Opacity, Translation, Scale, Rotation,
        /// Skew, Perspective
        /// /// todo use matrix
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="destination"></param>
        public virtual void DrawRenderObject(CachedObject cache, SkiaDrawingContext ctx, SKRect destination)
        {

            lock (LockDraw)
            {

                DrawWithClipAndTransforms(ctx, destination, true, true, (ctx) =>
                {
                    if (_paintWithOpacity == null)
                    {
                        _paintWithOpacity = new SKPaint();
                    }

                    _paintWithOpacity.GuardColor(ref _paintWithOpacityColor, SKColors.White);
                    _paintWithOpacity.GuardIsAntialias(ref _paintWithOpacityIsAntialias, true);
                    _paintWithOpacity.GuardFilterQuality(ref _paintWithOpacityFilterQuality, SKFilterQuality.Medium);

                    cache.Draw(ctx.Canvas, destination, _paintWithOpacity);
                });

            }

        }

        /// <summary>
        /// Use to render Absolute layout. Base method is not supporting templates, override it to implemen your logic.
        /// </summary>
        /// <param name="skiaControls"></param>
        /// <param name="context"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <param name="debug"></param>
        protected virtual void RenderViewsList(IEnumerable<SkiaControl> skiaControls,
            SkiaDrawingContext context,
            SKRect destination, float scale,
            bool debug = false)
        {
            foreach (var child in skiaControls)
            {
                if (child != null)
                {
                    child.OnBeforeDraw(); //could set IsVisible or whatever inside
                    if (child.CanDraw) //still visible 
                    {
                        child.Render(context, destination, scale);
                    }
                }
            }
        }


        #endregion
        public bool Invalidated { get; set; } = true;

        /// <summary>
        /// For internal use
        /// </summary>

        public bool NeedUpdate
        {
            get
            {
                return _needUpdate;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (_needUpdate != value)
                {
                    _needUpdate = value;

                    if (value)
                        RenderObjectNeedsUpdate = true;
                }
            }
        }
        bool _needUpdate;





        DrawnView _superview;

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
            set
            {
                if (value != _superview)
                {
                    _superview = value;
                    OnPropertyChanged();
                    SuperViewChanged();
                }
            }
        }

        public virtual ScaledRect GetOnScreenVisibleArea()
        {
            if (this.UseCache != SkiaCacheType.None)
            {
                //we are going to cache our children so they all must draw
                //regardless of the fact they might still be offscreen
                return ScaledRect.FromPixels(DrawingRect, RenderingScale);
            }

            //go up the tree to find the screen area or some parent will override this
            if (Parent != null)
            {
                return Parent.GetOnScreenVisibleArea();
            }

            if (Superview != null)
            {
                return Superview.GetOnScreenVisibleArea();
            }

            return ScaledRect.FromPixels(Destination, RenderingScale);
        }

        bool _lastUpdatedVisibility;

        /// <summary>
        /// Indicated that wants to be re-measured without invalidating cache
        /// </summary>
        public virtual void InvalidateViewport()
        {
            if (!IsDisposed)
            {
                NeedMeasure = true;
                NeedMeasure = true;
                IsLayoutDirty = true; //force recalc of DrawingRect
                Parent?.InvalidateViewport();
            }
        }

        public virtual void Update()
        {
            if (UseCache != SkiaCacheType.None && UseCache != SkiaCacheType.Operations)
                IsClippedToBounds = true;

            NeedUpdate = true;
            NeedUpdateCache = true;

            if (UpdateLocked)
                return;

            if (_lastUpdatedVisibility != IsVisible)
            {
                _lastUpdatedVisibility = IsVisible;
            }

            Parent?.Update();
        }

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


        public SKRect Destination { get; protected set; }

        /// <summary>
        /// Pixels, if you see no Scale parameter
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="destination"></param>
        public virtual void PaintTintBackground(SKCanvas canvas, SKRect destination)
        {
            if (BackgroundColor != Colors.Transparent)
            {
                using (var paint = new SKPaint
                {
                    Color = BackgroundColor.ToSKColor(),
                    Style = SKPaintStyle.StrokeAndFill,
                    BlendMode = this.FillBlendMode
                })
                {
                    SetupGradient(paint, FillGradient, destination);
                    canvas.DrawRect(destination, paint);
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

            canvas.Save();

            canvas.ClipPath(clip, SKClipOperation.Intersect, true);

            draw();

            canvas.Restore();
        }

        #region TAPER

        enum TaperSide { Left, Top, Right, Bottom }

        enum TaperCorner { LeftOrTop, RightOrBottom, Both }

        static class TaperTransform
        {
            public static SKMatrix Make(SKSize size, TaperSide taperSide, TaperCorner taperCorner, float taperFraction)
            {
                SKMatrix matrix = SKMatrix.MakeIdentity();

                switch (taperSide)
                {
                case TaperSide.Left:
                matrix.ScaleX = taperFraction;
                matrix.ScaleY = taperFraction;
                matrix.Persp0 = (taperFraction - 1) / size.Width;

                switch (taperCorner)
                {
                case TaperCorner.RightOrBottom:
                break;

                case TaperCorner.LeftOrTop:
                matrix.SkewY = size.Height * matrix.Persp0;
                matrix.TransY = size.Height * (1 - taperFraction);
                break;

                case TaperCorner.Both:
                matrix.SkewY = (size.Height / 2) * matrix.Persp0;
                matrix.TransY = size.Height * (1 - taperFraction) / 2;
                break;
                }
                break;

                case TaperSide.Top:
                matrix.ScaleX = taperFraction;
                matrix.ScaleY = taperFraction;
                matrix.Persp1 = (taperFraction - 1) / size.Height;

                switch (taperCorner)
                {
                case TaperCorner.RightOrBottom:
                break;

                case TaperCorner.LeftOrTop:
                matrix.SkewX = size.Width * matrix.Persp1;
                matrix.TransX = size.Width * (1 - taperFraction);
                break;

                case TaperCorner.Both:
                matrix.SkewX = (size.Width / 2) * matrix.Persp1;
                matrix.TransX = size.Width * (1 - taperFraction) / 2;
                break;
                }
                break;

                case TaperSide.Right:
                matrix.ScaleX = 1 / taperFraction;
                matrix.Persp0 = (1 - taperFraction) / (size.Width * taperFraction);

                switch (taperCorner)
                {
                case TaperCorner.RightOrBottom:
                break;

                case TaperCorner.LeftOrTop:
                matrix.SkewY = size.Height * matrix.Persp0;
                break;

                case TaperCorner.Both:
                matrix.SkewY = (size.Height / 2) * matrix.Persp0;
                break;
                }
                break;

                case TaperSide.Bottom:
                matrix.ScaleY = 1 / taperFraction;
                matrix.Persp1 = (1 - taperFraction) / (size.Height * taperFraction);

                switch (taperCorner)
                {
                case TaperCorner.RightOrBottom:
                break;

                case TaperCorner.LeftOrTop:
                matrix.SkewX = size.Width * matrix.Persp1;
                break;

                case TaperCorner.Both:
                matrix.SkewX = (size.Width / 2) * matrix.Persp1;
                break;
                }
                break;
                }
                return matrix;
            }
        }

        #endregion


        SK3dView _SK3dView;

        public SK3dView Helper3d
        {
            get
            {
                if (_SK3dView == null)
                {
                    _SK3dView = new SK3dView();
                }
                return _SK3dView;
            }
        }







        public void PaintClearBackground(SKCanvas canvas)
        {
            if (ClearColor != Colors.Transparent)
            {
                using (var paint = new SKPaint
                {
                    Color = ClearColor.ToSKColor(),
                    Style = SKPaintStyle.StrokeAndFill,
                })
                {
                    canvas.DrawRect(Destination, paint);
                }
            }
        }

        //static int countRedraws = 0;
        protected static void NeedDraw(BindableObject bindable, object oldvalue, object newvalue)
        {

            try
            {
                var control = bindable as SkiaControl;
                {
                    if (control != null && !control.IsDisposed)
                    {
                        control.Update();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        /// <summary>
        /// Just make us repaint to apply new transforms etc
        /// </summary>
        protected static void NeedRepaint(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as SkiaControl;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.Repaint();
                }
            }
        }

        protected bool PostponeNeedInvalidateMeasure;

        /// <summary>
        /// Soft invalidation, without requiring update. So next time we try to draw this one it will recalc everything.
        /// </summary>
        public virtual void InvalidateInternal()
        {
            InvalidateViewsList();
            IsLayoutDirty = true;
            NeedMeasure = true;
            RenderObjectNeedsUpdate = true;
        }



        /// <summary>
        /// Base calls InvalidateInternal and InvalidateParent
        /// </summary>
        public virtual void Invalidate()
        {
            InvalidateInternal();

            InvalidateParent();
        }


        public virtual void CalculateMargins()
        {
            //use Margin property as starting point
            //if specific margin is set (>=0) apply 
            //to final Thickness
            var margin = Margin;
            if (MarginTop >= 0)
                margin.Top = MarginTop;
            if (MarginBottom >= 0)
                margin.Bottom = MarginBottom;
            if (MarginLeft >= 0)
                margin.Left = MarginLeft;
            if (MarginRight >= 0)
                margin.Right = MarginRight;

            margin.Left += AddMarginLeft;
            margin.Right += AddMarginRight;
            margin.Top += AddMarginTop;
            margin.Bottom += AddMarginBottom;

            Margins = margin;
        }

        public virtual Thickness CalculateContentConstraintsInPixels(float scale)
        {
            var constraintLeft = Math.Round((Padding.Left + Margins.Left) * scale);
            var constraintRight = Math.Round((Padding.Right + Margins.Right) * scale);
            var constraintTop = Math.Round((Padding.Top + Margins.Top) * scale);
            var constraintBottom = Math.Round((Padding.Bottom + Margins.Bottom) * scale);
            return new(constraintLeft, constraintTop, constraintRight, constraintBottom);
        }

        /// <summary>
        /// Main method to call when dimensions changed
        /// </summary>
        public virtual void InvalidateMeasure()
        {
            if (!IsDisposed)
            {
                CalculateMargins();
                CalculateSizeRequest();

                InvalidateWithChildren();
                InvalidateParent();

                Update();
            }
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

        public virtual void InvalidateWithChildren()
        {
            UpdateLocked = true;

            foreach (var view in Views) //will crash? why adapter nor used??
            {
                InvalidateChildren(view as SkiaControl);
            }

            UpdateLocked = false;

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

        protected static void NeedInvalidateMeasure(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {
                control.InvalidateMeasure();
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



        public virtual bool IsTemplated
        {
            get
            {
                return (this.ItemTemplate != null || ItemTemplateType != null);
            }
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



        #region SHADOWS

        /// <summary>
        /// Creates and sets an ImageFilter for SKPaint
        /// </summary>
        /// <param name="paintShadow"></param>
        /// <param name="shadow"></param>
        protected void AddShadowFilter(SKPaint paintShadow, SkiaShadow shadow)
        {
            var colorShadow = shadow.Color;
            if (colorShadow.Alpha == 1.0)
            {
                colorShadow = shadow.Color.WithAlpha((float)shadow.Opacity);
            }

            paintShadow.ImageFilter = SKImageFilter.CreateDropShadow(
                (float)(shadow.X * RenderingScale), (float)(shadow.Y * RenderingScale),
                (float)(shadow.Blur * RenderingScale), (float)(shadow.Blur * RenderingScale),
                colorShadow.ToSKColor());
        }

        public static readonly BindableProperty ShadowsProperty = BindableProperty.Create(
            nameof(Shadows),
            typeof(IList<SkiaShadow>),
            typeof(SkiaControl),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableCollection<SkiaShadow>();
                ShadowsPropertyChanged(instance, null, created);
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaShadow>,
            propertyChanged: ShadowsPropertyChanged,
            coerceValue: CoerceShadows);

        private const int DefaultCornerRadius = 0;

        private static int instanceCount = 0;

        private readonly WeakEventSource<NotifyCollectionChangedEventArgs> _weakCollectionChangedSource
            = new WeakEventSource<NotifyCollectionChangedEventArgs>();


        public event EventHandler<NotifyCollectionChangedEventArgs> WeakCollectionChanged
        {
            add => _weakCollectionChangedSource.Subscribe(value);
            remove => _weakCollectionChangedSource.Unsubscribe(value);
        }


        public IList<SkiaShadow> Shadows
        {
            get => (IList<SkiaShadow>)GetValue(ShadowsProperty);
            set => SetValue(ShadowsProperty, value);
        }

        private static object CoerceShadows(BindableObject bindable, object value)
        {
            if (!(value is ReadOnlyCollection<SkiaShadow> readonlyCollection))
            {
                return value;
            }

            return new ReadOnlyCollection<SkiaShadow>(
                readonlyCollection.ToList());

            //return new ReadOnlyCollection<SkiaShadow>(
            //    readonlyCollection.Select(s => s.Clone() as SkiaShadow)
            //        .ToList());
        }

        private static void ShadowsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            var skiaControl = (SkiaControl)bindable;
            var enumerableShadows = (IEnumerable<SkiaShadow>)newvalue;

            if (oldvalue != null)
            {
                if (oldvalue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= skiaControl.OnSkiaPropertyShadowCollectionChanged;
                }

                if (oldvalue is IEnumerable<SkiaShadow> oldList)
                {
                    foreach (var shade in oldList)
                    {
                        shade.Parent = null;
                        shade.BindingContext = null;
                    }
                }
            }

            foreach (var shade in enumerableShadows)
            {
                shade.Parent = skiaControl;
                shade.BindingContext = skiaControl.BindingContext;
            }

            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= skiaControl.OnSkiaPropertyShadowCollectionChanged;
                newCollection.CollectionChanged += skiaControl.OnSkiaPropertyShadowCollectionChanged;
            }
        }

        private void OnSkiaPropertyShadowCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
            case NotifyCollectionChangedAction.Add:
            foreach (SkiaShadow newSkiaPropertyShadow in e.NewItems)
            {
                newSkiaPropertyShadow.Parent = this;
                newSkiaPropertyShadow.BindingContext = BindingContext;
                _weakCollectionChangedSource.Raise(this, e);
            }

            break;

            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Remove:
            foreach (SkiaShadow oldSkiaPropertyShadow in e.OldItems ?? new SkiaShadow[0])
            {
                oldSkiaPropertyShadow.Parent = null;
                oldSkiaPropertyShadow.BindingContext = null;
                _weakCollectionChangedSource.Raise(this, e);
            }

            break;
            }
        }

        #endregion


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
                Update();
                return true;
            }
            return false;
        }

        public void UnregisterAnimator(string uid)
        {
            var top = GetAnimatorsManager();
            if (top == null)
            {
                top = _lastAnimatorManager;
            }
            top?.RemoveAnimator(uid);
        }

        public void StopAndRemoveAnimatorsByKeySubstring(string keySubstring)
        {
            Superview?.StopAndRemoveAnimatorsByKeySubstring(keySubstring);
        }

        public async void PlayRippleAnimation(Color color, double x, double y, bool removePrevious = true)
        {
            if (removePrevious)
            {
                StopAndRemoveAnimatorsByKeySubstring("Ripple");
            }
            //Debug.WriteLine($"[RIPPLE] start play for '{Tag}'");
            var animation = new RippleAnimator(this)
            {
                Color = color.ToSKColor(),
                X = x,
                Y = y
            };
            animation.Start();
        }

        public async void PlayShimmerAnimation(Color color, float shimmerWidth = 50, float shimmerAngle = 45, int speedMs = 1000, bool removePrevious = true)
        {
            //Debug.WriteLine($"[SHIMMER] start play for '{Tag}'");
            if (removePrevious)
            {
                StopAndRemoveAnimatorsByKeySubstring("Shimmer");
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


        #region GRADIENTS

        public SKShader CreateGradientAsShader(SKRect destination, SkiaGradient gradient)
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

                float sweep = (float)Value3;//((float)this.Variable1 % (float)this.Variable2 / 100F) * 360.0F;

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

        /// <summary>
        /// Creates Shader for gradient and sets it to passed SKPaint along with BlendMode
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="gradient"></param>
        /// <param name="destination"></param>
        public void SetupGradient(SKPaint paint, SkiaGradient gradient, SKRect destination)
        {
            if (gradient != null && paint != null)
            {
                if (paint.Color.Alpha == 0)
                {
                    paint.Color = SKColor.FromHsl(0, 0, 0);
                }

                paint.Shader = CreateGradientAsShader(destination, gradient);
                paint.BlendMode = gradient.BlendMode;
            }
        }

        #endregion


        #region CHILDREN VIEWS

        private IReadOnlyList<SkiaControl> _orderedChildren;

        public IReadOnlyList<SkiaControl> GetOrderedSubviews(bool recalculate = false)
        {
            if (_orderedChildren == null || recalculate)
            {
                _orderedChildren = Views.OrderBy(x => x.ZIndex).ToList();
            }
            return _orderedChildren;
        }

        public List<SkiaControl> Views { get; } = new();

        public virtual void DisposeChildren()
        {
            foreach (var child in Views.ToList())
            {
                Tasks.StartDelayed(TimeSpan.FromSeconds(2), () =>
                {
                    child.Dispose();
                });
                RemoveSubView(child);
            }
            Views.Clear();
            Invalidate();
        }

        public virtual void ClearChildren()
        {
            foreach (var child in Views.ToList())
            {
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

        protected virtual void OnChildAdded(SkiaControl child)
        {
            Invalidate();
        }

        protected override void OnChildRemoved(Element child, int oldLogicalIndex)
        {
            //base.OnChildRemoved(child, oldLogicalIndex);
        }

        protected override void OnChildAdded(Element child)
        {
            //base.OnChildAdded(child);
        }

        protected virtual void OnChildRemoved(SkiaControl child)
        {
            Invalidate();
        }

        public DateTime GestureListenerRegistrationTime { get; set; }

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
                HadInput.TryRemove(gestureListener.Uid, out var _);
                GestureListeners.Remove(gestureListener);
                //Debug.WriteLine($"Removed {gestureListener} from gestures of {this.Tag} {this}");
            }
        }

        /// <summary>
        /// Children we should check for touch hits
        /// </summary>
        public SortedSet<ISkiaGestureListener> GestureListeners { get; } = new(new DescendingZIndexGestureListenerComparer());

        static object lockParent = new();

        public void SetParent(IDrawnBase parent)
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
                    Superview?.StopAndRemoveAnimatorsByParent(this);

                    oldParent.Views.Remove(this);

                    if (oldParent is SkiaControl skiaParent)
                    {
                        skiaParent.InvalidateViewsList();
                    }
                }

                if (parent == null)
                {
                    Parent = null;
                    //BindingContext = null;

                    this.SizeChanged -= OnFormsSizeChanged;
                    return;
                }

                if (!parent.Views.Contains(this))
                {
                    parent.Views.Add(this);
                    if (parent is SkiaControl skiaParent)
                    {
                        skiaParent.InvalidateViewsList();
                    }
                }

                Parent = parent;

                if (iAmGestureListener != null)
                {
                    parent.RegisterGestureListener(iAmGestureListener);
                }

                if (parent is SkiaControl control)
                {
                    //control.InvalidateViewsList();
                    if (BindingContext == null)
                        BindingContext = control.BindingContext;
                }

                //if (this is ISkiaHasChildren)
                //{
                //    ((ISkiaHasChildren)this).LayoutInvalidated = true;
                //}

                this.SizeChanged += OnFormsSizeChanged;

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


        public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate),
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


        #region SELECTABLE TEMPLATES


        public static readonly BindableProperty TemplatesProperty = BindableProperty.Create(
            nameof(Templates),
            typeof(IList<ISkiaAttachable>),
            typeof(SkiaControl),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableCollection<ISkiaAttachable>();
                ItemTemplateChanged(instance, null, created);
                return created;
            },
            validateValue: (bo, v) => v is IList<ISkiaAttachable>,
            propertyChanged: ItemTemplateChanged,
            coerceValue: CoerceTemplates);

        public IList<ISkiaAttachable> Templates
        {
            get => (IList<ISkiaAttachable>)GetValue(TemplatesProperty);
            set => SetValue(TemplatesProperty, value);
        }

        private static object CoerceTemplates(BindableObject bindable, object value)
        {
            if (!(value is ReadOnlyCollection<ISkiaAttachable> readonlyCollection))
            {
                return value;
            }

            return new ReadOnlyCollection<ISkiaAttachable>(
                readonlyCollection.ToList());
        }

        private void OnSkiaPropertyTemplateCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is SkiaControl control)
            {
                switch (e.Action)
                {
                case NotifyCollectionChangedAction.Add:
                control.OnItemTemplateChanged();
                break;

                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                control.OnItemTemplateChanged();
                break;
                }
            }
        }




        #endregion

        public static readonly BindableProperty ChildrenProperty = BindableProperty.Create(
            nameof(Children),
            typeof(IList<ISkiaAttachable>),
            typeof(SkiaControl),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableCollection<ISkiaAttachable>();
                ChildrenPropertyChanged(instance, null, created);
                return created;
            },
            validateValue: (bo, v) => v is IList<ISkiaAttachable>,
            propertyChanged: ChildrenPropertyChanged);

        private readonly WeakEventSource<NotifyCollectionChangedEventArgs> _weakViewsCollectionChangedSource
            = new WeakEventSource<NotifyCollectionChangedEventArgs>();


        public IList<ISkiaAttachable> Children
        {
            get => (IList<ISkiaAttachable>)GetValue(ChildrenProperty);
            set => SetValue(ChildrenProperty, value);
        }

        public virtual SkiaControl AttachControl
        {
            get
            {
                return this;
            }
        }

        protected void AddOrRemoveView(ISkiaAttachable child, bool add)
        {
            SkiaControl subView = child.AttachControl;

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

        public virtual void SetChildren(IEnumerable<ISkiaAttachable> views)
        {
            ClearChildren();
            foreach (var child in views)
            {
                AddOrRemoveView(child, true);
            }
        }

        private static void ChildrenPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
            {

                var enumerableChildren = (IEnumerable<ISkiaAttachable>)newvalue;

                if (oldvalue != null)
                {
                    var oldViews = (IEnumerable<ISkiaAttachable>)oldvalue;

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
            get
            {
                return ItemTemplate != null;
            }
        }

        private void OnChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (HasItemTemplate)
                return;



            switch (e.Action)
            {
            case NotifyCollectionChangedAction.Add:
            foreach (ISkiaAttachable newChildren in e.NewItems)
            {
                AddOrRemoveView(newChildren, true);
                //						AddSubView(newChildren);
                //newChildren.SetParent(this);
                _weakViewsCollectionChangedSource.Raise(this, e);
            }

            break;

            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Remove:
            foreach (ISkiaAttachable oldChildren in e.OldItems ?? Array.Empty<ISkiaAttachable>())
            {
                AddOrRemoveView(oldChildren, false);
                //						RemoveSubView(oldChildren);
                //oldChildren.SetParent(null);
                _weakViewsCollectionChangedSource.Raise(this, e);
            }

            break;
            }

            Update();
        }

        #endregion

        #region SAVED VIEWS

        public SkiaControl GetSavedControl(string tag)
        {
            return SavedControls.FirstOrDefault(v => v.Tag == tag) as SkiaControl;
        }

        public static readonly BindableProperty SavedControlsProperty = BindableProperty.Create(
    nameof(SavedControls),
    typeof(IList<SkiaControl>),
    typeof(SkiaControl),
    defaultValueCreator: (instance) =>
    {
        var created = new ObservableCollection<SkiaControl>();
        SavedControlsPropertyChanged(instance, null, created);
        return created;
    },
    validateValue: (bo, v) => v is IList<SkiaControl>,
    propertyChanged: SavedControlsPropertyChanged);

        private readonly WeakEventSource<NotifyCollectionChangedEventArgs> _weakSavedControlsCollectionChangedSource
            = new();

        public IList<SkiaControl> SavedControls
        {
            get => (IList<SkiaControl>)GetValue(SavedControlsProperty);
            set => SetValue(SavedControlsProperty, value);
        }

        private static void SavedControlsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
            {
                var enumerableSavedControls = (IEnumerable<SkiaControl>)newvalue;

                if (oldvalue != null)
                {
                    var oldViews = (IEnumerable<SkiaControl>)oldvalue;

                    if (oldvalue is INotifyCollectionChanged oldCollection)
                    {
                        oldCollection.CollectionChanged -= skiaControl.OnSavedControlsCollectionChanged;
                    }

                }

                if (newvalue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= skiaControl.OnSavedControlsCollectionChanged;
                    newCollection.CollectionChanged += skiaControl.OnSavedControlsCollectionChanged;
                }

            }
        }

        private void OnSavedControlsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
            case NotifyCollectionChangedAction.Add:
            foreach (SkiaControl newSavedControls in e.NewItems)
            {
                _weakSavedControlsCollectionChangedSource.Raise(this, e);
            }
            break;

            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Remove:
            foreach (SkiaControl oldSavedControls in e.OldItems ?? new SkiaControl[0])
            {
                _weakSavedControlsCollectionChangedSource.Raise(this, e);
            }
            break;
            }
        }


        #endregion

        public static readonly BindableProperty InputTransparentProperty = BindableProperty.Create(nameof(InputTransparent),
        typeof(bool),
        typeof(SkiaControl),
        false);

        private IAnimatorsManager _lastAnimatorManager;
        private Func<List<SkiaControl>> _createChildren;

        public bool InputTransparent
        {
            get { return (bool)GetValue(InputTransparentProperty); }
            set { SetValue(InputTransparentProperty, value); }
        }

        public HandleGestures.GestureListener GesturesEffect { get; set; }

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

            case TransformAspect.AspectFit:
            aspectX = Math.Min(s1, s2);
            aspectY = aspectX;
            break;

            case TransformAspect.AspectFill:
            aspectX = width < dest.Width ? Math.Max(s1, s2) : 1;
            aspectY = aspectX;
            break;

            case TransformAspect.Cover:
            aspectX = s1;
            aspectY = s2;
            break;

            case TransformAspect.AspectCover:
            aspectX = Math.Max(s1, s2);
            aspectY = aspectX;
            break;

            case TransformAspect.AspectFitFill:
            aspectX = (width < dest.Width || height < dest.Height) ? Math.Max(s1, s2) : Math.Min(s1, s2);
            aspectY = aspectX;
            break;

            case TransformAspect.FitFill:
            aspectX = width < dest.Width ? s1 : (dest.Width < width ? dest.Width / width : 1);
            aspectY = height < dest.Height ? s2 : (dest.Height < height ? dest.Height / height : 1);
            break;
            }

            return (aspectX, aspectY);
        }

        #region HELPERS

        public static Random Random = new Random();
        protected double _arrangedViewportHeightLimit;
        protected double _arrangedViewportWidthLimit;
        protected float _lastMeasuredForScale;
        private bool _isLayoutDirty;
        private Thickness _margins;

        public static Color GetRandomColor()
        {
            byte r = (byte)Random.Next(256);
            byte g = (byte)Random.Next(256);
            byte b = (byte)Random.Next(256);

            return Color.FromRgb(r, g, b);
        }

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
        public static bool CompareRects(SKRect a, SKRect b, float precision)
        {
            if ((float.IsInfinity(a.Left) && float.IsInfinity(b.Left)) || Math.Abs(a.Left - b.Left) <= precision)
            {
                if ((float.IsInfinity(a.Top) && float.IsInfinity(b.Top)) || Math.Abs(a.Top - b.Top) <= precision)
                {
                    if ((float.IsInfinity(a.Right) && float.IsInfinity(b.Right)) || Math.Abs(a.Right - b.Right) <= precision)
                    {
                        if ((float.IsInfinity(a.Bottom) && float.IsInfinity(b.Bottom)) || Math.Abs(a.Bottom - b.Bottom) <= precision)
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
                if ((float.IsInfinity(a.Height) && float.IsInfinity(b.Height)) || (Math.Abs(a.Height - b.Height) <= precision))
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
                if ((float.IsInfinity(a.Height) && float.IsInfinity(b.Height)) || Math.Abs(a.Height - b.Height) <= precision)
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
                return DirectionType.None; // The direction is neither horizontal nor vertical (the vectors are equal or diagonally aligned)
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


        /// <summary>
        /// Ported from Avalonia: Accepting POINTS calculates the value to be used for layout rounding at high DPI by rounding the value up
        /// to the nearest pixel.
        /// </summary>
        /// <param name="value">Input value to be rounded.</param>
        /// <param name="renderingScale">Ratio of screen's DPI to layout DPI</param>
        /// <returns>Adjusted value that will produce layout rounding on screen at high dpi.</returns>
        /// <remarks>
        /// This is a layout helper method. It takes DPI into account and also does not return
        /// the rounded value if it is unacceptable for layout, e.g. Infinity or NaN. It's a helper
        /// associated with the UseLayoutRounding property and should not be used as a general rounding
        /// utility.
        /// </remarks>
        public static float RoundPointsUp(float value, double renderingScale)
        {
            double newValue;

            // Round the value to avoid FP errors. This is needed because if `value` has a floating
            // point precision error (e.g. 79.333333333333343) then when it's multiplied by
            // `dpiScale` and rounded up, it will be rounded up to a value one greater than it
            // should be.
            value = (float)Math.Round(value, 8, MidpointRounding.ToZero);

            // If DPI == 1, don't use DPI-aware rounding.
            if (!IsOne(renderingScale))
            {
                newValue = Math.Ceiling(value * renderingScale) / renderingScale;

                // If rounding produces a value unacceptable to layout (NaN, Infinity or MaxValue),
                // use the original value.
                if (double.IsNaN(newValue) ||
                    double.IsInfinity(newValue) ||
                    AreClose(newValue, double.MaxValue))
                {
                    newValue = value;
                }
            }
            else
            {
                newValue = Math.Ceiling(value);
            }

            return (float)newValue;
        }


        /// <summary>
        /// Accepting PIXELS calculates the value to be used for layout rounding at high DPI by rounding the value up
        /// </summary>
        /// <param name="pixelValue"></param>
        /// <param name="renderingScale"></param>
        /// <returns></returns>
        public static float RoundPixelsUp(float pixelValue, double renderingScale)
        {
            float newValue;

            // Round the value to avoid FP errors
            pixelValue = (float)Math.Round(pixelValue, 8, MidpointRounding.ToZero);

            // If renderingScale == 1, don't use DPI-aware rounding.
            if (!IsOne(renderingScale))
            {
                // In this case, we're assuming pixelValue is already DPI-scaled.
                // So, we just round it to the nearest integer.
                newValue = (float)Math.Ceiling(pixelValue);

                // If rounding produces a value unacceptable to layout (NaN, Infinity or MaxValue),
                // use the original value.
                if (float.IsNaN(newValue) ||
                    float.IsInfinity(newValue) ||
                    AreClose(newValue, float.MaxValue))
                {
                    newValue = pixelValue;
                }
            }
            else
            {
                newValue = (float)Math.Ceiling(pixelValue);
            }

            return newValue;
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