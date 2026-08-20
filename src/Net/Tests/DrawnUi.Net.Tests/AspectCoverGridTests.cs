using System.Drawing;
using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Repro of the intent-picker layout: SkiaGrid with star columns where one tile spans two
/// rows. Each tile = SkiaShape(Fill) &gt; SkiaLayer &gt; SkiaImage(AspectCover, Fill).
/// AspectCover must center the source in EVERY tile, hero included. Verified on PIXELS
/// (a marker column painted at the source centre) so cache stretching is caught too.
/// </summary>
public class AspectCoverGridTests
{
    private readonly ITestOutputHelper _output;

    public AspectCoverGridTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Source with a full-height RED stripe exactly at its horizontal centre.</summary>
    private static SKImage MakeSource(int w = 768, int h = 1024)
    {
        var info = new SKImageInfo(w, h);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(20, 60, 20));
        using var paint = new SKPaint { Color = SKColors.Red };
        surface.Canvas.DrawRect(new SKRect(w / 2f - 8, 0, w / 2f + 8, h), paint);
        return surface.Snapshot();
    }

    private static SkiaControl Tile(string tag, SkiaCacheType cache, out SkiaImage image)
    {
        var img = new SkiaImage
        {
            Tag = tag,
            Aspect = TransformAspect.AspectCover,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        image = img;

        return new SkiaShape
        {
            Tag = tag + "_shape",
            Type = ShapeType.Rectangle,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            UseCache = cache,
            Children = new List<SkiaControl>
            {
                new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl> { img }
                }
            }
        };
    }

    /// <summary>Centre x of the red marker inside the given pixel band, or -1.</summary>
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

    // NOTE: ImageDoubleBuffered is intentionally NOT covered — its bake is async on the
    // offscreen rendering service, which the headless harness does not pump, so nothing
    // ever lands on the surface here. Cache correctness for that mode needs a device run.
    [Theory]
    [InlineData(SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Image)]
    public void HeroSpanningTwoRows_CropsCentered(SkiaCacheType cache)
    {
        const int hostW = 400, hostH = 400;
        const float tileH = 118, spacing = 12;

        var host = new HeadlessCanvasHost(hostW, hostH, scale: 1f, background: Colors.Black);

        var hero = Tile("hero", cache, out var heroImage);
        var small = Tile("small", cache, out var smallImage);
        var small2 = Tile("small2", cache, out var small2Image);

        var grid = new SkiaGrid
        {
            ColumnSpacing = spacing,
            RowSpacing = spacing,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            Children = new List<SkiaControl>
            {
                hero.WithColumn(0).WithRow(0).WithRowSpan(2),
                small.WithColumn(1).WithRow(0),
                small2.WithColumn(1).WithRow(1),
            }
        }
        .WithColumnDefinitions("*,*")
        .WithRowDefinitions($"{tileH},{tileH}");

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { grid }
        };

        heroImage.SetImageInternal(MakeSource());
        smallImage.SetImageInternal(MakeSource());
        small2Image.SetImageInternal(MakeSource());

        // several frames: async double-buffered bakes need time to land
        host.AdvanceFrames(12);

        using var snapshot = host.Canvas.CanvasView.Surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snapshot);

        // column bands from the grid geometry
        var colW = (hostW - spacing) / 2f;
        var heroBand = (left: 0, right: (int)colW);
        var smallBand = (left: (int)(colW + spacing), right: hostW);

        var heroCenter = RedCenterX(bmp, heroBand.left, heroBand.right, 60);
        var smallCenter = RedCenterX(bmp, smallBand.left, smallBand.right, 60);

        var heroExpected = (heroBand.left + heroBand.right) / 2f;
        var smallExpected = (smallBand.left + smallBand.right) / 2f;

        _output.WriteLine($"cache={cache}");
        foreach (var img in new[] { heroImage, smallImage, small2Image })
        {
            var d = img.DrawingRect;
            var expectedScale = Math.Max(d.Width, d.Height) / 1088f;
            _output.WriteLine($"  {img.Tag}: drawingRect={d.Width:0}x{d.Height:0} aspect={img.AspectScale.X:0.0000} expected={expectedScale:0.0000}");
        }
        _output.WriteLine($"hero  marker={heroCenter:0.0} expected={heroExpected:0.0} delta={heroCenter - heroExpected:0.0}");
        _output.WriteLine($"small marker={smallCenter:0.0} expected={smallExpected:0.0} delta={smallCenter - smallExpected:0.0}");

        Assert.True(heroCenter >= 0, "hero tile did not render the marker");
        Assert.True(smallCenter >= 0, "small tile did not render the marker");

        Assert.True(Math.Abs(heroCenter - heroExpected) <= 3f,
            $"hero off-center by {heroCenter - heroExpected:0.0}px (cache={cache})");
        Assert.True(Math.Abs(smallCenter - smallExpected) <= 3f,
            $"small off-center by {smallCenter - smallExpected:0.0}px (cache={cache})");
    }
}
