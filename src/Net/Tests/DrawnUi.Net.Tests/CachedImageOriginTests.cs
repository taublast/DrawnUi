using System.Drawing;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// CachedImageOrigin must report where the CACHE TEXTURE starts, not where the control box
/// is. Image caches are recorded into a surface sized after CachedObject.Bounds, which
/// GetCacheArea inflates by shadow/effects margins (ExpandDirtyRegion here) - so an
/// expanded control's texture starts ABOVE and LEFT of its DrawingRect. Post-renderer
/// effects sample by screen coords, so reporting DrawingRect there shifts them by the margin.
/// </summary>
public class CachedImageOriginTests
{
    private readonly ITestOutputHelper _output;

    public CachedImageOriginTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(0)]  // no expansion: origin == DrawingRect
    [InlineData(12)] // expanded cache: origin == inflated Bounds
    public void FollowsCacheBounds(double expand)
    {
        var host = new HeadlessCanvasHost(400, 400, scale: 1f, background: Colors.Black);

        var shape = new SkiaShape
        {
            Type = ShapeType.Rectangle,
            BackgroundColor = Colors.DarkSlateBlue,
            WidthRequest = 200,
            HeightRequest = 120,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(40, 30, 0, 0),
            UseCache = SkiaCacheType.Image,
            ExpandDirtyRegion = new Thickness(expand),
        };

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { shape }
        };

        host.AdvanceFrames(4);

        var cache = shape.RenderObject;
        Assert.NotNull(cache);

        var origin = shape.CachedImageOrigin;
        _output.WriteLine($"expand={expand}: dest={shape.DrawingRect.Left:0},{shape.DrawingRect.Top:0} " +
                          $"cacheBounds={cache.Bounds.Left:0},{cache.Bounds.Top:0} origin={origin.X:0},{origin.Y:0}");

        Assert.Equal(cache.Bounds.Left, origin.X, 1);
        Assert.Equal(cache.Bounds.Top, origin.Y, 1);

        if (expand > 0)
        {
            Assert.True(shape.DrawingRect.Left - origin.X >= expand - 1,
                $"expanded cache origin should sit {expand}px left of the box, got {shape.DrawingRect.Left - origin.X:0}");
        }
    }
}
