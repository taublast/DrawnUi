using System.Drawing;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A TEMPLATED SkiaCarousel with RecyclingTemplate.Disabled must realize one cell per item.
/// Repro for ShadersCarouselDemo: InitializeChildren asks the factory for every index and
/// NREs when one comes back null, leaving the carousel blank.
/// </summary>
public class CarouselTemplatedTests
{
    private readonly ITestOutputHelper _output;

    public CarouselTemplatedTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class Item
    {
        public int Id { get; set; }
    }

    [Theory]
    [InlineData(RecyclingTemplate.Disabled)]
    [InlineData(RecyclingTemplate.Enabled)]
    public void RealizesEveryCell(RecyclingTemplate recycling)
    {
        const int count = 10;
        var host = new HeadlessCanvasHost(400, 700, scale: 1f, background: Colors.Black);

        var items = Enumerable.Range(0, count).Select(i => new Item { Id = i }).ToList();

        var carousel = new SkiaCarousel
        {
            Tag = "Carousel",
            HeightRequest = 350,
            HorizontalOptions = LayoutOptions.Fill,
            RecyclingTemplate = recycling,
            ItemTemplate = new DataTemplate(() => new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.DarkSlateBlue,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            }),
            ItemsSource = items,
        };

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { carousel }
        };

        host.AdvanceFrames(8);

        _output.WriteLine($"{recycling}: ChildrenTotal={carousel.ChildrenTotal} MaxIndex={carousel.MaxIndex} " +
                          $"SnapPoints={carousel.SnapPoints?.Count}");

        Assert.Equal(count, carousel.ChildrenTotal);
        // the real symptom: InitializeChildren bailing out on a refused cell left no snap points
        Assert.Equal(count, carousel.SnapPoints?.Count ?? 0);

        if (recycling == RecyclingTemplate.Disabled)
        {
            // one cell per item is the contract of Disabled, and they are all rented at once
            var nulls = new List<int>();
            for (int i = 0; i < count; i++)
            {
                var view = carousel.ChildrenFactory.GetViewForIndex(i);
                if (view == null) nulls.Add(i);
                else carousel.ChildrenFactory.ReleaseViewInUseForIndex(view.ContextIndex, view);
            }

            _output.WriteLine($"{recycling}: null views at [{string.Join(",", nulls)}] " +
                              $"PoolMaxSize={carousel.ChildrenFactory.PoolMaxSize}");

            Assert.True(nulls.Count == 0, $"no view realized for indexes {string.Join(",", nulls)}");
        }
    }
}
