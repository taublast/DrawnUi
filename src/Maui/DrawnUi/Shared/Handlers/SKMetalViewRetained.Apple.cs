using CoreGraphics;
using Foundation;
using Metal;
using MetalKit;
using SkiaSharp.Views.iOS;
using UIKit;

namespace DrawnUi.Views
{
    /// <summary>
    /// A Metal-backed SkiaSharp view that implements retained rendering
    /// </summary>
    [Register(nameof(SKMetalViewRetained))]
    [DesignTimeVisible(true)]
    public class SKMetalViewRetained : MTKView, IMTKViewDelegate, IComponent
    {
        // for IComponent
#pragma warning disable 67
        private event EventHandler DisposedInternal;
#pragma warning restore 67
        ISite IComponent.Site { get; set; }
        event EventHandler IComponent.Disposed
        {
            add { DisposedInternal += value; }
            remove { DisposedInternal -= value; }
        }

        // ── Shared Metal pipeline ────────────────────────────────────────────────
        // One GRContext, one command queue, one Metal backend context shared across
        // all SKMetalViewRetained instances. Ref-counted: created on first view,
        // destroyed when the last view is disposed.
        private static GRContext _sharedContext;
        private static IMTLCommandQueue _sharedQueue;
        private static GRMtlBackendContext _sharedBackendContext;
        private static GCHandle _sharedQueuePin;
        private static int _sharedContextRefCount;
        private static readonly object _sharedContextLock = new object();

        // ── Per-view state ───────────────────────────────────────────────────────
        private bool _designMode;
        private bool _initialized; // true only if shared ref count was incremented
        private IMTLDevice _device;
        private SKSize _canvasSize;

        // Retained rendering buffer (per-view: each Canvas has its own texture)
        private IMTLTexture _retainedTexture;
        private IMTLTexture _pendingTexture;
        private readonly object _textureSwapLock = new object();
        private volatile bool _swapPending;
        private bool _firstFrame = true;
        private bool _needsFullRedraw = true;

        // CPU Pre-rendering optimization (per-view)
        private SKImage _preRenderedImage;
        private bool _preRenderingAttempted;

        // ── Public surface ───────────────────────────────────────────────────────

        /// <summary>
        /// Gets a value indicating whether the view is using manual refresh mode.
        /// </summary>
        public bool ManualRefresh => Paused && EnableSetNeedsDisplay;

        /// <summary>
        /// Gets the current canvas size.
        /// </summary>
        public SKSize CanvasSize => _canvasSize;

        /// <summary>
        /// Gets the SkiaSharp GRContext used for rendering (shared across all instances).
        /// </summary>
        public GRContext GRContext => _sharedContext;

        /// <summary>
        /// Gets the Metal backend context (shared across all instances).
        /// </summary>
        public GRMtlBackendContext MetalBackend => _sharedBackendContext;

        public IMTLCommandQueue Queue { get; protected set; }

        // ── Constructors ─────────────────────────────────────────────────────────

        // created in code
        public SKMetalViewRetained()
            : this(CGRect.Empty)
        {
        }

        // created in code
        public SKMetalViewRetained(CGRect frame)
            : base(frame, null)
        {
            Initialize();
        }

        // created in code
        public SKMetalViewRetained(CGRect frame, IMTLDevice device)
            : base(frame, device)
        {
            Initialize();
        }

        // created via designer
        public SKMetalViewRetained(IntPtr p)
            : base(p)
        {
        }

        // created via designer
        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            Initialize();
        }

