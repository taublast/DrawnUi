namespace DrawnUi.Draw;

public class ColorPresetEffect : BaseColorFilterEffect
{
    public static readonly BindableProperty PresetProperty = BindableProperty.Create(
        nameof(Preset),
        typeof(SkiaImageEffect),
        typeof(ColorPresetEffect),
        SkiaImageEffect.Pastel,
        propertyChanged: NeedUpdate);

    public SkiaImageEffect Preset
    {
        get { return (SkiaImageEffect)GetValue(PresetProperty); }
        set { SetValue(PresetProperty, value); }
    }

    public override SKColorFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = Preset switch
                {
                    SkiaImageEffect.BlackAndWhite
                        => SkiaImageEffects.Grayscale(),

                    SkiaImageEffect.Pastel
                        => SkiaImageEffects.Pastel(),

                    SkiaImageEffect.Sepia
                        => SkiaImageEffects.Sepia(),

                    SkiaImageEffect.InvertColors
                        => SkiaImageEffects.InvertColors(),

                    _ => null
                };
            }
        }
        return Filter;
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.Preset != SkiaImageEffect.None);
        }
    }
}