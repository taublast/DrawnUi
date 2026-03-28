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

        public void SetAntialias(bool value) =>
            SkiaNativeMethods.PaintSetAntialias(Handle, value);

        public void SetStyle(SKPaintStyle style) =>
            SkiaNativeMethods.PaintSetStyle(Handle, style);

        public void SetColor(uint color) =>
            SkiaNativeMethods.PaintSetColor(Handle, color);

        public void SetStrokeWidth(float width) =>
            SkiaNativeMethods.PaintSetStrokeWidth(Handle, width);

        public void SetStrokeCap(SKStrokeCap cap) =>
            SkiaNativeMethods.PaintSetStrokeCap(Handle, cap);

        public void SetStrokeJoin(SKStrokeJoin join) =>
            SkiaNativeMethods.PaintSetStrokeJoin(Handle, join);

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