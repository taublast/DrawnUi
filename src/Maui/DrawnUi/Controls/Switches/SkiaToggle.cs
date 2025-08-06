using System.Windows.Input;

namespace DrawnUi.Draw;

/// <summary>
/// Base control for toggling between 2 states.
/// It provides no gestures support by itsself.
/// </summary>
public class SkiaToggle : SkiaLayout
{
    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type == TouchActionResult.Tapped)
        {
            IsToggled = !IsToggled;
            return this;
        }

        return base.ProcessGestures(args, apply);
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ApplyProperties();
    }

    /// <summary>
    /// Base calls ApplyProperties()
    /// </summary>
    protected virtual void OnToggledChanged()
    {
        ApplyProperties();

        NotifyWasToggled();

        IsInternalCall = false;
    }

    protected bool IsInternalCall;

    protected virtual void ChangeDefaultValue()
    {
        IsInternalCall = true;

        IsToggled = DefaultValue;
    }


    protected virtual void NotifyWasToggled()
    {
        if (IsInternalCall)
        {
            return;
        }

        Toggled?.Invoke(this, IsToggled);

        CommandToggled?.Execute(IsToggled);
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        Toggled = null;
    }

    public event EventHandler<bool> Toggled;

    /// <summary>
    /// Base call Update()
    /// </summary>
    public virtual void ApplyProperties()
    {
        Update();
    }

    #region PROPERTIES

    public static readonly BindableProperty CommandToggledProperty = BindableProperty.Create(nameof(CommandToggled), typeof(ICommand),
        typeof(SkiaToggle),
        null);
    public ICommand CommandToggled
    {
        get { return (ICommand)GetValue(CommandToggledProperty); }
        set { SetValue(CommandToggledProperty, value); }
    }

    public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(
        nameof(IsToggled),
        typeof(bool),
        typeof(SkiaToggle),
        false, propertyChanged: WasToggled, defaultBindingMode: BindingMode.TwoWay);

    private static void WasToggled(BindableObject bindable, object oldvalue, object newvalue)
    {
        var control = bindable as SkiaToggle;
        control?.OnToggledChanged();
    }

    public bool IsToggled
    {
        get { return (bool)GetValue(IsToggledProperty); }
        set { SetValue(IsToggledProperty, value); }
    }

    public static readonly BindableProperty DefaultValueProperty = BindableProperty.Create(
        nameof(DefaultValue),
        typeof(bool),
        typeof(SkiaToggle),
        false, propertyChanged: NeedChangeDefault);

    private static void NeedChangeDefault(BindableObject bindable, object oldvalue, object newvalue)
    {
        var control = bindable as SkiaToggle;
        control?.ChangeDefaultValue();
    }

    public bool DefaultValue
    {
        get { return (bool)GetValue(DefaultValueProperty); }
        set { SetValue(DefaultValueProperty, value); }
    }

    public static readonly BindableProperty RespondsToGesturesProperty = BindableProperty.Create(
        nameof(RespondsToGestures),
        typeof(bool),
        typeof(SkiaToggle),
        true);

    public bool RespondsToGestures
    {
        get { return (bool)GetValue(RespondsToGesturesProperty); }
        set { SetValue(RespondsToGesturesProperty, value); }
    }

    public static readonly BindableProperty CommandTappedProperty = BindableProperty.Create(
        nameof(CommandTapped),
        typeof(bool),
        typeof(SkiaToggle),
        false);

    public bool CommandTapped
    {
        get { return (bool)GetValue(CommandTappedProperty); }
        set { SetValue(CommandTappedProperty, value); }
    }

    public static readonly BindableProperty CommandTappedParameterProperty = BindableProperty.Create(
        nameof(CommandTappedParameter),
        typeof(bool),
        typeof(SkiaToggle),
        false);

    public bool CommandTappedParameter
    {
        get { return (bool)GetValue(CommandTappedParameterProperty); }
        set { SetValue(CommandTappedParameterProperty, value); }
    }

    protected static void NeedUpdateProperties(BindableObject bindable, object oldvalue, object newvalue)
    {
        var control = bindable as SkiaToggle;
        control?.ApplyProperties();
    }


    public static readonly BindableProperty ColorThumbOnProperty = BindableProperty.Create(
        nameof(ColorThumbOn),
        typeof(Color),
        typeof(SkiaToggle),
        Colors.White, propertyChanged: NeedUpdateProperties);

    public Color ColorThumbOn
    {
        get { return (Color)GetValue(ColorThumbOnProperty); }
        set { SetValue(ColorThumbOnProperty, value); }
    }

    public static readonly BindableProperty ColorFrameOnProperty = BindableProperty.Create(
        nameof(ColorFrameOn),
        typeof(Color),
        typeof(SkiaToggle),
        Colors.Red, propertyChanged: NeedUpdateProperties);

    public Color ColorFrameOn
    {
        get { return (Color)GetValue(ColorFrameOnProperty); }
        set { SetValue(ColorFrameOnProperty, value); }
    }

    public static readonly BindableProperty ColorThumbOffProperty = BindableProperty.Create(
        nameof(ColorThumbOff),
        typeof(Color),
        typeof(SkiaToggle),
        Colors.White, propertyChanged: NeedUpdateProperties);

    public Color ColorThumbOff
    {
        get { return (Color)GetValue(ColorThumbOffProperty); }
        set { SetValue(ColorThumbOffProperty, value); }
    }

    public static readonly BindableProperty ColorFrameOffProperty = BindableProperty.Create(
        nameof(ColorFrameOff),
        typeof(Color),
        typeof(SkiaToggle),
        Colors.DarkGray, propertyChanged: NeedUpdateProperties);

    public Color ColorFrameOff
    {
        get { return (Color)GetValue(ColorFrameOffProperty); }
        set { SetValue(ColorFrameOffProperty, value); }
    }

    public static readonly BindableProperty IsAnimatedProperty = BindableProperty.Create(
        nameof(IsAnimated),
        typeof(bool),
        typeof(SkiaToggle),
        true);

    public bool IsAnimated
    {
        get { return (bool)GetValue(IsAnimatedProperty); }
        set { SetValue(IsAnimatedProperty, value); }
    }



    #endregion
}
