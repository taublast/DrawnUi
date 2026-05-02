namespace DrawnUi.Draw;

/// <summary>
/// Paints the parent view with the background color with a clipped viewport oth this view size
/// </summary>
public class SkiaHoverMask : SkiaShape
{
    protected override void Paint(DrawingContext ctx)
    {

        //paint clipped background
        if (BackgroundColor != Colors.Transparent && Parent != null)
        {
            using (var paint = new SKPaint
            {
                Color = BackgroundColor.ToSKColor(),
                Style = SKPaintStyle.StrokeAndFill,
            })
            {
                SetupGradient(paint, FillGradient, ctx.Destination);

                using (SKPath clipInsideParent = new SKPath())
                {
                    var saved = ctx.Context.Canvas.Save();

                    ShapePaintArguments? arguments = null;
                    if (ctx.GetArgument(nameof(ContextArguments.ShapePaint)) is ShapePaintArguments defined)
                    {
                        arguments = defined;
                    }

                    using var clipContent = CreateClip(arguments, true);
                    clipInsideParent.AddPath(clipContent);

                    ClipSmart(ctx.Context.Canvas, clipInsideParent, SKClipOperation.Difference);

                    //paint this taking viewport dimensions
                    ctx.Context.Canvas.DrawRect(Parent.DrawingRect, paint);

                    DrawViews(ctx);

                    //todo should maybe add stroke property?

                    ctx.Context.Canvas.RestoreToCount(saved);
                }
            }

        }

    }
}
