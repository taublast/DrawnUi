using Android.Util;
using Android.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;

namespace DrawnUi.Controls;

public class MyLayoutListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
{
    private DrawnUiBasePage _control;

    private double _lastKeyboardSize = -1;

    private global::Android.Views.View _view;

    public MyLayoutListener(global::Android.Views.View view, DrawnUiBasePage control)
    {
        _view = view;
        _control = control;
        _view.ViewTreeObserver.AddOnGlobalLayoutListener(this);
    }

    public void Release()
    {
        _view.ViewTreeObserver.RemoveOnGlobalLayoutListener(this);
        _view = null;
        _control = null;
    }

    public void OnGlobalLayout()
    {
        var keyboardSize = KeyboardSize(_view);

        if (_control == null) return; //check for being disposed.. all fine

        if (keyboardSize != _lastKeyboardSize)
        {
            _lastKeyboardSize = keyboardSize;
            _control.KeyboardResized(keyboardSize);
        }
    }

    private double KeyboardSize(global::Android.Views.View rootView)
    {
        
        try
        {
            if (rootView == null)
                return 0;

            var rectangle = new Android.Graphics.Rect();
            //var decorView = Platform.CurrentActivity.Window.DecorView;
            //if (decorView is not null)
            //{
            //    decorView.GetWindowVisibleDisplayFrame(rectangle);
            //    Trace.WriteLine($"[GetWindowVisibleDisplayFrame] Decor Height {rectangle.Height()}");
            //    if (rectangle.Bottom > 0)
            //    {
            //        var stop = 1;
            //    }
            //}
            rootView.GetWindowVisibleDisplayFrame(rectangle);
            var ret = 0f;
            if (rectangle.Height() > 0)
            {
                DisplayMetrics dm = rootView.Resources.DisplayMetrics;
                var heightDiff = rootView.Bottom - rectangle.Bottom;
                ret = heightDiff / dm.Density;
            }

            return ret;
        }
        catch
        {
            return 0;
        }
    }
}
