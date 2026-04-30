namespace DrawnUi.Draw;

public class TintEffect : BaseColorFilterEffect
{

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color),
        typeof(Color),
        typeof(SkiaControl),
        Colors.Red,
        propertyChanged: NeedUpdate);

    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
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
                Filter = SkiaImageEffects.Tint(Color, EffectBlendMode);
            }
        }
        return Filter;
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.Color != Colors.Transparent);
        }
    }
}