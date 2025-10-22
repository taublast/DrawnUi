namespace DrawnUi.Draw;

public interface IRefreshIndicator : IDrawnBase
{
    /// <summary>
    /// 0 - 1 ratio between overscroll and RefreshShowDistance.1 means control must be totally shown.
    /// 
    /// </summary>
    /// <param name="ratio"></param>
    void SetDragRatio(float ratio, float ptsScrollOffset, double ptsLimit, double ptsTrigger);
 

}
