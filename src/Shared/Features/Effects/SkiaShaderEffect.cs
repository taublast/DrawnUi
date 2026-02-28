namespace DrawnUi.Draw;

/// <summary>
/// IPostRendererEffect
/// </summary>
public class SkiaShaderEffect : SkiaEffect, IPostRendererEffect
{
    protected SKPaint PaintWithShader;

    // ─── IPostRendererEffect ────────────────────────────────────────────────

    /// <inheritdoc/>
    public PostRendererEffectUseBackgroud UseBackground { get; set; } = PostRendererEffectUseBackgroud.Always;

    /// <inheritdoc/>
    public bool AquiredBackground { get; set; }

    // Frozen snapshot for Once mode
    private SKImage _frozenSnapshot;
    private bool _frozenSnapshotOwned;

    public void ReleaseFrozenSnapshot()
    {
        if (Parent != null)
        {
            Parent.DisposeObject(_frozenSnapshot);
        }
        else
        if (_frozenSnapshotOwned && _frozenSnapshot != null)
        {
            _frozenSnapshot.Dispose();
            _frozenSnapshot = null;
        }

        _frozenSnapshotOwned = false;
        _frozenSnapshot = null;
    }

    // ─── UseContext / AutoCreateInputTexture ────────────────────────────────

    public static readonly BindableProperty UseContextProperty = BindableProperty.Create(nameof(UseContext),
        typeof(bool),
        typeof(SkiaShaderEffect),
        true,
        propertyChanged: NeedUpdate);

    /// <summary>
    /// Use either context or global Superview background, default is True.
    /// </summary>
    public bool UseContext
    {
        get { return (bool)GetValue(UseContextProperty); }
        set { SetValue(UseContextProperty, value); }
    }

