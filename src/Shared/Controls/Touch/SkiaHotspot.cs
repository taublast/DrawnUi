using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Windows.Input;

namespace DrawnUi.Draw
{
    public class SkiaHotspot : SkiaControl, ISkiaGestureListener
    {
        public SkiaHotspot()
        {
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
            //BackgroundColor = Color.Parse("#33ff0000");
        }


        protected override void Draw(DrawingContext context)
        {
            var scale = context.Scale;
            var destination = context.Destination;
            var canvas = context.Context.Canvas;

            if (IsRootView() && !IsOverlay)
                canvas.Clear(ClearColor.ToSKColor());

            Arrange(destination, SizeRequest.Width, SizeRequest.Height, scale);

            if (!CheckIsGhost())
            {
                DrawWithClipAndTransforms(context.WithDestination(DrawingRect), DrawingRect, true, true, (ctx) =>
                {
                    PaintTintBackground(canvas, DrawingRect);
                    //Paint(ctx, DrawingRect, scale, CreatePaintArguments());
                });

                FinalizeDrawingWithRenderObject(context);
            }
        }

        public static float PanThreshold = 5;

        private bool _TouchDown;
        public bool TouchDown
        {
            get
            {
                return _TouchDown;
            }
            set
            {
                if (_TouchDown != value)
                {
                    _TouchDown = value;
                    OnPropertyChanged();
                }
            }
        }



        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            //Trace.WriteLine($"SkiaHotspot. {type} {args.Action} {args.Event.Location.X} {args.Event.Location.Y}");

            if (args.Type == TouchActionResult.Down)
            {
                TotalDown++;
                TouchDown = true;
                Down?.Invoke(this, args);
            }
            else
            if (args.Type == TouchActionResult.Up)
            {
                TouchDown = false;
                Up?.Invoke(this, args);
            }
            else
            if (args.Type == TouchActionResult.Tapped)
            {
                var consumed = false;

                if (SendTapped(this, args, GestureEventProcessingInfo.Empty, Super.SendTapsOnMainThread))
                {
                    consumed = true;
                }

                TotalTapped++;
                var delay = 10;

                //var x = (args.Event.Location.X) / RenderingScale;
                //var y = (args.Event.Location.Y) / RenderingScale;

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

                        delay = DelayCallbackMs;
                    }
                    else
                    if (AnimationTapped == SkiaTouchAnimation.Shimmer)
                    {
                        var color = ShimmerEffectColor;
                        control.PlayShimmerAnimation(color, ShimmerEffectWidth, ShimmerEffectAngle, ShimmerEffectSpeed);
                        delay = DelayCallbackMs;
                    }
                }

                if (CommandTapped != null)
                {
                    consumed = true;
                    Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(delay), async () =>
                    {
                        await Task.Run(() =>
                        {
                            CommandTapped?.Execute(CommandTappedParameter);
                        }).ConfigureAwait(false);
                    });
                }

                return consumed ? this : null;
            }
            //do not need to call base we have no children
            else
            if (args.Type == TouchActionResult.Panning)
            {
                if (LockPanning)
                {
                    return this; //no panning for you my friend 
                }
            }

            return null;
        }


        private long _TotalTapped;
        public long TotalTapped
        {
            get
            {
                return _TotalTapped;
            }
            set
            {
                if (_TotalTapped != value)
                {
                    _TotalTapped = value;
                    OnPropertyChanged();
                }
            }
        }

        private long _TotalDown;
        public long TotalDown
        {
            get
            {
                return _TotalDown;
            }
            set
            {
                if (_TotalDown != value)
                {
                    _TotalDown = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// You might want to pause to show effect before executing command. Default is 0.
        /// </summary>
        public static int DelayCallbackMs = 0;

        public event EventHandler<SkiaGesturesParameters> Up;

        public event EventHandler<SkiaGesturesParameters> Down;

        //public event EventHandler<SkiaGesturesParameters> Tapped;
        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            Up = null;
            Down = null;
            //Tapped = null;
        }

        #region PROPERTIES


        public virtual bool OnFocusChanged(bool focus)
        {
            return false;
        }

        public static readonly BindableProperty ShimmerEffectColorProperty = BindableProperty.Create(nameof(ShimmerEffectColor),
            typeof(Color),
            typeof(SkiaHotspot),
            Colors.White.WithAlpha(0.33f));
        public Color ShimmerEffectColor
        {
            get { return (Color)GetValue(ShimmerEffectColorProperty); }
            set { SetValue(ShimmerEffectColorProperty, value); }
        }

        public static readonly BindableProperty ShimmerEffectAngleProperty = BindableProperty.Create(nameof(ShimmerEffectAngle),
            typeof(float),
            typeof(SkiaHotspot),
            33.0f);
        public float ShimmerEffectAngle
        {
            get { return (float)GetValue(ShimmerEffectAngleProperty); }
            set { SetValue(ShimmerEffectAngleProperty, value); }
        }

        public static readonly BindableProperty ShimmerEffectWidthProperty = BindableProperty.Create(nameof(ShimmerEffectWidth),
            typeof(float),
            typeof(SkiaHotspot),
            150.0f);
        public float ShimmerEffectWidth
        {
            get { return (float)GetValue(ShimmerEffectWidthProperty); }
            set { SetValue(ShimmerEffectWidthProperty, value); }
        }

        public static readonly BindableProperty ShimmerEffectSpeedProperty = BindableProperty.Create(nameof(ShimmerEffectSpeed),
            typeof(int),
            typeof(SkiaHotspot),
            500);
        public int ShimmerEffectSpeed
        {
            get { return (int)GetValue(ShimmerEffectSpeedProperty); }
            set { SetValue(ShimmerEffectSpeedProperty, value); }
        }



        public static readonly BindableProperty CommandTappedProperty = BindableProperty.Create(nameof(CommandTapped), typeof(ICommand),
            typeof(SkiaHotspot),
            null);
        public ICommand CommandTapped
        {
            get { return (ICommand)GetValue(CommandTappedProperty); }
            set { SetValue(CommandTappedProperty, value); }
        }

        public static readonly BindableProperty CommandTappedParameterProperty = BindableProperty.Create(nameof(CommandTappedParameter), typeof(object),
            typeof(SkiaHotspot),
            null);
        public object CommandTappedParameter
        {
            get { return GetValue(CommandTappedParameterProperty); }
            set { SetValue(CommandTappedParameterProperty, value); }
        }

        public static readonly BindableProperty CommandLongPressingProperty = BindableProperty.Create(nameof(CommandLongPressing), typeof(ICommand),
            typeof(SkiaHotspot),
            null);
        public ICommand CommandLongPressing
        {
            get { return (ICommand)GetValue(CommandLongPressingProperty); }
            set { SetValue(CommandLongPressingProperty, value); }
        }

        public static readonly BindableProperty CommandLongPressingParameterProperty = BindableProperty.Create(nameof(CommandLongPressingParameter), typeof(object),
            typeof(SkiaHotspot),
            null);
        public object CommandLongPressingParameter
        {
            get { return GetValue(CommandLongPressingParameterProperty); }
            set { SetValue(CommandLongPressingParameterProperty, value); }
        }

        public static readonly BindableProperty LockPanningProperty = BindableProperty.Create(nameof(LockPanning),
        typeof(bool),
        typeof(SkiaHotspot),
        false);
        public bool LockPanning
        {
            get { return (bool)GetValue(LockPanningProperty); }
            set { SetValue(LockPanningProperty, value); }
        }



        #endregion

    }
}
