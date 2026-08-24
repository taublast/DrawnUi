namespace DrawnUi.Draw;

/// <summary>
/// Two-texture shader effect driven by a 0..1 <see cref="Progress"/>, for slide transitions
/// (<see cref="Controls.SkiaShaderCarousel"/>). The shader receives the usual
/// <see cref="ShaderDoubleTexturesEffect"/> inputs (<c>iImage1</c> = from, <c>iImage2</c> = to) plus
/// <c>progress</c> and <c>ratio</c> (width / height) uniforms. Without an explicit
/// <see cref="SkiaShaderEffect.ShaderTemplate"/> the embedded gl-transitions adapter template is used, so any
/// transition from https://github.com/gl-transitions/gl-transitions ported to SkSL
/// (a <c>transition(vec2 uv)</c> function using <c>getFromColor</c>/<c>getToColor</c>) works as <see cref="SkiaShaderEffect.ShaderSource"/>.
/// </summary>
public class ShaderTransitionEffect : ShaderDoubleTexturesEffect
{
    /// <summary>
    /// gl-transitions adapter: declares the uniforms and <c>getFromColor</c>/<c>getToColor</c> sampling
    /// with the GLSL bottom-left to SkSL top-left Y flip. The transition source replaces <c>//script-goes-here</c>.
    /// </summary>
    public const string DefaultTemplate = @"
uniform float ratio; // width / height
uniform float progress; // 0.0 - 1.0
uniform shader iImage1; // Texture
uniform shader iImage2; // Texture for backside
uniform float2 iOffset; // Top-left corner of DrawingRect
uniform float2 iResolution; // Viewport resolution (pixels)
uniform float2 iImageResolution; // iImage1 resolution (pixels)
uniform float  iTime; // Shader playback time (s)
uniform float4 iMouse; // Mouse drag pos=.xy Click pos=.zw (pixels)

//In GLSL, the texture coordinate origin is at the bottom-left corner,
//whereas in SKSL the origin is at the top-left corner.

vec4 getFromColor(vec2 uv) {
    vec2 adjustedUV = float2(uv.x, 1.0 - uv.y) * iImageResolution;
    return iImage1.eval(adjustedUV);
}

vec4 getToColor(vec2 uv) {
    vec2 adjustedUV = float2(uv.x, 1.0 - uv.y) * iImageResolution;
    return iImage2.eval(adjustedUV);
}

//script-goes-here
";

    /// <summary>
    /// Transition progress from 0 (fully <see cref="ShaderDoubleTexturesEffect.ControlFrom"/>) to 1
    /// (fully <see cref="ShaderDoubleTexturesEffect.ControlTo"/>), passed to the shader as the <c>progress</c> uniform.
    /// Call <see cref="SkiaEffect.Update"/> after changing it.
    /// </summary>
    public double Progress { get; set; }

    protected override void CompileShader(string shaderCode, bool useCache = true, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(ShaderTemplate) && string.IsNullOrEmpty(_template))
        {
            _template = DefaultTemplate;
        }

        base.CompileShader(shaderCode, useCache, onError);
    }

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        uniforms["progress"] = (float)Progress;
        uniforms["ratio"] = (float)(destination.Width / destination.Height);

        return uniforms;
    }
}
