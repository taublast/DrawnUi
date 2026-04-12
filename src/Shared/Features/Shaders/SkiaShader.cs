namespace DrawnUi.Infrastructure;

/// <summary>
/// Lightweight reusable shader wrapper for applying SKSL shaders directly to an SKPaint.
/// Used standalone for direct rendering, or as the engine inside SkiaShaderEffect.
/// Subclass and override CreateUniforms/CreateChildren for custom uniforms.
/// </summary>
public class SkiaShader : IDisposable
{
    protected SKRuntimeEffect _compiled;
    private bool _ownCompiled;
    private SKPaint _paint;
    private bool _ownPaint;
    private bool _disposed;

    // Pre-allocated uniform buffers — reused every frame to avoid per-frame heap alloc
    protected readonly float[] _bufResolution = new float[2];
    protected readonly float[] _bufImageResolution = new float[2];
    protected readonly float[] _bufMouse = new float[4];
    protected readonly float[] _bufOffset = new float[2];

    // Cached native objects reused across frames. All are bound to _compiled and
    // must be disposed+nulled whenever _compiled changes (see DisposeCompiled).
    private SKRuntimeEffectUniforms _cachedUniforms;
    private SKRuntimeEffectChildren _cachedChildren;
    private SKShader _cachedTextureShader;
    private IntPtr _cachedTextureSourceHandle;
    private SKFilterMode _cachedTextureFilter;
    private SKMipmapMode _cachedTextureMipmap;
    private SKShaderTileMode _cachedTextureTile;

    /// <summary>
    /// Elapsed time in seconds, typically fed from a frame timer.
    /// </summary>
    public float Time { get; set; }

    /// <summary>
    /// Current mouse/touch position (Shadertoy convention).
    /// </summary>
    public SKPoint Mouse { get; set; }

    /// <summary>
    /// Where click/drag started. If zero, means not dragging.
    /// </summary>
    public SKPoint MouseInitial { get; set; }

    /// <summary>
    /// Top-left offset of the drawing rect. Default (0,0).
    /// </summary>
    public SKPoint Offset { get; set; }

    /// <summary>
    /// Filter mode for the primary input texture. Default is Linear.
    /// </summary>
    public SKFilterMode FilterMode { get; set; } = SKFilterMode.Linear;

    /// <summary>
    /// Mipmap mode for the primary input texture. Default is None.
    /// </summary>
    public SKMipmapMode MipmapMode { get; set; } = SKMipmapMode.None;

    /// <summary>
    /// Tile mode for input textures. Default is Clamp.
    /// </summary>
    public SKShaderTileMode TileMode { get; set; } = SKShaderTileMode.Clamp;

    /// <summary>
    /// Blend mode used when drawing. Default is SrcOver.
    /// </summary>
    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    /// <summary>
    /// Whether the shader is compiled and ready to use.
    /// </summary>
    public bool IsCompiled => _compiled != null;

    /// <summary>
    /// The compiled SKRuntimeEffect, if available.
    /// </summary>
    public SKRuntimeEffect Compiled => _compiled;


    // ─── Construction ───────────────────────────────────────────────────────


    /// <summary>
    /// Create and compile from a resource file path (e.g. "Shaders/myeffect.sksl").
    /// </summary>
    /// <param name="resourcePath"></param>
    /// <param name="useCache"></param>
    /// <param name="onError"></param>
    /// <returns></returns>
    public static SkiaShader FromResource(string resourcePath, bool useCache = true, Action<string>onError = null)
    {
        var shader = new SkiaShader();
        shader.CompileFromResource(resourcePath, useCache, onError);
        return shader;
    }

    /// <summary>
    /// Create and compile from inline SKSL code.
    /// </summary>
    /// <param name="skslCode"></param>
    /// <param name="onError"></param>
    /// <returns></returns>
    public static SkiaShader FromCode(string skslCode, Action<string> onError = null)
    {
        var shader = new SkiaShader();
        shader.CompileFromCode(skslCode,null, false, onError);
        return shader;
    }

    private string _code;

    /// <summary>
    /// Gets the shader code used for last compilation
    /// </summary>
    public string Code
    {
        get
        {
            return _code;
        }
    }

    // ─── Compilation ────────────────────────────────────────────────────────

