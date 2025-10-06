using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DrawnUi.Draw;

/// <summary>
/// For fast and lazy gestures handling to attach to dran controls inside the canvas only
/// </summary>
public static partial class AddGestures
{
    public class GestureListener : SkiaControl, ISkiaGestureListener
    {
        public new string Tag
        {
            get { return $"AddGestures:{_parent.Tag}"; }
            set { }
        }

        public override bool CanDraw
        {
            get
            {
                if (_parent == null)
                    return false;

                return _parent.CanDraw;
            }
        }

        public override DrawnView GetTopParentView()
        {
            if (_parent != null)
            {
                return _parent.Superview;
            }

            return null;
        }

        private readonly SkiaControl _parent;

        public GestureListener(SkiaControl control)
        {
            _parent = control;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool HitIsInside(float x, float y)
        {
            if (_parent == null)
                return false;

            return _parent.HitIsInside(x, y);
        }

        public override SKRect HitBoxAuto
        {
            get { return _parent.HitBoxAuto; }
        }

        public override SKPoint TranslateInputCoords(SKPoint childOffset, bool accountForCache = true)
        {
            return _parent.TranslateInputCoords(childOffset, accountForCache);
        }

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            if (_parent == null || !_parent.CanDraw)
                return null;

            if (_parent is ISkiaGestureListener listener)
            {
                var consumed = listener.OnSkiaGestureEvent(args, apply);
                if (consumed != null)
                {
                    return consumed;
                }
            }

            if (args.Type == TouchActionResult.LongPressing)
            {
                var command = GetCommandLongPressing(_parent);
                if (command != null)
                {
                    var parameter = GetCommandLongPressingParameter(_parent);
                    if (parameter == null)
                        parameter = _parent.BindingContext;
                    Tasks.StartDelayed(TimeSpan.FromMilliseconds(CommandDelayMs), () => { command?.Execute(parameter); });
                    return this;
                }
            }
            else if (args.Type == TouchActionResult.Down)
            {
                var anim = GetAnimationPressed(_parent);
                PlayTouchAnimation(anim, args, apply);

                var pressed = GetCommandPressed(_parent);
                if (pressed != null)
                {
                    var parameter = _parent.BindingContext;
                    ;
                    pressed?.Execute(parameter);
                    //return this;
                }
            }
            else if (args.Type == TouchActionResult.Tapped)
            {
                var anim = GetAnimationTapped(_parent);

                PlayTouchAnimation(anim, args, apply);

                var command = GetCommandTapped(_parent);
                if (command != null)
                {
                    var parameter = GetCommandTappedParameter(_parent);
                    //Trace.WriteLine($"[CommandTapped] ctx - {_parent.BindingContext} - {parameter}");
                    if (parameter == null)
                        parameter = _parent.BindingContext;
                    Tasks.StartDelayed(TimeSpan.FromMilliseconds(CommandDelayMs), () => { command?.Execute(parameter); });
                    return this;
                }
            }
            else if (args.Type == TouchActionResult.Panning)
            {
                if (GetLockPanning(_parent))
                {
                    return this;
                }
            }


            return base.ProcessGestures(args, apply);
        }

        public virtual bool OnFocusChanged(bool focus)
        {
            return false;
        }


        public void PlayTouchAnimation(SkiaTouchAnimation anim, SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            if (anim != SkiaTouchAnimation.None)
            {
                var view = GetTransformView(_parent);
                if (view == null)
                    view = _parent;

                if (view is IHasAfterEffects hasEffects)
                {
                    var thisOffset = TranslateInputCoords(apply.ChildOffset, false);
                    var pixX = args.Event.Location.X + thisOffset.X;
                    var pixY = args.Event.Location.Y + thisOffset.Y;
                    var x = pixX / RenderingScale;
                    var y = pixY / RenderingScale;

                    var color = GetTouchEffectColor(_parent);
                    if (anim == SkiaTouchAnimation.Ripple)
                    {
                        var ptsInsideControl =
                            hasEffects.GetOffsetInsideControlInPoints(args.Event.Location, apply.ChildOffset);
                        hasEffects.PlayRippleAnimation(color, ptsInsideControl.X, ptsInsideControl.Y);
                    }
                    else if (anim == SkiaTouchAnimation.Shimmer)
                    {
                        hasEffects.PlayShimmerAnimation(color, 150, 33, 500);
                    }
                }
            }
        }
    }

