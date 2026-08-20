using System.Drawing;
using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// AspectCover must center the source in ANY destination: the visible crop differs with
/// the box aspect, but the image center must stay on the box center. Repro for the
/// picker's hero tile (RowSpan=2) drawing differently from the small tiles.
/// </summary>
public class AspectCoverTests
{
    private readonly ITestOutputHelper _output;

    public AspectCoverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static SKImage MakeSource(int w, int h)
    {
        var info = new SKImageInfo(w, h);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);
        // a bright marker at the exact center of the source
        using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = false };
        canvas.DrawRect(new SKRect(w / 2f - 4, h / 2f - 4, w / 2f + 4, h / 2f + 4), paint);
        return surface.Snapshot();
    }

    [Theory]
    // (tileWidth, tileHeight) — hero is tall, small tiles are wide, same source
    [InlineData(270, 310)]
    [InlineData(210, 118)]
    [InlineData(500, 100)]
    public void AspectCover_CentersSource(float tileW, float tileH)
    {
        var host = new HeadlessCanvasHost(600, 800, scale: 1f, background: Colors.Black);

        SkiaImage image = null;

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    WidthRequest = tileW,
                    HeightRequest = tileH,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Children =
                    {
                        new SkiaImage
                        {
                            Aspect = TransformAspect.AspectCover,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }.Assign(out image)
                    }
                }
            }
        };

        image.SetImageInternal(MakeSource(768, 1024));

        host.RenderFrame();
        host.RenderFrame();

        var dest = image.DrawingRect;
        var display = image.DisplayRect;

        _output.WriteLine($"tile={tileW}x{tileH} dest={dest.Left:0}:{dest.Top:0} {dest.Width:0}x{dest.Height:0} " +
                          $"aspect={image.AspectScale.X:0.000},{image.AspectScale.Y:0.000} " +
                          $"display={display.Left:0}:{display.Top:0} {display.Width:0}x{display.Height:0}");

        // cover: the scaled image must be at least as big as the box on both axes
        Assert.True(display.Width >= dest.Width - 1, $"width {display.Width} < dest {dest.Width}");
        Assert.True(display.Height >= dest.Height - 1, $"height {display.Height} < dest {dest.Height}");

        // and centered: same margin left/right, top/bottom
        var leftOver = display.Left - dest.Left;
        var rightOver = dest.Right - display.Right;
        var topOver = display.Top - dest.Top;
        var bottomOver = dest.Bottom - display.Bottom;

        Assert.True(Math.Abs(leftOver - rightOver) <= 1.5f,
            $"horizontally off-center: left={leftOver:0.0} right={rightOver:0.0}");
        Assert.True(Math.Abs(topOver - bottomOver) <= 1.5f,
            $"vertically off-center: top={topOver:0.0} bottom={bottomOver:0.0}");
    }
}
