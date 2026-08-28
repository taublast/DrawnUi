using DrawnUi.Draw;
using Microsoft.Maui.Controls;
using SkiaSharp;
using Xunit;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    /// <summary>
    /// Measure-vs-arrange consistency sweep (2026-08-28). Every test here is a case where a control's
    /// internal layout used to be computed for one constraint while the control was arranged at another.
    /// </summary>
    public class LayoutSweepTests : DrawnTestsBase
    {
        public LayoutSweepTests()
        {
            Super.Screen.Density = 1;
        }

        private static SkiaControl Box(float w, float h) => new SkiaControl { WidthRequest = w, HeightRequest = h };

        private static SkiaLayout CenteredRow() => new SkiaLayout
        {
            Type = LayoutType.Row,
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children = new List<SkiaControl> { Box(90, 40), Box(90, 40), Box(90, 40) }
        };

        private static SkiaLayout Wrap(int n, float w, float h, LayoutOptions horizontal) => new SkiaLayout
        {
            Type = LayoutType.Wrap,
            Spacing = 0,
            HorizontalOptions = horizontal,
            Children = Enumerable.Range(0, n).Select(_ => Box(w, h)).ToList<SkiaControl>()
        };

        private static SkiaLayout Grid(bool fill = true) => new SkiaLayout
        {
            Type = LayoutType.Grid,
            ColumnSpacing = 0,
            RowSpacing = 0,
            HorizontalOptions = fill ? LayoutOptions.Fill : LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Fill,
        };

        [Fact]
        public void Grid_ImplicitAutoColumn_FillStack_CenteredRowIsCenteredAtFullWidth()
        {
            // ArtOfFoto exposure page: grid without ColumnDefinitions, Fill stack in row 1 holding centered rows
            var row = CenteredRow();
            var stack = new SkiaLayout
            {
                Type = LayoutType.Column, Spacing = 0, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 100,
                Children = new List<SkiaControl> { row }
            };
            var grid = Grid();
            grid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(100, GridUnitType.Absolute)),
            };
            grid.AddSubView(Box(10, 10).WithRow(0));
            grid.AddSubView(stack.WithRow(1));

            grid.CommitInvalidations();
            grid.Measure(375, 800, 1);

            Assert.Equal(375, stack.MeasuredSize.Pixels.Width);
            Assert.Equal(375, grid.GridStructureMeasured.GetCellBoundsFor(stack, 0, 0).Width);
        }

        [Fact]
        public void Grid_FillWrapInAutoColumn_DoesNotInflateTrackPastGrid()
        {
            // constraint-dependent Fill child measured unconstrained must be clamped to the grid, then wrap
            var wrap = Wrap(10, 50, 20, LayoutOptions.Fill);
            var grid = Grid();
            grid.AddSubView(wrap);

            grid.CommitInvalidations();
            grid.Measure(300, 700, 1);

            Assert.Equal(300, grid.GridStructureMeasured.Columns[0].Size);
            Assert.Equal(300, wrap.MeasuredSize.Pixels.Width);
            Assert.Equal(40, wrap.MeasuredSize.Pixels.Height); // 10 x 50px boxes in 300px = 2 rows of 20
        }

        [Fact]
        public void Grid_InfiniteHeight_DoesNotStretchLastRowToInfinity()
        {
            // Fill grid as content of a vertical scroll
            var grid = Grid();
            grid.AddSubView(Box(50, 20));

            grid.CommitInvalidations();
            var measured = grid.Measure(300, float.PositiveInfinity, 1);

            Assert.True(double.IsFinite(grid.GridStructureMeasured.Rows[0].Size));
            Assert.Equal(20, measured.Pixels.Height);
        }

        [Fact]
        public void Grid_ImplicitAutoColumns_FillWrapInNonLastColumn_IsNotZeroWidth()
        {
            var wrap = Wrap(10, 50, 20, LayoutOptions.Fill);
            var grid = Grid();
            grid.AddSubView(wrap.WithColumn(0));
            grid.AddSubView(Box(30, 20).WithColumn(1));

            grid.CommitInvalidations();
            grid.Measure(400, 700, 1);

            Assert.True(wrap.MeasuredSize.Pixels.Width > 0, "Fill wrap in a non-last implicit Auto column collapsed to 0");
            // Auto/Auto: the unconstrained Fill wrap (500) is clamped to the grid (400); like MAUI, Auto tracks
            // do not shrink for siblings, so the 30px column overflows. Point of the test: no collapse to 0.
            Assert.Equal(400, wrap.MeasuredSize.Pixels.Width);
            Assert.Equal(40, wrap.MeasuredSize.Pixels.Height);
        }

        [Fact]
        public void Arrange_FillAxisWiderThanMeasured_RemeasuresWithFinalBox()
        {
            var wrap = Wrap(10, 50, 20, LayoutOptions.Fill);
            var stack = new SkiaLayout
            {
                Type = LayoutType.Column, Spacing = 0, HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl> { wrap }
            };

            stack.CommitInvalidations();
            stack.Measure(300, 700, 1);
            Assert.Equal(300, stack.MeasuredSize.Pixels.Width);
            Assert.Equal(40, wrap.MeasuredSize.Pixels.Height);

            stack.Arrange(new SKRect(0, 0, 600, 700), 600, 700, 1);

            Assert.Equal(600, stack.MeasuredSize.Pixels.Width);
            Assert.Equal(600, wrap.MeasuredSize.Pixels.Width);
            Assert.Equal(20, wrap.MeasuredSize.Pixels.Height); // 10 x 50 = 500 fits one row at 600
        }

        [Fact]
        public void MaximumWidthRequest_CapsArrangeOnFillAxis()
        {
            var layout = new SkiaLayout { Type = LayoutType.Absolute, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 50, MaximumWidthRequest = 300 };
            layout.CommitInvalidations();
            layout.Measure(400, 100, 1);
            Assert.Equal(300, layout.MeasuredSize.Pixels.Width);

            layout.Arrange(new SKRect(0, 0, 400, 100), 400, 100, 1);
            Assert.Equal(300, layout.DrawingRect.Width);
        }
    }
}