        private void Initialize()
        {
            _designMode = ((IComponent)this).Site?.DesignMode == true;

            if (_designMode)
                return;

            _device = Device ?? MTLDevice.SystemDefault;
            if (_device == null)
            {
                Super.Log("Metal is not supported on this device.");
                return;
            }

            // Configure the Metal view
            ColorPixelFormat = MTLPixelFormat.BGRA8Unorm;
            DepthStencilPixelFormat = MTLPixelFormat.Depth32Float_Stencil8;

            nuint sampling = 1;
            if (UIKit.UIDevice.CurrentDevice.CheckSystemVersion(16, 0))
            {
                // Make simulator performant
                if (DeviceInfo.Current.DeviceType == DeviceType.Virtual)
                {
                    DepthStencilStorageMode = MTLStorageMode.Private;
                    sampling = 4;
                }
                else
                {
                    DepthStencilStorageMode = MTLStorageMode.Shared;
                }
            }
            SampleCount = sampling;

            // GPU memory used not only for rendering but could be read by SkiaSharp too
            FramebufferOnly = false;

            // Acquire a reference to the shared Metal pipeline.
            // Created on demand by the first view; reused by all subsequent views.
            lock (_sharedContextLock)
            {
                _sharedContextRefCount++;
                _initialized = true;

                if (_sharedContext == null)
                {
                    _sharedQueue = _device.CreateCommandQueue();

                    _sharedBackendContext = new GRMtlBackendContext
                    {
                        Device = _device,
                        Queue = _sharedQueue
                    };

                    // Prevent GC from moving/finalizing the queue while Metal holds raw pointers to it
                    _sharedQueuePin = GCHandle.Alloc(_sharedBackendContext.Queue, GCHandleType.Pinned);

                    _sharedContext = GRContext.CreateMetal(_sharedBackendContext);
                }
            }

            Device = _device;
            Queue = _sharedQueue; // MTKView uses the shared queue for drawable presentation
            Delegate = this;
        }

        // ── MTKView delegate ─────────────────────────────────────────────────────

        void IMTKViewDelegate.DrawableSizeWillChange(MTKView view, CGSize size)
        {
            if (stopped)
                return;

            var newSize = size.ToSKSize();
            _canvasSize = newSize;

            TryCpuPreRendering();

            PrepareNewTexture();

            if (ManualRefresh)
                SetNeedsDisplay();
        }

        void IMTKViewDelegate.Draw(MTKView view)
        {
            if (_designMode || _sharedBackendContext.Queue == null || CurrentDrawable?.Texture == null || stopped)
                return;

            _canvasSize = DrawableSize.ToSKSize();
            if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0)
                return;

            var context = _sharedContext;
            if (context == null)
                return;

