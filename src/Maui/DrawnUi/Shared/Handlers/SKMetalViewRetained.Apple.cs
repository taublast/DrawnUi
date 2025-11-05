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

        private bool _designMode;
        private IMTLDevice _device;
        private GRMtlBackendContext _backendContext;
        private GRContext _context;
        private SKSize _canvasSize;

        // Double-buffering for thread safety
        private IMTLTexture _retainedTexture; // Current texture being rendered
        private IMTLTexture _pendingTexture; // New texture awaiting swap
        private readonly object _textureSwapLock = new object();
        private volatile bool _swapPending; // Flag to signal swap
        private bool _firstFrame = true; // Track first frame for initial setup
        private bool _needsFullRedraw = true; // For initial frame or size change
        private GCHandle _queuePin;

        // CPU Pre-rendering optimization
        private SKImage _preRenderedImage;
        private bool _preRenderingAttempted;

        /// <summary>
        /// Gets a value indicating whether the view is using manual refresh mode.
        /// </summary>
        public bool ManualRefresh => Paused && EnableSetNeedsDisplay;

        /// <summary>
        /// Gets the current canvas size.
        /// </summary>
        public SKSize CanvasSize => _canvasSize;

        /// <summary>
        /// Gets the SkiaSharp GRContext used for rendering.
        /// </summary>
        public GRContext GRContext => _context;

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

            Device = _device;
            _backendContext = new GRMtlBackendContext
            {
                Device = _device,
                Queue = _device.CreateCommandQueue()
            };

            // Hook up the drawing
            Delegate = this;

            //fix GC crash
            _queuePin = GCHandle.Alloc(_backendContext.Queue, GCHandleType.Pinned);
        }

        //public override void LayoutSubviews()
        //{
        //    base.LayoutSubviews();

        //    // Update canvas size from frame
        //    var frameSize = Frame.Size.ToSKSize();
        //    if (frameSize.Width > 0 && frameSize.Height > 0)
        //    {
        //        _canvasSize = frameSize;
        //        // Try CPU pre-rendering when layout is established
        //        TryCpuPreRendering();
        //    }
        //}

        void IMTKViewDelegate.DrawableSizeWillChange(MTKView view, CGSize size)
        {
            var newSize = size.ToSKSize();

            _canvasSize = newSize;

            // Try CPU pre-rendering now that we have correct dimensions
            TryCpuPreRendering();

            PrepareNewTexture();

            if (ManualRefresh)
                SetNeedsDisplay(); // only if size *really* changed
        }

        void IMTKViewDelegate.Draw(MTKView view)
        {
            if (_designMode || _backendContext.Queue == null || CurrentDrawable?.Texture == null)
                return;

            _canvasSize = DrawableSize.ToSKSize();
            if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0)
                return;

            // Create context if needed
            _context ??= GRContext.CreateMetal(_backendContext);

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
            if (_preRenderedImage != null)
            {
                try
                {
                    // Fast blit: Just draw pre-rendered image to texture
                    using (var surface = SKSurface.Create(_context, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888))
                    {
                        surface.Canvas.DrawImage(_preRenderedImage, 0, 0);
                        surface.Flush();
                    }

                    _context.Flush();

                    // Dispose pre-rendered image and clear reference
                    _preRenderedImage.Dispose();
                    _preRenderedImage = null;

                    //Debug.WriteLine("[SKMetalView] First frame: Used CPU pre-rendered image (fast blit)");
                }
                catch (Exception ex)
                {
                    Super.Log($"[SKMetalView] Fast blit failed: {ex.Message}");
                    // If fast path fails, dispose and fall through to normal rendering
                    _preRenderedImage?.Dispose();
                    _preRenderedImage = null;
                }
            }

            // NORMAL RENDERING PATH (skip if fast path succeeded)
            if (_preRenderedImage == null)
            {
                // Create surface from the render target
                using var surface = SKSurface.Create(_context, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                using var canvas = surface.Canvas;

                // Pass surface to user for incremental updates
                var e = new SKPaintMetalSurfaceEventArgs(surface, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                OnPaintSurface(e);

                //canvas.Flush();
                surface.Flush();
                _context.Flush();
            }

            // Copy retained texture to screen
            using var commandBuffer = _backendContext.Queue.CommandBuffer();
            if (commandBuffer == null) return;
            using var blitEncoder = commandBuffer.BlitCommandEncoder;

            blitEncoder.CopyFromTexture(
                textureToUse, 0, 0, new MTLOrigin(0, 0, 0),
                new MTLSize((int)_canvasSize.Width, (int)_canvasSize.Height, 1),
                CurrentDrawable.Texture, 0, 0, new MTLOrigin(0, 0, 0));

            blitEncoder.EndEncoding();
            commandBuffer.PresentDrawable(CurrentDrawable);
            commandBuffer.Commit();
        }

        /// <summary>
        /// Creates a new texture for future use
        /// </summary>
        private void PrepareNewTexture()
        {
            if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0 || _device == null)
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
            if (_pendingTexture != null)
            {
                _retainedTexture?.Dispose();
                _retainedTexture = _pendingTexture;
                _pendingTexture = null;
                _swapPending = false;
            }
        }

        /// <summary>
        /// CPU PRE-RENDERING OPTIMIZATION
        /// Performs initial rendering on CPU surface before Metal initializes
        /// This eliminates blank canvas during view initialization
        /// </summary>
        private void TryCpuPreRendering()
        {
            // Skip if already attempted
            if (_preRenderingAttempted)
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

        public event EventHandler<SKPaintMetalSurfaceEventArgs> PaintSurface;

        /// <summary>
        /// Raises the PaintSurface event.
        /// </summary>
        protected virtual void OnPaintSurface(SKPaintMetalSurfaceEventArgs e)
        {
            PaintSurface?.Invoke(this, e);
        }

        /// <summary>
        /// Forces the view to redraw its contents.
        /// </summary>
        public void InvalidateSurface()
        {
            SetNeedsDisplay();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Safety: Dispose pre-rendered image if never used
                _preRenderedImage?.Dispose();
                _preRenderedImage = null;

                lock (_textureSwapLock)
                {
                    if (_queuePin.IsAllocated)
                    {
                        _queuePin.Free();
                    }

                    _retainedTexture?.Dispose();
                    _pendingTexture?.Dispose();
                    _retainedTexture = null;
                    _pendingTexture = null;
                    _swapPending = false;
                }
                _context?.Dispose();
                _context = null;
            }
            base.Dispose(disposing);
        }
    }
}
