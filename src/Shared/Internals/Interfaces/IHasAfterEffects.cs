namespace DrawnUi.Draw;

public interface IHasAfterEffects
{
    void PlayRippleAnimation(Color color, double x, double y, bool removePrevious = true);

    void PlayShimmerAnimation(Color color, float shimmerWidth, float shimmerAngle, int speedMs, bool removePrevious = true);

    SKPoint GetOffsetInsideControlInPoints(PointF argsLocation, SKPoint childOffset);
}