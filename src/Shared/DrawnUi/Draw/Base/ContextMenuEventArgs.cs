namespace DrawnUi.Draw
{
    /// <summary>
    /// Arguments of <see cref="SkiaControl.ContextMenu"/>: a context-menu request over the control — a right click, a
    /// long press on touch (browsers raise it), or the keyboard Menu key. Web heads only (Blazor, Wasm); other
    /// platforms never raise it. The handler returns true to take the request: the browser's own canvas menu
    /// ("Save image as…") is then suppressed; false and the browser menu shows as usual.
    /// </summary>
    public class ContextMenuEventArgs : ControlTappedEventArgs
    {
        public ContextMenuEventArgs(object control, SkiaGesturesParameters args, GestureEventProcessingInfo info)
            : base(control, args, info)
        {
        }

        /// <summary>
        /// Where the request came from: Mouse (right click), Touch / Pen (long press), Keyboard (Menu key).
        /// </summary>
        public ContextMenuSource Source { get; set; }

        /// <summary>
        /// Point inside the control, in points (DIPs), relative to its top-left.
        /// </summary>
        public SKPoint Local { get; set; }
    }

    /// <summary>
    /// Origin of a <see cref="SkiaControl.ContextMenu"/> request.
    /// </summary>
    public enum ContextMenuSource
    {
        Mouse,
        Touch,
        Keyboard
    }
}
