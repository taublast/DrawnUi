using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SkiaSharp.Views.Blazor;

namespace DrawnUi.Draw
{
    public partial class SkiaViewAccelerated : SKGLViewWrap, ISkiaDrawable
    {
        public Guid Uid { get; } = Guid.NewGuid();
        private DrawnView? _attachedSuperview;
        private bool _isConnected;

        public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

        [Parameter]
        public Func<SKSurface, SKRect, bool> DrawHandler
        {
            get => OnDraw;
            set => OnDraw = value;
        }

        [Parameter]
        public DrawnView? Superview { get; set; }

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
            this.OnPaintSurface = null;

            _surface = null;
            Superview = null;
            _attachedSuperview = null;

            base.Dispose();
        }

        protected virtual void OnHandlerChanging(object args)
        {
            if (args == null)
            {
                this.OnPaintSurface = null;
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

        private void OnOrientationChanged(object sender, DeviceOrientation deviceOrientation)
        {
            _attachedSuperview?.SetDeviceOrientation(deviceOrientation);
        }

        public GRContext GRContext => Context;
        private bool _newFrameReady;

        SKSurface _surface;
        private DateTime _lastFrame;
        private double _fps;
        private double _reportFps;

        public SKSurface Surface
        {
            get { return _surface; }
        }

        public bool IsHardwareAccelerated => true;

        public double FPS
        {
            get { return _reportFps; }
        }

        public bool IsDrawing
        {
            get => _isDrawing;
            set
            {
                if (value == _isDrawing) return;
                _isDrawing = value;
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
                Super.EnableRendering
                && this.RendererInfo != null)
            {
                Invalidate();

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
        private void OnPaintingSurface(SKPaintGLSurfaceEventArgs paintArgs)
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
            }

            if (paintArgs.BackendRenderTarget.Width == 0 || paintArgs.BackendRenderTarget.Height == 0)
            {
                maybeDrawn = false;
            }

            HasDrawn = maybeDrawn;
            IsDrawing = false;
        }

        static bool maybeLowEnd = true;
    }

    public partial class SKGLViewWrap : SKGLView
    {
        private FieldInfo? _context;

        public GRContext? Context
        {
            get
            {
                if (_context == null)
                {
                    _context = typeof(SKGLView).GetField("context",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }

                if (_context != null)
                {
                    return _context.GetValue(this) as GRContext;
                }

                return null;
            }
        }

        private FieldInfo? _canvasSize;

        public SKSize CanvasSize
        {
            get
            {
                if (_canvasSize == null)
                {
                    _canvasSize = typeof(SKGLView).GetField("canvasSize",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
