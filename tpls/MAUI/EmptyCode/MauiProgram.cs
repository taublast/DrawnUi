using Microsoft.Extensions.Logging;

namespace EmptyCode;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseDrawnUi(new DrawnUiStartupSettings
            {
                // Listen to hardware keys on desktop (Windows / Mac Catalyst).
                UseDesktopKeyboard = true,

                // Desktop runs in a phone-sized window so the layout matches mobile.
                // Drop this to get a normal resizable desktop window.
                DesktopWindow = new()
                {
                    Width = 375,
                    Height = 750,
                    //IsFixedSize = true
                }
            })
            .ConfigureFonts(fonts =>
            {
                // Same aliases the DrawnUI Fiddle registers, so exported snippets
                // find the fonts they were written against.
                fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
