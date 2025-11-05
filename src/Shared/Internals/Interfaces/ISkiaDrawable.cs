namespace DrawnUi.Draw;

public interface ISkiaDrawable : ISkiaSharpView, IDisposable
{
    /// <summary>
    /// Return true if need force invalidation on next frame
    /// </summary>
    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

    public SKSurface Surface { get; }

    public bool IsHardwareAccelerated { get; }

    public double FPS { get; }

    public bool IsDrawing { get; }

    public bool HasDrawn { get; }

    long FrameTime { get; }

    Guid Uid { get; }
}
