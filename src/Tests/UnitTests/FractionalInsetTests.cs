using DrawnUi.Draw;
using Microsoft.Maui.Controls;
using SkiaSharp;
using Xunit;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    /// <summary>
    /// Fractional insets (2pt padding at scale 1.25 = 2.5px) must be reserved at DRAW exactly as
    /// MEASURE reserved them: rounded per side. Subtracting the raw value took 5px where measure
    /// reserved 4, so children were arranged 1px narrower than the size they were measured for and
    /// clipped their content (button captions losing their last glyph, 2026-09-07).
    /// </summary>
    public class FractionalInsetTests : DrawnTestsBase
    {
        private const float Scale = 1.25f; // Windows at 125%, the case that surfaced the bug

        public FractionalInsetTests()
        {
            Super.Screen.Density = Scale;
        }

        private static SkiaControl Box(float w, float h) => new SkiaControl { WidthRequest = w, HeightRequest = h };

        private static SkiaLayout Padded(SkiaControl child, double padding) => new SkiaLayout
        {
            Type = LayoutType.Absolute,
            Padding = new Thickness(padding),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Children = new List<SkiaControl> { child }
        };

        [Fact]
        public void FractionalPadding_ChildrenRectMatchesWhatMeasureReserved()
        {
            // 2pt padding at 1.25 = 2.5px per side: measure reserves round(2.5)=2 per side
            var child = Box(80, 20);
            var layout = Padded(child, 2);

            layout.CommitInvalidations();
            var measured = layout.Measure(float.PositiveInfinity, float.PositiveInfinity, Scale);

            var reserved = (float)Math.Round(2 * Scale) * 2;
            var forChildren = layout.GetDrawingRectForChildren(
                new SKRect(0, 0, measured.Pixels.Width, measured.Pixels.Height), Scale);

            Assert.Equal(measured.Pixels.Width - reserved, forChildren.Width);
            Assert.Equal(measured.Pixels.Height - reserved, forChildren.Height);
            Assert.Equal(child.MeasuredSize.Pixels.Width, forChildren.Width);
        }

        [Fact]
        public void FractionalPadding_ChildrenRectDoesNotDependOnPosition()
        {
            // rounding the absolute edges made the reserved amount depend on where the parent sat:
            // round(0 + 2.5) = 2 but round(108.5) = 108, so the same layout lost a pixel at some offsets
            var layout = Padded(Box(80, 20), 2);
            layout.CommitInvalidations();
            var measured = layout.Measure(float.PositiveInfinity, float.PositiveInfinity, Scale);

            var w = measured.Pixels.Width;
            var h = measured.Pixels.Height;
            var atZero = layout.GetDrawingRectForChildren(new SKRect(0, 0, w, h), Scale);

            foreach (var offset in new[] { 0.5f, 1f, 7f, 85f, 85.5f, 122.25f })
            {
                var moved = layout.GetDrawingRectForChildren(
                    new SKRect(offset, offset, offset + w, offset + h), Scale);

                Assert.Equal(atZero.Width, moved.Width);
                Assert.Equal(atZero.Height, moved.Height);
            }
        }

        [Fact]
        public void FractionalPadding_ChildIsArrangedAtItsMeasuredSize()
        {
            // end-to-end: children are positioned by the draw pass, so render one frame and check that
            // the child was not squeezed below the size it measured for
            var child = Box(80, 20);
            var layout = Padded(child, 2);

            layout.CommitInvalidations();
            var measured = layout.Measure(float.PositiveInfinity, float.PositiveInfinity, Scale);
            var area = new SKRect(0, 0, measured.Pixels.Width, measured.Pixels.Height);

            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(area);
            var skiaContext = new SkiaDrawingContext
            {
                Superview = null,
                FrameTimeNanos = Super.GetCurrentTimeNanos(),
                Canvas = canvas,
                Width = canvas.DeviceClipBounds.Width,
                Height = canvas.DeviceClipBounds.Height
            };

            layout.Render(new DrawingContext(skiaContext, area, Scale));
            recorder.EndRecording().Dispose();

            Assert.True(child.DrawingRect.Width >= child.MeasuredSize.Pixels.Width,
                $"child arranged into {child.DrawingRect.Width} but measured {child.MeasuredSize.Pixels.Width}");
        }

        [Fact]
        public void IntegerInsetsAtWholeScale_Unchanged()
        {
            // guard: nothing changes where the inset already lands on whole pixels
            Super.Screen.Density = 2f;
            var layout = Padded(Box(80, 20), 3);
            layout.CommitInvalidations();
            var measured = layout.Measure(float.PositiveInfinity, float.PositiveInfinity, 2f);

            var forChildren = layout.GetDrawingRectForChildren(
                new SKRect(0, 0, measured.Pixels.Width, measured.Pixels.Height), 2f);

            Assert.Equal(6, forChildren.Left);
            Assert.Equal(measured.Pixels.Width - 12, forChildren.Width);
        }
    }
}
