using System.Diagnostics;
using System.Drawing;
using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Cost of a per-frame shader effect on a SkiaImage, comparing the two texture paths:
/// - box matches the drawn image (display == DrawingRect) → effect reuses ScaledSource
/// - image overflows its box (height-driven AspectCover) → effect must snapshot the
///   surface every frame (Canvas.Flush + Surface.Snapshot)
/// Not a micro-benchmark, just an order-of-magnitude signal on a CPU surface.
/// </summary>
public class ShaderEffectPerfTests
{
    private readonly ITestOutputHelper _output;

    public ShaderEffectPerfTests(ITestOutputHelper output)
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

    private static SKImage MakeSquareSource(int size = 1088)
    {
        var info = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(30, 90, 30));
        return surface.Snapshot();
    }

    private static double MeasureMsPerFrame(float tileW, float tileH, int frames, out string path)
    {
        var host = new HeadlessCanvasHost(400, 500, scale: 1f, background: Colors.Black);

        var image = new SkiaImage
        {
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
                    Type = ShapeType.Rectangle,
                    WidthRequest = tileW,
                    HeightRequest = tileH,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(32, 20, 0, 0),
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
        host.AdvanceFrames(5); // warm up: load, first bake, shader compile

        // the real discriminator: a non-null CachedImage means the effect reuses that
        // texture, null means it snapshots the surface on every draw
        path = image.CachedImage.IsValid ? "ScaledSource reuse" : "SNAPSHOT per frame";

        var sw = Stopwatch.StartNew();
        host.AdvanceFrames(frames);
        sw.Stop();

        return sw.Elapsed.TotalMilliseconds / frames;
    }

    [Fact]
    public void PerFrameShader_SnapshotPathCost()
    {
        const int frames = 200;

        // same width; the tall one overflows horizontally (height-driven cover)
        var matched = MeasureMsPerFrame(270, 118, frames, out var pathMatched);
        var overflowing = MeasureMsPerFrame(270, 400, frames, out var pathOverflow);

        _output.WriteLine($"270x118  {pathMatched,-20} {matched:0.000} ms/frame");
        _output.WriteLine($"270x400  {pathOverflow,-20} {overflowing:0.000} ms/frame");
        _output.WriteLine($"delta = {overflowing - matched:0.000} ms/frame " +
                          $"({(matched > 0 ? overflowing / matched : 0):0.00}x)");

        Assert.True(matched > 0 && overflowing > 0);
    }
}
