namespace DrawnUi.Views;



public partial class SkiaViewAccelerated : SKGLView, ISkiaDrawable
{
    public Guid Uid { get; }  = Guid.NewGuid();

    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

    public SkiaViewAccelerated()
    {
        EnableTouchEvents = false;
        //this.HasRenderLoop = true;
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

    private void OnOrientationChanged(object sender, DeviceOrientation deviceOrientation)
    {
        Superview?.SetDeviceOrientation(deviceOrientation);
    }

    public DrawnView Superview { get; set; }

    private bool _newFrameReady;

    public void Dispose()
    {
        PaintSurface -= OnPaintingSurface;
        _surface = null;
        Superview = null;

        //GC.SuppressFinalize(this);
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

    public bool IsHardwareAccelerated => true;

    public double FPS
    {
        get
        {
            return _reportFps;
        }
    }

    public bool IsDrawing
    {
        get => _isDrawing;
        set
        {
            if (value == _isDrawing) return;
            _isDrawing = value;
            OnPropertyChanged();
        }
    }

    public bool HasDrawn { get; protected set; }
    public long FrameTime { get; protected set; }

    public void SignalFrame(long nanoseconds)
    {

    }

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

    private int _fpsFrames;
    private long _fpsWindowStart;
    private long _fpsLastFrame;
    private bool _isDrawing;

    /// <summary>
    /// Nanoseconds without a counted frame after which the meter starts a fresh window: the
    /// pause is not a slow frame, and averaging it in would report a fraction of the real rate.
    /// </summary>
    private const long FpsIdleResetNanos = 1_000_000_000;

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

    /// <summary>
    /// Animation clock for draws: advances by EXACTLY one frame interval per draw
    /// (the view is the frame pacer), loosely anchored to the shared vsync clock.
    /// Mixing the two clocks per-draw is wrong in both directions: reusing a stale
    /// vsync value gives delta 0 (freeze + double-step jank, FPS=∞), and switching
    /// between synthesized and real values gives ~1ms deltas (FPS meter reads
    /// thousands). Resync to vsync only when genuinely behind it — e.g. after an
    /// idle period with no draws.
    /// </summary>
    private long NextFrameClock()
    {
        var vsync = Super.VsyncFrameTimeNanos;

        if (vsync == 0)
        {
            // No vsync source: the view is not the pacer, wall clock is the truth.
            _clockLast = Super.GetCurrentTimeNanos();
            return _clockLast;
        }

        // MaxFps is snapped to a cadence the display can present (Super.SnapMaxFpsToDisplay),
        // so this step is the real frame interval, not an approximation of one.
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

        // Advance by the number of frame slots that ACTUALLY passed, not always one. Always
        // stepping one turns a missed slot into a speed error: content travels half the
        // distance the elapsed time called for, then the resync below hops to catch up —
        // a stall followed by a snap. Counting the slot makes it an honest repeated frame.
        // Matters most under a low cap, where one missed slot is 33ms rather than 8 or 16.
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

        // Too far behind to walk back one frame at a time (idle period, app resumed).
        if (vsync > _clockLast + step * MaxCatchUpSlots)
            _clockLast = vsync;

        return _clockLast;
    }



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


    /// <summary>
    /// We are drawing the frame
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="paintArgs"></param>
    private void OnPaintingSurface(object sender, SKPaintGLSurfaceEventArgs paintArgs)
    {
        IsDrawing = true;
        bool maybeDrawn = true;

        // Vsync-aligned clock, made STRICTLY MONOTONIC per draw. The raw value advances
        // on the shared tick, but with view-driven pacing a draw can land between two
        // ticks and read a repeated value: delta 0 → animators no-op then double-step
        // (visible jank) and FPS meters divide by zero. When the tick hasn't advanced
        // yet, synthesize the next frame slot instead of reusing the stale timestamp.
        FrameTime = NextFrameClock();

        if (OnDraw != null && Super.EnableRendering)
        {
            var rect = new SKRect(0, 0, paintArgs.BackendRenderTarget.Width, paintArgs.BackendRenderTarget.Height);
            _surface = paintArgs.Surface;
            var isDirty = OnDraw.Invoke(paintArgs.Surface, rect);

            // FPS meter, measured on wall clock. On a continuously presenting platform
            // (MTKView re-presents retained content every slot) only frames that really
            // rendered content count, otherwise the meter would sit pinned at the cap.
            // Where painting happens only on invalidation every paint IS a real render,
            // and gating on isDirty there reports the content rate instead of the
            // rendering rate — a 60fps loop redrawing 30 times reads as 30.
            if (isDirty || !PresentsContinuously)
                CalculateFPS(Super.GetCurrentTimeNanos());

#if WINDOWS
            //fix handler renderer didn't render first frame at startup for skiasharp v3
            if (Handler?.PlatformView is DrawnUi.Controls.SoftwareWindowsCanvas canvas)
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
}




