namespace DrawnUi.Draw;

public interface IStateEffect : ISkiaEffect
{
    /// <summary>
    /// Will be invoked before actually painting but after gestures processing and other internal calculations. By SkiaControl.OnBeforeDrawing method. Beware if you call Update() inside will never stop updating.
    /// </summary>
    void UpdateState();
}