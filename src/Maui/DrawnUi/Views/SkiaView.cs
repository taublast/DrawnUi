using DrawnUi.Controls;

namespace DrawnUi.Views;

public partial class SkiaView : SKCanvasView, ISkiaDrawable
{
    public Guid Uid { get; } = Guid.NewGuid();

    public bool IsHardwareAccelerated => false;

    public void SignalFrame(long nanoseconds)
    {

    }

    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

    public DrawnView Superview { get; protected set; }

    public void Dispose()
    {
        _surface = null;
        PaintSurface -= OnPaintingSurface;
        Superview = null;
    }

    public SkiaView(DrawnView superview)
    {
        Superview = superview;
        EnableTouchEvents = false;
    }

    public void Disconnect()
    {
        PaintSurface -= OnPaintingSurface;
    }

    private void OnOrientationChanged(object sender, DeviceOrientation deviceOrientation)
    {
        Superview?.SetDeviceOrientation(deviceOrientation);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.NewHandler == null)
        {
            PaintSurface -= OnPaintingSurface;
            Superview?.DisconnectedHandler();
        }

        base.OnHandlerChanging(args);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null)
        {
            PaintSurface -= OnPaintingSurface;
            PaintSurface += OnPaintingSurface;

            Super.OrientationChanged += OnOrientationChanged;

            Superview?.ConnectedHandler();
        }
        else
        {
            Super.OrientationChanged -= OnOrientationChanged;
        }
    }

    SKSurface _surface;
    private DateTime _lastFrame;
    private double _fps;
    private double _reportFps;


    public SKSurface Surface
    {
        get
        {
            return _surface;
        }
    }

    public double FPS
    {
        get
        {
            return _reportFps;
        }
    }


    private double _fpsAverage;
    private int _fpsCount;
    private long _lastFrameTimestamp;

    /// <summary>
    /// Calculates the frames per second (FPS) and updates the rolling average FPS every 'averageAmount' frames.
    /// </summary>
    /// <param name="currentTimestamp">The current timestamp in nanoseconds.</param>
    /// <param name="averageAmount">The number of frames over which to average the FPS. Default is 10.</param>
    void CalculateFPS(long currentTimestamp, int averageAmount = 10)
    {
        // Convert nanoseconds to seconds for elapsed time calculation.
        double elapsedSeconds = (currentTimestamp - _lastFrameTimestamp) / 1_000_000_000.0;
        _lastFrameTimestamp = currentTimestamp;

        double currentFps = 1.0 / elapsedSeconds;

        _fpsAverage = ((_fpsAverage * _fpsCount) + currentFps) / (_fpsCount + 1);
        _fpsCount++;

        if (_fpsCount >= averageAmount)
        {
            _reportFps = _fpsAverage;
            _fpsCount = 0;
            _fpsAverage = 0.0;
        }
    }

    public long FrameTime { get; protected set; }

    public bool IsDrawing { get; protected set; }

    public bool HasDrawn { get; protected set; }

    private bool on;

    private void OnPaintingSurface(object sender, SKPaintSurfaceEventArgs paintArgs)
    {
        IsDrawing = true;
        bool maybeDrawn = true;

        FrameTime = Super.GetCurrentTimeNanos();

        CalculateFPS(FrameTime);

        if (OnDraw != null && Super.EnableRendering)
        {
            var rect = new SKRect(0, 0, paintArgs.Info.Width, paintArgs.Info.Height);
            _surface = paintArgs.Surface;
            bool isDirty = OnDraw.Invoke(paintArgs.Surface, rect);


#if WINDOWS
            //fix handler renderer didn't render first frame at startup for skiasharp v3
            if (Handler?.PlatformView is SoftwareWindowsCanvas canvas)
            {
                if (double.IsNaN(canvas.Height) || double.IsNaN(canvas.Width))
                {
                    maybeDrawn = false;
                }
            }
#endif

        }

        HasDrawn = maybeDrawn;
        IsDrawing = false;
    }

    static bool maybeLowEnd = true;

    public bool Update(long nanos)
    {
        if (
            Super.EnableRendering &&
            this.Handler != null && this.Handler.PlatformView != null)
        {
            InvalidateSurface();

            return true;
        }

        return false;
    }


}
