using System.Drawing;
using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A post-renderer shader effect (SkiaShaderEffect) on an UNCACHED image takes its input
/// texture from a surface snapshot. When the control is painted into a parent's CACHE
/// surface, that snapshot rect must be in the SAME space as the surface being drawn into.
/// If it is taken in canvas space instead, the effect samples the wrong region and the
/// error grows with the control's canvas offset — tiles at different positions/sizes then
/// render differently (intent-picker hero vs small tiles).
/// </summary>
public class ShaderEffectSnapshotTests
{
    private readonly ITestOutputHelper _output;

    public ShaderEffectSnapshotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string PassthroughShader = @"
uniform float4 iMouse;
uniform float  iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;
uniform float2 iOrigin;

half4 main(float2 fragCoord)
{
    float2 renderingScale = iImageResolution.xy / iResolution.xy;
    float2 inputCoord = (fragCoord - iOffset) * renderingScale;
    return iImage1.eval(inputCoord);
}
";

    /// <summary>Source with a full-height RED stripe exactly at its horizontal centre.</summary>
    private static SKImage MakeSource(int w = 400, int h = 400)
    {
        var info = new SKImageInfo(w, h);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(20, 60, 20));
        using var paint = new SKPaint { Color = SKColors.Red };
        surface.Canvas.DrawRect(new SKRect(w / 2f - 8, 0, w / 2f + 8, h), paint);
        return surface.Snapshot();
    }

    private static float RedCenterX(SKBitmap bmp, int left, int right, int y)
    {
        int first = -1, last = -1;
        for (int x = left; x < right; x++)
        {
            var c = bmp.GetPixel(x, y);
            if (c.Red > 120 && c.Green < 90 && c.Blue < 90)
            {
                if (first < 0) first = x;
                last = x;
            }
        }

        return first < 0 ? -1 : (first + last) / 2f;
    }

    [Theory]
    [InlineData(SkiaCacheType.None, 0)]
    [InlineData(SkiaCacheType.None, 200)]
    [InlineData(SkiaCacheType.Image, 0)]
    [InlineData(SkiaCacheType.Image, 200)]
    public void ShaderOnUncachedImage_SamplesItsOwnPixels(SkiaCacheType parentCache, int offsetX)
    {
        const int hostW = 400, hostH = 300;
        const float tileW = 180, tileH = 120;

        var host = new HeadlessCanvasHost(hostW, hostH, scale: 1f, background: Colors.Black);

        var image = new SkiaImage
        {
            Tag = "img",
            Aspect = TransformAspect.AspectCover,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            VisualEffects = { new SkiaShaderEffect { ShaderCode = PassthroughShader } }
        };

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    Tag = "tile",
                    Type = ShapeType.Rectangle,
                    WidthRequest = tileW,
                    HeightRequest = tileH,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(offsetX, 40, 0, 0),
                    UseCache = parentCache,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLayer
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            Children = new List<SkiaControl> { image }
                        }
                    }
                }
            }
        };

        image.SetImageInternal(MakeSource());
        host.AdvanceFrames(8);

        using var snapshot = host.Canvas.CanvasView.Surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snapshot);

        var bandLeft = offsetX;
        var bandRight = (int)(offsetX + tileW);
        var marker = RedCenterX(bmp, bandLeft, bandRight, 90);
        var expected = offsetX + tileW / 2f;

        _output.WriteLine($"cache={parentCache} offsetX={offsetX} marker={marker:0.0} expected={expected:0.0} delta={marker - expected:0.0}");

        Assert.True(marker >= 0, $"nothing rendered (cache={parentCache}, offsetX={offsetX})");
        Assert.True(Math.Abs(marker - expected) <= 3f,
            $"shader output shifted by {marker - expected:0.0}px (cache={parentCache}, offsetX={offsetX})");
    }
}
