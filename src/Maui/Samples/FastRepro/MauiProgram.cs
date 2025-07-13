global using DrawnUi.Draw;
global using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Sandbox
{
    public static class MauiProgram
    {
        public static string RTL = "مرحبًا بكم في عصر العناصر البصرية المصممة\r\nحيث الدقة تلتقي بالإبداع في كل تفصيل\r\nنقدم لكم تجربة مستخدم فريدة ومتطورة\r\nاستمتع بالسلاسة والأداء العالي في التصميمات\r\nنحن نبتكر لنجعل تجربتكم أكثر تميزًا وسهولة";
        public static string Multiline = "This is a single label with a multile text. The label that follows this one will have Spans defined.\r\nAnd a new line comes in. We can adjust space between paragraphs and characters. This text is aligned to Fill Words.\r\nLorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries.";

        public static string Testing = "1\r\n2\r\n3";

        public static List<SkiaPoint> PolygonStar
        {
            get
            {
                return SkiaShape.CreateStarPoints(5);
            }
        }

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
                UseDesktopKeyboard = true, //will not work with maui shell on apple!!
                DesktopWindow = new()
                {
                    Width = 550,
                    Height = 700,
                    //IsFixedSize = true //user cannot resize window
                }
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        public static readonly string ShadersFolder = "Shaders";
    }
}
