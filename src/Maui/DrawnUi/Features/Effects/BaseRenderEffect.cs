using static System.Net.WebRequestMethods;

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
        var kill = Paint;
        if (Parent != null)
        {
            Parent.DisposeObject(kill);
        }
        else
        {
            kill?.Dispose();
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

