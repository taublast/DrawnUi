using System.Reflection;
using Microsoft.AspNetCore.Components;
using SkiaSharp.Views.Blazor;

namespace DrawnUi.Draw
{
    public partial class SkiaView : SKCanvasViewWrap, ISkiaDrawable
    {
        public Guid Uid { get; } = Guid.NewGuid();

        private DrawnView? _attachedSuperview;
        private bool _isConnected;
        private SKSurface _surface;
        private double _reportFps;
        private double _fpsAverage;
        private int _fpsCount;
        private long _lastFrameTimestamp;

        public bool IsHardwareAccelerated => false;

        public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

        [Parameter]
        public Func<SKSurface, SKRect, bool> DrawHandler
        {
            get => OnDraw;
            set => OnDraw = value;
        }

        [Parameter]
        public DrawnView? Superview { get; set; }

        public SKSurface Surface => _surface;

        public double FPS => _reportFps;

        public long FrameTime { get; protected set; }

        public bool IsDrawing { get; protected set; }

        public bool HasDrawn { get; protected set; }

        public void SignalFrame(long nanoseconds)
        {
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            if (ReferenceEquals(_attachedSuperview, Superview))
                return;

            if (_attachedSuperview != null && _isConnected)
            {
                OnHandlerChanging(null);
                OnHandlerChanged(null);
                _isConnected = false;
            }

            _attachedSuperview = Superview;

            if (_attachedSuperview != null)
            {
                _attachedSuperview.AttachCanvasView(this);
                OnHandlerChanging(this);
                OnHandlerChanged(this);
                _isConnected = true;
            }
        }

        public new void Dispose()
        {
            if (_isConnected)
            {
                OnHandlerChanging(null);
                OnHandlerChanged(null);
                _isConnected = false;
            }

            _surface = null;
            OnPaintSurface = null;
            Superview = null;
            _attachedSuperview = null;

            base.Dispose();
        }

        public void Disconnect()
        {
            OnPaintSurface = null;
        }

        private void OnOrientationChanged(object sender, DeviceOrientation deviceOrientation)
        {
            _attachedSuperview?.SetDeviceOrientation(deviceOrientation);
        }

        protected virtual void OnHandlerChanging(object args)
        {
            if (args == null)
            {
                OnPaintSurface = null;
                _attachedSuperview?.DisconnectedHandler();
            }
        }

        protected virtual void OnHandlerChanged(object args)
        {
            if (args != null)
            {
                OnPaintSurface = OnPaintingSurface;
                Super.OrientationChanged += OnOrientationChanged;
                _attachedSuperview?.ConnectedHandler();
            }
            else
            {
                Super.OrientationChanged -= OnOrientationChanged;
            }
        }

        void CalculateFPS(long currentTimestamp, int averageAmount = 10)
        {
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

        private void OnPaintingSurface(SKPaintSurfaceEventArgs paintArgs)
        {
            IsDrawing = true;
            bool maybeDrawn = true;

            FrameTime = Super.GetCurrentTimeNanos();
            CalculateFPS(FrameTime);

            if (OnDraw != null && Super.EnableRendering)
            {
                var rect = new SKRect(0, 0, paintArgs.Info.Width, paintArgs.Info.Height);
                _surface = paintArgs.Surface;
                OnDraw.Invoke(paintArgs.Surface, rect);

                if (paintArgs.Info.Width == 0 || paintArgs.Info.Height == 0)
                {
                    maybeDrawn = false;
                }
            }

            HasDrawn = maybeDrawn;
            IsDrawing = false;
        }

        static bool maybeLowEnd = true;

        public bool Update(long nanos)
        {
            if (Super.EnableRendering)
            {
                Invalidate();
                return true;
            }

            return false;
        }
    }

    public partial class SKCanvasViewWrap : SKCanvasView
    {
        private FieldInfo? _canvasSize;

        public SKSize CanvasSize
        {
            get
            {
                if (_canvasSize == null)
                {
                    _canvasSize = typeof(SKCanvasView).GetField("canvasSize",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_canvasSize != null)
                {
                    return (SKSize)(_canvasSize.GetValue(this) ?? SKSize.Empty);
                }

                return SKSize.Empty;
            }
        }
    }
}
