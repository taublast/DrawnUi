 using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace DrawnUi.Controls;

 /// <summary>
 /// TODO
 /// </summary>
public partial class MauiEditorHandler : EditorHandler
{
    MauiTextView _control;

    protected override void ConnectHandler(MauiTextView platformView)
    {
        base.ConnectHandler(platformView);

        _control = platformView;

        ApplySettings();
    }

    protected override void DisconnectHandler(MauiTextView platformView)
    {
        base.DisconnectHandler(platformView);

        platformView?.Dispose();  //MAUI is not disposing PlatformView in base, avoid the leak
        _control = null;

    }

    public override void PlatformArrange(Rect frame)
    {
        base.PlatformArrange(frame);

        ApplySettings();
    }


    void ApplySettings()
    {
        if (_control != null)
        {
            _control.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 0);
        }
    }

}

