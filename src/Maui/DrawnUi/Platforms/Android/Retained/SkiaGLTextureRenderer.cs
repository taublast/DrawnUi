using Android.Opengl;
using SKPaintGLSurfaceEventArgs = SkiaSharp.Views.Android.SKPaintGLSurfaceEventArgs;

namespace DrawnUi;

public abstract partial class SkiaGLTextureRenderer : Java.Lang.Object, SkiaGLTextureView.IRenderer
{
    protected const SKColorType colorType = SKColorType.Rgba8888;
    protected const GRSurfaceOrigin surfaceOrigin = GRSurfaceOrigin.BottomLeft;
    protected GRContext context;
    protected GRGlFramebufferInfo glInfo;
    protected GRBackendRenderTarget renderTarget;
    private SKSurface surface;
    private SKCanvas canvas;
    protected SKSizeI lastSize;
    protected SKSizeI newSize;
    public SKSize CanvasSize => lastSize;
    public GRContext GRContext => context;

    protected virtual void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
    }

    public virtual void OnDrawFrame()
    {
        //thank you but no
        //GLES10.GlClear(GLES10.GlColorBufferBit | GLES10.GlDepthBufferBit | GLES10.GlStencilBufferBit);

        // create the contexts if not done already
        if (context == null)
        {
            var glInterface = GRGlInterface.Create();
            context = GRContext.CreateGl(glInterface);
        }

        // manage the drawing surface
        if (renderTarget == null || lastSize != newSize || !renderTarget.IsValid)
        {
            // create or update the dimensions
            lastSize = newSize;

            // read the info from the buffer
            var buffer = new int[3];
            GLES20.GlGetIntegerv(GLES20.GlFramebufferBinding, buffer, 0);
            GLES20.GlGetIntegerv(GLES20.GlStencilBits, buffer, 1);
            GLES20.GlGetIntegerv(GLES20.GlSamples, buffer, 2);
            var samples = buffer[2];
            var maxSamples = context.GetMaxSurfaceSampleCount(colorType);
            if (samples > maxSamples)
                samples = maxSamples;
            glInfo = new GRGlFramebufferInfo((uint)buffer[0], colorType.ToGlSizedFormat());

            // destroy the old surface
            surface?.Dispose();
            surface = null;
            canvas = null;

            // re-create the render target
            renderTarget?.Dispose();
            renderTarget = new GRBackendRenderTarget(newSize.Width, newSize.Height, samples, buffer[1], glInfo);
        }

        // create the surface
        if (surface == null)
        {
            surface = SKSurface.Create(context, renderTarget, surfaceOrigin, colorType);
        }

        if (surface != null)
        {
            canvas = surface.Canvas;
        }

        if (canvas != null)
        {
            var restore = canvas.Save();

            var e = new SKPaintGLSurfaceEventArgs(surface, renderTarget, surfaceOrigin, colorType);
            OnPaintSurface(e);

            canvas.RestoreToCount(restore);

            canvas.Flush();
            context.Flush();
        }
    }

    public void OnSurfaceChanged(int width, int height)
    {
        GLES20.GlViewport(0, 0, width, height);

        // get the new surface size
        newSize = new SKSizeI(width, height);
    }

    public void OnSurfaceCreated(EGLConfig config)
    {
        // Create the context and resources
        if (context != null)
        {
            FreeContext();
        }
    }

    public void OnSurfaceDestroyed()
    {
        FreeContext();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FreeContext();
        }

        base.Dispose(disposing);
    }

    private void FreeContext()
    {
        surface?.Dispose();
        surface = null;
        renderTarget?.Dispose();
        renderTarget = null;
        context?.Dispose();
        context = null;
    }
}
