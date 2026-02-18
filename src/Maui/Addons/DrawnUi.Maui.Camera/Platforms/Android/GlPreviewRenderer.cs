#if ANDROID
using Android.Opengl;
using Android.Views;
using Java.Nio;
using SkiaSharp;

namespace DrawnUi.Camera
{
    /// <summary>
    /// GPU-based preview renderer that replaces the RenderScript/ImageReader preview pipeline.
    /// Uses a dedicated GL thread with EGL pbuffer surface, SurfaceTexture for zero-copy camera
    /// frames, and OES shader for GPU-native YUV→RGB conversion.
    /// Mirrors AndroidCaptureVideoEncoder's GPU thread pattern but without encoding.
    /// </summary>
    public class GlPreviewRenderer : IDisposable
    {
        // EGL resources (owned by this class)
        private EGLDisplay _eglDisplay = EGL14.EglNoDisplay;
        private EGLContext _eglContext = EGL14.EglNoContext;
        private EGLSurface _eglPbufferSurface = EGL14.EglNoSurface;

        // GPU camera frame provider (reuse existing class)
        private GpuCameraFrameProvider _gpuFrameProvider;

        // Readback FBO (render OES texture here, then glReadPixels)
        private int _readbackFbo;
        private int _readbackRbo;

        // Source FBO for blit path (renders OES at camera resolution, then blits to readback FBO)
        private int _sourceFbo;
        private int _sourceRbo;

        private int _previewWidth;
        private int _previewHeight;

        // Pre-allocated readback buffers (same pattern as GlPreviewScaler)
        private ByteBuffer _readbackBuffer;
        private byte[] _managedBuffer;
        private byte[] _flipRowBuffer; // Pre-allocated row buffer for Y-flip

        // GL thread (same pattern as AndroidCaptureVideoEncoder.GpuEncodingLoop)
        private System.Threading.Thread _glThread;
        private System.Threading.ManualResetEventSlim _frameSignal;
        private volatile bool _stopThread;
        private volatile bool _running;

        // Preview delivery — event consumer owns the SKImage lifecycle

        // Camera parameters
        private bool _isFrontCamera;
        private int _cameraWidth;
        private int _cameraHeight;
        private int _sensorOrientation;
        // glReadPixels returns bottom-to-top data. The SurfaceTexture transform for 90°/0° sensors
        // produces an FBO where GL y=0 = image bottom, so we need the CPU Y-flip.
        // For 270°/180° sensors (most front cameras) the transform produces the opposite Y ordering,
        // so glReadPixels already returns top-to-bottom — CPU Y-flip would invert it.
        private bool _needsYFlip;

        // State
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Dev flag: Use glBlitFramebuffer path (recording-style) instead of direct render + CPU Y-flip.
        /// Renders OES at camera resolution to a source FBO, then blits downscaled + Y-flipped to the
        /// readback FBO via glBlitFramebuffer. Eliminates CPU Y-flip and uses GPU bilinear filtering.
        /// Toggle to compare preview FPS between approaches.
        /// </summary>
        internal static bool UseBlitPreview { get; set; } = false;

        /// <summary>
        /// Fired when a new preview frame is ready.
        /// Parameters: SKImage (caller takes ownership), timestampNs from SurfaceTexture.
        /// </summary>
        public event Action<SKImage, long> PreviewFrameReady;

        public bool IsInitialized => _initialized;
        public int PreviewWidth => _previewWidth;
        public int PreviewHeight => _previewHeight;

