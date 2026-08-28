using DrawnUi.Draw;
using Xunit;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    /// <summary>
    /// Column/Row second measure pass (ProcessSecondPass) must only change the PERPENDICULAR constraint
    /// of perpendicular-fill children. The main-axis constraint has to stay what the first pass used
    /// (possibly infinite, e.g. content of a scroll). Regression: a Fill-X child of an unbounded Column
    /// was re-measured with the final stack height, so anything inside sized from the height constraint
    /// (LockRatio, Fill descendants) ballooned to the whole stack size.
    /// </summary>
    public class SkiaLayoutSecondPassTests : DrawnTestsBase
    {
        private class MeasureSpy : SkiaLayout
        {
            public readonly List<float> HeightConstraints = new();
            public readonly List<float> WidthConstraints = new();

            public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
            {
                WidthConstraints.Add(widthConstraint);
                HeightConstraints.Add(heightConstraint);
                return base.OnMeasuring(widthConstraint, heightConstraint, scale);
            }
        }

        [Fact]
        public void ColumnSecondPass_KeepsInfiniteHeightForFillXChild()
        {
            var spy = new MeasureSpy
            {
                Type = LayoutType.Column,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaControl { HeightRequest = 40, HorizontalOptions = LayoutOptions.Fill }
                }
            };

            var stack = new SkiaLayout
            {
                Type = LayoutType.Column,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaControl { HeightRequest = 100, HorizontalOptions = LayoutOptions.Fill },
                    spy
                }
            };

            stack.CommitInvalidations();
            stack.Measure(300, float.PositiveInfinity, 1);

            Assert.NotEmpty(spy.HeightConstraints);
            Assert.All(spy.HeightConstraints, h => Assert.True(float.IsPositiveInfinity(h), $"second pass leaked finite height {h}"));
            Assert.Equal(40, spy.MeasuredSize.Pixels.Height);
            Assert.Equal(140, stack.MeasuredSize.Pixels.Height);
        }

        [Fact]
        public void RowSecondPass_KeepsInfiniteWidthForFillYChild()
        {
            var spy = new MeasureSpy
            {
                Type = LayoutType.Row,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaControl { WidthRequest = 40, VerticalOptions = LayoutOptions.Fill }
                }
            };

            var stack = new SkiaLayout
            {
                Type = LayoutType.Row,
                Spacing = 0,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaControl { WidthRequest = 100, VerticalOptions = LayoutOptions.Fill },
                    spy
                }
            };

            stack.CommitInvalidations();
            stack.Measure(float.PositiveInfinity, 300, 1);

            Assert.NotEmpty(spy.WidthConstraints);
            Assert.All(spy.WidthConstraints, w => Assert.True(float.IsPositiveInfinity(w), $"second pass leaked finite width {w}"));
            Assert.Equal(40, spy.MeasuredSize.Pixels.Width);
            Assert.Equal(140, stack.MeasuredSize.Pixels.Width);
        }
    }
}
