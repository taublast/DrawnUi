namespace DrawnUi.Draw;

public class BaseColorFilterEffect : SkiaEffect, IColorEffect
{
    public SKColorFilter Filter { get; set; }

    public virtual SKColorFilter CreateFilter(SKRect destination)
    {
        return null;
    }

    public override void Update()
    {
        if (Parent != null && Filter != null)
        {
            Parent.DisposeObject(Filter);
        }
        Filter = null;

        base.Update();
    }

    protected override void OnDisposing()
    {
        Filter?.Dispose();

        base.OnDisposing();
    }
}
