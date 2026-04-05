global using DrawnUi.Draw;
global using SkiaSharp;
using CameraTests.Services;
using Microsoft.Extensions.Logging;

namespace CameraTests
{
    public static class MauiProgram
    {

        public static readonly string ShadersFolder = "Shaders";
        public static readonly string Album = "SkiaCamera";
        public static readonly string ShaderRemoveCaption = @"Shaders\dissolve_light.sksl";

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
                    fonts.AddFont("material.ttf", "FontIcons");

                    fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                    fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBol");
                    fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
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

#if ANDROID
            //Super.MaxFps = 60;
#elif IOS
            Super.MaxFps = 30;
#endif

            return builder.Build();
        }

    }
}
