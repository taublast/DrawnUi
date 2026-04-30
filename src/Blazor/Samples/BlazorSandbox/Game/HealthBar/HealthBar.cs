using System.Runtime.CompilerServices;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace SpaceShooter.Game;

public partial class HealthBar : SkiaShape
{
    public HealthBar()
    {
        var ThisControlBar = this;

        StrokeColor = Colors.Black;
        Tag = "Health";
        BackgroundColor = "#000066".ToColor();
        StrokeWidth = 0.5;
        CornerRadius = 3;
        HeightRequest = 6;
        HorizontalOptions = LayoutOptions.Fill;
        
        IgnoreChildrenInvalidations = true;

        Children = new List<SkiaControl>()
        {
            /*
 
       <draw:SkiaShape.FillGradient>
   
           <draw:SkiaGradient
               EndXRatio="1"
               EndYRatio="0"
               Opacity="0.60"
               StartXRatio="0"
               StartYRatio="0"
               Type="Linear">
   
               <draw:SkiaGradient.Colors>
                   <Color>#ee281D</Color>
                   <Color>#F0E70B</Color>
                   <Color>#65FF10</Color>
                   <Color>#65FF10</Color>
               </draw:SkiaGradient.Colors>
   
               <draw:SkiaGradient.ColorPositions>
                   <x:Double>0.0</x:Double>
                   <x:Double>0.3</x:Double>
                   <x:Double>0.4</x:Double>
                   <x:Double>1.0</x:Double>
               </draw:SkiaGradient.ColorPositions>
   
           </draw:SkiaGradient>
   
       </draw:SkiaShape.FillGradient>
   
       <draw:SkiaShape.StrokeGradient>
   
           <draw:SkiaGradient
               EndXRatio="0.2"
               EndYRatio="0.8"
               StartXRatio="0.2"
               StartYRatio="0.2"
               Type="Linear">
               <draw:SkiaGradient.Colors>
                   <Color>#000022</Color>
                   <Color>#42464B</Color>
               </draw:SkiaGradient.Colors>
           </draw:SkiaGradient>
   
       </draw:SkiaShape.StrokeGradient>
   
       <!--  INVERTED PLUS <=  -->
       <!--
           "{Binding Source={x:Reference ThisControlBar},
           Path=Value}"
           ColorPositions="{Binding Source={x:Reference ThisControlBar}, Path=Points}"
       -->
       <spaceShooter:SignalInverter
           x:Name="Inverter"
           BackgroundColor="Black"
           HorizontalOptions="End"
           VerticalOptions="Fill"
           ZIndex="100">
   
           <draw:SkiaControl.FillGradient>
               <draw:SkiaGradient
                   ColorPositions="{Binding Source={x:Reference Inverter}, Path=Points}"
                   EndXRatio="1"
                   EndYRatio="0"
                   Opacity="1"
                   StartXRatio="0"
                   StartYRatio="0"
                   Type="Linear">
                   <draw:SkiaGradient.Colors>
                       <Color>#00000022</Color>
                       <Color>#000022</Color>
                       <Color>#000022</Color>
                   </draw:SkiaGradient.Colors>
   
                   <!--<draw:SkiaGradient.ColorPositions>
                               <x:Double>0.0</x:Double>
                               <x:Double>0.05</x:Double>
                               <x:Double>1.0</x:Double>
                           </draw:SkiaGradient.ColorPositions>-->
   
               </draw:SkiaGradient>
           </draw:SkiaControl.FillGradient>
   
       </spaceShooter:SignalInverter>
 
 */
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
