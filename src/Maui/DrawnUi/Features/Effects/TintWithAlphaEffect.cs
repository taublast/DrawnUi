namespace DrawnUi.Draw;

public class TintWithAlphaEffect : BaseColorFilterEffect
{

    public static readonly BindableProperty ColorTintProperty = BindableProperty.Create(
        nameof(ColorTint),
        typeof(Color),
        typeof(SkiaControl),
        Colors.Red,
        propertyChanged: NeedUpdate);

    public Color ColorTint
    {
        get { return (Color)GetValue(ColorTintProperty); }
        set { SetValue(ColorTintProperty, value); }
    }

    public static readonly BindableProperty AlphaProperty = BindableProperty.Create(
        nameof(Alpha),
        typeof(double),
        typeof(SkiaControl),
        1.0,
        propertyChanged: NeedUpdate);

    public double Alpha
    {
        get { return (double)GetValue(AlphaProperty); }
        set { SetValue(AlphaProperty, value); }
    }

    public static readonly BindableProperty EffectBlendModeProperty = BindableProperty.Create(
        nameof(EffectBlendMode),
        typeof(SKBlendMode),
        typeof(SkiaControl),
        SKBlendMode.SrcATop,
        propertyChanged: NeedUpdate);

    public SKBlendMode EffectBlendMode
    {
        get { return (SKBlendMode)GetValue(EffectBlendModeProperty); }
        set { SetValue(EffectBlendModeProperty, value); }
    }

    public override SKColorFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                var color =
                Filter = SkiaImageEffects.Tint(ColorTint.WithAlpha((float)Alpha), EffectBlendMode);
            }
        }
        return Filter;
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.ColorTint != Colors.Transparent);
        }
    }
}