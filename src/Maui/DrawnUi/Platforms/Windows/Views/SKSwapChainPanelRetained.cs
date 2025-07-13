using System.Collections.Concurrent;
using SkiaSharp.Views.Windows;

namespace DrawnUi.Views
{
    /// <summary>
    /// SwapChain panel with retained surface rendering for improved performance
    /// </summary>
    public class SKSwapChainPanelRetained : AngleSwapChainPanel
    {
        public Guid Uid = Guid.NewGuid();

        private const SKColorType ColorType = SKColorType.Rgba8888;
        private const GRSurfaceOrigin SurfaceOrigin = GRSurfaceOrigin.BottomLeft;

        private GRGlInterface _glInterface;
        private GRContext _context;
        private SKSurface _retainedSurface;
        private SKSurface _framebufferSurface; // Reuse instead of creating each frame
        private GRBackendRenderTarget _renderTarget;
        private SKSizeI _lastSize;
        private bool _needsFullRedraw = true;
        private readonly object _surfaceLock = new();

        public SKSize CanvasSize => _lastSize;
        public GRContext GRContext => _context;
        public event EventHandler<SkiaSharp.Views.Windows.SKPaintGLSurfaceEventArgs> PaintSurface;

        /// <summary>
        /// Raises the PaintSurface event
        /// </summary>
        /// <param name="e">Paint surface event arguments</param>
        protected virtual void OnPaintSurface(SkiaSharp.Views.Windows.SKPaintGLSurfaceEventArgs e)
        {
            PaintSurface?.Invoke(this, e);
        }

        private bool stopped;

        protected override void OnRenderFrame(Windows.Foundation.Rect rect)
        {
            try
            {
                if (_context == null)
                {
                    _glInterface = GRGlInterface.Create();
                    _context = GRContext.CreateGl(_glInterface);
                }

                var newSize = new SKSizeI((int)rect.Width, (int)rect.Height);
                bool sizeChanged = _lastSize != newSize;
                bool needNewSurfaces = _retainedSurface == null || sizeChanged;

                if (sizeChanged || _renderTarget == null || !_renderTarget.IsValid)
                {
                    Debug.WriteLine("Creating new render target");

                    // Dispose old render target immediately and synchronously
                    _renderTarget?.Dispose();

                    OpenGles.GetIntegerv(OpenGles.GL_FRAMEBUFFER_BINDING, out var framebuffer);
                    OpenGles.GetIntegerv(OpenGles.GL_STENCIL_BITS, out var stencil);
                    OpenGles.GetIntegerv(OpenGles.GL_SAMPLES, out var samples);
                    var maxSamples = _context.GetMaxSurfaceSampleCount(ColorType);
                    samples = Math.Min(samples, maxSamples);
                    var glInfo = new GRGlFramebufferInfo((uint)framebuffer, ColorType.ToGlSizedFormat());

                    _renderTarget = new GRBackendRenderTarget(newSize.Width, newSize.Height, samples, stencil, glInfo);
                    needNewSurfaces = true;
                }

                if (needNewSurfaces && newSize.Height > 0 && newSize.Width > 0)
                {
                    // Force context flush before creating new surfaces
                    _context.Flush();

                    // Dispose old surfaces immediately and synchronously
                    _retainedSurface?.Dispose();
                    _framebufferSurface?.Dispose();

                    _retainedSurface = SKSurface.Create(_context, _renderTarget, SurfaceOrigin, ColorType);
                    _framebufferSurface = SKSurface.Create(_context, _renderTarget, SurfaceOrigin, ColorType);

                    Debug.WriteLine("Created new retained and framebuffer surfaces");

                    _lastSize = newSize;
                    _needsFullRedraw = true;
                }

                if (_retainedSurface == null || _framebufferSurface == null)
                    return;

                lock (_surfaceLock)
                {
                    using (new SKAutoCanvasRestoreFixed(_retainedSurface.Canvas, true))
                    {
                        OnPaintSurface(new(_retainedSurface, _renderTarget, SurfaceOrigin, ColorType));
                    }

                    _retainedSurface.Canvas.Flush();

                    //retain result
                    using var image = _retainedSurface.Snapshot();
                    _framebufferSurface.Canvas.DrawImage(image, 0, 0);
                    _framebufferSurface.Canvas.Flush();

                    _context.Flush();
                }

                _needsFullRedraw = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Render frame error: {ex.Message}");
            }
        }

        protected override void OnDestroyingContext()
        {
            base.OnDestroyingContext();

            //all controls have to clear any gpu resources they might have cached
            Super.NeedGlobalUpdate();

            lock (_surfaceLock)
            {
                // Dispose all GPU resources immediately and synchronously
                _framebufferSurface?.Dispose();
                _framebufferSurface = null;

                _retainedSurface?.Dispose();
                _retainedSurface = null;

                _renderTarget?.Dispose();
                _renderTarget = null;

                // Abandon context before disposing to prevent validation errors
                _context?.AbandonContext(false);
                _context?.Dispose();
                _context = null;

                _glInterface?.Dispose();
                _glInterface = null;

                _lastSize = default;
                _needsFullRedraw = true;
            }

        }
    }

}
