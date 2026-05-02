using AppoMobi.Gestures;

namespace DrawnUi.Views
{
    public partial class Canvas : DrawnView, IGestureListener
    {
        public virtual bool SignalInput(ISkiaGestureListener listener, TouchActionResult gestureType) => true;

        public virtual bool Focus() => true;

        public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action) { }

        public bool InputTransparent => false;
    }
}
