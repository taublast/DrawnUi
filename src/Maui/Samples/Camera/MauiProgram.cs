global using DrawnUi.Draw;
global using SkiaSharp;
using CameraTests.Services;
using Microsoft.Extensions.Logging;

namespace CameraTests
{
    public static class MauiProgram
    {

        //LOTTIE anims for permissions are by https://lottiefiles.com/madhu

        public static MauiApp CreateMauiApp()
        {
            //SkiaImageManager.CacheLongevitySecs = 10;
            //SkiaImageManager.LogEnabled = true;

            Super.NavBarHeight = 47;

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("NotoColorEmoji-Regular.ttf", "FontEmoji");

                    fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                    fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBol");
                    fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");

                    fonts.AddFont("DOM.TTF", "FontBrand");
                    fonts.AddFont("DOMB.TTF", "FontBrandBold");

                    fonts.AddFont("Orbitron-Regular.ttf", "FontGame"); //400
                    fonts.AddFont("Orbitron-Medium.ttf", "FontGameMedium"); //500
                    fonts.AddFont("Orbitron-SemiBold.ttf", "FontGameSemiBold"); //600
                    fonts.AddFont("Orbitron-Bold.ttf", "FontGameBold"); //700
                    fonts.AddFont("Orbitron-ExtraBold.ttf", "FontGameExtraBold"); //800
                });

            builder.UseDrawnUi(new()
            {
                UseDesktopKeyboard = true, 

                //portrait
                DesktopWindow = new()
                {
                    Height = 500,
                    Width = 800,
                }

                //landscape
                //DesktopWindow = new()
                //{
                //    Height = 500,
                //    Width = 750,
                //}
            });

            builder.Services.AddSingleton<IRealtimeTranscriptionService, OpenAiRealtimeTranscriptionService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        public static readonly string ShadersFolder = "Shaders";
    }
}
