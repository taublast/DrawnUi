namespace DrawnUi.Draw;

public class ChainColorPresetEffect : BaseChainedEffect
{
    #region PROPERTIES

    public static readonly BindableProperty PresetProperty = BindableProperty.Create(
        nameof(Preset),
        typeof(SkiaImageEffect),
        typeof(ChainColorPresetEffect),
        SkiaImageEffect.Pastel,
        propertyChanged: NeedUpdate);

    public SkiaImageEffect Preset
    {
        get { return (SkiaImageEffect)GetValue(PresetProperty); }
        set { SetValue(PresetProperty, value); }
    }

    #endregion

    public override ChainEffectResult Draw(DrawingContext ctx, Action<DrawingContext> drawControl)
    {
        if (NeedApply)
        {
            if (Paint == null)
            {
                Paint = new()
                {
                    ColorFilter = Preset switch
                    {
                        SkiaImageEffect.Grayscale
                            => SkiaImageEffects.Grayscale2(),

                        SkiaImageEffect.BlackAndWhite
                            => SkiaImageEffects.Grayscale(),

                        SkiaImageEffect.Pastel
                            => SkiaImageEffects.Pastel(),

                        SkiaImageEffect.Sepia
                            => SkiaImageEffects.Sepia(),

                        SkiaImageEffect.InvertColors
                            => SkiaImageEffects.InvertColors(),

                        _ => null
                    }
                };
            }

            ctx.Context.Canvas.SaveLayer(Paint);

            drawControl(ctx);

            return ChainEffectResult.Create(true);
        }

        return base.Draw(ctx, drawControl);
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.Preset != SkiaImageEffect.None);
        }
    }
}
