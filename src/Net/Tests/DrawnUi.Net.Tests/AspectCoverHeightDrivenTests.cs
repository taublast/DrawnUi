using System.Drawing;
using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// AspectCover when the HEIGHT drives the scale (box taller than the source aspect):
/// the scaled image is wider than the box, so it must be cropped SYMMETRICALLY — a
/// subject at the source centre has to stay at the box centre. Repro for the intent
/// picker, where a square photo in a tall hero tile renders visibly off-centre while the
/// same photo in a short tile of identical width is centred.
/// </summary>
public class AspectCoverHeightDrivenTests
{
    private readonly ITestOutputHelper _output;

    public AspectCoverHeightDrivenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Square source with a narrow RED stripe at its exact horizontal centre.</summary>
    private static SKImage MakeSquareSource(int size = 1088)
    {
        var info = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(20, 60, 20));
        using var paint = new SKPaint { Color = SKColors.Red };
        surface.Canvas.DrawRect(new SKRect(size / 2f - 10, 0, size / 2f + 10, size), paint);
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

    [Theory]
    // width x height of the tile — same width, growing height crosses into height-driven cover
    [InlineData(270, 118, false)] // short  : width-driven
    [InlineData(270, 310, false)] // tall   : HEIGHT-driven — the picker's hero tile
    [InlineData(270, 118, true)]  // ... same, with the tile's shader effect
    [InlineData(270, 310, true)]
    [InlineData(270, 400, true)]
    public void SquareSource_StaysCentered(float tileW, float tileH, bool withShader)
    {
        const int hostW = 400, hostH = 500;
        const int tileLeft = 32;

        var host = new HeadlessCanvasHost(hostW, hostH, scale: 1f, background: Colors.Black);

        var image = new SkiaImage
        {
            Aspect = TransformAspect.AspectCover,
            HorizontalAlignment = DrawImageAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        if (withShader)
        {
            image.VisualEffects.Add(new SkiaShaderEffect { ShaderCode = PassthroughShader });
        }

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    WidthRequest = tileW,
                    HeightRequest = tileH,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(tileLeft, 20, 0, 0),
                    UseCache = SkiaCacheType.Image,
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

        image.SetImageInternal(MakeSquareSource());
        host.AdvanceFrames(6);

        using var snapshot = host.Canvas.CanvasView.Surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snapshot);

        var probeY = (int)(20 + tileH / 2);
        var marker = RedCenterX(bmp, tileLeft, (int)(tileLeft + tileW), probeY);
        var expected = tileLeft + tileW / 2f;

        var dest = image.DrawingRect;
        var display = image.DisplayRect;

        _output.WriteLine($"tile {tileW}x{tileH} shader={withShader}: aspect={image.AspectScale.X:0.0000} " +
                          $"dest={dest.Width:0}x{dest.Height:0} display={display.Left:0}..{display.Right:0} (w {display.Width:0})");
        _output.WriteLine($"  marker={marker:0.0} expected={expected:0.0} offset={marker - expected:0.0}");

        Assert.True(marker >= 0, "marker not rendered");
        Assert.True(Math.Abs(marker - expected) <= 2f,
            $"tile {tileW}x{tileH} shader={withShader}: subject off-centre by {marker - expected:0.0}px");
    }
}
