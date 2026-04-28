namespace DrawnUi.Draw;

public class SaturationEffect : BaseColorFilterEffect
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(float),
        typeof(SkiaControl),
        1f,
        propertyChanged: NeedUpdate);

    public float Value
    {
        get => (float)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override SKColorFilter CreateFilter(SKRect destination)
    {
        if (NeedApply && Filter == null)
        {
            Filter = SkiaImageEffects.Saturation(Value);
        }

        return Filter;
    }

    public override bool NeedApply => base.NeedApply && Value != 1f;
}