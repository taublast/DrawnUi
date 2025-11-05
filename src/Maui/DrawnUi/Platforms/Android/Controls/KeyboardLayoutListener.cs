using Android.Util;
using Microsoft.Maui.Controls.PlatformConfiguration;

namespace DrawnUi.Controls;

public class KeyboardLayoutListener : GlobalLayoutListener<DrawnUiBasePage>
{
    private double _lastKeyboardSize = -1;

    public KeyboardLayoutListener(global::Android.Views.View view, DrawnUiBasePage control) : base(view, control)
    {
    }
     
    public override void OnGlobalLayout()
    {
        var keyboardSize = KeyboardSize(View);

        if (Control == null)
            return; //check for being disposed.. all fine

        if (keyboardSize != _lastKeyboardSize)
        {
            _lastKeyboardSize = keyboardSize;
            Control.KeyboardResized(keyboardSize);
        }
    }

    private double KeyboardSize(global::Android.Views.View rootView)
    {
        
        try
        {
            if (rootView == null)
                return 0;

            var rectangle = new Android.Graphics.Rect();

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
