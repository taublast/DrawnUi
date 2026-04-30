using SkiaSharp;

namespace SpaceShooter.Game;

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