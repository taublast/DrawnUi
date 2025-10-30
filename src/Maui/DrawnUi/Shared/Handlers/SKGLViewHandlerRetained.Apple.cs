using Microsoft.Maui.Handlers;
using UIKit;
using Metal;

namespace DrawnUi.Views
{
    /// <summary>
    /// APPLE Handler with Mapper (originally iOS)
    /// </summary>
    public partial class SKGLViewHandlerRetained : ViewHandler<ISKGLView, SKMetalViewRetained>
    {
        private PaintSurfaceProxy? paintSurfaceProxy;

        // P3-iOS Optimization: Shared Metal device across all views
        private static IMTLDevice? _sharedMetalDevice;
        private static readonly object _deviceLock = new object();

        protected override SKMetalViewRetained CreatePlatformView()
        {
            // P3-iOS Optimization: Create/reuse shared Metal device
            lock (_deviceLock)
            {
                if (_sharedMetalDevice == null)
                {
                    _sharedMetalDevice = MTLDevice.SystemDefault;
                    if (_sharedMetalDevice == null)
                    {
                        throw new InvalidOperationException("Failed to create Metal device");
                    }
                }
            }

            return new MauiSkMetalViewRetained
            {
                BackgroundColor = UIColor.Clear,
                Opaque = false,
                Device = _sharedMetalDevice // Reuse shared device
            };
        }


        protected override void ConnectHandler(SKMetalViewRetained platformView)
        {
            paintSurfaceProxy = new();
            paintSurfaceProxy.Connect(VirtualView, platformView);

            base.ConnectHandler(platformView);
        }

        protected override void DisconnectHandler(SKMetalViewRetained platformView)
        {
            paintSurfaceProxy?.Disconnect(platformView);
            paintSurfaceProxy = null;

            base.DisconnectHandler(platformView);
        }

        // Mapper actions / properties

        public static void OnInvalidateSurface(SKGLViewHandlerRetained handler, ISKGLView view, object? args)
        {
            if (handler?.PlatformView == null)
                return;

            if (handler.PlatformView.ManualRefresh)
                handler.PlatformView.SetNeedsDisplay();
        }

        public static void MapIgnorePixelScaling(SKGLViewHandlerRetained handler, ISKGLView view)
        {
            if (handler?.PlatformView is MauiSkMetalViewRetained pv)
            {
                pv.IgnorePixelScaling = view.IgnorePixelScaling;
                handler.PlatformView.SetNeedsDisplay();
            }
        }

        public static void MapHasRenderLoop(SKGLViewHandlerRetained handler, ISKGLView view)
        {
            if (handler?.PlatformView == null)
                return;

            // P1-Cross Optimization: Start paused until first frame for better initialization
            if (handler.PlatformView is MauiSkMetalViewRetained metalView && !metalView.HasRenderedFirstFrame)
            {
                handler.PlatformView.Paused = true;
                handler.PlatformView.EnableSetNeedsDisplay = true;
            }
            else
            {
                handler.PlatformView.Paused = !view.HasRenderLoop;
                handler.PlatformView.EnableSetNeedsDisplay = !view.HasRenderLoop;
            }
        }

        public static void MapEnableTouchEvents(SKGLViewHandlerRetained handler, ISKGLView view)
        {
           //not used
        }

        // helper methods

        private class MauiSkMetalViewRetained : SKMetalViewRetained
        {
            public bool IgnorePixelScaling { get; set; }

            // P1-Cross Optimization: Track first frame render for initialization
            public bool HasRenderedFirstFrame { get; set; }

            protected override void OnPaintSurface(SkiaSharp.Views.iOS.SKPaintMetalSurfaceEventArgs e)
            {
                // P1-Cross Optimization: Mark first frame as rendered
                if (!HasRenderedFirstFrame)
                {
                    HasRenderedFirstFrame = true;
                }

                if (IgnorePixelScaling)
                {
                    var userVisibleSize = new SKSizeI((int)Bounds.Width, (int)Bounds.Height);
                    var canvas = e.Surface.Canvas;
                    canvas.Scale((float)ContentScaleFactor);
                    canvas.Save();

                    e = new SkiaSharp.Views.iOS.SKPaintMetalSurfaceEventArgs(e.Surface, e.BackendRenderTarget, e.Origin, e.Info.WithSize(userVisibleSize), e.Info);
                }

                base.OnPaintSurface(e);
            }
        }

        private class PaintSurfaceProxy : SKEventProxy<ISKGLView, SKMetalViewRetained>
        {
            private SKSizeI lastCanvasSize;
            private GRContext? lastGRContext;

            protected override void OnConnect(ISKGLView virtualView, SKMetalViewRetained platformView) =>
                platformView.PaintSurface += OnPaintSurface;

            protected override void OnDisconnect(SKMetalViewRetained platformView) =>
                platformView.PaintSurface -= OnPaintSurface;

            private void OnPaintSurface(object? sender, SkiaSharp.Views.iOS.SKPaintMetalSurfaceEventArgs e)
            {
                if (VirtualView is not { } view)
                    return;

                var newCanvasSize = e.Info.Size;
                if (lastCanvasSize != newCanvasSize)
                {
                    lastCanvasSize = newCanvasSize;
                    view.OnCanvasSizeChanged(newCanvasSize);
                }
                if (sender is SKMetalViewRetained platformView)
                {
                    var newGRContext = platformView.GRContext;
                    if (lastGRContext != newGRContext)
                    {
                        lastGRContext = newGRContext;
                        view.OnGRContextChanged(newGRContext);
                    }
                }

                view.OnPaintSurface(new (e.Surface, e.BackendRenderTarget, e.Origin, e.Info, e.RawInfo));
            }
        }



    }
}
