namespace DrawnUi.Draw;

public class BaseImageFilterEffect : SkiaEffect, IImageEffect
{
    public SKImageFilter Filter { get; set; }

    public virtual SKImageFilter CreateFilter(SKRect destination)
    {
        return null;
    }

    public override void Update()
    {
        if (Parent != null && Filter!=null)
        {
            Parent.DisposeObject(Filter);
        }
        Filter = null;

        base.Update();
    }

    protected override void OnDisposing()
    {
        Filter?.Dispose();
        Filter = null;

        base.OnDisposing();
    }

}
