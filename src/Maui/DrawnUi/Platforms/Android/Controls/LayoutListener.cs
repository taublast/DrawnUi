using View = Android.Views.View;

namespace DrawnUi.Controls
{
    public class LayoutListener<T> : Java.Lang.Object, View.IOnLayoutChangeListener
    {
        public T Control;

        public global::Android.Views.View View;

        public LayoutListener(global::Android.Views.View view, T control)
        {
            View = view;
            Control = control;
            View?.AddOnLayoutChangeListener(this);
        }

        public void Release()
        {
            View?.RemoveOnLayoutChangeListener(this);
            View = null;
            Control = default;
        }

        public virtual void OnLayoutChange(View v, int left, int top, int right, int bottom, int oldLeft, int oldTop, int oldRight,
            int oldBottom)
        {
 
        }
    }
}
