namespace DrawnUi.Draw;

public class SkiaShaderEffect : SkiaEffect, IPostRendererEffect
{
    protected SKPaint PaintWithShader;

    public static readonly BindableProperty UseContextProperty = BindableProperty.Create(nameof(UseContext),
        typeof(bool),
        typeof(SkiaShaderEffect),
        true,
        propertyChanged: NeedUpdate);

    /// <summary>
    /// Use either context of global Superview background, default is True.
    /// </summary>
    public bool UseContext
    {
        get { return (bool)GetValue(UseContextProperty); }
        set { SetValue(UseContextProperty, value); }
    }

    public static readonly BindableProperty AutoCreateInputTextureProperty = BindableProperty.Create(nameof(AutoCreateInputTexture),
        typeof(bool),
        typeof(SkiaShaderEffect),
        true,
        propertyChanged: NeedUpdate);

    /// <summary>
    /// Should create a texture from the current drawing to pass to shader as uniform shader iImage1, default is True.
    /// You need this set to False only if your shader is output-only.
    /// </summary>
    public bool AutoCreateInputTexture
    {
        get { return (bool)GetValue(AutoCreateInputTextureProperty); }
        set { SetValue(AutoCreateInputTextureProperty, value); }
    }

    /// <summary>
    /// Create snapshot from the current parent control drawing state to use as input texture for the shader
    /// </summary>
    protected virtual SKImage CreateSnapshot(SkiaDrawingContext ctx, SKRect destination)
    {
        if (UseContext)
        {
            ctx.Canvas.Flush();
            return ctx.Surface.Snapshot(new((int)destination.Left, (int)destination.Top,
                (int)destination.Right, (int)destination.Bottom));
        }
        else
        {
            //notice we read from the real canvas and we write to ctx.Canvas which can be cache
            ctx.Superview.CanvasView.Surface.Flush();
            return ctx.Superview.CanvasView.Surface.Snapshot(new((int)destination.Left,
                (int)destination.Top, (int)destination.Right, (int)destination.Bottom));
        }
    }

    void Flush(SkiaDrawingContext ctx)
    {
        if (UseContext)
        {
            ctx.Canvas.Flush();
        }
        else
        {
            ctx.Superview.CanvasView.Surface.Flush();
        }
    }

    protected virtual SKImage GetPrimaryTextureImage(SkiaDrawingContext ctx, SKRect destination)
    {
        if (Parent?.CachedImage == null && AutoCreateInputTexture)
        {
            return CreateSnapshot(ctx, destination);
        }

        return Parent?.CachedImage;
    }

    /// <summary>
    /// EffectPostRenderer
    /// </summary>
    public virtual void Render(DrawingContext ctx)
    {
        if (PaintWithShader == null)
        {
            PaintWithShader = new SKPaint();
        }

        var image = GetPrimaryTextureImage(ctx.Context, ctx.Destination);
        bool shouldDisposeImage = ShouldDisposePreviousTexture(image);
        SKShader shader = null;

        try
        {
            shader = CreateShader(ctx, image);
            if (shader != null)
            {
                PaintWithShader.Shader = shader;
                ctx.Context.Canvas.DrawRect(ctx.Destination, PaintWithShader);
            }
        }
        finally
        {
            // Dispose the image if we created it
            if (shouldDisposeImage && image != null)
            {
                image.Dispose();
            }

            // Dispose the shader we created this frame
            shader?.Dispose();

            // Clear shader from paint to avoid holding reference
            if (PaintWithShader != null)
                PaintWithShader.Shader = null;
        }
    }

    /// <summary>
    /// Checks if image is a new snapshot that needs disposal
    /// </summary>
    protected virtual bool ShouldDisposePreviousTexture(SKImage image)
    {
        // Don't dispose if it's the cached image from parent
        return image != null && image != Parent?.CachedImage;
    }

