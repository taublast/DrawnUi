namespace SpaceShooter.Game;

public class SignalInverter : SkiaShape
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value),
        typeof(double),
        typeof(SignalInverter),
        0.0, propertyChanged: OnValueChanged);
    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }

    private static void OnValueChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SignalInverter control)
        {
            control.UpdateValue();
        }
    }

    private float _widthConstraint;

    private bool _invalidateProcessed;

    protected override void OnMeasured()
    {
        base.OnMeasured();

        _invalidateProcessed = false;

        UpdateValue();
    }

    protected RangeAnimator _animatorValue;

    protected double LastValue = 0;

    protected void InitializeAnimator()
    {
        if (_animatorValue == null)
        {
            _animatorValue = new(this)
            {
                OnStop = () =>
                {
                    //SetValue(Value);
                }
            };
        }
    }

    public void SetValue(double v)
    {
        if (v > 1)
            v = 1;

        _widthConstraint = Parent.MeasuredSize.Units.Width;

        var value = _widthConstraint - _widthConstraint * v;

        WidthRequest = value;

        //var ratio = value / _widthConstraint;

        var max = 1.0 - v;

        foreach (var point in GradientPoints)
        {
            if (point.IsFixed)
            {
                point.Value = point.Base;
                continue;
            }

            if (max >= point.Base)
                point.Value = point.Base;
            else
                point.Value = max;

            if (point.Stick > 0)
            {
                point.Value = point.Stick + point.Stick / max;
                if (point.Value > 1)
                    point.Value = 1;
            }
        }

        OnPropertyChanged(nameof(Points));
    }

    public void UpdateValue()
    {
        if (Parent == null)
            return;

        if (!_invalidateProcessed)
        {
            _invalidateProcessed = true;
            LastValue = Value;
            SetValue(Value);
            return;
        }

        if (LastValue != Value)
        {
            InitializeAnimator();

            var start = LastValue;
            var end = Value;

            LastValue = Value;

            if (_animatorValue.IsRunning)
            {
                _animatorValue
                    .SetSpeed(400)
                    .SetValue(end);
            }
            else
            {
                _animatorValue.Start(
                    (value) =>
                    {
                        SetValue(value);
                    },
                    start, end, 400, Easing.SpringOut);
            }

        }

    }

    public static FloatingPosition[] GradientPoints =
    {
        //1
        new ()
        {
            IsFixed = true,
            Base = 0.0
        },
        //2
        new ()
        {
            Stick = 0.033,
            Base = 0.033
        },  
        //3
        new ()
        {
            IsFixed = true,
            Base = 1.0
        },
    };

    public double[] Points
    {
        get
        {
            return GradientPoints.Select(point => point.Value).ToArray();
        }
    }
}
