namespace DrawnUi.Draw;

public class ContrastEffect : BaseColorFilterEffect
{
    public static readonly BindableProperty ContrastProperty = BindableProperty.Create(
        nameof(Contrast),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public float Contrast
    {
        get => (float)GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    public override SKColorFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = CreateContrastFilter(Contrast);
            }
        }
        return Filter;
    }

    private SKColorFilter CreateContrastFilter(float contrast)
    {
        float t = (1 - contrast) / 2;
        float[] colorMatrix = {
            contrast, 0, 0, 0, t,
            0, contrast, 0, 0, t,
            0, 0, contrast, 0, t,
            0, 0, 0, 1, 0
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    public override bool NeedApply => base.NeedApply && Contrast != 1f;
}