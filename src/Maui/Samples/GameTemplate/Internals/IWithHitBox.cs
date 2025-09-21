using SkiaSharp;

namespace GameTemplate.Game;

public interface IWithHitBox
{
    void UpdateState(long time);
    SKRect HitBox { get; }
}

