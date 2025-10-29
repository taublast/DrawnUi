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
                    await Task.Delay(100);
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

            if (_retainedSurface == null)
            {
                _retainedSurface = SKSurface.Create(Context, renderTarget, surfaceOrigin, colorType);
                _needsFullRedraw = true;
            }

            if (_needsFullRedraw)
            {
                _retainedSurface.Canvas.Clear(SKColors.Transparent);
            }

            using (new SKAutoCanvasRestore(_retainedSurface.Canvas, true))
            {
                var e = new SKPaintGLSurfaceEventArgs(_retainedSurface, renderTarget, surfaceOrigin, colorType);
                OnPaintSurface(e);
            }

            using (var framebufferSurface = SKSurface.Create(Context, renderTarget, surfaceOrigin, colorType))
            {
                using (var image = _retainedSurface.Snapshot())
                {
                    framebufferSurface.Canvas.DrawImage(image, 0, 0);
                }

                framebufferSurface.Canvas.Flush();
                framebufferSurface.Flush();
            }

            Context.Flush();
            _needsFullRedraw = false;

            _frameCounter++;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cleanupRunning = false;
                _cleanupTask?.Wait(TimeSpan.FromSeconds(1));

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
