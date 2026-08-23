using System.Drawing;
using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// AspectScale must follow the FINAL arranged rect. It is computed in OnMeasuring from
/// the measure constraints, so whenever layout lands on a different size (star columns,
/// spans, resize) OnLayoutChanged has to recompute it — otherwise the image keeps a scale
/// belonging to another box and renders zoomed in/out.
/// </summary>
public class AspectScaleRelayoutTests
{
    private readonly ITestOutputHelper _output;

    public AspectScaleRelayoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static SKImage MakeSource(int w = 1088, int h = 1088)
    {
        var info = new SKImageInfo(w, h);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(20, 60, 20));
        using var paint = new SKPaint { Color = SKColors.Red };
        // marker square at the exact centre, 1/8 of the source
        surface.Canvas.DrawRect(new SKRect(w * 0.4375f, h * 0.4375f, w * 0.5625f, h * 0.5625f), paint);
        return surface.Snapshot();
    }

    private static (int min, int max) RedSpanX(SKBitmap bmp, int left, int right, int y)
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

        return (first, last);
    }

    [Fact]
    public void ResizedAfterLoad_RecomputesAspectScale()
    {
        const int hostW = 400, hostH = 400;

        var host = new HeadlessCanvasHost(hostW, hostH, scale: 1f, background: Colors.Black);

        var image = new SkiaImage
        {
            Aspect = TransformAspect.AspectCover,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        var tile = new SkiaShape
        {
            Type = ShapeType.Rectangle,
            WidthRequest = 320,
            HeightRequest = 320,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Children = new List<SkiaControl>
            {
                new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl> { image }
                }
            }
        };

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { tile }
        };

        image.SetImageInternal(MakeSource());
        host.AdvanceFrames(4);

        var scaleBig = image.AspectScale.X;

        // now the tile becomes much smaller — like a star column resolving to its real width
        tile.WidthRequest = 160;
        tile.HeightRequest = 160;
        host.AdvanceFrames(6);

        var scaleSmall = image.AspectScale.X;

        using var snapshot = host.Canvas.CanvasView.Surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snapshot);

        // the centre marker is 1/8 of the source, so inside a correctly covered 160px tile
        // it must be ~20px wide; a stale scale from the 320px tile doubles that
        var (first, last) = RedSpanX(bmp, 0, 160, 80);
        var markerWidth = last - first + 1;

        _output.WriteLine($"scale 320px tile = {scaleBig:0.0000}");
        _output.WriteLine($"scale 160px tile = {scaleSmall:0.0000} (expected ~{160f / 1088f:0.0000})");
        _output.WriteLine($"marker width on screen = {markerWidth}px (expected ~20px)");

        Assert.True(first >= 0, "marker not rendered");
        Assert.InRange(scaleSmall, 160f / 1088f * 0.9f, 160f / 1088f * 1.1f);
        Assert.InRange(markerWidth, 14, 28);
    }
}
