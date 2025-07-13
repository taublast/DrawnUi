using Microsoft.Maui.Handlers;

namespace DrawnUi.Views
{
    public partial class SKGLViewHandlerRetained : ViewHandler<ISKGLView, SKSwapChainPanelRetained>
    {
        private SKSizeI lastCanvasSize;
        private GRContext? lastGRContext;
        //private SKTouchHandler? touchHandler;

        protected override SKSwapChainPanelRetained CreatePlatformView() => new MauiSkSwapChainPanelRetained();

        protected override void ConnectHandler(SKSwapChainPanelRetained platformView)
        {
            platformView.PaintSurface += OnPaintSurface;

            base.ConnectHandler(platformView);
        }

        protected override void DisconnectHandler(SKSwapChainPanelRetained platformView)
        {
            //touchHandler?.Detach(platformView);
            //touchHandler = null;

            platformView.PaintSurface -= OnPaintSurface;

            base.DisconnectHandler(platformView);
        }

        // Mapper actions / properties

        public static void OnInvalidateSurface(SKGLViewHandlerRetained handler, ISKGLView view, object? args)
        {
            if (handler?.PlatformView == null)
                return;

            if (!handler.PlatformView.EnableRenderLoop)
                handler.PlatformView.Invalidate();
        }

        public static void MapIgnorePixelScaling(SKGLViewHandlerRetained handler, ISKGLView view)
        {
            if (handler?.PlatformView is not MauiSkSwapChainPanelRetained pv)
                return;

            pv.IgnorePixelScaling = view.IgnorePixelScaling;
            pv.Invalidate();
        }

        public static void MapHasRenderLoop(SKGLViewHandlerRetained handler, ISKGLView view)
        {
            if (handler?.PlatformView == null)
                return;

            handler.PlatformView.EnableRenderLoop = view.HasRenderLoop;
        }

        public static void MapEnableTouchEvents(SKGLViewHandlerRetained handler, ISKGLView view)
        {
            if (handler?.PlatformView == null)
                return;

            //handler.touchHandler ??= new SKTouchHandler(
            //    args => view.OnTouch(args),
            //    (x, y) => handler.OnGetScaledCoord(x, y));

            //handler.touchHandler?.SetEnabled(handler.PlatformView, view.EnableTouchEvents);
        }

        public static void MapBackground(SKGLViewHandlerRetained handler, ISKGLView view)
        {
            // WinUI 3 limitation:
            // Setting 'Background' property is not supported on SwapChainPanel.'.
        }

        // helper methods

        private void OnPaintSurface(object? sender, SkiaSharp.Views.Windows.SKPaintGLSurfaceEventArgs e)
        {
            var newCanvasSize = e.Info.Size;
            if (lastCanvasSize != newCanvasSize)
            {
                lastCanvasSize = newCanvasSize;
                VirtualView?.OnCanvasSizeChanged(newCanvasSize);
            }

            if (sender is SKSwapChainPanelRetained platformView)
            {
                var newGRContext = platformView.GRContext;
                if (lastGRContext != newGRContext)
                {
                    lastGRContext = newGRContext;
                    VirtualView?.OnGRContextChanged(newGRContext);
                }
            }

            VirtualView?.OnPaintSurface(new SKPaintGLSurfaceEventArgs(e.Surface, e.BackendRenderTarget, e.Origin,
                e.Info, e.RawInfo));
        }

        private SKPoint OnGetScaledCoord(double x, double y)
        {
            if (VirtualView?.IgnorePixelScaling == false && PlatformView != null)
            {
                var scale = PlatformView.ContentsScale;

                x *= scale;
                y *= scale;
            }

            return new SKPoint((float)x, (float)y);
        }

        private class MauiSkSwapChainPanelRetained : SKSwapChainPanelRetained
        {
            public bool IgnorePixelScaling { get; set; }

            protected override void OnPaintSurface(SkiaSharp.Views.Windows.SKPaintGLSurfaceEventArgs e)
            {
                if (IgnorePixelScaling)
                {
                    var density = (float)ContentsScale;
                    var userVisibleSize = new SKSizeI((int)(e.Info.Width / density), (int)(e.Info.Height / density));
                    var canvas = e.Surface.Canvas;
                    canvas.Scale(density);
                    canvas.Save();

                    e = new(e.Surface, e.BackendRenderTarget, e.Origin, e.Info.WithSize(userVisibleSize), e.Info);
                }

                base.OnPaintSurface(e);
            }
        }
    }
}
