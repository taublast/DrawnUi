namespace Sandbox
{
    internal sealed class SkiaPath : IDisposable
    {
        public IntPtr Handle { get; private set; }
        private bool _disposed;

        public SkiaPath()
        {
            Handle = SkiaNativeMethods.PathNew();
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate Skia path.");
        }

        public void MoveTo(float x, float y)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PathMoveTo(Handle, x, y);
        }

        public void LineTo(float x, float y)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PathLineTo(Handle, x, y);
        }

        public void QuadTo(float x0, float y0, float x1, float y1)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PathQuadTo(Handle, x0, y0, x1, y1);
        }

        public void CubicTo(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PathCubicTo(Handle, x0, y0, x1, y1, x2, y2);
        }

        public void Reset()
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PathReset(Handle);
        }

        private void EnsureNotDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (Handle != IntPtr.Zero)
            {
                SkiaNativeMethods.PathDelete(Handle);
                Handle = IntPtr.Zero;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~SkiaPath()
        {
            if (!_disposed)
            {
                SkiaNativeMethods.PathDelete(Handle);
            }
        }
    }
}