#if ANDROID
using Android.Graphics;
using Android.Opengl;
using Android.Views;
using System;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Manages SurfaceTexture for camera output and renders it using OES texture shader.
    /// Provides zero-copy GPU path for camera frames.
    /// </summary>
    public class CameraSurfaceTextureRenderer : IDisposable
    {
        private SurfaceTexture _surfaceTexture;
        private Surface _cameraSurface;
        private int _oesTextureId;
        private OesTextureShader _shader;
        private float[] _transformMatrix = new float[16];

        private int _width;
        private int _height;
        private bool _initialized;
        private bool _disposed;
        private int _creationThreadId;
        private bool _needsReattach = false;

        public int Width => _width;
        public int Height => _height;
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Event fired when a new frame is available from the camera.
        /// </summary>
        public event EventHandler<SurfaceTexture> OnFrameAvailable;

        /// <summary>
        /// Initialize the renderer. Must be called on GL thread with valid EGL context.
        /// </summary>
        /// <param name="width">Frame width</param>
        /// <param name="height">Frame height</param>
        public bool Initialize(int width, int height)
        {
            if (_initialized) return true;

            try
            {
                _width = width;
                _height = height;

                // Create OES texture
                int[] textures = new int[1];
                GLES20.GlGenTextures(1, textures, 0);
                _oesTextureId = textures[0];

                GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, _oesTextureId);
                GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter, GLES20.GlLinear);
                GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter, GLES20.GlLinear);
                GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
                GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
                GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);

                // Create SurfaceTexture from OES texture
                _surfaceTexture = new SurfaceTexture(_oesTextureId);
                _surfaceTexture.SetDefaultBufferSize(width, height);

                // Set up frame available listener with a Handler
                // CRITICAL: Must provide a Handler with a Looper, otherwise callbacks won't be delivered
                // if the current thread doesn't have a Looper (like the EGL thread)
                var handler = new Android.OS.Handler(Android.OS.Looper.MainLooper);
                _surfaceTexture.SetOnFrameAvailableListener(new FrameAvailableListener(this), handler);
                System.Diagnostics.Debug.WriteLine("[CameraSurfaceTextureRenderer] Frame listener set with MainLooper handler");

                // Create Surface for Camera2 output target
                _cameraSurface = new Surface(_surfaceTexture);

                // Initialize shader
                _shader = new OesTextureShader();
                if (!_shader.Initialize())
                {
                    System.Diagnostics.Debug.WriteLine("[CameraSurfaceTextureRenderer] Failed to initialize shader");
                    Dispose();
                    return false;
                }

                // Initialize transform matrix to identity
                Android.Opengl.Matrix.SetIdentityM(_transformMatrix, 0);

                _initialized = true;
                _creationThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                System.Diagnostics.Debug.WriteLine($"[CameraSurfaceTextureRenderer] Initialized: {width}x{height}, texture={_oesTextureId}, threadId={_creationThreadId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CameraSurfaceTextureRenderer] Initialize error: {ex.Message}");
                Dispose();
                return false;
            }
        }

        /// <summary>
        /// Get the Surface for use as Camera2 output target.
        /// </summary>
        public Surface GetCameraSurface()
        {
            return _cameraSurface;
        }

        /// <summary>
        /// Update the texture with the latest camera frame.
        /// Must be called on GL thread with valid EGL context.
        /// </summary>
        private long _lastTimestamp = 0;
        private int _updateCount = 0;

        public void UpdateTexImage()
        {
            if (!_initialized || _surfaceTexture == null) return;

            int currentThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            // CRITICAL FIX: SurfaceTexture must be used from the same thread that owns its GL context.
            // If we're on a different thread, we need to detach from old context and reattach to current.
            // This happens when SurfaceTexture is created during initialization on one thread,
            // but UpdateTexImage is called from OnFrameAvailable callback on a different thread.
            if (currentThreadId != _creationThreadId && !_needsReattach)
            {
                System.Diagnostics.Debug.WriteLine($"[SurfaceTexture] Thread mismatch detected! Created={_creationThreadId}, Current={currentThreadId}. Will reattach to current context.");
                _needsReattach = true;
            }

            try
            {
                // Reattach SurfaceTexture to current EGL context if needed (API 26+ only)
                if (_needsReattach)
                {
                    System.Diagnostics.Debug.WriteLine($"[SurfaceTexture] Reattaching to current EGL context on thread {currentThreadId}");
                    _surfaceTexture.DetachFromGLContext();
                    _surfaceTexture.AttachToGLContext(_oesTextureId);
                    _creationThreadId = currentThreadId;
                    _needsReattach = false;
                    System.Diagnostics.Debug.WriteLine($"[SurfaceTexture] ✓ Reattached successfully");
                }

                long beforeTs = _surfaceTexture.Timestamp;
                _surfaceTexture.UpdateTexImage();
                long afterTs = _surfaceTexture.Timestamp;
                _surfaceTexture.GetTransformMatrix(_transformMatrix);

                _updateCount++;
                _lastTimestamp = afterTs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CameraSurfaceTextureRenderer] UpdateTexImage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current camera frame timestamp in nanoseconds.
        /// </summary>
        public long GetTimestamp()
        {
            if (!_initialized || _surfaceTexture == null) return 0;
            return _surfaceTexture.Timestamp;
        }

        /// <summary>
        /// Render the camera texture to the current framebuffer.
        /// Must be called on GL thread with valid EGL context after UpdateTexImage().
        /// </summary>
        /// <param name="viewportWidth">Viewport width</param>
        /// <param name="viewportHeight">Viewport height</param>
        /// <param name="mirror">True to mirror horizontally (for front camera)</param>
        public void RenderToFramebuffer(int viewportWidth, int viewportHeight, bool mirror)
        {
            if (!_initialized || _shader == null) return;

            GLES20.GlViewport(0, 0, viewportWidth, viewportHeight);
            _shader.Draw(_oesTextureId, _transformMatrix, mirror);
        }

        /// <summary>
        /// Internal listener for frame available events.
        /// </summary>
        private class FrameAvailableListener : Java.Lang.Object, SurfaceTexture.IOnFrameAvailableListener
        {
            private readonly CameraSurfaceTextureRenderer _renderer;

            public FrameAvailableListener(CameraSurfaceTextureRenderer renderer)
            {
                _renderer = renderer;
            }

            public void OnFrameAvailable(SurfaceTexture surfaceTexture)
            {
                _renderer.OnFrameAvailable?.Invoke(_renderer, surfaceTexture);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _shader?.Dispose();
            _shader = null;

            _cameraSurface?.Release();
            _cameraSurface?.Dispose();
            _cameraSurface = null;

            _surfaceTexture?.Release();
            _surfaceTexture?.Dispose();
            _surfaceTexture = null;

            if (_oesTextureId != 0)
            {
                int[] textures = { _oesTextureId };
                GLES20.GlDeleteTextures(1, textures, 0);
                _oesTextureId = 0;
            }

            _initialized = false;
            System.Diagnostics.Debug.WriteLine("[CameraSurfaceTextureRenderer] Disposed");
        }
    }
}
#endif
