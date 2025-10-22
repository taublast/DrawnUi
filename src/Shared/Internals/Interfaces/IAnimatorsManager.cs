using DrawnUi.Draw;

namespace DrawnUi.Draw;

/// <summary>
/// This control is responsible for updating screen for running animators
/// </summary>
public interface IAnimatorsManager
{
    void AddAnimator(ISkiaAnimator animator);

    void RemoveAnimator(Guid uid);

}