namespace DrawnUi.Draw;

public class ChainAdjustContrastEffect : BaseChainedEffect
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(float),
        typeof(ChainAdjustBrightnessEffect),
        1f,
        propertyChanged: NeedUpdate);

    public float Value
    {
        get => (float)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override ChainEffectResult Draw(DrawingContext ctx, Action<DrawingContext> drawControl)
    {
        if (NeedApply)
        {
            if (Paint == null)
            {
                Paint = new()
                {
                    ColorFilter = CreateContrastFilter(Value)
                };
            }

            ctx.Context.Canvas.SaveLayer(Paint);

            drawControl(ctx);

            return ChainEffectResult.Create(true);
        }

        return base.Draw(ctx, drawControl);
    }

    public static SKColorFilter CreateContrastFilter(float contrast)
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

    public override bool NeedApply => base.NeedApply && Value != 1f;
}

public class AdjustBrightnessEffect : BaseColorFilterEffect
{
    public static readonly BindableProperty BrightnessProperty = BindableProperty.Create(
        nameof(Brightness),
        typeof(float),
        typeof(SkiaImage),
        0f, // Default to no change
        propertyChanged: NeedUpdate);

    public float Brightness
    {
        get => (float)GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    public override SKColorFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = CreateBrightnessFilter(Brightness);
            }
        }
        return Filter;
    }

    private SKColorFilter CreateBrightnessFilter(float brightness)
    {
        float b = brightness * 255; // Assuming brightness is in [0, 1] range; adjust if it's [-1, 1]
        float[] colorMatrix = {
            1, 0, 0, 0, b,
            0, 1, 0, 0, b,
            0, 0, 1, 0, b,
            0, 0, 0, 1, 0
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    public override bool NeedApply => base.NeedApply && Brightness != 0f;
}
