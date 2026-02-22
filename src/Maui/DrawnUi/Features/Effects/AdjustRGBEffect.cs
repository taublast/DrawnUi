namespace DrawnUi.Draw;

public class AdjustRGBEffect : BaseColorFilterEffect
{
    public static readonly BindableProperty RedProperty = BindableProperty.Create(
        nameof(Red),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public static readonly BindableProperty GreenProperty = BindableProperty.Create(
        nameof(Green),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public static readonly BindableProperty BlueProperty = BindableProperty.Create(
        nameof(Blue),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public float Red
    {
        get => (float)GetValue(RedProperty);
        set => SetValue(RedProperty, value);
    }

    public float Green
    {
        get => (float)GetValue(GreenProperty);
        set => SetValue(GreenProperty, value);
    }

    public float Blue
    {
        get => (float)GetValue(BlueProperty);
        set => SetValue(BlueProperty, value);
    }

    public override SKColorFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = CreateRGBAdjustmentFilter(Red, Green, Blue);
            }
        }
        return Filter;
    }

    private SKColorFilter CreateRGBAdjustmentFilter(float red, float green, float blue)
    {
        // Normalize the RGB values to the 0-1 range for matrix multiplication
        float[] colorMatrix = {
            red, 0, 0, 0, 0, // Red channel
            0, green, 0, 0, 0, // Green channel
            0, 0, blue, 0, 0, // Blue channel
            0, 0, 0, 1, 0 // Alpha channel
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    public override bool NeedApply => base.NeedApply && (Red != 1f || Green != 1f || Blue != 1f);
}

public class ChainAdjustRGBEffect : BaseChainedEffect
{
    public static readonly BindableProperty RedProperty = BindableProperty.Create(
        nameof(Red),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public static readonly BindableProperty GreenProperty = BindableProperty.Create(
        nameof(Green),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public static readonly BindableProperty BlueProperty = BindableProperty.Create(
        nameof(Blue),
        typeof(float),
        typeof(SkiaImage),
        1f, // Default to no change
        propertyChanged: NeedUpdate);

    public float Red
    {
        get => (float)GetValue(RedProperty);
        set => SetValue(RedProperty, value);
    }

    public float Green
    {
        get => (float)GetValue(GreenProperty);
        set => SetValue(GreenProperty, value);
    }

    public float Blue
    {
        get => (float)GetValue(BlueProperty);
        set => SetValue(BlueProperty, value);
    }

    public override ChainEffectResult Draw(DrawingContext ctx, Action<DrawingContext> drawControl)
    {
        if (NeedApply)
        {
            if (Paint == null)
            {
                Paint = new SKPaint { ColorFilter = CreateRGBAdjustmentFilter(Red, Green, Blue) };
            }

            ctx.Context.Canvas.SaveLayer(Paint);

            drawControl(ctx);

            return ChainEffectResult.Create(true);
        }

        return base.Draw(ctx, drawControl);
    }

    private SKColorFilter CreateRGBAdjustmentFilter(float red, float green, float blue)
    {
        // Normalize the RGB values to the 0-1 range for matrix multiplication
        float[] colorMatrix = {
            red, 0, 0, 0, 0, // Red channel
            0, green, 0, 0, 0, // Green channel
            0, 0, blue, 0, 0, // Blue channel
            0, 0, 0, 1, 0 // Alpha channel
        };

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    public override bool NeedApply => base.NeedApply && (Red != 1f || Green != 1f || Blue != 1f);
}
