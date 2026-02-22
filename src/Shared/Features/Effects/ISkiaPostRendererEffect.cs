namespace DrawnUi.Draw;

/// <summary>
/// Controls how the effect acquires the background texture for its shader.
/// </summary>
public enum PostRendererEffectUseBackgroud
{
    /// <summary>Live snapshot every frame (glass/blur effects). Default.</summary>
    Always,

    /// <summary>Snapshot taken once on first render, then frozen (transition effects). You can set AquiredBackground property to false to re-request background aquisition.</summary>
    Once,

    /// <summary>No background texture — effect renders on transparent (generative: confetti, fireworks).</summary>
    Never
}

public interface IPostRendererEffect : ISkiaEffect
{
    void Render(DrawingContext ctx);

    /// <summary>Controls how/when the background texture is captured. Default is Always.</summary>
    PostRendererEffectUseBackgroud UseBackground { get; set; }

    /// <summary>True once the Once-mode snapshot has been captured. Reset to false to re-capture.</summary>
    bool AquiredBackground { get; set; }
}
