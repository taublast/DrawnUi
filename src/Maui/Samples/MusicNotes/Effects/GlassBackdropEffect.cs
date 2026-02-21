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

    public static readonly BindableProperty BlurStrengthProperty = BindableProperty.Create(
        nameof(BlurStrength),
        typeof(float),
        typeof(GlassBackdropEffect),
        1.0f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty GlassOpacityProperty = BindableProperty.Create(
        nameof(GlassOpacity),
        typeof(float),
        typeof(GlassBackdropEffect),
        0.75f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty GlassColorProperty = BindableProperty.Create(
        nameof(GlassColor),
        typeof(Color),
        typeof(GlassBackdropEffect),
        Colors.Transparent,
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
    /// Default: 1.0.
    /// </summary>
    public float GlassDepth
    {
        get => (float)GetValue(GlassDepthProperty);
        set => SetValue(GlassDepthProperty, value);
    }

    /// <summary>
    /// Gets or sets the blur intensity multiplier for the frosted glass effect.
    /// Default: 1.0 (original appearance).
    /// </summary>
    public float BlurStrength
    {
        get => (float)GetValue(BlurStrengthProperty);
        set => SetValue(BlurStrengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the overall glass panel opacity.
    /// Range: 0.0 (fully transparent) to 1.0 (fully opaque).
    /// Default: 0.75.
    /// </summary>
    public float GlassOpacity
    {
        get => (float)GetValue(GlassOpacityProperty);
        set => SetValue(GlassOpacityProperty, value);
    }

    /// <summary>
    /// Gets or sets the glass tint color. The alpha channel controls tint strength:
    /// fully transparent = no tint, fully opaque = solid color overlay.
    /// Default: Transparent (no tint).
    /// </summary>
    public Color GlassColor
    {
        get => (Color)GetValue(GlassColorProperty);
        set => SetValue(GlassColorProperty, value);
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

        // Fix iResolution to be in logical points (base class sets it to pixels).
        // This makes renderingScale = iImageResolution / iResolution = actual screen density,
        // so the shader can convert iCornerRadius from points to pixels correctly.
        var scale = Parent?.RenderingScale ?? 1f;
        uniforms["iResolution"] = new[] { destination.Width / scale, destination.Height / scale };

        // Pass corner radius in points - shader converts to pixels via renderingScale
        uniforms["iCornerRadius"] = CornerRadius;

        uniforms["iGlassDepth"] = GlassDepth;
        uniforms["iBlurStrength"] = BlurStrength;
        uniforms["iGlassOpacity"] = GlassOpacity;

        // GlassColor: rgb = tint color, a = tint strength (0 = no tint, nothing changes)
        var c = GlassColor;
        uniforms["iGlassColor"] = new[] { (float)c.Red, (float)c.Green, (float)c.Blue, (float)c.Alpha };

        return uniforms;
    }
}
