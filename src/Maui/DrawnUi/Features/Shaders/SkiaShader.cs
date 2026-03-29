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

    public SkiaShader()
    {
    }

    /// <summary>
    /// Create and compile from a resource file path (e.g. "Shaders/myeffect.sksl").
    /// </summary>
    public static SkiaShader FromResource(string resourcePath, bool useCache = true)
    {
        var shader = new SkiaShader();
        shader.CompileFromResource(resourcePath, useCache);
        return shader;
    }

    /// <summary>
    /// Create and compile from inline SKSL code.
    /// </summary>
    public static SkiaShader FromCode(string skslCode)
    {
        var shader = new SkiaShader();
        shader.CompileFromCode(skslCode);
        return shader;
    }

    // ─── Compilation ────────────────────────────────────────────────────────

    /// <summary>
    /// Compile shader from an embedded resource file.
    /// </summary>
    public void CompileFromResource(string resourcePath, bool useCache = true, Action<string> onError = null)
    {
        DisposeCompiled();
        var shaderCode = NormalizeLineEndings(SkSl.LoadFromResources(resourcePath));
        _compiled = SkSl.Compile(shaderCode, resourcePath, useCache, onError);
        _ownCompiled = !useCache;
    }

    /// <summary>
    /// Compile shader from an SKSL code string.
    /// </summary>
    public void CompileFromCode(string skslCode, string cacheKey = null, bool useCache = false, Action<string> onError = null)
    {
        DisposeCompiled();
        var normalized = NormalizeLineEndings(skslCode);
        _compiled = SkSl.Compile(normalized, cacheKey, useCache, onError);
        _ownCompiled = !useCache;
    }

    public void DisposeCompiled()
    {
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
    /// </summary>
    public SKShader ApplyTo(SKPaint paint, SKImage source, float width, float height)
    {
        if (_compiled == null) return null;

        using var textureShader = CreateTextureShader(source);
        using var uniforms = CreateUniforms(width, height, source?.Width ?? width, source?.Height ?? height);
        using var children = CreateChildren(textureShader);

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

        using var uniforms = CreateUniforms(width, height, width, height);
        using var children = new SKRuntimeEffectChildren(_compiled);

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
    /// Override to add custom uniforms for specialized shaders.
    /// </summary>
    public virtual SKRuntimeEffectUniforms CreateUniforms(
        float viewportWidth, float viewportHeight,
        float imageWidth, float imageHeight)
    {
        var uniforms = new SKRuntimeEffectUniforms(_compiled);

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
    /// Override to add additional textures (iImage2, etc.).
    /// </summary>
    public virtual SKRuntimeEffectChildren CreateChildren(SKShader primaryTexture)
    {
        var children = new SKRuntimeEffectChildren(_compiled);
        if (primaryTexture != null)
        {
            children.Add("iImage1", primaryTexture);
        }
        return children;
    }

    /// <summary>
    /// Creates a shader from an SKImage for use as a texture uniform.
    /// </summary>
    public SKShader CreateTextureShader(SKImage source)
    {
        if (source == null) return null;
        var sampling = new SKSamplingOptions(FilterMode, MipmapMode);
        return source.ToShader(TileMode, TileMode, sampling);
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