            try
            {
                inQueue++;

                // Handle initial frame or ensure texture exists
                if (_firstFrame || _retainedTexture == null)
                {
                    PrepareNewTexture();
                    PerformTextureSwap(); // Immediate swap for first frame
                    _firstFrame = false;
                }

                // Try CPU pre-rendering if not attempted yet
                TryCpuPreRendering();

                // Get current texture (snapshot to avoid changes during rendering)
                IMTLTexture textureToUse;
                lock (_textureSwapLock)
                {
                    // Check for pending texture swap
                    if (_swapPending && _pendingTexture != null)
                    {
                        PerformTextureSwap();
                    }

                    textureToUse = _retainedTexture;
                    if (textureToUse == null)
                    {
                        //prevent mid-frame jank
                        return;
                    }
                }

                // Create Metal texture info and render target (shared by both paths)
                var metalInfo = new GRMtlTextureInfo(textureToUse);
                using var renderTarget = new GRBackendRenderTarget(
                    (int)_canvasSize.Width,
                    (int)_canvasSize.Height,
                    1, // Sample count must be 1 for render targets
                    metalInfo);

                // FAST FIRST FRAME: Use CPU pre-rendered image if available
                // CRITICAL: Check placement - must happen BEFORE normal rendering setup
                if (_preRenderedImage != null)
                {
                    try
                    {
                        // Fast blit: Draw pre-rendered image to texture surface
                        using (var surface = SKSurface.Create(context, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888))
                        {
                            surface.Canvas.DrawImage(_preRenderedImage, 0, 0);
                            surface.Flush();
                        }

                        context.Flush();

                        // Dispose pre-rendered image and clear reference BEFORE returning
                        // This ensures no other frame can see or use this image
                        _preRenderedImage.Dispose();
                        _preRenderedImage = null;

                        _needsFullRedraw = false;

                        //Debug.WriteLine("[SKMetalView] First frame: Used CPU pre-rendered image (fast blit)");

                        // Immediately copy to screen and return - skip normal rendering
                        using var commandBuffer = _sharedBackendContext.Queue.CommandBuffer();
                        if (commandBuffer == null) return;
                        using var blitEncoder = commandBuffer.BlitCommandEncoder;

                        blitEncoder.CopyFromTexture(
                            textureToUse, 0, 0, new MTLOrigin(0, 0, 0),
                            new MTLSize((int)_canvasSize.Width, (int)_canvasSize.Height, 1),
                            CurrentDrawable.Texture, 0, 0, new MTLOrigin(0, 0, 0));

                        blitEncoder.EndEncoding();
                        commandBuffer.PresentDrawable(CurrentDrawable);
                        commandBuffer.Commit();

                        return; // CRITICAL: Exit here - do NOT run normal rendering path
                    }
                    catch (Exception ex)
                    {
                        Super.Log($"[SKMetalView] Fast blit failed: {ex.Message}");
                        // If fast path fails, dispose and fall through to normal rendering
                        _preRenderedImage?.Dispose();
                        _preRenderedImage = null;
                    }
                }

                // NORMAL RENDERING PATH
                // Create surface from the render target
                using var surfaceNormal = SKSurface.Create(context, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                using var canvas = surfaceNormal.Canvas;

                // Clear if needed
                if (_needsFullRedraw)
                {
                    canvas.Clear(SKColors.Transparent);
                }

                // Pass surface to user for incremental updates
                var e = new SKPaintMetalSurfaceEventArgs(surfaceNormal, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                OnPaintSurface(e);

                surfaceNormal.Flush();
                context.Flush();

                _needsFullRedraw = false;

                // Copy retained texture to screen
                using var commandBuffer2 = _sharedBackendContext.Queue.CommandBuffer();
                if (commandBuffer2 == null) return;
                using var blitEncoder2 = commandBuffer2.BlitCommandEncoder;

                blitEncoder2.CopyFromTexture(
                    textureToUse, 0, 0, new MTLOrigin(0, 0, 0),
                    new MTLSize((int)_canvasSize.Width, (int)_canvasSize.Height, 1),
                    CurrentDrawable.Texture, 0, 0, new MTLOrigin(0, 0, 0));

                blitEncoder2.EndEncoding();
                commandBuffer2.PresentDrawable(CurrentDrawable);
                commandBuffer2.Commit();
            }
            catch (Exception ex)
            {
                Super.Log($"[SKMetalView] Draw exception: {ex.Message}");
            }
            finally
            {
                inQueue--;
            }

        }

        // ── Texture management ───────────────────────────────────────────────────

        /// <summary>
        /// Creates a new texture for future use
        /// </summary>
        private void PrepareNewTexture()
        {
            if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0 || _device == null || stopped)
                return;

            var descriptor = new MTLTextureDescriptor
            {
                TextureType = MTLTextureType.k2D,
                Width = (nuint)_canvasSize.Width,
                Height = (nuint)_canvasSize.Height,
                PixelFormat = ColorPixelFormat,
                Usage = MTLTextureUsage.RenderTarget | MTLTextureUsage.ShaderRead,
                StorageMode = DeviceInfo.Current.DeviceType == DeviceType.Virtual
                    ? MTLStorageMode.Private
                    : MTLStorageMode.Shared,
                SampleCount = 1 //required 1 for skiasharp
            };

            lock (_textureSwapLock)
            {
                // Dispose any existing pending texture
                _pendingTexture?.Dispose();
                _pendingTexture = _device.CreateTexture(descriptor);
                _swapPending = true;
            }
        }

        /// <summary>
        /// Performs the actual texture swap. Must be called within the _textureSwapLock.
        /// </summary>
        private void PerformTextureSwap()
        {
            // This should only be called from within a lock(_textureSwapLock) block
            if (_pendingTexture != null && !stopped)
            {
                _retainedTexture?.Dispose();
                _retainedTexture = _pendingTexture;
                _pendingTexture = null;
                _swapPending = false;
            }
        }

        // ── CPU pre-rendering ────────────────────────────────────────────────────

        /// <summary>
        /// CPU PRE-RENDERING OPTIMIZATION
        /// Performs initial rendering on CPU surface before Metal initializes
        /// This eliminates blank canvas during view initialization
        /// </summary>
        private void TryCpuPreRendering()
        {
            // Skip if already attempted
            if (_preRenderingAttempted || !Super.IsPrerenderingEnabled || stopped)
                return;

            // Skip if dimensions not available yet
            if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0)
            {
                Console.WriteLine($"[SKMetalView] TryCpuPreRendering - Skipped, invalid dimensions ({_canvasSize.Width}x{_canvasSize.Height})");
                return;
            }

