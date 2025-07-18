namespace DrawnUi.Draw;

public interface ISkiaSharpView
{
    /// <summary>
    /// Safe InvalidateSurface() call. If nanos not specified will generate ittself
    /// </summary>
    public void Update(long nanos = 0);

    public void SignalFrame(long nanoseconds);

    public SKSize CanvasSize { get; }

}
