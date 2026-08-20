using System.Drawing;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Star columns must resolve against the real viewport width when the grid sits inside a
/// vertical SkiaScroll (optionally under a padded SkiaStack). If the scroll measures its
/// content with an unbounded width, "*,*" resolves too wide and the grid overflows the
/// screen — the intent-picker symptom: the second column runs off the right edge and its
/// tiles are wider (so images cover at a different scale) than the first column's.
/// </summary>
public class GridInScrollStackTests
{
    private readonly ITestOutputHelper _output;

    public GridInScrollStackTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static SkiaControl Cell(string tag) => new SkiaShape
    {
        Tag = tag,
        Type = ShapeType.Rectangle,
        BackgroundColor = Colors.DarkSlateBlue,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        HeightRequest = 100,
    };

    [Theory]
    [InlineData(false, 1f)]   // grid directly in the scroll
    [InlineData(true, 1f)]    // grid inside a padded stack inside the scroll (picker structure)
    [InlineData(true, 1.25f)] // ... at 125% DPI, like the Windows app
    [InlineData(true, 2f)]
    public void StarColumns_FitViewport(bool insideStack, float scale)
    {
        const int hostW = 485, hostH = 692;
        const double padding = 20;
        const double spacing = 12;

        var host = new HeadlessCanvasHost(hostW, hostH, scale: scale, background: Colors.Black);

        var left = Cell("left");
        var right = Cell("right");

        var grid = new SkiaGrid
        {
            Tag = "grid",
            ColumnSpacing = spacing,
            RowSpacing = spacing,
            HorizontalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                left.WithColumn(0).WithRow(0),
                right.WithColumn(1).WithRow(0),
            }
        }
        .WithColumnDefinitions("*,*")
        .WithRowDefinitions("100");

        SkiaControl content = insideStack
            ? new SkiaStack
            {
                Padding = new Thickness(padding, 24, padding, 16),
                Spacing = 0,
                Children = new List<SkiaControl> { grid }
            }
            : grid;

        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = content
        };

        host.AdvanceFrames(6);

        // everything below is in PIXELS: padding/spacing are points, so scale them
        var expectedAvailable = insideStack ? hostW - padding * 2 * scale : hostW;
        var expectedColumn = (expectedAvailable - spacing * scale) / 2;

        _output.WriteLine($"insideStack={insideStack} scale={scale}");
        _output.WriteLine($"  grid  = {grid.DrawingRect.Left:0}..{grid.DrawingRect.Right:0} (w {grid.DrawingRect.Width:0}), viewport {hostW}");
        _output.WriteLine($"  left  = {left.DrawingRect.Left:0}..{left.DrawingRect.Right:0} (w {left.DrawingRect.Width:0})");
        _output.WriteLine($"  right = {right.DrawingRect.Left:0}..{right.DrawingRect.Right:0} (w {right.DrawingRect.Width:0})");
        _output.WriteLine($"  expected column width = {expectedColumn:0}");

        Assert.True(right.DrawingRect.Right <= hostW + 1,
            $"grid overflows the viewport: right column ends at {right.DrawingRect.Right:0}, viewport is {hostW}");
        Assert.True(Math.Abs(left.DrawingRect.Width - right.DrawingRect.Width) <= 1,
            $"columns not equal: {left.DrawingRect.Width:0} vs {right.DrawingRect.Width:0}");
        Assert.True(Math.Abs(left.DrawingRect.Width - expectedColumn) <= 2,
            $"column width {left.DrawingRect.Width:0}, expected {expectedColumn:0}");
    }

    /// <summary>
    /// The picker's grid does not set HorizontalOptions and relies on the SkiaGrid alias
    /// default. If that default does not fill, star columns resolve against the children's
    /// desired width and the grid overflows the viewport.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    public void StarColumns_WithoutExplicitFill_FitViewport(float scale)
    {
        const int hostW = 485, hostH = 692;
        const double padding = 20;
        const double spacing = 12;

        var host = new HeadlessCanvasHost(hostW, hostH, scale: scale, background: Colors.Black);

        var left = Cell("left");
        var right = Cell("right");

        var grid = new SkiaGrid
        {
            Tag = "grid",
            ColumnSpacing = spacing,
            RowSpacing = spacing,
            // NOTE: no HorizontalOptions here — exactly like IntentPickerOverlay.CreateGrid
            Children = new List<SkiaControl>
            {
                left.WithColumn(0).WithRow(0),
                right.WithColumn(1).WithRow(0),
            }
        }
        .WithColumnDefinitions("*,*")
        .WithRowDefinitions("100");

        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Padding = new Thickness(padding, 24, padding, 16),
                Spacing = 0,
                Children = new List<SkiaControl> { grid }
            }
        };

        host.AdvanceFrames(6);

        var expectedColumn = (hostW - padding * 2 * scale - spacing * scale) / 2;

        _output.WriteLine($"scale={scale} (no explicit Fill on grid)");
        _output.WriteLine($"  grid  = {grid.DrawingRect.Left:0}..{grid.DrawingRect.Right:0} (w {grid.DrawingRect.Width:0}), viewport {hostW}");
        _output.WriteLine($"  left  = {left.DrawingRect.Left:0}..{left.DrawingRect.Right:0} (w {left.DrawingRect.Width:0})");
        _output.WriteLine($"  right = {right.DrawingRect.Left:0}..{right.DrawingRect.Right:0} (w {right.DrawingRect.Width:0})");
        _output.WriteLine($"  expected column width = {expectedColumn:0}");

        Assert.True(right.DrawingRect.Right <= hostW + 1,
            $"grid overflows the viewport: right column ends at {right.DrawingRect.Right:0}, viewport is {hostW}");
    }
}