    /// <summary>
    /// Creates shader fresh each time - no caching of GPU resources
    /// </summary>
    public virtual SKShader CreateShader(DrawingContext ctx, SKImage source)
    {
        SKRect destination = ctx.Destination;
        SKImage sourceToDispose = null;

        // Step 1: Ensure shader is compiled (only CPU-side compilation is cached)
        if (CompiledShader == null || _hasNewShader)
        {
            try
            {
                if (string.IsNullOrEmpty(_customCode))
                {
                    CompileShader();
                }
                else
                {
                    CompileShader(_customCode, false);
                }
            }
            catch (Exception e)
            {
                Super.Log($"[SkiaShaderEffect] Failed to compile shader {e}");
                return null;
            }
            _hasNewShader = false;
        }

        if (CompiledShader == null)
            return null;

        try
        {
            // Step 2: Get or create source image
            if (source == null && AutoCreateInputTexture)
            {
                source = CreateSnapshot(ctx.Context, destination);
                sourceToDispose = source;
            }

            if (source == null)
                return null;

            // Step 3: Create everything fresh (no caching)
            var samplingOptions = new SKSamplingOptions(FilterMode, MipmapMode);
            using var primaryTexture = source.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, samplingOptions);

            using var textureUniforms = CreateTexturesUniforms(ctx.Context, destination, primaryTexture);
            using var uniforms = CreateUniforms(destination);

            // Step 4: Create final shader
            return CompiledShader.ToShader(uniforms, textureUniforms);
        }
        finally
        {
            // Dispose any snapshot we created
            if (sourceToDispose != null)
            {
                Parent?.DisposeObject(sourceToDispose);// ?? sourceToDispose.Dispose();
            }
        }
    }

    // ✅ KEEP: Only CPU-side compiled shader
    protected SKRuntimeEffect CompiledShader;
    private bool _hasNewShader;
    private string _customCode;
    private string _lastSource;

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && CompiledShader != null;
        }
    }

    /// <summary>
    /// Creates uniforms fresh each time
    /// </summary>
    protected virtual SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var viewport = destination;

        SKSize iResolution = new(viewport.Width, viewport.Height);
        SKSize iImageResolution = iResolution;
        var uniforms = new SKRuntimeEffectUniforms(CompiledShader);

        uniforms["iOffset"] = new[] { viewport.Left, viewport.Top };
        uniforms["iResolution"] = new[] { iResolution.Width, iResolution.Height };
        uniforms["iImageResolution"] = new[] { iImageResolution.Width, iImageResolution.Height };

        return uniforms;
    }

    /// <summary>
    /// Creates texture uniforms fresh each time
    /// </summary>
    protected virtual SKRuntimeEffectChildren CreateTexturesUniforms(SkiaDrawingContext ctx, SKRect destination, SKShader primaryTexture)
    {
        if (primaryTexture != null)
        {
            return new SKRuntimeEffectChildren(CompiledShader)
            {
                { "iImage1", primaryTexture }
            };
        }
        else
        {
            return new SKRuntimeEffectChildren(CompiledShader);
        }
    }

    protected string _template = null;
    protected string _templatePlacehodler = "//script-goes-here";

    /// <summary>
    /// Compiles the shader code - only CPU-side compilation
    /// </summary>
    protected virtual void CompileShader()
    {
        string shaderCode = SkSl.LoadFromResources(ShaderSource);
        CompileShader(shaderCode);
    }

    public string NormalizeShaderCode(string shaderText)
    {
        return shaderText.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
    }

    protected virtual void CompileShader(string shaderCode, bool useCache=true)
    {
        shaderCode = NormalizeShaderCode(shaderCode);
        LoadedCode = shaderCode;
        if (!string.IsNullOrEmpty(ShaderTemplate))
        {
            if (string.IsNullOrEmpty(_template))
                _template = SkSl.LoadFromResources(ShaderTemplate);
        }
        if (!string.IsNullOrEmpty(_template))
        {
            shaderCode = _template.Replace(_templatePlacehodler, shaderCode);
        }
        CompiledShader = SkSl.Compile(shaderCode, ShaderSource, useCache);
    }

    public string LoadedCode { get; set; }

    protected virtual void ApplyShaderSource()
    {
        if (_lastSource != ShaderSource || string.IsNullOrEmpty(ShaderSource))
        {
            _customCode = ShaderCode;
        }
        else
        {
            _customCode = "";
        }
        _hasNewShader = true;
        _template = null;
        Update();
    }

    protected static void NeedChangeSource(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaShaderEffect control)
        {
            control.ApplyShaderSource();
        }
    }

    public static readonly BindableProperty ShaderCodeProperty = BindableProperty.Create(nameof(ShaderCode),
        typeof(string),
        typeof(SkiaShaderEffect),
        string.Empty, propertyChanged: NeedChangeSource);

    public string ShaderCode
    {
        get { return (string)GetValue(ShaderCodeProperty); }
        set { SetValue(ShaderCodeProperty, value); }
    }

    public static readonly BindableProperty ShaderSourceProperty = BindableProperty.Create(nameof(ShaderSource),
        typeof(string),
        typeof(SkiaShaderEffect),
        string.Empty, propertyChanged: NeedChangeSource);

    public string ShaderSource
    {
        get { return (string)GetValue(ShaderSourceProperty); }
        set { SetValue(ShaderSourceProperty, value); }
    }

    public static readonly BindableProperty ShaderTemplateProperty = BindableProperty.Create(nameof(ShaderTemplate),
        typeof(string),
        typeof(SkiaShaderEffect),
        string.Empty, propertyChanged: NeedChangeSource);

    public string ShaderTemplate
    {
        get { return (string)GetValue(ShaderTemplateProperty); }
        set { SetValue(ShaderTemplateProperty, value); }
    }

    public static readonly BindableProperty FilterModeProperty = BindableProperty.Create(
        nameof(FilterMode),
        typeof(SKFilterMode),
        typeof(SkiaControl),
        SKFilterMode.Linear,
        propertyChanged: NeedChangeSource);

    public static readonly BindableProperty MipmapModeProperty = BindableProperty.Create(
        nameof(MipmapMode),
        typeof(SKMipmapMode),
        typeof(SkiaControl),
        SKMipmapMode.None,
        propertyChanged: NeedChangeSource);

    public SKFilterMode FilterMode
    {
        get => (SKFilterMode)GetValue(FilterModeProperty);
        set => SetValue(FilterModeProperty, value);
    }

    public SKMipmapMode MipmapMode
    {
        get => (SKMipmapMode)GetValue(MipmapModeProperty);
        set => SetValue(MipmapModeProperty, value);
    }

    /// <summary>
    /// Simplified update - no GPU resources to dispose
    /// </summary>
    public override void Update()
    {
        // Nothing to dispose - we don't cache GPU resources!
        base.Update();
    }

    /// <summary>
    /// Simplified dispose - only CPU-side resources
    /// </summary>
    protected override void OnDisposing()
    {
        // Only dispose CPU-side compiled shader
        CompiledShader?.Dispose();
        CompiledShader = null;

        PaintWithShader?.Dispose();
        PaintWithShader = null;

        base.OnDisposing();
    }
}
