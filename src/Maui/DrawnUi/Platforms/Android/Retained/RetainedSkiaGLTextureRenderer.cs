using System.Collections.Concurrent;
using Android.Opengl;
using SKPaintGLSurfaceEventArgs = SkiaSharp.Views.Android.SKPaintGLSurfaceEventArgs;

namespace DrawnUi
{
    public class RetainedSkiaGLTextureRenderer : SkiaGLTextureRenderer
    {
        private SKSurface _retainedSurface;
        private bool _needsFullRedraw = true;
        private readonly ConcurrentBag<SurfaceTrashItem> _trashBag = new();
        private bool _cleanupRunning;
        private Task _cleanupTask;
        private long _frameCounter = 0;

        private struct SurfaceTrashItem
        {
            public SKSurface Surface { get; set; }
            public long FrameCount { get; set; }
        }

        public RetainedSkiaGLTextureRenderer()
        {
            _cleanupRunning = true;
            _cleanupTask = Task.Run(async () =>
            {
                while (_cleanupRunning)
                {
                    await Task.Delay(200);
                    CleanupTrashBag();
                }
            });
        }

        /// <summary>
        /// Cleans up surfaces after 3 frames (Android triple buffering)
        /// </summary>
        private void CleanupTrashBag()
        {
            var itemsToRequeue = new List<SurfaceTrashItem>();

            while (_trashBag.TryTake(out var item))
            {
                // Android uses triple buffering at best, so after 3 frames the surface is safe
                if (_frameCounter - item.FrameCount > 3)
                {
                    item.Surface?.Dispose();
                    //System.Diagnostics.Debug.WriteLine($"Disposed surface after {_frameCounter - item.FrameCount} frames");
                }
                else
                {
                    itemsToRequeue.Add(item);
                }
            }

            foreach (var item in itemsToRequeue)
            {
                _trashBag.Add(item);
            }
        }

        public override void OnDrawFrame()
        {
            if (Context == null)
            {
                var glInterface = GRGlInterface.Create();
                Context = GRContext.CreateGl(glInterface);
            }

            // SHARED: Ensure renderTarget is ready (used by both fast and normal paths)
            if (renderTarget == null || LastSize != NewSize || !renderTarget.IsValid)
            {
                LastSize = NewSize;

                var buffer = new int[3];
                GLES20.GlGetIntegerv(GLES20.GlFramebufferBinding, buffer, 0);
                GLES20.GlGetIntegerv(GLES20.GlStencilBits, buffer, 1);
                GLES20.GlGetIntegerv(GLES20.GlSamples, buffer, 2);
                var samples = buffer[2];
                var maxSamples = Context.GetMaxSurfaceSampleCount(colorType);
                if (samples > maxSamples)
                    samples = maxSamples;
                GlInfo = new GRGlFramebufferInfo((uint)buffer[0], colorType.ToGlSizedFormat());

                // Dispose old retained surface if exists (for normal rendering path)
                if (_retainedSurface != null)
                {
                    _trashBag.Add(new SurfaceTrashItem
                    {
                        Surface = _retainedSurface,
                        FrameCount = _frameCounter
                    });
                    _retainedSurface = null;
                }

                renderTarget?.Dispose();
                renderTarget = new GRBackendRenderTarget(NewSize.Width, NewSize.Height, samples, buffer[1], GlInfo);

                _needsFullRedraw = true;
            }

            // FAST FIRST FRAME: Use CPU pre-rendered image if available
            if (PreRenderedImage != null)
            {
                try
                {
                    // Fast blit: Just draw pre-rendered image to framebuffer
                    using (var framebufferSurface = SKSurface.Create(Context, renderTarget, surfaceOrigin, colorType))
                    {
                        framebufferSurface.Canvas.DrawImage(PreRenderedImage, 0, 0);
                        framebufferSurface.Canvas.Flush();
                        framebufferSurface.Flush();
                    }

                    Context.Flush();

                    // Dispose pre-rendered image and clear reference
                    PreRenderedImage.Dispose();
                    PreRenderedImage = null;

                    _frameCounter++;

                    System.Diagnostics.Debug.WriteLine("[RetainedRenderer] First frame: Used CPU pre-rendered image (fast blit)");
                    return;
                }
                catch (Exception ex)
                {
                    Super.Log(ex);
                    // If fast path fails, dispose and fall through to normal rendering
                    PreRenderedImage?.Dispose();
                    PreRenderedImage = null;
                }
            }

            // NORMAL RENDERING PATH

            if (_retainedSurface == null)
            {
                _retainedSurface = SKSurface.Create(Context, renderTarget, surfaceOrigin, colorType);
                _needsFullRedraw = true;
            }

            if (_needsFullRedraw)
            {
                _retainedSurface.Canvas.Clear(SKColors.Transparent);
            }

            try
            {
                using (new SKAutoCanvasRestore(_retainedSurface.Canvas, true))
                {
                    var e = new SKPaintGLSurfaceEventArgs(_retainedSurface, renderTarget, surfaceOrigin, colorType);
                    OnPaintSurface(e);
                }

                using var framebufferSurface = SKSurface.Create(Context, renderTarget, surfaceOrigin, colorType);

                using var image = _retainedSurface.Snapshot();
                framebufferSurface.Canvas.DrawImage(image, 0, 0);

                framebufferSurface.Canvas.Flush();
                framebufferSurface.Flush();

                Context.Flush();

                _needsFullRedraw = false;
                _frameCounter++;
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cleanupRunning = false;
                _cleanupTask?.Wait(TimeSpan.FromSeconds(1));

                // Dispose pre-rendered image if never used
                PreRenderedImage?.Dispose();
                PreRenderedImage = null;

                _retainedSurface?.Dispose();
                _retainedSurface = null;

                while (_trashBag.TryTake(out var item))
                {
                    item.Surface?.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
