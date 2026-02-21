namespace DrawnUi.Draw;

public interface ISkiaEffect : ICanBeUpdatedWithContext
{
    public void Attach(SkiaControl parent);

    public void Dettach();

    public bool NeedApply { get; }

}