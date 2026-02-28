namespace DrawnUi.Draw;

public interface IRenderEffect : ISkiaEffect
{
    /// <summary>
    /// Returns true if has drawn control itsself, otherwise it will be drawn over it
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="ctx"></param>
    /// <param name="drawControl"></param>
    /// <returns></returns>
    ChainEffectResult Draw(DrawingContext ctx, Action<DrawingContext> drawControl);
}
