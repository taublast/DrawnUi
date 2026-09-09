using DrawnUi.Draw;
using Microsoft.Maui.Controls;
using SkiaSharp;
using Xunit;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    /// <summary>
    /// Fractional insets (2pt padding at scale 1.25 = 2.5px). Drawing used to subtract the raw inset
    /// while measuring reserved it rounded, so content was arranged into less room than it was measured
    /// for and clipped (button captions losing their last glyph, 2026-09-07). Drawing now reserves the
    /// SMALLER of the two, which can only give content more room than before — never less.
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

        private static readonly float[] Scales = { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f, 2.75f, 3f };
        private static readonly double[] Insets = { 0, 0.5, 1, 1.5, 2, 2.5, 3, 4, 6, 8, 10, 12, 16, 24 };

        [Fact]
        public void ContentInsetIsNeverMoreThanMeasuringReserved()
        {
            // the invariant that stops the clipping: content must fit the size it was measured for
            var rect = new SKRect(0, 0, 400, 400);

            foreach (var scale in Scales)
            foreach (var inset in Insets)
            {
                var reservedByMeasure = (float)Math.Round(inset * scale);
                var content = SkiaControl.ContractPixelsRectForContent(rect, scale, new Thickness(inset));
                var reservedByDraw = content.Left - rect.Left;

                Assert.True(reservedByDraw <= reservedByMeasure + 0.001f,
                    $"scale {scale} inset {inset}: draw reserved {reservedByDraw}, measure reserved {reservedByMeasure}");
            }
        }

        [Fact]
        public void ContentIsNeverGivenLessRoomThanBefore()
        {
            // no existing layout may shrink: the old rule subtracted the raw inset
            var rect = new SKRect(0, 0, 400, 400);

            foreach (var scale in Scales)
            foreach (var inset in Insets)
            {
                var legacy = SkiaControl.ContractPixelsRect(rect, scale, new Thickness(inset));
                var current = SkiaControl.ContractPixelsRectForContent(rect, scale, new Thickness(inset));

                Assert.True(current.Width >= legacy.Width - 0.001f,
                    $"scale {scale} inset {inset}: {legacy.Width} -> {current.Width}");
                Assert.True(current.Height >= legacy.Height - 0.001f,
                    $"scale {scale} inset {inset}: {legacy.Height} -> {current.Height}");
            }
        }

        [Fact]
        public void WholePixelInsets_Unchanged()
        {
            // guard: where the inset already lands on whole pixels nothing moves at all
            var rect = new SKRect(0, 0, 400, 400);

            foreach (var scale in new[] { 1f, 2f, 3f })
            foreach (var inset in new double[] { 0, 1, 2, 3, 8, 24 })
            {
                var legacy = SkiaControl.ContractPixelsRect(rect, scale, new Thickness(inset));
                var current = SkiaControl.ContractPixelsRectForContent(rect, scale, new Thickness(inset));

                Assert.Equal(legacy, current);
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
    }
}
