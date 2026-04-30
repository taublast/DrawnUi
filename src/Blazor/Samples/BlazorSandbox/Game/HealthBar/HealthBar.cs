using System.Runtime.CompilerServices;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace SpaceShooter.Game;

public partial class HealthBar : SkiaShape
{
    private SignalInverter Inverter;
    private SkiaGradient _inverterGradient;

    public HealthBar()
    {
        var ThisControlBar = this;

        StrokeColor = Colors.Black;
        Tag = "Health";
        BackgroundColor = Color.FromHex("#000066");
        StrokeWidth = 0.5;
        CornerRadius = 3;
        HeightRequest = 6;
        HorizontalOptions = LayoutOptions.Fill;

        IgnoreChildrenInvalidations = true;

        FillGradient = new SkiaGradient()
        {
            Type = GradientType.Linear,
            StartXRatio = 0, StartYRatio = 0, EndXRatio = 1, EndYRatio = 0,
            Opacity = 0.60f,
            Colors = new List<Color>
            {
                Color.FromHex("#ee281D"),
                Color.FromHex("#F0E70B"),
                Color.FromHex("#65FF10"),
                Color.FromHex("#65FF10"),
            },
            ColorPositions = new List<double> { 0.0, 0.3, 0.4, 1.0 },
        };

        StrokeGradient = new SkiaGradient()
        {
            Type = GradientType.Linear,
            StartXRatio = 0.2f, StartYRatio = 0.2f, EndXRatio = 0.2f, EndYRatio = 0.8f,
            Colors = new List<Color>
            {
                Color.FromHex("#000022"),
                Color.FromHex("#42464B"),
            },
        };

        _inverterGradient = new SkiaGradient()
        {
            Type = GradientType.Linear,
            StartXRatio = 0, StartYRatio = 0, EndXRatio = 1, EndYRatio = 0,
            Opacity = 1,
            Colors = new List<Color>
            {
                Color.FromHex("#00000022"),
                Color.FromHex("#000022"),
                Color.FromHex("#000022"),
            },
        };

        Children = new List<SkiaControl>()
        {
            new SignalInverter()
            {
                BackgroundColor = Colors.Black,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Fill,
                ZIndex = 100,
                FillGradient = _inverterGradient,
            }.Assign(out Inverter),
        };

        Inverter.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SignalInverter.Points))
            {
                _inverterGradient.ColorPositions = Inverter.Points;
            }
        };
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
