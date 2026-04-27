namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    protected virtual bool DrawAndTrackChild(DrawingContext context, SkiaControl child,
        List<SkiaControlWithRect> renderTree, int index, SKRect destinationRect)
    {
        if (child == null || child.IsDisposed || child.IsDisposing)
            return false;

        child.OptionalOnBeforeDrawing();
        if (!child.CanDraw)
            return false;

        child.Render(context.WithDestination(destinationRect));
        renderTree.Add(new SkiaControlWithRect(child,
            destinationRect,
            child.CreateHitRect(),
            index,
            child.ContextIndex,
            child.BindingContext));

        return true;
    }
}