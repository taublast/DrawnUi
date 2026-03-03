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
        var kill = Filter;
        if (Parent != null)
        {
            Parent.DisposeObject(kill);
        }
        else
        {
            kill?.Dispose();
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
