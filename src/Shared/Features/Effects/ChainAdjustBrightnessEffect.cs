using Newtonsoft.Json.Linq;

namespace DrawnUi.Draw;

public class ChainAdjustBrightnessEffect : BaseChainedEffect
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
                    ColorFilter = CreateBrightnessFilter(Value)
                };
            }

            ctx.Context.Canvas.SaveLayer(Paint);

            drawControl(ctx);

            return ChainEffectResult.Create(true);
        }

        return base.Draw(ctx, drawControl);
    }

    /// <summary>
    /// -1 -> 0 -> 1
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private SKColorFilter CreateLightnessFilter(float value)
    {
        var brightness = Math.Clamp(value - 1, -1f, 1f);

        float b = brightness;
        float[] colorMatrix = {
            1, 0, 0, 0, b,
            0, 1, 0, 0, b,
            0, 0, 1, 0, b,
            0, 0, 0, 1, 0
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    /// <summary>
    /// -1 -> 0 -> 1
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private SKColorFilter CreateBrightnessFilter(float value)
    {
        // maps -1 to a 0.5 scale (50% brightness), and 1 to a 1.5 scale (150% brightness).
        float minScale = 0.0f; // factor for -1 value
        float maxScale = 2.0f; // factor for 1 value
        var brightnessScale = ((value) / 2) * (maxScale - minScale) + minScale;

        brightnessScale = Math.Clamp(brightnessScale, 0.1f, 2f);

        float[] colorMatrix = {
            brightnessScale, 0, 0, 0, 0,
            0, brightnessScale, 0, 0, 0,
            0, 0, brightnessScale, 0, 0,
            0, 0, 0, 1, 0
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }


    public override bool NeedApply => base.NeedApply && Value != 1f;
}