    public static int CommandDelayMs = 6;

    public static Dictionary<SkiaControl, GestureListener> AttachedListeners = new();

    private static void OnAttachableChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is SkiaControl control)
        {
            ManageRegistration(control);
        }
    }

    static object _lockListeners = new();

    static void ManageListener(SkiaControl control, bool attach)
    {
        lock (_lockListeners)
        {
            if (control.Parent != null)
            {
                if (attach)
                {
                    if (!AttachedListeners.ContainsKey(control))
                    {
                        GestureListener gestureListener = new GestureListener(control);
                        AttachedListeners.TryAdd(control, gestureListener);
                        //will throw if parent null
                        control.GesturesEffect = gestureListener;
                        control.Parent.RegisterGestureListener(gestureListener);
                    }
                }
                else
                {
                    if (AttachedListeners.ContainsKey(control))
                    {
                        AttachedListeners.Remove(control, out var gestureListener);
                        control.GesturesEffect = null;
                        if (control.Parent != null)
                        {
                            control.Parent.UnregisterGestureListener(gestureListener);
                        }
                    }
                }
            }
        }
    }

    public static void ManageRegistration(SkiaControl control)
    {
        if (control.Parent != null)
        {
            ManageListener(control, IsActive(control));
        }

        control.ParentChanged -= OnParentChanged;
        control.ParentChanged += OnParentChanged;
    }

    private static void OnParentChanged(object sender, IDrawnBase parent)
    {
        if (sender is SkiaControl control)
        {
            control.ParentChanged -= OnParentChanged;
            if (parent == null)
            {
                ManageListener(control, false);
            }
            else
            {
                ManageListener(control, IsActive(control));
            }
        }
    }

    public static bool IsActive(SkiaControl listener)
    {
        if (listener is SkiaControl control)
        {
            ICommand command = GetCommandTapped(control);
            var pressed = GetCommandPressed(control);
            var effect2 = GetAnimationPressed(control);
            var effect = GetAnimationTapped(control);
            var needAttach = command != null || pressed != null
                                             || effect != SkiaTouchAnimation.None
                                             || effect2 != SkiaTouchAnimation.None
                                             || GetCommandLongPressing(control) != null || GetLockPanning(control);
            return needAttach;
        }

        return false;
    }

    public static readonly BindableProperty CommandTappedProperty =
        BindableProperty.CreateAttached(
            "CommandTapped",
            typeof(ICommand),
            typeof(AddGestures),
            null,
            propertyChanged: OnAttachableChanged);

    public static ICommand GetCommandTapped(BindableObject view)
    {
        return (ICommand)view.GetValue(CommandTappedProperty);
    }

    public static void SetCommandTapped(BindableObject view, ICommand value)
    {
        //Trace.WriteLine($"[SetCommandTapped] to {value}");

        view.SetValue(CommandTappedProperty, value);
    }

    public static readonly BindableProperty CommandTappedParameterProperty =
        BindableProperty.CreateAttached(
            "CommandTappedParameter",
            typeof(object),
            typeof(AddGestures),
            null);

    public static object GetCommandTappedParameter(BindableObject view)
    {
        return view.GetValue(CommandTappedParameterProperty);
    }

    public static void SetCommandTappedParameter(BindableObject view, object value)
    {
        //Trace.WriteLine($"[SetCommandTappedParameter] to {value}");

        view.SetValue(CommandTappedParameterProperty, value);
    }

    public static readonly BindableProperty CommandLongPressingProperty =
        BindableProperty.CreateAttached(
            "CommandLongPressing",
            typeof(ICommand),
            typeof(AddGestures),
            null,
            propertyChanged: OnAttachableChanged);

    public static ICommand GetCommandLongPressing(BindableObject view)
    {
        return (ICommand)view.GetValue(CommandLongPressingProperty);
    }

    public static void SetCommandLongPressing(BindableObject view, ICommand value)
    {
        view.SetValue(CommandLongPressingProperty, value);
    }


    public static readonly BindableProperty CommandLongPressingParameterProperty =
        BindableProperty.CreateAttached(
            "CommandLongPressingParameter",
            typeof(object),
            typeof(AddGestures),
            null);

    public static object GetCommandLongPressingParameter(BindableObject view)
    {
        return view.GetValue(CommandLongPressingParameterProperty);
    }

    public static void SetCommandLongPressingParameter(BindableObject view, object value)
    {
        view.SetValue(CommandLongPressingParameterProperty, value);
    }

    public static readonly BindableProperty TransformViewProperty =
        BindableProperty.CreateAttached(
            "TransformView",
            typeof(object),
            typeof(AddGestures),
            null);

    public static object GetTransformView(BindableObject view)
    {
        return view.GetValue(TransformViewProperty);
    }

    public static void SetTransformView(BindableObject view, object value)
    {
        view.SetValue(TransformViewProperty, value);
    }

    public static readonly BindableProperty AnimationTappedProperty =
        BindableProperty.CreateAttached(
            "AnimationTapped",
            typeof(SkiaTouchAnimation),
            typeof(AddGestures),
            null,
            propertyChanged: OnAttachableChanged);

    public static SkiaTouchAnimation GetAnimationTapped(BindableObject view)
    {
        var anim = (SkiaTouchAnimation)view.GetValue(AnimationTappedProperty);
        if (anim == SkiaTouchAnimation.None && view is SkiaControl skia)
        {
            anim = skia.AnimationTapped;
        }

        return anim;
    }

    public static void SetAnimationTapped(BindableObject view, SkiaTouchAnimation value)
    {
        view.SetValue(AnimationTappedProperty, value);
    }

    public static readonly BindableProperty CommandPressedProperty =
        BindableProperty.CreateAttached(
            "CommandPressed",
            typeof(ICommand),
            typeof(AddGestures),
            null,
            propertyChanged: OnAttachableChanged);

    public static ICommand GetCommandPressed(BindableObject view)
    {
        return (ICommand)view.GetValue(CommandPressedProperty);
    }

    public static void SetCommandPressed(BindableObject view, ICommand value)
    {
        //Trace.WriteLine($"[SetCommandPressed] to {value}");

        view.SetValue(CommandPressedProperty, value);
    }

    public static readonly BindableProperty AnimationPressedProperty =
        BindableProperty.CreateAttached(
            "AnimationPressed",
            typeof(SkiaTouchAnimation),
            typeof(AddGestures),
            null,
            propertyChanged: OnAttachableChanged);

    public static SkiaTouchAnimation GetAnimationPressed(BindableObject view)
    {
        return (SkiaTouchAnimation)view.GetValue(AnimationPressedProperty);
    }

    public static void SetAnimationPressed(BindableObject view, SkiaTouchAnimation value)
    {
        view.SetValue(AnimationPressedProperty, value);
    }

    public static readonly BindableProperty TouchEffectColorProperty =
        BindableProperty.CreateAttached(
            "TouchEffectColor",
            typeof(Color),
            typeof(AddGestures),
            Colors.White);

    public static Color GetTouchEffectColor(BindableObject view)
    {
        var color = (Color)view.GetValue(TouchEffectColorProperty);

        if (color == Colors.Transparent && view is SkiaControl skia)
        {
            color = skia.TouchEffectColor;
        }

        return color;
    }

    public static void SetTouchEffectColor(BindableObject view, Color value)
    {
        view.SetValue(TouchEffectColorProperty, value);
    }


    public static readonly BindableProperty LockPanningProperty =
        BindableProperty.CreateAttached(
            "LockPanning",
            typeof(bool),
            typeof(AddGestures),
            null,
            propertyChanged: OnAttachableChanged);

    public static bool GetLockPanning(BindableObject view)
    {
        return (bool)view.GetValue(LockPanningProperty);
    }

    public static void SetLockPanning(BindableObject view, bool value)
    {
        view.SetValue(LockPanningProperty, value);
    }
}
