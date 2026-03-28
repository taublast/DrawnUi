using DrawnUi.Draw;
using SkiaSharp;
using Xunit;

namespace UnitTests;

public class SkiaVisualComparisonTests
{
    [Fact]
    public void Shadow_UsesVisualStateOnly()
    {
        var first = new SkiaShadow
        {
            Opacity = 0.5,
            Color = Colors.Black,
            X = 2,
            Y = 3,
            Blur = 4,
            ShadowOnly = true,
            Tag = "first"
        };

        var second = new SkiaShadow
        {
            Opacity = 0.5,
            Color = Colors.Black,
            X = 2,
            Y = 3,
            Blur = 4,
            ShadowOnly = true,
            Tag = "second"
        };

        Assert.Equal(0, first.CompareTo(second));
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(0, first.CompareTo("not-a-shadow"));
        Assert.False(first.Equals("not-a-shadow"));
    }

    [Fact]
    public void Point_UsesCoordinatesOnly()
    {
        var first = new SkiaPoint(10, 20);
        var second = new SkiaPoint(10, 20);
        var third = new SkiaPoint(11, 20);

        Assert.Equal(0, first.CompareTo(second));
        Assert.True(first == second);
        Assert.NotEqual(0, first.CompareTo(third));
        Assert.Equal(0, first.CompareTo("not-a-point"));
        Assert.False(first.Equals("not-a-point"));
    }

    [Fact]
    public void ShaderEffect_UsesRenderAffectingStateOnly()
    {
        var first = CreateShaderEffect();
        var second = CreateShaderEffect();

        first.AquiredBackground = true;

        Assert.Equal(0, first.CompareTo(second));
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(0, first.CompareTo("not-a-shader"));
        Assert.False(first.Equals("not-a-shader"));

        second.MouseCurrent = new PointF(4, 5);

        Assert.NotEqual(0, first.CompareTo(second));
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void ScaledSize_UsesPixelsOnly()
    {
        var first = ScaledSize.FromUnits(10, 20, 2);
        var second = ScaledSize.FromPixels(20, 40, true, true, 5);
        var third = ScaledSize.FromPixels(21, 40, 2);

        Assert.Equal(0, first.CompareTo(second));
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(0, first.CompareTo(third));
        Assert.Equal(0, first.CompareTo("not-a-size"));
        Assert.False(first.Equals("not-a-size"));
    }

    private static SkiaShaderEffect CreateShaderEffect()
    {
        return new SkiaShaderEffect
        {
            UseBackground = PostRendererEffectUseBackgroud.Once,
            UseContext = true,
            AutoCreateInputTexture = false,
            BlendMode = SKBlendMode.Plus,
            ShaderCode = "return half4(1);",
            ShaderSource = "Shaders/test.sksl",
            ShaderTemplate = "Shaders/template.sksl",
            FilterMode = SKFilterMode.Linear,
            MipmapMode = SKMipmapMode.Linear,
            TileMode = SKShaderTileMode.Mirror,
            TimeSeconds = 3.5f,
            MouseCurrent = new PointF(1, 2),
            MouseInitial = new PointF(3, 4)
        };
    }
}