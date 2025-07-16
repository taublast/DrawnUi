using Microsoft.Extensions.Logging;

namespace DrawnUI.Tutorials;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseDrawnUi(new()
            {
                DesktopWindow = new()
                {
                    Width = 370,
                    Height = 750,
                    //IsFixedSize = true
                }
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
