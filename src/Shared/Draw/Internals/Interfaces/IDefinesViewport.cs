namespace DrawnUi.Draw;

public interface IDefinesViewport
{
    public ScaledRect Viewport { get; }

    public void UpdateVisibleIndex();

    public RelativePositionType TrackIndexPosition { get; }

    public void ScrollTo(float x, float y, float maxTimeSecs, bool clamp);

    /// <summary>
    /// So child can call parent to invalidate scrolling offset etc if child size changes
    /// </summary>
    void InvalidateByChild(SkiaControl child);
}
