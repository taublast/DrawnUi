using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageCodeNodes : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.White,
                //Content = new SkiaLottie()
                //{
                //    DefaultFrame = -1,
                //    Source = @"Lottie\ok.json",
                //    WidthRequest = 100,
                //    HeightRequest = 100,
                //    HorizontalOptions = LayoutOptions.Fill,
                //}
                Content = new SkiaLayout()
                {
                    BackgroundColor = Colors.Bisque,
                    Tag = "Container",
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaLayout()
                        {
                            Tag = "LayoutWithOffset",
                            TranslationY = 100,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            BackgroundColor = Colors.Aqua,
                            UseCache = SkiaCacheType.None,
                            Children = new List<SkiaControl>()
                            {
                                new SkiaLayout()
                                {
                                    Tag = "ReproContainer",
                                    UseCache = SkiaCacheType.None,
                                    BackgroundColor = Colors.Red,
                                    HeightRequest = 100,
                                    WidthRequest = 20,
                                    Children = new List<SkiaControl>()
                                    {
                                        new SkiaImage()
                                        {
                                            Tag = "Repro",
                                            Top=20,
                                            UseCache = SkiaCacheType.Image,
                                            Source = "file://dotnet_bot.png",
                                            BackgroundColor = Colors.Firebrick,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            VerticalOptions = LayoutOptions.Fill
                                        }
                                    }
                                }
                            }
                        }
                    }
                }.Fill()
            };


            this.Content = Canvas;
        }
    }
}
