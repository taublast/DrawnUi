namespace DrawnUi.Draw;

public interface IInsideViewport : IVisibilityAware, IDisposable
{
    /// <summary>
    /// Will be called when viewport containing this view has changed
    /// </summary>
    /// <param name="viewport"></param>
    public void OnViewportWasChanged(ScaledRect viewport);

    /// <summary>
    /// IInsideViewport interface: loaded is called when the view is created, but not yet visible
    /// </summary>
    void OnLoaded();
}
