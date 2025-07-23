using HarfBuzzSharp;

namespace DrawnUi.Views;



public partial class SkiaViewAccelerated : SKGLView, ISkiaDrawable
{
    public Guid Uid { get; }  = Guid.NewGuid();

    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

    public SkiaViewAccelerated(DrawnView superview)
    {
        Superview = superview;
        EnableTouchEvents = false;
        //this.HasRenderLoop = true;
    }

#if ANDROID

    private MyOrientationListener _orientationListener;

    public class MyOrientationListener : Android.Views.OrientationEventListener
    {
        private SkiaViewAccelerated _owner;

        public MyOrientationListener(IntPtr javaReference, Android.Runtime.JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public MyOrientationListener(Android.Content.Context context) : base(context)
        {
        }

        public MyOrientationListener(SkiaViewAccelerated parent, Android.Hardware.SensorDelay rate) : base(Platform.AppContext, rate)
        {
            _owner = parent;
        }

        public override void OnOrientationChanged(int rotation)
        {
            _owner.Superview?.SetDeviceOrientation(rotation);
        }
    }


#endif

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.NewHandler == null)
        {
            PaintSurface -= OnPaintingSurface;

#if ANDROID

            if (_orientationListener != null)
            {
                _orientationListener.Disable();
                _orientationListener.Dispose();
                _orientationListener = null;
            }

#endif
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

#if ANDROID

            var renderer = Handler;// as SkiaSharp.Views.Maui.Controls.Compatibility.SKGLViewRenderer;
                                   //var nativeView = renderer.Control as SkiaSharp.Views.Android.SKGLTextureView;
                                   //var renderer = Handler as SkiaSharp.Views.Maui.Handlers.SKGLViewHandler;
                                   //var nativeView = renderer.PlatformView as SkiaSharp.Views.Android.SKGLTextureView;

            _orientationListener = new MyOrientationListener(this, Android.Hardware.SensorDelay.Normal);
            if (_orientationListener.CanDetectOrientation())
                _orientationListener.Enable();

#elif IOS

            if (DeviceInfo.Current.DeviceType != DeviceType.Virtual)
            {
                //var renderer = Handler as SKMetalViewRenderer;
                //var nativeView = renderer.Control as SkiaSharp.Views.iOS.SKMetalView;
            }
            else
            {
                TestApple();
                //var renderer = Handler as SkiaSharp.Views.Maui.Controls.Compatibility.SKGLViewRenderer;
                //var nativeView = renderer.Control as SkiaSharp.Views.iOS.SKGLView;
            }

            //#elif MACCATALYST

            //            var renderer = Handler as SKMetalViewRenderer;
            //            var nativeView = renderer.Control as SkiaSharp.Views.iOS.SKMetalView;

#endif

            Superview?.ConnectedHandler();
        }
    }

    public DrawnView Superview { get; protected set; }
    private bool _newFrameReady;

    public void Dispose()
    {
        PaintSurface -= OnPaintingSurface;
        _surface = null;
        Superview = null;

        GC.SuppressFinalize(this);
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

        FrameTime = Super.GetCurrentTimeNanos();

        CalculateFPS(FrameTime);

        if (OnDraw != null && Super.EnableRendering)
        {
            var rect = new SKRect(0, 0, paintArgs.BackendRenderTarget.Width, paintArgs.BackendRenderTarget.Height);
            _surface = paintArgs.Surface;
            var isDirty = OnDraw.Invoke(paintArgs.Surface, rect);

#if WINDOWS
            //fix handler renderer didn't render first frame at startup for skiasharp v3
            if (Handler?.PlatformView is SkiaSharp.Views.Windows.SKXamlCanvas canvas)
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
}




