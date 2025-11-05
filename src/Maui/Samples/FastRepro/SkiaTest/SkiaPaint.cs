namespace Sandbox
{
    internal sealed class SkiaPaint : IDisposable
    {
        public IntPtr Handle { get; private set; }
        private bool _disposed;

        public SkiaPaint()
        {
            Handle = SkiaNativeMethods.PaintNew();
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate Skia paint.");
        }

        public void SetAntialias(bool value)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PaintSetAntialias(Handle, value);
        }

        public void SetStyle(SKPaintStyle style)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PaintSetStyle(Handle, style);
        }

        public void SetColor(uint color)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PaintSetColor(Handle, color);
        }

        public void SetStrokeWidth(float width)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PaintSetStrokeWidth(Handle, width);
        }

        public void SetStrokeCap(SKStrokeCap cap)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PaintSetStrokeCap(Handle, cap);
        }

        public void SetStrokeJoin(SKStrokeJoin join)
        {
            EnsureNotDisposed();
            SkiaNativeMethods.PaintSetStrokeJoin(Handle, join);
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
                SkiaNativeMethods.PaintDelete(Handle);
                Handle = IntPtr.Zero;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~SkiaPaint()
        {
            if (!_disposed)
            {
                SkiaNativeMethods.PaintDelete(Handle);
            }
        }
    }
}