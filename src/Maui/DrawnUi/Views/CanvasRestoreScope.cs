namespace DrawnUi.Views
{
    /// <summary>
    /// Zero-allocation canvas save/restore scope for use in hot rendering paths.
    /// Struct equivalent of SKAutoCanvasRestoreFixed — no heap allocation in using statements.
    /// </summary>
    public struct CanvasRestoreScope : IDisposable
    {
        private SKCanvas _canvas;
        private readonly int _saveCount;

        public CanvasRestoreScope(SKCanvas canvas)
        {
            _canvas = canvas;
            _saveCount = canvas?.Save() ?? 0;
        }

        public void Dispose()
        {
            if (_canvas != null && _canvas.Handle != IntPtr.Zero)
            {
                _canvas.RestoreToCount(_saveCount);
                _canvas = null;
            }
        }
    }
}
