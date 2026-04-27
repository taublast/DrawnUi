using CoreGraphics;
using Foundation;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace DrawnUi.Controls;

public partial class MauiEntryHandler : EntryHandler
{
    public class NativeiEntry : MauiTextField
    {
        public NativeiEntry(CGRect frame) : base(frame)
        {
            Initialize();
        }

        public NativeiEntry()
        {
            Initialize();
        }


        void Initialize()
        {
            BorderStyle = UITextBorderStyle.None;
            BackgroundColor = UIColor.Clear;

            // EditingDidBegin += (_, _) =>
            // {
            //     TintColor = UIColor.Clear; // This removes the selection handles
            //     this.BackgroundColor = UIColor.Clear;
            //     this.Layer.BackgroundColor = UIColor.Clear.CGColor;

            // };

        }
    }

    protected override MauiTextField CreatePlatformView()
    {
        return new NativeiEntry(CGRect.Empty);
    }

    protected override void ConnectHandler(MauiTextField platformView)
    {
        base.ConnectHandler(platformView);

        // Remove border
        platformView.BorderStyle = UITextBorderStyle.None;

        // Set transparent background
        platformView.BackgroundColor = UIColor.Clear;

        // Also make the text container/background transparent
        platformView.Layer.BackgroundColor = UIColor.Clear.CGColor;

        // Optional: Remove any default shadows or borders
        platformView.Layer.BorderWidth = 0;
        platformView.Layer.ShadowOpacity = 0;
        platformView.Layer.MasksToBounds = true;

        //platformView.TintColor = UIColor.Red; // todo text cursor color!
    }
}
