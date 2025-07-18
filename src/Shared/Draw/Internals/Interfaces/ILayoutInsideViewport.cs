namespace DrawnUi.Draw;

public interface ILayoutInsideViewport : IInsideViewport
{


    /// <summary>
    /// The point here is the rendering location, always on screen
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public ContainsPointResult GetVisibleChildIndexAt(SKPoint point);

    /// <summary>
    /// The point here is the position inside parent, can be offscreen
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public ContainsPointResult GetChildIndexAt(SKPoint point);

    /// <summary>
    /// Default implementation for layouts that don't need custom LoadMore logic
    /// </summary>
    bool IInsideViewport.ShouldTriggerLoadMore(ScaledRect viewport)
    {
        // Default implementation - always return false (no LoadMore)
        // Individual layouts can override this method for custom behavior
        return false;
    }
}