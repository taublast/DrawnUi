using DrawnUi.Controls;
using Microsoft.Maui.Handlers;
using UIKit;

namespace DrawnUi.Draw
{
    public static partial class DrawnExtensions
    {
        public static void ConfigureHandlers(IMauiHandlersCollection handlers)
        {
            if (StartupSettings != null && StartupSettings.MobileIsFullscreen.HasValue)
            {
                bool useFullScreen = StartupSettings.MobileIsFullscreen.Value;

                var useSafeArea = !useFullScreen;

                PageHandler.Mapper.AppendToMapping("Custom", (h, v) =>
                {
                    if (v is Microsoft.Maui.Controls.Page page)
                    {
                        Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.SetUseSafeArea(page, useSafeArea);
                    }
                });

                LayoutHandler.Mapper.AppendToMapping("Custom", (h, v) =>
                {
                    if (v is Layout layout)
                    {
                        layout.IgnoreSafeArea = useFullScreen;
                    }
                });
            }

            //handlers.AddHandler(typeof(DrawnView), typeof(DrawnViewHandler));
            handlers.AddHandler(typeof(DrawnUiBasePage), typeof(DrawnUiBasePageHandler));
            handlers.AddHandler(typeof(MauiEntry), typeof(MauiEntryHandler));
            handlers.AddHandler(typeof(MauiEditor), typeof(MauiEditorHandler));
            handlers.AddHandler(typeof(SkiaViewAccelerated), typeof(SKGLViewHandlerRetained));

            UIApplication.Notifications.ObserveDidReceiveMemoryWarning((s, e) =>
            {
                Super.Log("MEMORY LOW !!!");
            });

        }
    }

    namespace DrawnUi.Views
    {
    }

}