            _preRenderingAttempted = true;

            try
            {
                var imageInfo = new SKImageInfo((int)_canvasSize.Width, (int)_canvasSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var softSurface = SKSurface.Create(imageInfo);

                if (softSurface != null)
                {
                    Console.WriteLine($"[SKMetalView] TryCpuPreRendering - CPU pre-rendering ({_canvasSize.Width}x{_canvasSize.Height})");

                    using (new SKAutoCanvasRestoreFixed(softSurface.Canvas, true))
                    {
                        // Create dummy renderTarget for CPU rendering (won't be used but required by constructor)
                        // Use a fake Metal texture info
                        var dummyMtlInfo = new GRMtlTextureInfo(IntPtr.Zero);
                        using var dummyRenderTarget = new GRBackendRenderTarget(
                            (int)_canvasSize.Width,
                            (int)_canvasSize.Height,
                            1,
                            dummyMtlInfo);

                        var e = new SKPaintMetalSurfaceEventArgs(
                            softSurface,
                            dummyRenderTarget,
                            GRSurfaceOrigin.TopLeft,
                            SKColorType.Bgra8888
                        );

                        OnPaintSurface(e);
                    }

                    // Capture pre-rendered result for fast first Metal frame
                    _preRenderedImage = softSurface.Snapshot();
                    Console.WriteLine($"[SKMetalView] TryCpuPreRendering - CPU pre-render complete, snapshot captured");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SKMetalView] TryCpuPreRendering - CPU pre-render failed: {ex.Message}");
                // Dispose if we still have reference
                _preRenderedImage?.Dispose();
                _preRenderedImage = null;
            }

        }

        // ── Paint surface ────────────────────────────────────────────────────────

        public event EventHandler<SKPaintMetalSurfaceEventArgs> PaintSurface;

        /// <summary>
        /// Raises the PaintSurface event.
        /// </summary>
        protected virtual void OnPaintSurface(SKPaintMetalSurfaceEventArgs e)
        {
            if (stopped)
            {
                return;
            }
            PaintSurface?.Invoke(this, e);
        }

        /// <summary>
        /// Forces the view to redraw its contents.
        /// </summary>
        public void InvalidateSurface()
        {
            if (stopped)
            {
                return;
            }
            SetNeedsDisplay();
        }

        // ── Dispose ──────────────────────────────────────────────────────────────

        bool stopped = false;

        int inQueue = 0;

        protected override void Dispose(bool disposing)
        {
            stopped = true;

            if (disposing)
            {
                Tasks.StartDelayed(TimeSpan.FromMilliseconds(50), async () =>
                {
                    while (inQueue > 0)
                    {
                        await Task.Delay(16);
                    }

                    // ── Per-view resources (dispose immediately, no sharing) ──────
                    _preRenderedImage?.Dispose();
                    _preRenderedImage = null;

                    lock (_textureSwapLock)
                    {
                        _retainedTexture?.Dispose();
                        _pendingTexture?.Dispose();
                        _retainedTexture = null;
                        _pendingTexture = null;
                        _swapPending = false;
                    }

                    // ── Shared pipeline (decrement ref count, release when last) ──
                    lock (_sharedContextLock)
                    {
                        if (_initialized)
                        {
                            _initialized = false;
                            _sharedContextRefCount--;

                            if (_sharedContextRefCount == 0 && _sharedContext != null)
                            {
                                // Flush all pending Skia GPU work and release the
                                // GPU resource cache before tearing down the context.
                                _sharedContext.Flush();
                                _sharedContext.PurgeResources();
                                _sharedContext.Dispose();
                                _sharedContext = null;

                                // Release the queue pin, then explicitly dispose the
                                // queue so the ObjC reference count drops to zero.
                                if (_sharedQueuePin.IsAllocated)
                                {
                                    _sharedQueuePin.Free();
                                }
                                (_sharedQueue as IDisposable)?.Dispose();
                                _sharedQueue = null;
                                _sharedBackendContext = default;
                            }
                        }
                    }

                    base.Dispose(disposing);

                    Debug.WriteLine($"[SKMetalView] Disposed!");
                });
            }
        }
    }
}
