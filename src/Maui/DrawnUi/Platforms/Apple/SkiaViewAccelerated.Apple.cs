using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Views;

public partial class SkiaViewAccelerated : SKGLView, ISkiaDrawable
{
    protected void TestApple()
    {

    }

    void test()
    {
        string sksl = "...";
        SKShader _noiseImageShader = null;
        SKRuntimeShaderBuilder _shaderBuilder = null;

        var noiseImage = SKImage.FromEncodedData("noise.png");
        _noiseImageShader = SKShader.CreateImage(noiseImage);

        var effect = SKRuntimeEffect.CreateShader(sksl, out var str);
        if (effect != null)
        {
            _shaderBuilder = new SKRuntimeShaderBuilder(effect);
            _shaderBuilder.Uniforms["randomSeed"] = 0f;
            _shaderBuilder.Uniforms["noiseResolution"] = new SKSize(noiseImage.Width, noiseImage.Height);
        }

        //var filter = SKImageFilter.CreateRuntimeShader(_shaderBuilder, 0f, "src", null);

        var filter = _shaderBuilder.Build();

        using var paint = new SKPaint
        {
            Shader = filter
            //            ImageFilter = filter,
        };
    }

}
