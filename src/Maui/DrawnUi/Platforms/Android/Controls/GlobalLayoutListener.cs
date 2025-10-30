using Android.Views;

namespace DrawnUi.Controls
{
    public class GlobalLayoutListener<T> : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    {
        public T Control;

        public global::Android.Views.View View;

        public GlobalLayoutListener(global::Android.Views.View view, T control)
        {
            if (control == null)
            {
                throw new InvalidOperationException("Control cannot be null");
            }
            View = view;
            Control = control;
            View.ViewTreeObserver?.AddOnGlobalLayoutListener(this);
        }

        public void Release()
        {
            View?.ViewTreeObserver?.RemoveOnGlobalLayoutListener(this);
            View = null;
            Control = default;
        }

        public virtual void OnGlobalLayout()
        {
 
        }
    }
}
