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

    public double FPS
    {
        get
        {
            return _reportFps;
        }
    }


    private int _fpsFrames;
    private long _fpsWindowStart;
    private long _fpsLastFrame;

    /// <summary>
    /// Nanoseconds without a counted frame after which the meter starts a fresh window: the
    /// pause is not a slow frame, and averaging it in would report a fraction of the real rate.
    /// </summary>
    private const long FpsIdleResetNanos = 1_000_000_000;

    /// <summary>
    /// Measures frames per second the way the unit is defined: count the frames, divide by the
    /// time they actually took. Deliberately NOT a mean of per-frame 1/dt — that estimator is
    /// convex, so frame-interval jitter inflates it (measured 51fps reported over a real 31fps
    /// second), it makes the reading depend on where in the paint callback the timestamp is
    /// taken, and it can only refresh once every N frames, freezing on a stale value whenever
    /// the frame stream thins out.
    /// </summary>
    /// <param name="currentTimestamp">Wall clock timestamp in nanoseconds.</param>
    /// <param name="windowSeconds">Length of one measurement window. Shorter reacts faster, longer reads steadier.</param>
    void CalculateFPS(long currentTimestamp, double windowSeconds = 0.5)
    {
        if (_fpsWindowStart == 0 || currentTimestamp - _fpsLastFrame > FpsIdleResetNanos)
        {
            //first frame, or the first one after an idle gap
            _fpsWindowStart = currentTimestamp;
            _fpsFrames = 0;
        }

        _fpsLastFrame = currentTimestamp;
        _fpsFrames++;

        double elapsedSeconds = (currentTimestamp - _fpsWindowStart) / 1_000_000_000.0;
        if (elapsedSeconds >= windowSeconds)
        {
            _reportFps = _fpsFrames / elapsedSeconds;
            _fpsFrames = 0;
            _fpsWindowStart = currentTimestamp;
        }
    }

    public long FrameTime { get; protected set; }

    public bool IsDrawing { get; protected set; }

    public bool HasDrawn { get; protected set; }

    private bool on;

    private long _clockLast;
    private long _clockLastVsync;

    /// <summary>
    /// Upper bound on how many frame slots one draw may advance animation time by. Beyond this
    /// the gap is not dropped frames but an idle period, and is resynced instead of stepped.
    /// </summary>
    private const int MaxCatchUpSlots = 4;

    /// <summary>
    /// True when the platform presents CONTINUOUSLY: it re-presents retained content every slot,
    /// so the draw callback is not a reliable "real render" signal and the FPS meter would sit
    /// pinned at the cap if it counted every one. Platforms that paint only after an explicit
    /// invalidation are the opposite — there every paint IS a real render.
    /// Deliberately not derived from the vsync clock: having a vsync timestamp and presenting
    /// continuously are different properties, and Android has the first without the second.
    /// </summary>
    private static bool PresentsContinuously =>
#if IOS || MACCATALYST
        true;
#else
        false;
#endif

    // Frame clock: advances by the frame slots that actually elapsed, resyncs after an idle gap.
    // See SkiaViewAccelerated.NextFrameClock for the full rationale.
    private long NextFrameClock()
    {
        var vsync = Super.VsyncFrameTimeNanos;

        if (vsync == 0)
        {
            // No vsync source: the view is not the pacer, wall clock is the truth.
            _clockLast = Super.GetCurrentTimeNanos();
            return _clockLast;
        }

#if ONPLATFORM
        //RefreshRate lives in the platform partials of Super
        var fps = Super.MaxFps > 0 ? Super.MaxFps : (Super.RefreshRate > 0 ? Super.RefreshRate : 60);
#else
        var fps = Super.MaxFps > 0 ? Super.MaxFps : 60;
#endif
        var step = (long)(1_000_000_000.0 / fps);

        if (_clockLast == 0)
        {
            _clockLast = vsync;
            _clockLastVsync = vsync;
            return _clockLast;
        }

        var slots = 1L;
        if (vsync > _clockLastVsync)
        {
            slots = (long)Math.Round((vsync - _clockLastVsync) / (double)step);

            if (slots < 1)
                slots = 1;

            if (slots > MaxCatchUpSlots)
                slots = MaxCatchUpSlots;
        }

        _clockLastVsync = vsync;
        _clockLast += step * slots;

        if (vsync > _clockLast + step * MaxCatchUpSlots)
            _clockLast = vsync;

        return _clockLast;
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

            // FPS on wall clock, every paint unless the platform presents continuously —
            // see SkiaViewAccelerated.OnPaintingSurface for rationale.
            if (isDirty || !PresentsContinuously)
                CalculateFPS(Super.GetCurrentTimeNanos());


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
