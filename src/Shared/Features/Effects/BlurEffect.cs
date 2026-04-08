namespace DrawnUi.Draw;

public class BlurEffect : BaseImageFilterEffect
{
    public static readonly BindableProperty AmountProperty = BindableProperty.Create(
        nameof(Amount),
        typeof(double),
        typeof(SkiaImage),
        0.3,
        propertyChanged: NeedUpdate);

    public double Amount
    {
        get { return (double)GetValue(AmountProperty); }
        set { SetValue(AmountProperty, value); }
    }

    public override SKImageFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = SKImageFilter.CreateBlur((float)Amount, (float)Amount, SKShaderTileMode.Mirror);
            }
        }
        return Filter;
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.Amount > 0);
        }
    }
}