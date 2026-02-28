#if ANDROID
using Android.Opengl;
using Java.Nio;
using SkiaSharp;

namespace DrawnUi.Camera
{
    /// <summary>
    /// GPU-native preview downscaler using GLES 3.0 glBlitFramebuffer.
    /// Mirrors Apple's MetalPreviewScaler pattern for Android.
    ///
    /// Reads from the encoder's default FBO (0), blits with bilinear filtering
    /// to a smaller FBO, then reads back only the small buffer via glReadPixels.
    /// This avoids the expensive full-resolution GPU→CPU readback that Skia's
    /// Snapshot()+DrawImage approach triggers.
    ///
    /// Performance: ~3-7ms per preview frame (vs 15-40ms with naive Skia readback).
    /// </summary>
    public class GlPreviewScaler : IDisposable
    {
        private int _previewFbo;
        private int _previewRbo;

        private int _inputWidth, _inputHeight;
        private int _outputWidth, _outputHeight;

        // Pre-allocated readback buffers (no per-frame allocations)
        private ByteBuffer _readbackBuffer;
        private byte[] _managedBuffer;

        private bool _isInitialized;
        private bool _isDisposed;

        public bool IsInitialized => _isInitialized;
        public int OutputWidth => _outputWidth;
        public int OutputHeight => _outputHeight;

        /// <summary>
        /// Initialize GL resources. Must be called on GL thread with active EGL context.
        /// </summary>
        public bool Initialize(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
        {
            if (_isInitialized)
                return true;

            try
            {
                _inputWidth = inputWidth;
                _inputHeight = inputHeight;
                _outputWidth = outputWidth;
                _outputHeight = outputHeight;

                // Create preview FBO
                int[] fbo = new int[1];
                GLES20.GlGenFramebuffers(1, fbo, 0);
                _previewFbo = fbo[0];

                // Create renderbuffer (optimized for glReadPixels, no texture sampling needed)
                int[] rbo = new int[1];
                GLES20.GlGenRenderbuffers(1, rbo, 0);
                _previewRbo = rbo[0];

                GLES20.GlBindRenderbuffer(GLES20.GlRenderbuffer, _previewRbo);
                GLES30.GlRenderbufferStorage(GLES20.GlRenderbuffer, GLES30.GlRgba8, outputWidth, outputHeight);

                // Attach renderbuffer to FBO
                GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, _previewFbo);
                GLES20.GlFramebufferRenderbuffer(GLES20.GlFramebuffer,
                    GLES20.GlColorAttachment0, GLES20.GlRenderbuffer, _previewRbo);

                int status = GLES20.GlCheckFramebufferStatus(GLES20.GlFramebuffer);
                if (status != GLES20.GlFramebufferComplete)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlPreviewScaler] FBO incomplete: 0x{status:X}");
                    Dispose();
                    return false;
                }

                // Pre-allocate readback buffers
                int bufferSize = outputWidth * outputHeight * 4;
                _readbackBuffer = ByteBuffer.AllocateDirect(bufferSize);
                _readbackBuffer.Order(ByteOrder.NativeOrder());
                _managedBuffer = new byte[bufferSize];

                // Restore default FBO
                GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0);
                GLES20.GlBindRenderbuffer(GLES20.GlRenderbuffer, 0);

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[GlPreviewScaler] Initialized: {inputWidth}x{inputHeight} → {outputWidth}x{outputHeight}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlPreviewScaler] Initialize error: {ex.Message}");
                Dispose();
                return false;
            }
        }

        /// <summary>
        /// Downscale the current encoder framebuffer (FBO 0) to preview size and read back as SKImage.
        /// Must be called on GL thread after canvas.Flush()+grContext.Flush(), before EglSwapBuffers.
        /// Returns null on failure.
        /// </summary>
        public SKImage ScaleAndReadback(int sourceFbo = 0)
        {
            if (!_isInitialized)
                return null;

            try
            {
                // 1. Bind encoder FBO (0) as read source
                GLES30.GlBindFramebuffer(GLES30.GlReadFramebuffer, sourceFbo);

                // 2. Bind preview FBO as draw target
                GLES30.GlBindFramebuffer(GLES30.GlDrawFramebuffer, _previewFbo);

                // 3. Blit with bilinear filtering + Y-flip
                // Inverting dst Y coordinates (dstY0=height, dstY1=0) flips vertically,
                // correcting GL's bottom-up pixel order so glReadPixels returns
                // top-to-bottom data matching SKImage expectations.
                GLES30.GlBlitFramebuffer(
                    0, 0, _inputWidth, _inputHeight,
                    0, _outputHeight, _outputWidth, 0,
                    GLES20.GlColorBufferBit,
                    GLES20.GlLinear);

                // 4. Read pixels from preview FBO
                GLES30.GlBindFramebuffer(GLES30.GlReadFramebuffer, _previewFbo);
                _readbackBuffer.Clear();
                GLES20.GlReadPixels(0, 0, _outputWidth, _outputHeight,
                    GLES20.GlRgba, GLES20.GlUnsignedByte, _readbackBuffer);

                // 5. Copy to managed array
                _readbackBuffer.Rewind();
                _readbackBuffer.Get(_managedBuffer, 0, _managedBuffer.Length);

                // 6. Create SKImage (copies the small buffer, ~900KB for 640x360)
                var info = new SKImageInfo(_outputWidth, _outputHeight,
                    SKColorType.Rgba8888, SKAlphaType.Premul);
                var image = SKImage.FromPixelCopy(info, _managedBuffer);

                // 7. Restore default FBO for subsequent EglSwapBuffers
                GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0);

                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlPreviewScaler] ScaleAndReadback error: {ex.Message}");
                try { GLES20.GlBindFramebuffer(GLES20.GlFramebuffer, 0); } catch { }
                return null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;

            try
            {
                if (_previewFbo != 0)
                {
                    GLES20.GlDeleteFramebuffers(1, new int[] { _previewFbo }, 0);
                    _previewFbo = 0;
                }

                if (_previewRbo != 0)
                {
                    GLES20.GlDeleteRenderbuffers(1, new int[] { _previewRbo }, 0);
                    _previewRbo = 0;
                }

                _readbackBuffer?.Dispose();
                _readbackBuffer = null;
                _managedBuffer = null;
                _isInitialized = false;

                System.Diagnostics.Debug.WriteLine("[GlPreviewScaler] Disposed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlPreviewScaler] Dispose error: {ex.Message}");
            }
        }
    }
}
#endif
