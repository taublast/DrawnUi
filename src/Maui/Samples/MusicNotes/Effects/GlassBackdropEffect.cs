namespace MusicNotes.Effects;

/// <summary>
/// Glass backdrop effect with customizable corner radius.
/// Applies a liquid glass shader effect that respects rounded corners.
/// </summary>
public class GlassBackdropEffect : SkiaShaderEffect
{
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(float),
        typeof(GlassBackdropEffect),
        0f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty GlassDepthProperty = BindableProperty.Create(
        nameof(GlassDepth),
        typeof(float),
        typeof(GlassBackdropEffect),
        1.0f,
        propertyChanged: OnPropertyChanged);

    /// <summary>
    /// Gets or sets the corner radius in points (density-independent units).
    /// The shader will automatically convert this to pixels based on screen density.
    /// </summary>
    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the 3D depth/emboss intensity of the glass effect.
    /// Controls the refraction strength for the curved appearance.
    /// Range: 0.0 (flat/no distortion) to 2.0+ (very pronounced).
    /// Default: 1.0 (original appearance).
    /// </summary>
    public float GlassDepth
    {
        get => (float)GetValue(GlassDepthProperty);
        set => SetValue(GlassDepthProperty, value);
    }

    private static void OnPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GlassBackdropEffect effect)
        {
            effect.Update();
        }
    }

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        // Pass corner radius in points - shader will convert to pixels using renderingScale
        uniforms["iCornerRadius"] = CornerRadius;

        // Pass glass depth for controlling 3D emboss intensity
        uniforms["iGlassDepth"] = GlassDepth;

        return uniforms;
    }
}
