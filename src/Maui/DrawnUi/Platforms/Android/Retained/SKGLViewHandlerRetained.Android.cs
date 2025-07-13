using Android.Content;
using Android.Opengl;
using DrawnUi;
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using SkiaSharp.Views.Android;
using SkiaSharp.Views.Maui.Platform;
using SKPaintGLSurfaceEventArgs = SkiaSharp.Views.Android.SKPaintGLSurfaceEventArgs;

namespace DrawnUi.Views;

public partial class SKGLViewHandlerRetained : ViewHandler<ISKGLView, SkiaGLTexture>
{

    private SKSizeI lastCanvasSize;
    private GRContext? lastGRContext;
    
    protected override SkiaGLTexture CreatePlatformView()
    {
        var view = new MauiSKGLTextureView(Context);
        view.SetOpaque(false);
        return view;
    }

    protected override void ConnectHandler(SkiaGLTexture platformView)
    {
        platformView.PaintSurface += OnPaintSurface;

        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(SkiaGLTexture platformView)
    {
        platformView.PaintSurface -= OnPaintSurface;

        base.DisconnectHandler(platformView);
    }

    // Mapper actions / properties

    public static void OnInvalidateSurface(SKGLViewHandlerRetained handler, ISKGLView view, object? args)
    {
        if (handler?.PlatformView == null)
            return;

        if (handler.PlatformView.RenderMode == Rendermode.WhenDirty)
            handler.PlatformView.RequestRender();
    }

    public static void MapIgnorePixelScaling(SKGLViewHandlerRetained handler, ISKGLView view)
    {
        if (handler?.PlatformView is not MauiSKGLTextureView pv)
            return;

        pv.IgnorePixelScaling = view.IgnorePixelScaling;
        pv.RequestRender();
    }

    public static void MapHasRenderLoop(SKGLViewHandlerRetained handler, ISKGLView view)
    {
        if (handler?.PlatformView == null)
            return;

        handler.PlatformView.RenderMode = view.HasRenderLoop
            ? Rendermode.Continuously
            : Rendermode.WhenDirty;
    }

    public static void MapEnableTouchEvents(SKGLViewHandlerRetained handler, ISKGLView view)
    {
        //handler.touchHandler ??= new SKTouchHandler(
        //    args => view.OnTouch(args),
        //    (x, y) => handler.OnGetScaledCoord(x, y));

        //handler.touchHandler?.SetEnabled(handler.PlatformView, view.EnableTouchEvents);
    }

    // helper methods

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var newCanvasSize = e.Info.Size;
        if (lastCanvasSize != newCanvasSize)
        {
            lastCanvasSize = newCanvasSize;
            VirtualView?.OnCanvasSizeChanged(newCanvasSize);
        }

        if (sender is SkiaGLTexture platformView)
        {
            var newGRContext = platformView.GRContext;
            if (lastGRContext != newGRContext)
            {
                lastGRContext = newGRContext;
                VirtualView?.OnGRContextChanged(newGRContext);
            }
        }

        VirtualView?.OnPaintSurface(new (e.Surface, e.BackendRenderTarget, e.Origin, e.Info,
            e.RawInfo));
    }

    private SKPoint OnGetScaledCoord(double x, double y)
    {
        if (VirtualView?.IgnorePixelScaling == true && Context != null)
        {
            x = Context.FromPixels(x);
            y = Context.FromPixels(y);
        }

        return new SKPoint((float)x, (float)y);
    }

    private class MauiSKGLTextureView : SkiaGLTexture
    {
        private float density;

        public MauiSKGLTextureView(Context context)
            : base(context)
        {
            density = Resources?.DisplayMetrics?.Density ?? 1;
        }

        public bool IgnorePixelScaling { get; set; }

        protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
        {
            if (IgnorePixelScaling)
            {
                var userVisibleSize = new SKSizeI((int)(e.Info.Width / density), (int)(e.Info.Height / density));
                var canvas = e.Surface.Canvas;
                canvas.Scale(density);
                canvas.Save();

                e = new SKPaintGLSurfaceEventArgs(e.Surface, e.BackendRenderTarget, e.Origin,
                    e.Info.WithSize(userVisibleSize), e.Info);
            }

            base.OnPaintSurface(e);
        }
    }
}
