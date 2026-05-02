namespace DrawnUi.Draw;

public partial class SkiaViewAccelerated : ISkiaDrawable
{
    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }
    public SKSurface Surface => null;
    public bool IsHardwareAccelerated => false;
    public double FPS => 0;
    public bool IsDrawing => false;
    public bool HasDrawn => false;
    public long FrameTime => 0;
    public Guid Uid { get; } = Guid.NewGuid();
    public SKSize CanvasSize => SKSize.Empty;
    public GRContext GRContext => null;
    public bool Update(long nanos = 0) => false;
    public void SignalFrame(long nanoseconds) { }
    public void Dispose() { }
}
