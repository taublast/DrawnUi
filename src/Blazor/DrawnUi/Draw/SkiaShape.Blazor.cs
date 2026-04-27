using Color = Microsoft.Maui.Graphics.Color;

namespace DrawnUi.Draw
{
    public partial class SkiaShape : SkiaLayout
    {
        public override void OnDisposing()
        {
            DisposeSharedShapeResources();
            base.OnDisposing();
        }

        public override SKPath CreateClip(object arguments, bool usePosition, SKPath path = null)
        {
            path ??= new SKPath();

            TryCreateBasicClip(arguments, usePosition, path);

            return path;
        }

        protected override void Paint(DrawingContext ctx)
        {
            if (ctx.Destination.Width <= 0 || ctx.Destination.Height <= 0 || IsDisposing || IsDisposed)
            {
                return;
            }

            var scale = ctx.Scale;
            CalculateSizeForStroke(ctx.Destination, scale);

            var outRect = MeasuredStrokeAwareSize;
            var backgroundRect = MeasuredStrokeAwareClipSize;
            var minSize = Math.Min(outRect.Height, outRect.Width);

            AlignResizedPath(outRect);

            var scaledRadii = CreateScaledRadii(scale);

            var backgroundColor = BackgroundColor != null ? BackgroundColor.ToSKColor() : SKColors.Transparent;
            if (backgroundColor.Alpha > 0)
            {
                using var fillPaint = new SKPaint
                {
                    Color = backgroundColor,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = Type != ShapeType.Rectangle || CornerRadius != default
                };
                TryPaintBasicShape(ctx.Context.Canvas, backgroundRect, scaledRadii, minSize, fillPaint);
            }

            if (WillStroke)
            {
                using var strokePaint = new SKPaint
                {
                    Color = StrokeColor.ToSKColor(),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = StrokeWidth > 0 ? (float)(StrokeWidth * scale) : (float)(-StrokeWidth),
                    IsAntialias = true
                };
                TryPaintBasicShape(ctx.Context.Canvas, outRect, scaledRadii, minSize, strokePaint);
            }

            WasDrawn = true;
        }
    }
}