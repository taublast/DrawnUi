namespace GameTemplate.Game;

/// <summary>
/// Reusable item to avoid GC spikes
/// </summary>
public interface IReusableSprite
{
    bool IsActive { get; set; }
    Guid Uid { get; }
    void ResetAnimationState();
    Task AnimateDisappearing();
}

