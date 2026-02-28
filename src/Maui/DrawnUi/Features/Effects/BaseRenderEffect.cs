namespace DrawnUi.Draw;

public class BaseChainedEffect : SkiaEffect, IRenderEffect
{
    public SKPaint Paint { get; set; }

    public virtual ChainEffectResult Draw(DrawingContext ctx, Action<DrawingContext> drawControl)
    {
        return ChainEffectResult.Default;
    }

    public override void Update()
    {
        if (Parent != null && Paint != null)
        {
            Parent.DisposeObject(Paint);
        }
        Paint = null;

        base.Update();
    }

    protected override void OnDisposing()
    {
        Paint?.Dispose();

        base.OnDisposing();
    }
}

