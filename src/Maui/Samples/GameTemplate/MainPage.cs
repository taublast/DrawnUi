using DrawnUi.Controls;
using DrawnUi.Views;
using System.Globalization;
using Breakout;

#if PREVIEWS
using HotPreview;
#endif

namespace Breakout.Game
{
    public partial class MainPage : BasePageReloadable
    {
        public MainPage()
        {
            BackgroundColor = Colors.Black; //iOS statusbar and bottom insets
        }

        //for navigatiion 
        public static SkiaViewSwitcher? ViewsContainer;

        Canvas Canvas;

        // This is called by constructor and .NET HotReload
        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new RescalingCanvas()
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Gestures = GesturesMode.Lock,
                RenderingMode = RenderingModeType.Accelerated,
                BackgroundColor = Colors.Black,

                Content = new SkiaLayer()
                {
                    Children =
                    {
                        //WALLPAPER
                        new SkiaImage(@"Images/back.jpg")
                        {
                            UseCache = SkiaCacheType.Image,
                            AddEffect = SkiaImageEffect.Darken,
                            Darken = 0.2
                        }.Fill(),

                        //MAIN VIEW
                        new SkiaViewSwitcher()
                        {
                            HorizontalOptions = LayoutOptions.Center,
                            WidthRequest = 360,
                            HeightRequest = 760,
                            VerticalOptions = LayoutOptions.Center,
                            SelectedIndex = 0,
                            Children =
                            {
                                //new Game.BreakoutGame(),
                            }
                        }.Assign(out ViewsContainer),
#if NEED_DEBUG
                        new SkiaLabelFps()
                        {
                            Margin = new(0, 0, 4, 24),
                            VerticalOptions = LayoutOptions.End,
                            HorizontalOptions = LayoutOptions.End,
                            Rotation = -45,
                            FontSize = 11,
                            BackgroundColor = Colors.DarkRed,
                            TextColor = Colors.White,
                            ZIndex = 110,
                        }
#endif
                    }
                }.Fill()
            };
            
            Content = new Grid() //due to maui layout specifics we are forced to use a Grid as root wrapper
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = { Canvas }
            };
        }
    }
}
