global using DrawnUi.Controls;
using DrawnUi.Draw;
using Microsoft.Extensions.Logging;

namespace GameTemplate
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    // Optional custom fonts if present in Resources/Fonts
                    fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                    fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBold");
                });

            builder.UseDrawnUi(new()
            {
                UseDesktopKeyboard = true,
                DesktopWindow = new()
                {
                    Height = 600,
                    Width = 900,
                }
            });

            //to avoid returning many copies of same sprite bitmap for different images
            SkiaImageManager.ReuseBitmaps = true;

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