        /// <summary>
        /// Initialize the GPU preview renderer. Creates EGL context, SurfaceTexture, and readback FBO.
        /// Must be called before Start(). The initialization runs on a temporary GL thread.
        /// </summary>
        public bool Initialize(int cameraWidth, int cameraHeight, int previewWidth, int previewHeight, bool isFrontCamera, int sensorOrientation = 90)
        {
            if (_initialized) return true;

            _cameraWidth = cameraWidth;
            _cameraHeight = cameraHeight;
            _previewWidth = previewWidth;
            _previewHeight = previewHeight;
            _isFrontCamera = isFrontCamera;
            _sensorOrientation = sensorOrientation;
            // 90° and 0° sensor orientations: SurfaceTexture transform puts image bottom at GL y=0 → need CPU flip.
            // 270° and 180° sensor orientations: transform reverses Y in the FBO → CPU flip would double-invert.
            _needsYFlip = (sensorOrientation == 90 || sensorOrientation == 0);

            // Initialize EGL and GL resources on a dedicated thread (EGL context is thread-local)
            bool initSuccess = false;
            var initDone = new System.Threading.ManualResetEventSlim(false);
            Exception initError = null;

            var initThread = new System.Threading.Thread(() =>
            {
                try
                {
                    SetupEgl();
                    MakeCurrent();

                    // Create GPU camera frame provider (creates SurfaceTexture + OES shader)
                    _gpuFrameProvider = new GpuCameraFrameProvider();
                    if (!_gpuFrameProvider.Initialize(cameraWidth, cameraHeight))
                    {
                        throw new InvalidOperationException("Failed to initialize GpuCameraFrameProvider");
                    }

                    // Create readback FBO at preview resolution
                    SetupReadbackFbo(previewWidth, previewHeight);

                    // Blit path: also create source FBO at camera resolution for OES rendering
                    if (UseBlitPreview)
                    {
                        SetupSourceFbo(cameraWidth, cameraHeight);
                    }

                    // Pre-allocate readback buffers
                    int bufferSize = previewWidth * previewHeight * 4;
                    _readbackBuffer = ByteBuffer.AllocateDirect(bufferSize);
                    _readbackBuffer.Order(ByteOrder.NativeOrder());
                    _managedBuffer = new byte[bufferSize];

                    // CPU Y-flip buffer only needed for non-blit (direct) path
                    if (!UseBlitPreview)
                    {
                        _flipRowBuffer = new byte[previewWidth * 4];
                    }

                    // Unbind EGL context (GL thread will bind it later)
                    UnbindCurrent();

                    initSuccess = true;
                }
                catch (Exception ex)
                {
                    initError = ex;
                    System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] Initialize error: {ex.Message}");
                }
                finally
                {
                    initDone.Set();
                }
            })
            {
                IsBackground = true,
                Name = "GlPreviewInit"
            };

            initThread.Start();
            initDone.Wait(5000); // 5s timeout for initialization

            if (!initSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] Initialization failed: {initError?.Message}");
                Cleanup();
                return false;
            }

