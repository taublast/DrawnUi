using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public class SliderValueDesc : SkiaLayout
{

    public static readonly BindableProperty XCenterProperty = BindableProperty.Create(nameof(XCenter), typeof(double), typeof(SliderValueDesc), 0.0); //, BindingMode.TwoWay
    public double XCenter
    {
        get { return (double)GetValue(XCenterProperty); }
        set { SetValue(XCenterProperty, value); }
    }


    public static readonly BindableProperty XMaxLimitProperty = BindableProperty.Create(nameof(XMaxLimit), typeof(double), typeof(SliderValueDesc), 0.0); //, BindingMode.TwoWay
    public double XMaxLimit
    {
        get { return (double)GetValue(XMaxLimitProperty); }
        set { SetValue(XMaxLimitProperty, value); }
    }


    public static readonly BindableProperty XMinLimitProperty = BindableProperty.Create(nameof(XMinLimit), typeof(double), typeof(SliderValueDesc), 0.0); //, BindingMode.TwoWay
    public double XMinLimit
    {
        get { return (double)GetValue(XMinLimitProperty); }
        set { SetValue(XMinLimitProperty, value); }
    }



    public static readonly BindableProperty RightXProperty = BindableProperty.Create(nameof(RightX), typeof(double), typeof(SliderValueDesc), 0.0); //, BindingMode.TwoWay
    public double RightX
    {
        get { return (double)GetValue(RightXProperty); }
        set { SetValue(RightXProperty, value); }
    }


    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName.IsEither(nameof(TranslationX), nameof(Width)))
        {

            var right = TranslationX + Width;
            if (right < 0)
                right = 0;
            RightX = right;
        }

        if (propertyName == nameof(XCenter) ||
            propertyName == nameof(XMinLimit) ||
            propertyName == nameof(XMaxLimit) ||
            propertyName == nameof(Width))
        {

            var width = this.Width;
            var xc = XCenter;
            var maybe = xc - width / 2;
            var xm = XMinLimit;
            var xmx = XMaxLimit;

            if (maybe < xm)
            {
                maybe = xm;
            }
            else
            if (maybe >= xmx - width)
            {
                maybe = xmx - width;
            }

            this.TranslationX = maybe;
        }
    }


}