namespace DrawnUi.Draw;

public interface IPostRendererEffect : ISkiaEffect
{
    void Render(DrawingContext ctx);

    /// <summary>Controls how/when the background texture is captured. Default is Always.</summary>
    PostRendererEffectUseBackgroud UseBackground { get; set; }

    /// <summary>True once the Once-mode snapshot has been captured. Reset to false to re-capture.</summary>
    bool AquiredBackground { get; set; }
}
