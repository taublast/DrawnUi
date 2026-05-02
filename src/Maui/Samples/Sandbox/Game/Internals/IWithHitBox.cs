using SkiaSharp;

namespace SpaceShooter;

public interface IWithHitBox
{
    /// <summary>
    /// Calculate hitbox etc for the curent frame
    /// </summary>
    /// <param name="time"></param>
    void UpdateState(long time);

    /// <summary>
    /// Precalculated
    /// </summary>
    SKRect HitBox { get; }
}