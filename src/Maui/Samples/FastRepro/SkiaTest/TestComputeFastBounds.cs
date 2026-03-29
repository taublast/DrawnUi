using System.Diagnostics;

namespace Sandbox
{
    /// <summary>
    /// Test scenario for SkiaNativeMethods.ComputeFastBounds.
    /// Draws a rectangle with a drop-shadow paint, then visualizes
    /// the original rect bounds vs the expanded fast-bounds returned
    /// by the native sk_paint_compute_fast_bounds call.
    /// </summary>
    public class TestComputeFastBounds : SkiaControl
    {
        private SKRect _objectRect;
        private SKRect _fastBounds;
        private bool _computed;

        public TestComputeFastBounds()
        {
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
        }

        protected override void Paint(DrawingContext ctx)
        {
            var canvas = ctx.Context.Canvas;
            var w = DrawingRect.Width;
            var h = DrawingRect.Height;

            // Clear background
            canvas.Clear(SKColors.White);

            // The object we want to draw: a centered 200x120 rect
            _objectRect = new SKRect(
                (w - 200) / 2f,
                (h - 120) / 2f,
                (w + 200) / 2f,
                (h + 120) / 2f);

            // Paint with a drop shadow (image filter)
            using var shadowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0x40, 0x80, 0xFF),
                Style = SKPaintStyle.Fill,
                ImageFilter = SKImageFilter.CreateDropShadow(
                    dx: 10f,
                    dy: 10f,
                    sigmaX: 15f,
                    sigmaY: 15f,
                    color: new SKColor(0, 0, 0, 160))
            };

            // Compute fast bounds via native call
            bool canCompute = SkiaNativeMethods.CanComputeFastBounds(shadowPaint);
            if (canCompute)
            {
                _fastBounds = SkiaNativeMethods.ComputeFastBounds(shadowPaint, _objectRect);
                _computed = true;
            }

            // Draw the expanded fast-bounds outline (red dashed)
            if (_computed)
            {
                using var boundsPaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = SKColors.Red,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2f,
                    PathEffect = SKPathEffect.CreateDash(new float[] { 8, 4 }, 0)
                };
                canvas.DrawRect(_fastBounds, boundsPaint);
            }

            // Draw the actual object with shadow
            canvas.DrawRect(_objectRect, shadowPaint);

            // Draw the original rect outline (green)
            using var origPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Green,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
            };
            canvas.DrawRect(_objectRect, origPaint);

            // Draw labels
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                TextSize = 14f,
            };

            float textY = 30f;
            float textX = 16f;

            canvas.DrawText("ComputeFastBounds Test", textX, textY, textPaint);
            textY += 22f;

            canvas.DrawText($"Object rect: {FormatRect(_objectRect)}", textX, textY, textPaint);
            textY += 20f;

            if (_computed)
            {
                canvas.DrawText($"Fast bounds: {FormatRect(_fastBounds)}", textX, textY, textPaint);
                textY += 20f;

                var dx = _fastBounds.Width - _objectRect.Width;
                var dy = _fastBounds.Height - _objectRect.Height;
                canvas.DrawText($"Expansion: +{dx:F1}w, +{dy:F1}h", textX, textY, textPaint);
                textY += 20f;

                canvas.DrawText($"CanComputeFastBounds: {canCompute}", textX, textY, textPaint);
            }

            // Legend
            textY = h - 40f;

            textPaint.Color = SKColors.Green;
            canvas.DrawText("— Original rect", textX, textY, textPaint);

            textPaint.Color = SKColors.Red;
            canvas.DrawText("- - Fast bounds (shadow expanded)", textX + 160, textY, textPaint);

            if (!_computed)
            {
                Debug.WriteLine("[TestComputeFastBounds] CanComputeFastBounds returned false");
            }
            else
            {
                Debug.WriteLine($"[TestComputeFastBounds] Object: {FormatRect(_objectRect)}, FastBounds: {FormatRect(_fastBounds)}");
            }

            Repaint();
        }

        private static string FormatRect(SKRect r) =>
            $"[{r.Left:F0},{r.Top:F0} {r.Right:F0},{r.Bottom:F0}]";
    }
}
