#if ANDROID
using Android.Graphics;
using Android.Views;
using System;
using System.Threading;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Coordinates camera frames from SurfaceTexture with the video encoder.
    /// Handles thread synchronization between camera callback thread and encoder GL thread.
    /// </summary>
    public class GpuCameraFrameProvider : IDisposable
    {
        private CameraSurfaceTextureRenderer _renderer;
        private readonly object _frameLock = new();
        private volatile bool _frameAvailable;
        private long _frameTimestampNs;
        private bool _disposed;
        private bool _running;

        public CameraSurfaceTextureRenderer Renderer => _renderer;
        public bool IsRunning => _running;

        /// <summary>
        /// Check if GPU camera path is supported on this device.
        /// </summary>
        public static bool IsSupported()
        {
            // Require API 26+ for reliable SurfaceTexture behavior
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O)
            {
                System.Diagnostics.Debug.WriteLine("[GpuCameraFrameProvider] API < 26, not supported");
                return false;
            }

            // Check for OES_EGL_image_external extension
            // This is checked during shader compilation, so we assume it's available on API 26+
            return true;
        }

        /// <summary>
        /// Initialize the GPU camera frame provider.
        /// Must be called on GL thread with valid EGL context.
        /// </summary>
        /// <param name="width">Frame width</param>
        /// <param name="height">Frame height</param>
        public bool Initialize(int width, int height)
        {
            if (_renderer != null)
            {
                _renderer.Dispose();
            }

            _renderer = new CameraSurfaceTextureRenderer();
            if (!_renderer.Initialize(width, height))
            {
                System.Diagnostics.Debug.WriteLine("[GpuCameraFrameProvider] Failed to initialize renderer");
                _renderer = null;
                return false;
            }

            // Subscribe to frame available events
            _renderer.OnFrameAvailable += OnFrameAvailable;

            System.Diagnostics.Debug.WriteLine($"[GpuCameraFrameProvider] Initialized: {width}x{height}");
            return true;
        }

        /// <summary>
        /// Get the Surface for use as Camera2 output target.
        /// </summary>
        public Surface GetCameraOutputSurface()
        {
            return _renderer?.GetCameraSurface();
        }

        /// <summary>
        /// Start accepting camera frames.
        /// </summary>
        public void Start()
        {
            _running = true;
            _frameAvailable = false;
            System.Diagnostics.Debug.WriteLine("[GpuCameraFrameProvider] Started");
        }

        /// <summary>
        /// Stop accepting camera frames.
        /// </summary>
        public void Stop()
        {
            _running = false;
            lock (_frameLock)
            {
                _frameAvailable = false;
                Monitor.PulseAll(_frameLock);
            }
            System.Diagnostics.Debug.WriteLine("[GpuCameraFrameProvider] Stopped");
        }

        /// <summary>
        /// Called when a new camera frame is available.
        /// This is called on an arbitrary thread by SurfaceTexture.
        /// </summary>
        private void OnFrameAvailable(object sender, SurfaceTexture surfaceTexture)
        {
            if (!_running) return;

            lock (_frameLock)
            {
                _frameAvailable = true;
                _frameTimestampNs = surfaceTexture.Timestamp;
                Monitor.PulseAll(_frameLock);
            }
        }

        /// <summary>
        /// Try to process the next available camera frame.
        /// Must be called on GL thread with valid EGL context.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for a frame</param>
        /// <param name="timestampNs">Output: frame timestamp in nanoseconds</param>
        /// <returns>True if a frame was processed</returns>
        public bool TryProcessFrame(TimeSpan timeout, out long timestampNs)
        {
            timestampNs = 0;
            if (!_running || _renderer == null)
            {
                return false;
            }

            lock (_frameLock)
            {
                if (!_frameAvailable)
                {
                    Monitor.Wait(_frameLock, timeout);
                }

                if (!_frameAvailable || !_running)
                {
                    return false;
                }

                _frameAvailable = false;
                timestampNs = _frameTimestampNs;
            }

            // Update texture on GL thread
            // CRITICAL: UpdateTexImage() must be called on the same thread that created the EGL context
            _renderer.UpdateTexImage();

            return true;
        }

        /// <summary>
        /// Try to process a frame without waiting.
        /// Must be called on GL thread with valid EGL context.
        /// </summary>
        /// <param name="timestampNs">Output: frame timestamp in nanoseconds</param>
        /// <returns>True if a frame was processed</returns>
        public bool TryProcessFrameNoWait(out long timestampNs)
        {
            return TryProcessFrame(TimeSpan.Zero, out timestampNs);
        }

        /// <summary>
        /// Render the current camera frame to the current framebuffer.
        /// Must be called after TryProcessFrame() returns true, on GL thread.
        /// </summary>
        /// <param name="viewportWidth">Viewport width</param>
        /// <param name="viewportHeight">Viewport height</param>
        /// <param name="mirror">True to mirror horizontally (for front camera)</param>
        public void RenderToFramebuffer(int viewportWidth, int viewportHeight, bool mirror)
        {
            _renderer?.RenderToFramebuffer(viewportWidth, viewportHeight, mirror);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();

            if (_renderer != null)
            {
                _renderer.OnFrameAvailable -= OnFrameAvailable;
                _renderer.Dispose();
                _renderer = null;
            }

            System.Diagnostics.Debug.WriteLine("[GpuCameraFrameProvider] Disposed");
        }
    }
}
#endif