    public static readonly BindableProperty AutoCreateInputTextureProperty = BindableProperty.Create(
        nameof(AutoCreateInputTexture),
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
    /// Blend mode used when drawing the shader effect on the canvas. Default is SrcOver.
    /// For generative overlay effects (UseBackground = Never), use Plus for additive blending.
    /// </summary>
    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    // ─── Snapshot helpers ───────────────────────────────────────────────────

    protected void Flush(SkiaDrawingContext ctx)
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

    /// <summary>
    /// Returns the background texture according to the current UseBackground mode.
    /// Always  — live snapshot (or parent cache) every frame.
    /// Once    — snapshot taken on first call, frozen thereafter; reset AquiredBackground to re-capture.
    /// Never   — returns null; shader must not require iImage1.
    /// </summary>
    protected virtual SKImage GetPrimaryTextureImage(SkiaDrawingContext ctx, SKRect destination)
    {
        switch (UseBackground)
        {
            case PostRendererEffectUseBackgroud.Never:
                return null;

            case PostRendererEffectUseBackgroud.Once:
                if (!AquiredBackground || _frozenSnapshot.Handle==0) //check handle as cache might be disposed
                {
                    _frozenSnapshotOwned = false;

                    var snapshot = Parent?.CachedImage;
                    if (Parent?.CachedImage == null && AutoCreateInputTexture)
                    {
                        snapshot = CreateSnapshot(ctx, destination);
                    }

                    AquiredBackground = true;
                    _frozenSnapshot = snapshot;
                    return snapshot;
                }
                return _frozenSnapshot;

            default: // Always
                if (Parent?.CachedImage == null && AutoCreateInputTexture)
                {
                    return CreateSnapshot(ctx, destination);
                }
                return Parent?.CachedImage;
        }
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

        TimeSeconds = ctx.Context.FrameTimeNanos * NanosecondsToSeconds;

        var image = GetPrimaryTextureImage(ctx.Context, ctx.Destination);
        bool shouldDisposeImage = ShouldDisposePreviousTexture(image);
        SKShader shader = null;

        try
        {
            shader = CreateShader(ctx, image);
            if (shader != null)
            {
                PaintWithShader.BlendMode = BlendMode;
                PaintWithShader.Shader = shader;
                ctx.Context.Canvas.DrawRect(ctx.Destination, PaintWithShader);
            }
            else
            {
                Debug.WriteLine($"SkiaShaderEffect failed to create shader, source: {ShaderSource}");
            }
        }
        finally
        {
            // Dispose the image if we created it (not frozen, not parent cache)
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
    /// Checks if image is a new snapshot that needs disposal this frame.
    /// Frozen snapshots are managed by ReleaseFrozenSnapshot; parent cache is never owned.
    /// </summary>
    protected virtual bool ShouldDisposePreviousTexture(SKImage image)
    {
        // Frozen snapshot lifecycle is managed separately — never dispose per-frame
        if (image == _frozenSnapshot) return false;
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
                    CompileShader(_customCode, false, SendError);
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
            // Step 2: For Always/Once modes, ensure we have a source image
            if (UseBackground != PostRendererEffectUseBackgroud.Never)
            {
                if (source == null && AutoCreateInputTexture)
                {
                    source = CreateSnapshot(ctx.Context, destination);
                    sourceToDispose = source;
                }

                if (source == null || source.Handle==0)
                    return null;
            }

            // Step 3: Create everything fresh (no caching)
            SKShader primaryTextureShader = CreatePrimaryTextureShader(source);

            using (primaryTextureShader)
            {
                using var textureUniforms = CreateTexturesUniforms(ctx.Context, destination, primaryTextureShader);
                using var uniforms = CreateUniforms(destination);

                // Step 4: Create final shader
                return CompiledShader.ToShader(uniforms, textureUniforms);
            }
        }
        finally
        {
            // Dispose any snapshot we created this frame (not frozen, not parent cache)
            if (sourceToDispose != null)
            {
                Parent?.DisposeObject(sourceToDispose); // ?? sourceToDispose.Dispose();
            }
        }
    }

    protected virtual SKShader CreatePrimaryTextureShader(SKImage source)
    {
        if (source == null) return null;
        var samplingOptions = new SKSamplingOptions(FilterMode, MipmapMode);
        return source.ToShader(TileMode, TileMode, samplingOptions);
    }

    // Pre-allocated uniform value buffers — reused every frame to avoid per-frame heap alloc
    private readonly float[] _uniformMouse = new float[4];
    private readonly float[] _uniformOffset = new float[2];
    private readonly float[] _uniformResolution = new float[2];
    protected readonly float[] _uniformImageResolution = new float[2];

    // ✅ KEEP: Only CPU-side compiled shader
    protected SKRuntimeEffect CompiledShader;
    private bool _hasNewShader;
    private bool _ownCompiledShader; // true only when compiled without cache (custom ShaderCode)
    private string _customCode;
    private string _lastSource;

    public override bool NeedApply
    {
        get { return base.NeedApply && CompiledShader != null; }
    }


    #region STANDART UNIFORMS


    /// <summary>
    /// Normally will be automatically set in Render method from context FrameTimeNanos
    /// </summary>
    public float TimeSeconds { get; set; }

    const float NanosecondsToSeconds = 1e-9f; // 1 / 1,000,000,000

    /// <summary>
    /// Shadertoy conventions current mouse position (or drag position)
    /// </summary>
    public PointF MouseCurrent { get; set; }

    /// <summary>
    /// Shadertoy conventions where click/drag started. If zero could mean not dragging.
    /// </summary>
    public PointF MouseInitial { get; set; }

    /// <summary>
    /// Creates uniforms fresh each time
    /// </summary>
    protected virtual SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var viewport = destination;

        SKSize iResolution = new(viewport.Width, viewport.Height);
        SKSize iImageResolution = iResolution;
        var uniforms = new SKRuntimeEffectUniforms(CompiledShader);

        _uniformMouse[0] = MouseCurrent.X; _uniformMouse[1] = MouseCurrent.Y;
        _uniformMouse[2] = MouseInitial.X; _uniformMouse[3] = MouseInitial.Y;
        uniforms["iMouse"] = _uniformMouse;
        uniforms["iTime"] = TimeSeconds;
        _uniformOffset[0] = viewport.Left; _uniformOffset[1] = viewport.Top;
        uniforms["iOffset"] = _uniformOffset;

        // Viewport size in pixels, can be different from size of images passed as sources
        _uniformResolution[0] = iResolution.Width; _uniformResolution[1] = iResolution.Height;
        uniforms["iResolution"] = _uniformResolution;

        _uniformImageResolution[0] = iImageResolution.Width; _uniformImageResolution[1] = iImageResolution.Height;
        uniforms["iImageResolution"] = _uniformImageResolution;

        return uniforms;
    }

    /// <summary>
    /// Creates texture uniforms fresh each time
    /// </summary>
    protected virtual SKRuntimeEffectChildren CreateTexturesUniforms(SkiaDrawingContext ctx, SKRect destination,
        SKShader primaryTexture)
    {
        if (primaryTexture != null)
        {
            return new SKRuntimeEffectChildren(CompiledShader) { { "iImage1", primaryTexture } };
        }
        else
        {
            return new SKRuntimeEffectChildren(CompiledShader);
        }
    }

    #endregion

    protected string _template = null;
    protected string _templatePlacehodler = "//script-goes-here";

    /// <summary>
    /// Compiles the shader code - only CPU-side compilation
    /// </summary>
    protected virtual void CompileShader()
    {
        string shaderCode = SkSl.LoadFromResources(ShaderSource);
        CompileShader(shaderCode,true, SendError);
    }

    public event EventHandler<string> OnCompilationError;

    protected void SendError(string error)
    {
        if (OnCompilationError == null)
        {
            throw new ApplicationException($"Shader compilation error: {error}");
        }
        OnCompilationError?.Invoke(this, error);
    }

    public string NormalizeShaderCode(string shaderText)
    {
        return shaderText.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
    }

    protected virtual void CompileShader(string shaderCode, bool useCache = true, Action<string> onError = null)
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

        CompiledShader = SkSl.Compile(shaderCode, ShaderSource, useCache, onError);
        _ownCompiledShader = !useCache; // cache owns it when useCache=true
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

    /// <summary>
    /// Changing this directly will force  the shader to recompile
    /// </summary>
    public string ShaderCode
    {
        get { return (string)GetValue(ShaderCodeProperty); }
        set { SetValue(ShaderCodeProperty, value); }
    }

    public static readonly BindableProperty ShaderSourceProperty = BindableProperty.Create(nameof(ShaderSource),
        typeof(string),
        typeof(SkiaShaderEffect),
        string.Empty, propertyChanged: NeedChangeSource);

    /// <summary>
    /// FIlename from resources, ex: @"Shaders\blit.sksl"
    /// </summary>
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
        typeof(SkiaShaderEffect),
        SKFilterMode.Linear,
        propertyChanged: NeedChangeSource);

    public static readonly BindableProperty MipmapModeProperty = BindableProperty.Create(
        nameof(MipmapMode),
        typeof(SKMipmapMode),
        typeof(SkiaShaderEffect),
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

    public static readonly BindableProperty TileModeProperty = BindableProperty.Create(nameof(TileMode),
        typeof(SKShaderTileMode), typeof(SkiaShaderEffect),
        SKShaderTileMode.Clamp,
        propertyChanged: NeedChangeSource);

    /// <summary>
    /// Tile mode for input textures
    /// </summary>
    public SKShaderTileMode TileMode
    {
        get { return (SKShaderTileMode)GetValue(TileModeProperty); }
        set { SetValue(TileModeProperty, value); }
    }

    /// <summary>
    /// Simplified dispose - only CPU-side resources
    /// </summary>
    protected override void OnDisposing()
    {
        //ReleaseFrozenSnapshot();

        // Only dispose CompiledShader if this instance owns it (not from cache)
        if (_ownCompiledShader)
            CompiledShader?.Dispose();

        CompiledShader = null;
        _ownCompiledShader = false;

        PaintWithShader?.Dispose();
        PaintWithShader = null;

        base.OnDisposing();
    }
}
