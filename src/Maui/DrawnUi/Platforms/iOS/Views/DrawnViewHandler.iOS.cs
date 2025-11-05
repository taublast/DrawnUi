using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Handlers;
using ContentView = Microsoft.Maui.Platform.ContentView;

namespace DrawnUi.Controls;

public partial class DrawnViewHandler : ContentViewHandler
{
    public DrawnViewHandler() : base()
    {
    }


    protected override ContentView CreatePlatformView()
    {
        //return base.CreatePlatformView();
        var platformView = new VisibilityAwarePlatformView(VirtualView as DrawnView);

        return platformView;
    }

}
