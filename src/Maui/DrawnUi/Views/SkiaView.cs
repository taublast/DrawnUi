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

    public DrawnView Superview { get; set; }

    public void Dispose()
    {
        _surface = null;
        PaintSurface -= OnPaintingSurface;
        Superview = null;
    }

    public SkiaView()
    {
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

    /// <summary>
    /// Frames PRESENTED per second — see <see cref="SkiaViewAccelerated.FPS"/>.
    /// </summary>
    public double FPS
    {
        get
        {
            return _meter.Value;
        }
    }

    private readonly FpsMeter _meter = new();

    public long FrameTime { get; protected set; }

    public bool IsDrawing { get; protected set; }

    public bool HasDrawn { get; protected set; }

    private bool on;

    private long _clockLast;

    // Frame clock: one frame interval per draw, resync to vsync when behind.
    // See SkiaViewAccelerated.NextFrameClock for the full rationale.
    private long NextFrameClock()
    {
        var vsync = Super.VsyncFrameTimeNanos;
        var fps = Super.MaxFps > 0 ? Super.MaxFps : 60;
        var step = (long)(1_000_000_000.0 / fps);
        long now;

        if (_clockLast == 0)
        {
            now = vsync > 0 ? vsync : Super.GetCurrentTimeNanos();
        }
        else
        {
            now = _clockLast + step;

            if (vsync > now + step)
                now = vsync;
        }

        _clockLast = now;
        return now;
    }

    private void OnPaintingSurface(object sender, SKPaintSurfaceEventArgs paintArgs)
    {
        IsDrawing = true;
        bool maybeDrawn = true;

        // Strictly monotonic vsync-aligned clock — see SkiaViewAccelerated.NextFrameClock.
        FrameTime = NextFrameClock();

        if (OnDraw != null && Super.EnableRendering)
        {
            var rect = new SKRect(0, 0, paintArgs.Info.Width, paintArgs.Info.Height);
            _surface = paintArgs.Surface;
            bool isDirty = OnDraw.Invoke(paintArgs.Surface, rect);

            // Every presented frame, wall clock — see SkiaViewAccelerated.OnPaintingSurface.
            _meter.Tick(Super.GetCurrentTimeNanos());


#if WINDOWS
            if (Handler?.PlatformView is SoftwareWindowsCanvas canvas)
            {
                if (canvas.CanvasSize ==  SKSize.Empty)
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