            _initialized = true;
            System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] Initialized: camera={cameraWidth}x{cameraHeight} preview={previewWidth}x{previewHeight} front={isFrontCamera} sensor={sensorOrientation}° needsYFlip={_needsYFlip} blitPath={UseBlitPreview}");
            return true;
        }

        /// <summary>
        /// Get the Surface for use as Camera2 output target.
        /// </summary>
        public Surface GetCameraOutputSurface()
        {
            return _gpuFrameProvider?.GetCameraOutputSurface();
        }

        /// <summary>
        /// Start the GL preview thread. Begins processing and delivering camera frames.
        /// </summary>
        public void Start()
        {
            if (!_initialized || _running) return;

            // Re-register the SurfaceTexture frame listener — it stops firing after
            // Camera2 session cycles (removed from session during recording, re-added after)
            _gpuFrameProvider?.ResetFrameListener();

            _gpuFrameProvider?.Start();

            _stopThread = false;
            _frameSignal = new System.Threading.ManualResetEventSlim(false);

            // Subscribe to frame available events from SurfaceTexture
            if (_gpuFrameProvider?.Renderer != null)
            {
                _gpuFrameProvider.Renderer.OnFrameAvailable += OnCameraFrameAvailable;
            }

            _glThread = new System.Threading.Thread(GlPreviewLoop)
            {
                IsBackground = true,
                Name = "GlPreviewRenderer",
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            _glThread.Start();
            _running = true;
            System.Diagnostics.Debug.WriteLine("[GlPreviewRenderer] Started");
        }

        /// <summary>
        /// Stop the GL preview thread. Resources are kept for restart.
        /// </summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;

            // Unsubscribe from frame events
            if (_gpuFrameProvider?.Renderer != null)
            {
                _gpuFrameProvider.Renderer.OnFrameAvailable -= OnCameraFrameAvailable;
            }

            _gpuFrameProvider?.Stop();

            _stopThread = true;
            _frameSignal?.Set(); // Wake thread to exit
            _glThread?.Join(1000);
            _glThread = null;
            _frameSignal?.Dispose();
            _frameSignal = null;

            System.Diagnostics.Debug.WriteLine("[GlPreviewRenderer] Stopped");
        }

        /// <summary>
        /// Called on arbitrary camera thread when a new frame is available from SurfaceTexture.
        /// Just signals the GL thread — no heavy work here.
        /// </summary>
        private void OnCameraFrameAvailable(object sender, Android.Graphics.SurfaceTexture surfaceTexture)
        {
            if (!_running) return;
            _frameSignal?.Set();
        }

        /// <summary>
        /// GL preview loop — runs on dedicated background thread.
        /// Same pattern as AndroidCaptureVideoEncoder.GpuEncodingLoop.
        /// </summary>
        private void GlPreviewLoop()
        {
            while (!_stopThread)
            {
                try
                {
                    _frameSignal?.Wait(100); // Wait with timeout for frame signal
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (_stopThread) break;

                _frameSignal?.Reset();

                try
                {
                    ProcessPreviewFrame();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] GlPreviewLoop error: {ex.Message}");
                }
            }

            // Ensure EGL context is unbound when thread exits
            try { UnbindCurrent(); } catch { }
            System.Diagnostics.Debug.WriteLine("[GlPreviewRenderer] GL thread exited");
        }

        /// <summary>
        /// Process a single preview frame on the GL thread.
        /// MakeCurrent → UpdateTexImage → OES render → readback → SKImage.
        /// Dispatches to Direct or Blit path based on UseBlitPreview flag.
        /// </summary>
        private void ProcessPreviewFrame()
        {
            if (_eglDisplay == EGL14.EglNoDisplay || _eglContext == EGL14.EglNoContext)
                return;

            // 1. Make EGL context current
            MakeCurrent();

            // 2. Update SurfaceTexture (MUST be on EGL context thread)
            if (!_gpuFrameProvider.TryProcessFrameNoWait(out long timestampNs))
                return;

            // 3. Reset GL state to clean defaults
            GLES20.GlUseProgram(0);
            GLES20.GlDisable(GLES20.GlBlend);
            GLES20.GlDisable(GLES20.GlDepthTest);
            GLES20.GlDisable(GLES20.GlStencilTest);
            GLES20.GlDisable(GLES20.GlScissorTest);
            GLES20.GlColorMask(true, true, true, true);
            GLES20.GlActiveTexture(GLES20.GlTexture0);
            GLES20.GlBindTexture(GLES20.GlTexture2d, 0);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);
            GLES20.GlBindBuffer(GLES20.GlElementArrayBuffer, 0);
            while (GLES20.GlGetError() != GLES20.GlNoError) { }

            // 4. Dispatch to the appropriate processing path
            SKImage image = UseBlitPreview
                ? ProcessPreviewFrameBlit()
                : ProcessPreviewFrameDirect();

            // 5. Restore default FBO
            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0);

            // 6. Deliver preview — event consumer owns the SKImage lifecycle
            if (image != null)
            {
                // Decouple preview delivery from the GL loop (like the recording path does)
                // This prevents the UI update from throttling the camera/GL loop
                System.Threading.Tasks.Task.Run(() =>
                {
                    PreviewFrameReady?.Invoke(image, timestampNs);
                });
            }
        }

        /// <summary>
        /// DIRECT path (original): render OES at preview resolution → glReadPixels → CPU Y-flip → SKImage.
        /// </summary>
        private SKImage ProcessPreviewFrameDirect()
        {
            // Bind readback FBO, set viewport, clear
            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, _readbackFbo);
            GLES20.GlViewport(0, 0, _previewWidth, _previewHeight);
            GLES20.GlClear(GLES20.GlColorBufferBit);

            // Render OES texture — GPU driver does YUV→RGB automatically via samplerExternalOES
            _gpuFrameProvider.RenderToFramebuffer(_previewWidth, _previewHeight, _isFrontCamera);

            // Read pixels from the FBO (already at preview resolution)
            _readbackBuffer.Clear();
            GLES20.GlReadPixels(0, 0, _previewWidth, _previewHeight,
                GLES20.GlRgba, GLES20.GlUnsignedByte, _readbackBuffer);

            // Copy to managed buffer
            _readbackBuffer.Rewind();
            _readbackBuffer.Get(_managedBuffer, 0, _managedBuffer.Length);

            // glReadPixels returns bottom-to-top data. For 90°/0° sensor orientations the
            // SurfaceTexture transform leaves the image inverted in the FBO, so we must flip.
            // For 270°/180° sensors (most front cameras) the transform already yields the
            // correct Y ordering — flipping would invert the image (upside-down selfie bug).
            if (_needsYFlip)
            {
                int rowBytes = _previewWidth * 4;
                int halfHeight = _previewHeight / 2;
                for (int y = 0; y < halfHeight; y++)
                {
                    int topOffset = y * rowBytes;
                    int bottomOffset = (_previewHeight - 1 - y) * rowBytes;
                    System.Buffer.BlockCopy(_managedBuffer, topOffset, _flipRowBuffer, 0, rowBytes);
                    System.Buffer.BlockCopy(_managedBuffer, bottomOffset, _managedBuffer, topOffset, rowBytes);
                    System.Buffer.BlockCopy(_flipRowBuffer, 0, _managedBuffer, bottomOffset, rowBytes);
                }
            }

            var info = new SKImageInfo(_previewWidth, _previewHeight,
                SKColorType.Rgba8888, SKAlphaType.Premul);
            return SKImage.FromPixelCopy(info, _managedBuffer);
        }

        /// <summary>
        /// BLIT path (recording-style): render OES at camera resolution to source FBO →
        /// glBlitFramebuffer downscale + GPU Y-flip to readback FBO → glReadPixels small buffer.
        /// Mirrors AndroidCaptureVideoEncoder + GlPreviewScaler pipeline but without the encoder.
        /// Eliminates CPU Y-flip; GPU bilinear filtering handles downscale.
        /// </summary>
        private SKImage ProcessPreviewFrameBlit()
        {
            // 1. Render OES at camera resolution to source FBO
            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, _sourceFbo);
            GLES20.GlViewport(0, 0, _cameraWidth, _cameraHeight);
            GLES20.GlClear(GLES20.GlColorBufferBit);
            _gpuFrameProvider.RenderToFramebuffer(_cameraWidth, _cameraHeight, _isFrontCamera);

            // 2. Blit from source → readback with downscale + optional Y-flip.
            //    For 90°/0° sensors: invert dst Y (dstY0=height, dstY1=0) so glReadPixels
            //    returns top-to-bottom data — same trick as GlPreviewScaler.
            //    For 270°/180° sensors (most front cameras): SurfaceTexture transform already
            //    yields the correct Y ordering; blit without Y-flip to avoid double-inversion.
            int dstY0 = _needsYFlip ? _previewHeight : 0;
            int dstY1 = _needsYFlip ? 0 : _previewHeight;
            GLES30.GlBindFramebuffer(GLES30.GlReadFramebuffer, _sourceFbo);
            GLES30.GlBindFramebuffer(GLES30.GlDrawFramebuffer, _readbackFbo);
            GLES30.GlBlitFramebuffer(
                0, 0, _cameraWidth, _cameraHeight,          // src: full camera resolution
                0, dstY0, _previewWidth, dstY1,              // dst: preview resolution, conditionally Y-flipped
                GLES20.GlColorBufferBit,
                GLES20.GlLinear);                             // GPU bilinear filtering

            // 3. Read pixels from readback FBO (small preview buffer only)
            GLES30.GlBindFramebuffer(GLES30.GlReadFramebuffer, _readbackFbo);
            _readbackBuffer.Clear();
            GLES20.GlReadPixels(0, 0, _previewWidth, _previewHeight,
                GLES20.GlRgba, GLES20.GlUnsignedByte, _readbackBuffer);

            // 4. Copy to managed buffer — NO Y-flip needed, blit already flipped on GPU
            _readbackBuffer.Rewind();
            _readbackBuffer.Get(_managedBuffer, 0, _managedBuffer.Length);

            var info = new SKImageInfo(_previewWidth, _previewHeight,
                SKColorType.Rgba8888, SKAlphaType.Premul);
            return SKImage.FromPixelCopy(info, _managedBuffer);
        }

        #region EGL Setup

        /// <summary>
        /// Create EGL display, context, and a 1x1 pbuffer surface.
        /// Pattern from AndroidCaptureVideoEncoder.SetupEglForCodecSurface but using EGL_PBUFFER_BIT.
        /// </summary>
        private void SetupEgl()
        {
            _eglDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
            int[] version = new int[2];
            if (!EGL14.EglInitialize(_eglDisplay, version, 0, version, 1))
                throw new InvalidOperationException("EGL initialize failed");

            int[] attribList = {
                EGL14.EglRedSize, 8,
                EGL14.EglGreenSize, 8,
                EGL14.EglBlueSize, 8,
                EGL14.EglAlphaSize, 8,
                EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
                EGL14.EglSurfaceType, 0x0001, // EGL_PBUFFER_BIT
                EGL14.EglNone
            };
            EGLConfig[] configs = new EGLConfig[1];
            int[] numConfigs = new int[1];
            if (!EGL14.EglChooseConfig(_eglDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0))
                throw new InvalidOperationException("EGL choose config failed");

            int[] ctxAttribs = { EGL14.EglContextClientVersion, 2, EGL14.EglNone };
            _eglContext = EGL14.EglCreateContext(_eglDisplay, configs[0], EGL14.EglNoContext, ctxAttribs, 0);
            if (_eglContext == null || _eglContext == EGL14.EglNoContext)
                throw new InvalidOperationException("EGL create context failed");

            // 1x1 pbuffer — we only need a valid surface to make the context current.
            // All rendering goes to our FBO, not the pbuffer.
            int[] pbufferAttribs = { EGL14.EglWidth, 1, EGL14.EglHeight, 1, EGL14.EglNone };
            _eglPbufferSurface = EGL14.EglCreatePbufferSurface(_eglDisplay, configs[0], pbufferAttribs, 0);
            if (_eglPbufferSurface == null || _eglPbufferSurface == EGL14.EglNoSurface)
                throw new InvalidOperationException("EGL create pbuffer surface failed");

            System.Diagnostics.Debug.WriteLine("[GlPreviewRenderer] EGL setup complete");
        }

        private void MakeCurrent()
        {
            if (!EGL14.EglMakeCurrent(_eglDisplay, _eglPbufferSurface, _eglPbufferSurface, _eglContext))
                throw new InvalidOperationException("EGL make current failed");
        }

        private void UnbindCurrent()
        {
            EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
        }

        #endregion

        #region GL Resources

        /// <summary>
        /// Create FBO + renderbuffer at preview resolution for readback.
        /// Same pattern as GlPreviewScaler.Initialize().
        /// </summary>
        private void SetupReadbackFbo(int width, int height)
        {
            int[] fbo = new int[1];
            GLES20.GlGenFramebuffers(1, fbo, 0);
            _readbackFbo = fbo[0];

            int[] rbo = new int[1];
            GLES20.GlGenRenderbuffers(1, rbo, 0);
            _readbackRbo = rbo[0];

            GLES20.GlBindRenderbuffer(GLES20.GlRenderbuffer, _readbackRbo);
            GLES30.GlRenderbufferStorage(GLES20.GlRenderbuffer, GLES30.GlRgba8, width, height);

            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, _readbackFbo);
            GLES20.GlFramebufferRenderbuffer(GLES20.GlFramebuffer,
                GLES20.GlColorAttachment0, GLES20.GlRenderbuffer, _readbackRbo);

            int status = GLES20.GlCheckFramebufferStatus(GLES20.GlFramebuffer);
            if (status != GLES20.GlFramebufferComplete)
            {
                throw new InvalidOperationException($"Readback FBO incomplete: 0x{status:X}");
            }

            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0);
            GLES20.GlBindRenderbuffer(GLES20.GlRenderbuffer, 0);

            System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] Readback FBO created: {width}x{height}");
        }

        /// <summary>
        /// Create FBO + renderbuffer at camera resolution for OES rendering (blit path source).
        /// The OES texture is rendered here at full camera resolution, then glBlitFramebuffer
        /// downscales + Y-flips to the readback FBO.
        /// </summary>
        private void SetupSourceFbo(int width, int height)
        {
            int[] fbo = new int[1];
            GLES20.GlGenFramebuffers(1, fbo, 0);
            _sourceFbo = fbo[0];

            int[] rbo = new int[1];
            GLES20.GlGenRenderbuffers(1, rbo, 0);
            _sourceRbo = rbo[0];

            GLES20.GlBindRenderbuffer(GLES20.GlRenderbuffer, _sourceRbo);
            GLES30.GlRenderbufferStorage(GLES20.GlRenderbuffer, GLES30.GlRgba8, width, height);

            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, _sourceFbo);
            GLES20.GlFramebufferRenderbuffer(GLES20.GlFramebuffer,
                GLES20.GlColorAttachment0, GLES20.GlRenderbuffer, _sourceRbo);

            int status = GLES20.GlCheckFramebufferStatus(GLES20.GlFramebuffer);
            if (status != GLES20.GlFramebufferComplete)
            {
                throw new InvalidOperationException($"Source FBO incomplete: 0x{status:X}");
            }

            GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0);
            GLES20.GlBindRenderbuffer(GLES20.GlRenderbuffer, 0);

            System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] Source FBO created: {width}x{height}");
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            _gpuFrameProvider?.Dispose();
            _gpuFrameProvider = null;

            _readbackBuffer?.Dispose();
            _readbackBuffer = null;
            _managedBuffer = null;
            _flipRowBuffer = null;

            // GL resources must be cleaned up with EGL context current
            // If we can't make current, skip GL cleanup (context may already be destroyed)
            bool canCleanupGl = false;
            try
            {
                if (_eglDisplay != EGL14.EglNoDisplay && _eglContext != EGL14.EglNoContext && _eglPbufferSurface != EGL14.EglNoSurface)
                {
                    MakeCurrent();
                    canCleanupGl = true;
                }
            }
            catch { }

            if (canCleanupGl)
            {
                try
                {
                    if (_readbackFbo != 0)
                    {
                        GLES20.GlDeleteFramebuffers(1, new int[] { _readbackFbo }, 0);
                        _readbackFbo = 0;
                    }
                    if (_readbackRbo != 0)
                    {
                        GLES20.GlDeleteRenderbuffers(1, new int[] { _readbackRbo }, 0);
                        _readbackRbo = 0;
                    }
                    if (_sourceFbo != 0)
                    {
                        GLES20.GlDeleteFramebuffers(1, new int[] { _sourceFbo }, 0);
                        _sourceFbo = 0;
                    }
                    if (_sourceRbo != 0)
                    {
                        GLES20.GlDeleteRenderbuffers(1, new int[] { _sourceRbo }, 0);
                        _sourceRbo = 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] GL cleanup error: {ex.Message}");
                }
            }

            // Destroy EGL resources
            try
            {
                if (_eglDisplay != EGL14.EglNoDisplay)
                {
                    EGL14.EglMakeCurrent(_eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);

                    if (_eglPbufferSurface != EGL14.EglNoSurface)
                    {
                        EGL14.EglDestroySurface(_eglDisplay, _eglPbufferSurface);
                        _eglPbufferSurface = EGL14.EglNoSurface;
                    }
                    if (_eglContext != EGL14.EglNoContext)
                    {
                        EGL14.EglDestroyContext(_eglDisplay, _eglContext);
                        _eglContext = EGL14.EglNoContext;
                    }
                    EGL14.EglTerminate(_eglDisplay);
                    _eglDisplay = EGL14.EglNoDisplay;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlPreviewRenderer] EGL cleanup error: {ex.Message}");
            }

            _initialized = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            Cleanup();

            System.Diagnostics.Debug.WriteLine("[GlPreviewRenderer] Disposed");
        }

        #endregion
    }
}
#endif