    /// <summary>
    /// Compile shader from an embedded resource file.
    /// </summary>
    public void CompileFromResource(string resourcePath, bool useCache = true, Action<string> onError = null)
    {
        DisposeCompiled();
        _code = NormalizeLineEndings(SkSl.LoadFromResources(resourcePath));
        _compiled = SkSl.Compile(_code, resourcePath, useCache, onError);
        _ownCompiled = !useCache;
    }

    /// <summary>
    /// Compile shader from an SKSL code string.
    /// </summary>
    public void CompileFromCode(string skslCode, string cacheKey = null, bool useCache = false, Action<string> onError = null)
    {
        DisposeCompiled();
        _code = NormalizeLineEndings(skslCode);
        _compiled = SkSl.Compile(_code, cacheKey, useCache, onError);
        _ownCompiled = !useCache;
    }

    public void DisposeCompiled()
    {
        // Cached uniforms/children/texture shader are all bound to _compiled.
        // They MUST be torn down before the effect handle changes.
        _cachedUniforms?.Dispose();
        _cachedUniforms = null;

        _cachedChildren?.Dispose();
        _cachedChildren = null;

        _cachedTextureShader?.Dispose();
        _cachedTextureShader = null;
        _cachedTextureSourceHandle = IntPtr.Zero;

        if (_ownCompiled)
        {
            _compiled?.Dispose();
        }
        _compiled = null;
        _ownCompiled = false;
    }

    // ─── Rendering ──────────────────────────────────────────────────────────

    /// <summary>
    /// Apply the shader to a paint using an image as the primary input texture (iImage1).
    /// Returns the created SKShader so caller can dispose it, or null if not compiled.
    /// The uniforms, children, and texture shader objects are cached on the instance
    /// and reused across frames — callers must NOT dispose them.
    /// </summary>
    public SKShader ApplyTo(SKPaint paint, SKImage source, float width, float height)
    {
        if (_compiled == null) return null;

        var textureShader = CreateTextureShader(source); // cached, do not dispose
        var uniforms = CreateUniforms(width, height, source?.Width ?? width, source?.Height ?? height);
        var children = CreateChildren(textureShader);

        var shader = _compiled.ToShader(uniforms, children);
        paint.Shader = shader;
        paint.BlendMode = BlendMode;
        return shader;
    }

    /// <summary>
    /// Apply the shader without an input texture (generative/output-only shaders).
    /// </summary>
    public SKShader ApplyTo(SKPaint paint, float width, float height)
    {
        if (_compiled == null) return null;

        var uniforms = CreateUniforms(width, height, width, height);
        var children = CreateChildren(null);

        var shader = _compiled.ToShader(uniforms, children);
        paint.Shader = shader;
        paint.BlendMode = BlendMode;
        return shader;
    }

    /// <summary>
    /// Full render: apply the shader and draw a rect on the canvas.
    /// </summary>
    public void DrawRect(SKCanvas canvas, SKImage source, SKRect destination)
    {
        if (_compiled == null) return;

        var paint = GetPaint();
        SKShader shader = null;
        try
        {
            shader = ApplyTo(paint, source, destination.Width, destination.Height);
            if (shader != null)
            {
                canvas.DrawRect(destination, paint);
                canvas.Flush(); //without this next draw might not use this gpu result
            }
        }
        finally
        {
            paint.Shader = null;
            shader?.Dispose();
        }
    }

    /// <summary>
    /// Full render without input texture (generative shaders).
    /// </summary>
    public void DrawRect(SKCanvas canvas, SKRect destination)
    {
        if (_compiled == null) return;

        var paint = GetPaint();
        SKShader shader = null;
        try
        {
            shader = ApplyTo(paint, destination.Width, destination.Height);
            if (shader != null)
            {
                canvas.DrawRect(destination, paint);
                canvas.Flush();//without this next draw might not use this gpu result
            }
        }
        finally
        {
            paint.Shader = null;
            shader?.Dispose();
        }
    }

    /// <summary>
    /// Full render: apply the shader and draw the result on the canvas.
    /// The source image is passed to the shader as iImage1 texture;
    /// actual drawing uses DrawRect because the shader generates all pixels itself.
    /// </summary>
    public void DrawImage(SKCanvas canvas, SKImage source, float x, float y)
    {
        if (_compiled == null || source == null) return;

        var paint = GetPaint();
        SKShader shader = null;
        try
        {
            shader = ApplyTo(paint, source, source.Width, source.Height);
            if (shader != null)
            {
                canvas.DrawRect(SKRect.Create(x, y, source.Width, source.Height), paint);
                canvas.Flush(); //without this next draw might not use this gpu result
            }
        }
        finally
        {
            paint.Shader = null;
            shader?.Dispose();
        }
    }

