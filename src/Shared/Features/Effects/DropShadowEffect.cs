namespace DrawnUi.Draw;

public class DropShadowEffect : BaseImageFilterEffect
{

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(DropShadowEffect),
        Colors.Black,
        propertyChanged: NeedUpdate);
    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

    public static readonly BindableProperty BlurProperty = BindableProperty.Create(nameof(Blur), typeof(double), typeof(DropShadowEffect),
        5.0,
        propertyChanged: NeedUpdate);
    public double Blur
    {
        get { return (double)GetValue(BlurProperty); }
        set { SetValue(BlurProperty, value); }
    }


    public static readonly BindableProperty XProperty = BindableProperty.Create(nameof(X), typeof(double), typeof(DropShadowEffect),
        2.0,
        propertyChanged: NeedUpdate);
    public double X
    {
        get { return (double)GetValue(XProperty); }
        set { SetValue(XProperty, value); }
    }

    public static readonly BindableProperty YProperty = BindableProperty.Create(nameof(Y), typeof(double), typeof(DropShadowEffect),
        2.0,
        propertyChanged: NeedUpdate);
    public double Y
    {
        get { return (double)GetValue(YProperty); }
        set { SetValue(YProperty, value); }
    }

    public override SKImageFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = SKImageFilter.CreateDropShadow(
                    (float)X * Parent.RenderingScale,
                    (float)Y * Parent.RenderingScale,
                    (float)Blur, (float)Blur,
                    Color.ToSKColor());
            }
        }
        return Filter;
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.Blur > 0);
        }
    }

}
