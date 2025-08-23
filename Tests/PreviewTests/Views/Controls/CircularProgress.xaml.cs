using System.Windows.Input;

namespace PreviewTests.Views.Controls;

public partial class CircularProgress : SkiaLayout
{
    protected RangeAnimator _animatorValue;

    protected double LastValue = 0;

    public CircularProgress()
    {
        InitializeComponent();

        ShapePath = FindView<SkiaShape>("ShapePath");
        ShapeProgress = FindView<SkiaShape>("ShapeProgress");
        LabelProgress = FindView<SkiaLabel>("LabelProgress");

        //ApplyOptions();
    }

    public virtual void ApplyOptions()
    {
        LabelProgress.FontFamily = this.FontFamily;
        LabelProgress.AutoSizeText = this.AutoSizeText;
        LabelProgress.TextColor = this.FontColor;
        ShapePath.StrokeColor = this.PathColor;
        ShapeProgress.StrokeColor = this.AccentColor;
        ApplyProgress();
    }

    SkiaLabel LabelProgress;
    SkiaShape ShapeProgress;
    SkiaShape ShapePath;

    bool invalidateProcessed;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        invalidateProcessed = false;

        ApplyOptions();
    }

    protected void InitializeAnimator()
    {
        if (_animatorValue == null)
        {
            _animatorValue = new RangeAnimator(this)
            {
                mMinValue = 0,
                mMaxValue = 360,
                OnStop = () =>
                {
                    CommandOnAnimated?.Execute(this);
                    if (_animatorValue.mValue == 360)
                    {
                        CommandOnFinish?.Execute(this);
                    }
                }
            };
        }
    }

    protected void UpdateProgressText(double value)
    {
        LabelProgress.Text = string.Format(this.TextFormat, value);
    }

    public virtual void ApplyProgress()
    {
        if (LayoutReady && Parent != null) //can animate
        {
            var value = this.Value;

            if (LastValue != value)
            {
                InitializeAnimator();

                var start = LastValue;
                var end = value;

                LastValue = value;

                if (_animatorValue.IsRunning)
                {
                    _animatorValue
                        .SetSpeed(TransitionSpeedMs)
                        .SetValue(end);
                }
                else
                {
                    _animatorValue.Start(
                        (v) =>
                        {
                            var angle = 360 / 100.0 * v;
                            ShapeProgress.Value2 = angle;
                            UpdateProgressText(v);
                        },
                        start, end, (uint)TransitionSpeedMs, Easing.Linear);
                }
            }
        }
        else
        {
            ShapeProgress.Value2 = 360 / 100.0 * this.Value;
            UpdateProgressText(Value);
        }
    }


    private double _EndAngle;
    public double EndAngle
    {
        get
        {
            return _EndAngle;
        }
        set
        {
            if (_EndAngle != value)
            {
                _EndAngle = value;
                OnPropertyChanged();
            }
        }
    }


    protected static void NeedApplyOptions(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is CircularProgress control)
        {
            control.ApplyOptions();
        }
    }

    protected static void NeedApplyProgress(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is CircularProgress control)
        {
            control.ApplyProgress();
        }
    }

    public static readonly BindableProperty AccentColorProperty = BindableProperty.Create(
        nameof(AccentColor),
        typeof(Color),
        typeof(CircularProgress),
        Colors.Transparent,
        propertyChanged: NeedApplyOptions);

    public Color AccentColor
    {
        get { return (Color)GetValue(AccentColorProperty); }
        set { SetValue(AccentColorProperty, value); }
    }

    public static readonly BindableProperty TransitionSpeedMsProperty = BindableProperty.Create(
        nameof(TransitionSpeedMs),
        typeof(int),
        typeof(CircularProgress),
        400);

    /// <summary>
    /// To be applied to next value change
    /// </summary>
    public int TransitionSpeedMs
    {
        get { return (int)GetValue(TransitionSpeedMsProperty); }
        set { SetValue(TransitionSpeedMsProperty, value); }
    }

    public static readonly BindableProperty CommandOnFinishProperty = BindableProperty.Create(
        nameof(CommandOnFinish),
        typeof(ICommand),
        typeof(CircularProgress),
        null);

    public ICommand CommandOnFinish
    {
        get { return (ICommand)GetValue(CommandOnFinishProperty); }
        set { SetValue(CommandOnFinishProperty, value); }
    }

    public static readonly BindableProperty CommandOnAnimatedProperty = BindableProperty.Create(
        nameof(CommandOnAnimated),
        typeof(ICommand),
        typeof(CircularProgress),
        null);

    public ICommand CommandOnAnimated
    {
        get { return (ICommand)GetValue(CommandOnAnimatedProperty); }
        set { SetValue(CommandOnAnimatedProperty, value); }
    }


    public static readonly BindableProperty PathColorProperty = BindableProperty.Create(
        nameof(PathColor),
        typeof(Color),
        typeof(CircularProgress),
        Colors.Transparent,
        propertyChanged: NeedApplyOptions);

    public Color PathColor
    {
        get { return (Color)GetValue(PathColorProperty); }
        set { SetValue(PathColorProperty, value); }
    }

    public static readonly BindableProperty FontColorProperty = BindableProperty.Create(
        nameof(FontColor),
        typeof(Color),
        typeof(CircularProgress),
        Colors.Red,
        propertyChanged: NeedApplyOptions);

    public Color FontColor
    {
        get { return (Color)GetValue(FontColorProperty); }
        set { SetValue(FontColorProperty, value); }
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(CircularProgress),
        defaultValue: string.Empty,
        propertyChanged: NeedApplyOptions);

    public string FontFamily
    {
        get { return (string)GetValue(FontFamilyProperty); }
        set { SetValue(FontFamilyProperty, value); }
    }

    public static readonly BindableProperty TextFormatProperty = BindableProperty.Create(
        nameof(TextFormat), typeof(string), typeof(CircularProgress),
        "{0:0}%",
        propertyChanged: NeedApplyOptions);
    public string TextFormat
    {
        get { return (string)GetValue(TextFormatProperty); }
        set { SetValue(TextFormatProperty, value); }
    }


    public static readonly BindableProperty AutoSizeTextProperty = BindableProperty.Create(
        nameof(AutoSizeText), typeof(string), typeof(CircularProgress),
        null,
        propertyChanged: NeedApplyOptions);
    public string AutoSizeText
    {
        get { return (string)GetValue(AutoSizeTextProperty); }
        set { SetValue(AutoSizeTextProperty, value); }
    }

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(double),
        typeof(CircularProgress),
        0.0,
        propertyChanged: NeedApplyProgress);

    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }

}
