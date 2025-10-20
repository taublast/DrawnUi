using System.Diagnostics;
using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageCodeEditor : BasePageCodeBehind, IDisposable
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

            SkiaButton btn;

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
                    BackgroundColor = Colors.Red,
                    Tag = "Container",
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaMauiEditor()
                        {
                            LockFocus=true,
                            HeightRequest = 100,
                            WidthRequest = 300,
                            VerticalOptions = LayoutOptions.Center,
                            BackgroundColor = Colors.White,
                            TextColor = Colors.Black,
                            FontSize = 14
                        },

                        new SkiaButton()
                        {
                            Text = "BTN",
                            WidthRequest = 50,
                            HeightRequest = 90,
                            BackgroundColor = Colors.DarkOliveGreen,
                            HorizontalOptions = LayoutOptions.End,
                            VerticalOptions = LayoutOptions.Center,
                        }.OnTapped(me =>
                        {
                            Trace.WriteLine("BTN TAPPED");
                        })

                    }
                }.Fill()
            };


            this.Content = Canvas;
        }
    }
}
