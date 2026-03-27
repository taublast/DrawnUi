#if ANDROID
using SkiaSharp;
using System.Runtime.InteropServices;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Pre-allocates two pinned CPU pixel buffers for double-buffered SKImage delivery.
    ///
    /// Usage pattern (zero intermediate copies):
    ///   1. GetWriteBuffer() — get the current slot's byte[] to fill directly (e.g. via Marshal.Copy from PBO)
    ///   2. CommitAndGetImage() — wrap the filled slot in an SKImage and advance to the other slot
    ///
    /// SKImage.FromPixels with a non-null releaseProc uses SkImage::MakeFromRaster (borrow semantics):
    /// the pixel data is NOT copied — the SKImage holds a pointer to the pinned buffer.
    /// Disposing the returned SKImage releases only the lightweight SKImage handle; the pinned
    /// buffer stays alive and will be reused for a future frame.
    ///
    /// Two-slot safety: at most one frame is held by the consumer (current Preview). We write
    /// slot N, deliver it, then write slot 1-N. By the time we return to slot N (two frames later)
    /// the consumer has replaced Preview and disposed the old CapturedImage — slot N is free.
    /// </summary>
    internal sealed class SkiaCpuImagePool : IDisposable
    {
        private readonly GCHandle[] _handles = new GCHandle[2];
        private readonly byte[][] _pixels = new byte[2][];
        private readonly SKImageInfo _info;
        private int _writeSlot;
        private readonly int _byteCount;
        private bool _disposed;

        public int Width => _info.Width;
        public int Height => _info.Height;

        // Non-null release proc → Skia borrows our memory (SkImage::MakeFromRaster) instead of copying it.
        // Called when the SKImage is destroyed; we do nothing because the pool owns and manages the buffer.
        private static readonly SKImageRasterReleaseDelegate _noOpRelease = (addr, ctx) => { };

        public SkiaCpuImagePool(int width, int height)
        {
            _info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _byteCount = width * height * 4;
            for (int i = 0; i < 2; i++)
            {
                _pixels[i] = new byte[_byteCount];
                _handles[i] = GCHandle.Alloc(_pixels[i], GCHandleType.Pinned);
            }
        }

        /// <summary>
        /// Returns the current write slot's byte[] for direct filling (e.g. Marshal.Copy from a mapped PBO).
        /// Must be followed by CommitAndGetImage() on the same call site before any concurrent access.
        /// </summary>
        public byte[] GetWriteBuffer() => _pixels[_writeSlot];

        /// <summary>
        /// Wraps the current write slot in an SKImage (no pixel copy — borrows pinned memory),
        /// then advances to the other slot. Caller must dispose the returned SKImage when done.
        /// </summary>
        public SKImage CommitAndGetImage()
        {
            int slot = _writeSlot;
            using var pixmap = new SKPixmap(_info, _handles[slot].AddrOfPinnedObject(), _info.Width * 4);
            var image = SKImage.FromPixels(pixmap, _noOpRelease, null);
            _writeSlot = 1 - slot;
            return image;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < 2; i++)
            {
                if (_handles[i].IsAllocated)
                    _handles[i].Free();
            }
        }
    }
}
#endif
