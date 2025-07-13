using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public class SliderTrail : SkiaShape
{
    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        Debug.WriteLine("[SliderTrail] gestures hit");

        return base.ProcessGestures(args, apply);
    }

    public static readonly BindableProperty XPosProperty = BindableProperty.Create(nameof(XPos), typeof(double), typeof(SliderTrail), 0.0); //, BindingMode.TwoWay
    public double XPos
    {
        get { return (double)GetValue(XPosProperty); }
        set { SetValue(XPosProperty, value); }
    }



    public static readonly BindableProperty XPosEndProperty = BindableProperty.Create(nameof(XPosEnd), typeof(double), typeof(SliderTrail), 100.0); //, BindingMode.TwoWay
    public double XPosEnd
    {
        get { return (double)GetValue(XPosEndProperty); }
        set { SetValue(XPosEndProperty, value); }
    }

    public static readonly BindableProperty ModifyXPosEndProperty = BindableProperty.Create(nameof(ModifyXPosEnd), typeof(double), typeof(SliderTrail), 0.0); //, BindingMode.TwoWay
    public double ModifyXPosEnd
    {
        get { return (double)GetValue(ModifyXPosEndProperty); }
        set { SetValue(ModifyXPosEndProperty, value); }
    }

    public static readonly BindableProperty ModifyXPosStartProperty = BindableProperty.Create(nameof(ModifyXPosStart), typeof(double), typeof(SliderTrail), 0.0); //, BindingMode.TwoWay
    public double ModifyXPosStart
    {
        get { return (double)GetValue(ModifyXPosStartProperty); }
        set { SetValue(ModifyXPosStartProperty, value); }
    }

    public static readonly BindableProperty SideOffsetProperty = BindableProperty.Create(nameof(SideOffset), typeof(double), typeof(SliderTrail), 5.0);
    public double SideOffset
    {
        get { return (double)GetValue(SideOffsetProperty); }
        set { SetValue(SideOffsetProperty, value); }
    }


    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(XPosEnd)
            || propertyName == nameof(ModifyXPosEnd)
            || propertyName == nameof(ModifyXPosStart)
            || propertyName == nameof(XPos)
            || propertyName == nameof(SideOffset))
        {

            var offsetLeft = XPos + SideOffset + ModifyXPosStart;
            var offsetRight = ModifyXPosEnd;

            this.Margin = new Thickness(
                offsetLeft,
                0,
                offsetRight,
                0);

            this.WidthRequest = XPosEnd - XPos + (ModifyXPosEnd*2 + ModifyXPosStart*2);
        }

    }

}
