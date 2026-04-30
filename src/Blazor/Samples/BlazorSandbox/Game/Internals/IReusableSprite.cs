namespace SpaceShooter.Game;

/// <summary>
/// Resusable model, to avoid GC
/// </summary>
public interface IReusableSprite
{
    bool IsActive { get; set; }

    Guid Uid { get; }

    void ResetAnimationState();

    Task AnimateDisappearing();
}