    // ─── Uniform creation (virtual — override in subclass for custom uniforms) ──

    /// <summary>
    /// Creates standard uniforms: iResolution, iImageResolution, iTime, iOffset, iMouse.
    /// Override to add custom uniforms for specialized shaders — the typical pattern is
    /// to call <c>base.CreateUniforms(...)</c> and then set extra keys on the returned
    /// instance.
    /// <para>
    /// IMPORTANT: the returned <see cref="SKRuntimeEffectUniforms"/> is OWNED by this
    /// <see cref="SkiaShader"/> and reused every frame. Do not dispose it and do not
    /// return a different instance from an override — mutate and return the same one.
    /// It is invalidated automatically when the compiled effect changes.
    /// </para>
    /// </summary>
    public virtual SKRuntimeEffectUniforms CreateUniforms(
        float viewportWidth, float viewportHeight,
        float imageWidth, float imageHeight)
    {
        var uniforms = _cachedUniforms ??= new SKRuntimeEffectUniforms(_compiled);

        _bufResolution[0] = viewportWidth;
        _bufResolution[1] = viewportHeight;
        uniforms["iResolution"] = _bufResolution;

        _bufImageResolution[0] = imageWidth;
        _bufImageResolution[1] = imageHeight;
        uniforms["iImageResolution"] = _bufImageResolution;

        uniforms["iTime"] = Time;

        _bufOffset[0] = Offset.X;
        _bufOffset[1] = Offset.Y;
        uniforms["iOffset"] = _bufOffset;

        _bufMouse[0] = Mouse.X;
        _bufMouse[1] = Mouse.Y;
        _bufMouse[2] = MouseInitial.X;
        _bufMouse[3] = MouseInitial.Y;
        uniforms["iMouse"] = _bufMouse;

        return uniforms;
    }

    /// <summary>
    /// Creates texture children with the primary texture as iImage1.
    /// Override to add additional textures (iImage2, etc.) by setting extra keys on the
    /// returned instance.
    /// <para>
    /// IMPORTANT: the returned <see cref="SKRuntimeEffectChildren"/> is OWNED by this
    /// <see cref="SkiaShader"/> and reused every frame. Do not dispose it and do not
    /// return a different instance from an override. It is invalidated automatically
    /// when the compiled effect changes.
    /// </para>
    /// </summary>
    public virtual SKRuntimeEffectChildren CreateChildren(SKShader primaryTexture)
    {
        var children = _cachedChildren ??= new SKRuntimeEffectChildren(_compiled);
        if (primaryTexture != null)
        {
            children["iImage1"] = primaryTexture;
        }
        return children;
    }

    /// <summary>
    /// Creates (or reuses) a shader from an <see cref="SKImage"/> for use as a texture
    /// uniform. The returned <see cref="SKShader"/> is cached on the instance and reused
    /// while the source image handle and sampling options are unchanged — callers must
    /// NOT dispose it. It is disposed automatically when the source, sampling options, or
    /// compiled effect change, and in <see cref="Dispose"/>.
    /// </summary>
    public SKShader CreateTextureShader(SKImage source)
    {
        if (source == null) return null;

        var handle = source.Handle;
        if (_cachedTextureShader != null
            && _cachedTextureSourceHandle == handle
            && _cachedTextureFilter == FilterMode
            && _cachedTextureMipmap == MipmapMode
            && _cachedTextureTile == TileMode)
        {
            return _cachedTextureShader;
        }

        _cachedTextureShader?.Dispose();

        var sampling = new SKSamplingOptions(FilterMode, MipmapMode);
        _cachedTextureShader = source.ToShader(TileMode, TileMode, sampling);
        _cachedTextureSourceHandle = handle;
        _cachedTextureFilter = FilterMode;
        _cachedTextureMipmap = MipmapMode;
        _cachedTextureTile = TileMode;
        return _cachedTextureShader;
    }

    // ─── Internals ──────────────────────────────────────────────────────────

    public SKPaint GetPaint()
    {
        if (_paint == null)
        {
            _paint = new SKPaint();
            _ownPaint = true;
        }
        return _paint;
    }

    public static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
    }

    // ─── Disposal ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeCompiled();

        if (_ownPaint)
        {
            _paint?.Dispose();
        }
        _paint = null;
    }
}
