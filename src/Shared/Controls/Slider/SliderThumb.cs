using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public class SliderThumb : SkiaLayout
{

    public static readonly BindableProperty XCenterProperty = BindableProperty.Create(nameof(XCenter), typeof(double), typeof(SliderThumb), 0.0); //, BindingMode.TwoWay
    public double XCenter
    {
        get { return (double)GetValue(XCenterProperty); }
        set { SetValue(XCenterProperty, value); }
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(TranslationX) ||
            propertyName == nameof(Width))
        {
            var width = this.Width;
            XCenter = TranslationX + width / 2;
        }

    }
}