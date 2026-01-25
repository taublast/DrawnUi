using Android.Opengl;
using SKPaintGLSurfaceEventArgs = SkiaSharp.Views.Android.SKPaintGLSurfaceEventArgs;

namespace DrawnUi;

public abstract partial class SkiaGLTextureRenderer : Java.Lang.Object, SkiaGLTextureView.IRenderer
{
    protected const SKColorType colorType = SKColorType.Rgba8888;
    protected const GRSurfaceOrigin surfaceOrigin = GRSurfaceOrigin.BottomLeft;
    protected GRContext Context;
    protected GRGlFramebufferInfo GlInfo;
    protected GRBackendRenderTarget renderTarget;
    protected SKSurface Surface;
    protected SKCanvas Canvas;
    protected SKSizeI LastSize;
    protected SKSizeI NewSize;
    public SKSize CanvasSize => LastSize;
    public GRContext GRContext => Context;
    public SKImage PreRenderedImage { get; set; }

    public virtual void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
    }

    public virtual void OnDrawFrame()
    {
        //thank you but no
        //GLES10.GlClear(GLES10.GlColorBufferBit | GLES10.GlDepthBufferBit | GLES10.GlStencilBufferBit);

        // create the contexts if not done already
        if (Context == null)
        {
            var glInterface = GRGlInterface.Create();
            Context = GRContext.CreateGl(glInterface);
        }

        // manage the drawing surface
        if (renderTarget == null || LastSize != NewSize || !renderTarget.IsValid)
        {
            // create or update the dimensions
            LastSize = NewSize;

            // read the info from the buffer
            var buffer = new int[3];
            GLES20.GlGetIntegerv(GLES20.GlFramebufferBinding, buffer, 0);
            GLES20.GlGetIntegerv(GLES20.GlStencilBits, buffer, 1);
            GLES20.GlGetIntegerv(GLES20.GlSamples, buffer, 2);
            var samples = buffer[2];
            var maxSamples = Context.GetMaxSurfaceSampleCount(colorType);
            if (samples > maxSamples)
                samples = maxSamples;
            GlInfo = new GRGlFramebufferInfo((uint)buffer[0], colorType.ToGlSizedFormat());

            // destroy the old surface
            Surface?.Dispose();
            Surface = null;
            Canvas = null;

            // re-create the render target
            renderTarget?.Dispose();
            renderTarget = new GRBackendRenderTarget(NewSize.Width, NewSize.Height, samples, buffer[1], GlInfo);
        }

        // create the surface
        if (Surface == null)
        {
            Surface = SKSurface.Create(Context, renderTarget, surfaceOrigin, colorType);
        }

        if (Surface != null)
        {
            Canvas = Surface.Canvas;
        }

        if (Canvas != null)
        {
            var restore = Canvas.Save();

            var e = new SKPaintGLSurfaceEventArgs(Surface, renderTarget, surfaceOrigin, colorType);
            OnPaintSurface(e);

            Canvas.RestoreToCount(restore);

            Canvas.Flush();
            Context.Flush();
        }
    }

    public void OnSurfaceChanged(int width, int height)
    {
        GLES20.GlViewport(0, 0, width, height);

        // get the new surface size
        NewSize = new SKSizeI(width, height);
    }

    public void OnSurfaceCreated(EGLConfig config)
    {
        // Create the context and resources
        if (Context != null)
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
        // Dispose pre-rendered image if never used (safety for base class usage)
        PreRenderedImage?.Dispose();
        PreRenderedImage = null;

        Surface?.Dispose();
        Surface = null;
        renderTarget?.Dispose();
        renderTarget = null;
        Context?.Dispose();
        Context = null;
    }
}
