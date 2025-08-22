using System.Runtime.CompilerServices;

namespace SpaceShooter.Game;

public partial class HealthBar : SkiaShape
{
    public HealthBar()
    {
        InitializeComponent();
    }

    void UpdateControl()
    {
        Inverter.Value = GetValueForInverter();

        Update();
    }

    double GetValueForInverter()
    {
        return (this.Value - this.Min) / this.Max;
    }

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        Inverter.Invalidate();
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(Max)
            || propertyName == nameof(Min)
            || propertyName == nameof(Value)
            )
        {
            UpdateControl();
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
            Base = 0.25
        },  
        //3
        new ()
        {
            Base = 0.5
        },  
        //4
        new ()
        {
            Base = 0.95
        },
        //5
		new ()
        {
            Stick = 0.05,
            Base = 1.0
        },  
        //6
        new ()
        {
            IsFixed = true,
            Base = 1.0
        },
    };


    private double _InverterWidth;
    public double InverterWidth
    {
        get
        {
            return _InverterWidth;
        }
        set
        {
            if (_InverterWidth != value)
            {
                _InverterWidth = value;
                OnPropertyChanged();
            }
        }
    }


    public double[] Points
    {
        get
        {
            return GradientPoints.Select(point => point.Value).ToArray();
        }
    }

    public static readonly BindableProperty MaxProperty =
        BindableProperty.Create(nameof(Max),
            typeof(double),
            typeof(HealthBar),
            100.0);
    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly BindableProperty MinProperty =
        BindableProperty.Create(nameof(Min),
            typeof(double),
            typeof(HealthBar),
            0.0);
    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value),
            typeof(double),
            typeof(HealthBar),
            0.0);

    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }



}
