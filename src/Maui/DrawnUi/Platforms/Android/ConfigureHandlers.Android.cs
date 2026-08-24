using DrawnUi.Controls;

namespace DrawnUi.Draw
{
    public static partial class DrawnExtensions
    {
        public static void ConfigureHandlers(IMauiHandlersCollection handlers)
        {
#if NET10_0_OR_GREATER
            // MAUI 10 applies its own safe-area padding on Android (Layouts default to Container),
            // which would defeat MobileIsFullscreen. Mirror the iOS handler configuration.
            if (StartupSettings != null && StartupSettings.MobileIsFullscreen.HasValue)
            {
                bool useFullScreen = StartupSettings.MobileIsFullscreen.Value;

                Microsoft.Maui.Handlers.PageHandler.Mapper.AppendToMapping("Custom", (h, v) =>
                {
                    if (v is Microsoft.Maui.Controls.ContentPage page && useFullScreen)
                    {
                        page.SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.None;
                    }
                });

                Microsoft.Maui.Handlers.LayoutHandler.Mapper.AppendToMapping("Custom", (h, v) =>
                {
                    if (v is Layout layout && useFullScreen)
                    {
                        layout.SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.None;
                    }
                });

                Microsoft.Maui.Handlers.ScrollViewHandler.Mapper.AppendToMapping("Custom", (h, v) =>
                {
                    if (v is ScrollView scroll && useFullScreen)
                    {
                        scroll.SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.None;
                    }
                });
            }
#endif
            handlers.AddHandler(typeof(SkiaViewAccelerated), typeof(SKGLViewHandlerRetained));

            handlers.AddHandler(typeof(DrawnUiBasePage), typeof(DrawnUiBasePageHandler));
            handlers.AddHandler(typeof(MauiEntry), typeof(MauiEntryHandler));
            handlers.AddHandler(typeof(MauiEditor), typeof(MauiEditorHandler));
        }
    }
}
