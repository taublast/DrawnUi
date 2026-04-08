namespace DrawnUi.Draw;

public class ChainSaturationEffect : BaseChainedEffect
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
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
                    ColorFilter = CreateSaturationFilter(Value)
                };
            }

            ctx.Context.Canvas.SaveLayer(Paint);

            drawControl(ctx);

            return ChainEffectResult.Create(true);
        }

        return base.Draw(ctx, drawControl);
    }


    public static SKColorFilter CreateSaturationFilter(float saturation)
    {
        float invSat = 1 - saturation;
        float R = 0.213f * invSat;
        float G = 0.715f * invSat;
        float B = 0.072f * invSat;

        float[] colorMatrix = {
            R + saturation, G, B, 0, 0,
            R, G + saturation, B, 0, 0,
            R, G, B + saturation, 0, 0,
            0, 0, 0, 1, 0
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    public override bool NeedApply => base.NeedApply && Value != 1f;
}
