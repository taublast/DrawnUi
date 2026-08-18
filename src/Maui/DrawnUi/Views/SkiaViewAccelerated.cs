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

    private double _fpsAverage;
    private int _fpsCount;
    private long _lastFrameTimestamp;
    private bool _isDrawing;

    private long _clockLast;

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

            // Fell behind the real clock (idle gap, dropped frames) — jump forward.
            if (vsync > now + step)
                now = vsync;
        }

        _clockLast = now;
        return now;
    }



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

            // FPS meter counts frames where CONTENT actually rendered, measured on
            // wall clock. In continuous (view-paced) mode the MTKView re-presents
            // retained content every slot — counting those would pin the meter at
            // the cap forever; counting synthetic animation-clock deltas shows
            // nonsense (thousands). Real work + real time only.
            if (isDirty)
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




